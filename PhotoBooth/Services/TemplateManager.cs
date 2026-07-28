using System.IO;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class TemplateManager
{
    public IReadOnlyList<TemplateInfo> GetTemplates(string templatesPath)
    {
        if (!Directory.Exists(templatesPath))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(templatesPath, "*.json", SearchOption.AllDirectories)
            .Select(CreateTemplateInfo)
            .Where(template => template is not null)
            .Cast<TemplateInfo>()
            .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static TemplateInfo? CreateTemplateInfo(string jsonPath)
    {
        string folderPath = Path.GetDirectoryName(jsonPath)!;
        string templateName = Path.GetFileNameWithoutExtension(jsonPath);
        int requiredShotCount = LoadRequiredShotCount(jsonPath);
        string? overlayPath = Directory
            .EnumerateFiles(folderPath, "*.png")
            .FirstOrDefault(path =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    templateName,
                    StringComparison.OrdinalIgnoreCase));

        if (overlayPath is null)
        {
            return null;
        }

        return new TemplateInfo
        {
            Name = templateName,
            FolderPath = folderPath,
            PreviewPath = overlayPath,
            JsonPath = jsonPath,
            RequiredShotCount = requiredShotCount
        };
    }

    private static int LoadRequiredShotCount(string jsonPath)
    {
        try
        {
            return new TemplateDefinitionService()
                .Load(jsonPath)
                .RequiredShotCount;
        }
        catch
        {
            return 0;
        }
    }
}
