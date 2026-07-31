namespace PhotoBooth.Services;

public interface IPhotoCaptureService
{
    bool IsDemo { get; }

    string DisplayName { get; }

    string Status { get; }

    void PrepareCapture(
        string demoPhotosPath,
        string originalsPath,
        int shotCount);

    Task<string?> CapturePreviewAsync(
        int shotNumber,
        CancellationToken cancellationToken = default);

    Task<string> CapturePhotoAsync(
        int shotNumber,
        CancellationToken cancellationToken = default);

    Task<string> GetSettingsSummaryAsync(
        CancellationToken cancellationToken = default);
}
