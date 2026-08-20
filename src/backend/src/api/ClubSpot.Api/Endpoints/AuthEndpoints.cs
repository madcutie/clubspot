using ClubSpot.Api.Auth;
using ClubSpot.Application.Core.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ClubSpot.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/session", SignInAsync).AllowAnonymous().WithName("SignIn").WithTags("auth");
        return app;
    }

    private static async Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult, StatusCodeHttpResult>> SignInAsync(
        SignInRequest request,
        HttpContext context,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        JwtIssuer jwtIssuer,
        SignInThrottle throttle,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return TypedResults.Unauthorized();

        // Deliberately the connection address, like the portal's limiter: X-Forwarded-For is
        // caller-supplied, so trusting it here would hand an attacker a fresh budget per request.
        var caller = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (throttle.IsBlocked(request.Email, caller))
            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);

        var user = await users.FindForSignInAsync(request.Email, cancellationToken);
        if (user is null)
        {
            // Hash anyway: an unknown email has to cost the same as a wrong password, or sign-in
            // becomes an oracle that tells which emails exist.
            passwordHasher.Hash(request.Password);
            throttle.RecordFailure(request.Email, caller);
            return TypedResults.Unauthorized();
        }

        // Verify before looking at IsActive, for the same reason.
        if (!passwordHasher.Verify(user.PasswordHash, request.Password) || !user.IsActive)
        {
            throttle.RecordFailure(request.Email, caller);
            return TypedResults.Unauthorized();
        }

        throttle.RecordSuccess(request.Email);
        return TypedResults.Ok(new SessionResponse(jwtIssuer.Issue(user)));
    }

    internal sealed record SignInRequest(string Email, string Password);
    internal sealed record SessionResponse(string AccessToken);
}
