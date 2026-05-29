using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Models;

public record ReviewEligibility(bool IsEligible, ReviewerRole? Role, string? ErrorReason);
