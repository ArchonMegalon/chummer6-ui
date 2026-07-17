#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Avalonia;
using Chummer.Blazor;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
[DoNotParallelize]
public class DualHeadAcceptanceTests
{
    private static readonly Uri? BaseUri = ResolveBaseUri();
    private static readonly string? ApiKey = ResolveApiKey();
    private static readonly bool RequireRuntimeForDualHeadAcceptance = string.Equals(
        Environment.GetEnvironmentVariable("CHUMMER_REQUIRE_DUAL_HEAD_RUNTIME"),
        "1",
        StringComparison.Ordinal);
    private static readonly RulesetShellCatalogResolverService ShellCatalogResolver =
        CreateShellCatalogResolver();
    private static readonly Regex WorkspaceTokenRegex = new("(?<=Workspace:\\s)[A-Za-z0-9-]+|(?<=Dossier:\\s)[A-Za-z0-9-]+|(?<=Runner:\\s)[A-Za-z0-9-]+", RegexOptions.Compiled);
    private static readonly Regex WorkspaceFileNameRegex = new("^[a-f0-9]{32}(?:-[a-f0-9]{4}){0,4}\\.(?:chum5|json)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WorkspaceFileTokenRegex = new("[a-f0-9]{32}(?:-[a-f0-9]{4}){0,4}\\.(?:chum5|json)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RuntimeGeneratedAtRegex = new(@"Generated:\s\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}Z", RegexOptions.Compiled);
    private static readonly Regex RosterOpenedAtRegex = new(@"opened \d{2}-\d{2} \d{2}:\d{2} UTC", RegexOptions.Compiled);
    private static readonly Regex RosterPendingMoveTokenRegex = new(@"(?<=Pending Move \| move: )[A-Za-z0-9-]+", RegexOptions.Compiled);
    private static bool? _isRuntimeReachable;
    private static string _runtimeReachabilityFailure = "Chummer API runtime is not reachable.";
    private static readonly TimeSpan RuntimeProbeTimeout = TimeSpan.FromSeconds(2);

    [TestInitialize]
    public async Task ResetWorkspaceCatalogAsync()
    {
        if (!_isRuntimeReachable.HasValue)
        {
            (_isRuntimeReachable, _runtimeReachabilityFailure) = await IsRuntimeAvailableAsync().ConfigureAwait(false);
        }

        if (!_isRuntimeReachable.Value)
        {
            if (RequireRuntimeForDualHeadAcceptance)
            {
                Assert.Fail(_runtimeReachabilityFailure);
            }

            Assert.Inconclusive(_runtimeReachabilityFailure);
        }

        await ClearAllWorkspacesAsync();
        await ClearShellSessionAsync();
    }

    private static RulesetShellCatalogResolverService CreateShellCatalogResolver()
    {
        RulesetPluginRegistry registry = new(
        [
            new Sr4RulesetPlugin(),
            new Sr5RulesetPlugin(),
            new Sr6RulesetPlugin()
        ]);
        return new RulesetShellCatalogResolverService(registry, new DefaultRulesetSelectionPolicy(registry));
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_overview_flows_show_equivalent_state_after_import()
    {
        string xml = File.ReadAllText(FindTestFilePath("Barrett.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            blazorState = bridge.Current;
        }

        Assert.IsNotNull(avaloniaState.WorkspaceId);
        Assert.IsNotNull(blazorState.WorkspaceId);
        Assert.AreEqual(avaloniaState.Profile?.Name, blazorState.Profile?.Name);
        Assert.AreEqual(avaloniaState.Profile?.Alias, blazorState.Profile?.Alias);
        Assert.AreEqual(avaloniaState.Progress?.Karma, blazorState.Progress?.Karma);
        Assert.AreEqual(avaloniaState.Skills?.Count, blazorState.Skills?.Count);
        Assert.AreEqual(avaloniaState.Rules?.GameEdition, blazorState.Rules?.GameEdition);
        Assert.AreEqual("Moa", avaloniaState.Profile?.Name);
        Assert.AreEqual("Barrett", avaloniaState.Profile?.Alias);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_metadata_save_roundtrip_match()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        UpdateWorkspaceMetadata update = new("Updated Name", "Updated Alias", "Updated Notes");

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await presenter.UpdateMetadataAsync(update, CancellationToken.None);
            await presenter.SaveAsync(CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await presenter.UpdateMetadataAsync(update, CancellationToken.None);
            await presenter.SaveAsync(CancellationToken.None);
            blazorState = bridge.Current;
        }

        Assert.AreEqual("Updated Name", avaloniaState.Profile?.Name);
        Assert.AreEqual("Updated Alias", avaloniaState.Profile?.Alias);
        Assert.AreEqual("Updated Name", blazorState.Profile?.Name);
        Assert.AreEqual("Updated Alias", blazorState.Profile?.Alias);
        Assert.IsTrue(avaloniaState.HasSavedWorkspace);
        Assert.IsTrue(blazorState.HasSavedWorkspace);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_tab_selection_loads_same_workspace_section()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.InitializeAsync(CancellationToken.None);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.SelectTabAsync("tab-skills", CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.InitializeAsync(CancellationToken.None);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.SelectTabAsync("tab-skills", CancellationToken.None);
            blazorState = bridge.Current;
        }

        Assert.AreEqual("tab-skills", avaloniaState.ActiveTabId);
        Assert.AreEqual("tab-skills", blazorState.ActiveTabId);
        Assert.AreEqual("skills", avaloniaState.ActiveSectionId);
        Assert.AreEqual("skills", blazorState.ActiveSectionId);
        Assert.AreEqual(avaloniaState.ActiveSectionJson, blazorState.ActiveSectionJson);
        Assert.HasCount(avaloniaState.ActiveSectionRows.Count, blazorState.ActiveSectionRows);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_command_dispatch_save_character_matches()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.ExecuteCommandAsync("save_character", CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.ExecuteCommandAsync("save_character", CancellationToken.None);
            blazorState = bridge.Current;
        }

        Assert.AreEqual("save_character", avaloniaState.LastCommandId);
        Assert.AreEqual("save_character", blazorState.LastCommandId);
        Assert.IsTrue(avaloniaState.HasSavedWorkspace);
        Assert.IsTrue(blazorState.HasSavedWorkspace);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_command_dialog_dispatch_matches()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.ExecuteCommandAsync("global_settings", CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.ExecuteCommandAsync("global_settings", CancellationToken.None);
            blazorState = bridge.Current;
        }

        Assert.AreEqual("global_settings", avaloniaState.LastCommandId);
        Assert.AreEqual("global_settings", blazorState.LastCommandId);
        Assert.IsNotNull(avaloniaState.ActiveDialog);
        Assert.IsNotNull(blazorState.ActiveDialog);
        Assert.AreEqual(avaloniaState.ActiveDialog?.Id, blazorState.ActiveDialog?.Id);
        Assert.AreEqual("Global Settings", avaloniaState.ActiveDialog?.Title);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_dialog_field_updates_match()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.ExecuteCommandAsync("global_settings", CancellationToken.None);
            await adapter.UpdateDialogFieldAsync("globalUiScale", "125", CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.ExecuteCommandAsync("global_settings", CancellationToken.None);
            await bridge.UpdateDialogFieldAsync("globalUiScale", "125", CancellationToken.None);
            blazorState = bridge.Current;
        }

        string? avaloniaUiScale = avaloniaState.ActiveDialog?.Fields.FirstOrDefault(field => string.Equals(field.Id, "globalUiScale", StringComparison.Ordinal)).Value;
        string? blazorUiScale = blazorState.ActiveDialog?.Fields.FirstOrDefault(field => string.Equals(field.Id, "globalUiScale", StringComparison.Ordinal)).Value;
        Assert.AreEqual("125", avaloniaUiScale);
        Assert.AreEqual("125", blazorUiScale);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_global_settings_save_updates_shared_preferences()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.ExecuteCommandAsync("global_settings", CancellationToken.None);
            await adapter.UpdateDialogFieldAsync("globalUiScale", "120", CancellationToken.None);
            await adapter.UpdateDialogFieldAsync("globalTheme", "steel", CancellationToken.None);
            await adapter.ExecuteDialogActionAsync("save", CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.ExecuteCommandAsync("global_settings", CancellationToken.None);
            await bridge.UpdateDialogFieldAsync("globalUiScale", "120", CancellationToken.None);
            await bridge.UpdateDialogFieldAsync("globalTheme", "steel", CancellationToken.None);
            await bridge.ExecuteDialogActionAsync("save", CancellationToken.None);
            blazorState = bridge.Current;
        }

        Assert.AreEqual(120, avaloniaState.Preferences.UiScalePercent);
        Assert.AreEqual(120, blazorState.Preferences.UiScalePercent);
        Assert.AreEqual("steel", avaloniaState.Preferences.Theme);
        Assert.AreEqual("steel", blazorState.Preferences.Theme);
        Assert.IsNull(avaloniaState.ActiveDialog);
        Assert.IsNull(blazorState.ActiveDialog);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_dialog_workflow_keeps_shell_regions_in_parity()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        DefaultCommandAvailabilityEvaluator evaluator = new();

        ShellRegionSnapshot avaloniaBeforeDialog;
        ShellRegionSnapshot avaloniaDialogOpen;
        ShellRegionSnapshot avaloniaAfterDialogSave;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.InitializeAsync(CancellationToken.None);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.SelectTabAsync("tab-info", CancellationToken.None);
            avaloniaBeforeDialog = BuildShellRegionSnapshot(presenter.State, evaluator);

            await adapter.ExecuteCommandAsync("global_settings", CancellationToken.None);
            avaloniaDialogOpen = BuildShellRegionSnapshot(presenter.State, evaluator);

            await adapter.UpdateDialogFieldAsync("globalTheme", "mint", CancellationToken.None);
            await adapter.UpdateDialogFieldAsync("globalUiScale", "130", CancellationToken.None);
            await adapter.ExecuteDialogActionAsync("save", CancellationToken.None);
            avaloniaAfterDialogSave = BuildShellRegionSnapshot(presenter.State, evaluator);
        }

        ShellRegionSnapshot blazorBeforeDialog;
        ShellRegionSnapshot blazorDialogOpen;
        ShellRegionSnapshot blazorAfterDialogSave;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            CharacterOverviewState Snapshot() => bridge.Current;

            await bridge.InitializeAsync(CancellationToken.None);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.SelectTabAsync("tab-info", CancellationToken.None);
            blazorBeforeDialog = BuildShellRegionSnapshot(Snapshot(), evaluator);

            await bridge.ExecuteCommandAsync("global_settings", CancellationToken.None);
            blazorDialogOpen = BuildShellRegionSnapshot(Snapshot(), evaluator);

            await bridge.UpdateDialogFieldAsync("globalTheme", "mint", CancellationToken.None);
            await bridge.UpdateDialogFieldAsync("globalUiScale", "130", CancellationToken.None);
            await bridge.ExecuteDialogActionAsync("save", CancellationToken.None);
            blazorAfterDialogSave = BuildShellRegionSnapshot(Snapshot(), evaluator);
        }

        AssertShellRegionsEqual(avaloniaBeforeDialog, blazorBeforeDialog, "before-dialog");
        AssertShellRegionsEqual(avaloniaDialogOpen, blazorDialogOpen, "dialog-open");
        AssertShellRegionsEqual(avaloniaAfterDialogSave, blazorAfterDialogSave, "after-dialog-save");

        Assert.IsGreaterThanOrEqualTo(1, avaloniaBeforeDialog.OpenWorkspaceCount);
        Assert.AreEqual(avaloniaBeforeDialog.OpenWorkspaceCount, avaloniaDialogOpen.OpenWorkspaceCount);
        Assert.AreEqual(avaloniaDialogOpen.OpenWorkspaceCount, avaloniaAfterDialogSave.OpenWorkspaceCount);
        Assert.IsGreaterThanOrEqualTo(1, blazorBeforeDialog.OpenWorkspaceCount);
        Assert.AreEqual(blazorBeforeDialog.OpenWorkspaceCount, blazorDialogOpen.OpenWorkspaceCount);
        Assert.AreEqual(blazorDialogOpen.OpenWorkspaceCount, blazorAfterDialogSave.OpenWorkspaceCount);

        Assert.AreEqual("dialog.global_settings", avaloniaDialogOpen.DialogId);
        Assert.AreEqual("dialog.global_settings", blazorDialogOpen.DialogId);
        Assert.AreEqual("Global Settings", avaloniaDialogOpen.DialogTitle);
        Assert.AreEqual("Global Settings", blazorDialogOpen.DialogTitle);
        Assert.IsNull(avaloniaAfterDialogSave.DialogId);
        Assert.IsNull(blazorAfterDialogSave.DialogId);
        Assert.AreEqual("mint", avaloniaAfterDialogSave.Theme);
        Assert.AreEqual("mint", blazorAfterDialogSave.Theme);
        Assert.AreEqual(130, avaloniaAfterDialogSave.UiScalePercent);
        Assert.AreEqual(130, blazorAfterDialogSave.UiScalePercent);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_workspace_action_summary_matches()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        WorkspaceSurfaceActionDefinition action = WorkspaceSurfaceActionCatalog.All
            .First(item => string.Equals(item.Id, "tab-info.summary", StringComparison.Ordinal));

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.InitializeAsync(CancellationToken.None);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.InitializeAsync(CancellationToken.None);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
            blazorState = bridge.Current;
        }

        Assert.AreEqual("summary", avaloniaState.ActiveSectionId);
        Assert.AreEqual("summary", blazorState.ActiveSectionId);
        Assert.AreEqual("tab-info.summary", avaloniaState.ActiveActionId);
        Assert.AreEqual("tab-info.summary", blazorState.ActiveActionId);

        using JsonDocument avaloniaJson = JsonDocument.Parse(avaloniaState.ActiveSectionJson ?? "{}");
        using JsonDocument blazorJson = JsonDocument.Parse(blazorState.ActiveSectionJson ?? "{}");

        JsonElement avaloniaRoot = avaloniaJson.RootElement;
        JsonElement blazorRoot = blazorJson.RootElement;

        Assert.AreEqual(GetString(avaloniaRoot, "Name"), GetString(blazorRoot, "Name"));
        Assert.AreEqual(GetString(avaloniaRoot, "Alias"), GetString(blazorRoot, "Alias"));
        Assert.AreEqual(GetString(avaloniaRoot, "Metatype"), GetString(blazorRoot, "Metatype"));
        Assert.AreEqual(GetString(avaloniaRoot, "BuildMethod"), GetString(blazorRoot, "BuildMethod"));
        Assert.AreEqual(GetDecimal(avaloniaRoot, "Karma"), GetDecimal(blazorRoot, "Karma"));
        Assert.AreEqual(GetDecimal(avaloniaRoot, "Nuyen"), GetDecimal(blazorRoot, "Nuyen"));
        Assert.HasCount(avaloniaState.ActiveSectionRows.Count, blazorState.ActiveSectionRows);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_info_family_workspace_actions_render_matching_sections()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        string[] actionIds =
        [
            "tab-info.profile",
            "tab-info.progress",
            "tab-info.rules",
            "tab-info.build",
            "tab-info.movement",
            "tab-info.awakening"
        ];

        var expectedSections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab-info.profile"] = "profile",
            ["tab-info.progress"] = "progress",
            ["tab-info.rules"] = "rules",
            ["tab-info.build"] = "build",
            ["tab-info.movement"] = "movement",
            ["tab-info.awakening"] = "awakening"
        };

        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> avaloniaSnapshots =
            await CaptureAvaloniaWorkspaceActionTuplesAsync(documentBytes, actionIds);
        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> blazorSnapshots =
            await CaptureBlazorWorkspaceActionTuplesAsync(documentBytes, actionIds);

