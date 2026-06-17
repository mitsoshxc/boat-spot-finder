using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Settings;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;
using BoatSpotFinder.Web.Controllers;
using BoatSpotFinder.Web.Models;
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
        return BuildController(context, userManager, Substitute.For<IMarinaSearchService>());
    }

    private static AdminController BuildController(
        BoatSpotFinder.Infrastructure.Data.AppDbContext context,
        UserManager<ApplicationUser> userManager,
        IMarinaSearchService searchService)
    {
        return new AdminController(
            userManager,
            new BookingRepository(context),
            new MarinaRepository(context),
            new MarinaAdminRepository(context),
            new SpotRepository(context),
            new InvitationRepository(context),
            new AdminSettingsRepository(context),
            searchService,
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

    // ── SearchMarinas ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchMarinas_BlankQuery_ReturnsAllMarinasIncludingInactive()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var active = await SeedMarinaAsync(db.Context, "Active Marina");
        var inactive = new Marina("Inactive Marina", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        inactive.Deactivate();
        await db.Context.Marinas.AddAsync(inactive);
        await db.Context.SaveChangesAsync();

        var controller = BuildController(db.Context, BuildUserManager());
        SetControllerContext(controller);

        var result = await controller.SearchMarinas(null);

        var partial = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<AdminMarinaListItemViewModel>>(partial.Model);
        var ids = model.Select(m => m.Id).ToList();
        Assert.Contains(active.Id, ids);
        Assert.Contains(inactive.Id, ids);
    }

    [Fact]
    public async Task SearchMarinas_EsOff_FiltersByNameOrRegion_IncludingInactive()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var activePorto = new Marina("Porto Vell", "Desc", "Addr", "Mediterranean", "000", 0, 0, 50m);
        await db.Context.Marinas.AddAsync(activePorto);
        await db.Context.SaveChangesAsync();

        var inactivePorto = new Marina("Old Harbour", "Desc", "Addr", "Porto Region", "000", 0, 0, 50m);
        inactivePorto.Deactivate();
        await db.Context.Marinas.AddAsync(inactivePorto);
        await db.Context.SaveChangesAsync();

        var noMatch = new Marina("Sunny Bay", "Desc", "Addr", "Atlantic", "000", 0, 0, 50m);
        await db.Context.Marinas.AddAsync(noMatch);
        await db.Context.SaveChangesAsync();

        var searchService = Substitute.For<IMarinaSearchService>();
        searchService.SearchAsync(Arg.Any<string>()).Returns(Task.FromResult<IEnumerable<Guid>?>(null));

        var controller = BuildController(db.Context, BuildUserManager(), searchService);
        SetControllerContext(controller);

        var result = await controller.SearchMarinas("porto");

        var partial = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<AdminMarinaListItemViewModel>>(partial.Model).ToList();
        Assert.Contains(model, m => m.Id == activePorto.Id);
        Assert.Contains(model, m => m.Id == inactivePorto.Id);
        Assert.DoesNotContain(model, m => m.Id == noMatch.Id);
    }

    [Fact]
    public async Task SearchMarinas_EsOn_ReturnsActiveEsMatchesPlusInactiveSqlMatches()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var esMatchedActive = new Marina("Porto Vell", "Desc", "Addr", "Mediterranean", "000", 0, 0, 50m);
        await db.Context.Marinas.AddAsync(esMatchedActive);
        await db.Context.SaveChangesAsync();

        var inactiveSqlMatch = new Marina("Old Porto", "Desc", "Addr", "Porto Region", "000", 0, 0, 50m);
        inactiveSqlMatch.Deactivate();
        await db.Context.Marinas.AddAsync(inactiveSqlMatch);
        await db.Context.SaveChangesAsync();

        var activeNotReturned = new Marina("Sunny Bay", "Desc", "Addr", "Atlantic", "000", 0, 0, 50m);
        await db.Context.Marinas.AddAsync(activeNotReturned);
        await db.Context.SaveChangesAsync();

        var searchService = Substitute.For<IMarinaSearchService>();
        searchService.SearchAsync(Arg.Any<string>())
            .Returns(Task.FromResult<IEnumerable<Guid>?>(new[] { esMatchedActive.Id }));

        var controller = BuildController(db.Context, BuildUserManager(), searchService);
        SetControllerContext(controller);

        var result = await controller.SearchMarinas("porto");

        var partial = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<AdminMarinaListItemViewModel>>(partial.Model).ToList();
        Assert.Contains(model, m => m.Id == esMatchedActive.Id);
        Assert.Contains(model, m => m.Id == inactiveSqlMatch.Id);
        Assert.DoesNotContain(model, m => m.Id == activeNotReturned.Id);
    }
}
