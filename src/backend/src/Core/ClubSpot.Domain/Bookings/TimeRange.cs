namespace ClubSpot.Domain.Bookings;

public sealed record TimeRange
{
    public int OpensAtMinute { get; }
    public int ClosesAtMinute { get; }

    public TimeRange(int opensAtMinute, int closesAtMinute)
    {
        if (opensAtMinute is < 0 or > 1440 || closesAtMinute is < 0 or > 1440 || closesAtMinute <= opensAtMinute)
            throw new ArgumentException("A time range must be within a day and close after it opens.");
        OpensAtMinute = opensAtMinute;
        ClosesAtMinute = closesAtMinute;
    }
}
