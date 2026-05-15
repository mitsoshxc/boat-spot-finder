using System.ComponentModel.DataAnnotations;

namespace BoatSpotFinder.Web.Models;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
