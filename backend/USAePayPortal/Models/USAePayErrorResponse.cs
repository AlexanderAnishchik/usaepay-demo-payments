using System.Text.Json.Serialization;

namespace USAePayPortal.Models;

/// <summary>
/// USAePay v2 error response envelope.
/// </summary>
internal class USAePayErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("errorcode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}
