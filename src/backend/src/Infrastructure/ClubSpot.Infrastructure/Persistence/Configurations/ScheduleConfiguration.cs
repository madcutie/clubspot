using System.Text.Json;
using ClubSpot.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        var weeklyRangesConverter = new ValueConverter<IReadOnlyDictionary<DayOfWeek, IReadOnlyList<TimeRange>>, string>(
            ranges => JsonSerializer.Serialize(ranges, JsonOptions),
            json => ToWeeklyRanges(JsonSerializer.Deserialize<Dictionary<DayOfWeek, List<TimeRange>>>(json, JsonOptions)!));

        builder.ToTable("schedules");
        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(schedule => schedule.TenantId).HasColumnName("tenantId");
        builder.Property(schedule => schedule.Name).HasColumnName("name").HasMaxLength(80);
        builder.Property(schedule => schedule.WeeklyRanges).HasColumnName("weeklyRanges").HasColumnType("jsonb").HasConversion(weeklyRangesConverter)
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyDictionary<DayOfWeek, IReadOnlyList<TimeRange>>>(
                (left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
                value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(),
                value => ToWeeklyRanges(JsonSerializer.Deserialize<Dictionary<DayOfWeek, List<TimeRange>>>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!)));
        builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
    }

    private static Dictionary<DayOfWeek, IReadOnlyList<TimeRange>> ToWeeklyRanges(Dictionary<DayOfWeek, List<TimeRange>> source)
    {
        var result = new Dictionary<DayOfWeek, IReadOnlyList<TimeRange>>();
        foreach (var (day, ranges) in source) result[day] = ranges;
        return result;
    }
}
