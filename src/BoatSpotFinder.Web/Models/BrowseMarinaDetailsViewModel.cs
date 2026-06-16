namespace BoatSpotFinder.Web.Models;

public record BrowseMarinaDetailsViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Region { get; init; } = "";
    public string Description { get; init; } = "";
    public int LayoutWidth { get; init; }
    public int LayoutHeight { get; init; }
    public string? BackgroundImagePath { get; init; }
    public decimal? AverageRating { get; init; }
    public int ReviewCount { get; init; }
    public bool IsBoatOwner { get; init; }
    public IReadOnlyList<VesselOptionViewModel> Vessels { get; init; } = [];
}
