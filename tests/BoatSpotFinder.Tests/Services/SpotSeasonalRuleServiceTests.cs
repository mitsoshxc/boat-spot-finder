using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Models;
using BoatSpotFinder.Core.Services;

namespace BoatSpotFinder.Tests.Services;

public class SpotSeasonalRuleServiceTests
{
    private static readonly Guid SpotId = Guid.NewGuid();

    private static StubSpotSeasonalRuleRepository SeedRepository()
    {
        var repo = new StubSpotSeasonalRuleRepository();
        var existing = new SpotSeasonalRule(
            "Summer",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 9, 1),
            100m,
            3,
            SpotId);
        repo.Rules.Add(existing);
        return repo;
    }

    [Fact]
    public async Task Create_RejectsExactOverlap()
    {
        var repo = SeedRepository();
        var service = new SpotSeasonalRuleService(repo);

        var result = await service.CreateAsync(SpotId, new SpotSeasonalRuleInput(
            "Duplicate", new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 1), 80m, 2));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("overlaps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_RejectsPartialOverlap()
    {
        var repo = SeedRepository();
        var service = new SpotSeasonalRuleService(repo);

        var result = await service.CreateAsync(SpotId, new SpotSeasonalRuleInput(
            "Autumn", new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 1), 80m, 2));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_RejectsBoundaryOverlap()
    {
        var repo = SeedRepository();
        var service = new SpotSeasonalRuleService(repo);

        var result = await service.CreateAsync(SpotId, new SpotSeasonalRuleInput(
            "Autumn", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), 80m, 2));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_AllowsAdjacentNextDay()
    {
        var repo = SeedRepository();
        var service = new SpotSeasonalRuleService(repo);

        var result = await service.CreateAsync(SpotId, new SpotSeasonalRuleInput(
            "Autumn", new DateOnly(2026, 9, 2), new DateOnly(2026, 12, 31), 80m, 2));

        Assert.True(result.Success);
        Assert.Equal(2, repo.Rules.Count);
    }

    [Fact]
    public async Task Update_ExcludesSelfFromOverlap()
    {
        var repo = SeedRepository();
        repo.Rules[0].Id = Guid.NewGuid();
        var service = new SpotSeasonalRuleService(repo);
        var existingId = repo.Rules[0].Id;

        var result = await service.UpdateAsync(existingId, new SpotSeasonalRuleInput(
            "Summer", new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 1), 100m, 3));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Update_DetectsConflictWithOtherRule()
    {
        var repo = SeedRepository();
        repo.Rules[0].Id = Guid.NewGuid();
        var autumn = new SpotSeasonalRule("Autumn", new DateOnly(2026, 9, 2), new DateOnly(2026, 12, 31), 80m, 2, SpotId);
        autumn.Id = Guid.NewGuid();
        repo.Rules.Add(autumn);
        var service = new SpotSeasonalRuleService(repo);

        var result = await service.UpdateAsync(autumn.Id, new SpotSeasonalRuleInput(
            "Autumn extended", new DateOnly(2026, 8, 15), new DateOnly(2026, 12, 31), 80m, 2));

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("overlaps", StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class StubSpotSeasonalRuleRepository : ISpotSeasonalRuleRepository
{
    public List<SpotSeasonalRule> Rules { get; } = [];

    public Task<SpotSeasonalRule?> GetByIdAsync(Guid id) =>
        Task.FromResult(Rules.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<SpotSeasonalRule>> GetBySpotIdAsync(Guid spotId) =>
        Task.FromResult<IReadOnlyList<SpotSeasonalRule>>(Rules.Where(r => r.SpotId == spotId).ToList());

    public Task AddAsync(SpotSeasonalRule rule)
    {
        Rules.Add(rule);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SpotSeasonalRule rule)
    {
        var idx = Rules.FindIndex(r => r.Id == rule.Id);
        if (idx >= 0)
        {
            Rules[idx] = rule;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        Rules.RemoveAll(r => r.Id == id);
        return Task.CompletedTask;
    }
}
