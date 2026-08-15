using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Modules.Finanzas;

/// <summary>
/// El dinero del club: qué se cobra, cuánto, a quién se le debe y cómo entra la plata.
/// </summary>
/// <remarks>
/// Conceptos y precios · cuenta corriente · liquidación mensual · recibos · caja · pagos.
/// Es dependencia de <c>socios</c> y de <c>reservas</c> porque ambos generan cargos.
/// </remarks>
public sealed class FinanzasModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Finanzas;
    public override string DisplayName => "Finanzas";
    public override string Description =>
        "Conceptos y precios, cuenta corriente, liquidación mensual, recibos, caja y cobros.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Nucleo];
}
