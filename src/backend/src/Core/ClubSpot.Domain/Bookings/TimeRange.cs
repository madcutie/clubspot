namespace ClubSpot.Domain.Bookings;

public sealed record TimeRange(int OpensAtMinute, int ClosesAtMinute)
{
    public void Validate()
    {
        if (OpensAtMinute is < 0 or > 1440 || ClosesAtMinute is < 0 or > 1440 || ClosesAtMinute <= OpensAtMinute)
            throw new ArgumentException("A time range must be within a day and close after it opens.");
    }
}
