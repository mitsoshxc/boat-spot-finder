using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class MarinaAdminRepository : IMarinaAdminRepository
{
    private readonly AppDbContext _context;

    public MarinaAdminRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MarinaAdmin>> GetByMarinaIdAsync(Guid marinaId)
    {
        return await _context.MarinaAdmins
            .Where(ma => ma.MarinaId == marinaId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MarinaAdmin>> GetByUserIdAsync(string userId)
    {
        return await _context.MarinaAdmins
            .Where(ma => ma.UserId == userId)
            .ToListAsync();
    }

    public async Task AddAsync(MarinaAdmin marinaAdmin)
    {
        await _context.MarinaAdmins.AddAsync(marinaAdmin);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(MarinaAdmin marinaAdmin)
    {
        _context.MarinaAdmins.Remove(marinaAdmin);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid marinaId, string userId)
    {
        return await _context.MarinaAdmins
            .AnyAsync(ma => ma.MarinaId == marinaId && ma.UserId == userId);
    }
}
