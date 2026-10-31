using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls.ApplicationLifetimes;
using Chummer.Contracts.Workspaces;
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
            await window.InitializationForAutomation.WaitAsync(timeout.Token);
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
