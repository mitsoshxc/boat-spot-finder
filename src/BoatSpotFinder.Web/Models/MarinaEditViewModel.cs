using System.ComponentModel.DataAnnotations;

namespace BoatSpotFinder.Web.Models;

public record MarinaEditViewModel
{
    public Guid Id { get; init; }

    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; init; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string Address { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Region { get; init; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(40)]
    public string Phone { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(0, 1_000_000)]
    [DataType(DataType.Currency)]
    public decimal DefaultPricePerDay { get; init; }
}
