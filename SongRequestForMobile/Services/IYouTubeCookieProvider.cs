namespace SongRequestForMobile.Services;

public interface IYouTubeCookieProvider
{
    Task<IReadOnlyList<System.Net.Cookie>> CaptureCookiesAsync(string loginUrl, Func<string?, Task<bool>> isReadyAsync, CancellationToken cancellationToken = default);
}
