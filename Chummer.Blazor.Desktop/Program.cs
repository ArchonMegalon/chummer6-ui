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

        PhotinoBlazorAppBuilder builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        builder.Services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        builder.RootComponents.Add<App>("app");

        PhotinoBlazorApp app = builder.Build();
        app.Run();
        return 0;
    }
}
