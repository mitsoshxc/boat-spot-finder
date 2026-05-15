using BoatSpotFinder.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoatSpotFinder.Infrastructure.Data.Configurations;

public class SpotSeasonalRuleConfiguration : BaseEntityConfiguration<SpotSeasonalRule>
{
    public override void Configure(EntityTypeBuilder<SpotSeasonalRule> builder)
    {
        base.Configure(builder);

        builder.HasIndex(r => new { r.SpotId, r.StartDate, r.EndDate }).IsUnique();
        builder.Property(r => r.PricePerDay).HasPrecision(18, 2);
    }
}
