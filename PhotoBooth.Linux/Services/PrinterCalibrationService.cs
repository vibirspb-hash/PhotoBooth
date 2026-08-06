using SkiaSharp;

namespace PhotoBooth.Services;

public sealed class PrinterCalibrationService
{
    public string CreateAdjustedCopy(
        string sourcePath,
        int offsetX,
        int offsetY,
        double scalePercent)
    {
        using SKBitmap source = SKBitmap.Decode(sourcePath) ??
            throw new InvalidOperationException("Не удалось открыть макет для печати.");
        using SKBitmap result = new(source.Width, source.Height);
        using SKCanvas canvas = new(result);
        canvas.Clear(SKColors.White);

        float scale = (float)(Math.Clamp(scalePercent, 98, 102) / 100d);
        canvas.Translate(
            source.Width / 2f + Math.Clamp(offsetX, -20, 20),
            source.Height / 2f + Math.Clamp(offsetY, -20, 20));
        canvas.Scale(scale);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);

        using SKPaint paint = new()
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        canvas.DrawBitmap(source, 0, 0, paint);
        canvas.Flush();

        string outputPath = Path.Combine(
            Path.GetTempPath(),
            $"photobooth-print-{Guid.NewGuid():N}.png");
        SavePng(result, outputPath);
        return outputPath;
    }

    public string CreateTestPage()
    {
        const int width = 1240;
        const int height = 1844;
        using SKBitmap bitmap = new(width, height);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.White);

        using SKPaint line = new()
        {
            Color = new SKColor(62, 72, 96),
            StrokeWidth = 3,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        using SKPaint accent = new()
        {
            Color = new SKColor(123, 97, 255),
            StrokeWidth = 5,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };
        using SKPaint text = new()
        {
            Color = new SKColor(23, 34, 56),
            TextSize = 42,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        canvas.DrawRect(22, 22, width - 44, height - 44, accent);
        canvas.DrawRect(42, 42, width - 84, height - 84, line);
        canvas.DrawLine(width / 2f, 42, width / 2f, height - 42, accent);
        canvas.DrawLine(42, height / 2f, width - 42, height / 2f, accent);
        canvas.DrawCircle(width / 2f, height / 2f, 110, accent);

        for (int x = 100; x < width; x += 100)
        {
            canvas.DrawLine(x, 42, x, x % 500 == 0 ? 100 : 72, line);
            canvas.DrawLine(x, height - 42, x, x % 500 == 0 ? height - 100 : height - 72, line);
        }

        for (int y = 100; y < height; y += 100)
        {
            canvas.DrawLine(42, y, y % 500 == 0 ? 100 : 72, y, line);
            canvas.DrawLine(width - 42, y, y % 500 == 0 ? width - 100 : width - 72, y, line);
        }

        canvas.DrawText("PHOTOBOOTH · КАЛИБРОВКА ПЕЧАТИ", width / 2f, 180, text);
        text.TextSize = 30;
        canvas.DrawText("Проверьте одинаковые поля и положение линии реза", width / 2f, 235, text);
        canvas.Flush();

        string outputPath = Path.Combine(
            Path.GetTempPath(),
            $"photobooth-test-{Guid.NewGuid():N}.png");
        SavePng(bitmap, outputPath);
        return outputPath;
    }

    private static void SavePng(SKBitmap bitmap, string path)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Create(path);
        data.SaveTo(stream);
    }
}
