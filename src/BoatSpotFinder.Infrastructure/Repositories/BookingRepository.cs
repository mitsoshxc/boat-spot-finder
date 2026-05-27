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

    public async Task<IEnumerable<Booking>> GetByVesselIdAsync(Guid vesselId)
    {
        return await _context.Bookings.Where(b => b.VesselId == vesselId).ToListAsync();
    }
}
