namespace ClubSpot.Domain.Bookings;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    // Online-payment hold: blocks the slot until ExpiresAt.
    PendingPayment,
    Expired
}
