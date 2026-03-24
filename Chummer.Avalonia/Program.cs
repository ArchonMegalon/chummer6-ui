using Avalonia;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        int? specialModeExitCode = await DesktopUpdateRuntime.TryHandleSpecialModeAsync(args, CancellationToken.None).ConfigureAwait(false);
        if (specialModeExitCode is not null)
        {
            return specialModeExitCode.Value;
        }

        DesktopUpdateStartupResult updateResult = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
            "avalonia",
            args,
            CancellationToken.None).ConfigureAwait(false);
        if (updateResult.ExitRequested)
        {
            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
