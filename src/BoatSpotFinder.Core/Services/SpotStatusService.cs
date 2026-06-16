using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Enums;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Models;

namespace BoatSpotFinder.Core.Services;

public class SpotStatusService : ISpotStatusService
{
    private readonly ISpotRepository _spotRepository;
    private readonly IVesselRepository _vesselRepository;
    private readonly IBookingRepository _bookingRepository;

    public SpotStatusService(ISpotRepository spotRepository, IVesselRepository vesselRepository, IBookingRepository bookingRepository)
    {
        _spotRepository = spotRepository;
        _vesselRepository = vesselRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<IEnumerable<SpotWithStatus>> GetWithStatusAsync(Guid marinaId, DateOnly? start, DateOnly? end, Guid? vesselId)
    {
        var spots = await _spotRepository.GetByMarinaIdAsync(marinaId, includeInactive: true);
        Vessel? vessel = vesselId.HasValue ? await _vesselRepository.GetByIdAsync(vesselId.Value) : null;

        var result = new List<SpotWithStatus>();
        foreach (var spot in spots)
        {
            SpotAvailabilityStatus status;
            if (!spot.IsActive)
            {
                status = SpotAvailabilityStatus.Unavailable;
            }
            else if (vessel is not null && (
                (spot.AllowedVesselTypes != VesselType.None && (spot.AllowedVesselTypes & vessel.Type) == 0) ||
                vessel.LengthMeters > spot.LengthMeters || vessel.WidthMeters > spot.WidthMeters || vessel.DepthMeters > spot.DepthMeters))
            {
                status = SpotAvailabilityStatus.Incompatible;
            }
            else if (start.HasValue && end.HasValue && !await _bookingRepository.IsSpotAvailableAsync(spot.Id, start.Value, end.Value, null))
            {
                status = SpotAvailabilityStatus.Booked;
            }
            else
            {
                status = SpotAvailabilityStatus.Free;
            }

            result.Add(new SpotWithStatus(spot, status));
        }

        return result;
    }
}
