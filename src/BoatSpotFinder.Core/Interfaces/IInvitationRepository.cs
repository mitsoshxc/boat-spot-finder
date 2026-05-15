using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Core.Interfaces;

public interface IInvitationRepository
{
    Task AddAsync(Invitation invitation);
    Task<Invitation?> GetByTokenHashAsync(string tokenHash);
    Task<IReadOnlyList<Invitation>> GetByMarinaIdAsync(Guid marinaId);
    Task MarkUsedAsync(Invitation invitation);
}
