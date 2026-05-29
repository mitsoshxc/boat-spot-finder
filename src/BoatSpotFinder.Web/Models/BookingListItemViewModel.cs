using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Web.Models;

public class BookingListItemViewModel
{
    public Guid Id { get; set; }
    public string SpotName { get; set; } = string.Empty;
    public string MarinaName { get; set; } = string.Empty;
    public string VesselName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; }
    public string BoatOwnerName { get; set; } = string.Empty;
    public DateTime BookingCreatedAt { get; set; }
    public DateTime? AutoActionDeadline { get; set; }
    public bool CanCurrentUserReview { get; set; }
    public DateOnly? ReviewDeadline { get; set; }
    public int? CurrentUserScore { get; set; }
    public decimal? BoatOwnerAverageRating { get; set; }
    public int BoatOwnerReviewCount { get; set; }
}
