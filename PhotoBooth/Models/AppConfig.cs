namespace PhotoBooth.Models;

public sealed class AppConfig
{
    public bool DemoMode { get; set; } = true;

    public bool Fullscreen { get; set; } = true;

    public bool HideCursor { get; set; } = true;

    public string TemplatesPath { get; set; } = "Templates";

    public string DemoPhotosPath { get; set; } = "DemoPhotos";

    public string OutputPath { get; set; } = "Output";

    public bool WorkScheduleEnabled { get; set; }

    public int WorkStartHour { get; set; } = 9;

    public int WorkStartMinute { get; set; } = 30;

    public int WorkEndHour { get; set; } = 22;

    public int WorkEndMinute { get; set; }

    public bool ShutdownOutsideWorkHours { get; set; }
}
