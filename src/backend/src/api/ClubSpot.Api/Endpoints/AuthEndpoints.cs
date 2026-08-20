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

    private static async Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult>> SignInAsync(
        SignInRequest request,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        JwtIssuer jwtIssuer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return TypedResults.Unauthorized();

        var user = await users.FindForSignInAsync(request.Email, cancellationToken);
        if (user is null)
        {
            // Hash anyway: an unknown email has to cost the same as a wrong password, or sign-in
            // becomes an oracle that tells which emails exist.
            passwordHasher.Hash(request.Password);
            return TypedResults.Unauthorized();
        }

        // Verify before looking at IsActive, for the same reason.
        return passwordHasher.Verify(user.PasswordHash, request.Password) && user.IsActive
            ? TypedResults.Ok(new SessionResponse(jwtIssuer.Issue(user)))
            : TypedResults.Unauthorized();
    }

    internal sealed record SignInRequest(string Email, string Password);
    internal sealed record SessionResponse(string AccessToken);
}
