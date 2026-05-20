using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using SongRequestForMobile.Resources;
using SongRequestForMobile.Services;
#if ANDROID
using Android.OS;
using Android.Content;
#endif

namespace SongRequestForMobile.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly AppState _appState;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IYouTubeCookieStore _cookieStore;
    private readonly YouTubeSession _youtubeSession;
    private readonly ServerApiClient _serverApiClient;
    private readonly ISettingsExportService _exportService;
    private readonly Label _cookieStatusLabel;
    private readonly Label _statusLabel;
    private readonly Entry _serverBaseUrlEntry;
    private readonly Entry _bearerTokenEntry;
    private readonly Switch _autopilotToggle;

    public SettingsPage(AppState appState, IAppSettingsStore settingsStore, IYouTubeCookieStore cookieStore, YouTubeSession youtubeSession, ServerApiClient serverApiClient, ISettingsExportService exportService)
    {
        _appState = appState;
        _settingsStore = settingsStore;
        _cookieStore = cookieStore;
        _youtubeSession = youtubeSession;
        _serverApiClient = serverApiClient;
        _exportService = exportService;

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

        _autopilotToggle = new Switch
        {
            ThumbColor = Colors.White
        };

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
                        BackgroundColor = Application.Current?.Resources.MergedDictionaries.FirstOrDefault()?.ContainsKey("FrameBackground") == true 
                            ? (Color)Application.Current.Resources["FrameBackground"]
                            : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E1E1E") : Colors.White),
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
                                        new Button
                                        {
                                            Text = "Save",
                                            FontFamily = "OpenSansRegular",
                                            Command = new Command(SaveSettings)
                                        },
                                        new Button
                                        {
                                            Text = "Test",
                                            FontFamily = "OpenSansRegular",
                                            Command = new Command(TestServer)
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new Frame
                    {
                        Padding = 12,
                        BackgroundColor = Application.Current?.Resources.MergedDictionaries.FirstOrDefault()?.ContainsKey("FrameBackground") == true 
                            ? (Color)Application.Current.Resources["FrameBackground"]
                            : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E1E1E") : Colors.White),
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                new Label { Text = "YouTube authentication", FontAttributes = FontAttributes.Bold },
                                _cookieStatusLabel,
                                new Button
                                {
                                    Text = "Sign in / capture cookies",
                                    FontFamily = "OpenSansRegular",
                                    Command = new Command(OpenYoutubeAuth)
                                }
                            }
                        }
                    },
                    // Autopilot Mode section with gradient background
                    CreateAutopilotSection(),
                    // Import/Export section
                    CreateImportExportSection(),
                    new Frame
                    {
                        Padding = 12,
                        BackgroundColor = Application.Current?.Resources.MergedDictionaries.FirstOrDefault()?.ContainsKey("FrameBackground") == true 
                            ? (Color)Application.Current.Resources["FrameBackground"]
                            : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E1E1E") : Colors.White),
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

    private Frame CreateAutopilotSection()
    {
        var gradientBox = new BoxView
        {
            BackgroundColor = Colors.Transparent
        };

        var toggleLabel = new Label
        {
            Text = "Autopilot Mode",
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };

        var descriptionLabel = new Label
        {
            Text = "Automatically queue new songs as they arrive from the server.",
            FontSize = 12,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.Gray,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var contentGrid = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        toggleLabel,
                        _autopilotToggle
                    }
                },
                descriptionLabel
            }
        };

        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 2,
            Padding = 12,
            Content = contentGrid,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E1E1E") : Colors.White
        };

        // Bind border color to toggle state
        _autopilotToggle.Toggled += (s, e) =>
        {
            border.Stroke = e.Value
                ? new SolidColorBrush(CreateGradientColor())
                : Colors.LightGray;
        };

        var frame = new Frame
        {
            Padding = 0,
            Content = border
        };

        return frame;
    }

    private Frame CreateImportExportSection()
    {
        return new Frame
        {
            Padding = 12,
            BackgroundColor = Application.Current?.Resources.MergedDictionaries.FirstOrDefault()?.ContainsKey("FrameBackground") == true 
                ? (Color)Application.Current.Resources["FrameBackground"]
                : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E1E1E") : Colors.White),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Deployment & Sync", FontAttributes = FontAttributes.Bold },
                    new Label
                    {
                        Text = "Export all settings, cookies, and authentication to a file for easy deployment to multiple machines. Import settings from a previously exported file.",
                        FontSize = 12,
                        TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.Gray,
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Button
                            {
                                Text = "Export",
                                FontFamily = "OpenSansRegular",
                                BackgroundColor = Colors.Green,
                                TextColor = Colors.White,
                                Command = new Command(ExportSettings),
                                HorizontalOptions = LayoutOptions.FillAndExpand
                            },
                            new Button
                            {
                                Text = "Import",
                                FontFamily = "OpenSansRegular",
                                BackgroundColor = Colors.Blue,
                                TextColor = Colors.White,
                                Command = new Command(ImportSettings),
                                HorizontalOptions = LayoutOptions.FillAndExpand
                            }
                        }
                    }
                }
            }
        };
    }

    private Color CreateGradientColor()
    {
        // Create a multicolor gradient-like color (mixing vibrant colors)
        return Color.FromRgb(0.8f, 0.2f, 0.8f); // Vibrant purple/magenta
    }

    private async Task LoadAsync()
    {
        _appState.Settings = await _settingsStore.LoadAsync();
        _appState.YoutubeCookies = await _cookieStore.LoadAsync();
        _youtubeSession.SetCookies(_appState.YoutubeCookies);

        _serverBaseUrlEntry.Text = _appState.Settings.ServerBaseUrl;
        _bearerTokenEntry.Text = _appState.Settings.BearerToken;
        _autopilotToggle.IsToggled = _appState.Settings.AutopilotMode;
        UpdateCookieStatus();
        _statusLabel.Text = "Settings loaded.";

        // Update border color on load
        var border = ((Frame)((VerticalStackLayout)((ScrollView)Content).Content).Children[3]).Content as Border;
        if (border != null)
        {
            border.Stroke = _appState.Settings.AutopilotMode
                ? new SolidColorBrush(CreateGradientColor())
                : Colors.LightGray;
        }
    }

    private async void SaveSettings()
    {
        _appState.Settings.ServerBaseUrl = _serverBaseUrlEntry.Text?.Trim() ?? string.Empty;
        _appState.Settings.BearerToken = _bearerTokenEntry.Text?.Trim() ?? string.Empty;
        _appState.Settings.AutopilotMode = _autopilotToggle.IsToggled;
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

    private async void ExportSettings()
    {
        try
        {
            _statusLabel.Text = "Preparing export...";

            // Ensure we have the latest settings
            _appState.Settings.ServerBaseUrl = _serverBaseUrlEntry.Text?.Trim() ?? string.Empty;
            _appState.Settings.BearerToken = _bearerTokenEntry.Text?.Trim() ?? string.Empty;
            _appState.Settings.AutopilotMode = _autopilotToggle.IsToggled;
            _appState.YoutubeCookies = await _cookieStore.LoadAsync();

            // Export to JSON
            var json = await _exportService.ExportAsync(_appState.Settings, _appState.YoutubeCookies);

#if ANDROID
            // For Android, save to a user-accessible location using the Downloads folder
            await ExportSettingsAndroid(json);
#else
            // For other platforms, save to Documents folder
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var filePath = System.IO.Path.Combine(documentsPath, "SongRequest_Export.json");

            Directory.CreateDirectory(documentsPath);
            await File.WriteAllTextAsync(filePath, json);
            _statusLabel.Text = $"✓ Settings exported to:\n{filePath}";
#endif
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Export failed: {ex.Message}";
        }
    }

#if ANDROID
    private async Task ExportSettingsAndroid(string json)
    {
        try
        {
            // Get the Downloads directory which is user-accessible
            var context = Android.App.Application.Context;
            var downloadsDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);

            if (downloadsDir == null || !downloadsDir.CanWrite())
            {
                // Fallback to app-specific external files directory
                downloadsDir = context.GetExternalFilesDir(Android.OS.Environment.DirectoryDownloads);
            }

            if (downloadsDir == null)
            {
                // Last resort: use cache directory
                downloadsDir = context.CacheDir;
            }

            if (downloadsDir == null)
            {
                _statusLabel.Text = "Error: Cannot access storage";
                return;
            }

            if (!downloadsDir.Exists())
            {
                downloadsDir.Mkdirs();
            }

            var filePath = System.IO.Path.Combine(downloadsDir.AbsolutePath, "SongRequest_Export.json");

            await File.WriteAllTextAsync(filePath, json);

            _statusLabel.Text = $"✓ Settings exported to:\n{filePath}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Export failed: {ex.Message}";
        }
    }
