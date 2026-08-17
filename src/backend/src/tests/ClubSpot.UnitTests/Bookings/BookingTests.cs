using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Primitives;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Bookings;

public sealed class BookingTests
{
    private static Booking MakeBooking(
        int startMinute = 600, int durationMinutes = 60, string customerName = "Ana Suarez") =>
        new(Guid.NewGuid(), TenantId.From(Guid.NewGuid()), Guid.NewGuid(), new DateOnly(2026, 8, 20),
            startMinute, durationMinutes, Money.Of(14000m, "ARS"), customerName, null,
            null, BookingOrigin.Counter, DateTimeOffset.UtcNow, Guid.NewGuid());

    [Fact]
    public void An_empty_customer_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => MakeBooking(customerName: "   "));
    }

    [Fact]
    public void A_non_positive_duration_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => MakeBooking(durationMinutes: 0));
    }

    [Fact]
    public void A_booking_cannot_cross_midnight()
    {
        Assert.Throws<ArgumentException>(() => MakeBooking(startMinute: 1380, durationMinutes: 90));
    }

    [Fact]
    public void A_counter_booking_without_an_operator_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Booking(Guid.NewGuid(), TenantId.From(Guid.NewGuid()), Guid.NewGuid(), new DateOnly(2026, 8, 20),
                600, 60, Money.Of(14000m, "ARS"), "Ana Suarez", null,
                null, BookingOrigin.Counter, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void A_portal_booking_without_a_person_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Booking(Guid.NewGuid(), TenantId.From(Guid.NewGuid()), Guid.NewGuid(), new DateOnly(2026, 8, 20),
                600, 60, Money.Of(14000m, "ARS"), "Ana Suarez", "3624000000",
                null, BookingOrigin.Portal, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void Cancelling_marks_the_booking_cancelled()
    {
        var booking = MakeBooking();
        var at = DateTimeOffset.UtcNow;

        booking.Cancel(at);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(at, booking.CancelledAt);
    }

    [Fact]
    public void Cancelling_twice_throws()
    {
        var booking = MakeBooking();
        booking.Cancel(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => booking.Cancel(DateTimeOffset.UtcNow));
    }
}
