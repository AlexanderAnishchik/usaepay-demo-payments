using System.Text.Json.Serialization;

namespace USAePayPortal.Models;

/// <summary>
/// USAePay v2 payment method response envelope.
/// </summary>
internal class USAePayPaymentMethodResponse
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("method_name")]
    public string? MethodName { get; set; }

    [JsonPropertyName("ccnum4last")]
    public string? CcNum4Last { get; set; }

    [JsonPropertyName("card_type")]
    public string? CardType { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("errorcode")]
    public string? ErrorCode { get; set; }
}
