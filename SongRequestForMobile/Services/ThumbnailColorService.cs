namespace SongRequestForMobile.Services;

public interface IThumbnailColorService
{
    Color GetAccentColor(string? seed);
}

public sealed class ThumbnailColorService : IThumbnailColorService
{
    public Color GetAccentColor(string? seed)
    {
        if (string.IsNullOrWhiteSpace(seed))
        {
            return Colors.SteelBlue;
        }

        unchecked
        {
            var hash = seed.GetHashCode();
            var r = (byte)(96 + (hash & 0x3F));
            var g = (byte)(96 + ((hash >> 8) & 0x3F));
            var b = (byte)(96 + ((hash >> 16) & 0x3F));
            return Color.FromRgb(r, g, b);
        }
    }
}
