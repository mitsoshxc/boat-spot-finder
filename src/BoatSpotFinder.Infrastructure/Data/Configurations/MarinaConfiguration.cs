using BoatSpotFinder.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoatSpotFinder.Infrastructure.Data.Configurations;

public class MarinaConfiguration : BaseEntityConfiguration<Marina>
{
    public override void Configure(EntityTypeBuilder<Marina> builder)
    {
        base.Configure(builder);

        builder.Property(m => m.IsActive).HasDefaultValue(true);
        builder.Property(m => m.LayoutWidth).HasDefaultValue(1200);
        builder.Property(m => m.LayoutHeight).HasDefaultValue(800);
        builder.Property(m => m.DefaultPricePerDay).HasPrecision(18, 2);
    }
}
