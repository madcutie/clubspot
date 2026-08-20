using System.Security.Claims;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Application.Core.People;
using ClubSpot.Domain.Core.People;
using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Api.Endpoints;

public static class PeopleEndpoints
{
    public static IEndpointRouteBuilder MapPeople(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/people").RequireModule(ModuleId.Core);
        group.MapGet("/", SearchAsync).RequireAuthorization(AuthorizationPolicies.PeopleView);
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AuthorizationPolicies.PeopleView);
        group.MapPost("/", CreateAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage);
        group.MapPost("/blocks", BlockAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage);
        group.MapPut("/{id:guid}/block", SetBlockAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage);
        group.MapPost("/{id:guid}/notes", AddNoteAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage);
        group.MapPost("/{id:guid}/payments", RegisterPaymentAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage);
        return app;
    }

    private static async Task<IResult> SearchAsync(string? q, string? filter, int? page, IPeopleQueries queries, CancellationToken cancellationToken)
    {
        if (!TryParseFilter(filter, out var peopleFilter)) return Results.BadRequest();
        var result = await queries.SearchAsync(new PeopleSearch(q ?? string.Empty, peopleFilter, page ?? 0), cancellationToken);
        return Results.Ok(new PeoplePageResponse(result.Items.Select(PersonResponse.From), result.Total, result.Page, result.Pages,
            result.PageSize, result.Census, result.NeedsAttention, result.TotalDebt.Amount,
            result.Totals.ToDictionary(pair => ToWireFilter(pair.Key), pair => pair.Value)));
    }

    private static async Task<IResult> GetAsync(Guid id, IPeopleQueries queries, CancellationToken cancellationToken)
    {
        var result = await queries.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(new PersonDetailsResponse(PersonResponse.From(result.Person), result.Notes.Select(note => new NoteResponse(note.Text, note.AuthorName, note.CreatedAt))));
    }

    private static async Task<IResult> CreateAsync(CreatePersonRequest request, HttpContext context, CreatePersonHandler handler, CancellationToken cancellationToken)
    {
        var person = await handler.HandleAsync(request.Name, request.Phone, request.Email, UserId(context.User), cancellationToken);
        return Results.Created($"/api/people/{person.Id}", PersonResponse.From(person));
    }

    private static async Task<IResult> BlockAsync(BlockPeopleRequest request, BlockPeopleHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(new AffectedResponse(await handler.HandleAsync(request.Ids, request.Blocked, cancellationToken)));

    private static async Task<IResult> SetBlockAsync(Guid id, SetBlockRequest request, BlockPeopleHandler handler, CancellationToken cancellationToken)
    {
        var affected = await handler.HandleAsync([id], request.Blocked, cancellationToken);
        return affected == 0 ? Results.NotFound() : Results.Ok(new BlockResponse(request.Blocked));
    }

    private static async Task<IResult> AddNoteAsync(Guid id, AddNoteRequest request, HttpContext context, AddNoteHandler handler, CancellationToken cancellationToken)
    {
        var userId = UserId(context.User);
        if (userId is null) return Results.Unauthorized();
        var note = await handler.HandleAsync(id, request.Text, userId.Value, cancellationToken);
        return note is null ? Results.NotFound() : Results.Created($"/api/people/{id}/notes/{note.Id}", new NoteResponse(note.Text, context.User.Identity?.Name ?? string.Empty, note.CreatedAt));
    }

    private static async Task<IResult> RegisterPaymentAsync(Guid id, RegisterPersonPaymentHandler handler, CancellationToken cancellationToken)
    {
        var paid = await handler.HandleAsync(id, cancellationToken);
        return paid is null ? Results.NotFound() : Results.Ok(new PaymentResponse(paid.Value.Amount));
    }

    private static Guid? UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id) ? id : null;

    // Both sides of the check read the same normalized value: comparing the raw one rejected
    // "withoutBookings", which is the very spelling this endpoint publishes in Totals.
    private static bool TryParseFilter(string? value, out PeopleFilter filter)
    {
        var normalized = value?.ToLowerInvariant();
        filter = normalized switch
        {
            null or "" or "all" => PeopleFilter.All,
            "withoutbookings" => PeopleFilter.WithoutBookings,
            "counter" => PeopleFilter.Counter,
            "debt" => PeopleFilter.Debt,
            _ => default
        };
        return normalized is null or "" or "all" or "withoutbookings" or "counter" or "debt";
    }

    private static string ToWireFilter(PeopleFilter filter) => filter switch
    {
        PeopleFilter.All => "all",
        PeopleFilter.WithoutBookings => "withoutBookings",
        PeopleFilter.Counter => "counter",
        PeopleFilter.Debt => "debt",
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };

    private sealed record CreatePersonRequest(string Name, string Phone, string Email);
    private sealed record BlockPeopleRequest(IReadOnlyCollection<Guid> Ids, bool Blocked);
    private sealed record SetBlockRequest(bool Blocked);
    private sealed record AddNoteRequest(string Text);
    private sealed record PersonResponse(Guid Id, string Name, string Phone, string Email, PersonOrigin Origin,
        int Bookings, DateOnly? LastBookingOn, decimal Debt, bool IsBlocked, DateTimeOffset CreatedAt)
    {
        public static PersonResponse From(PersonListItem person) => new(person.Id, person.Name, person.Phone, person.Email,
            person.Origin, person.Bookings, person.LastBookingOn, person.Debt.Amount,
            person.IsBlocked, person.CreatedAt);
        public static PersonResponse From(ClubSpot.Domain.Core.People.Person person) => new(person.Id, person.Name, person.Phone, person.Email,
            person.Origin, 0, null, person.Debt.Amount, person.IsBlocked, person.CreatedAt);
    }
    private sealed record PeoplePageResponse(IEnumerable<PersonResponse> Items, int Total, int Page, int Pages,
        int PageSize, int Census, int NeedsAttention, decimal TotalDebt, IReadOnlyDictionary<string, int> Totals);
    private sealed record PersonDetailsResponse(PersonResponse Person, IEnumerable<NoteResponse> Notes);
    private sealed record NoteResponse(string Text, string AuthorName, DateTimeOffset CreatedAt);
    private sealed record AffectedResponse(int Affected);
    private sealed record BlockResponse(bool Blocked);
    private sealed record PaymentResponse(decimal Paid);
}
