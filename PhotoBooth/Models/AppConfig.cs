namespace PhotoBooth.Models;

public sealed class AppConfig
{
    public bool DemoMode { get; set; } = true;

    public bool HardwareFallbackToDemo { get; set; } = true;

    public string GPhotoCommand { get; set; } = "gphoto2";

    public string CupsLpCommand { get; set; } = "lp";

    public string CupsLpStatCommand { get; set; } = "lpstat";

    public string PrinterName { get; set; } = string.Empty;

    public string PrinterMedia { get; set; } = "w288h432";

    public int PrinterOffsetX { get; set; }

    public int PrinterOffsetY { get; set; }

    public double PrinterScalePercent { get; set; } = 100;

    public string PrinterQuality { get; set; } = "Fast";

    public string PrinterCutMode { get; set; } = "Standard";

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
