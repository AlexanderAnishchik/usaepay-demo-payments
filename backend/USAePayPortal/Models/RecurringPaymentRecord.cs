namespace USAePayPortal.Models;

/// <summary>
/// A single recurring charge attempt record.
/// </summary>
public record RecurringPaymentRecord(
    DateTime Timestamp,
    decimal Amount,
    bool Success,
    string? TransactionKey,
    string? Error
);
