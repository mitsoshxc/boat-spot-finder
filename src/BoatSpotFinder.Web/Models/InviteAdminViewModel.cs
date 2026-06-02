using System.ComponentModel.DataAnnotations;

namespace BoatSpotFinder.Web.Models;

public record InviteAdminViewModel
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    public Guid MarinaId { get; init; }
}
