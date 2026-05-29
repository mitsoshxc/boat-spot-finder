using BoatSpotFinder.Core.Common;
using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BoatSpotFinder.Core.Services;

public class ReviewService : IReviewService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IMarinaAdminRepository _marinaAdminRepository;
    private readonly IMarinaRepository _marinaRepository;
    private readonly IReviewSearchService _reviewSearchService;
    private readonly IMarinaSearchService _marinaSearchService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        IBookingRepository bookingRepository,
        IReviewRepository reviewRepository,
        IMarinaAdminRepository marinaAdminRepository,
        IMarinaRepository marinaRepository,
        IReviewSearchService reviewSearchService,
        IMarinaSearchService marinaSearchService,
        UserManager<ApplicationUser> userManager,
        ILogger<ReviewService> logger)
    {
        _bookingRepository = bookingRepository;
        _reviewRepository = reviewRepository;
        _marinaAdminRepository = marinaAdminRepository;
        _marinaRepository = marinaRepository;
        _reviewSearchService = reviewSearchService;
        _marinaSearchService = marinaSearchService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<ReviewEligibility> CanReviewAsync(Guid bookingId, string userId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);

        if (booking is null)
        {
            return new ReviewEligibility(false, null, "Booking not found");
        }

        if (booking.Status != BookingStatus.Completed)
        {
            return new ReviewEligibility(false, null, "Booking is not completed");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (booking.EndDate.AddDays(14) < today)
        {
            return new ReviewEligibility(false, null, "Review window has closed");
        }

        ReviewerRole resolvedRole;

        if (userId == booking.BoatOwnerId)
        {
            resolvedRole = ReviewerRole.BoatOwner;
        }
        else if (await _marinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, userId))
        {
            resolvedRole = ReviewerRole.PlaceOwner;
        }
        else
        {
            return new ReviewEligibility(false, null, "Not eligible to review this booking");
        }

        if (await _reviewRepository.ExistsAsync(bookingId, resolvedRole))
        {
            return new ReviewEligibility(false, null, "You have already reviewed this booking");
        }

        return new ReviewEligibility(true, resolvedRole, null);
    }

    public async Task<ServiceResult> CreateReviewAsync(Guid bookingId, string userId, int score, string? comment)
    {
        var eligibility = await CanReviewAsync(bookingId, userId);

        if (!eligibility.IsEligible)
        {
            return ServiceResult.Fail(eligibility.ErrorReason!);
        }

        var booking = await _bookingRepository.GetByIdAsync(bookingId);

        var review = new Review
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            ReviewerId = userId,
            ReviewerRole = eligibility.Role!.Value,
            Score = score,
            Comment = comment,
        };

        await _reviewRepository.AddAsync(review);

        review.Booking = booking!;

        if (eligibility.Role == ReviewerRole.BoatOwner)
        {
            var marinaId = booking!.Spot.MarinaId;
            var allMarinaReviews = (await _reviewRepository.GetAllByMarinaIdAsync(marinaId)).ToList();
            var marina = await _marinaRepository.GetByIdAsync(marinaId);

            if (marina is not null)
            {
                marina.ReviewCount = allMarinaReviews.Count;
                marina.AverageRating = allMarinaReviews.Count > 0
                    ? (decimal?)allMarinaReviews.Average(r => r.Score)
                    : null;
                await _marinaRepository.UpdateAsync(marina);
            }
        }
        else
        {
            var boatOwnerId = booking!.BoatOwnerId;
            var allBoatOwnerReviews = (await _reviewRepository.GetAllByBoatOwnerIdAsync(boatOwnerId)).ToList();
            var user = await _userManager.FindByIdAsync(boatOwnerId);

            if (user is not null)
            {
                user.ReviewCountAsBoatOwner = allBoatOwnerReviews.Count;
                user.AverageRatingAsBoatOwner = allBoatOwnerReviews.Count > 0
                    ? (decimal?)allBoatOwnerReviews.Average(r => r.Score)
                    : null;
                await _userManager.UpdateAsync(user);
            }
        }

        try
        {
            await _reviewSearchService.IndexAsync(review);

            if (eligibility.Role == ReviewerRole.BoatOwner)
            {
                var marina = await _marinaRepository.GetByIdAsync(booking!.Spot.MarinaId);
                if (marina is not null && marina.IsActive)
                {
                    await _marinaSearchService.IndexAsync(marina);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ES sync failed after creating review {ReviewId}", review.Id);
        }

        return ServiceResult.Ok();
    }

    public async Task<IEnumerable<Review>> GetForBookingAsync(Guid bookingId)
    {
        return await _reviewRepository.GetByBookingIdAsync(bookingId);
    }
}
