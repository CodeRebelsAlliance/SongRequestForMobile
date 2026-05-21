using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Plugin.Maui.Audio;
using SongRequestForMobile.Models;
using SongRequestForMobile.Resources;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Pages;

public sealed class PlayerPage : ContentPage
{
    private readonly IPlayerQueueService _queueService;
    private readonly IThumbnailColorService _thumbnailColorService;
    private readonly ILyricsDisplayService _lyricsDisplayService;
    private readonly ObservableCollection<PlayerQueueItem> _queueItems = new();

    // Main UI elements
    private readonly Grid _mainGrid;
    private readonly AbsoluteLayout _backgroundContainer;
    private readonly BoxView _blackBackground;
    private readonly Ellipse _accentCircle1;
    private readonly Ellipse _accentCircle2;
    private readonly Label _titleLabel;
    private readonly Label _channelLabel;
    private readonly Label _statusLabel;
    private readonly Image _thumbnailImage;
    private readonly Image _nextThumbnailImage;
    private readonly Border _heroBorder;
    private readonly Slider _progressSlider;
    private readonly Label _currentTimeLabel;
    private readonly Label _durationLabel;
    private readonly Button _playPauseButton;
    private readonly Button _shuffleButton;
    private readonly Button _queueButton;
    private readonly Button _lyricsButton;

    // Queue panel (sliding from bottom)
    private readonly Grid _queuePanel;
    private readonly CollectionView _queueCollectionView;
    private readonly ScrollView _queueScrollView;
    private bool _queuePanelOpen = false;

    // Lyrics panel (sliding from bottom)
    private readonly Grid _lyricsPanel;
    private readonly CollectionView _lyricsCollectionView;
    private readonly Grid _lyricsContentContainer;
    private readonly StackLayout _lyricsLoadingContainer;
    private bool _lyricsPanelOpen = false;

    private readonly IDispatcherTimer _timer;
    private readonly IDispatcherTimer _animationTimer;
    private bool _isActive;
    private bool _updatingSlider;
    private Color _accentColor1 = Colors.SteelBlue;
    private Color _accentColor2 = Colors.Navy;
    private double _accentRotation1 = 0;
    private double _accentRotation2 = 0;
    private PlayerQueueItem? _previousItem = null;

