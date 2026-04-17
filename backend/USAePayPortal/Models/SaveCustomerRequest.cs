using System.Text.Json.Serialization;

namespace USAePayPortal.Models;

/// <summary>
/// Request to save a customer and card to the USAePay vault with an initial charge.
/// </summary>
public record SaveCustomerRequest(
    [property: JsonPropertyName("paymentKey")] string PaymentKey,
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("chargeAmount")] decimal ChargeAmount,
    [property: JsonPropertyName("phone")] string? Phone = null
);
