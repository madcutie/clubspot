using System.Globalization;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.SharedKernel.Time;
using MercadoPago.Client;
using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Error;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClubSpot.Infrastructure.MercadoPago;

// Checkout Pro: one preference per hold, redirect to its init point. The webhook is the only
// source of truth — the payment is always fetched back by id, never trusted from the POST body.
// Orders (in-person Point/QR) will be a second capability of this same provider (ADR-0015).
public sealed class MercadoPagoProvider(
    IOptions<MercadoPagoOptions> options, IClock clock, ILogger<MercadoPagoProvider> logger) : IHostedCheckout
{
    public const string ProviderName = "mercadopago";

    private readonly MercadoPagoOptions _options = options.Value;

    public string Name => ProviderName;

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            throw new InvalidOperationException("Payments:PublicBaseUrl is required to create a checkout.");

        var preference = await new PreferenceClient().CreateAsync(new PreferenceRequest
        {
            Items =
            [
                new PreferenceItemRequest
                {
                    Title = request.Title,
                    Quantity = 1,
                    UnitPrice = request.Amount.Amount,
                    CurrencyId = request.Amount.Currency
                }
            ],
            ExternalReference = request.BookingId.ToString(),
            NotificationUrl = $"{_options.PublicBaseUrl}/api/payments/{ProviderName}/webhook/{request.ClubSlug}",
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = request.ReturnUrl,
                Pending = request.ReturnUrl,
                Failure = request.ReturnUrl
            },
            // Mercado Pago only honours auto_return with https back urls; over plain http
            // (local dev) the buyer clicks "Volver al sitio" instead.
            AutoReturn = request.ReturnUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "approved" : null,
            // Approved or rejected, nothing in between: the short-lived hold cannot wait out an in_process.
            BinaryMode = true,
            Expires = true,
            ExpirationDateTo = request.ExpiresAt.UtcDateTime
        }, RequestOptions(), cancellationToken);

        return new CheckoutSession(preference.InitPoint);
    }

    // Called by the webhook endpoint: resolves a payment id into what the store needs.
    public async Task<PaymentNotification?> GetPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        if (!long.TryParse(paymentId, CultureInfo.InvariantCulture, out var id)) return null;

        try
        {
            var payment = await new PaymentClient().GetAsync(id, RequestOptions(), cancellationToken);
            if (payment.ExternalReference is null || !Guid.TryParse(payment.ExternalReference, out var bookingId))
                return null;
            return new PaymentNotification(bookingId, ProviderName, PaymentRail.Checkout,
                payment.Id?.ToString(CultureInfo.InvariantCulture) ?? paymentId,
                OutcomeOf(payment.Status, paymentId, bookingId), payment.TransactionAmount, payment.CurrencyId);
        }
        catch (MercadoPagoApiException)
        {
            return null;
        }
    }

    // Reconciliation: whatever Mercado Pago holds for this booking, delivered webhook or not.
    public async Task<IReadOnlyList<PaymentNotification>> FindPaymentsAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var reference = bookingId.ToString();
        var search = await new PaymentClient().SearchAsync(new SearchRequest
        {
            Filters = new Dictionary<string, object> { ["external_reference"] = reference }
        }, RequestOptions(), cancellationToken);

        // The reference is checked on the way back too: a filter the API silently ignores would
        // otherwise settle this booking with somebody else's payments.
        var mine = (search.Results ?? [])
            .Where(payment => payment.Id is not null
                && string.Equals(payment.ExternalReference, reference, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return mine
            .Select(payment => new PaymentNotification(bookingId, ProviderName, PaymentRail.Checkout,
                payment.Id!.Value.ToString(CultureInfo.InvariantCulture),
                OutcomeOf(payment.Status, payment.Id!.Value.ToString(CultureInfo.InvariantCulture), bookingId),
                payment.TransactionAmount, payment.CurrencyId))
            .ToList();
    }

    // Mercado Pago's terminal states are approved, rejected and cancelled; everything else — pending,
    // in_process, authorized — means the payment exists and is not decided. Collapsing those into a
    // rejection is what wrote a Rejected row under the payment's own id and made the approval that
    // followed look like a replay, leaving the money taken and the booking unconfirmed.
    //
    // Provisional (18/08/2026, still open): refunded and charged_back are read as rejected, so a
    // refund leaves an already confirmed booking confirmed. Replaced when refunds are modelled.
    private PaymentOutcome OutcomeOf(string? status, string paymentId, Guid bookingId)
    {
        switch (status)
        {
            case "approved": return PaymentOutcome.Approved;
            case "rejected" or "cancelled": return PaymentOutcome.Rejected;
            case "pending" or "in_process" or "in_mediation" or "authorized": return PaymentOutcome.Pending;
            default:
                logger.LogWarning(
                    "Mercado Pago payment {PaymentId} for booking {BookingId} reports status {Status}, " +
                    "which is recorded as rejected because refunds are not modelled yet.",
                    paymentId, bookingId, status);
                return PaymentOutcome.Rejected;
        }
    }

    public bool VerifyWebhookSignature(string? xSignature, string? xRequestId, string? dataId) =>
        MercadoPagoWebhookSignature.IsValid(_options.WebhookSecret, xSignature, xRequestId, dataId, clock.UtcNow);

    private RequestOptions RequestOptions() => new() { AccessToken = _options.AccessToken };
}
