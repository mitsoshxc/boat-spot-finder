namespace BoatSpotFinder.Core.Entities;

public class Vessel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VesselType Type { get; set; }
    public double LengthMeters { get; set; }
    public double WidthMeters { get; set; }
    public double DepthMeters { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser Owner { get; set; } = null!;
}
