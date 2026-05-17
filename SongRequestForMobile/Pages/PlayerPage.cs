using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Shapes;
using Plugin.Maui.Audio;
using SongRequestForMobile.Models;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Pages;

public sealed class PlayerPage : ContentPage
{
    private readonly IPlayerQueueService _queueService;
    private readonly IThumbnailColorService _thumbnailColorService;
    private readonly ObservableCollection<PlayerQueueItem> _queueItems = new();
    private readonly Label _titleLabel;
    private readonly Label _channelLabel;
    private readonly Label _statusLabel;
    private readonly Label _timeLabel;
    private readonly Label _queueSummaryLabel;
    private readonly Label _currentTimeLabel;
    private readonly Label _durationLabel;
    private readonly Image _thumbnailImage;
    private readonly Border _heroBorder;
    private readonly Slider _progressSlider;
    private readonly Button _playPauseButton;
    private readonly CollectionView _queueCollectionView;
    private readonly IDispatcherTimer _timer;
    private bool _isActive;
    private bool _updatingSlider;

    public PlayerPage(IPlayerQueueService queueService, IThumbnailColorService thumbnailColorService)
    {
        _queueService = queueService;
        _thumbnailColorService = thumbnailColorService;
        _queueService.Updated += OnQueueUpdated;

        _thumbnailImage = new Image
        {
            HeightRequest = 210,
            Aspect = Aspect.AspectFill
        };

        _titleLabel = new Label
        {
            Text = "Nothing is playing yet.",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _channelLabel = new Label
        {
            Text = "Queue a song from Requests to get started.",
            FontSize = 13,
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _statusLabel = new Label
        {
            Text = "Ready.",
            FontSize = 12,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold
        };

        _timeLabel = new Label
        {
            Text = "No track selected",
            FontSize = 12,
            TextColor = Colors.White
        };

        _queueSummaryLabel = new Label
        {
            Text = "Queue is empty.",
            FontSize = 12,
            TextColor = Colors.Gray
        };

        _currentTimeLabel = new Label
        {
            Text = "0:00",
            FontSize = 12,
            TextColor = Colors.Gray
        };

        _durationLabel = new Label
        {
            Text = "0:00",
            FontSize = 12,
            TextColor = Colors.Gray,
            HorizontalTextAlignment = TextAlignment.End
        };

        _progressSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            ThumbColor = Colors.White,
            MinimumTrackColor = Colors.White,
            MaximumTrackColor = Color.FromArgb("#4DFFFFFF")
        };
        _progressSlider.ValueChanged += OnProgressChanged;

        _playPauseButton = new Button
        {
            Text = "▶",
            FontSize = 24,
            WidthRequest = 72,
            HeightRequest = 72,
            CornerRadius = 36,
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
            Command = new Command(async () => await OnPlayPauseClicked())
        };

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

        _heroBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 28 },
            Padding = 16,
            Content = BuildHeroContent(),
            BackgroundColor = Colors.SteelBlue
        };

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += async (_, _) => await TickAsync();

