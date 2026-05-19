namespace SongRequestForMobile.Services;

/// <summary>
/// Service for downloading and installing APK updates
/// </summary>
public interface IUpdateDownloadService
{
    Task<bool> DownloadAndInstallAsync(string downloadUrl, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default);
}

public class UpdateDownloadService : IUpdateDownloadService
{
    private readonly HttpClient _httpClient;

    public UpdateDownloadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> DownloadAndInstallAsync(string downloadUrl, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(downloadUrl))
                return false;

            progress?.Report(new UpdateProgress { Status = "Downloading update...", Percentage = 0 });

            // Download the APK file
            var fileName = Path.GetFileName(downloadUrl.Split('?')[0]);
            var cachePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);

            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var fileStream = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var totalRead = 0L;
            var buffer = new byte[8192];
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                totalRead += read;

                if (canReportProgress)
                {
                    var percentage = (int)((totalRead * 100) / totalBytes);
                    progress?.Report(new UpdateProgress
                    {
                        Status = "Downloading update...",
                        Percentage = percentage,
                        BytesDownloaded = totalRead,
                        TotalBytes = totalBytes
                    });
                }
            }

            progress?.Report(new UpdateProgress { Status = "Installing update...", Percentage = 100 });

            // Install the APK
            await InstallApkAsync(cachePath, cancellationToken).ConfigureAwait(false);

            progress?.Report(new UpdateProgress { Status = "Update complete!", Percentage = 100 });
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Download/install failed: {ex.Message}");
            progress?.Report(new UpdateProgress { Status = $"Error: {ex.Message}", Percentage = -1 });
            return false;
        }
    }

    private static async Task InstallApkAsync(string filePath, CancellationToken cancellationToken = default)
    {
#if ANDROID
        try
        {
            // Android: Use intent to install APK
            var androidPackageManager = Android.App.Application.Context.PackageManager;
            var uri = Android.Net.Uri.FromFile(new Java.IO.File(filePath));

            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
            intent.SetDataAndType(uri, "application/vnd.android.package-archive");
            intent.SetFlags(Android.Content.ActivityFlags.NewTask);

            Android.App.Application.Context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"APK install failed: {ex.Message}");
            throw;
        }
#else
        // On other platforms, attempt to open with default handler
        await Launcher.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(filePath)
        }).ConfigureAwait(false);
#endif
    }
}

public class UpdateProgress
{
    public string Status { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
}
