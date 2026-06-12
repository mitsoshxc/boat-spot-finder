using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;

namespace BoatSpotFinder.Tests.Repositories;

public class BookingRepositoryTests
{
    private static async Task SeedUserAsync(BoatSpotFinder.Infrastructure.Data.AppDbContext context, string userId, string email)
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

    private static async Task<(Marina marina, Spot spot)> SeedMarinaAndSpotAsync(
        BoatSpotFinder.Infrastructure.Data.AppDbContext context)
    {
        var marina = new Marina("Test Marina", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        await context.Marinas.AddAsync(marina);
        await context.SaveChangesAsync();

        var spot = new Spot("Berth 1", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);
        spot.Activate();
        await context.Spots.AddAsync(spot);
        await context.SaveChangesAsync();

        return (marina, spot);
    }

    [Fact]
    public async Task IsSpotAvailableAsync_OverlappingConfirmed_False()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await db.Context.SaveChangesAsync();

        var existing = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = "owner-1",
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        existing.Confirm();
        await db.Context.Bookings.AddAsync(existing);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var available = await repo.IsSpotAvailableAsync(spot.Id, new DateOnly(2027, 6, 5), new DateOnly(2027, 6, 15), null);

        Assert.False(available);
    }

    [Fact]
    public async Task IsSpotAvailableAsync_AdjacentDates_True()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await db.Context.SaveChangesAsync();

        var existing = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = "owner-1",
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        existing.Confirm();
        await db.Context.Bookings.AddAsync(existing);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var available = await repo.IsSpotAvailableAsync(spot.Id, new DateOnly(2027, 6, 10), new DateOnly(2027, 6, 15), null);

