namespace BoatSpotFinder.Core.Entities;

public class Spot : BaseEntity
{
    private Spot() { }

    public Spot(
        string name,
        string description,
        double lengthMeters,
        double widthMeters,
        double depthMeters,
        int defaultMinBookingDays,
        VesselType allowedVesselTypes,
        Guid marinaId,
        decimal? pricePerDay = null)
    {
        Name = name;
        Description = description;
        LengthMeters = lengthMeters;
        WidthMeters = widthMeters;
        DepthMeters = depthMeters;
        DefaultMinBookingDays = defaultMinBookingDays;
        AllowedVesselTypes = allowedVesselTypes;
        MarinaId = marinaId;
        PricePerDay = pricePerDay;
    }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public double LengthMeters { get; private set; }
    public double WidthMeters { get; private set; }
    public double DepthMeters { get; private set; }
    public decimal? PricePerDay { get; private set; }
    public int DefaultMinBookingDays { get; private set; }
    public bool IsActive { get; private set; }
    public VesselType AllowedVesselTypes { get; private set; }
    public Guid MarinaId { get; init; }

    public double? CanvasX { get; private set; }
    public double? CanvasY { get; private set; }
    public double? CanvasW { get; private set; }
    public double? CanvasH { get; private set; }
    public double? CanvasRotation { get; private set; }

    public Marina Marina { get; init; } = null!;
    public ICollection<SpotSeasonalRule> SeasonalRules { get; init; } = [];

    public void UpdateDetails(
        string name,
        string description,
        double lengthMeters,
        double widthMeters,
        double depthMeters,
        decimal? pricePerDay,
        int defaultMinBookingDays,
        VesselType allowedVesselTypes)
    {
        Name = name;
        Description = description;
        LengthMeters = lengthMeters;
        WidthMeters = widthMeters;
        DepthMeters = depthMeters;
        PricePerDay = pricePerDay;
        DefaultMinBookingDays = defaultMinBookingDays;
        AllowedVesselTypes = allowedVesselTypes;
    }

    public void UpdateCanvasPosition(double? x, double? y, double? w, double? h, double? rotation)
    {
        CanvasX = x;
        CanvasY = y;
        CanvasW = w;
        CanvasH = h;
        CanvasRotation = rotation;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
