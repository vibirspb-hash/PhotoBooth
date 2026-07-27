namespace PhotoBooth.Models;

public sealed class TemplateDefinition
{
    public int Width { get; set; }

    public int Height { get; set; }

    public string? Overlay { get; set; }

    public string? PreviewSelect { get; set; }

    public string? ComboPrint { get; set; }

    public List<TemplatePhotoSlot> Photos { get; set; } = [];

    public int RequiredShotCount => Photos.Count == 0 ? 0 : Photos.Max(photo => photo.Shoot);
}
