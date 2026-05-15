namespace BoatSpotFinder.Core.Entities;

public class Booking : BaseEntity
{
    public Guid SpotId { get; set; }
    public Guid? VesselId { get; set; }
    public string BoatOwnerId { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; }

    public Spot Spot { get; set; } = null!;
    public Vessel? Vessel { get; set; }
    public ApplicationUser BoatOwner { get; set; } = null!;
}
