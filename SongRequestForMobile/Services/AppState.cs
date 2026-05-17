using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public sealed class AppState
{
    private readonly IPlaybackStateStore _playbackStateStore;

    public AppState(IPlaybackStateStore playbackStateStore)
    {
        _playbackStateStore = playbackStateStore;
        LastDownloadedFilePath = _playbackStateStore.LoadLastDownloadedFilePath();
    }

    public AppSettings Settings { get; set; } = new();
    public IReadOnlyList<System.Net.Cookie> YoutubeCookies { get; set; } = Array.Empty<System.Net.Cookie>();
    public string? LastDownloadedFilePath { get; private set; }

    public void SetLastDownloadedFilePath(string? filePath)
    {
        LastDownloadedFilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
        _playbackStateStore.SaveLastDownloadedFilePath(LastDownloadedFilePath);
    }
}
