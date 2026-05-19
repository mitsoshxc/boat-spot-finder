using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class SpotSeasonalRuleRepository : ISpotSeasonalRuleRepository
{
    private readonly AppDbContext _context;

    public SpotSeasonalRuleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SpotSeasonalRule?> GetByIdAsync(Guid id)
    {
        return await _context.SpotSeasonalRules.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IReadOnlyList<SpotSeasonalRule>> GetBySpotIdAsync(Guid spotId)
    {
        return await _context.SpotSeasonalRules
            .Where(r => r.SpotId == spotId)
            .OrderBy(r => r.StartDate)
            .ToListAsync();
    }

    public async Task AddAsync(SpotSeasonalRule rule)
    {
        await _context.SpotSeasonalRules.AddAsync(rule);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SpotSeasonalRule rule)
    {
        _context.SpotSeasonalRules.Update(rule);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var rule = await _context.SpotSeasonalRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rule is null) return;
        _context.SpotSeasonalRules.Remove(rule);
        await _context.SaveChangesAsync();
    }
}
