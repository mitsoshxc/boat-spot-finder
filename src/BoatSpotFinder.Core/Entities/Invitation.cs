namespace BoatSpotFinder.Core.Entities;

public class Invitation : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public Guid MarinaId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public string InvitedById { get; set; } = string.Empty;

    public Marina Marina { get; set; } = null!;
    public ApplicationUser InvitedBy { get; set; } = null!;
}
