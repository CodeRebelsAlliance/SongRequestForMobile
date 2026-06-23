namespace SongRequestForMobile.Services;

public interface IYouTubeCookieProvider
{
    Task<IReadOnlyList<System.Net.Cookie>> CaptureCookiesAsync(Microsoft.Maui.Controls.WebView webView, string currentUrl, CancellationToken cancellationToken = default);
}
