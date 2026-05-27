using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id);
    Task<IEnumerable<Booking>> GetByVesselIdAsync(Guid vesselId);
    Task<IEnumerable<Booking>> GetByBoatOwnerIdAsync(string userId);
    Task<IEnumerable<Booking>> GetBySpotIdAsync(Guid spotId);
    Task<IEnumerable<Booking>> GetByMarinaOwnerIdAsync(string userId);
    Task<IEnumerable<Booking>> GetAllAsync();
    Task AddAsync(Booking booking);
    Task UpdateAsync(Booking booking);
    Task<bool> IsSpotAvailableAsync(Guid spotId, DateOnly start, DateOnly end, Guid? excludeBookingId);
}
