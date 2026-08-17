using ClubSpot.Domain.Bookings;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using System.Net;
using System.Net.Http.Json;

namespace ClubSpot.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class BookingEndpointsTests(PostgresFixture postgres)
{
    private static readonly TenantId SeedTenant = TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa"));

    [Fact]
    public async Task Creating_a_booking_removes_the_start_from_the_portal_for_that_court_only()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var backoffice = factory.CreateClient();
        await AuthorizeAsync(backoffice);
        using var portal = factory.CreateClient();
        var date = Today().AddDays(5);
        var (courtOne, courtTwo) = await PadelCourtsAsync(portal);

        var create = await backoffice.PostAsJsonAsync("/api/bookings", new
        {
            courtId = courtOne.Id, date, startMinute = 600, durationMinutes = 60,
            customerName = "Ana Suárez", customerPhone = "+54 362 400-0000"
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<BookingCreatedResponse>();
        Assert.Equal(14000m, created!.Price);
        var day = Assert.Single((await GetAvailabilityAsync(portal, date)).Days);
        var courtOneSlots = day.Courts.Single(court => court.CourtId == courtOne.Id).Slots;
        Assert.DoesNotContain(courtOneSlots, slot => slot.StartMinute < 660 && 600 < slot.StartMinute + slot.Duration);
        var courtTwoSlots = day.Courts.Single(court => court.CourtId == courtTwo.Id).Slots;
        Assert.Contains(courtTwoSlots, slot => slot.StartMinute == 600 && slot.Duration == 60);
    }

    [Fact]
    public async Task Selling_the_same_slot_twice_is_a_conflict()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var date = Today().AddDays(6);
        var (courtOne, _) = await PadelCourtsAsync(client);

        var first = await client.PostAsJsonAsync("/api/bookings", new
        {
            courtId = courtOne.Id, date, startMinute = 600, durationMinutes = 60, customerName = "Ana Suárez"
        });
        var second = await client.PostAsJsonAsync("/api/bookings", new
        {
            courtId = courtOne.Id, date, startMinute = 600, durationMinutes = 60, customerName = "Bruno Paz"
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_slot_outside_the_window_or_with_an_unoffered_duration_is_rejected()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var date = Today().AddDays(7);
        var (courtOne, _) = await PadelCourtsAsync(client);

        var outsideWindow = await client.PostAsJsonAsync("/api/bookings", new
        {
            courtId = courtOne.Id, date, startMinute = 300, durationMinutes = 60, customerName = "Ana Suárez"
        });
        var unofferedDuration = await client.PostAsJsonAsync("/api/bookings", new
        {
            courtId = courtOne.Id, date, startMinute = 600, durationMinutes = 45, customerName = "Ana Suárez"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, outsideWindow.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unofferedDuration.StatusCode);
    }

    [Fact]
    public async Task An_unknown_court_is_not_found()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            courtId = Guid.NewGuid(), date = Today().AddDays(7), startMinute = 600, durationMinutes = 60,
            customerName = "Ana Suárez"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_booking_returns_the_start_to_the_portal()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var backoffice = factory.CreateClient();
        await AuthorizeAsync(backoffice);
        using var portal = factory.CreateClient();
        var date = Today().AddDays(8);
        var (courtOne, _) = await PadelCourtsAsync(portal);
        var create = await backoffice.PostAsJsonAsync("/api/bookings", new
        {
            courtId = courtOne.Id, date, startMinute = 600, durationMinutes = 60, customerName = "Ana Suárez"
        });
        var created = await create.Content.ReadFromJsonAsync<BookingCreatedResponse>();
        var takenDay = Assert.Single((await GetAvailabilityAsync(portal, date)).Days);
        Assert.DoesNotContain(takenDay.Courts.Single(court => court.CourtId == courtOne.Id).Slots,
            slot => slot.StartMinute == 600 && slot.Duration == 60);

        var cancel = await backoffice.PostAsync($"/api/bookings/{created!.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var freedDay = Assert.Single((await GetAvailabilityAsync(portal, date)).Days);
        Assert.Contains(freedDay.Courts.Single(court => court.CourtId == courtOne.Id).Slots,
            slot => slot.StartMinute == 600 && slot.Duration == 60);
    }

    [Fact]
    public async Task Cancelling_an_unknown_booking_returns_not_found()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);

        var response = await client.PostAsync($"/api/bookings/{Guid.NewGuid()}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_agenda_lists_the_booking_with_its_customer_name()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var date = Today().AddDays(9);
        var (courtOne, _) = await PadelCourtsAsync(client);
        var create = await client.PostAsJsonAsync("/api/bookings", new
        {
            courtId = courtOne.Id, date, startMinute = 600, durationMinutes = 60,
            customerName = "Ana Suárez", customerPhone = "+54 362 400-0000"
        });
        var created = await create.Content.ReadFromJsonAsync<BookingCreatedResponse>();

        var agenda = await client.GetFromJsonAsync<AgendaResponse>($"/api/agenda?sport=padel&date={date:O}", TestJsonOptions.Default);

        Assert.Equal("ARS", agenda!.Currency);
        var agendaCourt = agenda.Courts.Single(court => court.CourtId == courtOne.Id);
        Assert.NotEmpty(agendaCourt.Windows);
        var booking = Assert.Single(agendaCourt.Bookings);
        Assert.Equal(created!.Id, booking.Id);
        Assert.Equal("Ana Suárez", booking.CustomerName);
        Assert.Equal("+54 362 400-0000", booking.CustomerPhone);
        Assert.Equal(600, booking.StartMinute);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.DoesNotContain(agendaCourt.Slots, slot => slot.StartMinute == 600 && slot.Duration == 60);
    }

    [Fact]
    public async Task Creating_a_booking_without_a_token_is_unauthorized()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            courtId = Guid.NewGuid(), date = Today().AddDays(5), startMinute = 600, durationMinutes = 60,
            customerName = "Ana Suárez"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<(CourtResponse CourtOne, CourtResponse CourtTwo)> PadelCourtsAsync(HttpClient client)
    {
        var catalog = await client.GetFromJsonAsync<CatalogResponse>("/api/portal/chaco-for-ever/catalog", TestJsonOptions.Default);
        var padelCourts = catalog!.Sports.Single(group => group.Sport == Sport.Padel).Courts;
        return (padelCourts.Single(court => court.Name == "Cancha 1"), padelCourts.Single(court => court.Name == "Cancha 2"));
    }

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

    private async Task ResetAsync()
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        db.Payments.RemoveRange(db.Payments);
        db.Bookings.RemoveRange(db.Bookings);
        db.AvailabilityOverrides.RemoveRange(db.AvailabilityOverrides);
        db.Courts.RemoveRange(db.Courts);
        db.Schedules.RemoveRange(db.Schedules);
        await db.SaveChangesAsync();
    }

    private sealed record SessionResponse(string AccessToken);
    private sealed record BookingCreatedResponse(Guid Id, decimal Price);
    private sealed record ClubResponse(string Name, string? Venue, string Currency, int DepositPercent);
    private sealed record CourtResponse(Guid Id, string Name, string Detail, bool IsCovered, int[] Durations);
    private sealed record SportResponse(Sport Sport, IReadOnlyList<CourtResponse> Courts);
    private sealed record CatalogResponse(ClubResponse Club, IReadOnlyList<SportResponse> Sports);
    private sealed record SlotResponse(int StartMinute, int Duration, decimal Price);
    private sealed record DayCourtResponse(Guid CourtId, IReadOnlyList<SlotResponse> Slots);
    private sealed record DayResponse(DateOnly Date, IReadOnlyList<DayCourtResponse> Courts);
    private sealed record AvailabilityResponse(string Currency, IReadOnlyList<DayResponse> Days);
    private sealed record AgendaWindowResponse(int OpensAtMinute, int ClosesAtMinute);
    private sealed record AgendaSlotResponse(int StartMinute, int Duration, decimal Price);
    private sealed record AgendaBookingResponse(Guid Id, int StartMinute, int DurationMinutes, string CustomerName,
        string? CustomerPhone, decimal Price, BookingStatus Status);
    private sealed record AgendaCourtResponse(Guid CourtId, string Name, string Detail, bool IsCovered,
        IReadOnlyList<AgendaWindowResponse> Windows, IReadOnlyList<AgendaSlotResponse> Slots,
        IReadOnlyList<AgendaBookingResponse> Bookings);
    private sealed record AgendaResponse(string Currency, IReadOnlyList<AgendaCourtResponse> Courts);
}
