using System.Security.Claims;
using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Enums;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "BoatOwner")]
[Route("browse")]
public class BrowseController : Controller
{
    private readonly IMarinaRepository _marinaRepository;
    private readonly ISpotRepository _spotRepository;
    private readonly IMarinaBrowseService _marinaBrowseService;
    private readonly IVesselRepository _vesselRepository;
    private readonly ISpotStatusService _spotStatusService;

    private const int PageSize = 20;

    public BrowseController(
        IMarinaRepository marinaRepository,
        ISpotRepository spotRepository,
        IMarinaBrowseService marinaBrowseService,
        IVesselRepository vesselRepository,
        ISpotStatusService spotStatusService)
    {
        _marinaRepository = marinaRepository;
        _spotRepository = spotRepository;
        _marinaBrowseService = marinaBrowseService;
        _vesselRepository = vesselRepository;
        _spotStatusService = spotStatusService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] MarinaSearchFilterViewModel filter)
    {
        var hasQuery = !string.IsNullOrWhiteSpace(filter.Query);
        var sort = filter.Sort ?? (hasQuery ? MarinaSortOption.Relevance : MarinaSortOption.TopRated);

        var page = await _marinaBrowseService.GetPageAsync(filter.Query, sort, filter.Page, PageSize);

        var cards = page.Items
            .Select(m => new BrowseMarinaCardViewModel
            {
                Id = m.Id,
                Name = m.Name,
                Region = m.Region,
                Description = m.Description,
                SpotCount = m.Spots.Count,
                AverageRating = m.AverageRating,
                ReviewCount = m.ReviewCount
            })
            .ToList();

        return View(new BrowseMarinaListViewModel
        {
            Filter = filter,
            Marinas = cards,
            Sort = sort,
            ShowRelevanceOption = hasQuery,
            Page = page.PageIndex,
            PageSize = PageSize,
            TotalPages = page.TotalPages,
            TotalCount = page.TotalCount,
            HasPreviousPage = page.HasPreviousPage,
            HasNextPage = page.HasNextPage
        });
    }

    [HttpGet("marina/{id:guid}")]
    public async Task<IActionResult> Marina(Guid id)
    {
        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null || !marina.IsActive)
        {
            return NotFound();
        }

        var isBoatOwner = User.Identity?.IsAuthenticated == true && User.IsInRole("BoatOwner");
        IReadOnlyList<VesselOptionViewModel> vessels = [];
        if (isBoatOwner)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var owned = await _vesselRepository.GetByOwnerIdAsync(userId);
            vessels = owned.Select(v => new VesselOptionViewModel(v.Id, v.Name)).ToList();
        }

        return View(new BrowseMarinaDetailsViewModel
        {
            Id = marina.Id,
            Name = marina.Name,
            Region = marina.Region,
            Description = marina.Description,
            LayoutWidth = marina.LayoutWidth,
            LayoutHeight = marina.LayoutHeight,
            BackgroundImagePath = marina.BackgroundImagePath,
            AverageRating = marina.AverageRating,
            ReviewCount = marina.ReviewCount,
            IsBoatOwner = isBoatOwner,
            Vessels = vessels
        });
    }

    [HttpGet("marina/{id:guid}/layout-data")]
    public async Task<IActionResult> LayoutData(Guid id)
    {
        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null)
        {
            return NotFound();
        }

        var spots = await _spotRepository.GetByMarinaIdAsync(id, includeInactive: true);

        var viewModel = new MarinaLayoutViewModel
        {
            Id = marina.Id,
            Name = marina.Name,
            LayoutWidth = marina.LayoutWidth,
            LayoutHeight = marina.LayoutHeight,
            BackgroundImagePath = marina.BackgroundImagePath,
            Spots = spots
                .Select(s => new SpotLayoutItemViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    CanvasX = s.CanvasX,
                    CanvasY = s.CanvasY,
                    CanvasW = s.CanvasW,
                    CanvasH = s.CanvasH,
                    CanvasRotation = s.CanvasRotation,
                    IsActive = s.IsActive
                })
                .ToList()
        };

        return Json(viewModel);
    }

    [HttpGet("marina/{id:guid}/spot-statuses")]
    public async Task<IActionResult> SpotStatuses(Guid id, DateOnly? start, DateOnly? end, Guid? vesselId)
    {
        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null)
        {
            return NotFound();
        }

        var results = await _spotStatusService.GetWithStatusAsync(id, start, end, vesselId);
        var viewModels = results.Select(r => new SpotStatusViewModel
        {
            Id = r.Spot.Id,
            Name = r.Spot.Name,
            Status = r.Status,
            CanvasX = r.Spot.CanvasX,
            CanvasY = r.Spot.CanvasY,
            CanvasW = r.Spot.CanvasW,
            CanvasH = r.Spot.CanvasH,
            Rotation = r.Spot.CanvasRotation,
            PricePerDay = r.Spot.PricePerDay ?? marina.DefaultPricePerDay,
            SpotLengthMeters = r.Spot.LengthMeters,
            SpotWidthMeters = r.Spot.WidthMeters,
            SpotDepthMeters = r.Spot.DepthMeters
        });
        return Json(viewModels);
    }
}
