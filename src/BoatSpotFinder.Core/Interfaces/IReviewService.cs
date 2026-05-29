using BoatSpotFinder.Core.Common;
using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Models;

namespace BoatSpotFinder.Core.Interfaces;

public interface IReviewService
{
    Task<ReviewEligibility> CanReviewAsync(Guid bookingId, string userId);
    Task<ServiceResult> CreateReviewAsync(Guid bookingId, string userId, int score, string? comment);
    Task<IEnumerable<Review>> GetForBookingAsync(Guid bookingId);
}
