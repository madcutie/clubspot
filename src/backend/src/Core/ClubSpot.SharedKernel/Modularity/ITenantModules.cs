namespace ClubSpot.SharedKernel.Modularity;

// Consulted only at the edges (endpoint filter, job dispatcher, capabilities endpoint).
// Domain logic never asks whether a module is enabled.
public interface ITenantModules
{
    IReadOnlySet<ModuleId> Enabled { get; }

    bool IsEnabled(ModuleId module);

    void Require(ModuleId module)
    {
        if (!IsEnabled(module)) throw new ModuleDisabledException(module);
    }
}

// Translated to 404 at the HTTP edge — never 403: a club that did not contract a module
// must not learn it exists.
public sealed class ModuleDisabledException(ModuleId module)
    : InvalidOperationException($"Module '{module}' is not enabled for this club.")
{
    public ModuleId Module { get; } = module;
}
