using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Web.Models;

public class ReviewSummaryViewModel
{
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public ReviewerRole ReviewerRole { get; set; }
}
