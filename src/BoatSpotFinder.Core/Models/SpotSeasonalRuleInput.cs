namespace BoatSpotFinder.Core.Models;

public record SpotSeasonalRuleInput(
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal PricePerDay,
    int MinBookingDays);
