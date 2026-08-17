using System.Text.Json;
using ClubSpot.Api.Modularity;
using ClubSpot.Application.Bookings;
using ClubSpot.Infrastructure.MercadoPago;
using ClubSpot.Infrastructure.Payments;
using ClubSpot.SharedKernel.Modularity;

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
        group.MapPost($"/{FakePaymentGateway.GatewayName}/webhook/{{clubSlug}}", FakeWebhookAsync);
        group.MapPost($"/{MercadoPagoGateway.GatewayName}/webhook/{{clubSlug}}", MercadoPagoWebhookAsync);
        return app;
    }

    private static async Task<IResult> FakeWebhookAsync(FakeWebhookRequest request, IBookingsStore store,
        IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return Results.NotFound();

        var outcome = await store.ApplyPaymentAsync(new PaymentNotification(
            request.BookingId, FakePaymentGateway.GatewayName, request.ExternalId, request.Approved, request.Amount),
            cancellationToken);
        return Results.Ok(new { outcome });
    }

    // Mercado Pago retries until it gets a 2xx, so anything not actionable is acknowledged —
    // except a bad signature, which is rejected outright.
    private static async Task<IResult> MercadoPagoWebhookAsync(HttpRequest httpRequest, IBookingsStore store,
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var gateway = services.GetService<MercadoPagoGateway>();
        if (gateway is null) return Results.NotFound();

        if (!gateway.VerifyWebhookSignature(
                httpRequest.Headers["x-signature"].FirstOrDefault(),
                httpRequest.Headers["x-request-id"].FirstOrDefault(),
                httpRequest.Query["data.id"].FirstOrDefault()))
            return Results.Unauthorized();

        var paymentId = httpRequest.Query["data.id"].FirstOrDefault();
        if (paymentId is null && httpRequest.ContentLength > 0)
        {
            using var body = await JsonDocument.ParseAsync(httpRequest.Body, cancellationToken: cancellationToken);
            if (body.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
                paymentId = id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
        }
        if (string.IsNullOrEmpty(paymentId)) return Results.Ok();

        var notification = await gateway.GetPaymentAsync(paymentId, cancellationToken);
        if (notification is null) return Results.Ok();

        await store.ApplyPaymentAsync(notification, cancellationToken);
        return Results.Ok();
    }

    private sealed record FakeWebhookRequest(Guid BookingId, string ExternalId, bool Approved, decimal? Amount);
}
