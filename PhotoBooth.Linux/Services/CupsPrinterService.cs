using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class CupsPrinterService : IPrinterService
{
    private readonly string _lpCommand;
    private readonly string _lpstatCommand;
    private readonly string _media;
    private readonly string _queueName;
    private PrinterOption? _qualityOption;
    private PrinterOption? _cutOption;
    private string _quality = "Fast";
    private string _cutMode = "Standard";

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

    public bool SupportsQualitySelection => _qualityOption is not null;

    public bool SupportsTwoInchCut => _cutOption is not null;

    public string DriverOptionsSummary =>
        $"Качество: {(SupportsQualitySelection ? "доступно" : "не предоставлено драйвером")}; " +
        $"рез 5×15: {(SupportsTwoInchCut ? "доступен" : "не предоставлен драйвером")}.";

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

        CommandResult serviceResult = await CommandRunner.RunAsync(
            lpstatCommand,
            ["-r"],
            TimeSpan.FromSeconds(6),
            cancellationToken);
        if (!serviceResult.Success ||
            !serviceResult.StandardOutput.Contains(
                "scheduler is running",
                StringComparison.OrdinalIgnoreCase))
        {
            return (null, "служба CUPS не запущена");
        }

        CommandResult result = await CommandRunner.RunAsync(
            lpstatCommand,
            ["-p"],
            TimeSpan.FromSeconds(6),
            cancellationToken);
        if (!result.Success)
        {
            return (null, "в CUPS пока нет настроенной очереди принтера");
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

        CupsPrinterService service = new(
                lpCommand,
                lpstatCommand,
                selected,
                media);
        await service.LoadDriverOptionsAsync(cancellationToken);
        return (service, string.Empty);
    }

    public void Configure(string quality, string cutMode)
    {
        _quality = quality;
        _cutMode = cutMode;
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
        AddSelectedOption(arguments, _qualityOption, _quality == "High");
        AddSelectedOption(arguments, _cutOption, _cutMode == "TwoInch");
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

    private async Task LoadDriverOptionsAsync(CancellationToken cancellationToken)
    {
        const string lpoptions = "lpoptions";
        if (!CommandRunner.Exists(lpoptions))
        {
            return;
        }

        CommandResult result = await CommandRunner.RunAsync(
            lpoptions,
            ["-p", _queueName, "-l"],
            TimeSpan.FromSeconds(8),
            cancellationToken);
        if (!result.Success)
        {
            return;
        }

        foreach (string line in result.StandardOutput.Split(
                     '\n',
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            PrinterOption? option = ParseOption(line);
            if (option is null)
            {
                continue;
            }

            string searchable = $"{option.Key} {option.Description}";
            if (_qualityOption is null &&
                (searchable.Contains("resolution", StringComparison.OrdinalIgnoreCase) ||
                 searchable.Contains("quality", StringComparison.OrdinalIgnoreCase)))
            {
                string? fast = option.Values.FirstOrDefault(value =>
                    value.Contains("300x300", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("fast", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("draft", StringComparison.OrdinalIgnoreCase));
                string? high = option.Values.FirstOrDefault(value =>
                    value.Contains("300x600", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("600x600", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("high", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("fine", StringComparison.OrdinalIgnoreCase));
                if (fast is not null && high is not null)
                {
                    _qualityOption = option with { StandardValue = fast, AlternateValue = high };
                }
            }

            if (_cutOption is null &&
                (searchable.Contains("cutter", StringComparison.OrdinalIgnoreCase) ||
                 searchable.Contains("cut", StringComparison.OrdinalIgnoreCase)))
            {
                string? twoInch = option.Values.FirstOrDefault(value =>
                    value.Contains("2inch", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("2-inch", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("2x6", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("5x15", StringComparison.OrdinalIgnoreCase));
                string? standard = option.Values.FirstOrDefault(value =>
                    value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("normal", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("standard", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("off", StringComparison.OrdinalIgnoreCase)) ?? option.DefaultValue;
                if (twoInch is not null && standard is not null)
                {
                    _cutOption = option with
                    {
                        StandardValue = standard,
                        AlternateValue = twoInch
                    };
                }
            }
        }
    }

    private static PrinterOption? ParseOption(string line)
    {
        int slash = line.IndexOf('/');
        int colon = line.IndexOf(':');
        if (slash <= 0 || colon <= slash)
        {
            return null;
        }

        string key = line[..slash].Trim();
        string description = line[(slash + 1)..colon].Trim();
        string[] rawValues = line[(colon + 1)..]
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string? defaultValue = rawValues
            .FirstOrDefault(value => value.StartsWith('*'))?
            .TrimStart('*');
        string[] values = rawValues
            .Select(value => value.TrimStart('*'))
            .ToArray();
        return values.Length == 0
            ? null
            : new PrinterOption(key, description, values, defaultValue, null, null);
    }

    private static void AddSelectedOption(
        List<string> arguments,
        PrinterOption? option,
        bool useAlternate)
    {
        string? value = useAlternate
            ? option?.AlternateValue
            : option?.StandardValue;
        if (option is null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add("-o");
        arguments.Add($"{option.Key}={value}");
    }

    private sealed record PrinterOption(
        string Key,
        string Description,
        IReadOnlyList<string> Values,
        string? DefaultValue,
        string? StandardValue,
        string? AlternateValue);
}
