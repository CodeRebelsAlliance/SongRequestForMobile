using System.Net;

namespace SongRequestForMobile.Services;

public sealed class YouTubeSession
{
    public IReadOnlyList<Cookie> Cookies { get; private set; } = Array.Empty<Cookie>();

    public void SetCookies(IReadOnlyList<Cookie> cookies)
    {
        Cookies = cookies?.ToArray() ?? Array.Empty<Cookie>();
    }

    public void ClearCookies()
    {
        Cookies = Array.Empty<Cookie>();
    }
}
