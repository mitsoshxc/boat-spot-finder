namespace BoatSpotFinder.Web.Models;

public record BrowseMarinaListViewModel
{
    public MarinaSearchFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<BrowseMarinaCardViewModel> Marinas { get; init; } = [];
}
