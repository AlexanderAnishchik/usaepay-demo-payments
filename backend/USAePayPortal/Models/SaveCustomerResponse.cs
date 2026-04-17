namespace USAePayPortal.Models;

/// <summary>
/// Result of saving a customer and running the initial charge.
/// </summary>
public record SaveCustomerResponse(
    bool Success,
    string? CustomerKey,
    string? PaymentMethodKey,
    string? Name,
    string? Error,
    ChargeResponse? InitialCharge = null
);
