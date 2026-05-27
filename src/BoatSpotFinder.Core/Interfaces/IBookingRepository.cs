using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IBookingRepository
{
    Task<IEnumerable<Booking>> GetByVesselIdAsync(Guid vesselId);
}
