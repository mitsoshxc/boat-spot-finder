using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Infrastructure.Repositories;
using BoatSpotFinder.Tests.Infrastructure;

namespace BoatSpotFinder.Tests.Repositories;

public class AdminSettingsRepositoryTests
{
    private static readonly Guid SeededId = new Guid("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task GetAsync_ReturnsSeededSingleton()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var repo = new AdminSettingsRepository(db.Context);
        var settings = await repo.GetAsync();

        Assert.NotNull(settings);
        Assert.Equal(SeededId, settings.Id);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        using var db = TestDbContextFactory.CreateSqliteInMemory();

        var repo = new AdminSettingsRepository(db.Context);
        var settings = await repo.GetAsync();

        settings.UpdateSettings(AutoActionType.AutoReject, 12);
        await repo.UpdateAsync(settings);

        var reloaded = await repo.GetAsync();
        Assert.Equal(AutoActionType.AutoReject, reloaded.AutoActionType);
        Assert.Equal(12, reloaded.AutoActionTimeoutHours);
    }
}
