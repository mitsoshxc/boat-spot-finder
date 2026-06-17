using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Enums;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Services;
using NSubstitute;

namespace BoatSpotFinder.Tests.Services;

public class MarinaBrowseServiceTests
{
    private static Marina MakeMarina(string name, decimal? rating = null, Guid? id = null)
    {
        var m = new Marina(name, "Desc", "Addr", "Region", "000", 0, 0, 50m);
        m.AverageRating = rating;
        if (id.HasValue)
        {
            m.Id = id.Value;
        }
        return m;
    }

    private static MarinaBrowseService BuildService(
        IMarinaSearchService? searchService = null,
        IMarinaRepository? marinaRepository = null)
    {
        searchService ??= Substitute.For<IMarinaSearchService>();
        marinaRepository ??= Substitute.For<IMarinaRepository>();
        return new MarinaBrowseService(searchService, marinaRepository);
    }

    [Fact]
    public async Task GetPageAsync_RelevanceWithEsIds_PreservesEsOrderAndTotalCount()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var g3 = Guid.NewGuid();

        var m1 = MakeMarina("Alpha", id: g1);
        var m2 = MakeMarina("Beta", id: g2);
        var m3 = MakeMarina("Gamma", id: g3);

        var searchService = Substitute.For<IMarinaSearchService>();
        // ES ranked order: g3, g1, g2
        searchService.SearchAsync("porto").Returns(Task.FromResult<IEnumerable<Guid>?>(new[] { g3, g1, g2 }));

        var marinaRepository = Substitute.For<IMarinaRepository>();
        // Repo returns in SQL order (different from ES): m1, m2, m3
        marinaRepository
            .GetActiveWithActiveSpotsAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(Task.FromResult<IReadOnlyList<Marina>>(new[] { m1, m2, m3 }));

        var service = BuildService(searchService, marinaRepository);

