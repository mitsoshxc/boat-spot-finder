namespace BoatSpotFinder.Core.Entities;

public class AdminSettings : BaseEntity
{
    private AdminSettings() { }

    public AdminSettings(AutoActionType autoActionType, int autoActionTimeoutHours = 6)
    {
        AutoActionType = autoActionType;
        AutoActionTimeoutHours = autoActionTimeoutHours;
    }

    public AutoActionType AutoActionType { get; private set; }
    public int AutoActionTimeoutHours { get; private set; } = 6;

    public void UpdateSettings(AutoActionType autoActionType, int autoActionTimeoutHours)
    {
        AutoActionType = autoActionType;
        AutoActionTimeoutHours = autoActionTimeoutHours;
    }
}
