using System.ComponentModel.DataAnnotations;

namespace BoatSpotFinder.Web.Models;

public class ResendConfirmationViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
