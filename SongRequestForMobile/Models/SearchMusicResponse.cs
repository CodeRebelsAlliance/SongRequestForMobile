using System.Text.Json.Serialization;

namespace SongRequestForMobile.Models;

public sealed class SearchMusicResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("results")]
    public List<MusicSearchResult>? Results { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
