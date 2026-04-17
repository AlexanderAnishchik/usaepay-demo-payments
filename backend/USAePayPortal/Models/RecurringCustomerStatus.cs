namespace USAePayPortal.Models;

/// <summary>
/// Saved customer and their full recurring payment history.
/// </summary>
internal record RecurringCustomerStatus(
    string CustomerKey,
    string PaymentMethodKey,
    string Name,
    string Email,
    List<RecurringPaymentRecord> Payments
);
