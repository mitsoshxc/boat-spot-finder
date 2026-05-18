using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Models;

namespace BoatSpotFinder.Core.Interfaces;

public interface ISpotRepository
{
    Task<Spot?> GetByIdAsync(Guid id);
    Task<Spot?> GetActiveByIdAsync(Guid id);
    Task<IReadOnlyList<Spot>> GetByMarinaIdAsync(Guid marinaId, bool includeInactive);
    Task<IReadOnlyList<Spot>> GetAllAsync(bool includeInactive);
    Task AddAsync(Spot spot);
    Task UpdateAsync(Spot spot);
    Task UpdatePositionsAsync(IEnumerable<SpotPositionUpdate> updates);
}
