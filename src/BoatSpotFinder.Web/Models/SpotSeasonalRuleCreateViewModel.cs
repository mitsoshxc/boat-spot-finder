using System.ComponentModel.DataAnnotations;

namespace BoatSpotFinder.Web.Models;

public record SpotSeasonalRuleCreateViewModel
{
    public Guid SpotId { get; init; }

    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; init; }

    [Required]
    public DateOnly EndDate { get; init; }

    [Required, Range(0, 100000)]
    public decimal PricePerDay { get; init; }

    [Required, Range(1, 365)]
    public int MinBookingDays { get; init; } = 1;
}
