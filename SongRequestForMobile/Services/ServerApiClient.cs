using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SongRequestForMobile.Models;

namespace SongRequestForMobile.Services;

public sealed class ServerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private Uri _baseUri = new("https://localhost");
    private string _bearerToken = string.Empty;

    public ServerApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void Configure(string? baseUrl, string? bearerToken)
    {
        _baseUri = NormalizeBaseUri(baseUrl);
        _bearerToken = bearerToken?.Trim() ?? string.Empty;
    }

    public async Task<string> PingAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "/ping", null, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{body}".Trim();
    }

    public async Task<IReadOnlyList<MusicSearchResult>> SearchMusicAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query is required.", nameof(query));
        }

        using var response = await SendAsync(HttpMethod.Get, $"/search-music?query={Uri.EscapeDataString(query)}", null, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ExtractErrorMessage(body, response.StatusCode));
        }

        var payload = JsonSerializer.Deserialize<SearchMusicResponse>(body, JsonOptions);
        return payload?.Results ?? new List<MusicSearchResult>();
    }

    public async Task<SendInResponse> SendSongAsync(string ytlink, string? message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ytlink))
        {
            throw new ArgumentException("A YouTube link or video id is required.", nameof(ytlink));
        }

        var formValues = new List<KeyValuePair<string, string>>
        {
            new("ytlink", ytlink.Trim())
        };

        if (!string.IsNullOrWhiteSpace(message))
        {
            formValues.Add(new KeyValuePair<string, string>("message", message.Trim()));
        }

        using var content = new FormUrlEncodedContent(formValues);
        using var response = await SendAsync(HttpMethod.Post, "/sendin", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect)
        {
            return new SendInResponse
            {
                Status = "success",
                Message = string.IsNullOrWhiteSpace(response.Headers.Location?.ToString())
                    ? "Song submitted."
                    : $"Song submitted. Redirected to {response.Headers.Location}."
            };
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ExtractErrorMessage(body, response.StatusCode));
        }

        return JsonSerializer.Deserialize<SendInResponse>(body, JsonOptions)
               ?? new SendInResponse { Status = "success", Message = "Song submitted." };
    }

    public async Task<IReadOnlyList<ServerRequestRow>> GetSentInSongsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "/fetch?method=get-database", null, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Unauthorized");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ExtractErrorMessage(body, response.StatusCode));
        }

        return ParseSentInSongs(body);
    }

    public Task<string> ApproveRequestAsync(string videoId, CancellationToken cancellationToken = default)
        => SendRequestActionAsync("approve", videoId, cancellationToken);

    public Task<string> UnapproveRequestAsync(string videoId, CancellationToken cancellationToken = default)
        => SendRequestActionAsync("unapprove", videoId, cancellationToken);

    public Task<string> BlacklistRequestAsync(string videoId, CancellationToken cancellationToken = default)
        => SendRequestActionAsync("blacklist", videoId, cancellationToken);

    public Task<string> UnblacklistRequestAsync(string videoId, CancellationToken cancellationToken = default)
        => SendRequestActionAsync("unblacklist", videoId, cancellationToken);

    public Task<string> DeleteRequestAsync(string videoId, CancellationToken cancellationToken = default)
        => SendRequestActionAsync("delete", videoId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetBlacklistAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "/fetch?method=get-blacklist", null, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Unauthorized");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ExtractErrorMessage(body, response.StatusCode));
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var list = new List<string>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var s = element.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s!);
                }
            }

            return list;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> PostJsonAsync<T>(string path, T payload, CancellationToken cancellationToken = default)
    {
        using var content = JsonContent.Create(payload);
        return await SendAsync(HttpMethod.Post, path, content, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<ServerRequestRow> ParseSentInSongs(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<ServerRequestRow>();
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ServerRequestRow>();
            }

            var rows = new List<ServerRequestRow>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    var values = element.EnumerateArray().ToArray();
                    if (values.Length >= 4)
                    {
                        rows.Add(new ServerRequestRow(
                            values[0].GetString() ?? string.Empty,
                            values[1].GetString() ?? string.Empty,
                            values[2].GetBoolean(),
                            values[3].GetString() ?? string.Empty));
                    }
                    continue;
                }

                if (element.ValueKind == JsonValueKind.Object)
                {
                    var videoId = element.TryGetProperty("ytid", out var idProp) ? idProp.GetString() : string.Empty;
                    var message = element.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : string.Empty;
                    var approved = element.TryGetProperty("is_approved", out var approvedProp) && approvedProp.ValueKind == JsonValueKind.True;
                    var time = element.TryGetProperty("time", out var timeProp) ? timeProp.GetString() : string.Empty;

                    if (!string.IsNullOrWhiteSpace(videoId))
                    {
                        rows.Add(new ServerRequestRow(videoId!, message ?? string.Empty, approved, time ?? string.Empty));
                    }
                }
            }

            return rows;
        }
        catch
        {
            return Array.Empty<ServerRequestRow>();
        }
    }

    private async Task<string> SendRequestActionAsync(string method, string videoId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new ArgumentException("Video id is required.", nameof(videoId));
        }

        using var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("ytid", videoId.Trim()) });
        using var response = await SendAsync(HttpMethod.Post, $"/fetch?method={Uri.EscapeDataString(method)}", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Unauthorized");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ExtractErrorMessage(body, response.StatusCode));
        }

        return ExtractSuccessMessage(body, method, videoId);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(path));
        if (!string.IsNullOrWhiteSpace(_bearerToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
        }

        if (content != null)
        {
            request.Content = content;
        }

        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private Uri BuildUri(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return _baseUri;
        }

        return Uri.TryCreate(_baseUri, path, out var combined)
            ? combined
            : new Uri(_baseUri, path.TrimStart('/'));
    }

    private static Uri NormalizeBaseUri(string? baseUrl)
    {
        var value = baseUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Uri("https://localhost");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            uri = Uri.TryCreate("https://" + value.TrimStart('/'), UriKind.Absolute, out var httpsUri)
                ? httpsUri
                : new Uri("https://localhost");
        }

        return uri;
    }

    private static string ExtractErrorMessage(string body, HttpStatusCode statusCode)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Server returned HTTP {(int)statusCode} ({statusCode}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? body;
            }
        }
        catch
        {
        }

        return body;
    }

    private static string ExtractSuccessMessage(string body, string method, string videoId)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString() ?? $"{method} succeeded for {videoId}.";
                }
            }
            catch
            {
            }
        }

        return $"{method} succeeded for {videoId}.";
    }
}
