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

    [Fact]
    public async Task A_portal_booking_creates_and_links_the_person()
    {
        await ResetBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 7);

        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Carla Ruiz", customerPhone = "362 411-2233", customerEmail = "Carla@Test.com",
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<BookingCreatedResponse>(TestJsonOptions.Default);
        Assert.Equal(slot.Price, created!.Price);
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var booking = await db.Bookings.SingleAsync(candidate => candidate.Id == created.Id);
        Assert.Equal(BookingOrigin.Portal, booking.Origin);
        Assert.Null(booking.CreatedBy);
        Assert.NotNull(booking.PersonId);
        var person = await db.People.SingleAsync(candidate => candidate.Id == booking.PersonId);
        Assert.Equal("carla@test.com", person.Email);
        Assert.Equal("3624112233", person.PhoneDigits);
    }

    [Fact]
    public async Task A_returning_email_reuses_the_person_even_with_a_new_phone()
    {
        await ResetBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 8);
        var second = await FirstSlotAsync(client, daysAhead: 9);

        var first = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Diego Paz", customerPhone = "362 400-0001", customerEmail = "diego@test.com",
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });
        var repeat = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = second.Court.Id, date = second.Date, startMinute = second.Slot.StartMinute,
            durationMinutes = second.Slot.Duration,
            customerName = "Diego Paz", customerPhone = "362 999-9999", customerEmail = "diego@test.com",
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, repeat.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<BookingCreatedResponse>(TestJsonOptions.Default))!.Id;
        var repeatId = (await repeat.Content.ReadFromJsonAsync<BookingCreatedResponse>(TestJsonOptions.Default))!.Id;
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var persons = await db.Bookings
            .Where(booking => booking.Id == firstId || booking.Id == repeatId)
            .Select(booking => booking.PersonId)
            .ToListAsync();
        Assert.Equal(2, persons.Count);
        Assert.Single(persons.Distinct());
    }

    [Fact]
    public async Task The_same_slot_cannot_be_sold_twice_from_the_portal()
    {
        await ResetBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 10);
        var request = new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Eva Sosa", customerPhone = "362 400-0002", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        };

        var first = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", request);
        var second = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_portal_booking_without_a_phone_is_a_bad_request()
    {
        await ResetBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 11);

        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Sin Telefono", customerPhone = "  ", customerEmail = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(CourtResponse Court, DateOnly Date, SlotResponse Slot)> FirstSlotAsync(HttpClient client, int daysAhead)
    {
        var date = Today().AddDays(daysAhead);
        var availability = await GetAvailabilityAsync(client, date);
        var day = Assert.Single(availability.Days);
        var dayCourt = day.Courts.First(court => court.Slots.Count > 0);
        var catalog = await client.GetFromJsonAsync<CatalogResponse>("/api/portal/chaco-for-ever/catalog", TestJsonOptions.Default);
        var court = catalog!.Sports.SelectMany(group => group.Courts).Single(candidate => candidate.Id == dayCourt.CourtId);
        return (court, date, dayCourt.Slots[0]);
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
        db.Payments.RemoveRange(db.Payments);
        db.BookingCheckouts.RemoveRange(db.BookingCheckouts);
        db.Bookings.RemoveRange(db.Bookings);
        db.AvailabilityOverrides.RemoveRange(db.AvailabilityOverrides);
        db.Courts.RemoveRange(db.Courts);
        db.Schedules.RemoveRange(db.Schedules);
        await db.SaveChangesAsync();
    }

    private sealed record BookingCreatedResponse(Guid Id, decimal Price);

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
