using BoatSpotFinder.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoatSpotFinder.Infrastructure.Data.Configurations;

public class SpotConfiguration : BaseEntityConfiguration<Spot>
{
    public override void Configure(EntityTypeBuilder<Spot> builder)
    {
        base.Configure(builder);

        builder.HasQueryFilter(s => s.IsActive);
        builder.Property(s => s.IsActive).HasDefaultValue(false);
        builder.Property(s => s.PricePerDay).HasPrecision(18, 2);
    }
}
