using ClubSpot.Domain.Bookings;
using ClubSpot.Domain.Core;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ClubSpot.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class PortalEndpointsTests(PostgresFixture postgres)
{
    private static readonly TenantId SeedTenant = TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa"));

    [Fact]
    public async Task The_catalog_lists_the_seeded_courts_anonymously()
    {
        await ResetBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var catalog = await client.GetFromJsonAsync<CatalogResponse>("/api/portal/chaco-for-ever/catalog", TestJsonOptions.Default);

        Assert.NotNull(catalog);
        Assert.Equal(50, catalog.Club.DepositPercent);
        Assert.Equal("ARS", catalog.Club.Currency);
        var padel = Assert.Single(catalog.Sports, group => group.Sport == Sport.Padel);
        Assert.Equal(["Cancha 1", "Cancha 2"], padel.Courts.Select(court => court.Name).Order().ToArray());
        var football = Assert.Single(catalog.Sports, group => group.Sport == Sport.Football);
        Assert.Equal(["Fútbol A"], football.Courts.Select(court => court.Name).ToArray());
    }

    [Fact]
    public async Task Availability_offers_slots_for_both_padel_courts()
    {
        await ResetBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var from = Today().AddDays(7);
        var to = from.AddDays(1);

        var response = await client.GetAsync($"/api/portal/chaco-for-ever/availability?sport=padel&from={from:O}&to={to:O}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availability = await response.Content.ReadFromJsonAsync<AvailabilityResponse>(TestJsonOptions.Default);
        Assert.Equal("ARS", availability!.Currency);
        Assert.Equal(2, availability.Days.Count);
        Assert.All(availability.Days, day =>
        {
            Assert.Equal(2, day.Courts.Count);
            Assert.All(day.Courts, court => Assert.NotEmpty(court.Slots));
        });
    }

    [Fact]
    public async Task An_unknown_sport_is_a_bad_request()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var from = Today().AddDays(7);

        var response = await client.GetAsync($"/api/portal/chaco-for-ever/availability?sport=tennis&from={from:O}&to={from:O}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_most_specific_override_wins()
    {
        await ResetBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var portal = factory.CreateClient();
        using var backoffice = factory.CreateClient();
        await AuthorizeAsync(backoffice);
        var date = Today().AddDays(10);
        var catalog = await portal.GetFromJsonAsync<CatalogResponse>("/api/portal/chaco-for-ever/catalog", TestJsonOptions.Default);
        var padelCourts = catalog!.Sports.Single(group => group.Sport == Sport.Padel).Courts;
        var courtOne = padelCourts.Single(court => court.Name == "Cancha 1");
        var courtTwo = padelCourts.Single(court => court.Name == "Cancha 2");

        var clubClosed = await backoffice.PostAsJsonAsync("/api/availability-overrides/", new
        {
            dates = new[] { date }, windows = Array.Empty<TimeRange>(), reason = "Holiday"
        });
        Assert.Equal(HttpStatusCode.Created, clubClosed.StatusCode);
        var closedDay = Assert.Single((await GetAvailabilityAsync(portal, date)).Days);
        Assert.All(closedDay.Courts, court => Assert.Empty(court.Slots));

        var courtWindow = await backoffice.PostAsJsonAsync("/api/availability-overrides/", new
        {
            courtId = courtOne.Id, dates = new[] { date }, windows = new[] { new TimeRange(600, 720) }
        });
        Assert.Equal(HttpStatusCode.Created, courtWindow.StatusCode);

        var reopenedDay = Assert.Single((await GetAvailabilityAsync(portal, date)).Days);
        var courtOneSlots = reopenedDay.Courts.Single(court => court.CourtId == courtOne.Id).Slots;
        Assert.NotEmpty(courtOneSlots);
        Assert.All(courtOneSlots, slot => Assert.True(slot.StartMinute >= 600 && slot.StartMinute + slot.Duration <= 720));
        Assert.Empty(reopenedDay.Courts.Single(court => court.CourtId == courtTwo.Id).Slots);
    }

    [Fact]
    public async Task An_unknown_slug_is_not_found()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/portal/no-such-club/catalog");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_club_without_the_bookings_module_is_not_found()
    {
        const string slug = "club-sin-reservas";
        await using (var db = postgres.CreateDbContext())
        {
            if (!await db.Clubs.AnyAsync(club => club.Slug == slug))
            {
                db.Clubs.Add(new Club(TenantId.From(Guid.NewGuid()), slug, "Club Sin Reservas", null,
                    "America/Argentina/Buenos_Aires", "ARS", 50, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }
        }
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/portal/{slug}/catalog");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<AvailabilityResponse> GetAvailabilityAsync(HttpClient client, DateOnly date)
    {
        var response = await client.GetAsync($"/api/portal/chaco-for-ever/availability?sport=padel&from={date:O}&to={date:O}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AvailabilityResponse>(TestJsonOptions.Default))!;
    }

    private static async Task AuthorizeAsync(HttpClient client)
    {
        var sessionResponse = await client.PostAsJsonAsync("/api/auth/session", new
        {
            club = "chaco-for-ever", email = "admin@chacoforever.test", password = "clubspot-dev"
        });
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session!.AccessToken);
    }

    private async Task ResetBookingsAsync()
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        db.AvailabilityOverrides.RemoveRange(db.AvailabilityOverrides);
        db.Courts.RemoveRange(db.Courts);
        db.Schedules.RemoveRange(db.Schedules);
        await db.SaveChangesAsync();
    }

    private sealed record SessionResponse(string AccessToken);
    private sealed record ClubResponse(string Name, string? Venue, string Currency, int DepositPercent);
    private sealed record CourtResponse(Guid Id, string Name, string Detail, bool IsCovered, int[] Durations);
    private sealed record SportResponse(Sport Sport, IReadOnlyList<CourtResponse> Courts);
    private sealed record CatalogResponse(ClubResponse Club, IReadOnlyList<SportResponse> Sports);
    private sealed record SlotResponse(int StartMinute, int Duration, decimal Price);
    private sealed record DayCourtResponse(Guid CourtId, IReadOnlyList<SlotResponse> Slots);
    private sealed record DayResponse(DateOnly Date, IReadOnlyList<DayCourtResponse> Courts);
    private sealed record AvailabilityResponse(string Currency, IReadOnlyList<DayResponse> Days);
}
