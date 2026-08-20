using System.Security.Claims;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Api.Payments;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Payments;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace ClubSpot.Api.Endpoints;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization(AuthorizationPolicies.AgendaOperate)
            .RequireModule(ModuleId.Bookings)
            .WithTags("bookings");
        group.MapGet("/agenda", GetAgendaAsync).WithName("GetAgenda");
        group.MapPost("/bookings", CreateAsync).WithName("CreateBooking");
        group.MapPost("/bookings/{id:guid}/cancel", CancelAsync).WithName("CancelBooking");
        group.MapPost("/bookings/{id:guid}/checkout", CreateCheckoutAsync).WithName("CreateBookingCheckout");
        group.MapGet("/people/{id:guid}/bookings", GetPersonBookingsAsync).WithName("GetPersonBookings");
        return app;
    }

    private static async Task<Ok<IReadOnlyList<PersonBookingResponse>>> GetPersonBookingsAsync(Guid id, IPersonBookings personBookings,
        CancellationToken cancellationToken)
    {
        var bookings = await personBookings.HistoryAsync(id, take: 20, cancellationToken);
        return TypedResults.Ok<IReadOnlyList<PersonBookingResponse>>([.. bookings.Select(booking => new PersonBookingResponse(booking.Id, booking.Date,
            booking.StartMinute, booking.DurationMinutes, booking.CourtName, booking.Sport,
            booking.Price.Amount, booking.Paid, booking.Status))]);
    }

    // Counter charge: hands the operator a link (shown as a QR) for the outstanding balance.
    // Reissuing is free — the slot is already the customer's, so nothing is being held.
    private static async Task<Results<Ok<BookingCheckoutResponse>, NotFound, Conflict, UnprocessableEntity>> CreateCheckoutAsync(Guid id, CreateBookingCheckoutHandler handler,
        IOptions<PaymentsOptions> paymentsOptions, CancellationToken cancellationToken)
    {
        var returnUrl = CheckoutReturnUrl.For(paymentsOptions.Value, paymentsOptions.Value.PortalBaseUrl, id);
        var result = await handler.HandleAsync(id, returnUrl, cancellationToken);
        return result.Outcome switch
        {
            BookingCheckoutOutcome.Created => TypedResults.Ok(
                new BookingCheckoutResponse(result.Url!, result.Amount, result.ExpiresAt!.Value)),
            BookingCheckoutOutcome.NotFound => TypedResults.NotFound(),
            BookingCheckoutOutcome.NotChargeable => TypedResults.Conflict(),
            BookingCheckoutOutcome.NoProvider => TypedResults.UnprocessableEntity(),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
        };
    }

    private static async Task<Results<Ok<Agenda>, BadRequest>> GetAgendaAsync(string sport, DateOnly date, GetAgendaHandler handler, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Sport>(sport, ignoreCase: true, out var parsedSport)) return TypedResults.BadRequest();
        return TypedResults.Ok(await handler.HandleAsync(parsedSport, date, cancellationToken));
    }

    private static async Task<Results<Created<BookingCreatedResponse>, NotFound, UnprocessableEntity, Conflict, UnauthorizedHttpResult>> CreateAsync(
        BookingRequest request, HttpContext context, IBookingsStore store, CancellationToken cancellationToken)
    {
        var createdBy = UserId(context.User);
        if (createdBy is null) return TypedResults.Unauthorized();

        var result = await store.CreateAsync(request.ToInput(createdBy.Value), cancellationToken);
        return result.Outcome switch
        {
            BookingCreateOutcome.Created => TypedResults.Created($"/api/bookings/{result.Id}", new BookingCreatedResponse(result.Id, result.Price.Amount)),
            BookingCreateOutcome.UnknownCourt => TypedResults.NotFound(),
            BookingCreateOutcome.InvalidSlot => TypedResults.UnprocessableEntity(),
            BookingCreateOutcome.SlotTaken => TypedResults.Conflict(),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
        };
    }

    private static async Task<Results<NoContent, NotFound, UnprocessableEntity>> CancelAsync(Guid id,
        CancelBookingRequest request, IBookingsStore store, CancellationToken cancellationToken) =>
        await store.CancelAsync(id, request.Reason, cancellationToken) switch
        {
            BookingCancelOutcome.Cancelled => TypedResults.NoContent(),
            BookingCancelOutcome.MissingReason => TypedResults.UnprocessableEntity(),
            BookingCancelOutcome.NotFound => TypedResults.NotFound(),
            _ => throw new ArgumentOutOfRangeException(nameof(store))
        };

    private static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id) ? id : null;

    internal sealed record BookingRequest(Guid CourtId, DateOnly Date, int StartMinute, int DurationMinutes, string CustomerName, string? CustomerPhone)
    {
        public BookingCreateInput ToInput(Guid createdBy) =>
            new(CourtId, Date, StartMinute, DurationMinutes, CustomerName, CustomerPhone, CustomerEmail: null,
                BookingOrigin.Counter, PaymentMode.Club, createdBy);
    }

    internal sealed record PersonBookingResponse(Guid Id, DateOnly Date, int StartMinute, int DurationMinutes,
        string CourtName, Sport Sport, decimal Price, decimal Paid, BookingStatus Status);
    internal sealed record BookingCreatedResponse(Guid Id, decimal Price);
    internal sealed record CancelBookingRequest(string Reason);

    internal sealed record BookingCheckoutResponse(string Url, decimal Amount, DateTimeOffset ExpiresAt);
}
