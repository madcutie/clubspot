using ClubSpot.Domain.Bookings;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ClubSpot.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class AvailabilityOverrideEndpointsTests(PostgresFixture postgres)
{
    private static readonly TenantId SeedTenant = TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa"));

    [Fact]
    public async Task A_club_override_with_two_dates_is_created_and_listed()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var firstDate = Today().AddDays(20);
        var secondDate = firstDate.AddDays(2);

        var create = await client.PostAsJsonAsync("/api/availability-overrides/", new
        {
            courtId = (Guid?)null, dates = new[] { firstDate, secondDate }, windows = Array.Empty<TimeRange>(), reason = "Holiday"
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();
        var listed = await client.GetFromJsonAsync<OverrideResponse[]>($"/api/availability-overrides/?from={firstDate:O}&to={secondDate:O}");
        var found = Assert.Single(listed!, item => item.Id == created!.Id);
        Assert.Null(found.CourtId);
        Assert.Equal([firstDate, secondDate], found.Dates.Order().ToArray());
        Assert.Empty(found.Windows);
    }

    [Fact]
    public async Task Creating_an_override_without_a_token_is_unauthorized()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/availability-overrides/", new
        {
            dates = new[] { Today().AddDays(20) }, windows = Array.Empty<TimeRange>()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_override_for_an_unknown_court_is_rejected()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);

        var response = await client.PostAsJsonAsync("/api/availability-overrides/", new
        {
            courtId = Guid.NewGuid(), dates = new[] { Today().AddDays(20) }, windows = Array.Empty<TimeRange>()
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task An_override_with_overlapping_windows_is_rejected()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);

        var response = await client.PostAsJsonAsync("/api/availability-overrides/", new
        {
            dates = new[] { Today().AddDays(20) }, windows = new[] { new TimeRange(480, 720), new TimeRange(600, 900) }
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task An_override_without_dates_is_rejected()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);

        var response = await client.PostAsJsonAsync("/api/availability-overrides/", new
        {
            dates = Array.Empty<DateOnly>(), windows = Array.Empty<TimeRange>()
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_override_cascades_its_dates()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var date = Today().AddDays(25);
        var create = await client.PostAsJsonAsync("/api/availability-overrides/", new
        {
            dates = new[] { date, date.AddDays(1) }, windows = Array.Empty<TimeRange>()
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var delete = await client.DeleteAsync($"/api/availability-overrides/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        Assert.Equal(0, await db.Set<AvailabilityOverrideDate>().CountAsync(item => item.OverrideId == created.Id));
    }

    [Fact]
    public async Task Deleting_an_unknown_override_returns_not_found()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);

        var response = await client.DeleteAsync($"/api/availability-overrides/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task AuthorizeAsync(HttpClient client)
    {
        var sessionResponse = await client.PostAsJsonAsync("/api/auth/session", new
        {
            club = "chaco-for-ever", email = "admin@chacoforever.test", password = "clubspot-dev"
        });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session!.AccessToken);
    }

    private sealed record SessionResponse(string AccessToken);
    private sealed record IdResponse(Guid Id);
    private sealed record OverrideResponse(Guid Id, Guid? CourtId, IReadOnlyList<DateOnly> Dates, IReadOnlyList<TimeRange> Windows, string? Reason);
}
