using Newtonsoft.Json;
using SongRequestForMobile.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.ClosedCaptions;
using YoutubeExplode.Videos.Streams;

namespace SongRequestForMobile
{
    public class YoutubeService
    {
        private readonly YoutubeClient _youtubeClient;
        private readonly HttpClient _httpClient;
        private readonly IReadOnlyList<Cookie>? _cookies;
        private readonly IDownloadLogService? _log;

        private static readonly string ToolPath = Path.Combine(FileSystem.AppDataDirectory, "yt-dlp-tools");
        private static readonly SemaphoreSlim InitSemaphore = new(1, 1);
        private static bool _toolsDownloaded;

        public YoutubeService(IReadOnlyList<Cookie>? cookies = null, IDownloadLogService? logService = null)
        {
            _log = logService;
            _cookies = cookies;
            var cookieList = cookies?.ToArray() ?? Array.Empty<Cookie>();
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true
            };

            foreach (var cookie in cookieList)
            {
                try
                {
                    handler.CookieContainer.Add(cookie);
                }
                catch (CookieException)
                {
                    // Skip invalid cookies that the platform may have exposed.
                }
            }

            _youtubeClient = cookieList.Length > 0
                ? new YoutubeClient(new HttpClient(handler))
                : new YoutubeClient();

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.lyrics.ovh/v1")
            };
        }

        public static string ExtractVideoId(string url)
        {
            // Define regex patterns for different types of YouTube URLs
            string[] patterns = new string[]
            {
            @"(?:https?://)?(?:www\.)?youtube\.com/watch\?v=([a-zA-Z0-9_-]{11})",  // Standard URL
            @"(?:https?://)?youtu\.be/([a-zA-Z0-9_-]{11})",                       // Shortened URL
            @"(?:https?://)?(?:www\.)?youtube\.com/embed/([a-zA-Z0-9_-]{11})",     // Embed URL
            @"(?:https?://)?(?:www\.)?youtube\.com/v/([a-zA-Z0-9_-]{11})",         // /v/ URL
            @"(?:https?://)?(?:www\.)?youtube\.com/e/([a-zA-Z0-9_-]{11})",         // /e/ URL
            @"(?:https?://)?(?:www\.)?youtube\.com/shorts/([a-zA-Z0-9_-]{11})",    // /shorts/ URL
            @"(?:https?://)?(?:www\.)?youtube\.com/live/([a-zA-Z0-9_-]{11})",      // /live/ URL
            @"(?:https?://)?(?:www\.)?music\.youtube\.com/watch\?v=([a-zA-Z0-9_-]{11})",  // Music URL
            @"(?:https?://)?m\.youtube\.com/watch\?app=desktop&v=([a-zA-Z0-9_-]{11})"     // Mobile URL
            };

            // Iterate over the patterns and search for a match
            foreach (string pattern in patterns)
            {
                var match = Regex.Match(url, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            // Return null if no match is found
            return "404";
        }

        public static string FormatSeconds(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            // Use PadLeft to ensure two-digit formatting for both minutes and seconds
            string formattedTime = minutes.ToString().PadLeft(2, '0') + ":" + seconds.ToString().PadLeft(2, '0');

            return formattedTime;
        }

        public async Task<(string Title, TimeSpan Length, string Creator)> GetVideoMetadataAsync(string videoUrl)
        {
            var video = await _youtubeClient.Videos.GetAsync(videoUrl);

            string title = video.Title;
            TimeSpan length = video.Duration ?? TimeSpan.Zero;
            string creator = video.Author.ChannelTitle;

            return (title, length, creator);
        }

        public string GetYouTubeVideoId(string url)
        {
            var videoId = ExtractVideoId(url);
            if (videoId == "404")
            {
                return string.Empty;
            }

            return videoId;
        }

        public async Task<string> DownloadVideoAsync(string videoUrl, string downloadPath)
        {
            _log?.Log(LogLevel.Info, "Download", $"Starting download for: {videoUrl}");

            await Task.Yield();

            string videoId = GetYouTubeVideoId(videoUrl);
            if (string.IsNullOrEmpty(videoId))
            {
                _log?.Log(LogLevel.Error, "Download", $"Failed to extract video ID from URL: {videoUrl}");
                throw new Exception("Invalid YouTube URL. Unable to extract video ID.");
            }
            _log?.Log(LogLevel.Debug, "Download", $"Extracted video ID: {videoId}");

            string videoFileName = $"{videoId}.mp3";
            string filePath = Path.Combine(downloadPath, videoFileName);

            try
            {
                _log?.Log(LogLevel.Info, "Download", $"Fetching stream manifest for {videoId}...");
                var streamManifest = await Task.Run(async () => await _youtubeClient.Videos.Streams.GetManifestAsync(videoId).ConfigureAwait(false)).ConfigureAwait(false);
                _log?.Log(LogLevel.Info, "Download", $"Stream manifest received");

                var audioStreams = streamManifest.GetAudioOnlyStreams();
                if (audioStreams == null || !audioStreams.Any())
                {
                    _log?.Log(LogLevel.Warning, "Download", "No audio-only streams found in manifest");
                    throw new Exception("No suitable video stream found. The input stream collection is empty.");
                }

                _log?.Log(LogLevel.Debug, "Download", $"Found {audioStreams.Count()} audio-only streams");

                var originalStreams = audioStreams
                    .Where(a => a.AudioLanguage != null &&
                                a.AudioLanguage.ToString().IndexOf("Original", StringComparison.OrdinalIgnoreCase) >= 0);

                var selectedStream = audioStreams.GetWithHighestBitrate();

                if (originalStreams.Any())
                {
                    selectedStream = originalStreams
                        .OrderByDescending(a => a.Bitrate)
                        .FirstOrDefault();
                    _log?.Log(LogLevel.Debug, "Download", $"Selected original-language stream with bitrate {selectedStream?.Bitrate}");
                }
                else
                {
                    selectedStream = audioStreams.GetWithHighestBitrate();
                    _log?.Log(LogLevel.Debug, "Download", $"Selected highest-bitrate stream: {selectedStream?.Bitrate}");
                }

                if (selectedStream == null)
                {
                    _log?.Log(LogLevel.Error, "Download", "No suitable audio stream selected (null)");
                    throw new Exception("No suitable audio stream found.");
                }

                _log?.Log(LogLevel.Info, "Download", $"Downloading audio stream ({selectedStream.Bitrate}) to: {filePath}");
                await Task.Run(async () => await _youtubeClient.Videos.Streams.DownloadAsync(selectedStream, filePath).ConfigureAwait(false)).ConfigureAwait(false);
                _log?.Log(LogLevel.Info, "Download", $"Download completed: {filePath}");

                return filePath;
            }
            catch (VideoUnavailableException ex)
            {
                _log?.Log(LogLevel.Warning, "Download", $"Video unavailable via YoutubeExplode: {ex.Message}. Falling back to yt-dlp...");
                return await DownloadWithYoutubeDLFallbackAsync(videoUrl, filePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log?.Log(LogLevel.Error, "Download", $"Download failed: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        private async Task<string> DownloadWithYoutubeDLFallbackAsync(string videoUrl, string outputPath)
        {
            _log?.Log(LogLevel.Info, "yt-dlp", "Starting yt-dlp fallback download...");
            await EnsureToolsDownloadedAsync().ConfigureAwait(false);

            var ytdl = new YoutubeDL
            {
                YoutubeDLPath = Path.Combine(ToolPath, OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp"),
                FFmpegPath = Path.Combine(ToolPath, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"),
                OutputFileTemplate = outputPath
            };
            _log?.Log(LogLevel.Debug, "yt-dlp", $"yt-dlp path: {ytdl.YoutubeDLPath}");
            _log?.Log(LogLevel.Debug, "yt-dlp", $"FFmpeg path: {ytdl.FFmpegPath}");

            var options = new OptionSet();
            if (_cookies is { Count: > 0 })
            {
                WriteCookiesFile(_cookies, out var cookieFile);
                options.Cookies = cookieFile;
                _log?.Log(LogLevel.Debug, "yt-dlp", $"Cookies written to: {cookieFile} ({_cookies.Count} cookies)");
            }

            _log?.Log(LogLevel.Info, "yt-dlp", $"Running yt-dlp for: {videoUrl}");
            var result = await ytdl.RunAudioDownload(
                videoUrl,
                AudioConversionFormat.Mp3,
                overrideOptions: options
            ).ConfigureAwait(false);

            if (!result.Success)
            {
                var errors = string.Join(Environment.NewLine, result.ErrorOutput);
                _log?.Log(LogLevel.Error, "yt-dlp", $"yt-dlp failed:{Environment.NewLine}{errors}");
                throw new Exception($"YoutubeDLSharp fallback failed: {string.Join(", ", result.ErrorOutput)}");
            }

            _log?.Log(LogLevel.Info, "yt-dlp", $"yt-dlp download completed: {result.Data ?? outputPath}");
            return result.Data ?? outputPath;
        }

        private async Task EnsureToolsDownloadedAsync()
        {
            if (_toolsDownloaded)
            {
                _log?.Log(LogLevel.Debug, "Tools", "Tools already downloaded, skipping");
                return;
            }

            _log?.Log(LogLevel.Info, "Tools", "Checking/downloading yt-dlp/ffmpeg tools...");
            await InitSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_toolsDownloaded) return;

                Directory.CreateDirectory(ToolPath);
                _log?.Log(LogLevel.Info, "Tools", "Downloading yt-dlp...");
                await Utils.DownloadYtDlp(ToolPath).ConfigureAwait(false);
                _log?.Log(LogLevel.Info, "Tools", "Downloading FFmpeg...");
                await Utils.DownloadFFmpeg(ToolPath).ConfigureAwait(false);
                _log?.Log(LogLevel.Info, "Tools", "Downloading Deno runtime...");
                await Utils.DownloadDeno(ToolPath).ConfigureAwait(false);

                _toolsDownloaded = true;
                _log?.Log(LogLevel.Info, "Tools", "All tools downloaded successfully");
            }
            catch (Exception ex)
            {
                _log?.Log(LogLevel.Error, "Tools", $"Tool download failed: {ex.Message}");
                throw;
            }
            finally
            {
                InitSemaphore.Release();
            }
        }

        private static void WriteCookiesFile(IReadOnlyList<Cookie> cookies, out string path)
        {
            path = Path.Combine(
                Path.GetTempPath(),
                $"ytdl_cookies_{Guid.NewGuid():N}.txt");

            using var writer = new StreamWriter(path, false, Encoding.UTF8);

            // Required header for Netscape cookie files
            writer.WriteLine("# Netscape HTTP Cookie File");
            writer.WriteLine();

            foreach (var cookie in cookies)
            {
                if (cookie == null)
                    continue;

                if (string.IsNullOrWhiteSpace(cookie.Name) ||
                    string.IsNullOrWhiteSpace(cookie.Domain))
                {
                    continue;
                }

                // Clean value
                var value = (cookie.Value ?? string.Empty)
                    .Replace("\r", string.Empty)
                    .Replace("\n", string.Empty)
                    .Replace("\t", " ");

                // Clean path
                var cookiePath = string.IsNullOrWhiteSpace(cookie.Path)
                    ? "/"
                    : cookie.Path
                        .Replace("\r", string.Empty)
                        .Replace("\n", string.Empty)
                        .Replace("\t", string.Empty);

                // Netscape format:
                // domain | includeSubdomains | path | secure | expires | name | value

                var domain = cookie.Domain.Trim();

                bool includeSubdomains = domain.StartsWith(".");

                if (!includeSubdomains)
                    domain = "." + domain;

                // HttpOnly cookies are represented by prefixing the domain
                if (cookie.HttpOnly)
                    domain = "#HttpOnly_" + domain;

                long expires = 0;

                try
                {
                    var dt = cookie.Expires.ToUniversalTime();

                    if (dt > DateTime.UnixEpoch)
                        expires = ((DateTimeOffset)dt).ToUnixTimeSeconds();
                }
                catch
                {
                    expires = 0;
                }

                writer.WriteLine(
                    string.Join("\t",
                        domain,
                        includeSubdomains ? "TRUE" : "FALSE",
                        cookiePath,
                        cookie.Secure ? "TRUE" : "FALSE",
                        expires.ToString(CultureInfo.InvariantCulture),
                        cookie.Name,
                        value));
            }
        }

        public async Task<string> DownloadAndConvertVideoAsync(string videoUrl, string downloadPath)
        {
            try
            {
                string videoId = GetYouTubeVideoId(videoUrl);
                if (string.IsNullOrEmpty(videoId))
                {
                    throw new Exception("Invalid YouTube URL. Unable to extract video ID.");
                }

                string mp4FileName = $"{videoId}.mp3";
                string mp3FileName = $"{videoId}.mp3";
                string mp4FilePath = Path.Combine(downloadPath, mp4FileName);
                string mp3FilePath = Path.Combine(downloadPath, mp3FileName);

                await DownloadVideoAsync(videoUrl, downloadPath).ConfigureAwait(false);
                //await ConvertMp4ToMp3WithFFmpeg(mp4FilePath, mp3FilePath);
                //if (File.Exists(mp3FilePath))
                //{
                //    File.Delete(mp3FilePath);
                //}

                return mp3FilePath;
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error downloading and converting video: {ex.Message}");
                throw;
            }
        }

        private async Task ConvertMp4ToMp3WithFFmpeg(string inputFilePath, string outputFilePath)
        {
            try
            {
                string ffmpegPath = @"ffmpeg\ffmpeg.exe";
                string arguments = $"-i \"{inputFilePath}\" -vn -acodec libmp3lame -q:a 2 \"{outputFilePath}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error converting MP4 to MP3: {ex.Message}");
                throw;
            }
        }

        public sealed class SubtitleFetchResult
        {
            public string PlainText { get; set; } = string.Empty;
            public string TimedLrcText { get; set; } = string.Empty;
        }

        public async Task<SubtitleFetchResult?> TryGetSubtitlesAsync(string videoUrl)
        {
            try
            {
                string videoId = GetYouTubeVideoId(videoUrl);
                if (string.IsNullOrWhiteSpace(videoId))
                {
                    return null;
                }

                var captionsManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(videoId);
                string[] preferredLanguages = { "de", "en", "fr" };

                ClosedCaptionTrackInfo subtitleTrack = null;

                foreach (var lang in preferredLanguages)
                {
                    subtitleTrack = captionsManifest.TryGetByLanguage(lang);
                    if (subtitleTrack != null)
                        break;
                }

                // If no preferred language tracks are found, use any available track
                if (subtitleTrack == null)
                {
                    subtitleTrack = captionsManifest.Tracks.FirstOrDefault();
                }

                if (subtitleTrack == null)
                {
                    return null;
                }

                var captions = await _youtubeClient.Videos.ClosedCaptions.GetAsync(subtitleTrack);

                var captionItems = captions.Captions
                    .Select(c => new { Offset = c.Offset, Text = c.Text?.Trim() })
                    .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                    .ToList();

                string subtitleText = string.Join(
                    Environment.NewLine,
                    captionItems.Select(c => c.Text));

                if (string.IsNullOrWhiteSpace(subtitleText))
                {
                    return null;
                }

                string timedText = string.Join(
                    Environment.NewLine,
                    captionItems.Select(c => $"[{FormatLrcTimestamp(c.Offset)}]{c.Text}"));

                return new SubtitleFetchResult
                {
                    PlainText = subtitleText,
                    TimedLrcText = timedText
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> TryGetSubtitlesTextAsync(string videoUrl)
        {
            var subtitleResult = await TryGetSubtitlesAsync(videoUrl);
            if (subtitleResult == null || string.IsNullOrWhiteSpace(subtitleResult.PlainText))
            {
                return null;
            }

            return subtitleResult.PlainText;
        }

        public async Task<string> DownloadSubtitlesAndGetTextAsync(string videoUrl, string downloadPath)
        {
            var subtitleText = await TryGetSubtitlesTextAsync(videoUrl);
            if (string.IsNullOrWhiteSpace(subtitleText))
            {
                throw new Exception("No subtitles available for this video.");
            }

            return subtitleText;
        }

        private static string FormatLrcTimestamp(TimeSpan offset)
        {
            int minutes = (int)offset.TotalMinutes;
            int seconds = offset.Seconds;
            int centiseconds = offset.Milliseconds / 10;
            return $"{minutes:D2}:{seconds:D2}.{centiseconds:D2}";
        }



        public async Task<string> GetLyricsAsync(string artist, string title, string id)
        {
            try
            {
                string preparedTitle = Uri.EscapeDataString(title);
                string preparedArtist = Uri.EscapeDataString(artist);
                string requestUri = $"/{preparedArtist}/{preparedTitle}";

                // Log the constructed URL to debu

                HttpResponseMessage response = await _httpClient.GetAsync(_httpClient.BaseAddress + requestUri);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();

                // Parse JSON response to get lyrics
                dynamic jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
                string lyrics = jsonResponse.lyrics;

                string completelyrics = "Lyrics: Lyrics.OVH API\n" + lyrics;

                return completelyrics;
            }
            catch (HttpRequestException ex)
            {
                try
                {
                    string desc = await GetVideoDescriptionAsync(id);

                    return "No Lyrics (YouTube Description)\n" + desc + "\n\nIf there aren't any lyrics in the description, please look them up by clicking the link below!";
                }
                catch (Exception exx)
                {
                    return "No Lyrics found, not even a description. Please look up your own lyrics using the button below!";
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error: {ex.Message}");
                throw;
            }
        }

        public void SearchLyricsOnDuckDuckGo(string artist, string title)
        {
            string searchQuery = $"{artist} {title} lyrics";
            //LyricsLookup searchForm = new LyricsLookup(searchQuery);
            //searchForm.Show();
        }

        public async Task<string> GetVideoDescriptionAsync(string videoUrl)
        {
            var video = await _youtubeClient.Videos.GetAsync(videoUrl);
            return video.Description;
        }

        public async Task<string> GetThumbnailUrlAsync(string videoUrl)
        {
            var video = await _youtubeClient.Videos.GetAsync(videoUrl);
            return video.Thumbnails.GetWithHighestResolution().Url;
        }

        public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 10)
        {
            try
            {
                var searchResults = await _youtubeClient.Search.GetVideosAsync(query).CollectAsync(maxResults);
                return searchResults.Select(video => new SearchResult
                {
                    VideoId = video.Id,
                    Title = video.Title,
                    Author = video.Author.ChannelTitle,
                    Duration = video.Duration
                }).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Search failed: {ex.Message}");
            }
        }
    }

    public class SearchResult
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public TimeSpan? Duration { get; set; }
    }
}
