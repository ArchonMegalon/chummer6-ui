using Chummer.Blazor.Components;
using Chummer.Desktop.Runtime;
using Photino.Blazor;

namespace Chummer.Blazor.Desktop;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        using DesktopCrashMonitor crashMonitor = DesktopCrashRuntime.InstallUnhandledExceptionMonitor("blazor-desktop");

        int? specialModeExitCode = await DesktopUpdateRuntime.TryHandleSpecialModeAsync(args, CancellationToken.None).ConfigureAwait(false);
        if (specialModeExitCode is not null)
        {
            return specialModeExitCode.Value;
        }

        DesktopUpdateStartupResult updateResult = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
            "blazor-desktop",
            args,
            CancellationToken.None).ConfigureAwait(false);
        if (updateResult.ExitRequested)
        {
            return 0;
        }

        DesktopPendingCrashReport? pendingCrash = DesktopCrashRuntime.TryLoadPendingCrashReport();
        if (pendingCrash is not null)
        {
            DesktopCrashSubmissionResult submission = await DesktopCrashRuntime.SubmitPendingCrashReportAsync(
                pendingCrash,
                CancellationToken.None).ConfigureAwait(false);
            Console.Error.WriteLine(pendingCrash.SummaryText);
            Console.Error.WriteLine(submission.Message);
            if (submission.Succeeded)
            {
                DesktopCrashRuntime.TryAcknowledgePendingCrashReport(pendingCrash.Report.CrashId);
            }
            else
            {
                DesktopCrashRuntime.TryOpenPathInShell(pendingCrash.ReportDirectory);
            }
        }

        DesktopInstallLinkingStartupContext installLinking = await DesktopInstallLinkingRuntime.InitializeForStartupAsync(
            "blazor-desktop",
            args,
            CancellationToken.None).ConfigureAwait(false);
        if (installLinking.ClaimResult is not null)
        {
            Console.Error.WriteLine(installLinking.ClaimResult.Message);
        }
        else if (installLinking.ShouldPrompt)
        {
            Console.Error.WriteLine("This desktop copy is not linked yet. Finish the signed-in install handoff in the guided installer or open Devices and access inside Chummer so the desktop can link this copy in-app.");
        }

        PhotinoBlazorAppBuilder builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        builder.Services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        builder.RootComponents.Add<DesktopAppHost>("app");

        int? startupSmokeExitCode = await DesktopStartupSmokeRuntime.TryHandleAsync(
            "blazor-desktop",
            args,
            CancellationToken.None).ConfigureAwait(false);
        if (startupSmokeExitCode is not null)
        {
            return startupSmokeExitCode.Value;
        }

        PhotinoBlazorApp app = builder.Build();
        string desktopIconPath = Path.Combine(AppContext.BaseDirectory, "chummer.ico");
        if (File.Exists(desktopIconPath))
        {
            app.MainWindow.IconFile = desktopIconPath;
        }

        app.Run();
        return 0;
    }
}
