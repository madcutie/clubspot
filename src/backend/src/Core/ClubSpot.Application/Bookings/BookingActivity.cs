namespace ClubSpot.Application.Bookings;

// Activity types owned by bookings, plus the ones about the money a booking moves — which live
// here while finance is still being built inside this module (ADR-0012).
public static class BookingActivity
{
    public const string BookingCreated = "bookingCreated";
    public const string BookingCancelled = "bookingCancelled";
    public const string HoldCreated = "holdCreated";
    public const string HoldReleased = "holdReleased";
    public const string HoldExpired = "holdExpired";
    public const string CheckoutIssued = "checkoutIssued";

    public const string PaymentApproved = "paymentApproved";
    public const string PaymentRejected = "paymentRejected";
    public const string PaymentOrphaned = "paymentOrphaned";

    public static readonly IReadOnlySet<string> RequireReason = new HashSet<string> { BookingCancelled };
}
