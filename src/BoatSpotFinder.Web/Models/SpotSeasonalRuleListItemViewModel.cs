namespace BoatSpotFinder.Web.Models;

public record SpotSeasonalRuleListItemViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal PricePerDay { get; init; }
    public int MinBookingDays { get; init; }
}
