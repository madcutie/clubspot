namespace ClubSpot.SharedKernel.Modularity;

/// <summary>
/// Declaración de un módulo del producto. Cada módulo se describe a sí mismo: qué es, de qué
/// depende y si se puede apagar. El catálogo se arma leyendo estas declaraciones, no una lista
/// escrita a mano en otro lado.
/// </summary>
public interface IClubModule
{
    ModuleId Id { get; }

    /// <summary>Nombre comercial, el que ve quien contrata. Puede cambiar sin romper nada.</summary>
    string DisplayName { get; }

    /// <summary>Qué resuelve, en una línea. Se muestra en la pantalla de contratación.</summary>
    string Description { get; }

    /// <summary>
    /// Módulos sin los cuales éste no puede funcionar. Habilitar uno habilita su cierre
    /// transitivo; deshabilitar uno del que otro depende se rechaza.
    /// </summary>
    IReadOnlyCollection<ModuleId> DependsOn { get; }

    /// <summary>
    /// Un módulo núcleo está siempre activo y no se puede contratar ni dar de baja.
    /// Sin él el sistema no es utilizable por nadie.
    /// </summary>
    bool IsCore { get; }
}

/// <summary>Base con los valores por defecto que casi todos los módulos comparten.</summary>
public abstract class ClubModuleBase : IClubModule
{
    public abstract ModuleId Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual IReadOnlyCollection<ModuleId> DependsOn { get; } = [];
    public virtual bool IsCore => false;
}
