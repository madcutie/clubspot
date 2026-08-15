using System.Globalization;

namespace ClubSpot.SharedKernel.Primitives;

/// <summary>
/// Mes calendario al que pertenece un cargo. Es la unidad de la liquidación y de la deuda:
/// "debe julio" es una afirmación sobre un <see cref="Periodo"/>, no sobre un rango de fechas.
/// </summary>
public readonly record struct Periodo : IComparable<Periodo>
{
    public int Year { get; }
    public int Month { get; }

    public Periodo(int year, int month)
    {
        if (year is < 1900 or > 2999) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        Year = year;
        Month = month;
    }

    public static Periodo From(DateOnly date) => new(date.Year, date.Month);

    public static Periodo Parse(string value) =>
        TryParse(value, out var periodo)
            ? periodo
            : throw new FormatException($"Período inválido: '{value}'. Formato esperado aaaa-mm.");

    public static bool TryParse(string? value, out Periodo periodo)
    {
        periodo = default;
        if (value is null || value.Length != 7 || value[4] != '-') return false;

        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y)) return false;
        if (!int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m)) return false;
        if (y is < 1900 or > 2999 || m is < 1 or > 12) return false;

        periodo = new Periodo(y, m);
        return true;
    }

    public Periodo Next() => Month == 12 ? new Periodo(Year + 1, 1) : new Periodo(Year, Month + 1);
    public Periodo Previous() => Month == 1 ? new Periodo(Year - 1, 12) : new Periodo(Year, Month - 1);

    public DateOnly FirstDay() => new(Year, Month, 1);
    public DateOnly LastDay() => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    /// <summary>Cantidad de meses transcurridos desde <paramref name="other"/> hasta este período.</summary>
    public int MonthsSince(Periodo other) => (Year - other.Year) * 12 + (Month - other.Month);

    public int CompareTo(Periodo other) => (Year, Month).CompareTo((other.Year, other.Month));

    public static bool operator <(Periodo a, Periodo b) => a.CompareTo(b) < 0;
    public static bool operator >(Periodo a, Periodo b) => a.CompareTo(b) > 0;
    public static bool operator <=(Periodo a, Periodo b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Periodo a, Periodo b) => a.CompareTo(b) >= 0;

    public override string ToString() => $"{Year:0000}-{Month:00}";
}
