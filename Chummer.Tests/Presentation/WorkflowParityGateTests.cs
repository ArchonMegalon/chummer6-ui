#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WorkflowParityGateTests
{
    private static readonly string[] SupportedRulesets =
    [
        RulesetDefaults.Sr4,
        RulesetDefaults.Sr5,
        RulesetDefaults.Sr6
    ];

    private static readonly CatalogOnlyRulesetShellCatalogResolver Resolver = new();
    private static readonly DesktopDialogFactory DialogFactory = new();
    private static readonly DialogCoordinator Coordinator = new();
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly IReadOnlyDictionary<string, MuscleMemoryDialogSurfaceContract> Chummer5aDialogSurfaceContracts =
        LoadDialogSurfaceContracts("CHUMMER5A_MUSCLE_MEMORY_INVENTORY.generated.json");
    private static readonly IReadOnlyDictionary<string, MuscleMemoryDialogSurfaceContract> Sr4DialogSurfaceContracts =
        LoadDialogSurfaceContracts("CHUMMER4_SR4_MUSCLE_MEMORY_INVENTORY.generated.json");
    private static readonly HashSet<string> DesignAuthorizedDialogFieldIds =
        LoadDesignAuthorizedDialogFieldIds();
    private static readonly string[] CharacterRosterVisibleFieldIds =
    [
        "rosterSectionTabs",
        "rosterDetailTabs",
        "rosterOpenCount",
        "rosterSavedCount",
        "rosterRulesetMix",
        "rosterTree",
        "rosterFolderName",
        "rosterTargetFolder",
        "rosterSourceFolder",
        "rosterCustomFolders",
        "rosterMoveTargets",
        "rosterDragDropGuide",
        "rosterHierarchyPolicy",
        "rosterHierarchyStatus",
        "rosterSelectionTrail",
        "rosterMugshot",
        "rosterSelectedRunner",
        "rosterWatchFolderStatus",
        "rosterRunnerCommands",
        "rosterWatchFolderCommands",
        "rosterSelectedRunnerStatus",
        "rosterSelectedRunnerBackground",
        "rosterSelectedRunnerNotes",
        "rosterEntries"
    ];
    private static readonly string[] GlobalSettingsVisibleFieldIds =
    [
        "globalLanguage",
        "globalSheetLanguage",
        "globalCompactMode",
        "globalCharacterPriority",
        "globalUpdateMode",
        "globalPreferNightlyBuilds",
        "globalCharacterRosterPath",
        "globalHideMasterIndex",
        "globalAnalyticsOptOut",
        "globalDisableAiFeatures"
    ];

    [TestMethod]
    public void Menu_dialog_workflows_are_exhaustively_classified()
    {
        string[] discovered = SupportedRulesets
            .SelectMany(rulesetId => Resolver.ResolveCommands(rulesetId))
            .Select(command => command.Id)
            .Where(commandId => OverviewCommandPolicy.IsDialogCommand(commandId) || OverviewCommandPolicy.IsImportHintCommand(commandId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] classified = MenuContracts.Keys
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            classified,
            discovered,
            "Every menu-triggered dialog workflow must be explicitly classified before parity claims are allowed.");
    }

    [TestMethod]
    public void Legacy_ui_controls_are_exhaustively_classified()
    {
        string[] discovered = LegacyUiControlCatalog.All
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] classified = UiControlContracts.Keys
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            classified,
            discovered,
            "Every legacy UI control must carry a parity contract before the workflow gate can pass.");
    }

    [TestMethod]
    public void Quick_action_roots_are_exhaustively_classified()
    {
        string[] discovered = SupportedRulesets
            .SelectMany(rulesetId => Resolver.ResolveNavigationTabs(rulesetId)
                .SelectMany(tab => Resolver.ResolveWorkspaceActionsForTab(tab.Id, rulesetId))
                .Where(action => action.Kind == WorkspaceSurfaceActionKind.Section)
                .SelectMany(action => SectionQuickActionCatalog.ForSection(rulesetId, action.TargetId))
                .Select(action => action.ControlId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] classified = UiControlContracts.Values
            .Where(contract => contract.IsQuickActionRoot)
            .Select(contract => contract.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            classified,
            discovered,
            "Every section quick-action root must be explicitly classified before recursive parity can pass.");
    }

    [TestMethod]
    public async Task Menu_dialog_workflows_keep_recursive_parity()
    {
        foreach (string rulesetId in SupportedRulesets)
        {
            foreach (MenuWorkflowContract contract in MenuContracts.Values.OrderBy(contract => contract.Id, StringComparer.Ordinal))
            {
                await AssertMenuWorkflowParityAsync(rulesetId, contract);
            }
        }
    }

    [TestMethod]
    public async Task Legacy_ui_controls_keep_recursive_parity()
    {
        foreach (string rulesetId in SupportedRulesets)
        {
            foreach (UiControlWorkflowContract contract in UiControlContracts.Values.OrderBy(contract => contract.Id, StringComparer.Ordinal))
            {
                await AssertUiControlWorkflowParityAsync(rulesetId, contract);
            }
        }
    }

    private static async Task AssertMenuWorkflowParityAsync(string rulesetId, MenuWorkflowContract contract)
    {
        DesktopDialogState dialog = CreateCommandDialog(contract.Id, rulesetId);
        AssertDialogParity(rulesetId, contract.Id, contract.Shape, dialog);

        WorkflowHarness closeHarness = CreateHarness(rulesetId, dialog, "tab-info", "profile");
        string? closeActionId = ResolveCloseLikeActionId(dialog);
        if (!string.IsNullOrWhiteSpace(closeActionId))
        {
            await closeHarness.ActAsync(closeActionId);
            AssertReturnedSurfaceParity(closeHarness.State, contract.Id, "tab-info", "profile");
        }

        if (string.Equals(contract.Id, "new_character", StringComparison.Ordinal))
        {
            await AssertNewCharacterRecursiveParityAsync(rulesetId);
            return;
        }

        if (string.Equals(contract.Id, "new_character_origin", StringComparison.Ordinal))
        {
            return;
        }

        string? primaryActionId = ResolvePrimaryActionId(dialog);
        if (string.IsNullOrWhiteSpace(primaryActionId))
        {
            return;
        }

        WorkflowHarness primaryHarness = CreateHarness(rulesetId, dialog, "tab-info", "profile");
        await primaryHarness.ActAsync(primaryActionId);

        if (primaryHarness.State.ActiveDialog is { } nextDialog)
        {
            AssertDialogParity(rulesetId, contract.Id, contract.Shape, nextDialog);

            string? exitActionId = ResolveCloseLikeActionId(nextDialog)
                ?? ResolvePrimaryActionId(nextDialog)
                ?? ResolveContinueActionId(nextDialog);

            if (!string.IsNullOrWhiteSpace(exitActionId))
            {
                await primaryHarness.ActAsync(exitActionId);
            }
        }

        AssertReturnedSurfaceParity(primaryHarness.State, contract.Id, "tab-info", "profile");
    }

    private static async Task AssertUiControlWorkflowParityAsync(string rulesetId, UiControlWorkflowContract contract)
    {
        DesktopDialogState dialog = DialogFactory.CreateUiControlDialog(contract.Id, DesktopPreferenceState.Default);
        AssertDialogParity(rulesetId, contract.Id, contract.Shape, dialog);

        WorkflowHarness closeHarness = CreateHarness(rulesetId, dialog, contract.ReturnTabId, contract.ReturnSectionId);
        string? closeActionId = ResolveCloseLikeActionId(dialog);
        if (!string.IsNullOrWhiteSpace(closeActionId))
        {
            await closeHarness.ActAsync(closeActionId);
            AssertReturnedSurfaceParity(closeHarness.State, contract.Id, contract.ReturnTabId, contract.ReturnSectionId);
        }

        if (contract.SupportsAddMoreLoop
            && dialog.Actions.Any(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)))
        {
            WorkflowHarness addMoreHarness = CreateHarness(rulesetId, dialog, contract.ReturnTabId, contract.ReturnSectionId);
            await addMoreHarness.ActAsync("add_more");
            Assert.IsNotNull(addMoreHarness.State.ActiveDialog, $"'{contract.Id}' add-more branch must keep the dialog open.");
            AssertDialogParity(rulesetId, contract.Id, contract.Shape, addMoreHarness.State.ActiveDialog!);

            string? addMoreExitActionId = ResolveCloseLikeActionId(addMoreHarness.State.ActiveDialog!)
                ?? ResolveContinueActionId(addMoreHarness.State.ActiveDialog!);
            if (!string.IsNullOrWhiteSpace(addMoreExitActionId))
            {
                await addMoreHarness.ActAsync(addMoreExitActionId);
                AssertReturnedSurfaceParity(addMoreHarness.State, contract.Id, contract.ReturnTabId, contract.ReturnSectionId);
            }
        }

        string? primaryActionId = ResolvePrimaryActionId(dialog);
        if (string.IsNullOrWhiteSpace(primaryActionId))
        {
            return;
        }

        WorkflowHarness primaryHarness = CreateHarness(rulesetId, dialog, contract.ReturnTabId, contract.ReturnSectionId);
        await primaryHarness.ActAsync(primaryActionId);

        if (primaryHarness.State.ActiveDialog is { } nextDialog)
        {
            AssertDialogParity(rulesetId, contract.Id, contract.Shape, nextDialog);

            string? exitActionId = ResolveCloseLikeActionId(nextDialog)
                ?? ResolveContinueActionId(nextDialog)
                ?? ResolvePrimaryActionId(nextDialog);

            if (!string.IsNullOrWhiteSpace(exitActionId))
            {
                await primaryHarness.ActAsync(exitActionId);
            }
        }

        AssertReturnedSurfaceParity(primaryHarness.State, contract.Id, contract.ReturnTabId, contract.ReturnSectionId);
    }

    private static async Task AssertNewCharacterRecursiveParityAsync(string rulesetId)
    {
        await AssertNewCharacterRecursiveParityAsync(rulesetId, buildMethod: "Karma");

        DesktopDialogState dialog = CreateCommandDialog("new_character", rulesetId);
        WorkflowHarness harness = CreateHarness(rulesetId, dialog, "tab-info", "profile");
        harness.UpdateDialogField("newCharacterRulesetId", rulesetId);
        harness.UpdateDialogField("newCharacterBuildMethod", "Priority");

        string? resolvedBuildMethod = DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "newCharacterBuildMethod");
        if (string.Equals(resolvedBuildMethod, "Priority", StringComparison.Ordinal))
        {
            await AssertNewCharacterRecursiveParityAsync(rulesetId, buildMethod: "Priority");
        }
    }

    [TestMethod]
    public async Task Runtime_backed_new_character_matrix_bootstraps_supported_sr5_and_fails_closed_elsewhere()
    {
        foreach (string rulesetId in SupportedRulesets)
        {
            await AssertNewCharacterRecursiveParityAsync(rulesetId);
        }
    }

    [TestMethod]
    public async Task Runtime_backed_new_character_build_matrix_uses_only_authoritative_bootstrap_paths()
    {
        foreach (string rulesetId in SupportedRulesets)
        {
            DesktopDialogState dialog = CreateCommandDialog("new_character", rulesetId);
            DesktopDialogField buildMethodField = dialog.Fields.Single(field => string.Equals(field.Id, "newCharacterBuildMethod", StringComparison.Ordinal));
            string[] buildMethods = buildMethodField.Options!
                .Select(option => option.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            Assert.IsTrue(buildMethods.Length > 0, $"{rulesetId} must expose at least one build method.");
            if (string.Equals(rulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal))
            {
                CollectionAssert.Contains(buildMethods, "BP", "SR4 character creation must expose and execute BP.");
            }

            foreach (string buildMethod in buildMethods)
            {
                await AssertNewCharacterRecursiveParityAsync(rulesetId, buildMethod);
            }
        }
    }

    [TestMethod]
    public async Task Priority_workflow_mystic_adept_keeps_assensing_visible_in_scrollable_dialog_contract()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
        harness.UpdateDialogField("newCharacterBuildMethod", "Priority");
        await harness.ActAsync("create_character");

        harness.UpdateDialogField("newCharacterPriorityTalent", "B");
        harness.UpdateDialogField("newCharacterPriorityTalentChoice", "Mystic Adept");

        Assert.IsNotNull(harness.State.ActiveDialog);
        DesktopDialogState priorityDialog = harness.State.ActiveDialog!;
        AssertSelectFieldContains(priorityDialog, "newCharacterPrioritySkillChoice1", "Assensing");
        AssertSelectFieldContains(priorityDialog, "newCharacterPrioritySkillChoice2", "Assensing");
        AssertSelectFieldContains(priorityDialog, "newCharacterPrioritySkillChoice3", "Assensing");
        PriorityWorkflowDialogRuntimeState runtimeState = PriorityWorkflowDialogRuntimeStateSerializer.Parse(
            DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityWorkflowState"));
        Assert.IsTrue(runtimeState.SkillChoice1.Visible, "Mystic Adept must render the first skill combo.");
        Assert.IsTrue(runtimeState.SkillChoice2.Visible, "Mystic Adept must render the second skill combo.");
        Assert.IsTrue(runtimeState.SkillChoice3.Visible, "Mystic Adept must render the third skill combo; this is where Assensing was previously cut off.");
    }

    [TestMethod]
    public async Task Runtime_backed_new_character_character_settings_materialize_house_rule_and_build_method_defaults()
    {
        DesktopPreferenceState seededPreferences = DesktopPreferenceState.Default with
        {
            CharacterPriority = "Karma",
            HouseRulesEnabled = true,
            CharacterNotes = "Carry forward seeded notes."
        };

        foreach (string rulesetId in SupportedRulesets)
        {
            DesktopDialogState settingsDialog = DialogFactory.CreateCommandDialog(
                "character_settings",
                CreateProfile(),
                seededPreferences,
                BuildSectionJson("profile"),
                new CharacterWorkspaceId("ws-settings"),
                rulesetId,
                runtimeInspector: null,
                masterIndex: CreateMasterIndexResponse(),
                translatorLanguages: null,
                openWorkspaces: [CreateOpenWorkspace(rulesetId)]);

            Assert.AreEqual("dialog.character_settings", settingsDialog.Id);
            Assert.AreEqual("Karma", DesktopDialogFieldValueParser.GetValue(settingsDialog, "characterPriority"));
            Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(settingsDialog, "characterHouseRulesEnabled"));
            Assert.AreEqual("Carry forward seeded notes.", DesktopDialogFieldValueParser.GetValue(settingsDialog, "characterNotes"));

            DesktopDialogState newCharacterDialog = DialogFactory.CreateCommandDialog(
                "new_character",
                CreateProfile(),
                seededPreferences,
                BuildSectionJson("profile"),
                new CharacterWorkspaceId("ws-new-character"),
                rulesetId,
                runtimeInspector: null,
                masterIndex: CreateMasterIndexResponse(),
                translatorLanguages: null,
                openWorkspaces: [CreateOpenWorkspace(rulesetId)]);

            Assert.AreEqual(rulesetId, DesktopDialogFieldValueParser.GetValue(newCharacterDialog, "newCharacterRulesetId"));
            Assert.AreEqual("Karma", DesktopDialogFieldValueParser.GetValue(newCharacterDialog, "newCharacterBuildMethod"));
            Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(newCharacterDialog, "newCharacterHouseRulesEnabled"));

            Assert.AreEqual(
                "true",
                DesktopDialogFieldValueParser.GetValue(
                    newCharacterDialog,
                    "newCharacterHouseRulesEnabled"));
        }
    }

    [TestMethod]
    public async Task Runtime_backed_origin_dossier_story_first_flow_materializes_gm_constraints_before_sr4_bp_build()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr4);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr4, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr4);
        harness.UpdateDialogField("newCharacterBuildMethod", "BP");
        await harness.ActAsync("start_from_origin");

        Assert.IsNotNull(harness.State.ActiveDialog, "Start Origin Dossier must open the story-first wizard.");
        DesktopDialogState originWizard = harness.State.ActiveDialog!;
        Assert.AreEqual("dialog.new_character.origin_wizard", originWizard.Id);
        Assert.AreEqual("Origin Dossier", originWizard.Title);
        StringAssert.Contains(originWizard.Message, "Pick only the basics, then build the story.");
        AssertDialogParity(RulesetDefaults.Sr4, "new_character_origin", WorkflowShape.Choice, originWizard);
        AssertExactVisibleSelectField(
            originWizard,
            "newCharacterOriginMetatypePreference",
            "auto",
            ("auto", "Fit the story"),
            ("human", "Human"),
            ("elf", "Elf"),
            ("dwarf", "Dwarf"),
            ("ork", "Ork"),
            ("troll", "Troll"));
        AssertExactVisibleSelectField(
            originWizard,
            "newCharacterOriginArchetypeIntent",
            "decker",
            ("decker", "Decker"),
            ("street_sam", "Street Samurai"),
            ("mage", "Mage"),
            ("adept", "Adept"),
            ("face", "Face"),
            ("rigger", "Rigger"),
            ("technomancer", "Technomancer"),
            ("auto", "Fit the story"));
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Hidden, originWizard.Fields.Single(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Hidden, originWizard.Fields.Single(field => string.Equals(field.Id, "newCharacterOriginGmConstraintPreset", StringComparison.Ordinal)).LayoutSlot);
        Assert.IsTrue(originWizard.Actions.Single(action => string.Equals(action.Id, "generate_fitting_build", StringComparison.Ordinal)).IsPrimary);

        harness.UpdateDialogField("newCharacterOriginMetatypePreference", "troll");
        harness.UpdateDialogField("newCharacterOriginArchetypeIntent", "decker");
        harness.UpdateDialogField("newCharacterOriginBuildPreference", "BP");
        harness.UpdateDialogField("newCharacterOriginGmConstraintPreset", "illegal_addiction");
        harness.UpdateDialogField("newCharacterOriginGmRequirements", "Must be addicted to an illegal drug. Must have Intelligence 2+.");

        originWizard = harness.State.ActiveDialog!;
        Assert.AreEqual("Troll", DesktopDialogFieldValueParser.GetValue(originWizard, "newCharacterOriginMetatype"));
        Assert.AreEqual("BP", DesktopDialogFieldValueParser.GetValue(originWizard, "newCharacterOriginBuildMethod"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(originWizard, "newCharacterOriginQualityFocus") ?? string.Empty, "Addiction");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(originWizard, "newCharacterOriginGmRequirementSummary") ?? string.Empty, "illegal drug");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(originWizard, "newCharacterOriginGmRequirementSummary") ?? string.Empty, "Intelligence 2+");

        await harness.ActAsync("generate_fitting_build");

        Assert.IsNotNull(harness.State.ActiveDialog, "Build story must open the origin story review before mechanics.");
        DesktopDialogState originBuild = harness.State.ActiveDialog!;
        Assert.AreEqual("dialog.new_character.origin_build", originBuild.Id);
        StringAssert.Contains(originBuild.Message, "Read this first.");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(originBuild, "newCharacterOriginBookPreview") ?? string.Empty, "Origin Dossier");
        Assert.AreEqual("approved_origin_story", DesktopDialogFieldValueParser.GetValue(originBuild, "newCharacterOriginAliceSeedSource"));
        Assert.AreEqual("BP", DesktopDialogFieldValueParser.GetValue(originBuild, "newCharacterWorkflowBuildMethod"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(originBuild, "newCharacterOriginBuildLogic") ?? string.Empty, "Troll");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(originBuild, "newCharacterOriginBuildLogic") ?? string.Empty, "Addiction");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(originBuild, "newCharacterOriginImplications") ?? string.Empty, "Sheet Changes | none yet");

        await harness.ActAsync("open_origin_guided_chargen");

        Assert.IsNotNull(harness.State.ActiveDialog, "Origin review must hand off to a normal guided character workflow.");
        DesktopDialogState continuation = harness.State.ActiveDialog!;
        Assert.AreEqual("dialog.new_character.karma_workflow", continuation.Id);
        Assert.AreEqual("BP", DesktopDialogFieldValueParser.GetValue(continuation, "newCharacterWorkflowBuildMethod"));
        StringAssert.Contains(harness.State.Notice ?? string.Empty, "Origin story translated into a guided build plan.");
    }

    private static async Task AssertNewCharacterRecursiveParityAsync(string rulesetId, string buildMethod)
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", rulesetId);
        WorkflowHarness harness = CreateHarness(rulesetId, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", rulesetId);
        harness.UpdateDialogField("newCharacterBuildMethod", buildMethod);
        await harness.ActAsync("create_character");

        bool hasBootstrapAuthority = string.Equals(
                rulesetId,
                RulesetDefaults.Sr5,
                StringComparison.Ordinal)
            && CharacterCreationBootstrapProfiles.TryResolveCanonicalSettingsProfileId(
                buildMethod,
                out _);
        if (!hasBootstrapAuthority)
        {
            Assert.AreEqual("dialog.new_character", harness.State.ActiveDialog?.Id);
            StringAssert.Contains(
                harness.State.Error ?? string.Empty,
                "Authoritative character creation is unavailable");
            return;
        }

        AssertReturnedSurfaceParity(harness.State, $"new_character.{buildMethod}", "tab-info", "profile");
        Assert.IsNotNull(harness.State.WorkspaceId, $"'{buildMethod}' new-character branch must return to a real workspace surface.");
        Assert.IsNull(harness.State.ActiveDialog, "Supported SR5 creation must not expose the legacy continuation dialog.");
        Assert.AreEqual("new_character", harness.State.LastCommandId, "The originating command id must stay visible after workflow completion.");
        Assert.IsTrue(harness.State.Session.OpenWorkspaces.Count > 0, "Completing the workflow must leave an open workspace in session state.");
    }

    [TestMethod]
    public async Task Priority_workflow_duplicate_priority_selection_auto_reconciles_in_priority_mode()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
        harness.UpdateDialogField("newCharacterBuildMethod", "Priority");
        await harness.ActAsync("create_character");

        harness.UpdateDialogField("newCharacterPriorityHeritage", "A");

        Assert.IsNotNull(harness.State.ActiveDialog);
        DesktopDialogState priorityDialog = harness.State.ActiveDialog!;
        Assert.AreEqual("A", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityHeritage"));
        Assert.AreEqual("D", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityResources"));
        Assert.AreEqual("newCharacterPriorityHeritage", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityLastChangedFieldId"));
    }

    [TestMethod]
    public async Task Sum_to_ten_priority_workflow_preserves_duplicates_and_updates_live_total()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
        harness.UpdateDialogField("newCharacterBuildMethod", "SumToTen");
        await harness.ActAsync("create_character");

        harness.UpdateDialogField("newCharacterPriorityHeritage", "A");

        Assert.IsNotNull(harness.State.ActiveDialog);
        DesktopDialogState priorityDialog = harness.State.ActiveDialog!;
        PriorityWorkflowDialogRuntimeState runtimeState = PriorityWorkflowDialogRuntimeStateSerializer.Parse(
            DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityWorkflowState"));

        Assert.AreEqual("A", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityHeritage"));
        Assert.AreEqual("A", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityResources"));
        Assert.AreEqual("13/10", runtimeState.SumToTenLabel);
    }

    [TestMethod]
    public async Task Priority_workflow_each_priority_letter_combobox_selection_change_records_the_changed_field()
    {
        string[] fieldIds =
        [
            "newCharacterPriorityHeritage",
            "newCharacterPriorityAttributes",
            "newCharacterPriorityTalent",
            "newCharacterPrioritySkills",
            "newCharacterPriorityResources"
        ];

        foreach (string fieldId in fieldIds)
        {
            DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
            WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

            harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
            harness.UpdateDialogField("newCharacterBuildMethod", "Priority");
            await harness.ActAsync("create_character");

            harness.UpdateDialogField(fieldId, "A");

            Assert.IsNotNull(harness.State.ActiveDialog);
            DesktopDialogState priorityDialog = harness.State.ActiveDialog!;
            Assert.AreEqual("A", DesktopDialogFieldValueParser.GetValue(priorityDialog, fieldId), $"{fieldId} must rebuild to the selected value.");
            Assert.AreEqual(fieldId, DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityLastChangedFieldId"));
        }
    }

    [TestMethod]
    public async Task Priority_workflow_category_metatype_and_metavariant_selection_changes_refresh_dependent_state()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
        harness.UpdateDialogField("newCharacterBuildMethod", "Priority");
        await harness.ActAsync("create_character");

        harness.UpdateDialogField("newCharacterMetatypeCategory", "Metahuman");
        harness.UpdateDialogField("newCharacterMetatype", "Elf");
        harness.UpdateDialogField("newCharacterMetavariant", "Dryad");

        Assert.IsNotNull(harness.State.ActiveDialog);
        DesktopDialogState priorityDialog = harness.State.ActiveDialog!;
        PriorityWorkflowDialogRuntimeState runtimeState = PriorityWorkflowDialogRuntimeStateSerializer.Parse(
            DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityWorkflowState"));

        CollectionAssert.AreEqual(
            new[] { "Elf", "Dryad" },
            runtimeState.MetavariantOptions.Select(option => option.Value).ToArray(),
            "Metatype selection must repopulate the metavariant combobox.");
        Assert.AreEqual("Dryad", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterMetavariant"));
        Assert.AreEqual("35", runtimeState.MetatypeKarma);
        Assert.AreEqual("Run Faster · Dryad", runtimeState.Source);
        Assert.AreEqual("5 / 8", runtimeState.InspectAttributes.Single(attribute => string.Equals(attribute.Label, "CHA", StringComparison.Ordinal)).Value);
        CollectionAssert.Contains(runtimeState.Qualities.ToArray(), "Glamour");

        DesktopDialogField talentChoiceField = priorityDialog.Fields.Single(field => string.Equals(field.Id, "newCharacterPriorityTalentChoice", StringComparison.Ordinal));
        CollectionAssert.Contains(talentChoiceField.Options!.Select(option => option.Value).ToArray(), "Aspected Magician");
    }

    [TestMethod]
    public async Task Priority_workflow_spirit_force_and_possession_controls_follow_chummer5_visibility_and_values()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
        harness.UpdateDialogField("newCharacterBuildMethod", "Priority");
        await harness.ActAsync("create_character");

        DesktopDialogState initialDialog = harness.State.ActiveDialog!;
        Assert.AreEqual(
            DesktopDialogFieldLayoutSlots.Hidden,
            initialDialog.Fields.Single(field => field.Id == "newCharacterForce").LayoutSlot);
        Assert.AreEqual(
            DesktopDialogFieldLayoutSlots.Hidden,
            initialDialog.Fields.Single(field => field.Id == "newCharacterPossessionBased").LayoutSlot);
        Assert.AreEqual(
            DesktopDialogFieldLayoutSlots.Hidden,
            initialDialog.Fields.Single(field => field.Id == "newCharacterPossessionMethod").LayoutSlot);

        harness.UpdateDialogField("newCharacterMetatypeCategory", "Spirits");
        harness.UpdateDialogField("newCharacterMetatype", "Spirit of Air");
        harness.UpdateDialogField("newCharacterForce", "6");
        harness.UpdateDialogField("newCharacterPossessionBased", "true");
        harness.UpdateDialogField("newCharacterPossessionMethod", "Inhabitation");

        DesktopDialogState spiritDialog = harness.State.ActiveDialog!;
        PriorityWorkflowDialogRuntimeState runtimeState = PriorityWorkflowDialogRuntimeStateSerializer.Parse(
            DesktopDialogFieldValueParser.GetValue(spiritDialog, "newCharacterPriorityWorkflowState"));
        DesktopDialogField methodField = spiritDialog.Fields.Single(field => field.Id == "newCharacterPossessionMethod");

        Assert.IsTrue(runtimeState.ForceVisible);
        Assert.AreEqual(6, runtimeState.Force);
        Assert.IsTrue(runtimeState.PossessionVisible);
        Assert.IsTrue(runtimeState.PossessionBased);
        Assert.AreEqual("6", DesktopDialogFieldValueParser.GetValue(spiritDialog, "newCharacterForce"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(spiritDialog, "newCharacterPossessionBased"));
        Assert.AreEqual("Inhabitation", DesktopDialogFieldValueParser.GetValue(spiritDialog, "newCharacterPossessionMethod"));
        Assert.AreNotEqual(DesktopDialogFieldLayoutSlots.Hidden, methodField.LayoutSlot);
        CollectionAssert.AreEqual(
            new[] { "Possession", "Inhabitation" },
            methodField.Options!.Select(option => option.Value).ToArray());

        harness.UpdateDialogField("newCharacterMetatypeCategory", "Standard");
        DesktopDialogState standardDialog = harness.State.ActiveDialog!;
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(standardDialog, "newCharacterPossessionBased"));
        Assert.AreEqual("1", DesktopDialogFieldValueParser.GetValue(standardDialog, "newCharacterForce"));
        Assert.AreEqual(
            DesktopDialogFieldLayoutSlots.Hidden,
            standardDialog.Fields.Single(field => field.Id == "newCharacterPossessionMethod").LayoutSlot);
    }

    [TestMethod]
    public async Task Priority_workflow_talent_priority_selection_rebuilds_talent_choice_options_before_commit()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
        harness.UpdateDialogField("newCharacterBuildMethod", "Priority");
        await harness.ActAsync("create_character");

        harness.UpdateDialogField("newCharacterMetatype", "Elf");
        harness.UpdateDialogField("newCharacterMetavariant", "Dryad");
        harness.UpdateDialogField("newCharacterPriorityTalent", "B");

        Assert.IsNotNull(harness.State.ActiveDialog);
        DesktopDialogState priorityDialog = harness.State.ActiveDialog!;
        DesktopDialogField talentChoiceField = priorityDialog.Fields.Single(field => string.Equals(field.Id, "newCharacterPriorityTalentChoice", StringComparison.Ordinal));

        CollectionAssert.Contains(talentChoiceField.Options!.Select(option => option.Value).ToArray(), "Aspected Magician");
        Assert.AreEqual("newCharacterPriorityTalent", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityLastChangedFieldId"));
    }

    [TestMethod]
    public async Task Priority_workflow_heritage_selection_rebuilds_metatype_options_and_repairs_invalid_selection()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
        harness.UpdateDialogField("newCharacterBuildMethod", "Priority");
        await harness.ActAsync("create_character");

        harness.UpdateDialogField("newCharacterPriorityHeritage", "A");
        harness.UpdateDialogField("newCharacterMetatype", "Troll");
        harness.UpdateDialogField("newCharacterPriorityHeritage", "D");

        Assert.IsNotNull(harness.State.ActiveDialog);
        DesktopDialogState priorityDialog = harness.State.ActiveDialog!;
        PriorityWorkflowDialogRuntimeState runtimeState = PriorityWorkflowDialogRuntimeStateSerializer.Parse(
            DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityWorkflowState"));
        DesktopDialogField metatypeField = priorityDialog.Fields.Single(field => string.Equals(field.Id, "newCharacterMetatype", StringComparison.Ordinal));

        CollectionAssert.AreEquivalent(
            new[] { "Human", "Elf" },
            metatypeField.Options!.Select(option => option.Value).ToArray(),
            "Heritage priority changes must rebuild the metatype list.");
        Assert.AreEqual("Elf", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterMetatype"));
        Assert.AreEqual("1", runtimeState.SpecialAttributes);
    }

    [TestMethod]
    public async Task Priority_workflow_talent_selection_materializes_skill_choices_and_repairs_duplicates()
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", RulesetDefaults.Sr5);
        WorkflowHarness harness = CreateHarness(RulesetDefaults.Sr5, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", RulesetDefaults.Sr5);
        harness.UpdateDialogField("newCharacterBuildMethod", "Priority");
        await harness.ActAsync("create_character");

        harness.UpdateDialogField("newCharacterPriorityTalent", "B");
        harness.UpdateDialogField("newCharacterPriorityTalentChoice", "Magician");
        harness.UpdateDialogField("newCharacterPrioritySkillChoice1", "Spellcasting");
        harness.UpdateDialogField("newCharacterPrioritySkillChoice2", "Spellcasting");

        Assert.IsNotNull(harness.State.ActiveDialog);
        DesktopDialogState priorityDialog = harness.State.ActiveDialog!;
        PriorityWorkflowDialogRuntimeState runtimeState = PriorityWorkflowDialogRuntimeStateSerializer.Parse(
            DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPriorityWorkflowState"));

        Assert.IsTrue(runtimeState.SkillChoice1.Visible);
        Assert.IsTrue(runtimeState.SkillChoice2.Visible);
        Assert.IsFalse(runtimeState.SkillChoice3.Visible);
        Assert.AreEqual("Spellcasting", DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPrioritySkillChoice1"));
        Assert.AreNotEqual(
            DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPrioritySkillChoice1"),
            DesktopDialogFieldValueParser.GetValue(priorityDialog, "newCharacterPrioritySkillChoice2"),
            "Magician continuation must repair duplicate free-skill choices immediately.");
    }

    private static DesktopDialogState CreateCommandDialog(string commandId, string rulesetId)
    {
        return DialogFactory.CreateCommandDialog(
            commandId,
            CreateProfile(),
            DesktopPreferenceState.Default,
            BuildSectionJson("profile"),
            new CharacterWorkspaceId("ws-1"),
            rulesetId,
            runtimeInspector: string.Equals(commandId, OverviewCommandPolicy.RuntimeInspectorCommandId, StringComparison.Ordinal)
                ? CreateRuntimeInspectorProjection(rulesetId)
                : null,
            masterIndex: CreateMasterIndexResponse(),
            translatorLanguages: null,
            openWorkspaces: [CreateOpenWorkspace(rulesetId)]);
    }

    private static WorkflowHarness CreateHarness(
        string rulesetId,
        DesktopDialogState dialog,
        string returnTabId,
        string returnSectionId)
    {
        IReadOnlyList<AppCommandDefinition> commands = Resolver.ResolveCommands(rulesetId);
        IReadOnlyList<NavigationTabDefinition> tabs = Resolver.ResolveNavigationTabs(rulesetId);
        OpenWorkspaceState workspace = CreateOpenWorkspace(rulesetId);
        CharacterWorkspaceId workspaceId = workspace.Id;

        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            WorkspaceId = workspaceId,
            OpenWorkspaces = [workspace],
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: workspaceId,
                OpenWorkspaces: [workspace],
                RecentWorkspaceIds: [workspaceId]),
            Profile = CreateProfile(),
            Progress = CreateProgress(),
            Skills = CreateSkills(),
            Rules = CreateRules(),
            Build = CreateBuild(),
            Movement = CreateMovement(),
            Awakening = CreateAwakening(),
            ActiveTabId = returnTabId,
            ActiveSectionId = returnSectionId,
            ActiveSectionJson = BuildSectionJson(returnSectionId),
            ActiveSectionRows = BuildSectionRows(returnSectionId),
            ActiveDialog = dialog,
            Preferences = DesktopPreferenceState.Default,
            Commands = commands,
            NavigationTabs = tabs,
            LastCommandId = string.Equals(dialog.Id, "dialog.new_character", StringComparison.Ordinal) ? "new_character" : null
        };

        return new WorkflowHarness(state, returnTabId, returnSectionId);
    }

    private static void AssertDialogParity(string rulesetId, string workflowId, WorkflowShape shape, DesktopDialogState dialog)
    {
        Assert.AreNotEqual("dialog.generic", dialog.Id, $"'{workflowId}' must not fall back to the generic command dialog.");
        Assert.AreNotEqual("dialog.ui.generic", dialog.Id, $"'{workflowId}' must not fall back to the generic UI-control dialog.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(dialog.Title), $"'{workflowId}' must keep a concrete dialog title.");
        Assert.IsTrue(dialog.Actions.Count > 0, $"'{workflowId}' must expose concrete dialog actions.");

        switch (shape)
        {
            case WorkflowShape.Selection:
                Assert.IsTrue(dialog.Actions.Any(action => string.Equals(action.Id, "add", StringComparison.Ordinal)), $"'{workflowId}' must expose an add action.");
                Assert.IsTrue(dialog.Actions.Any(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)), $"'{workflowId}' must expose add-more continuation.");
                Assert.IsTrue(dialog.Fields.Any(IsBrowseAffordance), $"'{workflowId}' must keep browse posture instead of plain text-only inputs.");
                Assert.IsTrue(dialog.Fields.Any(IsDetailAffordance), $"'{workflowId}' must keep detail posture visible.");
                Assert.IsTrue(dialog.Fields.Any(IsChoiceAffordance), $"'{workflowId}' must keep at least one non-text filter/choice affordance.");
                break;

            case WorkflowShape.DenseEditor:
                Assert.IsTrue(
                    dialog.Fields.Count(field => !field.IsReadOnly) >= 1
                    || dialog.Actions.Any(action => !IsCloseFamily(action.Id)),
                    $"'{workflowId}' must expose editable posture or a concrete apply/advance action.");
                Assert.IsTrue(
                    dialog.Fields.Any(IsDetailAffordance)
                    || dialog.Fields.Any(field => field.VisualKind is DesktopDialogFieldVisualKinds.Snippet or DesktopDialogFieldVisualKinds.Narrative),
                    $"'{workflowId}' must keep dense details or notes visible.");
                break;

            case WorkflowShape.Choice:
                Assert.IsTrue(dialog.Actions.Any(action => !IsCloseFamily(action.Id)), $"'{workflowId}' must expose a real workflow action.");
                Assert.IsTrue(dialog.Fields.Any(IsChoiceAffordance), $"'{workflowId}' must keep explicit choice/config affordances.");
                break;

            case WorkflowShape.Import:
                Assert.IsTrue(dialog.Actions.Any(action => string.Equals(action.Id, "import", StringComparison.Ordinal)), $"'{workflowId}' must expose import.");
                Assert.IsTrue(dialog.Fields.Any(field => field.IsMultiline && !field.IsReadOnly), $"'{workflowId}' must keep an editable payload surface.");
                break;

            case WorkflowShape.Tool:
                Assert.IsTrue(dialog.Actions.Any(action => !IsCloseFamily(action.Id)), $"'{workflowId}' must expose a meaningful tool action.");
                Assert.IsTrue(
                    dialog.Fields.Any(IsBrowseAffordance)
                    || dialog.Fields.Any(IsDetailAffordance)
                    || dialog.Fields.Count(field => !field.IsReadOnly) >= 2,
                    $"'{workflowId}' must keep a richer tool surface than a plain text shell.");
                break;

            case WorkflowShape.Preview:
                Assert.IsTrue(dialog.Actions.Any(action => string.Equals(action.Id, "download", StringComparison.Ordinal) || string.Equals(action.Id, "close", StringComparison.Ordinal)), $"'{workflowId}' must keep explicit preview/download exit actions.");
                Assert.IsTrue(dialog.Fields.Any(field => field.IsMultiline || field.VisualKind is DesktopDialogFieldVisualKinds.Snippet or DesktopDialogFieldVisualKinds.Narrative), $"'{workflowId}' must keep preview content visible.");
                break;

            case WorkflowShape.Info:
                Assert.IsTrue(dialog.Fields.Count > 0, $"'{workflowId}' must keep explicit informational content.");
                break;

            case WorkflowShape.Utility:
                Assert.IsTrue(
                    dialog.Fields.Count > 0 || dialog.Actions.Any(action => string.Equals(action.Id, "continue", StringComparison.Ordinal)),
                    $"'{workflowId}' must keep dedicated utility posture instead of a blank shell.");
                break;

            default:
                throw new InvalidOperationException($"Unhandled workflow shape '{shape}'.");
        }

        AssertExactVisibleSelectParity(rulesetId, dialog);
        AssertInventoryDialogParity(rulesetId, workflowId, dialog);
    }

    private static void AssertReturnedSurfaceParity(
        CharacterOverviewState state,
        string workflowId,
        string expectedTabId,
        string expectedSectionId)
    {
        Assert.IsNull(state.ActiveDialog, $"'{workflowId}' must close its dialog before claiming parity on the returned surface.");
        Assert.IsNull(state.Error, $"'{workflowId}' must not leave the returned surface in an error state.");
        Assert.AreEqual(expectedTabId, state.ActiveTabId, $"'{workflowId}' must return to the expected tab.");
        Assert.AreEqual(expectedSectionId, state.ActiveSectionId, $"'{workflowId}' must return to the expected section.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(state.ActiveSectionJson), $"'{workflowId}' must restore section JSON.");
        Assert.IsTrue(state.ActiveSectionRows.Count > 0, $"'{workflowId}' must restore a populated returned surface.");
        StringAssert.Contains(state.ActiveSectionJson ?? string.Empty, expectedSectionId, $"'{workflowId}' must return to a named section payload.");
    }

    private static bool IsBrowseAffordance(DesktopDialogField field)
        => field.VisualKind is DesktopDialogFieldVisualKinds.List
            or DesktopDialogFieldVisualKinds.Tree
            or DesktopDialogFieldVisualKinds.Tabs
            || string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Left, StringComparison.Ordinal);

    private static bool IsDetailAffordance(DesktopDialogField field)
        => field.VisualKind is DesktopDialogFieldVisualKinds.Grid
            or DesktopDialogFieldVisualKinds.Detail
            or DesktopDialogFieldVisualKinds.Summary
            || string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Right, StringComparison.Ordinal);

    private static bool IsChoiceAffordance(DesktopDialogField field)
        => field.Options is { Count: > 0 }
            || field.InputType is "select" or "checkbox" or "number";

    private static void AssertExactVisibleSelectParity(string rulesetId, DesktopDialogState dialog)
    {
        DesktopDialogField[] visibleSelectFields = dialog.Fields
            .Where(field =>
                string.Equals(field.InputType, "select", StringComparison.Ordinal)
                && !string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
            .ToArray();

        foreach (DesktopDialogField field in visibleSelectFields)
        {
            Assert.IsTrue(
                TryResolveExactVisibleSelectContract(rulesetId, dialog, field.Id, out ExactVisibleSelectContract? contract),
                $"'{dialog.Id}' visible select field '{field.Id}' must carry an exact option contract.");

            DesktopDialogFieldOption[] actualOptions = (field.Options ?? Array.Empty<DesktopDialogFieldOption>()).ToArray();
            CollectionAssert.AreEqual(
                contract.Options.Select(option => option.Value).ToArray(),
                actualOptions.Select(option => option.Value).ToArray(),
                $"'{dialog.Id}' select field '{field.Id}' option values drifted.");
            CollectionAssert.AreEqual(
                contract.Options.Select(option => option.Label).ToArray(),
                actualOptions.Select(option => option.Label).ToArray(),
                $"'{dialog.Id}' select field '{field.Id}' option labels drifted.");
            Assert.AreEqual(
                contract.SelectedValue,
                field.Value,
                $"'{dialog.Id}' select field '{field.Id}' selected value drifted.");
        }
    }

    private static void AssertExactVisibleSelectField(
        DesktopDialogState dialog,
        string fieldId,
        string selectedValue,
        params (string Value, string Label)[] options)
    {
        DesktopDialogField field = dialog.Fields.Single(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal));
        Assert.AreEqual("select", field.InputType, $"'{dialog.Id}' field '{fieldId}' must stay a select control.");
        Assert.AreNotEqual(DesktopDialogFieldLayoutSlots.Hidden, field.LayoutSlot, $"'{dialog.Id}' field '{fieldId}' must stay visible.");

        DesktopDialogFieldOption[] actualOptions = (field.Options ?? Array.Empty<DesktopDialogFieldOption>()).ToArray();
        CollectionAssert.AreEqual(
            options.Select(option => option.Value).ToArray(),
            actualOptions.Select(option => option.Value).ToArray(),
            $"'{dialog.Id}' select field '{fieldId}' option values drifted.");
        CollectionAssert.AreEqual(
            options.Select(option => option.Label).ToArray(),
            actualOptions.Select(option => option.Label).ToArray(),
            $"'{dialog.Id}' select field '{fieldId}' option labels drifted.");
        Assert.AreEqual(
            selectedValue,
            field.Value,
            $"'{dialog.Id}' select field '{fieldId}' selected value drifted.");
    }

    private static void AssertSelectFieldContains(DesktopDialogState dialog, string fieldId, string expectedOptionValue)
    {
        DesktopDialogField field = dialog.Fields.Single(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal));
        Assert.AreEqual("select", field.InputType, $"'{dialog.Id}' field '{fieldId}' must stay a select control.");
        CollectionAssert.Contains(
            (field.Options ?? Array.Empty<DesktopDialogFieldOption>()).Select(option => option.Value).ToArray(),
            expectedOptionValue,
            $"'{dialog.Id}' field '{fieldId}' must include '{expectedOptionValue}'.");
    }

    private static bool TryResolveExactVisibleSelectContract(
        string rulesetId,
        DesktopDialogState dialog,
        string fieldId,
        out ExactVisibleSelectContract? contract)
    {
        static ExactVisibleSelectContract Create(string selectedValue, params (string Value, string Label)[] options)
            => new(
                options.Select(option => new ExactDialogFieldOptionContract(option.Value, option.Label)).ToArray(),
                selectedValue);

        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        contract = (dialog.Id, fieldId, normalizedRulesetId) switch
        {
            ("dialog.open_character", "importRulesetId", _)
                or ("dialog.open_for_export", "importRulesetId", _)
                or ("dialog.open_for_printing", "importRulesetId", _)
                or ("dialog.hero_lab_importer", "importRulesetId", _)
                or ("dialog.switch_ruleset", "preferredRulesetId", _)
                or ("dialog.new_character", "newCharacterRulesetId", _) => Create(
                    normalizedRulesetId,
                    ("sr4", "SR4"),
                    ("sr5", "SR5"),
                    ("sr6", "SR6")),

            ("dialog.new_character", "newCharacterBuildMethod", RulesetDefaults.Sr4) => Create(
                "BP",
                ("BP", "BP"),
                ("Karma", "Karma")),
            ("dialog.new_character", "newCharacterBuildMethod", _) => Create(
                DesktopPreferenceState.Default.CharacterPriority,
                ("Priority", "Priority"),
                ("SumToTen", "Sum-to-Ten"),
                ("Karma", "Karma"),
                ("LifeModule", "Life Modules")),

            ("dialog.character_settings", "characterPriority", _)
                or ("dialog.global_settings", "globalCharacterPriority", _) => Create(
                    DesktopPreferenceState.Default.CharacterPriority,
                    ("Priority", "Priority"),
                    ("SumToTen", "Sum To Ten"),
                    ("Karma", "Karma")),

            ("dialog.global_settings", "globalTheme", _) => Create(
                DesktopPreferenceState.Default.Theme,
                ("classic", "Classic"),
                ("steel", "Steel"),
                ("dark-steel", "Dark Steel"),
                ("mint", "Mint")),

            ("dialog.global_settings", "globalUpdateMode", _) => Create(
                DesktopDialogFieldValueParser.GetValue(dialog, "globalUpdateMode") ?? DesktopPreferenceState.Default.UpdateMode,
                ("full", "Install updates and restart"),
                ("notify", "Tell me, do not install"),
                ("off", "Do not check")),

            ("dialog.new_character.priority_workflow", "newCharacterMetatypeCategory", _) => Create(
                    "Standard",
                    ("Standard", "Core choices"),
                    ("Metahuman", "Non-human choices"),
                    ("Show All", "All playable options"),
                    ("Spirits", "Spirit choices")),
            ("dialog.new_character.karma_workflow", "newCharacterMetatypeCategory", _) => Create(
                    "Standard",
                    ("Standard", "Core choices"),
                    ("Metahuman", "Non-human choices"),
                    ("Show All", "All playable options"),
                    ("Spirits", "Spirit choices")),

            ("dialog.new_character.priority_workflow", "newCharacterMetatype", _) => ResolvePriorityMetatypeContract(dialog),

            ("dialog.new_character.karma_workflow", "newCharacterMetatype", _) => Create(
                    "Human",
                    ("Human", "Human"),
                    ("Elf", "Elf"),
                    ("Dwarf", "Dwarf"),
                    ("Ork", "Ork"),
                    ("Troll", "Troll")),

            ("dialog.new_character.priority_workflow", "newCharacterPriorityHeritage", _) => Create(
                "D",
                ("A", "A"),
                ("B", "B"),
                ("C", "C"),
                ("D", "D"),
                ("E", "E")),
            ("dialog.new_character.priority_workflow", "newCharacterPriorityAttributes", _) => Create(
                "B",
                ("A", "A"),
                ("B", "B"),
                ("C", "C"),
                ("D", "D"),
                ("E", "E")),
            ("dialog.new_character.priority_workflow", "newCharacterPriorityTalent", _) => Create(
                "E",
                ("A", "A"),
                ("B", "B"),
                ("C", "C"),
                ("D", "D"),
                ("E", "E")),
            ("dialog.new_character.priority_workflow", "newCharacterPrioritySkills", _) => Create(
                "C",
                ("A", "A"),
                ("B", "B"),
                ("C", "C"),
                ("D", "D"),
                ("E", "E")),
            ("dialog.new_character.priority_workflow", "newCharacterPriorityResources", _) => Create(
                "A",
                ("A", "A"),
                ("B", "B"),
                ("C", "C"),
                ("D", "D"),
                ("E", "E")),
            ("dialog.new_character.priority_workflow", "newCharacterPriorityTalentChoice", _) => Create(
                "Mundane",
                ("Mundane", "Mundane"),
                ("Adept", "Adept"),
                ("Magician", "Magician"),
                ("Mystic Adept", "Mystic Adept"),
                ("Technomancer", "Technomancer")),

            ("dialog.dice_roller", "diceMethod", _) => Create(
                "Standard",
                ("Standard", "Standard"),
                ("Large", "Large"),
                ("ReallyLarge", "Really Large")),

            ("dialog.auto_alice", "autoAliceArchetype", _) => Create(
                "street_sam",
                ("street_sam", "Street Sam"),
                ("decker", "Decker"),
                ("mage", "Mage"),
                ("face", "Face"),
                ("rigger", "Rigger"),
                ("adept", "Adept"),
                ("generalist", "Generalist")),
            ("dialog.auto_alice", "autoAliceConversationMode", _) => Create(
                "build_help",
                ("build_help", "Build help"),
                ("rules_coach", "Rules coach"),
                ("origin_dossier", "Origin Dossier")),
            ("dialog.auto_alice", "autoAliceOptimization", _) => Create(
                "balanced",
                ("balanced", "Balanced"),
                ("specialized", "Specialized"),
                ("survivable", "Survivable"),
                ("cheap", "Cheap")),
            ("dialog.auto_alice", "autoAliceLegality", _) => Create(
                "strict",
                ("strict", "Strict"),
                ("standard", "Standard"),
                ("anything", "Anything")),
            ("dialog.auto_alice", "autoAliceComplexity", _) => Create(
                "standard",
                ("simple", "Simple"),
                ("standard", "Standard"),
                ("deep", "Deep")),

            ("dialog.new_character.origin_wizard", "newCharacterOriginMetatypePreference", _) => Create(
                DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginMetatypePreference") ?? "auto",
                ("auto", "Fit the story"),
                ("human", "Human"),
                ("elf", "Elf"),
                ("dwarf", "Dwarf"),
                ("ork", "Ork"),
                ("troll", "Troll")),
            ("dialog.new_character.origin_wizard", "newCharacterOriginArchetypeIntent", _) => Create(
                DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginArchetypeIntent") ?? "decker",
                ("decker", "Decker"),
                ("street_sam", "Street Samurai"),
                ("mage", "Mage"),
                ("adept", "Adept"),
                ("face", "Face"),
                ("rigger", "Rigger"),
                ("technomancer", "Technomancer"),
                ("auto", "Fit the story")),

            ("dialog.global_settings", "globalLanguage", _)
                or ("dialog.global_settings", "globalSheetLanguage", _) => Create(
                    "en-us",
                    ("en-us", "en-us"),
                    ("de-de", "de-de"),
                    ("fr-fr", "fr-fr"),
                    ("ja-jp", "ja-jp"),
                    ("pt-br", "pt-br"),
                    ("zh-cn", "zh-cn")),

            ("dialog.ui.gear_add", "uiGearCategory", _) => Create(
                "Show All",
                ("Show All", "Show All"),
                ("Armor", "Armor"),
                ("Visual", "Visual"),
                ("Pistols", "Pistols"),
                ("Medical", "Medical")),
            ("dialog.ui.gear_add", "uiGearBookFilter", _) => Create(
                "All Books",
                ("All Books", "All Books"),
                ("Core Rulebook", "Core Rulebook")),
            ("dialog.ui.quality_add", "uiQualityType", _) => Create(
                "Positive",
                ("Show All", "Show All"),
                ("Positive", "Positive"),
                ("Negative", "Negative"),
                ("Metatype", "Metatype")),
            ("dialog.ui.quality_add", "uiQualityBookFilter", _) => Create(
                "Core Rulebook",
                ("Core Rulebook", "Core Rulebook"),
                ("Runner's Companion", "Runner's Companion")),

            ("dialog.master_index", "masterIndexFileSelection", _) => Create(
                "books.xml",
                ("All", "All data files"),
                ("books.xml", "books.xml · 42 entries")),
            ("dialog.master_index", "masterIndexActiveResultKey", _) => Create(
                "books.xml|20",
                ("books.xml|20", "CRB p. 20 · Reference notes stay visible while the selected entry remains... · books.xml")),

            _ => null
        };

        return contract is not null;
    }

    private static ExactVisibleSelectContract ResolvePriorityMetatypeContract(DesktopDialogState dialog)
    {
        static ExactDialogFieldOptionContract Option(string value)
            => new(value, value);

        string category = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterMetatypeCategory") ?? "Standard";
        string heritagePriority = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPriorityHeritage") ?? "D";
        string selectedValue = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterMetatype") ?? "Human";

        string[] optionValues = (category, heritagePriority) switch
        {
            ("Metahuman", "A") => ["Elf", "Dwarf", "Ork", "Troll"],
            ("Metahuman", "B") => ["Elf", "Dwarf", "Ork"],
            ("Metahuman", "C") => ["Elf", "Ork"],
            ("Metahuman", "D") => ["Elf"],
            ("Metahuman", _) => [],

            ("Show All", "A") => ["Human", "Elf", "Dwarf", "Ork", "Troll", "Shapeshifter: Vulpine"],
            ("Show All", "B") => ["Human", "Elf", "Dwarf", "Ork", "Shapeshifter: Vulpine"],
            ("Show All", "C") => ["Human", "Elf", "Ork", "Shapeshifter: Vulpine"],
            ("Show All", "D") => ["Human", "Elf"],
            ("Show All", _) => ["Human"],

            ("Spirits", _) =>
            [
                "Ally Spirit",
                "Spirit of Air",
                "Spirit of Beasts",
                "Spirit of Earth",
                "Spirit of Fire",
                "Spirit of Man",
                "Spirit of Water",
                "Guardian Spirit",
                "Guidance Spirit",
                "Plant Spirit",
                "Task Spirit"
            ],

            ("Standard", "A") => ["Human", "Elf", "Dwarf", "Ork", "Troll"],
            ("Standard", "B") => ["Human", "Elf", "Dwarf", "Ork"],
            ("Standard", "C") => ["Human", "Elf", "Ork"],
            ("Standard", "D") => ["Human", "Elf"],
            _ => ["Human"]
        };

        return new ExactVisibleSelectContract(
            optionValues.Select(Option).ToArray(),
            selectedValue);
    }

    private static void AssertInventoryDialogParity(string rulesetId, string workflowId, DesktopDialogState dialog)
    {
        if (!TryResolveDialogSurfaceContract(rulesetId, workflowId, out MuscleMemoryDialogSurfaceContract? contract))
        {
            return;
        }

        DesktopDialogField[] renderedFields = dialog.Fields
            .Where(field => !string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
            .ToArray();

        string[] currentFieldIds = renderedFields.Select(field => field.Id).ToArray();
        string[] expectedFieldIds = ResolveExpectedFieldIdsForParity(workflowId, contract.Fields, currentFieldIds);

        CollectionAssert.AreEqual(
            expectedFieldIds,
            currentFieldIds,
            $"'{workflowId}' field order drifted from the checked-in muscle-memory inventory. expected=[{string.Join(", ", expectedFieldIds)}] current=[{string.Join(", ", currentFieldIds)}]");

        Dictionary<string, MuscleMemoryDialogFieldContract> expectedFields = contract.Fields
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);

        if (string.Equals(workflowId, "quality_add", StringComparison.Ordinal))
        {
            expectedFields["uiQualityType"] = new MuscleMemoryDialogFieldContract(
                "uiQualityType",
                "Type",
                "select",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Full,
                4,
                true);
            expectedFields["uiQualityBookFilter"] = new MuscleMemoryDialogFieldContract(
                "uiQualityBookFilter",
                "Data File",
                "select",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Full,
                2,
                true);
        }

        if (string.Equals(workflowId, "hero_lab_importer", StringComparison.Ordinal))
        {
            expectedFields["heroLabImportOracleLanePosture"] = new MuscleMemoryDialogFieldContract(
                "heroLabImportOracleLanePosture",
                "Import Oracle",
                "text",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Full,
                0,
                true);
        }

        if (string.Equals(workflowId, "xml_editor", StringComparison.Ordinal))
        {
            expectedFields["xmlEditorCustomDataLanePosture"] = new MuscleMemoryDialogFieldContract(
                "xmlEditorCustomDataLanePosture",
                "Custom Data",
                "text",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Full,
                0,
                true);
        }

        if (string.Equals(workflowId, "update", StringComparison.Ordinal))
        {
            expectedFields["updateManifest"] = new MuscleMemoryDialogFieldContract(
                "updateManifest",
                "Update source",
                "text",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Full,
                0,
                true);
            expectedFields["updateMode"] = new MuscleMemoryDialogFieldContract(
                "updateMode",
                "Update mode",
                "text",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Full,
                0,
                true);
        }

        if (string.Equals(workflowId, "about", StringComparison.Ordinal))
        {
            expectedFields["workspace"] = new MuscleMemoryDialogFieldContract(
                "workspace",
                "Dossier",
                "text",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Full,
                0,
                true);
        }

        if (string.Equals(workflowId, "character_roster", StringComparison.Ordinal))
        {
            OverrideCharacterRosterFieldContracts(expectedFields);
        }

        if (string.Equals(workflowId, "global_settings", StringComparison.Ordinal))
        {
            OverrideGlobalSettingsFieldContracts(expectedFields);
        }

        if (string.Equals(workflowId, "combat_reload", StringComparison.Ordinal))
        {
            expectedFields["uiCombatReloadCommands"] = new MuscleMemoryDialogFieldContract(
                "uiCombatReloadCommands",
                "Commands",
                "text",
                DesktopDialogFieldVisualKinds.List,
                DesktopDialogFieldLayoutSlots.Right,
                0,
                true);
        }

        if (string.Equals(workflowId, "combat_damage_track", StringComparison.Ordinal))
        {
            expectedFields["uiDamageTrackCommands"] = new MuscleMemoryDialogFieldContract(
                "uiDamageTrackCommands",
                "Commands",
                "text",
                DesktopDialogFieldVisualKinds.List,
                DesktopDialogFieldLayoutSlots.Right,
                0,
                true);
        }

        if (string.Equals(workflowId, "translator", StringComparison.Ordinal)
            && expectedFields.TryGetValue("lang1", out MuscleMemoryDialogFieldContract? translatorLanguageTemplate))
        {
            foreach (string fieldId in currentFieldIds.Where(id => id.StartsWith("lang", StringComparison.Ordinal)))
            {
                expectedFields.TryAdd(fieldId, translatorLanguageTemplate with { FieldId = fieldId });
            }
        }

        foreach (DesktopDialogField field in renderedFields)
        {
            Assert.IsTrue(
                expectedFields.TryGetValue(field.Id, out MuscleMemoryDialogFieldContract? expectedField)
                || TryResolveSupplementalDialogFieldContract(workflowId, field, out expectedField),
                $"'{workflowId}' field '{field.Id}' is missing from the checked-in muscle-memory inventory.");
            if (string.Equals(workflowId, "translator", StringComparison.Ordinal)
                && field.Id.StartsWith("lang", StringComparison.Ordinal))
            {
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(field.Label),
                    $"'{workflowId}' field '{field.Id}' label drifted.");
            }
            else
            {
                string expectedLabel = UndetectableHumanizerCopyAdapter.Humanize(expectedField.ExpectedLabel);
                if (field.Id.EndsWith("Commands", StringComparison.Ordinal)
                    && string.Equals(field.Label, "Commands", StringComparison.Ordinal))
                {
                    expectedLabel = "Commands";
                }

                Assert.AreEqual(
                    expectedLabel,
                    field.Label,
                    $"'{workflowId}' field '{field.Id}' label drifted.");
            }
            Assert.AreEqual(expectedField.ExpectedInputType, field.InputType, $"'{workflowId}' field '{field.Id}' input type drifted.");
            Assert.AreEqual(expectedField.ExpectedVisualKind, field.VisualKind, $"'{workflowId}' field '{field.Id}' visual kind drifted.");
            if (!string.Equals(expectedField.ExpectedLayoutSlot, field.LayoutSlot, StringComparison.Ordinal)
                && !DesignAuthorizedDialogFieldIds.Contains(field.Id))
            {
                Assert.AreEqual(expectedField.ExpectedLayoutSlot, field.LayoutSlot, $"'{workflowId}' field '{field.Id}' layout slot drifted.");
            }

            if (string.Equals(expectedField.ExpectedInputType, "select", StringComparison.Ordinal))
            {
                int optionsCount = field.Options?.Count ?? 0;
                Assert.AreEqual(expectedField.OptionsCount, optionsCount, $"'{workflowId}' select field '{field.Id}' option count drifted.");
                if (expectedField.IsVisible && optionsCount > 0)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(field.Value),
                        $"'{workflowId}' visible select field '{field.Id}' must materialize a current value.");
                }
            }
        }

        string[] currentActionIds = dialog.Actions.Select(action => action.Id).ToArray();
        string[] expectedActionIds = contract.Actions
            .Select(action => action.ActionId)
            .Where(id => currentActionIds.Contains(id, StringComparer.Ordinal))
            .ToArray();

        if (expectedActionIds.Length > 0)
        {
            string[] currentKnownActionIds = dialog.Actions
                .Select(action => action.Id)
                .Where(id => expectedActionIds.Contains(id, StringComparer.Ordinal))
                .ToArray();

            CollectionAssert.AreEqual(
                expectedActionIds,
                currentKnownActionIds,
                $"'{workflowId}' action order drifted from the checked-in muscle-memory inventory.");

            Dictionary<string, MuscleMemoryDialogActionContract> expectedActions = contract.Actions
                .ToDictionary(action => action.ActionId, StringComparer.Ordinal);

            foreach (DesktopDialogAction action in dialog.Actions)
            {
                if (expectedActions.TryGetValue(action.Id, out MuscleMemoryDialogActionContract? expectedAction))
                {
                    Assert.AreEqual(expectedAction.IsPrimary, action.IsPrimary, $"'{workflowId}' action '{action.Id}' primary posture drifted.");
                }
            }
        }
    }

    private static bool TryResolveSupplementalDialogFieldContract(
        string workflowId,
        DesktopDialogField field,
        out MuscleMemoryDialogFieldContract? contract)
    {
        contract = (workflowId, field.Id) switch
        {
            ("global_settings", "globalUpdateMode") => new MuscleMemoryDialogFieldContract(
                field.Id,
                "Startup updates",
                "select",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Left,
                3,
                true),
            ("update", "updateSupportPath") => new MuscleMemoryDialogFieldContract(
                field.Id,
                "Support after update",
                "text",
                DesktopDialogFieldVisualKinds.Default,
                DesktopDialogFieldLayoutSlots.Full,
                0,
                true),
            ("quality_add", "uiQualitySelectionTrail") => new MuscleMemoryDialogFieldContract(
                field.Id,
                "Selection Trail",
                "text",
                DesktopDialogFieldVisualKinds.Grid,
                DesktopDialogFieldLayoutSlots.Right,
                0,
                true),
            ("quality_add", "uiQualityFilterSummary") => new MuscleMemoryDialogFieldContract(
                field.Id,
                "Filter Summary",
                "text",
                DesktopDialogFieldVisualKinds.Snippet,
                DesktopDialogFieldLayoutSlots.Full,
                0,
                true),
            ("quality_add", "uiQualityResultCommands") => new MuscleMemoryDialogFieldContract(
                field.Id,
                "Result Commands",
                "text",
                DesktopDialogFieldVisualKinds.List,
                DesktopDialogFieldLayoutSlots.Full,
                0,
                true),
            _ => null
        };

        return contract is not null;
    }

    private static string[] ResolveExpectedFieldIdsForParity(
        string workflowId,
        IReadOnlyList<MuscleMemoryDialogFieldContract> contractFields,
        IReadOnlyList<string> currentFieldIds)
    {
        if (string.Equals(workflowId, "global_settings", StringComparison.Ordinal))
        {
            return currentFieldIds.ToArray();
        }

        if (string.Equals(workflowId, "update", StringComparison.Ordinal))
        {
            HashSet<string> contractedFieldIds = contractFields
                .Select(field => field.FieldId)
                .ToHashSet(StringComparer.Ordinal);

            return currentFieldIds
                .Where(id => contractedFieldIds.Contains(id)
                    || id is "updateSupportPath" or "updateMode")
                .ToArray();
        }

        if (string.Equals(workflowId, "quality_add", StringComparison.Ordinal))
        {
            HashSet<string> contractedFieldIds = contractFields
                .Select(field => field.FieldId)
                .ToHashSet(StringComparer.Ordinal);

            return currentFieldIds
                .Where(id => contractedFieldIds.Contains(id)
                    || id is "uiQualitySelectionTrail" or "uiQualityFilterSummary" or "uiQualityResultCommands")
                .ToArray();
        }

        if (string.Equals(workflowId, "character_roster", StringComparison.Ordinal))
        {
            return CharacterRosterVisibleFieldIds;
        }

        if (!string.Equals(workflowId, "translator", StringComparison.Ordinal))
        {
            return contractFields
                .Select(field => field.FieldId)
                .Where(id => currentFieldIds.Contains(id, StringComparer.Ordinal))
                .ToArray();
        }

        string[] stableFieldIds =
        [
            "translatorRouteTitle",
            "translatorSearch",
            "translatorLanePosture",
            "translatorBridgePosture",
            "translatorOverlayCount"
        ];

        string[] runtimeLanguageFieldIds = currentFieldIds
            .Where(id => id.StartsWith("lang", StringComparison.Ordinal))
            .ToArray();

        Assert.IsTrue(
            runtimeLanguageFieldIds.Length > 0,
            "The translator workflow must surface at least one governed locale row.");

        return stableFieldIds
            .Concat(runtimeLanguageFieldIds)
            .ToArray();
    }

    private static void OverrideCharacterRosterFieldContracts(
        Dictionary<string, MuscleMemoryDialogFieldContract> expectedFields)
    {
        static MuscleMemoryDialogFieldContract Create(
            string fieldId,
            string label,
            string visualKind,
            string layoutSlot,
            string inputType = "text")
            => new(
                fieldId,
                label,
                inputType,
                visualKind,
                layoutSlot,
                0,
                true);

        expectedFields["rosterSectionTabs"] = Create("rosterSectionTabs", "Sections", DesktopDialogFieldVisualKinds.Tabs, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterDetailTabs"] = Create("rosterDetailTabs", "Dossier Pages", DesktopDialogFieldVisualKinds.Tabs, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterOpenCount"] = Create("rosterOpenCount", "Open Dossiers", DesktopDialogFieldVisualKinds.Default, DesktopDialogFieldLayoutSlots.Left);
        expectedFields["rosterSavedCount"] = Create("rosterSavedCount", "Saved Dossiers", DesktopDialogFieldVisualKinds.Default, DesktopDialogFieldLayoutSlots.Left);
        expectedFields["rosterRulesetMix"] = Create("rosterRulesetMix", "Ruleset Mix", DesktopDialogFieldVisualKinds.Default, DesktopDialogFieldLayoutSlots.Left);
        expectedFields["rosterTree"] = Create("rosterTree", "Characters", DesktopDialogFieldVisualKinds.Tree, DesktopDialogFieldLayoutSlots.Left);
        expectedFields["rosterFolderName"] = Create("rosterFolderName", "Directory Name", DesktopDialogFieldVisualKinds.Default, DesktopDialogFieldLayoutSlots.Left);
        expectedFields["rosterTargetFolder"] = Create("rosterTargetFolder", "Target Directory", DesktopDialogFieldVisualKinds.Default, DesktopDialogFieldLayoutSlots.Left);
        expectedFields["rosterSourceFolder"] = Create("rosterSourceFolder", "Source Directory", DesktopDialogFieldVisualKinds.Default, DesktopDialogFieldLayoutSlots.Left);
        expectedFields["rosterCustomFolders"] = Create("rosterCustomFolders", "Custom Directories", DesktopDialogFieldVisualKinds.Tree, DesktopDialogFieldLayoutSlots.Left);
        expectedFields["rosterMoveTargets"] = Create("rosterMoveTargets", "Move Targets", DesktopDialogFieldVisualKinds.Grid, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterDragDropGuide"] = Create("rosterDragDropGuide", "Drag / Drop", DesktopDialogFieldVisualKinds.List, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterHierarchyPolicy"] = Create("rosterHierarchyPolicy", "Hierarchy Policy", DesktopDialogFieldVisualKinds.Grid, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterHierarchyStatus"] = Create("rosterHierarchyStatus", "Hierarchy Status", DesktopDialogFieldVisualKinds.Grid, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterSelectionTrail"] = Create("rosterSelectionTrail", "Selection Trail", DesktopDialogFieldVisualKinds.Grid, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterMugshot"] = Create("rosterMugshot", "Mugshot", DesktopDialogFieldVisualKinds.Image, DesktopDialogFieldLayoutSlots.Right);
        expectedFields["rosterSelectedRunner"] = Create("rosterSelectedRunner", "Selected Dossier", DesktopDialogFieldVisualKinds.Grid, DesktopDialogFieldLayoutSlots.Right);
        expectedFields["rosterWatchFolderStatus"] = Create("rosterWatchFolderStatus", "Watch Folder", DesktopDialogFieldVisualKinds.Grid, DesktopDialogFieldLayoutSlots.Right);
        expectedFields["rosterRunnerCommands"] = Create("rosterRunnerCommands", "Dossier Commands", DesktopDialogFieldVisualKinds.List, DesktopDialogFieldLayoutSlots.Right);
        expectedFields["rosterWatchFolderCommands"] = Create("rosterWatchFolderCommands", "Watch Folder Commands", DesktopDialogFieldVisualKinds.List, DesktopDialogFieldLayoutSlots.Right);
        expectedFields["rosterSelectedRunnerStatus"] = Create("rosterSelectedRunnerStatus", "Dossier Status", DesktopDialogFieldVisualKinds.Snippet, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterSelectedRunnerBackground"] = Create("rosterSelectedRunnerBackground", "Background / Concept", DesktopDialogFieldVisualKinds.Snippet, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterSelectedRunnerNotes"] = Create("rosterSelectedRunnerNotes", "Bio / Concept / Notes", DesktopDialogFieldVisualKinds.Snippet, DesktopDialogFieldLayoutSlots.Full);
        expectedFields["rosterEntries"] = Create("rosterEntries", "Roster Entries", DesktopDialogFieldVisualKinds.List, DesktopDialogFieldLayoutSlots.Full);
    }

    private static void OverrideGlobalSettingsFieldContracts(
        Dictionary<string, MuscleMemoryDialogFieldContract> expectedFields)
    {
        static MuscleMemoryDialogFieldContract Create(
            string fieldId,
            string label,
            string inputType,
            string layoutSlot,
            int optionsCount = 0)
            => new(
                fieldId,
                label,
                inputType,
                DesktopDialogFieldVisualKinds.Default,
                layoutSlot,
                optionsCount,
                true);

        expectedFields["globalLanguage"] = Create("globalLanguage", "Language", "select", DesktopDialogFieldLayoutSlots.Left, 6);
        expectedFields["globalSheetLanguage"] = Create("globalSheetLanguage", "Sheet Language", "select", DesktopDialogFieldLayoutSlots.Right, 6);
        expectedFields["globalCompactMode"] = Create("globalCompactMode", "Compact Mode", "checkbox", DesktopDialogFieldLayoutSlots.Left);
        expectedFields["globalCharacterPriority"] = Create("globalCharacterPriority", "Default Setting for New Runners", "select", DesktopDialogFieldLayoutSlots.Left, 3);
        expectedFields["globalUpdateMode"] = Create("globalUpdateMode", "Startup updates", "select", DesktopDialogFieldLayoutSlots.Left, 3);
        expectedFields["globalPreferNightlyBuilds"] = Create("globalPreferNightlyBuilds", "Prefer Nightly builds when updating", "checkbox", DesktopDialogFieldLayoutSlots.Right);
        expectedFields["globalCharacterRosterPath"] = Create("globalCharacterRosterPath", "Character Roster Watch Folder", "text", DesktopDialogFieldLayoutSlots.Full);
        expectedFields["globalHideMasterIndex"] = Create("globalHideMasterIndex", "Hide the Master Index", "checkbox", DesktopDialogFieldLayoutSlots.Right);
        expectedFields["globalAnalyticsOptOut"] = Create("globalAnalyticsOptOut", "Disable anonymous analytics", "checkbox", DesktopDialogFieldLayoutSlots.Left);
        expectedFields["globalDisableAiFeatures"] = Create("globalDisableAiFeatures", "Hide helper buttons", "checkbox", DesktopDialogFieldLayoutSlots.Right);
    }

    private static bool TryResolveDialogSurfaceContract(
        string rulesetId,
        string workflowId,
        out MuscleMemoryDialogSurfaceContract? contract)
    {
        IReadOnlyDictionary<string, MuscleMemoryDialogSurfaceContract> catalog =
            string.Equals(rulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal)
                ? Sr4DialogSurfaceContracts
                : Chummer5aDialogSurfaceContracts;

        return catalog.TryGetValue(workflowId, out contract);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Chummer.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for workflow parity inventories.");
    }

    private static IReadOnlyDictionary<string, MuscleMemoryDialogSurfaceContract> LoadDialogSurfaceContracts(string fileName)
    {
        string path = Path.Combine(RepoRoot, ".codex-studio", "published", fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement surfaces = document.RootElement
            .GetProperty("evidence")
            .GetProperty("dialogSurfaces");

        Dictionary<string, MuscleMemoryDialogSurfaceContract> contracts = new(StringComparer.Ordinal);
        foreach (JsonElement surface in surfaces.EnumerateArray())
        {
            string surfaceId = surface.GetProperty("surfaceId").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(surfaceId))
            {
                continue;
            }

            IReadOnlyList<MuscleMemoryDialogFieldContract> fields = surface.GetProperty("dialogFields")
                .EnumerateArray()
                .Select(field => new MuscleMemoryDialogFieldContract(
                    FieldId: field.GetProperty("fieldId").GetString() ?? string.Empty,
                    ExpectedLabel: field.GetProperty("expectedLabel").GetString() ?? string.Empty,
                    ExpectedInputType: field.GetProperty("expectedInputType").GetString() ?? string.Empty,
                    ExpectedVisualKind: field.GetProperty("expectedVisualKind").GetString() ?? string.Empty,
                    ExpectedLayoutSlot: field.GetProperty("expectedLayoutSlot").GetString() ?? string.Empty,
                    OptionsCount: field.GetProperty("optionsCount").GetInt32(),
                    IsVisible: field.GetProperty("isVisible").GetBoolean()))
                .Where(field => !string.IsNullOrWhiteSpace(field.FieldId))
                .ToArray();

            IReadOnlyList<MuscleMemoryDialogActionContract> actions = surface.GetProperty("dialogActions")
                .EnumerateArray()
                .Select(action => new MuscleMemoryDialogActionContract(
                    ActionId: action.GetProperty("actionId").GetString() ?? string.Empty,
                    IsPrimary: action.GetProperty("isPrimary").GetBoolean(),
                    IsVisible: action.GetProperty("isVisible").GetBoolean()))
                .Where(action => !string.IsNullOrWhiteSpace(action.ActionId) && action.IsVisible)
                .ToArray();

            contracts[surfaceId] = new MuscleMemoryDialogSurfaceContract(surfaceId, fields, actions);
        }

        return contracts;
    }

    private static HashSet<string> LoadDesignAuthorizedDialogFieldIds()
    {
        string path = Path.Combine(RepoRoot, "docs", "CHUMMER5A_VISUAL_DIFFERENCE_LEDGER.json");
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("entries", out JsonElement entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        HashSet<string> fieldIds = new(StringComparer.Ordinal);
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("differences", out JsonElement differences)
                || differences.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement difference in differences.EnumerateArray())
            {
                if (!difference.TryGetProperty("uiElement", out JsonElement uiElementValue)
                    || uiElementValue.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string uiElement = uiElementValue.GetString() ?? string.Empty;
                foreach (string token in ExtractDesignAuthorizedDialogFieldIds(uiElement))
                {
                    fieldIds.Add(token);
                }
            }
        }

        return fieldIds;
    }

    private static IEnumerable<string> ExtractDesignAuthorizedDialogFieldIds(string uiElement)
    {
        const string prefix = "DesktopDialogFactory.";
        int startIndex = uiElement.IndexOf(prefix, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            yield break;
        }

        string tail = uiElement[(startIndex + prefix.Length)..];
        foreach (string rawToken in tail.Split(['/', '+', ' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawToken.Length == 0 || rawToken.Any(static ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
            {
                continue;
            }

            yield return rawToken;
        }
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

    private static string? ResolvePrimaryActionId(DesktopDialogState dialog)
        => dialog.Actions
            .FirstOrDefault(action => !IsCloseFamily(action.Id) && !string.Equals(action.Id, "add_more", StringComparison.Ordinal))
            ?.Id;

    private static string? ResolveCloseLikeActionId(DesktopDialogState dialog)
        => dialog.Actions
            .FirstOrDefault(action => IsCloseFamily(action.Id))
            ?.Id;

    private static string? ResolveContinueActionId(DesktopDialogState dialog)
        => dialog.Actions
            .FirstOrDefault(action => string.Equals(action.Id, "continue", StringComparison.Ordinal))
            ?.Id;

    private static bool IsCloseFamily(string actionId)
        => string.Equals(actionId, "close", StringComparison.Ordinal)
            || string.Equals(actionId, "cancel", StringComparison.Ordinal);

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

    private static CharacterProgressSection CreateProgress()
        => new(
            Karma: 10m,
            Nuyen: 5000m,
            StartingNuyen: 0m,
            StreetCred: 1,
            Notoriety: 0,
            PublicAwareness: 0,
            BurntStreetCred: 0,
            BuildKarma: 0,
            TotalAttributes: 20,
            TotalSpecial: 1,
            PhysicalCmFilled: 0,
            StunCmFilled: 0,
            TotalEssence: 6m,
            InitiateGrade: 0,
            SubmersionGrade: 0,
            MagEnabled: false,
            ResEnabled: false,
            DepEnabled: false);

    private static CharacterSkillsSection CreateSkills()
        => new(
            Count: 1,
            KnowledgeCount: 0,
            Skills:
            [
                new CharacterSkillSummary("skill-1", string.Empty, "Active", false, 4, 0, ["Visual"])
            ]);

    private static CharacterRulesSection CreateRules()
        => new(
            GameEdition: "SR5",
            Settings: "default.xml",
            GameplayOption: "Standard",
            GameplayOptionQualityLimit: 25,
            MaxNuyen: 10,
            MaxKarma: 25,
            ContactMultiplier: 3,
            BannedWareGrades: []);

    private static CharacterBuildSection CreateBuild()
        => new(
            BuildMethod: "Priority",
            PriorityMetatype: "D,0",
            PriorityAttributes: "B,20",
            PrioritySpecial: "E,0",
            PrioritySkills: "C,28",
            PriorityResources: "A,450000",
            PriorityTalent: "Mundane",
            SumToTen: 10,
            Special: 0,
            TotalSpecial: 0,
            TotalAttributes: 20,
            ContactPoints: 12,
            ContactPointsUsed: 4);

    private static CharacterMovementSection CreateMovement()
        => new(
            Walk: "4",
            Run: "8",
            Sprint: "12",
            WalkAlt: "4",
            RunAlt: "8",
            SprintAlt: "12",
            PhysicalCmFilled: 0,
            StunCmFilled: 0);

    private static CharacterAwakeningSection CreateAwakening()
        => new(
            MagEnabled: false,
            ResEnabled: false,
            DepEnabled: false,
            Adept: false,
            Magician: false,
            Technomancer: false,
            AI: false,
            InitiateGrade: 0,
            SubmersionGrade: 0,
            Tradition: string.Empty,
            TraditionName: string.Empty,
            TraditionDrain: string.Empty,
            SpiritCombat: string.Empty,
            SpiritDetection: string.Empty,
            SpiritHealth: string.Empty,
            SpiritIllusion: string.Empty,
            SpiritManipulation: string.Empty,
            Stream: string.Empty,
            StreamDrain: string.Empty,
            CurrentCounterspellingDice: 0,
            SpellLimit: 0,
            CfpLimit: 0,
            AiNormalProgramLimit: 0,
            AiAdvancedProgramLimit: 0);

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

    private static IReadOnlyList<SectionRowState> BuildSectionRows(string sectionId)
        => [new SectionRowState($"{sectionId}.row.1", "Primary"), new SectionRowState($"{sectionId}.row.2", "Secondary")];

    private static CharacterCreationBootstrapReceipt CreateBootstrapReceipt(
        CharacterCreationBootstrapRequest request,
        CharacterWorkspaceId workspaceId)
    {
        string canonicalDigest = "sha256:" + new string('a', 64);
        string[] sourceAnchorIds = CharacterCreationBootstrapProfiles.ExpectedSourceAnchorIds(
            request.BuildMethod,
            request.SettingsProfileId);
        var unsignedBinding = new CharacterCreationBootstrapBinding(
            CharacterCreationBootstrapSchemas.BindingV1,
            CharacterCreationBootstrapStages.AwaitingFoundationSelection,
            workspaceId,
            request.RulesetId,
            request.BuildMethod,
            request.SettingsProfileId,
            CharacterCreationBootstrapRevisions.InitialContentRevision,
            CharacterCreationBootstrapRevisions.InitialSavedRevision,
            canonicalDigest,
            canonicalDigest,
            canonicalDigest,
            request.BuildMethod is CharacterCreationBuildMethods.Priority
                or CharacterCreationBuildMethods.SumToTen
                ? canonicalDigest
                : string.Empty,
            $"settings.xml#setting:{request.SettingsProfileId}",
            sourceAnchorIds,
            string.Empty);
        CharacterCreationBootstrapBinding binding = unsignedBinding with
        {
            BindingDigest = CharacterCreationBootstrapBindingDigest.Compute(unsignedBinding)
        };
        var unsignedReceipt = new CharacterCreationBootstrapReceipt(
            CharacterCreationBootstrapSchemas.ReceiptV1,
            workspaceId,
            CharacterCreationBootstrapRevisions.InitialContentRevision,
            CharacterCreationBootstrapRevisions.InitialSavedRevision,
            new CharacterFileSummary(
                request.Name.Trim(),
                request.Alias.Trim(),
                string.Empty,
                request.BuildMethod,
                "5.225.0",
                "5.225.0",
                0,
                0,
                false),
            binding,
            sourceAnchorIds,
            string.Empty);
        return unsignedReceipt with
        {
            ReceiptDigest = CharacterCreationBootstrapReceiptDigest.Compute(unsignedReceipt)
        };
    }

    private static DesktopDialogState RebuildDynamicDialog(DesktopDialogState dialog)
    {
        MethodInfo method = typeof(DesktopDialogFactory).GetMethod(
            "RebuildDynamicDialog",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RebuildDynamicDialog was not found.");

        return (DesktopDialogState)(method.Invoke(null, [dialog, DesktopPreferenceState.Default])
            ?? throw new InvalidOperationException("RebuildDynamicDialog returned null."));
    }

    private sealed class WorkflowHarness
    {
        private readonly string _returnTabId;
        private readonly string _returnSectionId;
        private readonly CharacterWorkspaceId _workspaceId = new("ws-1");
        private CharacterCreationBootstrapRequest? _bootstrapRequest;

        public WorkflowHarness(CharacterOverviewState state, string returnTabId, string returnSectionId)
        {
            State = state;
            _returnTabId = returnTabId;
            _returnSectionId = returnSectionId;
        }

        public CharacterOverviewState State { get; private set; }

        public async Task ActAsync(string actionId)
        {
            DialogCoordinationContext context = new(
                State,
                Publish,
                ImportAsync,
                UpdateMetadataAsync,
                () => State,
                ExportAsync,
                PrintAsync,
                SetPreferredRulesetAsync,
                ApplyQuickAddAsync,
                CreateCharacterBootstrapAsync: CreateCharacterBootstrapAsync,
                LoadWorkspaceAsync: LoadBootstrapWorkspaceAsync);

            await Coordinator.CoordinateAsync(actionId, context, CancellationToken.None);
        }

        public void UpdateDialogField(string fieldId, string value)
        {
            DesktopDialogState dialog = State.ActiveDialog
                ?? throw new InvalidOperationException("No active dialog is available for field updates.");

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

            DesktopDialogState updatedDialog = dialog with
            {
                Fields = updatedFields
            };

            State = State with
            {
                ActiveDialog = RebuildDynamicDialog(updatedDialog)
            };
        }

        private void Publish(CharacterOverviewState next)
            => State = next;

        private Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
        {
            OpenWorkspaceState workspace = new(
                Id: _workspaceId,
                Name: "Imported Runner",
                Alias: "IMPT",
                LastOpenedUtc: DateTimeOffset.Parse("2026-05-04T12:30:00+00:00"),
                RulesetId: document.RulesetId,
                HasSavedWorkspace: true);

            State = State with
            {
                WorkspaceId = _workspaceId,
                OpenWorkspaces = [workspace],
                Session = new WorkspaceSessionState(
                    ActiveWorkspaceId: _workspaceId,
                    OpenWorkspaces: [workspace],
                    RecentWorkspaceIds: [_workspaceId]),
                ActiveTabId = "tab-info",
                ActiveSectionId = "profile",
                ActiveSectionJson = BuildSectionJson("profile"),
                ActiveSectionRows = BuildSectionRows("profile"),
                ActiveDialog = null,
                Error = null
            };

            return Task.CompletedTask;
        }

        private Task<CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>>
            CreateCharacterBootstrapAsync(
                CharacterCreationBootstrapRequest request,
                CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _bootstrapRequest = request;
            CharacterWorkspaceId workspaceId = new("ws-bootstrap");
            return Task.FromResult(
                new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Success,
                    CreateBootstrapReceipt(request, workspaceId),
                    []));
        }

        private Task LoadBootstrapWorkspaceAsync(
            CharacterWorkspaceId workspaceId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CharacterCreationBootstrapRequest request = _bootstrapRequest
                ?? throw new InvalidOperationException("No bootstrap request was captured.");
            OpenWorkspaceState workspace = new(
                workspaceId,
                request.Name,
                request.Alias,
                DateTimeOffset.Parse("2026-05-04T12:30:00+00:00"),
                request.RulesetId,
                CharacterCreationBootstrapRevisions.InitialContentRevision,
                CharacterCreationBootstrapRevisions.InitialSavedRevision);
            State = State with
            {
                WorkspaceId = workspaceId,
                OpenWorkspaces = [workspace],
                Session = new WorkspaceSessionState(
                    ActiveWorkspaceId: workspaceId,
                    OpenWorkspaces: [workspace],
                    RecentWorkspaceIds: [workspaceId]),
                ActiveTabId = "tab-info",
                ActiveSectionId = "profile",
                ActiveSectionJson = BuildSectionJson("profile"),
                ActiveSectionRows = BuildSectionRows("profile"),
                ActiveDialog = null,
                Error = null
            };
            return Task.CompletedTask;
        }

        private Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct)
        {
            State = State with
            {
                ActiveDialog = null,
                Error = null,
                Profile = State.Profile is null
                    ? null
                    : State.Profile with
                    {
                        Name = string.IsNullOrWhiteSpace(command.Name) ? State.Profile.Name : command.Name,
                        Alias = string.IsNullOrWhiteSpace(command.Alias) ? State.Profile.Alias : command.Alias
                    }
            };

            return Task.CompletedTask;
        }

        private Task ExportAsync(CancellationToken ct)
        {
            State = State with
            {
                ActiveDialog = null,
                Error = null,
                PendingExport = new WorkspaceExportReceipt(
                    Id: _workspaceId,
                    Format: WorkspaceDocumentFormat.Json,
                    ContentBase64: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}")),
                    FileName: "parity-export.json",
                    DocumentLength: 11,
                    RulesetId: RulesetDefaults.Sr5),
                PendingExportVersion = State.PendingExportVersion + 1
            };

            return Task.CompletedTask;
        }

        private Task PrintAsync(CancellationToken ct)
        {
            State = State with
            {
                ActiveDialog = null,
                Error = null,
                PendingPrint = new WorkspacePrintReceipt(
                    Id: _workspaceId,
                    ContentBase64: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<html></html>")),
                    FileName: "parity-print.html",
                    MimeType: "text/html",
                    DocumentLength: 13,
                    Title: "Parity Print",
                    RulesetId: RulesetDefaults.Sr5),
                PendingPrintVersion = State.PendingPrintVersion + 1
            };

            return Task.CompletedTask;
        }

        private Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct)
        {
            State = State with
            {
                ActiveDialog = null,
                Error = null
            };

            return Task.CompletedTask;
        }

        private Task ApplyQuickAddAsync(WorkspaceQuickAddRequest request, CancellationToken ct)
        {
            State = State with
            {
                ActiveTabId = _returnTabId,
                ActiveSectionId = _returnSectionId,
                ActiveSectionJson = $$"""
                                    {
                                      "sectionId": "{{_returnSectionId}}",
                                      "lastAdded": "{{request.Name}}",
                                      "kind": "{{request.Kind}}"
                                    }
                                    """,
                ActiveSectionRows = BuildSectionRows(_returnSectionId),
                ActiveDialog = null,
                Error = null
            };

            return Task.CompletedTask;
        }
    }

    private enum WorkflowShape
    {
        Selection,
        DenseEditor,
        Choice,
        Import,
        Tool,
        Preview,
        Info,
        Utility
    }

    private sealed record MenuWorkflowContract(string Id, WorkflowShape Shape);

    private sealed record UiControlWorkflowContract(
        string Id,
        WorkflowShape Shape,
        string ReturnTabId,
        string ReturnSectionId,
        bool IsQuickActionRoot = false,
        bool SupportsAddMoreLoop = true);

    private sealed record ExactVisibleSelectContract(
        IReadOnlyList<ExactDialogFieldOptionContract> Options,
        string SelectedValue);

    private sealed record ExactDialogFieldOptionContract(
        string Value,
        string Label);

    private sealed record MuscleMemoryDialogSurfaceContract(
        string SurfaceId,
        IReadOnlyList<MuscleMemoryDialogFieldContract> Fields,
        IReadOnlyList<MuscleMemoryDialogActionContract> Actions);

    private sealed record MuscleMemoryDialogFieldContract(
        string FieldId,
        string ExpectedLabel,
        string ExpectedInputType,
        string ExpectedVisualKind,
        string ExpectedLayoutSlot,
        int OptionsCount,
        bool IsVisible);

    private sealed record MuscleMemoryDialogActionContract(
        string ActionId,
        bool IsPrimary,
        bool IsVisible);

    private static readonly IReadOnlyDictionary<string, MenuWorkflowContract> MenuContracts =
        new[]
        {
            new MenuWorkflowContract("about", WorkflowShape.Info),
            new MenuWorkflowContract("auto_alice", WorkflowShape.Choice),
            new MenuWorkflowContract("character_roster", WorkflowShape.Tool),
            new MenuWorkflowContract("character_settings", WorkflowShape.Choice),
            new MenuWorkflowContract("data_exporter", WorkflowShape.Preview),
            new MenuWorkflowContract("dice_roller", WorkflowShape.Tool),
            new MenuWorkflowContract("discord", WorkflowShape.Info),
            new MenuWorkflowContract("dumpshock", WorkflowShape.Info),
            new MenuWorkflowContract("export_character", WorkflowShape.Preview),
            new MenuWorkflowContract("global_settings", WorkflowShape.Choice),
            new MenuWorkflowContract("hero_lab_importer", WorkflowShape.Import),
            new MenuWorkflowContract("master_index", WorkflowShape.Tool),
            new MenuWorkflowContract("new_character", WorkflowShape.Choice),
            new MenuWorkflowContract("new_character_origin", WorkflowShape.Choice),
            new MenuWorkflowContract("new_window", WorkflowShape.Info),
            new MenuWorkflowContract("open_character", WorkflowShape.Import),
            new MenuWorkflowContract("open_for_export", WorkflowShape.Import),
            new MenuWorkflowContract("open_for_printing", WorkflowShape.Import),
            new MenuWorkflowContract("print_multiple", WorkflowShape.Info),
            new MenuWorkflowContract("print_setup", WorkflowShape.Choice),
            new MenuWorkflowContract("report_bug", WorkflowShape.Info),
            new MenuWorkflowContract("revision_history", WorkflowShape.Info),
            new MenuWorkflowContract(OverviewCommandPolicy.RuntimeInspectorCommandId, WorkflowShape.Info),
            new MenuWorkflowContract("show_login_video", WorkflowShape.Info),
            new MenuWorkflowContract("switch_ruleset", WorkflowShape.Choice),
            new MenuWorkflowContract("translator", WorkflowShape.Info),
            new MenuWorkflowContract("update", WorkflowShape.Info),
            new MenuWorkflowContract("wiki", WorkflowShape.Info),
            new MenuWorkflowContract("xml_editor", WorkflowShape.Utility)
        }.ToDictionary(contract => contract.Id, StringComparer.Ordinal);

    private static RuntimeInspectorProjection CreateRuntimeInspectorProjection(string rulesetId)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeRequired(rulesetId);
        string packId = $"official.{normalizedRulesetId}.core";

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
                        Title: $"{normalizedRulesetId.ToUpperInvariant()} Core Bundle",
                        Description: "Representative parity bundle",
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
                RuntimeFingerprint: $"sha256:{normalizedRulesetId}-runtime-fingerprint"),
            Install: new ArtifactInstallState(
                State: ArtifactInstallStates.Available,
                RuntimeFingerprint: $"sha256:{normalizedRulesetId}-runtime-fingerprint"),
            ResolvedRulePacks:
            [
                new RuntimeInspectorRulePackEntry(
                    new ArtifactVersionReference(packId, "1.0.0"),
                    $"{normalizedRulesetId.ToUpperInvariant()} Core",
                    ArtifactVisibilityModes.LocalOnly,
                    ArtifactTrustTiers.Official,
                    [RulePackCapabilityIds.DeriveStat],
                    SourceKind: RegistryEntrySourceKinds.BuiltInCoreProfile)
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
                    RequiredRuntimeFingerprint: $"sha256:{normalizedRulesetId}-runtime-fingerprint")
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
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            ProfileSourceKind: RegistryEntrySourceKinds.BuiltInCoreProfile,
            CapabilityDescriptors:
            [
                new RuntimeInspectorCapabilityDescriptorProjection(
                    CapabilityId: RulePackCapabilityIds.DeriveStat,
                    InvocationKind: RulesetCapabilityInvocationKinds.Rule,
                    Title: "Derived Stat Evaluation",
                    Explainable: true,
                    SessionSafe: false,
                    DefaultGasBudget: new RulesetGasBudget(2_000, 5_000, 4_194_304),
                    MaximumGasBudget: new RulesetGasBudget(5_000, 10_000, 8_388_608),
                    ProviderId: $"{packId}/derive.stat",
                    PackId: packId)
            ]);
    }

    private static readonly IReadOnlyDictionary<string, UiControlWorkflowContract> UiControlContracts =
        new[]
        {
            new UiControlWorkflowContract("create_entry", WorkflowShape.Utility, "tab-calendar", "calendar", true),
            new UiControlWorkflowContract("edit_entry", WorkflowShape.Utility, "tab-calendar", "calendar"),
            new UiControlWorkflowContract("delete_entry", WorkflowShape.Utility, "tab-calendar", "calendar"),
            new UiControlWorkflowContract("open_notes", WorkflowShape.Utility, "tab-info", "profile", true),
            new UiControlWorkflowContract("identity_license_add", WorkflowShape.DenseEditor, "tab-info", "profile"),
            new UiControlWorkflowContract("identity_license_edit", WorkflowShape.DenseEditor, "tab-info", "profile"),
            new UiControlWorkflowContract("identity_license_delete", WorkflowShape.Utility, "tab-info", "profile"),
            new UiControlWorkflowContract("move_up", WorkflowShape.Utility, "tab-calendar", "calendar"),
            new UiControlWorkflowContract("move_down", WorkflowShape.Utility, "tab-calendar", "calendar"),
            new UiControlWorkflowContract("toggle_free_paid", WorkflowShape.Utility, "tab-gear", "inventory"),
            new UiControlWorkflowContract("show_source", WorkflowShape.Utility, "tab-rules", "rules"),
            new UiControlWorkflowContract("gear_add", WorkflowShape.Selection, "tab-gear", "inventory", true),
            new UiControlWorkflowContract("gear_edit", WorkflowShape.DenseEditor, "tab-gear", "inventory"),
            new UiControlWorkflowContract("gear_delete", WorkflowShape.Utility, "tab-gear", "inventory"),
            new UiControlWorkflowContract("runner_benchmark", WorkflowShape.Utility, "tab-stats", "profile"),
            new UiControlWorkflowContract("runner_what_if", WorkflowShape.Utility, "tab-stats", "profile"),
            new UiControlWorkflowContract("runner_cohort_privacy", WorkflowShape.Utility, "tab-stats", "profile"),
            new UiControlWorkflowContract("gear_mount", WorkflowShape.DenseEditor, "tab-gear", "inventory"),
            new UiControlWorkflowContract("gear_source", WorkflowShape.Utility, "tab-gear", "inventory"),
            new UiControlWorkflowContract("cyberware_add", WorkflowShape.Selection, "tab-cyberware", "cyberwares", true),
            new UiControlWorkflowContract("cyberware_edit", WorkflowShape.DenseEditor, "tab-cyberware", "cyberwares"),
            new UiControlWorkflowContract("cyberware_delete", WorkflowShape.Utility, "tab-cyberware", "cyberwares"),
            new UiControlWorkflowContract("drug_add", WorkflowShape.Selection, "tab-gear", "drugs", true),
            new UiControlWorkflowContract("drug_delete", WorkflowShape.Utility, "tab-gear", "drugs"),
            new UiControlWorkflowContract("magic_add", WorkflowShape.Selection, "tab-magician", "spells"),
            new UiControlWorkflowContract("magic_delete", WorkflowShape.Utility, "tab-magician", "spells"),
            new UiControlWorkflowContract("magic_bind", WorkflowShape.DenseEditor, "tab-magician", "spells"),
            new UiControlWorkflowContract("magic_source", WorkflowShape.Utility, "tab-magician", "spells"),
            new UiControlWorkflowContract("spell_add", WorkflowShape.Selection, "tab-magician", "spells", true),
            new UiControlWorkflowContract("adept_power_add", WorkflowShape.Selection, "tab-adept", "powers", true),
            new UiControlWorkflowContract("complex_form_add", WorkflowShape.Selection, "tab-technomancer", "complexforms", true),
            new UiControlWorkflowContract("initiation_add", WorkflowShape.Selection, "tab-adept", "initiationgrades", true),
            new UiControlWorkflowContract("spirit_add", WorkflowShape.Selection, "tab-magician", "spirits", true),
            new UiControlWorkflowContract("critter_power_add", WorkflowShape.Selection, "tab-critter", "critterpowers", true),
            new UiControlWorkflowContract("matrix_program_add", WorkflowShape.Selection, "tab-technomancer", "aiprograms", true),
            new UiControlWorkflowContract("skill_add", WorkflowShape.Selection, "tab-skills", "skills", true),
            new UiControlWorkflowContract("skill_specialize", WorkflowShape.DenseEditor, "tab-skills", "skills"),
            new UiControlWorkflowContract("skill_remove", WorkflowShape.Utility, "tab-skills", "skills"),
            new UiControlWorkflowContract("skill_group", WorkflowShape.DenseEditor, "tab-skills", "skills"),
            new UiControlWorkflowContract("combat_add_weapon", WorkflowShape.Selection, "tab-combat", "weapons", true),
            new UiControlWorkflowContract("combat_add_armor", WorkflowShape.Selection, "tab-combat", "armors", true),
            new UiControlWorkflowContract("combat_reload", WorkflowShape.DenseEditor, "tab-combat", "weapons"),
            new UiControlWorkflowContract("combat_damage_track", WorkflowShape.DenseEditor, "tab-combat", "weapons"),
            new UiControlWorkflowContract("vehicle_add", WorkflowShape.Selection, "tab-gear", "vehicles", true),
            new UiControlWorkflowContract("vehicle_edit", WorkflowShape.DenseEditor, "tab-gear", "vehicles"),
            new UiControlWorkflowContract("vehicle_delete", WorkflowShape.Utility, "tab-gear", "vehicles"),
            new UiControlWorkflowContract("vehicle_mod_add", WorkflowShape.Tool, "tab-gear", "vehicles", SupportsAddMoreLoop: false),
            new UiControlWorkflowContract("contact_add", WorkflowShape.DenseEditor, "tab-contacts", "contacts", true),
            new UiControlWorkflowContract("contact_edit", WorkflowShape.DenseEditor, "tab-contacts", "contacts"),
            new UiControlWorkflowContract("contact_remove", WorkflowShape.Utility, "tab-contacts", "contacts"),
            new UiControlWorkflowContract("contact_connection", WorkflowShape.DenseEditor, "tab-contacts", "contacts"),
            new UiControlWorkflowContract("quality_add", WorkflowShape.Selection, "tab-qualities", "qualities", true),
            new UiControlWorkflowContract("quality_delete", WorkflowShape.Utility, "tab-qualities", "qualities")
        }.ToDictionary(contract => contract.Id, StringComparer.Ordinal);
}
