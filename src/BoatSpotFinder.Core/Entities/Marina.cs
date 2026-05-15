namespace BoatSpotFinder.Core.Entities;

public class Marina : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal DefaultPricePerDay { get; set; }
    public bool IsActive { get; set; } = true;
    public int LayoutWidth { get; set; } = 1200;
    public int LayoutHeight { get; set; } = 800;
    public string? BackgroundImagePath { get; set; }

    public ICollection<Spot> Spots { get; set; } = [];
    public ICollection<MarinaAdmin> Admins { get; set; } = [];
}
