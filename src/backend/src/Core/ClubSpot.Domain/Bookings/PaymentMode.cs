namespace ClubSpot.Domain.Bookings;

public enum PaymentMode
{
    // Paid at the club's counter; the booking confirms immediately.
    Club,
    OnlineFull,
    OnlineDeposit
}
