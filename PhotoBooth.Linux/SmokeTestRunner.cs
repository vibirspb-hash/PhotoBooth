using PhotoBooth.Models;
using PhotoBooth.Services;

namespace PhotoBooth.Linux;

internal static class SmokeTestRunner
{
    public static int Run()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"photobooth-smoke-{Guid.NewGuid():N}");

        try
        {
            string templatesPath = Path.Combine(AppContext.BaseDirectory, "Templates");
            string demoPhotosPath = Path.Combine(AppContext.BaseDirectory, "DemoPhotos");
            TemplateInfo template = new TemplateManager()
                .GetTemplates(templatesPath)
                .First();
            TemplateDefinition definition =
                new TemplateDefinitionService().Load(template.JsonPath!);
            string originalsPath = Path.Combine(testRoot, "Photos");
            IReadOnlyList<string> shots = new SkiaDemoCameraService().PrepareShots(
                demoPhotosPath,
                originalsPath,
                definition.RequiredShotCount);
            string resultPath = Path.Combine(testRoot, "Prints", "result.png");
            string overlayPath = Path.Combine(template.FolderPath, definition.Overlay!);

            new SkiaImageComposer().Compose(
                definition,
                overlayPath,
                shots,
                resultPath);
            PrintResult printResult =
                new DemoPrinterService().Print(resultPath, 3);
            PrintAuditService audit = new();
            audit.Record(testRoot, resultPath, 2);
            audit.Record(testRoot, resultPath, 1);

            if (!File.Exists(resultPath) ||
                !printResult.Success ||
                audit.GetTotalCopies(testRoot) != 3 ||
                audit.GetCopiesForImage(testRoot, resultPath) != 3)
            {
                throw new InvalidOperationException(printResult.Message);
            }

            Console.WriteLine(
                $"OK: {template.Name}, shots={shots.Count}, result={new FileInfo(resultPath).Length} bytes");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAILED: {exception.Message}");
            return 1;
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }
    }
}
