using System.Security.Claims;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Application.Core.People;
using ClubSpot.Domain.Core.People;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ClubSpot.Api.Endpoints;

public static class PeopleEndpoints
{
    public static IEndpointRouteBuilder MapPeople(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/people").RequireModule(ModuleId.Core).WithTags("people");
        group.MapGet("/", SearchAsync).RequireAuthorization(AuthorizationPolicies.PeopleView).WithName("SearchPeople");
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AuthorizationPolicies.PeopleView).WithName("GetPerson");
        group.MapPost("/", CreateAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage).WithName("CreatePerson");
        group.MapPost("/blocks", BlockAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage).WithName("BlockPeople");
        group.MapPut("/{id:guid}/block", SetBlockAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage).WithName("SetPersonBlock");
        group.MapPost("/{id:guid}/notes", AddNoteAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage).WithName("AddPersonNote");
        group.MapPost("/{id:guid}/payments", RegisterPaymentAsync).RequireAuthorization(AuthorizationPolicies.PeopleManage).WithName("RegisterPersonPayment");
        return app;
    }

    private static async Task<Results<Ok<PeoplePageResponse>, BadRequest>> SearchAsync(string? q, string? filter, int? page, IPeopleQueries queries, CancellationToken cancellationToken)
    {
        if (!TryParseFilter(filter, out var peopleFilter)) return TypedResults.BadRequest();
        var result = await queries.SearchAsync(new PeopleSearch(q ?? string.Empty, peopleFilter, page ?? 0), cancellationToken);
        return TypedResults.Ok(new PeoplePageResponse([.. result.Items.Select(PersonResponse.From)], result.Total, result.Page, result.Pages,
            result.PageSize, result.Census, result.NeedsAttention, result.TotalDebt.Amount,
            result.Totals.ToDictionary(pair => ToWireFilter(pair.Key), pair => pair.Value)));
    }

    private static async Task<Results<Ok<PersonDetailsResponse>, NotFound>> GetAsync(Guid id, IPeopleQueries queries, CancellationToken cancellationToken)
    {
        var result = await queries.GetAsync(id, cancellationToken);
        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new PersonDetailsResponse(PersonResponse.From(result.Person),
                [.. result.Notes.Select(note => new NoteResponse(note.Text, note.AuthorName, note.CreatedAt))]));
    }

    private static async Task<Created<PersonResponse>> CreateAsync(CreatePersonRequest request, HttpContext context, CreatePersonHandler handler, CancellationToken cancellationToken)
    {
        var person = await handler.HandleAsync(request.Name, request.Phone, request.Email, UserId(context.User), cancellationToken);
        return TypedResults.Created($"/api/people/{person.Id}", PersonResponse.From(person));
    }

    private static async Task<Ok<AffectedResponse>> BlockAsync(BlockPeopleRequest request, BlockPeopleHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(new AffectedResponse(await handler.HandleAsync(request.Ids, request.Blocked, cancellationToken)));

    private static async Task<Results<Ok<BlockResponse>, NotFound>> SetBlockAsync(Guid id, SetBlockRequest request, BlockPeopleHandler handler, CancellationToken cancellationToken)
    {
        var affected = await handler.HandleAsync([id], request.Blocked, cancellationToken);
        return affected == 0 ? TypedResults.NotFound() : TypedResults.Ok(new BlockResponse(request.Blocked));
    }

    private static async Task<Results<Created<NoteResponse>, NotFound, UnauthorizedHttpResult>> AddNoteAsync(Guid id, AddNoteRequest request, HttpContext context, AddNoteHandler handler, CancellationToken cancellationToken)
    {
        var userId = UserId(context.User);
        if (userId is null) return TypedResults.Unauthorized();
        var note = await handler.HandleAsync(id, request.Text, userId.Value, cancellationToken);
        return note is null
            ? TypedResults.NotFound()
            : TypedResults.Created($"/api/people/{id}/notes/{note.Id}",
                new NoteResponse(note.Text, context.User.Identity?.Name ?? string.Empty, note.CreatedAt));
    }

    private static async Task<Results<Ok<PaymentResponse>, NotFound>> RegisterPaymentAsync(Guid id, RegisterPersonPaymentHandler handler, CancellationToken cancellationToken)
    {
        var paid = await handler.HandleAsync(id, cancellationToken);
        return paid is null ? TypedResults.NotFound() : TypedResults.Ok(new PaymentResponse(paid.Value.Amount));
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

    internal sealed record CreatePersonRequest(string Name, string Phone, string Email);
    internal sealed record BlockPeopleRequest(IReadOnlyCollection<Guid> Ids, bool Blocked);
    internal sealed record SetBlockRequest(bool Blocked);
    internal sealed record AddNoteRequest(string Text);
    internal sealed record PersonResponse(Guid Id, string Name, string Phone, string Email, PersonOrigin Origin,
        int Bookings, DateOnly? LastBookingOn, decimal Debt, bool IsBlocked, DateTimeOffset CreatedAt)
    {
        public static PersonResponse From(PersonListItem person) => new(person.Id, person.Name, person.Phone, person.Email,
            person.Origin, person.Bookings, person.LastBookingOn, person.Debt.Amount,
            person.IsBlocked, person.CreatedAt);
        public static PersonResponse From(ClubSpot.Domain.Core.People.Person person) => new(person.Id, person.Name, person.Phone, person.Email,
            person.Origin, 0, null, person.Debt.Amount, person.IsBlocked, person.CreatedAt);
    }
    internal sealed record PeoplePageResponse(IReadOnlyList<PersonResponse> Items, int Total, int Page, int Pages,
        int PageSize, int Census, int NeedsAttention, decimal TotalDebt, IReadOnlyDictionary<string, int> Totals);
    internal sealed record PersonDetailsResponse(PersonResponse Person, IReadOnlyList<NoteResponse> Notes);
    internal sealed record NoteResponse(string Text, string AuthorName, DateTimeOffset CreatedAt);
    internal sealed record AffectedResponse(int Affected);
    internal sealed record BlockResponse(bool Blocked);
    internal sealed record PaymentResponse(decimal Paid);
}
