using Microsoft.Extensions.DependencyInjection;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly AppState _appState;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IYouTubeCookieStore _cookieStore;
    private readonly YouTubeSession _youtubeSession;
    private readonly ServerApiClient _serverApiClient;
    private readonly Label _cookieStatusLabel;
    private readonly Label _statusLabel;
    private readonly Entry _serverBaseUrlEntry;
    private readonly Entry _bearerTokenEntry;

    public SettingsPage(AppState appState, IAppSettingsStore settingsStore, IYouTubeCookieStore cookieStore, YouTubeSession youtubeSession, ServerApiClient serverApiClient)
    {
        _appState = appState;
        _settingsStore = settingsStore;
        _cookieStore = cookieStore;
        _youtubeSession = youtubeSession;
        _serverApiClient = serverApiClient;

        _cookieStatusLabel = new Label
        {
            Text = "No YouTube cookies loaded yet.",
            LineBreakMode = LineBreakMode.WordWrap
        };

        _statusLabel = new Label
        {
            Text = "Ready.",
            LineBreakMode = LineBreakMode.WordWrap
        };

        _serverBaseUrlEntry = new Entry
        {
            Placeholder = "https://your-server.example"
        };

        _bearerTokenEntry = new Entry
        {
            Placeholder = "Bearer token",
            IsPassword = true
        };

        Title = "Settings";
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Settings",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Frame
                    {
                        Padding = 12,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                new Label { Text = "Server settings", FontAttributes = FontAttributes.Bold },
                                _serverBaseUrlEntry,
                                _bearerTokenEntry,
                                new HorizontalStackLayout
                                {
                                    Spacing = 10,
                                    Children =
                                    {
                                        new Button { Text = "Save settings", Command = new Command(SaveSettings) },
                                        new Button { Text = "Test server", Command = new Command(TestServer) }
                                    }
                                }
                            }
                        }
                    },
                    new Frame
                    {
                        Padding = 12,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                new Label { Text = "YouTube authentication", FontAttributes = FontAttributes.Bold },
                                _cookieStatusLabel,
                                new Button { Text = "Sign in / capture cookies", Command = new Command(OpenYoutubeAuth) }
                            }
                        }
                    },
                    new Frame
                    {
                        Padding = 12,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                new Label { Text = "Status", FontAttributes = FontAttributes.Bold },
                                _statusLabel
                            }
                        }
                    }
                }
            }
        };

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _appState.Settings = await _settingsStore.LoadAsync();
        _appState.YoutubeCookies = await _cookieStore.LoadAsync();
        _youtubeSession.SetCookies(_appState.YoutubeCookies);

        _serverBaseUrlEntry.Text = _appState.Settings.ServerBaseUrl;
        _bearerTokenEntry.Text = _appState.Settings.BearerToken;
        UpdateCookieStatus();
        _statusLabel.Text = "Settings loaded.";
    }

    private async void SaveSettings()
    {
        _appState.Settings.ServerBaseUrl = _serverBaseUrlEntry.Text?.Trim() ?? string.Empty;
        _appState.Settings.BearerToken = _bearerTokenEntry.Text?.Trim() ?? string.Empty;
        await _settingsStore.SaveAsync(_appState.Settings);
        _statusLabel.Text = "Settings saved.";
    }

    private async void TestServer()
    {
        try
        {
            _serverApiClient.Configure(_serverBaseUrlEntry.Text, _bearerTokenEntry.Text);
            _statusLabel.Text = "Testing server...";
            var result = await _serverApiClient.PingAsync();
            _statusLabel.Text = result;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Server test failed: {ex.Message}";
        }
    }

    private async void OpenYoutubeAuth()
    {
        if (Application.Current?.MainPage?.Handler?.MauiContext?.Services == null)
        {
            _statusLabel.Text = "Navigation is not ready yet.";
            return;
        }

        var authPage = Application.Current.MainPage.Handler.MauiContext.Services.GetRequiredService<YouTubeAuthPage>();
        await Navigation.PushModalAsync(new NavigationPage(authPage));
        _appState.YoutubeCookies = await _cookieStore.LoadAsync();
        UpdateCookieStatus();
    }

    private void UpdateCookieStatus()
    {
        _cookieStatusLabel.Text = _appState.YoutubeCookies.Count == 0
            ? "No YouTube cookies loaded yet."
            : $"Loaded {_appState.YoutubeCookies.Count} cookie(s) from device storage.";
    }
}
