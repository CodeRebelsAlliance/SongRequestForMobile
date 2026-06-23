using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public sealed class SystemMediaControlService : ISystemMediaControlService, IDisposable
{
    private bool _disposed;

    public event EventHandler? PlayPressed;
    public event EventHandler? PausePressed;
    public event EventHandler? TogglePlayPausePressed;
    public event EventHandler? SkipNextPressed;
    public event EventHandler? SkipPreviousPressed;
    public event EventHandler<double>? SeekTo;

#if ANDROID
    private static SystemMediaControlService? _instance;

    private readonly Android.Content.Context _context;
    private bool _serviceStarted;

    public SystemMediaControlService()
    {
        _instance = this;
        _context = global::Android.App.Application.Context;
    }

    public void UpdateMetadata(string title, string artist, string? thumbnailUrl, TimeSpan duration)
    {
        var intent = new Android.Content.Intent(_context, typeof(Platforms.Android.ForegroundMediaService));
        intent.SetAction(Platforms.Android.ForegroundMediaService.ActionUpdateMetadata);
        intent.PutExtra("title", title);
        intent.PutExtra("artist", artist);
        intent.PutExtra("thumbnailUrl", thumbnailUrl ?? "");
        intent.PutExtra("durationMs", (long)duration.TotalMilliseconds);

        if (!_serviceStarted)
        {
            _serviceStarted = true;
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                _context.StartForegroundService(intent);
            }
            else
            {
                _context.StartService(intent);
            }
        }
        else
        {
            _context.StartService(intent);
        }
    }

    public void UpdatePlaybackState(bool isPlaying, TimeSpan position, TimeSpan duration)
    {
        if (!_serviceStarted) return;

        var intent = new Android.Content.Intent(_context, typeof(Platforms.Android.ForegroundMediaService));
        intent.SetAction(Platforms.Android.ForegroundMediaService.ActionUpdatePlayback);
        intent.PutExtra("isPlaying", isPlaying);
        intent.PutExtra("positionMs", (long)position.TotalMilliseconds);
        _context.StartService(intent);
    }

    public void ClearAll()
    {
        if (!_serviceStarted) return;
        _serviceStarted = false;

        var intent = new Android.Content.Intent(_context, typeof(Platforms.Android.ForegroundMediaService));
        intent.SetAction(Platforms.Android.ForegroundMediaService.ActionStop);
        _context.StartService(intent);
    }

    internal static void RaisePlay() =>
        _instance?.PlayPressed?.Invoke(_instance, EventArgs.Empty);

    internal static void RaiseTogglePlayPause() =>
        _instance?.TogglePlayPausePressed?.Invoke(_instance, EventArgs.Empty);

    internal static void RaisePause() =>
        _instance?.PausePressed?.Invoke(_instance, EventArgs.Empty);

    internal static void RaiseSkipNext() =>
        _instance?.SkipNextPressed?.Invoke(_instance, EventArgs.Empty);

    internal static void RaiseSkipPrevious() =>
        _instance?.SkipPreviousPressed?.Invoke(_instance, EventArgs.Empty);

    internal static void RaiseSeekTo(TimeSpan position) =>
        _instance?.SeekTo?.Invoke(_instance, position.TotalSeconds);

