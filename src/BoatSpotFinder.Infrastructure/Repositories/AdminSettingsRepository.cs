using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class AdminSettingsRepository : IAdminSettingsRepository
{
    private readonly AppDbContext _context;

    public AdminSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminSettings> GetAsync()
    {
        return await _context.AdminSettings.SingleAsync();
    }

    public async Task UpdateAsync(AdminSettings settings)
    {
        _context.AdminSettings.Update(settings);
        await _context.SaveChangesAsync();
    }
}
