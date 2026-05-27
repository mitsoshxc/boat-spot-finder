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
}
