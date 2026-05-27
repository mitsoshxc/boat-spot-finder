using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;

namespace BoatSpotFinder.Infrastructure.Search;

public class NullMarinaSearchService : IMarinaSearchService
{
    public Task IndexAsync(Marina marina) => Task.CompletedTask;

    public Task DeleteAsync(Guid id) => Task.CompletedTask;

    public Task<IEnumerable<Guid>?> SearchAsync(string? query) =>
        Task.FromResult<IEnumerable<Guid>?>(null);
}
