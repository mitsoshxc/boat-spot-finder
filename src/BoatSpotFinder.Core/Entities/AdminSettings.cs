namespace BoatSpotFinder.Core.Entities;

public class AdminSettings : BaseEntity
{
    public AutoActionType AutoActionType { get; set; }
    public int AutoActionTimeoutHours { get; set; } = 6;
}
