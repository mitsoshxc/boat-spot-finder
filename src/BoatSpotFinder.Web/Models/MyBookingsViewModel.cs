namespace BoatSpotFinder.Web.Models;

public class MyBookingsViewModel
{
    public List<BookingListItemViewModel> Pending { get; set; } = [];
    public List<BookingListItemViewModel> Confirmed { get; set; } = [];
    public List<BookingListItemViewModel> Past { get; set; } = [];
}
