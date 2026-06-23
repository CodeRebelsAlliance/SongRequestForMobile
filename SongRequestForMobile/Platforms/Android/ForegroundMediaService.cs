using System.Drawing;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using NAudio.Wave;
using SongRequestForMobile.Services;
using PlaybackStateCode = Android.Media.Session.PlaybackStateCode;
using AndroidPlaybackState = Android.Media.Session.PlaybackState;

namespace SongRequestForMobile.Platforms.Android;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback, Exported = false)]
public class ForegroundMediaService : Service
{
    private const string ChannelId = "media_playback";
    private const int NotificationId = 1001;

    private MediaSession? _mediaSession;
    private MediaMetadata? _currentMetadata;
    private PlaybackStateCode _playbackState = PlaybackStateCode.None;

    public const string ActionUpdateMetadata = "UPDATE_METADATA";
    public const string ActionUpdatePlayback = "UPDATE_PLAYBACK";
    public const string ActionStop = "STOP";
    public const string ActionPlay = "PLAY";
    public const string ActionPause = "PAUSE";
    public const string ActionPrevious = "PREVIOUS";
    public const string ActionNext = "NEXT";

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();
        CreateMediaSession();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action != null)
        {
            switch (intent.Action)
            {
                case ActionUpdateMetadata:
                    HandleUpdateMetadata(intent);
                    break;
                case ActionUpdatePlayback:
                    HandleUpdatePlayback(intent);
                    break;
                case ActionStop:
                    Stop();
                    break;
                case ActionPlay:
                case "android.intent.action.MEDIA_BUTTON":
                    SystemMediaControlService.RaiseTogglePlayPause();
                    break;
                case ActionPause:
                    SystemMediaControlService.RaisePause();
                    break;
                case ActionPrevious:
                    SystemMediaControlService.RaiseSkipPrevious();
                    break;
                case ActionNext:
                    SystemMediaControlService.RaiseSkipNext();
                    break;
                default:
                    StartForeground(NotificationId, BuildNotification());
                    break;
            }
        }
        else
        {
            StartForeground(NotificationId, BuildNotification());
        }

        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        _mediaSession?.Dispose();
        _mediaSession = null;
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Media Playback", NotificationImportance.Low)
            {
                Description = "Controls for media playback"
            };
            var manager = GetSystemService(NotificationService) as NotificationManager;
            manager?.CreateNotificationChannel(channel);
        }
    }

    private void CreateMediaSession()
    {
        _mediaSession = new MediaSession(this, "SongRequest");
        _mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls);
        _mediaSession.SetCallback(new MediaSessionCallback());
        _mediaSession.Active = true;
    }

    private void HandleUpdateMetadata(Intent intent)
    {
        var title = intent.GetStringExtra("title") ?? "Unknown";
        var artist = intent.GetStringExtra("artist") ?? "Unknown";
        var thumbnailUrl = intent.GetStringExtra("thumbnailUrl");
        var durationMs = intent.GetLongExtra("durationMs", 0L);

        var builder = new MediaMetadata.Builder();
        builder.PutString(MediaMetadata.MetadataKeyTitle, title);
        builder.PutString(MediaMetadata.MetadataKeyArtist, artist);
        builder.PutLong(MediaMetadata.MetadataKeyDuration, durationMs);

        if (!string.IsNullOrEmpty(thumbnailUrl))
        {
            _ = LoadThumbnailAsync(thumbnailUrl, bitmap =>
            {
                if (bitmap != null)
                {
                    builder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, bitmap);
                    _currentMetadata = builder.Build();
                    _mediaSession?.SetMetadata(_currentMetadata);
                    UpdateNotification();
                }
            });
        }

        _currentMetadata = builder.Build();
        _mediaSession?.SetMetadata(_currentMetadata);

        if (_mediaSession?.Active != true)
        {
            _mediaSession!.Active = true;
            StartForeground(NotificationId, BuildNotification());
        }
        else
        {
            UpdateNotification();
        }
    }

    private void HandleUpdatePlayback(Intent intent)
    {
        var isPlaying = intent.GetBooleanExtra("isPlaying", false);
        var positionMs = intent.GetLongExtra("positionMs", 0L);

        var stateBuilder = new AndroidPlaybackState.Builder();
        stateBuilder.SetActions(
            AndroidPlaybackState.ActionPlay |
            AndroidPlaybackState.ActionPause |
            AndroidPlaybackState.ActionSkipToNext |
            AndroidPlaybackState.ActionSkipToPrevious |
            AndroidPlaybackState.ActionSeekTo |
            AndroidPlaybackState.ActionStop);

        stateBuilder.SetState(
            isPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused,
            positionMs, 1.0f);

        _playbackState = isPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused;
        _mediaSession?.SetPlaybackState(stateBuilder.Build());

        UpdateNotification();
    }

    private void Stop()
    {
        _mediaSession?.Active = false;
        StopForeground(StopForegroundFlags.Remove);
        StopSelfResult((int)StartCommandResult.Sticky);
    }

    private Notification BuildNotification()
    {
        var title = _currentMetadata?.GetString(MediaMetadata.MetadataKeyTitle) ?? "SongRequest";
        var artist = _currentMetadata?.GetString(MediaMetadata.MetadataKeyArtist) ?? "";
        var albumArt = _currentMetadata?.GetBitmap(MediaMetadata.MetadataKeyAlbumArt);

        var playIcon = _playbackState == PlaybackStateCode.Playing
            ? global::Android.Resource.Drawable.IcMediaPause
            : global::Android.Resource.Drawable.IcMediaPlay;

        var notification = BuildBaseNotification(title, artist, albumArt);

        var prevIntent = CreateMediaPendingIntent(global::Android.Resource.Drawable.IcMediaPrevious, "Previous", ActionPrevious);
        var playIntent = CreateMediaPendingIntent(playIcon,
            _playbackState == PlaybackStateCode.Playing ? "Pause" : "Play", ActionPlay);
        var nextIntent = CreateMediaPendingIntent(global::Android.Resource.Drawable.IcMediaNext, "Next", ActionNext);

        notification.AddAction(prevIntent.Item1, prevIntent.Item2, prevIntent.Item3);
        notification.AddAction(playIntent.Item1, playIntent.Item2, playIntent.Item3);
        notification.AddAction(nextIntent.Item1, nextIntent.Item2, nextIntent.Item3);

        return notification.Build();
    }

    private Notification.Builder BuildBaseNotification(string title, string artist, Bitmap? albumArt)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var builder = new Notification.Builder(this, ChannelId)
                .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
                .SetContentTitle(title)
                .SetContentText(artist)
                .SetOngoing(_playbackState == PlaybackStateCode.Playing)
                .SetShowWhen(false)
                .SetDeleteIntent(CreateStopPendingIntent());

            if (albumArt != null)
                builder.SetLargeIcon(albumArt);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                var style = new Notification.MediaStyle()
                    .SetMediaSession(_mediaSession?.SessionToken);
                builder.SetStyle(style);
            }

            return builder;
        }

