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
            new Dictionary<DayOfWeek, List<TimeRange>> { [DayOfWeek.Monday] = [new(480, 1320)] });

        await using (var db = postgres.CreateDbContext(tenantContext))
        {
            using var scope = tenantContext.BeginScope(tenant);
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
        }

        await using (var db = postgres.CreateDbContext(tenantContext))
        {
            using var scope = tenantContext.BeginScope(tenant);
            var read = await db.Schedules.SingleAsync();
            Assert.Single(read.WeeklyRanges[DayOfWeek.Monday]);
        }
    }

    [Fact]
    public async Task An_administrator_can_replace_schedules()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await ClearBookingsAsync();
        var sessionResponse = await SignInAsync(client);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session!.AccessToken);

        var response = await client.PutAsJsonAsync("/api/schedules/", new[]
        {
            new
            {
                id = Guid.NewGuid(),
                name = "Weekdays",
                weeklyRanges = new Dictionary<DayOfWeek, TimeRange[]> { [DayOfWeek.Monday] = [new(480, 1320)] }
            }
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_can_replace_courts()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await ClearBookingsAsync();
        var sessionResponse = await SignInAsync(client);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session!.AccessToken);
        var scheduleId = Guid.NewGuid();
        await client.PutAsJsonAsync("/api/schedules/", new[]
        {
            new { id = scheduleId, name = "Weekdays", weeklyRanges = new Dictionary<DayOfWeek, TimeRange[]>() }
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

    [Fact]
    public async Task A_stale_schedule_version_returns_conflict()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await ClearBookingsAsync();
        await AuthorizeAsync(client);
        var scheduleId = Guid.NewGuid();
        var schedule = new
        {
            id = scheduleId,
            name = "Weekdays",
            weeklyRanges = new Dictionary<DayOfWeek, TimeRange[]>()
        };
        await client.PutAsJsonAsync("/api/schedules/", new[] { schedule });
        var original = Assert.Single((await client.GetFromJsonAsync<ScheduleResponse[]>("/api/schedules/"))!);

        var firstSave = await client.PutAsJsonAsync("/api/schedules/", new[] { new { schedule.id, original.Version, name = "Updated", schedule.weeklyRanges } });
        var staleSave = await client.PutAsJsonAsync("/api/schedules/", new[] { new { schedule.id, original.Version, name = "Stale", schedule.weeklyRanges } });

        Assert.Equal(HttpStatusCode.NoContent, firstSave.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleSave.StatusCode);
    }

    [Fact]
    public async Task A_stale_court_version_returns_conflict()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();
        await ClearBookingsAsync();
        await AuthorizeAsync(client);
        var scheduleId = Guid.NewGuid();
        await client.PutAsJsonAsync("/api/schedules/", new[]
        {
            new { id = scheduleId, name = "Weekdays", weeklyRanges = new Dictionary<DayOfWeek, TimeRange[]>() }
        });
        var court = new
        {
            id = Guid.NewGuid(), sport = Sport.Padel, sortOrder = 1, name = "Padel 1", detail = "Covered", isCovered = true, isActive = true,
            scheduleId, durations = new[] { 60, 90 }, startIncrementMinutes = 30, minimumNoticeMinutes = 0,
            dayPrice = 10000m, nightPrice = 12000m, nightStartsAtMinute = 1080
        };
        await client.PutAsJsonAsync("/api/courts/", new[] { court });
        var original = Assert.Single((await client.GetFromJsonAsync<CourtResponse[]>("/api/courts/"))!);

        var firstSave = await client.PutAsJsonAsync("/api/courts/", new[] { new { court.id, original.Version, court.sport, court.sortOrder, name = "Padel A", court.detail, court.isCovered, court.isActive, court.scheduleId, court.durations, court.startIncrementMinutes, court.minimumNoticeMinutes, dayPrice = 15000m, court.nightPrice, court.nightStartsAtMinute } });
        var afterFirstSave = Assert.Single((await client.GetFromJsonAsync<CourtResponse[]>("/api/courts/"))!);
        var staleSave = await client.PutAsJsonAsync("/api/courts/", new[] { new { court.id, original.Version, court.sport, court.sortOrder, name = "Padel B", court.detail, court.isCovered, court.isActive, court.scheduleId, court.durations, court.startIncrementMinutes, court.minimumNoticeMinutes, court.dayPrice, court.nightPrice, court.nightStartsAtMinute } });

        Assert.Equal(HttpStatusCode.NoContent, firstSave.StatusCode);
        Assert.Equal(15000m, afterFirstSave.DayPrice);
        Assert.Equal(HttpStatusCode.Conflict, staleSave.StatusCode);
    }

    private sealed record SessionResponse(string AccessToken);
    private sealed record ScheduleResponse(Guid Id, uint Version);
    private sealed record CourtResponse(Guid Id, string Name, decimal DayPrice, uint Version);

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/session", new
        {
            club = "chaco-for-ever",
            email = "admin@chacoforever.test",
            password = "clubspot-dev"
        });

    private static async Task AuthorizeAsync(HttpClient client)
    {
        var sessionResponse = await SignInAsync(client);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session!.AccessToken);
    }

    private async Task ClearBookingsAsync()
    {
        var tenantContext = new AsyncLocalTenantContext();
        await using var db = postgres.CreateDbContext(tenantContext);
        using var scope = tenantContext.BeginScope(TenantId.From(Guid.Parse("a7b00b98-6191-433d-8930-3273904c1faa")));
        // Bookings first: a court cannot go while a booking points at it. Without this the reset
        // only worked while no earlier test in the collection had sold anything.
        db.Payments.RemoveRange(db.Payments);
        db.Bookings.RemoveRange(db.Bookings);
        db.AvailabilityOverrides.RemoveRange(db.AvailabilityOverrides);
        db.Courts.RemoveRange(db.Courts);
        db.Schedules.RemoveRange(db.Schedules);
        await db.SaveChangesAsync();
    }
}
