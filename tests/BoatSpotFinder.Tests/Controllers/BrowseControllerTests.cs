using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Enums;
using BoatSpotFinder.Core.Services;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Infrastructure.Search;
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

        var spotRepo = new SpotRepository(db.Context);
        var bookingRepo = new BookingRepository(db.Context);
        var vesselRepo = new VesselRepository(db.Context);
        var statusService = new SpotStatusService(spotRepo, vesselRepo, bookingRepo);
        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            spotRepo,
            new NullMarinaSearchService(),
            vesselRepo,
            statusService);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await controller.SpotStatuses(marina.Id, today, today.AddDays(1), null);

        var json = Assert.IsType<JsonResult>(result);
        var statuses = Assert.IsAssignableFrom<IEnumerable<SpotStatusViewModel>>(json.Value);
        var dict = statuses.ToDictionary(s => s.Id, s => s.Status);

        Assert.Equal(SpotAvailabilityStatus.Free, dict[active.Id]);
        Assert.Equal(SpotAvailabilityStatus.Booked, dict[booked.Id]);
        Assert.Equal(SpotAvailabilityStatus.Unavailable, dict[inactive.Id]);
    }

    [Fact]
    public async Task SpotStatuses_MarinaNotFound_ReturnsNotFound()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var spotRepo = new SpotRepository(db.Context);
        var bookingRepo = new BookingRepository(db.Context);
        var vesselRepo = new VesselRepository(db.Context);
        var statusService = new SpotStatusService(spotRepo, vesselRepo, bookingRepo);
        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            spotRepo,
            new NullMarinaSearchService(),
            vesselRepo,
            statusService);

        var result = await controller.SpotStatuses(Guid.NewGuid(), null, null, null);

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

        var spotRepo = new SpotRepository(db.Context);
        var bookingRepo = new BookingRepository(db.Context);
        var vesselRepo = new VesselRepository(db.Context);
        var statusService = new SpotStatusService(spotRepo, vesselRepo, bookingRepo);
        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            spotRepo,
            new NullMarinaSearchService(),
            vesselRepo,
            statusService);

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

        var spotRepo = new SpotRepository(db.Context);
        var bookingRepo = new BookingRepository(db.Context);
        var vesselRepo = new VesselRepository(db.Context);
        var statusService = new SpotStatusService(spotRepo, vesselRepo, bookingRepo);
        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            spotRepo,
            new NullMarinaSearchService(),
            vesselRepo,
            statusService);

        var result = await controller.LayoutData(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SpotStatuses_NoDates_BookedSpotShowsFree()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, active, booked, inactive) = await SeedThreeSpotsAsync(db.Context);

        var spotRepo = new SpotRepository(db.Context);
        var bookingRepo = new BookingRepository(db.Context);
        var vesselRepo = new VesselRepository(db.Context);
        var statusService = new SpotStatusService(spotRepo, vesselRepo, bookingRepo);
        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            spotRepo,
            new NullMarinaSearchService(),
            vesselRepo,
            statusService);

        var result = await controller.SpotStatuses(marina.Id, null, null, null);

        var json = Assert.IsType<JsonResult>(result);
        var statuses = Assert.IsAssignableFrom<IEnumerable<SpotStatusViewModel>>(json.Value);
        var dict = statuses.ToDictionary(s => s.Id, s => s.Status);

        Assert.Equal(SpotAvailabilityStatus.Free, dict[booked.Id]);
        Assert.Equal(SpotAvailabilityStatus.Unavailable, dict[inactive.Id]);
    }

    private sealed class EmptyResultMarinaSearchService : BoatSpotFinder.Core.Interfaces.IMarinaSearchService
    {
        public Task IndexAsync(Marina marina) => Task.CompletedTask;
        public Task DeleteAsync(Guid id) => Task.CompletedTask;
        public Task<IEnumerable<Guid>?> SearchAsync(string? query) =>
            Task.FromResult<IEnumerable<Guid>?>(Array.Empty<Guid>());
    }

    [Fact]
    public async Task SpotStatuses_VesselExceedsSpot_ReturnsIncompatible()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        var (marina, active, booked, inactive) = await SeedThreeSpotsAsync(db.Context);

        var vessel = new Vessel("Big Yacht", VesselType.Yacht, 999, 999, 999, "owner-1");
        await db.Context.Vessels.AddAsync(vessel);
        await db.Context.SaveChangesAsync();

        var spotRepo = new SpotRepository(db.Context);
        var bookingRepo = new BookingRepository(db.Context);
        var vesselRepo = new VesselRepository(db.Context);
        var statusService = new SpotStatusService(spotRepo, vesselRepo, bookingRepo);
        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            spotRepo,
            new NullMarinaSearchService(),
            vesselRepo,
            statusService);

        var result = await controller.SpotStatuses(marina.Id, null, null, vessel.Id);

        var json = Assert.IsType<JsonResult>(result);
        var statuses = Assert.IsAssignableFrom<IEnumerable<SpotStatusViewModel>>(json.Value);
        var dict = statuses.ToDictionary(s => s.Id, s => s.Status);

        Assert.Equal(SpotAvailabilityStatus.Incompatible, dict[active.Id]);
    }

    [Fact]
    public async Task Index_BlankQuery_IgnoresEmptyEsResult_ReturnsAllMarinas()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedThreeSpotsAsync(db.Context);

        var spotRepo = new SpotRepository(db.Context);
        var vesselRepo = new VesselRepository(db.Context);
        var statusService = new SpotStatusService(spotRepo, vesselRepo, new BookingRepository(db.Context));
        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            spotRepo,
            new EmptyResultMarinaSearchService(),
            vesselRepo,
            statusService);

        var result = await controller.Index(new MarinaSearchFilterViewModel { Query = null });

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<BrowseMarinaListViewModel>(view.Model);
        Assert.Single(vm.Marinas);
    }

    [Fact]
    public async Task Index_WithSearchTerm_EmptyEsResult_ReturnsNoMarinas()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();
        await SeedThreeSpotsAsync(db.Context);

        var spotRepo = new SpotRepository(db.Context);
        var vesselRepo = new VesselRepository(db.Context);
        var statusService = new SpotStatusService(spotRepo, vesselRepo, new BookingRepository(db.Context));
        var controller = new BrowseController(
            new MarinaRepository(db.Context),
            spotRepo,
            new EmptyResultMarinaSearchService(),
            vesselRepo,
            statusService);

        var result = await controller.Index(new MarinaSearchFilterViewModel { Query = "no-match" });

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<BrowseMarinaListViewModel>(view.Model);
        Assert.Empty(vm.Marinas);
    }
}
