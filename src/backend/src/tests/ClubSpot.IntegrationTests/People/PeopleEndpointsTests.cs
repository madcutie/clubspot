using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;

namespace ClubSpot.IntegrationTests.People;

[Collection("postgres")]
public sealed class PeopleEndpointsTests(PostgresFixture postgres)
{
    [Fact]
    public async Task An_administrator_can_create_search_block_note_and_pay_a_person()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(client));

        var create = await client.PostAsJsonAsync("/api/people", new
        {
            name = "Julián Gómez", phone = "362 415-8890", email = "julian@example.test"
        });
        var person = await create.Content.ReadFromJsonAsync<PersonResponse>();
        var search = await client.GetFromJsonAsync<PeoplePageResponse>("/api/people?q=4158890&filter=all&page=0");
        var note = await client.PostAsJsonAsync($"/api/people/{person!.Id}/notes", new { text = "Call before the next booking." });
        var block = await client.PutAsJsonAsync($"/api/people/{person.Id}/block", new { blocked = true });
        var payment = await client.PostAsync($"/api/people/{person.Id}/payments", null);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(search);
        Assert.Contains(search.Items, item => item.Id == person.Id);
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
    }

    [Fact]
    public async Task A_person_who_booked_stops_counting_as_without_bookings()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(29);
        var availability = await client.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/portal/chaco-for-ever/availability?sport=padel&from={date:O}&to={date:O}", TestJsonOptions.Default);
        var dayCourt = availability!.Days.Single().Courts.First(court => court.Slots.Count > 0);
        var slot = dayCourt.Slots[0];
        var email = "conturnos@example.test";
        var hold = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = dayCourt.CourtId, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Con Turnos", customerPhone = "362 400-0900", customerEmail = email,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });
        Assert.Equal(HttpStatusCode.Created, hold.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(client));
        var all = await client.GetFromJsonAsync<PeoplePageResponse>($"/api/people?q={email}&filter=all&page=0");
        var person = Assert.Single(all!.Items);
        Assert.Equal(1, person.Bookings);
        // The hold is for next month, and "last time" means the last time they played.
        Assert.Null(person.LastBookingOn);

        var withoutBookings = await client.GetFromJsonAsync<PeoplePageResponse>(
            $"/api/people?q={email}&filter=withoutBookings&page=0");
        Assert.Empty(withoutBookings!.Items);

        var history = await client.GetFromJsonAsync<IReadOnlyList<PersonBookingResponse>>(
            $"/api/people/{person.Id}/bookings", TestJsonOptions.Default);
        var booking = Assert.Single(history!);
        Assert.Equal(date, booking.Date);
        Assert.Equal(slot.StartMinute, booking.StartMinute);
        Assert.Equal(0m, booking.Paid);
    }

    private static async Task<string> GetTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/session", new
        {
            club = "chaco-for-ever", email = "admin@chacoforever.test", password = "clubspot-dev"
        });
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        return session!.AccessToken;
    }

    private sealed record SessionResponse(string AccessToken);
    private sealed record PersonResponse(Guid Id, int Bookings, DateOnly? LastBookingOn);
    private sealed record PeoplePageResponse(IReadOnlyList<PersonResponse> Items);
    private sealed record PersonBookingResponse(Guid Id, DateOnly Date, int StartMinute, int DurationMinutes,
        string CourtName, decimal Price, decimal Paid);
    private sealed record SlotResponse(int StartMinute, int Duration, decimal Price);
    private sealed record DayCourtResponse(Guid CourtId, IReadOnlyList<SlotResponse> Slots);
    private sealed record DayResponse(DateOnly Date, IReadOnlyList<DayCourtResponse> Courts);
    private sealed record AvailabilityResponse(string Currency, IReadOnlyList<DayResponse> Days);
}
