using System.Text.Json;
using System.Text.Json.Serialization;
using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public interface ISettingsExportService
{
    Task<string> ExportAsync(AppSettings settings, IReadOnlyList<System.Net.Cookie> youtubeCookies);
    Task<(AppSettings settings, Dictionary<string, string> cookies)> ImportAsync(string jsonData);
}

public sealed class SettingsExportData
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("exportedAt")]
    public DateTime ExportedAt { get; set; }

    [JsonPropertyName("serverBaseUrl")]
    public string ServerBaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("bearerToken")]
    public string BearerToken { get; set; } = string.Empty;

    [JsonPropertyName("autopilotMode")]
    public bool AutopilotMode { get; set; }

    [JsonPropertyName("lastYoutubeLoginUrl")]
    public string? LastYoutubeLoginUrl { get; set; }

    [JsonPropertyName("youtubeCookies")]
    public Dictionary<string, string> YoutubeCookies { get; set; } = new();
}

public sealed class SettingsExportService : ISettingsExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    public async Task<string> ExportAsync(AppSettings settings, IReadOnlyList<System.Net.Cookie> youtubeCookies)
    {
        // Convert cookies to dictionary (name -> value)
        var cookieDict = new Dictionary<string, string>();
        foreach (var cookie in youtubeCookies)
        {
            cookieDict[cookie.Name] = cookie.Value;
        }

        var exportData = new SettingsExportData
        {
            Version = 1,
            ExportedAt = DateTime.UtcNow,
            ServerBaseUrl = settings.ServerBaseUrl ?? string.Empty,
            BearerToken = settings.BearerToken ?? string.Empty,
            AutopilotMode = settings.AutopilotMode,
            LastYoutubeLoginUrl = settings.LastYoutubeLoginUrl,
            YoutubeCookies = cookieDict
        };

        var json = JsonSerializer.Serialize(exportData, JsonOptions);
        return await Task.FromResult(json);
    }

    public async Task<(AppSettings settings, Dictionary<string, string> cookies)> ImportAsync(string jsonData)
    {
        var exportData = JsonSerializer.Deserialize<SettingsExportData>(jsonData, JsonOptions);
        if (exportData == null)
        {
            throw new InvalidOperationException("Invalid export data format.");
        }

        if (exportData.Version != 1)
        {
            throw new InvalidOperationException($"Unsupported export version: {exportData.Version}");
        }

        var settings = new AppSettings
        {
            ServerBaseUrl = exportData.ServerBaseUrl ?? "https://localhost",
            BearerToken = exportData.BearerToken ?? string.Empty,
            LastYoutubeLoginUrl = exportData.LastYoutubeLoginUrl,
            AutopilotMode = exportData.AutopilotMode
        };

        var cookies = new Dictionary<string, string>(exportData.YoutubeCookies ?? new());

        return await Task.FromResult((settings, cookies));
    }
}
