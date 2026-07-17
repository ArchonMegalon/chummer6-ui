#nullable enable annotations

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Chummer.Blazor.RunnerIntelligence;
using Chummer.Blazor.Components.Shell;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.RunnerIntelligence;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class Sr5Sr6RulesetParityAuditTests
{
    private static readonly DesktopDialogFactory DialogFactory = new();
    private static readonly CatalogOnlyRulesetShellCatalogResolver CompatibilityResolver = new();
    private static readonly string[] SharedCommandExecutionIds =
    [
        "close_all",
        "close_window",
        "copy",
        "exit",
        "new_critter",
        "paste",
        "print_character",
        "restart",
        "save_character",
        "save_character_as"
    ];

    private static readonly string[] GovernedDataDialogCommandIds =
    [
        "open_sourcebooks",
        "open_errata",
        "open_custom_data",
        "update_data_packs",
        "validate_data_scope",
        "open_data_folder"
    ];

    [TestMethod]
    public void Sr6_ruleset_provider_keeps_sr5_command_tab_action_and_workflow_pendants()
    {
        AssertCatalogParity(
            "commands",
            new Sr5RulesetShellDefinitionProvider().GetCommands(),
            new Sr6RulesetShellDefinitionProvider().GetCommands());
        AssertCatalogParity(
            "navigation tabs",
            new Sr5RulesetShellDefinitionProvider().GetNavigationTabs(),
            new Sr6RulesetShellDefinitionProvider().GetNavigationTabs());
        AssertCatalogParity(
            "workspace actions",
            new Sr5RulesetCatalogProvider().GetWorkspaceActions(),
            new Sr6RulesetCatalogProvider().GetWorkspaceActions());
        AssertCatalogParity(
            "workflow definitions",
            new Sr5RulesetCatalogProvider().GetWorkflowDefinitions(),
            new Sr6RulesetCatalogProvider().GetWorkflowDefinitions());
        AssertCatalogParity(
            "workflow surfaces",
            new Sr5RulesetCatalogProvider().GetWorkflowSurfaces(),
            new Sr6RulesetCatalogProvider().GetWorkflowSurfaces());
    }

    [TestMethod]
    public void Sr6_ruleset_quick_action_pendants_exist_for_every_sr5_workspace_section()
    {
        string[] sr5SectionIds = new Sr5RulesetCatalogProvider().GetWorkspaceActions()
            .Where(action => action.Kind == WorkspaceSurfaceActionKind.Section)
            .Select(action => action.TargetId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(sectionId => sectionId, StringComparer.Ordinal)
            .ToArray();

        foreach (string sectionId in sr5SectionIds)
        {
            string[] sr5QuickActions = SectionQuickActionCatalog.ForSection(RulesetDefaults.Sr5, sectionId)
                .Select(NormalizeValue)
                .ToArray();
            string[] sr6QuickActions = SectionQuickActionCatalog.ForSection(RulesetDefaults.Sr6, sectionId)
                .Select(NormalizeValue)
                .ToArray();

            if (sr5QuickActions.SequenceEqual(sr6QuickActions))
            {
                continue;
            }

            string[] missingInSr6 = sr5QuickActions.Except(sr6QuickActions, StringComparer.Ordinal).ToArray();
            string[] extraInSr6 = sr6QuickActions.Except(sr5QuickActions, StringComparer.Ordinal).ToArray();
            Assert.Fail(
                $"SR6 quick actions drifted from SR5 for section '{sectionId}'. Missing in SR6: {FormatList(missingInSr6)}. Extra in SR6: {FormatList(extraInSr6)}.");
        }
    }

    [TestMethod]
    public void Sr6_ruleset_keeps_sr5_section_target_hosting_groups()
    {
        IReadOnlyDictionary<string, string[]> sr5Groups = BuildSectionTargetHostingGroups(new Sr5RulesetCatalogProvider().GetWorkspaceActions());
        IReadOnlyDictionary<string, string[]> sr6Groups = BuildSectionTargetHostingGroups(new Sr6RulesetCatalogProvider().GetWorkspaceActions());

        AssertDictionaryParity("section target hosting groups", sr5Groups, sr6Groups);
    }

    [TestMethod]
    public void Sr6_ruleset_rendered_section_surfaces_keep_sr5_pendants_without_placeholder_fallback()
    {
        string[] sr5SectionIds = new Sr5RulesetCatalogProvider().GetWorkspaceActions()
            .Where(action => action.Kind == WorkspaceSurfaceActionKind.Section)
            .Select(action => action.TargetId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(sectionId => sectionId, StringComparer.Ordinal)
            .ToArray();

        using var context = CreateRenderContext();
        foreach (string sectionId in sr5SectionIds)
        {
            CharacterOverviewState sr5State = CreateRenderedSectionState(RulesetDefaults.Sr5, sectionId);
            CharacterOverviewState sr6State = CreateRenderedSectionState(RulesetDefaults.Sr6, sectionId);

            IRenderedComponent<SectionPane> sr5Cut = context.Render<SectionPane>(parameters => parameters
                .Add(component => component.State, sr5State));
            IRenderedComponent<SectionPane> sr6Cut = context.Render<SectionPane>(parameters => parameters
                .Add(component => component.State, sr6State));

            AssertRenderedSectionSurface("SR5", sectionId, sr5Cut);
            AssertRenderedSectionSurface("SR6", sectionId, sr6Cut);
            AssertRenderedQuickActionParity("SR5", sectionId, sr5Cut, RulesetDefaults.Sr5);
            AssertRenderedQuickActionParity("SR6", sectionId, sr6Cut, RulesetDefaults.Sr6);
        }
    }

    [TestMethod]
    public void Sr6_ruleset_command_dialog_contracts_keep_sr5_field_and_action_pendants()
    {
        string[] commandIds = CompatibilityResolver.ResolveCommands(RulesetDefaults.Sr5)
            .Select(static command => command.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static commandId => commandId, StringComparer.Ordinal)
            .ToArray();

        foreach (string commandId in commandIds)
        {
            DesktopDialogState sr5Dialog = CreateCommandDialog(commandId, RulesetDefaults.Sr5);
            DesktopDialogState sr6Dialog = CreateCommandDialog(commandId, RulesetDefaults.Sr6);
            AssertDialogContractParity($"command dialog '{commandId}'", sr5Dialog, sr6Dialog);
        }
    }

    [TestMethod]
    public void Sr6_ruleset_new_character_workflow_dialog_contracts_keep_sr5_field_and_action_pendants()
    {
        string[] buildMethods = ["Priority", "SumToTen", "Karma", "LifeModule"];

        foreach (string buildMethod in buildMethods)
        {
            DesktopDialogState sr5Dialog = DesktopDialogFactory.BuildNewCharacterContinuationDialog(
                RulesetDefaults.Sr5,
                buildMethod,
                false,
                "Parity Runner",
                "PRTY",
                DesktopPreferenceState.Default);
            DesktopDialogState sr6Dialog = DesktopDialogFactory.BuildNewCharacterContinuationDialog(
                RulesetDefaults.Sr6,
                buildMethod,
                false,
                "Parity Runner",
                "PRTY",
                DesktopPreferenceState.Default);

            AssertDialogContractParity($"new character continuation '{buildMethod}'", sr5Dialog, sr6Dialog);
        }
    }

    [TestMethod]
    public void Sr6_ruleset_origin_and_priority_rebuild_dialog_contracts_keep_sr5_field_and_action_pendants()
    {
        DesktopDialogState sr5OriginWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(
            RulesetDefaults.Sr5,
            "Parity Runner",
            "PRTY",
            DesktopPreferenceState.Default);
        DesktopDialogState sr6OriginWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(
            RulesetDefaults.Sr6,
            "Parity Runner",
            "PRTY",
            DesktopPreferenceState.Default);

        AssertDialogContractParity("origin wizard", sr5OriginWizard, sr6OriginWizard);

        DesktopDialogState sr5OriginBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(sr5OriginWizard);
        DesktopDialogState sr6OriginBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(sr6OriginWizard);

        AssertDialogContractParity("origin build", sr5OriginBuild, sr6OriginBuild);

        DesktopDialogState sr5PriorityDialog = DesktopDialogFactory.BuildNewCharacterContinuationDialog(
            RulesetDefaults.Sr5,
            "Priority",
            false,
            "Parity Runner",
            "PRTY",
            DesktopPreferenceState.Default);
        DesktopDialogState sr6PriorityDialog = DesktopDialogFactory.BuildNewCharacterContinuationDialog(
            RulesetDefaults.Sr6,
            "Priority",
            false,
            "Parity Runner",
            "PRTY",
            DesktopPreferenceState.Default);

        sr5PriorityDialog = RebuildDynamicDialog(UpdateDialogField(UpdateDialogField(sr5PriorityDialog, "newCharacterPriorityTalent", "B"), "newCharacterPriorityTalentChoice", "Magician"));
        sr6PriorityDialog = RebuildDynamicDialog(UpdateDialogField(UpdateDialogField(sr6PriorityDialog, "newCharacterPriorityTalent", "B"), "newCharacterPriorityTalentChoice", "Magician"));

        AssertDialogContractParity("priority workflow rebuild", sr5PriorityDialog, sr6PriorityDialog);
    }

    [TestMethod]
    public void Sr6_shared_legacy_ui_control_dialogs_keep_explicit_sr5_pendants()
    {
        foreach (string controlId in LegacyUiControlCatalog.All.OrderBy(static value => value, StringComparer.Ordinal))
        {
            DesktopDialogState dialog = DialogFactory.CreateUiControlDialog(controlId, DesktopPreferenceState.Default);

            Assert.AreEqual(
                $"dialog.ui.{controlId}",
                dialog.Id,
                $"Legacy control '{controlId}' must keep a dedicated dialog id instead of falling back to a generic shared control surface.");
            Assert.AreEqual(
                0,
                dialog.Fields.Count(static field => string.IsNullOrWhiteSpace(field.Id)),
                $"Legacy control '{controlId}' produced an unnamed dialog field.");
            Assert.AreEqual(
                dialog.Fields.Count,
                dialog.Fields.Select(static field => field.Id).Distinct(StringComparer.Ordinal).Count(),
                $"Legacy control '{controlId}' produced duplicate dialog field ids.");
            Assert.AreEqual(
                0,
                dialog.Actions.Count(static action => string.IsNullOrWhiteSpace(action.Id)),
                $"Legacy control '{controlId}' produced an unnamed dialog action.");
            Assert.AreEqual(
                dialog.Actions.Count,
                dialog.Actions.Select(static action => action.Id).Distinct(StringComparer.Ordinal).Count(),
                $"Legacy control '{controlId}' produced duplicate dialog action ids.");
            Assert.IsFalse(
                (dialog.Message ?? string.Empty).Contains("dedicated legacy-shaped utility form", StringComparison.Ordinal),
                $"Legacy control '{controlId}' regressed to a generic placeholder dialog message.");
        }
    }

    [TestMethod]
    public async Task Sr6_shared_legacy_ui_control_dialog_actions_do_not_fall_back_to_generic_execution()
    {
        DialogCoordinator coordinator = new();

        foreach (string controlId in LegacyUiControlCatalog.All.OrderBy(static value => value, StringComparer.Ordinal))
        {
            DesktopDialogState dialog = DialogFactory.CreateUiControlDialog(controlId, DesktopPreferenceState.Default);

            foreach (DesktopDialogAction action in dialog.Actions.Where(static candidate =>
                         !string.Equals(candidate.Id, "cancel", StringComparison.Ordinal)
                         && !string.Equals(candidate.Id, "close", StringComparison.Ordinal)))
            {
                OpenWorkspaceState workspace = CreateOpenWorkspace(RulesetDefaults.Sr6);
                CharacterWorkspaceId workspaceId = workspace.Id;
                CharacterOverviewState published = CharacterOverviewState.Empty with
                {
                    WorkspaceId = workspaceId,
                    OpenWorkspaces = [workspace],
                    Session = new WorkspaceSessionState(
                        ActiveWorkspaceId: workspaceId,
                        OpenWorkspaces: [workspace],
                        RecentWorkspaceIds: [workspaceId]),
                    Profile = CreateProfile(),
                    ActiveTabId = "tab-info",
                    ActiveSectionId = "profile",
                    ActiveSectionJson = BuildSectionJson("profile"),
                    Preferences = DesktopPreferenceState.Default,
                    Commands = CompatibilityResolver.ResolveCommands(RulesetDefaults.Sr6),
                    NavigationTabs = CompatibilityResolver.ResolveNavigationTabs(RulesetDefaults.Sr6),
                    ActiveDialog = dialog
                };

                DialogCoordinationContext context = new(
                    State: published,
                    Publish: state => published = state,
                    ImportAsync: static (_, _) => Task.CompletedTask,
                    UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
                    GetState: () => published,
                    ApplyQuickAddAsync: null,
                    ExecuteCommandAsync: static (_, _) => Task.CompletedTask);

                await coordinator.CoordinateAsync(action.Id, context, CancellationToken.None);

                string genericFallbackNotice = $"{dialog.Title}: action '{action.Id}' executed.";
                Assert.AreNotEqual(
                    genericFallbackNotice,
                    published.Notice,
                    $"Legacy control '{controlId}' action '{action.Id}' fell back to the generic coordinator branch.");
            }
        }
    }

    [TestMethod]
    public async Task Sr6_ruleset_shared_command_execution_contracts_keep_sr5_function_pendants()
    {
        string[] sharedCommandIds = CompatibilityResolver.ResolveCommands(RulesetDefaults.Sr5)
            .Select(static command => command.Id)
            .Where(static commandId =>
                !OverviewCommandPolicy.IsMenuCommand(commandId)
                && !OverviewCommandPolicy.IsDialogCommand(commandId)
                && !OverviewCommandPolicy.IsImportHintCommand(commandId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static commandId => commandId, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            SharedCommandExecutionIds,
            sharedCommandIds,
            "Every non-dialog SR5 shell command must carry an explicit SR6 execution-parity audit contract.");

        foreach (string commandId in sharedCommandIds)
        {
            string sr5Contract = await CaptureSharedCommandExecutionContractAsync(commandId, RulesetDefaults.Sr5);
            string sr6Contract = await CaptureSharedCommandExecutionContractAsync(commandId, RulesetDefaults.Sr6);
            Assert.AreEqual(
                sr5Contract,
                sr6Contract,
                $"SR6 shared command '{commandId}' drifted from the SR5 execution contract.");
        }
    }

    [TestMethod]
    public async Task Governed_data_surface_commands_open_explicit_dialogs_instead_of_shared_error_fallback()
    {
        foreach (string rulesetId in new[] { RulesetDefaults.Sr4, RulesetDefaults.Sr5, RulesetDefaults.Sr6 })
        {
            foreach (string commandId in GovernedDataDialogCommandIds)
            {
                SharedCommandExecutionObservation observation = await ObserveSharedCommandExecutionAsync(commandId, rulesetId);
                Assert.AreEqual(string.Empty, observation.Error, $"'{commandId}' under '{rulesetId}' must not fall back to the shared error branch.");
                Assert.AreNotEqual(string.Empty, observation.ActiveDialogId, $"'{commandId}' under '{rulesetId}' must open an explicit dialog.");
                Assert.AreNotEqual("dialog.generic", observation.ActiveDialogId, $"'{commandId}' under '{rulesetId}' must not use the generic dialog fallback.");
            }
        }
    }

    private static void AssertCatalogParity<T>(string catalogName, IReadOnlyList<T> sr5Items, IReadOnlyList<T> sr6Items)
    {
        string[] normalizedSr5 = sr5Items.Select(item => NormalizeValue(item)).ToArray();
        string[] normalizedSr6 = sr6Items.Select(item => NormalizeValue(item)).ToArray();

        if (normalizedSr5.SequenceEqual(normalizedSr6))
        {
            return;
        }

        string[] missingInSr6 = normalizedSr5.Except(normalizedSr6, StringComparer.Ordinal).ToArray();
        string[] extraInSr6 = normalizedSr6.Except(normalizedSr5, StringComparer.Ordinal).ToArray();
        Assert.Fail(
            $"SR6 {catalogName} drifted from SR5. Missing in SR6: {FormatList(missingInSr6)}. Extra in SR6: {FormatList(extraInSr6)}.");
    }

    private static string NormalizeValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        if (value is string text)
        {
            return NormalizeToken(text);
        }

        Type type = value.GetType();
        if (type.IsEnum || value is bool || value is byte || value is sbyte || value is short || value is ushort
            || value is int || value is uint || value is long || value is ulong || value is float || value is double
            || value is decimal || value is char)
        {
            return value.ToString() ?? string.Empty;
        }

        if (value is IEnumerable enumerable)
        {
            List<string> items = [];
            foreach (object? item in enumerable)
            {
                items.Add(NormalizeValue(item));
            }

            return $"[{string.Join(",", items)}]";
        }

        PropertyInfo[] properties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

        List<string> normalizedProperties = new(properties.Length);
        foreach (PropertyInfo property in properties)
        {
            normalizedProperties.Add($"{property.Name}={NormalizeValue(property.GetValue(value))}");
        }

        return $"{type.Name}{{{string.Join(";", normalizedProperties)}}}";
    }

    private static BunitContext CreateRenderContext()
    {
        BunitContext context = new();
        context.JSInterop.SetupVoid("chummerDialogs.revealActiveDialog").SetVoidResult();
        context.JSInterop.Setup<double[]>("chummerDialogs.captureDialogScroll", _ => true).SetResult([180d, 0d]);
        context.JSInterop.SetupVoid("chummerDialogs.restoreDialogScroll", _ => true).SetVoidResult();
        context.JSInterop.Setup<bool>("chummerDialogs.restorePendingDialogScroll", _ => true).SetResult(false);
        context.Services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        context.Services.AddSingleton<IRunnerIntelligenceCalculator, RunnerIntelligenceCalculator>();
        context.Services.AddSingleton<IRunnerIntelligenceScenarioCatalog, RunnerIntelligenceScenarioCatalog>();
        context.Services.AddSingleton<BlazorRunnerIntelligencePreviewService>();
        return context;
    }

    private static string NormalizeToken(string value)
        => value
            .Replace("sr5.", "srX.", StringComparison.Ordinal)
            .Replace("sr6.", "srX.", StringComparison.Ordinal)
            .Replace("sr5", "srX", StringComparison.Ordinal)
            .Replace("sr6", "srX", StringComparison.Ordinal);

    private static CharacterOverviewState CreateRenderedSectionState(string rulesetId, string sectionId)
        => CharacterOverviewState.Empty with
        {
            WorkspaceId = new CharacterWorkspaceId("ws-1"),
            OpenWorkspaces = [CreateOpenWorkspace(rulesetId)],
            ActiveSectionId = sectionId,
            ActiveSectionJson = BuildRenderedSectionJson(sectionId),
            ActiveSectionRows = BuildRenderedSectionRows(sectionId)
        };

    private static string BuildRenderedSectionJson(string sectionId)
    {
        if (!AttributeWorkbenchProjector.IsAttributeSection(sectionId))
        {
            return BuildSectionJson(sectionId);
        }

        return $$"""
               {
                 "sectionId": "{{sectionId}}",
                 "attributes": [
                   {
                     "name": "Body",
                     "baseValue": 3,
                     "karmaValue": 1,
                     "totalValue": 4,
                     "metatypeMin": 1,
                     "metatypeMax": 6,
                     "metatypeAugMax": 9,
                     "priorityMaximum": 6,
                     "karmaMaximum": 5,
                     "baseUnlocked": true
                   }
                 ]
               }
               """;
    }

    private static SectionRowState[] BuildRenderedSectionRows(string sectionId)
    {
        if (AttributeWorkbenchProjector.IsAttributeSection(sectionId))
        {
            return Array.Empty<SectionRowState>();
        }

        return
        [
            new SectionRowState($"{sectionId}[0].name", "Parity Entry"),
            new SectionRowState($"{sectionId}[0].detail", "Ready")
        ];
    }

    private static void AssertRenderedSectionSurface(
        string surfaceLabel,
        string sectionId,
        IRenderedComponent<SectionPane> cut)
    {
        Assert.IsFalse(
            cut.Markup.Contains("Select a tab to render a runner section", StringComparison.Ordinal),
            $"{surfaceLabel} section '{sectionId}' fell back to the empty placeholder instead of a real pendant surface.");
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(cut.Find("h2").TextContent),
            $"{surfaceLabel} section '{sectionId}' rendered without a visible heading.");

        if (string.Equals(surfaceLabel, "SR6", StringComparison.Ordinal)
            && AttributeWorkbenchProjector.IsAttributeSection(sectionId))
        {
            StringAssert.Contains(
                cut.Markup,
                "data-sr6-attribute-workbench",
                $"SR6 attribute section '{sectionId}' must render the real SR6 attribute workbench.");
            return;
        }

        bool hasSectionTable = cut.FindAll(".section-table").Count > 0;
        bool hasClassicSheet = cut.FindAll(".classic-runner-sheet").Count > 0;
        bool hasContextCard = cut.FindAll(".section-context-card").Count > 0;
        Assert.IsTrue(
            hasSectionTable || hasClassicSheet || hasContextCard,
            $"{surfaceLabel} section '{sectionId}' did not render any concrete section content surface.");
    }

    private static void AssertRenderedQuickActionParity(
        string surfaceLabel,
        string sectionId,
        IRenderedComponent<SectionPane> cut,
        string rulesetId)
    {
        string[] expectedIds = SectionQuickActionCatalog.ForSection(rulesetId, sectionId)
            .Select(static action => action.ControlId)
            .ToArray();
        string[] expectedLabels = SectionQuickActionCatalog.ForSection(rulesetId, sectionId)
            .Select(static action => action.Label)
            .ToArray();
        string[] renderedIds = cut.FindAll("[data-section-quick-action]")
            .Select(node => node.GetAttribute("data-section-quick-action") ?? string.Empty)
            .ToArray();
        string[] renderedLabels = cut.FindAll("[data-section-quick-action]")
            .Select(node => node.TextContent.Trim())
            .ToArray();

        CollectionAssert.AreEqual(
            expectedIds,
            renderedIds,
            $"{surfaceLabel} section '{sectionId}' rendered a drifted quick-action id set.");
        CollectionAssert.AreEqual(
            expectedLabels,
            renderedLabels,
            $"{surfaceLabel} section '{sectionId}' rendered drifted quick-action labels.");
    }

    private static async Task<string> CaptureSharedCommandExecutionContractAsync(string commandId, string rulesetId)
        => NormalizeValue(await ObserveSharedCommandExecutionAsync(commandId, rulesetId));

    private static async Task<SharedCommandExecutionObservation> ObserveSharedCommandExecutionAsync(string commandId, string rulesetId)
    {
        OverviewCommandDispatcher dispatcher = new();
        OpenWorkspaceState workspace = CreateOpenWorkspace(rulesetId);
        CharacterWorkspaceId workspaceId = workspace.Id;
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            WorkspaceId = workspaceId,
            OpenWorkspaces = [workspace],
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: workspaceId,
                OpenWorkspaces: [workspace],
                RecentWorkspaceIds: [workspaceId]),
            Profile = CreateProfile(),
            ActiveTabId = "tab-info",
            ActiveSectionId = "profile",
            ActiveSectionJson = BuildSectionJson("profile"),
            Commands = CompatibilityResolver.ResolveCommands(rulesetId),
            NavigationTabs = CompatibilityResolver.ResolveNavigationTabs(rulesetId),
            Preferences = DesktopPreferenceState.Default
        };

        bool saveInvoked = false;
        bool downloadInvoked = false;
        bool printInvoked = false;
        bool closeAllInvoked = false;
        string closeAllMessage = string.Empty;
        CharacterWorkspaceId? closedWorkspaceId = null;
        WorkspaceImportDocument? importedDocument = null;

        OverviewCommandExecutionContext context = new(
            State: published,
            CurrentWorkspace: workspaceId,
            DialogFactory: DialogFactory,
            Publish: state => published = state,
            GetShellBootstrapAsync: static (_, _) => throw new InvalidOperationException("Shared-command parity should not require shell bootstrap."),
            GetRuntimeInspectorProfileAsync: static (_, _, _) => throw new InvalidOperationException("Shared-command parity should not require runtime inspector."),
            GetMasterIndexAsync: static _ => throw new InvalidOperationException("Shared-command parity should not require master index."),
            GetTranslatorLanguagesAsync: static _ => throw new InvalidOperationException("Shared-command parity should not require translator languages."),
            SaveAsync: _ =>
            {
                saveInvoked = true;
                return Task.CompletedTask;
            },
            DownloadAsync: _ =>
            {
                downloadInvoked = true;
                return Task.CompletedTask;
            },
            PrintAsync: _ =>
            {
                printInvoked = true;
                return Task.CompletedTask;
            },
            ImportAsync: (document, _) =>
            {
                importedDocument = document;
                return Task.CompletedTask;
            },
            LoadAsync: static (_, _) => throw new InvalidOperationException("Shared-command parity should not require workspace load."),
            CreateResetState: static (_, _) => CharacterOverviewState.Empty,
            CloseAllAsync: (_, message) =>
            {
                closeAllInvoked = true;
                closeAllMessage = message;
                return Task.CompletedTask;
            },
            CloseWorkspaceAsync: (currentWorkspaceId, _) =>
            {
                closedWorkspaceId = currentWorkspaceId;
                return Task.CompletedTask;
            });

        await dispatcher.DispatchAsync(commandId, context, CancellationToken.None);

        return new SharedCommandExecutionObservation(
            CommandId: commandId,
            Notice: published.Notice ?? string.Empty,
            Error: published.Error ?? string.Empty,
            ActiveDialogId: published.ActiveDialog?.Id ?? string.Empty,
            SaveInvoked: saveInvoked,
            DownloadInvoked: downloadInvoked,
            PrintInvoked: printInvoked,
            CloseAllInvoked: closeAllInvoked,
            CloseAllMessage: closeAllMessage,
            ClosedWorkspaceId: closedWorkspaceId?.Value ?? string.Empty,
            ImportedRulesetId: importedDocument?.RulesetId ?? string.Empty,
            ImportedFormat: importedDocument?.Format.ToString() ?? string.Empty,
            ImportedHasCritterMetatype: importedDocument?.Content.Contains("<metatype>Critter</metatype>", StringComparison.Ordinal) ?? false,
            ImportedHasPriorityBuildMethod: importedDocument?.Content.Contains("<buildmethod>Priority</buildmethod>", StringComparison.Ordinal) ?? false,
            ImportedHasStarterCritterConcept: importedDocument?.Content.Contains("starter", StringComparison.OrdinalIgnoreCase) == true
                && importedDocument.Content.Contains("critter", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertDialogContractParity(string dialogName, DesktopDialogState sr5Dialog, DesktopDialogState sr6Dialog)
    {
        Assert.AreEqual(
            NormalizeToken(sr5Dialog.Id),
            NormalizeToken(sr6Dialog.Id),
            $"{dialogName} drifted at the dialog id level.");

        AssertSequenceParity(
            $"{dialogName} field contracts",
            sr5Dialog.Fields.Select(NormalizeFieldContract).ToArray(),
            sr6Dialog.Fields.Select(NormalizeFieldContract).ToArray());
        AssertSequenceParity(
            $"{dialogName} action contracts",
            sr5Dialog.Actions.Select(NormalizeActionContract).ToArray(),
            sr6Dialog.Actions.Select(NormalizeActionContract).ToArray());
    }

    private static string NormalizeFieldContract(DesktopDialogField field)
    {
        string[] optionValues = (field.Options ?? Array.Empty<DesktopDialogFieldOption>())
            .Select(option => NormalizeToken(option.Value))
            .ToArray();

        return string.Join(
            ";",
            new[]
            {
                $"id={NormalizeToken(field.Id)}",
                $"input={NormalizeToken(field.InputType)}",
                $"visual={NormalizeToken(field.VisualKind)}",
                $"layout={NormalizeToken(field.LayoutSlot)}",
                $"readonly={field.IsReadOnly}",
                $"multiline={field.IsMultiline}",
                $"options=[{string.Join(",", optionValues)}]"
            });
    }

    private static string NormalizeActionContract(DesktopDialogAction action)
        => $"id={NormalizeToken(action.Id)};primary={action.IsPrimary}";

    private static void AssertSequenceParity(string contractName, IReadOnlyList<string> sr5Items, IReadOnlyList<string> sr6Items)
    {
        if (sr5Items.SequenceEqual(sr6Items))
        {
            return;
        }

        string[] missingInSr6 = sr5Items.Except(sr6Items, StringComparer.Ordinal).ToArray();
        string[] extraInSr6 = sr6Items.Except(sr5Items, StringComparer.Ordinal).ToArray();
        Assert.Fail(
            $"SR6 {contractName} drifted from SR5. Missing in SR6: {FormatList(missingInSr6)}. Extra in SR6: {FormatList(extraInSr6)}.");
    }

    private static IReadOnlyDictionary<string, string[]> BuildSectionTargetHostingGroups(
        IReadOnlyList<WorkspaceSurfaceActionDefinition> actions)
        => actions
            .Where(action => action.Kind == WorkspaceSurfaceActionKind.Section)
            .GroupBy(action => NormalizeToken(action.TargetId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(action => $"{NormalizeToken(action.TabId)}::{NormalizeToken(action.Id)}::{action.Kind}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

    private static void AssertDictionaryParity(
        string catalogName,
        IReadOnlyDictionary<string, string[]> sr5Items,
        IReadOnlyDictionary<string, string[]> sr6Items)
    {
        string[] sr5Keys = sr5Items.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        string[] sr6Keys = sr6Items.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        if (!sr5Keys.SequenceEqual(sr6Keys))
        {
            string[] missingKeys = sr5Keys.Except(sr6Keys, StringComparer.Ordinal).ToArray();
            string[] extraKeys = sr6Keys.Except(sr5Keys, StringComparer.Ordinal).ToArray();
            Assert.Fail(
                $"SR6 {catalogName} drifted from SR5. Missing groups in SR6: {FormatList(missingKeys)}. Extra groups in SR6: {FormatList(extraKeys)}.");
        }

        foreach ((string key, string[] sr5GroupValues) in sr5Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            string[] sr6GroupValues = sr6Items[key];
            if (sr5GroupValues.SequenceEqual(sr6GroupValues))
            {
                continue;
            }

            string[] missingValues = sr5GroupValues.Except(sr6GroupValues, StringComparer.Ordinal).ToArray();
            string[] extraValues = sr6GroupValues.Except(sr5GroupValues, StringComparer.Ordinal).ToArray();
            Assert.Fail(
                $"SR6 {catalogName} drifted for '{key}'. Missing in SR6: {FormatList(missingValues)}. Extra in SR6: {FormatList(extraValues)}.");
        }
    }

    private static string FormatList(IReadOnlyList<string> values)
        => values.Count == 0
            ? "(none)"
            : string.Join(" | ", values);

    private static DesktopDialogState CreateCommandDialog(string commandId, string rulesetId)
    {
        return DialogFactory.CreateCommandDialog(
            commandId,
            CreateProfile(),
            DesktopPreferenceState.Default,
            BuildSectionJson("profile"),
            new CharacterWorkspaceId("ws-1"),
            rulesetId,
            activeSectionId: "profile",
            runtimeInspector: string.Equals(commandId, OverviewCommandPolicy.RuntimeInspectorCommandId, StringComparison.Ordinal)
                ? CreateRuntimeInspectorProjection(rulesetId)
                : null,
            masterIndex: CreateMasterIndexResponse(),
            translatorLanguages: null,
            openWorkspaces: [CreateOpenWorkspace(rulesetId)]);
    }

    private static string BuildSectionJson(string sectionId)
        => $$"""
           {
             "sectionId": "{{sectionId}}",
             "rows": [
               "Primary",
               "Secondary"
             ]
           }
           """;

    private static DesktopDialogState UpdateDialogField(DesktopDialogState dialog, string fieldId, string value)
    {
        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field =>
            {
                if (string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                {
                    return field with { Value = value };
                }

                if (string.Equals(dialog.Id, "dialog.new_character.priority_workflow", StringComparison.Ordinal)
                    && string.Equals(field.Id, "newCharacterPriorityLastChangedFieldId", StringComparison.Ordinal))
                {
                    return field with { Value = fieldId };
                }

                return field;
            })
            .ToArray();

        return dialog with { Fields = updatedFields };
    }

    private static DesktopDialogState RebuildDynamicDialog(DesktopDialogState dialog)
    {
        MethodInfo method = typeof(DesktopDialogFactory).GetMethod(
            "RebuildDynamicDialog",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("DesktopDialogFactory.RebuildDynamicDialog was not found.");

        return (DesktopDialogState)(method.Invoke(null, [dialog, DesktopPreferenceState.Default])
            ?? throw new AssertFailedException("DesktopDialogFactory.RebuildDynamicDialog returned null."));
    }

    private static MasterIndexResponse CreateMasterIndexResponse()
        => new(
            Count: 1,
            GeneratedUtc: DateTimeOffset.UtcNow,
            Files:
            [
                new MasterIndexFileEntry("books.xml", "chummer", 42)
            ],
            ReferenceLanePosture: "governed",
            SourcebookCount: 1,
            Sourcebooks:
            [
                new MasterIndexSourcebookEntry(
                    Id: "core-rulebook",
                    Code: "CRB",
                    Name: "Core Rulebook",
                    Permanent: true,
                    ReferencePosture: "governed",
                    RuleSnippetCount: 1,
                    RuleSnippets:
                    [
                        new MasterIndexRuleSnippetEntry(
                            Language: "en-us",
                            Page: 20,
                            Snippet: "Reference notes stay visible while the selected entry remains focused.",
                            Provenance: "books.xml")
                    ],
                    ReferenceSourcePosture: "governed",
                    LocalPdfPath: "/books/core-rulebook.pdf")
            ]);

    private static RuntimeInspectorProjection CreateRuntimeInspectorProjection(string rulesetId)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeRequired(rulesetId);
        string packId = $"official.{normalizedRulesetId}.core";
        string runtimeFingerprint = $"sha256:{normalizedRulesetId}-runtime-fingerprint";
        string runtimeTitle = normalizedRulesetId == RulesetDefaults.Sr6
            ? "SR6 Core"
            : "SR5 Core";

        return new RuntimeInspectorProjection(
            TargetKind: RuntimeInspectorTargetKinds.RuntimeLock,
            TargetId: packId,
            RuntimeLock: new ResolvedRuntimeLock(
                RulesetId: normalizedRulesetId,
                ContentBundles:
                [
                    new ContentBundleDescriptor(
                        BundleId: $"{normalizedRulesetId}.core.bundle",
                        RulesetId: normalizedRulesetId,
                        Version: "1.0.0",
                        Title: "Core Bundle",
                        Description: "Default bundle",
                        AssetPaths: ["data/core.xml"])
                ],
                RulePacks:
                [
                    new ArtifactVersionReference(packId, "1.0.0")
                ],
                ProviderBindings: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [RulePackCapabilityIds.DeriveStat] = $"{packId}/derive.stat"
                },
                EngineApiVersion: "1.0.0",
                RuntimeFingerprint: runtimeFingerprint),
            Install: new ArtifactInstallState(
                State: ArtifactInstallStates.Available,
                RuntimeFingerprint: runtimeFingerprint),
            ResolvedRulePacks:
            [
                new RuntimeInspectorRulePackEntry(
                    new ArtifactVersionReference(packId, "1.0.0"),
                    runtimeTitle,
                    ArtifactVisibilityModes.LocalOnly,
                    ArtifactTrustTiers.Official,
                    [RulePackCapabilityIds.DeriveStat])
            ],
            ProviderBindings:
            [
                new RuntimeInspectorProviderBinding(
                    CapabilityId: RulePackCapabilityIds.DeriveStat,
                    ProviderId: $"{packId}/derive.stat",
                    PackId: packId)
            ],
            CompatibilityDiagnostics:
            [
                new RuntimeLockCompatibilityDiagnostic(
                    State: RuntimeLockCompatibilityStates.Compatible,
                    Message: "Runtime lock resolves against the current RuleProfile and RulePack catalog.",
                    RequiredRulesetId: normalizedRulesetId,
                    RequiredRuntimeFingerprint: runtimeFingerprint)
            ],
            Warnings: [],
            MigrationPreview:
            [
                new RuntimeMigrationPreviewItem(
                    Kind: RuntimeMigrationPreviewChangeKinds.RulePackAdded,
                    Summary: $"Profile applies RulePack '{packId}@1.0.0'.",
                    SubjectId: packId,
                    AfterValue: "1.0.0")
            ],
            GeneratedAtUtc: DateTimeOffset.UtcNow);
    }

    private static OpenWorkspaceState CreateOpenWorkspace(string rulesetId)
        => new(
            Id: new CharacterWorkspaceId("ws-1"),
            Name: "Parity Runner",
            Alias: "PRTY",
            LastOpenedUtc: DateTimeOffset.Parse("2026-05-04T12:00:00+00:00"),
            RulesetId: rulesetId,
            HasSavedWorkspace: true);

    private static CharacterProfileSection CreateProfile()
        => new(
            Name: "Parity Runner",
            Alias: "PRTY",
            PlayerName: string.Empty,
            Metatype: "Human",
            Metavariant: string.Empty,
            Sex: string.Empty,
            Age: string.Empty,
            Height: string.Empty,
            Weight: string.Empty,
            Hair: string.Empty,
            Eyes: string.Empty,
            Skin: string.Empty,
            Concept: string.Empty,
            Description: string.Empty,
            Background: string.Empty,
            CreatedVersion: string.Empty,
            AppVersion: string.Empty,
            BuildMethod: "Priority",
            GameplayOption: string.Empty,
            Created: true,
            Adept: false,
            Magician: false,
            Technomancer: false,
            AI: false,
            MainMugshotIndex: 0,
            MugshotCount: 0);

    private sealed record SharedCommandExecutionObservation(
        string CommandId,
        string Notice,
        string Error,
        string ActiveDialogId,
        bool SaveInvoked,
        bool DownloadInvoked,
        bool PrintInvoked,
        bool CloseAllInvoked,
        string CloseAllMessage,
        string ClosedWorkspaceId,
        string ImportedRulesetId,
        string ImportedFormat,
        bool ImportedHasCritterMetatype,
        bool ImportedHasPriorityBuildMethod,
        bool ImportedHasStarterCritterConcept);
}
