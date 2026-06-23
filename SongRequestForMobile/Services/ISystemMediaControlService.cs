namespace SongRequestForMobile.Services;

public interface ISystemMediaControlService
{
    void UpdateMetadata(string title, string artist, string? thumbnailUrl, TimeSpan duration);
    void UpdatePlaybackState(bool isPlaying, TimeSpan position, TimeSpan duration);
    void ClearAll();

    event EventHandler? PlayPressed;
    event EventHandler? PausePressed;
    event EventHandler? TogglePlayPausePressed;
    event EventHandler? SkipNextPressed;
    event EventHandler? SkipPreviousPressed;
    event EventHandler<double>? SeekTo;
}
