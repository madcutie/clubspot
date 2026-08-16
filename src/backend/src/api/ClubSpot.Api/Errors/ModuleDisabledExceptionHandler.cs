using ClubSpot.SharedKernel.Modularity;
using Microsoft.AspNetCore.Diagnostics;

namespace ClubSpot.Api.Errors;

public sealed class ModuleDisabledExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ModuleDisabledException) return ValueTask.FromResult(false);

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return ValueTask.FromResult(true);
    }
}