#endif

    private async void ImportSettings()
    {
        try
        {
            _statusLabel.Text = "Select export file...";

            // Use file picker to select export file
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Settings Export File"
            });

            if (result == null)
            {
                _statusLabel.Text = "Import cancelled.";
                return;
            }

            var json = await File.ReadAllTextAsync(result.FullPath);
            var (settings, cookies) = await _exportService.ImportAsync(json);

            // Apply imported settings
            _appState.Settings = settings;

            // Save settings
            await _settingsStore.SaveAsync(settings);

            // Save cookies if any exist
            if (cookies.Count > 0)
            {
                await _cookieStore.SaveAsync(cookies.Select(kvp => 
                    new System.Net.Cookie(kvp.Key, kvp.Value) { Domain = ".youtube.com" }).ToList());
            }

            // Update UI
            _serverBaseUrlEntry.Text = settings.ServerBaseUrl;
            _bearerTokenEntry.Text = settings.BearerToken;
            _autopilotToggle.IsToggled = settings.AutopilotMode;
            _appState.YoutubeCookies = await _cookieStore.LoadAsync();
            UpdateCookieStatus();

            _statusLabel.Text = "Settings imported successfully!";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Import failed: {ex.Message}";
        }
    }

    private void UpdateCookieStatus()
    {
        _cookieStatusLabel.Text = _appState.YoutubeCookies.Count == 0
            ? "No YouTube cookies loaded yet."
            : $"Loaded {_appState.YoutubeCookies.Count} cookie(s) from device storage.";
    }
}
