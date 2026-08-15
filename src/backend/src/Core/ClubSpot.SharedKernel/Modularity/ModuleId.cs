namespace ClubSpot.SharedKernel.Modularity;

public readonly record struct ModuleId
{
    public string Value { get; }

    private ModuleId(string value) => Value = value;

    public static ModuleId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Module id cannot be empty.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();

        if (!normalized.All(c => char.IsAsciiLetterLower(c) || c == '-'))
            throw new ArgumentException(
                $"Invalid module id: '{value}'. Only lowercase ASCII letters and hyphens.", nameof(value));

        return new ModuleId(normalized);
    }

    public override string ToString() => Value;

    // Persisted per club and exposed to the frontend: these ids never change.
    public static readonly ModuleId Core = From("core");
    public static readonly ModuleId Members = From("members");
    public static readonly ModuleId Finance = From("finance");
    public static readonly ModuleId Bookings = From("bookings");
    public static readonly ModuleId Padel = From("padel");
    public static readonly ModuleId Football = From("football");
}
