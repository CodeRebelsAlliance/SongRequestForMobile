using System.Net;

namespace SongRequestForMobile;

public static class YouTubeCookieStore
{
    private static readonly string[] CookieSources =
    [
        "https://www.youtube.com",
        "https://m.youtube.com",
        "https://accounts.google.com"
    ];

    public static Task<IReadOnlyList<Cookie>> CaptureAsync()
    {
#if ANDROID
        var manager = Android.Webkit.CookieManager.Instance;
        if (manager is null)
        {
            return Task.FromResult<IReadOnlyList<Cookie>>(Array.Empty<Cookie>());
        }

        var cookies = new List<Cookie>();
        foreach (var source in CookieSources)
        {
            var cookieHeader = manager.GetCookie(source);
            cookies.AddRange(CookieParsing.ParseCookieHeader(cookieHeader, new Uri(source).Host));
        }

        return Task.FromResult<IReadOnlyList<Cookie>>(CookieParsing.Sanitize(cookies));
#else
        throw new PlatformNotSupportedException("YouTube cookie capture is implemented for Android only.");
#endif
    }
}