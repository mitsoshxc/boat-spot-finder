using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Enums;
using BoatSpotFinder.Core.Helpers;

namespace BoatSpotFinder.Core.Interfaces;

public interface IMarinaBrowseService
{
    Task<PaginatedList<Marina>> GetPageAsync(string? query, MarinaSortOption sort, int page, int pageSize = 20);
}
