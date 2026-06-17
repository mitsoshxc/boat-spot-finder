using BoatSpotFinder.Core.Enums;

namespace BoatSpotFinder.Web.Models;

public record MarinaSearchFilterViewModel
{
    public string? Query { get; init; }
    public MarinaSortOption? Sort { get; init; }
    public int Page { get; init; } = 1;
}
