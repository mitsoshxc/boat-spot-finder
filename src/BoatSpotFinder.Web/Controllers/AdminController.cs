using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Helpers;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Settings;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace BoatSpotFinder.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBookingRepository _bookingRepository;
    private readonly IMarinaRepository _marinaRepository;
    private readonly IMarinaAdminRepository _marinaAdminRepository;
    private readonly ISpotRepository _spotRepository;
    private readonly IInvitationRepository _invitationRepository;
    private readonly IAdminSettingsRepository _adminSettingsRepository;
    private readonly IMarinaSearchService _marinaSearchService;
    private readonly IEmailSender _emailSender;
    private readonly IBookingService _bookingService;
    private readonly AppSettings _appSettings;
    private readonly IAuditLogger _auditLogger;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        IBookingRepository bookingRepository,
        IMarinaRepository marinaRepository,
        IMarinaAdminRepository marinaAdminRepository,
        ISpotRepository spotRepository,
        IInvitationRepository invitationRepository,
        IAdminSettingsRepository adminSettingsRepository,
        IMarinaSearchService marinaSearchService,
        IEmailSender emailSender,
        IBookingService bookingService,
        IOptions<AppSettings> appSettings,
        IAuditLogger auditLogger)
    {
        _userManager = userManager;
        _bookingRepository = bookingRepository;
        _marinaRepository = marinaRepository;
        _marinaAdminRepository = marinaAdminRepository;
        _spotRepository = spotRepository;
        _invitationRepository = invitationRepository;
        _adminSettingsRepository = adminSettingsRepository;
        _marinaSearchService = marinaSearchService;
        _emailSender = emailSender;
        _bookingService = bookingService;
        _appSettings = appSettings.Value;
        _auditLogger = auditLogger;
    }

    [HttpGet("dashboard")]
    public IActionResult Dashboard()
    {
        return View();
    }

    [HttpGet("jobs")]
    public IActionResult Jobs()
    {
        return View();
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = _userManager.Users.ToList();
        var viewModels = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var vm = new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? "",
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                AverageRatingAsBoatOwner = roles.Contains("BoatOwner") ? user.AverageRatingAsBoatOwner : null,
            };
            viewModels.Add(vm);
        }

        var ordered = viewModels.OrderBy(u => u.Email).ToList();
        return View(ordered);
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> AllBookings()
    {
        var bookings = await _bookingRepository.GetAllAsync();
        var viewModels = new List<BookingListItemViewModel>();

        foreach (var b in bookings)
        {
            var owner = await _userManager.FindByIdAsync(b.BoatOwnerId);
            var ownerName = owner?.UserName ?? owner?.Email ?? "";
            var vm = new BookingListItemViewModel
            {
                Id = b.Id,
                SpotName = b.Spot.Name,
                MarinaName = b.Spot.Marina.Name,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalPrice = b.TotalPrice,
                Status = b.Status,
                BoatOwnerName = ownerName,
                BookingCreatedAt = b.CreatedAt,
            };
            viewModels.Add(vm);
        }

        var ordered = viewModels.OrderByDescending(vm => vm.BookingCreatedAt).ToList();
        return View(ordered);
    }

    [HttpGet("marinas")]
    public async Task<IActionResult> AllMarinas(string? q)
    {
        var list = await GetMarinaListAsync(q);
        ViewData["MarinaQuery"] = q;
        return View(list);
    }

    [HttpGet("marinas/search")]
    public async Task<IActionResult> SearchMarinas(string? q)
    {
        var list = await GetMarinaListAsync(q);
        return PartialView("_MarinaListRows", list);
    }

    private async Task<List<AdminMarinaListItemViewModel>> GetMarinaListAsync(string? q)
    {
        var all = await _marinaRepository.GetAllAsync(includeInactive: true);

        IEnumerable<Marina> matched;
        if (string.IsNullOrWhiteSpace(q))
        {
            matched = all;
        }
        else
        {
            var term = q.Trim();
            var esIds = await _marinaSearchService.SearchAsync(term);
            if (esIds is null)
            {
                matched = all.Where(m => MatchesTerm(m, term));
            }
            else
            {
                var idSet = esIds.ToHashSet();
                matched = all.Where(m =>
                    (m.IsActive && idSet.Contains(m.Id)) ||
                    (!m.IsActive && MatchesTerm(m, term)));
            }
        }

        var ordered = matched.OrderBy(m => m.Name).ToList();
        var viewModels = new List<AdminMarinaListItemViewModel>();
        foreach (var m in ordered)
        {
            var adminCount = (await _marinaAdminRepository.GetByMarinaIdAsync(m.Id)).Count(a => a.RevokedAt == null);
            var spotCount = (await _spotRepository.GetByMarinaIdAsync(m.Id, includeInactive: true)).Count;
            viewModels.Add(new AdminMarinaListItemViewModel
            {
                Id = m.Id,
                Name = m.Name,
                Region = m.Region,
                IsActive = m.IsActive,
                AdminCount = adminCount,
                SpotCount = spotCount,
            });
        }
        return viewModels;
    }

    private static bool MatchesTerm(Marina m, string term) =>
        m.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        m.Region.Contains(term, StringComparison.OrdinalIgnoreCase);

    [HttpGet("marinas/{marinaId:guid}/spots")]
    public async Task<IActionResult> MarinaSpots(Guid marinaId)
    {
        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina == null)
        {
            return NotFound();
        }

        ViewData["MarinaName"] = marina.Name;

        var spots = await _spotRepository.GetByMarinaIdAsync(marinaId, includeInactive: true);
        var viewModels = new List<SpotListItemViewModel>();

        foreach (var s in spots)
        {
            var vm = new SpotListItemViewModel
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
                IsActive = s.IsActive,
            };
            viewModels.Add(vm);
        }

        var ordered = viewModels.OrderBy(s => s.Name).ToList();
        return View(ordered);
    }

    [HttpGet("marinas/{marinaId:guid}/admins")]
    public async Task<IActionResult> MarinaAdmins(Guid marinaId)
    {
        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina == null)
        {
            return NotFound();
        }

        ViewData["MarinaName"] = marina.Name;
        ViewData["MarinaId"] = marinaId;

        var marinaAdmins = await _marinaAdminRepository.GetByMarinaIdAsync(marinaId);
        var viewModels = new List<MarinaAdminListItemViewModel>();

        foreach (var ma in marinaAdmins)
        {
            var user = await _userManager.FindByIdAsync(ma.UserId);
            var invitedByUser = await _userManager.FindByIdAsync(ma.InvitedById);
            var vm = new MarinaAdminListItemViewModel
            {
                UserId = ma.UserId,
                Email = user?.Email ?? user?.UserName ?? "",
                InvitedAt = ma.InvitedAt,
                InvitedBy = invitedByUser?.Email ?? invitedByUser?.UserName ?? "",
                IsRevoked = ma.IsRevoked,
            };
            viewModels.Add(vm);
        }

        var ordered = viewModels.OrderBy(a => a.IsRevoked).ThenBy(a => a.InvitedAt).ToList();
        return View(ordered);
    }

    [HttpGet("marinas/{marinaId:guid}/invitations")]
    public async Task<IActionResult> MarinaInvitations(Guid marinaId)
    {
        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina == null)
        {
            return NotFound();
        }

        ViewData["MarinaName"] = marina.Name;
        ViewData["MarinaId"] = marinaId;

        var invitations = await _invitationRepository.GetByMarinaIdAsync(marinaId);
        var viewModels = new List<InvitationListItemViewModel>();

        foreach (var inv in invitations)
        {
            string status = inv.IsUsed ? "Accepted" : (inv.ExpiresAt < DateTimeOffset.UtcNow ? "Expired" : "Pending");
            var vm = new InvitationListItemViewModel
            {
                Email = inv.Email,
                InvitedAt = new DateTimeOffset(DateTime.SpecifyKind(inv.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
                ExpiresAt = inv.ExpiresAt,
                IsUsed = inv.IsUsed,
                Status = status,
            };
            viewModels.Add(vm);
        }

        var ordered = viewModels.OrderByDescending(i => i.InvitedAt).ToList();
        return View(ordered);
    }

    [HttpGet("marinas/{marinaId:guid}/layout")]
    public async Task<IActionResult> MarinaLayout(Guid marinaId)
    {
        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina == null)
        {
            return NotFound();
        }

        var spots = await _spotRepository.GetByMarinaIdAsync(marinaId, includeInactive: true);
        var spotViewModels = spots
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

        var vm = new MarinaLayoutViewModel
        {
            Id = marina.Id,
            Name = marina.Name,
            LayoutWidth = marina.LayoutWidth,
            LayoutHeight = marina.LayoutHeight,
            BackgroundImagePath = marina.BackgroundImagePath,
            Spots = spotViewModels
        };

        return View(vm);
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings()
    {
        var settings = await _adminSettingsRepository.GetAsync();
        var vm = new AdminSettingsViewModel
        {
            AutoActionType = settings.AutoActionType,
            AutoActionTimeoutHours = settings.AutoActionTimeoutHours
        };
        return View(vm);
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Settings(AdminSettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var settings = await _adminSettingsRepository.GetAsync();
        settings.UpdateSettings(model.AutoActionType, model.AutoActionTimeoutHours);
        await _adminSettingsRepository.UpdateAsync(settings);
        var settingsUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        _auditLogger.Log(settingsUserId, User.Identity!.Name ?? "", action: "SettingsUpdated", entityType: "AdminSettings", entityId: "10000000-0000-0000-0000-000000000001", marinaId: null, details: null);
        TempData["Success"] = "Settings saved.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpGet("marinas/create")]
    public IActionResult CreateMarina()
    {
        return View(new MarinaCreateViewModel());
    }

    [HttpPost("marinas/create")]
    public async Task<IActionResult> CreateMarina(MarinaCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var marina = new Marina(
            name: model.Name,
            description: "",
            address: "",
            region: model.Region,
            phone: "",
            latitude: 0,
            longitude: 0,
            defaultPricePerDay: 0);

        await _marinaRepository.AddAsync(marina);
        var createMarinaUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        _auditLogger.Log(createMarinaUserId, User.Identity!.Name ?? "", action: "MarinaCreated", entityType: "Marina", entityId: marina.Id.ToString(), marinaId: marina.Id.ToString(), details: new { name = marina.Name });
        return RedirectToAction(nameof(InviteAdmin), new { marinaId = marina.Id });
    }

    [HttpGet("marinas/{marinaId:guid}/edit")]
    public async Task<IActionResult> EditMarina(Guid marinaId)
    {
        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina == null)
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

    [HttpPost("marinas/{marinaId:guid}/edit")]
    public async Task<IActionResult> EditMarina(Guid marinaId, MarinaEditViewModel model)
    {
        if (marinaId != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina == null)
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

        TempData["Success"] = "Marina updated.";
        return RedirectToAction(nameof(EditMarina), new { marinaId = marina.Id });
    }

    [HttpPost("marinas/{marinaId:guid}/toggle-active")]
    public async Task<IActionResult> ToggleMarinaActive(Guid marinaId)
    {
        var marina = await _marinaRepository.GetByIdAsync(marinaId);
        if (marina == null)
        {
            return NotFound();
        }

        if (marina.IsActive)
        {
            marina.Deactivate();
            await _marinaSearchService.DeleteAsync(marina.Id);
        }
        else
        {
            marina.Activate();
            await _marinaSearchService.IndexAsync(marina);
        }

        await _marinaRepository.UpdateAsync(marina);
        var toggleMarinaUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var toggleMarinaAction = marina.IsActive ? "MarinaActivated" : "MarinaDeactivated";
        _auditLogger.Log(toggleMarinaUserId, User.Identity!.Name ?? "", action: toggleMarinaAction, entityType: "Marina", entityId: marinaId.ToString(), marinaId: marinaId.ToString(), details: null);
        return RedirectToAction(nameof(AllMarinas));
    }

    [HttpPost("spots/{spotId:guid}/toggle-active")]
    public async Task<IActionResult> ToggleSpotActive(Guid spotId)
    {
        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot == null)
        {
            return NotFound();
        }

        if (spot.IsActive)
        {
            spot.Deactivate();
        }
        else
        {
            spot.Activate();
        }

        await _spotRepository.UpdateAsync(spot);
        var toggleSpotUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var toggleSpotAction = spot.IsActive ? "SpotActivated" : "SpotDeactivated";
        _auditLogger.Log(toggleSpotUserId, User.Identity!.Name ?? "", action: toggleSpotAction, entityType: "Spot", entityId: spotId.ToString(), marinaId: spot.MarinaId.ToString(), details: null);
        return RedirectToAction(nameof(MarinaSpots), new { marinaId = spot.MarinaId });
    }

    [HttpGet("marinas/{marinaId:guid}/invite")]
    public IActionResult InviteAdmin(Guid marinaId)
    {
        return View(new InviteAdminViewModel { MarinaId = marinaId });
    }

    [HttpPost("marinas/{marinaId:guid}/invite")]
    public async Task<IActionResult> InviteAdmin(Guid marinaId, InviteAdminViewModel model)
    {
        if (marinaId != model.MarinaId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var rawToken = Guid.NewGuid().ToString("N");
        var invitation = new Invitation
        {
            Email = model.Email,
            Token = TokenHasher.Hash(rawToken),
            MarinaId = model.MarinaId,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
            InvitedById = User.FindFirstValue(ClaimTypes.NameIdentifier)!
        };

        await _invitationRepository.AddAsync(invitation);

        var link = $"{_appSettings.BaseUrl}/account/invite-register?token={rawToken}";
        await _emailSender.SendAsync(
            model.Email,
            "You're invited to manage a marina",
            $"You have been invited to manage a marina on Boat Spot Finder. Click <a href='{link}'>here</a> to set up your account. This link expires in 48 hours.");

        var inviteUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        _auditLogger.Log(inviteUserId, User.Identity!.Name ?? "", action: "AdminInvited", entityType: "Invitation", entityId: invitation.Id.ToString(), marinaId: invitation.MarinaId.ToString(), details: new { email = invitation.Email });
        return RedirectToAction(nameof(MarinaInvitations), new { marinaId = model.MarinaId });
    }

    [HttpPost("marinas/{marinaId:guid}/admins/{userId}/revoke")]
    public async Task<IActionResult> RevokeAdmin(Guid marinaId, string userId)
    {
        var admins = await _marinaAdminRepository.GetByMarinaIdAsync(marinaId);
        var record = admins.FirstOrDefault(a => a.UserId == userId);
        if (record == null)
        {
            return NotFound();
        }

        var actingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        record.Revoke();
        await _marinaAdminRepository.UpdateAsync(record);

        var activeRemaining = (await _marinaAdminRepository.GetByUserIdAsync(userId)).Count(a => a.RevokedAt == null);
        if (activeRemaining == 0)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                await _userManager.RemoveFromRoleAsync(user, "PlaceOwner");
            }
        }

        _auditLogger.Log(actingUserId, User.Identity!.Name ?? "", action: "AdminRevoked", entityType: "MarinaAdmin", entityId: userId, marinaId: marinaId.ToString(), details: null);
        return RedirectToAction(nameof(MarinaAdmins), new { marinaId });
    }

    [HttpPost("marinas/{marinaId:guid}/admins/{userId}/reenable")]
    public async Task<IActionResult> ReEnableAdmin(Guid marinaId, string userId)
    {
        var admins = await _marinaAdminRepository.GetByMarinaIdAsync(marinaId);
        var record = admins.FirstOrDefault(a => a.UserId == userId);
        if (record == null)
        {
            return NotFound();
        }

        var actingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        record.Reinstate();
        await _marinaAdminRepository.UpdateAsync(record);

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null && !await _userManager.IsInRoleAsync(user, "PlaceOwner"))
        {
            await _userManager.AddToRoleAsync(user, "PlaceOwner");
        }

        _auditLogger.Log(actingUserId, User.Identity!.Name ?? "", action: "AdminReinstated", entityType: "MarinaAdmin", entityId: userId, marinaId: marinaId.ToString(), details: null);
        return RedirectToAction(nameof(MarinaAdmins), new { marinaId });
    }

    [HttpPost("bookings/{bookingId:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid bookingId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        var previousStatus = booking?.Status;
        var result = await _bookingService.CancelAsync(bookingId, currentUserId);

        if (!result.Success)
        {
            TempData["Error"] = string.Join(" ", result.Errors);
        }
        else
        {
            if (booking != null)
            {
                _auditLogger.Log(currentUserId, User.Identity!.Name ?? "", action: "BookingCancelledByAdmin", entityType: "Booking", entityId: bookingId.ToString(), marinaId: booking.Spot.MarinaId.ToString(), details: new { previousStatus = previousStatus!.Value.ToString() });
            }
            TempData["Success"] = "Booking cancelled.";
        }

        return RedirectToAction(nameof(AllBookings));
    }
}
