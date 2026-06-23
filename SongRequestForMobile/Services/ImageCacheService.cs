using System.Collections.Concurrent;

namespace SongRequestForMobile.Services;

public sealed class ImageCacheService : IImageCacheService, IDisposable
{
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);
    private readonly HttpClient _client = new() { Timeout = FetchTimeout };

    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _cacheDir;

    public ImageCacheService()
    {
        _cacheDir = Path.Combine(FileSystem.CacheDirectory, "thumbnails");
        Directory.CreateDirectory(_cacheDir);
    }

    public ImageSource? GetCached(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (_cache.TryGetValue(url, out var localPath) && File.Exists(localPath))
            return ImageSource.FromFile(localPath);

        return null;
    }

    public async Task<ImageSource?> GetImageAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var localPath = await DownloadAsync(url).ConfigureAwait(false);
        return localPath != null ? ImageSource.FromFile(localPath) : null;
    }

    public void Preload(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (_cache.ContainsKey(url))
            return;

        _ = DownloadAsync(url);
    }

    private async Task<string?> DownloadAsync(string url)
    {
        try
        {
            var fileName = $"thumb_{url.GetHashCode():X8}.jpg";
            var localPath = Path.Combine(_cacheDir, fileName);

            if (File.Exists(localPath))
            {
                _cache[url] = localPath;
                return localPath;
            }

            var bytes = await _client.GetByteArrayAsync(url).ConfigureAwait(false);
            await File.WriteAllBytesAsync(localPath, bytes).ConfigureAwait(false);
            _cache[url] = localPath;
            return localPath;
        }
        catch
        {
            return null;
        }
    }

    public void Invalidate(string url)
    {
        if (_cache.TryRemove(url, out var localPath) && localPath != null)
        {
            try { File.Delete(localPath); } catch { }
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
