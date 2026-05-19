using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Models;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "PlaceOwner")]
[Route("placeowner/marinas/{marinaId:guid}/spots")]
public class SpotsController : Controller
{
    private readonly ISpotRepository _spotRepository;
    private readonly IMarinaRepository _marinaRepository;
    private readonly IMarinaAdminRepository _marinaAdminRepository;
    private readonly IAuditLogger _auditLogger;

    public SpotsController(
        ISpotRepository spotRepository,
        IMarinaRepository marinaRepository,
        IMarinaAdminRepository marinaAdminRepository,
        IAuditLogger auditLogger)
    {
        _spotRepository = spotRepository;
        _marinaRepository = marinaRepository;
        _marinaAdminRepository = marinaAdminRepository;
        _auditLogger = auditLogger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid marinaId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina is null) return NotFound();

        var spots = await _spotRepository.GetByMarinaIdAsync(marinaId, includeInactive: true);

        var list = spots
            .Select(s => new SpotListItemViewModel
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                LengthMeters = s.LengthMeters,
                WidthMeters = s.WidthMeters,
                DepthMeters = s.DepthMeters,
                PricePerDay = s.PricePerDay,
                DefaultMinBookingDays = s.DefaultMinBookingDays,
                AllowedVesselTypes = s.AllowedVesselTypes,
                IsActive = s.IsActive
            })
            .ToList();

        ViewData["MarinaName"] = marina.Name;
        ViewData["MarinaId"] = marinaId;

        return View(list);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(Guid marinaId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina is null) return NotFound();

        var model = new SpotCreateViewModel
        {
            VesselTypeOptions = BuildVesselTypeOptions()
        };

        ViewData["MarinaName"] = marina.Name;
        ViewData["MarinaId"] = marinaId;

        return View(model);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(Guid marinaId, SpotCreateViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var isJson = Request.Headers.Accept.ToString().Contains("application/json");

        if (!ModelState.IsValid)
        {
            if (isJson) return BadRequest(ModelState);

            var marina0 = await _marinaRepository.GetByIdAsync(marinaId);
            model = model with { VesselTypeOptions = BuildVesselTypeOptions() };
            ViewData["MarinaName"] = marina0?.Name;
            ViewData["MarinaId"] = marinaId;
            return View(model);
        }

        var allowedFlags = model.AllowedVesselTypes.Count > 0
            ? model.AllowedVesselTypes.Aggregate(VesselType.None, (acc, v) => acc | v)
            : VesselType.None;

        var spot = new Spot(
            model.Name,
            model.Description ?? string.Empty,
            model.LengthMeters,
            model.WidthMeters,
            model.DepthMeters,
            model.DefaultMinBookingDays,
            allowedFlags,
            marinaId,
            model.PricePerDay);

        await _spotRepository.AddAsync(spot);

        _auditLogger.Log(
            userId: userId,
            userEmail: User.Identity!.Name!,
            action: "SpotCreated",
            entityType: "Spot",
            entityId: spot.Id.ToString(),
            marinaId: marinaId.ToString(),
            details: new { spotName = spot.Name });

        if (isJson) return Json(new { id = spot.Id, name = spot.Name });

        return RedirectToAction(nameof(Index), new { marinaId });
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid marinaId, Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot is null) return NotFound();
        if (spot.MarinaId != marinaId) return Forbid();

        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina is null) return NotFound();

        var allowedList = Enum.GetValues<VesselType>()
            .Where(v => v != VesselType.None && spot.AllowedVesselTypes.HasFlag(v))
            .ToList();

        var model = new SpotEditViewModel
        {
            Id = spot.Id,
            Name = spot.Name,
            Description = spot.Description,
            LengthMeters = spot.LengthMeters,
            WidthMeters = spot.WidthMeters,
            DepthMeters = spot.DepthMeters,
            PricePerDay = spot.PricePerDay,
            DefaultMinBookingDays = spot.DefaultMinBookingDays,
            AllowedVesselTypes = allowedList,
            VesselTypeOptions = BuildVesselTypeOptions(),
            IsActive = spot.IsActive
        };

        ViewData["MarinaName"] = marina.Name;
        ViewData["MarinaId"] = marinaId;

        return View(model);
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid marinaId, Guid id, SpotEditViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            model = model with { VesselTypeOptions = BuildVesselTypeOptions() };
            var marina0 = await _marinaRepository.GetByIdAsync(marinaId);
            ViewData["MarinaName"] = marina0?.Name;
            ViewData["MarinaId"] = marinaId;
            return View(model);
        }

        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot is null) return NotFound();
        if (spot.MarinaId != marinaId) return Forbid();

        var allowedFlags = model.AllowedVesselTypes.Count > 0
            ? model.AllowedVesselTypes.Aggregate(VesselType.None, (acc, v) => acc | v)
            : VesselType.None;

        spot.UpdateDetails(
            model.Name,
            model.Description ?? string.Empty,
            model.LengthMeters,
            model.WidthMeters,
            model.DepthMeters,
            model.PricePerDay,
            model.DefaultMinBookingDays,
            allowedFlags);

        await _spotRepository.UpdateAsync(spot);

        _auditLogger.Log(
            userId: userId,
            userEmail: User.Identity!.Name!,
            action: "SpotEdited",
            entityType: "Spot",
            entityId: spot.Id.ToString(),
            marinaId: marinaId.ToString(),
            details: null);

        return RedirectToAction(nameof(Index), new { marinaId });
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid marinaId, Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot is null) return NotFound();
        if (spot.MarinaId != marinaId) return Forbid();

        spot.Deactivate();
        await _spotRepository.UpdateAsync(spot);

        _auditLogger.Log(
            userId: userId,
            userEmail: User.Identity!.Name!,
            action: "SpotDeactivated",
            entityType: "Spot",
            entityId: id.ToString(),
            marinaId: marinaId.ToString(),
            details: null);

        return RedirectToAction(nameof(Index), new { marinaId });
    }

    [HttpPost("save-positions")]
    public async Task<IActionResult> SavePositions(Guid marinaId, [FromBody] List<SpotPositionUpdateViewModel> updates)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        if (updates == null) return BadRequest();

        var positions = updates
            .Select(v => new SpotPositionUpdate(v.Id, v.CanvasX, v.CanvasY, v.CanvasW, v.CanvasH, v.CanvasRotation))
            .ToList();

        await _spotRepository.UpdatePositionsAsync(positions);

        return Ok();
    }

    private static List<SelectListItem> BuildVesselTypeOptions() =>
        Enum.GetValues<VesselType>()
            .Where(v => v != VesselType.None)
            .Select(v => new SelectListItem(v.ToString(), ((int)v).ToString()))
            .ToList();
}
