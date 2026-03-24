using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Chummer.Desktop.Runtime;
using Chummer.Contracts.Presentation;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Chummer.Avalonia;

public partial class App : global::Avalonia.Application
{
    private ServiceProvider? _serviceProvider;
    internal static IServiceProvider? Services { get; private set; }

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
            desktop.MainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            desktop.MainWindow.Opened += MainWindow_OnOpened;
            desktop.Exit += (_, _) =>
            {
                if (desktop.MainWindow is not null)
                {
                    desktop.MainWindow.Opened -= MainWindow_OnOpened;
                }

                Services = null;
                _serviceProvider?.Dispose();
                _serviceProvider = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new();
        ConfigureServices(services);
        return services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
        services.AddSingleton(CreateApiHttpClient());
        services.AddSingleton<IAvaloniaCoachSidecarClient>(serviceProvider =>
            new HttpAvaloniaCoachSidecarClient(serviceProvider.GetRequiredService<HttpClient>()));
        services.AddSingleton<IShellBootstrapDataProvider, ShellBootstrapDataProvider>();
        services.AddSingleton<ICharacterOverviewPresenter, CharacterOverviewPresenter>();
        services.AddSingleton<IShellPresenter, ShellPresenter>();
        services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();
        services.AddSingleton<CharacterOverviewViewModelAdapter>();
        services.AddSingleton<MainWindow>();
    }

    private static HttpClient CreateApiHttpClient()
    {
        string? configured = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        string baseUrl = string.IsNullOrWhiteSpace(configured) ? "http://chummer-api:8080" : configured.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseAddress))
        {
            throw new InvalidOperationException($"Invalid CHUMMER_API_BASE_URL value '{baseUrl}'.");
        }

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

    private static async void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        if (sender is not MainWindow owner)
        {
            return;
        }

        owner.Opened -= MainWindow_OnOpened;

        try
        {
            await DesktopCrashRecoveryWindow.ShowPendingAsync(owner);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to display the desktop crash recovery window: {ex}");
        }
    }
}
