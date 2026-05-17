using System.ComponentModel.DataAnnotations;

namespace BoatSpotFinder.Web.Models;

public record LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; init; }
}
