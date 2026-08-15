namespace ClubSpot.SharedKernel.Tenancy;

/// <summary>
/// Provee el tenant de la operación en curso.
/// </summary>
/// <remarks>
/// En una request HTTP el tenant se resuelve del token o del host. <b>En un job no existe
/// ninguno de los dos</b>: el proceso corre sin request, y si el contexto queda vacío los
/// filtros globales de persistencia o bien no filtran nada —fuga entre clubes— o bien filtran
/// todo y el job procesa cero filas sin fallar.
/// <para>
/// Por eso <see cref="Current"/> <b>lanza</b> cuando no hay tenant, en vez de devolver un valor
/// neutro: es preferible que un job explote a que trabaje sobre el club equivocado o sobre nada.
/// Todo job recibe el tenant como parámetro explícito y abre un ámbito antes de tocar la base.
/// </para>
/// </remarks>
public interface ITenantContext
{
    /// <summary>Indica si hay un tenant establecido, sin lanzar.</summary>
    bool HasTenant { get; }

    /// <summary>Tenant en curso. Lanza <see cref="MissingTenantException"/> si no hay ninguno.</summary>
    TenantId Current { get; }
}

/// <summary>
/// Se intentó operar sin tenant establecido. Nunca se captura para continuar: indica un bug
/// de cableado, no una condición esperable.
/// </summary>
public sealed class MissingTenantException(string operation)
    : InvalidOperationException(
        $"No hay tenant establecido para la operación '{operation}'. " +
        "Si esto ocurre dentro de un proceso de background, falta abrir el ámbito de tenant " +
        "antes de acceder a la persistencia.");
