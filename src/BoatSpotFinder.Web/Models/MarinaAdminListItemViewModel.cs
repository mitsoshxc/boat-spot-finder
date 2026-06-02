namespace BoatSpotFinder.Web.Models;

public record MarinaAdminListItemViewModel
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTimeOffset InvitedAt { get; init; }
    public string InvitedBy { get; init; } = string.Empty;
}
