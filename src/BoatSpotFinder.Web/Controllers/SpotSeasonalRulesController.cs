using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Models;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "PlaceOwner")]
[Route("placeowner/marinas/{marinaId:guid}/spots/{spotId:guid}/seasonal-rules")]
public class SpotSeasonalRulesController : Controller
{
    private readonly IMarinaAdminRepository _marinaAdminRepository;
    private readonly IMarinaRepository _marinaRepository;
    private readonly ISpotRepository _spotRepository;
    private readonly ISpotSeasonalRuleRepository _ruleRepository;
    private readonly ISpotSeasonalRuleService _ruleService;

    public SpotSeasonalRulesController(
        IMarinaAdminRepository marinaAdminRepository,
        IMarinaRepository marinaRepository,
        ISpotRepository spotRepository,
        ISpotSeasonalRuleRepository ruleRepository,
        ISpotSeasonalRuleService ruleService)
    {
        _marinaAdminRepository = marinaAdminRepository;
        _marinaRepository = marinaRepository;
        _spotRepository = spotRepository;
        _ruleRepository = ruleRepository;
        _ruleService = ruleService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(Guid marinaId, Guid spotId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot is null || spot.MarinaId != marinaId) return Forbid();

        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        var rules = await _ruleRepository.GetBySpotIdAsync(spotId);

        var list = rules.Select(r => new SpotSeasonalRuleListItemViewModel
        {
            Id = r.Id,
            Name = r.Name,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            PricePerDay = r.PricePerDay,
            MinBookingDays = r.MinBookingDays
        });

        ViewData["MarinaName"] = marina?.Name;
        ViewData["SpotName"] = spot.Name;
        ViewData["MarinaId"] = marinaId;
        ViewData["SpotId"] = spotId;

        return View(list);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(Guid marinaId, Guid spotId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot is null || spot.MarinaId != marinaId) return Forbid();

        var marina = await _marinaRepository.GetByIdAsync(marinaId);

        ViewData["MarinaName"] = marina?.Name;
        ViewData["SpotName"] = spot.Name;
        ViewData["MarinaId"] = marinaId;
        ViewData["SpotId"] = spotId;

        return View(new SpotSeasonalRuleCreateViewModel { SpotId = spotId });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(Guid marinaId, Guid spotId, SpotSeasonalRuleCreateViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot is null || spot.MarinaId != marinaId) return Forbid();

        if (!ModelState.IsValid)
        {
            var marina0 = await _marinaRepository.GetByIdAsync(marinaId);
            ViewData["MarinaName"] = marina0?.Name;
            ViewData["SpotName"] = spot.Name;
            ViewData["MarinaId"] = marinaId;
            ViewData["SpotId"] = spotId;
            return View(model);
        }

        var input = new SpotSeasonalRuleInput(model.Name, model.StartDate!.Value, model.EndDate!.Value, model.PricePerDay, model.MinBookingDays);
        var result = await _ruleService.CreateAsync(spotId, input);

        if (!result.Success)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError("", err);

            var marina0 = await _marinaRepository.GetByIdAsync(marinaId);
            ViewData["MarinaName"] = marina0?.Name;
            ViewData["SpotName"] = spot.Name;
            ViewData["MarinaId"] = marinaId;
            ViewData["SpotId"] = spotId;
            return View(model);
        }

        return RedirectToAction(nameof(Index), new { marinaId, spotId });
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid marinaId, Guid spotId, Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot is null || spot.MarinaId != marinaId) return Forbid();

        var rule = await _ruleRepository.GetByIdAsync(id);
        if (rule is null || rule.SpotId != spotId) return NotFound();

        var marina = await _marinaRepository.GetByIdAsync(marinaId);

        var model = new SpotSeasonalRuleEditViewModel
        {
            Id = rule.Id,
            SpotId = rule.SpotId,
            Name = rule.Name,
            StartDate = rule.StartDate,
            EndDate = rule.EndDate,
            PricePerDay = rule.PricePerDay,
            MinBookingDays = rule.MinBookingDays
        };

        ViewData["MarinaName"] = marina?.Name;
        ViewData["SpotName"] = spot.Name;
        ViewData["MarinaId"] = marinaId;
        ViewData["SpotId"] = spotId;

        return View(model);
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid marinaId, Guid spotId, Guid id, SpotSeasonalRuleEditViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot is null || spot.MarinaId != marinaId) return Forbid();

        if (id != model.Id) return BadRequest();

        var rule = await _ruleRepository.GetByIdAsync(id);
        if (rule is null || rule.SpotId != spotId) return NotFound();

        if (!ModelState.IsValid)
        {
            var marina0 = await _marinaRepository.GetByIdAsync(marinaId);
            ViewData["MarinaName"] = marina0?.Name;
            ViewData["SpotName"] = spot.Name;
            ViewData["MarinaId"] = marinaId;
            ViewData["SpotId"] = spotId;
            return View(model);
        }

        var input = new SpotSeasonalRuleInput(model.Name, model.StartDate, model.EndDate, model.PricePerDay, model.MinBookingDays);
        var result = await _ruleService.UpdateAsync(id, input);

        if (!result.Success)
        {
            foreach (var err in result.Errors)
                ModelState.AddModelError("", err);

            var marina0 = await _marinaRepository.GetByIdAsync(marinaId);
            ViewData["MarinaName"] = marina0?.Name;
            ViewData["SpotName"] = spot.Name;
            ViewData["MarinaId"] = marinaId;
            ViewData["SpotId"] = spotId;
            return View(model);
        }

        return RedirectToAction(nameof(Index), new { marinaId, spotId });
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid marinaId, Guid spotId, Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(marinaId, userId)) return Forbid();

        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot is null || spot.MarinaId != marinaId) return Forbid();

        var rule = await _ruleRepository.GetByIdAsync(id);
        if (rule is null || rule.SpotId != spotId) return RedirectToAction(nameof(Index), new { marinaId, spotId });

        await _ruleService.DeleteAsync(id);

        return RedirectToAction(nameof(Index), new { marinaId, spotId });
    }
}
