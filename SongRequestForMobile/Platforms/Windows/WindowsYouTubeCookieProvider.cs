using SongRequestForMobile.Services;

namespace SongRequestForMobile.Platforms.Windows;

public sealed class WindowsYouTubeCookieProvider : IYouTubeCookieProvider
{
    public async Task<IReadOnlyList<System.Net.Cookie>> CaptureCookiesAsync(Microsoft.Maui.Controls.WebView webView, string currentUrl, CancellationToken cancellationToken = default)
    {
        var platformView = webView.Handler?.PlatformView as Microsoft.UI.Xaml.Controls.WebView2;
        if (platformView?.CoreWebView2 == null)
            return Array.Empty<System.Net.Cookie>();

        var host = TryGetHost(currentUrl);
        var uri = $"https://{host}/";

        var webViewCookies = await platformView.CoreWebView2.CookieManager.GetCookiesAsync(uri);

        if (webViewCookies == null || webViewCookies.Count == 0)
            return Array.Empty<System.Net.Cookie>();

        var cookies = new List<System.Net.Cookie>();

        foreach (var wvCookie in webViewCookies)
        {
            try
            {
                cookies.Add(new System.Net.Cookie(
                    wvCookie.Name ?? string.Empty,
                    wvCookie.Value ?? string.Empty,
                    string.IsNullOrWhiteSpace(wvCookie.Path) ? "/" : wvCookie.Path,
                    string.IsNullOrWhiteSpace(wvCookie.Domain) ? host : wvCookie.Domain)
                {
                    Secure = wvCookie.IsSecure,
                    HttpOnly = wvCookie.IsHttpOnly,
                    Expires = DateTimeOffset
                        .FromUnixTimeSeconds((long)wvCookie.Expires)
                        .LocalDateTime
                });
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
