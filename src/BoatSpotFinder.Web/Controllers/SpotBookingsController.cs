using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "PlaceOwner")]
[Route("placeowner/spot-bookings")]
public class SpotBookingsController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IAdminSettingsRepository _adminSettingsRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IReviewRepository _reviewRepository;
    private readonly IAuditLogger _auditLogger;

    public SpotBookingsController(
        IBookingService bookingService,
        IBookingRepository bookingRepository,
        IAdminSettingsRepository adminSettingsRepository,
        UserManager<ApplicationUser> userManager,
        IReviewRepository reviewRepository,
        IAuditLogger auditLogger)
    {
        _bookingService = bookingService;
        _bookingRepository = bookingRepository;
        _adminSettingsRepository = adminSettingsRepository;
        _userManager = userManager;
        _reviewRepository = reviewRepository;
        _auditLogger = auditLogger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Incoming()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bookings = await _bookingRepository.GetByMarinaOwnerIdAsync(userId);
        var settings = await _adminSettingsRepository.GetAsync();

        var viewModels = new List<BookingListItemViewModel>();
        foreach (var b in bookings)
        {
            var owner = await _userManager.FindByIdAsync(b.BoatOwnerId);
            var displayName = owner?.UserName ?? owner?.Email ?? "";

            DateTime? deadline = b.Status == BookingStatus.Pending
                ? b.CreatedAt.AddHours(settings.AutoActionTimeoutHours)
                : (DateTime?)null;

            var vm = new BookingListItemViewModel
            {
                Id = b.Id,
                SpotName = b.Spot.Name,
                MarinaName = b.Spot.Marina.Name,
                VesselName = b.VesselId.HasValue ? b.Vessel!.Name : "Vessel deleted",
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalPrice = b.TotalPrice,
                Status = b.Status,
                BoatOwnerName = displayName,
                BookingCreatedAt = b.CreatedAt,
                AutoActionDeadline = deadline,
                BoatOwnerAverageRating = owner?.AverageRatingAsBoatOwner,
                BoatOwnerReviewCount = owner?.ReviewCountAsBoatOwner ?? 0,
            };

            if (b.Status == BookingStatus.Completed)
            {
                var reviewDeadline = b.EndDate.AddDays(14);
                vm.ReviewDeadline = reviewDeadline;
                var reviews = (await _reviewRepository.GetByBookingIdAsync(b.Id)).ToList();
                var myReview = reviews.FirstOrDefault(r => r.ReviewerRole == ReviewerRole.PlaceOwner);
                vm.CurrentUserScore = myReview?.Score;
                vm.CanCurrentUserReview = myReview == null && DateOnly.FromDateTime(DateTime.UtcNow) <= reviewDeadline;
            }

            viewModels.Add(vm);
        }

        var ordered = viewModels
            .OrderBy(vm => vm.Status == BookingStatus.Pending ? 0 : 1)
            .ThenBy(vm => vm.StartDate)
            .ToList();

        return View(ordered);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _bookingService.ConfirmAsync(id, userId);

        if (!result.Success)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
        }
        else
        {
            var booking = await _bookingRepository.GetByIdAsync(id);
            if (booking != null)
            {
                _auditLogger.Log(userId, User.Identity!.Name ?? "", action: "BookingConfirmed", entityType: "Booking", entityId: id.ToString(), marinaId: booking.Spot.MarinaId.ToString(), details: null);
            }
            TempData["Success"] = "Booking confirmed.";
        }

        return RedirectToAction(nameof(Incoming));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _bookingService.RejectAsync(id, userId);

        if (!result.Success)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
        }
        else
        {
            var booking = await _bookingRepository.GetByIdAsync(id);
            if (booking != null)
            {
                _auditLogger.Log(userId, User.Identity!.Name ?? "", action: "BookingRejected", entityType: "Booking", entityId: id.ToString(), marinaId: booking.Spot.MarinaId.ToString(), details: null);
            }
            TempData["Success"] = "Booking rejected.";
        }

        return RedirectToAction(nameof(Incoming));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _bookingService.CancelAsync(id, userId);

        if (!result.Success)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
        }
        else
        {
            TempData["Success"] = "Booking cancelled.";
        }

        return RedirectToAction(nameof(Incoming));
    }
}
