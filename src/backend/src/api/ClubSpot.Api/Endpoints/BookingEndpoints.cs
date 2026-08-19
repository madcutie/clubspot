using System.Security.Claims;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Modularity;
using ClubSpot.Api.Payments;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.Payments;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.Extensions.Options;

namespace ClubSpot.Api.Endpoints;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization(AuthorizationPolicies.AgendaOperate)
            .RequireModule(ModuleId.Bookings);
        group.MapGet("/agenda", GetAgendaAsync);
        group.MapPost("/bookings", CreateAsync);
        group.MapPost("/bookings/{id:guid}/cancel", CancelAsync);
        group.MapPost("/bookings/{id:guid}/checkout", CreateCheckoutAsync);
        return app;
    }

    // Counter charge: hands the operator a link (shown as a QR) for the outstanding balance.
    // Reissuing is free — the slot is already the customer's, so nothing is being held.
    private static async Task<IResult> CreateCheckoutAsync(Guid id, CreateBookingCheckoutHandler handler,
        IOptions<PaymentsOptions> paymentsOptions, CancellationToken cancellationToken)
    {
        var returnUrl = CheckoutReturnUrl.For(paymentsOptions.Value, paymentsOptions.Value.PortalBaseUrl, id);
        var result = await handler.HandleAsync(id, returnUrl, cancellationToken);
        return result.Outcome switch
        {
            BookingCheckoutOutcome.Created => Results.Ok(
                new BookingCheckoutResponse(result.Url!, result.Amount, result.ExpiresAt!.Value)),
            BookingCheckoutOutcome.NotFound => Results.NotFound(),
            BookingCheckoutOutcome.NotChargeable => Results.Conflict(),
            BookingCheckoutOutcome.NoProvider => Results.UnprocessableEntity(),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
        };
    }

    private static async Task<IResult> GetAgendaAsync(string sport, DateOnly date, GetAgendaHandler handler, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Sport>(sport, ignoreCase: true, out var parsedSport)) return Results.BadRequest();
        return Results.Ok(await handler.HandleAsync(parsedSport, date, cancellationToken));
    }

    private static async Task<IResult> CreateAsync(BookingRequest request, HttpContext context, IBookingsStore store, CancellationToken cancellationToken)
    {
        var createdBy = UserId(context.User);
        if (createdBy is null) return Results.Unauthorized();

        var result = await store.CreateAsync(request.ToInput(createdBy.Value), cancellationToken);
        return result.Outcome switch
        {
            BookingCreateOutcome.Created => Results.Created($"/api/bookings/{result.Id}", new BookingCreatedResponse(result.Id, result.Price.Amount)),
            BookingCreateOutcome.UnknownCourt => Results.NotFound(),
            BookingCreateOutcome.InvalidSlot => Results.UnprocessableEntity(),
            BookingCreateOutcome.SlotTaken => Results.Conflict(),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
        };
    }

    private static async Task<IResult> CancelAsync(Guid id, IBookingsStore store, CancellationToken cancellationToken) =>
        await store.CancelAsync(id, cancellationToken) == BookingCancelOutcome.Cancelled ? Results.NoContent() : Results.NotFound();

    private static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id) ? id : null;

    private sealed record BookingRequest(Guid CourtId, DateOnly Date, int StartMinute, int DurationMinutes, string CustomerName, string? CustomerPhone)
    {
        public BookingCreateInput ToInput(Guid createdBy) =>
            new(CourtId, Date, StartMinute, DurationMinutes, CustomerName, CustomerPhone, CustomerEmail: null,
                BookingOrigin.Counter, PaymentMode.Club, createdBy);
    }

    private sealed record BookingCreatedResponse(Guid Id, decimal Price);

    private sealed record BookingCheckoutResponse(string Url, decimal Amount, DateTimeOffset ExpiresAt);
}
