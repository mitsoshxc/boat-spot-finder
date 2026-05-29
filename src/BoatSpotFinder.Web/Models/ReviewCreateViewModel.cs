using System.ComponentModel.DataAnnotations;
using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Web.Models;

public class ReviewCreateViewModel
{
    public Guid BookingId { get; set; }

    [Required(ErrorMessage = "Please select a rating.")]
    [Range(1, 5, ErrorMessage = "Please select a rating from 1 to 5.")]
    public int Score { get; set; }

    [StringLength(2000)]
    public string? Comment { get; set; }

    public ReviewerRole ReviewerRole { get; set; }
    public string MarinaName { get; set; } = string.Empty;
    public string SpotName { get; set; } = string.Empty;
    public string OtherPartyName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public DateOnly ReviewDeadline { get; set; }
}
