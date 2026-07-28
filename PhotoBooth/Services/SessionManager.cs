using System.IO;
using System.Text.Json;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class SessionManager
{
    private const string StateFileName = "active-session.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public PhotoSession? LoadActiveSession(string outputRootPath)
    {
        string statePath = Path.Combine(outputRootPath, StateFileName);

        if (!File.Exists(statePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(statePath);
            PhotoSession? session = JsonSerializer.Deserialize<PhotoSession>(json, JsonOptions);

            return session is not null && Directory.Exists(session.FolderPath)
                ? session
                : null;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public PhotoSession CreateSession(string outputRootPath, string sessionName)
    {
        string trimmedName = sessionName.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("Введите название сессии.");
        }

        Directory.CreateDirectory(outputRootPath);

        string safeName = SanitizeFolderName(trimmedName);
        string baseFolderName = $"{DateTime.Now:yyyy-MM-dd}_{safeName}";
        string sessionFolderPath = GetUniqueFolderPath(outputRootPath, baseFolderName);

        Directory.CreateDirectory(Path.Combine(sessionFolderPath, "Photos"));
        Directory.CreateDirectory(Path.Combine(sessionFolderPath, "Prints"));

        PhotoSession session = new()
        {
            Name = trimmedName,
            FolderPath = sessionFolderPath,
            StartedAt = DateTime.Now
        };

        SaveActiveSession(outputRootPath, session);
        return session;
    }

    private static void SaveActiveSession(string outputRootPath, PhotoSession session)
    {
        Directory.CreateDirectory(outputRootPath);
        string statePath = Path.Combine(outputRootPath, StateFileName);
        string temporaryStatePath = $"{statePath}.tmp";
        string json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(temporaryStatePath, json);
        File.Move(temporaryStatePath, statePath, true);
    }

    private static string SanitizeFolderName(string name)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = string.Concat(
            name.Select(character => invalidCharacters.Contains(character) ? '_' : character));

        sanitized = sanitized.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "Session" : sanitized;
    }

    private static string GetUniqueFolderPath(string outputRootPath, string baseFolderName)
    {
        string candidatePath = Path.Combine(outputRootPath, baseFolderName);

        if (!Directory.Exists(candidatePath))
        {
            return candidatePath;
        }

        int suffix = 2;

        while (Directory.Exists($"{candidatePath}_{suffix}"))
        {
            suffix++;
        }

        return $"{candidatePath}_{suffix}";
    }
}
