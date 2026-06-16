using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Settings;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;
using BoatSpotFinder.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;

namespace BoatSpotFinder.Tests.Controllers;

public class AdminControllerTests
{
    private static async Task SeedUserAsync(
        BoatSpotFinder.Infrastructure.Data.AppDbContext context,
        string userId,
        string email)
    {
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        await context.Users.AddAsync(user);
    }

    private static async Task<Marina> SeedMarinaAsync(
        BoatSpotFinder.Infrastructure.Data.AppDbContext context,
        string name = "Test Marina")
    {
        var marina = new Marina(name, "Desc", "Addr", "Region", "000", 0, 0, 50m);
        await context.Marinas.AddAsync(marina);
        await context.SaveChangesAsync();
        return marina;
    }

    private static UserManager<ApplicationUser> BuildUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static AdminController BuildController(
        BoatSpotFinder.Infrastructure.Data.AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        return new AdminController(
            userManager,
            new BookingRepository(context),
            new MarinaRepository(context),
            new MarinaAdminRepository(context),
            new SpotRepository(context),
            new InvitationRepository(context),
            new AdminSettingsRepository(context),
            Substitute.For<IMarinaSearchService>(),
            Substitute.For<IEmailSender>(),
            Substitute.For<IBookingService>(),
            Options.Create(new AppSettings()),
            Substitute.For<IAuditLogger>());
    }

    private static void SetControllerContext(AdminController controller)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "acting-admin"),
                new Claim(ClaimTypes.Name, "admin@boatspotfinder.com")
            }, "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
    }

    private static MarinaAdmin MakeMarinaAdmin(Guid marinaId, string userId, string invitedById) =>
        new()
        {
            MarinaId = marinaId,
            UserId = userId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = invitedById
        };

    // ── RevokeAdmin ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAdmin_LastActiveMembership_StripsPlaceOwnerRole()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "target-user", "target@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        var marina = await SeedMarinaAsync(db.Context);

        await db.Context.MarinaAdmins.AddAsync(MakeMarinaAdmin(marina.Id, "target-user", "inviter-1"));
        await db.Context.SaveChangesAsync();

        var targetUser = new ApplicationUser { Id = "target-user", Email = "target@test.com", UserName = "target@test.com" };
        var userManager = BuildUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns(Task.FromResult<ApplicationUser?>(targetUser));
        userManager.RemoveFromRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        var controller = BuildController(db.Context, userManager);
        SetControllerContext(controller);

        var result = await controller.RevokeAdmin(marina.Id, "target-user");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MarinaAdmins", redirect.ActionName);

        var repo = new MarinaAdminRepository(db.Context);
        Assert.False(await repo.ExistsAsync(marina.Id, "target-user"));

        await userManager.Received(1).RemoveFromRoleAsync(Arg.Any<ApplicationUser>(), "PlaceOwner");
    }

    [Fact]
    public async Task RevokeAdmin_OtherActiveMembershipRemains_KeepsRole()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "target-user", "target@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        var marinaA = await SeedMarinaAsync(db.Context, "Marina A");
        var marinaB = await SeedMarinaAsync(db.Context, "Marina B");

        await db.Context.MarinaAdmins.AddRangeAsync(
            MakeMarinaAdmin(marinaA.Id, "target-user", "inviter-1"),
            MakeMarinaAdmin(marinaB.Id, "target-user", "inviter-1"));
        await db.Context.SaveChangesAsync();

        var targetUser = new ApplicationUser { Id = "target-user", Email = "target@test.com", UserName = "target@test.com" };
        var userManager = BuildUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns(Task.FromResult<ApplicationUser?>(targetUser));
        userManager.RemoveFromRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        var controller = BuildController(db.Context, userManager);
        SetControllerContext(controller);

        var result = await controller.RevokeAdmin(marinaA.Id, "target-user");

        Assert.IsType<RedirectToActionResult>(result);

        var repo = new MarinaAdminRepository(db.Context);
        Assert.False(await repo.ExistsAsync(marinaA.Id, "target-user"));
        Assert.True(await repo.ExistsAsync(marinaB.Id, "target-user"));

        await userManager.DidNotReceive().RemoveFromRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RevokeAdmin_RecordNotFound_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var marina = await SeedMarinaAsync(db.Context);

        var userManager = BuildUserManager();
        var controller = BuildController(db.Context, userManager);
        SetControllerContext(controller);

        var result = await controller.RevokeAdmin(marina.Id, "nonexistent-user");

        Assert.IsType<NotFoundResult>(result);
    }

    // ── ReEnableAdmin ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ReEnableAdmin_UserNotInRole_AddsPlaceOwnerRole()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "target-user", "target@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        var marina = await SeedMarinaAsync(db.Context);

        var revokedAdmin = MakeMarinaAdmin(marina.Id, "target-user", "inviter-1");
        revokedAdmin.Revoke();
        await db.Context.MarinaAdmins.AddAsync(revokedAdmin);
        await db.Context.SaveChangesAsync();

        var targetUser = new ApplicationUser { Id = "target-user", Email = "target@test.com", UserName = "target@test.com" };
        var userManager = BuildUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns(Task.FromResult<ApplicationUser?>(targetUser));
        userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(Task.FromResult(false));
        userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        var controller = BuildController(db.Context, userManager);
        SetControllerContext(controller);

        var result = await controller.ReEnableAdmin(marina.Id, "target-user");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MarinaAdmins", redirect.ActionName);

        var repo = new MarinaAdminRepository(db.Context);
        Assert.True(await repo.ExistsAsync(marina.Id, "target-user"));

        await userManager.Received(1).AddToRoleAsync(Arg.Any<ApplicationUser>(), "PlaceOwner");
    }

    [Fact]
    public async Task ReEnableAdmin_UserAlreadyInRole_DoesNotAddAgain()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "target-user", "target@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        var marina = await SeedMarinaAsync(db.Context);

        var revokedAdmin = MakeMarinaAdmin(marina.Id, "target-user", "inviter-1");
        revokedAdmin.Revoke();
        await db.Context.MarinaAdmins.AddAsync(revokedAdmin);
        await db.Context.SaveChangesAsync();

        var targetUser = new ApplicationUser { Id = "target-user", Email = "target@test.com", UserName = "target@test.com" };
        var userManager = BuildUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns(Task.FromResult<ApplicationUser?>(targetUser));
        userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(Task.FromResult(true));
        userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);

        var controller = BuildController(db.Context, userManager);
        SetControllerContext(controller);

        var result = await controller.ReEnableAdmin(marina.Id, "target-user");

        Assert.IsType<RedirectToActionResult>(result);

        var repo = new MarinaAdminRepository(db.Context);
        Assert.True(await repo.ExistsAsync(marina.Id, "target-user"));

        await userManager.DidNotReceive().AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReEnableAdmin_RecordNotFound_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var marina = await SeedMarinaAsync(db.Context);

        var userManager = BuildUserManager();
        var controller = BuildController(db.Context, userManager);
        SetControllerContext(controller);

        var result = await controller.ReEnableAdmin(marina.Id, "nonexistent-user");

        Assert.IsType<NotFoundResult>(result);
    }
}
