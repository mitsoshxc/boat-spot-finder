using BoatSpotFinder.Core.Enums;

namespace BoatSpotFinder.Web.Models;

public record BrowseMarinaListViewModel
{
    public MarinaSearchFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<BrowseMarinaCardViewModel> Marinas { get; init; } = [];
    public MarinaSortOption Sort { get; init; }
    public bool ShowRelevanceOption { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
}
