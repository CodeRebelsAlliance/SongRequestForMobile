using System.Net;

namespace SongRequestForMobile;

public static class CookieParsing
{
    public static IReadOnlyList<Cookie> ParseCookieHeader(string? cookieHeader, string host)
    {
        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return Array.Empty<Cookie>();
        }

        var cookies = new List<Cookie>();
        foreach (var pair in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = pair[..separatorIndex].Trim();
            var value = pair[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            try
            {
                cookies.Add(new Cookie(name, value, "/", host));
            }
            catch (CookieException)
            {
                // Ignore invalid cookie entries from the platform store.
            }
        }

        return cookies;
    }

    public static IReadOnlyList<Cookie> Sanitize(IEnumerable<Cookie> cookies)
    {
        var sanitized = new List<Cookie>();
        foreach (var cookie in cookies)
        {
            try
            {
                var container = new CookieContainer();
                container.Add(cookie);
                sanitized.Add(cookie);
            }
            catch (CookieException)
            {
                // Skip invalid cookies.
            }
        }

        return sanitized;
    }
}