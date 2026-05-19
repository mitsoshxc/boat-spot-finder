using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Models;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;

namespace BoatSpotFinder.Tests.Repositories;

public class SpotRepositoryTests
{
    private static Marina CreateMarina() => new(
        name: "Test Marina",
        description: "Test",
        address: "1 Harbour Rd",
        region: "South",
        phone: "0000000000",
        latitude: 37.9,
        longitude: 23.7,
        defaultPricePerDay: 50m);

    private static Spot CreateInactiveSpot(Guid marinaId)
    {
        var spot = new Spot(
            name: "Berth 1",
            description: "Test berth",
            lengthMeters: 10,
            widthMeters: 3,
            depthMeters: 2,
            defaultMinBookingDays: 1,
            allowedVesselTypes: VesselType.None,
            marinaId: marinaId);
        spot.Deactivate();
        return spot;
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsInactiveSpot()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var marina = CreateMarina();
        db.Context.Marinas.Add(marina);
        await db.Context.SaveChangesAsync();

        var spot = CreateInactiveSpot(marina.Id);
        db.Context.Spots.Add(spot);
        await db.Context.SaveChangesAsync();

        var repo = new SpotRepository(db.Context);
        var found = await repo.GetByIdAsync(spot.Id);

        Assert.NotNull(found);
        Assert.Equal(spot.Id, found.Id);
    }

    [Fact]
    public async Task GetActiveByIdAsync_ReturnsNullForInactiveSpot()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var marina = CreateMarina();
        db.Context.Marinas.Add(marina);
        await db.Context.SaveChangesAsync();

        var spot = CreateInactiveSpot(marina.Id);
        db.Context.Spots.Add(spot);
        await db.Context.SaveChangesAsync();

        var repo = new SpotRepository(db.Context);
        var found = await repo.GetActiveByIdAsync(spot.Id);

        Assert.Null(found);
    }

    [Fact]
    public async Task UpdatePositionsAsync_WritesAllCanvasFieldsAndActivatesSpot()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var marina = CreateMarina();
        db.Context.Marinas.Add(marina);
        await db.Context.SaveChangesAsync();

        var spot = CreateInactiveSpot(marina.Id);
        db.Context.Spots.Add(spot);
        await db.Context.SaveChangesAsync();

        var repo = new SpotRepository(db.Context);
        var update = new SpotPositionUpdate(spot.Id, CanvasX: 100, CanvasY: 200, CanvasW: 50, CanvasH: 30, CanvasRotation: 45);
        await repo.UpdatePositionsAsync(new[] { update });

        var found = await repo.GetByIdAsync(spot.Id);

        Assert.NotNull(found);
        Assert.Equal(100, found.CanvasX);
        Assert.Equal(200, found.CanvasY);
        Assert.Equal(50, found.CanvasW);
        Assert.Equal(30, found.CanvasH);
        Assert.Equal(45, found.CanvasRotation);
        Assert.True(found.IsActive);
    }
}
