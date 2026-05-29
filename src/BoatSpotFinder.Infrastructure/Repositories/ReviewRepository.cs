using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly AppDbContext _context;

    public ReviewRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Review>> GetByBookingIdAsync(Guid bookingId)
    {
        return await _context.Reviews
            .Where(r => r.BookingId == bookingId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetRecentByMarinaIdAsync(Guid marinaId, int count)
    {
        return await _context.Reviews
            .Include(r => r.Booking).ThenInclude(b => b.Spot)
            .Where(r => r.ReviewerRole == ReviewerRole.BoatOwner && r.Booking.Spot.MarinaId == marinaId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetAllByMarinaIdAsync(Guid marinaId)
    {
        return await _context.Reviews
            .Include(r => r.Booking).ThenInclude(b => b.Spot)
            .Where(r => r.ReviewerRole == ReviewerRole.BoatOwner && r.Booking.Spot.MarinaId == marinaId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetAllByBoatOwnerIdAsync(string userId)
    {
        return await _context.Reviews
            .Include(r => r.Booking)
            .Where(r => r.ReviewerRole == ReviewerRole.PlaceOwner && r.Booking.BoatOwnerId == userId)
            .ToListAsync();
    }

    public async Task AddAsync(Review review)
    {
        await _context.Reviews.AddAsync(review);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid bookingId, ReviewerRole reviewerRole)
    {
        return await _context.Reviews.AnyAsync(r => r.BookingId == bookingId && r.ReviewerRole == reviewerRole);
    }
}
