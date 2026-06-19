namespace PhotoBooth.Models;

public sealed class AppConfig
{
    public bool DemoMode { get; set; } = true;

    public string TemplatesPath { get; set; } = "Templates";

    public string DemoPhotosPath { get; set; } = "DemoPhotos";

    public string OutputPath { get; set; } = "Output";
}
