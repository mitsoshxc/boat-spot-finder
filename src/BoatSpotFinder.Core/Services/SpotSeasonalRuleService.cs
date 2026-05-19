using BoatSpotFinder.Core.Common;
using BoatSpotFinder.Core.Entities;
using BoatSpotFinder.Core.Interfaces;
using BoatSpotFinder.Core.Models;

namespace BoatSpotFinder.Core.Services;

public class SpotSeasonalRuleService : ISpotSeasonalRuleService
{
    private readonly ISpotSeasonalRuleRepository _ruleRepository;

    public SpotSeasonalRuleService(ISpotSeasonalRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    public async Task<ServiceResult> CreateAsync(Guid spotId, SpotSeasonalRuleInput input)
    {
        var validation = Validate(input);
        if (!validation.Success) return validation;

        var existing = await _ruleRepository.GetBySpotIdAsync(spotId);
        if (Overlaps(existing, input.StartDate, input.EndDate, excludeId: null))
            return ServiceResult.Fail("Date range overlaps with an existing rule.");

        var rule = new SpotSeasonalRule(input.Name, input.StartDate, input.EndDate, input.PricePerDay, input.MinBookingDays, spotId);
        await _ruleRepository.AddAsync(rule);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateAsync(Guid ruleId, SpotSeasonalRuleInput input)
    {
        var rule = await _ruleRepository.GetByIdAsync(ruleId);
        if (rule is null) return ServiceResult.Fail("Rule not found.");

        var validation = Validate(input);
        if (!validation.Success) return validation;

        var existing = await _ruleRepository.GetBySpotIdAsync(rule.SpotId);
        if (Overlaps(existing, input.StartDate, input.EndDate, excludeId: ruleId))
            return ServiceResult.Fail("Date range overlaps with an existing rule.");

        rule.UpdateDetails(input.Name, input.StartDate, input.EndDate, input.PricePerDay, input.MinBookingDays);
        await _ruleRepository.UpdateAsync(rule);
        return ServiceResult.Ok();
    }

    public Task DeleteAsync(Guid ruleId) => _ruleRepository.DeleteAsync(ruleId);

    private static ServiceResult Validate(SpotSeasonalRuleInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name)) errors.Add("Name is required.");
        if (input.EndDate < input.StartDate) errors.Add("End date must be on or after start date.");
        if (input.PricePerDay < 0) errors.Add("Price per day cannot be negative.");
        if (input.MinBookingDays < 1) errors.Add("Minimum booking days must be at least 1.");
        return errors.Count == 0 ? ServiceResult.Ok() : ServiceResult.Fail(errors.ToArray());
    }

    private static bool Overlaps(IEnumerable<SpotSeasonalRule> existing, DateOnly start, DateOnly end, Guid? excludeId)
    {
        // Inclusive bounds on both sides: A.End == B.Start counts as overlap (per PLAN.md task 3.10).
        return existing.Any(r => (excludeId is null || r.Id != excludeId) && start <= r.EndDate && end >= r.StartDate);
    }
}
