using System.Collections.Concurrent;
using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

/// <summary>
/// Manages lyrics fetching, caching, and display for the player.
/// Handles prefetching and real-time position updates for timed lyrics.
/// </summary>
public class LyricsDisplayService : ILyricsDisplayService
{
    private readonly LyricsService _lyricsService;
    private readonly IDownloadLogService? _log;
    private readonly ConcurrentDictionary<string, LyricsResult> _cache = new();
    private LyricsResult? _currentLyrics;
    private List<(TimeSpan Time, string Text)> _currentSyncedLines = new();
    private int _currentLineIndex = -1;
    private bool _isLoading;

    public LyricsResult? CurrentLyrics
    {
        get => _currentLyrics;
        private set
        {
            if (_currentLyrics != value)
            {
                _currentLyrics = value;
                LyricsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public List<(TimeSpan Time, string Text)> CurrentSyncedLines
    {
        get => _currentSyncedLines;
        private set
        {
            _currentSyncedLines = value ?? new();
            LyricsUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public int CurrentLineIndex
    {
        get => _currentLineIndex;
        private set
        {
            if (_currentLineIndex != value)
            {
                _currentLineIndex = value;
                CurrentLineChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                LoadingStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? LyricsUpdated;
    public event EventHandler? LoadingStateChanged;
    public event EventHandler? CurrentLineChanged;

    public LyricsDisplayService(LyricsService lyricsService, IDownloadLogService? logService = null)
    {
        _lyricsService = lyricsService ?? throw new ArgumentNullException(nameof(lyricsService));
        _log = logService;
    }

    public async Task FetchLyricsAsync(PlayerQueueItem item, CancellationToken ct = default)
    {
        if (item == null)
        {
            _log?.Log(LogLevel.Warning, "Lyrics", "FetchLyricsAsync called with null item");
            return;
        }

        _log?.Log(LogLevel.Info, "Lyrics", $"Fetching lyrics for: \"{item.Title}\" by {item.Channel}");

        if (item.Duration.TotalSeconds <= 0)
        {
            _log?.Log(LogLevel.Warning, "Lyrics", $"Skipping fetch: duration is {item.Duration.TotalSeconds:F0}s for \"{item.Title}\"");
            return;
        }

        var cacheKey = GetCacheKey(item);
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            _log?.Log(LogLevel.Debug, "Lyrics", $"Cache hit for \"{item.Title}\" by {item.Channel}");
            CurrentLyrics = cachedResult;
            CurrentSyncedLines = cachedResult.ParseSyncedLines();
            CurrentLineIndex = -1;
            return;
        }

        _log?.Log(LogLevel.Debug, "Lyrics", $"Cache miss, fetching from API...");
        IsLoading = true;
        try
        {
            var song = new Song(item.Title, item.Channel, null, item.Duration, string.Empty);
            var normalizedQuery = LyricsQueryNormalizer.Build(song);
            _log?.Log(LogLevel.Debug, "Lyrics", $"Normalized query: artist=\"{normalizedQuery.Artist}\" title=\"{normalizedQuery.Title}\"");

            _log?.Log(LogLevel.Info, "Lyrics", $"Calling lrclib.net for \"{normalizedQuery.Title}\" by {normalizedQuery.Artist}...");
            var result = await _lyricsService.GetLyricsAsync(
                normalizedQuery.Artist,
                normalizedQuery.Title,
                item.Duration,
                null,
                ct
            ).ConfigureAwait(false);

            _cache.TryAdd(cacheKey, result);
            CurrentLyrics = result;
            CurrentSyncedLines = result.Found ? result.ParseSyncedLines() : new();
            CurrentLineIndex = -1;

            if (!result.Found)
                _log?.Log(LogLevel.Warning, "Lyrics", $"No lyrics found on lrclib.net for \"{normalizedQuery.Title}\"");
            else if (result.Instrumental)
                _log?.Log(LogLevel.Info, "Lyrics", $"Track is instrumental, no lyrics needed");
        }
        catch (Exception ex)
        {
            _log?.Log(LogLevel.Error, "Lyrics", $"Fetch failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task PrefetchNextLyricsAsync(PlayerQueueItem? nextItem, CancellationToken ct = default)
    {
        if (nextItem == null)
        {
            _log?.Log(LogLevel.Debug, "Lyrics", "Prefetch skipped: no next item");
            return;
        }

        if (nextItem.Duration.TotalSeconds <= 0)
        {
            _log?.Log(LogLevel.Debug, "Lyrics", $"Prefetch skipped: duration is 0s for \"{nextItem.Title}\"");
            return;
        }

        var cacheKey = GetCacheKey(nextItem);
        if (_cache.ContainsKey(cacheKey))
        {
            _log?.Log(LogLevel.Debug, "Lyrics", $"Prefetch skipped: \"{nextItem.Title}\" already cached");
            return;
        }

        _log?.Log(LogLevel.Debug, "Lyrics", $"Prefetching lyrics for next: \"{nextItem.Title}\" by {nextItem.Channel}");
        try
        {
            var song = new Song(nextItem.Title, nextItem.Channel, null, nextItem.Duration, string.Empty);
            var normalizedQuery = LyricsQueryNormalizer.Build(song);

            var result = await _lyricsService.GetCachedLyricsAsync(
                normalizedQuery.Artist,
                normalizedQuery.Title,
                nextItem.Duration,
                null,
                ct
            ).ConfigureAwait(false);

            if (result.Found)
            {
                _cache.TryAdd(cacheKey, result);
                _log?.Log(LogLevel.Info, "Lyrics", $"Prefetched lyrics for \"{nextItem.Title}\"");
            }
            else
            {
                _log?.Log(LogLevel.Debug, "Lyrics", $"Prefetch found no lyrics for \"{nextItem.Title}\"");
            }
        }
        catch (Exception ex)
        {
            _log?.Log(LogLevel.Debug, "Lyrics", $"Prefetch failed for \"{nextItem.Title}\": {ex.Message}");
        }
    }
    public void UpdatePlaybackPosition(TimeSpan currentPosition)
    {
        if (CurrentSyncedLines.Count == 0)
        {
            CurrentLineIndex = -1;
            return;
        }

        // Find the line at or before current position
        int newIndex = -1;
        for (int i = 0; i < CurrentSyncedLines.Count; i++)
        {
            if (CurrentSyncedLines[i].Time <= currentPosition)
            {
                newIndex = i;
            }
            else
            {
                break;
            }
        }

        CurrentLineIndex = newIndex;
    }

    public void ClearCurrent()
    {
        CurrentLyrics = null;
        CurrentSyncedLines = new();
        CurrentLineIndex = -1;
        IsLoading = true;
    }

    public void ClearCache()
    {
        _cache.Clear();
        CurrentLyrics = null;
        CurrentSyncedLines = new();
        CurrentLineIndex = -1;
        IsLoading = false;
    }

    private static string GetCacheKey(PlayerQueueItem item)
    {
        return $"{item.Channel}|{item.Title}".ToLowerInvariant();
    }
}
