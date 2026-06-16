using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Enums;

namespace BoatSpotFinder.Core.Models;

public record SpotWithStatus(Spot Spot, SpotAvailabilityStatus Status);
