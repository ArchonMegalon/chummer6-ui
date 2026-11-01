using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls.ApplicationLifetimes;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.Extensions.DependencyInjection;

namespace Chummer.Avalonia;

/// <summary>Opt-in native, two-process persistence verification against the production workspace store.</summary>
internal static class DesktopPersistenceProbe
{
    public const string Argument = "--windows-persistence-probe";

    public static async Task RunAsync(MainWindow window)
    {
        int exitCode = 1;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new InvalidOperationException("This probe requires a native Windows host.");
            string root = Required("CHUMMER_DESKTOP_STATE_ROOT");
            if (!Path.IsPathFullyQualified(root))
                throw new InvalidOperationException("An absolute isolated state root is required.");
            string receipt = Required("CHUMMER_PERSISTENCE_RECEIPT");
            string phase = Required("CHUMMER_PERSISTENCE_PHASE");
            // Negative gate + acceptance assertion. The guard call in App's ConfigureServices
            // already cleared any hostile inherited variables (before this head's DI host was
            // built) and recorded them in LastScopedVariables; if that record is non-empty the
            // environment was hostile and the evidence would not be trustworthy, so the probe
            // refuses rather than reporting.
            IReadOnlyList<string> scopedHostile = DesktopProbeEnvironmentGuard.LastScopedVariables;
            if (scopedHostile.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Refusing to run the persistence probe: the invoking environment leaked "
                    + $"store-steering variables [{string.Join(", ", scopedHostile)}] that had to be "
                    + "cleared; evidence from a contaminated environment is not trustworthy.");
            }
            await window.InitializationForAutomation.WaitAsync(timeout.Token);
            RequireHealthy(window);
            // Acceptance: verify the ACTUAL resolved client is the local in-process one before
            // Import/Save, so the evidence cannot come from an HTTP-redirected head.
            IChummerClient resolvedClient = App.Services!.GetRequiredService<IChummerClient>();
            if (resolvedClient is not InProcessChummerClient)
            {
                throw new InvalidOperationException(
                    $"The probe head must run the local in-process client; resolved "
                    + $"{resolvedClient.GetType().FullName} instead.");
            }
            // Acceptance: verify the ACTUAL local store location before Import/Save — the resolved
            // CHUMMER_STATE_PATH must point inside the isolated state root, not anywhere else.
            string resolvedStatePath = Environment.GetEnvironmentVariable("CHUMMER_STATE_PATH")
                ?? throw new InvalidOperationException(
                    "CHUMMER_STATE_PATH was not resolved; the workspace store location cannot be verified.");
            string expectedStatePath = Path.Combine(Path.GetFullPath(root), "Chummer6", "state");
            if (!string.Equals(Path.GetFullPath(resolvedStatePath), expectedStatePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The resolved workspace store '{resolvedStatePath}' is not the expected isolated store "
                    + $"'{expectedStatePath}'.");
            }

            var presenter = App.Services!.GetRequiredService<ICharacterOverviewPresenter>();
            string? expectedName = null;
            string? expectedHash = null;
            string? expectedId = null;
            if (phase == "save")
            {
                string fixture = Required("CHUMMER_PERSISTENCE_FIXTURE");
                await presenter.ImportAsync(new(
                    await File.ReadAllTextAsync(fixture, timeout.Token), "sr5", WorkspaceDocumentFormat.NativeXml), timeout.Token);
                RequireHealthy(window);
                await presenter.SaveAsync(timeout.Token);
            }
            else if (phase == "reopen")
            {
                using JsonDocument saved = JsonDocument.Parse(await File.ReadAllTextAsync(
                    Required("CHUMMER_PERSISTENCE_SAVED_RECEIPT"), timeout.Token));
                if (saved.RootElement.GetProperty("phase").GetString() != "save"
                    || !saved.RootElement.GetProperty("succeeded").GetBoolean())
                    throw new InvalidOperationException("A successful save receipt is required.");
                string id = saved.RootElement.GetProperty("workspaceId").GetString()!;
                if (!string.Equals(Path.GetFullPath(root), saved.RootElement.GetProperty("stateRoot").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Save and reopen state roots must match.");
                expectedId = id;
                expectedName = saved.RootElement.GetProperty("name").GetString();
                expectedHash = saved.RootElement.GetProperty("profileSha256").GetString();
                await presenter.LoadAsync(new CharacterWorkspaceId(id), timeout.Token);
            }
            else
                throw new InvalidOperationException("Phase must be save or reopen.");

            RequireHealthy(window);
            var state = window.SnapshotStateForAutomation();
            var idValue = state.WorkspaceId ?? throw new InvalidOperationException("Workspace is missing.");
            var workspace = state.Session.FindWorkspace(idValue);
            if (workspace is null || !workspace.HasSavedWorkspace || workspace.IsDirty)
                throw new InvalidOperationException("Workspace is not clean and persisted.");
            string name = state.Profile?.Name ?? throw new InvalidOperationException("Profile is missing.");
            if (expectedName is not null && !string.Equals(name, expectedName, StringComparison.Ordinal))
                throw new InvalidOperationException("Reopened profile does not match saved profile.");
            string profileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state.Profile))));
            if (expectedId is not null && (idValue.Value != expectedId || profileHash != expectedHash))
                throw new InvalidOperationException("Reopened workspace identity or full profile differs from save receipt.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(receipt))!);
            await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new
            {
                succeeded = true, phase, processId = Environment.ProcessId,
                workspaceId = idValue.Value, name, profileSha256 = profileHash,
                stateRoot = Path.GetFullPath(root), saved = workspace.HasSavedWorkspace,
                dirty = workspace.IsDirty, os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                timestamp = DateTimeOffset.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true }), timeout.Token);
            Console.WriteLine($"Native persistence {phase} succeeded.");
            exitCode = 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Native persistence probe failed: {error}");
        }
        finally
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown(exitCode);
        }
    }

    private static string Required(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value : throw new InvalidOperationException($"Missing {name}.");

    private static void RequireHealthy(MainWindow window)
    {
        var state = window.SnapshotStateForAutomation();
        if (!string.IsNullOrWhiteSpace(state.Error))
            throw new InvalidOperationException(state.Error);
    }
}
