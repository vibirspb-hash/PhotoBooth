using System.IO;
using System.Text.Json;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class SessionManager
{
    private const string StateFileName = "active-session.json";
    private const string SessionFileName = "session.json";

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

        SaveSessionMetadata(session);
        SaveActiveSession(outputRootPath, session);
        return session;
    }

    public IReadOnlyList<PhotoSession> ListSessions(string outputRootPath)
    {
        if (!Directory.Exists(outputRootPath))
        {
            return [];
        }

        return Directory
            .EnumerateDirectories(outputRootPath)
            .Select(LoadSession)
            .Where(session => session is not null)
            .Cast<PhotoSession>()
            .OrderByDescending(session => session.StartedAt)
            .ToList();
    }

    public void SetActiveSession(string outputRootPath, PhotoSession session)
    {
        SaveSessionMetadata(session);
        SaveActiveSession(outputRootPath, session);
    }

    private static PhotoSession? LoadSession(string folderPath)
    {
        try
        {
            string metadataPath = Path.Combine(folderPath, SessionFileName);
            if (File.Exists(metadataPath))
            {
                PhotoSession? stored = JsonSerializer.Deserialize<PhotoSession>(
                    File.ReadAllText(metadataPath),
                    JsonOptions);
                if (stored is not null)
                {
                    return new PhotoSession
                    {
                        Name = stored.Name,
                        FolderPath = folderPath,
                        StartedAt = stored.StartedAt
                    };
                }
            }

            string folderName = Path.GetFileName(folderPath);
            string inferredName = folderName.Length > 11 && folderName[10] == '_'
                ? folderName[11..]
                : folderName;
            return new PhotoSession
            {
                Name = inferredName.Replace('_', ' '),
                FolderPath = folderPath,
                StartedAt = Directory.GetCreationTime(folderPath)
            };
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void SaveSessionMetadata(PhotoSession session)
    {
        string metadataPath = Path.Combine(session.FolderPath, SessionFileName);
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(session, JsonOptions));
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
