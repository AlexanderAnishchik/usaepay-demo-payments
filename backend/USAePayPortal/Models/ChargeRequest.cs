using System.Text.Json.Serialization;

namespace USAePayPortal.Models;

// Inbound request models

/// <summary>
/// Single-use token returned by pay.js <c>client.getPaymentKey()</c>.
/// Replaces the raw PAN so card data never reaches the server.
/// </summary>
public record ChargeRequest(
    [property: JsonPropertyName("paymentKey")] string PaymentKey,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("description")] string? Description = null
);
