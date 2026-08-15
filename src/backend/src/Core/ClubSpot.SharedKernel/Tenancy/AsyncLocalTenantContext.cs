namespace ClubSpot.SharedKernel.Tenancy;

public sealed class AsyncLocalTenantContext : ITenantContext, ITenantScopeFactory
{
    // Static on purpose: the scope belongs to the async flow, not to the instance.
    private static readonly AsyncLocal<TenantId?> Ambient = new();

    public bool HasTenant => Ambient.Value is not null;

    public TenantId Current =>
        Ambient.Value ?? throw new MissingTenantException("ITenantContext.Current");

    public IDisposable BeginScope(TenantId tenant)
    {
        var previous = Ambient.Value;
        Ambient.Value = tenant;
        return new Scope(previous);
    }

    private sealed class Scope(TenantId? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Ambient.Value = previous;
        }
    }
}
