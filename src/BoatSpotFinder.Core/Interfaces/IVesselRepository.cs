using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IVesselRepository
{
    Task<Vessel?> GetByIdAsync(Guid id);
    Task<IEnumerable<Vessel>> GetByOwnerIdAsync(string userId);
    Task AddAsync(Vessel vessel);
    Task UpdateAsync(Vessel vessel);
    Task DeleteAsync(Vessel vessel);
}
