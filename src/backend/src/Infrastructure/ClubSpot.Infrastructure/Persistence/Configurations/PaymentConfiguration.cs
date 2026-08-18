using ClubSpot.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(payment => payment.TenantId).HasColumnName("tenantId");
        builder.Property(payment => payment.BookingId).HasColumnName("bookingId");
        builder.Property(payment => payment.Gateway).HasColumnName("gateway").HasMaxLength(40);
        builder.Property(payment => payment.ExternalId).HasColumnName("externalId").HasMaxLength(120);
        builder.ComplexProperty(payment => payment.Amount, amount =>
        {
            amount.Property(money => money.Amount).HasColumnName("amount").HasPrecision(14, 2);
            amount.Property(money => money.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        });
        builder.Property(payment => payment.Kind).HasColumnName("kind").HasConversion<string>();
        builder.Property(payment => payment.Status).HasColumnName("status").HasConversion<string>();
        builder.Property(payment => payment.Source).HasColumnName("source").HasConversion<string>();
        builder.Property(payment => payment.CreatedAt).HasColumnName("createdAt");
        builder.HasIndex(payment => new { payment.Gateway, payment.ExternalId }).IsUnique();
        builder.HasIndex(payment => payment.BookingId);
        builder.HasOne<Booking>().WithMany().HasForeignKey(payment => payment.BookingId).OnDelete(DeleteBehavior.Restrict);
    }
}
