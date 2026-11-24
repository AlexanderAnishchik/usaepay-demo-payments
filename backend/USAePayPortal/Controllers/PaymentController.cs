using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using USAePayPortal.Models;
using USAePayPortal.Services;

namespace USAePayPortal.Controllers;

/// <summary>
/// Exposes the USAePay payment gateway operations as a REST API.
/// </summary>
[ApiController]
[Route("api/payment")]
public class PaymentController(
    IUSAePayService usaePay,
    CustomerStoreSingleton store,
    IOptions<USAePayOptions> opts) : ControllerBase
{
    private const decimal RecurringChargeAmount = 1.00m;

    private readonly IUSAePayService _usaePay = usaePay;
    private readonly CustomerStoreSingleton _store = store;
    private readonly USAePayOptions _opts = opts.Value;

    // Public API key (used by pay.js in the browser)

    /// <summary>
    /// Returns the Public API Key so the frontend can initialise pay.js.
    /// The public key is safe to expose — it cannot make server-side API calls.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig() =>
        Ok(new
        {
            publicApiKey = _opts.PublicApiKey,
            // pay.js reads its own <script> src to set the API host, so loading
            // from sandbox.usaepay.com makes the hosted card iframe and all
            // tokenisation calls target the sandbox environment.
            payJsUrl = "https://sandbox.usaepay.com/js/v1/pay.js"
        });

    // One-off charge

    /// <summary>
    /// Accepts the single-use <c>payment_key</c> produced by pay.js and
    /// processes a credit-card sale for the requested amount.
    /// </summary>
    [HttpPost("charge")]
    public async Task<IActionResult> Charge([FromBody] ChargeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PaymentKey))
        {
            return BadRequest(new { error = "payment_key is required" });
        }

        if (req.Amount <= 0)
        {
            return BadRequest(new { error = "amount must be greater than zero" });
        }

        ChargeResponse result = await _usaePay.ChargeTokenAsync(req.PaymentKey, req.Amount, req.Description);

        if (!result.Success)
        {
            return UnprocessableEntity(new { error = result.Error, detail = result.ResultText });
        }

        return Ok(result);
    }

    // Save card + initial charge

    /// <summary>
    /// Saves the card to the USAePay customer vault and registers the customer
    /// for recurring billing.
    /// The approved sale's transaction reference is used to attach a reusable payment method.
    /// If that step fails, the 422 response includes the approved initial charge.
    /// </summary>
    [HttpPost("customers")]
    public async Task<IActionResult> SaveCustomer([FromBody] SaveCustomerRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PaymentKey))
        {
            return BadRequest(new { error = "payment_key is required" });
        }

        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
        {
            return BadRequest(new { error = "first name and last name are required" });
        }

        if (string.IsNullOrWhiteSpace(req.Email))
        {
            return BadRequest(new { error = "email is required" });
        }

        if (req.ChargeAmount <= 0)
        {
            return BadRequest(new { error = "chargeAmount must be greater than zero" });
        }

        SaveCustomerResponse saveResult = await _usaePay.SaveCustomerAsync(
            req.PaymentKey, req.FirstName, req.LastName, req.Email, req.ChargeAmount, req.Phone);

        if (!saveResult.Success)
        {
            if (saveResult.InitialCharge is { Success: true } initialCharge)
            {
                return UnprocessableEntity(new { error = saveResult.Error, initialCharge });
            }

            return UnprocessableEntity(new { error = saveResult.Error });
        }

        // Register in local store for recurring billing.
        _store.Add(saveResult.CustomerKey!, saveResult.PaymentMethodKey!, saveResult.Name!, req.Email);

        return Ok(new
        {
            customer = saveResult,
            initialCharge = saveResult.InitialCharge
        });
    }

    // Recurring status

    /// <summary>Returns all saved customers and their recurring payment history.</summary>
    [HttpGet("recurring")]
    public IActionResult GetRecurringStatus()
    {
        List<RecurringCustomerStatus> result = _store.GetAll()
            .Select(c => new RecurringCustomerStatus(
                c.CustomerKey,
                c.PaymentMethodKey,
                c.Name,
                c.Email,
                c.Payments))
            .ToList();

        return Ok(result);
    }

    // Force billing cycle

    /// <summary>Immediately runs a billing cycle for every saved customer.</summary>
    [HttpPost("recurring/run")]
    public async Task<IActionResult> ForceRecurringRun()
    {
        IReadOnlyList<CustomerEntry> customers = _store.GetAll();
        List<object> results = new();

        foreach (CustomerEntry customer in customers)
        {
            RecurringPaymentRecord record = await _usaePay.ChargeCustomerAsync(
                customer.CustomerKey, customer.PaymentMethodKey, RecurringChargeAmount);

            _store.RecordPayment(customer.CustomerKey, record);

            results.Add(new
            {
                customerKey = customer.CustomerKey,
                name = customer.Name,
                record.Success,
                record.TransactionKey,
                record.Error,
            });
        }

        return Ok(new { charged = results.Count, results });
    }

    // Delete customer from local store

    /// <summary>
    /// Removes the customer from the local recurring-billing store.
    /// The customer record in the USAePay portal is NOT affected.
    /// </summary>
    [HttpDelete("customers/{customerKey}")]
    public IActionResult DeleteCustomer(string customerKey)
    {
        if (!_store.Remove(customerKey))
        {
            return NotFound(new { error = $"Customer '{customerKey}' not found in local store." });
        }

        return NoContent();
    }
}
