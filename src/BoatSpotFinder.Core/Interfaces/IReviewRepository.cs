using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IReviewRepository
{
    Task<IEnumerable<Review>> GetByBookingIdAsync(Guid bookingId);
    Task<IEnumerable<Review>> GetRecentByMarinaIdAsync(Guid marinaId, int count);
    Task<IEnumerable<Review>> GetAllByMarinaIdAsync(Guid marinaId);
    Task<IEnumerable<Review>> GetAllByBoatOwnerIdAsync(string userId);
    Task AddAsync(Review review);
    Task<bool> ExistsAsync(Guid bookingId, ReviewerRole reviewerRole);
}
