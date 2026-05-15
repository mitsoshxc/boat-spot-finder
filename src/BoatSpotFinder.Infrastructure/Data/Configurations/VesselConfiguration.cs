using BoatSpotFinder.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoatSpotFinder.Infrastructure.Data.Configurations;

public class VesselConfiguration : BaseEntityConfiguration<Vessel>
{
    public override void Configure(EntityTypeBuilder<Vessel> builder)
    {
        base.Configure(builder);

        builder.Property(v => v.Type).HasConversion<int>();
    }
}
