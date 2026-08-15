namespace ClubSpot.SharedKernel.Time;

/// <summary>
/// Fuente de tiempo del sistema. Nunca se usa <c>DateTime.Now</c> directamente: el tiempo
/// se inyecta para que los procesos de vencimiento y liquidación sean testeables.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Conversión entre el instante absoluto y el calendario del club.
/// </summary>
/// <remarks>
/// Todo lo que el negocio llama "día" —el cierre de caja, la apertura de agenda, el período de
/// liquidación, el vencimiento de una cuota— es un día <b>en la zona del club</b>, no en UTC.
/// Un job diario agendado a las 00:00 UTC corre a las 21:00 del día anterior en Argentina y
/// liquida el mes equivocado cada día 1.
/// </remarks>
public sealed class ClubCalendar(TimeZoneInfo timeZone, IClock clock)
{
    public static readonly TimeZoneInfo ArgentinaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public TimeZoneInfo TimeZone { get; } = timeZone;

    /// <summary>Fecha de hoy según el calendario del club.</summary>
    public DateOnly Today() => DateOnly.FromDateTime(Now().DateTime);

    /// <summary>Hora local del club para el instante actual.</summary>
    public DateTimeOffset Now() => TimeZoneInfo.ConvertTime(clock.UtcNow, TimeZone);

    /// <summary>Convierte una fecha y hora local del club al instante absoluto que se persiste.</summary>
    public DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        var offset = TimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
