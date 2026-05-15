using BoatSpotFinder.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoatSpotFinder.Infrastructure.Data.Configurations;

public class BookingConfiguration : BaseEntityConfiguration<Booking>
{
    public override void Configure(EntityTypeBuilder<Booking> builder)
    {
        base.Configure(builder);

        builder.HasOne(b => b.Spot)
            .WithMany()
            .HasForeignKey(b => b.SpotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.BoatOwner)
            .WithMany()
            .HasForeignKey(b => b.BoatOwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Vessel)
            .WithMany()
            .HasForeignKey(b => b.VesselId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(b => b.TotalPrice).HasPrecision(18, 2);
        builder.Property(b => b.Status).HasConversion<int>();
    }
}
