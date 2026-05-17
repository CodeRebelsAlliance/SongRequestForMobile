using System.Net.Http.Headers;
using System.Text;

namespace SongRequestForMobile;

public sealed class ServerConnectionService
{
    private readonly HttpClient _httpClient;

    public ServerConnectionService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public string BaseUrl { get; private set; } = string.Empty;

    public string BearerToken { get; private set; } = string.Empty;

    public string HealthPath { get; set; } = "/";

    public void Configure(string? baseUrl, string? bearerToken)
    {
        BaseUrl = NormalizeBaseUrl(baseUrl);
        BearerToken = bearerToken?.Trim() ?? string.Empty;
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            return (false, "Server URL is not configured.");
        }

        try
        {
            using var response = await SendRequestAsync(HttpMethod.Get, HealthPath, null, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? (true, $"Connected successfully ({(int)response.StatusCode}).")
                : (false, $"Server returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string path, HttpContent? content = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new InvalidOperationException("Server URL is not configured.");
        }

        var requestUri = BuildUri(path);
        using var request = new HttpRequestMessage(method, requestUri);
        if (!string.IsNullOrWhiteSpace(BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
        }

        if (content != null)
        {
            request.Content = content;
        }

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<HttpResponseMessage> SendJsonAsync(string path, string json, CancellationToken cancellationToken = default)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return SendRequestAsync(HttpMethod.Post, path, content, cancellationToken);
    }

    private Uri BuildUri(string path)
    {
        var baseUri = new Uri(BaseUrl, UriKind.Absolute);
        if (string.IsNullOrWhiteSpace(path))
        {
            return baseUri;
        }

        return Uri.TryCreate(baseUri, path, out var combined)
            ? combined
            : new Uri(baseUri, path.TrimStart('/'));
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = baseUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            value = "https://" + value.TrimStart('/');
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return string.Empty;
        }

        return value.TrimEnd('/');
    }
}