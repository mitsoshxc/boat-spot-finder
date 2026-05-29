namespace BoatSpotFinder.Web.Models;

public class BookingReviewStatusViewModel
{
    public bool CanCurrentUserReview { get; set; }
    public DateOnly ReviewDeadline { get; set; }
    public int? CurrentUserScore { get; set; }
    public int? OtherPartyScore { get; set; }
}
