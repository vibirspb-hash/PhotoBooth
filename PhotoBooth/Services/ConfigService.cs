using System.IO;
using System.Text.Json;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class ConfigService
{
    private const string ConfigFileName = "config.json";

    public AppConfig Load()
    {
        if (!File.Exists(ConfigFileName))
        {
            return new AppConfig();
        }

        string json = File.ReadAllText(ConfigFileName);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }
}
