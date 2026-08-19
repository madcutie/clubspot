using ClubSpot.Domain.Bookings;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ClubSpot.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class PaymentFlowTests(PostgresFixture postgres)
{
    private static readonly TenantId SeedTenant = TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa"));

    [Fact]
    public async Task An_online_booking_holds_the_slot_and_confirms_on_approval()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 12);

        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Pago Online", customerPhone = "362 400-0100", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(TestJsonOptions.Default);
        Assert.Equal(BookingStatus.PendingPayment, created!.Status);
        Assert.Equal(created.Price, created.ChargeAmount);
        Assert.NotNull(created.ExpiresAt);
        Assert.Contains("/dev/checkout", created.CheckoutUrl);

        // The live hold blocks a second sale of the same slot.
        var conflicting = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Otro Cliente", customerPhone = "362 400-0101", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-test-1", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Confirmed, snapshot!.Status);
        Assert.Equal(created.ChargeAmount, snapshot.PaidAmount);
    }

    [Fact]
    public async Task A_replayed_webhook_does_not_duplicate_the_payment()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 13);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0102");

        var body = new { bookingId = created.Id, externalId = "fake-replay-1", approved = true, amount = created.ChargeAmount };
        var first = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", body);
        var replay = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        Assert.Equal(1, await db.Payments.CountAsync(payment => payment.BookingId == created.Id));
    }

    [Fact]
    public async Task A_rejected_payment_keeps_the_hold_pending()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 14);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0103");

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-reject-1", approved = false, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.PendingPayment, snapshot!.Status);
        Assert.Equal(0, snapshot.PaidAmount);
    }

    [Fact]
    public async Task A_deposit_charges_the_club_percentage()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 15);

        var created = await HoldAsync(client, court.Id, date, slot, "onlineDeposit", "362 400-0104");

        // Seeded club: depositPercent = 50.
        Assert.Equal(Math.Round(created.Price / 2, 2), created.ChargeAmount);
    }

    [Fact]
    public async Task An_expired_hold_stops_blocking_the_slot()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 16);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0105");

        // Time travel: force the hold past its TTL straight in the database.
        var tenantContext = new AsyncLocalTenantContext();
        await using (var db = postgres.CreateDbContext(tenantContext))
        {
            using var scope = tenantContext.BeginScope(SeedTenant);
            await db.Bookings.Where(booking => booking.Id == created.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(booking => booking.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var retry = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Segundo Cliente", customerPhone = "362 400-0106", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        await using var verify = postgres.CreateDbContext(tenantContext);
        using var verifyScope = tenantContext.BeginScope(SeedTenant);
        var stale = await verify.Bookings.SingleAsync(booking => booking.Id == created.Id);
        Assert.Equal(BookingStatus.Expired, stale.Status);
    }

    [Fact]
    public async Task Releasing_a_hold_frees_the_slot_immediately()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 17);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0107");

        var release = await SendWithTokenAsync(client, HttpMethod.Post,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}/release", created.Token);
        Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);

        var retry = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Segundo Cliente", customerPhone = "362 400-0108", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Cancelled, snapshot!.Status);
    }

    [Fact]
    public async Task Releasing_never_touches_a_confirmed_booking()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 18);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0109");
        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-release-1", approved = true, amount = created.ChargeAmount
        });

        var release = await SendWithTokenAsync(client, HttpMethod.Post,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}/release", created.Token);

        Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);
        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Confirmed, snapshot!.Status);
    }

    [Fact]
    public async Task A_payment_landing_on_a_released_hold_is_orphaned()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 19);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0110");
        await SendWithTokenAsync(client, HttpMethod.Post,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}/release", created.Token);

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-orphan-1", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payment = await db.Payments.SingleAsync(candidate => candidate.BookingId == created.Id);
        Assert.Equal(PaymentStatus.ApprovedOrphan, payment.Status);
        var booking = await db.Bookings.SingleAsync(candidate => candidate.Id == created.Id);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public async Task A_second_payment_on_a_fully_paid_booking_is_orphaned()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 20);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0111");
        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-double-1", approved = true, amount = created.ChargeAmount
        });

        // The operator reissued the link and the customer paid it twice.
        var second = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-double-2", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payments = await db.Payments.Where(payment => payment.BookingId == created.Id)
            .OrderBy(payment => payment.CreatedAt).ToListAsync();
        Assert.Equal(2, payments.Count);
        Assert.Equal(PaymentStatus.Approved, payments[0].Status);
        Assert.Equal(PaymentStatus.ApprovedOrphan, payments[1].Status);
    }

    [Fact]
    public async Task The_balance_of_a_deposit_booking_is_an_ordinary_payment()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 21);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineDeposit", "362 400-0112");
        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-deposit-1", approved = true, amount = created.ChargeAmount
        });

        // Confirmed by the deposit, the booking still owes the balance: that is not duplicate money.
        var balance = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-deposit-2", approved = true,
            amount = created.Price - created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payments = await db.Payments.Where(payment => payment.BookingId == created.Id).ToListAsync();
        Assert.Equal(2, payments.Count);
        Assert.All(payments, payment => Assert.Equal(PaymentStatus.Approved, payment.Status));
        Assert.Equal(created.Price, payments.Sum(payment => payment.Amount.Amount));
    }

    [Fact]
    public async Task A_payment_short_of_the_asking_price_never_confirms_the_slot()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 24);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0115");

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-short-1", approved = true,
            amount = created.ChargeAmount - 1m
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.PendingPayment, snapshot!.Status);

        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        var payment = await db.Payments.SingleAsync(candidate => candidate.BookingId == created.Id);
        Assert.Equal(PaymentStatus.ApprovedOrphan, payment.Status);
    }

    [Fact]
    public async Task The_exact_deposit_the_site_asked_for_confirms_the_slot()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 25);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineDeposit", "362 400-0116");

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-exact-1", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await SnapshotAsync(client, created);
        Assert.Equal(BookingStatus.Confirmed, snapshot!.Status);
        Assert.Equal(created.ChargeAmount, snapshot.PaidAmount);
    }

    [Fact]
    public async Task The_portal_refuses_to_confirm_a_slot_against_a_promise_to_pay_at_the_club()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 22);

        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Vivo Vivaldi", customerPhone = "362 400-0113", customerEmail = (string?)null,
            paymentMode = "club", returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        Assert.False(await db.Bookings.AnyAsync());
    }

    [Fact]
    public async Task A_portal_booking_without_a_payment_mode_defaults_to_paying_online()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 23);

        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Sin Modo", customerPhone = "362 400-0114", customerEmail = (string?)null,
            returnUrl = "http://localhost:5183/?retorno=x"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>(TestJsonOptions.Default);
        Assert.Equal(BookingStatus.PendingPayment, created!.Status);
        Assert.Equal(created.Price, created.ChargeAmount);
    }

    [Fact]
    public async Task Flooding_the_portal_with_holds_runs_out_of_permits()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 26);
        var request = new
        {
            courtId = court.Id, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Bot Flood", customerPhone = "362 400-0117", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        };

        // A permit is spent per request, whatever the request turns out to be worth.
        var codes = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 12; attempt++)
            codes.Add((await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", request)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, codes[0]);
        Assert.Equal(10, codes.Count(code => code != HttpStatusCode.TooManyRequests));
        Assert.Equal(2, codes.Count(code => code == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Settling_an_unknown_booking_never_reaches_the_provider()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var settle = await client.PostAsync(
            $"/api/portal/chaco-for-ever/bookings/{Guid.NewGuid()}/settle", null);

        Assert.Equal(HttpStatusCode.NotFound, settle.StatusCode);
    }

    [Fact]
    public async Task A_visitor_holding_only_the_id_can_neither_read_nor_release_the_booking()
    {
        await ResetAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var (court, date, slot) = await FirstSlotAsync(client, daysAhead: 27);
        var created = await HoldAsync(client, court.Id, date, slot, "onlineFull", "362 400-0118");
        var path = $"/api/portal/chaco-for-ever/bookings/{created.Id}";

        var readNoToken = await SendWithTokenAsync(client, HttpMethod.Get, path, null);
        var readWrongToken = await SendWithTokenAsync(client, HttpMethod.Get, path, new string('a', 64));
        var release = await SendWithTokenAsync(client, HttpMethod.Post, path + "/release", null);

        Assert.Equal(HttpStatusCode.NotFound, readNoToken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, readWrongToken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, release.StatusCode);
        // The hold the attacker tried to free is untouched, so the slot stays the buyer's.
        Assert.Equal(BookingStatus.PendingPayment, (await SnapshotAsync(client, created)).Status);
    }

    private static Task<HttpResponseMessage> SendWithTokenAsync(HttpClient client, HttpMethod method,
        string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Add("X-Booking-Token", token);
        return client.SendAsync(request);
    }

    private static async Task<SnapshotResponse> SnapshotAsync(HttpClient client, CreatedResponse created)
    {
        var response = await SendWithTokenAsync(client, HttpMethod.Get,
            $"/api/portal/chaco-for-ever/bookings/{created.Id}", created.Token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SnapshotResponse>(TestJsonOptions.Default))!;
    }

    private static async Task<CreatedResponse> HoldAsync(HttpClient client, Guid courtId, DateOnly date,
        SlotResponse slot, string mode, string phone)
    {
        var response = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Cliente Online", customerPhone = phone, customerEmail = (string?)null,
            paymentMode = mode, returnUrl = "http://localhost:5183/?retorno=x"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>(TestJsonOptions.Default))!;
    }

    private async Task<(CourtResponse Court, DateOnly Date, SlotResponse Slot)> FirstSlotAsync(HttpClient client, int daysAhead)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysAhead);
        var availability = await client.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/portal/chaco-for-ever/availability?sport=padel&from={date:O}&to={date:O}", TestJsonOptions.Default);
        var day = Assert.Single(availability!.Days);
        var dayCourt = day.Courts.First(court => court.Slots.Count > 0);
        var catalog = await client.GetFromJsonAsync<CatalogResponse>("/api/portal/chaco-for-ever/catalog", TestJsonOptions.Default);
        var court = catalog!.Sports.SelectMany(group => group.Courts).Single(candidate => candidate.Id == dayCourt.CourtId);
        return (court, date, dayCourt.Slots[0]);
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

    private sealed record CreatedResponse(Guid Id, decimal Price, decimal ChargeAmount, BookingStatus Status,
        DateTimeOffset? ExpiresAt, string? CheckoutUrl, string Token);
    private sealed record SnapshotResponse(Guid Id, Guid CourtId, string CourtName, Sport Sport, DateOnly Date,
        int StartMinute, int DurationMinutes, decimal Price, decimal PaidAmount, BookingStatus Status,
        PaymentMode PaymentMode, DateTimeOffset? ExpiresAt);
    private sealed record CourtResponse(Guid Id, string Name, string Detail, bool IsCovered, int[] Durations);
    private sealed record SportResponse(Sport Sport, IReadOnlyList<CourtResponse> Courts);
    private sealed record ClubResponse(string Name, string? Venue, string Currency, int DepositPercent);
    private sealed record CatalogResponse(ClubResponse Club, IReadOnlyList<SportResponse> Sports, bool OnlinePayments);
    private sealed record SlotResponse(int StartMinute, int Duration, decimal Price);
    private sealed record DayCourtResponse(Guid CourtId, IReadOnlyList<SlotResponse> Slots);
    private sealed record DayResponse(DateOnly Date, IReadOnlyList<DayCourtResponse> Courts);
    private sealed record AvailabilityResponse(string Currency, IReadOnlyList<DayResponse> Days);
}
