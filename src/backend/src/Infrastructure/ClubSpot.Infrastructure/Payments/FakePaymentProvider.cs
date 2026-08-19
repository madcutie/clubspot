using ClubSpot.Application.Bookings;
using Microsoft.Extensions.Options;

namespace ClubSpot.Infrastructure.Payments;

// Development only: "checkout" is a page served by the API whose buttons hit the real webhook.
public sealed class FakePaymentProvider(IOptions<PaymentsOptions> options) : IHostedCheckout
{
    public const string ProviderName = "fake";

    public string Name => ProviderName;

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var url = $"{options.Value.ApiBaseUrl}/dev/checkout" +
            $"?club={Uri.EscapeDataString(request.ClubSlug)}" +
            $"&booking={request.BookingId}" +
            $"&title={Uri.EscapeDataString(request.Title)}" +
            $"&amount={request.Amount.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&currency={request.Amount.Currency}" +
            $"&return={Uri.EscapeDataString(request.ReturnUrl)}";
        return Task.FromResult(new CheckoutSession(url));
    }

    // The fake has no store of its own: whatever the dev checkout posted already hit the webhook.
    public Task<IReadOnlyList<PaymentNotification>> FindPaymentsAsync(Guid bookingId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PaymentNotification>>([]);
}
