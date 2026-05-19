using System.Text.Json;
using System.Text.Json.Serialization;

namespace SongRequestForMobile.Services;

/// <summary>
/// Service for checking GitHub releases for app updates
/// </summary>
public interface IUpdateCheckService
{
    Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}

public class UpdateCheckService : IUpdateCheckService
{
    private readonly HttpClient _httpClient;
    private const string GitHubApiUrl = "https://api.github.com/repos/CodeRebelsAlliance/SongRequestForMobile/releases/latest";

    public UpdateCheckService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var currentVersion = Microsoft.Maui.ApplicationModel.AppInfo.VersionString;

            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.Add("Accept", "application/vnd.github.v3+json");
            request.Headers.Add("User-Agent", "SongRequest For Mobile");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            if (release == null)
                return null;

            var latestVersion = NormalizeVersion(release.TagName);
            var appVersion = NormalizeVersion(currentVersion);

            // Check if update is available
            if (IsNewerVersion(latestVersion, appVersion))
            {
                var apkAsset = release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));

                return new UpdateInfo
                {
                    LatestVersion = latestVersion,
                    CurrentVersion = appVersion,
                    ReleaseNotes = release.Body ?? string.Empty,
                    DownloadUrl = apkAsset?.BrowserDownloadUrl ?? string.Empty,
                    ReleaseName = release.Name ?? release.TagName ?? string.Empty
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Normalizes version string by removing 'v' prefix if present
    /// </summary>
    private static string NormalizeVersion(string version)
    {
        return version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? version[1..]
            : version;
    }

    /// <summary>
    /// Compares versions to determine if a newer version is available
    /// </summary>
    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (!Version.TryParse(latestVersion, out var latest) || !Version.TryParse(currentVersion, out var current))
        {
            return false;
        }

        return latest > current;
    }
}

public class UpdateInfo
{
    public string LatestVersion { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
}

// GitHub API Models
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;
}
