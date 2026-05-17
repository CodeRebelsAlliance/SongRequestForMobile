using System.Net;

namespace SongRequestForMobile.Services;

public interface IYouTubeCookieStore
{
    Task<IReadOnlyList<Cookie>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<Cookie> cookies, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
