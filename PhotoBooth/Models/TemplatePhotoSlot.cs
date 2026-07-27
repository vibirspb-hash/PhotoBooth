using System.Text.Json.Serialization;

namespace PhotoBooth.Models;

public sealed class TemplatePhotoSlot
{
    [JsonPropertyName("shoot")]
    public int Shoot { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("w")]
    public int Width { get; set; }

    [JsonPropertyName("h")]
    public int Height { get; set; }
}
