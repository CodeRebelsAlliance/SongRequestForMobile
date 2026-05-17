using System.Text.Json.Serialization;

namespace SongRequestForMobile.Models;

public sealed class SendInResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }
}
