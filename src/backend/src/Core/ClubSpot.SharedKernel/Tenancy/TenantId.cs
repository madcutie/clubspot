namespace ClubSpot.SharedKernel.Tenancy;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("TenantId cannot be empty.", nameof(value))
            : new TenantId(value);

    public override string ToString() => Value.ToString();
}
