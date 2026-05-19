using BoatSpotFinder.Core.Common;
using BoatSpotFinder.Core.Models;

namespace BoatSpotFinder.Core.Interfaces;

public interface ISpotSeasonalRuleService
{
    Task<ServiceResult> CreateAsync(Guid spotId, SpotSeasonalRuleInput input);
    Task<ServiceResult> UpdateAsync(Guid ruleId, SpotSeasonalRuleInput input);
    Task DeleteAsync(Guid ruleId);
}
