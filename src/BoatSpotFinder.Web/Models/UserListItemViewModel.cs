namespace BoatSpotFinder.Web.Models;

public record UserListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = [];
    public bool IsActive { get; init; }
    public decimal? AverageRatingAsBoatOwner { get; init; }
}
