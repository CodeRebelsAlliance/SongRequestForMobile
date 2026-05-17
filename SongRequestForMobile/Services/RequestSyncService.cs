using System.Collections.Concurrent;
using System.Text.Json;
using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public interface IRequestSyncService
{
    IReadOnlyList<RequestDisplayItem> Items { get; }
    RequestSyncState State { get; }
    event EventHandler? Updated;
    Task RefreshAsync(CancellationToken cancellationToken = default);
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

    public IReadOnlyList<RequestDisplayItem> Items => _itemsById.Values.OrderByDescending(x => x.Time).ToList();
    public RequestSyncState State => _state;
    public event EventHandler? Updated;

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

            foreach (var row in rows)
            {
                var item = _itemsById.GetOrAdd(row.VideoId, _ => new RequestDisplayItem
                {
                    VideoId = row.VideoId,
                    Message = row.Message,
                    IsApproved = row.IsApproved,
                    Time = row.Time,
                    IsDownloading = true
                });

                item.Message = row.Message;
                item.IsApproved = row.IsApproved;
                item.Time = row.Time;
                item.IsDownloading = true;
            }

            await CacheAndEnrichAsync(rows, cancellationToken).ConfigureAwait(false);
            SaveCache();
            Updated?.Invoke(this, EventArgs.Empty);
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
