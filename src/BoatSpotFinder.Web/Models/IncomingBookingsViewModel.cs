namespace BoatSpotFinder.Web.Models;

public class IncomingBookingsViewModel
{
    public List<BookingListItemViewModel> Pending { get; set; } = [];
    public List<BookingListItemViewModel> Confirmed { get; set; } = [];
    public List<BookingListItemViewModel> Past { get; set; } = [];
}
