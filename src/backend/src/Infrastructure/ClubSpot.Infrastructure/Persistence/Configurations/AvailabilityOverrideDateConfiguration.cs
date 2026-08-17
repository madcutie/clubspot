using ClubSpot.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class AvailabilityOverrideDateConfiguration : IEntityTypeConfiguration<AvailabilityOverrideDate>
{
    public void Configure(EntityTypeBuilder<AvailabilityOverrideDate> builder)
    {
        builder.ToTable("availabilityOverrideDates");
        builder.HasKey(date => new { date.OverrideId, date.Date });
        builder.Property(date => date.OverrideId).HasColumnName("overrideId");
        builder.Property(date => date.TenantId).HasColumnName("tenantId");
        builder.Property(date => date.Date).HasColumnName("date");
        builder.HasIndex(date => new { date.TenantId, date.Date });
    }
}
