using System.Threading.RateLimiting;

namespace ClubSpot.Api.Endpoints;

// Rate limits for the anonymous surface. The webhook is deliberately not limited: Mercado Pago
// retries until it gets a 2xx, so a 429 there would turn a burst into a retry storm.
public static class PortalRateLimits
{
    public const string Reads = "portal-reads";
    public const string Bookings = "portal-bookings";

    public static Func<HttpContext, RateLimitPartition<string>> PerCallerAndClub(int permitPerMinute) =>
        context => RateLimitPartition.GetFixedWindowLimiter(
            $"{Caller(context)}|{context.Request.RouteValues["clubSlug"]}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

    // Behind a reverse proxy the connection address is the proxy's: honour a forwarded address when
    // one is configured upstream, and fall back to a single shared bucket rather than to no limit.
    private static string Caller(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString()
        ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
        ?? "unknown";
}
