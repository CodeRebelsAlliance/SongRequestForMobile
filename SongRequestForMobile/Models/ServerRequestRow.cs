namespace SongRequestForMobile.Models;

public sealed record ServerRequestRow(
    string VideoId,
    string Message,
    bool IsApproved,
    string Time);
