namespace BoatSpotFinder.Core.Entities;

public class Vessel : BaseEntity
{
    private Vessel() { }

    public Vessel(
        string name,
        VesselType type,
        double lengthMeters,
        double widthMeters,
        double depthMeters,
        string ownerId,
        string? description = null)
    {
        Name = name;
        Type = type;
        LengthMeters = lengthMeters;
        WidthMeters = widthMeters;
        DepthMeters = depthMeters;
        OwnerId = ownerId;
        Description = description;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public VesselType Type { get; private set; }
    public double LengthMeters { get; private set; }
    public double WidthMeters { get; private set; }
    public double DepthMeters { get; private set; }
    public string OwnerId { get; init; } = string.Empty;

    public ApplicationUser Owner { get; init; } = null!;

    public void UpdateDetails(
        string name,
        VesselType type,
        double lengthMeters,
        double widthMeters,
        double depthMeters,
        string? description)
    {
        Name = name;
        Type = type;
        LengthMeters = lengthMeters;
        WidthMeters = widthMeters;
        DepthMeters = depthMeters;
        Description = description;
    }
}
