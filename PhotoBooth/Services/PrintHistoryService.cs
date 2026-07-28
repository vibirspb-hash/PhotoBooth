using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class PrintHistoryService
{
    public IReadOnlyList<PrintHistoryItem> GetItems(PhotoSession session)
    {
        string printsPath = Path.Combine(session.FolderPath, "Prints");

        if (!Directory.Exists(printsPath))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(printsPath, "*.png")
            .Select(path => new
            {
                Path = path,
                CreatedAt = File.GetCreationTime(path)
            })
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new PrintHistoryItem
            {
                FilePath = item.Path,
                Thumbnail = LoadThumbnail(item.Path),
                DisplayTime = item.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss")
            })
            .ToList();
    }

    public void RecordPrintJob(
        PhotoSession session,
        string imagePath,
        int copies,
        string printerName,
        PrintResult result)
    {
        try
        {
            string logPath = Path.Combine(session.FolderPath, "print-jobs.jsonl");
            string json = JsonSerializer.Serialize(new
            {
                PrintedAt = DateTime.Now,
                ImagePath = imagePath,
                Copies = copies,
                Printer = printerName,
                result.Success,
                result.Message
            });

            File.AppendAllText(logPath, $"{json}{Environment.NewLine}");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // A print result should not be lost because its optional audit log is unavailable.
        }
    }

    private static BitmapSource LoadThumbnail(string path)
    {
        using FileStream stream = File.OpenRead(path);
        BitmapImage thumbnail = new();
        thumbnail.BeginInit();
        thumbnail.CacheOption = BitmapCacheOption.OnLoad;
        thumbnail.DecodePixelWidth = 220;
        thumbnail.StreamSource = stream;
        thumbnail.EndInit();
        thumbnail.Freeze();
        return thumbnail;
    }
}
