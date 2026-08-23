#nullable enable

using System;
using System.IO;
using Chummer.Desktop.Runtime;

namespace Chummer.Tests;

[TestClass]
public sealed class AvaloniaCreationWizardSourceTests
{
    [TestMethod]
    public void Unfinished_character_owns_wizard_surface_and_keeps_unrestricted_editor_locked()
    {
        string root = ResolveRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "Chummer.Avalonia", "MainWindow.axaml"));
        string integration = File.ReadAllText(Path.Combine(root, "Chummer.Avalonia", "MainWindow.CreationWizard.cs"));
        string refresh = File.ReadAllText(Path.Combine(root, "Chummer.Avalonia", "MainWindow.StateRefresh.cs"));

        Assert.Contains("CharacterCreationWizardControl x:Name=\"CharacterCreationWizardControl\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ApplyCreationWizardState(state);", refresh, StringComparison.Ordinal);
        Assert.Contains("CharacterCreationWizardSnapshot? snapshot = overview.CreationWizard;", integration, StringComparison.Ordinal);
        Assert.Contains("SectionHostControl.IsVisible = false;", integration, StringComparison.Ordinal);
        Assert.Contains("ClassicFormPortHostControl.IsVisible = false;", integration, StringComparison.Ordinal);
        Assert.Contains("overview.Profile?.Created == true", integration, StringComparison.Ordinal);
        Assert.Contains("TryDeleteWizardCheckpoint", integration, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Shared_windows_macos_input_picker_and_recovery_seams_remain_platform_neutral()
    {
        string root = ResolveRoot();
        string control = File.ReadAllText(Path.Combine(
            root,
            "Chummer.Avalonia",
            "Controls",
            "CharacterCreationWizardControl.axaml.cs"));
        string integration = File.ReadAllText(Path.Combine(root, "Chummer.Avalonia", "MainWindow.CreationWizard.cs"));
        string store = File.ReadAllText(Path.Combine(root, "Chummer.Avalonia", "AvaloniaCreationWizardCheckpointStore.cs"));

        Assert.Contains("KeyModifiers.Control", control, StringComparison.Ordinal);
        Assert.Contains("KeyModifiers.Meta", control, StringComparison.Ordinal);
        Assert.Contains("KeyModifiers.Alt", control, StringComparison.Ordinal);
        Assert.Contains("StorageProvider.CanOpen", integration, StringComparison.Ordinal);
        Assert.Contains("StorageProvider.OpenFilePickerAsync", integration, StringComparison.Ordinal);
        Assert.Contains("StorageProvider.CanSave", integration, StringComparison.Ordinal);
        Assert.Contains("StorageProvider.SaveFilePickerAsync", integration, StringComparison.Ordinal);
        Assert.Contains("AvaloniaCreationWizardCheckpointStore.MaximumCheckpointBytes", integration, StringComparison.Ordinal);
        Assert.Contains("ReadBoundedCheckpointAsync", integration, StringComparison.Ordinal);
        Assert.Contains("new byte[AvaloniaCreationWizardCheckpointStore.MaximumCheckpointBytes + 1]", integration, StringComparison.Ordinal);
        Assert.Contains("Environment.SpecialFolder.LocalApplicationData", store, StringComparison.Ordinal);
        Assert.Contains("if (input.Length > MaximumCheckpointBytes)", store, StringComparison.Ordinal);
        Assert.DoesNotContain("File.ReadAllBytes", store, StringComparison.Ordinal);
        Assert.Contains("FileOptions.WriteThrough", store, StringComparison.Ordinal);
        Assert.Contains("Flush(flushToDisk: true)", store, StringComparison.Ordinal);
        Assert.Contains("File.Move(temporary, path, overwrite: true)", store, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Build_ghost_side_panel_is_revision_bound_and_has_no_character_mutation_route()
    {
        string root = ResolveRoot();
        string integration = File.ReadAllText(Path.Combine(root, "Chummer.Avalonia", "MainWindow.CreationWizard.cs"));
        string session = File.ReadAllText(Path.Combine(
            root,
            "Chummer.Presentation",
            "Overview",
            "CharacterCreationWizardDesktopSession.cs"));

        Assert.Contains("WizardSnapshotDigest", integration, StringComparison.Ordinal);
        Assert.Contains("WorkspaceRevision", integration, StringComparison.Ordinal);
        Assert.Contains("MatchesCurrentBuildGhostContext", integration, StringComparison.Ordinal);
        Assert.Contains("Advice and reviewable suggestions only", integration, StringComparison.Ordinal);
        Assert.Contains("SendBuildTurnAsync", integration, StringComparison.Ordinal);
        Assert.Contains("_creationWizardBuildGhostPreferenceEnabled = !overview.Preferences.DisableAiFeatures", integration, StringComparison.Ordinal);
        Assert.Contains("CharacterCreationWizardBuildGhostPolicy.CanSend", integration, StringComparison.Ordinal);
        Assert.IsLessThan(
            integration.IndexOf("SendBuildTurnAsync", StringComparison.Ordinal),
            integration.IndexOf("CharacterCreationWizardBuildGhostPolicy.CanSend", StringComparison.Ordinal));
        Assert.DoesNotContain("_adapter.Apply", integration, StringComparison.Ordinal);
        Assert.DoesNotContain("_adapter.Finalize", integration, StringComparison.Ordinal);
        Assert.Contains("contains no command, mutation request, payload XML, or confirm/finalize capability", session, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Native_platform_plan_refuses_linux_substitution_and_missing_signing_authority()
    {
        string plan = File.ReadAllText(Path.Combine(
            ResolveRoot(),
            "docs",
            "AVALONIA_CREATION_WIZARD_WINDOWS_MACOS_SMOKE_PLAN.md"));

        Assert.Contains("does **not** prove a Windows or macOS binary", plan, StringComparison.Ordinal);
        Assert.Contains("pending-signing", plan, StringComparison.Ordinal);
        Assert.Contains("pending-notarization", plan, StringComparison.Ordinal);
        Assert.Contains("zero transport invocation", plan, StringComparison.Ordinal);
        Assert.Contains("Explicitly deferred Build Ghost interaction proof", plan, StringComparison.Ordinal);
        Assert.Contains("must not simulate those facts", plan, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Shift+G", plan, StringComparison.Ordinal);
        Assert.Contains("⌘+Shift+G", plan, StringComparison.Ordinal);
        Assert.Contains("Windows and macOS are independently fail-closed", plan, StringComparison.Ordinal);
    }

    private static string ResolveRoot()
        => DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
}
