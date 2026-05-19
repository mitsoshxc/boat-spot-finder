using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface ISpotSeasonalRuleRepository
{
    Task<SpotSeasonalRule?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<SpotSeasonalRule>> GetBySpotIdAsync(Guid spotId);
    Task AddAsync(SpotSeasonalRule rule);
    Task UpdateAsync(SpotSeasonalRule rule);
    Task DeleteAsync(Guid id);
}
