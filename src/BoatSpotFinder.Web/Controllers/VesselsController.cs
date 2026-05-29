using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "BoatOwner")]
[Route("vessels")]
public class VesselsController : Controller
{
    private readonly IVesselRepository _vesselRepository;
    private readonly IBookingRepository _bookingRepository;

    public VesselsController(IVesselRepository vesselRepository, IBookingRepository bookingRepository)
    {
        _vesselRepository = vesselRepository;
        _bookingRepository = bookingRepository;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vessels = await _vesselRepository.GetByOwnerIdAsync(userId);

        var list = vessels
            .Select(v => new VesselListItemViewModel
            {
                Id = v.Id,
                Name = v.Name,
                Type = v.Type,
                LengthMeters = v.LengthMeters,
                WidthMeters = v.WidthMeters,
                DepthMeters = v.DepthMeters
            })
            .ToList();

        return View(list);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        var model = new VesselCreateViewModel
        {
            VesselTypeOptions = BuildVesselTypeOptions()
        };
        return View(model);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(VesselCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.VesselTypeOptions = BuildVesselTypeOptions();
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vessel = new Vessel(
            model.Name,
            model.Type,
            model.LengthMeters,
            model.WidthMeters,
            model.DepthMeters,
            userId,
            model.Description);

        await _vesselRepository.AddAsync(vessel);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vessel = await _vesselRepository.GetByIdAsync(id);
        if (vessel is null)
        {
            return NotFound();
        }
        if (vessel.OwnerId != userId)
        {
            return Forbid();
        }

        var model = new VesselEditViewModel
        {
            Id = vessel.Id,
            Name = vessel.Name,
            Description = vessel.Description,
            Type = vessel.Type,
            LengthMeters = vessel.LengthMeters,
            WidthMeters = vessel.WidthMeters,
            DepthMeters = vessel.DepthMeters,
            VesselTypeOptions = BuildVesselTypeOptions()
        };

        return View(model);
    }

    [HttpPost("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, VesselEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.VesselTypeOptions = BuildVesselTypeOptions();
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vessel = await _vesselRepository.GetByIdAsync(id);
        if (vessel is null)
        {
            return NotFound();
        }
        if (vessel.OwnerId != userId)
        {
            return Forbid();
        }

        vessel.UpdateDetails(
            model.Name,
            model.Type,
            model.LengthMeters,
            model.WidthMeters,
            model.DepthMeters,
            model.Description);

        await _vesselRepository.UpdateAsync(vessel);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vessel = await _vesselRepository.GetByIdAsync(id);
        if (vessel is null)
        {
            return NotFound();
        }
        if (vessel.OwnerId != userId)
        {
            return Forbid();
        }

        var bookings = await _bookingRepository.GetByVesselIdAsync(vessel.Id);
        var hasActiveBookings = bookings.Any(b =>
            b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

        if (hasActiveBookings)
        {
            TempData["Error"] = "Cannot delete a vessel with active bookings.";
            return RedirectToAction(nameof(Index));
        }

        await _vesselRepository.DeleteAsync(vessel);

        return RedirectToAction(nameof(Index));
    }

    private static List<SelectListItem> BuildVesselTypeOptions() =>
        Enum.GetValues<VesselType>()
            .Where(v => v != VesselType.None)
            .Select(v => new SelectListItem(v.ToString(), ((int)v).ToString()))
            .ToList();
}
