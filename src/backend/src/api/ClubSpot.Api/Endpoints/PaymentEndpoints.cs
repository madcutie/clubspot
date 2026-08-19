using System.Text.Json;
using ClubSpot.Api.Modularity;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.MercadoPago;
using ClubSpot.Infrastructure.Payments;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.Extensions.Options;

namespace ClubSpot.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPayments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments")
            .AllowAnonymous();
        // Mandatory order: the slug filter opens the tenant scope that RequireModule needs.
        group.AddEndpointFilter(ClubScope.ResolveAsync);
        group.RequireModule(ModuleId.Bookings);
        group.MapPost($"/{FakePaymentProvider.ProviderName}/webhook/{{clubSlug}}", FakeWebhookAsync);
        group.MapPost($"/{MercadoPagoProvider.ProviderName}/webhook/{{clubSlug}}", MercadoPagoWebhookAsync);

        // Checkout back urls must be https; this hop lives behind the public tunnel and bounces
        // the buyer back to the local portal so auto_return works in development.
        app.MapGet("/api/payments/return", Return).AllowAnonymous();
        return app;
    }

    private static IResult Return(string to, IOptions<PaymentsOptions> options)
    {
        var allowed = options.Value.AllowedReturnOrigins
            .Any(origin => to.StartsWith(origin, StringComparison.OrdinalIgnoreCase));
        return allowed ? Results.Redirect(to) : Results.BadRequest();
    }

    private static async Task<IResult> FakeWebhookAsync(FakeWebhookRequest request, IBookingsStore store,
        IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return Results.NotFound();

        var outcome = await store.ApplyPaymentAsync(new PaymentNotification(
            request.BookingId, FakePaymentProvider.ProviderName, PaymentRail.Checkout,
            request.ExternalId, request.Approved, request.Amount),
            PaymentSource.Webhook, cancellationToken);
        return Results.Ok(new { outcome });
    }

    // Mercado Pago retries until it gets a 2xx, so anything not actionable is acknowledged —
    // except a bad signature when strict validation is on, which is rejected outright.
    private static async Task<IResult> MercadoPagoWebhookAsync(HttpRequest httpRequest, IBookingsStore store,
        IServiceProvider services, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var provider = services.GetService<MercadoPagoProvider>();
        if (provider is null) return Results.NotFound();

        var dataId = httpRequest.Query["data.id"].FirstOrDefault();
        var signatureValid = provider.VerifyWebhookSignature(
            httpRequest.Headers["x-signature"].FirstOrDefault(),
            httpRequest.Headers["x-request-id"].FirstOrDefault(),
            dataId);
        if (!signatureValid)
        {
            var logger = loggerFactory.CreateLogger("MercadoPagoWebhook");
            logger.LogWarning(
                "Invalid webhook signature (data.id {DataId}, request-id {RequestId}, x-signature '{Signature}').",
                dataId, httpRequest.Headers["x-request-id"].FirstOrDefault(),
                httpRequest.Headers["x-signature"].FirstOrDefault());
            if (services.GetRequiredService<IOptions<MercadoPagoOptions>>().Value.RequireValidSignature)
                return Results.Unauthorized();
        }

        var paymentId = dataId;
        if (paymentId is null && httpRequest.ContentLength > 0)
        {
            using var body = await JsonDocument.ParseAsync(httpRequest.Body, cancellationToken: cancellationToken);
            if (body.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
                paymentId = id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
        }
        if (string.IsNullOrEmpty(paymentId)) return Results.Ok();

        var notification = await provider.GetPaymentAsync(paymentId, cancellationToken);
        if (notification is null) return Results.Ok();

        await store.ApplyPaymentAsync(notification, PaymentSource.Webhook, cancellationToken);
        return Results.Ok();
    }

    private sealed record FakeWebhookRequest(Guid BookingId, string ExternalId, bool Approved, decimal? Amount);
}
