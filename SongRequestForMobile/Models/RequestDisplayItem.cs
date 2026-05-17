namespace SongRequestForMobile.Models;

public sealed class RequestDisplayItem
{
    public string VideoId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public string LocalFilePath { get; set; } = string.Empty;
    public bool IsDownloading { get; set; }
    public bool IsCached => !string.IsNullOrWhiteSpace(LocalFilePath) && File.Exists(LocalFilePath);
}
