using ClubSpot.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("clubs", table =>
            table.HasCheckConstraint("ckClubsDepositPercent", "\"depositPercent\" BETWEEN 0 AND 100"));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.Slug).HasColumnName("slug").HasMaxLength(60).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(c => c.Venue).HasColumnName("venue").HasMaxLength(120);
        builder.Property(c => c.TimeZone).HasColumnName("timeZone").HasMaxLength(60).IsRequired();
        builder.Property(c => c.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(c => c.DepositPercent).HasColumnName("depositPercent");
        builder.Property(c => c.CreatedAt).HasColumnName("createdAt");
    }
}
