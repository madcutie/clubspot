using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubSpot.Application.Core.Activity;
using ClubSpot.Domain.Core.Activity;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Json;
using ClubSpot.IntegrationTests.Persistence;
using ClubSpot.SharedKernel.Activity;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubSpot.IntegrationTests.Activity;

[Collection("postgres")]
public sealed class ActivityLogTests(PostgresFixture postgres)
{
    private static readonly TenantId SeedTenant = TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa"));

    [Fact]
    public async Task An_online_booking_and_its_payment_leave_the_whole_story()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(24);
        var availability = await client.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/portal/chaco-for-ever/availability?sport=padel&from={date:O}&to={date:O}", TestJsonOptions.Default);
        var dayCourt = availability!.Days.Single().Courts.First(court => court.Slots.Count > 0);
        var slot = dayCourt.Slots[0];

        var hold = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = dayCourt.CourtId, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Cronica Completa", customerPhone = "362 400-2200",
            customerEmail = "cronica@example.test", paymentMode = "onlineFull",
            returnUrl = "http://localhost:5183/?retorno=x"
        });
        var created = await hold.Content.ReadFromJsonAsync<CreatedResponse>(TestJsonOptions.Default);

        var webhook = await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created!.Id, externalId = $"activity-{created.Id}", approved = true, amount = created.ChargeAmount
        });
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        var entries = await EntriesForBookingAsync(created.Id);

        var holdCreated = Assert.Single(entries, entry => entry.Type == "holdCreated");
        Assert.Equal(ActivitySource.Portal, holdCreated.Source);
        Assert.Null(holdCreated.ActorUserId);
        Assert.NotNull(holdCreated.PersonId);
        Assert.Contains("\"durationMinutes\":", holdCreated.Data);

        var approved = Assert.Single(entries, entry => entry.Type == "paymentApproved");
        // The webhook is not a person: what matters is knowing it arrived, and when.
        Assert.Equal(ActivitySource.Webhook, approved.Source);
        Assert.Null(approved.ActorUserId);
        Assert.NotNull(approved.PaymentId);
        Assert.True(approved.OccurredAt >= holdCreated.OccurredAt);

        // The person created by the portal is part of the same story.
        var personCreated = await EntriesForPersonAsync(holdCreated.PersonId!.Value);
        Assert.Contains(personCreated, entry => entry.Type == "personCreated");
    }

    [Fact]
    public async Task A_rejected_payment_is_recorded_and_the_hold_stays()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(25);
        var availability = await client.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/portal/chaco-for-ever/availability?sport=padel&from={date:O}&to={date:O}", TestJsonOptions.Default);
        var dayCourt = availability!.Days.Single().Courts.First(court => court.Slots.Count > 0);
        var slot = dayCourt.Slots[0];

        var hold = await client.PostAsJsonAsync("/api/portal/chaco-for-ever/bookings", new
        {
            courtId = dayCourt.CourtId, date, startMinute = slot.StartMinute, durationMinutes = slot.Duration,
            customerName = "Pago Rechazado", customerPhone = "362 400-2201", customerEmail = (string?)null,
            paymentMode = "onlineFull", returnUrl = "http://localhost:5183/?retorno=x"
        });
        var created = await hold.Content.ReadFromJsonAsync<CreatedResponse>(TestJsonOptions.Default);

        await client.PostAsJsonAsync("/api/payments/fake/webhook/chaco-for-ever", new
        {
            bookingId = created!.Id, externalId = $"activity-rejected-{created.Id}", approved = false,
            amount = created.ChargeAmount
        });

        var entries = await EntriesForBookingAsync(created.Id);
        Assert.Single(entries, entry => entry.Type == "paymentRejected");
        Assert.DoesNotContain(entries, entry => entry.Type == "paymentApproved");
    }

    [Fact]
    public async Task A_counter_action_records_the_operator_who_did_it()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(client));

        var create = await client.PostAsJsonAsync("/api/people", new
        {
            name = "Alta De Mostrador", phone = "362 400-2202", email = "mostrador-activity@example.test"
        });
        var person = await create.Content.ReadFromJsonAsync<PersonResponse>();
        await client.PostAsJsonAsync($"/api/people/{person!.Id}/notes", new { text = "Nota de la cronica." });

        var entries = await EntriesForPersonAsync(person.Id);

        var alta = Assert.Single(entries, entry => entry.Type == "personCreated");
        Assert.Equal(ActivitySource.Counter, alta.Source);
        Assert.NotNull(alta.ActorUserId);
        Assert.False(string.IsNullOrWhiteSpace(alta.ActorName));
        Assert.Contains("counter", alta.Data, StringComparison.OrdinalIgnoreCase);

        Assert.Single(entries, entry => entry.Type == "personNoteAdded");
    }

    [Fact]
    public void A_type_that_demands_a_reason_refuses_to_be_recorded_without_one()
    {
        using var factory = new ApiFactory(postgres);
        using var scope = factory.Services.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IActivityLog>();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScopeFactory>().BeginScope(SeedTenant);
        using var actorScope = scope.ServiceProvider.GetRequiredService<IActivityActorScopeFactory>()
            .BeginScope(new ActivityActor(Guid.NewGuid(), "Operador", ActivitySource.Counter));

        Assert.Throws<ArgumentException>(() =>
            log.Record(new ActivityRecord("bookingCancelled", BookingId: Guid.NewGuid())));

        log.Record(new ActivityRecord("bookingCancelled", BookingId: Guid.NewGuid(),
            Reason: "El cliente avisó que no viene."));
    }

    [Fact]
    public void Recording_without_an_actor_scope_throws_instead_of_inventing_one()
    {
        using var factory = new ApiFactory(postgres);
        using var scope = factory.Services.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<IActivityLog>();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScopeFactory>().BeginScope(SeedTenant);

        Assert.Throws<MissingActivityActorException>(() => log.Record(new ActivityRecord("personNoteAdded")));
    }

    private async Task<IReadOnlyList<ActivityLogEntry>> EntriesForBookingAsync(Guid bookingId)
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        return await db.ActivityLogEntries.AsNoTracking()
            .Where(entry => entry.BookingId == bookingId)
            .OrderBy(entry => entry.OccurredAt)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<ActivityLogEntry>> EntriesForPersonAsync(Guid personId)
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(SeedTenant);
        return await db.ActivityLogEntries.AsNoTracking()
            .Where(entry => entry.PersonId == personId)
            .OrderBy(entry => entry.OccurredAt)
            .ToListAsync();
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
    private sealed record PersonResponse(Guid Id);
    private sealed record CreatedResponse(Guid Id, decimal Price, decimal ChargeAmount, string Status);
    private sealed record SlotResponse(int StartMinute, int Duration, decimal Price);
    private sealed record DayCourtResponse(Guid CourtId, IReadOnlyList<SlotResponse> Slots);
    private sealed record DayResponse(DateOnly Date, IReadOnlyList<DayCourtResponse> Courts);
    private sealed record AvailabilityResponse(string Currency, IReadOnlyList<DayResponse> Days);
}
