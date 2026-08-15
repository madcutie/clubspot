namespace ClubSpot.SharedKernel.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

// Everything the business calls a "day" is a day in the club's time zone, never UTC.
public sealed class ClubCalendar(TimeZoneInfo timeZone, IClock clock)
{
    public static readonly TimeZoneInfo ArgentinaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public TimeZoneInfo TimeZone { get; } = timeZone;

    public DateOnly Today() => DateOnly.FromDateTime(Now().DateTime);

    public DateTimeOffset Now() => TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZone);

    public DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        var offset = TimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
