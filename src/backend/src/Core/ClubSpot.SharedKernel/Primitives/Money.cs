using System.Globalization;

namespace ClubSpot.SharedKernel.Primitives;

public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency) => new(0m, currency);

    public static Money Of(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        return new Money(decimal.Round(amount, 2, MidpointRounding.AwayFromZero), currency.ToUpperInvariant());
    }

    public bool IsZero => Amount == 0m;
    public bool IsNegative => Amount < 0m;

    public static Money operator +(Money a, Money b) => Of(a.Amount + Ensure(a, b).Amount, a.Currency);
    public static Money operator -(Money a, Money b) => Of(a.Amount - Ensure(a, b).Amount, a.Currency);
    public static Money operator *(Money a, decimal factor) => Of(a.Amount * factor, a.Currency);

    public Money LessPercent(decimal percent) =>
        percent is < 0m or > 100m
            ? throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be between 0 and 100.")
            : Of(Amount * (1m - percent / 100m), Currency);

    private static Money Ensure(Money a, Money b) =>
        a.Currency == b.Currency
            ? b
            : throw new InvalidOperationException($"Cannot operate on amounts in {a.Currency} and {b.Currency}.");

    public override string ToString() =>
        Amount.ToString("C", CultureInfo.GetCultureInfo("es-AR")) + $" {Currency}";
}
