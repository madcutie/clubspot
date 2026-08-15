namespace ClubSpot.SharedKernel.Modularity;

/// <summary>
/// Identificador estable de un módulo del producto. Se persiste en la configuración del club
/// y viaja al frontend, así que <b>no cambia nunca</b> aunque cambie el nombre comercial.
/// </summary>
public readonly record struct ModuleId
{
    public string Value { get; }

    private ModuleId(string value) => Value = value;

    public static ModuleId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El id de módulo no puede ser vacío.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();

        if (!normalized.All(c => char.IsAsciiLetterLower(c) || c == '-'))
            throw new ArgumentException(
                $"Id de módulo inválido: '{value}'. Sólo letras minúsculas ASCII y guiones.", nameof(value));

        return new ModuleId(normalized);
    }

    public override string ToString() => Value;

    // Catálogo del producto. Agregar acá es la única forma de que exista un módulo nuevo.
    public static readonly ModuleId Core = From("core");
    public static readonly ModuleId Members = From("members");
    public static readonly ModuleId Finance = From("finance");
    public static readonly ModuleId Bookings = From("bookings");
    public static readonly ModuleId Padel = From("padel");
    public static readonly ModuleId Football = From("football");
}
