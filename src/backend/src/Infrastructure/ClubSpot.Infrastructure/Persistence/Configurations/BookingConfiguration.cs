using ClubSpot.Domain.Bookings;
using ClubSpot.Domain.Core.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");
        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(booking => booking.TenantId).HasColumnName("tenantId");
        builder.Property(booking => booking.CourtId).HasColumnName("courtId");
        builder.Property(booking => booking.Date).HasColumnName("date");
        builder.Property(booking => booking.StartMinute).HasColumnName("startMinute");
        builder.Property(booking => booking.DurationMinutes).HasColumnName("durationMinutes");
        builder.ComplexProperty(booking => booking.Price, price =>
        {
            price.Property(money => money.Amount).HasColumnName("priceAmount").HasPrecision(14, 2);
            price.Property(money => money.Currency).HasColumnName("priceCurrency").HasMaxLength(3).IsFixedLength();
        });
        builder.Property(booking => booking.CustomerName).HasColumnName("customerName").HasMaxLength(120);
        builder.Property(booking => booking.CustomerPhone).HasColumnName("customerPhone").HasMaxLength(40);
        builder.Property(booking => booking.PersonId).HasColumnName("personId");
        builder.Property(booking => booking.Status).HasColumnName("status").HasConversion<string>();
        builder.Property(booking => booking.Origin).HasColumnName("origin").HasConversion<string>();
        builder.Property(booking => booking.PaymentMode).HasColumnName("paymentMode").HasConversion<string>();
        builder.Property(booking => booking.ExpiresAt).HasColumnName("expiresAt");
        builder.Property(booking => booking.CreatedAt).HasColumnName("createdAt");
        builder.Property(booking => booking.CreatedBy).HasColumnName("createdBy");
        builder.Property(booking => booking.CancelledAt).HasColumnName("cancelledAt");
        // Null on a released or expired hold: only an operator cancellation carries a reason.
        builder.Property(booking => booking.CancellationReason).HasColumnName("cancellationReason")
            .HasMaxLength(Booking.CancellationReasonMaxLength);
        builder.HasIndex(booking => new { booking.TenantId, booking.Date });
        builder.HasIndex(booking => new { booking.CourtId, booking.Date });
        builder.HasOne<Court>().WithMany().HasForeignKey(booking => booking.CourtId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(booking => booking.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
}
