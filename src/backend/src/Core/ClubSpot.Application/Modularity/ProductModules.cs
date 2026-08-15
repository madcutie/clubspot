using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Application.Modularity;

// Los manifiestos del catálogo del producto. Uno por módulo contratable. La estructura de
// código es por capas (Domain/Application/Infrastructure, con carpetas por módulo); la
// modularidad de acá es comercial: qué contrató cada club, resuelto en runtime contra
// core.club_module. Ver ADR-0005.

/// <summary>
/// Personas, usuarios, roles y configuración del club. Es el padrón sobre el que se apoya
/// todo lo demás: quien reserva una cancha también es una persona.
/// </summary>
public sealed class CoreModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Core;
    public override string DisplayName => "Núcleo";
    public override string Description =>
        "Personas, usuarios, roles y configuración del club.";
    public override bool IsCore => true;
}

/// <summary>
/// Gestión societaria: membresías, categorías, grupos familiares, antigüedad y habilitación.
/// </summary>
/// <remarks>
/// Depende de <c>finance</c> porque la razón de ser de un padrón societario es la cuota:
/// sin cuenta corriente no hay deuda, sin deuda no hay habilitación, y sin habilitación la
/// condición de socio no decide nada. Un club que sólo quiere una lista de gente contrata
/// <c>core</c>, que ya se la da.
/// </remarks>
public sealed class MembersModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Members;
    public override string DisplayName => "Socios";
    public override string Description =>
        "Membresías, categorías, grupos familiares, altas y bajas, y habilitación del socio.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Core, ModuleId.Finance];
}

/// <summary>
/// El dinero del club: qué se cobra, cuánto, a quién se le debe y cómo entra la plata.
/// </summary>
/// <remarks>
/// Conceptos y precios · cuenta corriente · liquidación mensual · recibos · caja · pagos.
/// Es dependencia de <c>members</c> y de <c>bookings</c> porque ambos generan cargos.
/// </remarks>
public sealed class FinanceModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Finance;
    public override string DisplayName => "Finanzas";
    public override string Description =>
        "Conceptos y precios, cuenta corriente, liquidación mensual, recibos, caja y cobros.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Core];
}

/// <summary>
/// Motor de reserva de espacios: espacios, grilla horaria, bloqueos, tarifas, turnos y reservas.
/// </summary>
/// <remarks>
/// No se contrata solo por deporte: <c>padel</c> y <c>football</c> se apoyan acá y aportan lo
/// que sí difiere entre uno y otro. Un turno es un recurso de capacidad 1, y la reserva sigue
/// el mismo camino de dos fases que cualquier venta: retención con vencimiento, pago,
/// confirmación.
/// </remarks>
public sealed class BookingsModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Bookings;
    public override string DisplayName => "Reservas";
    public override string Description =>
        "Espacios, grilla horaria, tarifas, turnos y reservas con pago.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Core, ModuleId.Finance];
}

/// <summary>
/// Canchas de pádel.
/// </summary>
/// <remarks>
/// Aporta lo propio del deporte sobre el motor de <c>bookings</c>: tipo de espacio y duración
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
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Bookings];
}

/// <summary>
/// Canchas de fútbol.
/// </summary>
/// <remarks>
/// Aporta lo propio del deporte sobre el motor de <c>bookings</c>: formatos F5 / F7 / F11 y
/// duración de turno por defecto.
/// <para>
/// <b>Pendiente de definir con el usuario</b> qué más difiere realmente del pádel más allá de
/// la configuración. Candidatos a discutir: seña, cantidad de jugadores, alquiler de pecheras.
/// </para>
/// </remarks>
public sealed class FootballModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Football;
    public override string DisplayName => "Fútbol";
    public override string Description => "Canchas de fútbol: turnos, tarifas y reglas propias del deporte.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Bookings];
}
