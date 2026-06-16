namespace BoatSpotFinder.Web.Models;

public record SpotAvailabilityFilterViewModel
{
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public Guid? VesselId { get; init; }
}
