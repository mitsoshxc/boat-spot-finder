using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class MarinaRepository : IMarinaRepository
{
    private readonly AppDbContext _context;

    public MarinaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Marina?> GetByIdAsync(Guid id)
    {
        return await _context.Marinas
            .Include(m => m.Spots).IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IReadOnlyList<Marina>> GetByUserIdAsync(string userId)
    {
        return await _context.Marinas
            .Include(m => m.Spots)
            .Where(m => m.Admins.Any(a => a.UserId == userId))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Marina>> GetAllAsync(bool includeInactive)
    {
        var query = _context.Marinas.AsQueryable();

        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        return await query.ToListAsync();
    }

    public async Task<IReadOnlyList<Marina>> GetActiveWithActiveSpotsAsync(IReadOnlyCollection<Guid>? marinaIds = null)
    {
        var query = _context.Marinas
            .Where(m => m.IsActive && m.Spots.Any());

        if (marinaIds != null)
            query = query.Where(m => marinaIds.Contains(m.Id));

        return await query.ToListAsync();
    }

    public async Task AddAsync(Marina marina)
    {
        await _context.Marinas.AddAsync(marina);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Marina marina)
    {
        _context.Marinas.Update(marina);
        await _context.SaveChangesAsync();
    }
}
