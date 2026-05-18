using Microsoft.Maui.Controls;
using SongRequestForMobile.Models;
using SongRequestForMobile.Services;
using System.Collections.ObjectModel;

namespace SongRequestForMobile.Pages;

public sealed class BlacklistPage : ContentPage
{
    private readonly IRequestSyncService _syncService;
    private readonly ServerApiClient _serverApiClient;
    private readonly AppState _appState;
    private readonly ObservableCollection<RequestDisplayItem> _items = new();
    private readonly CollectionView _collectionView;

    public BlacklistPage(IRequestSyncService syncService, ServerApiClient serverApiClient, AppState appState)
    {
        _syncService = syncService;
        _serverApiClient = serverApiClient;
        _appState = appState;
        Title = "Blacklist";

        _collectionView = new CollectionView
        {
            ItemsSource = _items,
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(RequestDisplayItem.Title));

                var unblacklistButton = new Button { Text = "Unblacklist", BackgroundColor = Colors.SeaGreen, TextColor = Colors.White };

                var stack = new VerticalStackLayout { Spacing = 4, Children = { title, unblacklistButton } };
                var border = new Border { Padding = 12, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }, Content = stack, Margin = new Thickness(0,0,0,8) };

                border.BindingContextChanged += (_, _) => {
                    if (border.BindingContext is RequestDisplayItem it)
                    {
                        unblacklistButton.Command = new Command(async () => await UnblacklistAsync(it));
                    }
                };

                return border;
            })
        };

        Content = new Grid
        {
            Padding = 16,
            Children = { _collectionView }
        };

        _syncService.Updated += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        _items.Clear();
        foreach (var item in _syncService.BlacklistItems)
        {
            _items.Add(item);
        }
    }

    private async Task UnblacklistAsync(RequestDisplayItem item)
    {
        try
        {
            _serverApiClient.Configure(_appState.Settings.ServerBaseUrl, _appState.Settings.BearerToken);
            await _serverApiClient.UnblacklistRequestAsync(item.VideoId);
            await _syncService.RefreshAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
