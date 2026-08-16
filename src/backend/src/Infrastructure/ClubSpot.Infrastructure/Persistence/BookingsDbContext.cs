using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace ClubSpot.Infrastructure.Persistence;

public sealed class BookingsDbContext(DbContextOptions<BookingsDbContext> options, ITenantContext tenantContext) : ModuleDbContextBase(options, tenantContext)
{
    public const string Schema = "bookings";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Court> Courts => Set<Court>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var weeklyRangesConverter = new ValueConverter<Dictionary<DayOfWeek, List<TimeRange>>, string>(
            ranges => JsonSerializer.Serialize(ranges, jsonOptions),
            json => JsonSerializer.Deserialize<Dictionary<DayOfWeek, List<TimeRange>>>(json, jsonOptions)!);
        var specialDatesConverter = new ValueConverter<List<SpecialDate>, string>(
            dates => JsonSerializer.Serialize(dates, jsonOptions),
            json => JsonSerializer.Deserialize<List<SpecialDate>>(json, jsonOptions)!);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Schedule>(builder =>
        {
            builder.ToTable("schedule");
            builder.HasKey(schedule => schedule.Id);
            builder.Property(schedule => schedule.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(schedule => schedule.TenantId).HasColumnName("tenantId");
            builder.Property(schedule => schedule.Name).HasColumnName("name").HasMaxLength(80);
            builder.Property(schedule => schedule.TimeZone).HasColumnName("timeZone").HasMaxLength(60);
            builder.Property(schedule => schedule.WeeklyRanges).HasColumnName("weeklyRanges").HasColumnType("jsonb").HasConversion(weeklyRangesConverter)
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<DayOfWeek, List<TimeRange>>>(
                    (left, right) => JsonSerializer.Serialize(left, jsonOptions) == JsonSerializer.Serialize(right, jsonOptions),
                    value => JsonSerializer.Serialize(value, jsonOptions).GetHashCode(),
                    value => JsonSerializer.Deserialize<Dictionary<DayOfWeek, List<TimeRange>>>(JsonSerializer.Serialize(value, jsonOptions), jsonOptions)!));
            builder.Property(schedule => schedule.SpecialDates).HasColumnName("specialDates").HasColumnType("jsonb").HasConversion(specialDatesConverter)
                .Metadata.SetValueComparer(new ValueComparer<List<SpecialDate>>(
                    (left, right) => JsonSerializer.Serialize(left, jsonOptions) == JsonSerializer.Serialize(right, jsonOptions),
                    value => JsonSerializer.Serialize(value, jsonOptions).GetHashCode(),
                    value => JsonSerializer.Deserialize<List<SpecialDate>>(JsonSerializer.Serialize(value, jsonOptions), jsonOptions)!));
        });
        modelBuilder.Entity<Court>(builder =>
        {
            builder.ToTable("court");
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
            builder.Property(court => court.DayPrice).HasColumnName("dayPrice");
            builder.Property(court => court.NightPrice).HasColumnName("nightPrice");
            builder.Property(court => court.NightStartsAtMinute).HasColumnName("nightStartsAtMinute");
            builder.HasIndex(court => new { court.TenantId, court.Sport, court.SortOrder }).IsUnique().HasDatabaseName("uxCourtTenantSportSortOrder");
            builder.HasOne<Schedule>().WithMany().HasForeignKey(court => court.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        });
        base.OnModelCreating(modelBuilder);
    }
}
