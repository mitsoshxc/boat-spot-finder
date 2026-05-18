namespace BoatSpotFinder.Web.Models;

public record MarinaLayoutViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int LayoutWidth { get; init; }
    public int LayoutHeight { get; init; }
    public string? BackgroundImagePath { get; init; }
    public List<SpotLayoutItemViewModel> Spots { get; init; } = [];
}
