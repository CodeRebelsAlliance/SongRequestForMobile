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

    public LyricsDisplayService(LyricsService lyricsService)
    {
        _lyricsService = lyricsService ?? throw new ArgumentNullException(nameof(lyricsService));
    }

    public async Task FetchLyricsAsync(PlayerQueueItem item, CancellationToken ct = default)
    {
        if (item == null) return;

        // Check cache first
        var cacheKey = GetCacheKey(item);
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
            CurrentLyrics = cachedResult;
            CurrentSyncedLines = cachedResult.ParseSyncedLines();
            CurrentLineIndex = -1;
            return;
        }

        // Not in cache, fetch from API
        IsLoading = true;
        try
        {
            // Create a Song object from the PlayerQueueItem to use the normalizer
            var song = new Song(item.Title, item.Channel, null, item.Duration, string.Empty);

            // Use LyricsQueryNormalizer to properly clean the artist and title
            var normalizedQuery = LyricsQueryNormalizer.Build(song);

            var result = await _lyricsService.GetLyricsAsync(
                normalizedQuery.Artist,
                normalizedQuery.Title,
                item.Duration,  // Use actual song duration instead of zero
                null,
                ct
            ).ConfigureAwait(false);

            _cache.TryAdd(cacheKey, result);
            CurrentLyrics = result;
            CurrentSyncedLines = result.Found ? result.ParseSyncedLines() : new();
            CurrentLineIndex = -1;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task PrefetchNextLyricsAsync(PlayerQueueItem? nextItem, CancellationToken ct = default)
    {
        if (nextItem == null) return;

        var cacheKey = GetCacheKey(nextItem);
        if (_cache.ContainsKey(cacheKey)) return; // Already cached

        try
        {
            // Create a Song object from the PlayerQueueItem to use the normalizer
            var song = new Song(nextItem.Title, nextItem.Channel, null, nextItem.Duration, string.Empty);

            // Use LyricsQueryNormalizer to properly clean the artist and title
            var normalizedQuery = LyricsQueryNormalizer.Build(song);

            var result = await _lyricsService.GetCachedLyricsAsync(
                normalizedQuery.Artist,
                normalizedQuery.Title,
                nextItem.Duration,  // Use actual song duration instead of zero
                null,
                ct
            ).ConfigureAwait(false);

            if (result.Found)
            {
                _cache.TryAdd(cacheKey, result);
            }
        }
        catch
        {
            // Silently fail on prefetch
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
