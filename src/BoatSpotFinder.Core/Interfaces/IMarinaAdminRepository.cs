using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IMarinaAdminRepository
{
    Task<IReadOnlyList<MarinaAdmin>> GetByMarinaIdAsync(Guid marinaId);
    Task<IReadOnlyList<MarinaAdmin>> GetByUserIdAsync(string userId);
    Task AddAsync(MarinaAdmin marinaAdmin);
    Task RemoveAsync(MarinaAdmin marinaAdmin);
    Task UpdateAsync(MarinaAdmin marinaAdmin);
    Task<bool> ExistsAsync(Guid marinaId, string userId);
}
