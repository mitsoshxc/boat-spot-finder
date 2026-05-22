using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Models;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class SpotRepository : ISpotRepository
{
    private readonly AppDbContext _context;

    public SpotRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Spot?> GetByIdAsync(Guid id)
    {
        return await _context.Spots
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Spot?> GetActiveByIdAsync(Guid id)
    {
        return await _context.Spots
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IReadOnlyList<Spot>> GetByMarinaIdAsync(Guid marinaId, bool includeInactive)
    {
        var query = _context.Spots.AsQueryable();

        if (includeInactive)
            query = query.IgnoreQueryFilters();

        return await query
            .Where(s => s.MarinaId == marinaId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Spot>> GetAllAsync(bool includeInactive)
    {
        var query = _context.Spots.AsQueryable();

        if (includeInactive)
            query = query.IgnoreQueryFilters();

        return await query.ToListAsync();
    }

    public async Task AddAsync(Spot spot)
    {
        await _context.Spots.AddAsync(spot);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Spot spot)
    {
        _context.Spots.Update(spot);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasBookingsAsync(Guid spotId)
    {
        return await _context.Bookings.AnyAsync(b => b.SpotId == spotId);
    }

    public async Task DeleteAsync(Spot spot)
    {
        _context.Spots.Remove(spot);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePositionsAsync(IEnumerable<SpotPositionUpdate> updates)
    {
        var ids = updates.Select(u => u.Id).ToList();

        var spots = await _context.Spots
            .IgnoreQueryFilters()
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

        var updateMap = updates.ToDictionary(u => u.Id);

        foreach (var spot in spots)
        {
            var u = updateMap[spot.Id];
            spot.UpdateCanvasPosition(u.CanvasX, u.CanvasY, u.CanvasW, u.CanvasH, u.CanvasRotation);
            spot.Activate();
        }

        await _context.SaveChangesAsync();
    }
}
