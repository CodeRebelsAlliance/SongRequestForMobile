namespace SongRequestForMobile.Services;

public sealed class PlaybackStateStore : IPlaybackStateStore
{
    private const string LastDownloadedFilePathKey = "last_downloaded_file_path";

    public string? LoadLastDownloadedFilePath()
    {
        return Preferences.Default.Get(LastDownloadedFilePathKey, string.Empty);
    }

    public void SaveLastDownloadedFilePath(string? filePath)
    {
        Preferences.Default.Set(LastDownloadedFilePathKey, filePath ?? string.Empty);
    }
}
