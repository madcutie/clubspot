using ClubSpot.Domain.Core.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class ActivityLogEntryConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
{
    public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
    {
        builder.ToTable("activityLogEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.TenantId).HasColumnName("tenantId").IsRequired();
        builder.Property(entry => entry.OccurredAt).HasColumnName("occurredAt").IsRequired();
        builder.Property(entry => entry.Type).HasColumnName("type").HasMaxLength(ActivityLogEntry.TypeMaxLength).IsRequired();
        builder.Property(entry => entry.Source).HasColumnName("source").HasConversion<string>().IsRequired();
        builder.Property(entry => entry.ActorUserId).HasColumnName("actorUserId");
        builder.Property(entry => entry.ActorName).HasColumnName("actorName").HasMaxLength(ActivityLogEntry.ActorNameMaxLength).IsRequired();
        builder.Property(entry => entry.Reason).HasColumnName("reason").HasMaxLength(ActivityLogEntry.ReasonMaxLength);
        builder.Property(entry => entry.BookingId).HasColumnName("bookingId");
        builder.Property(entry => entry.PersonId).HasColumnName("personId");
        builder.Property(entry => entry.PaymentId).HasColumnName("paymentId");
        builder.Property(entry => entry.Data).HasColumnName("data").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.TenantId, entry.BookingId });
        builder.HasIndex(entry => new { entry.TenantId, entry.PersonId });
    }
}
