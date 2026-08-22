using System.IO;
using PhotoBooth.Models;

namespace PhotoBooth.Services;

public sealed class DemoPrinterService : IPrinterService
{
    public bool IsDemo => true;

    public string DisplayName => "Демо-принтер";

    public Task<PrintResult> PrintAsync(
        string imagePath,
        int copies,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
        {
            return Task.FromResult(new PrintResult(false, "Файл для печати не найден."));
        }

        if (copies is < 1 or > 3)
        {
            return Task.FromResult(new PrintResult(false, "Можно напечатать от одной до трёх копий."));
        }

        string copiesText = copies == 1 ? "1 копия" : $"{copies} копии";
        return Task.FromResult(new PrintResult(true, $"Демо-печать: {copiesText}."));
    }
}
