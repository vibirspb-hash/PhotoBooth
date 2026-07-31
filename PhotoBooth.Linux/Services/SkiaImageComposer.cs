using PhotoBooth.Models;
using SkiaSharp;

namespace PhotoBooth.Services;

public sealed class SkiaImageComposer
{
    public string Compose(
        TemplateDefinition definition,
        string overlayPath,
        IReadOnlyList<string> shotPaths,
        string outputPath)
    {
        if (shotPaths.Count < definition.RequiredShotCount)
        {
            throw new InvalidOperationException(
                "Недостаточно снимков для выбранного шаблона.");
        }

        using SKBitmap result = new(definition.Width, definition.Height);
        using SKCanvas canvas = new(result);
        canvas.Clear(SKColors.White);

        foreach (TemplatePhotoSlot slot in definition.Photos)
        {
            using SKBitmap shot = SKBitmap.Decode(shotPaths[slot.Shoot - 1]) ??
                throw new InvalidOperationException(
                    $"Не удалось открыть снимок {slot.Shoot}.");
            SKRectI source = GetCropRect(shot, slot.Width, slot.Height);
            SKRect destination = new(
                slot.X,
                slot.Y,
                slot.X + slot.Width,
                slot.Y + slot.Height);

            using SKPaint paint = new()
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };
            canvas.DrawBitmap(shot, source, destination, paint);
        }

        using SKBitmap overlay = SKBitmap.Decode(overlayPath) ??
            throw new InvalidOperationException(
                $"Не удалось открыть PNG-рамку: {overlayPath}");
        canvas.DrawBitmap(
            overlay,
            new SKRect(0, 0, definition.Width, definition.Height));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using SKImage image = SKImage.FromBitmap(result);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.Create(outputPath);
        data.SaveTo(stream);
        return outputPath;
    }

    private static SKRectI GetCropRect(
        SKBitmap source,
        int targetWidth,
        int targetHeight)
    {
        double sourceRatio = (double)source.Width / source.Height;
        double targetRatio = (double)targetWidth / targetHeight;
        int cropWidth = source.Width;
        int cropHeight = source.Height;
        int cropX = 0;
        int cropY = 0;

        if (sourceRatio > targetRatio)
        {
            cropWidth = Math.Max(1, (int)Math.Round(source.Height * targetRatio));
            cropX = (source.Width - cropWidth) / 2;
        }
        else if (sourceRatio < targetRatio)
        {
            cropHeight = Math.Max(1, (int)Math.Round(source.Width / targetRatio));
            cropY = (source.Height - cropHeight) / 2;
        }

        return new SKRectI(
            cropX,
            cropY,
            cropX + cropWidth,
            cropY + cropHeight);
    }
}
