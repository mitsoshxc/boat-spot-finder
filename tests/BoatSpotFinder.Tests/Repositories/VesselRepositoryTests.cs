using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;

namespace BoatSpotFinder.Tests.Repositories;

public class VesselRepositoryTests
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

    [Fact]
    public async Task GetByOwnerIdAsync_ReturnsOnlyOwnersVessels()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner1@test.com");
        await SeedUserAsync(db.Context, "owner-2", "owner2@test.com");
        await db.Context.SaveChangesAsync();

        var vesselOwner1 = new Vessel("Boat A", VesselType.SailBoat, 10, 4, 2, "owner-1");
        var vesselOwner2 = new Vessel("Boat B", VesselType.MotorBoat, 8, 3, 2, "owner-2");
        await db.Context.Vessels.AddRangeAsync(vesselOwner1, vesselOwner2);
        await db.Context.SaveChangesAsync();

        var repo = new VesselRepository(db.Context);
        var results = (await repo.GetByOwnerIdAsync("owner-1")).ToList();

        Assert.Single(results);
        Assert.Equal(vesselOwner1.Id, results[0].Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissing()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var repo = new VesselRepository(db.Context);
        var result = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesVessel()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedUserAsync(db.Context, "owner-1", "owner1@test.com");
        await db.Context.SaveChangesAsync();

        var vessel = new Vessel("Boat A", VesselType.SailBoat, 10, 4, 2, "owner-1");
        await db.Context.Vessels.AddAsync(vessel);
        await db.Context.SaveChangesAsync();

        var repo = new VesselRepository(db.Context);
        await repo.DeleteAsync(vessel);

        var found = await repo.GetByIdAsync(vessel.Id);
        Assert.Null(found);
    }
}
