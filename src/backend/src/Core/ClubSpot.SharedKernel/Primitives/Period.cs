using System.Globalization;

namespace ClubSpot.SharedKernel.Primitives;

/// <summary>
/// Mes calendario al que pertenece un cargo. Es la unidad de la liquidación y de la deuda:
/// "debe julio" es una afirmación sobre un <see cref="Period"/>, no sobre un rango de fechas.
/// </summary>
public readonly record struct Period : IComparable<Period>
{
    public int Year { get; }
    public int Month { get; }

    public Period(int year, int month)
    {
        if (year is < 1900 or > 2999) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        Year = year;
        Month = month;
    }

    public static Period From(DateOnly date) => new(date.Year, date.Month);

    public static Period Parse(string value) =>
        TryParse(value, out var period)
            ? period
            : throw new FormatException($"Período inválido: '{value}'. Formato esperado aaaa-mm.");

    public static bool TryParse(string? value, out Period period)
    {
        period = default;
        if (value is null || value.Length != 7 || value[4] != '-') return false;

        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y)) return false;
        if (!int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m)) return false;
        if (y is < 1900 or > 2999 || m is < 1 or > 12) return false;

        period = new Period(y, m);
        return true;
    }

    public Period Next() => Month == 12 ? new Period(Year + 1, 1) : new Period(Year, Month + 1);
    public Period Previous() => Month == 1 ? new Period(Year - 1, 12) : new Period(Year, Month - 1);

    public DateOnly FirstDay() => new(Year, Month, 1);
    public DateOnly LastDay() => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    /// <summary>Cantidad de meses transcurridos desde <paramref name="other"/> hasta este período.</summary>
    public int MonthsSince(Period other) => (Year - other.Year) * 12 + (Month - other.Month);

    public int CompareTo(Period other) => (Year, Month).CompareTo((other.Year, other.Month));

    public static bool operator <(Period a, Period b) => a.CompareTo(b) < 0;
    public static bool operator >(Period a, Period b) => a.CompareTo(b) > 0;
    public static bool operator <=(Period a, Period b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Period a, Period b) => a.CompareTo(b) >= 0;

    public override string ToString() => $"{Year:0000}-{Month:00}";
}
