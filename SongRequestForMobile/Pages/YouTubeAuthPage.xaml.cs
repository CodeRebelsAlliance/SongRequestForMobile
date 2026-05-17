using SongRequestForMobile.Models;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Pages;

public sealed class YouTubeAuthPage : ContentPage
{
    private readonly AppState _appState;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IYouTubeCookieProvider _cookieProvider;
    private readonly YouTubeSession _youtubeSession;
    private readonly IYouTubeCookieStore _cookieStore;
    private readonly WebView _authWebView;
    private readonly Label _statusLabel;
    private string? _currentUrl;

    public YouTubeAuthPage(AppState appState, IAppSettingsStore settingsStore, IYouTubeCookieProvider cookieProvider, YouTubeSession youtubeSession, IYouTubeCookieStore cookieStore)
    {
        _appState = appState;
        _settingsStore = settingsStore;
        _cookieProvider = cookieProvider;
        _youtubeSession = youtubeSession;
        _cookieStore = cookieStore;

        _statusLabel = new Label
        {
            Text = "Ready",
            TextColor = Colors.Gray
        };

        _authWebView = new WebView
        {
            HeightRequest = 500,
            Source = string.IsNullOrWhiteSpace(_appState.Settings.LastYoutubeLoginUrl)
                ? "https://www.youtube.com/"
                : _appState.Settings.LastYoutubeLoginUrl
        };
        _authWebView.Navigated += OnWebViewNavigated;

        Content = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "Sign in to YouTube, then tap Capture Cookies.",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold
                },
                _statusLabel,
                _authWebView,
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Button { Text = "Capture Cookies", Command = new Command(async () => await OnCaptureCookiesClicked()) },
                        new Button { Text = "Load YouTube", Command = new Command(OnLoadYouTubeClicked) },
                        new Button { Text = "Close", Command = new Command(async () => await OnCloseClicked()) }
                    }
                }
            }
        };
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _currentUrl = e.Url;
        _statusLabel.Text = $"Current page: {e.Url}";
    }

    private void OnLoadYouTubeClicked()
    {
        _authWebView.Source = "https://www.youtube.com/";
        _statusLabel.Text = "Loading YouTube...";
    }

    private async Task OnCaptureCookiesClicked()
    {
        try
        {
            _statusLabel.Text = "Capturing cookies...";
            var cookies = await _cookieProvider.CaptureCookiesAsync(_currentUrl ?? "https://www.youtube.com/", _ => Task.FromResult(true));

            if (cookies.Count == 0)
            {
                _statusLabel.Text = "No cookies captured. Make sure you are signed in.";
                return;
            }

            _youtubeSession.SetCookies(cookies);
            _appState.YoutubeCookies = cookies;
            _appState.Settings.LastYoutubeLoginUrl = _currentUrl;
            await _cookieStore.SaveAsync(cookies);
            await _settingsStore.SaveAsync(_appState.Settings);
            _statusLabel.Text = $"Saved {cookies.Count} cookie(s). Closing...";
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Cookie capture failed: {ex.Message}";
        }
    }

    private async Task OnCloseClicked()
    {
        await Navigation.PopModalAsync();
    }
}
