using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class VesselRepository : IVesselRepository
{
    private readonly AppDbContext _context;

    public VesselRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Vessel?> GetByIdAsync(Guid id)
    {
        return await _context.Vessels.FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<IEnumerable<Vessel>> GetByOwnerIdAsync(string userId)
    {
        return await _context.Vessels
            .Where(v => v.OwnerId == userId)
            .ToListAsync();
    }

    public async Task AddAsync(Vessel vessel)
    {
        await _context.Vessels.AddAsync(vessel);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Vessel vessel)
    {
        _context.Vessels.Update(vessel);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Vessel vessel)
    {
        _context.Vessels.Remove(vessel);
        await _context.SaveChangesAsync();
    }
}
