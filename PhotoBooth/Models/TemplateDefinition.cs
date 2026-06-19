namespace PhotoBooth.Models;

public sealed class TemplateDefinition
{
    public int Width { get; set; }

    public int Height { get; set; }

    public List<TemplatePhotoSlot> Photos { get; set; } = [];
}
