using ClubSpot.Domain.Bookings;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ClubSpot.IntegrationTests.Bookings;

// Counter charge with Mercado Pago: the operator hands the customer a link for the balance.
[Collection("postgres")]
public sealed class CounterCheckoutTests(PostgresFixture postgres)
{
    private static readonly TenantId SeedTenant = TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa"));

    [Fact]
    public async Task A_counter_booking_gets_a_checkout_for_its_full_price()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var booking = await CounterBookingAsync(client, daysAhead: 20, phone: "362 500-0100");

        var response = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponse>(TestJsonOptions.Default);
        Assert.Equal(booking.Price, checkout!.Amount);
        Assert.Contains("/dev/checkout", checkout.Url);
        Assert.True(checkout.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Asking_again_for_the_same_charge_hands_back_the_same_link()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var booking = await CounterBookingAsync(client, daysAhead: 21, phone: "362 500-0101");

        var first = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);
        var second = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);

        // The operator can ask as often as they need — no slot is being held — but two payable links
        // for the same money is money the club did not agree to twice.
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstUrl = (await first.Content.ReadFromJsonAsync<CheckoutResponse>(TestJsonOptions.Default))!.Url;
        var secondUrl = (await second.Content.ReadFromJsonAsync<CheckoutResponse>(TestJsonOptions.Default))!.Url;
        Assert.Equal(firstUrl, secondUrl);
        Assert.Single(await CheckoutsAsync(booking.Id));
    }

    [Fact]
    public async Task A_link_for_a_different_amount_is_a_new_link()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var booking = await CounterBookingAsync(client, daysAhead: 30, phone: "362 500-0110");

        var first = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);
        await PayAsync(client, booking.Id, "counter-reissue-1", 5000);
        var second = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);

        // Part of it was paid in cash: what is owed changed, so the old link would charge too much.
        var firstCheckout = (await first.Content.ReadFromJsonAsync<CheckoutResponse>(TestJsonOptions.Default))!;
        var secondCheckout = (await second.Content.ReadFromJsonAsync<CheckoutResponse>(TestJsonOptions.Default))!;
        Assert.Equal(booking.Price, firstCheckout.Amount);
        Assert.Equal(booking.Price - 5000, secondCheckout.Amount);
        Assert.NotEqual(firstCheckout.Url, secondCheckout.Url);
        Assert.Equal(2, (await CheckoutsAsync(booking.Id)).Count);
    }

    [Fact]
    public async Task The_issued_link_is_kept_with_what_it_charges_and_when_it_dies()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var booking = await CounterBookingAsync(client, daysAhead: 29, phone: "362 500-0109");

        var response = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);
        var issued = await response.Content.ReadFromJsonAsync<CheckoutResponse>(TestJsonOptions.Default);

        var stored = Assert.Single(await CheckoutsAsync(booking.Id));
        Assert.Equal(issued!.Url, stored.Url);
        Assert.Equal(issued.Amount, stored.Amount.Amount);
        Assert.Equal(issued.ExpiresAt, stored.ExpiresAt);
        Assert.Equal("fake", stored.Provider);
    }

    [Fact]
    public async Task Charging_a_booking_that_owes_nothing_is_rejected()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var booking = await CounterBookingAsync(client, daysAhead: 22, phone: "362 500-0102");
        await PayAsync(client, booking.Id, "counter-paid-1", booking.Price);

        var response = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_checkout_charges_only_what_is_still_owed()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var booking = await CounterBookingAsync(client, daysAhead: 23, phone: "362 500-0103");
        await PayAsync(client, booking.Id, "counter-partial-1", 5000);

        var response = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);

        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponse>(TestJsonOptions.Default);
        Assert.Equal(booking.Price - 5000, checkout!.Amount);
    }

    [Fact]
    public async Task Paying_twice_records_both_payments_and_shows_the_excess()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var booking = await CounterBookingAsync(client, daysAhead: 24, phone: "362 500-0104");

        await PayAsync(client, booking.Id, "counter-twice-1", booking.Price);
        await PayAsync(client, booking.Id, "counter-twice-2", booking.Price);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        Assert.Equal(2, await db.Payments.CountAsync(payment => payment.BookingId == booking.Id));
        var paid = await db.Payments.Where(payment => payment.BookingId == booking.Id)
            .SumAsync(payment => payment.Amount.Amount);
        Assert.Equal(booking.Price * 2, paid);
    }

    [Fact]
    public async Task Charging_a_cancelled_booking_is_rejected()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var booking = await CounterBookingAsync(client, daysAhead: 25, phone: "362 500-0105");
        await client.PostAsJsonAsync($"/api/bookings/{booking.Id}/cancel", new { reason = "El cliente avisó que no viene" });

        var response = await client.PostAsync($"/api/bookings/{booking.Id}/checkout", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_agenda_reports_how_much_each_booking_has_been_paid()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(26);
        var booking = await CounterBookingAsync(client, daysAhead: 26, phone: "362 500-0106");
        await PayAsync(client, booking.Id, "counter-agenda-1", 9000);

        var agenda = await client.GetFromJsonAsync<AgendaResponse>(
            $"/api/agenda?sport=padel&date={date:O}", TestJsonOptions.Default);

        var charged = agenda!.Courts.SelectMany(court => court.Bookings).Single(entry => entry.Id == booking.Id);
        Assert.Equal(9000, charged.PaidAmount);
    }

    private static async Task<BookingCreatedResponse> CounterBookingAsync(HttpClient client, int daysAhead, string phone)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysAhead);
        var availability = await client.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/portal/chaco-for-ever/availability?sport=padel&from={date:O}&to={date:O}", TestJsonOptions.Default);
        var court = availability!.Days.Single().Courts.First(entry => entry.Slots.Count > 0);
        var slot = court.Slots[0];

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            courtId = court.CourtId, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Cliente Mostrador", customerPhone = phone
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingCreatedResponse>(TestJsonOptions.Default))!;
    }

    private static async Task PayAsync(HttpClient client, Guid bookingId, string externalId, decimal amount)
    {
        var response = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId, externalId, approved = true, amount
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private async Task<IReadOnlyList<BookingCheckout>> CheckoutsAsync(Guid bookingId)
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        return await db.BookingCheckouts.AsNoTracking()
            .Where(checkout => checkout.BookingId == bookingId)
            .OrderBy(checkout => checkout.IssuedAt)
            .ToListAsync();
    }

    private async Task ResetAsync()
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

    private sealed record SessionResponse(string AccessToken);
    private sealed record BookingCreatedResponse(Guid Id, decimal Price);
    private sealed record CheckoutResponse(string Url, decimal Amount, DateTimeOffset ExpiresAt);
    private sealed record SlotResponse(int StartMinute, int Duration, decimal Price);
    private sealed record DayCourtResponse(Guid CourtId, IReadOnlyList<SlotResponse> Slots);
    private sealed record DayResponse(DateOnly Date, IReadOnlyList<DayCourtResponse> Courts);
    private sealed record AvailabilityResponse(string Currency, IReadOnlyList<DayResponse> Days);
    private sealed record AgendaBookingResponse(Guid Id, int StartMinute, int DurationMinutes, string CustomerName,
        string? CustomerPhone, decimal Price, decimal PaidAmount, BookingStatus Status);
    private sealed record AgendaCourtResponse(Guid CourtId, string Name, IReadOnlyList<AgendaBookingResponse> Bookings);
    private sealed record AgendaResponse(string Currency, IReadOnlyList<AgendaCourtResponse> Courts);
}
