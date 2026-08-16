using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Domain.Core;

public sealed class ClubModule : ITenantOwned
{
    public TenantId TenantId { get; private set; }
    public ModuleId ModuleId { get; private set; }
    public DateTimeOffset ContractedAt { get; private set; }

    public ClubModule(TenantId tenantId, ModuleId moduleId, DateTimeOffset contractedAt)
    {
        TenantId = tenantId;
        ModuleId = moduleId;
        ContractedAt = contractedAt;
    }

    private ClubModule()
    {
    }
}
