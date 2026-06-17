using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chummer.Desktop.Runtime;
using Chummer.Application.AI;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Chummer.Avalonia;

public partial class App : global::Avalonia.Application
{
    private ServiceProvider? _serviceProvider;
    internal static IServiceProvider? Services { get; private set; }
    internal static DesktopInstallLinkingStartupContext? InstallLinkingStartupContext { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _serviceProvider = BuildServiceProvider();
            Services = _serviceProvider;
            desktop.MainWindow = CreateDesktopWindow(_serviceProvider);
            desktop.MainWindow.Opened += MainWindow_OnOpened;
            desktop.Exit += (_, _) =>
            {
                if (desktop.MainWindow is not null)
                {
                    desktop.MainWindow.Opened -= MainWindow_OnOpened;
                }

                Services = null;
                InstallLinkingStartupContext = null;
                _serviceProvider?.Dispose();
                _serviceProvider = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window CreateDesktopWindow(IServiceProvider services)
    {
        MainWindow window = services.GetRequiredService<MainWindow>();
        DesktopUiMode mode = ClassicModePolicy.ResolveCurrentMode();
        switch (mode)
        {
            case DesktopUiMode.Classic:
                window.Title = "Chummer Desktop Classic";
                break;
            case DesktopUiMode.Modern:
                window.Title = "Chummer Desktop Modern";
                break;
            case DesktopUiMode.SupportRecovery:
                window.Title = "Chummer Desktop Support/Recovery";
                break;
            case DesktopUiMode.Developer:
                window.Title = "Chummer Desktop Developer";
                break;
        }

        return window;
    }

    private static ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new();
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        string contentRoot = DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
        services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, contentRoot, "avalonia");
        services.AddSingleton(CreateApiHttpClient());
        if (UseHttpCoachSidecar())
        {
            services.AddSingleton<IAvaloniaCoachSidecarClient>(serviceProvider =>
                new HttpAvaloniaCoachSidecarClient(serviceProvider.GetRequiredService<HttpClient>()));
        }
        else
        {
            services.AddSingleton<IAvaloniaCoachSidecarClient, InProcessAvaloniaCoachSidecarClient>();
        }

        services.AddSingleton<IShellBootstrapDataProvider, ShellBootstrapDataProvider>();
        services.AddSingleton<IRulesetShellCatalogResolver, CatalogOnlyRulesetShellCatalogResolver>();
        services.AddSingleton<ICharacterOverviewPresenter, CharacterOverviewPresenter>();
        services.AddSingleton<IShellPresenter, ShellPresenter>();
        services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();
        services.AddSingleton<CharacterOverviewViewModelAdapter>();
        services.AddSingleton<DesktopAnalyticsClient>();
        services.AddSingleton<MainWindow>();
    }

    private static HttpClient CreateApiHttpClient()
    {
        Uri baseAddress = ResolveApiBaseAddress();
        HttpClient client = new()
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(20)
        };

