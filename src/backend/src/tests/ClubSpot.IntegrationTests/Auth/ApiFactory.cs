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
    }
}
