using PhotoBooth.Models;

namespace PhotoBooth.Services;

public interface IPrinterService
{
    bool IsDemo { get; }

    string DisplayName { get; }

    PrintResult Print(string imagePath, int copies);
}
