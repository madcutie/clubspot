using ClubSpot.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> builder)
    {
        builder.ToTable("courts");
        builder.HasKey(court => court.Id);
        builder.Property(court => court.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(court => court.TenantId).HasColumnName("tenantId");
        builder.Property(court => court.Sport).HasColumnName("sport").HasConversion<string>();
        builder.Property(court => court.SortOrder).HasColumnName("sortOrder");
        builder.Property(court => court.Name).HasColumnName("name").HasMaxLength(120);
        builder.Property(court => court.Detail).HasColumnName("detail").HasMaxLength(200);
        builder.Property(court => court.IsCovered).HasColumnName("isCovered");
        builder.Property(court => court.IsActive).HasColumnName("isActive");
        builder.Property(court => court.ScheduleId).HasColumnName("scheduleId");
        builder.Property(court => court.Durations).HasColumnName("durations");
        builder.Property(court => court.StartIncrementMinutes).HasColumnName("startIncrementMinutes");
        builder.Property(court => court.MinimumNoticeMinutes).HasColumnName("minimumNoticeMinutes");
        builder.ComplexProperty(court => court.DayPrice, dayPrice =>
        {
            dayPrice.Property(price => price.Amount).HasColumnName("dayPriceAmount").HasPrecision(14, 2);
            dayPrice.Property(price => price.Currency).HasColumnName("dayPriceCurrency").HasMaxLength(3).IsFixedLength();
        });
        builder.ComplexProperty(court => court.NightPrice, nightPrice =>
        {
            nightPrice.Property(price => price.Amount).HasColumnName("nightPriceAmount").HasPrecision(14, 2);
            nightPrice.Property(price => price.Currency).HasColumnName("nightPriceCurrency").HasMaxLength(3).IsFixedLength();
        });
        builder.Property(court => court.NightStartsAtMinute).HasColumnName("nightStartsAtMinute");
        builder.HasIndex(court => new { court.TenantId, court.Sport, court.SortOrder }).IsUnique();
        builder.HasOne<Schedule>().WithMany().HasForeignKey(court => court.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
    }
}
