using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IMarinaSearchService
{
    Task IndexAsync(Marina marina);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<Guid>?> SearchAsync(string? query);
}
