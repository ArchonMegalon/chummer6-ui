using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Explain;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class DesktopDialogFactoryTests
{
    [TestMethod]
    public void CreateCommandDialog_uses_current_preferences_and_workspace_context()
    {
        DesktopDialogFactory factory = new();
        DesktopPreferenceState preferences = DesktopPreferenceState.Default with
        {
            UiScalePercent = 125,
            Theme = "neo",
            Language = "de-de",
            CompactMode = true
        };

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "global_settings",
            profile: null,
            preferences,
            activeSectionJson: null,
            currentWorkspace: new CharacterWorkspaceId("ws-42"),
            rulesetId: RulesetDefaults.Sr5);

        Assert.AreEqual("dialog.global_settings", dialog.Id);
        Assert.AreEqual("125", DesktopDialogFieldValueParser.GetValue(dialog, "globalUiScale"));
        Assert.AreEqual("neo", DesktopDialogFieldValueParser.GetValue(dialog, "globalTheme"));
        Assert.AreEqual("de-de", DesktopDialogFieldValueParser.GetValue(dialog, "globalLanguage"));
        Assert.AreEqual("de-de", DesktopDialogFieldValueParser.GetValue(dialog, "globalSheetLanguage"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "globalCompactMode"));
        Assert.AreEqual("general", DesktopDialogFieldValueParser.GetValue(dialog, "globalActivePane"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsSections"), "Sourcebooks");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsDetailTabs"), "Language");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalLegacyTabBar"), "Global Options");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalLegacyTabBar"), "Plugins");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsTree"), "[Global Settings]");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsTree"), "Desktop");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsTree"), "Data Paths");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsPropertyGrid"), "Scale | 125%");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsPropertyGrid"), "Sheet Language | de-de");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalPaneTools"), "Desktop Language | de-de");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalPaneCommandList"), "Set sheet language");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalCurrentPaneHeader"), "General / Desktop Language");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalCurrentPaneWorkflows"), "Set sheet language");
        Assert.AreEqual("/Characters", DesktopDialogFieldValueParser.GetValue(dialog, "globalCharacterRosterPath"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalCurrentPaneNotes"), "old utility settings form");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalRestartPosture"), "restart");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalVisibilityPolicy"), "status strip");
        Assert.AreEqual("Apply", dialog.Actions.Single(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)).Label);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tabs, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsSections", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tabs, dialog.Fields.Single(field => string.Equals(field.Id, "globalLegacyTabBar", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tabs, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsDetailTabs", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsPropertyGrid", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "globalPaneTools", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "globalPaneCommandList", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsTree", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsPropertyGrid", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "globalPaneTools", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Hidden, dialog.Fields.Single(field => string.Equals(field.Id, "globalVisibilityPolicy", StringComparison.Ordinal)).LayoutSlot);
        StringAssert.Contains(dialog.Message ?? string.Empty, "Phase-1 desktop language changes apply on restart.");
    }

    [TestMethod]
    public void CreateMetadataDialog_prefills_profile_name_alias_and_notes()
    {
        DesktopDialogFactory factory = new();
        CharacterProfileSection profile = CreateProfile("Apex", "Predator");
        DesktopPreferenceState preferences = DesktopPreferenceState.Default with
        {
            CharacterNotes = "Stealth loadout"
        };

        DesktopDialogState dialog = factory.CreateMetadataDialog(profile, preferences);

        Assert.AreEqual("dialog.workspace.metadata", dialog.Id);
        Assert.AreEqual("Apex", DesktopDialogFieldValueParser.GetValue(dialog, "metadataName"));
        Assert.AreEqual("Predator", DesktopDialogFieldValueParser.GetValue(dialog, "metadataAlias"));
        Assert.AreEqual("Stealth loadout", DesktopDialogFieldValueParser.GetValue(dialog, "metadataNotes"));
    }

    [TestMethod]
    public void CreateUiControlDialog_open_notes_uses_character_notes_preference()
    {
        DesktopDialogFactory factory = new();
        DesktopPreferenceState preferences = DesktopPreferenceState.Default with
        {
            CharacterNotes = "From notes panel"
        };

        DesktopDialogState dialog = factory.CreateUiControlDialog("open_notes", preferences);

        Assert.AreEqual("dialog.ui.open_notes", dialog.Id);
        Assert.AreEqual("From notes panel", DesktopDialogFieldValueParser.GetValue(dialog, "uiNotesEditor"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiNotesSections"), "Metadata");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiNotesDetails"), "Behavior | inline notes editing");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiNotesDetails", StringComparison.Ordinal)).VisualKind);
    }

    [TestMethod]
    public void CreateCommandDialog_wiki_uses_dense_external_link_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "wiki",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null);

        Assert.AreEqual("dialog.wiki", dialog.Id);
        Assert.AreEqual("Chummer Wiki", DesktopDialogFieldValueParser.GetValue(dialog, "uiLinkLabel"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiLinkDetails"), "Action | open in browser");
    }

    [TestMethod]
    public void CreateCommandDialog_print_character_uses_dense_print_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "print_character",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null);

        Assert.AreEqual("dialog.print_character", dialog.Id);
        Assert.AreEqual("Current runner", DesktopDialogFieldValueParser.GetValue(dialog, "uiPrintScope"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiPrintDetails"), "host print preview");
    }

    [TestMethod]
    public void CreateCommandDialog_update_uses_dense_update_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "update",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null);

        Assert.AreEqual("dialog.update", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "updateSections"), "Channel");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "updateDetails"), "Support Path | /account/support");
    }

    [TestMethod]
    public void CreateUiControlDialog_mutating_controls_use_explicit_action_ids()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState gearAddDialog = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default);
        DesktopDialogState gearEditDialog = factory.CreateUiControlDialog("gear_edit", DesktopPreferenceState.Default);
        DesktopDialogState gearDeleteDialog = factory.CreateUiControlDialog("gear_delete", DesktopPreferenceState.Default);

        Assert.IsNotNull(gearAddDialog.Actions.SingleOrDefault(action => string.Equals(action.Id, "add", StringComparison.Ordinal)));
        Assert.IsNotNull(gearEditDialog.Actions.SingleOrDefault(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)));
        Assert.IsNotNull(gearDeleteDialog.Actions.SingleOrDefault(action => string.Equals(action.Id, "delete", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateUiControlDialog_edit_entry_keeps_legacy_navigation_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("edit_entry", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.edit_entry", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiEntrySections"), "Details");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiEntryContextTree"), "Current Entry");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiEntryCommandList"), "Apply changes to the current row");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "uiEntryContextTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiEntryCommandList", StringComparison.Ordinal)).VisualKind);
    }

    [TestMethod]
    public void CreateCommandDialog_translator_lists_locked_shipping_locales()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "translator",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null);

        CollectionAssert.AreEquivalent(
            DesktopLocalizationCatalog.ShippingLanguages.Select(language => language.Code).ToArray(),
            dialog.Fields.Where(field => field.Id.StartsWith("lang", StringComparison.Ordinal)).Select(field => field.Value).ToArray());
    }

    [TestMethod]
    public void CreateCommandDialog_master_index_surfaces_sourcebook_and_parity_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "master_index",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null,
            masterIndex: CreateMasterIndexResponse());

        Assert.AreEqual("dialog.master_index", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSections"), "Results");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexDetailTabs"), "Setting");
        Assert.AreEqual("Data File / Search / Notes", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexPaneHeader"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexFileSelection"), "All");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexFileSelection"), "books.xml · 42 entries");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCurrentFile"), "books.xml · 42 indexed entries");
        Assert.AreEqual("CRB · Core Rulebook", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCurrentSourcebook"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSearchHints"), "Data File filters the list on the left");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSelectionTrail"), "Data File | books.xml");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSelectionTrail"), "Search | all rows");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSelectionTrail"), "Selected Result | Reference notes stay in this pane");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceTree"), "CRB · Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceTree"), "SW · Street Wyrd");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceTree"), "> CRB · Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCatalogEntries"), "[Current File]");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCatalogEntries"), "books.xml");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCatalogEntries"), "[Current Book]");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCatalogEntries"), "p. 20 · Reference notes stay in this pane");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexResultList"), "> p. 20 · Reference notes stay in this pane");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexResultList"), "books.xml");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceCommands"), "Change data file filter");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceCommands"), "Modify character setting");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceClickReminder"), "Click to open linked PDF at p. 20");
        Assert.AreEqual("/books/core-rulebook.pdf", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSelectedSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexDetails"), "Data File | books.xml");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexDetails"), "Selected item | Core Rulebook (CRB)");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexDetails"), "Source | CRB · Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexResultInspector"), "Selected Result | Reference notes stay in this pane");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexResultInspector"), "Data File | books.xml");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexResultInspector"), "Activation | select row / open source");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexResultInspector"), "Reference Posture | governed");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexResultCommands"), "Select result to refresh notes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexResultCommands"), "Keep source and notes pinned on the right");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSnippetInspector"), "Snippet Count | 16");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSnippetPreview"), "Reference notes stay in this pane");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexNotesPane"), "Use Data File on the left");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCharacterSetting"), "Use Setting | governed");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCharacterSetting"), "Modify | Modify...");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexFileSelection", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexCurrentFile", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexSourceTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexCatalogEntries", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexCatalogEntries", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexResultList", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexResultInspector", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexSnippetInspector", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexSnippetPreview", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexSourceClickReminder", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexNotesPane", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexCharacterSetting", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexSelectedSource", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexSourceSelectionSummary", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexImportOracleMatrix", StringComparison.Ordinal)).VisualKind);
        CollectionAssert.AreEqual(
            new[] { "open_source", "switch_file", "edit_setting", "close" },
            dialog.Actions.Select(action => action.Id).ToArray());
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Hidden, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexLibraryNotes", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Hidden, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexImportNotes", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Hidden, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexSr6Notes", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("12", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourcebooks"));
        Assert.AreEqual("67% (8/12)", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexReferenceCoverage"));
        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSettingsLane"));
        Assert.AreEqual("all sourcebooks expose governed PDF/URL/site-snapshot references.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexReferenceSourceReceipt"));
        Assert.AreEqual("sourcebook selection is governed by 24 toggles across 12 sourcebooks (67% coverage).", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceSelectionReceipt"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceSelectionSummary"), "2 sourcebooks");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourcebook1"), "Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourcebook2"), "Street Wyrd");
        Assert.AreEqual("custom-data authoring is partial: 2 configured custom-data directories with stale overlay bridge posture.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCustomDataAuthoringReceipt"));
        Assert.AreEqual("xml bridge is governed: 2 enabled data overlays expose XML payloads.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexXmlBridgeReceipt"));
        Assert.AreEqual("translator lane is governed: 6 translator corpus files and 3 enabled language overlays.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexTranslatorReceipt"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexImportOracleLane"), "75%");
        Assert.AreEqual("import oracle is partial: 3/4 fixture families covered (missing: Hero Lab), adjacent SR6 oracle coverage 1/2.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexImportOracleReceipt"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexImportOracleMatrix"), "Chummer4 fixtures 18");
        Assert.AreEqual("Hero Lab", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexImportOracleMissingSources"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexAdjacentSr6OracleLane"), "1/2");
        Assert.AreEqual("adjacent SR6 oracle lane is partial: 1/2 covered with stale receipts for Genesis/CommLink.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexAdjacentSr6OracleReceipt"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexOnlineStorageLane"), "50%");
        Assert.AreEqual("50% (1/2)", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexOnlineStorageCoverage"));
        Assert.AreEqual("online storage lane is partial: 1/2 continuity receipts are current with stale release proof on one required host lane.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexOnlineStorageReceipt"));
        Assert.AreEqual("partial", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSr6SupplementLane"));
        Assert.AreEqual("partial", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSr6DesignerToolsLane"));
        Assert.AreEqual("4/5", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSr6DesignerCoverage"));
        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexHouseRuleLane"));
        Assert.AreEqual("3", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexHouseRuleOverlayCount"));
        Assert.AreEqual("sr6 successor lane is partial: supplement/governed designers/house-rule posture remains mixed.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSr6SuccessorReceipt"));
    }

    [TestMethod]
    public void RebuildDynamicDialog_master_index_filters_results_from_search_state()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "master_index",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null,
            masterIndex: CreateMasterIndexResponse());

        DesktopDialogState rebuilt = RebuildDynamicDialog(WithFieldValues(
            dialog,
            ("masterIndexSearch", "Indexed"),
            ("masterIndexActiveFile", "books.xml"),
            ("masterIndexActiveResultKey", string.Empty)));

        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexSearchHints"), "1 visible rows");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexSelectionTrail"), "Search | Indexed");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexResultList"), "> p. 21");
        Assert.IsFalse(
            DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexResultList").Contains("p. 20", StringComparison.Ordinal),
            "Filtered result list should no longer contain the page 20 row.");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexSnippetPreview"), "Indexed source detail remains on the right");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexSourceCommands"), "Switch sourcebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexResultInspector"), "Activation | double-click row / open source");
    }

    [TestMethod]
    public void RebuildDynamicDialog_master_index_switches_sourcebook_from_browser_state()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "master_index",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null,
            masterIndex: CreateMasterIndexResponse());

        DesktopDialogState rebuilt = RebuildDynamicDialog(WithFieldValues(
            dialog,
            ("masterIndexActiveSourcebookId", "street-wyrd"),
            ("masterIndexCurrentSourcebook", "SW · Street Wyrd"),
            ("masterIndexActiveFile", "All"),
            ("masterIndexActiveResultKey", string.Empty)));

        Assert.AreEqual("SW · Street Wyrd", DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexCurrentSourcebook"));
        Assert.AreEqual("street-wyrd", DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexActiveSourcebookId"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexSourceTree"), "> SW · Street Wyrd");
        Assert.AreEqual("No indexed entries discovered.", DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexResultList"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexDetails"), "Street Wyrd (SW)");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexSourceClickReminder"), "No local PDF is attached");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(rebuilt, "masterIndexCatalogEntries"), "Street Wyrd [SW]");
    }

    [TestMethod]
    public void CreateCommandDialog_character_settings_surfaces_rules_environment_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "character_settings",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null,
            masterIndex: CreateMasterIndexResponse());

        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "characterSettingsLanePosture"));
        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "characterSourceToggleLanePosture"));
        Assert.AreEqual("67% (24 toggles)", DesktopDialogFieldValueParser.GetValue(dialog, "characterSourceToggleCoverage"));
        Assert.AreEqual("partial", DesktopDialogFieldValueParser.GetValue(dialog, "characterCustomDataLanePosture"));
        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "characterXmlBridgePosture"));
    }

    [TestMethod]
    public void CreateCommandDialog_translator_prefers_catalog_languages_and_surfaces_lane_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "translator",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: null,
            masterIndex: CreateMasterIndexResponse(),
            translatorLanguages: new TranslatorLanguagesResponse(
                Count: 2,
                Languages:
                [
                    new TranslatorLanguageEntry("en-us", "English"),
                    new TranslatorLanguageEntry("de-de", "Deutsch")
                ],
                TranslatorBridgePosture: "governed",
                EnabledLanguageOverlayCount: 3));

        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "translatorLanePosture"));
        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "translatorBridgePosture"));
        Assert.AreEqual("3", DesktopDialogFieldValueParser.GetValue(dialog, "translatorOverlayCount"));
        CollectionAssert.AreEquivalent(
            new[] { "en-us", "de-de" },
            dialog.Fields.Where(field => field.Id.StartsWith("lang", StringComparison.Ordinal)).Select(field => field.Value).ToArray());
    }

    [TestMethod]
    public void CreateCommandDialog_xml_editor_surfaces_xml_bridge_and_custom_data_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "xml_editor",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: "{\"sectionId\":\"profile\"}",
            currentWorkspace: null,
            rulesetId: null,
            masterIndex: CreateMasterIndexResponse());

        Assert.AreEqual("dialog.xml_editor", dialog.Id);
        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "xmlEditorLanePosture"));
        Assert.AreEqual("2", DesktopDialogFieldValueParser.GetValue(dialog, "xmlEditorOverlayCount"));
        Assert.AreEqual("partial", DesktopDialogFieldValueParser.GetValue(dialog, "xmlEditorCustomDataLanePosture"));
        Assert.AreEqual("2", DesktopDialogFieldValueParser.GetValue(dialog, "xmlEditorCustomDataDirectoryCount"));
        Assert.AreEqual("xml bridge is governed: 2 enabled data overlays expose XML payloads.", DesktopDialogFieldValueParser.GetValue(dialog, "xmlEditorReceipt"));
        Assert.AreEqual("{\"sectionId\":\"profile\"}", DesktopDialogFieldValueParser.GetValue(dialog, "xmlEditorDialog"));
    }

    [TestMethod]
    public void CreateCommandDialog_dice_roller_surfaces_initiative_preview_and_roster_context()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "dice_roller",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: new CharacterWorkspaceId("ws-2"),
            rulesetId: RulesetDefaults.Sr5,
            openWorkspaces:
            [
                new OpenWorkspaceState(new CharacterWorkspaceId("ws-1"), "Apex", "APX", DateTimeOffset.Parse("2026-04-04T11:00:00+00:00"), RulesetDefaults.Sr5, true),
                new OpenWorkspaceState(new CharacterWorkspaceId("ws-2"), "Ghost", "GST", DateTimeOffset.Parse("2026-04-04T12:00:00+00:00"), RulesetDefaults.Sr6, false)
            ]);

        Assert.AreEqual("dialog.dice_roller", dialog.Id);
        Assert.AreEqual("ruleset-backed roll + initiative preview", DesktopDialogFieldValueParser.GetValue(dialog, "diceUtilityLane"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "diceRosterContext"), "2 open runners");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "diceRosterContext"), "active ws-2");
        Assert.AreEqual("10 + 1d6 · pass 1 · range 11-16 · avg 13.5", DesktopDialogFieldValueParser.GetValue(dialog, "initiativePreview"));
        Assert.IsNotNull(dialog.Actions.SingleOrDefault(action => string.Equals(action.Id, "derive_initiative", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateCommandDialog_character_roster_summarizes_open_workspaces()
    {
        DesktopDialogFactory factory = new();
        string rosterPath = Path.Combine(Path.GetTempPath(), $"chummer-roster-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rosterPath);
        File.WriteAllText(Path.Combine(rosterPath, "APX.chum5"), "runner");
        File.WriteAllText(Path.Combine(rosterPath, "GST.chum5"), "runner");
        File.WriteAllText(Path.Combine(rosterPath, "GST.png"), "portrait");

        try
        {
            DesktopPreferenceState preferences = DesktopPreferenceState.Default with
            {
                CharacterRosterPath = rosterPath
            };

            DesktopDialogState dialog = factory.CreateCommandDialog(
                "character_roster",
                profile: CreateProfile("Fallback Runner", "FALL"),
                preferences,
                activeSectionJson: null,
                currentWorkspace: new CharacterWorkspaceId("ws-2"),
                rulesetId: RulesetDefaults.Sr5,
                openWorkspaces:
                [
                    new OpenWorkspaceState(new CharacterWorkspaceId("ws-1"), "Apex", "APX", DateTimeOffset.Parse("2026-04-04T11:00:00+00:00"), RulesetDefaults.Sr5, true),
                    new OpenWorkspaceState(new CharacterWorkspaceId("ws-2"), "Ghost", "GST", DateTimeOffset.Parse("2026-04-04T12:00:00+00:00"), RulesetDefaults.Sr6, false)
                ]);

            Assert.AreEqual("dialog.character_roster", dialog.Id);
            Assert.AreEqual("2", DesktopDialogFieldValueParser.GetValue(dialog, "rosterOpenCount"));
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSectionTabs"), "Background");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSectionTabs"), "Notes");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterDetailTabs"), "Description");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterDetailTabs"), "Game Notes");
            Assert.AreEqual("1", DesktopDialogFieldValueParser.GetValue(dialog, "rosterSavedCount"));
            Assert.AreEqual("2", DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchedCount"));
            Assert.AreEqual("sr6, sr5", DesktopDialogFieldValueParser.GetValue(dialog, "rosterRulesetMix"));
            Assert.AreEqual("ws-2", DesktopDialogFieldValueParser.GetValue(dialog, "rosterActiveWorkspace"));
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterTree"), "* GST · Ghost [sr6]");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterTree"), "[Watch Folder]");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterTree"), rosterPath);
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterTree"), "APX.chum5");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterTree"), "GST.chum5");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectionTrail"), "Active Runner | GST · Ghost");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectionTrail"), "Save Posture | not saved yet");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectionTrail"), $"Watch Folder | {rosterPath}");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectionTrail"), "Watch File | GST.chum5");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunner"), "Character Name | Ghost");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunner"), "Alias | GST");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunner"), "Watch File | GST.chum5");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunner"), "Settings File | sr6 roster setting");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterMugshot"), "GST · Ghost");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterMugshot"), $"Portrait Source | {Path.Combine(rosterPath, "GST.png")}");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterMugshot"), "Portrait Match | watched runner sibling");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterMugshot"), "Portrait Status | loaded from watched runner sibling");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterMugshot"), "Portrait Bytes | 8");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderStatus"), $"Watch Folder | {rosterPath}");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderStatus"), "Watcher | FileSystemWatcher active");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderStatus"), "Include Subdirectories | Yes");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderStatus"), "Watched Files | 2");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderStatus"), "Selected Watch File | GST.chum5");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderStatus"), "Portrait Match | watched runner sibling");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterRunnerCommands"), "Open selected runner");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterRunnerCommands"), "Save runner to roster folder");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderCommands"), "Open roster folder");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderCommands"), "Open selected watched runner");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderCommands"), "Open matched portrait");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerStatus"), "active ruleset sr6");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerStatus"), "watch file GST.chum5");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerBackground"), "Dense-workbench veteran entry");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerBackground"), "Description:");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerNotes"), "Character Notes:");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerNotes"), "Game Notes:");
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "rosterTree", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectionTrail", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectedRunner", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Image, dialog.Fields.Single(field => string.Equals(field.Id, "rosterMugshot", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "rosterWatchFolderStatus", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "rosterRunnerCommands", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "rosterWatchFolderCommands", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Tabs, dialog.Fields.Single(field => string.Equals(field.Id, "rosterDetailTabs", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectedRunnerStatus", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectedRunnerBackground", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectedRunnerNotes", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "rosterEntries", StringComparison.Ordinal)).VisualKind);
            Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "rosterTree", StringComparison.Ordinal)).LayoutSlot);
            Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectedRunner", StringComparison.Ordinal)).LayoutSlot);
            Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "rosterWatchFolderStatus", StringComparison.Ordinal)).LayoutSlot);
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterEntries"), "GST · Ghost · sr6 · unsaved");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterEntries"), "APX · Apex · sr5 · saved");
        }
        finally
        {
            if (Directory.Exists(rosterPath))
            {
                Directory.Delete(rosterPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CreateCommandDialog_character_roster_prefers_watched_runner_sibling_portrait()
    {
        DesktopDialogFactory factory = new();
        string rosterPath = Path.Combine(Path.GetTempPath(), $"chummer-roster-{Guid.NewGuid():N}");
        string nestedPath = Path.Combine(rosterPath, "campaign-a");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "ghost-runner.chum5"), "runner");
        File.WriteAllText(Path.Combine(nestedPath, "ghost-runner.png"), "portrait");

        try
        {
            DesktopPreferenceState preferences = DesktopPreferenceState.Default with
            {
                CharacterRosterPath = rosterPath
            };

            DesktopDialogState dialog = factory.CreateCommandDialog(
                "character_roster",
                profile: CreateProfile("Ghost Runner", "GST"),
                preferences,
                activeSectionJson: null,
                currentWorkspace: new CharacterWorkspaceId("ghost-runner"),
                rulesetId: RulesetDefaults.Sr5,
                openWorkspaces:
                [
                    new OpenWorkspaceState(new CharacterWorkspaceId("ghost-runner"), "Ghost Runner", "GST", DateTimeOffset.Parse("2026-04-04T12:00:00+00:00"), RulesetDefaults.Sr5, true)
                ]);

            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectionTrail"), "Watch File | campaign-a/ghost-runner.chum5");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterMugshot"), $"Portrait Source | {Path.Combine(nestedPath, "ghost-runner.png")}");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterMugshot"), "Portrait Match | watched runner sibling");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterWatchFolderCommands"), "Open matched portrait");
        }
        finally
        {
            if (Directory.Exists(rosterPath))
            {
                Directory.Delete(rosterPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CreateUiControlDialog_cyberware_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("cyberware_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.cyberware_add", dialog.Id);
        Assert.AreEqual("Wired Reflexes 2", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareName"));
        Assert.AreEqual("Show All", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCategory"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSearchInCategoryOnly"));
        Assert.AreEqual("All Books", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareBookFilter"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSections"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSections"), "Filters");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCategoryTree"), "Cyberlimbs");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCandidateList"), "Cybereyes Rating 4");
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareHideBannedGrades"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareHideOverAvailLimit"));
        Assert.AreEqual("0.00", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEssDiscount"));
        Assert.AreEqual("Core Rulebook p. 461", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSelectionDetails"), "Grade | Standard");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSelectionDetails"), "Availability | 12R");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSelectionTrail"), "Category Path | Cyberware > Bodyware > Wired Reflexes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCategoryCommands"), "Move the tree without losing grade or availability posture");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareResultCommands"), "Use OK for one add or Add & More to keep the selector open");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareSelectionTrail", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareCategoryCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareFilterSummary", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareLiveRecalc", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareResultCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareFilterSummary"), "Category Path | Cyberware > Bodyware");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareLiveRecalc"), "Add Again | Stays open");
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
        Assert.AreEqual("Add & More", dialog.Actions.Single(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_gear_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.gear_add", dialog.Id);
        Assert.AreEqual("Ares Predator V", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSections"), "Details");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSections"), "Filters");
        Assert.AreEqual("All Books", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearBookFilter"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearHideOverAvailLimit"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearShowOnlyAffordItems"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearStack"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearDoItYourself"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSelectionDetails"), "Category | Firearms");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSelectionDetails"), "Book | Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCategoryTree"), "Electronics");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCandidateList"), "Armor Jacket");
        Assert.AreEqual("Show All", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCategory"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSearchInCategoryOnly"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSelectionTrail"), "Category Path | Gear > Pistols > Ares Predator V");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCategoryCommands"), "Keep Do It Yourself and Stack visible while browsing");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearResultCommands"), "Keep markup, quantity, and source visible through confirmation");
        Assert.AreEqual("Core Rulebook p. 424", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSource"));
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearSelectionTrail", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearCategoryCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearFilterSummary", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearLiveRecalc", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearResultCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearFilterSummary"), "Category Path | Gear > Pistols");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearLiveRecalc"), "Add Again | Stays open");
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
        Assert.AreEqual("Add & More", dialog.Actions.Single(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_gear_edit_uses_dense_edit_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("gear_edit", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.gear_edit", dialog.Id);
        Assert.AreEqual("Armor Jacket", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditSections"), "Details");
        Assert.AreEqual("Core Rulebook p. 437", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditContextTree"), "Armor Jacket");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditNeighborList"), "Actioneer Business Clothes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditDetails"), "Availability | 12");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditLiveSummary"), "Total Cost | ¥1,000");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditCommands"), "Return to gear tabs");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearEditDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearEditLiveSummary", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearEditNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearEditContextTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearEditDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("Apply", dialog.Actions.Single(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_cyberware_edit_uses_dense_edit_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("cyberware_edit", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.cyberware_edit", dialog.Id);
        Assert.AreEqual("Cybereyes Rating 4", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditSections"), "Notes");
        Assert.AreEqual("Core Rulebook p. 455", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditContextTree"), "Cybereyes Rating 4");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditNeighborList"), "Datajack");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditDetails"), "Essence | 0.40");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditLiveSummary"), "Recalculated Essence | 0.40");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditCommands"), "Return to cyberware tabs");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareEditDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareEditLiveSummary", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareEditNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareEditContextTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareEditDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("Apply", dialog.Actions.Single(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_spell_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("spell_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.spell_add", dialog.Id);
        Assert.AreEqual("Stunbolt", DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellSections"), "Notes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellSections"), "Filters");
        Assert.AreEqual("All Books", DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellBookFilter"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellExtendedOnly"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellSelectionDetails"), "Type | Mana");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellSelectionDetails"), "Book | Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellCategoryTree"), "Illusion");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellCandidateList"), "Improved Invisibility");
        Assert.AreEqual("Core Rulebook p. 288", DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellSource"));
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiSpellSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiSpellNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiSpellCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiSpellSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_skill_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("skill_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.skill_add", dialog.Id);
        Assert.AreEqual("Perception", DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSections"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSections"), "Filters");
        Assert.AreEqual("Core Rulebook", DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillBookFilter"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillShowOnlyUsable"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSelectionDetails"), "Defaulting | Yes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSelectionDetails"), "Book | Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillCategoryTree"), "Language");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillCandidateList"), "Sneaking");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiSkillSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiSkillNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiSkillCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiSkillSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_drug_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("drug_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.drug_add", dialog.Id);
        Assert.AreEqual("Jazz", DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugSections"), "Notes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugSelectionDetails"), "Crash | 1 hour");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugCategoryTree"), "Stimulants");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugCandidateList"), "Cram");
        Assert.AreEqual("Core Rulebook p. 411", DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugSource"));
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiDrugSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiDrugNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
        Assert.AreEqual("Add & More", dialog.Actions.Single(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_contact_add_uses_dense_contact_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("contact_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.contact_add", dialog.Id);
        Assert.AreEqual("Dr. Mercy", DesktopDialogFieldValueParser.GetValue(dialog, "uiContactName"));
        Assert.AreEqual("Street Doc", DesktopDialogFieldValueParser.GetValue(dialog, "uiContactRole"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiContactDetails"), "Connection/Loyalty | 3 / 2");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiContactDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiContactNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
        Assert.AreEqual("Add & More", dialog.Actions.Single(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_contact_edit_uses_dense_contact_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("contact_edit", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.contact_edit", dialog.Id);
        Assert.AreEqual("Mr. Johnson", DesktopDialogFieldValueParser.GetValue(dialog, "uiContactEditName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiContactEditDetails"), "Connection/Loyalty | 5 / 3");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiContactEditDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiContactEditNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("Apply", dialog.Actions.Single(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_contact_connection_uses_dense_connection_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("contact_connection", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.contact_connection", dialog.Id);
        Assert.AreEqual("Mr. Johnson", DesktopDialogFieldValueParser.GetValue(dialog, "uiContactConnectionName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiContactConnectionDetails"), "Current Connection/Loyalty | 5 / 3");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiContactConnectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiContactConnectionNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("Apply", dialog.Actions.Single(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_skill_group_uses_dense_utility_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("skill_group", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.skill_group", dialog.Id);
        Assert.AreEqual("Stealth", DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillGroupName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillGroupSections"), "Details");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillGroupDetails"), "Skills | Disguise, Palming, Sneaking");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiSkillGroupDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiSkillGroupNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("Close", dialog.Actions.Single(action => string.Equals(action.Id, "close", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_combat_reload_uses_dense_utility_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("combat_reload", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.combat_reload", dialog.Id);
        Assert.AreEqual("Colt M23", DesktopDialogFieldValueParser.GetValue(dialog, "uiCombatReloadWeapon"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCombatReloadDetails"), "Current Magazine | 3 / 15");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiCombatReloadDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiCombatReloadNotes", StringComparison.Ordinal)).VisualKind);
    }

    [TestMethod]
    public void CreateUiControlDialog_combat_damage_track_uses_dense_utility_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("combat_damage_track", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.combat_damage_track", dialog.Id);
        Assert.AreEqual("3 / 10", DesktopDialogFieldValueParser.GetValue(dialog, "uiDamageTrackPhysical"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDamageTrackDetails"), "Penalty | none");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiDamageTrackDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiDamageTrackNotes", StringComparison.Ordinal)).VisualKind);
    }

    [TestMethod]
    public void CreateUiControlDialog_matrix_program_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("matrix_program_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.matrix_program_add", dialog.Id);
        Assert.AreEqual("Armor", DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramSections"), "Details");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramSections"), "Filters");
        Assert.AreEqual("Data Trails", DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramBookFilter"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramShowDongles"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramSelectionDetails"), "Slot | Common");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramSelectionDetails"), "Book | Data Trails");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramCategoryTree"), "Dongles");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramCandidateList"), "Baby Monitor");
        Assert.AreEqual("Data Trails p. 60", DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramSource"));
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiMatrixProgramSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiMatrixProgramNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiMatrixProgramCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiMatrixProgramSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_initiation_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("initiation_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.initiation_add", dialog.Id);
        Assert.AreEqual("Masking", DesktopDialogFieldValueParser.GetValue(dialog, "uiInitiationReward"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiInitiationSections"), "Details");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiInitiationSelectionDetails"), "Grade | 1");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiInitiationCategoryTree"), "Echos");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiInitiationCandidateList"), "Centering");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiInitiationSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiInitiationNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiInitiationCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiInitiationSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_spirit_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("spirit_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.spirit_add", dialog.Id);
        Assert.AreEqual("Watcher Spirit", DesktopDialogFieldValueParser.GetValue(dialog, "uiSpiritName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpiritSections"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpiritSelectionDetails"), "Force | 3");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpiritCategoryTree"), "Ally");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpiritCandidateList"), "Air Spirit");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiSpiritSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiSpiritNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_critter_power_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("critter_power_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.critter_power_add", dialog.Id);
        Assert.AreEqual("Natural Weapon", DesktopDialogFieldValueParser.GetValue(dialog, "uiCritterPowerName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCritterPowerSections"), "Notes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCritterPowerSelectionDetails"), "Type | Passive");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCritterPowerCategoryTree"), "Combat");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCritterPowerCandidateList"), "Elemental Attack");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiCritterPowerSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiCritterPowerNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_quality_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("quality_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.quality_add", dialog.Id);
        Assert.AreEqual("First Impression", DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiQualitySections"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiQualitySections"), "Filters");
        Assert.AreEqual("Core Rulebook", DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityBookFilter"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityShowNegative"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiQualitySelectionDetails"), "Karma | 11");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiQualitySelectionDetails"), "Book | Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityCategoryTree"), "Negative");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityCandidateList"), "Toughness");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiQualitySelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiQualityNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
        Assert.AreEqual("Add & More", dialog.Actions.Single(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_skill_specialize_uses_dense_specialization_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("skill_specialize", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.skill_specialize", dialog.Id);
        Assert.AreEqual("Perception", DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSpecializationSkill"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSpecializationDetails"), "Existing Specializations | Audio");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiSkillSpecializationDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiSkillSpecializationNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("Apply", dialog.Actions.Single(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_vehicle_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("vehicle_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.vehicle_add", dialog.Id);
        Assert.AreEqual("Hyundai Shin-Hyung", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSections"), "Details");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSections"), "Filters");
        Assert.AreEqual("Show All", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleCategory"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSearchInCategoryOnly"));
        Assert.AreEqual("All Books", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleBookFilter"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleShowDrones"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleShowOnlyAffordItems"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleFreeItem"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleBlackMarketDiscount"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleUsedVehicle"));
        Assert.AreEqual("25.00", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleUsedVehicleDiscount"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleViewModes"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSelectionDetails"), "Armor | 8");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSelectionDetails"), "Book | Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleCategoryTree"), "Drones");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleCandidateList"), "GMC Roadmaster");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSelectionTrail"), "Category Path | Vehicles > Cars > Hyundai Shin-Hyung");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleCategoryCommands"), "Keep used-vehicle and availability posture visible while browsing");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleResultCommands"), "Keep cost and used-vehicle posture visible through confirmation");
        Assert.AreEqual("Core Rulebook p. 465", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSource"));
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleSelectionTrail", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleCategoryCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleResultCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
        Assert.AreEqual("Add & More", dialog.Actions.Single(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_vehicle_edit_uses_dense_edit_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("vehicle_edit", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.vehicle_edit", dialog.Id);
        Assert.AreEqual("GMC Roadmaster", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditSections"), "Browse");
        Assert.AreEqual("Core Rulebook p. 466", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditContextTree"), "GMC Roadmaster");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditNeighborList"), "MCT Fly-Spy");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditDetails"), "Seats | 6");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditLiveSummary"), "Damage Soak | 34");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditCommands"), "Return to vehicle tabs");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleEditDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleEditLiveSummary", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleEditNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleEditContextTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleEditDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("Apply", dialog.Actions.Single(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_combat_add_weapon_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("combat_add_weapon", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.combat_add_weapon", dialog.Id);
        Assert.AreEqual("Colt M23", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSections"), "Notes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSections"), "Filters");
        Assert.AreEqual("All Books", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponBookFilter"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponHideOverAvailLimit"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponShowOnlyAffordItems"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponViewModes"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSelectionDetails"), "Damage | 7P");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSelectionDetails"), "Book | Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponCategoryTree"), "Heavy Pistols");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponIncludedAccessories"), "Smartgun System");
        Assert.AreEqual("Show All", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponCategory"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSearchInCategoryOnly"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSelectionTrail"), "Category Path | Weapons > Heavy Pistols > Colt M23");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponCategoryCommands"), "Review accessories and ammo follow-through after choosing the base weapon");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponResultCommands"), "Use OK for one add or Add & More to keep the selector open");
        Assert.AreEqual("Core Rulebook p. 424", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponCandidateList"), "Ares Alpha");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponSelectionTrail", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponCategoryCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponResultCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
        Assert.AreEqual("Add & More", dialog.Actions.Single(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_combat_add_armor_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("combat_add_armor", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.combat_add_armor", dialog.Id);
        Assert.AreEqual("Armor Jacket", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSections"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSections"), "Filters");
        Assert.AreEqual("All Books", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorBookFilter"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorHideOverAvailLimit"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorShowOnlyAffordItems"));
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorBlackMarketDiscount"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorViewModes"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSelectionDetails"), "Availability | 12");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSelectionDetails"), "Book | Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorCategoryTree"), "Clothing");
        Assert.AreEqual("Show All", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorCategory"));
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSearchInCategoryOnly"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSelectionTrail"), "Category Path | Armor > Armor > Armor Jacket");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorCategoryCommands"), "Review mods and accessories after selecting the base armor");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorResultCommands"), "Keep markup and capacity posture visible through confirmation");
        Assert.AreEqual("Core Rulebook p. 436", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorCandidateList"), "Actioneer Business Clothes");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorSelectionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorSelectionTrail", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorCategoryCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorResultCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void RebuildDynamicDialog_cyberware_add_updates_selection_from_filters()
    {
        DesktopDialogFactory factory = new();
        DesktopDialogState dialog = WithFieldValues(
            factory.CreateUiControlDialog("cyberware_add", DesktopPreferenceState.Default),
            ("uiCyberwareCategory", "Headware"),
            ("uiCyberwareSearch", "eyes"),
            ("uiCyberwareName", "Cybereyes Rating 4"),
            ("uiCyberwareGrade", "Alpha"),
            ("uiCyberwareBlackMarketDiscount", "true"));

        dialog = RebuildDynamicDialog(dialog);

        Assert.AreEqual("Cybereyes Rating 4", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareName"));
        Assert.AreEqual("Headware", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCategory"));
        Assert.AreEqual("Core Rulebook p. 455", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCandidateList"), "> Cybereyes Rating 4");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSelectionDetails"), "Cost | ¥17,280");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSelectionDetails"), "Essence | 0.32");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSelectionTrail"), "Category Path | Cyberware > Headware > Cybereyes Rating 4");
    }

    [TestMethod]
    public void RebuildDynamicDialog_gear_add_updates_selection_from_search_and_affordability()
    {
        DesktopDialogFactory factory = new();
        DesktopDialogState dialog = WithFieldValues(
            factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default),
            ("uiGearSearch", "medkit"),
            ("uiGearName", "Medkit Rating 6"),
            ("uiGearShowOnlyAffordItems", "true"));

        dialog = RebuildDynamicDialog(dialog);

        Assert.AreEqual("Medkit Rating 6", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearName"));
        Assert.AreEqual("Medical", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCategory"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCandidateList"), "> Medkit Rating 6");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSelectionDetails"), "Category | General");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSelectionTrail"), "Category Path | Gear > Medical > Medkit Rating 6");
    }

    [TestMethod]
    public void RebuildDynamicDialog_weapon_add_updates_selection_and_accessories()
    {
        DesktopDialogFactory factory = new();
        DesktopDialogState dialog = WithFieldValues(
            factory.CreateUiControlDialog("combat_add_weapon", DesktopPreferenceState.Default),
            ("uiWeaponSearch", "Defiance"),
            ("uiWeaponName", "Defiance T-250"),
            ("uiWeaponShowOnlyAffordItems", "true"));

        dialog = RebuildDynamicDialog(dialog);

        Assert.AreEqual("Defiance T-250", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponName"));
        Assert.AreEqual("Shotguns", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponCategory"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSelectionDetails"), "Mode | SS/SA");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponIncludedAccessories"), "Internal Smartgun");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSelectionTrail"), "Category Path | Weapons > Shotguns > Defiance T-250");
    }

    [TestMethod]
    public void RebuildDynamicDialog_armor_add_updates_selection_from_branch_filters()
    {
        DesktopDialogFactory factory = new();
        DesktopDialogState dialog = WithFieldValues(
            factory.CreateUiControlDialog("combat_add_armor", DesktopPreferenceState.Default),
            ("uiArmorCategory", "Shields"),
            ("uiArmorSearch", "Ballistic"),
            ("uiArmorName", "Ballistic Shield"),
            ("uiArmorShowOnlyAffordItems", "true"),
            ("uiArmorBlackMarketDiscount", "true"));

        dialog = RebuildDynamicDialog(dialog);

        Assert.AreEqual("Ballistic Shield", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorName"));
        Assert.AreEqual("Shields", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorCategory"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSelectionDetails"), "Cost | ¥810");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSelectionTrail"), "Category Path | Armor > Shields > Ballistic Shield");
    }

    [TestMethod]
    public void RebuildDynamicDialog_vehicle_add_updates_selection_with_used_vehicle_flow()
    {
        DesktopDialogFactory factory = new();
        DesktopDialogState dialog = WithFieldValues(
            factory.CreateUiControlDialog("vehicle_add", DesktopPreferenceState.Default),
            ("uiVehicleSearch", "Fly-Spy"),
            ("uiVehicleName", "MCT Fly-Spy"),
            ("uiVehicleShowOnlyAffordItems", "true"),
            ("uiVehicleBlackMarketDiscount", "true"),
            ("uiVehicleUsedVehicle", "true"),
            ("uiVehicleUsedVehicleDiscount", "25.00"));

        dialog = RebuildDynamicDialog(dialog);

        Assert.AreEqual("MCT Fly-Spy", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleName"));
        Assert.AreEqual("Drones", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleCategory"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSelectionDetails"), "Role | Vehicle / Drone Catalog");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleLiveRecalc"), "Selected Cost | ¥1,350");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSelectionTrail"), "Category Path | Vehicles > Drones > MCT Fly-Spy");
    }

    [TestMethod]
    public void CreateUiControlDialog_show_source_uses_compact_source_detail_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("show_source", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.show_source", dialog.Id);
        Assert.AreEqual("Core Rulebook", DesktopDialogFieldValueParser.GetValue(dialog, "uiSourceBook"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSourceSections"), "Notes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSourceDetails"), "PDF | /books/core-rulebook.pdf#page=424");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiSourceDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiSourceNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiSourceDetails", StringComparison.Ordinal)).LayoutSlot);
    }

    [TestMethod]
    public void CreateUiControlDialog_create_entry_uses_dense_entry_editor_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("create_entry", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.create_entry", dialog.Id);
        Assert.AreEqual("New entry", DesktopDialogFieldValueParser.GetValue(dialog, "uiCreateEntryName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiEntrySections"), "Notes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiEntryDetails"), "Operation | Create entry");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiEntryDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiEntryNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual("Add & More", dialog.Actions.Single(action => string.Equals(action.Id, "add_more", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_move_up_uses_receipt_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("move_up", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.move_up", dialog.Id);
        Assert.AreEqual("Move Up", DesktopDialogFieldValueParser.GetValue(dialog, "uiActionLabel"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiActionDetails"), "one position higher");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiActionImpact"), "List Context | preserved");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiActionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiActionImpact", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiActionNotes", StringComparison.Ordinal)).VisualKind);
    }

    [TestMethod]
    public void CreateUiControlDialog_toggle_free_paid_uses_receipt_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("toggle_free_paid", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.toggle_free_paid", dialog.Id);
        Assert.AreEqual("Toggle Free/Paid", DesktopDialogFieldValueParser.GetValue(dialog, "uiActionLabel"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiActionSections"), "Impact");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiActionImpact"), "Next step | continue in the same section");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiActionNotes"), "Pricing state changes remain compact");
    }

    [TestMethod]
    public void CreateUiControlDialog_gear_delete_uses_impact_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("gear_delete", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.gear_delete", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteSections"), "Impact");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "Armor Jacket");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "> Armor Jacket");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteImpact"), "Undo Posture | re-add from gear selector");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Return to gear tab");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "uiDeleteNavigationTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.List, dialog.Fields.Single(field => string.Equals(field.Id, "uiDeleteRecoveryCommands", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiDeleteImpact", StringComparison.Ordinal)).VisualKind);
    }

    [TestMethod]
    public void CreateUiControlDialog_cyberware_delete_uses_legacy_delete_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("cyberware_delete", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.cyberware_delete", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "Cybereyes Rating 4");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "Datajack");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteSummary"), "Essence | 0.40");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteImpact"), "Undo Posture | re-add from selector");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Return to cyberware tab");
    }

    [TestMethod]
    public void CreateUiControlDialog_vehicle_delete_uses_legacy_delete_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("vehicle_delete", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.vehicle_delete", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "GMC Roadmaster");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "MCT Fly-Spy");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteSummary"), "Seats | 6");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Return to vehicle tab");
    }

    [TestMethod]
    public void CreateUiControlDialog_skill_remove_uses_legacy_delete_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("skill_remove", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.skill_remove", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "Perception");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "Sneaking");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteSummary"), "Linked Attribute | Intuition");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Return to skills tab");
    }

    [TestMethod]
    public void CreateUiControlDialog_delete_entry_uses_legacy_delete_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("delete_entry", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.delete_entry", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "Current Entry");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "Next Entry");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteImpact"), "Focus | selection moves to adjacent entry");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Re-open Add Entry");
    }

    [TestMethod]
    public void CreateUiControlDialog_drug_delete_uses_legacy_delete_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("drug_delete", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.drug_delete", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "Jazz");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "Kamikaze");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteSummary"), "Crash | Stun + fatigue");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Return to drugs tab");
    }

    [TestMethod]
    public void CreateUiControlDialog_magic_delete_uses_legacy_delete_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("magic_delete", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.magic_delete", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "Stunbolt");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "Increase Reflexes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteSummary"), "Drain | F-3");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Return to magic tab");
    }

    [TestMethod]
    public void CreateUiControlDialog_contact_remove_uses_legacy_delete_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("contact_remove", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.contact_remove", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "Mr. Johnson");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "Nyx");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteSummary"), "Connection / Loyalty | 5 / 3");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Return to contacts tab");
    }

    [TestMethod]
    public void CreateUiControlDialog_quality_delete_uses_legacy_delete_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("quality_delete", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.quality_delete", dialog.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNavigationTree"), "First Impression");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteNeighborList"), "Distinctive Style");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteSummary"), "Karma | 11");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDeleteRecoveryCommands"), "Return to qualities tab");
    }

    [TestMethod]
    public void CreateUiControlDialog_all_catalog_controls_use_dedicated_dialog_shapes()
    {
        DesktopDialogFactory factory = new();

        foreach (string controlId in LegacyUiControlCatalog.All)
        {
            DesktopDialogState dialog = factory.CreateUiControlDialog(controlId, DesktopPreferenceState.Default);

            Assert.AreEqual($"dialog.ui.{controlId}", dialog.Id, $"Unexpected dialog id for control '{controlId}'.");
            Assert.AreNotEqual("dialog.ui.generic", dialog.Id, $"Control '{controlId}' fell back to the generic dialog.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(dialog.Title), $"Control '{controlId}' must keep a dialog title.");
            Assert.IsTrue(dialog.Fields.Count > 0, $"Control '{controlId}' must surface at least one dialog field.");
            Assert.IsTrue(dialog.Actions.Count > 0, $"Control '{controlId}' must surface at least one dialog action.");
        }
    }

    [TestMethod]
    public void DialogFieldValueParser_normalizes_and_parses_checkbox_values()
    {
        DesktopDialogField checkboxField = new(
            "globalCompactMode",
            "Compact Mode",
            "false",
            "false",
            InputType: "checkbox");

        string normalized = DesktopDialogFieldValueParser.Normalize(checkboxField, "on");
        DesktopDialogState dialog = new(
            "dialog.test",
            "Test",
            null,
            [checkboxField with { Value = normalized }],
            [new DesktopDialogAction("close", "Close", true)]);

        Assert.AreEqual("true", normalized);
        Assert.IsTrue(DesktopDialogFieldValueParser.ParseBool(dialog, "globalCompactMode", false));
    }

    [TestMethod]
    public void CreateCommandDialog_open_character_uses_import_template()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "open_character",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: "sr6");

        Assert.AreEqual("dialog.open_character", dialog.Id);
        Assert.IsNotNull(dialog.Fields.SingleOrDefault(field => string.Equals(field.Id, "openCharacterXml", StringComparison.Ordinal)));
        Assert.AreEqual("sr6", DesktopDialogFieldValueParser.GetValue(dialog, "importRulesetId"));
        Assert.IsNotNull(dialog.Actions.SingleOrDefault(action => string.Equals(action.Id, "import", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateCommandDialog_hero_lab_importer_uses_xml_compatibility_fields()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "hero_lab_importer",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: "sr6");

        Assert.AreEqual("dialog.hero_lab_importer", dialog.Id);
        Assert.IsNotNull(dialog.Fields.SingleOrDefault(field => string.Equals(field.Id, "heroLabXml", StringComparison.Ordinal)));
        Assert.AreEqual("sr6", DesktopDialogFieldValueParser.GetValue(dialog, "importRulesetId"));
        Assert.IsNotNull(dialog.Actions.SingleOrDefault(action => string.Equals(action.Id, "import", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateCommandDialog_switch_ruleset_uses_ruleset_selection_template()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "switch_ruleset",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: "sr6");

        Assert.AreEqual("dialog.switch_ruleset", dialog.Id);
        Assert.AreEqual("sr6", DesktopDialogFieldValueParser.GetValue(dialog, "preferredRulesetId"));
        Assert.IsNotNull(dialog.Actions.SingleOrDefault(action => string.Equals(action.Id, "apply_ruleset", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateCommandDialog_runtime_inspector_uses_runtime_projection_details()
    {
        DesktopDialogFactory factory = new();
        RuntimeInspectorProjection projection = new(
            TargetKind: RuntimeInspectorTargetKinds.RuntimeLock,
            TargetId: "official.sr5.core",
            RuntimeLock: new ResolvedRuntimeLock(
                RulesetId: RulesetDefaults.Sr5,
                ContentBundles:
                [
                    new ContentBundleDescriptor("sr5.core.bundle", RulesetDefaults.Sr5, "1.0.0", "SR5 Core", "Core bundle", ["data/core.xml"])
                ],
                RulePacks:
                [
                    new ArtifactVersionReference("official.sr5.core", "1.0.0")
                ],
                ProviderBindings: new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [RulePackCapabilityIds.DeriveStat] = "official.sr5.core/derive.stat"
                },
                EngineApiVersion: "1.0.0",
                RuntimeFingerprint: "sha256:sr5-runtime-fingerprint"),
            Install: new ArtifactInstallState(ArtifactInstallStates.Available, RuntimeFingerprint: "sha256:sr5-runtime-fingerprint"),
            ResolvedRulePacks:
            [
                new RuntimeInspectorRulePackEntry(
                    new ArtifactVersionReference("official.sr5.core", "1.0.0"),
                    "SR5 Core",
                    ArtifactVisibilityModes.LocalOnly,
                    ArtifactTrustTiers.Official,
                    [RulePackCapabilityIds.DeriveStat],
                    SourceKind: RegistryEntrySourceKinds.BuiltInCoreProfile)
            ],
            ProviderBindings:
            [
                new RuntimeInspectorProviderBinding(RulePackCapabilityIds.DeriveStat, "official.sr5.core/derive.stat", "official.sr5.core")
            ],
            CompatibilityDiagnostics:
            [
                new RuntimeLockCompatibilityDiagnostic(RuntimeLockCompatibilityStates.Compatible, "Compatible", RulesetDefaults.Sr5, "sha256:sr5-runtime-fingerprint")
            ],
            Warnings: [],
            MigrationPreview:
            [
                new RuntimeMigrationPreviewItem(RuntimeMigrationPreviewChangeKinds.RulePackAdded, "Profile applies RulePack 'official.sr5.core@1.0.0'.")
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
                    ProviderId: "official.sr5.core/derive.stat",
                    PackId: "official.sr5.core")
            ]);

        DesktopDialogState dialog = factory.CreateCommandDialog(
            OverviewCommandPolicy.RuntimeInspectorCommandId,
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5,
            runtimeInspector: projection);

        Assert.AreEqual("dialog.runtime_inspector", dialog.Id);
        Assert.AreEqual("official.sr5.core", DesktopDialogFieldValueParser.GetValue(dialog, "runtimeProfileId"));
        Assert.AreEqual(RegistryEntrySourceKinds.BuiltInCoreProfile, DesktopDialogFieldValueParser.GetValue(dialog, "runtimeProfileSource"));
        Assert.AreEqual("sha256:sr5-runtime-fingerprint", DesktopDialogFieldValueParser.GetValue(dialog, "runtimeFingerprint"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "runtimeProfileDiagnostics"), "Session-safe bindings");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "runtimeHubClientDiagnostics"), "Hub-origin RulePacks");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "runtimeProviderBindings"), "derive.stat");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "runtimeCapabilities"), "derive.stat");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "runtimeRulePacks"), RegistryEntrySourceKinds.BuiltInCoreProfile);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "runtimeRulePackDiagnostics"), "bindings=1");
    }

    [TestMethod]
    public void CreateExplainTraceDialog_uses_shared_localized_trace_contract()
    {
        DesktopDialogFactory factory = new();
        MapExplainTextLocalization localization = new(
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["explain.chrome.title"] = "Explain Trace",
                ["explain.chrome.empty"] = "No explain payload is available for this selection.",
                ["explain.chrome.value_label"] = "Value",
                ["explain.chrome.missing_value"] = "(none)",
                ["explain.chrome.reason_label"] = "Reason",
                ["explain.chrome.provider_label"] = "Provider",
                ["explain.chrome.capability_label"] = "Capability",
                ["explain.chrome.pack_label"] = "Pack",
                ["explain.chrome.trace_steps_label"] = "Trace Steps",
                ["explain.chrome.diff_label"] = "What Changed",
                ["explain.chrome.before_label"] = "Before",
                ["explain.chrome.after_label"] = "After",
                ["explain.chrome.close_action"] = "Close",
                ["subject.desktop.trace"] = "Desktop Trace",
                ["provider.runtime"] = "Runtime Evaluator",
                ["capability.skill_check"] = "Skill Check",
                ["pack.street_magic"] = "Street Magic Overlay",
                ["message.desktop.trace"] = "Provider delta applied from the active rule bundle.",
                ["fragment.label.base"] = "Base",
                ["fragment.label.modified"] = "Modified",
                ["fragment.reason.active_traits"] = "Active traits and status modifiers are included."
            },
            throwOnMissing: true);

        LocalizedRulesetExplainTrace trace = RulesetExplainRenderer.Project(
            RulesetExplainContractFactory.CreateTrace(
                new ExplainTraceSeed(
                    Subject: new ExplainTextSeed("subject.desktop.trace"),
                    Providers:
                    [
                        new ExplainProviderSeed(
                            Provider: new ExplainProvenanceSeed("provider.runtime", new ExplainTextSeed("provider.runtime")),
                            Capability: new ExplainProvenanceSeed("capability.skill_check", new ExplainTextSeed("capability.skill_check")),
                            Pack: new ExplainProvenanceSeed("pack.street_magic", new ExplainTextSeed("pack.street_magic")),
                            Success: true,
                            Fragments:
                            [
                                new ExplainFragmentSeed(
                                    Label: new ExplainTextSeed("fragment.label.base"),
                                    Value: "10",
                                    Reason: new ExplainTextSeed("fragment.reason.active_traits"),
                                    Pack: new ExplainProvenanceSeed("pack.street_magic", new ExplainTextSeed("pack.street_magic")),
                                    Provider: new ExplainProvenanceSeed("provider.runtime", new ExplainTextSeed("provider.runtime"))),
                                new ExplainFragmentSeed(
                                    Label: new ExplainTextSeed("fragment.label.modified"),
                                    Value: "12",
                                    Reason: new ExplainTextSeed("fragment.reason.active_traits"),
                                    Pack: new ExplainProvenanceSeed("pack.street_magic", new ExplainTextSeed("pack.street_magic")),
                                    Provider: new ExplainProvenanceSeed("provider.runtime", new ExplainTextSeed("provider.runtime")))
                            ],
                            GasUsage: new RulesetGasUsage(1, 2, 64),
                            Messages:
                            [
                                new ExplainTextSeed("message.desktop.trace")
                            ])
                    ],
                    Messages: [],
                    AggregateGasUsage: new RulesetGasUsage(1, 2, 64))),
            localization)!;
        LocalizedExplainChrome chrome = RulesetExplainRenderer.CreateChrome(localization);

        DesktopDialogState dialog = factory.CreateExplainTraceDialog(trace, chrome);

        Assert.AreEqual("dialog.explain_trace", dialog.Id);
        Assert.AreEqual("Explain Trace", dialog.Title);
        Assert.AreEqual("Desktop Trace", dialog.Message);
        Assert.AreEqual("Close", dialog.Actions[0].Label);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "explainTraceBody"), "What Changed:");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "explainTraceBody"), "Base: Before 10 -> After 12");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "explainTraceBody"), "Trace Steps:");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "explainTraceBody"), "Provider: Runtime Evaluator");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "explainTraceBody"), "Pack: Street Magic Overlay");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "explainTraceBody"), "Reason: Active traits and status modifiers are included.");
    }

    private static CharacterProfileSection CreateProfile(string name, string alias)
    {
        return new CharacterProfileSection(
            Name: name,
            Alias: alias,
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
    }

    private static MasterIndexResponse CreateMasterIndexResponse()
    {
        return new MasterIndexResponse(
            Count: 4,
            GeneratedUtc: DateTimeOffset.UtcNow,
            Files:
            [
                new MasterIndexFileEntry("armor.xml", "chummer", 18),
                new MasterIndexFileEntry("books.xml", "chummer", 42),
                new MasterIndexFileEntry("weapons.xml", "chummer", 27)
            ],
            ReferenceLanePosture: "governed",
            SourcebookCount: 12,
            Sourcebooks:
            [
                new MasterIndexSourcebookEntry(
                    Id: "core-rulebook",
                    Code: "CRB",
                    Name: "Core Rulebook",
                    Permanent: true,
                    ReferencePosture: "governed",
                    RuleSnippetCount: 16,
                    RuleSnippets:
                    [
                        new MasterIndexRuleSnippetEntry(
                            Language: "en-us",
                            Page: 20,
                            Snippet: "Reference notes stay in this pane while the selected entry remains visible.",
                            Provenance: "books.xml"),
                        new MasterIndexRuleSnippetEntry(
                            Language: "en-us",
                            Page: 21,
                            Snippet: "Indexed source detail remains on the right, matching the legacy utility posture.",
                            Provenance: "books.xml")
                    ],
                    ReferenceSourcePosture: "governed",
                    LocalPdfPath: "/books/core-rulebook.pdf",
                    ReferenceUrl: "https://example.test/core-rulebook"),
                new MasterIndexSourcebookEntry(
                    Id: "street-wyrd",
                    Code: "SW",
                    Name: "Street Wyrd",
                    Permanent: false,
                    ReferencePosture: "stale",
                    RuleSnippetCount: 6,
                    RuleSnippets: [],
                    ReferenceSourcePosture: "stale",
                    ReferenceSnapshot: "https://example.test/snapshots/street-wyrd")
            ],
            ReferenceCoveragePercent: 67,
            SourcebooksWithSnippets: 8,
            ReferenceSourceLanePosture: "governed",
            SourcebooksWithGovernedReferenceSources: 9,
            SourcebooksWithStaleReferenceSources: 2,
            SourcebooksMissingReferenceSources: 1,
            ReferenceSourceLaneReceipt: "all sourcebooks expose governed PDF/URL/site-snapshot references.",
            SettingsLanePosture: "governed",
            SettingsProfileCount: 6,
            SettingsProfilesWithSourceToggles: 5,
            DistinctSourcebookToggles: 24,
            SourceToggleLanePosture: "governed",
            SourceSelectionLaneReceipt: "sourcebook selection is governed by 24 toggles across 12 sourcebooks (67% coverage).",
            SourcebookToggleCoveragePercent: 67,
            CustomDataLanePosture: "partial",
            CustomDataLaneReceipt: "custom-data lane is partial: 2 configured custom-data directories stay governed but not yet fully automated.",
            CustomDataAuthoringLaneReceipt: "custom-data authoring is partial: 2 configured custom-data directories with stale overlay bridge posture.",
            SettingsProfilesWithCustomDataDirectories: 2,
            DistinctCustomDataDirectoryCount: 2,
            XmlBridgePosture: "governed",
            XmlBridgeLaneReceipt: "xml bridge is governed: 2 enabled data overlays expose XML payloads.",
            EnabledDataOverlayCount: 2,
            TranslatorLanePosture: "governed",
            TranslatorLaneReceipt: "translator lane is governed: 6 translator corpus files and 3 enabled language overlays.",
            TranslatorBridgePosture: "governed",
            TranslatorLanguageCount: 6,
            EnabledLanguageOverlayCount: 3,
            OnlineStorageLanePosture: "partial",
            OnlineStorageReceiptPosture: "stale",
            OnlineStorageLaneReceipt: "online storage lane is partial: 1/2 continuity receipts are current with stale release proof on one required host lane.",
            OnlineStorageReceiptsCovered: 1,
            OnlineStorageReceiptsExpected: 2,
            OnlineStorageCoveragePercent: 50,
            ImportOracleLanePosture: "partial",
            ImportOracleReceiptPosture: "stale",
            LegacyChummer4FixtureCount: 18,
            LegacyChummer5FixtureCount: 31,
            HeroLabFixtureCount: 0,
            AdjacentSr6OracleReceiptPosture: "partial",
            AdjacentSr6OracleSourcesCovered: 1,
            AdjacentSr6OracleSourcesExpected: 2,
            ImportOracleSourcesCovered: 3,
            ImportOracleSourcesExpected: 4,
            ImportOracleCoveragePercent: 75,
            ImportOracleMissingSources: ["Hero Lab"],
            ImportOracleLaneReceipt: "import oracle is partial: 3/4 fixture families covered (missing: Hero Lab), adjacent SR6 oracle coverage 1/2.",
            AdjacentSr6OracleLaneReceipt: "adjacent SR6 oracle lane is partial: 1/2 covered with stale receipts for Genesis/CommLink.",
            Sr6SupplementLanePosture: "partial",
            Sr6DesignerToolsPosture: "partial",
            Sr6DesignerFamiliesAvailable: 4,
            Sr6DesignerFamiliesExpected: 5,
            HouseRuleLanePosture: "governed",
            HouseRuleOverlayCount: 3,
            Sr6SuccessorLaneReceipt: "sr6 successor lane is partial: supplement/governed designers/house-rule posture remains mixed.");
    }

    private static DesktopDialogState WithFieldValues(DesktopDialogState dialog, params (string FieldId, string Value)[] replacements)
    {
        return dialog with
        {
            Fields = dialog.Fields
                .Select(field =>
                {
                    foreach ((string fieldId, string value) in replacements)
                    {
                        if (string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                        {
                            return field with
                            {
                                Value = value,
                                Placeholder = value
                            };
                        }
                    }

                    return field;
                })
                .ToArray()
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
}
