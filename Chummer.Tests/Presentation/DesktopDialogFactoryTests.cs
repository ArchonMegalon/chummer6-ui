using System;
using System.Linq;
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsSections"), "Sourcebooks");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsTree"), "Data Paths");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalSettingsPropertyGrid"), "Scale | 125%");
        Assert.AreEqual("/Characters", DesktopDialogFieldValueParser.GetValue(dialog, "globalCharacterRosterPath"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalCurrentPaneNotes"), "old utility settings form");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "globalVisibilityPolicy"), "status strip");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tabs, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsSections", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsPropertyGrid", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsTree", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "globalSettingsPropertyGrid", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Summary, dialog.Fields.Single(field => string.Equals(field.Id, "globalVisibilityPolicy", StringComparison.Ordinal)).VisualKind);
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiLinkDetails"), "Action: open in browser");
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "updateSections"), "Support");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "updateDetails"), "Support Path: /account/support");
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSections"), "SR6 Successor");
        Assert.AreEqual("CRB · Core Rulebook", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCurrentSourcebook"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCatalogEntries"), "Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCatalogEntries"), "Street Wyrd");
        Assert.AreEqual("/books/core-rulebook.pdf", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSelectedSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexDetails"), "Reference posture | governed");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSnippetPreview"), "No governed rule snippets");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCharacterSetting"), "Source toggles: governed");
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexCatalogEntries", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexCatalogEntries", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "masterIndexSnippetPreview", StringComparison.Ordinal)).VisualKind);
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

        DesktopDialogState dialog = factory.CreateCommandDialog(
            "character_roster",
            profile: CreateProfile("Fallback Runner", "FALL"),
            DesktopPreferenceState.Default,
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSectionTabs"), "Notes");
        Assert.AreEqual("1", DesktopDialogFieldValueParser.GetValue(dialog, "rosterSavedCount"));
        Assert.AreEqual("sr6, sr5", DesktopDialogFieldValueParser.GetValue(dialog, "rosterRulesetMix"));
        Assert.AreEqual("ws-2", DesktopDialogFieldValueParser.GetValue(dialog, "rosterActiveWorkspace"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterTree"), "* GST · Ghost");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunner"), "Ghost (GST)");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterMugshot"), "GST · Ghost");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerStatus"), "active ruleset sr6");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerNotes"), "dense workbench");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Tree, dialog.Fields.Single(field => string.Equals(field.Id, "rosterTree", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectedRunner", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Image, dialog.Fields.Single(field => string.Equals(field.Id, "rosterMugshot", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectedRunnerNotes", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "rosterTree", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "rosterSelectedRunner", StringComparison.Ordinal)).LayoutSlot);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterEntries"), "GST · Ghost · unsaved · sr6");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "rosterEntries"), "APX · Apex · saved · sr5");
    }

    [TestMethod]
    public void CreateUiControlDialog_cyberware_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("cyberware_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.cyberware_add", dialog.Id);
        Assert.AreEqual("Wired Reflexes 2", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareName"));
        Assert.AreEqual("Bodyware", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCategory"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSections"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCategoryTree"), "Cyberlimbs");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCandidateList"), "Cybereyes Rating 4");
        Assert.AreEqual("Core Rulebook p. 461", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSelectionDetails"), "Availability: 12R");
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_gear_add_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.gear_add", dialog.Id);
        Assert.AreEqual("Ares Predator V", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSections"), "Details");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCategoryTree"), "Electronics");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCandidateList"), "Armor Jacket");
        Assert.AreEqual("Core Rulebook p. 424", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSource"));
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditDetails"), "Availability | 12");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearEditDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiGearEditNotes", StringComparison.Ordinal)).VisualKind);
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditDetails"), "Essence | 0.40");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareEditDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiCyberwareEditNotes", StringComparison.Ordinal)).VisualKind);
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellCategoryTree"), "Illusion");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellCandidateList"), "Improved Invisibility");
        Assert.AreEqual("Core Rulebook p. 288", DesktopDialogFieldValueParser.GetValue(dialog, "uiSpellSource"));
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillCategoryTree"), "Language");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillCandidateList"), "Sneaking");
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugCategoryTree"), "Stimulants");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugCandidateList"), "Cram");
        Assert.AreEqual("Core Rulebook p. 411", DesktopDialogFieldValueParser.GetValue(dialog, "uiDrugSource"));
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_contact_add_uses_dense_contact_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("contact_add", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.contact_add", dialog.Id);
        Assert.AreEqual("Dr. Mercy", DesktopDialogFieldValueParser.GetValue(dialog, "uiContactName"));
        Assert.AreEqual("Street Doc", DesktopDialogFieldValueParser.GetValue(dialog, "uiContactRole"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiContactDetails"), "Connection/Loyalty: 3 / 2");
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_contact_edit_uses_dense_contact_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("contact_edit", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.contact_edit", dialog.Id);
        Assert.AreEqual("Mr. Johnson", DesktopDialogFieldValueParser.GetValue(dialog, "uiContactEditName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiContactEditDetails"), "Connection/Loyalty: 5 / 3");
        Assert.AreEqual("Apply", dialog.Actions.Single(action => string.Equals(action.Id, "apply", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_contact_connection_uses_dense_connection_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("contact_connection", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.contact_connection", dialog.Id);
        Assert.AreEqual("Mr. Johnson", DesktopDialogFieldValueParser.GetValue(dialog, "uiContactConnectionName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiContactConnectionDetails"), "Current Connection/Loyalty: 5 / 3");
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramCategoryTree"), "Dongles");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramCandidateList"), "Baby Monitor");
        Assert.AreEqual("Data Trails p. 60", DesktopDialogFieldValueParser.GetValue(dialog, "uiMatrixProgramSource"));
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiInitiationCategoryTree"), "Echos");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiInitiationCandidateList"), "Centering");
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpiritCategoryTree"), "Ally");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSpiritCandidateList"), "Air Spirit");
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCritterPowerCategoryTree"), "Combat");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiCritterPowerCandidateList"), "Elemental Attack");
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityCategoryTree"), "Negative");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityCandidateList"), "Toughness");
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_skill_specialize_uses_dense_specialization_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("skill_specialize", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.skill_specialize", dialog.Id);
        Assert.AreEqual("Perception", DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSpecializationSkill"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSpecializationDetails"), "Existing Specializations: Audio");
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleCategoryTree"), "Drones");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleCandidateList"), "GMC Roadmaster");
        Assert.AreEqual("Core Rulebook p. 465", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSource"));
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditDetails"), "Seats | 6");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Grid, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleEditDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiVehicleEditNotes", StringComparison.Ordinal)).VisualKind);
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
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponCategoryTree"), "Heavy Pistols");
        Assert.AreEqual("Core Rulebook p. 424", DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponCandidateList"), "Ares Alpha");
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiWeaponSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
    }

    [TestMethod]
    public void CreateUiControlDialog_combat_add_armor_uses_selection_form_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("combat_add_armor", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.combat_add_armor", dialog.Id);
        Assert.AreEqual("Armor Jacket", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorName"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSections"), "Browse");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorCategoryTree"), "Clothing");
        Assert.AreEqual("Core Rulebook p. 436", DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSource"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorCandidateList"), "Actioneer Business Clothes");
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Left, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorCandidateList", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual(DesktopDialogFieldLayoutSlots.Right, dialog.Fields.Single(field => string.Equals(field.Id, "uiArmorSelectionDetails", StringComparison.Ordinal)).LayoutSlot);
        Assert.AreEqual("OK", dialog.Actions.Single(action => string.Equals(action.Id, "add", StringComparison.Ordinal)).Label);
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
    }

    [TestMethod]
    public void CreateUiControlDialog_move_up_uses_receipt_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("move_up", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.move_up", dialog.Id);
        Assert.AreEqual("Move Up", DesktopDialogFieldValueParser.GetValue(dialog, "uiActionLabel"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiActionDetails"), "one position higher");
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiActionDetails", StringComparison.Ordinal)).VisualKind);
        Assert.AreEqual(DesktopDialogFieldVisualKinds.Snippet, dialog.Fields.Single(field => string.Equals(field.Id, "uiActionNotes", StringComparison.Ordinal)).VisualKind);
    }

    [TestMethod]
    public void CreateUiControlDialog_toggle_free_paid_uses_receipt_posture()
    {
        DesktopDialogFactory factory = new();

        DesktopDialogState dialog = factory.CreateUiControlDialog("toggle_free_paid", DesktopPreferenceState.Default);

        Assert.AreEqual("dialog.ui.toggle_free_paid", dialog.Id);
        Assert.AreEqual("Toggle Free/Paid", DesktopDialogFieldValueParser.GetValue(dialog, "uiActionLabel"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiActionSections"), "Receipt");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "uiActionNotes"), "Pricing state changes remain compact");
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
            Files: [],
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
                    RuleSnippets: [],
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
            CustomDataAuthoringLaneReceipt: "custom-data authoring is partial: 2 configured custom-data directories with stale overlay bridge posture.",
            XmlBridgePosture: "governed",
            XmlBridgeLaneReceipt: "xml bridge is governed: 2 enabled data overlays expose XML payloads.",
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
}
