using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Modules.Futbol;

/// <summary>
/// Canchas de fútbol.
/// </summary>
/// <remarks>
/// Aporta lo propio del deporte sobre el motor de <c>reservas</c>: formatos F5 / F7 / F11 y
/// duración de turno por defecto.
/// <para>
/// <b>Pendiente de definir con el usuario</b> qué más difiere realmente del pádel más allá de
/// la configuración. Candidatos a discutir: seña, cantidad de jugadores, alquiler de pecheras.
/// </para>
/// </remarks>
public sealed class FutbolModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Futbol;
    public override string DisplayName => "Fútbol";
    public override string Description => "Canchas de fútbol: turnos, tarifas y reglas propias del deporte.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Reservas];
}
