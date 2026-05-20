using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

/// <summary>
/// Manages lyrics fetching, caching, and display state for the player.
/// </summary>
public interface ILyricsDisplayService
{
    /// <summary>
    /// Gets the current lyrics result (may be loading or not found).
    /// </summary>
    LyricsResult? CurrentLyrics { get; }

    /// <summary>
    /// Gets the synced lyrics lines for current song.
    /// </summary>
    List<(TimeSpan Time, string Text)> CurrentSyncedLines { get; }

    /// <summary>
    /// Gets the index of the current line based on playback position.
    /// </summary>
    int CurrentLineIndex { get; }

    /// <summary>
    /// Indicates whether lyrics are currently loading.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Fired when lyrics are loaded or updated.
    /// </summary>
    event EventHandler? LyricsUpdated;

    /// <summary>
    /// Fired when loading state changes.
    /// </summary>
    event EventHandler? LoadingStateChanged;

    /// <summary>
    /// Fired when current line index changes.
    /// </summary>
    event EventHandler? CurrentLineChanged;

    /// <summary>
    /// Fetch lyrics for the given queue item asynchronously.
    /// </summary>
    Task FetchLyricsAsync(PlayerQueueItem item, CancellationToken ct = default);

    /// <summary>
    /// Prefetch lyrics for the next queue item (if available).
    /// </summary>
    Task PrefetchNextLyricsAsync(PlayerQueueItem? nextItem, CancellationToken ct = default);

    /// <summary>
    /// Update current playback position to update current line index.
    /// </summary>
    void UpdatePlaybackPosition(TimeSpan currentPosition);

    /// <summary>
    /// Clear all cached lyrics.
    /// </summary>
    void ClearCache();
}
