namespace ClubSpot.SharedKernel.Tenancy;

/// <summary>
/// Identidad del club. Todo dato del sistema pertenece a exactamente un tenant.
/// </summary>
public readonly record struct TenantId(Guid Value)
{
    public static TenantId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("TenantId no puede ser vacío.", nameof(value))
            : new TenantId(value);

    public override string ToString() => Value.ToString();
}
