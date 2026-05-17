namespace BoatSpotFinder.Core.Entities;

public class Booking : BaseEntity
{
    public Guid SpotId { get; init; }
    public Guid? VesselId { get; init; }
    public string BoatOwnerId { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal TotalPrice { get; init; }
    public BookingStatus Status { get; private set; }

    public Spot Spot { get; init; } = null!;
    public Vessel? Vessel { get; init; }
    public ApplicationUser BoatOwner { get; init; } = null!;

    public void Confirm() => Status = BookingStatus.Confirmed;
    public void Cancel() => Status = BookingStatus.Cancelled;
    public void Complete() => Status = BookingStatus.Completed;
}
