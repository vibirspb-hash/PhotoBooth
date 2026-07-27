using System.IO;
using System.Text.Json;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class ConfigService
{
    private const string ConfigFileName = "config.json";

    public AppConfig Load()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);

        if (!File.Exists(configPath))
        {
            return new AppConfig();
        }

        string json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }
}
