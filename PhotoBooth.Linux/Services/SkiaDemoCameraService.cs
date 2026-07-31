using SkiaSharp;

namespace PhotoBooth.Services;

public sealed class SkiaDemoCameraService : ICameraService
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp"];

    private static readonly SKColor[] PlaceholderColors =
    [
        new(98, 121, 238),
        new(207, 100, 205),
        new(250, 123, 174),
        new(75, 177, 216)
    ];

    public bool IsDemo => true;

    public string DisplayName => "Демо-камера";

    public IReadOnlyList<string> PrepareShots(
        string sourcePath,
        string originalsPath,
        int shotCount)
    {
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(originalsPath);

        List<string> sourcePhotos = Directory
            .EnumerateFiles(sourcePath)
            .Where(path => SupportedExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> shots = [];

        for (int index = 0; index < shotCount; index++)
        {
            string destinationPath =
                Path.Combine(originalsPath, $"shot-{index + 1:00}.png");

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

        using SKBitmap bitmap = new(width, height);
        using SKCanvas canvas = new(bitmap);
        SKColor background =
            PlaceholderColors[(shotNumber - 1) % PlaceholderColors.Length];
        canvas.Clear(background);

        using SKPaint decoration = new()
        {
            Color = new SKColor(255, 255, 255, 38),
            IsAntialias = true
        };
        canvas.DrawCircle(width * 0.2f, height * 0.25f, 310, decoration);
        decoration.Color = new SKColor(25, 28, 55, 35);
        canvas.DrawCircle(width * 0.85f, height * 0.82f, 390, decoration);

        using SKPaint numberPaint = new()
        {
            Color = SKColors.White,
            TextSize = 380,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyle.Bold)
        };
        canvas.DrawText(
            shotNumber.ToString(),
            width / 2f,
            height / 2f + 130,
            numberPaint);

        using SKPaint captionPaint = new()
        {
            Color = SKColors.White,
            TextSize = 68,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyle.Bold)
        };
        canvas.DrawText("PHOTO STAR", width / 2f, height - 110, captionPaint);
        SaveBitmap(bitmap, destinationPath);
    }

    private static void SaveAsPng(string sourcePath, string destinationPath)
    {
        using SKBitmap bitmap = SKBitmap.Decode(sourcePath) ??
            throw new InvalidOperationException(
                $"Не удалось открыть демо-фотографию: {sourcePath}");
        SaveBitmap(bitmap, destinationPath);
    }

    private static void SaveBitmap(SKBitmap bitmap, string destinationPath)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Create(destinationPath);
        data.SaveTo(stream);
    }
}
