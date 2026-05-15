namespace BoatSpotFinder.Core.Entities;

public class Spot : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double LengthMeters { get; set; }
    public double WidthMeters { get; set; }
    public double DepthMeters { get; set; }
    public decimal? PricePerDay { get; set; }
    public int DefaultMinBookingDays { get; set; }
    public bool IsActive { get; set; }
    public VesselType AllowedVesselTypes { get; set; }
    public Guid MarinaId { get; set; }

    public double? CanvasX { get; set; }
    public double? CanvasY { get; set; }
    public double? CanvasW { get; set; }
    public double? CanvasH { get; set; }
    public double? CanvasRotation { get; set; }

    public Marina Marina { get; set; } = null!;
    public ICollection<SpotSeasonalRule> SeasonalRules { get; set; } = [];
}
