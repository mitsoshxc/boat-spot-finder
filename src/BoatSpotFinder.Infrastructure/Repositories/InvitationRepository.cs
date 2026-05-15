using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class InvitationRepository : IInvitationRepository
{
    private readonly AppDbContext _context;

    public InvitationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Invitation invitation)
    {
        await _context.Invitations.AddAsync(invitation);
        await _context.SaveChangesAsync();
    }

    public async Task<Invitation?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.Invitations
            .FirstOrDefaultAsync(i => i.Token == tokenHash);
    }

    public async Task<IReadOnlyList<Invitation>> GetByMarinaIdAsync(Guid marinaId)
    {
        return await _context.Invitations
            .Where(i => i.MarinaId == marinaId)
            .ToListAsync();
    }

    public async Task MarkUsedAsync(Invitation invitation)
    {
        invitation.IsUsed = true;
        await _context.SaveChangesAsync();
    }
}
