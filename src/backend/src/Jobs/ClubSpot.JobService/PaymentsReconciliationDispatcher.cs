using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Tenancy;
using Hangfire;
using Hangfire.Storage;

namespace ClubSpot.JobService;

// One run every 5 minutes: iterates the clubs and reconciles each one under its own explicit
// tenant scope. A failing tenant never takes the others down.
public sealed class PaymentsReconciliationDispatcher(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopeFactory,
    ILogger<PaymentsReconciliationDispatcher> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantId> clubs;
        using (var scope = scopeFactory.CreateScope())
        {
            if (scope.ServiceProvider.GetService<IPaymentGateway>() is null)
            {
                logger.LogWarning("No payment gateway configured; reconciliation skipped.");
                return;
            }
            clubs = await scope.ServiceProvider.GetRequiredService<IClubDirectory>()
                .GetAllClubIdsAsync(cancellationToken);
        }

        foreach (var club in clubs)
        {
            try
            {
                await RunForTenantAsync(club, cancellationToken);
            }
            catch (DistributedLockTimeoutException)
            {
                logger.LogWarning("Tenant {Tenant} is already being reconciled; skipped.", club.Value);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reconciliation failed for tenant {Tenant}.", club.Value);
            }
        }
    }

    private async Task RunForTenantAsync(TenantId tenant, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        using var tenantScope = tenantScopeFactory.BeginScope(tenant);

        // Module gating at the edge: the job never runs for a club without bookings.
        if (!scope.ServiceProvider.GetRequiredService<ITenantModules>().IsEnabled(ModuleId.Bookings)) return;

        using var jobLock = JobStorage.Current.GetConnection()
            .AcquireDistributedLock($"reconcile-payments:{tenant.Value}", TimeSpan.Zero);

        var result = await scope.ServiceProvider.GetRequiredService<ReconcileOnlinePaymentsHandler>()
            .HandleAsync(cancellationToken);
        // Provisional run record until real metrics exist (observability phase).
        logger.LogInformation(
            "Reconciliation for tenant {Tenant}: {Candidates} candidates, {Applied} applied, {Orphaned} orphaned.",
            tenant.Value, result.Candidates, result.Applied, result.Orphaned);
    }
}
