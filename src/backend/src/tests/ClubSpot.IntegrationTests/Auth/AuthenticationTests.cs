using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using ClubSpot.Api.Auth;
using ClubSpot.Domain.Core;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using ClubSpot.SharedKernel.Time;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClubSpot.IntegrationTests.Auth;

[Collection("postgres")]
public sealed class AuthenticationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_valid_session_request_returns_a_token_with_tenant_and_roles()
    {
        var club = NewClub("club-auth", "Club Auth");
        var user = NewUser(club.Id, "operator@clubspot.test", "Court Operator", Role.CourtReception);
        await SaveAsync(club, user);

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
            email = "operator@clubspot.test",
            password = "correct-password"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal(club.Id.Value.ToString(), token.Claims.Single(claim => claim.Type == "tenant").Value);
        Assert.Equal("Court Operator", token.Claims.Single(claim => claim.Type == "name").Value);
        Assert.Equal("courtReception", token.Claims.Single(claim => claim.Type == "role").Value);
    }

    [Fact]
    public async Task The_club_comes_from_the_user_not_from_the_request()
    {
        var first = NewClub("club-origin-a", "Club A");
        var second = NewClub("club-origin-b", "Club B");
        var here = NewUser(first.Id, "here@clubspot.test", "Here", Role.CourtReception);
        var there = NewUser(second.Id, "there@clubspot.test", "There", Role.Administrator);
        await SaveAsync(first, here);
        await SaveAsync(second, there);

        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        Assert.Equal(first.Id.Value.ToString(), await TenantOfSessionAsync(client, "here@clubspot.test"));
        Assert.Equal(second.Id.Value.ToString(), await TenantOfSessionAsync(client, "there@clubspot.test"));
    }

    [Fact]
    public async Task The_same_email_cannot_exist_in_two_clubs()
    {
        var first = NewClub("club-shared-a", "Club Shared A");
        var second = NewClub("club-shared-b", "Club Shared B");
        await SaveAsync(first, NewUser(first.Id, "shared@clubspot.test", "First", Role.Administrator));

        var conflict = await Assert.ThrowsAsync<DbUpdateException>(() =>
            SaveAsync(second, NewUser(second.Id, "shared@clubspot.test", "Second", Role.Administrator)));

        Assert.Contains("uxUsersEmail", conflict.InnerException?.Message ?? conflict.Message);
    }

    [Fact]
    public async Task Guessing_one_password_runs_out_of_attempts()
    {
        var club = NewClub("club-throttle", "Club Throttle");
        var user = NewUser(club.Id, "throttled@clubspot.test", "Court Operator", Role.CourtReception);
        await SaveAsync(club, user);

        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < SignInThrottle.MaxFailuresPerAccount; attempt++)
        {
            var refused = await client.PostAsJsonAsync("/api/auth/session", new
            {
                email = "throttled@clubspot.test", password = $"guess-{attempt}"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/session", new
        {
            email = "throttled@clubspot.test", password = "guess-again"
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        // The right password is refused too while the block holds: a throttle that the attacker can
        // step past by guessing correctly is not a throttle.
        var correct = await client.PostAsJsonAsync("/api/auth/session", new
        {
            email = "throttled@clubspot.test", password = "correct-password"
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }

    [Fact]
    public async Task Signing_in_correctly_is_never_throttled()
    {
        var club = NewClub("club-not-throttled", "Club Not Throttled");
        var user = NewUser(club.Id, "steady@clubspot.test", "Court Operator", Role.CourtReception);
        await SaveAsync(club, user);

        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        // Well past the per-account budget: only failures count, so a whole shift signing in is fine.
        for (var attempt = 0; attempt < SignInThrottle.MaxFailuresPerAccount + 5; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/session", new
            {
                email = "steady@clubspot.test", password = "correct-password"
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Invalid_credentials_return_a_generic_unauthorized_response()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var unknownEmail = await client.PostAsJsonAsync("/api/auth/session", new
        {
            email = "missing@clubspot.test",
            password = "wrong-password"
        });
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/session", new
        {
            email = "admin@chacoforever.test",
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(
            await unknownEmail.Content.ReadAsStringAsync(),
            await wrongPassword.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_seeded_administrator_can_read_the_enabled_module_context()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", await TokenAsync(client, "admin@chacoforever.test"));

        var contextResponse = await client.GetAsync("/api/context");
        var context = await contextResponse.Content.ReadFromJsonAsync<ContextResponse>(TestJsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, contextResponse.StatusCode);
        Assert.Equal(["bookings", "core", "finance", "members"], context!.Modules.Order());
        Assert.Equal("Club Atlético Chaco For Ever", context.Club.Name);
    }

    [Fact]
    public async Task The_court_reception_operates_the_agenda_but_does_not_configure_the_club()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", await TokenAsync(client, "reception@chacoforever.test"));

        var agenda = await client.GetAsync($"/api/agenda?sport=padel&date={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
        var courts = await client.GetAsync("/api/courts");
        var schedules = await client.GetAsync("/api/schedules");
        var people = await client.GetAsync("/api/people");

        Assert.Equal(HttpStatusCode.OK, agenda.StatusCode);
        Assert.Equal(HttpStatusCode.OK, people.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, courts.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, schedules.StatusCode);
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

    [Fact]
    public async Task The_token_expires_twelve_hours_after_the_clock_says_it_was_issued()
    {
        var club = NewClub("club-expiry", "Club Expiry");
        var user = NewUser(club.Id, "expiry@clubspot.test", "Expiry", Role.CourtReception);
        await SaveAsync(club, user);

        var frozen = new DateTimeOffset(2026, 3, 4, 9, 15, 0, TimeSpan.Zero);
        using var factory = new ApiFactory(postgres).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.Replace(
                ServiceDescriptor.Singleton<IClock>(new FrozenClock(frozen)))));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/session", new
        {
            email = "expiry@clubspot.test",
            password = "correct-password"
        });
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(session!.AccessToken);

        Assert.Equal(frozen.UtcDateTime.AddHours(12), token.ValidTo);
    }

    private static Club NewClub(string slug, string name) => new(
        TenantId.From(Guid.NewGuid()),
        slug,
        name,
        null,
        "America/Argentina/Buenos_Aires",
        "ARS",
        50,
        DateTimeOffset.UtcNow);

    private static User NewUser(TenantId tenantId, string email, string name, Role role) => new(
        Guid.NewGuid(),
        tenantId,
        email,
        name,
        new PasswordHasher<User>().HashPassword(null!, "correct-password"),
        [role],
        DateTimeOffset.UtcNow);

    private async Task SaveAsync(Club club, User user)
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        if (!await db.Clubs.AnyAsync(candidate => candidate.Id == club.Id))
        {
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
        }

        using var tenantScope = tenantContext.BeginScope(club.Id);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private static async Task<string> TokenAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/session", new { email, password = "clubspot-dev" });
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        return session!.AccessToken;
    }

    private static async Task<string> TenantOfSessionAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/session", new { email, password = "correct-password" });
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(session!.AccessToken);
        return token.Claims.Single(claim => claim.Type == "tenant").Value;
    }

    private sealed record SessionResponse(string AccessToken);
    private sealed record ClubResponse(string Name, string? Venue);
    private sealed record ContextResponse(ClubResponse Club, IEnumerable<string> Modules);

    private sealed class FrozenClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
