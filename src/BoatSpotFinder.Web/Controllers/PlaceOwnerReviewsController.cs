using System.Security.Claims;
using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "PlaceOwner")]
[Route("placeowner/reviews")]
public class PlaceOwnerReviewsController : Controller
{
    private readonly IReviewService _reviewService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IMarinaRepository _marinaRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlaceOwnerReviewsController(
        IReviewService reviewService,
        IBookingRepository bookingRepository,
        IMarinaRepository marinaRepository,
        UserManager<ApplicationUser> userManager)
    {
        _reviewService = reviewService;
        _bookingRepository = bookingRepository;
        _marinaRepository = marinaRepository;
        _userManager = userManager;
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(Guid bookingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var eligibility = await _reviewService.CanReviewAsync(bookingId, userId);
        if (!eligibility.IsEligible)
        {
            return NotFound();
        }

        var model = await BuildViewModelAsync(bookingId, eligibility.Role!.Value);
        if (model == null)
        {
            return NotFound();
        }

        return View("~/Views/Reviews/Create.cshtml", model);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(ReviewCreateViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (!ModelState.IsValid)
        {
            await RepopulateSummaryAsync(model);
            return View("~/Views/Reviews/Create.cshtml", model);
        }

        var result = await _reviewService.CreateReviewAsync(model.BookingId, userId, model.Score, model.Comment);
        if (!result.Success)
        {
            foreach (var err in result.Errors)
            {
                ModelState.AddModelError("", err);
            }
            await RepopulateSummaryAsync(model);
            return View("~/Views/Reviews/Create.cshtml", model);
        }

        TempData["Success"] = "Thank you — your review has been recorded.";
        return RedirectToAction("Incoming", "SpotBookings");
    }

    private async Task<ReviewCreateViewModel?> BuildViewModelAsync(Guid bookingId, ReviewerRole role)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
        {
            return null;
        }
        var marina = await _marinaRepository.GetByIdAsync(booking.Spot.MarinaId);
        if (marina == null)
        {
            return null;
        }
        var owner = await _userManager.FindByIdAsync(booking.BoatOwnerId);

        return new ReviewCreateViewModel
        {
            BookingId = bookingId,
            ReviewerRole = role,
            MarinaName = marina.Name,
            SpotName = booking.Spot.Name,
            OtherPartyName = owner?.UserName ?? owner?.Email ?? "",
            StartDate = booking.StartDate,
            EndDate = booking.EndDate,
            TotalPrice = booking.TotalPrice,
            ReviewDeadline = booking.EndDate.AddDays(14),
        };
    }

    private async Task RepopulateSummaryAsync(ReviewCreateViewModel model)
    {
        var rebuilt = await BuildViewModelAsync(model.BookingId, ReviewerRole.PlaceOwner);
        if (rebuilt == null)
        {
            return;
        }
        model.ReviewerRole = rebuilt.ReviewerRole;
        model.MarinaName = rebuilt.MarinaName;
        model.SpotName = rebuilt.SpotName;
        model.OtherPartyName = rebuilt.OtherPartyName;
        model.StartDate = rebuilt.StartDate;
        model.EndDate = rebuilt.EndDate;
        model.TotalPrice = rebuilt.TotalPrice;
        model.ReviewDeadline = rebuilt.ReviewDeadline;
    }
}
