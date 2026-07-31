using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class CupsPrinterService : IPrinterService
{
    private readonly string _lpCommand;
    private readonly string _lpstatCommand;
    private readonly string _media;
    private readonly string _queueName;

    private CupsPrinterService(
        string lpCommand,
        string lpstatCommand,
        string queueName,
        string media)
    {
        _lpCommand = lpCommand;
        _lpstatCommand = lpstatCommand;
        _queueName = queueName;
        _media = media;
    }

    public bool IsDemo => false;

    public string DisplayName => _queueName;

    public string Status
    {
        get
        {
            CommandResult result = CommandRunner.RunAsync(
                    _lpstatCommand,
                    ["-p", _queueName],
                    TimeSpan.FromSeconds(5))
                .GetAwaiter()
                .GetResult();
            return result.Success
                ? result.StandardOutput.Trim()
                : $"Очередь {_queueName} недоступна: {result.CombinedOutput}";
        }
    }

    public static async Task<(CupsPrinterService? Service, string Error)> TryCreateAsync(
        string lpCommand,
        string lpstatCommand,
        string configuredQueue,
        string media,
        CancellationToken cancellationToken = default)
    {
        if (!CommandRunner.Exists(lpCommand) || !CommandRunner.Exists(lpstatCommand))
        {
            return (null, "CUPS не установлен");
        }

        CommandResult result = await CommandRunner.RunAsync(
            lpstatCommand,
            ["-p"],
            TimeSpan.FromSeconds(6),
            cancellationToken);
        if (!result.Success)
        {
            return (null, "служба CUPS не запущена");
        }

        List<string> queues = result.StandardOutput
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseQueueName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        string? selected = !string.IsNullOrWhiteSpace(configuredQueue)
            ? queues.FirstOrDefault(name =>
                name.Equals(configuredQueue, StringComparison.OrdinalIgnoreCase))
            : queues.FirstOrDefault(name =>
                name.Contains("rx1", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("dnp", StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return (null, "очередь DNP DS-RX1 не найдена в CUPS");
        }

        return (
            new CupsPrinterService(
                lpCommand,
                lpstatCommand,
                selected,
                media),
            string.Empty);
    }

    public PrintResult Print(string imagePath, int copies)
    {
        if (!File.Exists(imagePath))
        {
            return new PrintResult(false, "Файл для печати не найден.");
        }

        if (copies is < 1 or > 3)
        {
            return new PrintResult(false, "Можно напечатать от одной до трёх копий.");
        }

        List<string> arguments =
        [
            "-d", _queueName,
            "-n", copies.ToString(),
            "-o", "fit-to-page"
        ];
        if (!string.IsNullOrWhiteSpace(_media))
        {
            arguments.Add("-o");
            arguments.Add($"media={_media}");
        }
        arguments.Add(imagePath);

        CommandResult result = CommandRunner.RunAsync(
                _lpCommand,
                arguments,
                TimeSpan.FromSeconds(15))
            .GetAwaiter()
            .GetResult();
        return result.Success
            ? new PrintResult(true, result.StandardOutput.Trim())
            : new PrintResult(
                false,
                string.IsNullOrWhiteSpace(result.CombinedOutput)
                    ? "CUPS не принял задание печати."
                    : result.CombinedOutput);
    }

    private static string ParseQueueName(string line)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[0].Equals("printer", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : string.Empty;
    }
}
