using ClubSpot.Api.Modularity;
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
        return app;
    }

    private static async Task<IResult> CreateBookingAsync(string clubSlug, PortalBookingRequest request,
        IBookingsStore store, IClubSettings clubSettings, IEnumerable<IPaymentGateway> gateways,
        Microsoft.Extensions.Options.IOptions<ClubSpot.Infrastructure.Payments.PaymentsOptions> paymentsOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone))
            return Results.BadRequest();

        var paymentMode = request.PaymentMode ?? PaymentMode.Club;
        var gateway = gateways.FirstOrDefault();
        if (paymentMode != PaymentMode.Club && (gateway is null || string.IsNullOrWhiteSpace(request.ReturnUrl)))
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
            var returnUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers
                .AddQueryString(request.ReturnUrl!, "retorno", result.Id.ToString());
            // Providers demand https back urls: route the return through the public tunnel,
            // which bounces to the local portal (/api/payments/return) and enables auto_return.
            var publicBaseUrl = paymentsOptions.Value.PublicBaseUrl;
            if (!string.IsNullOrWhiteSpace(publicBaseUrl) && !returnUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                returnUrl = $"{publicBaseUrl}/api/payments/return?to={Uri.EscapeDataString(returnUrl)}";
            try
            {
                var session = await gateway!.CreateCheckoutAsync(new CheckoutRequest(
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

    private static string TimeLabel(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    private sealed record PortalBookingRequest(
        Guid CourtId, DateOnly Date, int StartMinute, int DurationMinutes,
        string CustomerName, string CustomerPhone, string? CustomerEmail,
        PaymentMode? PaymentMode, string? ReturnUrl);

    private sealed record PortalBookingCreatedResponse(Guid Id, decimal Price, decimal ChargeAmount,
        BookingStatus Status, DateTimeOffset? ExpiresAt, string? CheckoutUrl);

    private static async Task<IResult> GetCatalogAsync(GetPortalCatalogHandler handler,
        IEnumerable<IPaymentGateway> gateways, CancellationToken cancellationToken)
    {
        var catalog = await handler.HandleAsync(cancellationToken);
        return Results.Ok(new { catalog.Club, catalog.Sports, OnlinePayments = gateways.Any() });
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
