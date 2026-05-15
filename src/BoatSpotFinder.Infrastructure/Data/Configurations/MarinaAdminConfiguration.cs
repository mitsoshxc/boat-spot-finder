using BoatSpotFinder.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoatSpotFinder.Infrastructure.Data.Configurations;

public class MarinaAdminConfiguration : IEntityTypeConfiguration<MarinaAdmin>
{
    public void Configure(EntityTypeBuilder<MarinaAdmin> builder)
    {
        builder.HasKey(ma => new { ma.MarinaId, ma.UserId });

        builder.HasIndex(ma => ma.UserId);

        builder.HasOne(ma => ma.Marina)
            .WithMany(m => m.Admins)
            .HasForeignKey(ma => ma.MarinaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ma => ma.User)
            .WithMany()
            .HasForeignKey(ma => ma.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ma => ma.InvitedBy)
            .WithMany()
            .HasForeignKey(ma => ma.InvitedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
