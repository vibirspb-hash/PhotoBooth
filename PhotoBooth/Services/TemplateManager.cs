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
            .GetDirectories(templatesPath)
            .Select(CreateTemplateInfo)
            .OrderBy(template => template.Name)
            .ToList();
    }

    private static TemplateInfo CreateTemplateInfo(string folderPath)
    {
        string? previewPath = Directory
            .GetFiles(folderPath, "preview.*")
            .FirstOrDefault();

        string? jsonPath = Directory
            .GetFiles(folderPath, "*.json")
            .FirstOrDefault();

        return new TemplateInfo
        {
            Name = Path.GetFileName(folderPath),
            FolderPath = folderPath,
            PreviewPath = previewPath,
            JsonPath = jsonPath
        };
    }
}
