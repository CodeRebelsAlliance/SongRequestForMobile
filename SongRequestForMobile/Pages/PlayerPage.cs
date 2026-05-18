using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.Audio;
using SongRequestForMobile.Models;
using SongRequestForMobile.Resources;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Pages;

public sealed class PlayerPage : ContentPage
{
    private readonly IPlayerQueueService _queueService;
    private readonly IThumbnailColorService _thumbnailColorService;
    private readonly ObservableCollection<PlayerQueueItem> _queueItems = new();

    // Main UI elements
    private readonly Grid _mainGrid;
    private readonly BoxView _gradientBackgroundBox;
    private readonly Label _titleLabel;
    private readonly Label _channelLabel;
    private readonly Label _statusLabel;
    private readonly Image _thumbnailImage;
    private readonly Border _heroBorder;
    private readonly Slider _progressSlider;
    private readonly Label _currentTimeLabel;
    private readonly Label _durationLabel;
    private readonly Button _playPauseButton;
    private readonly Button _shuffleButton;
    private readonly Button _queueButton;

    // Queue popout
    private readonly Frame _queuePopout;
    private readonly CollectionView _queueCollectionView;
    private double _originalQueueOpacity = 0;

    private readonly IDispatcherTimer _timer;
    private bool _isActive;
    private bool _updatingSlider;
    private Color _gradientColor1 = Colors.SteelBlue;
    private Color _gradientColor2 = Colors.Navy;

    public PlayerPage(IPlayerQueueService queueService, IThumbnailColorService thumbnailColorService)
    {
        _queueService = queueService;
        _thumbnailColorService = thumbnailColorService;
        _queueService.Updated += OnQueueUpdated;

        // Get theme colors
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var res = Application.Current?.Resources;
        var fg = isDark ? (res != null && res.ContainsKey("White") ? (Color)res["White"] : Colors.White) : (res != null && res.ContainsKey("Black") ? (Color)res["Black"] : Colors.Black);
        var secondaryText = isDark ? (res != null && res.ContainsKey("Gray200") ? (Color)res["Gray200"] : Colors.Gray) : (res != null && res.ContainsKey("Gray500") ? (Color)res["Gray500"] : Colors.Gray);

        // Build gradient background
        _gradientBackgroundBox = new BoxView
        {
            BackgroundColor = _gradientColor1
        };

        // Thumbnail
        _thumbnailImage = new Image
        {
            HeightRequest = 280,
            WidthRequest = 280,
            Aspect = Aspect.AspectFill
        };

        _heroBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            StrokeThickness = 0,
            Padding = 0,
            Content = _thumbnailImage,
            BackgroundColor = Colors.Transparent,
            Shadow = new Shadow
            {
                Brush = Colors.Black,
                Offset = new Point(0, 10),
                Opacity = 0.3f,
                Radius = 15
            }
        };

        // Title and channel
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

