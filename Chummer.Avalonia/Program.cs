using Avalonia;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        using DesktopCrashMonitor crashMonitor = DesktopCrashRuntime.InstallUnhandledExceptionMonitor("avalonia");

        int? specialModeExitCode = await DesktopUpdateRuntime.TryHandleSpecialModeAsync(args, CancellationToken.None).ConfigureAwait(false);
        if (specialModeExitCode is not null)
        {
            return specialModeExitCode.Value;
        }

        // Headless callbacks, including rejected ones, must never initialize
        // graphics or acquire the GUI activation endpoint.
        if (DesktopInstallLinkingRuntime.IsHeadlessInstallLinkRequested(args))
        {
            DesktopInstallLinkingStartupContext context = await DesktopInstallLinkingRuntime.InitializeForStartupAsync(
                "avalonia", args, CancellationToken.None).ConfigureAwait(false);
            return await DesktopInstallLinkingRuntime.TryHandleHeadlessInstallLinkModeAsync(
                "avalonia", args, context, Console.Out, Console.Error, CancellationToken.None).ConfigureAwait(false) ?? 1;
        }

        // Register before startup/network work so a browser return can reach
        // the existing process even while its first window is still opening.
        bool approvalReturn = DesktopInstallLinkingRuntime.TryExtractBrowserApprovalReturn(args, out string? callbackUri);
        bool interactiveStartup = ShouldRegisterInstallLinkActivation(args);
        if (interactiveStartup && approvalReturn && !DesktopInstallLinkingRuntime.IsCurrentBrowserApprovalReturn(
                DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia"), callbackUri!))
        {
            App.InstallLinkReturnRefused = true;
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(Array.Empty<string>());
        }
        using DesktopInstallLinkActivation? activation = OperatingSystem.IsWindows()
            && interactiveStartup
            ? DesktopInstallLinkActivation.TryRegister("avalonia", App.ReceiveInstallLinkReturnAsync)
            : null;
        if (OperatingSystem.IsWindows() && interactiveStartup && approvalReturn && activation is null)
        {
            if (await DesktopInstallLinkActivation.TryForwardAsync("avalonia", callbackUri!).ConfigureAwait(false))
                return 0;
            App.InstallLinkReturnRefused = true;
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(Array.Empty<string>());
        }

        App.StartupArguments = args;
        App.InstallLinkingStartupContext = await DesktopInstallLinkingRuntime.InitializeForStartupAsync(
            "avalonia",
            args,
            CancellationToken.None).ConfigureAwait(false);
        if (interactiveStartup && App.InstallLinkingStartupContext.PromptReason == "browser_return_rejected")
        {
            App.InstallLinkReturnRefused = true;
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(Array.Empty<string>());
        }
        AppBuilder builder = BuildAvaloniaApp();
        int? startupSmokeExitCode = await DesktopStartupSmokeRuntime.TryHandleAsync(
                "avalonia",
                args,
                CancellationToken.None,
                App.InstallLinkingStartupContext)
            .ConfigureAwait(false);
        if (startupSmokeExitCode is not null)
        {
            return startupSmokeExitCode.Value;
        }

        return builder.StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    internal static bool ShouldRegisterInstallLinkActivation(string[] args)
        => !DesktopInstallLinkingRuntime.IsHeadlessInstallLinkRequested(args)
            && !args.Contains("--startup-smoke", StringComparer.OrdinalIgnoreCase)
            && !args.Contains(DesktopPersistenceProbe.Argument, StringComparer.OrdinalIgnoreCase)
            && !DesktopMouseFirstJourneyRuntime.ShouldRun(args);
}
