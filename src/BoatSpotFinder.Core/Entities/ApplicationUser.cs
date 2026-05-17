using Microsoft.AspNetCore.Identity;

namespace BoatSpotFinder.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public bool IsActive { get; private set; } = true;
    public bool IsSuperAdmin { get; init; }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
