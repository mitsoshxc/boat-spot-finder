using Microsoft.AspNetCore.Identity;

namespace BoatSpotFinder.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
    public bool IsSuperAdmin { get; set; }
}
