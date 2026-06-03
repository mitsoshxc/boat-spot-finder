using BoatSpotFinder.Core.Common;
using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Models;
using BoatSpotFinder.Core.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoatSpotFinder.Core.Services;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IVesselRepository _vesselRepository;
    private readonly ISpotRepository _spotRepository;
    private readonly IMarinaRepository _marinaRepository;
    private readonly IMarinaAdminRepository _marinaAdminRepository;
    private readonly ISpotSeasonalRuleRepository _spotSeasonalRuleRepository;
    private readonly IAdminSettingsRepository _adminSettingsRepository;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppSettings _appSettings;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookingRepository,
        IVesselRepository vesselRepository,
        ISpotRepository spotRepository,
        IMarinaRepository marinaRepository,
        IMarinaAdminRepository marinaAdminRepository,
        ISpotSeasonalRuleRepository spotSeasonalRuleRepository,
        IAdminSettingsRepository adminSettingsRepository,
        IEmailSender emailSender,
        UserManager<ApplicationUser> userManager,
        IOptions<AppSettings> appSettings,
        ILogger<BookingService> logger)
    {
        _bookingRepository = bookingRepository;
        _vesselRepository = vesselRepository;
        _spotRepository = spotRepository;
        _marinaRepository = marinaRepository;
        _marinaAdminRepository = marinaAdminRepository;
        _spotSeasonalRuleRepository = spotSeasonalRuleRepository;
        _adminSettingsRepository = adminSettingsRepository;
        _emailSender = emailSender;
        _userManager = userManager;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<Guid>> CreateAsync(Guid spotId, Guid vesselId, string boatOwnerId, DateOnly start, DateOnly end)
    {
        var vessel = await _vesselRepository.GetByIdAsync(vesselId);
        if (vessel is null)
        {
            return ServiceResult<Guid>.Fail("Vessel not found");
        }

        var spot = await _spotRepository.GetActiveByIdAsync(spotId);
        if (spot is null)
        {
            return ServiceResult<Guid>.Fail("Spot not available");
        }

        var marina = await _marinaRepository.GetByIdAsync(spot.MarinaId);
        if (marina is null)
        {
            return ServiceResult<Guid>.Fail("Marina not found");
        }

        if (spot.AllowedVesselTypes != VesselType.None && (spot.AllowedVesselTypes & vessel.Type) == 0)
        {
            return ServiceResult<Guid>.Fail("Vessel type is not allowed for this spot");
        }

        if (vessel.LengthMeters > spot.LengthMeters || vessel.WidthMeters > spot.WidthMeters || vessel.DepthMeters > spot.DepthMeters)
        {
            return ServiceResult<Guid>.Fail("Vessel dimensions exceed spot limits");
        }

        if (end <= start)
        {
            return ServiceResult<Guid>.Fail("Departure must be after the arrival date.");
        }

        if (!await _bookingRepository.IsSpotAvailableAsync(spotId, start, end, null))
        {
            return ServiceResult<Guid>.Fail("Spot is not available for the selected dates");
        }

        var (resolvedPricePerDay, resolvedMinBookingDays) = await ResolvePricingAsync(spot, marina, start);
        var totalPrice = resolvedPricePerDay * (end.DayNumber - start.DayNumber);

        if ((end.DayNumber - start.DayNumber) < resolvedMinBookingDays)
        {
            return ServiceResult<Guid>.Fail($"Minimum booking duration is {resolvedMinBookingDays} days");
        }

        var booking = new Booking
        {
            SpotId = spotId,
            VesselId = vesselId,
            BoatOwnerId = boatOwnerId,
            StartDate = start,
            EndDate = end,
            TotalPrice = totalPrice
        };

        await _bookingRepository.AddAsync(booking);

        var admins = await _marinaAdminRepository.GetByMarinaIdAsync(spot.MarinaId);
        foreach (var admin in admins)
        {
            var adminUser = await _userManager.FindByIdAsync(admin.UserId);
            if (adminUser?.Email is null)
            {
                continue;
            }
            try
            {
                await _emailSender.SendAsync(
                    adminUser.Email,
                    $"New booking request for {spot.Name}",
                    $"A new booking request has been made for spot <strong>{spot.Name}</strong> at {marina.Name}.<br>" +
                    $"Dates: {start:yyyy-MM-dd} to {end:yyyy-MM-dd}<br>" +
                    $"Total price: {totalPrice:F2}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to marina admin {UserId}", admin.UserId);
            }
        }

        return ServiceResult<Guid>.Ok(booking.Id);
    }

    public async Task<PricePreview> PreviewPriceAsync(Guid spotId, Guid vesselId, DateOnly start, DateOnly end)
    {
        var spot = await _spotRepository.GetActiveByIdAsync(spotId);
        var marina = await _marinaRepository.GetByIdAsync(spot!.MarinaId);

        var (resolvedPricePerDay, resolvedMinBookingDays) = await ResolvePricingAsync(spot, marina!, start);
        var totalPrice = resolvedPricePerDay * (end.DayNumber - start.DayNumber);

        return new PricePreview(resolvedPricePerDay, resolvedMinBookingDays, totalPrice);
    }

    public async Task<ServiceResult> DismissAsync(Guid bookingId, string userId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return ServiceResult.Fail("Booking not found");
        }

        if (booking.BoatOwnerId != userId)
        {
            return ServiceResult.Fail("Forbidden");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (booking.Status != BookingStatus.Cancelled && booking.Status != BookingStatus.Completed && booking.EndDate >= today)
        {
            return ServiceResult.Fail("Only past or cancelled bookings can be dismissed");
        }

        booking.DismissByOwner();
        await _bookingRepository.UpdateAsync(booking);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DismissByMarinaAsync(Guid bookingId, string userId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return ServiceResult.Fail("Booking not found");
        }

        if (!await _marinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, userId))
        {
            return ServiceResult.Fail("Forbidden");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (booking.Status != BookingStatus.Cancelled && booking.Status != BookingStatus.Completed && booking.EndDate >= today)
        {
            return ServiceResult.Fail("Only past or cancelled bookings can be dismissed");
        }

        booking.DismissByMarina();
        await _bookingRepository.UpdateAsync(booking);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelAsync(Guid bookingId, string cancellerUserId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return ServiceResult.Fail("Booking not found");
        }

        string callerRole;
        if (booking.BoatOwnerId == cancellerUserId)
        {
            callerRole = "BoatOwner";
        }
        else if (await _marinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, cancellerUserId))
        {
            callerRole = "PlaceOwner";
        }
        else
        {
            var user = await _userManager.FindByIdAsync(cancellerUserId);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
            {
                callerRole = "Admin";
            }
            else
            {
                return ServiceResult.Fail("Forbidden");
            }
        }

        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
        {
            return ServiceResult.Fail("Only pending or confirmed bookings can be cancelled");
        }

        if (callerRole != "Admin" && booking.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return ServiceResult.Fail("Cannot cancel a booking that has already started");
        }

        booking.Cancel();
        await _bookingRepository.UpdateAsync(booking);

        if (callerRole == "BoatOwner")
        {
            var admins = await _marinaAdminRepository.GetByMarinaIdAsync(booking.Spot.MarinaId);
            foreach (var admin in admins)
            {
                var adminUser = await _userManager.FindByIdAsync(admin.UserId);
                if (adminUser?.Email is null)
                {
                    continue;
                }
                try
                {
                    await _emailSender.SendAsync(
                        adminUser.Email,
                        $"Booking cancelled by guest for {booking.Spot.Name}",
                        $"A booking for spot <strong>{booking.Spot.Name}</strong> has been cancelled by the guest.<br>" +
                        $"Dates: {booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send cancellation email to marina admin {UserId}", admin.UserId);
                }
            }
        }
        else if (callerRole == "PlaceOwner")
        {
            var boatOwner = await _userManager.FindByIdAsync(booking.BoatOwnerId);
            if (boatOwner?.Email is not null)
            {
                try
                {
                    await _emailSender.SendAsync(
                        boatOwner.Email,
                        $"Your booking for {booking.Spot.Name} has been cancelled",
                        $"Your booking for spot <strong>{booking.Spot.Name}</strong> has been cancelled.<br>" +
                        $"Dates: {booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send cancellation email to boat owner {UserId}", booking.BoatOwnerId);
                }
            }
        }
        else
        {
            var boatOwner = await _userManager.FindByIdAsync(booking.BoatOwnerId);
            if (boatOwner?.Email is not null)
            {
                try
                {
                    await _emailSender.SendAsync(
                        boatOwner.Email,
                        $"Your booking for {booking.Spot.Name} has been cancelled by support",
                        $"Your booking for spot <strong>{booking.Spot.Name}</strong> has been cancelled by support.<br>" +
                        $"Dates: {booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}<br>" +
                        "If this was unexpected, please contact support.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send admin cancellation email to boat owner {UserId}", booking.BoatOwnerId);
                }
            }

            var admins = await _marinaAdminRepository.GetByMarinaIdAsync(booking.Spot.MarinaId);
            foreach (var admin in admins)
            {
                var adminUser = await _userManager.FindByIdAsync(admin.UserId);
                if (adminUser?.Email is null)
                {
                    continue;
                }
                try
                {
                    await _emailSender.SendAsync(
                        adminUser.Email,
                        $"Your booking for {booking.Spot.Name} has been cancelled by support",
                        $"A booking for spot <strong>{booking.Spot.Name}</strong> has been cancelled by support.<br>" +
                        $"Dates: {booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}<br>" +
                        "If this was unexpected, please contact support.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send admin cancellation email to marina admin {UserId}", admin.UserId);
                }
            }
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ConfirmAsync(Guid bookingId, string performerUserId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return ServiceResult.Fail("Booking not found");
        }

        if (!await _marinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, performerUserId))
        {
            return ServiceResult.Fail("Forbidden");
        }

        if (booking.Status != BookingStatus.Pending)
        {
            return ServiceResult.Fail("Only pending bookings can be confirmed");
        }

        return await ConfirmCoreAsync(booking);
    }

    public async Task<ServiceResult> RejectAsync(Guid bookingId, string performerUserId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking is null)
        {
            return ServiceResult.Fail("Booking not found");
        }

        if (!await _marinaAdminRepository.ExistsAsync(booking.Spot.MarinaId, performerUserId))
        {
            return ServiceResult.Fail("Forbidden");
        }

        if (booking.Status != BookingStatus.Pending)
        {
            return ServiceResult.Fail("Only pending bookings can be rejected");
        }

        return await RejectCoreAsync(booking);
    }

    public async Task AutoActionAsync()
    {
        var settings = await _adminSettingsRepository.GetAsync();
        var allBookings = await _bookingRepository.GetAllAsync();
        var cutoff = DateTime.UtcNow;

        var timedOut = allBookings
            .Where(b => b.Status == BookingStatus.Pending &&
                        b.CreatedAt.AddHours(settings.AutoActionTimeoutHours) < cutoff)
            .ToList();

        foreach (var booking in timedOut)
        {
            if (settings.AutoActionType == AutoActionType.AutoApprove)
            {
                await ConfirmCoreAsync(booking);
            }
            else
            {
                await RejectCoreAsync(booking);
            }
        }
    }

    public async Task CompleteOverdueAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var allBookings = await _bookingRepository.GetAllAsync();

        var overdue = allBookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.EndDate < today)
            .ToList();

        foreach (var booking in overdue)
        {
            booking.Complete();
            await _bookingRepository.UpdateAsync(booking);

            var boatOwner = await _userManager.FindByIdAsync(booking.BoatOwnerId);
            if (boatOwner?.Email != null)
            {
                try
                {
                    await _emailSender.SendAsync(
                        boatOwner.Email,
                        $"How was your stay at {booking.Spot.Marina.Name}?",
                        $"Your stay at <strong>{booking.Spot.Marina.Name}</strong> ({booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}) is complete.<br>" +
                        $"Share your experience: <a href=\"{_appSettings.BaseUrl}/reviews/create?bookingId={booking.Id}\">{_appSettings.BaseUrl}/reviews/create?bookingId={booking.Id}</a><br>" +
                        $"Review window closes {booking.EndDate.AddDays(14):yyyy-MM-dd}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send review-invite email to boat owner {UserId}", boatOwner.Id);
                }
            }

            var ownerName = boatOwner?.UserName ?? boatOwner?.Email ?? "your guest";
            var admins = await _marinaAdminRepository.GetByMarinaIdAsync(booking.Spot.MarinaId);
            foreach (var admin in admins)
            {
                var adminUser = await _userManager.FindByIdAsync(admin.UserId);
                if (adminUser?.Email != null)
                {
                    try
                    {
                        await _emailSender.SendAsync(
                            adminUser.Email,
                            $"Rate {ownerName} — stay complete at {booking.Spot.Name}",
                            $"<strong>{ownerName}</strong>'s stay at <strong>{booking.Spot.Name}</strong> ({booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}) is complete.<br>" +
                            $"Share your experience: <a href=\"{_appSettings.BaseUrl}/placeowner/reviews/create?bookingId={booking.Id}\">{_appSettings.BaseUrl}/placeowner/reviews/create?bookingId={booking.Id}</a><br>" +
                            $"Review window closes {booking.EndDate.AddDays(14):yyyy-MM-dd}.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send review-invite email to marina admin {UserId}", admin.UserId);
                    }
                }
            }
        }
    }

    private async Task<ServiceResult> ConfirmCoreAsync(Booking booking)
    {
        booking.Confirm();
        await _bookingRepository.UpdateAsync(booking);

        var boatOwner = await _userManager.FindByIdAsync(booking.BoatOwnerId);
        if (boatOwner?.Email is not null)
        {
            try
            {
                await _emailSender.SendAsync(
                    boatOwner.Email,
                    $"Your booking for {booking.Spot.Name} is confirmed",
                    $"Your booking for spot <strong>{booking.Spot.Name}</strong> is confirmed.<br>" +
                    $"Dates: {booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}<br>" +
                    $"Total price: {booking.TotalPrice:F2}<br>" +
                    $"View your bookings: <a href=\"{_appSettings.BaseUrl}/bookings\">{_appSettings.BaseUrl}/bookings</a>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email to boat owner {UserId}", booking.BoatOwnerId);
            }
        }

        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> RejectCoreAsync(Booking booking)
    {
        booking.Cancel();
        await _bookingRepository.UpdateAsync(booking);

        var boatOwner = await _userManager.FindByIdAsync(booking.BoatOwnerId);
        if (boatOwner?.Email is not null)
        {
            try
            {
                await _emailSender.SendAsync(
                    boatOwner.Email,
                    $"Your booking request for {booking.Spot.Name} was not accepted",
                    $"Your booking request for spot <strong>{booking.Spot.Name}</strong> was not accepted.<br>" +
                    $"Dates: {booking.StartDate:yyyy-MM-dd} to {booking.EndDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send rejection email to boat owner {UserId}", booking.BoatOwnerId);
            }
        }

        return ServiceResult.Ok();
    }

    private async Task<(decimal PricePerDay, int MinBookingDays)> ResolvePricingAsync(Spot spot, Marina marina, DateOnly start)
    {
        var rules = await _spotSeasonalRuleRepository.GetBySpotIdAsync(spot.Id);
        var rule = rules
            .Where(r => r.StartDate <= start && start <= r.EndDate)
            .OrderBy(r => r.StartDate)
            .FirstOrDefault();

        decimal pricePerDay;
        int minBookingDays;

        if (rule is not null)
        {
            pricePerDay = rule.PricePerDay;
            minBookingDays = rule.MinBookingDays;
        }
        else
        {
            pricePerDay = spot.PricePerDay ?? marina.DefaultPricePerDay;
            minBookingDays = spot.DefaultMinBookingDays;
        }

        return (pricePerDay, minBookingDays);
    }
}
