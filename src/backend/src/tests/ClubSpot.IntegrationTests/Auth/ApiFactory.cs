using ClubSpot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ClubSpot.IntegrationTests.Persistence;

namespace ClubSpot.IntegrationTests.Auth;

public sealed class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ClubSpot", postgres.ConnectionString);
        builder.UseSetting("Jwt:Issuer", "ClubSpot.Tests");
        builder.UseSetting("Jwt:Audience", "ClubSpot.Tests");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-must-have-at-least-32-characters");
        builder.UseSetting("Payments:Provider", "fake");
        builder.UseSetting("Payments:HoldMinutes", "15");
        // Development selects the rolling file sink, whose default path is the API project folder —
        // the very file ADR-0019 designates for diagnosing. A full run would bury the real lines
        // under a host start/stop pair per test class.
        builder.UseSetting("Diagnostics:LogDirectory", Path.Combine(Path.GetTempPath(), "clubspot-tests-logs"));
    }
}
