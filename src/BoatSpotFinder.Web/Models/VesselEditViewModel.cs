using System.ComponentModel.DataAnnotations;
using BoatSpotFinder.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BoatSpotFinder.Web.Models;

public record VesselEditViewModel
{
    public Guid Id { get; init; }

    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Required]
    public VesselType Type { get; init; }

    [Range(0, 1000)]
    public double LengthMeters { get; init; }

    [Range(0, 1000)]
    public double WidthMeters { get; init; }

    [Range(0, 100)]
    public double DepthMeters { get; init; }

    public List<SelectListItem> VesselTypeOptions { get; set; } = [];
}
