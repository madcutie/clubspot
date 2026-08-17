using System.Text.Json;
using ClubSpot.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class AvailabilityOverrideConfiguration : IEntityTypeConfiguration<AvailabilityOverride>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<AvailabilityOverride> builder)
    {
        var windowsConverter = new ValueConverter<IReadOnlyList<TimeRange>, string>(
            windows => JsonSerializer.Serialize(windows, JsonOptions),
            json => JsonSerializer.Deserialize<List<TimeRange>>(json, JsonOptions)!);

        builder.ToTable("availabilityOverrides");
        builder.HasKey(availabilityOverride => availabilityOverride.Id);
        builder.Property(availabilityOverride => availabilityOverride.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(availabilityOverride => availabilityOverride.TenantId).HasColumnName("tenantId");
        builder.Property(availabilityOverride => availabilityOverride.CourtId).HasColumnName("courtId");
        builder.Property(availabilityOverride => availabilityOverride.Windows).HasColumnName("windows").HasColumnType("jsonb").HasConversion(windowsConverter)
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<TimeRange>>(
                (left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
                value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(),
                value => JsonSerializer.Deserialize<List<TimeRange>>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!));
        builder.Property(availabilityOverride => availabilityOverride.Reason).HasColumnName("reason").HasMaxLength(200);
        builder.Property(availabilityOverride => availabilityOverride.CreatedAt).HasColumnName("createdAt");
        builder.Property(availabilityOverride => availabilityOverride.CreatedBy).HasColumnName("createdBy");
        builder.HasOne<Court>().WithMany().HasForeignKey(availabilityOverride => availabilityOverride.CourtId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(availabilityOverride => availabilityOverride.Dates).WithOne().HasForeignKey(date => date.OverrideId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(availabilityOverride => availabilityOverride.Dates).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
