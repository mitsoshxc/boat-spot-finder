namespace BoatSpotFinder.Core.Entities;

public class Marina : BaseEntity
{
    private Marina() { }

    public Marina(
        string name,
        string description,
        string address,
        string region,
        string phone,
        double latitude,
        double longitude,
        decimal defaultPricePerDay,
        int layoutWidth = 1200,
        int layoutHeight = 800)
    {
        Name = name;
        Description = description;
        Address = address;
        Region = region;
        Phone = phone;
        Latitude = latitude;
        Longitude = longitude;
        DefaultPricePerDay = defaultPricePerDay;
        LayoutWidth = layoutWidth;
        LayoutHeight = layoutHeight;
    }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public decimal DefaultPricePerDay { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int LayoutWidth { get; private set; } = 1200;
    public int LayoutHeight { get; private set; } = 800;
    public string? BackgroundImagePath { get; private set; }
    public decimal? AverageRating { get; set; }
    public int ReviewCount { get; set; }

    public ICollection<Spot> Spots { get; init; } = [];
    public ICollection<MarinaAdmin> Admins { get; init; } = [];

    public void UpdateDetails(
        string name,
        string description,
        string address,
        string region,
        string phone,
        double latitude,
        double longitude,
        decimal defaultPricePerDay,
        int layoutWidth,
        int layoutHeight)
    {
        Name = name;
        Description = description;
        Address = address;
        Region = region;
        Phone = phone;
        Latitude = latitude;
        Longitude = longitude;
        DefaultPricePerDay = defaultPricePerDay;
        LayoutWidth = layoutWidth;
        LayoutHeight = layoutHeight;
    }

    public void SetBackgroundImage(string? path) => BackgroundImagePath = path;
    public void ClearBackgroundImage() => BackgroundImagePath = null;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
