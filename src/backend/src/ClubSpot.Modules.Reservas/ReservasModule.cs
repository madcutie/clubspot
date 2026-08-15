using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Modules.Reservas;

/// <summary>
/// Motor de reserva de espacios: espacios, grilla horaria, bloqueos, tarifas, turnos y reservas.
/// </summary>
/// <remarks>
/// No se contrata solo por deporte: <c>padel</c> y <c>futbol</c> se apoyan acá y aportan lo
/// que sí difiere entre uno y otro. Un turno es un recurso de capacidad 1, y la reserva sigue
/// el mismo camino de dos fases que cualquier venta: retención con vencimiento, pago,
/// confirmación.
/// </remarks>
public sealed class ReservasModule : ClubModuleBase
{
    public override ModuleId Id => ModuleId.Reservas;
    public override string DisplayName => "Reservas";
    public override string Description =>
        "Espacios, grilla horaria, tarifas, turnos y reservas con pago.";
    public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.Nucleo, ModuleId.Finanzas];
}
