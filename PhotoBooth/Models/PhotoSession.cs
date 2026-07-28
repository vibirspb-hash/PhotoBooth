namespace PhotoBooth.Models;

public sealed class PhotoSession
{
    public required string Name { get; init; }

    public required string FolderPath { get; init; }

    public DateTime StartedAt { get; init; }
}
