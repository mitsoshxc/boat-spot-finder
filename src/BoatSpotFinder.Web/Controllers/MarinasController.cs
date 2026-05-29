using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "PlaceOwner")]
[Route("placeowner/marinas")]
public class MarinasController : Controller
{
    private readonly IMarinaRepository _marinaRepository;
    private readonly IMarinaAdminRepository _marinaAdminRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditLogger _auditLogger;
    private readonly IMarinaSearchService _marinaSearchService;

    public MarinasController(
        IMarinaRepository marinaRepository,
        IMarinaAdminRepository marinaAdminRepository,
        IFileStorageService fileStorageService,
        IAuditLogger auditLogger,
        IMarinaSearchService marinaSearchService)
    {
        _marinaRepository = marinaRepository;
        _marinaAdminRepository = marinaAdminRepository;
        _fileStorageService = fileStorageService;
        _auditLogger = auditLogger;
        _marinaSearchService = marinaSearchService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var marinas = await _marinaRepository.GetByUserIdAsync(userId);

        var list = marinas
            .Select(m => new MarinaListItemViewModel
            {
                Id = m.Id,
                Name = m.Name,
                Region = m.Region,
                IsActive = m.IsActive,
                SpotCount = m.Spots.Count
            })
            .ToList();

        return View(list);
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(id, userId))
        {
            return Forbid();
        }

        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null)
        {
            return NotFound();
        }

        var model = new MarinaEditViewModel
        {
            Id = marina.Id,
            Name = marina.Name,
            Description = marina.Description,
            Address = marina.Address,
            Region = marina.Region,
            Phone = marina.Phone,
            Latitude = marina.Latitude,
            Longitude = marina.Longitude,
            DefaultPricePerDay = marina.DefaultPricePerDay
        };

        return View(model);
    }

    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, MarinaEditViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(id, userId))
        {
            return Forbid();
        }

        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null)
        {
            return NotFound();
        }

        marina.UpdateDetails(
            model.Name,
            model.Description,
            model.Address,
            model.Region,
            model.Phone,
            model.Latitude,
            model.Longitude,
            model.DefaultPricePerDay,
            marina.LayoutWidth,
            marina.LayoutHeight);

        await _marinaRepository.UpdateAsync(marina);

        if (marina.IsActive)
        {
            await _marinaSearchService.IndexAsync(marina);
        }

        _auditLogger.Log(
            userId: userId,
            userEmail: User.Identity!.Name!,
            action: "MarinaEdited",
            entityType: "Marina",
            entityId: marina.Id.ToString(),
            marinaId: marina.Id.ToString(),
            details: null);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/layout")]
    public async Task<IActionResult> Layout(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(id, userId))
        {
            return Forbid();
        }

        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null)
        {
            return NotFound();
        }

        var spots = marina.Spots
            .OrderBy(s => s.Name)
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
            .ToList();

        var viewModel = new MarinaLayoutViewModel
        {
            Id = marina.Id,
            Name = marina.Name,
            LayoutWidth = marina.LayoutWidth,
            LayoutHeight = marina.LayoutHeight,
            BackgroundImagePath = marina.BackgroundImagePath,
            Spots = spots
        };

        return View(viewModel);
    }

    [HttpPost("{id:guid}/background")]
    public async Task<IActionResult> UploadBackground(Guid id, IFormFile backgroundImage)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(id, userId))
        {
            return Forbid();
        }

        if (backgroundImage == null || backgroundImage.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedMimeTypes.Contains(backgroundImage.ContentType))
        {
            return BadRequest("Invalid file type.");
        }

        var ext = Path.GetExtension(backgroundImage.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest("Invalid file type.");
        }

        if (backgroundImage.Length > 5 * 1024 * 1024)
        {
            return BadRequest("File exceeds 5 MB.");
        }

        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(marina.BackgroundImagePath))
        {
            await _fileStorageService.DeleteAsync(marina.BackgroundImagePath);
        }

        var saved = await _fileStorageService.SaveAsync(
            backgroundImage.OpenReadStream(),
            $"marina-backgrounds/{id}{ext}",
            backgroundImage.ContentType);

        marina.SetBackgroundImage(saved);
        await _marinaRepository.UpdateAsync(marina);

        return RedirectToAction(nameof(Layout), new { id });
    }

    [HttpPost("{id:guid}/background/clear")]
    public async Task<IActionResult> ClearBackground(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await _marinaAdminRepository.ExistsAsync(id, userId))
        {
            return Forbid();
        }

        var marina = await _marinaRepository.GetByIdAsync(id);
        if (marina is null)
        {
            return NotFound();
        }

        var isJson = Request.Headers.Accept.ToString().Contains("application/json");

        if (!string.IsNullOrEmpty(marina.BackgroundImagePath))
        {
            await _fileStorageService.DeleteAsync(marina.BackgroundImagePath);
            marina.ClearBackgroundImage();
            await _marinaRepository.UpdateAsync(marina);
        }

        if (isJson)
        {
            return Json(new { ok = true });
        }

        return RedirectToAction(nameof(Layout), new { id });
    }
}
