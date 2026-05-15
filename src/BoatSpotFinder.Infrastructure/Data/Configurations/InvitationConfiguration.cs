using BoatSpotFinder.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoatSpotFinder.Infrastructure.Data.Configurations;

public class InvitationConfiguration : BaseEntityConfiguration<Invitation>
{
    public override void Configure(EntityTypeBuilder<Invitation> builder)
    {
        base.Configure(builder);

        builder.HasIndex(i => i.Token);
        builder.HasIndex(i => i.MarinaId);
        builder.Property(i => i.IsUsed).HasDefaultValue(false);
    }
}