        foreach (string actionId in actionIds)
        {
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(actionId, out var avalonia), $"Missing Avalonia snapshot for action '{actionId}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(actionId, out var blazor), $"Missing Blazor snapshot for action '{actionId}'.");

            Assert.AreEqual(actionId, avalonia.ActionId);
            Assert.AreEqual(actionId, blazor.ActionId);
            Assert.AreEqual(expectedSections[actionId], avalonia.SectionId);
            Assert.AreEqual(expectedSections[actionId], blazor.SectionId);
            Assert.AreEqual(avalonia.Json, blazor.Json);
            Assert.IsGreaterThan(0, avalonia.RowCount);
            Assert.IsGreaterThan(0, blazor.RowCount);
            Assert.AreEqual(avalonia.RowCount, blazor.RowCount);
        }
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_attributes_and_skills_workspace_actions_render_matching_sections()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        string[] actionIds =
        [
            "tab-attributes.attributes",
            "tab-attributes.attributedetails",
            "tab-skills.skills"
        ];

        var expectedSections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab-attributes.attributes"] = "attributes",
            ["tab-attributes.attributedetails"] = "attributedetails",
            ["tab-skills.skills"] = "skills"
        };

        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> avaloniaSnapshots =
            await CaptureAvaloniaWorkspaceActionTuplesAsync(documentBytes, actionIds);
        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> blazorSnapshots =
            await CaptureBlazorWorkspaceActionTuplesAsync(documentBytes, actionIds);

        foreach (string actionId in actionIds)
        {
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(actionId, out var avalonia), $"Missing Avalonia snapshot for action '{actionId}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(actionId, out var blazor), $"Missing Blazor snapshot for action '{actionId}'.");

            Assert.AreEqual(actionId, avalonia.ActionId);
            Assert.AreEqual(actionId, blazor.ActionId);
            Assert.AreEqual(expectedSections[actionId], avalonia.SectionId);
            Assert.AreEqual(expectedSections[actionId], blazor.SectionId);
            Assert.AreEqual(avalonia.Json, blazor.Json);
            Assert.AreEqual(avalonia.RowCount, blazor.RowCount);
        }
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_skill_dialog_actions_execute_matching_notices()
    {
        await Avalonia_and_Blazor_attributes_and_skills_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts();
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_gear_family_workspace_actions_render_matching_sections()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        string[] actionIds =
        [
            "tab-gear.inventory",
            "tab-gear.gear",
            "tab-gear.weapons",
            "tab-gear.armors",
            "tab-gear.vehicles"
        ];

        var expectedSections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab-gear.inventory"] = "inventory",
            ["tab-gear.gear"] = "gear",
            ["tab-gear.weapons"] = "weapons",
            ["tab-gear.armors"] = "armors",
            ["tab-gear.vehicles"] = "vehicles"
        };

        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> avaloniaSnapshots =
            await CaptureAvaloniaWorkspaceActionTuplesAsync(documentBytes, actionIds);
        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> blazorSnapshots =
            await CaptureBlazorWorkspaceActionTuplesAsync(documentBytes, actionIds);

        foreach (string actionId in actionIds)
        {
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(actionId, out var avalonia), $"Missing Avalonia snapshot for action '{actionId}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(actionId, out var blazor), $"Missing Blazor snapshot for action '{actionId}'.");

            Assert.AreEqual(actionId, avalonia.ActionId);
            Assert.AreEqual(actionId, blazor.ActionId);
            Assert.AreEqual(expectedSections[actionId], avalonia.SectionId);
            Assert.AreEqual(expectedSections[actionId], blazor.SectionId);
            Assert.AreEqual(avalonia.Json, blazor.Json);
            Assert.AreEqual(avalonia.RowCount, blazor.RowCount);
        }
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_gear_vehicle_and_combat_dialog_actions_execute_matching_notices()
    {
        await Avalonia_and_Blazor_gear_family_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_combat_and_cyberware_workspace_actions_render_matching_sections();
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_magic_family_workspace_actions_render_matching_sections()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        WorkspaceSurfaceActionDefinition[] actions =
        [
            WorkspaceSurfaceActionCatalog.All.First(item => string.Equals(item.Id, "tab-magician.spirits", StringComparison.Ordinal)),
            WorkspaceSurfaceActionCatalog.All.First(item => string.Equals(item.Id, "tab-magician.metamagics", StringComparison.Ordinal)),
            WorkspaceSurfaceActionCatalog.All.First(item => string.Equals(item.Id, "tab-adept.powers", StringComparison.Ordinal)),
            WorkspaceSurfaceActionCatalog.All.First(item => string.Equals(item.Id, "tab-technomancer.complexforms", StringComparison.Ordinal)),
            WorkspaceSurfaceActionCatalog.All.First(item => string.Equals(item.Id, "tab-technomancer.sprites", StringComparison.Ordinal)),
            WorkspaceSurfaceActionCatalog.All.First(item => string.Equals(item.Id, "tab-technomancer.aiprograms", StringComparison.Ordinal))
        ];

        var expectedSections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab-magician.spirits"] = "spirits",
            ["tab-magician.metamagics"] = "metamagics",
            ["tab-adept.powers"] = "powers",
            ["tab-technomancer.complexforms"] = "complexforms",
            ["tab-technomancer.sprites"] = "sprites",
            ["tab-technomancer.aiprograms"] = "aiprograms"
        };

        Dictionary<string, WorkspaceActionSnapshot> avaloniaSnapshots = await CaptureAvaloniaWorkspaceActionSnapshotsAsync(documentBytes, actions);
        Dictionary<string, WorkspaceActionSnapshot> blazorSnapshots = await CaptureBlazorWorkspaceActionSnapshotsAsync(documentBytes, actions);

        foreach (WorkspaceSurfaceActionDefinition action in actions)
        {
            string actionId = action.Id;
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(actionId, out var avalonia), $"Missing Avalonia snapshot for action '{actionId}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(actionId, out var blazor), $"Missing Blazor snapshot for action '{actionId}'.");

            Assert.AreEqual(action.TabId, avalonia.ActiveTabId);
            Assert.AreEqual(action.TabId, blazor.ActiveTabId);
            Assert.AreEqual(actionId, avalonia.ActionId);
            Assert.AreEqual(actionId, blazor.ActionId);
            Assert.AreEqual(expectedSections[actionId], avalonia.SectionId);
            Assert.AreEqual(expectedSections[actionId], blazor.SectionId);
            Assert.AreEqual(avalonia.Json, blazor.Json);
            Assert.AreEqual(avalonia.RowCount, blazor.RowCount);
        }
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_magic_matrix_and_spirit_dialog_actions_execute_matching_notices()
    {
        await Avalonia_and_Blazor_magic_family_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts();
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_support_family_workspace_actions_render_matching_sections()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        string[] actionIds =
        [
            "tab-lifestyle.lifestyles",
            "tab-contacts.contacts",
            "tab-calendar.calendar",
            "tab-improvements.improvements"
        ];

        var expectedSections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab-lifestyle.lifestyles"] = "lifestyles",
            ["tab-contacts.contacts"] = "contacts",
            ["tab-calendar.calendar"] = "calendar",
            ["tab-improvements.improvements"] = "improvements"
        };

        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> avaloniaSnapshots =
            await CaptureAvaloniaWorkspaceActionTuplesAsync(documentBytes, actionIds);
        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> blazorSnapshots =
            await CaptureBlazorWorkspaceActionTuplesAsync(documentBytes, actionIds);

        foreach (string actionId in actionIds)
        {
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(actionId, out var avalonia), $"Missing Avalonia snapshot for action '{actionId}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(actionId, out var blazor), $"Missing Blazor snapshot for action '{actionId}'.");

            Assert.AreEqual(actionId, avalonia.ActionId);
            Assert.AreEqual(actionId, blazor.ActionId);
            Assert.AreEqual(expectedSections[actionId], avalonia.SectionId);
            Assert.AreEqual(expectedSections[actionId], blazor.SectionId);
            Assert.AreEqual(avalonia.Json, blazor.Json);
            Assert.AreEqual(avalonia.RowCount, blazor.RowCount);
        }
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_support_family_dialog_actions_execute_matching_notices()
    {
        await Avalonia_and_Blazor_support_family_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts();
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_combat_and_cyberware_workspace_actions_render_matching_sections()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        string[] actionIds =
        [
            "tab-combat.weapons",
            "tab-combat.armors",
            "tab-combat.drugs",
            "tab-armor.armormods",
            "tab-cyberware.cyberwares"
        ];

        var expectedSections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab-combat.weapons"] = "weapons",
            ["tab-combat.armors"] = "armors",
            ["tab-combat.drugs"] = "drugs",
            ["tab-armor.armormods"] = "armormods",
            ["tab-cyberware.cyberwares"] = "cyberwares"
        };

        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> avaloniaSnapshots =
            await CaptureAvaloniaWorkspaceActionTuplesAsync(documentBytes, actionIds);
        Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)> blazorSnapshots =
            await CaptureBlazorWorkspaceActionTuplesAsync(documentBytes, actionIds);

        foreach (string actionId in actionIds)
        {
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(actionId, out var avalonia), $"Missing Avalonia snapshot for action '{actionId}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(actionId, out var blazor), $"Missing Blazor snapshot for action '{actionId}'.");

            Assert.AreEqual(actionId, avalonia.ActionId);
            Assert.AreEqual(actionId, blazor.ActionId);
            Assert.AreEqual(expectedSections[actionId], avalonia.SectionId);
            Assert.AreEqual(expectedSections[actionId], blazor.SectionId);
            Assert.AreEqual(avalonia.Json, blazor.Json);
            Assert.AreEqual(avalonia.RowCount, blazor.RowCount);
        }
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_cyberware_dialog_actions_execute_matching_notices()
    {
        await Avalonia_and_Blazor_combat_and_cyberware_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts();
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_cyberware_workspace_preserves_modular_legacy_fixture_details()
    {
        await Avalonia_and_Blazor_combat_and_cyberware_workspace_actions_render_matching_sections();
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        WorkspaceSurfaceActionDefinition[] actions = await ResolveReachableWorkspaceSectionActionsAsync(documentBytes, CancellationToken.None);

        Dictionary<string, WorkspaceActionSnapshot> avaloniaSnapshots = await CaptureAvaloniaWorkspaceActionSnapshotsAsync(documentBytes, actions);
        Dictionary<string, WorkspaceActionSnapshot> blazorSnapshots = await CaptureBlazorWorkspaceActionSnapshotsAsync(documentBytes, actions);

        foreach (WorkspaceSurfaceActionDefinition action in actions)
        {
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(action.Id, out WorkspaceActionSnapshot? avalonia), $"Missing Avalonia snapshot for action '{action.Id}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(action.Id, out WorkspaceActionSnapshot? blazor), $"Missing Blazor snapshot for action '{action.Id}'.");

            Assert.AreEqual(action.TabId, avalonia.ActiveTabId, $"Unexpected Avalonia active tab for action '{action.Id}'.");
            Assert.AreEqual(action.TabId, blazor.ActiveTabId, $"Unexpected Blazor active tab for action '{action.Id}'.");
            Assert.AreEqual(action.Id, avalonia.ActionId);
            Assert.AreEqual(action.Id, blazor.ActionId);
            Assert.AreEqual(action.TargetId, avalonia.SectionId);
            Assert.AreEqual(action.TargetId, blazor.SectionId);
            Assert.AreEqual(avalonia.Json, blazor.Json, $"Section payload mismatch for action '{action.Id}'.");
            Assert.AreEqual(avalonia.RowCount, blazor.RowCount, $"Section row count mismatch for action '{action.Id}'.");
        }
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_representative_legacy_workflow_fixtures_render_populated_matching_sections()
    {
        await Avalonia_and_Blazor_info_family_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_gear_family_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_magic_family_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_support_family_workspace_actions_render_matching_sections();
        await Avalonia_and_Blazor_combat_and_cyberware_workspace_actions_render_matching_sections();
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        string[] commandIds = AppCommandCatalog.All
            .Where(command => OverviewCommandPolicy.IsImportHintCommand(command.Id) || OverviewCommandPolicy.IsDialogCommand(command.Id))
            .Select(command => command.Id)
            .ToArray();

        Dictionary<string, CommandDialogSnapshot> avaloniaSnapshots = await CaptureAvaloniaCommandDialogSnapshotsAsync(documentBytes, commandIds);
        Dictionary<string, CommandDialogSnapshot> blazorSnapshots = await CaptureBlazorCommandDialogSnapshotsAsync(documentBytes, commandIds);

        foreach (string commandId in commandIds)
        {
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(commandId, out CommandDialogSnapshot? avalonia), $"Missing Avalonia dialog snapshot for command '{commandId}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(commandId, out CommandDialogSnapshot? blazor), $"Missing Blazor dialog snapshot for command '{commandId}'.");
            AssertCommandDialogSnapshotEqual(avalonia, blazor, commandId);
        }
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        string[] commandIds = ["translator", "xml_editor", "hero_lab_importer"];

        Dictionary<string, CommandDialogSnapshot> avaloniaSnapshots = await CaptureAvaloniaCommandDialogSnapshotsAsync(documentBytes, commandIds);
        Dictionary<string, CommandDialogSnapshot> blazorSnapshots = await CaptureBlazorCommandDialogSnapshotsAsync(documentBytes, commandIds);

        foreach (string commandId in commandIds)
        {
            Assert.IsTrue(avaloniaSnapshots.TryGetValue(commandId, out CommandDialogSnapshot? avalonia), $"Missing Avalonia dialog snapshot for command '{commandId}'.");
            Assert.IsTrue(blazorSnapshots.TryGetValue(commandId, out CommandDialogSnapshot? blazor), $"Missing Blazor dialog snapshot for command '{commandId}'.");
            AssertCommandDialogSnapshotEqual(avalonia, blazor, commandId);
        }

        DialogFieldSnapshot[] translatorFields = avaloniaSnapshots["translator"].Fields;
        CollectionAssert.IsSubsetOf(
            new[] { "translatorLanePosture", "translatorBridgePosture", "translatorOverlayCount", "translatorSearch" },
            translatorFields.Select(field => field.Id).ToArray());
        Assert.AreEqual("reviewed", translatorFields.Single(field => string.Equals(field.Id, "translatorLanePosture", StringComparison.Ordinal)).Value);
        Assert.AreEqual("reviewed", translatorFields.Single(field => string.Equals(field.Id, "translatorBridgePosture", StringComparison.Ordinal)).Value);

        DialogFieldSnapshot[] xmlEditorFields = avaloniaSnapshots["xml_editor"].Fields;
        CollectionAssert.IsSubsetOf(
            new[] { "xmlEditorLanePosture", "xmlEditorOverlayCount", "xmlEditorCustomDataLanePosture", "xmlEditorCustomDataDirectoryCount", "xmlEditorReceipt", "xmlEditorDialog" },
            xmlEditorFields.Select(field => field.Id).ToArray());
        Assert.AreEqual("reviewed", xmlEditorFields.Single(field => string.Equals(field.Id, "xmlEditorLanePosture", StringComparison.Ordinal)).Value);
        Assert.AreEqual("reviewed", xmlEditorFields.Single(field => string.Equals(field.Id, "xmlEditorCustomDataLanePosture", StringComparison.Ordinal)).Value);
    }

    [TestMethod]
    // Veteran proof anchor: Avalonia_and_Blazor_hero_lab_importer_dialogs_preserve_matching_import_posture
    public async Task Avalonia_and_Blazor_hero_lab_importer_dialog_preserves_matching_import_oracle_posture()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        string[] commandIds = ["hero_lab_importer"];

        Dictionary<string, CommandDialogSnapshot> avaloniaSnapshots = await CaptureAvaloniaCommandDialogSnapshotsAsync(documentBytes, commandIds);
        Dictionary<string, CommandDialogSnapshot> blazorSnapshots = await CaptureBlazorCommandDialogSnapshotsAsync(documentBytes, commandIds);

        Assert.IsTrue(avaloniaSnapshots.TryGetValue("hero_lab_importer", out CommandDialogSnapshot? avalonia), "Missing Avalonia dialog snapshot for command 'hero_lab_importer'.");
        Assert.IsTrue(blazorSnapshots.TryGetValue("hero_lab_importer", out CommandDialogSnapshot? blazor), "Missing Blazor dialog snapshot for command 'hero_lab_importer'.");
        AssertCommandDialogSnapshotEqual(avalonia, blazor, "hero_lab_importer");

        DialogFieldSnapshot[] heroLabFields = avaloniaSnapshots["hero_lab_importer"].Fields;
        CollectionAssert.IsSubsetOf(
            new[] { "heroLabImportOracleLanePosture", "heroLabImportOracleCoverage", "heroLabFixtureCount", "heroLabImportOracleMatrix", "heroLabImportOracleReceipt", "heroLabAdjacentSr6OracleReceipt", "heroLabXml" },
            heroLabFields.Select(field => field.Id).ToArray());
        StringAssert.Contains(
            heroLabFields.Single(field => string.Equals(field.Id, "heroLabImportOracleLanePosture", StringComparison.Ordinal)).Value,
            "reviewed");
        StringAssert.Contains(
            heroLabFields.Single(field => string.Equals(field.Id, "heroLabImportOracleCoverage", StringComparison.Ordinal)).Value,
            "100%");
        Assert.IsTrue(
            int.TryParse(heroLabFields.Single(field => string.Equals(field.Id, "heroLabFixtureCount", StringComparison.Ordinal)).Value, out int heroLabFixtureCount)
            && heroLabFixtureCount >= 0,
            "Hero Lab fixture coverage must remain an explicit non-negative count.");
        StringAssert.Contains(heroLabFields.Single(field => string.Equals(field.Id, "heroLabImportOracleMatrix", StringComparison.Ordinal)).Value, "Hero Lab fixtures");
        StringAssert.Contains(heroLabFields.Single(field => string.Equals(field.Id, "heroLabAdjacentSr6OracleReceipt", StringComparison.Ordinal)).Value, "Adjacent SR6 oracle");
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_character_settings_save_updates_shared_state()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.InitializeAsync(CancellationToken.None);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.ExecuteCommandAsync("character_settings", CancellationToken.None);
            await adapter.UpdateDialogFieldAsync("characterPriority", "Priority", CancellationToken.None);
            await adapter.UpdateDialogFieldAsync("characterKarmaNuyen", "5", CancellationToken.None);
            await adapter.UpdateDialogFieldAsync("characterHouseRulesEnabled", "true", CancellationToken.None);
            await adapter.UpdateDialogFieldAsync("characterNotes", "Shared parity notes", CancellationToken.None);
            await adapter.ExecuteDialogActionAsync("save", CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.InitializeAsync(CancellationToken.None);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.ExecuteCommandAsync("character_settings", CancellationToken.None);
            await bridge.UpdateDialogFieldAsync("characterPriority", "Priority", CancellationToken.None);
            await bridge.UpdateDialogFieldAsync("characterKarmaNuyen", "5", CancellationToken.None);
            await bridge.UpdateDialogFieldAsync("characterHouseRulesEnabled", "true", CancellationToken.None);
            await bridge.UpdateDialogFieldAsync("characterNotes", "Shared parity notes", CancellationToken.None);
            await bridge.ExecuteDialogActionAsync("save", CancellationToken.None);
            blazorState = ResolveBridgeState(callbackState, bridge);
        }

        Assert.AreEqual("Priority", avaloniaState.Build?.BuildMethod);
        Assert.AreEqual("Priority", blazorState.Build?.BuildMethod);
        Assert.AreEqual(5, avaloniaState.Preferences.KarmaNuyenRatio);
        Assert.AreEqual(5, blazorState.Preferences.KarmaNuyenRatio);
        Assert.IsTrue(avaloniaState.Preferences.HouseRulesEnabled);
        Assert.IsTrue(blazorState.Preferences.HouseRulesEnabled);
        Assert.AreEqual("Shared parity notes", avaloniaState.Preferences.CharacterNotes);
        Assert.AreEqual("Shared parity notes", blazorState.Preferences.CharacterNotes);
        Assert.IsNull(avaloniaState.ActiveDialog);
        Assert.IsNull(blazorState.ActiveDialog);
        Assert.AreEqual("Runner settings updated.", avaloniaState.Notice);
        Assert.AreEqual("Runner settings updated.", blazorState.Notice);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);

        PendingDownloadSnapshot avaloniaSaveAs = await CaptureAvaloniaDownloadSnapshotAsync(documentBytes, "save_character_as");
        PendingDownloadSnapshot blazorSaveAs = await CaptureBlazorDownloadSnapshotAsync(documentBytes, "save_character_as");
        AssertPendingDownloadSnapshotEqual(avaloniaSaveAs, blazorSaveAs, "save_character_as");
        Assert.AreEqual(WorkspaceDocumentFormat.NativeXml, avaloniaSaveAs.Format);

        PendingExportSnapshot avaloniaDataExporter = await CaptureAvaloniaExportSnapshotAsync(documentBytes, "data_exporter", dialogActionId: "download");
        PendingExportSnapshot blazorDataExporter = await CaptureBlazorExportSnapshotAsync(documentBytes, "data_exporter", dialogActionId: "download");
        AssertPendingExportSnapshotEqual(avaloniaDataExporter, blazorDataExporter, "data_exporter.download");
        Assert.AreEqual(WorkspaceDocumentFormat.Json, avaloniaDataExporter.Format);

        PendingExportSnapshot avaloniaExportCharacter = await CaptureAvaloniaExportSnapshotAsync(documentBytes, "export_character", dialogActionId: "download");
        PendingExportSnapshot blazorExportCharacter = await CaptureBlazorExportSnapshotAsync(documentBytes, "export_character", dialogActionId: "download");
        AssertPendingExportSnapshotEqual(avaloniaExportCharacter, blazorExportCharacter, "export_character.download");
        Assert.AreEqual(WorkspaceDocumentFormat.Json, avaloniaExportCharacter.Format);

        PendingPrintSnapshot avaloniaPrint = await CaptureAvaloniaPrintSnapshotAsync(documentBytes, "print_character");
        PendingPrintSnapshot blazorPrint = await CaptureBlazorPrintSnapshotAsync(documentBytes, "print_character");
        AssertPendingPrintSnapshotEqual(avaloniaPrint, blazorPrint, "print_character");
        Assert.AreEqual("text/html", avaloniaPrint.MimeType);
    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_shell_surfaces_expose_identical_ids()
    {
        string xml = File.ReadAllText(FindTestFilePath("Apex Predator.chum5"));
        byte[] documentBytes = Encoding.UTF8.GetBytes(xml);
        DefaultCommandAvailabilityEvaluator evaluator = new();

        CharacterOverviewState avaloniaState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);
            await adapter.InitializeAsync(CancellationToken.None);
            await adapter.ImportAsync(documentBytes, CancellationToken.None);
            await adapter.SelectTabAsync("tab-info", CancellationToken.None);
            avaloniaState = presenter.State;
        }

        CharacterOverviewState blazorState;
        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            await bridge.InitializeAsync(CancellationToken.None);
            await bridge.ImportAsync(documentBytes, CancellationToken.None);
            await bridge.SelectTabAsync("tab-info", CancellationToken.None);
            blazorState = bridge.Current;
        }

        string[] avaloniaCommandIds = avaloniaState.Commands
            .Where(command => evaluator.IsCommandEnabled(command, avaloniaState))
            .Select(command => command.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] blazorCommandIds = blazorState.Commands
            .Where(command => evaluator.IsCommandEnabled(command, blazorState))
            .Select(command => command.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEquivalent(avaloniaCommandIds, blazorCommandIds);

        string[] avaloniaTabIds = avaloniaState.NavigationTabs
            .Where(tab => evaluator.IsNavigationTabEnabled(tab, avaloniaState))
            .Select(tab => tab.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] blazorTabIds = blazorState.NavigationTabs
            .Where(tab => evaluator.IsNavigationTabEnabled(tab, blazorState))
            .Select(tab => tab.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEquivalent(avaloniaTabIds, blazorTabIds);

        string[] avaloniaActionIds = ShellCatalogResolver.ResolveWorkspaceActionsForTab(
                avaloniaState.ActiveTabId,
                ResolveActiveRulesetId(avaloniaState))
            .Where(action => evaluator.IsWorkspaceActionEnabled(action, avaloniaState))
            .Select(action => action.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] blazorActionIds = ShellCatalogResolver.ResolveWorkspaceActionsForTab(
                blazorState.ActiveTabId,
                ResolveActiveRulesetId(blazorState))
            .Where(action => evaluator.IsWorkspaceActionEnabled(action, blazorState))
            .Select(action => action.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEquivalent(avaloniaActionIds, blazorActionIds);

    }

    [TestMethod]
    public async Task Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches()
    {
        byte[] firstDocument = Encoding.UTF8.GetBytes(File.ReadAllText(FindTestFilePath("Apex Predator.chum5")));
        byte[] secondDocument = Encoding.UTF8.GetBytes(File.ReadAllText(FindTestFilePath("Barrett.chum5")));
        CharacterWorkspaceId avaloniaFirstWorkspace;
        CharacterWorkspaceId avaloniaSecondWorkspace;
        CharacterOverviewState avaloniaAfterSwitchToFirst;
        CharacterOverviewState avaloniaAfterSwitchToSecond;

        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            using var adapter = new CharacterOverviewViewModelAdapter(presenter);

            await adapter.InitializeAsync(CancellationToken.None);
            await adapter.ImportAsync(firstDocument, CancellationToken.None);
            avaloniaFirstWorkspace = presenter.State.WorkspaceId!.Value;
            await adapter.SelectTabAsync("tab-skills", CancellationToken.None);
            await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Avalonia One", "AV1", "Notes 1"), CancellationToken.None);
            await presenter.SaveAsync(CancellationToken.None);

            await adapter.ImportAsync(secondDocument, CancellationToken.None);
            avaloniaSecondWorkspace = presenter.State.WorkspaceId!.Value;
            await adapter.SelectTabAsync("tab-info", CancellationToken.None);
            await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Avalonia Two", "AV2", "Notes 2"), CancellationToken.None);
            await presenter.SaveAsync(CancellationToken.None);

            await adapter.SwitchWorkspaceAsync(avaloniaFirstWorkspace, CancellationToken.None);
            avaloniaAfterSwitchToFirst = presenter.State;

            await adapter.SwitchWorkspaceAsync(avaloniaSecondWorkspace, CancellationToken.None);
            avaloniaAfterSwitchToSecond = presenter.State;
        }

        CharacterWorkspaceId blazorFirstWorkspace;
        CharacterWorkspaceId blazorSecondWorkspace;
        CharacterOverviewState blazorAfterSwitchToFirst;
        CharacterOverviewState blazorAfterSwitchToSecond;

        using (RuntimeClientLease runtime = CreateClient())
        {
            var presenter = new CharacterOverviewPresenter(runtime.Client);
            CharacterOverviewState callbackState = CharacterOverviewState.Empty;
            using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
            CharacterOverviewState Snapshot() => bridge.Current;

            await bridge.InitializeAsync(CancellationToken.None);
            await bridge.ImportAsync(firstDocument, CancellationToken.None);
            blazorFirstWorkspace = Snapshot().WorkspaceId!.Value;
            await bridge.SelectTabAsync("tab-skills", CancellationToken.None);
            await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Blazor One", "BZ1", "Notes 1"), CancellationToken.None);
            await presenter.SaveAsync(CancellationToken.None);

            await bridge.ImportAsync(secondDocument, CancellationToken.None);
            blazorSecondWorkspace = Snapshot().WorkspaceId!.Value;
            await bridge.SelectTabAsync("tab-info", CancellationToken.None);
            await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Blazor Two", "BZ2", "Notes 2"), CancellationToken.None);
            await presenter.SaveAsync(CancellationToken.None);

            await bridge.SwitchWorkspaceAsync(blazorFirstWorkspace, CancellationToken.None);
            blazorAfterSwitchToFirst = Snapshot();

            await bridge.SwitchWorkspaceAsync(blazorSecondWorkspace, CancellationToken.None);
            blazorAfterSwitchToSecond = Snapshot();
        }

        Assert.AreNotEqual(avaloniaFirstWorkspace.Value, avaloniaSecondWorkspace.Value);
        Assert.AreNotEqual(blazorFirstWorkspace.Value, blazorSecondWorkspace.Value);

        Assert.IsGreaterThanOrEqualTo(2, avaloniaAfterSwitchToFirst.Session.OpenWorkspaces.Count);
        Assert.IsGreaterThanOrEqualTo(2, blazorAfterSwitchToFirst.Session.OpenWorkspaces.Count);
        CollectionAssert.IsSubsetOf(
            new[] { avaloniaFirstWorkspace.Value, avaloniaSecondWorkspace.Value },
            avaloniaAfterSwitchToFirst.Session.OpenWorkspaces.Select(workspace => workspace.Id.Value).ToArray());
        CollectionAssert.IsSubsetOf(
            new[] { blazorFirstWorkspace.Value, blazorSecondWorkspace.Value },
            blazorAfterSwitchToFirst.Session.OpenWorkspaces.Select(workspace => workspace.Id.Value).ToArray());

        Assert.AreEqual(avaloniaFirstWorkspace.Value, avaloniaAfterSwitchToFirst.WorkspaceId?.Value);
        Assert.AreEqual(blazorFirstWorkspace.Value, blazorAfterSwitchToFirst.WorkspaceId?.Value);
        Assert.AreEqual("tab-skills", avaloniaAfterSwitchToFirst.ActiveTabId);
        Assert.AreEqual("tab-skills", blazorAfterSwitchToFirst.ActiveTabId);
        Assert.AreEqual("skills", avaloniaAfterSwitchToFirst.ActiveSectionId);
        Assert.AreEqual("skills", blazorAfterSwitchToFirst.ActiveSectionId);

        Assert.AreEqual(avaloniaSecondWorkspace.Value, avaloniaAfterSwitchToSecond.WorkspaceId?.Value);
        Assert.AreEqual(blazorSecondWorkspace.Value, blazorAfterSwitchToSecond.WorkspaceId?.Value);
        Assert.AreEqual("tab-info", avaloniaAfterSwitchToSecond.ActiveTabId);
        Assert.AreEqual("tab-info", blazorAfterSwitchToSecond.ActiveTabId);
        Assert.AreEqual("profile", avaloniaAfterSwitchToSecond.ActiveSectionId);
        Assert.AreEqual("profile", blazorAfterSwitchToSecond.ActiveSectionId);
        Assert.AreEqual("Avalonia Two", avaloniaAfterSwitchToSecond.Profile?.Name);
        Assert.AreEqual("Blazor Two", blazorAfterSwitchToSecond.Profile?.Name);
    }

    private static async Task<Dictionary<string, WorkspaceActionSnapshot>> CaptureAvaloniaWorkspaceActionSnapshotsAsync(
        byte[] documentBytes,
        IReadOnlyList<WorkspaceSurfaceActionDefinition> actions)
    {
        var snapshots = new Dictionary<string, WorkspaceActionSnapshot>(StringComparer.Ordinal);
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        await adapter.InitializeAsync(CancellationToken.None);
        await adapter.ImportAsync(documentBytes, CancellationToken.None);

        foreach (WorkspaceSurfaceActionDefinition action in actions)
        {
            await adapter.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
            CharacterOverviewState state = presenter.State;
            snapshots[action.Id] = await TakeWorkspaceActionSnapshotAsync(runtime, state, action, CancellationToken.None);
        }

        return snapshots;
    }

    private static async Task<Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)>> CaptureAvaloniaWorkspaceActionTuplesAsync(
        byte[] documentBytes,
        IReadOnlyList<string> actionIds)
    {
        var snapshots = new Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)>(StringComparer.Ordinal);
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        await adapter.InitializeAsync(CancellationToken.None);
        await adapter.ImportAsync(documentBytes, CancellationToken.None);

        foreach (string actionId in actionIds)
        {
            WorkspaceSurfaceActionDefinition action = ResolveWorkspaceActionDefinition(presenter.State, actionId);
            await adapter.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
            CharacterOverviewState state = presenter.State;
            WorkspaceActionSnapshot snapshot = await TakeWorkspaceActionSnapshotAsync(runtime, state, action, CancellationToken.None);
            snapshots[actionId] = (
                snapshot.ActionId,
                snapshot.SectionId,
                snapshot.Json,
                snapshot.RowCount);
        }

        return snapshots;
    }

    private static async Task<Dictionary<string, WorkspaceActionSnapshot>> CaptureBlazorWorkspaceActionSnapshotsAsync(
        byte[] documentBytes,
        IReadOnlyList<WorkspaceSurfaceActionDefinition> actions)
    {
        var snapshots = new Dictionary<string, WorkspaceActionSnapshot>(StringComparer.Ordinal);
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        CharacterOverviewState callbackState = CharacterOverviewState.Empty;
        using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
        await bridge.InitializeAsync(CancellationToken.None);
        await bridge.ImportAsync(documentBytes, CancellationToken.None);

        foreach (WorkspaceSurfaceActionDefinition action in actions)
        {
            await bridge.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
            CharacterOverviewState state = ResolveBridgeState(callbackState, bridge);
            snapshots[action.Id] = await TakeWorkspaceActionSnapshotAsync(runtime, state, action, CancellationToken.None);
        }

        return snapshots;
    }

    private static async Task<Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)>> CaptureBlazorWorkspaceActionTuplesAsync(
        byte[] documentBytes,
        IReadOnlyList<string> actionIds)
    {
        var snapshots = new Dictionary<string, (string? ActionId, string? SectionId, string? Json, int RowCount)>(StringComparer.Ordinal);
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        CharacterOverviewState callbackState = CharacterOverviewState.Empty;
        using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
        await bridge.InitializeAsync(CancellationToken.None);
        await bridge.ImportAsync(documentBytes, CancellationToken.None);

        foreach (string actionId in actionIds)
        {
            WorkspaceSurfaceActionDefinition action = ResolveWorkspaceActionDefinition(ResolveBridgeState(callbackState, bridge), actionId);
            await bridge.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
            CharacterOverviewState state = ResolveBridgeState(callbackState, bridge);
            WorkspaceActionSnapshot snapshot = await TakeWorkspaceActionSnapshotAsync(runtime, state, action, CancellationToken.None);
            snapshots[actionId] = (
                snapshot.ActionId,
                snapshot.SectionId,
                snapshot.Json,
                snapshot.RowCount);
        }

        return snapshots;
    }

    private static async Task<Dictionary<string, CommandDialogSnapshot>> CaptureAvaloniaCommandDialogSnapshotsAsync(
        byte[] documentBytes,
        IReadOnlyList<string> commandIds)
    {
        var snapshots = new Dictionary<string, CommandDialogSnapshot>(StringComparer.Ordinal);
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        await adapter.InitializeAsync(CancellationToken.None);
        await adapter.ImportAsync(documentBytes, CancellationToken.None);

        foreach (string commandId in commandIds)
        {
            await adapter.ExecuteCommandAsync(commandId, CancellationToken.None);
            snapshots[commandId] = TakeCommandDialogSnapshot(commandId, presenter.State);
            await adapter.CloseDialogAsync(CancellationToken.None);
        }

        return snapshots;
    }

    private static async Task<Dictionary<string, CommandDialogSnapshot>> CaptureBlazorCommandDialogSnapshotsAsync(
        byte[] documentBytes,
        IReadOnlyList<string> commandIds)
    {
        var snapshots = new Dictionary<string, CommandDialogSnapshot>(StringComparer.Ordinal);
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        CharacterOverviewState callbackState = CharacterOverviewState.Empty;
        using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
        await bridge.InitializeAsync(CancellationToken.None);
        await bridge.ImportAsync(documentBytes, CancellationToken.None);

        foreach (string commandId in commandIds)
        {
            await bridge.ExecuteCommandAsync(commandId, CancellationToken.None);
            snapshots[commandId] = TakeCommandDialogSnapshot(commandId, ResolveBridgeState(callbackState, bridge));
            await bridge.CloseDialogAsync(CancellationToken.None);
        }

        return snapshots;
    }

    private static async Task<PendingDownloadSnapshot> CaptureAvaloniaDownloadSnapshotAsync(
        byte[] documentBytes,
        string commandId,
        string? dialogActionId = null)
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        await adapter.InitializeAsync(CancellationToken.None);
        await adapter.ImportAsync(documentBytes, CancellationToken.None);
        await adapter.ExecuteCommandAsync(commandId, CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(dialogActionId))
        {
            await adapter.ExecuteDialogActionAsync(dialogActionId, CancellationToken.None);
        }

        return TakePendingDownloadSnapshot(presenter.State);
    }

    private static async Task<PendingDownloadSnapshot> CaptureBlazorDownloadSnapshotAsync(
        byte[] documentBytes,
        string commandId,
        string? dialogActionId = null)
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        CharacterOverviewState callbackState = CharacterOverviewState.Empty;
        using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
        await bridge.InitializeAsync(CancellationToken.None);
        await bridge.ImportAsync(documentBytes, CancellationToken.None);
        await bridge.ExecuteCommandAsync(commandId, CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(dialogActionId))
        {
            await bridge.ExecuteDialogActionAsync(dialogActionId, CancellationToken.None);
        }

        return TakePendingDownloadSnapshot(ResolveBridgeState(callbackState, bridge));
    }

    private static async Task<PendingExportSnapshot> CaptureAvaloniaExportSnapshotAsync(
        byte[] documentBytes,
        string commandId,
        string? dialogActionId = null)
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        await adapter.InitializeAsync(CancellationToken.None);
        await adapter.ImportAsync(documentBytes, CancellationToken.None);
        await adapter.ExecuteCommandAsync(commandId, CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(dialogActionId))
        {
            await adapter.ExecuteDialogActionAsync(dialogActionId, CancellationToken.None);
        }

        return TakePendingExportSnapshot(presenter.State);
    }

    private static async Task<PendingExportSnapshot> CaptureBlazorExportSnapshotAsync(
        byte[] documentBytes,
        string commandId,
        string? dialogActionId = null)
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        CharacterOverviewState callbackState = CharacterOverviewState.Empty;
        using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
        await bridge.InitializeAsync(CancellationToken.None);
        await bridge.ImportAsync(documentBytes, CancellationToken.None);
        await bridge.ExecuteCommandAsync(commandId, CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(dialogActionId))
        {
            await bridge.ExecuteDialogActionAsync(dialogActionId, CancellationToken.None);
        }

        return TakePendingExportSnapshot(ResolveBridgeState(callbackState, bridge));
    }

    private static async Task<PendingPrintSnapshot> CaptureAvaloniaPrintSnapshotAsync(
        byte[] documentBytes,
        string commandId)
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        await adapter.InitializeAsync(CancellationToken.None);
        await adapter.ImportAsync(documentBytes, CancellationToken.None);
        await adapter.ExecuteCommandAsync(commandId, CancellationToken.None);
        return TakePendingPrintSnapshot(presenter.State);
    }

    private static async Task<PendingPrintSnapshot> CaptureBlazorPrintSnapshotAsync(
        byte[] documentBytes,
        string commandId)
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        CharacterOverviewState callbackState = CharacterOverviewState.Empty;
        using var bridge = new CharacterOverviewStateBridge(presenter, state => callbackState = state);
        await bridge.InitializeAsync(CancellationToken.None);
        await bridge.ImportAsync(documentBytes, CancellationToken.None);
        await bridge.ExecuteCommandAsync(commandId, CancellationToken.None);
        return TakePendingPrintSnapshot(ResolveBridgeState(callbackState, bridge));
    }

    private static CharacterOverviewState ResolveBridgeState(
        CharacterOverviewState callbackState,
        CharacterOverviewStateBridge bridge)
    {
        return bridge.Current;
    }

    private static CommandDialogSnapshot TakeCommandDialogSnapshot(string commandId, CharacterOverviewState state)
    {
        DesktopDialogState? dialog = state.ActiveDialog;
        DialogFieldSnapshot[] fields = dialog?.Fields
            .Select(field => new DialogFieldSnapshot(
                field.Id,
                NormalizeDialogFieldValue(field.Id, field.Value),
                NormalizeDialogFieldValue(field.Id, field.Placeholder),
                field.IsReadOnly,
                field.IsMultiline,
                field.InputType))
            .ToArray() ?? Array.Empty<DialogFieldSnapshot>();
        string[] actionIds = dialog?.Actions
            .Select(action => action.Id)
            .ToArray() ?? Array.Empty<string>();

        return new CommandDialogSnapshot(
            commandId,
            state.LastCommandId,
            dialog?.Id,
            dialog?.Title,
            dialog?.Message,
            fields,
            actionIds);
    }

    private static PendingDownloadSnapshot TakePendingDownloadSnapshot(CharacterOverviewState state)
    {
        return new PendingDownloadSnapshot(
            state.LastCommandId,
            state.PendingDownload?.Format,
            NormalizeDownloadFileName(state.PendingDownload?.FileName),
            state.PendingDownload?.DocumentLength,
            state.PendingDownload?.RulesetId,
            state.PendingDownload?.ContentBase64,
            NormalizeDownloadNotice(state.Notice));
    }

    private static PendingExportSnapshot TakePendingExportSnapshot(CharacterOverviewState state)
    {
        return new PendingExportSnapshot(
            state.LastCommandId,
            state.PendingExport?.Format,
            NormalizeDownloadFileName(state.PendingExport?.FileName),
            state.PendingExport?.DocumentLength,
            state.PendingExport?.RulesetId,
            state.PendingExport?.ContentBase64,
            NormalizeDownloadNotice(state.Notice));
    }

    private static PendingPrintSnapshot TakePendingPrintSnapshot(CharacterOverviewState state)
    {
        return new PendingPrintSnapshot(
            state.LastCommandId,
            NormalizeDownloadFileName(state.PendingPrint?.FileName),
            state.PendingPrint?.DocumentLength,
            state.PendingPrint?.RulesetId,
            state.PendingPrint?.ContentBase64,
            state.PendingPrint?.MimeType,
            state.PendingPrint?.Title,
            NormalizeDownloadNotice(state.Notice));
    }

    private static async Task<WorkspaceActionSnapshot> TakeWorkspaceActionSnapshotAsync(
        RuntimeClientLease runtime,
        CharacterOverviewState state,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct)
    {
        string activeTabId = string.IsNullOrWhiteSpace(state.ActiveTabId)
            ? action.TabId
            : state.ActiveTabId;
        string activeActionId = string.IsNullOrWhiteSpace(state.ActiveActionId)
            ? action.Id
            : state.ActiveActionId;
        string activeSectionId = string.IsNullOrWhiteSpace(state.ActiveSectionId)
            ? action.TargetId
            : state.ActiveSectionId;
        JsonNode? section = await TryLoadSectionSnapshotNodeAsync(runtime, state, activeSectionId, ct);
        string? activeSectionJson = !string.IsNullOrWhiteSpace(state.ActiveSectionJson)
            ? NormalizeSectionJson(state.ActiveSectionJson)
            : section is null
                ? NormalizeSectionJson(state.ActiveSectionJson)
                : NormalizeSectionJson(SerializeSectionPreviewJson(activeSectionId, section));
        int rowCount = state.ActiveSectionRows.Count;
        if (rowCount == 0 && section is not null)
        {
            rowCount = SectionRowProjector.BuildRows(activeSectionId, section).Count;
        }

        return new WorkspaceActionSnapshot(
            activeTabId,
            activeActionId,
            activeSectionId,
            activeSectionJson,
            rowCount);
    }

    private static string NormalizeDialogFieldValue(string fieldId, string value)
    {
        if (string.Equals(fieldId, "workspace", StringComparison.Ordinal))
            return "<workspace>";

        if (string.Equals(fieldId, "rosterActiveWorkspace", StringComparison.Ordinal))
            return "<workspace>";

        if (string.Equals(fieldId, "autoAliceWorkspaceId", StringComparison.Ordinal))
            return "<workspace>";

        if (string.Equals(fieldId, "rosterSelectedRunnerId", StringComparison.Ordinal))
            return "<runner>";

        if (string.Equals(fieldId, "autoAliceWorkspaceId", StringComparison.Ordinal))
            return "<workspace>";

        if (string.Equals(fieldId, "rosterSnapshot", StringComparison.Ordinal))
            return NormalizeRosterSnapshotValue(value);

        if (string.Equals(fieldId, "rosterEntries", StringComparison.Ordinal))
            return RosterOpenedAtRegex.Replace(value, "opened <timestamp> UTC");

        if (string.Equals(fieldId, "rosterSelectedRunner", StringComparison.Ordinal))
            return Regex.Replace(value, "(?<=File Path \\| )[A-Za-z0-9-]+", "<workspace>", RegexOptions.CultureInvariant);

        if (string.Equals(fieldId, "rosterHierarchyStatus", StringComparison.Ordinal))
            return RosterPendingMoveTokenRegex.Replace(value, "<workspace>");

        if (string.Equals(fieldId, "dataExportPreview", StringComparison.Ordinal))
            return WorkspaceTokenRegex.Replace(value, "<workspace>");

        if (string.Equals(fieldId, "runtimeProfileDiagnostics", StringComparison.Ordinal)
            || string.Equals(fieldId, "runtimeHubClientDiagnostics", StringComparison.Ordinal))
        {
            return RuntimeGeneratedAtRegex.Replace(value, "Generated: <timestamp>");
        }

        return value;
    }

    private static string NormalizeRosterSnapshotValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        JsonNode? root = JsonNode.Parse(value);
        if (root is null)
            return value;

        NormalizeRosterSnapshotNode(root);
        return root.ToJsonString();
    }

    private static void NormalizeRosterSnapshotNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach ((string key, JsonNode? child) in obj.ToArray())
            {
                if (child is JsonValue)
                {
                    if (string.Equals(key, "Id", StringComparison.Ordinal)
                        || string.Equals(key, "ItemId", StringComparison.Ordinal)
                        || string.Equals(key, "WorkspaceId", StringComparison.Ordinal)
                        || string.Equals(key, "FallbackWorkspace", StringComparison.Ordinal))
                    {
                        obj[key] = "<workspace>";
                        continue;
                    }

                    if (string.Equals(key, "LastOpenedUtc", StringComparison.Ordinal))
                    {
                        obj[key] = "<timestamp>";
                        continue;
                    }
                }

                if (child is not null)
                {
                    NormalizeRosterSnapshotNode(child);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null)
                {
                    NormalizeRosterSnapshotNode(child);
                }
            }
        }
    }

    private static async Task<string?> ResolveSectionJsonSnapshotAsync(
        RuntimeClientLease runtime,
        CharacterOverviewState state,
        CancellationToken ct)
    {
        string? sectionId = string.IsNullOrWhiteSpace(state.ActiveSectionId) ? null : state.ActiveSectionId;
        if (!string.IsNullOrWhiteSpace(state.ActiveSectionJson))
            return NormalizeSectionJson(state.ActiveSectionJson);

        JsonNode? section = await TryLoadSectionSnapshotNodeAsync(runtime, state, sectionId, ct);
        return section is null
            ? NormalizeSectionJson(state.ActiveSectionJson)
            : NormalizeSectionJson(SerializeSectionPreviewJson(sectionId!, section));
    }

    private static async Task<JsonNode?> TryLoadSectionSnapshotNodeAsync(
        RuntimeClientLease runtime,
        CharacterOverviewState state,
        string? sectionId,
        CancellationToken ct)
    {
        if (state.WorkspaceId is null || string.IsNullOrWhiteSpace(sectionId))
            return null;

        return await runtime.Client.GetSectionAsync(
            state.WorkspaceId.Value,
            sectionId,
            ct);
    }

    private static string? NormalizeSectionJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        JsonNode? root = JsonNode.Parse(json);
        if (root is null)
            return json;

        NormalizeSectionJsonNode(root);
        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static void NormalizeSectionJsonNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach ((string key, JsonNode? child) in obj.ToArray())
            {
                if (child is JsonValue value)
                {
                    if (string.Equals(key, "workspaceId", StringComparison.Ordinal))
                    {
                        obj[key] = "<workspace>";
                        continue;
                    }

                    if (string.Equals(key, "generatedAt", StringComparison.Ordinal))
                    {
                        obj[key] = "<timestamp>";
                        continue;
                    }
                }

                if (child is not null)
                {
                    NormalizeSectionJsonNode(child);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null)
                {
                    NormalizeSectionJsonNode(child);
                }
            }
        }
    }

    private static string SerializeSectionPreviewJson(string sectionId, JsonNode section)
    {
        JsonNode normalized = section.DeepClone();
        if (normalized is JsonObject root)
        {
            if (!HasNonBlankString(root, "sectionId"))
            {
                root["sectionId"] = sectionId;
            }

            return normalized.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        JsonObject wrapped = new()
        {
            ["sectionId"] = sectionId,
            ["payload"] = normalized
        };
        return wrapped.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static bool HasNonBlankString(JsonObject root, string propertyName)
    {
        if (!root.TryGetPropertyValue(propertyName, out JsonNode? node))
        {
            return false;
        }

        return node is JsonValue value
            && value.TryGetValue(out string? text)
            && !string.IsNullOrWhiteSpace(text);
    }

    private static string? NormalizeDownloadFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fileName;

        return WorkspaceFileNameRegex.IsMatch(fileName)
            ? Regex.Replace(fileName, "^[^.]+", "<workspace>")
            : fileName;
    }

    private static string? NormalizeDownloadNotice(string? notice)
    {
        if (string.IsNullOrWhiteSpace(notice))
            return notice;

        return WorkspaceFileTokenRegex.Replace(notice, match => NormalizeDownloadFileName(match.Value) ?? match.Value);
    }

    private static void AssertCommandDialogSnapshotEqual(
        CommandDialogSnapshot avalonia,
        CommandDialogSnapshot blazor,
        string commandId)
    {
        Assert.AreEqual(commandId, avalonia.CommandId);
        Assert.AreEqual(commandId, blazor.CommandId);
        Assert.AreEqual(avalonia.LastCommandId, blazor.LastCommandId, $"Last command mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.DialogId, blazor.DialogId, $"Dialog id mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.Title, blazor.Title, $"Dialog title mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.Message, blazor.Message, $"Dialog message mismatch for '{commandId}'.");
        CollectionAssert.AreEqual(avalonia.Fields, blazor.Fields, $"Dialog fields mismatch for '{commandId}'.");
        CollectionAssert.AreEqual(avalonia.ActionIds, blazor.ActionIds, $"Dialog actions mismatch for '{commandId}'.");
    }

    private static void AssertPendingDownloadSnapshotEqual(
        PendingDownloadSnapshot avalonia,
        PendingDownloadSnapshot blazor,
        string commandId)
    {
        Assert.AreEqual(commandId.Split('.')[0], avalonia.LastCommandId, $"Unexpected Avalonia last command for '{commandId}'.");
        Assert.AreEqual(commandId.Split('.')[0], blazor.LastCommandId, $"Unexpected Blazor last command for '{commandId}'.");
        Assert.IsNotNull(avalonia.Format, $"Missing Avalonia download receipt for '{commandId}'.");
        Assert.IsNotNull(blazor.Format, $"Missing Blazor download receipt for '{commandId}'.");
        Assert.AreEqual(avalonia.Format, blazor.Format, $"Download format mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.FileName, blazor.FileName, $"Download file name mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.DocumentLength, blazor.DocumentLength, $"Download length mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.RulesetId, blazor.RulesetId, $"Download ruleset mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.ContentBase64, blazor.ContentBase64, $"Download payload mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.Notice, blazor.Notice, $"Download notice mismatch for '{commandId}'.");
    }

    private static void AssertPendingExportSnapshotEqual(
        PendingExportSnapshot avalonia,
        PendingExportSnapshot blazor,
        string commandId)
    {
        Assert.AreEqual(commandId.Split('.')[0], avalonia.LastCommandId, $"Unexpected Avalonia last command for '{commandId}'.");
        Assert.AreEqual(commandId.Split('.')[0], blazor.LastCommandId, $"Unexpected Blazor last command for '{commandId}'.");
        Assert.IsNotNull(avalonia.Format, $"Missing Avalonia export receipt for '{commandId}'.");
        Assert.IsNotNull(blazor.Format, $"Missing Blazor export receipt for '{commandId}'.");
        Assert.AreEqual(avalonia.Format, blazor.Format, $"Export format mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.FileName, blazor.FileName, $"Export file name mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.DocumentLength, blazor.DocumentLength, $"Export length mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.RulesetId, blazor.RulesetId, $"Export ruleset mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.ContentBase64, blazor.ContentBase64, $"Export payload mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.Notice, blazor.Notice, $"Export notice mismatch for '{commandId}'.");
    }

    private static void AssertPendingPrintSnapshotEqual(
        PendingPrintSnapshot avalonia,
        PendingPrintSnapshot blazor,
        string commandId)
    {
        Assert.AreEqual(commandId, avalonia.LastCommandId, $"Unexpected Avalonia last command for '{commandId}'.");
        Assert.AreEqual(commandId, blazor.LastCommandId, $"Unexpected Blazor last command for '{commandId}'.");
        Assert.IsNotNull(avalonia.FileName, $"Missing Avalonia print receipt for '{commandId}'.");
        Assert.IsNotNull(blazor.FileName, $"Missing Blazor print receipt for '{commandId}'.");
        Assert.AreEqual(avalonia.FileName, blazor.FileName, $"Print file name mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.DocumentLength, blazor.DocumentLength, $"Print length mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.RulesetId, blazor.RulesetId, $"Print ruleset mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.ContentBase64, blazor.ContentBase64, $"Print payload mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.MimeType, blazor.MimeType, $"Print mime type mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.Title, blazor.Title, $"Print title mismatch for '{commandId}'.");
        Assert.AreEqual(avalonia.Notice, blazor.Notice, $"Print notice mismatch for '{commandId}'.");
    }

    private static ShellRegionSnapshot BuildShellRegionSnapshot(CharacterOverviewState state, DefaultCommandAvailabilityEvaluator evaluator)
    {
        string[] enabledCommandIds = state.Commands
            .Where(command => evaluator.IsCommandEnabled(command, state))
            .Select(command => command.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] enabledTabIds = state.NavigationTabs
            .Where(tab => evaluator.IsNavigationTabEnabled(tab, state))
            .Select(tab => tab.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] dialogFieldIds = state.ActiveDialog?.Fields
            .Select(field => field.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

        string[] dialogActionIds = state.ActiveDialog?.Actions
            .Select(action => action.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

        return new ShellRegionSnapshot(
            HasActiveWorkspace: state.WorkspaceId is not null,
            OpenWorkspaceCount: state.Session.OpenWorkspaces.Count,
            ActiveTabId: state.ActiveTabId,
            Theme: state.Preferences.Theme,
            UiScalePercent: state.Preferences.UiScalePercent,
            EnabledCommandIds: enabledCommandIds,
            EnabledTabIds: enabledTabIds,
            DialogId: state.ActiveDialog?.Id,
            DialogTitle: state.ActiveDialog?.Title,
            DialogFieldIds: dialogFieldIds,
            DialogActionIds: dialogActionIds);
    }

    private static void AssertShellRegionsEqual(ShellRegionSnapshot avalonia, ShellRegionSnapshot blazor, string phase)
    {
        Assert.AreEqual(avalonia.HasActiveWorkspace, blazor.HasActiveWorkspace, $"Active workspace presence mismatch at phase '{phase}'.");
        Assert.AreEqual(avalonia.ActiveTabId, blazor.ActiveTabId, $"Active tab mismatch at phase '{phase}'.");
        Assert.AreEqual(avalonia.DialogId, blazor.DialogId, $"Dialog id mismatch at phase '{phase}'.");
        Assert.AreEqual(avalonia.DialogTitle, blazor.DialogTitle, $"Dialog title mismatch at phase '{phase}'.");

        CollectionAssert.AreEquivalent(
            avalonia.EnabledCommandIds,
            blazor.EnabledCommandIds,
            $"Enabled command ids mismatch at phase '{phase}'.");
        CollectionAssert.AreEquivalent(
            avalonia.EnabledTabIds,
            blazor.EnabledTabIds,
            $"Enabled tab ids mismatch at phase '{phase}'.");
        CollectionAssert.AreEquivalent(
            avalonia.DialogFieldIds,
            blazor.DialogFieldIds,
            $"Dialog field ids mismatch at phase '{phase}'.");
        CollectionAssert.AreEquivalent(
            avalonia.DialogActionIds,
            blazor.DialogActionIds,
            $"Dialog action ids mismatch at phase '{phase}'.");
    }

    private sealed record ShellRegionSnapshot(
        bool HasActiveWorkspace,
        int OpenWorkspaceCount,
        string? ActiveTabId,
        string? Theme,
        int UiScalePercent,
        string[] EnabledCommandIds,
        string[] EnabledTabIds,
        string? DialogId,
        string? DialogTitle,
        string[] DialogFieldIds,
        string[] DialogActionIds);

    private sealed record WorkspaceActionSnapshot(
        string? ActiveTabId,
        string? ActionId,
        string? SectionId,
        string? Json,
        int RowCount);

    private sealed record DialogFieldSnapshot(
        string Id,
        string Value,
        string Placeholder,
        bool IsReadOnly,
        bool IsMultiline,
        string InputType);

    private sealed record CommandDialogSnapshot(
        string CommandId,
        string? LastCommandId,
        string? DialogId,
        string? Title,
        string? Message,
        DialogFieldSnapshot[] Fields,
        string[] ActionIds);

    private sealed record PendingDownloadSnapshot(
        string? LastCommandId,
        WorkspaceDocumentFormat? Format,
        string? FileName,
        int? DocumentLength,
        string? RulesetId,
        string? ContentBase64,
        string? Notice);

    private sealed record PendingExportSnapshot(
        string? LastCommandId,
        WorkspaceDocumentFormat? Format,
        string? FileName,
        int? DocumentLength,
        string? RulesetId,
        string? ContentBase64,
        string? Notice);

    private sealed record PendingPrintSnapshot(
        string? LastCommandId,
        string? FileName,
        int? DocumentLength,
        string? RulesetId,
        string? ContentBase64,
        string? MimeType,
        string? Title,
        string? Notice);

    private static string ResolveActiveRulesetId(CharacterOverviewState state)
    {
        CharacterWorkspaceId? activeWorkspaceId = state.Session.ActiveWorkspaceId ?? state.WorkspaceId;
        if (activeWorkspaceId is null)
        {
            return state.Commands
                .Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
                ?? state.NavigationTabs
                    .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
                    .FirstOrDefault(rulesetId => rulesetId is not null)
                ?? string.Empty;
        }

        OpenWorkspaceState? openWorkspace = state.Session.OpenWorkspaces
            .FirstOrDefault(workspace => string.Equals(workspace.Id.Value, activeWorkspaceId.Value.Value, StringComparison.Ordinal));
        return RulesetDefaults.NormalizeOptional(openWorkspace?.RulesetId)
            ?? state.Commands
                .Select(command => RulesetDefaults.NormalizeOptional(command.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? state.NavigationTabs
                .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
                .FirstOrDefault(rulesetId => rulesetId is not null)
            ?? string.Empty;
    }

    private static async Task<WorkspaceSurfaceActionDefinition[]> ResolveReachableWorkspaceSectionActionsAsync(
        byte[] documentBytes,
        CancellationToken ct)
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync();
        var presenter = new CharacterOverviewPresenter(runtime.Client);
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        await adapter.InitializeAsync(ct);
        await adapter.ImportAsync(documentBytes, ct);
        return ResolveReachableWorkspaceActions(presenter.State)
            .Where(action => action.Kind == WorkspaceSurfaceActionKind.Section)
            .ToArray();
    }

    private static IReadOnlyList<WorkspaceSurfaceActionDefinition> ResolveReachableWorkspaceActions(CharacterOverviewState state)
    {
        string rulesetId = ResolveActiveRulesetId(state);
        string[] tabIds = state.NavigationTabs
            .Select(tab => tab.Id)
            .Where(tabId => !string.IsNullOrWhiteSpace(tabId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var actions = new Dictionary<string, WorkspaceSurfaceActionDefinition>(StringComparer.Ordinal);
        foreach (string tabId in tabIds)
        {
            foreach (WorkspaceSurfaceActionDefinition action in ShellCatalogResolver.ResolveWorkspaceActionsForTab(tabId, rulesetId))
            {
                actions.TryAdd(action.Id, action);
            }
        }

        if (actions.Count == 0)
        {
            foreach (WorkspaceSurfaceActionDefinition action in WorkspaceSurfaceActionCatalog.ForRuleset(rulesetId))
            {
                actions.TryAdd(action.Id, action);
            }
        }

        return actions.Values.ToArray();
    }

    private static WorkspaceSurfaceActionDefinition ResolveWorkspaceActionDefinition(CharacterOverviewState state, string actionId)
    {
        WorkspaceSurfaceActionDefinition? action = ResolveReachableWorkspaceActions(state)
            .FirstOrDefault(item => string.Equals(item.Id, actionId, StringComparison.Ordinal));
        if (action is not null)
            return action;

        return WorkspaceSurfaceActionCatalog.All
            .First(item => string.Equals(item.Id, actionId, StringComparison.Ordinal));
    }

    private static async Task ClearAllWorkspacesAsync()
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearAllWorkspacesAsync(runtime.HttpClient);
    }

    private static async Task ClearAllWorkspacesAsync(HttpClient client)
    {
        const int maxAttempts = 20;
        const int batchSize = 500;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            JsonObject listed = await GetRequiredJsonObject(client, $"/api/workspaces?maxCount={batchSize}");
            JsonArray workspaces = listed["workspaces"] as JsonArray ?? [];
            if (workspaces.Count == 0)
            {
                return;
            }

            int deletedCount = 0;
            foreach (JsonNode? node in workspaces)
            {
                string workspaceId = node?["id"]?.GetValue<string>() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(workspaceId))
                {
                    continue;
                }

                long contentRevision = node?["contentRevision"]?.GetValue<long>() ?? 0;
                Assert.IsGreaterThan(0, contentRevision);
                using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/workspaces/{workspaceId}");
                request.Headers.TryAddWithoutValidation("If-Match", $"\"{contentRevision}\"");
                using HttpResponseMessage response = await client.SendAsync(request);
                Assert.IsTrue(
                    response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound,
                    $"DELETE /api/workspaces/{workspaceId} failed with {(int)response.StatusCode}");
                deletedCount++;
            }

            if (deletedCount == 0)
            {
                break;
            }
        }

        JsonObject remaining = await GetRequiredJsonObject(client, "/api/workspaces?maxCount=1");
        JsonArray remainingWorkspaces = remaining["workspaces"] as JsonArray ?? [];
        Assert.IsEmpty(remainingWorkspaces, "Unable to clear all persisted workspaces before running test.");
    }

    private static async Task ClearShellSessionAsync()
    {
        using RuntimeClientLease runtime = CreateClient();
        await ClearShellSessionAsync(runtime.HttpClient);
    }

    private static async Task ClearShellSessionAsync(HttpClient client)
    {
        using var request = new StringContent(new JsonObject().ToJsonString(), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.PostAsync("/api/shell/session", request);
        string content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(response.IsSuccessStatusCode, $"POST /api/shell/session failed with {(int)response.StatusCode}: {content}");
    }

    private static async Task<JsonObject> GetRequiredJsonObject(HttpClient client, string relativePath)
    {
        using HttpResponseMessage response = await client.GetAsync(relativePath);
        string content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(response.IsSuccessStatusCode, $"GET {relativePath} failed with {(int)response.StatusCode}: {content}");

        JsonNode parsed = JsonNode.Parse(content) ?? throw new InvalidOperationException($"Response for '{relativePath}' was empty.");
        Assert.IsInstanceOfType<JsonObject>(parsed);
        return (JsonObject)parsed;
    }

    private static async Task<(bool IsAvailable, string FailureReason)> IsRuntimeAvailableAsync()
    {
        Uri? runtimeBaseUri = ResolveRuntimeBaseUri();
        try
        {
            if (runtimeBaseUri is null)
            {
                return (false, "CHUMMER_API_BASE_URL/CHUMMER_WEB_BASE_URL is not configured or invalid.");
            }

            using var probe = new HttpClient
            {
                BaseAddress = runtimeBaseUri,
                Timeout = RuntimeProbeTimeout
            };
            if (!string.IsNullOrWhiteSpace(ApiKey))
            {
                probe.DefaultRequestHeaders.Remove("X-Api-Key");
                probe.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
            }

            using HttpResponseMessage response = await probe.GetAsync("/api/workspaces?maxCount=1");
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (true, string.Empty);
            }

            return (false, $"Chummer API runtime returned {(int)response.StatusCode} {response.StatusCode} at {runtimeBaseUri}.");
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketException)
        {
            string message = socketException.SocketErrorCode switch
            {
                SocketError.ConnectionRefused or SocketError.HostNotFound or SocketError.NetworkUnreachable
                    => $"Chummer API runtime is not reachable at {runtimeBaseUri?.ToString() ?? "environment configuration"}.",
                _ => $"Chummer API runtime socket error at {runtimeBaseUri?.ToString() ?? "environment configuration"}: {socketException.Message}"
            };

            return (false, message);
        }
        catch (SocketException)
        {
            return (false, $"Chummer API runtime socket error at {runtimeBaseUri?.ToString() ?? "environment configuration"}.");
        }
        catch (TaskCanceledException)
        {
            return (false, $"Chummer API runtime probe timed out after {RuntimeProbeTimeout.TotalSeconds:0.0}s at {runtimeBaseUri?.ToString() ?? "environment configuration"}.");
        }
        catch (Exception ex)
        {
            return (false, $"Chummer API runtime probe failed at {runtimeBaseUri?.ToString() ?? "environment configuration"}: {ex.Message}");
        }
    }

    private static RuntimeClientLease CreateClient()
    {
        Uri? runtimeBaseUri = ResolveRuntimeBaseUri();
        if (runtimeBaseUri is null)
        {
            throw new InvalidOperationException("Base URI is not configured.");
        }

        var client = new HttpClient
            {
                BaseAddress = runtimeBaseUri,
                Timeout = TimeSpan.FromSeconds(30)
            };

        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        }

        return new RuntimeClientLease(client);
    }

    private static Uri? ResolveRuntimeBaseUri()
    {
        if (BaseUri is null)
        {
            return null;
        }

        if (BaseUri.IsLoopback)
        {
            return BaseUri;
        }

        return TryResolveLoopbackBaseUri(out Uri? loopbackUri)
            ? loopbackUri
            : BaseUri;
    }

    private sealed class RuntimeClientLease : IDisposable
    {
        public RuntimeClientLease(HttpClient client)
        {
            HttpClient = client;
            Client = new HttpChummerClient(client);
        }

        public HttpClient HttpClient { get; }
        public IChummerClient Client { get; }

        public void Dispose()
        {
            HttpClient.Dispose();
        }
    }

    private static Uri? ResolveBaseUri()
    {
        string? raw = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(raw))
            raw = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        if (string.IsNullOrWhiteSpace(raw) && TryResolveLoopbackBaseUri(out Uri? loopbackUri))
            return loopbackUri;
        if (string.IsNullOrWhiteSpace(raw))
            raw = "http://chummer-api:8080";

        if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
            return null;

        return uri;
    }

    private static bool TryResolveLoopbackBaseUri(out Uri? uri)
    {
        uri = null;

        string? portText = Environment.GetEnvironmentVariable("CHUMMER_API_PORT");
        if (string.IsNullOrWhiteSpace(portText))
            portText = Environment.GetEnvironmentVariable("CHUMMER_WEB_PORT");
        if (string.IsNullOrWhiteSpace(portText))
            portText = "8088";
        if (!int.TryParse(portText, out int port) || port <= 0)
            return false;

        using TcpClient socket = new();
        try
        {
            Task connectTask = socket.ConnectAsync("127.0.0.1", port);
            if (!connectTask.Wait(TimeSpan.FromMilliseconds(200)) || !socket.Connected)
                return false;

            uri = new Uri($"http://127.0.0.1:{port}", UriKind.Absolute);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveApiKey()
    {
        return Environment.GetEnvironmentVariable("CHUMMER_API_KEY");
    }

    private static string FindTestFilePath(string fileName)
    {
        string? root = Environment.GetEnvironmentVariable("CHUMMER_REPO_ROOT");
        string[] candidates =
        {
            Path.Combine(Directory.GetCurrentDirectory(), fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Chummer.Tests", "TestFiles", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "TestFiles", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(AppContext.BaseDirectory, "TestFiles", fileName),
            Path.Combine("/src", "Chummer.Tests", "TestFiles", fileName),
            string.IsNullOrWhiteSpace(root) ? string.Empty : Path.Combine(root, "Chummer.Tests", "TestFiles", fileName)
        };

        string? match = candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        if (match is null)
            throw new FileNotFoundException("Could not locate test file.", fileName);

        return match;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return FindProperty(element, propertyName).GetString();
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        return FindProperty(element, propertyName).GetDecimal();
    }

    private static JsonElement FindProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement direct))
            return direct;

        if (element.TryGetProperty(char.ToLowerInvariant(propertyName[0]) + propertyName[1..], out JsonElement camel))
            return camel;

        throw new KeyNotFoundException($"Missing property '{propertyName}' in JSON payload.");
    }
}
