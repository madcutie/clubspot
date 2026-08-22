using Serilog.Context;

namespace ClubSpot.Api.Observability;

// First in the pipeline, before the exception handler: an unhandled 500 is exactly the line that has
// to say which request it came from. The tenant is not known yet — that one is pushed where it is
// resolved, and both reach every log line written downstream.
public sealed class RequestLogContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using (LogContext.PushProperty("requestId", context.TraceIdentifier))
        using (LogContext.PushProperty("method", context.Request.Method))
        using (LogContext.PushProperty("path", context.Request.Path.Value))
        {
            await next(context);
        }
    }
}
