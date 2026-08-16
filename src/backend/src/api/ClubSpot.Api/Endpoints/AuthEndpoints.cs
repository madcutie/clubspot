using ClubSpot.Api.Auth;
using ClubSpot.Application.Core.Users;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

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
        CoreDbContext db,
        ITenantScopeFactory tenantScopeFactory,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        JwtIssuer jwtIssuer,
        CancellationToken cancellationToken)
    {
        var club = await db.Clubs.SingleOrDefaultAsync(club => club.Slug == request.Club.Trim(), cancellationToken);
        if (club is null) return Results.Unauthorized();

        using var tenantScope = tenantScopeFactory.BeginScope(club.Id);
        var user = await users.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, request.Password))
            return Results.Unauthorized();

        return Results.Ok(new SessionResponse(jwtIssuer.Issue(user)));
    }

    private sealed record SignInRequest(string Club, string Email, string Password);
    private sealed record SessionResponse(string AccessToken);
}
