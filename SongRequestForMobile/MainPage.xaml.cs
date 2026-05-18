using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using SongRequestForMobile.Models;
using SongRequestForMobile.Resources;
using SongRequestForMobile.Services;

namespace SongRequestForMobile;

public partial class MainPage : ContentPage
{
    private readonly ObservableCollection<MusicSearchResult> _searchResults = new();
    private readonly Entry _searchQueryEntry;
    private readonly Label _searchStatusLabel;
    private readonly Entry _youtubeLinkEntry;
    private readonly Label _selectedSongLabel;
    private readonly CollectionView _searchResultsCollectionView;
    private readonly Editor _messageEntry;
    private readonly Label _statusLabel;
    private AppState? _appState;
    private IAppSettingsStore? _settingsStore;
    private ServerApiClient? _serverApiClient;
    private MusicSearchResult? _selectedResult;

    public MainPage()
    {
        _searchQueryEntry = new Entry
        {
            Placeholder = "Search title or artist"
        };
        _searchQueryEntry.Completed += OnSearchCompleted;

        _searchStatusLabel = new Label
        {
            Text = "Search the server for matching songs.",
            LineBreakMode = LineBreakMode.WordWrap
        };

        _youtubeLinkEntry = new Entry
        {
            Placeholder = "https://www.youtube.com/watch?v=...",
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };

        _selectedSongLabel = new Label
        {
            Text = "No song selected.",
            LineBreakMode = LineBreakMode.WordWrap
        };

        _searchResultsCollectionView = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            EmptyView = "No search results yet."
        };
        _searchResultsCollectionView.SelectionChanged += OnSearchResultSelectionChanged;
        _searchResultsCollectionView.ItemTemplate = new DataTemplate(() =>
        {
            var image = new Image
            {
                HeightRequest = 64,
                WidthRequest = 84,
                Aspect = Aspect.AspectFill
            };
            image.SetBinding(Image.SourceProperty, nameof(MusicSearchResult.Thumbnail));

            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            title.SetBinding(Label.TextProperty, nameof(MusicSearchResult.Title));

            var channel = new Label
            {
                FontSize = 12,
                TextColor = Colors.Gray,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            channel.SetBinding(Label.TextProperty, nameof(MusicSearchResult.Channel));

            var videoId = new Label
            {
                FontSize = 12,
                TextColor = Colors.Gray
            };
            videoId.SetBinding(Label.TextProperty, nameof(MusicSearchResult.VideoId));

            return new Border
            {
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = 12
                },
                StrokeThickness = 1,
                Padding = 10,
                Margin = new Thickness(0, 0, 0, 10),
                Content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition { Width = 84 },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    ColumnSpacing = 10,
                    Children =
                    {
                        image,
                        new VerticalStackLayout
                        {
                            Spacing = 4,
                            Children = { title, channel, videoId }
                        }.WithGridColumn(1)
                    }
                }
            };
        });

        _messageEntry = new Editor
        {
            Placeholder = "Optional message",
            AutoSize = EditorAutoSizeOption.TextChanges,
            HeightRequest = 100
        };

        _statusLabel = new Label
        {
            Text = "Ready.",
            LineBreakMode = LineBreakMode.WordWrap
        };

        Content = BuildContent();
        Loaded += OnLoaded;
        _searchResultsCollectionView.ItemsSource = _searchResults;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EnsureServicesAsync();
    }

    private View BuildContent()
    {
        _searchResultsCollectionView.HeightRequest = 340;
        _searchResultsCollectionView.VerticalOptions = LayoutOptions.Fill;

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "Add a song",
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
                                new Label { Text = "Search music", FontAttributes = FontAttributes.Bold },
                                _searchQueryEntry,
                                new Button
                                {
                                    Text = $"{MaterialIcons.Search} Search",
                                    FontFamily = "OpenSansRegular",
                                    Command = new Command(async () => await SearchAsync())
                                },
                                _searchStatusLabel
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
                                new Label { Text = "Selected song", FontAttributes = FontAttributes.Bold },
                                _youtubeLinkEntry,
                                _selectedSongLabel
                            }
                        }
                    },
                    new Label
                    {
                        Text = "Search results",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold
                    },
                    _searchResultsCollectionView,
                    new Frame
                    {
                        Padding = 12,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                new Label { Text = "Message to server", FontAttributes = FontAttributes.Bold },
                                _messageEntry
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
                                new Label { Text = "Submit", FontAttributes = FontAttributes.Bold },
                                new Button
                                {
                                    Text = $"{MaterialIcons.Send} Send request",
                                    FontFamily = "OpenSansRegular",
                                    Command = new Command(async () => await OnSendClicked())
                                },
                                _statusLabel
                            }
                        }
                    }
                }
            }
        };
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await EnsureServicesAsync();
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        await SearchAsync();
    }

    private async Task EnsureServicesAsync()
    {
        if (_appState != null)
        {
            return;
        }

        var services = Handler?.MauiContext?.Services;
        if (services == null)
        {
            return;
        }

        _appState = services.GetRequiredService<AppState>();
        _settingsStore = services.GetRequiredService<IAppSettingsStore>();
        _serverApiClient = services.GetRequiredService<ServerApiClient>();

        _appState.Settings = await _settingsStore.LoadAsync();
        _appState.YoutubeCookies = await services.GetRequiredService<IYouTubeCookieStore>().LoadAsync();
        _searchStatusLabel.Text = "Search the server for matching songs.";
    }

    private async Task SearchAsync()
    {
        await EnsureServicesAsync();
        if (_appState == null || _serverApiClient == null)
        {
            _searchStatusLabel.Text = "Services are not ready yet.";
            return;
        }

        var query = _searchQueryEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            _searchStatusLabel.Text = "Enter a search query first.";
            return;
        }

        try
        {
            _serverApiClient.Configure(_appState.Settings.ServerBaseUrl, _appState.Settings.BearerToken);
            _searchStatusLabel.Text = "Searching...";

            var results = await _serverApiClient.SearchMusicAsync(query);
            _searchResults.Clear();
            foreach (var result in results)
            {
                _searchResults.Add(result);
            }

            if (_searchResults.Count == 0)
            {
                _selectedResult = null;
                _selectedSongLabel.Text = "No matching songs found.";
                _youtubeLinkEntry.Text = string.Empty;
                _searchStatusLabel.Text = "No results found.";
                return;
            }

            _searchResultsCollectionView.SelectedItem = _searchResults[0];
            _searchStatusLabel.Text = $"Found {_searchResults.Count} result(s).";
        }
        catch (Exception ex)
        {
            _searchStatusLabel.Text = $"Search failed: {ex.Message}";
        }
    }

    private void OnSearchResultSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedResult = e.CurrentSelection.FirstOrDefault() as MusicSearchResult;
        if (_selectedResult == null)
        {
            _selectedSongLabel.Text = "No song selected.";
            return;
        }

        _selectedSongLabel.Text = $"Selected: {_selectedResult.Title} — {_selectedResult.Channel}";
        _youtubeLinkEntry.Text = $"https://www.youtube.com/watch?v={_selectedResult.VideoId}";
    }

    private async Task OnSendClicked()
    {
        await EnsureServicesAsync();
        if (_appState == null || _serverApiClient == null)
        {
            _statusLabel.Text = "Services are not ready yet.";
            return;
        }

        var ytlink = _youtubeLinkEntry.Text?.Trim();
        var message = _messageEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(ytlink))
        {
            _statusLabel.Text = "Select a song from the search results or enter a YouTube link/ID first.";
            return;
        }

        try
        {
            _serverApiClient.Configure(_appState.Settings.ServerBaseUrl, _appState.Settings.BearerToken);
            _statusLabel.Text = "Sending song request...";

            var result = await _serverApiClient.SendSongAsync(ytlink, message);
            _statusLabel.Text = string.IsNullOrWhiteSpace(result.Message)
                ? "Song request sent."
                : result.Message;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Send failed: {ex.Message}";
        }
    }
}

static class ViewExtensions
{
    public static T WithGridRow<T>(this T view, int row) where T : View
    {
        Grid.SetRow(view, row);
        return view;
    }

    public static T WithGridColumn<T>(this T view, int column) where T : View
    {
        Grid.SetColumn(view, column);
        return view;
    }
}
