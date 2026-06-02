using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Infrastructure.Data;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;

namespace BoatSpotFinder.Tests.Repositories;

public class ReviewRepositoryTests
{
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

    private static async Task<(Marina marina, Spot spot, Booking booking)> SeedBookingAsync(
        AppDbContext context,
        string boatOwnerId,
        Marina? existingMarina = null)
    {
        Marina marina;
        if (existingMarina is null)
        {
            marina = new Marina("Test Marina", "Desc", "Addr", "Region", "000", 0, 0, 50m);
            await context.Marinas.AddAsync(marina);
        }
        else
        {
            marina = existingMarina;
        }

        var spot = new Spot("Spot A", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);
        spot.Activate();
        await context.Spots.AddAsync(spot);

        var booking = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        booking.Complete();
        await context.Bookings.AddAsync(booking);
        await context.SaveChangesAsync();

        return (marina, spot, booking);
    }

    private static Review MakeReview(Guid bookingId, string reviewerId, ReviewerRole role, int score) =>
        new()
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            ReviewerId = reviewerId,
            ReviewerRole = role,
            Score = score
        };

    [Fact]
    public async Task ExistsAsync_SameBookingDifferentRole_False()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner1@test.com");
        await db.Context.SaveChangesAsync();
        var (_, _, booking) = await SeedBookingAsync(db.Context, "owner-1");

        await db.Context.Reviews.AddAsync(MakeReview(booking.Id, "owner-1", ReviewerRole.BoatOwner, 4));
        await db.Context.SaveChangesAsync();

        var repo = new ReviewRepository(db.Context);
        var exists = await repo.ExistsAsync(booking.Id, ReviewerRole.PlaceOwner);

        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_Match_True()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner1@test.com");
        await db.Context.SaveChangesAsync();
        var (_, _, booking) = await SeedBookingAsync(db.Context, "owner-1");

        await db.Context.Reviews.AddAsync(MakeReview(booking.Id, "owner-1", ReviewerRole.BoatOwner, 4));
        await db.Context.SaveChangesAsync();

        var repo = new ReviewRepository(db.Context);
        var exists = await repo.ExistsAsync(booking.Id, ReviewerRole.BoatOwner);

        Assert.True(exists);
    }

    [Fact]
    public async Task GetAllByMarinaIdAsync_ReturnsOnlyBoatOwnerReviewsForThatMarina()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner1@test.com");
        await SeedUserAsync(db.Context, "owner-2", "owner2@test.com");
        await SeedUserAsync(db.Context, "po-1", "po1@test.com");
        await db.Context.SaveChangesAsync();

        var (marinaA, _, bookingA) = await SeedBookingAsync(db.Context, "owner-1");
        var (_, _, bookingB) = await SeedBookingAsync(db.Context, "owner-2");

        // BoatOwner review for marina A — should be returned
        await db.Context.Reviews.AddAsync(MakeReview(bookingA.Id, "owner-1", ReviewerRole.BoatOwner, 5));
        // PlaceOwner review for marina A — should be excluded (wrong role)
        await db.Context.Reviews.AddAsync(MakeReview(bookingA.Id, "po-1", ReviewerRole.PlaceOwner, 3));
        // BoatOwner review for marina B — should be excluded (wrong marina)
        await db.Context.Reviews.AddAsync(MakeReview(bookingB.Id, "owner-2", ReviewerRole.BoatOwner, 4));
        await db.Context.SaveChangesAsync();

        var repo = new ReviewRepository(db.Context);
        var results = (await repo.GetAllByMarinaIdAsync(marinaA.Id)).ToList();

        Assert.Single(results);
        Assert.Equal(ReviewerRole.BoatOwner, results[0].ReviewerRole);
        Assert.Equal(bookingA.Id, results[0].BookingId);
    }

    [Fact]
    public async Task GetAllByBoatOwnerIdAsync_ReturnsOnlyPlaceOwnerReviewsForThatOwner()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner1@test.com");
        await SeedUserAsync(db.Context, "owner-2", "owner2@test.com");
        await SeedUserAsync(db.Context, "po-1", "po1@test.com");
        await SeedUserAsync(db.Context, "po-2", "po2@test.com");
        await db.Context.SaveChangesAsync();

        var (_, _, bookingOwner1) = await SeedBookingAsync(db.Context, "owner-1");
        var (_, _, bookingOwner2) = await SeedBookingAsync(db.Context, "owner-2");

        // PlaceOwner review targeting owner-1 — should be returned
        await db.Context.Reviews.AddAsync(MakeReview(bookingOwner1.Id, "po-1", ReviewerRole.PlaceOwner, 5));
        // BoatOwner review by owner-1 — should be excluded (wrong role)
        await db.Context.Reviews.AddAsync(MakeReview(bookingOwner1.Id, "owner-1", ReviewerRole.BoatOwner, 4));
        // PlaceOwner review for owner-2 — should be excluded (different boat owner)
        await db.Context.Reviews.AddAsync(MakeReview(bookingOwner2.Id, "po-2", ReviewerRole.PlaceOwner, 3));
        await db.Context.SaveChangesAsync();

        var repo = new ReviewRepository(db.Context);
        var results = (await repo.GetAllByBoatOwnerIdAsync("owner-1")).ToList();

        Assert.Single(results);
        Assert.Equal(ReviewerRole.PlaceOwner, results[0].ReviewerRole);
        Assert.Equal(bookingOwner1.Id, results[0].BookingId);
    }

    [Fact]
    public async Task GetRecentByMarinaIdAsync_OrdersByCreatedAtDescAndRespectsCount()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner1@test.com");
        await SeedUserAsync(db.Context, "owner-2", "owner2@test.com");
        await SeedUserAsync(db.Context, "owner-3", "owner3@test.com");
        await db.Context.SaveChangesAsync();

        var (marina, spot, booking) = await SeedBookingAsync(db.Context, "owner-1");

        var booking2 = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = "owner-2",
            StartDate = new DateOnly(2027, 7, 1),
            EndDate = new DateOnly(2027, 7, 10),
            TotalPrice = 450m
        };
        booking2.Complete();
        var booking3 = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = "owner-3",
            StartDate = new DateOnly(2027, 8, 1),
            EndDate = new DateOnly(2027, 8, 10),
            TotalPrice = 450m
        };
        booking3.Complete();
        await db.Context.Bookings.AddRangeAsync(booking2, booking3);
        await db.Context.SaveChangesAsync();

        // Save each review separately so SetTimestamps assigns distinct CreatedAt values
        var oldest = MakeReview(booking.Id, "owner-1", ReviewerRole.BoatOwner, 3);
        await db.Context.Reviews.AddAsync(oldest);
        await db.Context.SaveChangesAsync();
        await Task.Delay(5);

        var middle = MakeReview(booking2.Id, "owner-2", ReviewerRole.BoatOwner, 4);
        await db.Context.Reviews.AddAsync(middle);
        await db.Context.SaveChangesAsync();
        await Task.Delay(5);

        var newest = MakeReview(booking3.Id, "owner-3", ReviewerRole.BoatOwner, 5);
        await db.Context.Reviews.AddAsync(newest);
        await db.Context.SaveChangesAsync();

        var repo = new ReviewRepository(db.Context);
        var results = (await repo.GetRecentByMarinaIdAsync(marina.Id, 2)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(newest.Id, results[0].Id);
        Assert.Equal(middle.Id, results[1].Id);
    }
}
