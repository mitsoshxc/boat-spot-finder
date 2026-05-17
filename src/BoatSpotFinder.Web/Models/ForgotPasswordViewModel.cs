using System.ComponentModel.DataAnnotations;

namespace BoatSpotFinder.Web.Models;

public record ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}
