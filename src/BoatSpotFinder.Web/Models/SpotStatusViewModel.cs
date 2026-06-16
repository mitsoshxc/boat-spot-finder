using System.Text.Json.Serialization;
using BoatSpotFinder.Core.Enums;

namespace BoatSpotFinder.Web.Models;

public class SpotStatusViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SpotAvailabilityStatus Status { get; init; }

    public double? CanvasX { get; init; }
    public double? CanvasY { get; init; }
    public double? CanvasW { get; init; }
    public double? CanvasH { get; init; }
    public double? Rotation { get; init; }
    public decimal PricePerDay { get; init; }
    public double SpotLengthMeters { get; init; }
    public double SpotWidthMeters { get; init; }
    public double SpotDepthMeters { get; init; }
}
