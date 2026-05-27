using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(Guid id)
    {
        return await _context.Bookings
            .Include(b => b.Spot)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IEnumerable<Booking>> GetByVesselIdAsync(Guid vesselId)
    {
        return await _context.Bookings.Where(b => b.VesselId == vesselId).ToListAsync();
    }

    public async Task<IEnumerable<Booking>> GetByBoatOwnerIdAsync(string userId)
    {
        return await _context.Bookings
            .Include(b => b.Spot)
            .Where(b => b.BoatOwnerId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Booking>> GetBySpotIdAsync(Guid spotId)
    {
        return await _context.Bookings
            .Include(b => b.Spot)
            .Where(b => b.SpotId == spotId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Booking>> GetByMarinaOwnerIdAsync(string userId)
    {
        return await _context.Bookings
            .Include(b => b.Spot)
            .Where(b => _context.MarinaAdmins.Any(ma => ma.MarinaId == b.Spot.MarinaId && ma.UserId == userId))
            .ToListAsync();
    }

    public async Task<IEnumerable<Booking>> GetAllAsync()
    {
        return await _context.Bookings
            .Include(b => b.Spot)
            .ToListAsync();
    }

    public async Task AddAsync(Booking booking)
    {
        await _context.Bookings.AddAsync(booking);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Booking booking)
    {
        _context.Bookings.Update(booking);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsSpotAvailableAsync(Guid spotId, DateOnly start, DateOnly end, Guid? excludeBookingId)
    {
        return !await _context.Bookings.AnyAsync(b =>
            b.SpotId == spotId &&
            b.Status != BookingStatus.Cancelled &&
            (excludeBookingId == null || b.Id != excludeBookingId) &&
            start < b.EndDate &&
            end > b.StartDate);
    }
}
