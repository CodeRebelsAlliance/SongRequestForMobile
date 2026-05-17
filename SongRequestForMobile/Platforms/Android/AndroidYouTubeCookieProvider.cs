using Android.Webkit;

namespace SongRequestForMobile.Services;

public sealed class AndroidYouTubeCookieProvider : IYouTubeCookieProvider
{
    public Task<IReadOnlyList<System.Net.Cookie>> CaptureCookiesAsync(string loginUrl, Func<string?, Task<bool>> isReadyAsync, CancellationToken cancellationToken = default)
    {
        var uri = Uri.TryCreate(loginUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : new Uri("https://www.youtube.com/");

        var host = uri.Host;
        var cookieManager = CookieManager.Instance;
        var cookieHeader = cookieManager.GetCookie($"{uri.Scheme}://{host}");

        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return Task.FromResult<IReadOnlyList<System.Net.Cookie>>(Array.Empty<System.Net.Cookie>());
        }

        var cookies = ParseCookieHeader(cookieHeader, host).ToList();
        return Task.FromResult<IReadOnlyList<System.Net.Cookie>>(cookies);
    }

    private static IEnumerable<System.Net.Cookie> ParseCookieHeader(string cookieHeader, string host)
    {
        foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equalsIndex = part.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var name = part[..equalsIndex].Trim();
            var value = part[(equalsIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            yield return new System.Net.Cookie(name, value, "/", host);
        }
    }
}
