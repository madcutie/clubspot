using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.SharedKernel.Activity;
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
    IActivityActorScopeFactory actorScopeFactory,
    ILogger<PaymentsReconciliationDispatcher> logger)
{
    public const string JobName = "payments-reconciliation";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantId> clubs;
        using (var scope = scopeFactory.CreateScope())
        {
            if (!scope.ServiceProvider.GetRequiredService<IEnumerable<IPaymentProvider>>().Any())
            {
                logger.LogWarning("No payment provider configured; reconciliation skipped.");
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
        // Applying a payment writes to the activity log, and that refuses to record without knowing
        // who acted. Without this the job throws the moment it finds money to apply — which is the
        // only moment it matters.
        using var actorScope = actorScopeFactory.BeginScope(ActivityActor.Job(JobName));

        // Module gating at the edge: the job never runs for a club without bookings.
        if (!scope.ServiceProvider.GetRequiredService<ITenantModules>().IsEnabled(ModuleId.Bookings)) return;

        using var jobLock = JobStorage.Current.GetConnection()
            .AcquireDistributedLock($"reconcile-payments:{tenant.Value}", TimeSpan.Zero);

        var results = await scope.ServiceProvider.GetRequiredService<ReconcileOnlinePaymentsHandler>()
            .HandleAsync(cancellationToken);
        // Provisional run record until real metrics exist (observability phase).
        foreach (var result in results)
            logger.LogInformation(
                "Reconciliation for tenant {Tenant} via {Provider}: {Candidates} candidates, {Applied} applied, {Orphaned} orphaned.",
                tenant.Value, result.Provider, result.Candidates, result.Applied, result.Orphaned);
    }
}
