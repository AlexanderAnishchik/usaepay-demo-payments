namespace USAePayPortal.Models;

// Outbound response models

/// <summary>
/// Result of a one-off credit-card charge.
/// </summary>
public record ChargeResponse(
    bool Success,
    string? TransactionKey,
    string? AuthCode,
    string? ResultText,
    string? Error
);
