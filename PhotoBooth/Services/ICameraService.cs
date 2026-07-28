namespace PhotoBooth.Services;

public interface ICameraService
{
    bool IsDemo { get; }

    string DisplayName { get; }

    IReadOnlyList<string> PrepareShots(
        string sourcePath,
        string originalsPath,
        int shotCount);
}
