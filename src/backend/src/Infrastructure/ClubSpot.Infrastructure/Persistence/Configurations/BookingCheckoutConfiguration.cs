using ClubSpot.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClubSpot.Infrastructure.Persistence.Configurations;

internal sealed class BookingCheckoutConfiguration : IEntityTypeConfiguration<BookingCheckout>
{
    public void Configure(EntityTypeBuilder<BookingCheckout> builder)
    {
        builder.ToTable("bookingCheckouts");
        builder.HasKey(checkout => checkout.Id);
        builder.Property(checkout => checkout.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(checkout => checkout.TenantId).HasColumnName("tenantId");
        builder.Property(checkout => checkout.BookingId).HasColumnName("bookingId");
        builder.Property(checkout => checkout.Provider).HasColumnName("provider").HasMaxLength(40);
        builder.Property(checkout => checkout.Url).HasColumnName("url").HasMaxLength(BookingCheckout.UrlMaxLength);
        builder.ComplexProperty(checkout => checkout.Amount, amount =>
        {
            amount.Property(money => money.Amount).HasColumnName("amount").HasPrecision(14, 2);
            amount.Property(money => money.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
        });
        builder.Property(checkout => checkout.ExpiresAt).HasColumnName("expiresAt");
        builder.Property(checkout => checkout.IssuedAt).HasColumnName("issuedAt");
        builder.HasIndex(checkout => new { checkout.BookingId, checkout.IssuedAt });
        builder.HasOne<Booking>().WithMany().HasForeignKey(checkout => checkout.BookingId).OnDelete(DeleteBehavior.Restrict);
    }
}
