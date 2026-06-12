using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;

namespace BoatSpotFinder.Tests.Repositories;

public class MarinaRepositoryTests
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

    private static Marina CreateActiveMarina(string name = "Marina A") =>
        new(name, "Desc", "Addr", "Region", "000", 0, 0, 50m);

    private static Spot CreateActiveSpot(Guid marinaId)
    {
        var spot = new Spot("Berth 1", "Desc", 10, 3, 2, 1, VesselType.None, marinaId);
        spot.Activate();
        return spot;
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyMarinasWhereUserIsAdmin()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "user-1", "user1@test.com");
        await SeedUserAsync(db.Context, "admin-1", "admin@test.com");
        await db.Context.SaveChangesAsync();

        var marinaA = CreateActiveMarina("Marina A");
        var marinaB = CreateActiveMarina("Marina B");
        await db.Context.Marinas.AddRangeAsync(marinaA, marinaB);
        await db.Context.SaveChangesAsync();

        await db.Context.MarinaAdmins.AddAsync(new MarinaAdmin
        {
            MarinaId = marinaA.Id,
            UserId = "user-1",
            InvitedAt = DateTimeOffset.UtcNow,
            InvitedById = "admin-1"
        });
        await db.Context.SaveChangesAsync();

        var repo = new MarinaRepository(db.Context);
        var results = await repo.GetByUserIdAsync("user-1");

        Assert.Single(results);
        Assert.Equal(marinaA.Id, results[0].Id);
    }

    [Fact]
    public async Task GetActiveWithActiveSpotsAsync_ExcludesMarinaWithNoSpots()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var marinaWithSpot = CreateActiveMarina("With Spot");
        var marinaNoSpot = CreateActiveMarina("No Spot");
        await db.Context.Marinas.AddRangeAsync(marinaWithSpot, marinaNoSpot);
        await db.Context.SaveChangesAsync();

        await db.Context.Spots.AddAsync(CreateActiveSpot(marinaWithSpot.Id));
        await db.Context.SaveChangesAsync();

        var repo = new MarinaRepository(db.Context);
        var results = await repo.GetActiveWithActiveSpotsAsync();

        Assert.Single(results);
        Assert.Equal(marinaWithSpot.Id, results[0].Id);
    }

    [Fact]
    public async Task GetActiveWithActiveSpotsAsync_ExcludesInactiveMarina()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var activeM = CreateActiveMarina("Active Marina");
        var inactiveM = CreateActiveMarina("Inactive Marina");
        inactiveM.Deactivate();
        await db.Context.Marinas.AddRangeAsync(activeM, inactiveM);
        await db.Context.SaveChangesAsync();

        await db.Context.Spots.AddRangeAsync(
            CreateActiveSpot(activeM.Id),
            CreateActiveSpot(inactiveM.Id));
        await db.Context.SaveChangesAsync();

        var repo = new MarinaRepository(db.Context);
        var results = await repo.GetActiveWithActiveSpotsAsync();

        Assert.Single(results);
        Assert.Equal(activeM.Id, results[0].Id);
    }

    [Fact]
    public async Task GetActiveWithActiveSpotsAsync_AppliesIdFilter()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var marinaA = CreateActiveMarina("Marina A");
        var marinaB = CreateActiveMarina("Marina B");
        var marinaC = CreateActiveMarina("Marina C");
        await db.Context.Marinas.AddRangeAsync(marinaA, marinaB, marinaC);
        await db.Context.SaveChangesAsync();

        await db.Context.Spots.AddRangeAsync(
            CreateActiveSpot(marinaA.Id),
            CreateActiveSpot(marinaB.Id),
            CreateActiveSpot(marinaC.Id));
        await db.Context.SaveChangesAsync();

        var filter = new[] { marinaA.Id, marinaC.Id };
        var repo = new MarinaRepository(db.Context);
        var results = await repo.GetActiveWithActiveSpotsAsync(filter);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Id == marinaA.Id);
        Assert.Contains(results, r => r.Id == marinaC.Id);
    }

    [Fact]
    public async Task GetActiveWithActiveSpotsAsync_ReturnsMarinaWithAtLeastOneActiveSpot()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var marina = CreateActiveMarina();
        await db.Context.Marinas.AddAsync(marina);
        await db.Context.SaveChangesAsync();

        await db.Context.Spots.AddAsync(CreateActiveSpot(marina.Id));
        await db.Context.SaveChangesAsync();

        var repo = new MarinaRepository(db.Context);
        var results = await repo.GetActiveWithActiveSpotsAsync();

        Assert.Single(results);
        Assert.Equal(marina.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_ExcludesRevokedMembership()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "user-1", "user1@test.com");
        await SeedUserAsync(db.Context, "admin-1", "admin@test.com");
        await db.Context.SaveChangesAsync();

        var marinaA = CreateActiveMarina("Marina A");
        var marinaB = CreateActiveMarina("Marina B");
        await db.Context.Marinas.AddRangeAsync(marinaA, marinaB);
        await db.Context.SaveChangesAsync();

        var activeOnA = new MarinaAdmin { MarinaId = marinaA.Id, UserId = "user-1", InvitedAt = DateTimeOffset.UtcNow, InvitedById = "admin-1" };
        var revokedOnB = new MarinaAdmin { MarinaId = marinaB.Id, UserId = "user-1", InvitedAt = DateTimeOffset.UtcNow, InvitedById = "admin-1" };
        revokedOnB.Revoke();
        await db.Context.MarinaAdmins.AddRangeAsync(activeOnA, revokedOnB);
        await db.Context.SaveChangesAsync();

        var repo = new MarinaRepository(db.Context);
        var results = await repo.GetByUserIdAsync("user-1");

        Assert.Single(results);
        Assert.Equal(marinaA.Id, results[0].Id);
    }
}
