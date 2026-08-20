using System.Net;
using ClubSpot.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Hosting;

namespace ClubSpot.IntegrationTests.Auth;

[Collection("postgres")]
public sealed class DeploymentSurfaceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Liveness_answers_without_a_token()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_answers_when_the_database_is_reachable()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task The_root_no_longer_greets_anyone()
    {
        using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cors_allows_the_origins_configuration_names_and_no_others()
    {
        using var factory = new ApiFactory(postgres).WithWebHostBuilder(builder =>
            builder.UseSetting("Cors:AllowedOrigins:0", "https://admin.clubspot.test"));
        using var client = factory.CreateClient();

        Assert.Contains("https://admin.clubspot.test",
            (await PreflightAsync(client, "https://admin.clubspot.test")).Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.False((await PreflightAsync(client, "http://localhost:5184"))
            .Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Production_refuses_to_start_without_its_own_cors_origins()
    {
        using var factory = new ApiFactory(postgres).WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));

        // Inheriting the dev ports would surface as an empty screen at the counter, not as an error.
        var failure = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Cors:AllowedOrigins", failure.Message);
    }

    private static Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/context");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return client.SendAsync(request);
    }
}