        Assert.True(available);
    }

    [Fact]
    public async Task IsSpotAvailableAsync_CancelledBookingIgnored_True()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await db.Context.SaveChangesAsync();

        var existing = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = "owner-1",
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        existing.Cancel();
        await db.Context.Bookings.AddAsync(existing);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var available = await repo.IsSpotAvailableAsync(spot.Id, new DateOnly(2027, 6, 5), new DateOnly(2027, 6, 15), null);

        Assert.True(available);
    }

    [Fact]
    public async Task IsSpotAvailableAsync_ExcludeBookingId_IgnoresSelf_True()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await db.Context.SaveChangesAsync();

        var existing = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = "owner-1",
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        existing.Confirm();
        await db.Context.Bookings.AddAsync(existing);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var available = await repo.IsSpotAvailableAsync(spot.Id, new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 10), existing.Id);

        Assert.True(available);
    }

    [Fact]
    public async Task GetByMarinaOwnerIdAsync_ReturnsBookingsForMarinasUserAdmins()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await SeedUserAsync(db.Context, "admin-user-1", "admin1@test.com");
        await SeedUserAsync(db.Context, "inviter-1", "inviter@test.com");
        await db.Context.SaveChangesAsync();

        var marinaA = new Marina("Marina A", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        var marinaB = new Marina("Marina B", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        await db.Context.Marinas.AddRangeAsync(marinaA, marinaB);
        await db.Context.SaveChangesAsync();

        var spotA = new Spot("Spot A", "Desc", 20, 10, 5, 1, VesselType.None, marinaA.Id, 50m);
        spotA.Activate();
        var spotB = new Spot("Spot B", "Desc", 20, 10, 5, 1, VesselType.None, marinaB.Id, 50m);
        spotB.Activate();
        await db.Context.Spots.AddRangeAsync(spotA, spotB);
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marinaA.Id,
            UserId = "admin-user-1",
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "inviter-1"
        });

        var bookingA = new Booking
        {
            SpotId = spotA.Id,
            BoatOwnerId = "owner-1",
            StartDate = new DateOnly(2027, 7, 1),
            EndDate = new DateOnly(2027, 7, 5),
            TotalPrice = 200m
        };
        var bookingB = new Booking
        {
            SpotId = spotB.Id,
            BoatOwnerId = "owner-1",
            StartDate = new DateOnly(2027, 7, 10),
            EndDate = new DateOnly(2027, 7, 15),
            TotalPrice = 250m
        };
        await db.Context.Bookings.AddRangeAsync(bookingA, bookingB);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var results = (await repo.GetByMarinaOwnerIdAsync("admin-user-1")).ToList();

        Assert.Single(results);
        Assert.Equal(bookingA.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByBoatOwnerIdAsync_FiltersByOwner()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner1@test.com");
        await SeedUserAsync(db.Context, "owner-2", "owner2@test.com");
        await db.Context.SaveChangesAsync();

        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var bookingOwner1 = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = "owner-1",
            StartDate = new DateOnly(2027, 7, 1),
            EndDate = new DateOnly(2027, 7, 5),
            TotalPrice = 200m
        };
        var bookingOwner2 = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = "owner-2",
            StartDate = new DateOnly(2027, 8, 1),
            EndDate = new DateOnly(2027, 8, 5),
            TotalPrice = 200m
        };
        await db.Context.Bookings.AddRangeAsync(bookingOwner1, bookingOwner2);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var results = (await repo.GetByBoatOwnerIdAsync("owner-1")).ToList();

        Assert.Single(results);
        Assert.Equal(bookingOwner1.Id, results[0].Id);
    }

    private static readonly DateOnly OnDate = new(2027, 6, 15);

    [Fact]
    public async Task GetOccupiedSpotIdsAsync_ReturnsPendingAndConfirmedOverlapping()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spotPending) = await SeedMarinaAndSpotAsync(db.Context);
        var spotConfirmed = new Spot("Berth 2", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);
        spotConfirmed.Activate();
        await db.Context.Spots.AddAsync(spotConfirmed);
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await db.Context.SaveChangesAsync();

        var pending = new Booking { SpotId = spotPending.Id, BoatOwnerId = "owner-1", StartDate = new DateOnly(2027, 6, 10), EndDate = new DateOnly(2027, 6, 20), TotalPrice = 100m };
        var confirmed = new Booking { SpotId = spotConfirmed.Id, BoatOwnerId = "owner-1", StartDate = new DateOnly(2027, 6, 14), EndDate = new DateOnly(2027, 6, 16), TotalPrice = 100m };
        confirmed.Confirm();
        await db.Context.Bookings.AddRangeAsync(pending, confirmed);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var occupied = (await repo.GetOccupiedSpotIdsAsync(marina.Id, OnDate)).ToList();

        Assert.Equal(2, occupied.Count);
        Assert.Contains(spotPending.Id, occupied);
        Assert.Contains(spotConfirmed.Id, occupied);
    }

    [Fact]
    public async Task GetOccupiedSpotIdsAsync_ExcludesCancelledAndNonOverlapping()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spotCancelled) = await SeedMarinaAndSpotAsync(db.Context);
        var spotFuture = new Spot("Berth 2", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);
        spotFuture.Activate();
        await db.Context.Spots.AddAsync(spotFuture);
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await db.Context.SaveChangesAsync();

        var cancelled = new Booking { SpotId = spotCancelled.Id, BoatOwnerId = "owner-1", StartDate = new DateOnly(2027, 6, 10), EndDate = new DateOnly(2027, 6, 20), TotalPrice = 100m };
        cancelled.Cancel();
        var future = new Booking { SpotId = spotFuture.Id, BoatOwnerId = "owner-1", StartDate = new DateOnly(2027, 6, 20), EndDate = new DateOnly(2027, 6, 25), TotalPrice = 100m };
        future.Confirm();
        await db.Context.Bookings.AddRangeAsync(cancelled, future);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var occupied = (await repo.GetOccupiedSpotIdsAsync(marina.Id, OnDate)).ToList();

        Assert.Empty(occupied);
    }

    [Fact]
    public async Task GetOccupiedSpotIdsAsync_ScopedToMarina()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var marinaA = new Marina("Marina A", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        var marinaB = new Marina("Marina B", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        await db.Context.Marinas.AddRangeAsync(marinaA, marinaB);
        await db.Context.SaveChangesAsync();
        var spotB = new Spot("Berth B", "Desc", 20, 10, 5, 1, VesselType.None, marinaB.Id, 50m);
        spotB.Activate();
        await db.Context.Spots.AddAsync(spotB);
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await db.Context.SaveChangesAsync();

        var booking = new Booking { SpotId = spotB.Id, BoatOwnerId = "owner-1", StartDate = new DateOnly(2027, 6, 10), EndDate = new DateOnly(2027, 6, 20), TotalPrice = 100m };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var occupied = (await repo.GetOccupiedSpotIdsAsync(marinaA.Id, OnDate)).ToList();

        Assert.Empty(occupied);
    }

    [Fact]
    public async Task GetOccupiedSpotIdsAsync_DistinctPerSpot()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, spot) = await SeedMarinaAndSpotAsync(db.Context);
        await SeedUserAsync(db.Context, "owner-1", "owner@test.com");
        await db.Context.SaveChangesAsync();

        var b1 = new Booking { SpotId = spot.Id, BoatOwnerId = "owner-1", StartDate = new DateOnly(2027, 6, 10), EndDate = new DateOnly(2027, 6, 16), TotalPrice = 100m };
        b1.Confirm();
        var b2 = new Booking { SpotId = spot.Id, BoatOwnerId = "owner-1", StartDate = new DateOnly(2027, 6, 14), EndDate = new DateOnly(2027, 6, 20), TotalPrice = 100m };
        b2.Confirm();
        await db.Context.Bookings.AddRangeAsync(b1, b2);
        await db.Context.SaveChangesAsync();

        var repo = new BookingRepository(db.Context);
        var occupied = (await repo.GetOccupiedSpotIdsAsync(marina.Id, OnDate)).ToList();

        Assert.Single(occupied);
        Assert.Equal(spot.Id, occupied[0]);
    }
}
