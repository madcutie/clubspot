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
            customerName = "Otro Cliente", customerPhone = "362 400-0101", customerEmail = (string?)null
        });
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created.Id, externalId = "fake-test-1", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var snapshot = await client.GetFromJsonAsync<SnapshotResponse>(
            $"/api/portal/chaco-for-ever/bookings/{created.Id}", TestJsonOptions.Default);
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

        var snapshot = await client.GetFromJsonAsync<SnapshotResponse>(
            $"/api/portal/chaco-for-ever/bookings/{created.Id}", TestJsonOptions.Default);
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
            customerName = "Segundo Cliente", customerPhone = "362 400-0106", customerEmail = (string?)null
        });

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        await using var verify = postgres.CreateDbContext(tenantContext);
        using var verifyScope = tenantContext.BeginScope(SeedTenant);
        var stale = await verify.Bookings.SingleAsync(booking => booking.Id == created.Id);
        Assert.Equal(BookingStatus.Expired, stale.Status);
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
        DateTimeOffset? ExpiresAt, string? CheckoutUrl);
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
