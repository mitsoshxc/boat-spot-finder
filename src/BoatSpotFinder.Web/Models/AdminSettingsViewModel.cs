using System.ComponentModel.DataAnnotations;
using BoatSpotFinder.Core.Entities;

namespace BoatSpotFinder.Web.Models;

public record AdminSettingsViewModel
{
    public AutoActionType AutoActionType { get; init; }

    [Range(1, 168)]
    public int AutoActionTimeoutHours { get; init; }
}
