using System.Diagnostics;

namespace PhotoBooth.Services;

public sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Success => ExitCode == 0;

    public string CombinedOutput =>
        string.Join(
            Environment.NewLine,
            new[] { StandardOutput, StandardError }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
        .Trim();
}

public static class CommandRunner
{
    public static bool Exists(string command)
    {
        if (Path.IsPathRooted(command))
        {
            return File.Exists(command);
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        return (path ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Combine(folder, command)));
    }

    public static async Task<CommandResult> RunAsync(
        string command,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = new(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["LC_ALL"] = "C";

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception exception)
        {
            return new CommandResult(-1, string.Empty, exception.Message);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may have exited between cancellation and cleanup.
            }

            string reason = timeoutSource.IsCancellationRequested
                ? $"Команда не завершилась за {timeout.TotalSeconds:0} сек."
                : "Команда отменена.";
            return new CommandResult(-1, await outputTask, reason);
        }

        return new CommandResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }
}
