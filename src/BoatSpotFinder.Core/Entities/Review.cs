namespace BoatSpotFinder.Core.Entities;

public class Review : BaseEntity
{
    public Guid BookingId { get; init; }
    public string ReviewerId { get; init; } = string.Empty;
    public ReviewerRole ReviewerRole { get; init; }
    public int Score { get; init; }
    public string? Comment { get; init; }

    public Booking Booking { get; set; } = null!;
    public ApplicationUser Reviewer { get; set; } = null!;
}
