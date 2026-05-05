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

    private static async Task AssertNewCharacterRecursiveParityAsync(string rulesetId, string buildMethod)
    {
        DesktopDialogState dialog = CreateCommandDialog("new_character", rulesetId);
        WorkflowHarness harness = CreateHarness(rulesetId, dialog, "tab-info", "profile");

        harness.UpdateDialogField("newCharacterRulesetId", rulesetId);
        harness.UpdateDialogField("newCharacterBuildMethod", buildMethod);
        await harness.ActAsync("create_character");

        string expectedDialogId = string.Equals(buildMethod, "Priority", StringComparison.Ordinal)
            ? "dialog.new_character.priority_workflow"
            : "dialog.new_character.karma_workflow";

        Assert.IsNotNull(harness.State.ActiveDialog, $"'{buildMethod}' new-character branch must materialize a continuation dialog.");
        Assert.AreEqual(expectedDialogId, harness.State.ActiveDialog!.Id);
        AssertDialogParity(rulesetId, "new_character.continuation", WorkflowShape.Choice, harness.State.ActiveDialog);

        if (string.Equals(buildMethod, "Priority", StringComparison.Ordinal))
        {
            harness.UpdateDialogField("newCharacterMetatypeCategory", "Metahuman");
            harness.UpdateDialogField("newCharacterMetatype", "Elf");
            harness.UpdateDialogField("newCharacterPriorityTalentChoice", "Adept");

            Assert.IsNotNull(harness.State.ActiveDialog, "Priority continuation must stay open after combobox updates.");
            DesktopDialogState mutatedDialog = harness.State.ActiveDialog!;
            AssertExactVisibleSelectField(
                mutatedDialog,
                "newCharacterMetatypeCategory",
                "Metahuman",
                ("Standard", "Standard"),
                ("Metahuman", "Metahuman"),
                ("Show All", "Show All"));
            AssertExactVisibleSelectField(
                mutatedDialog,
                "newCharacterMetatype",
                "Elf",
                ("Elf", "Elf"),
                ("Dwarf", "Dwarf"),
                ("Ork", "Ork"),
                ("Troll", "Troll"));
            AssertExactVisibleSelectField(
                mutatedDialog,
                "newCharacterPriorityTalentChoice",
                "Adept",
                ("Mundane", "Mundane"),
                ("Adept", "Adept"),
                ("Magician", "Magician"),
                ("Mystic Adept", "Mystic Adept"),
                ("Technomancer", "Technomancer"));

            DesktopDialogField summaryField = mutatedDialog.Fields.Single(field => string.Equals(field.Id, "newCharacterPriorityWorkflowSummary", StringComparison.Ordinal));
            StringAssert.Contains(summaryField.Value ?? string.Empty, "Metatype | Elf (Metahuman)");
            StringAssert.Contains(summaryField.Value ?? string.Empty, "Talent Choice | Adept");
        }

        await harness.ActAsync("complete_new_character_workflow");

        AssertReturnedSurfaceParity(harness.State, $"new_character.{buildMethod}", "tab-info", "profile");
        Assert.IsNotNull(harness.State.WorkspaceId, $"'{buildMethod}' new-character branch must return to a real workspace surface.");
        Assert.AreEqual("new_character", harness.State.LastCommandId, "The originating command id must stay visible after workflow completion.");
        Assert.IsTrue(harness.State.Session.OpenWorkspaces.Count > 0, "Completing the workflow must leave an open workspace in session state.");
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
            runtimeInspector: null,
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
            HasSavedWorkspace = true,
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
                    || dialog.Fields.Any(field => field.VisualKind == DesktopDialogFieldVisualKinds.Snippet),
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
                Assert.IsTrue(dialog.Fields.Any(field => field.IsMultiline || field.VisualKind == DesktopDialogFieldVisualKinds.Snippet), $"'{workflowId}' must keep preview content visible.");
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
                TryResolveExactVisibleSelectContract(rulesetId, dialog.Id, field.Id, out ExactVisibleSelectContract? contract),
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

    private static bool TryResolveExactVisibleSelectContract(
        string rulesetId,
        string dialogId,
        string fieldId,
        out ExactVisibleSelectContract? contract)
    {
        static ExactVisibleSelectContract Create(string selectedValue, params (string Value, string Label)[] options)
            => new(
                options.Select(option => new ExactDialogFieldOptionContract(option.Value, option.Label)).ToArray(),
                selectedValue);

        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        contract = (dialogId, fieldId, normalizedRulesetId) switch
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
                "SumToTen",
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

            ("dialog.new_character.priority_workflow", "newCharacterMetatypeCategory", _)
                or ("dialog.new_character.karma_workflow", "newCharacterMetatypeCategory", _) => Create(
                    "Standard",
                    ("Standard", "Standard"),
                    ("Metahuman", "Metahuman"),
                    ("Show All", "Show All")),

            ("dialog.new_character.priority_workflow", "newCharacterMetatype", _)
                or ("dialog.new_character.karma_workflow", "newCharacterMetatype", _) => Create(
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
        string[] expectedFieldIds = contract.Fields
            .Select(field => field.FieldId)
            .Where(id => currentFieldIds.Contains(id, StringComparer.Ordinal))
            .ToArray();

        CollectionAssert.AreEqual(
            expectedFieldIds,
            currentFieldIds,
            $"'{workflowId}' field order drifted from the checked-in muscle-memory inventory.");

        Dictionary<string, MuscleMemoryDialogFieldContract> expectedFields = contract.Fields
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);

        foreach (DesktopDialogField field in renderedFields)
        {
            Assert.IsTrue(expectedFields.TryGetValue(field.Id, out MuscleMemoryDialogFieldContract? expectedField),
                $"'{workflowId}' field '{field.Id}' is missing from the checked-in muscle-memory inventory.");
            Assert.AreEqual(expectedField.ExpectedLabel, field.Label, $"'{workflowId}' field '{field.Id}' label drifted.");
            Assert.AreEqual(expectedField.ExpectedInputType, field.InputType, $"'{workflowId}' field '{field.Id}' input type drifted.");
            Assert.AreEqual(expectedField.ExpectedVisualKind, field.VisualKind, $"'{workflowId}' field '{field.Id}' visual kind drifted.");
            Assert.AreEqual(expectedField.ExpectedLayoutSlot, field.LayoutSlot, $"'{workflowId}' field '{field.Id}' layout slot drifted.");

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
                ApplyQuickAddAsync);

            await Coordinator.CoordinateAsync(actionId, context, CancellationToken.None);
        }

        public void UpdateDialogField(string fieldId, string value)
        {
            DesktopDialogState dialog = State.ActiveDialog
                ?? throw new InvalidOperationException("No active dialog is available for field updates.");

            DesktopDialogState updatedDialog = dialog with
            {
                Fields = dialog.Fields
                    .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                        ? field with { Value = value }
                        : field)
                    .ToArray()
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
            new MenuWorkflowContract("new_window", WorkflowShape.Info),
            new MenuWorkflowContract("open_character", WorkflowShape.Import),
            new MenuWorkflowContract("open_for_export", WorkflowShape.Import),
            new MenuWorkflowContract("open_for_printing", WorkflowShape.Import),
            new MenuWorkflowContract("print_multiple", WorkflowShape.Info),
            new MenuWorkflowContract("print_setup", WorkflowShape.Choice),
            new MenuWorkflowContract("report_bug", WorkflowShape.Info),
            new MenuWorkflowContract("revision_history", WorkflowShape.Info),
            new MenuWorkflowContract("switch_ruleset", WorkflowShape.Choice),
            new MenuWorkflowContract("translator", WorkflowShape.Info),
            new MenuWorkflowContract("update", WorkflowShape.Info),
            new MenuWorkflowContract("wiki", WorkflowShape.Info),
            new MenuWorkflowContract("xml_editor", WorkflowShape.Utility)
        }.ToDictionary(contract => contract.Id, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, UiControlWorkflowContract> UiControlContracts =
        new[]
        {
            new UiControlWorkflowContract("create_entry", WorkflowShape.Utility, "tab-info", "profile"),
            new UiControlWorkflowContract("edit_entry", WorkflowShape.Utility, "tab-info", "profile"),
            new UiControlWorkflowContract("delete_entry", WorkflowShape.Utility, "tab-info", "profile"),
            new UiControlWorkflowContract("open_notes", WorkflowShape.Utility, "tab-info", "profile", true),
            new UiControlWorkflowContract("move_up", WorkflowShape.Utility, "tab-info", "profile"),
            new UiControlWorkflowContract("move_down", WorkflowShape.Utility, "tab-info", "profile"),
            new UiControlWorkflowContract("toggle_free_paid", WorkflowShape.Utility, "tab-info", "profile"),
            new UiControlWorkflowContract("show_source", WorkflowShape.Utility, "tab-info", "profile"),
            new UiControlWorkflowContract("gear_add", WorkflowShape.Selection, "tab-gear", "inventory", true),
            new UiControlWorkflowContract("gear_edit", WorkflowShape.DenseEditor, "tab-gear", "inventory"),
            new UiControlWorkflowContract("gear_delete", WorkflowShape.Utility, "tab-gear", "inventory"),
            new UiControlWorkflowContract("gear_mount", WorkflowShape.DenseEditor, "tab-gear", "inventory"),
            new UiControlWorkflowContract("gear_source", WorkflowShape.Utility, "tab-gear", "inventory"),
            new UiControlWorkflowContract("cyberware_add", WorkflowShape.Selection, "tab-gear", "cyberwares"),
            new UiControlWorkflowContract("cyberware_edit", WorkflowShape.DenseEditor, "tab-gear", "cyberwares"),
            new UiControlWorkflowContract("cyberware_delete", WorkflowShape.Utility, "tab-gear", "cyberwares"),
            new UiControlWorkflowContract("drug_add", WorkflowShape.Selection, "tab-gear", "drugs"),
            new UiControlWorkflowContract("drug_delete", WorkflowShape.Utility, "tab-gear", "drugs"),
            new UiControlWorkflowContract("magic_add", WorkflowShape.Selection, "tab-magician", "spells"),
            new UiControlWorkflowContract("magic_delete", WorkflowShape.Utility, "tab-magician", "spells"),
            new UiControlWorkflowContract("magic_bind", WorkflowShape.DenseEditor, "tab-magician", "spells"),
            new UiControlWorkflowContract("magic_source", WorkflowShape.Utility, "tab-magician", "spells"),
            new UiControlWorkflowContract("spell_add", WorkflowShape.Selection, "tab-magician", "spells", true),
            new UiControlWorkflowContract("adept_power_add", WorkflowShape.Selection, "tab-adept", "powers"),
            new UiControlWorkflowContract("complex_form_add", WorkflowShape.Selection, "tab-adept", "complexforms"),
            new UiControlWorkflowContract("initiation_add", WorkflowShape.Selection, "tab-adept", "initiationgrades"),
            new UiControlWorkflowContract("spirit_add", WorkflowShape.Selection, "tab-magician", "spirits"),
            new UiControlWorkflowContract("critter_power_add", WorkflowShape.Selection, "tab-magician", "critterpowers"),
            new UiControlWorkflowContract("matrix_program_add", WorkflowShape.Selection, "tab-adept", "aiprograms"),
            new UiControlWorkflowContract("skill_add", WorkflowShape.Selection, "tab-skills", "skills", true),
            new UiControlWorkflowContract("skill_specialize", WorkflowShape.DenseEditor, "tab-skills", "skills"),
            new UiControlWorkflowContract("skill_remove", WorkflowShape.Utility, "tab-skills", "skills"),
            new UiControlWorkflowContract("skill_group", WorkflowShape.DenseEditor, "tab-skills", "skills"),
            new UiControlWorkflowContract("combat_add_weapon", WorkflowShape.Selection, "tab-combat", "weapons", true),
            new UiControlWorkflowContract("combat_add_armor", WorkflowShape.Selection, "tab-combat", "armors"),
            new UiControlWorkflowContract("combat_reload", WorkflowShape.DenseEditor, "tab-combat", "weapons"),
            new UiControlWorkflowContract("combat_damage_track", WorkflowShape.DenseEditor, "tab-combat", "weapons"),
            new UiControlWorkflowContract("vehicle_add", WorkflowShape.Selection, "tab-gear", "vehicles"),
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
