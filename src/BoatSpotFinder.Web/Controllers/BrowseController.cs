using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace BoatSpotFinder.Web.Controllers;

[Route("browse")]
public class BrowseController : Controller
{
    private readonly IMarinaRepository _marinaRepository;
    private readonly ISpotRepository _spotRepository;

    public BrowseController(IMarinaRepository marinaRepository, ISpotRepository spotRepository)
    {
        _marinaRepository = marinaRepository;
        _spotRepository = spotRepository;
    }

    [HttpGet("marina/{id:guid}/layout-data")]
    public async Task<IActionResult> LayoutData(Guid id)
    {
        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null)
            return NotFound();

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
}
