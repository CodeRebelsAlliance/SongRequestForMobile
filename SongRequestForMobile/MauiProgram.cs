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
            });

        builder.Services.AddSingleton<IPlaybackStateStore, PlaybackStateStore>();
        builder.Services.AddSingleton<AppState>();
        builder.Services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        builder.Services.AddSingleton<IYouTubeCookieStore, PersistedYouTubeCookieStore>();
        builder.Services.AddSingleton<YouTubeSession>();
        builder.Services.AddSingleton(sp => new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        }));
        builder.Services.AddSingleton<ServerApiClient>();
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

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
