namespace BoatSpotFinder.Core.Entities;

public class MarinaAdmin
{
    public Guid MarinaId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public DateTimeOffset InvitedAt { get; init; }
    public string InvitedById { get; init; } = string.Empty;
    public DateTimeOffset? RevokedAt { get; private set; }

    public Marina Marina { get; init; } = null!;
    public ApplicationUser User { get; init; } = null!;
    public ApplicationUser InvitedBy { get; init; } = null!;

    public bool IsRevoked => RevokedAt.HasValue;

    public void Revoke()
    {
        RevokedAt = DateTimeOffset.UtcNow;
    }

    public void Reinstate()
    {
        RevokedAt = null;
    }
}
