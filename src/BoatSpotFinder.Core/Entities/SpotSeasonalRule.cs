namespace BoatSpotFinder.Core.Entities;

public class SpotSeasonalRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal PricePerDay { get; set; }
    public int MinBookingDays { get; set; }
    public Guid SpotId { get; set; }

    public Spot Spot { get; set; } = null!;
}
