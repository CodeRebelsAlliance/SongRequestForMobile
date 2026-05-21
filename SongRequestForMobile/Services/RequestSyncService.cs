using System.Collections.Concurrent;
using System.Text.Json;
using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public interface IRequestSyncService
{
    IReadOnlyList<RequestDisplayItem> Items { get; }
    IReadOnlyList<RequestDisplayItem> ArchivedItems { get; }
    IReadOnlyList<RequestDisplayItem> BlacklistItems { get; }
    RequestSyncState State { get; }
    event EventHandler? Updated;
    event EventHandler<AutopilotQueueEventArgs>? NewItemsDetected;
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task ClearArchiveAsync(CancellationToken cancellationToken = default);
    Task DeleteFromDeviceAsync(string videoId, CancellationToken cancellationToken = default);
}

public sealed class AutopilotQueueEventArgs : EventArgs
{
    public List<RequestDisplayItem> NewItems { get; set; } = new();
}

public sealed class RequestSyncService : IRequestSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly AppState _appState;
    private readonly ServerApiClient _serverApiClient;
    private readonly IYouTubeCookieStore _cookieStore;
    private readonly YoutubeService _youtubeService;
    private readonly string _cacheFolder = Path.Combine(FileSystem.AppDataDirectory, "request-cache");
    private readonly string _itemsCachePath;
    private readonly ConcurrentDictionary<string, RequestDisplayItem> _itemsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly RequestSyncState _state = new();
    private readonly HashSet<string> _blacklistIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _previouslySyncedIds = new(StringComparer.OrdinalIgnoreCase);

    public RequestSyncService(AppState appState, ServerApiClient serverApiClient, IYouTubeCookieStore cookieStore)
    {
        _appState = appState;
        _serverApiClient = serverApiClient;
        _cookieStore = cookieStore;
        _youtubeService = new YoutubeService();
        Directory.CreateDirectory(_cacheFolder);
        _itemsCachePath = Path.Combine(_cacheFolder, "requests.json");
        LoadCache();
    }

    public IReadOnlyList<RequestDisplayItem> Items => _itemsById.Values.Where(x => x.IsOnServer).OrderByDescending(x => x.Time).ToList();
    public IReadOnlyList<RequestDisplayItem> ArchivedItems => _itemsById.Values.Where(x => !x.IsOnServer).OrderByDescending(x => x.Time).ToList();
    public IReadOnlyList<RequestDisplayItem> BlacklistItems => _blacklistIds.Select(id => _itemsById.TryGetValue(id, out var it) ? it : new RequestDisplayItem { VideoId = id, Title = id, IsOnServer = false }).ToList();
    public RequestSyncState State => _state;
    public event EventHandler? Updated;
    public event EventHandler<AutopilotQueueEventArgs>? NewItemsDetected;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            _appState.YoutubeCookies = await _cookieStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            _serverApiClient.Configure(_appState.Settings.ServerBaseUrl, _appState.Settings.BearerToken);

            var rows = await _serverApiClient.GetSentInSongsAsync(cancellationToken).ConfigureAwait(false);
            _state.IsUnauthorized = false;
            _state.LastSyncUtc = DateTimeOffset.UtcNow;
            _state.StatusText = rows.Count == 0 ? "No requests yet." : $"Synced {rows.Count} request(s).";

            var serverIds = new HashSet<string>(rows.Select(r => r.VideoId), StringComparer.OrdinalIgnoreCase);
            var newItems = new List<RequestDisplayItem>();

            // Ensure items that are on server are present and marked
            foreach (var row in rows)
            {
                var isNew = !_previouslySyncedIds.Contains(row.VideoId);
                var item = _itemsById.GetOrAdd(row.VideoId, _ => new RequestDisplayItem
                {
                    VideoId = row.VideoId,
                    Message = row.Message,
                    IsApproved = row.IsApproved,
                    Time = row.Time,
                    IsDownloading = true,
                    IsOnServer = true
                });

                item.Message = row.Message;
                item.IsApproved = row.IsApproved;
                item.Time = row.Time;
                item.IsDownloading = true;
                item.IsOnServer = true;

                if (isNew)
                {
                    _previouslySyncedIds.Add(row.VideoId);
                    newItems.Add(item);
                }
            }

            // Any cached items not in serverIds become archived (IsOnServer=false)
            foreach (var kv in _itemsById)
            {
                if (!serverIds.Contains(kv.Key))
                {
                    kv.Value.IsOnServer = false;
                }
            }

            // Fetch blacklist from server and store ids
            try
            {
                var blacklist = await _serverApiClient.GetBlacklistAsync(cancellationToken).ConfigureAwait(false);
                _blacklistIds.Clear();
                foreach (var id in blacklist) _blacklistIds.Add(id);
            }
            catch
            {
                // ignore blacklist fetch errors - not critical for main list
            }

            await CacheAndEnrichAsync(rows, cancellationToken).ConfigureAwait(false);
            SaveCache();
            Updated?.Invoke(this, EventArgs.Empty);

            // Notify about new items if autopilot is enabled
            if (_appState.Settings.AutopilotMode && newItems.Count > 0)
            {
                NewItemsDetected?.Invoke(this, new AutopilotQueueEventArgs { NewItems = newItems });
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase))
        {
            _state.IsUnauthorized = true;
            _state.StatusText = "Unauthorized - update token in Settings.";
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _state.StatusText = $"Sync failed: {ex.Message}";
            Updated?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public async Task ClearArchiveAsync(CancellationToken cancellationToken = default)
    {
        if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var archived = ArchivedItems.ToList();
            foreach (var item in archived)
            {
                await DeleteCachedItemAsync(item, cancellationToken).ConfigureAwait(false);
                _itemsById.TryRemove(item.VideoId, out _);
            }

            SaveCache();
            Updated?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public async Task DeleteFromDeviceAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId)) return;
        if (!await _syncGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            if (_itemsById.TryRemove(videoId, out var item))
            {
                await DeleteCachedItemAsync(item, cancellationToken).ConfigureAwait(false);
                SaveCache();
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task DeleteCachedItemAsync(RequestDisplayItem item, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = GetMetadataPath(item.VideoId);
            var audio = GetAudioPath(item.VideoId);
            if (File.Exists(metadata)) File.Delete(metadata);
            if (File.Exists(audio)) File.Delete(audio);
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task CacheAndEnrichAsync(IReadOnlyList<ServerRequestRow> rows, CancellationToken cancellationToken)
    {
        var cookies = await _cookieStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var youtubeService = new YoutubeService(cookies);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = _itemsById[row.VideoId];
            var metadataPath = GetMetadataPath(row.VideoId);

            if (File.Exists(metadataPath))
            {
                try
                {
                    var cached = JsonSerializer.Deserialize<RequestDisplayItem>(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false), JsonOptions);
                    if (cached != null)
                    {
                        MergeCachedItem(item, cached);
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(item.Title))
            {
                try
                {
                    var metadata = await youtubeService.GetVideoMetadataAsync($"https://www.youtube.com/watch?v={row.VideoId}").ConfigureAwait(false);
                    item.Title = metadata.Title;
                    item.Channel = metadata.Creator;
                    item.Duration = metadata.Length;
                    item.Thumbnail = await youtubeService.GetThumbnailUrlAsync($"https://www.youtube.com/watch?v={row.VideoId}").ConfigureAwait(false);
                    await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(item, JsonOptions), cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    item.Title = row.VideoId;
                }
            }

            var localFilePath = GetAudioPath(row.VideoId);
            if (!File.Exists(localFilePath))
            {
                try
                {
                    item.IsDownloading = true;
                    localFilePath = await youtubeService.DownloadVideoAsync($"https://www.youtube.com/watch?v={row.VideoId}", _cacheFolder).ConfigureAwait(false);
                }
                catch
                {
                }
            }

            item.LocalFilePath = localFilePath;
            item.IsDownloading = false;
        }
    }

    private void MergeCachedItem(RequestDisplayItem target, RequestDisplayItem source)
    {
        if (!string.IsNullOrWhiteSpace(source.Title)) target.Title = source.Title;
        if (!string.IsNullOrWhiteSpace(source.Channel)) target.Channel = source.Channel;
        if (!string.IsNullOrWhiteSpace(source.Thumbnail)) target.Thumbnail = source.Thumbnail;
        if (!string.IsNullOrWhiteSpace(source.LocalFilePath)) target.LocalFilePath = source.LocalFilePath;
    }

    private void LoadCache()
    {
        if (!File.Exists(_itemsCachePath))
        {
            return;
        }

        try
        {
            var cached = JsonSerializer.Deserialize<List<RequestDisplayItem>>(File.ReadAllText(_itemsCachePath), JsonOptions);
            if (cached == null)
            {
                return;
            }

            foreach (var item in cached)
            {
                _itemsById[item.VideoId] = item;
            }
        }
        catch
        {
        }
    }

    private void SaveCache()
    {
        try
        {
            File.WriteAllText(_itemsCachePath, JsonSerializer.Serialize(_itemsById.Values, JsonOptions));
        }
        catch
        {
        }
    }

    private string GetMetadataPath(string videoId) => Path.Combine(_cacheFolder, $"{videoId}.json");
    private string GetAudioPath(string videoId) => Path.Combine(_cacheFolder, $"{videoId}.mp3");
}
