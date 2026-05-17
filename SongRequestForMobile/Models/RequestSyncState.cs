namespace SongRequestForMobile.Models;

public sealed class RequestSyncState
{
    public DateTimeOffset? LastSyncUtc { get; set; }
    public bool IsUnauthorized { get; set; }
    public string StatusText { get; set; } = "Not synced yet.";
}
