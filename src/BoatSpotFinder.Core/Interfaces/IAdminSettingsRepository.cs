using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IAdminSettingsRepository
{
    Task<AdminSettings> GetAsync();
    Task UpdateAsync(AdminSettings settings);
}
