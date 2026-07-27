using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class ImageComposer
{
    public string Compose(
        TemplateDefinition definition,
        string overlayPath,
        IReadOnlyList<string> shotPaths,
        string outputPath)
    {
        if (shotPaths.Count < definition.RequiredShotCount)
        {
            throw new InvalidOperationException("Недостаточно снимков для выбранного шаблона.");
        }

        BitmapSource[] shots = shotPaths.Select(LoadBitmap).ToArray();
        DrawingVisual visual = new();

        using (DrawingContext drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, definition.Width, definition.Height));

            foreach (TemplatePhotoSlot slot in definition.Photos)
            {
                BitmapSource shot = shots[slot.Shoot - 1];
                BitmapSource croppedShot = CropToAspectRatio(shot, slot.Width, slot.Height);
                drawing.DrawImage(croppedShot, new Rect(slot.X, slot.Y, slot.Width, slot.Height));
            }

            BitmapSource overlay = LoadBitmap(overlayPath);
            drawing.DrawImage(overlay, new Rect(0, 0, definition.Width, definition.Height));
        }

        RenderTargetBitmap result = new(
            definition.Width,
            definition.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        result.Render(visual);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(result));

        using FileStream stream = File.Create(outputPath);
        encoder.Save(stream);

        return outputPath;
    }

    private static BitmapSource CropToAspectRatio(BitmapSource source, int targetWidth, int targetHeight)
    {
        double sourceRatio = (double)source.PixelWidth / source.PixelHeight;
        double targetRatio = (double)targetWidth / targetHeight;
        int cropWidth = source.PixelWidth;
        int cropHeight = source.PixelHeight;
        int cropX = 0;
        int cropY = 0;

        if (sourceRatio > targetRatio)
        {
            cropWidth = Math.Max(1, (int)Math.Round(source.PixelHeight * targetRatio));
            cropX = (source.PixelWidth - cropWidth) / 2;
        }
        else if (sourceRatio < targetRatio)
        {
            cropHeight = Math.Max(1, (int)Math.Round(source.PixelWidth / targetRatio));
            cropY = (source.PixelHeight - cropHeight) / 2;
        }

        return new CroppedBitmap(source, new Int32Rect(cropX, cropY, cropWidth, cropHeight));
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
}
