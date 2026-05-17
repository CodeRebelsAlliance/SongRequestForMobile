namespace SongRequestForMobile.Models;

public sealed class PlayerQueueItem
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public string LocalFilePath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string AccentKey { get; set; } = string.Empty;
}
