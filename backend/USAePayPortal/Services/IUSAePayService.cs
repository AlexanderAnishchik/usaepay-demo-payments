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
    /// and uses the approved transaction reference to attach a reusable payment method.
    /// If vaulting fails after approval, the result includes the initial charge receipt.
    /// </summary>
    Task<SaveCustomerResponse> SaveCustomerAsync(
        string paymentKey, string firstName, string lastName, string email,
        decimal chargeAmount, string? phone = null);

    /// <summary>
    /// Runs a <c>customer:sale</c> via <c>POST /transactions</c> with the
    /// customer's key and stored payment method key.
    /// </summary>
    Task<RecurringPaymentRecord> ChargeCustomerAsync(string customerKey, string paymentMethodKey, decimal amount);
}