        var result = await service.GetPageAsync("porto", MarinaSortOption.Relevance, 1, 10);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(g3, result.Items[0].Id);
        Assert.Equal(g1, result.Items[1].Id);
        Assert.Equal(g2, result.Items[2].Id);
    }

    [Fact]
    public async Task GetPageAsync_RelevanceWithEsIds_DropsIdsRemovedBySqlFilter()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var g3 = Guid.NewGuid();

        var m1 = MakeMarina("Alpha", id: g1);
        var m3 = MakeMarina("Gamma", id: g3);

        var searchService = Substitute.For<IMarinaSearchService>();
        // ES returns g1, g2, g3 but g2 is filtered out by the SQL active-spots filter
        searchService.SearchAsync("bay").Returns(Task.FromResult<IEnumerable<Guid>?>(new[] { g1, g2, g3 }));

        var marinaRepository = Substitute.For<IMarinaRepository>();
        // Repo returns only g1 and g3 (g2 was inactive/no spots)
        marinaRepository
            .GetActiveWithActiveSpotsAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(Task.FromResult<IReadOnlyList<Marina>>(new[] { m1, m3 }));

        var service = BuildService(searchService, marinaRepository);

        var result = await service.GetPageAsync("bay", MarinaSortOption.Relevance, 1, 10);

        // TotalCount is the full ES count, not the SQL-filtered count
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(g1, result.Items[0].Id);
        Assert.Equal(g3, result.Items[1].Id);
    }

    [Fact]
    public async Task GetPageAsync_RelevanceWithEsOff_DegradesToTopRatedOverAllActive()
    {
        var searchService = Substitute.For<IMarinaSearchService>();
        // null = ES off
        searchService.SearchAsync(Arg.Any<string?>()).Returns(Task.FromResult<IEnumerable<Guid>?>(null));

        var m1 = MakeMarina("Zeta", rating: 4.5m);
        var m2 = MakeMarina("Alpha", rating: 3.0m);

        var marinaRepository = Substitute.For<IMarinaRepository>();
        marinaRepository
            .GetActiveWithActiveSpotsAsync()
            .Returns(Task.FromResult<IReadOnlyList<Marina>>(new[] { m1, m2 }));

        var service = BuildService(searchService, marinaRepository);

        var result = await service.GetPageAsync("something", MarinaSortOption.Relevance, 1, 20);

        // Called the no-arg overload (all active), not the filtered overload.
        // The interface has one method with an optional param; no-arg call passes null.
        // Assert the non-null (filtered) path was never taken.
        await marinaRepository.Received(1).GetActiveWithActiveSpotsAsync(null);
        await marinaRepository.DidNotReceive().GetActiveWithActiveSpotsAsync(Arg.Is<IReadOnlyCollection<Guid>?>(x => x != null));

        // Should be sorted by rating desc: Zeta (4.5) then Alpha (3.0)
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Zeta", result.Items[0].Name);
        Assert.Equal("Alpha", result.Items[1].Name);
    }

    [Fact]
    public async Task GetPageAsync_TopRated_SortsByRatingDescNullsLastThenName()
    {
        var mHighRated = MakeMarina("Zeta", rating: 4.8m);
        var mLowRated = MakeMarina("Alpha", rating: 2.1m);
        var mNoRating = MakeMarina("Marina");
        var mTieName = MakeMarina("Beta", rating: 4.8m);

        var searchService = Substitute.For<IMarinaSearchService>();
        searchService.SearchAsync(Arg.Any<string?>()).Returns(Task.FromResult<IEnumerable<Guid>?>(null));

        var marinaRepository = Substitute.For<IMarinaRepository>();
        marinaRepository
            .GetActiveWithActiveSpotsAsync()
            .Returns(Task.FromResult<IReadOnlyList<Marina>>(new[] { mNoRating, mLowRated, mHighRated, mTieName }));

        var service = BuildService(searchService, marinaRepository);

        var result = await service.GetPageAsync(null, MarinaSortOption.TopRated, 1, 20);

        Assert.Equal(4, result.Items.Count);
        // 4.8 tie: Beta before Zeta alphabetically
        Assert.Equal("Beta", result.Items[0].Name);
        Assert.Equal("Zeta", result.Items[1].Name);
        Assert.Equal("Alpha", result.Items[2].Name);
        // null rating sorts last
        Assert.Equal("Marina", result.Items[3].Name);
    }

    [Fact]
    public async Task GetPageAsync_Alphabetical_SortsByNameAsc()
    {
        var mC = MakeMarina("Charlie");
        var mA = MakeMarina("Alpha");
        var mB = MakeMarina("Bravo");

        var searchService = Substitute.For<IMarinaSearchService>();
        searchService.SearchAsync(Arg.Any<string?>()).Returns(Task.FromResult<IEnumerable<Guid>?>(null));

        var marinaRepository = Substitute.For<IMarinaRepository>();
        marinaRepository
            .GetActiveWithActiveSpotsAsync()
            .Returns(Task.FromResult<IReadOnlyList<Marina>>(new[] { mC, mA, mB }));

        var service = BuildService(searchService, marinaRepository);

        var result = await service.GetPageAsync(null, MarinaSortOption.Alphabetical, 1, 20);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Alpha", result.Items[0].Name);
        Assert.Equal("Bravo", result.Items[1].Name);
        Assert.Equal("Charlie", result.Items[2].Name);
    }

    [Fact]
    public async Task GetPageAsync_Pagination_PageSizeHonoredAndTotalPagesCorrect()
    {
        // 25 candidates, pageSize 10, page 2 → items 11–20, TotalPages == 3
        var marinas = Enumerable.Range(1, 25)
            .Select(i => MakeMarina($"Marina {i:D2}"))
            .ToList();

        var searchService = Substitute.For<IMarinaSearchService>();
        searchService.SearchAsync(Arg.Any<string?>()).Returns(Task.FromResult<IEnumerable<Guid>?>(null));

        var marinaRepository = Substitute.For<IMarinaRepository>();
        marinaRepository
            .GetActiveWithActiveSpotsAsync()
            .Returns(Task.FromResult<IReadOnlyList<Marina>>(marinas));

        var service = BuildService(searchService, marinaRepository);

        var result = await service.GetPageAsync(null, MarinaSortOption.Alphabetical, 2, 10);

        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.PageIndex);
        Assert.Equal(10, result.Items.Count);
        // Alphabetical sort: Marina 01..25, page 2 = items 11–20
        Assert.Equal("Marina 11", result.Items[0].Name);
        Assert.Equal("Marina 20", result.Items[9].Name);
    }
}
