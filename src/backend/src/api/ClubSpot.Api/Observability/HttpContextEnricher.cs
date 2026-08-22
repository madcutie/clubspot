using Serilog.Core;
using Serilog.Events;

namespace ClubSpot.Api.Observability;

// `tenant` and `userId` are resolved deeper than the exception handler sits, so a LogContext scope
// opened there is already popped by the time an unhandled exception is logged — the 500, which is the
// line that most needs to name its club, was coming out without either. Reading them off
// `HttpContext.Items` when the event is written instead of when the scope opens makes them survive
// the unwind, and covers the anonymous surfaces too: the portal and the payment webhooks resolve
// their club in an endpoint filter and never pass through a middleware that could push anything.
public sealed class HttpContextEnricher : ILogEventEnricher
{
    public const string TenantKey = "tenant";
    public const string UserIdKey = "userId";

    // Every HttpContextAccessor shares one AsyncLocal, so this needs nothing from DI — which matters
    // because the logger is built before the container exists.
    private readonly IHttpContextAccessor accessor = new HttpContextAccessor();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (accessor.HttpContext is not { } context) return;
        Add(logEvent, propertyFactory, context, TenantKey);
        Add(logEvent, propertyFactory, context, UserIdKey);
    }

    private static void Add(LogEvent logEvent, ILogEventPropertyFactory factory, HttpContext context, string key)
    {
        if (context.Items.TryGetValue(key, out var value) && value is not null)
            logEvent.AddPropertyIfAbsent(factory.CreateProperty(key, value));
    }
}
