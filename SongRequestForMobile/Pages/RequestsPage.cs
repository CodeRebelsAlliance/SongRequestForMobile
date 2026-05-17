using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Shapes;
using SongRequestForMobile.Models;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Pages;

public sealed class RequestsPage : ContentPage
{
    private readonly IRequestSyncService _requestSyncService;
    private readonly IPlayerQueueService _queueService;
    private readonly IQueueItemFactory _queueItemFactory;
    private readonly ObservableCollection<RequestDisplayItem> _items = new();
    private readonly CollectionView _collectionView;
    private readonly Border _badgeBorder;
    private readonly Label _badgeLabel;
    private readonly Label _subtitleLabel;
    private readonly IDispatcherTimer _timer;
    private bool _isActive;

    public RequestsPage(IRequestSyncService requestSyncService, IPlayerQueueService queueService, IQueueItemFactory queueItemFactory)
    {
        _requestSyncService = requestSyncService;
        _queueService = queueService;
        _queueItemFactory = queueItemFactory;
        _requestSyncService.Updated += OnServiceUpdated;

        _badgeLabel = new Label
        {
            Text = "Not synced yet",
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };

        _badgeBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 8),
            BackgroundColor = Colors.Gray,
            Content = _badgeLabel,
            HorizontalOptions = LayoutOptions.Start
        };

        _subtitleLabel = new Label
        {
            Text = "Refreshing every 30 seconds while authorized.",
            FontSize = 12,
            TextColor = Colors.Gray
        };

        _collectionView = new CollectionView
        {
            ItemsSource = _items,
            SelectionMode = SelectionMode.None,
            EmptyView = new Grid
            {
                Children =
                {
                    new Label
                    {
                        Text = "No requests cached yet.",
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        TextColor = Colors.Gray
                    }
                }
            }
        };
        _collectionView.ItemTemplate = CreateTemplate();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(30);
        _timer.Tick += async (_, _) => await RefreshAsync();

        Title = "Requests";
        Content = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            Padding = 16,
            RowSpacing = 12,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new Label
                        {
                            Text = "Requests",
                            FontSize = 24,
                            FontAttributes = FontAttributes.Bold
                        },
                        _badgeBorder,
                        _subtitleLabel
                    }
                },
                _collectionView.WithGridRow(2)
            }
        };

        UpdateUiFromService();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isActive = true;
        _timer.Start();
        await RefreshAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isActive = false;
        _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (!_isActive)
        {
            return;
        }

        await _requestSyncService.RefreshAsync();
        await MainThread.InvokeOnMainThreadAsync(UpdateUiFromService);
    }

    private void UpdateUiFromService()
    {
        _items.Clear();
        foreach (var item in _requestSyncService.Items)
        {
            _items.Add(item);
        }

        var state = _requestSyncService.State;
        _badgeLabel.Text = state.StatusText;
        _badgeBorder.BackgroundColor = state.IsUnauthorized
            ? Colors.IndianRed
            : state.LastSyncUtc.HasValue
                ? Colors.SeaGreen
                : Colors.SteelBlue;
        _subtitleLabel.Text = state.IsUnauthorized
            ? "Token is invalid or expired. Update Settings to resume syncing."
            : "Refreshing every 30 seconds while authorized.";
    }

    private void OnServiceUpdated(object? sender, EventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(UpdateUiFromService);
    }

    private DataTemplate CreateTemplate()
    {
        return new DataTemplate(() =>
        {
            var thumbnail = new Image
            {
                WidthRequest = 88,
                HeightRequest = 66,
                Aspect = Aspect.AspectFill,
                BackgroundColor = Colors.Black
            };
            thumbnail.SetBinding(Image.SourceProperty, nameof(RequestDisplayItem.Thumbnail));

            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.TailTruncation,
                FontSize = 15
            };
            title.SetBinding(Label.TextProperty, nameof(RequestDisplayItem.Title));

            var channel = new Label
            {
                FontSize = 12,
                TextColor = Colors.Gray,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            channel.SetBinding(Label.TextProperty, nameof(RequestDisplayItem.Channel));

            var message = new Label
            {
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap,
                MaxLines = 2
            };
            message.SetBinding(Label.TextProperty, nameof(RequestDisplayItem.Message));

            var state = new Label
            {
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.DarkOrange
            };
            state.SetBinding(Label.TextProperty, new Binding(nameof(RequestDisplayItem.IsDownloading), converter: new DownloadStateConverter()));

            var meta = new Label
            {
                FontSize = 12,
                TextColor = Colors.Gray
            };
            meta.SetBinding(Label.TextProperty, new Binding(nameof(RequestDisplayItem.IsApproved), converter: new ApprovalStateConverter()));

            var textStack = new VerticalStackLayout
            {
                Spacing = 4,
                Children = { title, channel, message, state, meta }
            };

            var border = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                StrokeThickness = 1,
                Padding = 12,
                Margin = new Thickness(0, 0, 0, 10),
                Content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition { Width = 88 },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    ColumnSpacing = 12,
                    Children =
                    {
                        thumbnail,
                        textStack.WithGridColumn(1)
                    }
                }
            };

            border.BindingContextChanged += (_, _) =>
            {
                if (border.BindingContext is RequestDisplayItem item)
                {
                    var accent = AccentFrom(item.Thumbnail ?? item.VideoId);
                    border.Stroke = accent;
                    border.BackgroundColor = Colors.White;
                }
            };

            var tap = new TapGestureRecognizer
            {
                Command = new Command<RequestDisplayItem>(item => _ = ShowActionMenuAsync(item))
            };
            tap.SetBinding(TapGestureRecognizer.CommandParameterProperty, new Binding("."));
            border.GestureRecognizers.Add(tap);
            return border;
        });
    }

    private async Task ShowActionMenuAsync(RequestDisplayItem item)
    {
        var choice = await DisplayActionSheet($"Queue '{item.Title}'", "Cancel", null, "Queue", "Play next", "Play now (replace queue)");
        var queueItem = _queueItemFactory.FromRequest(item);

        switch (choice)
        {
            case "Queue":
                _queueService.Enqueue(queueItem);
                break;
            case "Play next":
                await _queueService.PlayNextAsync(queueItem);
                break;
            case "Play now (replace queue)":
                await _queueService.PlayNowReplaceQueueAsync(queueItem);
                break;
        }
    }

    private static Color AccentFrom(string? seed)
    {
        if (string.IsNullOrWhiteSpace(seed))
        {
            return Colors.SteelBlue;
        }

        unchecked
        {
            var hash = seed.GetHashCode();
            var r = (byte)(96 + (hash & 0x3F));
            var g = (byte)(96 + ((hash >> 8) & 0x3F));
            var b = (byte)(96 + ((hash >> 16) & 0x3F));
            return Color.FromRgb(r, g, b);
        }
    }

    private sealed class DownloadStateConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is true ? "Downloading and caching..." : "Cached";

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    private sealed class ApprovalStateConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is true ? "Approved" : "Waiting / not approved";

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}

static class RequestsViewExtensions
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
