namespace PhotoBooth.Services;

public sealed class DemoPhotoCaptureService : IPhotoCaptureService
{
    private readonly SkiaDemoCameraService _camera = new();
    private readonly string _status;
    private IReadOnlyList<string> _shots = [];

    public DemoPhotoCaptureService(string? fallbackReason = null)
    {
        _status = string.IsNullOrWhiteSpace(fallbackReason)
            ? "Деморежим включён в config.json."
            : $"Деморежим: {fallbackReason}";
    }

    public bool IsDemo => true;

    public string DisplayName => "Демо-камера";

    public string Status => _status;

    public void PrepareCapture(
        string demoPhotosPath,
        string originalsPath,
        int shotCount)
    {
        _shots = _camera.PrepareShots(
            demoPhotosPath,
            originalsPath,
            shotCount);
    }

    public Task<string?> CapturePreviewAsync(
        int shotNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(GetShot(shotNumber));

    public Task<string> CapturePhotoAsync(
        int shotNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetShot(shotNumber));

    public Task<string> GetSettingsSummaryAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Status);

    private string GetShot(int shotNumber)
    {
        if (shotNumber <= 0 || shotNumber > _shots.Count)
        {
            throw new InvalidOperationException("Демонстрационный кадр не подготовлен.");
        }

        return _shots[shotNumber - 1];
    }
}
