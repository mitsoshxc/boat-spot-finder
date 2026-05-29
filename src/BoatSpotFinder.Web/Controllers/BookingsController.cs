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

    public BookingsController(
        IBookingService bookingService,
        IBookingRepository bookingRepository,
        ISpotRepository spotRepository,
        IVesselRepository vesselRepository,
        IReviewRepository reviewRepository)
    {
        _bookingService = bookingService;
        _bookingRepository = bookingRepository;
        _spotRepository = spotRepository;
        _vesselRepository = vesselRepository;
        _reviewRepository = reviewRepository;
    }

    [HttpGet("")]
    public async Task<IActionResult> MyBookings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var bookings = (await _bookingRepository.GetByBoatOwnerIdAsync(userId))
            .OrderByDescending(b => b.StartDate)
            .ThenByDescending(b => b.CreatedAt)
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
                Status = b.Status
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

        return View(viewModels);
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
            TempData["Success"] = "Booking cancelled.";
        }

        return RedirectToAction(nameof(MyBookings));
    }
}
