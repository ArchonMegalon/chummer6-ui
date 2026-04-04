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
        Assert.AreEqual("true", DesktopDialogFieldValueParser.GetValue(dialog, "globalCompactMode"));
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
        Assert.AreEqual("12", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourcebooks"));
        Assert.AreEqual("67% (8/12)", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexReferenceCoverage"));
        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSettingsLane"));
        Assert.AreEqual("all sourcebooks expose governed PDF/URL/site-snapshot references.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexReferenceSourceReceipt"));
        Assert.AreEqual("sourcebook selection is governed by 24 toggles across 12 sourcebooks (67% coverage).", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSourceSelectionReceipt"));
        Assert.AreEqual("custom-data authoring is partial: 2 configured custom-data directories with stale overlay bridge posture.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCustomDataAuthoringReceipt"));
        Assert.AreEqual("xml bridge is governed: 2 enabled data overlays expose XML payloads.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexXmlBridgeReceipt"));
        Assert.AreEqual("translator lane is governed: 6 translator corpus files and 3 enabled language overlays.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexTranslatorReceipt"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexImportOracleLane"), "75%");
        Assert.AreEqual("import oracle is partial: 3/4 fixture families covered (missing: Hero Lab), adjacent SR6 oracle coverage 1/2.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexImportOracleReceipt"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexAdjacentSr6OracleLane"), "1/2");
        Assert.AreEqual("adjacent SR6 oracle lane is partial: 1/2 covered with stale receipts for Genesis/CommLink.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexAdjacentSr6OracleReceipt"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexOnlineStorageLane"), "50%");
        Assert.AreEqual("online storage lane is partial: 1/2 continuity receipts are current with stale release proof on one required host lane.", DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexOnlineStorageReceipt"));
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
            Sourcebooks: [],
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
            Sr6DesignerFamiliesAvailable: 4,
            Sr6DesignerFamiliesExpected: 5,
            HouseRuleLanePosture: "governed",
            Sr6SuccessorLaneReceipt: "sr6 successor lane is partial: supplement/governed designers/house-rule posture remains mixed.");
    }
}