        // Progress slider
        var primary = res != null && res.ContainsKey("Primary") ? (Color)res["Primary"] : Colors.SteelBlue;
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
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
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
            TextColor = Colors.White,
            BorderWidth = 2,
            BorderColor = Colors.White,
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
            TextColor = Colors.White,
            BorderWidth = 2,
            BorderColor = Colors.White,
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
            TextColor = Colors.White,
            BorderWidth = 2,
            BorderColor = Colors.White,
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
                _queueButton
            }
        };

        // Build queue popout
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

        var queueHeader = new HorizontalStackLayout
        {
            Padding = 16,
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "Queue",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    VerticalOptions = LayoutOptions.Center
                },
                new Label
                {
                    Text = "0 items",
                    FontSize = 12,
                    TextColor = Colors.Gray,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.FillAndExpand
                },
                new Button
                {
                    Text = MaterialIcons.Close,
                    FontFamily = "MaterialIcons",
                    FontSize = 20,
                    WidthRequest = 36,
                    HeightRequest = 36,
                    CornerRadius = 18,
                    Padding = 0,
                    BackgroundColor = Colors.Transparent,
                    TextColor = Colors.Black,
                    Command = new Command(OnCloseQueuePopout)
                }
            }
        };

        _queuePopout = new Frame
        {
            CornerRadius = 24,
            HasShadow = true,
            Padding = 0,
            Margin = 0,
            BorderColor = Colors.Transparent,
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    queueHeader,
                    new BoxView { HeightRequest = 1, Color = Colors.LightGray },
                    _queueCollectionView
                }
            }
        };
        _queuePopout.IsVisible = false;
        _queuePopout.Opacity = 0;

        // Main content grid
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

        // Create scrollable main content
        var mainContent = new ScrollView
        {
            Content = _mainGrid
        };

        // Overlay grid with background + content + popout
        var overlayGrid = new Grid();
        overlayGrid.Add(_gradientBackgroundBox);
        overlayGrid.Add(mainContent);
        overlayGrid.Add(_queuePopout);

        Title = "Player";
        Content = overlayGrid;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += async (_, _) => await TickAsync();

        RefreshUi();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isActive = true;
        _timer.Start();
        await TickAsync();
        RefreshUi();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isActive = false;
        _timer.Stop();
    }

    private async void OnShuffleClicked()
    {
        // Toggle shuffle on the queue service (if it supports it)
        // For now, we'll just shuffle the current queue
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

            // Update the queue service
            await _queueService.ReplaceQueueAsync(items);

            // Animate button feedback
            await _shuffleButton.ScaleTo(1.2, 100);
            await _shuffleButton.ScaleTo(1.0, 100);
        }
    }

    private async void OnQueueButtonClicked()
    {
        if (_queuePopout.IsVisible)
        {
            await CloseQueuePopoutAsync();
        }
        else
        {
            await OpenQueuePopoutAsync();
        }
    }

    private async void OnCloseQueuePopout()
    {
        await CloseQueuePopoutAsync();
    }

    private async Task OpenQueuePopoutAsync()
    {
        _queuePopout.IsVisible = true;
        await _queuePopout.FadeTo(1, 300, Easing.CubicOut);
        await _queueButton.RotateTo(180, 300);
    }

    private async Task CloseQueuePopoutAsync()
    {
        await _queuePopout.FadeTo(0, 300, Easing.CubicIn);
        _queuePopout.IsVisible = false;
        await _queueButton.RotateTo(0, 300);
    }

    private async Task OnPlayPauseClicked()
    {
        await _queueService.TogglePlayPauseAsync();
        RefreshUi();
    }

    private async Task TickAsync()
    {
        if (!_isActive)
        {
            return;
        }

        await _queueService.TickAsync();
        MainThread.BeginInvokeOnMainThread(RefreshUi);
    }

    private void OnQueueUpdated(object? sender, EventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(RefreshUi);
    }

    private void RefreshUi()
    {
        _queueItems.Clear();
        foreach (var item in _queueService.Queue)
        {
            _queueItems.Add(item);
        }

        var current = _queueService.CurrentItem;
        var isPlaying = _queueService.IsPlaying && !_queueService.IsPaused;

        // Animate play/pause button
        _playPauseButton.Text = isPlaying ? MaterialIcons.Pause : MaterialIcons.PlayArrow;

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
            UpdateGradient(Colors.SteelBlue, Colors.Navy);
        }
        else
        {
            // Load thumbnail with animation
            if (string.IsNullOrWhiteSpace(current.Thumbnail))
            {
                _thumbnailImage.Source = null;
            }
            else
            {
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

            // Update gradient based on thumbnail
            var accentColor = _thumbnailColorService.GetAccentColor(current.AccentKey ?? current.Thumbnail ?? current.VideoId);
            var complementary = GetComplementaryColor(accentColor);
            UpdateGradient(accentColor, complementary);
        }
    }

    private void UpdateGradient(Color color1, Color color2)
    {
        // Animate gradient transition
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_gradientColor1 != color1 || _gradientColor2 != color2)
            {
                // Create a blended animation between colors
                const int steps = 30;
                const int duration = 300;
                const int stepDuration = duration / steps;

                var startColor1 = _gradientColor1;
                var startColor2 = _gradientColor2;
                var targetColor1 = color1;
                var targetColor2 = color2;

                for (int i = 0; i <= steps; i++)
                {
                    var progress = i / (double)steps;
                    _gradientColor1 = BlendColors(startColor1, targetColor1, progress);
                    _gradientColor2 = BlendColors(startColor2, targetColor2, progress);

                    _gradientBackgroundBox.BackgroundColor = _gradientColor1;

                    if (i < steps)
                    {
                        await Task.Delay(stepDuration);
                    }
                }
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
        // Extract RGB components and darken for a nice gradient
        var r = (int)(baseColor.Red * 255);
        var g = (int)(baseColor.Green * 255);
        var b = (int)(baseColor.Blue * 255);

        // Create a darker version by reducing brightness
        var factor = 0.6;
        return Color.FromRgba((int)(r * factor), (int)(g * factor), (int)(b * factor), 255);
    }

    private async void OnProgressChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_updatingSlider)
        {
            return;
        }

        var duration = _queueService.Duration;
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var position = TimeSpan.FromSeconds(duration.TotalSeconds * Math.Clamp(e.NewValue, 0, 1));
        await _queueService.SeekAsync(position);
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

            var dragHandle = new Label
            {
                Text = MaterialIcons.DragHandle,
                FontFamily = "MaterialIcons",
                FontSize = 18,
                TextColor = Colors.Gray,
                VerticalOptions = LayoutOptions.Center
            };

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = 16 },
                    new ColumnDefinition { Width = 52 },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10,
                Children =
                {
                    dragHandle,
                    thumbnail,
                    info
                }
            };
            Grid.SetColumn(thumbnail, 1);
            Grid.SetColumn(info, 2);

            var card = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                StrokeThickness = 1,
                Padding = 10,
                Margin = new Thickness(10, 6, 10, 6),
                Content = contentGrid,
                BackgroundColor = Colors.White
            };

            // Add long-press gesture
            var longPressAction = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
            longPressAction.Tapped += (s, e) =>
            {
                if (card.BindingContext is PlayerQueueItem item)
                {
                    OnQueueItemLongPressed(item);
                }
            };
            card.GestureRecognizers.Add(longPressAction);

            // Update card appearance based on binding
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

    private async void OnQueueItemLongPressed(PlayerQueueItem item)
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
        {
            return "0:00";
        }

        var hours = (int)time.TotalHours;
        var minutes = time.Minutes;
        var seconds = time.Seconds;

        if (hours > 0)
        {
            return $"{hours}:{minutes:D2}:{seconds:D2}";
        }

        return $"{minutes}:{seconds:D2}";
    }
}
