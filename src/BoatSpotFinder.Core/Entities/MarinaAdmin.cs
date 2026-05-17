namespace BoatSpotFinder.Core.Entities;

public class MarinaAdmin
{
    public Guid MarinaId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public DateTimeOffset InvitedAt { get; init; }
    public string InvitedById { get; init; } = string.Empty;

    public Marina Marina { get; init; } = null!;
    public ApplicationUser User { get; init; } = null!;
    public ApplicationUser InvitedBy { get; init; } = null!;
}
