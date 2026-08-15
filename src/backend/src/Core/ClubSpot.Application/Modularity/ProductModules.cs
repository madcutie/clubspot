using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Application.Modularity;

public sealed class CoreModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Core;
    public override string DisplayName => "Núcleo";
    public override string Description =>
        "Personas, usuarios, roles y configuración del club.";
    public override bool IsCore => true;
}

public sealed class MembersModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Members;
    public override string DisplayName => "Socios";
    public override string Description =>
        "Membresías, categorías, grupos familiares, altas y bajas, y habilitación del socio.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Core, ModuleId.Finance];
}

public sealed class FinanceModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Finance;
    public override string DisplayName => "Finanzas";
    public override string Description =>
        "Conceptos y precios, cuenta corriente, liquidación mensual, recibos, caja y cobros.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Core];
}

public sealed class BookingsModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Bookings;
    public override string DisplayName => "Reservas";
    public override string Description =>
        "Espacios, grilla horaria, tarifas, turnos y reservas con pago.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Core, ModuleId.Finance];
}

public sealed class PadelModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Padel;
    public override string DisplayName => "Pádel";
    public override string Description => "Canchas de pádel: turnos, tarifas y reglas propias del deporte.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Bookings];
}

public sealed class FootballModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Football;
    public override string DisplayName => "Fútbol";
    public override string Description => "Canchas de fútbol: turnos, tarifas y reglas propias del deporte.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Bookings];
}
