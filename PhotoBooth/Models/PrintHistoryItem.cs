using System.Windows.Media.Imaging;

namespace PhotoBooth.Models;

public sealed class PrintHistoryItem
{
    public required string FilePath { get; init; }

    public required BitmapSource Thumbnail { get; init; }

    public required string DisplayTime { get; init; }
}
