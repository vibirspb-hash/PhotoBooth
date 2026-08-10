using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PhotoBooth.Services;

public sealed class GPhotoCameraService : IPhotoCaptureService
{
    private const int SigInt = 2;

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
    private byte[]? _latestPreviewFrame;
    private string _liveViewError = string.Empty;
    private bool _viewfinderEnabled;

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
        Volatile.Write(ref _latestPreviewFrame, null);
    }

    public async Task<byte[]?> CapturePreviewAsync(
        int shotNumber,
        CancellationToken cancellationToken = default)
    {
        await _cameraLock.WaitAsync(cancellationToken);
        try
        {
            EnsurePrepared();
            await StartLiveViewAsync(cancellationToken);

            DateTime deadline = DateTime.UtcNow.AddSeconds(6);
            while (Volatile.Read(ref _latestPreviewFrame) is null &&
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

            byte[]? frame = Volatile.Read(ref _latestPreviewFrame);
            if (frame is null)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(_liveViewError)
                        ? "Canon не передал кадр Live View за 6 секунд."
                        : _liveViewError);
            }

            return frame;
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
        Stopwatch captureTimer = Stopwatch.StartNew();
        await _cameraLock.WaitAsync(cancellationToken);
        try
        {
            EnsurePrepared();
            await StopLiveViewCoreAsync(cancellationToken, disableViewfinder: false);
            await Task.Delay(200, cancellationToken);
            string path = Path.Combine(_originalsPath, $"shot-{shotNumber:00}.jpg");
            CommandResult result = await CaptureImageAsync(path, cancellationToken);
            if (!result.Success || !File.Exists(path))
            {
                Console.Error.WriteLine(
                    $"{DateTime.Now:O} Canon capture attempt 1 failed: {result.CombinedOutput}");
                CommandResult resetResult = await CommandRunner.RunAsync(
                    _command,
                    ["--set-config", "viewfinder=0"],
                    TimeSpan.FromSeconds(8),
                    cancellationToken);
                if (resetResult.Success)
                {
                    _viewfinderEnabled = false;
                }

                await Task.Delay(900, cancellationToken);
                result = await CaptureImageAsync(path, cancellationToken);
            }

            if (!result.Success || !File.Exists(path))
            {
                throw new InvalidOperationException(
                    CleanError(result, "Canon не передал фотографию"));
            }

            Console.Error.WriteLine(
                $"{DateTime.Now:O} Canon captured shot {shotNumber} in {captureTimer.ElapsedMilliseconds} ms.");
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

    public async Task<IReadOnlyList<CameraSettingDefinition>> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await _cameraLock.WaitAsync(cancellationToken);
        try
        {
            await StopLiveViewCoreAsync(cancellationToken);
            List<CameraSettingDefinition> settings = [];
            foreach (string setting in SettingNames)
            {
                CommandResult result = await CommandRunner.RunAsync(
                    _command,
                    ["--get-config", setting],
                    TimeSpan.FromSeconds(8),
                    cancellationToken);
                if (!result.Success)
                {
                    settings.Add(new CameraSettingDefinition(
                        setting,
                        GetSettingTitle(setting),
                        "недоступно",
                        [],
                        CleanError(result, $"Настройка {setting} недоступна")));
                    continue;
                }

                string current = ReadConfigValue(result.StandardOutput, "Current:") ??
                    "недоступно";
                List<string> choices = result.StandardOutput
                    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.StartsWith("Choice:", StringComparison.OrdinalIgnoreCase))
                    .Select(ParseChoiceValue)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (current != "недоступно" &&
                    !choices.Contains(current, StringComparer.OrdinalIgnoreCase))
                {
                    choices.Insert(0, current);
                }

                settings.Add(new CameraSettingDefinition(
                    setting,
                    GetSettingTitle(setting),
                    current,
                    choices,
                    string.Empty));
            }

            return settings;
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    public async Task<string> SetSettingAsync(
        string setting,
        string value,
        CancellationToken cancellationToken = default)
    {
        if (!SettingNames.Contains(setting, StringComparer.OrdinalIgnoreCase))
        {
            return $"Неизвестная настройка Canon: {setting}.";
        }

        await _cameraLock.WaitAsync(cancellationToken);
        try
        {
            await StopLiveViewCoreAsync(cancellationToken);
            CommandResult result = await CommandRunner.RunAsync(
                _command,
                ["--set-config", $"{setting}={value}"],
                TimeSpan.FromSeconds(10),
                cancellationToken);
            return result.Success
                ? string.Empty
                : CleanError(result, $"Не удалось установить {GetSettingTitle(setting)}");
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> ApplySettingsAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        await _cameraLock.WaitAsync(cancellationToken);
        try
        {
            await StopLiveViewCoreAsync(cancellationToken);
            List<string> errors = [];
            foreach ((string setting, string value) in values)
            {
                if (!SettingNames.Contains(setting, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Неизвестная настройка Canon: {setting}.");
                    continue;
                }

                CommandResult result = await CommandRunner.RunAsync(
                    _command,
                    ["--set-config", $"{setting}={value}"],
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
                if (!result.Success)
                {
                    errors.Add(CleanError(
                        result,
                        $"Не удалось установить {GetSettingTitle(setting)}"));
                }
            }

            return errors;
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    private static string? ReadConfigValue(string output, string prefix) =>
        output
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?
            .Split(':', 2)[1]
            .Trim();

    private static string ParseChoiceValue(string line)
    {
        string choice = line["Choice:".Length..].Trim();
        int separator = choice.IndexOf(' ');
        return separator >= 0 ? choice[(separator + 1)..].Trim() : choice;
    }

    private void EnsurePrepared()
    {
        if (string.IsNullOrWhiteSpace(_originalsPath))
        {
            throw new InvalidOperationException("Папка для фотографий не подготовлена.");
        }
    }

    private Task<CommandResult> CaptureImageAsync(
        string path,
        CancellationToken cancellationToken) =>
        CommandRunner.RunAsync(
            _command,
            [
                "--capture-image-and-download",
                "--filename", path,
                "--force-overwrite"
            ],
            TimeSpan.FromSeconds(25),
            cancellationToken);

    private async Task StartLiveViewAsync(CancellationToken cancellationToken)
    {
        if (_liveViewProcess is { HasExited: false })
        {
            return;
        }

        await StopLiveViewCoreAsync(cancellationToken, disableViewfinder: false);
        if (!_viewfinderEnabled)
        {
            CommandResult viewfinderResult = await CommandRunner.RunAsync(
                _command,
                ["--set-config", "viewfinder=1"],
                TimeSpan.FromSeconds(8),
                cancellationToken);
            if (!viewfinderResult.Success)
            {
                throw new InvalidOperationException(
                    CleanError(viewfinderResult, "Canon не включил Live View"));
            }

            _viewfinderEnabled = true;
        }

        _liveViewError = string.Empty;
        Volatile.Write(ref _latestPreviewFrame, null);

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
            _liveViewCancellation.Token);
        Console.Error.WriteLine(
            $"{DateTime.Now:O} Canon Live View stream started (PID {process.Id}).");
    }

    private async Task StopLiveViewCoreAsync(
        CancellationToken cancellationToken,
        bool disableViewfinder = true)
    {
        Stopwatch stopTimer = Stopwatch.StartNew();
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
                    // gPhoto2 restores the camera state only when movie capture
                    // receives the same SIGINT as an interactive Ctrl+C.
                    SendSignal(process.Id, SigInt);
                    Task exited = process.WaitForExitAsync();
                    if (await Task.WhenAny(exited, Task.Delay(2000)) != exited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
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

        if (disableViewfinder && _viewfinderEnabled)
        {
            CommandResult result = await CommandRunner.RunAsync(
                _command,
                ["--set-config", "viewfinder=0"],
                TimeSpan.FromSeconds(8),
                cancellationToken);
            if (result.Success)
            {
                _viewfinderEnabled = false;
            }
        }

        if (process is not null)
        {
            Console.Error.WriteLine(
                $"{DateTime.Now:O} Canon Live View stream stopped in {stopTimer.ElapsedMilliseconds} ms; " +
                $"viewfinder disabled: {disableViewfinder}.");
        }
    }

    private async Task PumpLiveViewAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        byte[] buffer = new byte[64 * 1024];
        using MemoryStream frame = new();
        bool inFrame = false;
        byte previous = 0;
        int frameCount = 0;
        Stopwatch frameRateTimer = Stopwatch.StartNew();

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

                int segmentStart = 0;
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
                            segmentStart = index + 1;
                        }
                    }
                    else if (previous == 0xFF && current == 0xD9)
                    {
                        if (index >= segmentStart)
                        {
                            frame.Write(buffer, segmentStart, index - segmentStart + 1);
                        }

                        Volatile.Write(ref _latestPreviewFrame, frame.ToArray());
                        frameCount++;
                        if (frameRateTimer.Elapsed >= TimeSpan.FromSeconds(5))
                        {
                            double framesPerSecond = frameCount / frameRateTimer.Elapsed.TotalSeconds;
                            Console.Error.WriteLine(
                                $"{DateTime.Now:O} Canon Live View: {framesPerSecond:F1} FPS.");
                            frameCount = 0;
                            frameRateTimer.Restart();
                        }

                        frame.SetLength(0);
                        inFrame = false;
                        segmentStart = index + 1;
                    }

                    previous = current;
                }

                if (inFrame && segmentStart < count)
                {
                    frame.Write(buffer, segmentStart, count - segmentStart);
                }

                if (frame.Length > 20 * 1024 * 1024)
                {
                    frame.SetLength(0);
                    inFrame = false;
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

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int processId, int signal);

    private static void SendSignal(int processId, int signal)
    {
        if (kill(processId, signal) != 0)
        {
            throw new InvalidOperationException(
                $"Не удалось корректно остановить Live View (errno {Marshal.GetLastPInvokeError()}).");
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

public sealed record CameraSettingDefinition(
    string Key,
    string Title,
    string CurrentValue,
    IReadOnlyList<string> Choices,
    string Error);
