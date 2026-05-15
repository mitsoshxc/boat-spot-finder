namespace BoatSpotFinder.Core.Entities;

public class MarinaAdmin
{
    public Guid MarinaId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset InvitedAt { get; set; }
    public string InvitedById { get; set; } = string.Empty;

    public Marina Marina { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser InvitedBy { get; set; } = null!;
}
