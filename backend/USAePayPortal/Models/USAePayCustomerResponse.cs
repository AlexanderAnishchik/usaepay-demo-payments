using System.Text.Json.Serialization;

namespace USAePayPortal.Models;

/// <summary>
/// USAePay v2 customer response envelope.
/// </summary>
internal class USAePayCustomerResponse
{
    /// <summary>USAePay v2 may return the customer key as "custkey" or "key".</summary>
    [JsonPropertyName("custkey")]
    public string? CustKey { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>Resolves the customer key regardless of which field USAePay populated.</summary>
    public string? CustomerKey => CustKey ?? Key;

    [JsonPropertyName("firstname")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastname")]
    public string? LastName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("errorcode")]
    public string? ErrorCode { get; set; }
}
