using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Services;
using BoatSpotFinder.Core.Settings;
using BoatSpotFinder.Infrastructure.Data;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BoatSpotFinder.Tests.Services;

public class BookingServiceTests
{
    private static BookingService BuildService(
        AppDbContext context,
        UserManager<ApplicationUser>? userManager = null,
        IEmailSender? emailSender = null)
    {
        userManager ??= BuildUserManager();
        emailSender ??= Substitute.For<IEmailSender>();

        return new BookingService(
            new BookingRepository(context),
            new VesselRepository(context),
            new SpotRepository(context),
            new MarinaRepository(context),
            new MarinaAdminRepository(context),
            new SpotSeasonalRuleRepository(context),
            new AdminSettingsRepository(context),
            emailSender,
            userManager,
            Options.Create(new AppSettings { BaseUrl = "http://localhost" }),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<BookingService>>());
    }

    private static UserManager<ApplicationUser> BuildUserManager(ApplicationUser? user = null)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var mgr = Substitute.For<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);
        var stubUser = user ?? new ApplicationUser { Id = "user-1", Email = "test@example.com", UserName = "test@example.com" };
        mgr.FindByIdAsync(Arg.Any<string>()).Returns(Task.FromResult<ApplicationUser?>(stubUser));
        mgr.IsInRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(Task.FromResult(false));
        return mgr;
    }

    private static async Task SeedUserAsync(AppDbContext context, string userId, string email)
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

    private static async Task<(Marina marina, Spot spot, Vessel vessel, string boatOwnerId)> SeedBasicAsync(
        AppDbContext context,
        decimal spotPricePerDay = 50m,
        decimal marinaPricePerDay = 30m,
        int defaultMinBookingDays = 1,
        double spotLength = 20,
        double spotWidth = 10,
        double spotDepth = 5,
        double vesselLength = 10,
        double vesselWidth = 5,
        double vesselDepth = 3,
        VesselType allowedVesselTypes = VesselType.None,
        VesselType vesselType = VesselType.SailBoat)
    {
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(context, boatOwnerId, "owner@test.com");

        var marina = new Marina("Test Marina", "Desc", "Addr", "Region", "000", 0, 0, marinaPricePerDay);
        await context.Marinas.AddAsync(marina);

        var spot = new Spot("Spot A", "Desc", spotLength, spotWidth, spotDepth, defaultMinBookingDays, allowedVesselTypes, marina.Id, spotPricePerDay);
        spot.Activate();
        await context.Spots.AddAsync(spot);

        var vessel = new Vessel("My Boat", vesselType, vesselLength, vesselWidth, vesselDepth, boatOwnerId);
        await context.Vessels.AddAsync(vessel);

        await context.SaveChangesAsync();
        return (marina, spot, vessel, boatOwnerId);
    }

    [Fact]
    public async Task CreateAsync_OverlappingDates_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var existing = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        existing.Confirm();
        await db.Context.Bookings.AddAsync(existing);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 6, 5), new DateOnly(2027, 6, 15));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("not available", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_AdjacentDates_Allowed()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var existing = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        existing.Confirm();
        await db.Context.Bookings.AddAsync(existing);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 6, 10), new DateOnly(2027, 6, 15));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_PriceCalculation_UsesSpotPrice()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context, spotPricePerDay: 50m);

        var service = BuildService(db.Context);
        await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 6));

        var booking = db.Context.Bookings.Single(b => b.SpotId == spot.Id);
        Assert.Equal(250m, booking.TotalPrice);
    }

    [Fact]
    public async Task CreateAsync_SeasonalRule_UseRulePrice()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context, spotPricePerDay: 50m);

        var rule = new SpotSeasonalRule("Summer", new DateOnly(2027, 6, 1), new DateOnly(2027, 8, 31), 80m, 1, spot.Id);
        await db.Context.SpotSeasonalRules.AddAsync(rule);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 6, 5), new DateOnly(2027, 6, 10));

        var booking = db.Context.Bookings.Single(b => b.SpotId == spot.Id);
        Assert.Equal(400m, booking.TotalPrice);
    }

    [Fact]
    public async Task CreateAsync_NoSpotPrice_FallsBackToMarinaDefault()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");

        var marina = new Marina("Test Marina", "Desc", "Addr", "Region", "000", 0, 0, 40m);
        await db.Context.Marinas.AddAsync(marina);

        var spot = new Spot("Spot A", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, null);
        spot.Activate();
        await db.Context.Spots.AddAsync(spot);

        var vessel = new Vessel("My Boat", VesselType.SailBoat, 10, 5, 3, boatOwnerId);
        await db.Context.Vessels.AddAsync(vessel);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 4));

        var booking = db.Context.Bookings.Single(b => b.SpotId == spot.Id);
        Assert.Equal(120m, booking.TotalPrice);
    }

    [Fact]
    public async Task CreateAsync_VesselTooLarge_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context,
            spotLength: 10, vesselLength: 15);

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 5));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("dimensions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_VesselExactFit_Allowed()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context,
            spotLength: 10, spotWidth: 5, spotDepth: 3,
            vesselLength: 10, vesselWidth: 5, vesselDepth: 3);

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 5));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_VesselTypeNotAllowed_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context,
            allowedVesselTypes: VesselType.SailBoat,
            vesselType: VesselType.MotorBoat);

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 5));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_VesselTypeAllowed_FlagsMatch()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context,
            allowedVesselTypes: VesselType.SailBoat | VesselType.MotorBoat,
            vesselType: VesselType.MotorBoat);

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 5));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_AllowedVesselTypesNone_AllowsAny()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context,
            allowedVesselTypes: VesselType.None,
            vesselType: VesselType.Yacht);

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 5));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_BelowMinBookingDays_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context, defaultMinBookingDays: 3);

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 3));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("minimum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAsync_ExactMinBookingDays_Allowed()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context, defaultMinBookingDays: 3);

        var service = BuildService(db.Context);
        var result = await service.CreateAsync(spot.Id, vessel.Id, boatOwnerId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 4));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CancelAsync_BoatOwnerBeforeStart_Allowed()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            TotalPrice = 250m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CancelAsync(booking.Id, boatOwnerId);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CancelAsync_BoatOwnerOnStartDate_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            TotalPrice = 250m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CancelAsync(booking.Id, boatOwnerId);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CancelAsync_PlaceOwnerBeforeStart_Allowed()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string placeOwnerId = "po-1";
        await SeedUserAsync(db.Context, placeOwnerId, "po@test.com");
        await SeedUserAsync(db.Context, "admin-1", "admin1@test.com");
        await db.Context.SaveChangesAsync();

        var marinaAdmin = new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = placeOwnerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "admin-1"
        };
        await db.Context.MarinaAdmins.AddAsync(marinaAdmin);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            TotalPrice = 250m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CancelAsync(booking.Id, placeOwnerId);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CancelAsync_PlaceOwnerOnStartDate_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string placeOwnerId = "po-1";
        await SeedUserAsync(db.Context, placeOwnerId, "po@test.com");
        await SeedUserAsync(db.Context, "admin-1", "admin1@test.com");
        await db.Context.SaveChangesAsync();

        var marinaAdmin = new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = placeOwnerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "admin-1"
        };
        await db.Context.MarinaAdmins.AddAsync(marinaAdmin);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            TotalPrice = 250m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CancelAsync(booking.Id, placeOwnerId);

        Assert.False(result.Success);
    }

    // ──────────────────────────────────────────────────────────────────────
    // ConfirmAsync
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmAsync_PerformerNotMarinaAdmin_Forbidden()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 8, 1),
            EndDate = new DateOnly(2027, 8, 7),
            TotalPrice = 300m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.ConfirmAsync(booking.Id, "non-admin-user");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Forbidden", StringComparison.OrdinalIgnoreCase));
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Pending, reloaded!.Status);
    }

    [Fact]
    public async Task ConfirmAsync_MarinaAdmin_ConfirmsAndEmailsBoatOwner()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string performerId = "performer-1";
        await SeedUserAsync(db.Context, performerId, "performer@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = performerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "inviter-1"
        });

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 8, 1),
            EndDate = new DateOnly(2027, 8, 7),
            TotalPrice = 300m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var emailSender = Substitute.For<IEmailSender>();
        var service = BuildService(db.Context, emailSender: emailSender);
        var result = await service.ConfirmAsync(booking.Id, performerId);

        Assert.True(result.Success);
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, reloaded!.Status);
        await emailSender.Received().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ConfirmAsync_NonPendingBooking_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string performerId = "performer-1";
        await SeedUserAsync(db.Context, performerId, "performer@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = performerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "inviter-1"
        });

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 8, 1),
            EndDate = new DateOnly(2027, 8, 7),
            TotalPrice = 300m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.ConfirmAsync(booking.Id, performerId);

        Assert.False(result.Success);
    }

    // ──────────────────────────────────────────────────────────────────────
    // RejectAsync
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RejectAsync_PerformerNotMarinaAdmin_Forbidden()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 8, 1),
            EndDate = new DateOnly(2027, 8, 7),
            TotalPrice = 300m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.RejectAsync(booking.Id, "non-admin-user");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RejectAsync_MarinaAdmin_CancelsAndEmailsBoatOwner()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string performerId = "performer-1";
        await SeedUserAsync(db.Context, performerId, "performer@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = performerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "inviter-1"
        });

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 8, 1),
            EndDate = new DateOnly(2027, 8, 7),
            TotalPrice = 300m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var emailSender = Substitute.For<IEmailSender>();
        var service = BuildService(db.Context, emailSender: emailSender);
        var result = await service.RejectAsync(booking.Id, performerId);

        Assert.True(result.Success);
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Cancelled, reloaded!.Status);
        await emailSender.Received().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ──────────────────────────────────────────────────────────────────────
    // CancelAsync — additional cases
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_UnrelatedUser_Forbidden()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            TotalPrice = 250m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CancelAsync(booking.Id, "unrelated-user-99");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CancelAsync_AdminOnStartDate_Allowed()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string adminUserId = "admin-caller-1";
        await SeedUserAsync(db.Context, adminUserId, "admincaller@test.com");
        await db.Context.SaveChangesAsync();

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            TotalPrice = 250m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var adminUser = new ApplicationUser { Id = adminUserId, Email = "admincaller@test.com", UserName = "admincaller@test.com" };
        var userManager = BuildAdminUserManager(adminUser);
        var service = BuildService(db.Context, userManager: userManager);
        var result = await service.CancelAsync(booking.Id, adminUserId);

        Assert.True(result.Success);
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Cancelled, reloaded!.Status);
    }

    [Fact]
    public async Task CancelAsync_AdminCompletedBooking_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string adminUserId = "admin-caller-1";
        await SeedUserAsync(db.Context, adminUserId, "admincaller@test.com");
        await db.Context.SaveChangesAsync();

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 1, 7),
            TotalPrice = 250m
        };
        booking.Complete();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var adminUser = new ApplicationUser { Id = adminUserId, Email = "admincaller@test.com", UserName = "admincaller@test.com" };
        var userManager = BuildAdminUserManager(adminUser);
        var service = BuildService(db.Context, userManager: userManager);
        var result = await service.CancelAsync(booking.Id, adminUserId);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelledBooking_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            TotalPrice = 250m
        };
        booking.Cancel();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CancelAsync(booking.Id, boatOwnerId);

        Assert.False(result.Success);
    }

    // ──────────────────────────────────────────────────────────────────────
    // AutoActionAsync
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoActionAsync_AutoApprove_ConfirmsTimedOutPending()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var settings = await new AdminSettingsRepository(db.Context).GetAsync();
        settings.UpdateSettings(AutoActionType.AutoApprove, 0);
        await new AdminSettingsRepository(db.Context).UpdateAsync(settings);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 9, 1),
            EndDate = new DateOnly(2027, 9, 7),
            TotalPrice = 300m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.AutoActionAsync();

        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, reloaded!.Status);
    }

    [Fact]
    public async Task AutoActionAsync_AutoReject_CancelsTimedOutPending()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var settings = await new AdminSettingsRepository(db.Context).GetAsync();
        settings.UpdateSettings(AutoActionType.AutoReject, 0);
        await new AdminSettingsRepository(db.Context).UpdateAsync(settings);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 9, 1),
            EndDate = new DateOnly(2027, 9, 7),
            TotalPrice = 300m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.AutoActionAsync();

        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Cancelled, reloaded!.Status);
    }

    [Fact]
    public async Task AutoActionAsync_WithinTimeout_LeavesPending()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var settings = await new AdminSettingsRepository(db.Context).GetAsync();
        settings.UpdateSettings(AutoActionType.AutoApprove, 24);
        await new AdminSettingsRepository(db.Context).UpdateAsync(settings);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 9, 1),
            EndDate = new DateOnly(2027, 9, 7),
            TotalPrice = 300m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.AutoActionAsync();

        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Pending, reloaded!.Status);
    }

    [Fact]
    public async Task AutoActionAsync_IgnoresNonPendingBookings()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var settings = await new AdminSettingsRepository(db.Context).GetAsync();
        settings.UpdateSettings(AutoActionType.AutoReject, 0);
        await new AdminSettingsRepository(db.Context).UpdateAsync(settings);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 9, 1),
            EndDate = new DateOnly(2027, 9, 7),
            TotalPrice = 300m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.AutoActionAsync();

        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, reloaded!.Status);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CompleteOverdueAsync
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteOverdueAsync_OverdueConfirmed_TransitionsToCompleted()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var twoDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = twoDaysAgo,
            EndDate = yesterday,
            TotalPrice = 100m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.CompleteOverdueAsync();

        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Completed, reloaded!.Status);
    }

    [Fact]
    public async Task CompleteOverdueAsync_FutureConfirmed_Unchanged()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)),
            TotalPrice = 250m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.CompleteOverdueAsync();

        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, reloaded!.Status);
    }

    [Fact]
    public async Task CompleteOverdueAsync_NonConfirmed_Unchanged()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var twoDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = twoDaysAgo,
            EndDate = yesterday,
            TotalPrice = 100m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        await service.CompleteOverdueAsync();

        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Pending, reloaded!.Status);
    }

    [Fact]
    public async Task CompleteOverdueAsync_SendsReviewInviteEmails()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string marinaAdminId = "ma-1";
        const string inviterId = "inviter-1";
        await SeedUserAsync(db.Context, marinaAdminId, "marinaadmin@test.com");
        await SeedUserAsync(db.Context, inviterId, "inviter@test.com");
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = marinaAdminId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = inviterId
        });

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var twoDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = twoDaysAgo,
            EndDate = yesterday,
            TotalPrice = 100m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var boatOwnerUser = new ApplicationUser { Id = boatOwnerId, Email = "owner@test.com", UserName = "owner@test.com" };
        var marinaAdminUser = new ApplicationUser { Id = marinaAdminId, Email = "marinaadmin@test.com", UserName = "marinaadmin@test.com" };

        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);
        userManager.FindByIdAsync(boatOwnerId).Returns(Task.FromResult<ApplicationUser?>(boatOwnerUser));
        userManager.FindByIdAsync(marinaAdminId).Returns(Task.FromResult<ApplicationUser?>(marinaAdminUser));
        userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(Task.FromResult(false));

        var emailSender = Substitute.For<IEmailSender>();
        var service = BuildService(db.Context, userManager: userManager, emailSender: emailSender);
        await service.CompleteOverdueAsync();

        await emailSender.Received(2).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ──────────────────────────────────────────────────────────────────────
    // PreviewPriceAsync
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PreviewPriceAsync_ReturnsResolvedPricingWithoutPersisting()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, _) = await SeedBasicAsync(db.Context, spotPricePerDay: 50m);

        var service = BuildService(db.Context);
        var preview = await service.PreviewPriceAsync(spot.Id, vessel.Id, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 6));

        Assert.Equal(50m, preview.PricePerDay);
        Assert.Equal(250m, preview.TotalPrice);
        Assert.Empty(db.Context.Bookings);
    }

    [Fact]
    public async Task PreviewPriceAsync_SeasonalRule_UsesRulePrice()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, _) = await SeedBasicAsync(db.Context, spotPricePerDay: 50m);

        var rule = new SpotSeasonalRule("Summer", new DateOnly(2027, 6, 1), new DateOnly(2027, 8, 31), 100m, 1, spot.Id);
        await db.Context.SpotSeasonalRules.AddAsync(rule);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var preview = await service.PreviewPriceAsync(spot.Id, vessel.Id, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 6));

        Assert.Equal(100m, preview.PricePerDay);
        Assert.Equal(500m, preview.TotalPrice);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DismissAsync
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DismissAsync_BookingNotFound_Fails()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var service = BuildService(db.Context);
        var result = await service.DismissAsync(Guid.NewGuid(), "owner-1");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Booking not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DismissAsync_WrongUser_Forbidden()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 1, 7),
            TotalPrice = 300m
        };
        booking.Cancel();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.DismissAsync(booking.Id, "other-user-99");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DismissAsync_ActiveUpcomingConfirmed_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var futureEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var futureStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = futureStart,
            EndDate = futureEnd,
            TotalPrice = 300m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.DismissAsync(booking.Id, boatOwnerId);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("past or cancelled", StringComparison.OrdinalIgnoreCase));
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.False(reloaded!.DismissedByOwner);
    }

    [Fact]
    public async Task DismissAsync_CancelledBooking_SucceedsAndSetsDismissed()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2025, 3, 1),
            EndDate = new DateOnly(2025, 3, 7),
            TotalPrice = 300m
        };
        booking.Cancel();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.DismissAsync(booking.Id, boatOwnerId);

        Assert.True(result.Success);
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.True(reloaded!.DismissedByOwner);
    }

    [Fact]
    public async Task DismissAsync_CompletedBooking_Succeeds()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2025, 2, 1),
            EndDate = new DateOnly(2025, 2, 7),
            TotalPrice = 300m
        };
        booking.Complete();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.DismissAsync(booking.Id, boatOwnerId);

        Assert.True(result.Success);
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.True(reloaded!.DismissedByOwner);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DismissByMarinaAsync
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DismissByMarinaAsync_BookingNotFound_Fails()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var service = BuildService(db.Context);
        var result = await service.DismissByMarinaAsync(Guid.NewGuid(), "po-1");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Booking not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DismissByMarinaAsync_UserNotMarinaAdmin_Forbidden()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 1, 7),
            TotalPrice = 300m
        };
        booking.Cancel();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.DismissByMarinaAsync(booking.Id, "not-an-admin-99");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DismissByMarinaAsync_ActiveUpcomingConfirmed_Rejected()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string placeOwnerId = "po-1";
        await SeedUserAsync(db.Context, placeOwnerId, "po@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = placeOwnerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "inviter-1"
        });

        var futureStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var futureEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = futureStart,
            EndDate = futureEnd,
            TotalPrice = 300m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.DismissByMarinaAsync(booking.Id, placeOwnerId);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("past or cancelled", StringComparison.OrdinalIgnoreCase));
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.False(reloaded!.DismissedByMarina);
    }

    [Fact]
    public async Task DismissByMarinaAsync_CancelledBooking_SucceedsAndSetsDismissed()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string placeOwnerId = "po-1";
        await SeedUserAsync(db.Context, placeOwnerId, "po@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = placeOwnerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "inviter-1"
        });

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2025, 3, 1),
            EndDate = new DateOnly(2025, 3, 7),
            TotalPrice = 300m
        };
        booking.Cancel();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.DismissByMarinaAsync(booking.Id, placeOwnerId);

        Assert.True(result.Success);
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.True(reloaded!.DismissedByMarina);
    }

    [Fact]
    public async Task DismissByMarinaAsync_CompletedBooking_Succeeds()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot, vessel, boatOwnerId) = await SeedBasicAsync(db.Context);

        const string placeOwnerId = "po-1";
        await SeedUserAsync(db.Context, placeOwnerId, "po@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = placeOwnerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "inviter-1"
        });

        var booking = new Booking
        {
            SpotId = spot.Id,
            VesselId = vessel.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2025, 2, 1),
            EndDate = new DateOnly(2025, 2, 7),
            TotalPrice = 300m
        };
        booking.Complete();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.DismissByMarinaAsync(booking.Id, placeOwnerId);

        Assert.True(result.Success);
        var reloaded = await db.Context.Bookings.FindAsync(booking.Id);
        Assert.True(reloaded!.DismissedByMarina);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static UserManager<ApplicationUser> BuildAdminUserManager(ApplicationUser adminUser)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var mgr = Substitute.For<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);
        mgr.FindByIdAsync(adminUser.Id).Returns(Task.FromResult<ApplicationUser?>(adminUser));
        mgr.FindByIdAsync(Arg.Is<string>(id => id != adminUser.Id)).Returns(Task.FromResult<ApplicationUser?>(null));
        mgr.IsInRoleAsync(Arg.Any<ApplicationUser>(), "Admin").Returns(Task.FromResult(true));
        mgr.IsInRoleAsync(Arg.Any<ApplicationUser>(), Arg.Is<string>(r => r != "Admin")).Returns(Task.FromResult(false));
        return mgr;
    }
}
