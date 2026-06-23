namespace SongRequestForMobile.Services;

public interface IImageCacheService
{
    ImageSource? GetCached(string url);
    Task<ImageSource?> GetImageAsync(string url);
    void Preload(string url);
}