    public PlayerPage(IPlayerQueueService queueService, IThumbnailColorService thumbnailColorService, ILyricsDisplayService lyricsDisplayService)
    {
        _queueService = queueService;
        _thumbnailColorService = thumbnailColorService;
        _lyricsDisplayService = lyricsDisplayService;
        _queueService.Updated += OnQueueUpdated;
        _lyricsDisplayService.LyricsUpdated += OnLyricsUpdated;
        _lyricsDisplayService.LoadingStateChanged += OnLyricsLoadingStateChanged;
        _lyricsDisplayService.CurrentLineChanged += OnCurrentLyricsLineChanged;

        // Get theme colors
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var fg = isDark ? Colors.White : Colors.Black;
        var secondaryText = isDark ? Colors.Gray : Colors.Gray;

        // === BUILD BACKGROUND WITH ANIMATED ACCENT CIRCLES ===
        _blackBackground = new BoxView
        {
            BackgroundColor = Colors.Black
        };

        // Animated accent circles
        _accentCircle1 = new Ellipse
        {
            Fill = new SolidColorBrush(_accentColor1),
            Opacity = 0.15,
            WidthRequest = 300,
            HeightRequest = 300
        };

        _accentCircle2 = new Ellipse
        {
            Fill = new SolidColorBrush(_accentColor2),
            Opacity = 0.12,
            WidthRequest = 250,
            HeightRequest = 250
        };

        _backgroundContainer = new AbsoluteLayout
        {
            BackgroundColor = Colors.Black
        };
        _backgroundContainer.Add(_blackBackground);
        AbsoluteLayout.SetLayoutBounds(_blackBackground, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(_blackBackground, AbsoluteLayoutFlags.All);

        _backgroundContainer.Add(_accentCircle1);
        AbsoluteLayout.SetLayoutBounds(_accentCircle1, new Rect(0.5, 0.5, 0.6, 0.6));
        AbsoluteLayout.SetLayoutFlags(_accentCircle1, AbsoluteLayoutFlags.PositionProportional);

        _backgroundContainer.Add(_accentCircle2);
        AbsoluteLayout.SetLayoutBounds(_accentCircle2, new Rect(0.3, 0.3, 0.5, 0.5));
        AbsoluteLayout.SetLayoutFlags(_accentCircle2, AbsoluteLayoutFlags.PositionProportional);

        // === BUILD THUMBNAIL WITH TRANSITIONS ===
        _thumbnailImage = new Image
        {
            HeightRequest = 280,
            WidthRequest = 280,
            Aspect = Aspect.AspectFill
        };

        _nextThumbnailImage = new Image
        {
            HeightRequest = 280,
            WidthRequest = 280,
            Aspect = Aspect.AspectFill,
            Opacity = 0,
            IsVisible = false
        };

        _heroBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            StrokeThickness = 0,
            Padding = 0,
            Content = new Grid
            {
                Children =
                {
                    _thumbnailImage,
                    _nextThumbnailImage
                }
            },
            BackgroundColor = Colors.Transparent,
            Shadow = new Shadow
            {
                Brush = Colors.Black,
                Offset = new Point(0, 10),
                Opacity = 0.5f,
                Radius = 20
            }
        };

        // === BUILD PLAYER CONTROLS ===
        _titleLabel = new Label
        {
            Text = "Nothing is playing yet.",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = fg,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _channelLabel = new Label
        {
            Text = "Queue a song from Requests to get started.",
            FontSize = 13,
            TextColor = fg,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _statusLabel = new Label
        {
            Text = "Ready.",
            FontSize = 12,
            TextColor = fg,
            FontAttributes = FontAttributes.Bold
        };

        _progressSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            ThumbColor = Colors.White,
            MinimumTrackColor = Colors.White,
            MaximumTrackColor = secondaryText
        };
        _progressSlider.ValueChanged += OnProgressChanged;

        _currentTimeLabel = new Label
        {
            Text = "0:00",
            FontSize = 11,
            TextColor = fg
        };

        _durationLabel = new Label
        {
            Text = "0:00",
            FontSize = 11,
            TextColor = fg,
            HorizontalTextAlignment = TextAlignment.End
        };

        var progressGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8
        };
        progressGrid.Add(_currentTimeLabel);
        progressGrid.Add(_durationLabel);
        Grid.SetColumn(_durationLabel, 1);

        var progressSection = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                _progressSlider,
                progressGrid
            }
        };

        // Transport controls
        _playPauseButton = new Button
        {
            Text = MaterialIcons.PlayArrow,
            FontFamily = "MaterialIcons",
            FontSize = 36,
            WidthRequest = 80,
            HeightRequest = 80,
            CornerRadius = 40,
            BackgroundColor = Color.FromArgb("#2E2E2E"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            Command = new Command(async () => await OnPlayPauseClicked())
        };

        var previousButton = new Button
        {
            Text = MaterialIcons.SkipPrevious,
            FontFamily = "MaterialIcons",
            FontSize = 28,
            WidthRequest = 60,
            HeightRequest = 60,
            CornerRadius = 30,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.LightGray,
            BorderWidth = 2,
            BorderColor = Colors.LightGray,
            Command = new Command(async () => await _queueService.PlayPreviousAsync())
        };

        var nextButton = new Button
        {
            Text = MaterialIcons.SkipNext,
            FontFamily = "MaterialIcons",
            FontSize = 28,
            WidthRequest = 60,
            HeightRequest = 60,
            CornerRadius = 30,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.LightGray,
            BorderWidth = 2,
            BorderColor = Colors.LightGray,
            Command = new Command(async () => await _queueService.SkipNextAsync())
        };

        _shuffleButton = new Button
        {
            Text = MaterialIcons.Shuffle,
            FontFamily = "MaterialIcons",
            FontSize = 22,
            WidthRequest = 50,
            HeightRequest = 50,
            CornerRadius = 25,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.LightGray,
            BorderWidth = 2,
            BorderColor = Colors.LightGray,
            Command = new Command(OnShuffleClicked)
        };

        _queueButton = new Button
        {
            Text = MaterialIcons.QueueMusic,
            FontFamily = "MaterialIcons",
            FontSize = 24,
            WidthRequest = 50,
            HeightRequest = 50,
            CornerRadius = 25,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            BorderWidth = 2,
            BorderColor = Colors.White,
            Command = new Command(OnQueueButtonClicked)
        };

        _lyricsButton = new Button
        {
            Text = MaterialIcons.Lyrics,
            FontFamily = "MaterialIcons",
            FontSize = 24,
            WidthRequest = 50,
            HeightRequest = 50,
            CornerRadius = 25,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            BorderWidth = 2,
            BorderColor = Colors.White,
            Command = new Command(OnLyricsButtonClicked)
        };

        var transportControls = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                previousButton,
                _playPauseButton,
                nextButton
            }
        };

        var secondaryControls = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                _shuffleButton,
                _lyricsButton,
                _queueButton
            }
        };

        // === BUILD MAIN CONTENT GRID ===
        _mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 24,
            Padding = new Thickness(20, 40, 20, 30),
            BackgroundColor = Colors.Transparent
        };

        _mainGrid.Add(_heroBorder);
        _mainGrid.Add(_titleLabel);
        Grid.SetRow(_titleLabel, 1);
        _mainGrid.Add(_channelLabel);
        Grid.SetRow(_channelLabel, 2);
        _mainGrid.Add(progressSection);
        Grid.SetRow(progressSection, 3);
        _mainGrid.Add(transportControls);
        Grid.SetRow(transportControls, 4);
        _mainGrid.Add(secondaryControls);
        Grid.SetRow(secondaryControls, 5);

        var mainContent = new ScrollView
        {
            Content = _mainGrid
        };

        // === BUILD QUEUE PANEL (SLIDING FROM BOTTOM) ===
        _queueCollectionView = new CollectionView
        {
            ItemsSource = _queueItems,
            SelectionMode = SelectionMode.None,
            EmptyView = new Label
            {
                Text = "Your queue is empty.",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Colors.Gray
            }
        };
        _queueCollectionView.ItemTemplate = CreateQueueTemplate();

        var queueHandleBar = new BoxView
        {
            WidthRequest = 40,
            HeightRequest = 4,
            CornerRadius = 2,
            BackgroundColor = Colors.Gray,
            Margin = new Thickness(0, 8, 0, 8),
            HorizontalOptions = LayoutOptions.Center
        };

        var queueHeader = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(16, 0, 16, 0),
            Children =
            {
                queueHandleBar,
                new Label
                {
                    Text = "Queue",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    Margin = new Thickness(0, 8, 0, 8)
                }
            }
        };

        _queueScrollView = new ScrollView
        {
            Content = _queueCollectionView
        };

        _queuePanel = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#1E1E1E"),
            Children =
            {
                queueHeader,
                _queueScrollView
            }
        };
        Grid.SetRow(_queueScrollView, 1);

        // Add gesture recognizer to queue header for dragging
        var queueDragGesture = new SwipeGestureRecognizer { Direction = SwipeDirection.Down };
        queueDragGesture.Swiped += async (s, e) => await CloseQueuePanelAsync();
        _queuePanel.GestureRecognizers.Add(queueDragGesture);

        // === BUILD LYRICS PANEL (SLIDING FROM BOTTOM) ===
        _lyricsCollectionView = new CollectionView
        {
            ItemsSource = new ObservableCollection<LyricsDisplayItem>(),
            SelectionMode = SelectionMode.None,
            EmptyView = new Label
            {
                Text = "Could not find any lyrics for this song",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Colors.Gray,
                FontSize = 14,
                Padding = 20
            }
        };
        _lyricsCollectionView.ItemTemplate = CreateLyricsTemplate();

        _lyricsLoadingContainer = new StackLayout
        {
            Spacing = 10,
            Padding = 20,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                new ActivityIndicator
                {
                    IsRunning = true,
                    IsVisible = true,
                    Color = Colors.White
                },
                new Label
                {
                    Text = "Loading lyrics...",
                    HorizontalOptions = LayoutOptions.Center,
                    TextColor = Colors.Gray
                }
            }
        };

        var lyricsHandleBar = new BoxView
        {
            WidthRequest = 40,
            HeightRequest = 4,
            CornerRadius = 2,
            BackgroundColor = Colors.Gray,
            Margin = new Thickness(0, 8, 0, 8),
            HorizontalOptions = LayoutOptions.Center
        };

        var lyricsHeader = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(16, 0, 16, 0),
            Children =
            {
                lyricsHandleBar,
                new Label
                {
                    Text = "Lyrics",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    Margin = new Thickness(0, 8, 0, 8)
                }
            }
        };

        _lyricsContentContainer = new Grid
        {
            Children =
            {
                _lyricsCollectionView,
                _lyricsLoadingContainer
            }
        };
        _lyricsLoadingContainer.IsVisible = false;

        _lyricsPanel = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#1E1E1E"),
            Padding = 0,
            ColumnSpacing = 0,
            RowSpacing = 0,
            Children =
            {
                lyricsHeader,
                _lyricsContentContainer
            }
        };
        Grid.SetRow(_lyricsContentContainer, 1);

        // Add gesture recognizer to lyrics header for dragging
        var lyricsDragGesture = new SwipeGestureRecognizer { Direction = SwipeDirection.Down };
        lyricsDragGesture.Swiped += async (s, e) => await CloseLyricsPanelAsync();
        _lyricsPanel.GestureRecognizers.Add(lyricsDragGesture);

        // === LAYOUT EVERYTHING ===
        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        rootGrid.Add(_backgroundContainer);
        rootGrid.Add(mainContent);

        // Queue panel overlay - sits at bottom and slides up
        var queueOverlay = new BoxView
        {
            BackgroundColor = new Color(0, 0, 0, 0.5f),
            Opacity = 0
        };

        var mainOverlay = new Grid();
        mainOverlay.Add(rootGrid);
        mainOverlay.Add(queueOverlay);
        mainOverlay.Add(_queuePanel);
        mainOverlay.Add(_lyricsPanel);

        // Initialize queue and lyrics panels off-screen at bottom
        var screenHeight = DeviceDisplay.Current.MainDisplayInfo.Height / DeviceDisplay.Current.MainDisplayInfo.Density;
        _queuePanel.TranslationY = screenHeight;
        _lyricsPanel.TranslationY = screenHeight;

        Content = mainOverlay;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += async (_, _) => await TickAsync();

        _animationTimer = Dispatcher.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(50);
        _animationTimer.Tick += (_, _) => AnimateAccentCircles();

        RefreshUi();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isActive = true;
        _timer.Start();
        _animationTimer.Start();
        await TickAsync();
        RefreshUi();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isActive = false;
        _timer.Stop();
        _animationTimer.Stop();
    }

    private void AnimateAccentCircles()
    {
        // Slowly rotate the accent circles
        _accentRotation1 += 0.3;
        _accentRotation2 -= 0.2;

        _accentCircle1.Rotation = _accentRotation1 % 360;
        _accentCircle2.Rotation = _accentRotation2 % 360;
    }

    private async void OnShuffleClicked()
    {
        if (_queueItems.Count > 1)
        {
            var random = new Random();
            var items = _queueItems.ToList();
            for (int i = items.Count - 1; i > 0; i--)
            {
                int randomIndex = random.Next(i + 1);
                var temp = items[i];
                items[i] = items[randomIndex];
                items[randomIndex] = temp;
            }

            _queueItems.Clear();
            foreach (var item in items)
            {
                _queueItems.Add(item);
            }

            await _queueService.ReplaceQueueAsync(items);

            await _shuffleButton.ScaleTo(1.2, 100);
            await _shuffleButton.ScaleTo(1.0, 100);
        }
    }

    private async void OnQueueButtonClicked()
    {
        if (_queuePanelOpen)
        {
            await CloseQueuePanelAsync();
        }
        else
        {
            await OpenQueuePanelAsync();
        }
    }

    private async Task OpenQueuePanelAsync()
    {
        _queuePanelOpen = true;
        var mainOverlay = Content as Grid;
        if (mainOverlay != null && mainOverlay.Children.Count > 2)
        {
            var queueOverlay = mainOverlay.Children[1] as BoxView;
            if (queueOverlay != null)
            {
                await queueOverlay.FadeTo(1, 300, Easing.CubicOut);
            }
        }

        var screenHeight = DeviceDisplay.Current.MainDisplayInfo.Height / DeviceDisplay.Current.MainDisplayInfo.Density;
        _queuePanel.TranslationY = screenHeight;
        await _queuePanel.TranslateTo(0, 0, 400, Easing.CubicOut);
        await _queueButton.RotateTo(180, 300);
    }

    private async Task CloseQueuePanelAsync()
    {
        _queuePanelOpen = false;
        var mainOverlay = Content as Grid;
        if (mainOverlay != null && mainOverlay.Children.Count > 2)
        {
            var queueOverlay = mainOverlay.Children[1] as BoxView;
            if (queueOverlay != null)
            {
                await queueOverlay.FadeTo(0, 300, Easing.CubicIn);
            }
        }

        var screenHeight = DeviceDisplay.Current.MainDisplayInfo.Height / DeviceDisplay.Current.MainDisplayInfo.Density;
        await _queuePanel.TranslateTo(0, screenHeight, 400, Easing.CubicIn);
        await _queueButton.RotateTo(0, 300);
    }

    private async void OnLyricsButtonClicked()
    {
        if (_lyricsPanelOpen)
        {
            await CloseLyricsPanelAsync();
        }
        else
        {
            await OpenLyricsPanelAsync();
        }
    }

    private async Task OpenLyricsPanelAsync()
    {
        _lyricsPanelOpen = true;
        var screenHeight = DeviceDisplay.Current.MainDisplayInfo.Height / DeviceDisplay.Current.MainDisplayInfo.Density;
        _lyricsPanel.TranslationY = screenHeight;

        // Ensure lyrics display is refreshed when opening the panel
        RefreshLyricsDisplay();

        await _lyricsPanel.TranslateTo(0, 0, 400, Easing.CubicOut);
        await _lyricsButton.RotateTo(180, 300);
    }

    private async Task CloseLyricsPanelAsync()
    {
        _lyricsPanelOpen = false;
        var screenHeight = DeviceDisplay.Current.MainDisplayInfo.Height / DeviceDisplay.Current.MainDisplayInfo.Density;
        await _lyricsPanel.TranslateTo(0, screenHeight, 400, Easing.CubicIn);
        await _lyricsButton.RotateTo(0, 300);
    }

    private async Task OnPlayPauseClicked()
    {
        await _queueService.TogglePlayPauseAsync();
        RefreshUi();
    }

    private async Task TickAsync()
    {
        if (!_isActive)
            return;

        await _queueService.TickAsync();
        MainThread.BeginInvokeOnMainThread(RefreshUi);
    }

    private void OnQueueUpdated(object? sender, EventArgs e)
    {
        if (!_isActive)
            return;

        MainThread.BeginInvokeOnMainThread(RefreshUi);
    }

    private void RefreshUi()
    {
        var current = _queueService.CurrentItem;
        var isPlaying = _queueService.IsPlaying && !_queueService.IsPaused;

        _playPauseButton.Text = isPlaying ? MaterialIcons.Pause : MaterialIcons.PlayArrow;

        // Handle queue items
        _queueItems.Clear();
        foreach (var item in _queueService.Queue)
        {
            _queueItems.Add(item);
        }

        if (current == null)
        {
            _thumbnailImage.Source = null;
            _titleLabel.Text = "Nothing is playing yet.";
            _channelLabel.Text = "Queue a song from Requests to get started.";
            _statusLabel.Text = "Ready.";
            _currentTimeLabel.Text = "0:00";
            _durationLabel.Text = "0:00";
            _progressSlider.Maximum = 1;
            _progressSlider.Value = 0;
            UpdateAccentCircles(Colors.SteelBlue, Colors.Navy);
            _lyricsDisplayService.ClearCache();
        }
        else
        {
            // Fetch lyrics for current song
            FetchLyricsForCurrentSongAsync();

            // Animate thumbnail transition if song changed
            if (_previousItem?.VideoId != current.VideoId)
            {
                AnimateThumbnailTransition(current);
                _previousItem = current;
            }
            else
            {
                // Just update the image
                if (string.IsNullOrWhiteSpace(current.Thumbnail))
                    _thumbnailImage.Source = null;
                else
                    _thumbnailImage.Source = ImageSource.FromUri(new Uri(current.Thumbnail));
            }

            _titleLabel.Text = current.Title;
            _channelLabel.Text = current.Channel;
            _statusLabel.Text = isPlaying ? "Playing" : _queueService.IsPaused ? "Paused" : "Ready";

            var duration = _queueService.Duration;
            var position = _queueService.CurrentPosition;
            _currentTimeLabel.Text = FormatTime(position);
            _durationLabel.Text = FormatTime(duration);

            _updatingSlider = true;
            _progressSlider.Maximum = 1;
            _progressSlider.Value = duration.TotalSeconds > 0 ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1) : 0;
            _updatingSlider = false;

            // Update accent circles
            var accentColor = _thumbnailColorService.GetAccentColor(current.AccentKey ?? current.Thumbnail ?? current.VideoId);
            var complementary = GetComplementaryColor(accentColor);
            UpdateAccentCircles(accentColor, complementary);

            // Update lyrics display
            _lyricsDisplayService.UpdatePlaybackPosition(position);
            RefreshLyricsDisplay();
        }
    }

    private async void FetchLyricsForCurrentSongAsync()
    {
        var current = _queueService.CurrentItem;
        if (current == null) return;

        // Fetch lyrics for current song
        await _lyricsDisplayService.FetchLyricsAsync(current);

        // Prefetch next song if available
        var queue = _queueService.Queue;
        var currentIndex = queue.IndexOf(current);
        if (currentIndex >= 0 && currentIndex + 1 < queue.Count)
        {
            var nextItem = queue[currentIndex + 1];
            _ = _lyricsDisplayService.PrefetchNextLyricsAsync(nextItem);
        }
    }

    private void RefreshLyricsDisplay()
    {
        var lyricsCollection = _lyricsCollectionView.ItemsSource as ObservableCollection<LyricsDisplayItem>;
        if (lyricsCollection == null)
        {
            lyricsCollection = new ObservableCollection<LyricsDisplayItem>();
            _lyricsCollectionView.ItemsSource = lyricsCollection;
        }

        // Show loading indicator if still loading
        if (_lyricsDisplayService.IsLoading)
        {
            _lyricsLoadingContainer.IsVisible = true;
            _lyricsCollectionView.IsVisible = false;
            return;
        }

        _lyricsLoadingContainer.IsVisible = false;
        _lyricsCollectionView.IsVisible = true;

        var lyrics = _lyricsDisplayService.CurrentLyrics;
        var syncedLines = _lyricsDisplayService.CurrentSyncedLines;
        var currentLineIdx = _lyricsDisplayService.CurrentLineIndex;

        lyricsCollection.Clear();

        if (lyrics == null || !lyrics.Found || syncedLines.Count == 0)
        {
            // Show empty state - will use EmptyView from CollectionView
            return;
        }

        // Populate lyrics with indices
        for (int i = 0; i < syncedLines.Count; i++)
        {
            var (time, text) = syncedLines[i];
            lyricsCollection.Add(new LyricsDisplayItem { Index = i, Time = time, Text = text });
        }

        // Scroll to current line and highlight it
        if (currentLineIdx >= 0 && currentLineIdx < syncedLines.Count)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _lyricsCollectionView.ScrollTo(currentLineIdx, -1, ScrollToPosition.Center, true);

                // Update styling for all items
                for (int i = 0; i < lyricsCollection.Count; i++)
                {
                    var item = lyricsCollection[i];
                    if (i == currentLineIdx)
                    {
                        // Highlight current line
                        // Note: We'll update cell styling via a custom approach
                    }
                }
            });
        }
    }

    private void OnLyricsUpdated(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshLyricsDisplay();
        });
    }

    private void OnLyricsLoadingStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshLyricsDisplay();
        });
    }

    private void OnCurrentLyricsLineChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshLyricsDisplay();
        });
    }

    private async void AnimateThumbnailTransition(PlayerQueueItem current)
    {
        // Set next thumbnail
        if (string.IsNullOrWhiteSpace(current.Thumbnail))
            _nextThumbnailImage.Source = null;
        else
            _nextThumbnailImage.Source = ImageSource.FromUri(new Uri(current.Thumbnail));

        var screenWidth = DeviceDisplay.Current.MainDisplayInfo.Width / DeviceDisplay.Current.MainDisplayInfo.Density;

        _nextThumbnailImage.IsVisible = true;
        _nextThumbnailImage.TranslationX = screenWidth;
        _nextThumbnailImage.Opacity = 1;

        // Animate current out to left, next in from right
        await Task.WhenAll(
            _thumbnailImage.TranslateTo(-screenWidth, 0, 400, Easing.CubicIn),
            _nextThumbnailImage.TranslateTo(0, 0, 400, Easing.CubicOut)
        );

        // Swap images
        _thumbnailImage.Source = _nextThumbnailImage.Source;
        _thumbnailImage.TranslationX = 0;
        _nextThumbnailImage.IsVisible = false;
        _nextThumbnailImage.TranslationX = 0;
    }

    private void UpdateAccentCircles(Color color1, Color color2)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            const int steps = 20;
            const int duration = 400;
            const int stepDuration = duration / steps;

            var startColor1 = _accentColor1;
            var startColor2 = _accentColor2;

            for (int i = 0; i <= steps; i++)
            {
                var progress = i / (double)steps;
                var c1 = BlendColors(startColor1, color1, progress);
                var c2 = BlendColors(startColor2, color2, progress);

                _accentColor1 = c1;
                _accentColor2 = c2;

                _accentCircle1.Fill = new SolidColorBrush(c1);
                _accentCircle2.Fill = new SolidColorBrush(c2);

                if (i < steps)
                    await Task.Delay(stepDuration);
            }
        });
    }

    private static Color BlendColors(Color color1, Color color2, double factor)
    {
        return Color.FromRgba(
            (float)((color1.Red * (1 - factor)) + (color2.Red * factor)),
            (float)((color1.Green * (1 - factor)) + (color2.Green * factor)),
            (float)((color1.Blue * (1 - factor)) + (color2.Blue * factor)),
            1.0f
        );
    }

    private Color GetComplementaryColor(Color baseColor)
    {
        var r = (int)(baseColor.Red * 255);
        var g = (int)(baseColor.Green * 255);
        var b = (int)(baseColor.Blue * 255);

        var factor = 0.6;
        return Color.FromRgba((int)(r * factor), (int)(g * factor), (int)(b * factor), 255);
    }

    private async void OnProgressChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_updatingSlider)
            return;

        var duration = _queueService.Duration;
        if (duration <= TimeSpan.Zero)
            return;

        var position = TimeSpan.FromSeconds(duration.TotalSeconds * Math.Clamp(e.NewValue, 0, 1));
        await _queueService.SeekAsync(position);
    }

    private DataTemplate CreateLyricsTemplate()
    {
        return new DataTemplate(() =>
        {
            var lyricsContainer = new StackLayout
            {
                Padding = new Thickness(16, 12),
                Spacing = 0
            };

            var lyricsLabel = new Label
            {
                LineBreakMode = LineBreakMode.WordWrap,
                FontSize = 16,
                TextColor = Colors.LightGray,
                FontAttributes = FontAttributes.Bold
            };
            lyricsLabel.SetBinding(Label.TextProperty, "Text");

            // Binding context for styling
            lyricsLabel.BindingContextChanged += (s, e) =>
            {
                if (lyricsLabel.BindingContext is LyricsDisplayItem lyricsItem)
                {
                    var isCurrentLine = lyricsItem.Index == _lyricsDisplayService.CurrentLineIndex;
                    lyricsLabel.TextColor = isCurrentLine ? Colors.White : Colors.LightGray;
                    lyricsLabel.FontSize = isCurrentLine ? 20 : 16;
                }
            };

            lyricsContainer.Children.Add(lyricsLabel);
            return lyricsContainer;
        });
    }

    private DataTemplate CreateQueueTemplate()
    {
        return new DataTemplate(() =>
        {
            var thumbnail = new Image
            {
                WidthRequest = 52,
                HeightRequest = 52,
                Aspect = Aspect.AspectFill
            };
            thumbnail.SetBinding(Image.SourceProperty, nameof(PlayerQueueItem.Thumbnail));

            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            title.SetBinding(Label.TextProperty, nameof(PlayerQueueItem.Title));

            var channel = new Label
            {
                FontSize = 11,
                TextColor = Colors.Gray,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            channel.SetBinding(Label.TextProperty, nameof(PlayerQueueItem.Channel));

            var info = new VerticalStackLayout
            {
                Spacing = 2,
                Children = { title, channel }
            };

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = 52 },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10,
                Children =
                {
                    thumbnail,
                    info
                }
            };
            Grid.SetColumn(info, 1);

            var card = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                StrokeThickness = 1,
                Padding = 10,
                Margin = new Thickness(10, 6, 10, 6),
                Content = contentGrid,
                BackgroundColor = Color.FromArgb("#2A2A2A")
            };

            // Tap for quick actions
            var tapGesture = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
            tapGesture.Tapped += (s, e) =>
            {
                if (card.BindingContext is PlayerQueueItem item)
                {
                    OnQueueItemTapped(item);
                }
            };
            card.GestureRecognizers.Add(tapGesture);

            // Pan gesture for drag detection
            var panGesture = new PanGestureRecognizer();
            var dragStartX = 0.0;
            var dragStartY = 0.0;

            panGesture.PanUpdated += (s, e) =>
            {
                if (e.StatusType == GestureStatus.Started)
                {
                    dragStartX = e.TotalX;
                    dragStartY = e.TotalY;
                }
                else if (e.StatusType == GestureStatus.Running)
                {
                    // Vertical drag for reordering
                    var deltaY = e.TotalY - dragStartY;

                    if (Math.Abs(deltaY) > 30 && card.BindingContext is PlayerQueueItem item)
                    {
                        if (deltaY < 0)
                        {
                            // Dragged up - move down in queue
                            _queueService.MoveDown(item);
                        }
                        else if (deltaY > 0)
                        {
                            // Dragged down - move up in queue
                            _queueService.MoveUp(item);
                        }
                        dragStartY = e.TotalY;
                    }
                }
            };
            card.GestureRecognizers.Add(panGesture);

            // Update card appearance
            card.BindingContextChanged += (_, _) =>
            {
                if (card.BindingContext is PlayerQueueItem item)
                {
                    var accent = _thumbnailColorService.GetAccentColor(item.AccentKey ?? item.Thumbnail ?? item.VideoId);
                    card.Stroke = accent;
                }
            };

            return card;
        });
    }

    private async void OnQueueItemTapped(PlayerQueueItem item)
    {
        var action = await DisplayActionSheet(
            $"{item.Title}",
            "Cancel",
            null,
            "Move Up",
            "Move Down",
            "Play Now",
            "Remove"
        );

        switch (action)
        {
            case "Move Up":
                _queueService.MoveUp(item);
                break;
            case "Move Down":
                _queueService.MoveDown(item);
                break;
            case "Play Now":
                await _queueService.PlayNowAsync(item);
                RefreshUi();
                break;
            case "Remove":
                _queueService.Remove(item);
                break;
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            return "0:00";

        var hours = (int)time.TotalHours;
        var minutes = time.Minutes;
        var seconds = time.Seconds;

        if (hours > 0)
            return $"{hours}:{minutes:D2}:{seconds:D2}";

        return $"{minutes}:{seconds:D2}";
    }
}

/// <summary>
/// Represents a single line of lyrics for display in the collection view.
/// </summary>
internal class LyricsDisplayItem
{
    public int Index { get; set; }
    public TimeSpan Time { get; set; }
    public string Text { get; set; } = string.Empty;
}
