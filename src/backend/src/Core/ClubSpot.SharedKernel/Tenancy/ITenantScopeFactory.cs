namespace ClubSpot.SharedKernel.Tenancy;

// HTTP: the middleware opens one scope per request from the token.
// Background: the process opens it with the tenant it received as an explicit parameter.
public interface ITenantScopeFactory
{
    IDisposable BeginScope(TenantId tenant);
}
