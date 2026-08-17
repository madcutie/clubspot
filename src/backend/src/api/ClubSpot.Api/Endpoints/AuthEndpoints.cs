using ClubSpot.Api.Auth;
using ClubSpot.Application.Core;
using ClubSpot.Application.Core.Users;
using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/session", SignInAsync).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> SignInAsync(
        SignInRequest request,
        IClubDirectory clubDirectory,
        ITenantScopeFactory tenantScopeFactory,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        JwtIssuer jwtIssuer,
        CancellationToken cancellationToken)
    {
        var clubId = await clubDirectory.FindClubIdBySlugAsync(request.Club, cancellationToken);
        if (clubId is null) return Results.Unauthorized();

        using var tenantScope = tenantScopeFactory.BeginScope(clubId.Value);
        var user = await users.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, request.Password))
            return Results.Unauthorized();

        return Results.Ok(new SessionResponse(jwtIssuer.Issue(user)));
    }

    private sealed record SignInRequest(string Club, string Email, string Password);
    private sealed record SessionResponse(string AccessToken);
}