        Title = "Player";
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
                        Text = "Player",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },
                    _heroBorder,
                    BuildControls(),
                    BuildProgressSection(),
                    new Label
                    {
                        Text = "Editable queue",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold
                    },
                    _queueSummaryLabel,
                    _queueCollectionView
                }
            }
        };

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

    private View BuildHeroContent()
    {
        var thumbFrame = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            StrokeThickness = 0,
            HeightRequest = 210,
            Content = _thumbnailImage,
            BackgroundColor = Colors.Black
        };

        return new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                thumbFrame,
                _titleLabel,
                _channelLabel,
                _statusLabel,
                _timeLabel
            }
        };
    }

    private View BuildControls()
    {
        var previous = new Button
        {
            Text = "⏮",
            FontSize = 26,
            WidthRequest = 64,
            HeightRequest = 64,
            CornerRadius = 32,
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
            Command = new Command(async () => await _queueService.PlayPreviousAsync())
        };

        var next = new Button
        {
            Text = "⏭",
            FontSize = 26,
            WidthRequest = 64,
            HeightRequest = 64,
            CornerRadius = 32,
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
            Command = new Command(async () => await _queueService.SkipNextAsync())
        };

        return new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                previous,
                _playPauseButton,
                next
            }
        };
    }

    private View BuildProgressSection()
    {
        var progressGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        progressGrid.Children.Add(_currentTimeLabel);
        progressGrid.Children.Add(_durationLabel);
        Grid.SetColumn(_durationLabel, 1);

        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                _progressSlider,
                progressGrid
            }
        };
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
        var accent = _queueService.AccentColor;
        _heroBorder.BackgroundColor = accent;
        _playPauseButton.Text = _queueService.IsPlaying && !_queueService.IsPaused ? "⏸" : "▶";

        if (current == null)
        {
            _thumbnailImage.Source = null;
            _titleLabel.Text = "Nothing is playing yet.";
            _channelLabel.Text = "Queue a song from Requests to get started.";
            _statusLabel.Text = "Ready.";
            _timeLabel.Text = "No track selected";
            _currentTimeLabel.Text = "0:00";
            _durationLabel.Text = "0:00";
            _progressSlider.Maximum = 1;
            _progressSlider.Value = 0;
        }
        else
        {
            _thumbnailImage.Source = string.IsNullOrWhiteSpace(current.Thumbnail) ? null : ImageSource.FromUri(new Uri(current.Thumbnail));
            _titleLabel.Text = current.Title;
            _channelLabel.Text = current.Channel;
            _statusLabel.Text = _queueService.IsPaused ? "Paused" : _queueService.IsPlaying ? "Playing" : "Ready";
            _timeLabel.Text = string.IsNullOrWhiteSpace(current.Message) ? current.VideoId : current.Message;

            var duration = _queueService.Duration;
            var position = _queueService.CurrentPosition;
            _currentTimeLabel.Text = FormatTime(position);
            _durationLabel.Text = FormatTime(duration);

            _updatingSlider = true;
            _progressSlider.Maximum = 1;
            _progressSlider.Value = duration.TotalSeconds > 0 ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0, 1) : 0;
            _updatingSlider = false;
        }

        _queueSummaryLabel.Text = _queueItems.Count == 0
            ? "Queue is empty."
            : $"{_queueItems.Count} item(s) waiting.";
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
                WidthRequest = 56,
                HeightRequest = 56,
                Aspect = Aspect.AspectFill
            };
            thumbnail.SetBinding(Image.SourceProperty, nameof(PlayerQueueItem.Thumbnail));

            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            title.SetBinding(Label.TextProperty, nameof(PlayerQueueItem.Title));

            var channel = new Label
            {
                FontSize = 12,
                TextColor = Colors.Gray,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            channel.SetBinding(Label.TextProperty, nameof(PlayerQueueItem.Channel));

            var info = new VerticalStackLayout
            {
                Spacing = 2,
                Children = { title, channel }
            };

            var up = new Button
            {
                Text = "↑",
                FontSize = 18,
                WidthRequest = 34,
                HeightRequest = 34,
                CornerRadius = 17,
                Padding = 0,
                Command = new Command<PlayerQueueItem>(item => _queueService.MoveUp(item))
            };
            up.SetBinding(Button.CommandParameterProperty, new Binding("."));

            var down = new Button
            {
                Text = "↓",
                FontSize = 18,
                WidthRequest = 34,
                HeightRequest = 34,
                CornerRadius = 17,
                Padding = 0,
                Command = new Command<PlayerQueueItem>(item => _queueService.MoveDown(item))
            };
            down.SetBinding(Button.CommandParameterProperty, new Binding("."));

            var remove = new Button
            {
                Text = "✕",
                FontSize = 18,
                WidthRequest = 34,
                HeightRequest = 34,
                CornerRadius = 17,
                Padding = 0,
                BackgroundColor = Colors.IndianRed,
                TextColor = Colors.White,
                Command = new Command<PlayerQueueItem>(item => _queueService.Remove(item))
            };
            remove.SetBinding(Button.CommandParameterProperty, new Binding("."));

            var actions = new HorizontalStackLayout
            {
                Spacing = 8,
                Children = { up, down, remove }
            };

            var contentGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = 56 },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 10,
                Children =
                {
                    thumbnail,
                    info,
                    actions
                }
            };
            Grid.SetColumn(info, 1);
            Grid.SetColumn(actions, 2);

            var card = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                StrokeThickness = 1,
                Padding = 10,
                Margin = new Thickness(0, 0, 0, 10),
                Content = contentGrid
            };

            card.BindingContextChanged += (_, _) =>
            {
                if (card.BindingContext is PlayerQueueItem item)
                {
                    var accent = _thumbnailColorService.GetAccentColor(item.AccentKey ?? item.Thumbnail ?? item.VideoId);
                    card.Stroke = accent;
                    card.BackgroundColor = Colors.White;
                }
            };

            return card;
        });
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            return "0:00";
        }

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }
}
