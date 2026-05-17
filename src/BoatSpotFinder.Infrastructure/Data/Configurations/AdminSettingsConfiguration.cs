using BoatSpotFinder.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoatSpotFinder.Infrastructure.Data.Configurations;

public class AdminSettingsConfiguration : BaseEntityConfiguration<AdminSettings>
{
    public override void Configure(EntityTypeBuilder<AdminSettings> builder)
    {
        base.Configure(builder);

        builder.HasData(new AdminSettings(AutoActionType.AutoApprove, 6)
        {
            Id = new Guid("10000000-0000-0000-0000-000000000001"),
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