        string? apiKey = Environment.GetEnvironmentVariable("CHUMMER_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }

    private static Uri ResolveApiBaseAddress()
    {
        string? configured = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        }

        string baseUrl = string.IsNullOrWhiteSpace(configured) ? "https://chummer.run/" : configured.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseAddress))
        {
            throw new InvalidOperationException(
                $"Invalid CHUMMER_API_BASE_URL/CHUMMER_WEB_BASE_URL value '{baseUrl}'.");
        }

        return baseAddress;
    }

    private static bool UseHttpCoachSidecar()
    {
        string? mode = Environment.GetEnvironmentVariable("CHUMMER_CLIENT_MODE");
        string? legacyMode = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_CLIENT_MODE");
        return string.Equals(mode?.Trim(), "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(legacyMode?.Trim(), "http", StringComparison.OrdinalIgnoreCase);
    }

    private static async void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        if (sender is not MainWindow owner)
        {
            return;
        }

        owner.Opened -= MainWindow_OnOpened;

        if (DesktopMouseFirstJourneyRuntime.ShouldRun(Environment.GetCommandLineArgs()))
        {
            await DesktopMouseFirstJourneyRunner.RunAsync(owner, "avalonia");
            return;
        }

        bool crashRecoveryShown = false;
        try
        {
            crashRecoveryShown = await DesktopCrashRecoveryWindow.TryShowPendingAsync(owner);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to display the desktop crash recovery window: {ex}");
        }

        DesktopInstallLinkingStartupContext? installLinkingContext = InstallLinkingStartupContext;
        InstallLinkingStartupContext = null;
        if (installLinkingContext is not null)
        {
            try
            {
                await DesktopInstallLinkingWindow.ShowIfNeededAsync(owner, installLinkingContext);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop install linking window: {ex}");
            }

            DesktopInstallLinkingState currentInstallState = DesktopInstallLinkingRuntime.LoadOrCreateState(installLinkingContext.State.HeadId);
            owner.ApplyInstallLinkingChrome(currentInstallState);
            if (installLinkingContext.ShouldPrompt && !DesktopInstallLinkingRuntime.IsClaimed(currentInstallState))
            {
                DesktopInstallLinkingRuntime.MarkPromptDismissed(currentInstallState.HeadId);
                owner.Close();
                return;
            }
        }

        if (DesktopUpdateRuntime.ShouldPromptForStartupUpdate("avalonia"))
        {
            try
            {
                await DesktopUpdateWindow.ShowAsync(owner, "avalonia");
                DesktopUpdateRuntime.MarkStartupUpdatePromptShown("avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop startup update window: {ex}");
            }
        }

        string? startupSurface = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SURFACE");
        if (!IsStartupSurfaceAllowedInCurrentMode(startupSurface))
        {
            return;
        }

        if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.CampaignWorkspace))
        {
            try
            {
                await DesktopCampaignWorkspaceWindow.ShowAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop campaign workspace window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.GmPrepPackets))
        {
            try
            {
                await DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop GM prep packets window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.GmRunboard))
        {
            try
            {
                await DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop GM runboard window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.RosterMovement))
        {
            try
            {
                await DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop roster movement window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.OrganizerOperations))
        {
            try
            {
                await DesktopOrganizerOperationsWindow.ShowAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop organizer operations window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.OrganizerRoles))
        {
            try
            {
                await DesktopOrganizerOperationsWindow.ShowRolesAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop organizer roles window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.RuleEnvironmentStudio))
        {
            try
            {
                await DesktopRuleEnvironmentStudioWindow.ShowAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop rule environment studio window: {ex}");
            }
        }
        else if (string.Equals(startupSurface, "update", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await DesktopUpdateWindow.ShowAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop update window: {ex}");
            }
        }
        else if (string.Equals(startupSurface, "support", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await DesktopSupportWindow.ShowAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop support window: {ex}");
            }
        }
        else if (string.Equals(startupSurface, "support_case", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await DesktopSupportCaseWindow.ShowPreviewAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop tracked support case window: {ex}");
            }
        }
        else if (string.Equals(startupSurface, "devices_access", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await DesktopDevicesAccessWindow.ShowAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop devices window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.CampaignPrimer))
        {
            try
            {
                await DesktopCampaignArtifactWindow.ShowPrimerAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop campaign primer window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.MissionBriefing))
        {
            try
            {
                await DesktopCampaignArtifactWindow.ShowMissionBriefingAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop mission briefing window: {ex}");
            }
        }
        else if (string.Equals(startupSurface, "report_issue", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await DesktopReportIssueWindow.ShowAsync(owner, "avalonia");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop report window: {ex}");
            }
        }
        else if (string.Equals(startupSurface, "crash_recovery", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!crashRecoveryShown)
                {
                    await DesktopCrashRecoveryWindow.ShowPreviewAsync(owner, "avalonia");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop crash recovery window: {ex}");
            }
        }
        else if (DesktopStartupSurfaceCatalog.Matches(startupSurface, DesktopStartupSurfaceCatalog.Settings))
        {
            try
            {
                await owner.OpenDesktopCommandFromSurfaceAsync("global_settings", "open global settings");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to display the desktop settings command surface: {ex}");
            }
        }
    }

    private static bool IsStartupSurfaceAllowedInCurrentMode(string? startupSurface)
    {
        if (string.IsNullOrWhiteSpace(startupSurface))
        {
            return true;
        }

        if (ClassicModePolicy.ResolveCurrentMode() != DesktopUiMode.Classic)
        {
            return true;
        }

        return startupSurface.Trim().ToLowerInvariant() switch
        {
            DesktopStartupSurfaceCatalog.Settings => true,
            DesktopStartupSurfaceCatalog.Update => true,
            DesktopStartupSurfaceCatalog.Support => true,
            DesktopStartupSurfaceCatalog.SupportCase => true,
            DesktopStartupSurfaceCatalog.DevicesAccess => true,
            DesktopStartupSurfaceCatalog.ReportIssue => true,
            DesktopStartupSurfaceCatalog.CrashRecovery => true,
            _ => false
        };
    }
}