#elif IOS
    private bool _commandsRegistered;

    public SystemMediaControlService()
    {
    }

    public void UpdateMetadata(string title, string artist, string? thumbnailUrl, TimeSpan duration)
    {
        RegisterCommands();

        var nowPlayingInfo = new MediaPlayer.MPNowPlayingInfo();

        nowPlayingInfo.Title = title;
        nowPlayingInfo.Artist = artist;
        nowPlayingInfo.PlaybackDuration = duration.TotalSeconds;

        if (!string.IsNullOrEmpty(thumbnailUrl))
        {
            _ = LoadArtworkAsync(thumbnailUrl, artwork =>
            {
                if (artwork != null)
                {
                    nowPlayingInfo.Artwork = artwork;
                    MediaPlayer.MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
                }
            });
        }

        MediaPlayer.MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
    }

    public void UpdatePlaybackState(bool isPlaying, TimeSpan position, TimeSpan duration)
    {
        var nowPlayingInfo = MediaPlayer.MPNowPlayingInfoCenter.DefaultCenter.NowPlaying 
            ?? new MediaPlayer.MPNowPlayingInfo();

        nowPlayingInfo.ElapsedPlaybackTime = position.TotalSeconds;
        nowPlayingInfo.PlaybackRate = isPlaying ? 1.0 : 0.0;

        MediaPlayer.MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlayingInfo;
    }

    public void ClearAll()
    {
        MediaPlayer.MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null;
        UnregisterCommands();
    }

    private void RegisterCommands()
    {
        if (_commandsRegistered) return;
        _commandsRegistered = true;

        var center = MediaPlayer.MPRemoteCommandCenter.Shared;

        center.PlayCommand.Enabled = true;
        center.PlayCommand.AddTarget(OnRemotePlay);

        center.PauseCommand.Enabled = true;
        center.PauseCommand.AddTarget(OnRemotePause);

        center.TogglePlayPauseCommand.Enabled = true;
        center.TogglePlayPauseCommand.AddTarget(OnRemoteTogglePlayPause);

        center.NextTrackCommand.Enabled = true;
        center.NextTrackCommand.AddTarget(OnRemoteNextTrack);

        center.PreviousTrackCommand.Enabled = true;
        center.PreviousTrackCommand.AddTarget(OnRemotePreviousTrack);

        center.ChangePlaybackPositionCommand.Enabled = true;
        center.ChangePlaybackPositionCommand.AddTarget(OnRemoteChangePlaybackPosition);
    }

    private void UnregisterCommands()
    {
        if (!_commandsRegistered) return;
        _commandsRegistered = false;

        var center = MediaPlayer.MPRemoteCommandCenter.Shared;
        center.PlayCommand.RemoveTarget(null);
        center.PauseCommand.RemoveTarget(null);
        center.TogglePlayPauseCommand.RemoveTarget(null);
        center.NextTrackCommand.RemoveTarget(null);
        center.PreviousTrackCommand.RemoveTarget(null);
        center.ChangePlaybackPositionCommand.RemoveTarget(null);
    }

    private MediaPlayer.MPRemoteCommandHandlerStatus OnRemotePlay(MediaPlayer.MPRemoteCommandEvent _)
    {
        PlayPressed?.Invoke(this, EventArgs.Empty);
        return MediaPlayer.MPRemoteCommandHandlerStatus.Success;
    }

    private MediaPlayer.MPRemoteCommandHandlerStatus OnRemotePause(MediaPlayer.MPRemoteCommandEvent _)
    {
        PausePressed?.Invoke(this, EventArgs.Empty);
        return MediaPlayer.MPRemoteCommandHandlerStatus.Success;
    }

    private MediaPlayer.MPRemoteCommandHandlerStatus OnRemoteTogglePlayPause(MediaPlayer.MPRemoteCommandEvent _)
    {
        TogglePlayPausePressed?.Invoke(this, EventArgs.Empty);
        return MediaPlayer.MPRemoteCommandHandlerStatus.Success;
    }

    private MediaPlayer.MPRemoteCommandHandlerStatus OnRemoteNextTrack(MediaPlayer.MPRemoteCommandEvent _)
    {
        SkipNextPressed?.Invoke(this, EventArgs.Empty);
        return MediaPlayer.MPRemoteCommandHandlerStatus.Success;
    }

    private MediaPlayer.MPRemoteCommandHandlerStatus OnRemotePreviousTrack(MediaPlayer.MPRemoteCommandEvent _)
    {
        SkipPreviousPressed?.Invoke(this, EventArgs.Empty);
        return MediaPlayer.MPRemoteCommandHandlerStatus.Success;
    }

    private MediaPlayer.MPRemoteCommandHandlerStatus OnRemoteChangePlaybackPosition(MediaPlayer.MPRemoteCommandEvent e)
    {
        if (e is MediaPlayer.MPChangePlaybackPositionCommandEvent positionEvent)
        {
            SeekTo?.Invoke(this, positionEvent.PositionTime);
        }
        return MediaPlayer.MPRemoteCommandHandlerStatus.Success;
    }

    private static async Task LoadArtworkAsync(string url, Action<MediaPlayer.MPMediaItemArtwork?> callback)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            var data = Foundation.NSData.FromArray(bytes);
            var image = UIKit.UIImage.LoadFromData(data);
            if (image != null)
            {
                var artwork = new MediaPlayer.MPMediaItemArtwork(
                    new CoreGraphics.CGSize(image.Size.Width, image.Size.Height),
                    _ => image);
                callback(artwork);
                return;
            }
        }
        catch { }

        callback(null);
    }

