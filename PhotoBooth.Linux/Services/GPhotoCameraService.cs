using System.Text;

namespace PhotoBooth.Services;

public sealed class GPhotoCameraService : IPhotoCaptureService
{
    private static readonly string[] SettingNames =
    [
        "iso",
        "aperture",
        "shutterspeed",
        "whitebalance"
    ];

    private readonly string _command;
    private readonly SemaphoreSlim _cameraLock = new(1, 1);
    private string _originalsPath = string.Empty;

    private GPhotoCameraService(string command, string displayName)
    {
        _command = command;
        DisplayName = displayName;
    }

    public bool IsDemo => false;

    public string DisplayName { get; }

    public string Status => $"Подключена: {DisplayName} через gPhoto2.";

    public static async Task<(GPhotoCameraService? Service, string Error)> TryCreateAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        if (!CommandRunner.Exists(command))
        {
            return (null, $"команда {command} не установлена");
        }

        CommandResult result = await CommandRunner.RunAsync(
            command,
            ["--auto-detect"],
            TimeSpan.FromSeconds(8),
            cancellationToken);
        if (!result.Success)
        {
            return (null, CleanError(result, "gPhoto2 не смог проверить USB-камеру"));
        }

        string? cameraLine = result.StandardOutput
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line =>
                line.Contains("usb:", StringComparison.OrdinalIgnoreCase));
        if (cameraLine is null)
        {
            return (null, "Canon не найден по USB");
        }

        int usbIndex = cameraLine.IndexOf("usb:", StringComparison.OrdinalIgnoreCase);
        string displayName = cameraLine[..usbIndex].Trim();
        return (
            new GPhotoCameraService(
                command,
                string.IsNullOrWhiteSpace(displayName) ? "Canon EOS" : displayName),
            string.Empty);
    }

    public void PrepareCapture(
        string demoPhotosPath,
        string originalsPath,
        int shotCount)
    {
        Directory.CreateDirectory(originalsPath);
        _originalsPath = originalsPath;
    }

    public async Task<string?> CapturePreviewAsync(
        int shotNumber,
        CancellationToken cancellationToken = default)
    {
        await _cameraLock.WaitAsync(cancellationToken);
        try
        {
            EnsurePrepared();
            string path = Path.Combine(_originalsPath, ".live-preview.jpg");
            CommandResult result = await CommandRunner.RunAsync(
                _command,
                [
                    "--capture-preview",
                    "--filename", path,
                    "--force-overwrite"
                ],
                TimeSpan.FromSeconds(8),
                cancellationToken);

            return result.Success && File.Exists(path) ? path : null;
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    public async Task<string> CapturePhotoAsync(
        int shotNumber,
        CancellationToken cancellationToken = default)
    {
        await _cameraLock.WaitAsync(cancellationToken);
        try
        {
            EnsurePrepared();
            string path = Path.Combine(_originalsPath, $"shot-{shotNumber:00}.jpg");
            CommandResult result = await CommandRunner.RunAsync(
                _command,
                [
                    "--capture-image-and-download",
                    "--filename", path,
                    "--force-overwrite"
                ],
                TimeSpan.FromSeconds(30),
                cancellationToken);
            if (!result.Success || !File.Exists(path))
            {
                throw new InvalidOperationException(
                    CleanError(result, "Canon не передал фотографию"));
            }

            return path;
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    public async Task<string> GetSettingsSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        StringBuilder summary = new();
        summary.AppendLine(Status);

        foreach (string setting in SettingNames)
        {
            CommandResult result = await CommandRunner.RunAsync(
                _command,
                ["--get-config", setting],
                TimeSpan.FromSeconds(6),
                cancellationToken);
            string current = result.StandardOutput
                .Split('\n', StringSplitOptions.TrimEntries)
                .FirstOrDefault(line => line.StartsWith("Current:", StringComparison.OrdinalIgnoreCase))?
                .Split(':', 2)[1]
                .Trim() ?? "недоступно";
            summary.Append($"{GetSettingTitle(setting)}: {current} · ");
        }

        return summary.ToString().TrimEnd(' ', '·', '\r', '\n');
    }

    private void EnsurePrepared()
    {
        if (string.IsNullOrWhiteSpace(_originalsPath))
        {
            throw new InvalidOperationException("Папка для фотографий не подготовлена.");
        }
    }

    private static string GetSettingTitle(string setting) => setting switch
    {
        "iso" => "ISO",
        "aperture" => "Диафрагма",
        "shutterspeed" => "Выдержка",
        "whitebalance" => "Баланс белого",
        _ => setting
    };

    private static string CleanError(CommandResult result, string fallback)
    {
        string message = result.CombinedOutput
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? fallback;
        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }
}
