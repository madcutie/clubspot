using ClubSpot.Domain.Bookings;
using ClubSpot.IntegrationTests.Auth;
using ClubSpot.IntegrationTests.Persistence;
using ClubSpot.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ClubSpot.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class SchedulePersistenceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_schedule_round_trips_its_json_ranges()
    {
        var tenantContext = new AsyncLocalTenantContext();
        var tenant = TenantId.From(Guid.NewGuid());
        var schedule = new Schedule(
            Guid.NewGuid(),
            tenant,
            "Weekdays",
            "America/Argentina/Buenos_Aires",
            new Dictionary<DayOfWeek, List<TimeRange>> { [DayOfWeek.Monday] = [new(480, 1320)] },
            [new SpecialDate(new DateOnly(2026, 12, 25), [])]);

        await using (var db = postgres.CreateBookingsDbContext(tenantContext))
        {
            using var scope = tenantContext.BeginScope(tenant);
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        await using (var db = postgres.CreateBookingsDbContext(tenantContext))
        {
            using var scope = tenantContext.BeginScope(tenant);
            var read = await db.Schedules.SingleAsync();
            Assert.Single(read.WeeklyRanges[DayOfWeek.Monday]);
            Assert.Single(read.SpecialDates);
        }
    }

    [Fact]
    public async Task An_administrator_can_replace_schedules()
    {
        await ClearBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var sessionResponse = await SignInAsync(client);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session!.AccessToken);

        var response = await client.PutAsJsonAsync("/api/schedules/", new[]
        {
            new
            {
                id = Guid.NewGuid(),
                name = "Weekdays",
                timeZone = "America/Argentina/Buenos_Aires",
                weeklyRanges = new Dictionary<DayOfWeek, TimeRange[]> { [DayOfWeek.Monday] = [new(480, 1320)] },
                specialDates = Array.Empty<SpecialDate>()
            }
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_can_replace_courts()
    {
        await ClearBookingsAsync();
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        var sessionResponse = await SignInAsync(client);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session!.AccessToken);
        var scheduleId = Guid.NewGuid();
        await client.PutAsJsonAsync("/api/schedules/", new[]
        {
            new { id = scheduleId, name = "Weekdays", timeZone = "America/Argentina/Buenos_Aires", weeklyRanges = new Dictionary<DayOfWeek, TimeRange[]>(), specialDates = Array.Empty<SpecialDate>() }
        });

        var response = await client.PutAsJsonAsync("/api/courts/", new[]
        {
            new
            {
                id = Guid.NewGuid(), sport = Sport.Padel, sortOrder = 1, name = "Padel 1", detail = "Covered", isCovered = true, isActive = true,
                scheduleId, durations = new[] { 60, 90 }, startIncrementMinutes = 30, minimumNoticeMinutes = 0,
                dayPrice = 10000m, nightPrice = 12000m, nightStartsAtMinute = 1080
            }
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var courts = await client.GetFromJsonAsync<CourtResponse[]>("/api/courts/");
        Assert.Single(courts!);
        Assert.Equal("Padel 1", courts![0].Name);
    }

    private sealed record SessionResponse(string AccessToken);
    private sealed record CourtResponse(Guid Id, string Name);

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/session", new
        {
            club = "chaco-for-ever",
            email = "admin@chacoforever.test",
            password = "clubspot-dev"
        });

    private async Task ClearBookingsAsync()
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateBookingsDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa")));
        db.Courts.RemoveRange(db.Courts);
        db.Schedules.RemoveRange(db.Schedules);
        await db.SaveChangesAsync();
    }
}
