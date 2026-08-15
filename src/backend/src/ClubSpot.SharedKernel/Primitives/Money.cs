using System.Globalization;

namespace ClubSpot.SharedKernel.Primitives;

/// <summary>
/// Importe con moneda explícita. No existe un <c>decimal</c> suelto representando plata
/// en ningún agregado del sistema.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public const string DefaultCurrency = "ARS";

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money Of(decimal amount, string currency = DefaultCurrency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("La moneda debe ser un código ISO de 3 letras.", nameof(currency));

        return new Money(decimal.Round(amount, 2, MidpointRounding.AwayFromZero), currency.ToUpperInvariant());
    }

    public bool IsZero => Amount == 0m;
    public bool IsNegative => Amount < 0m;

    public static Money operator +(Money a, Money b) => Of(a.Amount + Ensure(a, b).Amount, a.Currency);
    public static Money operator -(Money a, Money b) => Of(a.Amount - Ensure(a, b).Amount, a.Currency);
    public static Money operator *(Money a, decimal factor) => Of(a.Amount * factor, a.Currency);

    /// <summary>Aplica un porcentaje de descuento (0–100) y devuelve el importe resultante.</summary>
    public Money LessPercent(decimal percent) =>
        percent is < 0m or > 100m
            ? throw new ArgumentOutOfRangeException(nameof(percent), "El porcentaje debe estar entre 0 y 100.")
            : Of(Amount * (1m - percent / 100m), Currency);

    private static Money Ensure(Money a, Money b) =>
        a.Currency == b.Currency
            ? b
            : throw new InvalidOperationException($"No se pueden operar importes en {a.Currency} y {b.Currency}.");

    public override string ToString() =>
        Amount.ToString("C", CultureInfo.GetCultureInfo("es-AR")) + $" {Currency}";
}
