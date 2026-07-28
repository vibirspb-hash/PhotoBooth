using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoBooth.Services;

public sealed class DemoPhotoService : ICameraService
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp"];

    private static readonly Color[] PlaceholderColors =
    [
        Color.FromRgb(229, 57, 53),
        Color.FromRgb(0, 137, 123),
        Color.FromRgb(30, 136, 229),
        Color.FromRgb(251, 140, 0)
    ];

    public bool IsDemo => true;

    public string DisplayName => "Демо-камера";

    public IReadOnlyList<string> PrepareShots(string demoPhotosPath, string originalsPath, int shotCount)
    {
        Directory.CreateDirectory(demoPhotosPath);
        Directory.CreateDirectory(originalsPath);

        List<string> sourcePhotos = Directory
            .EnumerateFiles(demoPhotosPath)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> shots = [];

        for (int index = 0; index < shotCount; index++)
        {
            string destinationPath = Path.Combine(originalsPath, $"shot-{index + 1:00}.png");

            if (sourcePhotos.Count == 0)
            {
                CreatePlaceholder(destinationPath, index + 1);
            }
            else
            {
                SaveAsPng(sourcePhotos[index % sourcePhotos.Count], destinationPath);
            }

            shots.Add(destinationPath);
        }

        return shots;
    }

    private static void CreatePlaceholder(string destinationPath, int shotNumber)
    {
        const int width = 1600;
        const int height = 1200;

        DrawingVisual visual = new();

        using (DrawingContext drawing = visual.RenderOpen())
        {
            Color background = PlaceholderColors[(shotNumber - 1) % PlaceholderColors.Length];
            drawing.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, width, height));
            drawing.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                null,
                new Point(width * 0.22, height * 0.28),
                width * 0.22,
                width * 0.22);
            drawing.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)),
                null,
                new Point(width * 0.82, height * 0.78),
                width * 0.3,
                width * 0.3);

            FormattedText number = new(
                shotNumber.ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                360,
                Brushes.White,
                1);

            drawing.DrawText(
                number,
                new Point((width - number.Width) / 2, (height - number.Height) / 2 - 40));

            FormattedText caption = new(
                "ДЕМО-КАДР",
                System.Globalization.CultureInfo.GetCultureInfo("ru-RU"),
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                72,
                Brushes.White,
                1);

            drawing.DrawText(caption, new Point((width - caption.Width) / 2, height - 170));
        }

        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        SaveBitmap(bitmap, destinationPath);
    }

    private static void SaveAsPng(string sourcePath, string destinationPath)
    {
        BitmapSource bitmap = LoadBitmap(sourcePath);
        SaveBitmap(bitmap, destinationPath);
    }

    private static BitmapSource LoadBitmap(string path)
    {
        using FileStream stream = File.OpenRead(path);
        BitmapDecoder decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        BitmapSource bitmap = decoder.Frames[0];
        bitmap.Freeze();
        return bitmap;
    }

    private static void SaveBitmap(BitmapSource bitmap, string destinationPath)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using FileStream stream = File.Create(destinationPath);
        encoder.Save(stream);
    }
}
