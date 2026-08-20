using System.Globalization;
using System.Text.Encodings.Web;

namespace ClubSpot.Api.Endpoints;

// Development only: stands in for the payment provider's checkout page. Its buttons hit the
// real fake webhook, so the whole online-payment flow is exercised without credentials.
public static class DevCheckoutEndpoints
{
    public static IEndpointRouteBuilder MapDevCheckout(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dev/checkout", Render).AllowAnonymous().ExcludeFromDescription();
        return app;
    }

    private static IResult Render(string club, Guid booking, string title, decimal amount, string currency, string @return)
    {
        var encoder = HtmlEncoder.Default;
        var amountLabel = amount.ToString("N0", CultureInfo.GetCultureInfo("es-AR"));
        var html = $$"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Checkout de prueba</title>
              <style>
                body { font-family: system-ui, sans-serif; background: #f4f4f5; margin: 0;
                       display: flex; align-items: center; justify-content: center; min-height: 100vh; }
                .card { background: #fff; border-radius: 16px; padding: 32px; max-width: 420px; width: 90%;
                        box-shadow: 0 8px 30px rgba(0,0,0,.08); }
                h1 { font-size: 15px; color: #666; margin: 0 0 4px; font-weight: 600; }
                .title { font-size: 18px; font-weight: 700; margin-bottom: 4px; }
                .amount { font-size: 34px; font-weight: 800; margin: 12px 0 24px; }
                button { width: 100%; border: 0; border-radius: 10px; padding: 14px; font-size: 15px;
                         font-weight: 700; cursor: pointer; margin-top: 10px; }
                .ok { background: #16a34a; color: #fff; }
                .bad { background: #eee; color: #b91c1c; }
                .note { font-size: 12px; color: #999; margin-top: 18px; text-align: center; }
              </style>
            </head>
            <body>
              <div class="card">
                <h1>Checkout de prueba — sin dinero real</h1>
                <div class="title">{{encoder.Encode(title)}}</div>
                <div class="amount">$ {{amountLabel}} <small style="font-size:14px">{{encoder.Encode(currency)}}</small></div>
                <button class="ok" onclick="pay(true)">Aprobar pago</button>
                <button class="bad" onclick="pay(false)">Rechazar pago</button>
                <div class="note">Simula el resultado que informaría el proveedor de pagos.</div>
              </div>
              <script>
                async function pay(approved) {
                  const externalId = 'fake-' + crypto.randomUUID();
                  await fetch('/api/payments/fake/webhook/{{encoder.Encode(club)}}', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                      bookingId: '{{booking}}',
                      externalId,
                      approved,
                      amount: {{amount.ToString(CultureInfo.InvariantCulture)}}
                    })
                  });
                  window.location = {{JsString(@return)}};
                }
              </script>
            </body>
            </html>
            """;
        return Results.Content(html, "text/html");
    }

    private static string JsString(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);
}
