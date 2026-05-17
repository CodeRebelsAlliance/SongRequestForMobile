namespace SongRequestForMobile.Services;

public interface IPlaybackStateStore
{
    string? LoadLastDownloadedFilePath();
    void SaveLastDownloadedFilePath(string? filePath);
}
