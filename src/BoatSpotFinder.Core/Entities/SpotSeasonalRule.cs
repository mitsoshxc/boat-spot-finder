namespace BoatSpotFinder.Core.Entities;

public class SpotSeasonalRule : BaseEntity
{
    private SpotSeasonalRule() { }

    public SpotSeasonalRule(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        decimal pricePerDay,
        int minBookingDays,
        Guid spotId)
    {
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        PricePerDay = pricePerDay;
        MinBookingDays = minBookingDays;
        SpotId = spotId;
    }

    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public decimal PricePerDay { get; private set; }
    public int MinBookingDays { get; private set; }
    public Guid SpotId { get; init; }

    public Spot Spot { get; init; } = null!;

    public void UpdateDetails(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        decimal pricePerDay,
        int minBookingDays)
    {
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        PricePerDay = pricePerDay;
        MinBookingDays = minBookingDays;
    }
}
