using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;

namespace BoatSpotFinder.Tests.Repositories;

public class InvitationRepositoryTests
{
    private static async Task<(TestDb db, Invitation inv)> SeedInvitation(bool used, bool expired)
    {
        var db = TestDbContextFactory.CreateSqliteInMemory();
        var marina = new Marina(
            name: "M", description: "d", address: "a", region: "r", phone: "p",
            latitude: 0, longitude: 0, defaultPricePerDay: 0m);
        db.Context.Marinas.Add(marina);
        await db.Context.SaveChangesAsync();

        var inv = new Invitation
        {
            Email = "x@y.z",
            Token = "HASH",
            MarinaId = marina.Id,
            ExpiresAt = expired
                ? DateTimeOffset.UtcNow.AddDays(-1)
                : DateTimeOffset.UtcNow.AddDays(1),
            InvitedById = "30000000-0000-0000-0000-000000000001"
        };
        if (used) inv.MarkUsed();
        db.Context.Invitations.Add(inv);
        await db.Context.SaveChangesAsync();
        return (db, inv);
    }

    [Fact]
    public async Task GetByTokenHashAsync_ReturnsRowEvenWhenUsed()
    {
        var (db, _) = await SeedInvitation(used: true, expired: false);
        try
        {
            var repo = new InvitationRepository(db.Context);
            var found = await repo.GetByTokenHashAsync("HASH");

            Assert.NotNull(found);
            Assert.True(found.IsUsed);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task GetByTokenHashAsync_ReturnsRowEvenWhenExpired()
    {
        var (db, _) = await SeedInvitation(used: false, expired: true);
        try
        {
            var repo = new InvitationRepository(db.Context);
            var found = await repo.GetByTokenHashAsync("HASH");

            Assert.NotNull(found);
            Assert.True(found.ExpiresAt < DateTimeOffset.UtcNow);
        }
        finally
        {
            db.Dispose();
        }
    }

    [Fact]
    public async Task GetByTokenHashAsync_ReturnsNullForUnknownHash()
    {
        var (db, _) = await SeedInvitation(used: false, expired: false);
        try
        {
            var repo = new InvitationRepository(db.Context);
            var found = await repo.GetByTokenHashAsync("DIFFERENT");

            Assert.Null(found);
        }
        finally
        {
            db.Dispose();
        }
    }
}
