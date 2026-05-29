using Microsoft.AspNetCore.Mvc.Rendering;

namespace BoatSpotFinder.Web.Models;

public class BookingCreateViewModel
{
    public Guid SpotId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? VesselId { get; set; }
    public BoatSpotFinder.Core.Models.PricePreview? Preview { get; set; }
    public List<SelectListItem> Vessels { get; set; } = [];
}
