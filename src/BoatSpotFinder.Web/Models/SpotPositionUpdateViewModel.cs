namespace BoatSpotFinder.Web.Models;

public record SpotPositionUpdateViewModel
{
    public Guid Id { get; init; }
    public double CanvasX { get; init; }
    public double CanvasY { get; init; }
    public double CanvasW { get; init; }
    public double CanvasH { get; init; }
    public double CanvasRotation { get; init; }
}
