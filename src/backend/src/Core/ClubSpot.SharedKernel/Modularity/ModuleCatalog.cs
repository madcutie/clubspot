namespace ClubSpot.SharedKernel.Modularity;

/// <summary>
/// Todos los módulos que el producto sabe ofrecer, con su grafo de dependencias validado.
/// Es único para el producto: qué módulos tiene contratado cada club lo dice
/// <see cref="ITenantModules"/>.
/// </summary>
public sealed class ModuleCatalog
{
    private readonly Dictionary<ModuleId, IClubModule> _modules;

    public ModuleCatalog(IEnumerable<IClubModule> modules)
    {
        _modules = [];

        foreach (var module in modules)
        {
            if (!_modules.TryAdd(module.Id, module))
                throw new InvalidOperationException(
                    $"El módulo '{module.Id}' está declarado más de una vez.");
        }

        Validate();
    }

    public IReadOnlyCollection<IClubModule> All => _modules.Values;

    public IReadOnlyCollection<ModuleId> CoreModules =>
        [.. _modules.Values.Where(m => m.IsCore).Select(m => m.Id)];

    public IClubModule Get(ModuleId id) =>
        _modules.TryGetValue(id, out var module)
            ? module
            : throw new UnknownModuleException(id);

    public bool Exists(ModuleId id) => _modules.ContainsKey(id);

    /// <summary>
    /// Expande una selección de módulos a su cierre transitivo e incorpora los del núcleo.
    /// Contratar <c>padel</c> trae <c>bookings</c>, <c>finance</c> y <c>core</c> sin que
    /// nadie tenga que acordarse de tildarlos.
    /// </summary>
    public IReadOnlySet<ModuleId> Resolve(IEnumerable<ModuleId> selected)
    {
        var resolved = new HashSet<ModuleId>(CoreModules);
        foreach (var id in selected) Walk(id, resolved);
        return resolved;

        void Walk(ModuleId id, HashSet<ModuleId> acc)
        {
            if (!acc.Add(id)) return;
            foreach (var dependency in Get(id).DependsOn) Walk(dependency, acc);
        }
    }

    /// <summary>
    /// Módulos habilitados que dejarían de tener sus dependencias si se apagara
    /// <paramref name="id"/>. Si devuelve algo, el apagado se rechaza.
    /// </summary>
    public IReadOnlyCollection<ModuleId> DependentsOf(ModuleId id, IReadOnlySet<ModuleId> enabled) =>
        [.. enabled.Where(other => other != id && Get(other).DependsOn.Contains(id))];

    private void Validate()
    {
        foreach (var module in _modules.Values)
        {
            foreach (var dependency in module.DependsOn)
            {
                if (!_modules.ContainsKey(dependency))
                    throw new InvalidOperationException(
                        $"El módulo '{module.Id}' depende de '{dependency}', que no está declarado.");
            }
        }

        // Un ciclo haría que Resolve nunca termine y que no exista un orden de arranque válido.
        var visiting = new HashSet<ModuleId>();
        var done = new HashSet<ModuleId>();
        foreach (var module in _modules.Values) DetectCycle(module.Id, visiting, done, []);
    }

    private void DetectCycle(ModuleId id, HashSet<ModuleId> visiting, HashSet<ModuleId> done, List<ModuleId> path)
    {
        if (done.Contains(id)) return;

        if (!visiting.Add(id))
            throw new InvalidOperationException(
                $"Dependencia circular entre módulos: {string.Join(" → ", path.Append(id))}.");

        path.Add(id);
        foreach (var dependency in _modules[id].DependsOn) DetectCycle(dependency, visiting, done, path);
        path.RemoveAt(path.Count - 1);

        visiting.Remove(id);
        done.Add(id);
    }
}

public sealed class UnknownModuleException(ModuleId id)
    : InvalidOperationException($"El módulo '{id}' no existe en el catálogo del producto.");
