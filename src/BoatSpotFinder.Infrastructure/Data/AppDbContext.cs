using BoatSpotFinder.Core.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoatSpotFinder.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public DbSet<Marina> Marinas { get; set; }
    public DbSet<MarinaAdmin> MarinaAdmins { get; set; }
    public DbSet<Invitation> Invitations { get; set; }
    public DbSet<Spot> Spots { get; set; }
    public DbSet<SpotSeasonalRule> SpotSeasonalRules { get; set; }
    public DbSet<Vessel> Vessels { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<AdminSettings> AdminSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "20000000-0000-0000-0000-000000000001",
                Name = "Admin",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "50000000-0000-0000-0000-000000000001"
            },
            new IdentityRole
            {
                Id = "20000000-0000-0000-0000-000000000002",
                Name = "PlaceOwner",
                NormalizedName = "PLACEOWNER",
                ConcurrencyStamp = "50000000-0000-0000-0000-000000000002"
            },
            new IdentityRole
            {
                Id = "20000000-0000-0000-0000-000000000003",
                Name = "BoatOwner",
                NormalizedName = "BOATOWNER",
                ConcurrencyStamp = "50000000-0000-0000-0000-000000000003"
            }
        );

        modelBuilder.Entity<ApplicationUser>().HasData(new ApplicationUser
        {
            Id = "30000000-0000-0000-0000-000000000001",
            Email = "admin@boatspotfinder.com",
            NormalizedEmail = "ADMIN@BOATSPOTFINDER.COM",
            UserName = "admin@boatspotfinder.com",
            NormalizedUserName = "ADMIN@BOATSPOTFINDER.COM",
            IsSuperAdmin = true,
            EmailConfirmed = true,
            SecurityStamp = "40000000-0000-0000-0000-000000000001",
            ConcurrencyStamp = "40000000-0000-0000-0000-000000000002",
            PasswordHash = "AQAAAAIAAYagAAAAEOOY9BG1Raqma0RXyzQ+cW9iUzEnENEkUt66zN38FtvsmdwfM7OAQtaSuZVKm2wBRA=="
        });

        modelBuilder.Entity<IdentityUserRole<string>>().HasData(new IdentityUserRole<string>
        {
            UserId = "30000000-0000-0000-0000-000000000001",
            RoleId = "20000000-0000-0000-0000-000000000001"
        });
    }

    private void SetTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = entry.Entity.UpdatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }
}
