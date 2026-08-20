using ClubSpot.Infrastructure.Payments;
using Microsoft.AspNetCore.WebUtilities;

namespace ClubSpot.Api.Payments;

// Where the buyer lands after paying: the portal's return screen, which polls until the payment
// settles. Providers only honour auto_return over https, so when a public base url is
// configured the return hops through it and /api/payments/return bounces back to the portal.
internal static class CheckoutReturnUrl
{
    // The one place that decides whether a url may be handed to a payment provider or redirected to.
    // Origins are compared parsed, never as string prefixes: "https://club.com.attacker.io" starts
    // with an allowed "https://club.com" and would otherwise pass.
    public static bool IsAllowed(PaymentsOptions options, string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var target)
        && target.Scheme is "http" or "https"
        && options.AllowedReturnOrigins.Any(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var allowedOrigin)
            && Uri.Compare(target, allowedOrigin, UriComponents.SchemeAndServer, UriFormat.UriEscaped,
                StringComparison.OrdinalIgnoreCase) == 0);

    public static string For(PaymentsOptions options, string portalUrl, Guid bookingId)
    {
        var target = QueryHelpers.AddQueryString(portalUrl, "retorno", bookingId.ToString());
        return !string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            && !target.StartsWith("https", StringComparison.OrdinalIgnoreCase)
            ? $"{options.PublicBaseUrl}/api/payments/return?to={Uri.EscapeDataString(target)}"
            : target;
    }
}
