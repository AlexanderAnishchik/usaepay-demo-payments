using System.Collections.Concurrent;
using USAePayPortal.Models;

namespace USAePayPortal.Services;

/// <summary>
/// In-memory store for saved customers and their recurring payment history.
/// Replace with a persistent store (EF Core, etc.) in production.
/// </summary>
public class CustomerStoreSingleton
{
    private readonly ConcurrentDictionary<string, CustomerEntry> _customers = new();

    /// <summary>Adds or replaces a customer entry keyed by <paramref name="customerKey"/>.</summary>
    public void Add(string customerKey, string paymentMethodKey, string name, string email)
    {
        _customers[customerKey] = new CustomerEntry(customerKey, paymentMethodKey, name, email);
    }

    /// <summary>Returns a read-only snapshot of all saved customers.</summary>
    public IReadOnlyList<CustomerEntry> GetAll() =>
        _customers.Values.ToList().AsReadOnly();

    /// <summary>Returns the entry for <paramref name="customerKey"/>, or <c>null</c> if not found.</summary>
    public CustomerEntry? Get(string customerKey) =>
        _customers.TryGetValue(customerKey, out CustomerEntry? entry) ? entry : null;

    /// <summary>Removes the entry for <paramref name="customerKey"/>. Returns <c>true</c> if it existed.</summary>
    public bool Remove(string customerKey) =>
        _customers.TryRemove(customerKey, out _);

    /// <summary>Appends <paramref name="record"/> to the payment history of the given customer.</summary>
    public void RecordPayment(string customerKey, RecurringPaymentRecord record)
    {
        if (_customers.TryGetValue(customerKey, out CustomerEntry? entry))
        {
            entry.Payments.Add(record);
        }
    }
}

/// <summary>
/// A saved customer with their associated payment method and charge history.
/// </summary>
public class CustomerEntry(string customerKey, string paymentMethodKey, string name, string email)
{
    /// <summary>USAePay customer key.</summary>
    public string CustomerKey { get; } = customerKey;

    /// <summary>USAePay payment method key for recurring billing.</summary>
    public string PaymentMethodKey { get; } = paymentMethodKey;

    /// <summary>Customer display name (<c>FirstName LastName</c>).</summary>
    public string Name { get; } = name;

    /// <summary>Customer e-mail address.</summary>
    public string Email { get; } = email;

    /// <summary>All recurring charge attempts recorded against this customer.</summary>
    public List<RecurringPaymentRecord> Payments { get; } = [];
}
