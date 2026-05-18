using System.ComponentModel.DataAnnotations;
using BoatSpotFinder.Core.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BoatSpotFinder.Web.Models;

public record SpotCreateViewModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Range(0, 1000)]
    public double LengthMeters { get; init; }

    [Range(0, 1000)]
    public double WidthMeters { get; init; }

    [Range(0, 100)]
    public double DepthMeters { get; init; }

    [Range(0, 1_000_000)]
    public decimal? PricePerDay { get; init; }

    [Range(1, 365)]
    public int DefaultMinBookingDays { get; init; }

    public List<VesselType> AllowedVesselTypes { get; init; } = [];

    public List<SelectListItem> VesselTypeOptions { get; init; } = [];
}
