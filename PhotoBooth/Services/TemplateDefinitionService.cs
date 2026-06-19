using System.IO;
using System.Text.Json;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class TemplateDefinitionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TemplateDefinition Load(string jsonPath)
    {
        string json = File.ReadAllText(jsonPath);
        return JsonSerializer.Deserialize<TemplateDefinition>(json, JsonOptions) ?? new TemplateDefinition();
    }
}
