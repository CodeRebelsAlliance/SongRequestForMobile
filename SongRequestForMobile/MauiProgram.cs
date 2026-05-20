using Microsoft.Extensions.Logging;
using SongRequestForMobile.Pages;
using SongRequestForMobile.Services;

namespace SongRequestForMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialIcons");
            });

        builder.Services.AddSingleton<IPlaybackStateStore, PlaybackStateStore>();
        builder.Services.AddSingleton<AppState>();
        builder.Services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        builder.Services.AddSingleton<IYouTubeCookieStore, PersistedYouTubeCookieStore>();
        builder.Services.AddSingleton<YouTubeSession>();
        builder.Services.AddSingleton(sp =>
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
            // Set User-Agent to "SongRequest For Mobile v{version}"
            var version = Microsoft.Maui.ApplicationModel.AppInfo.VersionString;
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"SongRequest For Mobile v{version}");
            return client;
        });
        builder.Services.AddSingleton<ServerApiClient>();
        builder.Services.AddSingleton<IRequestSyncService, RequestSyncService>();
        builder.Services.AddSingleton<IThumbnailColorService, ThumbnailColorService>();
        builder.Services.AddSingleton<IPlayerQueueService, PlayerQueueService>();
        builder.Services.AddSingleton<IQueueItemFactory, QueueItemFactory>();
        builder.Services.AddSingleton<ISettingsExportService, SettingsExportService>();
        builder.Services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
        builder.Services.AddSingleton<IUpdateDownloadService, UpdateDownloadService>();
        builder.Services.AddSingleton(sp =>
        {
            return new LyricsService(sp.GetRequiredService<HttpClient>());
        });
        builder.Services.AddSingleton<ILyricsDisplayService, LyricsDisplayService>();
        builder.Services.AddTransient<YouTubeAuthPage>();
#if ANDROID
        builder.Services.AddSingleton<IYouTubeCookieProvider, AndroidYouTubeCookieProvider>();
#else
        builder.Services.AddSingleton<IYouTubeCookieProvider, DefaultYouTubeCookieProvider>();
#endif
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<RequestsPage>();
        builder.Services.AddTransient<PlayerPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<UpdatePopup>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
