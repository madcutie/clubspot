using ClubSpot.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("club", table =>
            table.HasCheckConstraint("ck_club_deposit_percent", "deposit_percent BETWEEN 0 AND 100"));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.Slug).HasColumnName("slug").HasMaxLength(60).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique().HasDatabaseName("ux_club_slug");

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(c => c.Venue).HasColumnName("venue").HasMaxLength(120);
        builder.Property(c => c.TimeZone).HasColumnName("time_zone").HasMaxLength(60).IsRequired();
        builder.Property(c => c.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(c => c.DepositPercent).HasColumnName("deposit_percent");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
    }
}
