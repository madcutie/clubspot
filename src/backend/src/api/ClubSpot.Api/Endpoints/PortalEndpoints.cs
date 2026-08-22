using ClubSpot.Api.Modularity;
using ClubSpot.Api.Payments;
using ClubSpot.Application.Bookings;
using ClubSpot.Application.Core;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.AspNetCore.Http.HttpResults;

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
        group.WithTags("portal");
        group.MapGet("/catalog", GetCatalogAsync).RequireRateLimiting(PortalRateLimits.Reads).WithName("GetPortalCatalog");
        group.MapGet("/availability", GetAvailabilityAsync).RequireRateLimiting(PortalRateLimits.Reads).WithName("GetPortalAvailability");
        group.MapGet("/bookings/{id:guid}", GetBookingAsync).RequireRateLimiting(PortalRateLimits.Reads).WithName("GetPortalBooking");
        // Taking, freeing or settling a slot is the expensive side: it writes, and it reaches the
        // payment provider. Anonymous callers get a much shorter leash there than on reads.
        group.MapPost("/bookings", CreateBookingAsync).RequireRateLimiting(PortalRateLimits.Bookings).WithName("CreatePortalBooking");
        group.MapPost("/bookings/{id:guid}/release", ReleaseBookingAsync).RequireRateLimiting(PortalRateLimits.Bookings).WithName("ReleasePortalBooking");
        group.MapPost("/bookings/{id:guid}/settle", SettleBookingAsync).RequireRateLimiting(PortalRateLimits.Bookings).WithName("SettlePortalBooking");
        return app;
    }

    private static async Task<Results<Created<PortalBookingCreatedResponse>, BadRequest, NotFound, UnprocessableEntity, Conflict>> CreateBookingAsync(
        string clubSlug, PortalBookingRequest request,
        IBookingsStore store, IClubSettings clubSettings, IEnumerable<IHostedCheckout> checkouts,
        Microsoft.Extensions.Options.IOptions<ClubSpot.Infrastructure.Payments.PaymentsOptions> paymentsOptions,
        PortalBookingToken tokens, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone))
            return TypedResults.BadRequest();

        // How a portal booking may be paid is the server's call, never the caller's: with a checkout
        // wired the slot is only held against an online payment, so a request asking to pay at the
        // club cannot confirm it for free. Paying at the club stays the only mode where no gateway
        // is contracted, which is a supported configuration (AGENTS.md §5).
        var checkout = checkouts.FirstOrDefault();
        PaymentMode[] allowedModes = checkout is null
            ? [PaymentMode.Club]
            : [PaymentMode.OnlineFull, PaymentMode.OnlineDeposit];
        var paymentMode = request.PaymentMode ?? allowedModes[0];
        if (!allowedModes.Contains(paymentMode)) return TypedResults.UnprocessableEntity();
        // Where the buyer is sent back to is the club's configuration, never the caller's: this is an
        // anonymous endpoint, and the url travels to the provider as a back url it auto-returns to.
        // Checking it only at /api/payments/return was not enough — that hop is skipped entirely for
        // an https url, which is also exactly what turns auto_return on.
        if (checkout is not null && !CheckoutReturnUrl.IsAllowed(paymentsOptions.Value, request.ReturnUrl))
            return TypedResults.UnprocessableEntity();

        var result = await store.CreateAsync(new BookingCreateInput(
            request.CourtId, request.Date, request.StartMinute, request.DurationMinutes,
            request.CustomerName, request.CustomerPhone, request.CustomerEmail,
            BookingOrigin.Portal, paymentMode, CreatedBy: null), cancellationToken);
        if (result.Outcome != BookingCreateOutcome.Created)
            return result.Outcome switch
            {
                BookingCreateOutcome.UnknownCourt => TypedResults.NotFound(),
                BookingCreateOutcome.InvalidSlot => TypedResults.UnprocessableEntity(),
                BookingCreateOutcome.SlotTaken => TypedResults.Conflict(),
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
                // A fresh hold has no earlier link, so this is the one just minted — taken from the
                // store anyway so both callers hand over whatever it says is the live one.
                var handed = await store.RecordCheckoutIssuedAsync(new CheckoutIssued(result.Id, checkout.Name,
                    session.Url, result.ChargeAmount, result.ExpiresAt.Value), cancellationToken);
                checkoutUrl = handed.Url;
            }
            catch
            {
                // No checkout, no hold: otherwise the slot stays blocked for the whole TTL. This is a
                // release and not a cancellation: nobody decided anything, so there is no reason to give.
                await store.ReleaseHoldAsync(result.Id, cancellationToken);
                throw;
            }
        }

        return TypedResults.Created($"/api/portal/{clubSlug}/bookings/{result.Id}",
            new PortalBookingCreatedResponse(result.Id, result.Price.Amount, result.ChargeAmount.Amount,
                result.Status, result.ExpiresAt, checkoutUrl, tokens.For(result.Id)));
    }

    // A wrong or missing token answers exactly like an id that does not exist: whoever is guessing
    // learns nothing about which bookings are real.
    private static async Task<Results<Ok<BookingSnapshot>, NotFound>> GetBookingAsync(Guid id, HttpRequest httpRequest,
        PortalBookingToken tokens, IBookingsStore store, CancellationToken cancellationToken)
    {
        if (!tokens.IsValid(id, httpRequest.Headers[PortalBookingToken.HeaderName].FirstOrDefault()))
            return TypedResults.NotFound();
        return await store.GetAsync(id, cancellationToken) is { } snapshot
            ? TypedResults.Ok(snapshot) : TypedResults.NotFound();
    }

    // The buyer is staring at the waiting screen: ask the providers right now instead of J2.
    // Anonymous endpoint: the token, then the booking, before an unknown id reaches the provider's API.
    private static async Task<Results<Ok<PortalSettleResponse>, NotFound>> SettleBookingAsync(Guid id, HttpRequest httpRequest,
        PortalBookingToken tokens, IBookingsStore store, SettleBookingHandler handler,
        CancellationToken cancellationToken)
    {
        if (!tokens.IsValid(id, httpRequest.Headers[PortalBookingToken.HeaderName].FirstOrDefault()))
            return TypedResults.NotFound();
        if (await store.GetAsync(id, cancellationToken) is null) return TypedResults.NotFound();
        return TypedResults.Ok(new PortalSettleResponse(await handler.HandleAsync(id, cancellationToken)));
    }

    // Only frees pending holds; anything already settled is left as is, so the call is idempotent.
    private static async Task<Results<NoContent, NotFound>> ReleaseBookingAsync(Guid id, HttpRequest httpRequest,
        PortalBookingToken tokens, IBookingsStore store, CancellationToken cancellationToken)
    {
        if (!tokens.IsValid(id, httpRequest.Headers[PortalBookingToken.HeaderName].FirstOrDefault()))
            return TypedResults.NotFound();
        return await store.ReleaseHoldAsync(id, cancellationToken) == HoldReleaseOutcome.NotFound
            ? TypedResults.NotFound()
            : TypedResults.NoContent();
    }

    private static string TimeLabel(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    internal sealed record PortalBookingRequest(
        Guid CourtId, DateOnly Date, int StartMinute, int DurationMinutes,
        string CustomerName, string CustomerPhone, string? CustomerEmail,
        PaymentMode? PaymentMode, string? ReturnUrl);

    internal sealed record PortalBookingCreatedResponse(Guid Id, decimal Price, decimal ChargeAmount,
        BookingStatus Status, DateTimeOffset? ExpiresAt, string? CheckoutUrl, string Token);

    internal sealed record PortalCatalogResponse(PortalClub Club, IReadOnlyList<PortalSport> Sports, bool OnlinePayments);

    internal sealed record PortalSettleResponse(PaymentApplyOutcome? Outcome);

    private static async Task<Ok<PortalCatalogResponse>> GetCatalogAsync(GetPortalCatalogHandler handler,
        IEnumerable<IHostedCheckout> checkouts, CancellationToken cancellationToken)
    {
        var catalog = await handler.HandleAsync(cancellationToken);
        return TypedResults.Ok(new PortalCatalogResponse(catalog.Club, catalog.Sports, checkouts.Any()));
    }

    private static async Task<Results<Ok<PortalAvailability>, BadRequest, UnprocessableEntity>> GetAvailabilityAsync(
        string sport, DateOnly from, DateOnly to,
        GetPortalAvailabilityHandler handler, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Sport>(sport, ignoreCase: true, out var parsedSport)) return TypedResults.BadRequest();

        var result = await handler.HandleAsync(parsedSport, from, to, cancellationToken);
        return result.Outcome switch
        {
            PortalAvailabilityOutcome.Ok => TypedResults.Ok(result.Availability!),
            PortalAvailabilityOutcome.RangeTooLong => TypedResults.UnprocessableEntity(),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Outcome))
        };
    }
}
