using Foundation;
using WebKit;
using SongRequestForMobile.Services;

namespace SongRequestForMobile.Platforms.iOS;

public sealed class iOSYouTubeCookieProvider : IYouTubeCookieProvider
{
    public async Task<IReadOnlyList<System.Net.Cookie>> CaptureCookiesAsync(Microsoft.Maui.Controls.WebView webView, string currentUrl, CancellationToken cancellationToken = default)
    {
        var platformView = webView.Handler?.PlatformView as WKWebView;
        if (platformView == null)
            return Array.Empty<System.Net.Cookie>();

        var cookieStore = platformView.Configuration.WebsiteDataStore.HttpCookieStore;
        var nsCookies = await cookieStore.GetAllCookiesAsync();

        if (nsCookies == null || nsCookies.Length == 0)
            return Array.Empty<System.Net.Cookie>();

        var host = TryGetHost(currentUrl);
        var cookies = new List<System.Net.Cookie>();

        foreach (var nsCookie in nsCookies)
        {
            try
            {
                var domain = nsCookie.Domain ?? host;
                var cookie = new System.Net.Cookie(
                    nsCookie.Name ?? string.Empty,
                    nsCookie.Value ?? string.Empty,
                    string.IsNullOrWhiteSpace(nsCookie.Path) ? "/" : nsCookie.Path,
                    domain);

                if (nsCookie.ExpiresDate != null)
                {
                    cookie.Expires = (DateTime)nsCookie.ExpiresDate;
                }

                cookies.Add(cookie);
            }
            catch
            {
            }
        }

        return cookies;
    }

    private static string TryGetHost(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Host;
        return "youtube.com";
    }
}
