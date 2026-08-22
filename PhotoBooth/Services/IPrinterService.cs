using PhotoBooth.Models;

namespace PhotoBooth.Services;

public interface IPrinterService
{
    bool IsDemo { get; }

    string DisplayName { get; }

    Task<PrintResult> PrintAsync(
        string imagePath,
        int copies,
        CancellationToken cancellationToken = default);
}