#elif WINDOWS
    private Windows.Media.SystemMediaTransportControls? _smtc;
    private Windows.Media.SystemMediaTransportControlsDisplayUpdater? _displayUpdater;

    public SystemMediaControlService()
    {
        try
        {
            _smtc = Windows.Media.SystemMediaTransportControls.GetForCurrentView();
            if (_smtc != null)
            {
                _smtc.ButtonPressed += OnSmtcButtonPressed;
                _smtc.IsPlayEnabled = true;
                _smtc.IsPauseEnabled = true;
                _smtc.IsNextEnabled = true;
                _smtc.IsPreviousEnabled = true;
                _smtc.IsStopEnabled = true;

                _displayUpdater = _smtc.DisplayUpdater;
                _displayUpdater.Type = Windows.Media.MediaPlaybackType.Music;
            }
        }
        catch
        {
            // Not supported (e.g., in some test environments)
        }
    }

    public void UpdateMetadata(string title, string artist, string? thumbnailUrl, TimeSpan duration)
    {
        if (_displayUpdater?.MusicProperties == null) return;

        _displayUpdater.MusicProperties.Title = title;
        _displayUpdater.MusicProperties.Artist = artist;
        _displayUpdater.MusicProperties.AlbumArtist = artist;
        _displayUpdater.Thumbnail = null;

        if (!string.IsNullOrEmpty(thumbnailUrl))
        {
            _ = LoadThumbnailAsync(thumbnailUrl);
        }

        _displayUpdater.Update();
    }

    public void UpdatePlaybackState(bool isPlaying, TimeSpan position, TimeSpan duration)
    {
        if (_smtc == null) return;

        _smtc.PlaybackStatus = isPlaying
            ? Windows.Media.MediaPlaybackStatus.Playing
            : Windows.Media.MediaPlaybackStatus.Paused;
    }

    public void ClearAll()
    {
        if (_displayUpdater != null)
        {
            _displayUpdater.MusicProperties.Title = "";
            _displayUpdater.MusicProperties.Artist = "";
            _displayUpdater.ClearAll();
        }

        if (_smtc != null)
        {
            _smtc.PlaybackStatus = Windows.Media.MediaPlaybackStatus.Stopped;
        }
    }

    private async Task LoadThumbnailAsync(string url)
    {
        try
        {
            if (_displayUpdater == null) return;

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);

            var tempFile = await Windows.Storage.ApplicationData.Current.TemporaryFolder
                .CreateFileAsync($"thumbnail_{Guid.NewGuid()}.jpg", Windows.Storage.CreationCollisionOption.ReplaceExisting);

            await Windows.Storage.FileIO.WriteBytesAsync(tempFile, bytes);

            _displayUpdater.Thumbnail = 
                Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(tempFile);
            _displayUpdater.Update();
        }
        catch { }
    }

    private void OnSmtcButtonPressed(Windows.Media.SystemMediaTransportControls sender,
        Windows.Media.SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        switch (args.Button)
        {
            case Windows.Media.SystemMediaTransportControlsButton.Play:
                PlayPressed?.Invoke(this, EventArgs.Empty);
                break;
            case Windows.Media.SystemMediaTransportControlsButton.Pause:
                PausePressed?.Invoke(this, EventArgs.Empty);
                break;
            case Windows.Media.SystemMediaTransportControlsButton.Next:
                SkipNextPressed?.Invoke(this, EventArgs.Empty);
                break;
            case Windows.Media.SystemMediaTransportControlsButton.Previous:
                SkipPreviousPressed?.Invoke(this, EventArgs.Empty);
                break;
            case Windows.Media.SystemMediaTransportControlsButton.Stop:
                PausePressed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }
#else
    // Fallback for other platforms - no-op
    public SystemMediaControlService() { }
    public void UpdateMetadata(string title, string artist, string? thumbnailUrl, TimeSpan duration) { }
    public void UpdatePlaybackState(bool isPlaying, TimeSpan position, TimeSpan duration) { }
    public void ClearAll() { }
#endif

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearAll();

#if WINDOWS
        if (_smtc != null)
        {
            _smtc.ButtonPressed -= OnSmtcButtonPressed;
            _smtc = null;
        }
#endif
    }
}