#pragma warning disable CA1422
        var legacy = new Notification.Builder(this)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle(title)
            .SetContentText(artist)
            .SetOngoing(_playbackState == PlaybackStateCode.Playing)
            .SetShowWhen(false)
            .SetDeleteIntent(CreateStopPendingIntent());
#pragma warning restore CA1422

        if (albumArt != null)
            legacy.SetLargeIcon(albumArt);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            var style = new Notification.MediaStyle()
                .SetMediaSession(_mediaSession?.SessionToken);
            legacy.SetStyle(style);
        }

        return legacy;
    }

    private void UpdateNotification()
    {
        var mgr = GetSystemService(NotificationService) as NotificationManager;
        if (mgr == null) return;

        mgr.Notify(NotificationId, BuildNotification());
    }

    private PendingIntent CreateStopPendingIntent()
    {
        var intent = new Intent(this, typeof(ForegroundMediaService));
        intent.SetAction(ActionStop);
        var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
        return PendingIntent.GetService(this, 0, intent, flags);
    }

    private (int, string, PendingIntent) CreateMediaPendingIntent(int iconResId, string title, string action)
    {
        var intent = new Intent(this, typeof(ForegroundMediaService));
        intent.SetAction(action);
        var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
        var pi = PendingIntent.GetService(this, action.GetHashCode(), intent, flags);
        return (iconResId, title, pi);
    }

    private static async Task LoadThumbnailAsync(string url, Action<Bitmap?> callback)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            var bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
            if (bitmap != null)
            {
                var maxSize = 512;
                var scale = Math.Min(1.0, maxSize / (double)Math.Max(bitmap.Width, bitmap.Height));
                if (scale < 1.0)
                {
                    var scaled = Bitmap.CreateScaledBitmap(bitmap,
                        (int)(bitmap.Width * scale), (int)(bitmap.Height * scale), true);
                    bitmap.Recycle();
                    bitmap = scaled;
                }
            }
            callback(bitmap);
        }
        catch
        {
            callback(null);
        }
    }

    private class MediaSessionCallback : MediaSession.Callback
    {
        public override void OnPlay() =>
            SystemMediaControlService.RaisePlay();

        public override void OnPause() =>
            SystemMediaControlService.RaisePause();

        public override void OnSkipToNext() =>
            SystemMediaControlService.RaiseSkipNext();

        public override void OnSkipToPrevious() =>
            SystemMediaControlService.RaiseSkipPrevious();

        public override void OnSeekTo(long pos) =>
            SystemMediaControlService.RaiseSeekTo(TimeSpan.FromMilliseconds(pos));

        public override void OnStop() =>
            SystemMediaControlService.RaisePause();
    }
}
