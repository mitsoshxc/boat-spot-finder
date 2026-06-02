namespace BoatSpotFinder.Web.Models;

public record AdminMarinaListItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int AdminCount { get; init; }
    public int SpotCount { get; init; }
}
