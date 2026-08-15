namespace ClubSpot.SharedKernel.Modularity;

public interface IClubModule
{
    ModuleId Id { get; }
    string DisplayName { get; }
    string Description { get; }
    IReadOnlyCollection<ModuleId> DependsOn { get; }
    bool IsCore { get; }
}

public abstract class ClubModuleBase : IClubModule
{
    public abstract ModuleId Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual IReadOnlyCollection<ModuleId> DependsOn { get; } = [];
    public virtual bool IsCore => false;
}
