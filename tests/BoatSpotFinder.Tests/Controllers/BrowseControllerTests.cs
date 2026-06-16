using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;
using BoatSpotFinder.Web.Controllers;
using BoatSpotFinder.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace BoatSpotFinder.Tests.Controllers;

public class BrowseControllerTests
{
    private static async Task<(Marina marina, Spot active, Spot booked, Spot inactive)> SeedThreeSpotsAsync(
        BoatSpotFinder.Infrastructure.Data.AppDbContext context)
    {
        var marina = new Marina("Test Marina", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        await context.Marinas.AddAsync(marina);
        await context.SaveChangesAsync();

        var active = new Spot("Active Berth", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);
        active.Activate();

        var booked = new Spot("Booked Berth", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);
        booked.Activate();

        var inactive = new Spot("Inactive Berth", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);

        await context.Spots.AddRangeAsync(active, booked, inactive);

        await context.Users.AddAsync(new ApplicationUser
        {
            Id = "owner-1",
            UserName = "owner@test.com",
            NormalizedUserName = "OWNER@TEST.COM",
            Email = "owner@test.com",
            NormalizedEmail = "OWNER@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var booking = new Booking
        {
            SpotId = booked.Id,
            BoatOwnerId = "owner-1",
            StartDate = today.AddDays(-2),
            EndDate = today.AddDays(2),
            TotalPrice = 100m
        };
        booking.Confirm();
        await context.Bookings.AddAsync(booking);
        await context.SaveChangesAsync();

        return (marina, active, booked, inactive);
    }

    [Fact]
    public async Task SpotStatuses_MapsFreeBookedUnavailable()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, active, booked, inactive) = await SeedThreeSpotsAsync(db.Context);

        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            new SpotRepository(db.Context),
            new BookingRepository(db.Context));

        var result = await controller.SpotStatuses(marina.Id);

        var json = Assert.IsType<JsonResult>(result);
        var statuses = Assert.IsAssignableFrom<IEnumerable<SpotStatusViewModel>>(json.Value);
        var dict = statuses.ToDictionary(s => s.Id, s => s.Status);

        Assert.Equal("Free", dict[active.Id]);
        Assert.Equal("Booked", dict[booked.Id]);
        Assert.Equal("Unavailable", dict[inactive.Id]);
    }

    [Fact]
    public async Task SpotStatuses_MarinaNotFound_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            new SpotRepository(db.Context),
            new BookingRepository(db.Context));

        var result = await controller.SpotStatuses(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task LayoutData_MapsMarinaAndSpots()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var marina = new Marina("Layout Marina", "Desc", "Addr", "Region", "000", 0, 0, 50m);
        await db.Context.Marinas.AddAsync(marina);
        await db.Context.SaveChangesAsync();

        var spot = new Spot("Spot A", "Desc", 20, 10, 5, 1, VesselType.None, marina.Id, 50m);
        spot.Activate();
        spot.UpdateCanvasPosition(10, 20, 30, 40, 0);
        await db.Context.Spots.AddAsync(spot);
        await db.Context.SaveChangesAsync();

        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            new SpotRepository(db.Context),
            new BookingRepository(db.Context));

        var result = await controller.LayoutData(marina.Id);

        var json = Assert.IsType<JsonResult>(result);
        var vm = Assert.IsType<MarinaLayoutViewModel>(json.Value);

        Assert.Equal(marina.Id, vm.Id);
        Assert.Equal("Layout Marina", vm.Name);
        Assert.Equal(marina.LayoutWidth, vm.LayoutWidth);
        Assert.Equal(marina.LayoutHeight, vm.LayoutHeight);
        Assert.Single(vm.Spots);
        Assert.Equal(spot.Id, vm.Spots[0].Id);
        Assert.Equal("Spot A", vm.Spots[0].Name);
    }

    [Fact]
    public async Task LayoutData_MarinaNotFound_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            new SpotRepository(db.Context),
            new BookingRepository(db.Context));

        var result = await controller.LayoutData(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }
}
