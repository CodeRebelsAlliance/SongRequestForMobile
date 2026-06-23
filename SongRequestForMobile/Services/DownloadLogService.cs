using System.Collections.ObjectModel;

namespace SongRequestForMobile.Services;

public sealed class DownloadLogService : IDownloadLogService
{
    private const int MaxEntries = 1000;

    public ObservableCollection<string> Entries { get; } = new();

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
        var line = $"[{timestamp}] [{levelStr}] [{category}] {message}";

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Entries.Add(line);
            if (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        });
    }

    public void Clear()
    {
        MainThread.BeginInvokeOnMainThread(() => Entries.Clear());
    }
}
