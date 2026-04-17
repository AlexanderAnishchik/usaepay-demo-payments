using USAePayPortal.Models;

namespace USAePayPortal.Services;

/// <summary>
/// Abstraction over the USAePay v2 REST API used by the payment portal.
/// </summary>
public interface IUSAePayService
{
    /// <summary>
    /// Charges the card identified by <paramref name="paymentKey"/> (single-use
    /// token returned by the pay.js <c>client.getPaymentKey()</c> call).
    /// </summary>
    Task<ChargeResponse> ChargeTokenAsync(string paymentKey, decimal amount, string? description = null);

    /// <summary>
    /// Creates a customer in the USAePay vault, runs an initial cc:sale with
    /// <paramref name="chargeAmount"/> using the single-use <paramref name="paymentKey"/>,
    /// converts the resulting transaction into a reusable vault token, then attaches
    /// that token as a payment method for future recurring billing.
    /// </summary>
    Task<SaveCustomerResponse> SaveCustomerAsync(
        string paymentKey, string firstName, string lastName, string email,
        decimal chargeAmount, string? phone = null);

    /// <summary>
    /// Runs a cc:sale against the customer's stored payment method via
    /// <c>POST /customers/{custkey}/transactions</c> (USAePay Customer Transaction).
    /// </summary>
    Task<RecurringPaymentRecord> ChargeCustomerAsync(string customerKey, string paymentMethodKey, decimal amount);
}
