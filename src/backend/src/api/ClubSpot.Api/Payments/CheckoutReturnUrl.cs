using ClubSpot.Infrastructure.Payments;
using Microsoft.AspNetCore.WebUtilities;

namespace ClubSpot.Api.Payments;

// Where the buyer lands after paying: the portal's return screen, which polls until the payment
// settles. Providers only honour auto_return over https, so when a public base url is
// configured the return hops through it and /api/payments/return bounces back to the portal.
internal static class CheckoutReturnUrl
{
    public static string For(PaymentsOptions options, string portalUrl, Guid bookingId)
    {
        var target = QueryHelpers.AddQueryString(portalUrl, "retorno", bookingId.ToString());
        return !string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            && !target.StartsWith("https", StringComparison.OrdinalIgnoreCase)
            ? $"{options.PublicBaseUrl}/api/payments/return?to={Uri.EscapeDataString(target)}"
            : target;
    }
}
