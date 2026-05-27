using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Web.Models;

public record VesselListItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public VesselType Type { get; init; }
    public double LengthMeters { get; init; }
    public double WidthMeters { get; init; }
    public double DepthMeters { get; init; }
}
