using System.Collections.ObjectModel;

namespace SongRequestForMobile.Services;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Debug
}

public interface IDownloadLogService
{
    ObservableCollection<string> Entries { get; }
    void Log(LogLevel level, string category, string message);
    void Clear();
}
