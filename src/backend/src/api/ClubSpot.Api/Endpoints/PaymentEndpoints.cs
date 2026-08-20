using System.Text.Json;
using ClubSpot.Api.Modularity;
using ClubSpot.Api.Payments;
using ClubSpot.Application.Bookings;
using ClubSpot.Domain.Bookings;
using ClubSpot.Infrastructure.MercadoPago;
using ClubSpot.Infrastructure.Payments;
using ClubSpot.SharedKernel.Activity;
using ClubSpot.SharedKernel.Modularity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace ClubSpot.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPayments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments")
            .AllowAnonymous();
        // Mandatory order: the slug filter opens the tenant scope that RequireModule needs.
        group.AddEndpointFilter((context, next) =>
            ClubScope.ResolveAsync(context, next, ActivityActor.Webhook("payments")));
        group.RequireModule(ModuleId.Bookings);
        group.WithTags("payments");
        group.MapPost($"/{FakePaymentProvider.ProviderName}/webhook/{{clubSlug}}", FakeWebhookAsync).WithName("PostFakeWebhook");
        group.MapPost($"/{MercadoPagoProvider.ProviderName}/webhook/{{clubSlug}}", MercadoPagoWebhookAsync).WithName("PostMercadoPagoWebhook");

        // Checkout back urls must be https; this hop lives behind the public tunnel and bounces
        // the buyer back to the local portal so auto_return works in development.
        app.MapGet("/api/payments/return", Return).AllowAnonymous().ExcludeFromDescription();
        return app;
    }

    private static IResult Return(string to, IOptions<PaymentsOptions> options) =>
        CheckoutReturnUrl.IsAllowed(options.Value, to) ? Results.Redirect(to) : Results.BadRequest();

    private static async Task<Results<Ok<PaymentApplyResponse>, NotFound>> FakeWebhookAsync(FakeWebhookRequest request, IBookingsStore store,
        IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return TypedResults.NotFound();

        var outcome = await store.ApplyPaymentAsync(new PaymentNotification(
            request.BookingId, FakePaymentProvider.ProviderName, PaymentRail.Checkout,
            request.ExternalId,
            request.Outcome ?? (request.Approved ? PaymentOutcome.Approved : PaymentOutcome.Rejected),
            request.Amount, request.Currency),
            PaymentSource.Webhook, cancellationToken);
        return TypedResults.Ok(new PaymentApplyResponse(outcome));
    }

    // Mercado Pago retries until it gets a 2xx, so anything not actionable is acknowledged —
    // except a bad signature when strict validation is on, which is rejected outright.
    private static async Task<Results<Ok, NotFound, UnauthorizedHttpResult>> MercadoPagoWebhookAsync(HttpRequest httpRequest, IBookingsStore store,
        IServiceProvider services, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var provider = services.GetService<MercadoPagoProvider>();
        if (provider is null) return TypedResults.NotFound();

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
                return TypedResults.Unauthorized();
        }

        var paymentId = dataId;
        if (paymentId is null && httpRequest.ContentLength > 0)
        {
            using var body = await JsonDocument.ParseAsync(httpRequest.Body, cancellationToken: cancellationToken);
            if (body.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
                paymentId = id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
        }
        if (string.IsNullOrEmpty(paymentId)) return TypedResults.Ok();

        var notification = await provider.GetPaymentAsync(paymentId, cancellationToken);
        if (notification is null) return TypedResults.Ok();

        await store.ApplyPaymentAsync(notification, PaymentSource.Webhook, cancellationToken);
        return TypedResults.Ok();
    }

    // Outcome overrides Approved when present: the dev checkout page only knows approve/reject, but a
    // test needs to drive the undecided state the real providers report.
    internal sealed record FakeWebhookRequest(Guid BookingId, string ExternalId, bool Approved, decimal? Amount,
        string? Currency = null, PaymentOutcome? Outcome = null);

    internal sealed record PaymentApplyResponse(PaymentApplyOutcome Outcome);
}
