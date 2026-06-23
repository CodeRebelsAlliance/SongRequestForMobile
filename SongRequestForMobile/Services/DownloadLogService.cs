using System.Collections.ObjectModel;

namespace SongRequestForMobile.Services;

public sealed class DownloadLogService : IDownloadLogService
{
    private const int MaxEntries = 1000;

    private static readonly Color ColorInfo = Color.FromArgb("#00FF00");
    private static readonly Color ColorWarning = Color.FromArgb("#FFA500");
    private static readonly Color ColorError = Color.FromArgb("#FF4444");
    private static readonly Color ColorDebug = Color.FromArgb("#888888");

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public void Log(LogLevel level, string category, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var levelStr = level switch
        {
            LogLevel.Warning => "WARN",
            LogLevel.Error   => "FAIL",
            LogLevel.Debug   => "DBUG",
            _                => "INFO"
        };
        var text = $"[{timestamp}] [{levelStr}] [{category}] {message}";
        var color = level switch
        {
            LogLevel.Warning => ColorWarning,
            LogLevel.Error   => ColorError,
            LogLevel.Debug   => ColorDebug,
            _                => ColorInfo
        };

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Entries.Add(new LogEntry { Text = text, TextColor = color });
            if (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        });
    }

    public void Clear()
    {
        MainThread.BeginInvokeOnMainThread(() => Entries.Clear());
    }
}
