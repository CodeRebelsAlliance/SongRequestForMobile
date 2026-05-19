using Microsoft.Extensions.DependencyInjection;
using SongRequestForMobile.Pages;
using SongRequestForMobile.Services;

namespace SongRequestForMobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Check for updates on app start
            _ = CheckForUpdatesAsync();

            return window;
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Give the app a moment to fully load before checking for updates
                await Task.Delay(2000);

                var updateCheckService = Application.Current?.Handler?.MauiContext?.Services.GetService<IUpdateCheckService>();
                if (updateCheckService == null)
                    return;

                var updateInfo = await updateCheckService.CheckForUpdatesAsync();
                if (updateInfo == null)
                    return;

                // Get the download service
                var downloadService = Application.Current?.Handler?.MauiContext?.Services.GetService<IUpdateDownloadService>();
                if (downloadService == null)
                    return;

                // Show the update popup
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var updatePopup = new UpdatePopup();
                    var viewModel = new UpdatePopupViewModel(updatePopup, downloadService, updateInfo);
                    updatePopup.BindingContext = viewModel;

                    if (Application.Current?.MainPage?.Navigation != null)
                    {
                        await Application.Current.MainPage.Navigation.PushModalAsync(updatePopup);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check error: {ex.Message}");
                // Silently ignore errors - don't disrupt app startup
            }
        }
    }
}
