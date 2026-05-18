namespace SongRequestForMobile.Models;

public sealed class AppSettings
{
    public string ServerBaseUrl { get; set; } = "https://localhost";
    public string BearerToken { get; set; } = string.Empty;
    public string? LastYoutubeLoginUrl { get; set; }
    public bool AutopilotMode { get; set; } = false;
}
