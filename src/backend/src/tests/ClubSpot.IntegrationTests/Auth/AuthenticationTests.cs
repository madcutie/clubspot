using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using ClubSpot.Domain.Core;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubSpot.IntegrationTests.Auth;

[Collection("postgres")]
public sealed class AuthenticationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_valid_session_request_returns_a_token_with_tenant_and_roles()
    {
        var club = new Club(
            TenantId.From(Guid.NewGuid()),
            "club-auth",
            "Club Auth",
            null,
            "America/Argentina/Buenos_Aires",
            "ARS",
            50,
            DateTimeOffset.UtcNow);
        var passwordHasher = new PasswordHasher<User>();
        var user = new User(
            Guid.NewGuid(),
            club.Id,
            "operator@clubspot.test",
            "Court Operator",
            passwordHasher.HashPassword(null!, "correct-password"),
            [Role.CourtReception],
            DateTimeOffset.UtcNow);

        var tenantContext = new AsyncLocalTenantContext();
        await using (var db = postgres.CreateDbContext(tenantContext))
        {
            db.Clubs.Add(club);
            await db.SaveChangesAsync();

            using var tenantScope = tenantContext.BeginScope(club.Id);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        using var factory = new ApiFactory(postgres);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var apiTenantScopeFactory = scope.ServiceProvider.GetRequiredService<ITenantScopeFactory>();
            using var apiTenantScope = apiTenantScopeFactory.BeginScope(club.Id);
            var apiDb = scope.ServiceProvider.GetRequiredService<ClubSpotDbContext>();
            Assert.NotNull(await apiDb.Users.SingleOrDefaultAsync(candidate => candidate.Email == user.Email));
        }
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/session", new
        {
            club = club.Slug,
            email = "operator@clubspot.test",
            password = "correct-password"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal(club.Id.Value.ToString(), token.Claims.Single(claim => claim.Type == "tenant").Value);
        Assert.Contains(token.Claims, claim => claim.Type.EndsWith("role") && claim.Value == Role.CourtReception.ToString());
    }

    [Fact]
    public async Task Invalid_credentials_return_a_generic_unauthorized_response()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/session", new
        {
            club = "missing-club",
            email = "missing@clubspot.test",
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_seeded_administrator_can_read_the_enabled_module_context()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var sessionResponse = await client.PostAsJsonAsync("/api/auth/session", new
        {
            club = "chaco-for-ever",
            email = "admin@chacoforever.test",
            password = "clubspot-dev"
        });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();

        client.DefaultRequestHeaders.Authorization = new("Bearer", session!.AccessToken);
        var contextResponse = await client.GetAsync("/api/context");
        var context = await contextResponse.Content.ReadFromJsonAsync<ContextResponse>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, contextResponse.StatusCode);
        Assert.Equal(["bookings", "core", "finance", "members"], context!.Modules.Order());
        Assert.Equal("Club Atlético Chaco For Ever", context.Club.Name);
        Assert.Equal("Administrador", context.Operator.Name);
        Assert.Equal([Role.Administrator], context.Operator.Roles);
    }

    [Fact]
    public async Task Context_requires_a_token_and_allows_the_backoffice_origin()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var unauthorized = await client.GetAsync("/api/context");
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/context");
        preflight.Headers.Add("Origin", "http://localhost:5184");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");
        var cors = await client.SendAsync(preflight);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cors.StatusCode);
        Assert.Contains("http://localhost:5184", cors.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    private sealed record SessionResponse(string AccessToken);
    private sealed record ClubResponse(string Name, string? Venue);
    private sealed record OperatorResponse(string Name, IReadOnlyCollection<Role> Roles);
    private sealed record ContextResponse(ClubResponse Club, OperatorResponse Operator, IEnumerable<string> Modules);
}
