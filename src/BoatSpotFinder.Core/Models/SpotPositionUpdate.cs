namespace BoatSpotFinder.Core.Models;

public record SpotPositionUpdate(
    Guid Id,
    double CanvasX,
    double CanvasY,
    double CanvasW,
    double CanvasH,
    double CanvasRotation);
