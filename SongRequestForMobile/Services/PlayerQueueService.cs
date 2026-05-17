using System.Collections.ObjectModel;
using Plugin.Maui.Audio;
using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public interface IPlayerQueueService
{
    ObservableCollection<PlayerQueueItem> Queue { get; }
    PlayerQueueItem? CurrentItem { get; }
    bool IsPlaying { get; }
    bool IsPaused { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan Duration { get; }
    Color AccentColor { get; }
    event EventHandler? Updated;
    void Enqueue(PlayerQueueItem item);
    Task PlayNextAsync(PlayerQueueItem item);
    Task PlayNowReplaceQueueAsync(PlayerQueueItem item);
    Task PlayCurrentQueueAsync();
    Task SkipNextAsync();
    Task PlayPreviousAsync();
    Task TogglePlayPauseAsync();
    Task PauseAsync();
    Task ResumeAsync();
    void Stop();
    void Remove(PlayerQueueItem item);
    void MoveUp(PlayerQueueItem item);
    void MoveDown(PlayerQueueItem item);
    void ClearQueue();
    Task SeekAsync(TimeSpan position);
    Task TickAsync();
}

public sealed class PlayerQueueService : IPlayerQueueService, IDisposable
{
    private readonly IAudioManager _audioManager;
    private readonly IThumbnailColorService _thumbnailColorService;
    private IAudioPlayer? _audioPlayer;
    private Stream? _audioStream;
    private readonly List<PlayerQueueItem> _history = new();
    private bool _manualPause;

    public PlayerQueueService(IThumbnailColorService thumbnailColorService)
    {
        _thumbnailColorService = thumbnailColorService;
        _audioManager = AudioManager.Current;
        Queue = new ObservableCollection<PlayerQueueItem>();
    }

    public ObservableCollection<PlayerQueueItem> Queue { get; }
    public PlayerQueueItem? CurrentItem { get; private set; }
    public bool IsPlaying => _audioPlayer?.IsPlaying == true;
    public bool IsPaused => _manualPause;
    public TimeSpan CurrentPosition => TimeSpan.FromSeconds(_audioPlayer?.CurrentPosition ?? 0);
    public TimeSpan Duration => TimeSpan.FromSeconds(_audioPlayer?.Duration ?? 0);
    public Color AccentColor => _thumbnailColorService.GetAccentColor(CurrentItem?.AccentKey ?? CurrentItem?.Thumbnail ?? CurrentItem?.VideoId);

    public event EventHandler? Updated;

    public void Enqueue(PlayerQueueItem item)
    {
        Queue.Add(Clone(item));
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public async Task PlayNextAsync(PlayerQueueItem item)
    {
        var clone = Clone(item);
        if (CurrentItem == null && _audioPlayer?.IsPlaying != true && Queue.Count == 0)
        {
            await PlayItemAsync(clone).ConfigureAwait(false);
            return;
        }

        Queue.Insert(0, clone);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public async Task PlayNowReplaceQueueAsync(PlayerQueueItem item)
    {
        ClearQueue();
        _history.Clear();
        await PlayItemAsync(Clone(item), addCurrentToHistory: false).ConfigureAwait(false);
    }

    public async Task PlayCurrentQueueAsync()
    {
        if (CurrentItem != null && _audioPlayer?.IsPlaying == true)
        {
            return;
        }

        if (Queue.Count == 0)
        {
            Updated?.Invoke(this, EventArgs.Empty);
            return;
        }

        var next = Queue[0];
        Queue.RemoveAt(0);
        await PlayItemAsync(next).ConfigureAwait(false);
    }

    public async Task SkipNextAsync()
    {
        if (Queue.Count == 0)
        {
            StopInternal();
            CurrentItem = null;
            _manualPause = false;
            Updated?.Invoke(this, EventArgs.Empty);
            return;
        }

        var next = Queue[0];
        Queue.RemoveAt(0);
        await PlayItemAsync(next).ConfigureAwait(false);
    }

    public async Task PlayPreviousAsync()
    {
        if (_history.Count == 0)
        {
            return;
        }

        var previous = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        if (CurrentItem != null)
        {
            Queue.Insert(0, Clone(CurrentItem));
        }

        await PlayItemAsync(previous, addCurrentToHistory: false).ConfigureAwait(false);
    }

    public Task TogglePlayPauseAsync()
    {
        if (_audioPlayer?.IsPlaying == true && !_manualPause)
        {
            return PauseAsync();
        }

        return ResumeAsync();
    }

    public Task PauseAsync()
    {
        if (_audioPlayer == null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _audioPlayer.Pause();
            _manualPause = true;
        }
        catch
        {
        }

        Updated?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        if (_audioPlayer == null)
        {
            return PlayCurrentQueueAsync();
        }

        try
        {
            _audioPlayer.Play();
            _manualPause = false;
        }
        catch
        {
        }

        Updated?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        StopInternal();
        _manualPause = false;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(PlayerQueueItem item)
    {
        var target = Queue.FirstOrDefault(x => SameItem(x, item));
        if (target != null)
        {
            Queue.Remove(target);
            Updated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void MoveUp(PlayerQueueItem item)
    {
        var index = IndexOf(item);
        if (index <= 0)
        {
            return;
        }

        Queue.Move(index, index - 1);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void MoveDown(PlayerQueueItem item)
    {
        var index = IndexOf(item);
        if (index < 0 || index >= Queue.Count - 1)
        {
            return;
        }

        Queue.Move(index, index + 1);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void ClearQueue()
    {
        Queue.Clear();
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public Task SeekAsync(TimeSpan position)
    {
        try
        {
            _audioPlayer?.Seek(position.TotalSeconds);
        }
        catch
        {
        }

        Updated?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task TickAsync()
    {
        if (_audioPlayer == null || CurrentItem == null)
        {
            return;
        }

        if (_audioPlayer.IsPlaying)
        {
            Updated?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_manualPause)
        {
            Updated?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Duration > TimeSpan.Zero && CurrentPosition >= Duration - TimeSpan.FromMilliseconds(800))
        {
            await SkipNextAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        StopInternal();
        _audioStream?.Dispose();
        _audioStream = null;
    }

    private async Task PlayItemAsync(PlayerQueueItem item, bool addCurrentToHistory = true)
    {
        if (string.IsNullOrWhiteSpace(item.LocalFilePath) || !File.Exists(item.LocalFilePath))
        {
            Updated?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (CurrentItem != null && addCurrentToHistory)
        {
            _history.Add(Clone(CurrentItem));
        }

        StopInternal();
        CurrentItem = Clone(item);
        _manualPause = false;
        _audioStream = File.OpenRead(item.LocalFilePath);
        _audioPlayer = _audioManager.CreatePlayer(_audioStream);
        _audioPlayer.Play();
        Updated?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask;
    }

    private void StopInternal()
    {
        try
        {
            _audioPlayer?.Stop();
        }
        catch
        {
        }

        _audioPlayer?.Dispose();
        _audioPlayer = null;
        _audioStream?.Dispose();
        _audioStream = null;
    }

    private int IndexOf(PlayerQueueItem item)
    {
        for (var i = 0; i < Queue.Count; i++)
        {
            if (SameItem(Queue[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool SameItem(PlayerQueueItem left, PlayerQueueItem right)
        => string.Equals(left.VideoId, right.VideoId, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(left.LocalFilePath, right.LocalFilePath, StringComparison.OrdinalIgnoreCase);

    private static PlayerQueueItem Clone(PlayerQueueItem item) => new()
    {
        VideoId = item.VideoId,
        Title = item.Title,
        Channel = item.Channel,
        Thumbnail = item.Thumbnail,
        LocalFilePath = item.LocalFilePath,
        Message = item.Message,
        Time = item.Time,
        AccentKey = item.AccentKey
    };
}
