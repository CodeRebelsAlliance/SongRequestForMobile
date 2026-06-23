using SongRequestForMobile.Services;

namespace SongRequestForMobile;

public sealed class DefaultYouTubeCookieProvider : IYouTubeCookieProvider
{
    public async Task<IReadOnlyList<System.Net.Cookie>> CaptureCookiesAsync(Microsoft.Maui.Controls.WebView webView, string currentUrl, CancellationToken cancellationToken = default)
    {
        var host = TryGetHost(currentUrl);
        var cookies = new List<System.Net.Cookie>();

        try
        {
            var js = await webView.EvaluateJavaScriptAsync("document.cookie");
            if (!string.IsNullOrWhiteSpace(js))
            {
                foreach (var part in js.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    var name = part[..eq].Trim();
                    var value = part[(eq + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    try
                    {
                        cookies.Add(new System.Net.Cookie(name, value, "/", host));
                    }
                    catch { }
                }
            }
        }
        catch
        {
            // JavaScript evaluation may fail in some environments
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
