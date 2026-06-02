using System.ComponentModel.DataAnnotations;

namespace BoatSpotFinder.Web.Models;

public record MarinaCreateViewModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Region { get; init; } = string.Empty;
}
