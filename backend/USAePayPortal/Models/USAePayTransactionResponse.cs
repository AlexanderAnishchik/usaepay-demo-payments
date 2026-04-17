using System.Text.Json.Serialization;

namespace USAePayPortal.Models;

// USAePay REST API response shapes (deserialized via System.Text.Json)

/// <summary>
/// USAePay v2 transaction response envelope.
/// </summary>
internal class USAePayTransactionResponse
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("result_code")]
    public string? ResultCode { get; set; }

    [JsonPropertyName("authcode")]
    public string? AuthCode { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("errorcode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("creditcard")]
    public CreditCardInfo? CreditCard { get; set; }
}
