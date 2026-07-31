using Avalonia;
using Avalonia.Rendering;
using System.Reflection;

namespace PhotoBooth.Linux;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = SmokeTestRunner.Run();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder
            .Configure<App>()
            .UsePlatformDetect();

        if (OperatingSystem.IsMacOS())
        {
            builder
                .With(new AvaloniaNativePlatformOptions
                {
                    RenderingMode = [AvaloniaNativeRenderingMode.Software]
                })
                .AfterPlatformServicesSetup(_ => UseManagedMacRenderTimer());
        }

        return builder
            .LogToTrace();
    }

    private static void UseManagedMacRenderTimer()
    {
        const BindingFlags staticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags instanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        AvaloniaLocator locator = (AvaloniaLocator)(
            typeof(AvaloniaLocator)
                .GetProperty("CurrentMutable", staticFlags)?
                .GetValue(null) ??
            throw new InvalidOperationException(
                "Не удалось получить контейнер сервисов Avalonia."));
        MethodInfo bindMethod = typeof(AvaloniaLocator)
            .GetMethods(instanceFlags)
            .Single(method =>
                method.Name == "Bind" &&
                method.IsGenericMethodDefinition);
        object registration = bindMethod
            .MakeGenericMethod(typeof(IRenderTimer))
            .Invoke(locator, null) ??
            throw new InvalidOperationException(
                "Не удалось зарегистрировать таймер Avalonia.");
        IRenderTimer timer = (IRenderTimer)(
            Activator.CreateInstance(
                typeof(DefaultRenderTimer),
                instanceFlags,
                binder: null,
                args: [60],
                culture: null) ??
            throw new InvalidOperationException(
                "Не удалось создать совместимый таймер Avalonia."));
        MethodInfo toConstant = registration
            .GetType()
            .GetMethods(instanceFlags)
            .Single(method =>
                method.Name == "ToConstant" &&
                method.IsGenericMethodDefinition);

        toConstant
            .MakeGenericMethod(typeof(DefaultRenderTimer))
            .Invoke(registration, [timer]);
    }
}
