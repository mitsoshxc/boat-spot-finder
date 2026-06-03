using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "BoatOwner")]
[Route("bookings")]
public class BookingsController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly IBookingRepository _bookingRepository;
    private readonly ISpotRepository _spotRepository;
    private readonly IVesselRepository _vesselRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IAuditLogger _auditLogger;

    public BookingsController(
        IBookingService bookingService,
        IBookingRepository bookingRepository,
        ISpotRepository spotRepository,
        IVesselRepository vesselRepository,
        IReviewRepository reviewRepository,
        IAuditLogger auditLogger)
    {
        _bookingService = bookingService;
        _bookingRepository = bookingRepository;
        _spotRepository = spotRepository;
        _vesselRepository = vesselRepository;
        _reviewRepository = reviewRepository;
        _auditLogger = auditLogger;
    }

    [HttpGet("")]
    public async Task<IActionResult> MyBookings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bookings = (await _bookingRepository.GetByBoatOwnerIdAsync(userId))
            .Where(b => !b.DismissedByOwner)
            .ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var viewModels = new List<BookingListItemViewModel>();
        foreach (var b in bookings)
        {
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
                BookingCreatedAt = b.CreatedAt
            };

            if (b.Status == BookingStatus.Completed)
            {
                var deadline = b.EndDate.AddDays(14);
                vm.ReviewDeadline = deadline;
                var reviews = (await _reviewRepository.GetByBookingIdAsync(b.Id)).ToList();
                var myReview = reviews.FirstOrDefault(r => r.ReviewerRole == ReviewerRole.BoatOwner);
                vm.CurrentUserScore = myReview?.Score;
                vm.CanCurrentUserReview = myReview == null && today <= deadline;
            }

            viewModels.Add(vm);
        }

        var pending = viewModels
            .Where(b => b.Status == BookingStatus.Pending && b.EndDate >= today)
            .OrderBy(b => b.StartDate)
            .ThenBy(b => b.BookingCreatedAt)
            .ToList();

        var confirmed = viewModels
            .Where(b => b.Status == BookingStatus.Confirmed && b.EndDate >= today)
            .OrderBy(b => b.StartDate)
            .ThenBy(b => b.BookingCreatedAt)
            .ToList();

        var past = viewModels
            .Where(b => !pending.Contains(b) && !confirmed.Contains(b))
            .OrderByDescending(b => b.EndDate)
            .ThenByDescending(b => b.BookingCreatedAt)
            .ToList();

        return View(new MyBookingsViewModel { Pending = pending, Confirmed = confirmed, Past = past });
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _bookingService.DismissAsync(id, userId);

        if (!result.Success)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
        }
        else
        {
            TempData["Success"] = "Booking dismissed.";
        }

        return RedirectToAction(nameof(MyBookings));
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(Guid? spotId, DateOnly? startDate, DateOnly? endDate, Guid? vesselId)
    {
        if (spotId == null)
        {
            return NotFound();
        }

        var spot = await _spotRepository.GetActiveByIdAsync(spotId.Value);
        if (spot is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vessels = await _vesselRepository.GetByOwnerIdAsync(userId);
        var vesselList = vessels.ToList();

        if (vesselList.Count == 0)
        {
            TempData["Error"] = "Please register a vessel before making a booking.";
            return RedirectToAction("Create", "Vessels");
        }

        var model = new BookingCreateViewModel
        {
            SpotId = spotId.Value,
            StartDate = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndDate = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
            VesselId = vesselId,
            Vessels = vesselList.Select(v => new SelectListItem(v.Name, v.Id.ToString())).ToList()
        };

        if (vesselId.HasValue && startDate.HasValue && endDate.HasValue)
        {
            var vessel = await _vesselRepository.GetByIdAsync(vesselId.Value);
            if (vessel is not null && vessel.OwnerId == userId)
            {
                model.Preview = await _bookingService.PreviewPriceAsync(
                    spotId.Value, vesselId.Value, startDate.Value, endDate.Value);
            }
        }

        return View(model);
    }

    [HttpGet("preview-price")]
    public async Task<IActionResult> PreviewPrice(Guid spotId, Guid vesselId, DateOnly startDate, DateOnly endDate)
    {
        var spot = await _spotRepository.GetActiveByIdAsync(spotId);
        if (spot is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vessel = await _vesselRepository.GetByIdAsync(vesselId);
        if (vessel is null || vessel.OwnerId != userId)
        {
            return Forbid();
        }

        if (endDate <= startDate)
        {
            return BadRequest();
        }

        var preview = await _bookingService.PreviewPriceAsync(spotId, vesselId, startDate, endDate);
        return Json(new { pricePerDay = preview.PricePerDay, minBookingDays = preview.MinBookingDays, totalPrice = preview.TotalPrice });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(BookingCreateViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (model.VesselId == null)
        {
            ModelState.AddModelError(nameof(model.VesselId), "Please select a vessel.");
        }

        if (!ModelState.IsValid)
        {
            var vessels = await _vesselRepository.GetByOwnerIdAsync(userId);
            model.Vessels = vessels.Select(v => new SelectListItem(v.Name, v.Id.ToString())).ToList();
            model.Preview = null;
            return View(model);
        }

        var result = await _bookingService.CreateAsync(
            model.SpotId, model.VesselId!.Value, userId, model.StartDate, model.EndDate);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }

            var vessels = await _vesselRepository.GetByOwnerIdAsync(userId);
            model.Vessels = vessels.Select(v => new SelectListItem(v.Name, v.Id.ToString())).ToList();
            model.Preview = null;
            return View(model);
        }

        var booking = await _bookingRepository.GetByIdAsync(result.Value);
        if (booking != null)
        {
            _auditLogger.Log(userId, User.Identity!.Name ?? "", action: "BookingCreated", entityType: "Booking", entityId: result.Value.ToString(), marinaId: booking.Spot.MarinaId.ToString(), details: new { spotId = booking.SpotId, startDate = booking.StartDate.ToString("yyyy-MM-dd"), endDate = booking.EndDate.ToString("yyyy-MM-dd"), totalPrice = booking.TotalPrice });
        }

        TempData["Success"] = "Booking request submitted. The marina will review it shortly.";
        return RedirectToAction(nameof(MyBookings));
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
            var cancelledBooking = await _bookingRepository.GetByIdAsync(id);
            if (cancelledBooking != null)
            {
                _auditLogger.Log(userId, User.Identity!.Name ?? "", action: "BookingCancelledByBoatOwner", entityType: "Booking", entityId: id.ToString(), marinaId: cancelledBooking.Spot.MarinaId.ToString(), details: null);
            }
            TempData["Success"] = "Booking cancelled.";
        }

        return RedirectToAction(nameof(MyBookings));
    }
}
