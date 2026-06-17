using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Enums;
using BoatSpotFinder.Core.Helpers;
using BoatSpotFinder.Core.Interfaces;

namespace BoatSpotFinder.Core.Services;

public class MarinaBrowseService : IMarinaBrowseService
{
    private readonly IMarinaSearchService _searchService;
    private readonly IMarinaRepository _marinaRepository;

    public MarinaBrowseService(IMarinaSearchService searchService, IMarinaRepository marinaRepository)
    {
        _searchService = searchService;
        _marinaRepository = marinaRepository;
    }

    public async Task<PaginatedList<Marina>> GetPageAsync(string? query, MarinaSortOption sort, int page, int pageSize = 20)
    {
        page = Math.Max(1, page);

        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var ids = hasQuery ? await _searchService.SearchAsync(query) : null; // blank query => no ES filter => all active

        // Relevance is only meaningful with ES-ranked ids for an actual query.
        if (sort == MarinaSortOption.Relevance && hasQuery && ids is not null)
        {
            var idList = ids.ToList();
            var totalCount = idList.Count;
            var pageIds = idList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var fetched = await _marinaRepository.GetActiveWithActiveSpotsAsync(pageIds);
            var byId = fetched.ToDictionary(m => m.Id);

            // Preserve ES relevance order; drop any id the active+active-spots SQL filter removed.
            var ordered = pageIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

            return PaginatedList<Marina>.CreateFromItems(ordered, totalCount, page, pageSize);
        }

        // Otherwise: build the candidate set, then sort + page in memory.
        IReadOnlyList<Marina> candidates = ids is null
            ? await _marinaRepository.GetActiveWithActiveSpotsAsync()
            : await _marinaRepository.GetActiveWithActiveSpotsAsync(ids.ToList());

        // Unsatisfiable Relevance (ES off, or no query) degrades to Top rated.
        var effectiveSort = sort == MarinaSortOption.Relevance ? MarinaSortOption.TopRated : sort;

        IReadOnlyList<Marina> sorted = effectiveSort switch
        {
            MarinaSortOption.Alphabetical => candidates.OrderBy(m => m.Name).ToList(),
            _ => candidates
                    .OrderByDescending(m => m.AverageRating ?? decimal.MinValue)
                    .ThenBy(m => m.Name)
                    .ToList()
        };

        return PaginatedList<Marina>.CreateFromList(sorted, page, pageSize);
    }
}
