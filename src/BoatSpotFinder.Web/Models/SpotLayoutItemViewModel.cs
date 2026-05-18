namespace BoatSpotFinder.Web.Models;

public record SpotLayoutItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public double? CanvasX { get; init; }
    public double? CanvasY { get; init; }
    public double? CanvasW { get; init; }
    public double? CanvasH { get; init; }
    public double? CanvasRotation { get; init; }
    public bool IsActive { get; init; }
}
