using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public interface IQueueItemFactory
{
    PlayerQueueItem FromRequest(RequestDisplayItem request);
}

public sealed class QueueItemFactory : IQueueItemFactory
{
    public PlayerQueueItem FromRequest(RequestDisplayItem request) => new()
    {
        VideoId = request.VideoId,
        Title = request.Title,
        Channel = request.Channel,
        Thumbnail = request.Thumbnail,
        LocalFilePath = request.LocalFilePath,
        Message = request.Message,
        Time = request.Time,
        AccentKey = request.Thumbnail
    };
}
