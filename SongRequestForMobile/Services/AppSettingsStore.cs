using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}

public sealed class AppSettingsStore : IAppSettingsStore
{
    private const string ServerBaseUrlKey = "server_base_url";
    private const string YoutubeLoginUrlKey = "youtube_login_url";
    private const string BearerTokenKey = "bearer_token";
    private const string AutopilotModeKey = "autopilot_mode";

    public async Task<AppSettings> LoadAsync()
    {
        var settings = new AppSettings
        {
            ServerBaseUrl = Preferences.Default.Get(ServerBaseUrlKey, "https://localhost"),
            LastYoutubeLoginUrl = Preferences.Default.Get(YoutubeLoginUrlKey, string.Empty),
            AutopilotMode = Preferences.Default.Get(AutopilotModeKey, false)
        };

        settings.BearerToken = await SecureStorage.Default.GetAsync(BearerTokenKey).ConfigureAwait(false) ?? string.Empty;
        return settings;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Preferences.Default.Set(ServerBaseUrlKey, settings.ServerBaseUrl ?? string.Empty);
        Preferences.Default.Set(YoutubeLoginUrlKey, settings.LastYoutubeLoginUrl ?? string.Empty);
        Preferences.Default.Set(AutopilotModeKey, settings.AutopilotMode);

        if (string.IsNullOrWhiteSpace(settings.BearerToken))
        {
            SecureStorage.Default.Remove(BearerTokenKey);
        }
        else
        {
            await SecureStorage.Default.SetAsync(BearerTokenKey, settings.BearerToken).ConfigureAwait(false);
        }
    }
}
