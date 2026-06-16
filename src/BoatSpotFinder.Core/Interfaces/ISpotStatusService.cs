using BoatSpotFinder.Core.Models;

namespace BoatSpotFinder.Core.Interfaces;

public interface ISpotStatusService
{
    Task<IEnumerable<SpotWithStatus>> GetWithStatusAsync(Guid marinaId, DateOnly? start, DateOnly? end, Guid? vesselId);
}
