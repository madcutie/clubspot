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

    // Deliberately the connection address and nothing else: X-Forwarded-For is caller-supplied, so
    // reading it here would hand an attacker a fresh bucket per request. Behind a reverse proxy the
    // fix is UseForwardedHeaders with the proxy whitelisted, which rewrites RemoteIpAddress itself.
    private static string Caller(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
