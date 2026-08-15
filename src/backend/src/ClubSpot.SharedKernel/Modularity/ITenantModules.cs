namespace ClubSpot.SharedKernel.Modularity;

/// <summary>
/// Qué módulos tiene contratados el club de la operación en curso.
/// </summary>
/// <remarks>
/// Se consulta en tres lugares y en ninguno más:
/// <list type="bullet">
///   <item>al resolver un endpoint, que responde 404 si el módulo está apagado;</item>
///   <item>al despachar un job, que directamente no se encola para ese club;</item>
///   <item>al armar las capacidades que el frontend usa para construir su menú.</item>
/// </list>
/// La lógica de dominio <b>no</b> pregunta por módulos: un agregado de reservas no se entera
/// de si el club contrató reservas, porque nunca llega a ejecutarse si no las contrató.
/// </remarks>
public interface ITenantModules
{
    IReadOnlySet<ModuleId> Enabled { get; }

    bool IsEnabled(ModuleId module);

    /// <summary>Lanza si el módulo no está habilitado. Para usar en bordes, no en dominio.</summary>
    void Require(ModuleId module)
    {
        if (!IsEnabled(module)) throw new ModuleDisabledException(module);
    }
}

/// <summary>
/// Se intentó usar un módulo que el club no tiene contratado. En el borde HTTP se traduce a
/// <b>404, no 403</b>: quien no contrató un módulo no tiene por qué enterarse de que existe.
/// </summary>
public sealed class ModuleDisabledException(ModuleId module)
    : InvalidOperationException($"El módulo '{module}' no está habilitado para este club.")
{
    public ModuleId Module { get; } = module;
}
