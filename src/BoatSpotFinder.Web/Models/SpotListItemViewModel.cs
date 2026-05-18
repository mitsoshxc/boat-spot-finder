using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Web.Models;

public record SpotListItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double LengthMeters { get; init; }
    public double WidthMeters { get; init; }
    public double DepthMeters { get; init; }
    public decimal? PricePerDay { get; init; }
    public int DefaultMinBookingDays { get; init; }
    public VesselType AllowedVesselTypes { get; init; }
    public bool IsActive { get; init; }
}
