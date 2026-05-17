namespace SongRequestForMobile.Pages;

public sealed class RequestsPage : ContentPage
{
    public RequestsPage()
    {
        Title = "Requests";
        Content = new Grid
        {
            Children =
            {
                new Label
                {
                    Text = "Requests tab coming soon.",
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };
    }
}
