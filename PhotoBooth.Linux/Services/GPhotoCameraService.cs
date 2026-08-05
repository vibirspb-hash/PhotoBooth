using System.Diagnostics;
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
    private Process? _liveViewProcess;
    private CancellationTokenSource? _liveViewCancellation;
    private Task? _liveViewPumpTask;
    private string _latestPreviewPath = string.Empty;
    private string _liveViewError = string.Empty;

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
            await StartLiveViewAsync(cancellationToken);

            DateTime deadline = DateTime.UtcNow.AddSeconds(6);
            while (!File.Exists(_latestPreviewPath) &&
                   DateTime.UtcNow < deadline &&
                   !cancellationToken.IsCancellationRequested)
            {
                if (_liveViewProcess?.HasExited == true)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(_liveViewError)
                            ? "Canon завершил Live View без изображения."
                            : _liveViewError);
                }

                await Task.Delay(100, cancellationToken);
            }

            if (!File.Exists(_latestPreviewPath))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(_liveViewError)
                        ? "Canon не передал кадр Live View за 6 секунд."
                        : _liveViewError);
            }

            return _latestPreviewPath;
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
            await StopLiveViewCoreAsync(cancellationToken);
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

    public async Task StopPreviewAsync(
        CancellationToken cancellationToken = default)
    {
        await _cameraLock.WaitAsync(cancellationToken);
        try
        {
            await StopLiveViewCoreAsync(cancellationToken);
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

    private async Task StartLiveViewAsync(CancellationToken cancellationToken)
    {
        if (_liveViewProcess is { HasExited: false })
        {
            return;
        }

        await StopLiveViewCoreAsync(cancellationToken);
        await CommandRunner.RunAsync(
            _command,
            ["--set-config", "viewfinder=1"],
            TimeSpan.FromSeconds(8),
            cancellationToken);

        _latestPreviewPath = Path.Combine(_originalsPath, ".live-preview.jpg");
        _liveViewError = string.Empty;
        TryDelete(_latestPreviewPath);

        ProcessStartInfo startInfo = new(_command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["LC_ALL"] = "C";
        startInfo.ArgumentList.Add("--stdout");
        startInfo.ArgumentList.Add("--capture-movie");

        Process process = new() { StartInfo = startInfo };
        process.Start();
        _liveViewProcess = process;
        _liveViewCancellation = new CancellationTokenSource();
        _liveViewPumpTask = PumpLiveViewAsync(
            process,
            _latestPreviewPath,
            _liveViewCancellation.Token);
    }

    private async Task StopLiveViewCoreAsync(CancellationToken cancellationToken)
    {
        Process? process = _liveViewProcess;
        Task? pumpTask = _liveViewPumpTask;
        _liveViewProcess = null;
        _liveViewPumpTask = null;
        _liveViewCancellation?.Cancel();
        _liveViewCancellation?.Dispose();
        _liveViewCancellation = null;

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // The process already exited while the stop request was handled.
            }
        }

        if (pumpTask is not null)
        {
            try
            {
                await pumpTask;
            }
            catch (Exception exception) when (
                exception is OperationCanceledException or IOException)
            {
                // Stopping Live View intentionally cancels the stream reader.
            }
        }

        process?.Dispose();

        await CommandRunner.RunAsync(
            _command,
            ["--set-config", "viewfinder=0"],
            TimeSpan.FromSeconds(8),
            cancellationToken);
    }

    private async Task PumpLiveViewAsync(
        Process process,
        string outputPath,
        CancellationToken cancellationToken)
    {
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        byte[] buffer = new byte[64 * 1024];
        using MemoryStream frame = new();
        bool inFrame = false;
        byte previous = 0;
        DateTime lastSavedAt = DateTime.MinValue;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int count = await process.StandardOutput.BaseStream.ReadAsync(
                    buffer,
                    cancellationToken);
                if (count == 0)
                {
                    break;
                }

                for (int index = 0; index < count; index++)
                {
                    byte current = buffer[index];
                    if (!inFrame)
                    {
                        if (previous == 0xFF && current == 0xD8)
                        {
                            frame.SetLength(0);
                            frame.WriteByte(0xFF);
                            frame.WriteByte(0xD8);
                            inFrame = true;
                        }
                    }
                    else
                    {
                        frame.WriteByte(current);
                        if (previous == 0xFF && current == 0xD9)
                        {
                            if (DateTime.UtcNow - lastSavedAt >= TimeSpan.FromMilliseconds(150))
                            {
                                await SaveFrameAsync(
                                    outputPath,
                                    frame.ToArray(),
                                    cancellationToken);
                                lastSavedAt = DateTime.UtcNow;
                            }

                            frame.SetLength(0);
                            inFrame = false;
                        }
                        else if (frame.Length > 20 * 1024 * 1024)
                        {
                            frame.SetLength(0);
                            inFrame = false;
                        }
                    }

                    previous = current;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            string error = await errorTask;
            if (!cancellationToken.IsCancellationRequested &&
                !string.IsNullOrWhiteSpace(error))
            {
                _liveViewError = error
                    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault() ?? "Canon завершил Live View.";
            }
        }
    }

    private static async Task SaveFrameAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        string temporaryPath = path + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
        catch (IOException)
        {
            // A stale preview is harmless; the next complete frame replaces it.
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
