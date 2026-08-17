using System.Security.Cryptography;
using System.Text;
using ClubSpot.Infrastructure.MercadoPago;

namespace ClubSpot.UnitTests.MercadoPago;

public sealed class MercadoPagoWebhookSignatureTests
{
    private const string Secret = "test-secret";

    [Fact]
    public void A_correctly_signed_notification_is_valid()
    {
        var header = Sign("id:12345;request-id:req-1;ts:1704908010;", ts: "1704908010");

        Assert.True(MercadoPagoWebhookSignature.IsValid(Secret, header, "req-1", "12345"));
    }

    [Fact]
    public void An_alphanumeric_data_id_is_lowercased_before_signing()
    {
        var header = Sign("id:abc123;request-id:req-1;ts:1704908010;", ts: "1704908010");

        Assert.True(MercadoPagoWebhookSignature.IsValid(Secret, header, "req-1", "ABC123"));
    }

    [Fact]
    public void Missing_sections_are_omitted_from_the_manifest()
    {
        var header = Sign("ts:1704908010;", ts: "1704908010");

        Assert.True(MercadoPagoWebhookSignature.IsValid(Secret, header, xRequestId: null, dataId: null));
    }

    [Fact]
    public void A_tampered_data_id_is_rejected()
    {
        var header = Sign("id:12345;request-id:req-1;ts:1704908010;", ts: "1704908010");

        Assert.False(MercadoPagoWebhookSignature.IsValid(Secret, header, "req-1", "99999"));
    }

    [Fact]
    public void A_wrong_secret_is_rejected()
    {
        var header = Sign("id:12345;request-id:req-1;ts:1704908010;", ts: "1704908010");

        Assert.False(MercadoPagoWebhookSignature.IsValid("other-secret", header, "req-1", "12345"));
    }

    [Fact]
    public void A_missing_header_is_rejected()
    {
        Assert.False(MercadoPagoWebhookSignature.IsValid(Secret, null, "req-1", "12345"));
    }

    private static string Sign(string manifest, string ts)
    {
        var hash = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(manifest)));
        return $"ts={ts},v1={hash}";
    }
}
