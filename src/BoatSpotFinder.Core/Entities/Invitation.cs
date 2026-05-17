namespace BoatSpotFinder.Core.Entities;

public class Invitation : BaseEntity
{
    public string Email { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public Guid MarinaId { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsUsed { get; private set; }
    public string InvitedById { get; init; } = string.Empty;

    public Marina Marina { get; init; } = null!;
    public ApplicationUser InvitedBy { get; init; } = null!;

    public void MarkUsed() => IsUsed = true;
}
