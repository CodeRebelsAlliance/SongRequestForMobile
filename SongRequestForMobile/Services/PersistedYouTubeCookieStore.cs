using System.Net;
using System.Text.Json;

namespace SongRequestForMobile.Services;

public sealed class PersistedYouTubeCookieStore : IYouTubeCookieStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _storagePath = Path.Combine(FileSystem.AppDataDirectory, "youtube-cookies.json");

    public async Task<IReadOnlyList<Cookie>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<Cookie>();
        }

        await using var stream = File.OpenRead(_storagePath);
        var persistedCookies = await JsonSerializer.DeserializeAsync<List<PersistedCookie>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (persistedCookies == null || persistedCookies.Count == 0)
        {
            return Array.Empty<Cookie>();
        }

        var cookies = new List<Cookie>();
        foreach (var persisted in persistedCookies)
        {
            try
            {
                cookies.Add(persisted.ToCookie());
            }
            catch
            {
                // Skip malformed cookies and continue loading the rest.
            }
        }

        return cookies;
    }

    public async Task SaveAsync(IReadOnlyList<Cookie> cookies, CancellationToken cancellationToken = default)
    {
        var persistedCookies = cookies
            .Where(cookie => !string.IsNullOrWhiteSpace(cookie.Name))
            .Select(PersistedCookie.FromCookie)
            .ToList();

        await using var stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, persistedCookies, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_storagePath))
        {
            File.Delete(_storagePath);
        }

        return Task.CompletedTask;
    }

    private sealed class PersistedCookie
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Path { get; set; } = "/";
        public DateTime? ExpiresUtc { get; set; }
        public bool Secure { get; set; }
        public bool HttpOnly { get; set; }
        public int Version { get; set; }

        public static PersistedCookie FromCookie(Cookie cookie) => new()
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = cookie.Domain,
            Path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
            ExpiresUtc = cookie.Expires == DateTime.MinValue ? null : cookie.Expires.ToUniversalTime(),
            Secure = cookie.Secure,
            HttpOnly = cookie.HttpOnly,
            Version = cookie.Version
        };

        public Cookie ToCookie()
        {
            var cookie = new Cookie(Name, Value, string.IsNullOrWhiteSpace(Path) ? "/" : Path, Domain)
            {
                Secure = Secure,
                HttpOnly = HttpOnly,
                Version = Version
            };

            if (ExpiresUtc.HasValue)
            {
                cookie.Expires = ExpiresUtc.Value.ToLocalTime();
            }

            return cookie;
        }
    }
}
