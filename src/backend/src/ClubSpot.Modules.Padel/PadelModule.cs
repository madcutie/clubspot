using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Modules.Padel;

/// <summary>
/// Canchas de pádel.
/// </summary>
/// <remarks>
/// Aporta lo propio del deporte sobre el motor de <c>reservas</c>: tipo de espacio y duración
/// de turno por defecto.
/// <para>
/// <b>Pendiente de definir con el usuario</b> qué más difiere realmente del fútbol más allá de
/// la configuración. Candidatos a discutir: partido abierto para completar jugadores, alquiler
/// de paletas, precio por jugador en vez de por cancha.
/// </para>
/// </remarks>
public sealed class PadelModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Padel;
    public override string DisplayName => "Pádel";
    public override string Description => "Canchas de pádel: turnos, tarifas y reglas propias del deporte.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Reservas];
}
