using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

    public AdminController(
        UserManager<ApplicationUser> userManager,
        IBookingRepository bookingRepository,
        IMarinaRepository marinaRepository,
        IMarinaAdminRepository marinaAdminRepository,
        ISpotRepository spotRepository,
        IInvitationRepository invitationRepository)
    {
        _userManager = userManager;
        _bookingRepository = bookingRepository;
        _marinaRepository = marinaRepository;
        _marinaAdminRepository = marinaAdminRepository;
        _spotRepository = spotRepository;
        _invitationRepository = invitationRepository;
    }

    [HttpGet("dashboard")]
    public IActionResult Dashboard()
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
    public async Task<IActionResult> AllMarinas()
    {
        var marinas = await _marinaRepository.GetAllAsync(includeInactive: true);
        var viewModels = new List<AdminMarinaListItemViewModel>();

        foreach (var m in marinas)
        {
            var adminCount = (await _marinaAdminRepository.GetByMarinaIdAsync(m.Id)).Count;
            var spotCount = (await _spotRepository.GetByMarinaIdAsync(m.Id, includeInactive: true)).Count;
            var vm = new AdminMarinaListItemViewModel
            {
                Id = m.Id,
                Name = m.Name,
                Region = m.Region,
                IsActive = m.IsActive,
                AdminCount = adminCount,
                SpotCount = spotCount,
            };
            viewModels.Add(vm);
        }

        var ordered = viewModels.OrderBy(m => m.Name).ToList();
        return View(ordered);
    }

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
            };
            viewModels.Add(vm);
        }

        var ordered = viewModels.OrderBy(a => a.InvitedAt).ToList();
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
}
