using System.Text.Json.Serialization;

namespace USAePayPortal.Models;

/// <summary>
/// Masked credit-card details returned inside a transaction response.
/// </summary>
internal class CreditCardInfo
{
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("expiration")]
    public string? Expiration { get; set; }
}
