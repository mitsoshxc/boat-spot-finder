namespace BoatSpotFinder.Web.Models;

public record BrowseMarinaCardViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Region { get; init; } = "";
    public string Description { get; init; } = "";
    public int SpotCount { get; init; }
    public decimal? AverageRating { get; init; }
    public int ReviewCount { get; init; }
}
