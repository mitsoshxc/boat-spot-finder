using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IMarinaRepository
{
    Task<Marina?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Marina>> GetByUserIdAsync(string userId);
    Task<IReadOnlyList<Marina>> GetAllAsync(bool includeInactive);
    Task<IReadOnlyList<Marina>> GetActiveWithActiveSpotsAsync(IReadOnlyCollection<Guid>? marinaIds = null);
    Task AddAsync(Marina marina);
    Task UpdateAsync(Marina marina);
}
