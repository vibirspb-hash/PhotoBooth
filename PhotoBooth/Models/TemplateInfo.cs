namespace PhotoBooth.Models;

public sealed class TemplateInfo
{
    public required string Name { get; init; }

    public required string FolderPath { get; init; }

    public string? PreviewPath { get; init; }

    public string? JsonPath { get; init; }

    public int RequiredShotCount { get; init; }

    public string PhotoCountText =>
        RequiredShotCount > 0 ? $"{RequiredShotCount} фото" : "Кадры не указаны";
}
