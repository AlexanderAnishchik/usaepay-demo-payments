using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using USAePayPortal.Models;

namespace USAePayPortal.Services;

// Configuration

/// <summary>
/// Strongly-typed settings for the USAePay v2 REST API.
/// </summary>
public class USAePayOptions
{
    /// <summary>Server-side API key used for HMAC-signed requests.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>PIN used as part of the HMAC-SHA256 hash computation.</summary>
    public string Pin { get; set; } = string.Empty;

    /// <summary>
    /// Public (client-side) API key used to initialise pay.js in the browser.
    /// Safe to expose — it cannot be used for server-side API calls.
    /// </summary>
    public string PublicApiKey { get; set; } = string.Empty;

    /// <summary>Base URL of the USAePay v2 REST API.</summary>
    public string BaseUrl { get; set; } = "https://sandbox.usaepay.com/api/v2";

    /// <summary>Allowed origin for CORS (the frontend URL).</summary>
    public string FrontendUrl { get; set; } = "http://localhost:5173";
}

// Service implementation

/// <summary>
/// Implements <see cref="IUSAePayService"/> by calling the USAePay v2 REST API directly.
/// </summary>
internal class USAePayService(
    IOptions<USAePayOptions> opts,
    HttpClient http,
    ILogger<USAePayService> logger) : IUSAePayService
{
    private const string PortalChargeDescription = "Portal charge";
    private const string InitialChargeDescription = "Initial charge";
    private const string RecurringChargeDescription = "Recurring charge";
    private const string DealerPortalPaymentMethodName = "Dealer Portal Payment Method";

    private readonly USAePayOptions _opts = opts.Value;
    private readonly HttpClient _http = http;
    private readonly ILogger<USAePayService> _logger = logger;

    // Charge via pay.js payment_key

    /// <inheritdoc/>
    public async Task<ChargeResponse> ChargeTokenAsync(
        string paymentKey, decimal amount, string? description = null)
    {
        // pay.js payment_key is a top-level field, NOT creditcard.number.
        // Passing it inside creditcard.number causes "Card Number was not between
        // 13 and 16 digits" because the gateway validates it as a PAN.
        object body = new
        {
            command = "cc:sale",
            amount = amount.ToString("F2"),
            description = description ?? PortalChargeDescription,
            payment_key = paymentKey
        };

        return await PostTransactionAsync(body);
    }

    // Save customer + payment method to vault

    /// <inheritdoc/>
    public async Task<SaveCustomerResponse> SaveCustomerAsync(
        string paymentKey, string firstName, string lastName, string email,
        decimal chargeAmount, string? phone = null)
    {
        string fullName = $"{firstName} {lastName}";

        try
        {
            // 1. Create customer record (no payment method yet).
            object custBody = new
            {
                first_name = firstName,
                last_name = lastName,
                email,
                phone = phone ?? string.Empty
            };

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("POST customers request: {Body}", JsonSerializer.Serialize(custBody));
            }

            using HttpResponseMessage custResp = await SendJsonAsync(HttpMethod.Post, "customers", custBody);
            string custJson = await custResp.Content.ReadAsStringAsync();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("POST customers [{Status}]: {Json}", (int)custResp.StatusCode, custJson);
            }

            USAePayCustomerResponse cust = JsonSerializer.Deserialize<USAePayCustomerResponse>(custJson)!;

            if (!string.IsNullOrEmpty(cust.Error))
            {
                return new SaveCustomerResponse(false, null, null, null, $"[step:customers][{cust.ErrorCode}] {cust.Error}");
            }

            string? customerKey = cust.CustomerKey;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Extracted customerKey: {Key}", customerKey);
            }

            if (string.IsNullOrEmpty(customerKey))
            {
                return new SaveCustomerResponse(false, null, null, null, $"[step:customers] No customer key in response: {custJson}");
            }

            // 2. Run cc:sale with the single-use payment_key, linked to the new customer.
            object txBody = new
            {
                command = "cc:sale",
                amount = chargeAmount.ToString("F2"),
                description = InitialChargeDescription,
                payment_key = paymentKey,
                custkey = customerKey
            };

            ChargeResponse chargeResult = await PostTransactionAsync(txBody);

            if (!chargeResult.Success)
            {
                return new SaveCustomerResponse(false, customerKey, null, fullName,
                    $"[step:transaction] {chargeResult.Error}", chargeResult);
            }

            // 3. Attach the transaction as a stored payment method on the customer.
            object pmBody = new[]
            {
                new
                {
                    method_name = DealerPortalPaymentMethodName,
                    pay_type = "transaction",
                    transaction_key = chargeResult.TransactionKey
                }
            };

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("POST payment_methods request: {Body}", JsonSerializer.Serialize(pmBody));
            }

            using HttpResponseMessage pmResp = await SendJsonAsync(HttpMethod.Post, $"customers/{customerKey}/payment_methods", pmBody);
            string pmJson = await pmResp.Content.ReadAsStringAsync();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("POST payment_methods [{Status}]: {Json}", (int)pmResp.StatusCode, pmJson);
            }

            List<USAePayPaymentMethodResponse>? pm = JsonSerializer.Deserialize<List<USAePayPaymentMethodResponse>>(pmJson);

            if (pm == null || pm.Count == 0)
            {
                return new SaveCustomerResponse(false, customerKey, null, fullName,
                    "[step:payment_methods] Empty response", chargeResult);
            }

            USAePayPaymentMethodResponse method = pm[0];

            if (!string.IsNullOrEmpty(method.Error))
            {
                return new SaveCustomerResponse(false, customerKey, null, fullName,
                    $"[step:payment_methods][{method.ErrorCode}] {method.Error}", chargeResult);
            }

            return new SaveCustomerResponse(true, customerKey, method.Key, fullName, null, chargeResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving customer");
            return new SaveCustomerResponse(false, null, null, null, ex.Message);
        }
    }

    // Charge saved customer (recurring)

    /// <inheritdoc/>
    public async Task<RecurringPaymentRecord> ChargeCustomerAsync(
        string customerKey, string paymentMethodKey, decimal amount)
    {
        object body = new
        {
            command = "customer:sale",
            amount = amount.ToString("F2"),
            description = RecurringChargeDescription,
            custkey = customerKey,
            paymethod_key = paymentMethodKey
        };

        using HttpResponseMessage resp = await SendJsonAsync(HttpMethod.Post, "transactions", body);
        string json = await resp.Content.ReadAsStringAsync();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Customer transaction [{Status}]: {Json}", (int)resp.StatusCode, json);
        }

        USAePayTransactionResponse tx = JsonSerializer.Deserialize<USAePayTransactionResponse>(json)!;
        bool approved = (tx.ResultCode == "A") || (tx.Result == "Approved");

        return new RecurringPaymentRecord(
            Timestamp: DateTime.UtcNow,
            Amount: amount,
            Success: approved,
            TransactionKey: tx.Key,
            Error: approved ? null : (!string.IsNullOrEmpty(tx.Error) ? $"[{tx.ErrorCode}] {tx.Error}" : tx.Result));
    }

    // Shared transaction helper

    /// <summary>
    /// Posts a transaction body to <c>/transactions</c> and maps the result to a
    /// <see cref="ChargeResponse"/>.
    /// </summary>
    private async Task<ChargeResponse> PostTransactionAsync(object body)
    {
        try
        {
            using HttpResponseMessage resp = await SendJsonAsync(HttpMethod.Post, "transactions", body);
            string json = await resp.Content.ReadAsStringAsync();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Transaction response [{Status}]: {Json}", (int)resp.StatusCode, json);
            }

            USAePayTransactionResponse tx = JsonSerializer.Deserialize<USAePayTransactionResponse>(json)!;

            // USAePay returns result_code "A" for Approved.
            bool approved = (tx.ResultCode == "A") || (tx.Result == "Approved");

            if (!approved)
            {
                string errMsg = !string.IsNullOrEmpty(tx.Error)
                    ? $"[{tx.ErrorCode}] {tx.Error}"
                    : $"Transaction declined: {tx.Result}";

                return new ChargeResponse(false, tx.Key, null, tx.Result, errMsg);
            }

            return new ChargeResponse(true, tx.Key, tx.AuthCode, tx.Result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting transaction");
            return new ChargeResponse(false, null, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Sends a JSON request with a fresh per-request <c>Authorization</c> header so that
    /// mutating <c>DefaultRequestHeaders</c> (which is not thread-safe) is avoided.
    /// </summary>
    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string relPath, object body)
    {
        using HttpRequestMessage request = new(method, $"{_opts.BaseUrl}/{relPath}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };

        request.Headers.Authorization = BuildAuthHeader();

        return await _http.SendAsync(request);
    }

    /// <summary>
    /// Builds a fresh <c>Basic</c> authorization header with a per-request HMAC seed.
    /// </summary>
    private AuthenticationHeaderValue BuildAuthHeader()
    {
        string seed = NewSeed();
        string hash = BuildHash(seed);
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.ApiKey}:{hash}"));

        return new AuthenticationHeaderValue("Basic", encoded);
    }

    /// <summary>
    /// Builds the HMAC-SHA256 hash string required by USAePay:
    /// "s2/{seed}/{sha256(apikey + seed + pin)}".
    /// </summary>
    private string BuildHash(string seed)
    {
        string raw = $"{_opts.ApiKey}{seed}{_opts.Pin}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        string hex = Convert.ToHexString(bytes).ToLowerInvariant();

        return $"s2/{seed}/{hex}";
    }

    /// <summary>Generates a unique seed combining a Unix timestamp and a short GUID fragment.</summary>
    private static string NewSeed() =>
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "_" + Guid.NewGuid().ToString("N")[..8];
}
