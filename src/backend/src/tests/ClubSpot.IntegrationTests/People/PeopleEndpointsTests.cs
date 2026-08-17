using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClubSpot.IntegrationTests.Auth;
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
    private sealed record PeoplePageResponse(IReadOnlyList<PersonResponse> Items);
}
