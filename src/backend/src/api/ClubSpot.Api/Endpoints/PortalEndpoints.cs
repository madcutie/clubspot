using ClubSpot.Api.Modularity;
using ClubSpot.Api.Payments;
using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.Api.Endpoints;

public static class PortalEndpoints
{
    public static IEndpointRouteBuilder MapPortal(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal/{clubSlug}")
            .AllowAnonymous();
        // Mandatory order: the slug filter opens the tenant scope that RequireModule needs.
        group.AddEndpointFilter(ClubScope.ResolveAsync);
        group.RequireModule(ModuleId.Bookings);
        group.MapGet("/catalog", GetCatalogAsync);
        group.MapGet("/availability", GetAvailabilityAsync);
        group.MapPost("/bookings", CreateBookingAsync);
        group.MapGet("/bookings/{id:guid}", GetBookingAsync);
        group.MapPost("/bookings/{id:guid}/release", ReleaseBookingAsync);
        group.MapPost("/bookings/{id:guid}/settle", SettleBookingAsync);
        return app;
    }

    private static async Task<IResult> CreateBookingAsync(string clubSlug, PortalBookingRequest request,
        IBookingsStore store, IClubSettings clubSettings, IEnumerable<IHostedCheckout> checkouts,
        Microsoft.Extensions.Options.IOptions<ClubSpot.Infrastructure.Payments.PaymentsOptions> paymentsOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone))
            return Results.BadRequest();

        // How a portal booking may be paid is the server's call, never the caller's: with a checkout
        // wired the slot is only held against an online payment, so a request asking to pay at the
        // club cannot confirm it for free. Paying at the club stays the only mode where no gateway
        // is contracted, which is a supported configuration (AGENTS.md §5).
        var checkout = checkouts.FirstOrDefault();
        PaymentMode[] allowedModes = checkout is null
            ? [PaymentMode.Club]
            : [PaymentMode.OnlineFull, PaymentMode.OnlineDeposit];
        var paymentMode = request.PaymentMode ?? allowedModes[0];
        if (!allowedModes.Contains(paymentMode)) return Results.UnprocessableEntity();
        if (checkout is not null && string.IsNullOrWhiteSpace(request.ReturnUrl))
            return Results.UnprocessableEntity();

        var result = await store.CreateAsync(new BookingCreateInput(
            request.CourtId, request.Date, request.StartMinute, request.DurationMinutes,
            request.CustomerName, request.CustomerPhone, request.CustomerEmail,
            BookingOrigin.Portal, paymentMode, CreatedBy: null), cancellationToken);
        if (result.Outcome != BookingCreateOutcome.Created)
            return result.Outcome switch
            {
                BookingCreateOutcome.UnknownCourt => Results.NotFound(),
                BookingCreateOutcome.InvalidSlot => Results.UnprocessableEntity(),
                BookingCreateOutcome.SlotTaken => Results.Conflict(),
                _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
            };

        string? checkoutUrl = null;
        if (paymentMode != PaymentMode.Club)
        {
            var club = await clubSettings.GetAsync(cancellationToken);
            var title = $"{club.Name} · reserva {request.Date:dd/MM} {TimeLabel(request.StartMinute)}";
            // The client cannot know the booking id before this call; the return URL gets it here.
            var returnUrl = CheckoutReturnUrl.For(paymentsOptions.Value, request.ReturnUrl!, result.Id);
            try
            {
                var session = await checkout!.CreateCheckoutAsync(new CheckoutRequest(
                    result.Id, clubSlug, title, result.ChargeAmount, result.ExpiresAt!.Value, returnUrl),
                    cancellationToken);
                checkoutUrl = session.Url;
            }
            catch
            {
                // No checkout, no hold: otherwise the slot stays blocked for the whole TTL.
                await store.CancelAsync(result.Id, cancellationToken);
                throw;
            }
        }

        return Results.Created($"/api/portal/{clubSlug}/bookings/{result.Id}",
            new PortalBookingCreatedResponse(result.Id, result.Price.Amount, result.ChargeAmount.Amount,
                result.Status, result.ExpiresAt, checkoutUrl));
    }

    private static async Task<IResult> GetBookingAsync(Guid id, IBookingsStore store, CancellationToken cancellationToken) =>
        await store.GetAsync(id, cancellationToken) is { } snapshot ? Results.Ok(snapshot) : Results.NotFound();

    // The buyer is staring at the waiting screen: ask the providers right now instead of J2.
    // Anonymous endpoint: the booking must exist before an unknown id can reach the provider's API.
    private static async Task<IResult> SettleBookingAsync(Guid id, IBookingsStore store,
        SettleBookingHandler handler, CancellationToken cancellationToken)
    {
        if (await store.GetAsync(id, cancellationToken) is null) return Results.NotFound();
        return Results.Ok(new { outcome = await handler.HandleAsync(id, cancellationToken) });
    }

    // Only frees pending holds; anything already settled is left as is, so the call is idempotent.
    private static async Task<IResult> ReleaseBookingAsync(Guid id, IBookingsStore store, CancellationToken cancellationToken) =>
        await store.ReleaseHoldAsync(id, cancellationToken) == HoldReleaseOutcome.NotFound
            ? Results.NotFound()
            : Results.NoContent();

    private static string TimeLabel(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    private sealed record PortalBookingRequest(
        Guid CourtId, DateOnly Date, int StartMinute, int DurationMinutes,
        string CustomerName, string CustomerPhone, string? CustomerEmail,
        PaymentMode? PaymentMode, string? ReturnUrl);

    private sealed record PortalBookingCreatedResponse(Guid Id, decimal Price, decimal ChargeAmount,
        BookingStatus Status, DateTimeOffset? ExpiresAt, string? CheckoutUrl);

    private static async Task<IResult> GetCatalogAsync(GetPortalCatalogHandler handler,
        IEnumerable<IHostedCheckout> checkouts, CancellationToken cancellationToken)
    {
        var catalog = await handler.HandleAsync(cancellationToken);
        return Results.Ok(new { catalog.Club, catalog.Sports, OnlinePayments = checkouts.Any() });
    }

    private static async Task<IResult> GetAvailabilityAsync(string sport, DateOnly from, DateOnly to,
        GetPortalAvailabilityHandler handler, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Sport>(sport, ignoreCase: true, out var parsedSport)) return Results.BadRequest();

        var result = await handler.HandleAsync(parsedSport, from, to, cancellationToken);
        return result.Outcome switch
        {
            PortalAvailabilityOutcome.Ok => Results.Ok(result.Availability),
            PortalAvailabilityOutcome.RangeTooLong => Results.UnprocessableEntity(),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
        };
    }
}
