using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;

namespace BoatSpotFinder.Tests.Repositories;

public class MarinaAdminRepositoryTests
{
    private static async Task SeedUserAsync(BoatSpotFinder.Infrastructure.Data.AppDbContext context, string userId, string email)
    {
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        await context.Users.AddAsync(user);
    }

    private static async Task<Marina> SeedMarinaAsync(BoatSpotFinder.Infrastructure.Data.AppDbContext context, string name = "Test Marina")
    {
        var marina = new Marina(name, "Desc", "Addr", "Region", "000", 0, 0, 50m);
        await context.Marinas.AddAsync(marina);
        await context.SaveChangesAsync();
        return marina;
    }

    private static MarinaAdmin MakeAdmin(Guid marinaId, string userId, string invitedById = "admin-1") =>
        new()
        {
            MarinaId = marinaId,
            UserId = userId,
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = invitedById
        };

    [Fact]
    public async Task ExistsAsync_Present_True()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "user-1", "user1@test.com");
        await SeedUserAsync(db.Context, "admin-1", "admin@test.com");
        await db.Context.SaveChangesAsync();
        var marina = await SeedMarinaAsync(db.Context);

        await db.Context.MarinaAdmins.AddAsync(MakeAdmin(marina.Id, "user-1"));
        await db.Context.SaveChangesAsync();

        var repo = new MarinaAdminRepository(db.Context);
        var exists = await repo.ExistsAsync(marina.Id, "user-1");

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_Absent_False()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var marina = await SeedMarinaAsync(db.Context);
        // No users or admin rows needed — just checking absence

        var repo = new MarinaAdminRepository(db.Context);
        var exists = await repo.ExistsAsync(marina.Id, "nonexistent-user");

        Assert.False(exists);
    }

    [Fact]
    public async Task GetByUserIdAsync_FiltersByUser()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "user-1", "user1@test.com");
        await SeedUserAsync(db.Context, "user-2", "user2@test.com");
        await SeedUserAsync(db.Context, "admin-1", "admin@test.com");
        await db.Context.SaveChangesAsync();
        var marinaA = await SeedMarinaAsync(db.Context, "Marina A");
        var marinaB = await SeedMarinaAsync(db.Context, "Marina B");

        await db.Context.MarinaAdmins.AddRangeAsync(
            MakeAdmin(marinaA.Id, "user-1"),
            MakeAdmin(marinaB.Id, "user-2"));
        await db.Context.SaveChangesAsync();

        var repo = new MarinaAdminRepository(db.Context);
        var results = await repo.GetByUserIdAsync("user-1");

        Assert.Single(results);
        Assert.Equal(marinaA.Id, results[0].MarinaId);
    }

    [Fact]
    public async Task GetByMarinaIdAsync_FiltersByMarina()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "user-1", "user1@test.com");
        await SeedUserAsync(db.Context, "user-2", "user2@test.com");
        await SeedUserAsync(db.Context, "admin-1", "admin@test.com");
        await db.Context.SaveChangesAsync();
        var marina = await SeedMarinaAsync(db.Context);

        await db.Context.MarinaAdmins.AddRangeAsync(
            MakeAdmin(marina.Id, "user-1"),
            MakeAdmin(marina.Id, "user-2"));
        await db.Context.SaveChangesAsync();

        var repo = new MarinaAdminRepository(db.Context);
        var results = await repo.GetByMarinaIdAsync(marina.Id);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(marina.Id, r.MarinaId));
    }
}
