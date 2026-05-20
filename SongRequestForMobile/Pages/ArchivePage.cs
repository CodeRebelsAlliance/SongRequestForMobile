using Microsoft.Maui.Controls;
using SongRequestForMobile.Models;
using SongRequestForMobile.Resources;
using SongRequestForMobile.Services;
using System.Collections.ObjectModel;

namespace SongRequestForMobile.Pages;

public sealed class ArchivePage : ContentPage
{
    private readonly IRequestSyncService _syncService;
    private readonly ObservableCollection<RequestDisplayItem> _items = new();
    private readonly CollectionView _collectionView;

    public ArchivePage(IRequestSyncService syncService)
    {
        _syncService = syncService;

        _collectionView = new CollectionView
        {
            ItemsSource = _items,
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(RequestDisplayItem.Title));

                var subtitle = new Label { FontSize = 12, TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.Gray };
                subtitle.SetBinding(Label.TextProperty, nameof(RequestDisplayItem.Channel));

                var deleteButton = new Button
                {
                    Text = "Delete",
                    FontFamily = "OpenSansRegular",
                    BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#8B4545") : Colors.IndianRed,
                    TextColor = Colors.White
                };

                var stack = new VerticalStackLayout { Spacing = 4, Children = { title, subtitle, deleteButton } };
                var border = new Border { Padding = 12, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }, Content = stack, Margin = new Thickness(0,0,0,8) };
                border.BindingContextChanged += (_, _) => {
                    if (border.BindingContext is RequestDisplayItem it)
                    {
                        deleteButton.Command = new Command(async () => await DeleteFromDeviceAsync(it));
                    }
                };

                return border;
            })
        };

        var clearButton = new Button
        {
            Text = "Clear All",
            FontFamily = "OpenSansRegular",
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#404040") : Colors.DarkGray,
            TextColor = Colors.White
        };
        clearButton.Clicked += async (_, _) => await ClearArchiveAsync();

        Content = new Grid
        {
            Padding = 16,
            RowDefinitions = new RowDefinitionCollection { new RowDefinition { Height = GridLength.Star }, new RowDefinition { Height = GridLength.Auto } },
            Children = { _collectionView, clearButton.WithGridRow(1) }
        };

        _syncService.Updated += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        _items.Clear();
        foreach (var item in _syncService.ArchivedItems)
        {
            _items.Add(item);
        }
    }

    private async Task ClearArchiveAsync()
    {
        await _syncService.ClearArchiveAsync();
    }

    private async Task DeleteFromDeviceAsync(RequestDisplayItem item)
    {
        await _syncService.DeleteFromDeviceAsync(item.VideoId);
    }
}
