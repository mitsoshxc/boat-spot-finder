namespace BoatSpotFinder.Web.Models;

public record InvitationListItemViewModel
{
    public string Email { get; init; } = string.Empty;
    public DateTimeOffset InvitedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsUsed { get; init; }
    public string Status { get; init; } = string.Empty;
}
