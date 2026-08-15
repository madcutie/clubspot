using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Modules.Clubes;

/// <summary>
/// Personas, usuarios, roles y configuración del club. Es el padrón sobre el que se apoya
/// todo lo demás: quien reserva una cancha también es una persona.
/// </summary>
public sealed class NucleoModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Nucleo;
    public override string DisplayName => "Núcleo";
    public override string Description =>
        "Personas, usuarios, roles y configuración del club.";
    public override bool IsCore => true;
}

/// <summary>
/// Gestión societaria: membresías, categorías, grupos familiares, antigüedad y habilitación.
/// </summary>
/// <remarks>
/// Depende de <c>finanzas</c> porque la razón de ser de un padrón societario es la cuota:
/// sin cuenta corriente no hay deuda, sin deuda no hay habilitación, y sin habilitación la
/// condición de socio no decide nada. Un club que sólo quiere una lista de gente contrata
/// <c>nucleo</c>, que ya se la da.
/// </remarks>
public sealed class SociosModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Socios;
    public override string DisplayName => "Socios";
    public override string Description =>
        "Membresías, categorías, grupos familiares, altas y bajas, y habilitación del socio.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Nucleo, ModuleId.Finanzas];
}
