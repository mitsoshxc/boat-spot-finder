using BoatSpotFinder.Core.Common;
using BoatSpotFinder.Core.Models;

namespace BoatSpotFinder.Core.Interfaces;

public interface IBookingService
{
    Task<ServiceResult<Guid>> CreateAsync(Guid spotId, Guid vesselId, string boatOwnerId, DateOnly start, DateOnly end);
    Task<PricePreview> PreviewPriceAsync(Guid spotId, Guid vesselId, DateOnly start, DateOnly end);
    Task<ServiceResult> CancelAsync(Guid bookingId, string cancellerUserId);
    Task<ServiceResult> ConfirmAsync(Guid bookingId, string performerUserId);
    Task<ServiceResult> RejectAsync(Guid bookingId, string performerUserId);
    Task<ServiceResult> DismissAsync(Guid bookingId, string userId);
    Task<ServiceResult> DismissByMarinaAsync(Guid bookingId, string userId);
    Task AutoActionAsync();
    Task CompleteOverdueAsync();
}
