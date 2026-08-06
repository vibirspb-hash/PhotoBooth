using System.Text.Json;

namespace PhotoBooth.Services;

public sealed class PrintAuditService
{
    private const string FileName = "print-jobs.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void Record(string sessionFolderPath, string imagePath, int copies)
    {
        if (copies < 1 || string.IsNullOrWhiteSpace(sessionFolderPath))
        {
            return;
        }

        List<PrintAuditEntry> entries = Load(sessionFolderPath).ToList();
        entries.Add(new PrintAuditEntry
        {
            PrintedAt = DateTime.Now,
            ImageFileName = Path.GetFileName(imagePath),
            Copies = copies
        });

        string auditPath = Path.Combine(sessionFolderPath, FileName);
        string temporaryPath = $"{auditPath}.tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(entries, JsonOptions));
        File.Move(temporaryPath, auditPath, true);
    }

    public int GetTotalCopies(string sessionFolderPath) =>
        Load(sessionFolderPath).Sum(entry => Math.Max(0, entry.Copies));

    public int GetCopiesForImage(string sessionFolderPath, string imagePath)
    {
        string fileName = Path.GetFileName(imagePath);
        return Load(sessionFolderPath)
            .Where(entry => entry.ImageFileName.Equals(
                fileName,
                StringComparison.OrdinalIgnoreCase))
            .Sum(entry => Math.Max(0, entry.Copies));
    }

    private static IReadOnlyList<PrintAuditEntry> Load(string sessionFolderPath)
    {
        string auditPath = Path.Combine(sessionFolderPath, FileName);
        if (!File.Exists(auditPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PrintAuditEntry>>(
                       File.ReadAllText(auditPath),
                       JsonOptions) ?? [];
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private sealed class PrintAuditEntry
    {
        public DateTime PrintedAt { get; set; }

        public string ImageFileName { get; set; } = string.Empty;

        public int Copies { get; set; }
    }
}
