using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Services;
using BoatSpotFinder.Infrastructure.Data;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace BoatSpotFinder.Tests.Services;

public class ReviewServiceTests
{
    private static ReviewService BuildService(
        AppDbContext context,
        UserManager<ApplicationUser>? userManager = null,
        IReviewSearchService? reviewSearchService = null,
        IMarinaSearchService? marinaSearchService = null)
    {
        userManager ??= BuildUserManager();
        reviewSearchService ??= Substitute.For<IReviewSearchService>();
        marinaSearchService ??= Substitute.For<IMarinaSearchService>();

        return new ReviewService(
            new BookingRepository(context),
            new ReviewRepository(context),
            new MarinaAdminRepository(context),
            new MarinaRepository(context),
            reviewSearchService,
            marinaSearchService,
            userManager,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ReviewService>>());
    }

    private static UserManager<ApplicationUser> BuildUserManager(ApplicationUser? user = null)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var mgr = Substitute.For<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);
        var stubUser = user ?? new ApplicationUser { Id = "user-1", Email = "test@example.com", UserName = "test@example.com" };
        mgr.FindByIdAsync(Arg.Any<string>()).Returns(Task.FromResult<ApplicationUser?>(stubUser));
        mgr.UpdateAsync(Arg.Any<ApplicationUser>()).Returns(Task.FromResult(IdentityResult.Success));
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

    private static async Task<(Marina marina, Spot spot)> SeedMarinaAndSpotAsync(AppDbContext context, bool marinaActive = true)
    {
        var marina = new Marina("Test Marina", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        if (!marinaActive)
        {
            marina.Deactivate();
        }
        await context.Marinas.AddAsync(marina);

        var spot = new Spot("Spot A", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);
        spot.Activate();
        await context.Spots.AddAsync(spot);

        await context.SaveChangesAsync();
        return (marina, spot);
    }

    private static async Task<Booking> SeedCompletedBookingAsync(
        AppDbContext context,
        Guid spotId,
        string boatOwnerId,
        DateOnly endDate)
    {
        var booking = new Booking
        {
            SpotId = spotId,
            BoatOwnerId = boatOwnerId,
            StartDate = endDate.AddDays(-5),
            EndDate = endDate,
            TotalPrice = 250m
        };
        booking.Complete();
        await context.Bookings.AddAsync(booking);
        await context.SaveChangesAsync();
        return booking;
    }

    // ── CanReviewAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CanReviewAsync_BookingNotFound_NotEligible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var service = BuildService(db.Context);

        var result = await service.CanReviewAsync(Guid.NewGuid(), "any-user");

        Assert.False(result.IsEligible);
        Assert.Contains("not found", result.ErrorReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanReviewAsync_BookingNotCompleted_NotEligible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        booking.Confirm();
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CanReviewAsync(booking.Id, boatOwnerId);

        Assert.False(result.IsEligible);
        Assert.Contains("not completed", result.ErrorReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanReviewAsync_ReviewWindowClosed_NotEligible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-15);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var service = BuildService(db.Context);
        var result = await service.CanReviewAsync(booking.Id, boatOwnerId);

        Assert.False(result.IsEligible);
        Assert.Contains("closed", result.ErrorReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanReviewAsync_LastDayOfWindow_Eligible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-14);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var service = BuildService(db.Context);
        var result = await service.CanReviewAsync(booking.Id, boatOwnerId);

        Assert.True(result.IsEligible);
        Assert.Equal(ReviewerRole.BoatOwner, result.Role);
    }

    [Fact]
    public async Task CanReviewAsync_BoatOwner_Eligible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var service = BuildService(db.Context);
        var result = await service.CanReviewAsync(booking.Id, boatOwnerId);

        Assert.True(result.IsEligible);
        Assert.Equal(ReviewerRole.BoatOwner, result.Role);
    }

    [Fact]
    public async Task CanReviewAsync_PlaceOwnerViaMarinaAdmin_Eligible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        const string placeOwnerId = "po-1";
        const string adminId = "admin-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        await SeedUserAsync(db.Context, placeOwnerId, "po@test.com");
        await SeedUserAsync(db.Context, adminId, "admin@test.com");
        var (marina, spot) = await SeedMarinaAndSpotAsync(db.Context);

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = placeOwnerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = adminId
        });

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var service = BuildService(db.Context);
        var result = await service.CanReviewAsync(booking.Id, placeOwnerId);

        Assert.True(result.IsEligible);
        Assert.Equal(ReviewerRole.PlaceOwner, result.Role);
    }

    [Fact]
    public async Task CanReviewAsync_UnrelatedUser_NotEligible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var service = BuildService(db.Context);
        var result = await service.CanReviewAsync(booking.Id, "unrelated-user");

        Assert.False(result.IsEligible);
        Assert.Contains("eligible", result.ErrorReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanReviewAsync_AlreadyReviewedForRole_NotEligible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        await db.Context.Reviews.AddAsync(new Review
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            ReviewerId = boatOwnerId,
            ReviewerRole = ReviewerRole.BoatOwner,
            Score = 4
        });
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CanReviewAsync(booking.Id, boatOwnerId);

        Assert.False(result.IsEligible);
        Assert.Contains("already reviewed", result.ErrorReason, StringComparison.OrdinalIgnoreCase);
    }

    // ── CreateReviewAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateReviewAsync_IneligibleBooking_Fails()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var booking = new Booking
        {
            SpotId = spot.Id,
            BoatOwnerId = boatOwnerId,
            StartDate = new DateOnly(2027, 6, 1),
            EndDate = new DateOnly(2027, 6, 10),
            TotalPrice = 450m
        };
        await db.Context.Bookings.AddAsync(booking);
        await db.Context.SaveChangesAsync();

        var service = BuildService(db.Context);
        var result = await service.CreateReviewAsync(booking.Id, boatOwnerId, 4, null);

        Assert.False(result.Success);
        Assert.Empty(db.Context.Reviews);
    }

    [Fact]
    public async Task CreateReviewAsync_BoatOwnerReview_PersistsAndRecomputesMarinaRating()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (marina, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var service = BuildService(db.Context);
        var result = await service.CreateReviewAsync(booking.Id, boatOwnerId, 4, "Great spot");

        Assert.True(result.Success);

        var review = db.Context.Reviews.Single();
        Assert.Equal(ReviewerRole.BoatOwner, review.ReviewerRole);
        Assert.Equal(4, review.Score);

        var updatedMarina = await db.Context.Marinas.FindAsync(marina.Id);
        Assert.NotNull(updatedMarina);
        Assert.Equal(1, updatedMarina.ReviewCount);
        Assert.Equal(4m, updatedMarina.AverageRating);
    }

    [Fact]
    public async Task CreateReviewAsync_BoatOwner_AveragesAcrossMultipleBookings()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId1 = "owner-1";
        const string boatOwnerId2 = "owner-2";
        await SeedUserAsync(db.Context, boatOwnerId1, "owner1@test.com");
        await SeedUserAsync(db.Context, boatOwnerId2, "owner2@test.com");
        var (marina, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);

        var booking1 = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId1, endDate.AddDays(-10));
        var booking2 = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId2, endDate);

        var service = BuildService(db.Context);
        await service.CreateReviewAsync(booking1.Id, boatOwnerId1, 3, null);
        await service.CreateReviewAsync(booking2.Id, boatOwnerId2, 5, null);

        var updatedMarina = await db.Context.Marinas.FindAsync(marina.Id);
        Assert.NotNull(updatedMarina);
        Assert.Equal(2, updatedMarina.ReviewCount);
        Assert.Equal(4m, updatedMarina.AverageRating);
    }

    [Fact]
    public async Task CreateReviewAsync_PlaceOwnerReview_RecomputesBoatOwnerRating()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        const string placeOwnerId = "po-1";
        const string adminId = "admin-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        await SeedUserAsync(db.Context, placeOwnerId, "po@test.com");
        await SeedUserAsync(db.Context, adminId, "admin@test.com");
        var (marina, spot) = await SeedMarinaAndSpotAsync(db.Context);

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marina.Id,
            UserId = placeOwnerId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = adminId
        });
        await db.Context.SaveChangesAsync();

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var boatOwnerUser = new ApplicationUser
        {
            Id = boatOwnerId,
            Email = "owner@test.com",
            UserName = "owner@test.com"
        };
        var userManager = BuildUserManager(boatOwnerUser);

        var service = BuildService(db.Context, userManager);
        var result = await service.CreateReviewAsync(booking.Id, placeOwnerId, 5, "Excellent guest");

        Assert.True(result.Success);
        Assert.Equal(1, boatOwnerUser.ReviewCountAsBoatOwner);
        Assert.Equal(5m, boatOwnerUser.AverageRatingAsBoatOwner);
        await userManager.Received(1).UpdateAsync(boatOwnerUser);
    }

    [Fact]
    public async Task CreateReviewAsync_IndexesReviewToEs()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var reviewSearchService = Substitute.For<IReviewSearchService>();

        var service = BuildService(db.Context, reviewSearchService: reviewSearchService);
        await service.CreateReviewAsync(booking.Id, boatOwnerId, 4, null);

        await reviewSearchService.Received(1).IndexAsync(Arg.Any<Review>());
    }

    [Fact]
    public async Task CreateReviewAsync_EsIndexThrows_StillSucceeds()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        const string boatOwnerId = "owner-1";
        await SeedUserAsync(db.Context, boatOwnerId, "owner@test.com");
        var (_, spot) = await SeedMarinaAndSpotAsync(db.Context);

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var booking = await SeedCompletedBookingAsync(db.Context, spot.Id, boatOwnerId, endDate);

        var reviewSearchService = Substitute.For<IReviewSearchService>();
        reviewSearchService.IndexAsync(Arg.Any<Review>()).ThrowsAsync(new Exception("ES offline"));

        var service = BuildService(db.Context, reviewSearchService: reviewSearchService);
        var result = await service.CreateReviewAsync(booking.Id, boatOwnerId, 4, null);

        Assert.True(result.Success);
        Assert.Single(db.Context.Reviews);
    }
}
