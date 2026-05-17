namespace SongRequestForMobile.Services;

public sealed class DefaultYouTubeCookieProvider : IYouTubeCookieProvider
{
    public Task<IReadOnlyList<System.Net.Cookie>> CaptureCookiesAsync(string loginUrl, Func<string?, Task<bool>> isReadyAsync, CancellationToken cancellationToken = default)
    {
        return Task.FromException<IReadOnlyList<System.Net.Cookie>>(new PlatformNotSupportedException("YouTube cookie capture is only implemented for Android in this MVP."));
    }
}
