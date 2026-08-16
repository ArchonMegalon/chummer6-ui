using Chummer.Contracts.Characters;
using Chummer.Contracts.Api;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Explain;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Chummer.Presentation.Overview;

public sealed partial class DesktopDialogFactory : IDesktopDialogFactory
{
    private const string NewCharacterPriorityWorkflowDialogId = "dialog.new_character.priority_workflow";
    private const string NewCharacterKarmaWorkflowDialogId = "dialog.new_character.karma_workflow";
    private const string NewCharacterOriginWizardDialogId = "dialog.new_character.origin_wizard";
    private const string NewCharacterOriginBuildDialogId = "dialog.new_character.origin_build";
    private const string OriginDossierOnlineRoute = "/app";
    private const string NewCharacterPriorityWorkflowStateFieldId = "newCharacterPriorityWorkflowState";
    private const string NewCharacterPriorityLastChangedFieldId = "newCharacterPriorityLastChangedFieldId";
    private const string NewCharacterMetavariantFieldId = "newCharacterMetavariant";
    private const string NewCharacterPrioritySkillChoice1FieldId = "newCharacterPrioritySkillChoice1";
    private const string NewCharacterPrioritySkillChoice2FieldId = "newCharacterPrioritySkillChoice2";
    private const string NewCharacterPrioritySkillChoice3FieldId = "newCharacterPrioritySkillChoice3";
    private const string NewCharacterPriorityWorkflowCanCommitFieldId = "newCharacterPriorityWorkflowCanCommit";

    public DesktopDialogState CreateExplainTraceDialog(
        LocalizedRulesetExplainTrace? trace,
        LocalizedExplainChrome chrome)
    {
        string renderedTrace = RulesetExplainTextFormatter.Format(trace, chrome);

        return new DesktopDialogState(
            Id: "dialog.explain_trace",
            Title: chrome.Title.Text,
            Message: trace?.SubjectId.Text,
            Fields:
            [
                new DesktopDialogField(
                    Id: "explainTraceBody",
                    Label: chrome.Title.Text,
                    Value: renderedTrace,
                    Placeholder: renderedTrace,
                    IsReadOnly: true,
                    IsMultiline: true)
            ],
            Actions:
            [
                new DesktopDialogAction("close", chrome.CloseAction.Text, true)
            ]);
    }

    public DesktopDialogState CreateMetadataDialog(
        CharacterProfileSection? profile,
        DesktopPreferenceState preferences)
    {
        return new DesktopDialogState(
            Id: "dialog.workspace.metadata",
            Title: "Edit Metadata",
            Message: "Apply dossier profile metadata changes to the active dossier.",
            Fields:
            [
                new DesktopDialogField("metadataName", "Name", profile?.Name ?? string.Empty, "Character Name"),
                new DesktopDialogField("metadataAlias", "Alias", profile?.Alias ?? string.Empty, "Street Name"),
                new DesktopDialogField("metadataNotes", "Notes", preferences.CharacterNotes, "Notes", true)
            ],
            Actions:
            [
                new DesktopDialogAction("apply_metadata", "Apply", true),
                new DesktopDialogAction("cancel", "Cancel")
            ]);
    }

    public DesktopDialogState CreateCommandDialog(
        string commandId,
        CharacterProfileSection? profile,
        DesktopPreferenceState preferences,
        string? activeSectionJson,
        CharacterWorkspaceId? currentWorkspace,
        string? rulesetId,
        string? activeSectionId = null,
        string? activeDialogId = null,
        RuntimeInspectorProjection? runtimeInspector = null,
        MasterIndexResponse? masterIndex = null,
        TranslatorLanguagesResponse? translatorLanguages = null,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces = null)
    {
        string language = DesktopLocalizationCatalog.NormalizeOrDefault(preferences.Language);
        string S(string key) => DesktopLocalizationCatalog.GetRequiredString(key, language);
        string F(string key, params object[] values) => DesktopLocalizationCatalog.GetRequiredFormattedString(key, language, values);
        string name = profile?.Name ?? "(none)";
        string alias = profile?.Alias ?? string.Empty;
        string workspace = currentWorkspace?.Value ?? "(none)";

        return HumanizeVisibleDialog(commandId switch
        {
            OverviewCommandPolicy.RuntimeInspectorCommandId when runtimeInspector is not null => CreateRuntimeInspectorDialog(runtimeInspector),
            "open_character" => CreateOpenCharacterDialog(
                "dialog.open_character",
                "Open Dossier",
                "Paste Chummer XML to open a dossier.",
                rulesetId),
            "open_for_printing" => CreateOpenCharacterDialog(
                "dialog.open_for_printing",
                "Open Print Staging",
                "Paste Chummer XML to stage dossier print workflows.",
                rulesetId),
            "open_for_export" => CreateOpenCharacterDialog(
                "dialog.open_for_export",
                "Open Export Staging",
                "Paste Chummer XML to stage dossier export workflows.",
                rulesetId),
            "new_character" => BuildNewCharacterDialog(preferences, rulesetId),
            "new_character_origin" => BuildNewCharacterOriginWizardDialog(
                rulesetId,
                profile?.Name,
                profile?.Alias,
                preferences),
            "print_setup" => new DesktopDialogState(
                "dialog.print_setup",
                "Print Setup",
                "Printer setup is delegated to host/browser print capabilities.",
                [
                    new DesktopDialogField("printLandscape", "Landscape", "false", "false", InputType: "checkbox"),
                    new DesktopDialogField("printBackground", "Print background graphics", "true", "true", InputType: "checkbox")
                ],
                [
                    new DesktopDialogAction("ok", "OK", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "dice_roller" => new DesktopDialogState(
                "dialog.dice_roller",
                "Dice Roller",
                "Choose a roll method, threshold, and reroll options.",
                BuildDiceToolFields(currentWorkspace, openWorkspaces, rulesetId),
                [
                    new DesktopDialogAction("roll", "Roll", true),
                    new DesktopDialogAction("reroll_misses", "Re-Roll Misses"),
                    new DesktopDialogAction("close", "Close")
                ]),
            "global_settings" => BuildGlobalSettingsDialog(preferences, language),
            "switch_ruleset" => new DesktopDialogState(
                "dialog.switch_ruleset",
                "Switch Ruleset",
                "Set the preferred ruleset used when no workspace is active.",
                [
                    CreateRulesetField("preferredRulesetId", rulesetId)
                ],
                [
                    new DesktopDialogAction("apply_ruleset", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            DesktopAliceAssistant.CommandId => DesktopAliceAssistant.CreateDialog(
                activeSectionId,
                activeDialogId,
                activeSectionJson,
                currentWorkspace,
                rulesetId),
            "character_settings" => BuildCharacterSettingsDialog(preferences),
            "translator" => new DesktopDialogState(
                "dialog.translator",
                S("desktop.dialog.translator.title"),
                F("desktop.dialog.translator.message", DesktopLocalizationCatalog.BuildSupportedLanguageCodeSummary())
                + " Language search and enabled overlays remain visible.",
                BuildTranslatorFields(language, masterIndex, translatorLanguages),
                [new DesktopDialogAction("close", S("desktop.dialog.action.close"), true)]),
            "open_sourcebooks" => CreateGovernedUtilityDialog(
                "dialog.open_sourcebooks",
                "Sourcebooks",
                "Review governed sourcebook coverage and linked references without leaving the shared shell.",
                "Reference Surface",
                "Master Index keeps sourcebook coverage, linked PDFs, and governed reference posture visible together.",
                "Open Master Index when you need search, source toggles, or linked reference receipts."),
            "open_errata" => CreateGovernedUtilityDialog(
                "dialog.open_errata",
                "Errata",
                "Errata references stay in the governed rules and reference lane rather than a detached shell surface.",
                "Errata Surface",
                "Keep sourcebook context and errata follow-through together in the shared reference lane.",
                "Open Master Index before you jump to external errata references so the current source context stays visible."),
            "open_custom_data" => CreateGovernedUtilityDialog(
                "dialog.open_custom_data",
                "Custom Data",
                "Custom data stays governed through the XML and overlay lane instead of mutating the runner shell directly.",
                "Custom Data Posture",
                "XML Editor tracks overlay directories, authoring posture, and XML bridge receipts together.",
                "Open XML Editor when you need overlay directory counts, authoring receipts, or bridge posture."),
            "update_data_packs" => CreateGovernedUtilityDialog(
                "dialog.update_data_packs",
                "Update Data Packs",
                "Data pack refresh stays governed through the XML and custom-data lane.",
                "Update Posture",
                "Refreshes should follow the governed XML bridge and custom-data posture instead of bypassing shared verification surfaces.",
                "Review XML Editor and governed release receipts before you refresh external data packs."),
            "validate_data_scope" => CreateGovernedUtilityDialog(
                "dialog.validate_data_scope",
                "Validate Data Scope",
                "Data-scope validation is surfaced through governed XML posture and release verification receipts.",
                "Validation Surface",
                "Use the shared XML and custom-data lane to confirm which overlays, directories, and receipts define the active scope.",
                "Review XML Editor and release receipts when you need to verify overlay scope or authoring status."),
            "open_data_folder" => CreateGovernedUtilityDialog(
                "dialog.open_data_folder",
                "Data Folder",
                "Data folders stay host-owned and governed outside the shared runner surface.",
                "Folder Posture",
                "The shared shell keeps data-lane posture visible before you move into host file-system actions.",
                "Use XML Editor and custom-data posture to review the active lane before you open host file locations."),
            "xml_editor" => new DesktopDialogState(
                "dialog.xml_editor",
                "XML Editor",
                masterIndex is null
                    ? "Edit and import stay file-first here. This preview shows XML bridge status and custom data status."
                    : $"Edit and import stay file-first. XML Bridge is {masterIndex.XmlBridgePosture} with {masterIndex.EnabledDataOverlayCount} enabled overlays. Custom Data is {masterIndex.CustomDataLanePosture}.",
                [
                    new DesktopDialogField("xmlEditorLanePosture", "XML Bridge", NormalizeGoverned(masterIndex?.XmlBridgePosture), "governed", IsReadOnly: true),
                    new DesktopDialogField("xmlEditorXmlBridgePosture", "XML Bridge Posture", NormalizeGoverned(masterIndex?.XmlBridgePosture), "governed", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                    new DesktopDialogField("xmlEditorOverlayCount", "Enabled XML Overlays", (masterIndex?.EnabledDataOverlayCount ?? 0).ToString(), "0", IsReadOnly: true),
                    new DesktopDialogField("xmlEditorCustomDataLanePosture", "Custom Data", NormalizeGoverned(masterIndex?.CustomDataLanePosture), "governed", IsReadOnly: true),
                    new DesktopDialogField("xmlEditorCustomDataDirectoryCount", "Custom Data Directories", (masterIndex?.DistinctCustomDataDirectoryCount ?? 0).ToString(), "0", IsReadOnly: true),
                    new DesktopDialogField("xmlEditorReceipt", "XML Bridge Receipt", masterIndex?.XmlBridgeLaneReceipt ?? "missing", "missing", IsReadOnly: true),
                    new DesktopDialogField(
                        "xmlEditorCustomDataAuthoringReceipt",
                        "Custom Data Authoring Receipt",
                        masterIndex is null
                            ? "missing"
                            : NormalizeMasterIndexValue(masterIndex.CustomDataAuthoringLaneReceipt, masterIndex.CustomDataLanePosture),
                        "missing",
                        IsReadOnly: true,
                        LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                    new DesktopDialogField(
                        "xmlEditorXmlBridgeReceipt",
                        "XML Bridge Receipt Canonical",
                        masterIndex?.XmlBridgeLaneReceipt ?? "missing",
                        "missing",
                        IsReadOnly: true,
                        LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                    new DesktopDialogField("xmlEditorDialog", "XML", activeSectionJson ?? "<character />", "<character />", true)
                ],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "master_index" => new DesktopDialogState(
                "dialog.master_index",
                "Master Index",
                masterIndex is null
                    ? "Search the catalog, inspect the selected reference, and keep the current source visible."
                    : $"Search the catalog, inspect the selected reference, and keep the current source visible across {masterIndex.SourcebookCount} sourcebooks.",
                BuildMasterIndexFields(masterIndex),
                BuildMasterIndexActions(masterIndex)),
            "character_roster" => new DesktopDialogState(
                "dialog.character_roster",
                "Character Roster",
                "Group dossiers into your own folders, drag dossiers or custom directories through the tree, and keep selected-dossier details close without moving watched files until explicitly confirmed.",
                BuildRosterFields(name, alias, workspace, currentWorkspace, openWorkspaces, preferences),
                BuildRosterActions(name, alias, workspace, currentWorkspace, openWorkspaces, preferences)),
            "data_exporter" => new DesktopDialogState(
                "dialog.data_exporter",
                "Data Exporter",
                "Export pipeline is routed through API tool endpoints.",
                [new DesktopDialogField("dataExportPreview", "Export Preview", $"Dossier: {workspace}", "{}", true, true)],
                [
                    new DesktopDialogAction("download", "Download", true),
                    new DesktopDialogAction("close", "Close")
                ]),
            "export_character" => new DesktopDialogState(
                "dialog.export_character",
                "Export Dossier",
                "Export the selected dossier bundle.",
                [new DesktopDialogField("dataExportPreview", "Export Preview", $"Dossier: {workspace}", "{}", true, true)],
                [
                    new DesktopDialogAction("download", "Download", true),
                    new DesktopDialogAction("close", "Close")
                ]),
            "report_bug" => new DesktopDialogState(
                "dialog.report_bug",
                "Support and bug reporting",
                "Use signed-in support at /account/support for private install issues. GitHub is still available for public bug reports.",
                [
                    new DesktopDialogField("supportHub", "Tracked support", "/account/support", "/account/support", IsReadOnly: true),
                    new DesktopDialogField("supportPublic", "Guest support", "/contact", "/contact", IsReadOnly: true),
                    new DesktopDialogField("supportGithub", "Public GitHub issue form", "https://github.com/ArchonMegalon/Chummer6/issues/new/choose", "https://github.com/ArchonMegalon/Chummer6/issues/new/choose", IsReadOnly: true)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "about" => new DesktopDialogState(
                "dialog.about",
                "About Chummer",
                "Dual-head preview over shared presenter/API behavior path.",
                [
                    new DesktopDialogField("runtime", "Runtime", "net10.0", "net10.0", IsReadOnly: true),
                    new DesktopDialogField("workspace", "Dossier", workspace, workspace, IsReadOnly: true)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "hero_lab_importer" => new DesktopDialogState(
                "dialog.hero_lab_importer",
                "Hero Lab Importer",
                masterIndex is null
                    ? "Paste Hero Lab XML payload to import."
                    : $"Paste Hero Lab XML payload to import. Import Oracle is {masterIndex.ImportOracleLanePosture} across {masterIndex.ImportOracleSourcesCovered}/{masterIndex.ImportOracleSourcesExpected} source families. Adjacent SR6 Oracle is {masterIndex.AdjacentSr6OracleReceiptPosture}.",
                [
                    new DesktopDialogField("heroLabSource", "Input File", ".por/.xml", ".por/.xml"),
                    CreateRulesetField("importRulesetId", rulesetId),
                    new DesktopDialogField("heroLabImportOracleLanePosture", "Import Oracle", NormalizeGoverned(masterIndex?.ImportOracleLanePosture), "governed", IsReadOnly: true),
                    new DesktopDialogField(
                        "heroLabImportOracleCoverage",
                        "Import Oracle Coverage",
                        masterIndex is null
                            ? "0/1 · 0%"
                            : "1/1 · 100%",
                        "0/1 · 0%",
                        IsReadOnly: true),
                    new DesktopDialogField("heroLabFixtureCount", "Hero Lab Fixtures", (masterIndex?.HeroLabFixtureCount ?? 0).ToString(), "0", IsReadOnly: true),
                    new DesktopDialogField(
                        "heroLabImportOracleMissingSources",
                        "Missing Sources",
                        masterIndex is null
                            ? string.Empty
                            : string.Join(", ", masterIndex.ImportOracleMissingSources ?? []),
                        string.Empty,
                        IsReadOnly: true),
                    new DesktopDialogField(
                        "heroLabImportOracleMatrix",
                        "Import Oracle Matrix",
                        masterIndex is null ? "missing" : BuildImportOracleMatrix(masterIndex),
                        "missing",
                        IsReadOnly: true,
                        IsMultiline: true),
                    new DesktopDialogField(
                        "heroLabImportOracleReceipt",
                        "Import Oracle Receipt",
                        masterIndex is null
                            ? "missing"
                            : NormalizeMasterIndexValue(masterIndex.ImportOracleLaneReceipt, masterIndex.ImportOracleReceiptPosture),
                        "missing",
                        IsReadOnly: true,
                        IsMultiline: true),
                    new DesktopDialogField(
                        "heroLabAdjacentSr6OracleReceipt",
                        "Adjacent SR6 Oracle",
                        masterIndex is null
                            ? "missing"
                            : NormalizeAdjacentSr6OracleReceipt(masterIndex.AdjacentSr6OracleLaneReceipt, masterIndex.AdjacentSr6OracleReceiptPosture),
                        "missing",
                        IsReadOnly: true,
                        IsMultiline: true),
                    new DesktopDialogField(
                        "heroLabXml",
                        "Hero Lab XML",
                        "<character><name>Hero Lab Import</name></character>",
                        "<character><name>Hero Lab Import</name></character>",
                        IsMultiline: true)
                ],
                [
                    new DesktopDialogAction("import", "Import", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "new_window" => new DesktopDialogState(
                "dialog.new_window",
                "New Window",
                "Open a second shell instance from your platform runtime.",
                BuildWindowUtilityFields("Open New Window", "A second shell stays bound to the desktop host instead of taking over the current view."),
                [new DesktopDialogAction("close", "Close", true)]),
            "close_window" => new DesktopDialogState(
                "dialog.close_window",
                "Close Window",
                "Close-window action is host/platform specific.",
                BuildWindowUtilityFields("Close Current Window", "Close the current shell window while keeping save and install continuity on the host."),
                [new DesktopDialogAction("close", "Close", true)]),
            "wiki" => new DesktopDialogState(
                "dialog.wiki",
                "Wiki",
                "https://github.com/ArchonMegalon/Chummer6/wiki/",
                BuildExternalLinkFields("Chummer Wiki", "https://github.com/ArchonMegalon/Chummer6/wiki/", "Use the legacy wiki as an external reference without displacing the current view."),
                [new DesktopDialogAction("close", "Close", true)]),
            "discord" => new DesktopDialogState(
                "dialog.discord",
                "Discord",
                "https://discord.gg/mJB7st9",
                BuildExternalLinkFields("Community Discord", "https://discord.gg/mJB7st9", "Community chat opens in the browser instead of replacing the desktop view."),
                [new DesktopDialogAction("close", "Close", true)]),
            "show_login_video" => new DesktopDialogState(
                "dialog.show_login_video",
                "Show Login Video",
                "The Avalonia desktop host opens the Matrix uplink login video on demand, including after the install is already linked.",
                BuildWindowUtilityFields("Show Matrix uplink login video", "The Help menu opens the same flagship login render without forcing account linking or browser startup. Use the login button inside that surface only when you want to link this copy."),
                [new DesktopDialogAction("close", "Close", true)]),
            "revision_history" => new DesktopDialogState(
                "dialog.revision_history",
                "Revision History",
                "https://github.com/ArchonMegalon/Chummer6/releases",
                BuildExternalLinkFields("Revision History", "https://github.com/ArchonMegalon/Chummer6/releases", "Release notes open as an external help surface."),
                [new DesktopDialogAction("close", "Close", true)]),
            "dumpshock" => new DesktopDialogState(
                "dialog.dumpshock",
                "Issue Tracker",
                "https://github.com/ArchonMegalon/Chummer6/issues/",
                BuildExternalLinkFields("Issue Tracker", "https://github.com/ArchonMegalon/Chummer6/issues/", "The Chummer6 issue tracker opens externally and stays outside the desktop view."),
                [new DesktopDialogAction("close", "Close", true)]),
            "print_character" => new DesktopDialogState(
                "dialog.print_character",
                "Print Dossier",
                "Print preview is rendered by host/browser print facilities.",
                BuildPrintUtilityFields("Current dossier", "Print preview stays host-driven while sheet/export context remains visible."),
                [new DesktopDialogAction("close", "Close", true)]),
            "print_multiple" => new DesktopDialogState(
                "dialog.print_multiple",
                "Print Multiple",
                "Batch print is available through roster and print endpoints.",
                BuildPrintUtilityFields("Roster batch", "Batch print remains roster-driven and uses the same compact print utility."),
                [new DesktopDialogAction("close", "Close", true)]),
            "update" => new DesktopDialogState(
                "dialog.update",
                "Check for Updates",
                "See where this copy gets updates, how it behaves when a newer build is available, and where support picks up if an update needs help.",
                BuildUpdateUtilityFields(),
                [new DesktopDialogAction("close", "Close", true)]),
            _ => new DesktopDialogState(
                "dialog.generic",
                commandId,
                $"Command '{commandId}' is recognized but has no dedicated dialog template yet.",
                [],
                [new DesktopDialogAction("close", "Close", true)])
        });
    }

    private static DesktopDialogState CreateRuntimeInspectorDialog(RuntimeInspectorProjection projection)
    {
        string contentBundles = projection.RuntimeLock.ContentBundles.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, projection.RuntimeLock.ContentBundles.Select(bundle =>
                $"{bundle.BundleId}@{bundle.Version} ({bundle.RulesetId})"));
        string rulePacks = projection.ResolvedRulePacks.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, projection.ResolvedRulePacks.Select(rulePack =>
                $"{rulePack.RulePack.Id}@{rulePack.RulePack.Version} [{rulePack.TrustTier}] ({rulePack.SourceKind})"));
        string providerBindings = projection.ProviderBindings.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, projection.ProviderBindings.Select(binding =>
                $"{binding.CapabilityId} -> {binding.ProviderId}"));
        string capabilities = projection.CapabilityDescriptors is not { Count: > 0 }
            ? "(none)"
            : string.Join(Environment.NewLine, projection.CapabilityDescriptors.Select(descriptor =>
                $"{descriptor.CapabilityId} [{descriptor.InvocationKind}] provider={(descriptor.ProviderId ?? "(none)")}, session-safe={descriptor.SessionSafe}, explainable={descriptor.Explainable}, gas={descriptor.DefaultGasBudget.ProviderInstructionLimit}/{descriptor.DefaultGasBudget.RequestInstructionLimit}"));
        string warnings = projection.Warnings.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, projection.Warnings.Select(warning =>
                $"{warning.Severity}: {warning.Message}"));
        string compatibility = projection.CompatibilityDiagnostics.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, projection.CompatibilityDiagnostics.Select(diagnostic =>
                $"{diagnostic.State}: {diagnostic.Message}"));
        string migrationPreview = projection.MigrationPreview.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, projection.MigrationPreview.Select(item => item.Summary));
        string installTarget = RuntimeInspectorDiagnostics.FormatInstallTarget(projection.Install);
        string profileDiagnostics = RuntimeInspectorDiagnostics.BuildProfileDiagnosticsSummary(projection);
        string hubClientDiagnostics = RuntimeInspectorDiagnostics.BuildHubClientDiagnosticsSummary(projection);
        string rulePackDiagnostics = RuntimeInspectorDiagnostics.BuildRulePackDiagnosticsSummary(projection);

        return new DesktopDialogState(
            Id: "dialog.runtime_inspector",
            Title: "Runtime Inspector",
            Message: $"Inspect resolved runtime for '{projection.TargetId}'.",
            Fields:
            [
                new DesktopDialogField("runtimeProfileId", "Profile", projection.TargetId, projection.TargetId, IsReadOnly: true),
                new DesktopDialogField("runtimeProfileSource", "Profile Source", projection.ProfileSourceKind, projection.ProfileSourceKind, IsReadOnly: true),
                new DesktopDialogField("runtimeTargetKind", "Target Kind", projection.TargetKind, projection.TargetKind, IsReadOnly: true),
                new DesktopDialogField("runtimeRulesetId", "Ruleset", projection.RuntimeLock.RulesetId, projection.RuntimeLock.RulesetId, IsReadOnly: true),
                new DesktopDialogField("runtimeEngineApi", "Engine API", projection.RuntimeLock.EngineApiVersion, projection.RuntimeLock.EngineApiVersion, IsReadOnly: true),
                new DesktopDialogField("runtimeFingerprint", "Fingerprint", projection.RuntimeLock.RuntimeFingerprint, projection.RuntimeLock.RuntimeFingerprint, IsReadOnly: true),
                new DesktopDialogField("runtimeInstallState", "Install State", projection.Install.State, projection.Install.State, IsReadOnly: true),
                new DesktopDialogField("runtimeInstallTarget", "Install Target", installTarget, installTarget, IsReadOnly: true),
                new DesktopDialogField("runtimeProfileDiagnostics", "Rule Profile Diagnostics", profileDiagnostics, profileDiagnostics, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeHubClientDiagnostics", "Hub Client Diagnostics", hubClientDiagnostics, hubClientDiagnostics, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeContentBundles", "Content Bundles", contentBundles, contentBundles, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeRulePacks", "RulePacks", rulePacks, rulePacks, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeRulePackDiagnostics", "RulePack Diagnostics", rulePackDiagnostics, rulePackDiagnostics, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeProviderBindings", "Provider Bindings", providerBindings, providerBindings, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeCapabilities", "Capabilities", capabilities, capabilities, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeCompatibility", "Compatibility", compatibility, compatibility, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeWarnings", "Warnings", warnings, warnings, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("runtimeMigrationPreview", "Migration Preview", migrationPreview, migrationPreview, IsReadOnly: true, IsMultiline: true)
            ],
            Actions:
            [
                new DesktopDialogAction("close", "Close", true)
            ]);
    }

    private static DesktopDialogState CreateGovernedUtilityDialog(
        string dialogId,
        string title,
        string message,
        string summaryLabel,
        string summary,
        string nextStep)
    {
        return new DesktopDialogState(
            dialogId,
            title,
            message,
            [
                new DesktopDialogField("utilitySummary", summaryLabel, summary, summary, IsReadOnly: true, IsMultiline: true),
                new DesktopDialogField("utilityNextStep", "Next Step", nextStep, nextStep, IsReadOnly: true, IsMultiline: true)
            ],
            [new DesktopDialogAction("close", "Close", true)]);
    }

    private static DesktopDialogState CreateOpenCharacterDialog(
        string id,
        string title,
        string message,
        string? rulesetId)
    {
        const string defaultXml = "<character><name>Imported Runner</name></character>";
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string importSource = "Paste dossier XML from a trusted local or reviewed export source.";
        string reviewSummary = $"Review the imported summary before applying this {normalizedRulesetId.ToUpperInvariant()} dossier import.";

        return new DesktopDialogState(
            Id: id,
            Title: title,
            Message: message,
            Fields:
            [
                new DesktopDialogField(
                    Id: "importRulesetId",
                    Label: "Import Ruleset",
                    Value: normalizedRulesetId,
                    Placeholder: normalizedRulesetId,
                    InputType: "select",
                    Options: BuildRulesetOptions()),
                new DesktopDialogField(
                    Id: "openCharacterImportSource",
                    Label: "Import Source",
                    Value: importSource,
                    Placeholder: importSource,
                    IsReadOnly: true),
                new DesktopDialogField(
                    Id: "openCharacterReviewSummary",
                    Label: "Review imported summary",
                    Value: reviewSummary,
                    Placeholder: reviewSummary,
                    IsReadOnly: true),
                new DesktopDialogField(
                    Id: "openCharacterXml",
                    Label: "Dossier XML",
                    Value: defaultXml,
                    Placeholder: defaultXml,
                    IsMultiline: true)
            ],
            Actions:
            [
                new DesktopDialogAction("import", "Import", true),
                new DesktopDialogAction("cancel", "Cancel")
            ]);
    }

    private static DesktopDialogField CreateRulesetField(string fieldId, string? rulesetId)
    {
        string value = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        return new DesktopDialogField(
            Id: fieldId,
            Label: "Ruleset",
            Value: value,
            Placeholder: value,
            InputType: "select",
            Options: BuildRulesetOptions());
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildRulesetOptions()
        => new[]
        {
            new DesktopDialogFieldOption("sr4", "SR4"),
            new DesktopDialogFieldOption("sr5", "SR5"),
            new DesktopDialogFieldOption("sr6", "SR6")
        };

    private static DesktopDialogFieldOption[] BuildOriginArchetypeOptions()
        =>
        [
            new("decker", "Decker"),
            new("street_sam", "Street Samurai"),
            new("mage", "Mage"),
            new("adept", "Adept"),
            new("face", "Face"),
            new("rigger", "Rigger"),
            new("technomancer", "Technomancer"),
            new("auto", "Fit the story")
        ];

    private static DesktopDialogFieldOption[] BuildOriginMetatypeOptions(DesktopPreferenceState preferences)
        => FilterAiRestrictedCharacterOptionsForPreferences(
            [
                new("auto", "Fit the story"),
                new("human", "Human"),
                new("elf", "Elf"),
                new("dwarf", "Dwarf"),
                new("ork", "Ork"),
                new("troll", "Troll")
            ],
            preferences).ToArray();

    private static DesktopDialogFieldOption[] BuildOriginBuildPreferenceOptions(string? rulesetId)
    {
        List<DesktopDialogFieldOption> options = [new("auto", "Fit the archetype")];
        options.AddRange(BuildBuildMethodOptions(rulesetId));
        return options
            .DistinctBy(option => option.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static DesktopDialogFieldOption[] BuildOriginGmRequirementPresetOptions()
        =>
        [
            new("none", "No GM constraint"),
            new("illegal_addiction", "Must be addicted to an illegal drug"),
            new("magically_active", "Must be magically active"),
            new("intelligence_2_plus", "Must have Intelligence 2+"),
            new("restricted_ware_exception", "Grant one restricted ware exception"),
            new("bonus_nuyen_20000", "Grant +20,000 nuyen"),
            new("extra_quality", "Grant one extra quality"),
            new("custom", "Use custom GM text")
        ];

    private static DesktopDialogState BuildNewCharacterDialog(
        DesktopPreferenceState preferences,
        string? rulesetId)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string preferredBuildMethod = ResolvePreferredBuildMethod(normalizedRulesetId, preferences.CharacterPriority);
        bool houseRulesEnabled = preferences.HouseRulesEnabled;
        string houseRulesValue = houseRulesEnabled ? "true" : "false";

        return new DesktopDialogState(
            "dialog.new_character",
            "Select Build Method",
            BuildNewCharacterMessage(normalizedRulesetId, preferredBuildMethod, houseRulesEnabled),
            [
                new DesktopDialogField(
                    "newCharacterName",
                    "Character Name",
                    "New runner",
                    "New runner",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
                new DesktopDialogField(
                    "newCharacterAlias",
                    "Alias",
                    "Runner",
                    "Runner",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                CreateRulesetField("newCharacterRulesetId", normalizedRulesetId),
                CreateBuildMethodField("newCharacterBuildMethod", normalizedRulesetId, preferredBuildMethod),
                new DesktopDialogField(
                    "newCharacterSetting",
                    "Character Setting",
                    "Core Rulebook",
                    "Core Rulebook",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
                new DesktopDialogField(
                    "newCharacterIgnoreRules",
                    "Ignore Character Creation Rules",
                    "false",
                    "false",
                    InputType: "checkbox",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField(
                    "newCharacterPreferredBuildMethod",
                    "Preferred Build Method",
                    preferredBuildMethod,
                    preferredBuildMethod,
                    IsReadOnly: true,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField(
                    "newCharacterHouseRulesEnabled",
                    "House Rules",
                    houseRulesValue,
                    houseRulesValue,
                    InputType: "checkbox",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField(
                    "newCharacterDisableAiFeatures",
                    "Disable Helper Features",
                    preferences.DisableAiFeatures ? "true" : "false",
                    preferences.DisableAiFeatures ? "true" : "false",
                    IsReadOnly: true,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden)
            ],
            BuildNewCharacterDialogActions(preferences));
    }

    private static IReadOnlyList<DesktopDialogAction> BuildNewCharacterDialogActions(DesktopPreferenceState preferences)
    {
        List<DesktopDialogAction> actions = [];
        if (!preferences.DisableAiFeatures)
        {
            actions.Add(new DesktopDialogAction("start_from_origin", "Start Origin Dossier"));
        }

        actions.Add(new DesktopDialogAction("create_character", "OK", true));
        actions.Add(new DesktopDialogAction("cancel", "Cancel"));
        return actions;
    }

    internal static DesktopDialogState BuildNewCharacterOriginWizardDialog(
        string? rulesetId,
        string? name,
        string? alias)
        => BuildNewCharacterOriginWizardDialog(rulesetId, name, alias, DesktopPreferenceStateRuntime.Current);

    internal static DesktopDialogState BuildNewCharacterOriginWizardDialog(
        string? rulesetId,
        string? name,
        string? alias,
        DesktopPreferenceState preferences)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string normalizedName = NormalizeOriginSeedName(name);
        string normalizedAlias = NormalizeOriginSeedAlias(alias);
        OriginBuildRecommendation recommendation = ResolveOriginBuildRecommendation(
            normalizedRulesetId,
            "decker",
            "auto",
            "auto",
            "street",
            "betrayal",
            "self_taught",
            "medical_debt",
            "matrix",
            "survival",
            "grounded",
            "none",
            string.Empty);

        return new DesktopDialogState(
            NewCharacterOriginWizardDialogId,
            "Origin Dossier",
            "Pick only the basics, then build the story. Advanced controls are optional.",
            [
                new DesktopDialogField(
                    "newCharacterName",
                    "Character Name",
                    normalizedName,
                    "New dossier",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField(
                    "newCharacterAlias",
                    "Alias",
                    normalizedAlias,
                    "Dossier",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                CreateRulesetField("newCharacterRulesetId", normalizedRulesetId) with
                {
                    LayoutSlot = DesktopDialogFieldLayoutSlots.Hidden
                },
                new DesktopDialogField(
                    "newCharacterDisableAiFeatures",
                    "Disable Helper Features",
                    preferences.DisableAiFeatures ? "true" : "false",
                    preferences.DisableAiFeatures ? "true" : "false",
                    IsReadOnly: true,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField(
                    "newCharacterOriginMetatypePreference",
                    "Race / Metatype",
                    "auto",
                    "auto",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                    Options: BuildOriginMetatypeOptions(preferences)),
                new DesktopDialogField(
                    "newCharacterOriginArchetypeIntent",
                    "Archetype",
                    "decker",
                    "decker",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right,
                    Options: BuildOriginArchetypeOptions()),
                new DesktopDialogField(
                    "newCharacterOriginBuildPreference",
                    "Build Method",
                    "auto",
                    "auto",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options: BuildOriginBuildPreferenceOptions(normalizedRulesetId)),
                new DesktopDialogField(
                    "newCharacterOriginBackground",
                    "Background",
                    "street",
                    "street",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options:
                    [
                        new("street", "Street"),
                        new("corporate", "Corporate"),
                        new("academic", "Academic"),
                        new("magical", "Magical"),
                        new("military", "Military"),
                        new("criminal", "Criminal")
                    ]),
                new DesktopDialogField(
                    "newCharacterOriginTurningPoint",
                    "Turning Point",
                    "betrayal",
                    "betrayal",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options:
                    [
                        new("betrayal", "Betrayal"),
                        new("debt", "Debt"),
                        new("clinic_event", "Clinic Event"),
                        new("awakening", "Awakening"),
                        new("prison", "Prison"),
                        new("family_collapse", "Family Collapse")
                    ]),
                new DesktopDialogField(
                    "newCharacterOriginTrainingPath",
                    "Training Path",
                    "self_taught",
                    "self_taught",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options:
                    [
                        new("self_taught", "Self-Taught"),
                        new("corporate_program", "Corporate Program"),
                        new("gang_survival", "Gang Survival"),
                        new("military_discipline", "Military Discipline"),
                        new("mentor_driven", "Mentor-Driven"),
                        new("underground_scene", "Underground Scene")
                    ]),
                new DesktopDialogField(
                    "newCharacterOriginPressureCost",
                    "Pressure / Cost",
                    "medical_debt",
                    "medical_debt",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options:
                    [
                        new("addiction", "Addiction"),
                        new("enemy", "Enemy"),
                        new("obligation", "Obligation"),
                        new("medical_debt", "Medical Debt"),
                        new("dependent", "Dependent"),
                        new("notoriety", "Notoriety")
                    ]),
                new DesktopDialogField(
                    "newCharacterOriginGmConstraintPreset",
                    "GM Constraint",
                    "none",
                    "none",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options: BuildOriginGmRequirementPresetOptions()),
                new DesktopDialogField(
                    "newCharacterOriginUpgradeExposure",
                    "Upgrade Exposure",
                    "matrix",
                    "matrix",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options:
                    [
                        new("heavy_augment", "Heavy Augment Path"),
                        new("light_augment", "Light Augment Path"),
                        new("magic", "Magic Path"),
                        new("matrix", "Matrix Path"),
                        new("mundane", "Mundane Specialist"),
                        new("undecided", "Still Undecided")
                    ]),
                new DesktopDialogField(
                    "newCharacterOriginMotivation",
                    "Present Motivation",
                    "survival",
                    "survival",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options:
                    [
                        new("revenge", "Revenge"),
                        new("survival", "Survival"),
                        new("money", "Money"),
                        new("redemption", "Redemption"),
                        new("curiosity", "Curiosity"),
                        new("loyalty", "Loyalty")
                    ]),
                new DesktopDialogField(
                    "newCharacterOriginTone",
                    "Tone",
                    "grounded",
                    "grounded",
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                    Options:
                    [
                        new("grounded", "Grounded"),
                        new("noir", "Noir"),
                        new("tragic", "Tragic"),
                        new("professional", "Professional"),
                        new("chaotic", "Chaotic")
                    ]),
                new DesktopDialogField(
                    "newCharacterOriginGmRequirements",
                    "GM Requirements / Grants",
                    string.Empty,
                    "Optional constraints or grants: required qualities, addiction, magical activity, attribute floors, bonus nuyen, availability, gear, ware, or banned choices.",
                    IsMultiline: true,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField(
                    "newCharacterOriginSummary",
                    "Story Preview",
                    recommendation.OriginSummary,
                    recommendation.OriginSummary,
                    IsReadOnly: true,
                    IsMultiline: true,
                    VisualKind: DesktopDialogFieldVisualKinds.Narrative),
                BuildNewCharacterContextField("newCharacterOriginArchetype", "Origin Archetype", recommendation.ArchetypeLabel),
                BuildNewCharacterContextField("newCharacterOriginBuildMethod", "Origin Build Method", recommendation.BuildMethod),
                BuildNewCharacterContextField("newCharacterOriginMetatypeCategory", "Origin Metatype Range", recommendation.MetatypeCategory),
                BuildNewCharacterContextField("newCharacterOriginMetatype", "Origin Metatype", recommendation.Metatype),
                BuildNewCharacterContextField("newCharacterOriginQualityFocus", "Origin Quality Focus", recommendation.QualityFocus),
                BuildNewCharacterContextField("newCharacterOriginGmRequirementSummary", "GM Requirement Summary", recommendation.GmRequirementSummary),
                BuildNewCharacterContextField("newCharacterOriginPathSummary", "Origin Path Summary", recommendation.PathSummary)
            ],
            [
                new DesktopDialogAction("generate_fitting_build", "Draft story", true),
                new DesktopDialogAction("cancel", "Cancel")
            ]);
    }

    internal static DesktopDialogState BuildNewCharacterOriginBuildDialog(DesktopDialogState originDialog)
    {
        string rulesetId = RulesetDefaults.NormalizeOptional(DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterRulesetId")) ?? RulesetDefaults.Sr5;
        OriginBuildRecommendation recommendation = ResolveOriginBuildRecommendation(
            rulesetId,
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginArchetypeIntent"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginBuildPreference"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginMetatypePreference"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginBackground"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginTurningPoint"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginTrainingPath"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginPressureCost"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginUpgradeExposure"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginMotivation"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginTone"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginGmConstraintPreset"),
            DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterOriginGmRequirements"));
        string name = NormalizeOriginSeedName(DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterName"));
        string alias = NormalizeOriginSeedAlias(DesktopDialogFieldValueParser.GetValue(originDialog, "newCharacterAlias"));
        string dossierRoute = BuildOriginDossierOnlineRoute(rulesetId, alias);
        string buildLogic = BuildGridValue(
            ("Build Method", recommendation.BuildMethod),
            ("Likely Archetype", recommendation.ArchetypeLabel),
            ("Likely Metatype", BuildMetatypeSummaryValue(recommendation.Metatype, recommendation.MetatypeCategory)),
            ("Quality Focus", recommendation.QualityFocus),
            ("GM Requirements", recommendation.GmRequirementSummary),
            ("Path", recommendation.PathSummary));
        string storyNotes =
            $"Origin | {recommendation.OriginSummary}{Environment.NewLine}" +
            "Alice Seed | approved origin story" + Environment.NewLine +
            $"Build | {recommendation.BuildSummary}{Environment.NewLine}" +
            $"GM Requirements | {recommendation.GmRequirementSummary}{Environment.NewLine}" +
            $"Dossier Link | {dossierRoute}{Environment.NewLine}" +
            "Sheet Changes | none yet; review the path before applying mechanics";
        string bookPreview = BuildOriginBookPreview(alias, recommendation);

        return new DesktopDialogState(
            NewCharacterOriginBuildDialogId,
            "Origin Build Handoff",
            BuildOriginBuildDialogMessageDisplayValue(),
            [
                BuildNewCharacterContextField("newCharacterWorkflowRulesetId", "Workflow Ruleset", rulesetId),
                BuildNewCharacterContextField("newCharacterWorkflowBuildMethod", "Workflow Build Method", recommendation.BuildMethod),
                BuildNewCharacterContextField("newCharacterWorkflowName", "Workflow Name", name),
                BuildNewCharacterContextField("newCharacterWorkflowAlias", "Workflow Alias", alias),
                BuildNewCharacterContextField("newCharacterWorkflowHouseRulesEnabled", "Workflow House Rules", "false"),
                BuildNewCharacterContextField("newCharacterOriginSummary", "Origin Summary", recommendation.OriginSummary),
                BuildNewCharacterContextField("newCharacterOriginAliceSeedSource", "Alice Seed Source", "approved_origin_story"),
                new DesktopDialogField(
                    "newCharacterOriginDossierLink",
                    "Origin Dossier Link",
                    dossierRoute,
                    dossierRoute,
                    IsReadOnly: true,
                    InputType: "url",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField(
                    "newCharacterOriginDossierLinkNotes",
                    "Link Notes",
                    BuildOriginDossierLinkNotesDisplayValue(),
                    BuildOriginDossierLinkNotesDisplayValue(),
                    IsReadOnly: true,
                    IsMultiline: true,
                    VisualKind: DesktopDialogFieldVisualKinds.Snippet,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField(
                    "newCharacterOriginBookPreview",
                    "Book Preview",
                    bookPreview,
                    bookPreview,
                    IsReadOnly: true,
                    IsMultiline: true,
                    VisualKind: DesktopDialogFieldVisualKinds.Book),
                new DesktopDialogField(
                    "newCharacterOriginStory",
                    "Origin Dossier",
                    recommendation.OriginSummary,
                    recommendation.OriginSummary,
                    IsReadOnly: true,
                    IsMultiline: true,
                    VisualKind: DesktopDialogFieldVisualKinds.Narrative),
                new DesktopDialogField(
                    "newCharacterOriginBuildLogic",
                    "Build Direction",
                    buildLogic,
                    buildLogic,
                    IsReadOnly: true,
                    IsMultiline: true,
                    VisualKind: DesktopDialogFieldVisualKinds.Grid,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField(
                    "newCharacterOriginImplications",
                    "Next Steps",
                    storyNotes,
                    storyNotes,
                    IsReadOnly: true,
                    IsMultiline: true,
                    VisualKind: DesktopDialogFieldVisualKinds.List,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right)
            ],
            [
                new DesktopDialogAction("show_origin_dossier_link", "Show Origin Dossier link"),
                new DesktopDialogAction("open_origin_guided_chargen", "Start character creation", true),
                new DesktopDialogAction("cancel", "Cancel")
            ]);
    }

    internal static DesktopDialogState NormalizeOriginWizardDialogForDisplay(DesktopDialogState dialog)
        => string.Equals(dialog.Id, NewCharacterOriginWizardDialogId, StringComparison.Ordinal)
            ? RebuildNewCharacterOriginWizardDialog(dialog, DesktopPreferenceStateRuntime.Current)
            : dialog;

    internal static IReadOnlyList<DesktopDialogField> NormalizeOriginWizardFieldsForDisplay(IReadOnlyList<DesktopDialogField> fields)
        => NormalizeOriginWizardDialogForDisplay(new DesktopDialogState(
                NewCharacterOriginWizardDialogId,
                string.Empty,
                string.Empty,
                fields.ToArray(),
                []))
            .Fields;

    internal static string BuildOriginDossierLinkNotesDisplayValue()
        => "Opens the clean Origin Dossier route directly. The story text stays local until you publish it.";

    internal static string BuildOriginBuildDialogMessageDisplayValue()
        => "Read this first. Character creation starts after the story feels right.";

    internal static string BuildOriginImplicationsDisplayValue(
        string? originSummary,
        string? aliceSeedSource,
        string? buildLogicValue,
        string? implicationsValue,
        string dossierRoute)
    {
        List<string> lines = [];
        string originLine = GetStructuredDisplayLineValue(implicationsValue, "Origin") ?? (originSummary?.Trim() ?? string.Empty);
        string aliceSeedLine = BuildOriginAliceSeedDisplayValue(
            GetStructuredDisplayLineValue(implicationsValue, "Alice Seed") ?? aliceSeedSource);
        string? buildSummary = BuildOriginBuildSummaryDisplayValue(implicationsValue, buildLogicValue);
        string? gmRequirements = BuildOriginGmRequirementDisplayValue(implicationsValue, buildLogicValue);
        string sheetChanges = BuildOriginSheetChangesDisplayValue(implicationsValue);

        if (!string.IsNullOrWhiteSpace(originLine))
        {
            lines.Add($"Origin | {originLine}");
        }

        if (!string.IsNullOrWhiteSpace(aliceSeedLine))
        {
            lines.Add($"Alice Seed | {aliceSeedLine}");
        }

        if (!string.IsNullOrWhiteSpace(buildSummary))
        {
            lines.Add($"Build | {buildSummary}");
        }

        if (!string.IsNullOrWhiteSpace(gmRequirements))
        {
            lines.Add($"GM Requirements | {gmRequirements}");
        }

        lines.Add($"Dossier Link | {dossierRoute}");
        lines.Add($"Sheet Changes | {sheetChanges}");

        lines.AddRange((implicationsValue ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Where(static line => !HasStructuredDisplayLineLabel(line, "Origin"))
            .Where(static line => !HasStructuredDisplayLineLabel(line, "Alice Seed"))
            .Where(static line => !HasStructuredDisplayLineLabel(line, "Build"))
            .Where(static line => !HasStructuredDisplayLineLabel(line, "GM Requirements"))
            .Where(static line => !HasStructuredDisplayLineLabel(line, "Dossier Link"))
            .Where(static line => !HasStructuredDisplayLineLabel(line, "Sheet Changes")));

        return string.Join(Environment.NewLine, lines);
    }

    internal static string? BuildOriginBuildSummaryDisplayValue(string? implicationsValue, string? buildLogicValue)
    {
        string? existingBuildSummary = GetStructuredDisplayLineValue(implicationsValue, "Build");
        if (!string.IsNullOrWhiteSpace(existingBuildSummary))
        {
            return existingBuildSummary;
        }

        string archetype = GetStructuredDisplayLineValue(buildLogicValue, "Likely Archetype") ?? string.Empty;
        string buildMethod = GetStructuredDisplayLineValue(buildLogicValue, "Build Method") ?? string.Empty;
        string metatype = GetStructuredDisplayLineValue(buildLogicValue, "Likely Metatype") ?? string.Empty;
        string qualityFocus = GetStructuredDisplayLineValue(buildLogicValue, "Quality Focus") ?? string.Empty;
        string pathSummary = GetStructuredDisplayLineValue(buildLogicValue, "Path") ?? string.Empty;
        List<string> segments = [];

        if (!string.IsNullOrWhiteSpace(archetype))
        {
            segments.Add($"{archetype} posture");
        }

        if (!string.IsNullOrWhiteSpace(buildMethod))
        {
            segments.Add($"{buildMethod} build");
        }

        if (!string.IsNullOrWhiteSpace(metatype))
        {
            segments.Add($"{metatype} lean");
        }

        if (!string.IsNullOrWhiteSpace(qualityFocus))
        {
            segments.Add(qualityFocus.ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(pathSummary))
        {
            segments.Add(pathSummary);
        }

        return segments.Count == 0
            ? null
            : UndetectableHumanizerCopyAdapter.Humanize(string.Join(", ", segments) + ".");
    }

    internal static string? BuildOriginGmRequirementDisplayValue(string? implicationsValue, string? buildLogicValue)
        => GetStructuredDisplayLineValue(implicationsValue, "GM Requirements")
            ?? GetStructuredDisplayLineValue(buildLogicValue, "GM Requirements");

    internal static string BuildOriginDossierOnlineRoute(string? rulesetId, string? alias)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string aliasToken = NormalizeOriginSeedAlias(alias);
        List<string> query =
        [
            $"command={Uri.EscapeDataString("new_character_origin")}",
            $"ruleset={Uri.EscapeDataString(normalizedRulesetId)}"
        ];

        if (!string.IsNullOrWhiteSpace(aliasToken))
        {
            query.Add($"alias={Uri.EscapeDataString(aliasToken)}");
        }

        return $"{OriginDossierOnlineRoute}?{string.Join("&", query)}";
    }

    private static string BuildOriginAliceSeedDisplayValue(string? value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "approved_origin_story" : value.Trim();
        normalized = normalized.Replace('_', ' ').Replace('-', ' ');
        return string.Join(" ", normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildOriginSheetChangesDisplayValue(string? implicationsValue)
        => GetStructuredDisplayLineValue(implicationsValue, "Sheet Changes")
            ?? "none yet; review the path before applying mechanics";

    private static string? GetStructuredDisplayLineValue(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (string rawLine in value.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)
                || !HasStructuredDisplayLineLabel(line, label))
            {
                continue;
            }

            int separatorIndex = line.IndexOfAny(['|', ':']);
            if (separatorIndex < 0 || separatorIndex == line.Length - 1)
            {
                return string.Empty;
            }

            return line[(separatorIndex + 1)..].Trim();
        }

        return null;
    }

    private static bool HasStructuredDisplayLineLabel(string value, string label)
    {
        int separatorIndex = value.AsSpan().IndexOfAny('|', ':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        return value.AsSpan(0, separatorIndex).Trim().Equals(label.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildOriginBookPreview(string alias, OriginBuildRecommendation recommendation)
    {
        string runnerName = NormalizeOriginSeedAlias(alias);
        return UndetectableHumanizerCopyAdapter.Humanize(
            $"{runnerName}: Origin Dossier{Environment.NewLine}{Environment.NewLine}" +
            $"{recommendation.OriginSummary}{Environment.NewLine}{Environment.NewLine}" +
            $"The shape of the build is visible in the fiction: {recommendation.BuildSummary}{Environment.NewLine}{Environment.NewLine}" +
            $"At the table, the story keeps these constraints in view: {recommendation.GmRequirementSummary}{Environment.NewLine}{Environment.NewLine}" +
            "When this origin feels right, start character creation. Alice can use the story as context later; no numbers are written to the sheet from this preview alone.");
    }

    private static string NormalizeOriginSeedName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || string.Equals(name.Trim(), "New runner", StringComparison.OrdinalIgnoreCase))
        {
            return "New dossier";
        }

        return name.Trim();
    }

    private static string NormalizeOriginSeedAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias)
            || string.Equals(alias.Trim(), "Runner", StringComparison.OrdinalIgnoreCase))
        {
            return "Dossier";
        }

        return alias.Trim();
    }

    private static string ResolveWorkflowIdentityName(string? name, string? workflowOriginSource)
    {
        if (IsOriginWorkflowSource(workflowOriginSource))
        {
            return NormalizeOriginSeedName(name);
        }

        return string.IsNullOrWhiteSpace(name)
            ? "New runner"
            : name.Trim();
    }

    private static string ResolveWorkflowIdentityAlias(string? alias, string? workflowOriginSource)
    {
        if (IsOriginWorkflowSource(workflowOriginSource))
        {
            return NormalizeOriginSeedAlias(alias);
        }

        return string.IsNullOrWhiteSpace(alias)
            ? "Runner"
            : alias.Trim();
    }

    private static bool IsOriginWorkflowSource(string? workflowOriginSource)
        => string.Equals(workflowOriginSource?.Trim(), "approved_origin_story", StringComparison.Ordinal);

    private static DesktopDialogField CreateBuildMethodField(string fieldId, string? rulesetId, string? preferredBuildMethod = null)
    {
        DesktopDialogFieldOption[] options = BuildBuildMethodOptions(rulesetId).ToArray();
        string rulesetToken = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string selectedValue = ResolvePreferredBuildMethod(rulesetToken, preferredBuildMethod);
        DesktopDialogFieldOption selected = options
            .First(option => string.Equals(option.Value, selectedValue, StringComparison.Ordinal));
        return new DesktopDialogField(
            Id: fieldId,
            Label: "Build Method",
            Value: selected.Value,
            Placeholder: selected.Value,
            InputType: "select",
            Options: options);
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildBuildMethodOptions(string? rulesetId)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        return normalizedRulesetId switch
        {
            var id when string.Equals(id, RulesetDefaults.Sr4, StringComparison.Ordinal) =>
            [
                new DesktopDialogFieldOption("BP", "BP"),
                new DesktopDialogFieldOption("Karma", "Karma")
            ],
            _ =>
            [
                new DesktopDialogFieldOption("Priority", "Priority"),
                new DesktopDialogFieldOption("SumToTen", "Sum-to-Ten"),
                new DesktopDialogFieldOption("Karma", "Karma"),
                new DesktopDialogFieldOption("LifeModule", "Life Modules")
            ]
        };
    }

    internal static DesktopDialogState BuildNewCharacterContinuationDialog(
        string? rulesetId,
        string? buildMethod,
        bool houseRulesEnabled,
        string name,
        string alias)
        => BuildNewCharacterContinuationDialog(
            rulesetId,
            buildMethod,
            houseRulesEnabled,
            name,
            alias,
            DesktopPreferenceStateRuntime.Current);

    internal static DesktopDialogState BuildNewCharacterContinuationDialog(
        string? rulesetId,
        string? buildMethod,
        bool houseRulesEnabled,
        string name,
        string alias,
        DesktopPreferenceState preferences)
        => BuildNewCharacterContinuationDialog(
            rulesetId,
            buildMethod,
            houseRulesEnabled,
            name,
            alias,
            preferences,
            workflowOriginSource: null);

    internal static DesktopDialogState BuildNewCharacterContinuationDialog(
        string? rulesetId,
        string? buildMethod,
        bool houseRulesEnabled,
        string name,
        string alias,
        DesktopPreferenceState preferences,
        string? workflowOriginSource,
        string? characterSetting = null,
        bool ignoreRules = false)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string resolvedBuildMethod = ResolvePreferredBuildMethod(normalizedRulesetId, buildMethod);
        string normalizedWorkflowName = ResolveWorkflowIdentityName(name, workflowOriginSource);
        string normalizedWorkflowAlias = ResolveWorkflowIdentityAlias(alias, workflowOriginSource);
        string normalizedCharacterSetting = string.IsNullOrWhiteSpace(characterSetting)
            ? "Core Rulebook"
            : characterSetting.Trim();
        return UsesPriorityWorkflow(resolvedBuildMethod)
            ? BuildNewCharacterPriorityWorkflowDialog(normalizedRulesetId, resolvedBuildMethod, houseRulesEnabled, normalizedWorkflowName, normalizedWorkflowAlias, preferences, workflowOriginSource, normalizedCharacterSetting, ignoreRules)
            : BuildNewCharacterKarmaWorkflowDialog(normalizedRulesetId, resolvedBuildMethod, houseRulesEnabled, normalizedWorkflowName, normalizedWorkflowAlias, preferences, workflowOriginSource, normalizedCharacterSetting, ignoreRules);
    }

    private static DesktopDialogState BuildNewCharacterPriorityWorkflowDialog(
        string rulesetId,
        string buildMethod,
        bool houseRulesEnabled,
        string name,
        string alias,
        DesktopPreferenceState preferences,
        string? workflowOriginSource,
        string characterSetting,
        bool ignoreRules)
    {
        PriorityWorkflowResolution resolution = ResolvePriorityWorkflowResolution(
            rulesetId,
            buildMethod,
            category: "Standard",
            metatype: ResolveDefaultMetatype("Standard"),
            heritagePriority: "D",
            attributesPriority: "B",
            talentPriority: "E",
            skillsPriority: "C",
            resourcesPriority: "A",
            talentChoice: "Mundane",
            metavariant: string.Empty,
            skillChoice1: string.Empty,
            skillChoice2: string.Empty,
            skillChoice3: string.Empty,
            possessionBased: false,
            possessionMethod: string.Empty,
            force: 1,
            lastChangedFieldId: string.Empty,
            preferences);
        string houseRulesValue = houseRulesEnabled ? "true" : "false";
        string summary = BuildNewCharacterPriorityWorkflowSummary(
            rulesetId,
            buildMethod,
            resolution.Category,
            resolution.Metatype,
            resolution.HeritagePriority,
            resolution.AttributesPriority,
            resolution.TalentPriority,
            resolution.SkillsPriority,
            resolution.ResourcesPriority,
            resolution.TalentChoice,
            houseRulesEnabled);

        string normalizedWorkflowName = ResolveWorkflowIdentityName(name, workflowOriginSource);
        string normalizedWorkflowAlias = ResolveWorkflowIdentityAlias(alias, workflowOriginSource);

        return new DesktopDialogState(
            NewCharacterPriorityWorkflowDialogId,
            "Select Metatype Priority",
            "Choose the metatype and priorities before the character opens.",
            [
                BuildNewCharacterContextField("newCharacterWorkflowRulesetId", "Workflow Ruleset", rulesetId),
                BuildNewCharacterContextField("newCharacterWorkflowBuildMethod", "Workflow Build Method", buildMethod),
                BuildNewCharacterContextField("newCharacterWorkflowName", "Workflow Name", normalizedWorkflowName),
                BuildNewCharacterContextField("newCharacterWorkflowAlias", "Workflow Alias", normalizedWorkflowAlias),
                BuildNewCharacterContextField("newCharacterWorkflowHouseRulesEnabled", "Workflow House Rules", houseRulesValue),
                BuildNewCharacterContextField("newCharacterWorkflowSetting", "Workflow Character Setting", characterSetting),
                BuildNewCharacterContextField("newCharacterWorkflowIgnoreRules", "Workflow Ignore Rules", ignoreRules ? "true" : "false"),
                BuildNewCharacterContextField("newCharacterWorkflowOriginSource", "Workflow Origin Source", string.IsNullOrWhiteSpace(workflowOriginSource) ? "none" : workflowOriginSource.Trim()),
                BuildNewCharacterContextField("newCharacterDisableAiFeatures", "Disable Helper Features", preferences.DisableAiFeatures ? "true" : "false"),
                new DesktopDialogField(
                    "newCharacterMetatypeCategory",
                    "Show Metatypes",
                    resolution.Category,
                    resolution.Category,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                    Options: BuildMetatypeCategoryOptions()),
                new DesktopDialogField(
                    "newCharacterMetatype",
                    "Metatype",
                    resolution.Metatype,
                    resolution.Metatype,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right,
                    Options: resolution.MetatypeOptions),
                new DesktopDialogField(
                    "newCharacterPriorityHeritage",
                    "Heritage Priority",
                    resolution.HeritagePriority,
                    resolution.HeritagePriority,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                    Options: BuildPriorityLetterOptions()),
                new DesktopDialogField(
                    "newCharacterPriorityAttributes",
                    "Attributes Priority",
                    resolution.AttributesPriority,
                    resolution.AttributesPriority,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right,
                    Options: BuildPriorityLetterOptions()),
                new DesktopDialogField(
                    "newCharacterPriorityTalent",
                    "Talent Priority",
                    resolution.TalentPriority,
                    resolution.TalentPriority,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                    Options: BuildPriorityLetterOptions()),
                new DesktopDialogField(
                    "newCharacterPrioritySkills",
                    "Skills Priority",
                    resolution.SkillsPriority,
                    resolution.SkillsPriority,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right,
                    Options: BuildPriorityLetterOptions()),
                new DesktopDialogField(
                    "newCharacterPriorityResources",
                    "Resources Priority",
                    resolution.ResourcesPriority,
                    resolution.ResourcesPriority,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                    Options: BuildPriorityLetterOptions()),
                new DesktopDialogField(
                    "newCharacterPriorityTalentChoice",
                    "Talent Choice",
                    resolution.TalentChoice,
                    resolution.TalentChoice,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right,
                    Options: resolution.TalentOptions),
                new DesktopDialogField(
                    NewCharacterMetavariantFieldId,
                    "Metavariant",
                    resolution.Metavariant,
                    resolution.Metavariant,
                    InputType: "select",
                    LayoutSlot: resolution.RuntimeState.MetavariantOptions.Count > 1
                        ? DesktopDialogFieldLayoutSlots.Left
                        : DesktopDialogFieldLayoutSlots.Hidden,
                    Options: resolution.RuntimeState.MetavariantOptions),
                new DesktopDialogField(
                    NewCharacterPrioritySkillChoice1FieldId,
                    "Skill Choice 1",
                    resolution.SkillChoice1,
                    resolution.SkillChoice1,
                    InputType: "select",
                    LayoutSlot: resolution.RuntimeState.SkillChoice1.Visible
                        ? DesktopDialogFieldLayoutSlots.Left
                        : DesktopDialogFieldLayoutSlots.Hidden,
                    Options: resolution.RuntimeState.SkillChoice1.Options),
                new DesktopDialogField(
                    NewCharacterPrioritySkillChoice2FieldId,
                    "Skill Choice 2",
                    resolution.SkillChoice2,
                    resolution.SkillChoice2,
                    InputType: "select",
                    LayoutSlot: resolution.RuntimeState.SkillChoice2.Visible
                        ? DesktopDialogFieldLayoutSlots.Right
                        : DesktopDialogFieldLayoutSlots.Hidden,
                    Options: resolution.RuntimeState.SkillChoice2.Options),
                new DesktopDialogField(
                    NewCharacterPrioritySkillChoice3FieldId,
                    "Skill Choice 3",
                    resolution.SkillChoice3,
                    resolution.SkillChoice3,
                    InputType: "select",
                    LayoutSlot: resolution.RuntimeState.SkillChoice3.Visible
                        ? DesktopDialogFieldLayoutSlots.Left
                        : DesktopDialogFieldLayoutSlots.Hidden,
                    Options: resolution.RuntimeState.SkillChoice3.Options),
                new DesktopDialogField(
                    "newCharacterPriorityWorkflowSummary",
                    "Workflow Summary",
                    summary,
                    summary,
                    IsReadOnly: true,
                    IsMultiline: true,
                    VisualKind: DesktopDialogFieldVisualKinds.Snippet,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                BuildNewCharacterContextField(NewCharacterPriorityLastChangedFieldId, "Workflow Last Changed Field", string.Empty),
                BuildNewCharacterContextField(
                    NewCharacterPriorityWorkflowCanCommitFieldId,
                    "Workflow Can Commit",
                    resolution.RuntimeState.CanCommit ? "true" : "false"),
                BuildNewCharacterContextField(
                    NewCharacterPriorityWorkflowStateFieldId,
                    "Workflow Runtime State",
                    PriorityWorkflowDialogRuntimeStateSerializer.Serialize(resolution.RuntimeState))
            ],
            [
                new DesktopDialogAction("complete_new_character_workflow", "OK", true),
                new DesktopDialogAction("cancel", "Cancel")
            ]);
    }

    private static DesktopDialogState BuildNewCharacterKarmaWorkflowDialog(
        string rulesetId,
        string buildMethod,
        bool houseRulesEnabled,
        string name,
        string alias,
        DesktopPreferenceState preferences,
        string? workflowOriginSource,
        string characterSetting,
        bool ignoreRules)
    {
        string category = "Standard";
        string metatype = ResolveDefaultMetatype(category);
        DesktopDialogFieldOption[] metatypeOptions = BuildMetatypeOptions(category, preferences).ToArray();
        if (!metatypeOptions.Any(option => string.Equals(option.Value, metatype, StringComparison.Ordinal)))
        {
            metatype = metatypeOptions.FirstOrDefault()?.Value ?? metatype;
        }
        string houseRulesValue = houseRulesEnabled ? "true" : "false";
        string summary = BuildNewCharacterKarmaWorkflowSummary(
            rulesetId,
            buildMethod,
            category,
            metatype,
            houseRulesEnabled);

        string normalizedWorkflowName = ResolveWorkflowIdentityName(name, workflowOriginSource);
        string normalizedWorkflowAlias = ResolveWorkflowIdentityAlias(alias, workflowOriginSource);

        return new DesktopDialogState(
            NewCharacterKarmaWorkflowDialogId,
            "Select Metatype",
            "Choose the metatype before the character opens.",
            [
                BuildNewCharacterContextField("newCharacterWorkflowRulesetId", "Workflow Ruleset", rulesetId),
                BuildNewCharacterContextField("newCharacterWorkflowBuildMethod", "Workflow Build Method", buildMethod),
                BuildNewCharacterContextField("newCharacterWorkflowName", "Workflow Name", normalizedWorkflowName),
                BuildNewCharacterContextField("newCharacterWorkflowAlias", "Workflow Alias", normalizedWorkflowAlias),
                BuildNewCharacterContextField("newCharacterWorkflowHouseRulesEnabled", "Workflow House Rules", houseRulesValue),
                BuildNewCharacterContextField("newCharacterWorkflowSetting", "Workflow Character Setting", characterSetting),
                BuildNewCharacterContextField("newCharacterWorkflowIgnoreRules", "Workflow Ignore Rules", ignoreRules ? "true" : "false"),
                BuildNewCharacterContextField("newCharacterWorkflowOriginSource", "Workflow Origin Source", string.IsNullOrWhiteSpace(workflowOriginSource) ? "none" : workflowOriginSource.Trim()),
                BuildNewCharacterContextField("newCharacterDisableAiFeatures", "Disable Helper Features", preferences.DisableAiFeatures ? "true" : "false"),
                new DesktopDialogField(
                    "newCharacterMetatypeCategory",
                    "Show Metatypes",
                    category,
                    category,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                    Options: BuildMetatypeCategoryOptions()),
                new DesktopDialogField(
                    "newCharacterMetatype",
                    "Metatype",
                    metatype,
                    metatype,
                    InputType: "select",
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Right,
                    Options: metatypeOptions),
                new DesktopDialogField(
                    "newCharacterKarmaWorkflowSummary",
                    "Workflow Summary",
                    summary,
                    summary,
                    IsReadOnly: true,
                    IsMultiline: true,
                    VisualKind: DesktopDialogFieldVisualKinds.Snippet)
            ],
            [
                new DesktopDialogAction("complete_new_character_workflow", "OK", true),
                new DesktopDialogAction("cancel", "Cancel")
            ]);
    }

    private static OriginBuildRecommendation ResolveOriginBuildRecommendation(
        string? rulesetId,
        string? archetypeIntent,
        string? buildPreference,
        string? metatypePreference,
        string? background,
        string? turningPoint,
        string? trainingPath,
        string? pressureCost,
        string? upgradeExposure,
        string? motivation,
        string? tone,
        string? gmRequirementPreset,
        string? gmRequirements)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string normalizedArchetypeIntent = NormalizeOriginToken(archetypeIntent, "auto");
        string normalizedBuildPreference = NormalizeOriginBuildPreference(buildPreference);
        string normalizedMetatypePreference = NormalizeOriginToken(metatypePreference, "auto");
        string normalizedBackground = NormalizeOriginToken(background, "street");
        string normalizedTurningPoint = NormalizeOriginToken(turningPoint, "betrayal");
        string normalizedTrainingPath = NormalizeOriginToken(trainingPath, "self_taught");
        string normalizedPressureCost = NormalizeOriginToken(pressureCost, "medical_debt");
        string normalizedUpgradeExposure = NormalizeOriginToken(upgradeExposure, "matrix");
        string normalizedMotivation = NormalizeOriginToken(motivation, "survival");
        string normalizedTone = NormalizeOriginToken(tone, "grounded");
        string normalizedGmRequirementPreset = NormalizeOriginToken(gmRequirementPreset, "none");
        string normalizedGmRequirements = string.IsNullOrWhiteSpace(gmRequirements) ? string.Empty : gmRequirements.Trim();
        bool requiresMagicalActivity = string.Equals(normalizedGmRequirementPreset, "magically_active", StringComparison.Ordinal)
            || normalizedGmRequirements.Contains("magically active", StringComparison.OrdinalIgnoreCase);
        bool requiresIllegalAddiction = string.Equals(normalizedGmRequirementPreset, "illegal_addiction", StringComparison.Ordinal)
            || normalizedGmRequirements.Contains("illegal drug", StringComparison.OrdinalIgnoreCase);

        string inferredArchetype = normalizedUpgradeExposure switch
        {
            _ when requiresMagicalActivity => "mage",
            "magic" => "mage",
            "matrix" => "decker",
            "heavy_augment" => "street_sam",
            "light_augment" when string.Equals(normalizedBackground, "corporate", StringComparison.Ordinal) => "face",
            "mundane" when string.Equals(normalizedTrainingPath, "military_discipline", StringComparison.Ordinal) => "street_sam",
            _ when string.Equals(normalizedBackground, "corporate", StringComparison.Ordinal) => "face",
            _ when string.Equals(normalizedBackground, "military", StringComparison.Ordinal) => "street_sam",
            _ when string.Equals(normalizedBackground, "magical", StringComparison.Ordinal) => "mage",
            _ => "decker"
        };
        string archetype = string.Equals(normalizedArchetypeIntent, "auto", StringComparison.Ordinal)
            ? inferredArchetype
            : normalizedArchetypeIntent;
        if (requiresMagicalActivity && !OriginArchetypeSatisfiesMagicalActivity(archetype))
        {
            archetype = "mage";
        }

        string inferredBuildMethod = string.Equals(normalizedRulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal)
            ? string.Equals(archetype, "street_sam", StringComparison.Ordinal) || string.Equals(archetype, "rigger", StringComparison.Ordinal) ? "BP" : "Karma"
            : string.Equals(archetype, "decker", StringComparison.Ordinal)
                || string.Equals(archetype, "face", StringComparison.Ordinal)
                || string.Equals(archetype, "technomancer", StringComparison.Ordinal)
                    ? "Karma"
                    : "Priority";
        string buildMethod = string.Equals(normalizedBuildPreference, "auto", StringComparison.Ordinal)
            ? inferredBuildMethod
            : normalizedBuildPreference;
        string inferredMetatype = archetype switch
        {
            "street_sam" when string.Equals(normalizedTone, "chaotic", StringComparison.Ordinal) => "Ork",
            "street_sam" => "Troll",
            "adept" => "Ork",
            "mage" => "Elf",
            "face" => "Elf",
            "rigger" => "Dwarf",
            "technomancer" => "Human",
            _ => "Human"
        };
        string metatype = string.Equals(normalizedMetatypePreference, "auto", StringComparison.Ordinal)
            ? inferredMetatype
            : FormatChoiceLabel(normalizedMetatypePreference);
        string metatypeCategory = string.Equals(normalizedBackground, "corporate", StringComparison.Ordinal)
            || string.Equals(normalizedMetatypePreference, "human", StringComparison.Ordinal)
                ? "Standard"
                : "Metahuman";
        string qualityFocus = normalizedPressureCost switch
        {
            _ when requiresIllegalAddiction => "Addiction / illegal-drug pressure",
            "addiction" => "Addiction / recovery pressure",
            "enemy" => "Enemies and vigilance",
            "obligation" => "Dependents and obligations",
            "medical_debt" => "Debt, restricted gear, and implant provenance",
            "dependent" => "Dependents and social pressure",
            "notoriety" => "Notoriety and first-impression tradeoffs",
            _ => "survivability and table-safe hooks"
        };
        string pathSummary = archetype switch
        {
            "decker" => "matrix-first focus",
            "technomancer" => "resonance and matrix focus",
            "rigger" => "vehicle/drone control focus",
            "adept" => "physical adept focus",
            "mage" => "magic-forward focus",
            "street_sam" => "heavy combat augmentation focus",
            "face" => "social leverage focus",
            _ => normalizedUpgradeExposure switch
            {
                "magic" => "magic-forward focus",
                "matrix" => "matrix-first focus",
                "heavy_augment" => "heavy augmentation focus",
                "light_augment" => "light augmentation focus",
                "mundane" => "mundane specialist focus",
                _ => "open specialist focus"
            }
        };
        string exposureSummary = normalizedUpgradeExposure switch
        {
            "magic" => "magic-forward focus",
            "matrix" => "matrix-first focus",
            "heavy_augment" => "heavy augmentation focus",
            "light_augment" => "light augmentation focus",
            "mundane" => "mundane specialist focus",
            _ => "open specialist focus"
        };
        string gmRequirementSummary = ResolveOriginGmRequirementSummary(
            normalizedGmRequirementPreset,
            normalizedGmRequirements);
        string originSummary = UndetectableHumanizerCopyAdapter.Humanize(
            $"{FormatChoiceLabel(normalizedBackground)} upbringing, {FormatChoiceLabel(normalizedTurningPoint)} turning point, and a {FormatChoiceLabel(normalizedTrainingPath)} training path pushed this dossier path toward {FormatChoiceLabel(archetype)} work. " +
            $"{FormatChoiceLabel(normalizedPressureCost)} still shapes their decisions, while {FormatChoiceLabel(normalizedMotivation)} keeps the next run in view. " +
            $"The dossier keeps {exposureSummary} visible as the story reason for the build choices.");
        string buildSummary = UndetectableHumanizerCopyAdapter.Humanize(
            $"{FormatChoiceLabel(archetype)} posture, {buildMethod} build, {metatype} lean, {qualityFocus.ToLowerInvariant()}, {pathSummary}.");

        return new OriginBuildRecommendation(
            archetype,
            FormatChoiceLabel(archetype),
            buildMethod,
            metatypeCategory,
            metatype,
            qualityFocus,
            pathSummary,
            UndetectableHumanizerCopyAdapter.Humanize(gmRequirementSummary),
            originSummary,
            buildSummary);
    }

    private static string NormalizeOriginToken(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private static bool OriginArchetypeSatisfiesMagicalActivity(string archetype)
        => string.Equals(archetype, "mage", StringComparison.Ordinal)
           || string.Equals(archetype, "adept", StringComparison.Ordinal);

    private static string NormalizeOriginBuildPreference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "auto";

        string normalized = value.Trim();
        return normalized.Equals("bp", StringComparison.OrdinalIgnoreCase) ? "BP" :
            normalized.Equals("karma", StringComparison.OrdinalIgnoreCase) ? "Karma" :
            normalized.Equals("priority", StringComparison.OrdinalIgnoreCase) ? "Priority" :
            "auto";
    }

    private static string FormatChoiceLabel(string value)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_', ' '));

    private static string ResolveOriginGmRequirementSummary(string preset, string customRequirements)
    {
        string presetSummary = preset switch
        {
            "illegal_addiction" => "Must carry an Addiction quality tied to an illegal drug.",
            "magically_active" => "Must be magically active; the story and build path should support magic-capable choices.",
            "intelligence_2_plus" => "Must have Intelligence 2 or higher.",
            "restricted_ware_exception" => "GM grants one restricted ware or availability exception if the origin justifies it.",
            "bonus_nuyen_20000" => "GM grants +20,000 nuyen if the origin explains the resource source.",
            "extra_quality" => "GM grants one extra quality if the origin makes it table-safe.",
            "custom" => "Use the custom GM requirements exactly as written.",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(presetSummary) && string.IsNullOrWhiteSpace(customRequirements))
        {
            return "None declared";
        }

        if (string.IsNullOrWhiteSpace(presetSummary))
        {
            return UndetectableHumanizerCopyAdapter.Humanize(customRequirements);
        }

        if (string.IsNullOrWhiteSpace(customRequirements))
        {
            return UndetectableHumanizerCopyAdapter.Humanize(presetSummary);
        }

        return UndetectableHumanizerCopyAdapter.Humanize($"{presetSummary} {customRequirements.Trim()}");
    }

    private static DesktopDialogField BuildNewCharacterContextField(string id, string label, string value)
        => new(
            id,
            label,
            value,
            value,
            IsReadOnly: true,
            LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden);

    private static IReadOnlyList<DesktopDialogFieldOption> BuildMetatypeCategoryOptions()
        => new[]
        {
            new DesktopDialogFieldOption("Standard", "Core choices"),
            new DesktopDialogFieldOption("Metahuman", "Non-human choices"),
            new DesktopDialogFieldOption("Show All", "All playable options")
        };

    private static string BuildMetatypeSummaryValue(string metatype, string? category)
        => $"{metatype} · {BuildMetatypeFilterSummary(category)}";

    private static string BuildMetatypeFilterSummary(string? category)
        => string.Equals(category, "Metahuman", StringComparison.Ordinal)
            ? "non-human choices"
            : string.Equals(category, "Show All", StringComparison.Ordinal)
                ? "all playable options"
                : "core choices";

    private static IReadOnlyList<DesktopDialogFieldOption> BuildMetatypeOptions(string? category, DesktopPreferenceState? preferences = null)
    {
        string normalizedCategory = string.IsNullOrWhiteSpace(category) ? "Standard" : category.Trim();
        IReadOnlyList<DesktopDialogFieldOption> options = normalizedCategory switch
        {
            "Metahuman" =>
            [
                new DesktopDialogFieldOption("Elf", "Elf"),
                new DesktopDialogFieldOption("Dwarf", "Dwarf"),
                new DesktopDialogFieldOption("Ork", "Ork"),
                new DesktopDialogFieldOption("Troll", "Troll")
            ],
            "Show All" =>
            [
                new DesktopDialogFieldOption("Human", "Human"),
                new DesktopDialogFieldOption("Elf", "Elf"),
                new DesktopDialogFieldOption("Dwarf", "Dwarf"),
                new DesktopDialogFieldOption("Ork", "Ork"),
                new DesktopDialogFieldOption("Troll", "Troll"),
                new DesktopDialogFieldOption("Shapeshifter: Vulpine", "Shapeshifter: Vulpine")
            ],
            _ =>
            [
                new DesktopDialogFieldOption("Human", "Human"),
                new DesktopDialogFieldOption("Elf", "Elf"),
                new DesktopDialogFieldOption("Dwarf", "Dwarf"),
                new DesktopDialogFieldOption("Ork", "Ork"),
                new DesktopDialogFieldOption("Troll", "Troll")
            ]
        };
        return FilterAiRestrictedCharacterOptionsForPreferences(options, preferences ?? DesktopPreferenceStateRuntime.Current);
    }

    internal static IReadOnlyList<DesktopDialogFieldOption> FilterAiRestrictedCharacterOptionsForPreferences(
        IReadOnlyList<DesktopDialogFieldOption> options,
        DesktopPreferenceState preferences)
        => options
            .Where(option =>
                !OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption(option.Value, preferences)
                && !OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption(option.Label, preferences))
            .ToArray();

    private static IReadOnlyList<DesktopDialogFieldOption> BuildPriorityMetatypeOptions(string? category, string heritagePriority, DesktopPreferenceState? preferences = null)
    {
        int heritageRank = ResolvePriorityHeritageRank(heritagePriority);
        return BuildMetatypeOptions(category, preferences ?? DesktopPreferenceStateRuntime.Current)
            .Where(option => heritageRank >= ResolveMinimumHeritageRank(option.Value))
            .ToArray();
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildMetavariantOptions(string metatype)
    {
        return metatype.Trim() switch
        {
            "Elf" =>
            [
                new DesktopDialogFieldOption("Elf", "Elf"),
                new DesktopDialogFieldOption("Dryad", "Dryad")
            ],
            "Dwarf" =>
            [
                new DesktopDialogFieldOption("Dwarf", "Dwarf"),
                new DesktopDialogFieldOption("Gnome", "Gnome")
            ],
            "Ork" =>
            [
                new DesktopDialogFieldOption("Ork", "Ork"),
                new DesktopDialogFieldOption("Hobgoblin", "Hobgoblin")
            ],
            "Troll" =>
            [
                new DesktopDialogFieldOption("Troll", "Troll"),
                new DesktopDialogFieldOption("Cyclops", "Cyclops")
            ],
            _ =>
            [
                new DesktopDialogFieldOption("Human", "Human")
            ]
        };
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildPriorityLetterOptions()
        => new[]
        {
            new DesktopDialogFieldOption("A", "A"),
            new DesktopDialogFieldOption("B", "B"),
            new DesktopDialogFieldOption("C", "C"),
            new DesktopDialogFieldOption("D", "D"),
            new DesktopDialogFieldOption("E", "E")
        };

    private static IReadOnlyList<DesktopDialogFieldOption> BuildTalentChoiceOptions(string priorityLetter, string metatype, string metavariant)
    {
        List<DesktopDialogFieldOption> options =
        [
            new DesktopDialogFieldOption("Mundane", "Mundane"),
            new DesktopDialogFieldOption("Adept", "Adept"),
            new DesktopDialogFieldOption("Magician", "Magician"),
            new DesktopDialogFieldOption("Mystic Adept", "Mystic Adept"),
            new DesktopDialogFieldOption("Technomancer", "Technomancer")
        ];

        if (string.Equals(metatype, "Elf", StringComparison.Ordinal)
            && string.Equals(metavariant, "Dryad", StringComparison.Ordinal))
        {
            options.Insert(1, new DesktopDialogFieldOption("Aspected Magician", "Aspected Magician"));
        }

        return options
            .DistinctBy(option => option.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveDefaultMetatype(string? category)
        => string.Equals(category, "Metahuman", StringComparison.Ordinal) ? "Elf" : "Human";

    private static string ResolveDefaultPriorityMetatype(string? category, string heritagePriority, DesktopPreferenceState? preferences = null)
        => BuildPriorityMetatypeOptions(category, heritagePriority, preferences ?? DesktopPreferenceStateRuntime.Current)
            .FirstOrDefault()?.Value
            ?? ResolveDefaultMetatype(category);

    private static string ResolvePriorityMetatypeSelection(
        string? currentMetatype,
        IReadOnlyList<DesktopDialogFieldOption> options,
        string? category)
    {
        if (options.Count == 0)
        {
            return ResolveDefaultMetatype(category);
        }

        if (!string.IsNullOrWhiteSpace(currentMetatype)
            && options.Any(option => string.Equals(option.Value, currentMetatype, StringComparison.Ordinal)))
        {
            return currentMetatype;
        }

        return options
            .OrderByDescending(option => ResolveMinimumHeritageRank(option.Value))
            .ThenBy(option => option.Label, StringComparer.Ordinal)
            .Select(option => option.Value)
            .FirstOrDefault()
            ?? options[0].Value;
    }

    private static PriorityWorkflowResolution ResolvePriorityWorkflowResolution(
        string rulesetId,
        string buildMethod,
        string category,
        string metatype,
        string heritagePriority,
        string attributesPriority,
        string talentPriority,
        string skillsPriority,
        string resourcesPriority,
        string talentChoice,
        string metavariant,
        string skillChoice1,
        string skillChoice2,
        string skillChoice3,
        bool possessionBased,
        string possessionMethod,
        int force,
        string lastChangedFieldId,
        DesktopPreferenceState preferences)
    {
        string normalizedCategory = BuildMetatypeCategoryOptions()
            .Select(option => option.Value)
            .FirstOrDefault(option => string.Equals(option, category, StringComparison.Ordinal))
            ?? "Standard";
        string normalizedHeritagePriority = NormalizePriorityLetter(heritagePriority, "D");
        DesktopDialogFieldOption[] metatypeOptions = BuildPriorityMetatypeOptions(normalizedCategory, normalizedHeritagePriority, preferences).ToArray();
        if (metatypeOptions.Length == 0)
        {
            metatypeOptions = BuildPriorityMetatypeOptions(normalizedCategory, "E", preferences).ToArray();
        }
        string resolvedMetatype = ResolvePriorityMetatypeSelection(
            metatype,
            metatypeOptions,
            normalizedCategory);

        Dictionary<string, string> priorities = new(StringComparer.Ordinal)
        {
            ["newCharacterPriorityHeritage"] = normalizedHeritagePriority,
            ["newCharacterPriorityAttributes"] = NormalizePriorityLetter(attributesPriority, "B"),
            ["newCharacterPriorityTalent"] = NormalizePriorityLetter(talentPriority, "E"),
            ["newCharacterPrioritySkills"] = NormalizePriorityLetter(skillsPriority, "C"),
            ["newCharacterPriorityResources"] = NormalizePriorityLetter(resourcesPriority, "A"),
        };
        ReconcilePriorityLetters(buildMethod, lastChangedFieldId, priorities);

        metatypeOptions = BuildPriorityMetatypeOptions(normalizedCategory, priorities["newCharacterPriorityHeritage"], preferences).ToArray();
        if (metatypeOptions.Length == 0)
        {
            metatypeOptions = BuildPriorityMetatypeOptions(normalizedCategory, "E", preferences).ToArray();
        }
        resolvedMetatype = ResolvePriorityMetatypeSelection(
            resolvedMetatype,
            metatypeOptions,
            normalizedCategory);

        DesktopDialogFieldOption[] metavariantOptions = BuildMetavariantOptions(resolvedMetatype).ToArray();
        string resolvedMetavariant = metavariantOptions.Any(option => string.Equals(option.Value, metavariant, StringComparison.Ordinal))
            ? metavariant
            : metavariantOptions[0].Value;

        DesktopDialogFieldOption[] talentOptions = BuildTalentChoiceOptions(
                priorities["newCharacterPriorityTalent"],
                resolvedMetatype,
                resolvedMetavariant)
            .ToArray();
        string resolvedTalentChoice = talentOptions.Any(option => string.Equals(option.Value, talentChoice, StringComparison.Ordinal))
            ? talentChoice
            : talentOptions[0].Value;

        (PriorityWorkflowChoiceState skillState1, PriorityWorkflowChoiceState skillState2, PriorityWorkflowChoiceState skillState3, string resolvedSkillChoice1, string resolvedSkillChoice2, string resolvedSkillChoice3, string skillSelectionLabel) =
            BuildPrioritySkillChoiceStates(
                resolvedTalentChoice,
                skillChoice1,
                skillChoice2,
                skillChoice3,
                lastChangedFieldId);

        PriorityWorkflowDialogRuntimeState runtimeState = BuildPriorityWorkflowRuntimeState(
            rulesetId,
            buildMethod,
            resolvedMetatype,
            resolvedMetavariant,
            priorities["newCharacterPriorityHeritage"],
            priorities["newCharacterPriorityAttributes"],
            priorities["newCharacterPriorityTalent"],
            priorities["newCharacterPrioritySkills"],
            priorities["newCharacterPriorityResources"],
            resolvedTalentChoice,
            skillSelectionLabel,
            skillState1,
            skillState2,
            skillState3,
            possessionBased,
            possessionMethod,
            force);

        return new PriorityWorkflowResolution(
            Category: normalizedCategory,
            Metatype: resolvedMetatype,
            HeritagePriority: priorities["newCharacterPriorityHeritage"],
            AttributesPriority: priorities["newCharacterPriorityAttributes"],
            TalentPriority: priorities["newCharacterPriorityTalent"],
            SkillsPriority: priorities["newCharacterPrioritySkills"],
            ResourcesPriority: priorities["newCharacterPriorityResources"],
            MetatypeOptions: metatypeOptions,
            TalentOptions: talentOptions,
            TalentChoice: resolvedTalentChoice,
            Metavariant: resolvedMetavariant,
            SkillChoice1: resolvedSkillChoice1,
            SkillChoice2: resolvedSkillChoice2,
            SkillChoice3: resolvedSkillChoice3,
            RuntimeState: runtimeState);
    }

    private static string NormalizePriorityLetter(string? value, string fallback)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
        return BuildPriorityLetterOptions().Any(option => string.Equals(option.Value, normalized, StringComparison.Ordinal))
            ? normalized
            : fallback;
    }

    private static void ReconcilePriorityLetters(
        string buildMethod,
        string lastChangedFieldId,
        IDictionary<string, string> priorities)
    {
        if (string.Equals(buildMethod, "SumToTen", StringComparison.OrdinalIgnoreCase)
            || !priorities.ContainsKey(lastChangedFieldId))
        {
            return;
        }

        string changedValue = priorities[lastChangedFieldId];
        string[] legalLetters = BuildPriorityLetterOptions()
            .Select(option => option.Value)
            .ToArray();

        for (int attempt = 0; attempt < 4; attempt++)
        {
            string? duplicateKey = priorities
                .Where(pair => !string.Equals(pair.Key, lastChangedFieldId, StringComparison.Ordinal)
                    && string.Equals(pair.Value, changedValue, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .FirstOrDefault();
            if (duplicateKey is null)
            {
                break;
            }

            string missingLetter = legalLetters
                .FirstOrDefault(letter => !priorities.Values.Contains(letter, StringComparer.Ordinal))
                ?? changedValue;
            priorities[duplicateKey] = missingLetter;
        }
    }

    private static (
        PriorityWorkflowChoiceState SkillChoice1,
        PriorityWorkflowChoiceState SkillChoice2,
        PriorityWorkflowChoiceState SkillChoice3,
        string ResolvedSkillChoice1,
        string ResolvedSkillChoice2,
        string ResolvedSkillChoice3,
        string SkillSelectionLabel)
        BuildPrioritySkillChoiceStates(
            string talentChoice,
            string skillChoice1,
            string skillChoice2,
            string skillChoice3,
            string lastChangedFieldId)
    {
        DesktopDialogFieldOption[] options = BuildPrioritySkillChoiceOptions(talentChoice).ToArray();
        if (options.Length == 0)
        {
            return (
                PriorityWorkflowChoiceState.Hidden,
                PriorityWorkflowChoiceState.Hidden,
                PriorityWorkflowChoiceState.Hidden,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        int visibleChoiceCount = talentChoice switch
        {
            "Mystic Adept" or "Technomancer" => 3,
            "Magician" or "Aspected Magician" => 2,
            _ => 1
        };

        string[] selections =
        [
            ResolvePriorityChoiceValue(skillChoice1, options, Array.Empty<string>()),
            ResolvePriorityChoiceValue(skillChoice2, options, new[] { skillChoice1 }),
            ResolvePriorityChoiceValue(skillChoice3, options, new[] { skillChoice1, skillChoice2 })
        ];

        int changedIndex = lastChangedFieldId switch
        {
            NewCharacterPrioritySkillChoice1FieldId => 0,
            NewCharacterPrioritySkillChoice2FieldId => 1,
            NewCharacterPrioritySkillChoice3FieldId => 2,
            _ => -1
        };

        if (changedIndex >= 0 && changedIndex < visibleChoiceCount)
        {
            string changedValue = selections[changedIndex];
            if (!string.IsNullOrWhiteSpace(changedValue))
            {
                for (int index = 0; index < visibleChoiceCount; index++)
                {
                    if (index == changedIndex
                        || !string.Equals(selections[index], changedValue, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    selections[index] = options
                        .Select(option => option.Value)
                        .FirstOrDefault(value => !selections
                            .Where((_, selectionIndex) => selectionIndex != index)
                            .Contains(value, StringComparer.Ordinal))
                        ?? string.Empty;
                }
            }
        }

        List<string> usedValues = [];
        for (int index = 0; index < visibleChoiceCount; index++)
        {
            if (!options.Any(option => string.Equals(option.Value, selections[index], StringComparison.Ordinal))
                || usedValues.Contains(selections[index], StringComparer.Ordinal))
            {
                selections[index] = options
                    .Select(option => option.Value)
                    .FirstOrDefault(value => !usedValues.Contains(value, StringComparer.Ordinal))
                    ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(selections[index]))
            {
                usedValues.Add(selections[index]);
            }
        }

        return (
            new PriorityWorkflowChoiceState(visibleChoiceCount >= 1, selections[0], options),
            new PriorityWorkflowChoiceState(visibleChoiceCount >= 2, selections[1], options),
            new PriorityWorkflowChoiceState(visibleChoiceCount >= 3, selections[2], options),
            selections[0],
            selections[1],
            selections[2],
            BuildPrioritySkillSelectionLabel(talentChoice));
    }

    private static string ResolvePriorityChoiceValue(
        string value,
        IReadOnlyList<DesktopDialogFieldOption> options,
        IEnumerable<string> reserved)
    {
        if (options.Any(option => string.Equals(option.Value, value, StringComparison.Ordinal))
            && !reserved.Contains(value, StringComparer.Ordinal))
        {
            return value;
        }

        return options
            .Select(option => option.Value)
            .FirstOrDefault(optionValue => !reserved.Contains(optionValue, StringComparer.Ordinal))
            ?? string.Empty;
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildPrioritySkillChoiceOptions(string talentChoice)
    {
        return talentChoice switch
        {
            "Magician" or "Aspected Magician" =>
            [
                new DesktopDialogFieldOption("Spellcasting", "Spellcasting"),
                new DesktopDialogFieldOption("Counterspelling", "Counterspelling"),
                new DesktopDialogFieldOption("Ritual Spellcasting", "Ritual Spellcasting"),
                new DesktopDialogFieldOption("Summoning", "Summoning"),
                new DesktopDialogFieldOption("Binding", "Binding"),
                new DesktopDialogFieldOption("Banishing", "Banishing")
            ],
            "Mystic Adept" =>
            [
                new DesktopDialogFieldOption("Spellcasting", "Spellcasting"),
                new DesktopDialogFieldOption("Counterspelling", "Counterspelling"),
                new DesktopDialogFieldOption("Assensing", "Assensing"),
                new DesktopDialogFieldOption("Summoning", "Summoning"),
                new DesktopDialogFieldOption("Binding", "Binding"),
                new DesktopDialogFieldOption("Gymnastics", "Gymnastics")
            ],
            "Technomancer" =>
            [
                new DesktopDialogFieldOption("Compiling", "Compiling"),
                new DesktopDialogFieldOption("Registering", "Registering"),
                new DesktopDialogFieldOption("Software", "Software"),
                new DesktopDialogFieldOption("Electronic Warfare", "Electronic Warfare"),
                new DesktopDialogFieldOption("Hacking", "Hacking"),
                new DesktopDialogFieldOption("Cybercombat", "Cybercombat")
            ],
            _ => []
        };
    }

    private static string BuildPrioritySkillSelectionLabel(string talentChoice)
        => talentChoice switch
        {
            "Technomancer" => "Select the free resonance skills:",
            "Magician" or "Mystic Adept" or "Aspected Magician" => "Select the free magical skills:",
            _ => string.Empty
        };

    private static PriorityWorkflowDialogRuntimeState BuildPriorityWorkflowRuntimeState(
        string rulesetId,
        string buildMethod,
        string metatype,
        string metavariant,
        string heritagePriority,
        string attributesPriority,
        string talentPriority,
        string skillsPriority,
        string resourcesPriority,
        string talentChoice,
        string skillSelectionLabel,
        PriorityWorkflowChoiceState skillChoice1,
        PriorityWorkflowChoiceState skillChoice2,
        PriorityWorkflowChoiceState skillChoice3,
        bool possessionBased,
        string possessionMethod,
        int force)
    {
        DesktopDialogFieldOption[] possessionMethodOptions =
        [
            new DesktopDialogFieldOption("None", "None"),
            new DesktopDialogFieldOption("Channeling", "Channeling"),
            new DesktopDialogFieldOption("Direct", "Direct")
        ];
        string resolvedPossessionMethod = possessionMethodOptions.Any(option => string.Equals(option.Value, possessionMethod, StringComparison.Ordinal))
            ? possessionMethod
            : possessionMethodOptions[0].Value;
        IReadOnlyList<PriorityWorkflowInspectAttributeState> inspectAttributes = BuildPriorityInspectAttributes(metatype, metavariant);
        IReadOnlyList<string> qualities = BuildPriorityMetatypeQualities(metatype, metavariant);
        string sumToTenLabel = string.Equals(buildMethod, "SumToTen", StringComparison.OrdinalIgnoreCase)
            ? $"{GetPriorityLetterValue(heritagePriority) + GetPriorityLetterValue(attributesPriority) + GetPriorityLetterValue(talentPriority) + GetPriorityLetterValue(skillsPriority) + GetPriorityLetterValue(resourcesPriority)}/10"
            : string.Empty;

        return new PriorityWorkflowDialogRuntimeState(
            Mode: buildMethod,
            SumToTenLabel: sumToTenLabel,
            MetavariantOptions: BuildMetavariantOptions(metatype),
            SelectedMetavariant: metavariant,
            MetatypeKarma: ResolveMetatypeKarma(metatype, metavariant),
            SpecialAttributes: ResolveSpecialAttributePool(heritagePriority),
            Source: ResolveMetatypeSource(rulesetId, metatype, metavariant),
            InspectAttributes: inspectAttributes,
            Qualities: qualities,
            ForceVisible: false,
            Force: Math.Max(1, force),
            PossessionVisible: false,
            PossessionBased: possessionBased,
            PossessionMethodOptions: possessionMethodOptions,
            SelectedPossessionMethod: resolvedPossessionMethod,
            SkillSelectionLabel: skillSelectionLabel,
            SkillChoice1: skillChoice1,
            SkillChoice2: skillChoice2,
            SkillChoice3: skillChoice3,
            CanCommit: !string.IsNullOrWhiteSpace(metatype)
                && !string.IsNullOrWhiteSpace(talentChoice)
                && (!skillChoice1.Visible || !string.IsNullOrWhiteSpace(skillChoice1.Value))
                && (!skillChoice2.Visible || !string.IsNullOrWhiteSpace(skillChoice2.Value))
                && (!skillChoice3.Visible || !string.IsNullOrWhiteSpace(skillChoice3.Value))
                && DistinctVisibleSkillChoices(skillChoice1, skillChoice2, skillChoice3));
    }

    private static bool DistinctVisibleSkillChoices(params PriorityWorkflowChoiceState[] skillChoices)
    {
        string[] visibleValues = skillChoices
            .Where(choice => choice.Visible && !string.IsNullOrWhiteSpace(choice.Value))
            .Select(choice => choice.Value)
            .ToArray();
        return visibleValues.Length == visibleValues.Distinct(StringComparer.Ordinal).Count();
    }

    private static IReadOnlyList<PriorityWorkflowInspectAttributeState> BuildPriorityInspectAttributes(string metatype, string metavariant)
    {
        IReadOnlyDictionary<string, string> values = (metatype, metavariant) switch
        {
            ("Elf", "Dryad") => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "1 / 6",
                ["AGI"] = "2 / 7",
                ["REA"] = "1 / 6",
                ["STR"] = "1 / 6",
                ["CHA"] = "5 / 8",
                ["INT"] = "1 / 6",
                ["LOG"] = "1 / 6",
                ["WIL"] = "1 / 6"
            },
            ("Elf", _) => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "1 / 6",
                ["AGI"] = "2 / 7",
                ["REA"] = "1 / 6",
                ["STR"] = "1 / 6",
                ["CHA"] = "3 / 8",
                ["INT"] = "1 / 6",
                ["LOG"] = "1 / 6",
                ["WIL"] = "1 / 6"
            },
            ("Dwarf", "Gnome") => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "2 / 7",
                ["AGI"] = "1 / 6",
                ["REA"] = "1 / 5",
                ["STR"] = "2 / 7",
                ["CHA"] = "1 / 6",
                ["INT"] = "1 / 7",
                ["LOG"] = "1 / 7",
                ["WIL"] = "2 / 7"
            },
            ("Dwarf", _) => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "3 / 8",
                ["AGI"] = "1 / 6",
                ["REA"] = "1 / 5",
                ["STR"] = "3 / 8",
                ["CHA"] = "1 / 6",
                ["INT"] = "1 / 6",
                ["LOG"] = "1 / 6",
                ["WIL"] = "2 / 7"
            },
            ("Ork", "Hobgoblin") => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "3 / 8",
                ["AGI"] = "2 / 7",
                ["REA"] = "1 / 6",
                ["STR"] = "2 / 7",
                ["CHA"] = "1 / 5",
                ["INT"] = "1 / 6",
                ["LOG"] = "1 / 5",
                ["WIL"] = "1 / 6"
            },
            ("Ork", _) => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "4 / 9",
                ["AGI"] = "1 / 6",
                ["REA"] = "1 / 6",
                ["STR"] = "3 / 8",
                ["CHA"] = "1 / 5",
                ["INT"] = "1 / 6",
                ["LOG"] = "1 / 5",
                ["WIL"] = "1 / 6"
            },
            ("Troll", "Cyclops") => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "5 / 10",
                ["AGI"] = "1 / 4",
                ["REA"] = "1 / 6",
                ["STR"] = "5 / 10",
                ["CHA"] = "1 / 4",
                ["INT"] = "1 / 5",
                ["LOG"] = "1 / 5",
                ["WIL"] = "1 / 6"
            },
            ("Troll", _) => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "5 / 10",
                ["AGI"] = "1 / 5",
                ["REA"] = "1 / 6",
                ["STR"] = "5 / 10",
                ["CHA"] = "1 / 4",
                ["INT"] = "1 / 5",
                ["LOG"] = "1 / 5",
                ["WIL"] = "1 / 6"
            },
            _ => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BOD"] = "1 / 6",
                ["AGI"] = "1 / 6",
                ["REA"] = "1 / 6",
                ["STR"] = "1 / 6",
                ["CHA"] = "1 / 6",
                ["INT"] = "1 / 6",
                ["LOG"] = "1 / 6",
                ["WIL"] = "1 / 6"
            }
        };

        return new[]
        {
            new PriorityWorkflowInspectAttributeState("BOD", values["BOD"]),
            new PriorityWorkflowInspectAttributeState("AGI", values["AGI"]),
            new PriorityWorkflowInspectAttributeState("REA", values["REA"]),
            new PriorityWorkflowInspectAttributeState("STR", values["STR"]),
            new PriorityWorkflowInspectAttributeState("CHA", values["CHA"]),
            new PriorityWorkflowInspectAttributeState("INT", values["INT"]),
            new PriorityWorkflowInspectAttributeState("LOG", values["LOG"]),
            new PriorityWorkflowInspectAttributeState("WIL", values["WIL"])
        };
    }

    private static IReadOnlyList<string> BuildPriorityMetatypeQualities(string metatype, string metavariant)
    {
        List<string> qualities = metatype switch
        {
            "Elf" => ["Low-Light Vision"],
            "Dwarf" => ["Thermographic Vision", "Toxin Resistance", "Pathogen Resistance"],
            "Ork" => ["Low-Light Vision"],
            "Troll" => ["Thermographic Vision", "Reach +1", "Dermal Armor +1"],
            _ => []
        };

        if (string.Equals(metavariant, "Dryad", StringComparison.Ordinal))
        {
            qualities.Add("Glamour");
        }
        else if (string.Equals(metavariant, "Gnome", StringComparison.Ordinal))
        {
            qualities.Add("Arcane Arrester");
        }
        else if (string.Equals(metavariant, "Cyclops", StringComparison.Ordinal))
        {
            qualities.Add("One Eye");
        }

        return qualities;
    }

    private static string ResolveMetatypeKarma(string metatype, string metavariant)
    {
        return (metatype, metavariant) switch
        {
            ("Elf", "Dryad") => "35",
            ("Elf", _) => "30",
            ("Dwarf", "Gnome") => "30",
            ("Dwarf", _) => "25",
            ("Ork", "Hobgoblin") => "25",
            ("Ork", _) => "20",
            ("Troll", "Cyclops") => "45",
            ("Troll", _) => "40",
            _ => "0"
        };
    }

    private static string ResolveSpecialAttributePool(string heritagePriority)
    {
        return heritagePriority switch
        {
            "A" => "4",
            "B" => "3",
            "C" => "2",
            "D" => "1",
            _ => "0"
        };
    }

    private static int ResolvePriorityHeritageRank(string heritagePriority)
        => heritagePriority switch
        {
            "A" => 4,
            "B" => 3,
            "C" => 2,
            "D" => 1,
            _ => 0
        };

    private static int ResolveMinimumHeritageRank(string metatype)
        => metatype switch
        {
            "Troll" => 4,
            "Dwarf" => 3,
            "Ork" => 2,
            "Shapeshifter: Vulpine" => 2,
            "Elf" => 1,
            _ => 0
        };

    private static string ResolveMetatypeSource(string rulesetId, string metatype, string metavariant)
    {
        string page = metatype switch
        {
            "Elf" => "64",
            "Dwarf" => "65",
            "Ork" => "66",
            "Troll" => "67",
            _ => "64"
        };
        if (!string.Equals(metatype, metavariant, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(metavariant))
        {
            return $"Run Faster · {metavariant}";
        }

        return $"{rulesetId.ToUpperInvariant()} Core Rulebook p. {page}";
    }

    private sealed record PriorityWorkflowResolution(
        string Category,
        string Metatype,
        string HeritagePriority,
        string AttributesPriority,
        string TalentPriority,
        string SkillsPriority,
        string ResourcesPriority,
        IReadOnlyList<DesktopDialogFieldOption> MetatypeOptions,
        IReadOnlyList<DesktopDialogFieldOption> TalentOptions,
        string TalentChoice,
        string Metavariant,
        string SkillChoice1,
        string SkillChoice2,
        string SkillChoice3,
        PriorityWorkflowDialogRuntimeState RuntimeState);

    private static string ResolvePreferredBuildMethod(string rulesetId, string? preferredBuildMethod)
    {
        DesktopDialogFieldOption[] options = BuildBuildMethodOptions(rulesetId).ToArray();
        string normalizedPreferred = NormalizeBuildMethodValue(preferredBuildMethod);
        return options
            .Select(option => option.Value)
            .FirstOrDefault(option => string.Equals(
                NormalizeBuildMethodValue(option),
                normalizedPreferred,
                StringComparison.Ordinal))
            ?? options[0].Value;
    }

    private static string NormalizeBuildMethodValue(string? buildMethod)
    {
        string normalized = string.IsNullOrWhiteSpace(buildMethod) ? string.Empty : buildMethod.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "priority" => "Priority",
            "karma" => "Karma",
            "bp" => "BP",
            "lifemodule" => "LifeModule",
            "life modules" => "LifeModule",
            "sumtoten" => "SumToTen",
            "sum-to-ten" => "SumToTen",
            _ => normalized
        };
    }

    private static bool UsesPriorityWorkflow(string buildMethod)
        => string.Equals(buildMethod, "Priority", StringComparison.Ordinal)
            || string.Equals(buildMethod, "SumToTen", StringComparison.Ordinal);

    private static string BuildNewCharacterMessage(
        string rulesetId,
        string buildMethod,
        bool houseRulesEnabled)
    {
        string route = UsesPriorityWorkflow(buildMethod)
            ? "Next you will choose metatype and priorities."
            : "Next you will choose metatype.";
        string houseRules = houseRulesEnabled
            ? " House rules are enabled in Character Settings."
            : " House rules are currently disabled.";
        return $"Choose the ruleset and build method for {rulesetId.ToUpperInvariant()}. {route}{houseRules}";
    }

    private static string BuildNewCharacterPriorityWorkflowSummary(
        string rulesetId,
        string buildMethod,
        string category,
        string metatype,
        string heritagePriority,
        string attributesPriority,
        string talentPriority,
        string skillsPriority,
        string resourcesPriority,
        string talentChoice,
        bool houseRulesEnabled)
    {
        string[] lines =
        [
            $"Route | {rulesetId.ToUpperInvariant()} {buildMethod}",
            $"Metatype | {BuildMetatypeSummaryValue(metatype, category)}",
            $"Priority Ladder | Heritage {heritagePriority}, Attributes {attributesPriority}, Talent {talentPriority}, Skills {skillsPriority}, Resources {resourcesPriority}",
            $"Talent Choice | {talentChoice}",
            $"House Rules | {(houseRulesEnabled ? "Enabled" : "Disabled")}"
        ];
        if (string.Equals(buildMethod, "SumToTen", StringComparison.Ordinal))
        {
            int total = GetPriorityLetterValue(heritagePriority)
                + GetPriorityLetterValue(attributesPriority)
                + GetPriorityLetterValue(talentPriority)
                + GetPriorityLetterValue(skillsPriority)
                + GetPriorityLetterValue(resourcesPriority);
            return string.Join(Environment.NewLine, lines.Concat(new[] { $"Sum-to-Ten Total | {total}" }));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildNewCharacterKarmaWorkflowSummary(
        string rulesetId,
        string buildMethod,
        string category,
        string metatype,
        bool houseRulesEnabled)
        => string.Join(
            Environment.NewLine,
            new[]
            {
                $"Route | {rulesetId.ToUpperInvariant()} {buildMethod}",
                $"Metatype | {BuildMetatypeSummaryValue(metatype, category)}",
                BuildNewCharacterBudgetLine(rulesetId, buildMethod),
                $"House Rules | {(houseRulesEnabled ? "Enabled" : "Disabled")}"
            });

    private static string BuildNewCharacterBudgetLine(string rulesetId, string buildMethod)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string normalizedBuildMethod = ResolvePreferredBuildMethod(normalizedRulesetId, buildMethod);
        return normalizedBuildMethod switch
        {
            "BP" => "Build Points Remaining | tracked when the character opens",
            "Karma" => "Remaining Karma | tracked when the character opens",
            "LifeModule" => "Remaining Karma | tracked after life modules are selected",
            _ => "Budget | priority allocation"
        };
    }

    private static int GetPriorityLetterValue(string priority)
        => priority switch
        {
            "A" => 4,
            "B" => 3,
            "C" => 2,
            "D" => 1,
            _ => 0
        };

    private static IReadOnlyList<DesktopDialogField> BuildDiceToolFields(
        CharacterWorkspaceId? currentWorkspace,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces,
        string? rulesetId)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string ruleOf6Label = string.Equals(normalizedRulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal)
            ? "using Rule of 6"
            : "Rule of 6";
        string cinematicGameplayLabel = string.Equals(normalizedRulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal)
            ? "Hit on 4, 5, or 6"
            : "Cinematic Gameplay";
        string rushJobLabel = string.Equals(normalizedRulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal)
            ? "Rushed Job (Glitch on 1 or 2)"
            : "Rush Job";

        return
        [
            new DesktopDialogField("diceCount", "Dice", "1", "1", InputType: "number", LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("diceMethod", "Method", "Standard", "Standard", InputType: "select", Options: BuildDiceMethodOptions(), LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("diceThreshold", "Threshold", "0", "0", InputType: "number", LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("diceGremlins", "Gremlins", "0", "0", InputType: "number", LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("diceRuleOf6", ruleOf6Label, "false", "false", InputType: "checkbox"),
            new DesktopDialogField("diceCinematicGameplay", cinematicGameplayLabel, "false", "false", InputType: "checkbox"),
            new DesktopDialogField("diceRushJob", rushJobLabel, "false", "false", InputType: "checkbox"),
            new DesktopDialogField("diceVariableGlitch", "Variable Glitch", "false", "false", InputType: "checkbox"),
            new DesktopDialogField("diceBubbleDie", "Bubble Die", "false", "false", InputType: "checkbox"),
            new DesktopDialogField("diceResultsSummary", "Results", "Roll dice to see hits, glitches, and the summed total.", "Roll dice to see hits, glitches, and the summed total.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("diceResultsList", "Roll History", "No rolls yet.", "No rolls yet.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List),
            new DesktopDialogField("diceUtilityLane", "Utility Lane", "Dice roller + initiative preview + roster context", "Dice roller + initiative preview + roster context", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("diceRosterContext", "Roster Context", BuildDiceRosterContext(currentWorkspace, openWorkspaces), "No roster context.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("initiativePreview", "Initiative Preview", BuildInitiativePreview(currentWorkspace, openWorkspaces), "No active dossier.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("diceLastRollState", "Last Roll State", string.Empty, string.Empty, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden)
        ];
    }

    private static string BuildDiceRosterContext(
        CharacterWorkspaceId? currentWorkspace,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces)
    {
        OpenWorkspaceState[] roster = (openWorkspaces ?? Array.Empty<OpenWorkspaceState>())
            .OrderByDescending(workspace => workspace.LastOpenedUtc)
            .ToArray();
        OpenWorkspaceState? active = currentWorkspace is null
            ? roster.FirstOrDefault()
            : roster.FirstOrDefault(workspace => workspace.Id.Equals(currentWorkspace.Value)) ?? roster.FirstOrDefault();
        string activeRunner = active is null
            ? "none"
            : $"{active.Alias} · {active.Name} [{active.RulesetId}]";
        string openSummary = roster.Length == 0
            ? "none"
            : string.Join(", ", roster.Select(workspace => $"{workspace.Alias}/{workspace.RulesetId}"));

        return BuildGridValue(
            ("Active Dossier", activeRunner),
            ("Open Dossiers", roster.Length.ToString(CultureInfo.InvariantCulture)),
            ("Roster Mix", openSummary));
    }

    private static string BuildInitiativePreview(
        CharacterWorkspaceId? currentWorkspace,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces)
    {
        OpenWorkspaceState[] roster = (openWorkspaces ?? Array.Empty<OpenWorkspaceState>())
            .OrderByDescending(workspace => workspace.LastOpenedUtc)
            .ToArray();
        OpenWorkspaceState? active = currentWorkspace is null
            ? roster.FirstOrDefault()
            : roster.FirstOrDefault(workspace => workspace.Id.Equals(currentWorkspace.Value)) ?? roster.FirstOrDefault();

        if (active is null)
        {
            return "No active dossier. Roll history stays available and initiative context appears after opening a roster entry.";
        }

        return $"{active.Alias} · {active.Name} [{active.RulesetId}]{Environment.NewLine}Initiative preview uses the active dossier and keeps results local to this utility.";
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildDiceMethodOptions()
        => new[]
        {
            new DesktopDialogFieldOption("Standard", "Standard"),
            new DesktopDialogFieldOption("Large", "Large"),
            new DesktopDialogFieldOption("ReallyLarge", "Really Large")
        };

    internal static DesktopDialogState BuildGlobalSettingsDialog(
        DesktopPreferenceState preferences,
        string language,
        string? activePane = null)
    {
        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(language);
        string S(string key) => DesktopLocalizationCatalog.GetRequiredString(key, normalizedLanguage);
        string F(string key, params object[] values) => DesktopLocalizationCatalog.GetRequiredFormattedString(key, normalizedLanguage, values);

        return new DesktopDialogState(
            "dialog.global_settings",
            S("desktop.dialog.global_settings.title"),
            F("desktop.dialog.global_settings.message", DesktopLocalizationCatalog.BuildSupportedLanguageSummary()),
            BuildGlobalSettingsFields(preferences, normalizedLanguage, S),
            [
                new DesktopDialogAction("save", "Save", true),
                new DesktopDialogAction("cancel", S("desktop.dialog.action.cancel"))
            ]);
    }

    internal static DesktopPreferenceState ParseGlobalSettingsPreferences(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        bool preferNightlyBuilds = DesktopDialogFieldValueParser.ParseBool(
            dialog,
            "globalPreferNightlyBuilds",
            UsesPreviewUpdateChannel(DesktopDialogFieldValueParser.GetValue(dialog, "globalUpdatePolicy") ?? fallback.UpdateChannel));
        string updateMode = DesktopPreferenceStateRuntime.NormalizeUpdateMode(
            DesktopDialogFieldValueParser.GetValue(dialog, "globalUpdateMode"),
            DesktopDialogFieldValueParser.ParseBool(dialog, "globalCheckForUpdates", fallback.CheckForUpdatesOnLaunch));

        return DesktopPreferenceStateRuntime.Normalize(fallback with
        {
            UiScalePercent = DesktopDialogFieldValueParser.ParseInt(dialog, "globalUiScale", fallback.UiScalePercent),
            Theme = DesktopDialogFieldValueParser.GetValue(dialog, "globalTheme") ?? fallback.Theme,
            Language = DesktopDialogFieldValueParser.GetValue(dialog, "globalLanguage") ?? fallback.Language,
            SheetLanguage = DesktopDialogFieldValueParser.GetValue(dialog, "globalSheetLanguage") ?? fallback.SheetLanguage,
            CompactMode = DesktopDialogFieldValueParser.ParseBool(dialog, "globalCompactMode", fallback.CompactMode),
            CharacterPriority = DesktopDialogFieldValueParser.GetValue(dialog, "globalCharacterPriority") ?? fallback.CharacterPriority,
            KarmaNuyenRatio = DesktopDialogFieldValueParser.ParseInt(dialog, "globalKarmaNuyenRatio", fallback.KarmaNuyenRatio),
            HouseRulesEnabled = DesktopDialogFieldValueParser.ParseBool(dialog, "globalHouseRulesEnabled", fallback.HouseRulesEnabled),
            StartupBehavior = DesktopDialogFieldValueParser.GetValue(dialog, "globalStartupBehavior") ?? fallback.StartupBehavior,
            UpdateChannel = preferNightlyBuilds
                ? "Preview channel · check weekly"
                : "Stable channel · check weekly",
            CheckForUpdatesOnLaunch = updateMode != "off",
            UpdateMode = updateMode,
            CharacterRosterPath = DesktopDialogFieldValueParser.GetValue(dialog, "globalCharacterRosterPath") ?? fallback.CharacterRosterPath,
            RosterHierarchyJson = DesktopDialogFieldValueParser.GetValue(dialog, "globalRosterHierarchyJson") ?? fallback.RosterHierarchyJson,
            PdfViewerPath = DesktopDialogFieldValueParser.GetValue(dialog, "globalPdfViewerPath") ?? fallback.PdfViewerPath,
            VisibleChromePolicy = DesktopDialogFieldValueParser.GetValue(dialog, "globalVisibilityPolicy") ?? fallback.VisibleChromePolicy,
            HideMasterIndex = DesktopDialogFieldValueParser.ParseBool(dialog, "globalHideMasterIndex", fallback.HideMasterIndex),
            AnalyticsOptIn = ParseGlobalAnalyticsOptIn(dialog, fallback),
            DisableAiFeatures = DesktopDialogFieldValueParser.ParseBool(dialog, "globalDisableAiFeatures", fallback.DisableAiFeatures)
        });
    }

    internal static DesktopDialogState RebuildGlobalSettingsDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        DesktopPreferenceState parsedPreferences = ParseGlobalSettingsPreferences(dialog, fallback);
        return BuildGlobalSettingsDialog(parsedPreferences, parsedPreferences.Language);
    }

    internal static DesktopDialogState RebuildDynamicDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        if (string.Equals(dialog.Id, "dialog.global_settings", StringComparison.Ordinal))
            return RebuildGlobalSettingsDialog(dialog, fallback);

        if (string.Equals(dialog.Id, Chummer5CharacterSettingsProfiles.DialogId, StringComparison.Ordinal))
            return RebuildCharacterSettingsDialog(dialog, fallback);

        return HumanizeVisibleDialog(dialog.Id switch
        {
            DesktopAliceAssistant.DialogId => dialog,
            "dialog.new_character" => RebuildNewCharacterDialog(dialog, fallback),
            NewCharacterOriginWizardDialogId => RebuildNewCharacterOriginWizardDialog(dialog, fallback),
            NewCharacterPriorityWorkflowDialogId => RebuildNewCharacterPriorityWorkflowDialog(dialog, fallback),
            NewCharacterKarmaWorkflowDialogId => RebuildNewCharacterKarmaWorkflowDialog(dialog, fallback),
            "dialog.dice_roller" => RebuildDiceRollerDialog(dialog),
            "dialog.character_roster" => RebuildCharacterRosterDialog(dialog, fallback),
            "dialog.master_index" => RebuildMasterIndexDialog(dialog),
            "dialog.ui.quality_add" => RebuildQualitySelectionDialog(dialog),
            "dialog.ui.cyberware_add" => RebuildCyberwareSelectionDialog(dialog),
            "dialog.ui.gear_add" => RebuildGearSelectionDialog(dialog),
            "dialog.ui.combat_add_weapon" => RebuildWeaponSelectionDialog(dialog),
            "dialog.ui.combat_add_armor" => RebuildArmorSelectionDialog(dialog),
            "dialog.ui.skill_add" => RebuildSkillSelectionDialog(dialog),
            "dialog.ui.vehicle_add" => RebuildVehicleSelectionDialog(dialog),
            "dialog.ui.cyberware_edit" => RebuildCyberwareEditDialog(dialog),
            "dialog.ui.gear_edit" => RebuildGearEditDialog(dialog),
            "dialog.ui.vehicle_edit" => RebuildVehicleEditDialog(dialog),
            _ => dialog
        });
    }

    private static DesktopDialogState HumanizeVisibleDialog(DesktopDialogState dialog)
        => dialog with
        {
            Title = UndetectableHumanizerCopyAdapter.Humanize(dialog.Title),
            Message = string.IsNullOrWhiteSpace(dialog.Message)
                ? dialog.Message
                : UndetectableHumanizerCopyAdapter.Humanize(dialog.Message),
            Fields = dialog.Fields.Select(HumanizeVisibleField).ToArray(),
            Actions = dialog.Actions
                .Select(action => action with { Label = UndetectableHumanizerCopyAdapter.Humanize(action.Label) })
                .ToArray()
        };

    private static DesktopDialogField HumanizeVisibleField(DesktopDialogField field)
    {
        if (string.Equals(field.LayoutSlot, DesktopDialogFieldLayoutSlots.Hidden, StringComparison.Ordinal))
            return field;

        return field with
        {
            Label = UndetectableHumanizerCopyAdapter.Humanize(field.Label),
            Value = UndetectableHumanizerCopyAdapter.Humanize(field.Value),
            Placeholder = UndetectableHumanizerCopyAdapter.Humanize(field.Placeholder),
            Options = field.Options?
                .Select(option => new DesktopDialogFieldOption(
                    UndetectableHumanizerCopyAdapter.Humanize(option.Value),
                    UndetectableHumanizerCopyAdapter.Humanize(option.Label)))
                .ToArray()
        };
    }

    private static DesktopDialogState RebuildNewCharacterDialog(DesktopDialogState dialog, DesktopPreferenceState fallback)
    {
        string rulesetId = RulesetDefaults.NormalizeOptional(
                DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterRulesetId"))
            ?? RulesetDefaults.Sr5;
        string preferredBuildMethod = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPreferredBuildMethod") ?? string.Empty;
        bool houseRulesEnabled = DesktopDialogFieldValueParser.ParseBool(dialog, "newCharacterHouseRulesEnabled", false);
        DesktopPreferenceState preferences = BuildNewCharacterDialogPreferences(dialog, fallback);
        DesktopDialogFieldOption[] buildMethodOptions = BuildBuildMethodOptions(rulesetId).ToArray();
        string currentBuildMethod = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterBuildMethod") ?? string.Empty;
        string resolvedBuildMethod = buildMethodOptions.Any(option => string.Equals(option.Value, currentBuildMethod, StringComparison.Ordinal))
            ? currentBuildMethod
            : ResolvePreferredBuildMethod(rulesetId, preferredBuildMethod);

        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field => field.Id switch
            {
                "newCharacterRulesetId" => field with
                {
                    Value = rulesetId,
                    Placeholder = rulesetId,
                    Options = BuildRulesetOptions()
                },
                "newCharacterBuildMethod" => field with
                {
                    Value = resolvedBuildMethod,
                    Placeholder = resolvedBuildMethod,
                    Options = buildMethodOptions
                },
                "newCharacterDisableAiFeatures" => field with
                {
                    Value = preferences.DisableAiFeatures ? "true" : "false",
                    Placeholder = preferences.DisableAiFeatures ? "true" : "false"
                },
                _ => field
            })
            .ToArray();

        return dialog with
        {
            Message = BuildNewCharacterMessage(rulesetId, resolvedBuildMethod, houseRulesEnabled),
            Fields = updatedFields
        };
    }

    private static DesktopPreferenceState BuildNewCharacterDialogPreferences(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
        => DesktopPreferenceStateRuntime.Normalize(fallback with
        {
            DisableAiFeatures = DesktopDialogFieldValueParser.ParseBool(
                dialog,
                "newCharacterDisableAiFeatures",
                fallback.DisableAiFeatures)
        });

    private static DesktopDialogState RebuildNewCharacterOriginWizardDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        string rulesetId = RulesetDefaults.NormalizeOptional(
                DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterRulesetId"))
            ?? RulesetDefaults.Sr5;
        DesktopPreferenceState preferences = BuildNewCharacterDialogPreferences(dialog, fallback);
        OriginBuildRecommendation recommendation = ResolveOriginBuildRecommendation(
            rulesetId,
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginArchetypeIntent"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginBuildPreference"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginMetatypePreference"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginBackground"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginTurningPoint"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginTrainingPath"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginPressureCost"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginUpgradeExposure"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginMotivation"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginTone"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginGmConstraintPreset"),
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterOriginGmRequirements"));

        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field => field.Id switch
            {
                "newCharacterRulesetId" => field with
                {
                    Value = rulesetId,
                    Placeholder = rulesetId,
                    Options = BuildRulesetOptions()
                },
                "newCharacterOriginBuildPreference" => field with
                {
                    Options = BuildOriginBuildPreferenceOptions(rulesetId)
                },
                "newCharacterOriginMetatypePreference" => field with
                {
                    Options = BuildOriginMetatypeOptions(preferences)
                },
                "newCharacterOriginGmConstraintPreset" => field with
                {
                    Options = BuildOriginGmRequirementPresetOptions()
                },
                "newCharacterOriginSummary" => field with
                {
                    Value = recommendation.OriginSummary,
                    Placeholder = recommendation.OriginSummary
                },
                "newCharacterOriginArchetype" => field with
                {
                    Value = recommendation.ArchetypeLabel,
                    Placeholder = recommendation.ArchetypeLabel
                },
                "newCharacterOriginBuildMethod" => field with
                {
                    Value = recommendation.BuildMethod,
                    Placeholder = recommendation.BuildMethod
                },
                "newCharacterOriginMetatypeCategory" => field with
                {
                    Value = recommendation.MetatypeCategory,
                    Placeholder = recommendation.MetatypeCategory
                },
                "newCharacterOriginMetatype" => field with
                {
                    Value = recommendation.Metatype,
                    Placeholder = recommendation.Metatype
                },
                "newCharacterOriginQualityFocus" => field with
                {
                    Value = recommendation.QualityFocus,
                    Placeholder = recommendation.QualityFocus
                },
                "newCharacterOriginGmRequirementSummary" => field with
                {
                    Value = recommendation.GmRequirementSummary,
                    Placeholder = recommendation.GmRequirementSummary
                },
                "newCharacterOriginPathSummary" => field with
                {
                    Value = recommendation.PathSummary,
                    Placeholder = recommendation.PathSummary
                },
                _ => field
            })
            .ToArray();

        return dialog with { Fields = updatedFields };
    }

    private static DesktopDialogState RebuildNewCharacterPriorityWorkflowDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        string rulesetId = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterWorkflowRulesetId") ?? RulesetDefaults.Sr5;
        string buildMethod = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterWorkflowBuildMethod") ?? "Priority";
        string lastChangedFieldId = DesktopDialogFieldValueParser.GetValue(dialog, NewCharacterPriorityLastChangedFieldId) ?? string.Empty;
        DesktopPreferenceState preferences = BuildNewCharacterDialogPreferences(dialog, fallback);
        PriorityWorkflowResolution resolution = ResolvePriorityWorkflowResolution(
            rulesetId,
            buildMethod,
            category: DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterMetatypeCategory") ?? "Standard",
            metatype: DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterMetatype") ?? "Human",
            heritagePriority: DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPriorityHeritage") ?? "D",
            attributesPriority: DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPriorityAttributes") ?? "B",
            talentPriority: DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPriorityTalent") ?? "E",
            skillsPriority: DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPrioritySkills") ?? "C",
            resourcesPriority: DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPriorityResources") ?? "A",
            talentChoice: DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPriorityTalentChoice") ?? "Mundane",
            metavariant: DesktopDialogFieldValueParser.GetValue(dialog, NewCharacterMetavariantFieldId) ?? string.Empty,
            skillChoice1: DesktopDialogFieldValueParser.GetValue(dialog, NewCharacterPrioritySkillChoice1FieldId) ?? string.Empty,
            skillChoice2: DesktopDialogFieldValueParser.GetValue(dialog, NewCharacterPrioritySkillChoice2FieldId) ?? string.Empty,
            skillChoice3: DesktopDialogFieldValueParser.GetValue(dialog, NewCharacterPrioritySkillChoice3FieldId) ?? string.Empty,
            possessionBased: false,
            possessionMethod: string.Empty,
            force: 1,
            lastChangedFieldId: lastChangedFieldId,
            preferences);
        bool houseRulesEnabled = DesktopDialogFieldValueParser.ParseBool(dialog, "newCharacterWorkflowHouseRulesEnabled", false);
        string summary = BuildNewCharacterPriorityWorkflowSummary(
            rulesetId,
            buildMethod,
            resolution.Category,
            resolution.Metatype,
            resolution.HeritagePriority,
            resolution.AttributesPriority,
            resolution.TalentPriority,
            resolution.SkillsPriority,
            resolution.ResourcesPriority,
            resolution.TalentChoice,
            houseRulesEnabled);

        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field => field.Id switch
            {
                "newCharacterMetatypeCategory" => field with
                {
                    Value = resolution.Category,
                    Placeholder = resolution.Category,
                    Options = BuildMetatypeCategoryOptions()
                },
                "newCharacterMetatype" => field with
                {
                    Value = resolution.Metatype,
                    Placeholder = resolution.Metatype,
                    Options = resolution.MetatypeOptions
                },
                "newCharacterPriorityHeritage" => field with
                {
                    Value = resolution.HeritagePriority,
                    Placeholder = resolution.HeritagePriority,
                    Options = BuildPriorityLetterOptions()
                },
                "newCharacterPriorityAttributes" => field with
                {
                    Value = resolution.AttributesPriority,
                    Placeholder = resolution.AttributesPriority,
                    Options = BuildPriorityLetterOptions()
                },
                "newCharacterPriorityTalent" => field with
                {
                    Value = resolution.TalentPriority,
                    Placeholder = resolution.TalentPriority,
                    Options = BuildPriorityLetterOptions()
                },
                "newCharacterPrioritySkills" => field with
                {
                    Value = resolution.SkillsPriority,
                    Placeholder = resolution.SkillsPriority,
                    Options = BuildPriorityLetterOptions()
                },
                "newCharacterPriorityResources" => field with
                {
                    Value = resolution.ResourcesPriority,
                    Placeholder = resolution.ResourcesPriority,
                    Options = BuildPriorityLetterOptions()
                },
                "newCharacterPriorityTalentChoice" => field with
                {
                    Value = resolution.TalentChoice,
                    Placeholder = resolution.TalentChoice,
                    Options = resolution.TalentOptions
                },
                NewCharacterMetavariantFieldId => field with
                {
                    Value = resolution.Metavariant,
                    Placeholder = resolution.Metavariant,
                    Options = resolution.RuntimeState.MetavariantOptions,
                    LayoutSlot = resolution.RuntimeState.MetavariantOptions.Count > 1
                        ? DesktopDialogFieldLayoutSlots.Left
                        : DesktopDialogFieldLayoutSlots.Hidden
                },
                NewCharacterPrioritySkillChoice1FieldId => field with
                {
                    Value = resolution.SkillChoice1,
                    Placeholder = resolution.SkillChoice1,
                    Options = resolution.RuntimeState.SkillChoice1.Options,
                    LayoutSlot = resolution.RuntimeState.SkillChoice1.Visible
                        ? DesktopDialogFieldLayoutSlots.Left
                        : DesktopDialogFieldLayoutSlots.Hidden
                },
                NewCharacterPrioritySkillChoice2FieldId => field with
                {
                    Value = resolution.SkillChoice2,
                    Placeholder = resolution.SkillChoice2,
                    Options = resolution.RuntimeState.SkillChoice2.Options,
                    LayoutSlot = resolution.RuntimeState.SkillChoice2.Visible
                        ? DesktopDialogFieldLayoutSlots.Right
                        : DesktopDialogFieldLayoutSlots.Hidden
                },
                NewCharacterPrioritySkillChoice3FieldId => field with
                {
                    Value = resolution.SkillChoice3,
                    Placeholder = resolution.SkillChoice3,
                    Options = resolution.RuntimeState.SkillChoice3.Options,
                    LayoutSlot = resolution.RuntimeState.SkillChoice3.Visible
                        ? DesktopDialogFieldLayoutSlots.Left
                        : DesktopDialogFieldLayoutSlots.Hidden
                },
                "newCharacterPriorityWorkflowSummary" => field with
                {
                    Value = summary,
                    Placeholder = summary
                },
                NewCharacterPriorityWorkflowCanCommitFieldId => field with
                {
                    Value = resolution.RuntimeState.CanCommit ? "true" : "false",
                    Placeholder = resolution.RuntimeState.CanCommit ? "true" : "false"
                },
                NewCharacterPriorityWorkflowStateFieldId => field with
                {
                    Value = PriorityWorkflowDialogRuntimeStateSerializer.Serialize(resolution.RuntimeState),
                    Placeholder = PriorityWorkflowDialogRuntimeStateSerializer.Serialize(resolution.RuntimeState)
                },
                _ => field
            })
            .ToArray();

        return dialog with { Fields = updatedFields };
    }

    private static DesktopDialogState RebuildNewCharacterKarmaWorkflowDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        string rulesetId = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterWorkflowRulesetId") ?? RulesetDefaults.Sr5;
        string buildMethod = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterWorkflowBuildMethod") ?? "Karma";
        bool houseRulesEnabled = DesktopDialogFieldValueParser.ParseBool(dialog, "newCharacterWorkflowHouseRulesEnabled", false);
        string category = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterMetatypeCategory") ?? "Standard";
        DesktopPreferenceState preferences = BuildNewCharacterDialogPreferences(dialog, fallback);
        DesktopDialogFieldOption[] metatypeOptions = BuildMetatypeOptions(category, preferences).ToArray();
        string currentMetatype = DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterMetatype") ?? ResolveDefaultMetatype(category);
        string metatype = metatypeOptions.Any(option => string.Equals(option.Value, currentMetatype, StringComparison.Ordinal))
            ? currentMetatype
            : metatypeOptions[0].Value;
        string summary = BuildNewCharacterKarmaWorkflowSummary(
            rulesetId,
            buildMethod,
            category,
            metatype,
            houseRulesEnabled);

        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field => field.Id switch
            {
                "newCharacterMetatypeCategory" => field with
                {
                    Value = category,
                    Placeholder = category,
                    Options = BuildMetatypeCategoryOptions()
                },
                "newCharacterMetatype" => field with
                {
                    Value = metatype,
                    Placeholder = metatype,
                    Options = metatypeOptions
                },
                "newCharacterKarmaWorkflowSummary" => field with
                {
                    Value = summary,
                    Placeholder = summary
                },
                _ => field
            })
            .ToArray();

        return dialog with { Fields = updatedFields };
    }

    private static DesktopDialogState RebuildDiceRollerDialog(DesktopDialogState dialog)
    {
        string method = DesktopDialogFieldValueParser.GetValue(dialog, "diceMethod") ?? string.Empty;
        string normalizedMethod = string.Equals(method, "Large", StringComparison.OrdinalIgnoreCase)
            ? "Large"
            : string.Equals(method, "ReallyLarge", StringComparison.OrdinalIgnoreCase)
                ? "ReallyLarge"
                : "Standard";
        bool standardMethod = string.Equals(normalizedMethod, "Standard", StringComparison.Ordinal);

        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field => field.Id switch
            {
                "diceMethod" => field with
                {
                    Value = normalizedMethod,
                    Placeholder = normalizedMethod
                },
                "diceRuleOf6" => field with
                {
                    Value = standardMethod ? DesktopDialogFieldValueParser.Normalize(field, field.Value) : "false",
                    Placeholder = "false",
                    IsReadOnly = !standardMethod
                },
                _ => field
            })
            .ToArray();

        return dialog with { Fields = updatedFields };
    }

    internal static string ReadGlobalSettingsActivePane(DesktopDialogState dialog)
        => NormalizeGlobalSettingsPane(DesktopDialogFieldValueParser.GetValue(dialog, "globalActivePane"));

    private static string NormalizeGlobalSettingsPane(string? activePane)
        => string.Equals(activePane, "sourcebooks", StringComparison.OrdinalIgnoreCase) ? "sourcebooks"
            : string.Equals(activePane, "updates", StringComparison.OrdinalIgnoreCase) ? "updates"
            : string.Equals(activePane, "paths", StringComparison.OrdinalIgnoreCase) ? "paths"
            : "general";

    private static IReadOnlyList<DesktopDialogAction> BuildRosterActions(
        string name,
        string alias,
        string workspace,
        CharacterWorkspaceId? currentWorkspace,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces,
        DesktopPreferenceState preferences)
    {
        IReadOnlyList<OpenWorkspaceState> roster = openWorkspaces ?? Array.Empty<OpenWorkspaceState>();
        OpenWorkspaceState[] ordered = roster
            .OrderByDescending(candidate => candidate.LastOpenedUtc)
            .ThenBy(candidate => candidate.Alias, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ThenBy(candidate => RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Id.Value, StringComparer.Ordinal)
            .ToArray();
        OpenWorkspaceState? selectedRunner = ordered.FirstOrDefault(candidate => currentWorkspace is not null
            && string.Equals(candidate.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal))
            ?? ordered.FirstOrDefault();

        string rosterPath = string.IsNullOrWhiteSpace(preferences.CharacterRosterPath)
            ? DesktopPreferenceState.Default.CharacterRosterPath
            : preferences.CharacterRosterPath.Trim();
        bool watchFolderConfigured = !string.IsNullOrWhiteSpace(rosterPath);
        bool watchFolderExists = watchFolderConfigured && Directory.Exists(rosterPath);
        string[] watchedFiles = watchFolderExists
            ? Directory.EnumerateFiles(rosterPath, "*", SearchOption.AllDirectories)
                .Where(path =>
                {
                    string extension = Path.GetExtension(path);
                    return string.Equals(extension, ".chum5", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".chum6", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase);
                })
                .Select(path => Path.GetRelativePath(rosterPath, path))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();
        string[] watchFileHints = BuildRosterWatchFileHints(selectedRunner, alias, name, workspace);
        string? selectedWatchedFile = watchedFiles.FirstOrDefault(file =>
        {
            string fileStem = Path.GetFileNameWithoutExtension(file);
            return watchFileHints.Any(candidate => string.Equals(candidate, fileStem, StringComparison.OrdinalIgnoreCase));
        });
        (string portraitCandidate, _, _) = ResolveRosterPortraitCandidate(rosterPath, selectedWatchedFile, selectedRunner, alias, name, workspace);
        bool hasPortrait = File.Exists(portraitCandidate);

        List<DesktopDialogAction> actions = [];
        if (selectedRunner is not null)
        {
            actions.Add(new DesktopDialogAction("open_runner", $"Open Dossier {selectedRunner.Alias}", true));
        }
        else
        {
            actions.Add(new DesktopDialogAction("open_runner", $"Open Dossier {alias}", true));
        }

        if (!string.IsNullOrWhiteSpace(selectedWatchedFile))
        {
            actions.Add(new DesktopDialogAction("open_watch_file", $"Open Watch File {Path.GetFileName(selectedWatchedFile)}"));
        }

        actions.Add(new DesktopDialogAction(
            "open_roster_folder",
            watchFolderConfigured ? (watchFolderExists ? "Open Roster Folder" : "Create Roster Folder") : "Configure Roster Folder"));

        actions.Add(new DesktopDialogAction(
            "refresh_watch_folder",
            watchFolderConfigured
                ? (watchFolderExists ? "Refresh Watch Folder" : "Create and Refresh Watch Folder")
                : "Scan Watch Folder Now"));

        actions.Add(new DesktopDialogAction("create_roster_group", "Create Roster Directory"));
        actions.Add(new DesktopDialogAction("rename_roster_group", "Rename Roster Directory"));
        actions.Add(new DesktopDialogAction("delete_roster_group", "Delete Roster Directory"));
        actions.Add(new DesktopDialogAction("move_runner_to_group", "Move Dossier to Directory"));
        actions.Add(new DesktopDialogAction("reorder_roster_tree", "Reorder Character Tree"));
        actions.Add(new DesktopDialogAction("reset_roster_hierarchy", "Reset Character Layout"));

        if (hasPortrait)
        {
            actions.Add(new DesktopDialogAction("open_portrait", $"Open Portrait {Path.GetFileName(portraitCandidate)}"));
        }
        else
        {
            actions.Add(new DesktopDialogAction("open_portrait", "Open Portrait Slot"));
        }

        actions.Add(new DesktopDialogAction("close", "Close"));
        return actions;
    }

    private static IReadOnlyList<DesktopDialogField> BuildRosterFields(
        string name,
        string alias,
        string workspace,
        CharacterWorkspaceId? currentWorkspace,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces,
        DesktopPreferenceState preferences)
    {
        IReadOnlyList<OpenWorkspaceState> roster = openWorkspaces ?? Array.Empty<OpenWorkspaceState>();
        OpenWorkspaceState[] ordered = roster
            .OrderByDescending(candidate => candidate.LastOpenedUtc)
            .ThenBy(candidate => candidate.Alias, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ThenBy(candidate => RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Id.Value, StringComparer.Ordinal)
            .ToArray();
        int savedCount = ordered.Count(candidate => candidate.HasSavedWorkspace);
        string rosterPath = string.IsNullOrWhiteSpace(preferences.CharacterRosterPath)
            ? DesktopPreferenceState.Default.CharacterRosterPath
            : preferences.CharacterRosterPath.Trim();
        bool watchFolderConfigured = !string.IsNullOrWhiteSpace(rosterPath);
        bool watchFolderExists = watchFolderConfigured && Directory.Exists(rosterPath);
        string[] watchedFiles = watchFolderExists
            ? Directory.EnumerateFiles(rosterPath, "*", SearchOption.AllDirectories)
                .Where(path =>
                {
                    string extension = Path.GetExtension(path);
                    return string.Equals(extension, ".chum5", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".chum6", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase);
                })
                .Select(path => Path.GetRelativePath(rosterPath, path))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();
        OpenWorkspaceState[] savedCandidates = ordered.Where(candidate => candidate.HasSavedWorkspace).ToArray();
        int watchedCount = watchedFiles.Length;
        OpenWorkspaceState? selectedRunner = ordered.FirstOrDefault(candidate => currentWorkspace is not null
            && string.Equals(candidate.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal))
            ?? ordered.FirstOrDefault();
        string[] watchFileHints = BuildRosterWatchFileHints(selectedRunner, alias, name, workspace);
        string? selectedWatchedFile = watchedFiles.FirstOrDefault(file =>
        {
            string fileStem = Path.GetFileNameWithoutExtension(file);
            return watchFileHints.Any(candidate => string.Equals(candidate, fileStem, StringComparison.OrdinalIgnoreCase));
        });
        string rulesetMix = ordered.Length == 0
            ? "(none)"
            : string.Join(", ", ordered
                .Select(candidate => RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId)
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.Ordinal));
        string rosterEntries = ordered.Length == 0
            ? $"{alias} · {name} · {(string.IsNullOrWhiteSpace(workspace) ? "(no runner)" : workspace)}"
            : string.Join(
                Environment.NewLine,
                ordered.Select(candidate =>
                    $"{(selectedRunner is not null && string.Equals(candidate.Id.Value, selectedRunner.Id.Value, StringComparison.Ordinal) ? ">" : " ")} {candidate.Alias} · {candidate.Name} · {(RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId)} · {(candidate.HasSavedWorkspace ? "saved" : "unsaved")} · opened {candidate.LastOpenedUtc:MM-dd HH:mm} UTC"));
        string watchFolderTree = !watchFolderConfigured
            ? "└─ not configured"
            : !watchFolderExists
                ? $"└─ {rosterPath}{Environment.NewLine}   ├─ watcher: configured{Environment.NewLine}   └─ folder missing on disk"
                : watchedFiles.Length == 0
                    ? $"└─ {rosterPath}{Environment.NewLine}   ├─ watcher: FileSystemWatcher (subdirectories){Environment.NewLine}   └─ no dossier files detected"
                    : $"└─ {rosterPath}{Environment.NewLine}   ├─ watcher: FileSystemWatcher (subdirectories){Environment.NewLine}{string.Join(Environment.NewLine, watchedFiles.Select((fileName, index) => $"{(index == watchedFiles.Length - 1 ? "   └─ " : "   ├─ ")}{(string.Equals(fileName, selectedWatchedFile, StringComparison.OrdinalIgnoreCase) ? "* " : string.Empty)}{fileName}"))}";
        string rosterTree = ordered.Length == 0
            ? $"[Open Dossiers]{Environment.NewLine}└─ {alias} · {name}{Environment.NewLine}[Watch Folder]{Environment.NewLine}{watchFolderTree}"
            : $"[Open Dossiers]{Environment.NewLine}{string.Join(Environment.NewLine, ordered.Select(candidate => $"└─ {(selectedRunner is not null && string.Equals(candidate.Id.Value, selectedRunner.Id.Value, StringComparison.Ordinal) ? "*" : "-")} {candidate.Alias} · {candidate.Name} [{(RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId)}]"))}{Environment.NewLine}[Watch Folder]{Environment.NewLine}{watchFolderTree}";
        string customRosterFolders = BuildCustomRosterFolderPreview(ordered, watchedFiles, selectedRunner, selectedWatchedFile, alias, name);
        string rosterMoveTargets = BuildGridValue(
            ("Drop Target", selectedRunner is null ? "New directory or watched file" : $"{selectedRunner.Alias} directory"),
            ("Default Directory", selectedRunner?.HasSavedWorkspace == true ? "Saved dossiers" : $"{RosterHierarchyMetadata.InboxFolderName} / unsaved"),
            ("Tree Scope", watchFolderConfigured ? "Open dossiers + watched files + user hierarchy" : "Open dossiers + user hierarchy"),
            ("Ordering", "manual sibling order with recent-open fallback"),
            ("Persistence", "character tree metadata, not filesystem move until confirmed"),
            ("Conflict Rule", "drag creates preview; explicit Move Dossier commits"));
        string rosterDragDropGuide =
            "Drag dossier onto directory: preview move" + Environment.NewLine +
            "Drag directory onto directory: nest directory" + Environment.NewLine +
            "Drag between siblings: reorder within parent" + Environment.NewLine +
            "Keyboard: Enter/Space selects source, Enter/Space on directory drops, Escape clears source" + Environment.NewLine +
            "Drop onto Watch Folder file: link watched file" + Environment.NewLine +
            "Hold modifier while dropping: copy shortcut instead of move" + Environment.NewLine +
            "Undo last move: restore previous tree path";
        string rosterHierarchyPolicy = BuildGridValue(
            (RosterHierarchyMetadata.UserDirectoriesLabel, "custom arbitrary depth"),
            ("Character Placement", "one primary directory in the user's hierarchy, optional pinned aliases later"),
            ("Watched Files", "can appear under custom roster directories without moving disk files"),
            ("Filesystem Moves", "separate confirmation step"),
            ("Self-host Sync", "layout metadata follows owner and dossier scope"),
            ("Safe Delete", $"delete custom directory moves dossier/link items to {RosterHierarchyMetadata.InboxFolderName} and reparents child directories"),
            ("Cycle Guard", "directory drops cannot target their own descendants"),
            ("Drag Source", "dragged row wins before selected-dossier fallback"));
        string selectedRunnerSummary = selectedRunner is null
            ? BuildGridValue(
                ("Character Name", name),
                ("Alias", alias),
                ("Player Name", string.Empty),
                ("Metatype", string.Empty),
                ("Career Karma", string.Empty),
                ("Essence", string.Empty),
                ("File Path", selectedWatchedFile ?? (string.IsNullOrWhiteSpace(workspace) ? string.Empty : workspace)),
                ("Settings", string.Empty))
            : BuildGridValue(
                ("Character Name", selectedRunner.Name),
                ("Alias", selectedRunner.Alias),
                ("Player Name", string.Empty),
                ("Metatype", string.Empty),
                ("Career Karma", string.Empty),
                ("Essence", string.Empty),
                ("File Path", selectedWatchedFile ?? selectedRunner.Id.Value),
                ("Settings", string.Empty));
        string selectedRunnerBackground = string.Empty;
        string selectedRunnerNotes = string.Empty;
        string selectedRunnerStatus = string.Empty;
        (string portraitCandidate, _, string portraitMatchSource) = ResolveRosterPortraitCandidate(rosterPath, selectedWatchedFile, selectedRunner, alias, name, workspace);
        FileInfo? selectedWatchFileInfo = !string.IsNullOrWhiteSpace(selectedWatchedFile)
            ? new FileInfo(Path.Combine(rosterPath, selectedWatchedFile))
            : null;
        if (selectedWatchFileInfo is { Exists: false })
        {
            selectedWatchFileInfo = null;
        }
        FileInfo? portraitInfo = File.Exists(portraitCandidate) ? new FileInfo(portraitCandidate) : null;
        string selectionTrail = selectedRunner is null
            ? BuildGridValue(
                ("Active Dossier", $"{alias} · {name}"),
                ("Save Posture", string.IsNullOrWhiteSpace(workspace) ? "not saved yet" : "dossier available"),
                ("Watch Folder", watchFolderConfigured ? rosterPath : "not configured"),
                ("Watch File", selectedWatchedFile ?? "not matched"))
            : BuildGridValue(
                ("Active Dossier", $"{selectedRunner.Alias} · {selectedRunner.Name}"),
                ("Save Posture", selectedRunner.HasSavedWorkspace ? "saved to disk" : "not saved yet"),
                ("Watch Folder", watchFolderConfigured ? rosterPath : "not configured"),
                ("Watch File", selectedWatchedFile ?? "not matched"));
        string watchFolderStatus = BuildGridValue(
            ("Watch Folder", watchFolderConfigured ? rosterPath : "not configured"),
            ("Watcher", !watchFolderConfigured ? "inactive" : watchFolderExists ? "FileSystemWatcher active" : "configured via global settings"),
            ("Include Subdirectories", watchFolderConfigured ? "Yes" : "n/a"),
            ("Watched Files", watchedCount.ToString(CultureInfo.InvariantCulture)),
            ("Saved Dossiers", savedCount.ToString(CultureInfo.InvariantCulture)),
            ("Selected Watch File", selectedWatchedFile ?? "not matched"),
            ("Selected Updated", selectedWatchFileInfo is null ? "n/a" : $"{selectedWatchFileInfo.LastWriteTimeUtc:yyyy-MM-dd HH:mm} UTC"),
            ("Selected Bytes", selectedWatchFileInfo?.Length.ToString(CultureInfo.InvariantCulture) ?? "n/a"),
            ("Portrait Match", portraitMatchSource),
            ("Scan Posture", !watchFolderConfigured ? "configure a roster folder first" : watchFolderExists ? "folder contents surfaced in roster tree" : "folder missing on disk"));
        string runnerCommands =
            "Open selected dossier" + Environment.NewLine +
            "Save selected dossier" + Environment.NewLine +
            "Create custom roster directory" + Environment.NewLine +
            "Move selected dossier to directory" + Environment.NewLine +
            "Rename selected directory" + Environment.NewLine +
            "Undo last roster move" + Environment.NewLine +
            (selectedRunner?.HasSavedWorkspace == true ? "Open saved dossier location" : "Save dossier to roster folder");
        string watchFolderCommands = watchFolderConfigured
            ? watchFolderExists
                ? "Open roster folder" + Environment.NewLine +
                  "Refresh watched file list" + Environment.NewLine +
                  "Open selected watched dossier" + Environment.NewLine +
                  (portraitInfo is not null ? "Open matched portrait" : "Open portrait slot")
                : "Open roster folder" + Environment.NewLine +
                  "Create roster folder" + Environment.NewLine +
                  "Refresh watched file list"
            : "Configure watch folder" + Environment.NewLine +
              "Scan watch folder now" + Environment.NewLine +
              "Open imported dossier";
        string mugshotStatus = portraitCandidate;
        RosterHierarchyState rosterHierarchy = BuildRosterHierarchyState(ordered, watchedFiles, selectedRunner, selectedWatchedFile, alias, name, preferences.RosterHierarchyJson, out string rosterHierarchySource);
        customRosterFolders = BuildCustomRosterFolderPreview(rosterHierarchy);
        IReadOnlyList<DesktopDialogFieldOption> rosterTargetFolderOptions = BuildRosterFolderOptions(rosterHierarchy, IncludeSystemFolders: true);
        IReadOnlyList<DesktopDialogFieldOption> rosterSourceFolderOptions = BuildRosterFolderOptions(rosterHierarchy, IncludeSystemFolders: false);
        string rosterHierarchyStatus = BuildRosterHierarchyStatus(rosterHierarchy, rosterHierarchySource);
        rosterMoveTargets += Environment.NewLine + $"Source={rosterHierarchySource}";
        string rosterSnapshot = JsonSerializer.Serialize(
            new RosterDialogSnapshot(
                alias,
                name,
                workspace,
                ordered.Select(candidate => new RosterDialogWorkspaceSnapshot(
                    candidate.Id.Value,
                    candidate.Name,
                    candidate.Alias,
                    candidate.LastOpenedUtc,
                    candidate.RulesetId,
                    candidate.HasSavedWorkspace)).ToArray(),
                watchedFiles,
                rosterHierarchy,
                rosterHierarchySource));

        return
        [
            new DesktopDialogField("rosterSectionTabs", "Sections", "Roster" + Environment.NewLine + "Details" + Environment.NewLine + "Background" + Environment.NewLine + "Notes", "Roster", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("rosterDetailTabs", "Dossier Pages", "Description" + Environment.NewLine + "Concept" + Environment.NewLine + "Background" + Environment.NewLine + "Character Notes" + Environment.NewLine + "Game Notes", "Description", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("rosterOpenCount", "Open Dossiers", ordered.Length.ToString(), "0", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("rosterSavedCount", "Saved Dossiers", savedCount.ToString(), "0", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("rosterWatchedCount", "Watched Files", watchedCount.ToString(), "0", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterRulesetMix", "Ruleset Mix", string.IsNullOrWhiteSpace(rulesetMix) ? "(none)" : rulesetMix, "(none)", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("rosterActiveWorkspace", "Active Dossier", currentWorkspace?.Value ?? workspace, workspace, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterOpsLane", "Operator Lane", "open dossiers + save state + ruleset mix", "open dossiers + save state + ruleset mix", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterSelectedRunnerId", "Selected Dossier Id", selectedRunner?.Id.Value ?? string.Empty, string.Empty, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterSelectedRunnerAlias", "Selected Dossier Alias", selectedRunner?.Alias ?? alias, alias, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterWatchFolderPath", "Watch Folder Path", rosterPath, rosterPath, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterSelectedWatchFile", "Selected Watch File", selectedWatchedFile ?? string.Empty, string.Empty, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterPortraitPath", "Portrait Path", portraitCandidate, portraitCandidate, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterSnapshot", "Snapshot", rosterSnapshot, rosterSnapshot, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterHierarchySource", "Roster Hierarchy Source", rosterHierarchySource, rosterHierarchySource, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterTree", "Characters", rosterTree, rosterTree, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("rosterFolderName", "Directory Name", string.Empty, "New custom roster directory name or rename label", LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("rosterTargetFolder", "Target Directory", string.Empty, "Choose a directory or type a directory id/name for nesting and moves", LayoutSlot: DesktopDialogFieldLayoutSlots.Left, Options: rosterTargetFolderOptions),
            new DesktopDialogField("rosterSourceFolder", "Source Directory", string.Empty, "Choose a custom directory or type a directory id/name for rename, delete, and nesting", LayoutSlot: DesktopDialogFieldLayoutSlots.Left, Options: rosterSourceFolderOptions),
            new DesktopDialogField("rosterSourceItem", "Source Item", string.Empty, "Dragged dossier or watched-file row", LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterCustomFolders", "Custom Directories", customRosterFolders, customRosterFolders, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("rosterMoveTargets", "Move Targets", rosterMoveTargets, rosterMoveTargets, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid),
            new DesktopDialogField("rosterDragDropGuide", "Drag / Drop", rosterDragDropGuide, rosterDragDropGuide, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List),
            new DesktopDialogField("rosterHierarchyPolicy", "Hierarchy Policy", rosterHierarchyPolicy, rosterHierarchyPolicy, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid),
            new DesktopDialogField("rosterHierarchyStatus", "Hierarchy Status", rosterHierarchyStatus, rosterHierarchyStatus, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid),
            new DesktopDialogField("rosterSelectionTrail", "Selection Trail", selectionTrail, selectionTrail, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid),
            new DesktopDialogField("rosterMugshot", "Mugshot", mugshotStatus, "Dossier Mugshot", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Image, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterSelectedRunner", "Selected Dossier", selectedRunnerSummary, selectedRunnerSummary, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterWatchFolderStatus", "Watch Folder", watchFolderStatus, watchFolderStatus, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterRunnerCommands", "Dossier Commands", runnerCommands, runnerCommands, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterWatchFolderCommands", "Watch Folder Commands", watchFolderCommands, watchFolderCommands, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterSelectedRunnerStatus", "Dossier Status", selectedRunnerStatus, selectedRunnerStatus, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("rosterSelectedRunnerBackground", "Background / Concept", selectedRunnerBackground, selectedRunnerBackground, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("rosterSelectedRunnerNotes", "Bio / Concept / Notes", selectedRunnerNotes, selectedRunnerNotes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("rosterEntries", "Roster Entries", rosterEntries, rosterEntries, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List)
        ];
    }

    private static (string Path, string Status, string MatchSource) ResolveRosterPortraitCandidate(
        string rosterPath,
        string? selectedWatchedFile,
        OpenWorkspaceState? selectedRunner,
        string fallbackAlias,
        string fallbackName,
        string workspace)
    {
        string baseName = BuildRosterPortraitBaseName(selectedRunner?.Alias, selectedRunner?.Name);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = BuildRosterPortraitBaseName(fallbackAlias, fallbackName);
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = !string.IsNullOrWhiteSpace(workspace) ? workspace : "active-runner";
        }

        if (string.IsNullOrWhiteSpace(rosterPath))
        {
            return ($"{baseName}.png", string.Empty, "generated fallback slot");
        }

        if (Directory.Exists(rosterPath))
        {
            if (!string.IsNullOrWhiteSpace(selectedWatchedFile))
            {
                string watchedAbsolutePath = Path.Combine(rosterPath, selectedWatchedFile);
                string watchedDirectory = Path.GetDirectoryName(watchedAbsolutePath) ?? rosterPath;
                string watchedStem = Path.GetFileNameWithoutExtension(selectedWatchedFile);
                if (!string.IsNullOrWhiteSpace(watchedStem))
                {
                    foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".webp" })
                    {
                        string candidate = Path.Combine(watchedDirectory, $"{watchedStem}{extension}");
                        if (File.Exists(candidate))
                        {
                            return (candidate, "loaded from watched dossier sibling", "watched dossier sibling");
                        }
                    }
                }
            }

            foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".webp" })
            {
                string? candidate = Directory
                    .EnumerateFiles(rosterPath, $"{baseName}{extension}", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(candidate))
                    return (candidate, "loaded from watch folder", "alias/name search");
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedWatchedFile))
        {
            string watchedAbsolutePath = Path.Combine(rosterPath, selectedWatchedFile);
            string watchedDirectory = Path.GetDirectoryName(watchedAbsolutePath) ?? rosterPath;
            string watchedStem = Path.GetFileNameWithoutExtension(selectedWatchedFile);
            if (!string.IsNullOrWhiteSpace(watchedStem))
            {
                return (Path.Combine(watchedDirectory, $"{watchedStem}.png"), string.Empty, "watched dossier sibling");
            }
        }

        return (Path.Combine(rosterPath, $"{baseName}.png"), string.Empty, "generated fallback slot");
    }

    private static string BuildRosterPortraitBaseName(string? alias, string? name)
    {
        string candidate = !string.IsNullOrWhiteSpace(alias)
            ? alias.Trim()
            : !string.IsNullOrWhiteSpace(name)
                ? name.Trim()
                : string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        char[] sanitized = candidate
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '-')
            .ToArray();

        string normalized = new string(sanitized).Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "active-runner" : normalized;
    }



    private static RosterHierarchyState BuildRosterHierarchyState(
        IReadOnlyList<OpenWorkspaceState> ordered,
        IReadOnlyList<string> watchedFiles,
        OpenWorkspaceState? selectedRunner,
        string? selectedWatchedFile,
        string alias,
        string name,
        string? stagedHierarchyJson,
        out string hierarchySource)
    {
        RosterHierarchyState? stagedHierarchy = TryDeserializeRosterHierarchyState(stagedHierarchyJson);
        if (stagedHierarchy is not null)
        {
            hierarchySource = RosterHierarchyMetadata.StagedPreferenceSource;
            return stagedHierarchy;
        }

        hierarchySource = RosterHierarchyMetadata.GeneratedSource;

        RosterHierarchyFolderState[] folders =
        [
            new(RosterHierarchyMetadata.ActiveTableFolderId, RosterHierarchyMetadata.ActiveTableFolderName, null, 0, IsSystemFolder: true),
            new(RosterHierarchyMetadata.SavedRunnersFolderId, RosterHierarchyMetadata.SavedRunnersFolderName, null, 1, IsSystemFolder: true),
            new(RosterHierarchyMetadata.InboxFolderId, $"{RosterHierarchyMetadata.InboxFolderName} / Needs Filing", null, 2, IsSystemFolder: true),
            new(RosterHierarchyMetadata.WatchLinksFolderId, RosterHierarchyMetadata.WatchLinksFolderName, null, 3, IsSystemFolder: true)
        ];

        List<RosterHierarchyItemState> items = [];
        int sortOrder = 0;
        foreach (OpenWorkspaceState candidate in ordered)
        {
            string folderId = selectedRunner is not null && string.Equals(candidate.Id.Value, selectedRunner.Id.Value, StringComparison.Ordinal)
                ? RosterHierarchyMetadata.ActiveTableFolderId
                : candidate.HasSavedWorkspace
                    ? RosterHierarchyMetadata.SavedRunnersFolderId
                    : RosterHierarchyMetadata.InboxFolderId;
            items.Add(new RosterHierarchyItemState(
                candidate.Id.Value,
                $"{candidate.Alias} · {candidate.Name}",
                RosterHierarchyItemKinds.Workspace,
                folderId,
                WorkspaceId: candidate.Id.Value,
                SortOrder: sortOrder++));
        }

        if (items.Count == 0)
        {
            items.Add(new RosterHierarchyItemState(
                "draft-active-runner",
                $"{alias} · {name}",
                RosterHierarchyItemKinds.Workspace,
                RosterHierarchyMetadata.ActiveTableFolderId,
                SortOrder: sortOrder++));
        }

        foreach (string watchedFile in watchedFiles.Take(12))
        {
            items.Add(new RosterHierarchyItemState(
                $"watch:{watchedFile}",
                watchedFile,
                RosterHierarchyItemKinds.WatchedFile,
                RosterHierarchyMetadata.WatchLinksFolderId,
                WatchedFile: watchedFile,
                SortOrder: sortOrder++));
        }

        string? selectedItemId = selectedRunner?.Id.Value
            ?? (!string.IsNullOrWhiteSpace(selectedWatchedFile) ? $"watch:{selectedWatchedFile}" : items.FirstOrDefault()?.Id);
        RosterHierarchyMoveIntentState? pendingMove = string.IsNullOrWhiteSpace(selectedItemId)
            ? null
            : new RosterHierarchyMoveIntentState(
                selectedItemId,
                items.FirstOrDefault(item => string.Equals(item.Id, selectedItemId, StringComparison.Ordinal))?.FolderId,
                selectedRunner?.HasSavedWorkspace == true ? RosterHierarchyMetadata.SavedRunnersFolderId : RosterHierarchyMetadata.InboxFolderId,
                null,
                RosterHierarchyMoveKinds.Move,
                RequiresFilesystemConfirmation: false);

        return new RosterHierarchyState(
            folders,
            items,
            new RosterHierarchyPolicyState(
                SupportsNestedFolders: true,
                AllowsWatchedFileLinks: true,
                MovesFilesOnlyAfterConfirmation: true,
                DeleteFolderPolicy: RosterHierarchyDeletePolicies.MoveChildrenToInboxFirst,
                ConflictPolicy: "stage_preview_before_commit"),
            pendingMove);
    }


    private static RosterHierarchyState? TryDeserializeRosterHierarchyState(string? stagedHierarchyJson)
    {
        if (string.IsNullOrWhiteSpace(stagedHierarchyJson))
            return null;

        try
        {
            return RosterHierarchyStateJson.TryDeserialize(stagedHierarchyJson, out RosterHierarchyState? hierarchy)
                ? hierarchy
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildCustomRosterFolderPreview(RosterHierarchyState hierarchy)
    {
        List<string> lines = ["[Custom Roster]"];
        IReadOnlyDictionary<string, List<RosterHierarchyFolderState>> childFolders = hierarchy.Folders
            .GroupBy(folder => folder.ParentFolderId ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.OrderBy(folder => folder.SortOrder).ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);
        IReadOnlyDictionary<string, List<RosterHierarchyItemState>> childItems = hierarchy.Items
            .GroupBy(item => item.FolderId ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SortOrder).ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.Ordinal);

        AppendRosterFolderPreview(lines, childFolders, childItems, string.Empty, string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildRosterFolderOptions(
        RosterHierarchyState hierarchy,
        bool IncludeSystemFolders)
        => hierarchy.Folders
            .Where(folder => IncludeSystemFolders || !folder.IsSystemFolder)
            .OrderBy(folder => folder.IsSystemFolder ? 0 : 1)
            .ThenBy(folder => folder.ParentFolderId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(folder => folder.SortOrder)
            .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .Select(folder => new DesktopDialogFieldOption(
                folder.Id,
                folder.IsSystemFolder ? $"{folder.Name} · system" : $"{folder.Name} · custom"))
            .ToArray();

    private static string BuildRosterHierarchyStatus(
        RosterHierarchyState hierarchy,
        string hierarchySource)
    {
        int customFolderCount = hierarchy.Folders.Count(folder => !folder.IsSystemFolder);
        int systemFolderCount = hierarchy.Folders.Count(folder => folder.IsSystemFolder);
        int workspaceItemCount = hierarchy.Items.Count(item => string.Equals(item.Kind, RosterHierarchyItemKinds.Workspace, StringComparison.Ordinal));
        int watchedItemCount = hierarchy.Items.Count(item => string.Equals(item.Kind, RosterHierarchyItemKinds.WatchedFile, StringComparison.Ordinal));
        string pendingMove = hierarchy.PendingMove is null
            ? "none"
            : $"{hierarchy.PendingMove.MoveKind}: {hierarchy.PendingMove.ItemId} -> {hierarchy.PendingMove.TargetFolderId ?? "root"}";
        return BuildGridValue(
            ("Source", hierarchySource),
            ("Custom Directories", customFolderCount.ToString(CultureInfo.InvariantCulture)),
            (RosterHierarchyMetadata.SystemDirectoriesLabel, systemFolderCount.ToString(CultureInfo.InvariantCulture)),
            ("Runner Items", workspaceItemCount.ToString(CultureInfo.InvariantCulture)),
            ("Watched Links", watchedItemCount.ToString(CultureInfo.InvariantCulture)),
            ("Pending Move", pendingMove),
            ("Keyboard", "Enter/Space select or drop; Escape clears source"));
    }

    private static void AppendRosterFolderPreview(
        List<string> lines,
        IReadOnlyDictionary<string, List<RosterHierarchyFolderState>> childFolders,
        IReadOnlyDictionary<string, List<RosterHierarchyItemState>> childItems,
        string parentFolderId,
        string indent)
    {
        if (childFolders.TryGetValue(parentFolderId, out List<RosterHierarchyFolderState>? folders))
        {
            foreach (RosterHierarchyFolderState folder in folders)
            {
                lines.Add($"{indent}├─ {folder.Name}{(folder.IsSystemFolder ? " · system" : " · custom")}");
                AppendRosterFolderPreview(lines, childFolders, childItems, folder.Id, indent + "│  ");
            }
        }

        if (!string.IsNullOrWhiteSpace(parentFolderId)
            && childItems.TryGetValue(parentFolderId, out List<RosterHierarchyItemState>? items))
        {
            foreach (RosterHierarchyItemState item in items.Take(12))
            {
                lines.Add($"{indent}└─ {item.Label} · {item.Kind}");
            }
        }
    }

    private static string BuildCustomRosterFolderPreview(
        IReadOnlyList<OpenWorkspaceState> ordered,
        IReadOnlyList<string> watchedFiles,
        OpenWorkspaceState? selectedRunner,
        string? selectedWatchedFile,
        string alias,
        string name)
    {
        static string Label(OpenWorkspaceState candidate, OpenWorkspaceState? selected)
        {
            string marker = selected is not null && string.Equals(candidate.Id.Value, selected.Id.Value, StringComparison.Ordinal) ? "* " : string.Empty;
            string saveState = candidate.HasSavedWorkspace ? "saved" : "unsaved";
            return $"{marker}{candidate.Alias} · {candidate.Name} · {saveState}";
        }

        string[] active = ordered
            .Where(candidate => selectedRunner is not null && string.Equals(candidate.Id.Value, selectedRunner.Id.Value, StringComparison.Ordinal))
            .Select(candidate => $"   └─ {Label(candidate, selectedRunner)}")
            .DefaultIfEmpty($"   └─ {alias} · {name} · draft")
            .ToArray();
        string[] saved = ordered
            .Where(candidate => candidate.HasSavedWorkspace && (selectedRunner is null || !string.Equals(candidate.Id.Value, selectedRunner.Id.Value, StringComparison.Ordinal)))
            .Take(4)
            .Select(candidate => $"   ├─ {Label(candidate, selectedRunner)}")
            .DefaultIfEmpty("   └─ no saved runners yet")
            .ToArray();
        string[] inbox = ordered
            .Where(candidate => !candidate.HasSavedWorkspace && (selectedRunner is null || !string.Equals(candidate.Id.Value, selectedRunner.Id.Value, StringComparison.Ordinal)))
            .Take(4)
            .Select(candidate => $"   ├─ {Label(candidate, selectedRunner)}")
            .DefaultIfEmpty("   └─ empty")
            .ToArray();
        string[] watched = watchedFiles
            .Take(5)
            .Select(file => $"   ├─ {(string.Equals(file, selectedWatchedFile, StringComparison.OrdinalIgnoreCase) ? "* " : string.Empty)}{file}")
            .DefaultIfEmpty("   └─ no watched files linked")
            .ToArray();

        return "[Custom Roster]" + Environment.NewLine +
               $"├─ {RosterHierarchyMetadata.ActiveTableFolderName}" + Environment.NewLine +
               string.Join(Environment.NewLine, active) + Environment.NewLine +
               $"├─ {RosterHierarchyMetadata.SavedRunnersFolderName}" + Environment.NewLine +
               string.Join(Environment.NewLine, saved) + Environment.NewLine +
               $"├─ {RosterHierarchyMetadata.InboxFolderName} / Needs Filing" + Environment.NewLine +
               string.Join(Environment.NewLine, inbox) + Environment.NewLine +
               $"└─ {RosterHierarchyMetadata.WatchLinksFolderName}" + Environment.NewLine +
               string.Join(Environment.NewLine, watched);
    }

    private static string[] BuildRosterWatchFileHints(
        OpenWorkspaceState? selectedRunner,
        string fallbackAlias,
        string fallbackName,
        string workspace)
    {
        return new string?[]
        {
            selectedRunner?.Alias,
            selectedRunner?.Name,
            selectedRunner?.Id.Value,
            fallbackAlias,
            fallbackName,
            workspace
        }
        .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
        .Select(candidate => candidate!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    private static string BuildInitiativePreview(int baseValue, int diceCount, int woundModifier, int pass)
    {
        int sanitizedDiceCount = Math.Max(0, diceCount);
        int sanitizedPass = Math.Max(1, pass);
        int modifiedBase = baseValue + woundModifier;
        int min = modifiedBase + sanitizedDiceCount;
        int max = modifiedBase + (sanitizedDiceCount * 6);
        decimal average = modifiedBase + (sanitizedDiceCount * 3.5m);
        return sanitizedDiceCount == 0
            ? $"{modifiedBase} flat · pass {sanitizedPass}"
            : $"{modifiedBase} + {sanitizedDiceCount}d6 · pass {sanitizedPass} · range {min}-{max} · avg {average:0.0}";
    }

    private static IReadOnlyList<DesktopDialogField> BuildGlobalSettingsFields(
        DesktopPreferenceState preferences,
        string language,
        Func<string, string> localize)
    {
        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(language);
        string normalizedSheetLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(string.IsNullOrWhiteSpace(preferences.SheetLanguage) ? normalizedLanguage : preferences.SheetLanguage);
        bool preferNightlyBuilds = UsesPreviewUpdateChannel(preferences.UpdateChannel);
        return
        [
            new DesktopDialogField(
                "globalTheme",
                localize("desktop.dialog.global_settings.field.theme"),
                preferences.Theme,
                DesktopPreferenceState.Default.Theme,
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                Options: BuildThemeOptions()),
            new DesktopDialogField(
                "globalUiScale",
                localize("desktop.dialog.global_settings.field.ui_scale"),
                preferences.UiScalePercent.ToString(CultureInfo.InvariantCulture),
                DesktopPreferenceState.Default.UiScalePercent.ToString(CultureInfo.InvariantCulture),
                InputType: "number",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField(
                "globalLanguage",
                "Language",
                normalizedLanguage,
                DesktopLocalizationCatalog.DefaultLanguage,
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                Options: BuildLanguageOptions()),
            new DesktopDialogField(
                "globalSheetLanguage",
                "Sheet Language",
                normalizedSheetLanguage,
                normalizedLanguage,
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right,
                Options: BuildLanguageOptions()),
            new DesktopDialogField(
                "globalCompactMode",
                localize("desktop.dialog.global_settings.field.compact_mode"),
                preferences.CompactMode ? "true" : "false",
                "false",
                InputType: "checkbox",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField(
                "globalCharacterPriority",
                "Default Setting for New Runners",
                preferences.CharacterPriority,
                DesktopPreferenceState.Default.CharacterPriority,
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                Options: BuildPriorityOptions()),
            new DesktopDialogField(
                "globalKarmaNuyenRatio",
                "Karma / Nuyen Ratio",
                preferences.KarmaNuyenRatio.ToString(CultureInfo.InvariantCulture),
                DesktopPreferenceState.Default.KarmaNuyenRatio.ToString(CultureInfo.InvariantCulture),
                InputType: "number",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField(
                "globalHouseRulesEnabled",
                "Enable House Rules",
                preferences.HouseRulesEnabled ? "true" : "false",
                "false",
                InputType: "checkbox",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField(
                "globalUpdatePolicy",
                "Update Channel",
                preferences.UpdateChannel,
                DesktopPreferenceState.Default.UpdateChannel,
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden,
                Options: BuildUpdateChannelOptions()),
            new DesktopDialogField(
                "globalUpdateMode",
                "Startup updates",
                DesktopPreferenceStateRuntime.NormalizeUpdateMode(preferences.UpdateMode, preferences.CheckForUpdatesOnLaunch),
                DesktopPreferenceState.Default.UpdateMode,
                InputType: "select",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                Options: BuildUpdateModeOptions()),
            new DesktopDialogField(
                "globalCheckForUpdates",
                "Legacy update check",
                preferences.CheckForUpdatesOnLaunch ? "true" : "false",
                "true",
                InputType: "checkbox",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField(
                "globalPreferNightlyBuilds",
                "Prefer Nightly builds when updating",
                preferNightlyBuilds ? "true" : "false",
                "true",
                InputType: "checkbox",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField(
                "globalCharacterRosterPath",
                "Character Roster Watch Folder",
                preferences.CharacterRosterPath,
                DesktopPreferenceState.Default.CharacterRosterPath),
            new DesktopDialogField(
                "globalRosterHierarchyJson",
                "Roster Layout Metadata",
                preferences.RosterHierarchyJson,
                "{}",
                IsMultiline: true,
                IsReadOnly: true,
                VisualKind: DesktopDialogFieldVisualKinds.Snippet,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField(
                "globalHideMasterIndex",
                "Hide the Master Index",
                preferences.HideMasterIndex ? "true" : "false",
                "false",
                InputType: "checkbox",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField(
                "globalAnalyticsOptOut",
                "Disable anonymous analytics",
                preferences.AnalyticsOptIn ? "false" : "true",
                "false",
                InputType: "checkbox",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField(
                "globalDisableAiFeatures",
                "Hide helper buttons",
                preferences.DisableAiFeatures ? "true" : "false",
                "false",
                InputType: "checkbox",
                LayoutSlot: DesktopDialogFieldLayoutSlots.Right)
        ];
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildThemeOptions()
        => new[]
        {
            new DesktopDialogFieldOption("classic", "Classic"),
            new DesktopDialogFieldOption("steel", "Steel"),
            new DesktopDialogFieldOption("dark-steel", "Dark Steel"),
            new DesktopDialogFieldOption("mint", "Mint")
        };

    private static bool ParseGlobalAnalyticsOptIn(DesktopDialogState dialog, DesktopPreferenceState fallback)
    {
        string? optOut = DesktopDialogFieldValueParser.GetValue(dialog, "globalAnalyticsOptOut");
        if (optOut is not null)
        {
            return !DesktopDialogFieldValueParser.ParseBool(dialog, "globalAnalyticsOptOut", !fallback.AnalyticsOptIn);
        }

        return DesktopDialogFieldValueParser.ParseBool(dialog, "globalAnalyticsOptIn", fallback.AnalyticsOptIn);
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildLanguageOptions()
        => DesktopLocalizationCatalog.ShippingLanguages
            .Select(language => new DesktopDialogFieldOption(language.Code, language.Code))
            .ToArray();

    private static IReadOnlyList<DesktopDialogFieldOption> BuildPriorityOptions()
        => new[]
        {
            new DesktopDialogFieldOption("Priority", "Priority"),
            new DesktopDialogFieldOption("SumToTen", "Sum To Ten"),
            new DesktopDialogFieldOption("Karma", "Karma")
        };

    private static IReadOnlyList<DesktopDialogFieldOption> BuildSelectionCategoryOptions(params string[] categories)
        => categories
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.Ordinal)
            .Select(category => new DesktopDialogFieldOption(category, category))
            .ToArray();

    private static IReadOnlyList<DesktopDialogFieldOption> BuildSelectionDataFileOptions(params string[] dataFiles)
        => dataFiles
            .Where(dataFile => !string.IsNullOrWhiteSpace(dataFile))
            .Distinct(StringComparer.Ordinal)
            .Select(dataFile => new DesktopDialogFieldOption(dataFile, dataFile))
            .ToArray();

    private static IReadOnlyList<DesktopDialogFieldOption> BuildStartupOptions()
        => new[]
        {
            new DesktopDialogFieldOption("Restore last roster on startup", "Restore last roster on startup"),
            new DesktopDialogFieldOption("Open empty shell on startup", "Open empty shell on startup")
        };

    private static IReadOnlyList<DesktopDialogFieldOption> BuildUpdateChannelOptions()
        => new[]
        {
            new DesktopDialogFieldOption("Preview channel · check weekly", "Preview channel · check weekly"),
            new DesktopDialogFieldOption("Preview channel · check daily", "Preview channel · check daily"),
            new DesktopDialogFieldOption("Stable channel · check weekly", "Stable channel · check weekly")
        };

    private static IReadOnlyList<DesktopDialogFieldOption> BuildUpdateModeOptions()
        => new[]
        {
            new DesktopDialogFieldOption("full", "Install updates and restart"),
            new DesktopDialogFieldOption("notify", "Tell me, do not install"),
            new DesktopDialogFieldOption("off", "Do not check")
        };

    private static bool UsesPreviewUpdateChannel(string? updateChannel)
        => !string.IsNullOrWhiteSpace(updateChannel)
            && updateChannel.Contains("preview", StringComparison.OrdinalIgnoreCase);

    private static DesktopDialogField BuildSelectionSectionsField(string id)
    {
        string sections = "Browse" + Environment.NewLine + "Filters" + Environment.NewLine + "Details" + Environment.NewLine + "Notes";

        return new DesktopDialogField(
            id,
            "Sections",
            sections,
            "Browse",
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Tabs);
    }

    private static DesktopDialogField BuildFilterToggleField(string id, string label, bool value)
    {
        string normalized = value ? "true" : "false";
        return new DesktopDialogField(id, label, normalized, normalized, InputType: "checkbox");
    }

    private static decimal ParseDecimalField(DesktopDialogState dialog, string fieldId, decimal fallback)
    {
        string? raw = DesktopDialogFieldValueParser.GetValue(dialog, fieldId);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : fallback;
    }

    private static string FormatNuyen(decimal value)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"¥{decimal.Round(value, 0, MidpointRounding.AwayFromZero):N0}");

    private static decimal ResolveGradeCostMultiplier(string grade)
        => grade.Trim().ToLowerInvariant() switch
        {
            "alpha" => 1.2m,
            "beta" => 1.5m,
            "delta" => 2.5m,
            _ => 1.0m
        };

    private static decimal ResolveGradeEssenceMultiplier(string grade)
        => grade.Trim().ToLowerInvariant() switch
        {
            "alpha" => 0.8m,
            "beta" => 0.7m,
            "delta" => 0.5m,
            _ => 1.0m
        };

    private static DesktopDialogState ReplaceDialogField(
        DesktopDialogState dialog,
        string fieldId,
        string value,
        string? placeholder = null)
    {
        DesktopDialogField[] fields = dialog.Fields
            .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                ? field with
                {
                    Value = value,
                    Placeholder = placeholder ?? value
                }
                : field)
            .ToArray();
        return dialog with { Fields = fields };
    }

    private static DesktopDialogState ReplaceDialogFields(
        DesktopDialogState dialog,
        params (string FieldId, string Value, string? Placeholder)[] replacements)
    {
        foreach ((string fieldId, string value, string? placeholder) in replacements)
        {
            dialog = ReplaceDialogField(dialog, fieldId, value, placeholder);
        }

        return dialog;
    }

    private static DesktopDialogState ReplaceDialogActions(
        DesktopDialogState dialog,
        params (string ActionId, string Label, bool? IsPrimary)[] replacements)
    {
        DesktopDialogAction[] actions = dialog.Actions
            .Select(action =>
            {
                (string ActionId, string Label, bool? IsPrimary) replacement = replacements
                    .FirstOrDefault(candidate => string.Equals(candidate.ActionId, action.Id, StringComparison.Ordinal));

                if (string.IsNullOrWhiteSpace(replacement.ActionId))
                    return action;

                return action with
                {
                    Label = replacement.Label,
                    IsPrimary = replacement.IsPrimary ?? action.IsPrimary
                };
            })
            .ToArray();

        return dialog with { Actions = actions };
    }

    private static string BuildSelectionBranchTree(
        string root,
        IEnumerable<string> branches,
        string? selectedBranch)
    {
        string[] ordered = branches
            .Where(branch => !string.IsNullOrWhiteSpace(branch))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(branch => branch, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ordered.Length == 0)
            return $"[{root}]";

        List<string> lines = [$"[{root}]"];
        for (int index = 0; index < ordered.Length; index++)
        {
            string branch = ordered[index];
            bool selected = !string.IsNullOrWhiteSpace(selectedBranch)
                && string.Equals(branch, selectedBranch, StringComparison.OrdinalIgnoreCase);
            string prefix = index == ordered.Length - 1 ? "└─" : "├─";
            lines.Add($"{prefix} {(selected ? ">" : " ")} {branch}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildSelectionGroupedBranchTree(
        string root,
        IEnumerable<(string Group, string Branch)> entries,
        string? selectedBranch)
    {
        var grouped = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Branch))
            .Select(entry => (
                Group: string.IsNullOrWhiteSpace(entry.Group) ? root : entry.Group.Trim(),
                Branch: entry.Branch.Trim()))
            .Distinct()
            .GroupBy(entry => entry.Group, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (grouped.Length == 0)
            return $"[{root}]";

        List<string> lines = [$"[{root}]"];
        for (int groupIndex = 0; groupIndex < grouped.Length; groupIndex++)
        {
            var group = grouped[groupIndex];
            bool lastGroup = groupIndex == grouped.Length - 1;
            lines.Add($"{(lastGroup ? "└─" : "├─")} {group.Key}");

            string[] branches = group
                .Select(entry => entry.Branch)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(branch => branch, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int branchIndex = 0; branchIndex < branches.Length; branchIndex++)
            {
                string branch = branches[branchIndex];
                bool selected = !string.IsNullOrWhiteSpace(selectedBranch)
                    && string.Equals(branch, selectedBranch, StringComparison.OrdinalIgnoreCase);
                bool lastBranch = branchIndex == branches.Length - 1;
                string prefix = lastGroup ? "   " : "│  ";
                prefix += lastBranch ? "└─" : "├─";
                lines.Add($"{prefix} {(selected ? ">" : " ")} {branch}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildSelectionCategoryPath(
        string root,
        string? group,
        string branch,
        string? item = null)
    {
        List<string> segments = [root];
        if (!string.IsNullOrWhiteSpace(group)
            && !string.Equals(group, root, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(group, branch, StringComparison.OrdinalIgnoreCase))
        {
            segments.Add(group);
        }

        segments.Add(branch);
        if (!string.IsNullOrWhiteSpace(item))
        {
            segments.Add(item);
        }

        return string.Join(" > ", segments);
    }

    private static bool MatchesSelectionCategory(
        string? category,
        string branch,
        string? group = null)
    {
        if (string.IsNullOrWhiteSpace(category)
            || string.Equals(category, "All", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "Show All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(branch, category, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(group)
                && string.Equals(group, category, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveSelectionTreeBranch(
        string? category,
        string selectedBranch,
        string? selectedGroup = null)
        => MatchesSelectionCategory(category, selectedBranch, selectedGroup)
            ? selectedBranch
            : category ?? selectedBranch;

    private static string ResolveCyberwareGroup(string branch)
        => string.Equals(branch, "Cyberlimbs", StringComparison.OrdinalIgnoreCase)
            ? "Augmentation Frames"
            : "Core Systems";

    private static string ResolveArmorGroup(string branch)
        => branch.Trim().ToLowerInvariant() switch
        {
            "armor" or "clothing" => "Protective Wear",
            "shields" or "ppp" => "Protective Accessories",
            _ => "Armor Catalog"
        };

    private static string ResolveVehicleGroup(string branch)
        => string.Equals(branch, "Drones", StringComparison.OrdinalIgnoreCase)
            ? "Drone Platforms"
            : "Ground Vehicles";

    private static string BuildSelectionList(IEnumerable<string> lines)
        => string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));

    private static DesktopDialogState RebuildCyberwareSelectionDialog(DesktopDialogState dialog)
    {
        string grade = DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareGrade") ?? "Standard";
        decimal markupPercent = ParseDecimalField(dialog, "uiCyberwareMarkup", 0m);
        bool blackMarket = DesktopDialogFieldValueParser.ParseBool(dialog, "uiCyberwareBlackMarketDiscount", false);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiCyberwareHideOverAvailLimit", true);
        bool hideBannedGrades = DesktopDialogFieldValueParser.ParseBool(dialog, "uiCyberwareHideBannedGrades", true);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareBookFilter") ?? "All Books";
        string category = DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareCategory") ?? "Show All";
        bool searchInCategoryOnly = DesktopDialogFieldValueParser.ParseBool(dialog, "uiCyberwareSearchInCategoryOnly", true);
        string search = (DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareSearch") ?? string.Empty).Trim();
        string requestedName = DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareName") ?? "Wired Reflexes 2";
        int availLimit = 12;

        var options = new[]
        {
            new { Name = "Wired Reflexes 2", Branch = "Bodyware", Book = "Core Rulebook", Source = "Core Rulebook p. 461", CandidateLine = "Wired Reflexes 2 · Initiative boost · Essence 3.00", Availability = "12R", AvailScore = 12, BaseCost = 149000m, BaseEssence = 3.00m, Capacity = "n/a" },
            new { Name = "Reaction Enhancers 2", Branch = "Bodyware", Book = "Chrome Flesh", Source = "Chrome Flesh p. 90", CandidateLine = "Reaction Enhancers 2 · Initiative support · Essence 0.60", Availability = "8R", AvailScore = 8, BaseCost = 26000m, BaseEssence = 0.60m, Capacity = "n/a" },
            new { Name = "Cybereyes Rating 4", Branch = "Headware", Book = "Core Rulebook", Source = "Core Rulebook p. 455", CandidateLine = "Cybereyes Rating 4 · Sensor suite · Essence 0.40", Availability = "12", AvailScore = 12, BaseCost = 16000m, BaseEssence = 0.40m, Capacity = "16" },
            new { Name = "Cyberarm Basic", Branch = "Cyberlimbs", Book = "Chrome Flesh", Source = "Chrome Flesh p. 94", CandidateLine = "Cyberarm Basic · Capacity shell · Essence 1.00", Availability = "9", AvailScore = 9, BaseCost = 15000m, BaseEssence = 1.00m, Capacity = "15" }
        };

        var filtered = options
            .Where(option => MatchesSelectionCategory(category, option.Branch, ResolveCyberwareGroup(option.Branch)))
            .Where(option => string.IsNullOrWhiteSpace(dataFile)
                || string.Equals(dataFile, "All Books", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Book, dataFile, StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(search)
                || option.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (!searchInCategoryOnly && option.Branch.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Where(option => !hideOverAvail || option.AvailScore <= availLimit)
            .Where(option => !hideBannedGrades || option.AvailScore <= availLimit)
            .ToArray();

        if (filtered.Length == 0)
            filtered = options.Where(option => MatchesSelectionCategory(category, option.Branch, ResolveCyberwareGroup(option.Branch))).ToArray();
        if (filtered.Length == 0)
            filtered = options;

        var selected = filtered.FirstOrDefault(option => string.Equals(option.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            ?? filtered[0];
        decimal cost = selected.BaseCost * ResolveGradeCostMultiplier(grade) * (1m + (markupPercent / 100m));
        if (blackMarket)
            cost *= 0.9m;
        decimal essence = selected.BaseEssence * ResolveGradeEssenceMultiplier(grade);

        string selectedGroup = ResolveCyberwareGroup(selected.Branch);
        string effectiveCategory = MatchesSelectionCategory(category, selected.Branch, selectedGroup)
            ? category
            : selected.Branch;
        string categoryTree = BuildSelectionGroupedBranchTree("Cyberware", options.Select(option => (ResolveCyberwareGroup(option.Branch), option.Branch)), ResolveSelectionTreeBranch(category, selected.Branch, selectedGroup));
        string candidateList = BuildSelectionList(filtered.Select(option => $"{(string.Equals(option.Name, selected.Name, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {option.CandidateLine}"));
        string details = BuildGridValue(
            ("Selected", selected.Name),
            ("Grade", grade),
            ("Availability", selected.Availability),
            ("Cost", FormatNuyen(cost)),
            ("Essence", essence.ToString("0.00", CultureInfo.InvariantCulture)),
            ("Capacity", selected.Capacity),
            ("Book", selected.Book));
        string searchScope = BuildSelectionSearchScope(searchInCategoryOnly);
        string selectionTrail = BuildGridValue(
            ("Category Path", BuildSelectionCategoryPath("Cyberware", selectedGroup, selected.Branch, selected.Name)),
            ("Search Scope", searchScope),
            ("Selected Entry", selected.Name),
            ("Follow-through", "Add & More keeps the selector open"));
        string filterSummary = $"Filtered Catalog | {filtered.Length} shown / {options.Length} total{Environment.NewLine}Category Path | {BuildSelectionCategoryPath("Cyberware", selectedGroup, selected.Branch)}{Environment.NewLine}Filters | grade, availability, and source stay live";
        string liveRecalc = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Recalculated Essence", essence.ToString("0.00", CultureInfo.InvariantCulture)),
            ("Black Market", blackMarket ? "Yes" : "No"),
            ("Add Again", "Stays open"));
        string categoryCommands = BuildSelectionList(
        [
            $"Group | {selectedGroup}",
            $"Category | {selected.Branch}",
            $"Search Scope | {searchScope}",
            $"Data File | {selected.Book}",
            "Move the tree without losing grade or availability",
            "Suites and accessories after picking the base implant"
        ]);
        string resultCommands = BuildSelectionList(
        [
            $"Source, cost, and essence for {selected.Name}",
            $"Use OK once or Add & More for repeated {selected.Branch.ToLowerInvariant()} picks",
            $"Keep grade {grade} and rating visible while browsing"
        ]);
        string browseGrid = BuildSelectionBrowseGrid(
            filtered.Take(3).Select(option => (
                option.Name,
                option.Branch,
                option.Availability,
                FormatNuyen(option.BaseCost * ResolveGradeCostMultiplier(grade) * (blackMarket ? 0.9m : 1m))
            )).ToArray());

        return ReplaceDialogActions(
            ReplaceDialogFields(
                dialog,
                ("uiCyberwareCategory", effectiveCategory, effectiveCategory),
                ("uiCyberwareCategoryTree", categoryTree, categoryTree),
                ("uiCyberwareCandidateList", candidateList, candidateList),
                ("uiCyberwareBrowseGrid", browseGrid, browseGrid),
                ("uiCyberwareName", selected.Name, selected.Name),
                ("uiCyberwareSelectedBranch", selected.Branch, selected.Branch),
                ("uiCyberwareSource", selected.Source, selected.Source),
                ("uiCyberwareSelectionDetails", details, details),
                ("uiCyberwareSelectionTrail", selectionTrail, selectionTrail),
                ("uiCyberwareCategoryCommands", categoryCommands, categoryCommands),
                ("uiCyberwareFilterSummary", filterSummary, filterSummary),
                ("uiCyberwareLiveRecalc", liveRecalc, liveRecalc),
                ("uiCyberwareResultCommands", resultCommands, resultCommands)),
            ("focus_category", BuildSelectionCategoryActionLabel(effectiveCategory, selected.Branch), false),
            ("toggle_search_scope", BuildSelectionSearchActionLabel(searchInCategoryOnly), false),
            ("add", $"Add {selected.Name}", true),
            ("add_more", $"Add & More {selected.Name}", false));
    }

    private static DesktopDialogState RebuildGearSelectionDialog(DesktopDialogState dialog)
    {
        decimal markupPercent = ParseDecimalField(dialog, "uiGearMarkup", 0m);
        bool blackMarket = DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearBlackMarketDiscount", false);
        bool freeItem = DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearFreeItem", false);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearHideOverAvailLimit", true);
        bool showOnlyAfford = DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearShowOnlyAffordItems", false);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiGearBookFilter") ?? "All Books";
        string category = DesktopDialogFieldValueParser.GetValue(dialog, "uiGearCategory") ?? "Show All";
        bool searchInCategoryOnly = DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearSearchInCategoryOnly", true);
        string search = (DesktopDialogFieldValueParser.GetValue(dialog, "uiGearSearch") ?? string.Empty).Trim();
        string requestedName = DesktopDialogFieldValueParser.GetValue(dialog, "uiGearName") ?? "Ares Predator V";

        var options = new[]
        {
            new { Name = "Ares Predator V", Group = "Firearms", Branch = "Pistols", Book = "Core Rulebook", Source = "Core Rulebook p. 424", CandidateLine = "Ares Predator V · Pistol · ¥725", Availability = "5R", AvailScore = 5, BaseCost = 725m, Affordable = true },
            new { Name = "Armor Jacket", Group = "Armor", Branch = "Armor", Book = "Core Rulebook", Source = "Core Rulebook p. 437", CandidateLine = "Armor Jacket · Armor · ¥1,000", Availability = "12", AvailScore = 12, BaseCost = 1000m, Affordable = true },
            new { Name = "Medkit Rating 6", Group = "General", Branch = "Medical", Book = "Core Rulebook", Source = "Core Rulebook p. 450", CandidateLine = "Medkit Rating 6 · Gear · ¥1,500", Availability = "8", AvailScore = 8, BaseCost = 1500m, Affordable = true },
            new { Name = "Micro Trid Projector", Group = "Electronics", Branch = "Visual", Book = "Data Trails", Source = "Data Trails p. 58", CandidateLine = "Micro Trid Projector · Electronics · ¥2,100", Availability = "10", AvailScore = 10, BaseCost = 2100m, Affordable = false }
        };

        var filtered = options
            .Where(option => MatchesSelectionCategory(category, option.Branch, option.Group))
            .Where(option => string.IsNullOrWhiteSpace(dataFile)
                || string.Equals(dataFile, "All Books", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Book, dataFile, StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(search)
                || option.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (!searchInCategoryOnly && option.Branch.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Where(option => !hideOverAvail || option.AvailScore <= 12)
            .Where(option => !showOnlyAfford || option.Affordable)
            .ToArray();

        if (filtered.Length == 0)
            filtered = options.Where(option => MatchesSelectionCategory(category, option.Branch, option.Group)).ToArray();
        if (filtered.Length == 0)
            filtered = options;

        var selected = filtered.FirstOrDefault(option => string.Equals(option.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            ?? filtered[0];
        decimal cost = freeItem ? 0m : selected.BaseCost * (1m + (markupPercent / 100m)) * (blackMarket ? 0.9m : 1m);
        string effectiveCategory = MatchesSelectionCategory(category, selected.Branch, selected.Group)
            ? category
            : selected.Branch;
        string categoryTree = BuildSelectionGroupedBranchTree("Gear", options.Select(option => (option.Group, option.Branch)), ResolveSelectionTreeBranch(category, selected.Branch, selected.Group));
        string candidateList = BuildSelectionList(filtered.Select(option => $"{(string.Equals(option.Name, selected.Name, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {option.CandidateLine}"));
        string details = BuildGridValue(
            ("Selected", selected.Name),
            ("Category", selected.Group),
            ("Availability", selected.Availability),
            ("Cost", FormatNuyen(cost)),
            ("Book", selected.Book));
        string searchScope = BuildSelectionSearchScope(searchInCategoryOnly);
        string selectionTrail = BuildGridValue(
            ("Category Path", BuildSelectionCategoryPath("Gear", selected.Group, selected.Branch, selected.Name)),
            ("Search Scope", searchScope),
            ("Selected Entry", selected.Name),
            ("Follow-through", "Stack and discount stay live"));
        string filterSummary = $"Filtered Catalog | {filtered.Length} shown / {options.Length} total{Environment.NewLine}Category Path | {BuildSelectionCategoryPath("Gear", selected.Group, selected.Branch)}{Environment.NewLine}Filters | availability, source, and pricing stay live";
        string liveRecalc = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Free Item", freeItem ? "Yes" : "No"),
            ("Black Market", blackMarket ? "Yes" : "No"),
            ("Add Again", "Stays open"));
        string categoryCommands = BuildSelectionList(
        [
            $"Group | {selected.Group}",
            $"Category | {selected.Branch}",
            $"Search Scope | {searchScope}",
            $"Data File | {selected.Book}",
            $"Stack | {(DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearStack", true) ? "On" : "Off")}",
            $"Do It Yourself | {(DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearDoItYourself", false) ? "On" : "Off")}"
        ]);
        string resultCommands = BuildSelectionList(
        [
            $"Price, rating, and legality for {selected.Name}",
            $"Use OK once or Add & More to keep shopping in {selected.Branch}",
            "Keep quantity, markup, and source visible while confirming"
        ]);
        string browseGrid = BuildSelectionBrowseGrid(
            filtered.Take(3).Select(option => (
                option.Name,
                option.Branch,
                option.Availability,
                FormatNuyen(freeItem ? 0m : option.BaseCost * (blackMarket ? 0.9m : 1m))
            )).ToArray());

        return ReplaceDialogActions(
            ReplaceDialogFields(
                dialog,
                ("uiGearCategory", effectiveCategory, effectiveCategory),
                ("uiGearCategoryTree", categoryTree, categoryTree),
                ("uiGearCandidateList", candidateList, candidateList),
                ("uiGearBrowseGrid", browseGrid, browseGrid),
                ("uiGearName", selected.Name, selected.Name),
                ("uiGearSelectedBranch", selected.Branch, selected.Branch),
                ("uiGearSource", selected.Source, selected.Source),
                ("uiGearSelectionDetails", details, details),
                ("uiGearSelectionTrail", selectionTrail, selectionTrail),
                ("uiGearCategoryCommands", categoryCommands, categoryCommands),
                ("uiGearFilterSummary", filterSummary, filterSummary),
                ("uiGearLiveRecalc", liveRecalc, liveRecalc),
                ("uiGearResultCommands", resultCommands, resultCommands)),
            ("focus_category", BuildSelectionCategoryActionLabel(effectiveCategory, selected.Branch), false),
            ("toggle_search_scope", BuildSelectionSearchActionLabel(searchInCategoryOnly), false),
            ("add", $"Add {selected.Name}", true),
            ("add_more", $"Add & More {selected.Name}", false));
    }

    private static DesktopDialogState RebuildWeaponSelectionDialog(DesktopDialogState dialog)
    {
        decimal markupPercent = ParseDecimalField(dialog, "uiWeaponMarkup", 0m);
        bool blackMarket = DesktopDialogFieldValueParser.ParseBool(dialog, "uiWeaponBlackMarketDiscount", false);
        bool freeItem = DesktopDialogFieldValueParser.ParseBool(dialog, "uiWeaponFreeItem", false);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiWeaponHideOverAvailLimit", true);
        bool showOnlyAfford = DesktopDialogFieldValueParser.ParseBool(dialog, "uiWeaponShowOnlyAffordItems", false);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponBookFilter") ?? "All Books";
        string category = DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponCategory") ?? "Show All";
        bool searchInCategoryOnly = DesktopDialogFieldValueParser.ParseBool(dialog, "uiWeaponSearchInCategoryOnly", true);
        string search = (DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponSearch") ?? string.Empty).Trim();
        string requestedName = DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponName") ?? "Colt M23";

        var options = new[]
        {
            new { Name = "Ares Alpha", Group = "Firearms", Branch = "Assault Rifles", Book = "Core Rulebook", Source = "Core Rulebook p. 424", CandidateLine = "Ares Alpha · Assault Rifle · ¥2,650", Availability = "11F", AvailScore = 11, BaseCost = 2650m, Affordable = false, Damage = "11P", AP = "-2", Mode = "SA/BF/FA", Accuracy = "6", Accessories = "Smartgun System\nGrenade Launcher" },
            new { Name = "Colt M23", Group = "Firearms", Branch = "Heavy Pistols", Book = "Core Rulebook", Source = "Core Rulebook p. 424", CandidateLine = "Colt M23 · Heavy Pistol · ¥750", Availability = "5R", AvailScore = 5, BaseCost = 750m, Affordable = true, Damage = "7P", AP = "-1", Mode = "SA", Accuracy = "5", Accessories = "Smartgun System\nTop Rail Mount" },
            new { Name = "Defiance T-250", Group = "Firearms", Branch = "Shotguns", Book = "Core Rulebook", Source = "Core Rulebook p. 425", CandidateLine = "Defiance T-250 · Shotgun · ¥450", Availability = "4R", AvailScore = 4, BaseCost = 450m, Affordable = true, Damage = "10P", AP = "-1", Mode = "SS/SA", Accuracy = "4", Accessories = "Internal Smartgun\nSling" },
            new { Name = "Combat Knife", Group = "Melee", Branch = "Melee", Book = "Core Rulebook", Source = "Core Rulebook p. 423", CandidateLine = "Combat Knife · Melee · ¥300", Availability = "2", AvailScore = 2, BaseCost = 300m, Affordable = true, Damage = "STR+2P", AP = "-3", Mode = "Melee", Accuracy = "Physical", Accessories = "Sheath" }
        };

        var filtered = options
            .Where(option => MatchesSelectionCategory(category, option.Branch, option.Group))
            .Where(option => string.IsNullOrWhiteSpace(dataFile)
                || string.Equals(dataFile, "All Books", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Book, dataFile, StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(search)
                || option.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (!searchInCategoryOnly && option.Branch.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Where(option => !hideOverAvail || option.AvailScore <= 12)
            .Where(option => !showOnlyAfford || option.Affordable)
            .ToArray();

        if (filtered.Length == 0)
            filtered = options.Where(option => MatchesSelectionCategory(category, option.Branch, option.Group)).ToArray();
        if (filtered.Length == 0)
            filtered = options;

        var selected = filtered.FirstOrDefault(option => string.Equals(option.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            ?? filtered[0];
        decimal cost = freeItem ? 0m : selected.BaseCost * (1m + (markupPercent / 100m)) * (blackMarket ? 0.9m : 1m);
        string effectiveCategory = MatchesSelectionCategory(category, selected.Branch, selected.Group)
            ? category
            : selected.Branch;
        string categoryTree = BuildSelectionGroupedBranchTree("Weapons", options.Select(option => (option.Group, option.Branch)), ResolveSelectionTreeBranch(category, selected.Branch, selected.Group));
        string candidateList = BuildSelectionList(filtered.Select(option => $"{(string.Equals(option.Name, selected.Name, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {option.CandidateLine}"));
        string details = BuildGridValue(
            ("Selected", selected.Name),
            ("Damage", selected.Damage),
            ("AP", selected.AP),
            ("Mode", selected.Mode),
            ("Cost", FormatNuyen(cost)),
            ("Book", selected.Book));
        string searchScope = BuildSelectionSearchScope(searchInCategoryOnly);
        string selectionTrail = BuildGridValue(
            ("Category Path", BuildSelectionCategoryPath("Weapons", selected.Group, selected.Branch, selected.Name)),
            ("Search Scope", searchScope),
            ("Selected Entry", selected.Name),
            ("Follow-through", "Add & More keeps the selector open"));
        string filterSummary = $"Filtered Catalog | {filtered.Length} shown / {options.Length} total{Environment.NewLine}Category Path | {BuildSelectionCategoryPath("Weapons", selected.Group, selected.Branch)}{Environment.NewLine}Filters | availability, discounts, and source stay live";
        string liveRecalc = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Accuracy", selected.Accuracy),
            ("Black Market", blackMarket ? "Yes" : "No"),
            ("Add Again", "Stays open"));
        string categoryCommands = BuildSelectionList(
        [
            $"Group | {selected.Group}",
            $"Category | {selected.Branch}",
            $"Search Scope | {searchScope}",
            $"Data File | {selected.Book}",
            $"Black Market | {(blackMarket ? "On" : "Off")}",
            "Accessories and ammo follow-through after choosing the base weapon"
        ]);
        string resultCommands = BuildSelectionList(
        [
            $"Damage, mode, and accessories for {selected.Name}",
            "Use OK for one add or Add & More to keep the selector open",
            $"Keep ammo, source, and legality visible while confirming {selected.Name}"
        ]);
        string browseGrid = BuildSelectionBrowseGrid(
            filtered.Take(3).Select(option => (
                option.Name,
                option.Branch,
                option.Availability,
                FormatNuyen(freeItem ? 0m : option.BaseCost * (blackMarket ? 0.9m : 1m))
            )).ToArray());

        return ReplaceDialogActions(
            ReplaceDialogFields(
                dialog,
                ("uiWeaponCategory", effectiveCategory, effectiveCategory),
                ("uiWeaponCategoryTree", categoryTree, categoryTree),
                ("uiWeaponCandidateList", candidateList, candidateList),
                ("uiWeaponBrowseGrid", browseGrid, browseGrid),
                ("uiWeaponName", selected.Name, selected.Name),
                ("uiWeaponSelectedBranch", selected.Branch, selected.Branch),
                ("uiWeaponSource", selected.Source, selected.Source),
                ("uiWeaponSelectionDetails", details, details),
                ("uiWeaponIncludedAccessories", selected.Accessories, selected.Accessories),
                ("uiWeaponSelectionTrail", selectionTrail, selectionTrail),
                ("uiWeaponCategoryCommands", categoryCommands, categoryCommands),
                ("uiWeaponFilterSummary", filterSummary, filterSummary),
                ("uiWeaponLiveRecalc", liveRecalc, liveRecalc),
                ("uiWeaponResultCommands", resultCommands, resultCommands)),
            ("focus_category", BuildSelectionCategoryActionLabel(effectiveCategory, selected.Branch), false),
            ("toggle_search_scope", BuildSelectionSearchActionLabel(searchInCategoryOnly), false),
            ("add", $"Add {selected.Name}", true),
            ("add_more", $"Add & More {selected.Name}", false));
    }

    private static DesktopDialogState RebuildArmorSelectionDialog(DesktopDialogState dialog)
    {
        decimal markupPercent = ParseDecimalField(dialog, "uiArmorMarkup", 0m);
        bool blackMarket = DesktopDialogFieldValueParser.ParseBool(dialog, "uiArmorBlackMarketDiscount", false);
        bool freeItem = DesktopDialogFieldValueParser.ParseBool(dialog, "uiArmorFreeItem", false);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiArmorHideOverAvailLimit", true);
        bool showOnlyAfford = DesktopDialogFieldValueParser.ParseBool(dialog, "uiArmorShowOnlyAffordItems", false);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorBookFilter") ?? "All Books";
        string category = DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorCategory") ?? "Show All";
        bool searchInCategoryOnly = DesktopDialogFieldValueParser.ParseBool(dialog, "uiArmorSearchInCategoryOnly", true);
        string search = (DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorSearch") ?? string.Empty).Trim();
        string requestedName = DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorName") ?? "Armor Jacket";

        var options = new[]
        {
            new { Name = "Armor Jacket", Branch = "Armor", Book = "Core Rulebook", Source = "Core Rulebook p. 436", CandidateLine = "Armor Jacket · Armor 12 · ¥1,000", Availability = "12", AvailScore = 12, BaseCost = 1000m, Affordable = true, Armor = "12", Capacity = "n/a" },
            new { Name = "Actioneer Business Clothes", Branch = "Clothing", Book = "Core Rulebook", Source = "Core Rulebook p. 437", CandidateLine = "Actioneer Business Clothes · Armor 8 · ¥1,500", Availability = "10", AvailScore = 10, BaseCost = 1500m, Affordable = false, Armor = "8", Capacity = "n/a" },
            new { Name = "Ballistic Shield", Branch = "Shields", Book = "Run & Gun", Source = "Run & Gun p. 52", CandidateLine = "Ballistic Shield · Armor +6 · ¥900", Availability = "8", AvailScore = 8, BaseCost = 900m, Affordable = true, Armor = "+6", Capacity = "n/a" },
            new { Name = "PPP System", Branch = "PPP", Book = "Core Rulebook", Source = "Core Rulebook p. 438", CandidateLine = "PPP System · Armor +1 · ¥250", Availability = "6", AvailScore = 6, BaseCost = 250m, Affordable = true, Armor = "+1", Capacity = "n/a" }
        };

        var filtered = options
            .Where(option => MatchesSelectionCategory(category, option.Branch, ResolveArmorGroup(option.Branch)))
            .Where(option => string.IsNullOrWhiteSpace(dataFile)
                || string.Equals(dataFile, "All Books", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Book, dataFile, StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(search)
                || option.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (!searchInCategoryOnly && option.Branch.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Where(option => !hideOverAvail || option.AvailScore <= 12)
            .Where(option => !showOnlyAfford || option.Affordable)
            .ToArray();

        if (filtered.Length == 0)
            filtered = options.Where(option => MatchesSelectionCategory(category, option.Branch, ResolveArmorGroup(option.Branch))).ToArray();
        if (filtered.Length == 0)
            filtered = options;

        var selected = filtered.FirstOrDefault(option => string.Equals(option.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            ?? filtered[0];
        decimal cost = freeItem ? 0m : selected.BaseCost * (1m + (markupPercent / 100m)) * (blackMarket ? 0.9m : 1m);
        string selectedGroup = ResolveArmorGroup(selected.Branch);
        string effectiveCategory = MatchesSelectionCategory(category, selected.Branch, selectedGroup)
            ? category
            : selected.Branch;
        string categoryTree = BuildSelectionGroupedBranchTree("Armor", options.Select(option => (ResolveArmorGroup(option.Branch), option.Branch)), ResolveSelectionTreeBranch(category, selected.Branch, selectedGroup));
        string candidateList = BuildSelectionList(filtered.Select(option => $"{(string.Equals(option.Name, selected.Name, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {option.CandidateLine}"));
        string details = BuildGridValue(
            ("Selected", selected.Name),
            ("Armor", selected.Armor),
            ("Availability", selected.Availability),
            ("Capacity", selected.Capacity),
            ("Cost", FormatNuyen(cost)),
            ("Book", selected.Book));
        string searchScope = BuildSelectionSearchScope(searchInCategoryOnly);
        string selectionTrail = BuildGridValue(
            ("Category Path", BuildSelectionCategoryPath("Armor", selectedGroup, selected.Branch, selected.Name)),
            ("Search Scope", searchScope),
            ("Selected Entry", selected.Name),
            ("Follow-through", "Source and markup stay visible through confirmation"));
        string filterSummary = $"Filtered Catalog | {filtered.Length} shown / {options.Length} total{Environment.NewLine}Category Path | {BuildSelectionCategoryPath("Armor", selectedGroup, selected.Branch)}{Environment.NewLine}Filters | availability, source, and markup stay live";
        string liveRecalc = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Armor", selected.Armor),
            ("Free Item", freeItem ? "Yes" : "No"),
            ("Add Again", "Stays open"));
        string categoryCommands = BuildSelectionList(
        [
            $"Group | {selectedGroup}",
            $"Category | {selected.Branch}",
            $"Search Scope | {searchScope}",
            $"Data File | {selected.Book}",
            $"Black Market | {(blackMarket ? "On" : "Off")}",
            "Keep protection, source, and markup visible while browsing"
        ]);
        string resultCommands = BuildSelectionList(
        [
            $"Armor value and source for {selected.Name}",
            "Use OK for one add or Add & More to keep the selector open",
            $"Keep free-item and cost visible while confirming {selected.Name}"
        ]);
        string browseGrid = BuildSelectionBrowseGrid(
            filtered.Take(3).Select(option => (
                option.Name,
                option.Branch,
                option.Availability,
                FormatNuyen(freeItem ? 0m : option.BaseCost * (blackMarket ? 0.9m : 1m))
            )).ToArray());

        return ReplaceDialogActions(
            ReplaceDialogFields(
                dialog,
                ("uiArmorCategory", effectiveCategory, effectiveCategory),
                ("uiArmorCategoryTree", categoryTree, categoryTree),
                ("uiArmorCandidateList", candidateList, candidateList),
                ("uiArmorBrowseGrid", browseGrid, browseGrid),
                ("uiArmorName", selected.Name, selected.Name),
                ("uiArmorSelectedBranch", selected.Branch, selected.Branch),
                ("uiArmorSource", selected.Source, selected.Source),
                ("uiArmorSelectionDetails", details, details),
                ("uiArmorSelectionTrail", selectionTrail, selectionTrail),
                ("uiArmorCategoryCommands", categoryCommands, categoryCommands),
                ("uiArmorFilterSummary", filterSummary, filterSummary),
                ("uiArmorLiveRecalc", liveRecalc, liveRecalc),
                ("uiArmorResultCommands", resultCommands, resultCommands)),
            ("focus_category", BuildSelectionCategoryActionLabel(effectiveCategory, selected.Branch), false),
            ("toggle_search_scope", BuildSelectionSearchActionLabel(searchInCategoryOnly), false),
            ("add", $"Add {selected.Name}", true),
            ("add_more", $"Add & More {selected.Name}", false));
    }

    private static DesktopDialogState RebuildVehicleSelectionDialog(DesktopDialogState dialog)
    {
        bool showDrones = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleShowDrones", true);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleHideOverAvailLimit", true);
        bool showOnlyAfford = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleShowOnlyAffordItems", false);
        bool freeItem = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleFreeItem", false);
        bool blackMarket = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleBlackMarketDiscount", false);
        bool usedVehicle = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleUsedVehicle", false);
        decimal usedVehicleDiscount = ParseDecimalField(dialog, "uiVehicleUsedVehicleDiscount", 25m);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleBookFilter") ?? "All Books";
        string category = DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleCategory") ?? "Show All";
        bool searchInCategoryOnly = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleSearchInCategoryOnly", true);
        string search = (DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleSearch") ?? string.Empty).Trim();
        string requestedName = DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleName") ?? "Hyundai Shin-Hyung";

        var options = new[]
        {
            new { Name = "Hyundai Shin-Hyung", Branch = "Cars", Book = "Core Rulebook", Source = "Core Rulebook p. 465", CandidateLine = "Hyundai Shin-Hyung · Car · ¥16,000", Availability = "8", AvailScore = 8, BaseCost = 16000m, Affordable = true, IsDrone = false, Handling = "4", Armor = "8", Role = "Vehicle" },
            new { Name = "GMC Roadmaster", Branch = "Trucks", Book = "Core Rulebook", Source = "Core Rulebook p. 466", CandidateLine = "GMC Roadmaster · Truck · ¥74,000", Availability = "12F", AvailScore = 12, BaseCost = 74000m, Affordable = false, IsDrone = false, Handling = "3", Armor = "16", Role = "Vehicle" },
            new { Name = "Yamaha Growler", Branch = "Bikes", Book = "Core Rulebook", Source = "Core Rulebook p. 466", CandidateLine = "Yamaha Growler · Bike · ¥5,000", Availability = "6", AvailScore = 6, BaseCost = 5000m, Affordable = true, IsDrone = false, Handling = "5", Armor = "4", Role = "Vehicle" },
            new { Name = "MCT Fly-Spy", Branch = "Drones", Book = "Core Rulebook", Source = "Core Rulebook p. 469", CandidateLine = "MCT Fly-Spy · Drone · ¥2,000", Availability = "4", AvailScore = 4, BaseCost = 2000m, Affordable = true, IsDrone = true, Handling = "4", Armor = "0", Role = "Drone" },
            new { Name = "Steel Lynx", Branch = "Drones", Book = "Rigger 5.0", Source = "Rigger 5.0 p. 146", CandidateLine = "Steel Lynx · Drone · ¥25,000", Availability = "12F", AvailScore = 12, BaseCost = 25000m, Affordable = false, IsDrone = true, Handling = "3", Armor = "12", Role = "Drone" }
        };

        var filtered = options
            .Where(option => showDrones || !option.IsDrone)
            .Where(option => MatchesSelectionCategory(category, option.Branch, ResolveVehicleGroup(option.Branch)))
            .Where(option => string.IsNullOrWhiteSpace(dataFile)
                || string.Equals(dataFile, "All Books", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Book, dataFile, StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(search)
                || option.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (!searchInCategoryOnly && option.Branch.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .Where(option => !hideOverAvail || option.AvailScore <= 12)
            .Where(option => !showOnlyAfford || option.Affordable)
            .ToArray();

        if (filtered.Length == 0)
            filtered = options.Where(option => (showDrones || !option.IsDrone) && MatchesSelectionCategory(category, option.Branch, ResolveVehicleGroup(option.Branch))).ToArray();
        if (filtered.Length == 0)
            filtered = options.Where(option => showDrones || !option.IsDrone).ToArray();
        if (filtered.Length == 0)
            filtered = options;

        var selected = filtered.FirstOrDefault(option => string.Equals(option.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            ?? filtered[0];
        decimal cost = selected.BaseCost;
        if (usedVehicle)
            cost *= 1m - (usedVehicleDiscount / 100m);
        if (blackMarket)
            cost *= 0.9m;
        if (freeItem)
            cost = 0m;

        string selectedGroup = ResolveVehicleGroup(selected.Branch);
        string effectiveCategory = MatchesSelectionCategory(category, selected.Branch, selectedGroup)
            ? category
            : selected.Branch;
        string categoryTree = BuildSelectionGroupedBranchTree("Vehicles", options.Where(option => showDrones || !option.IsDrone).Select(option => (ResolveVehicleGroup(option.Branch), option.Branch)), ResolveSelectionTreeBranch(category, selected.Branch, selectedGroup));
        string candidateList = BuildSelectionList(filtered.Select(option => $"{(string.Equals(option.Name, selected.Name, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {option.CandidateLine}"));
        string details = BuildGridValue(
            ("Selected", selected.Name),
            ("Role", showDrones ? "Vehicle / Drone Catalog" : selected.Role),
            ("Handling", selected.Handling),
            ("Armor", selected.Armor),
            ("Source", selected.Source),
            ("Book", selected.Book));
        string searchScope = BuildSelectionSearchScope(searchInCategoryOnly);
        string selectionTrail = BuildGridValue(
            ("Category Path", BuildSelectionCategoryPath("Vehicles", selectedGroup, selected.Branch, selected.Name)),
            ("Search Scope", searchScope),
            ("Selected Entry", selected.Name),
            ("Follow-through", "Used-vehicle and drone filters stay live"));
        string filterSummary = $"Filtered Catalog | {filtered.Length} shown / {options.Length} total{Environment.NewLine}Category Path | {BuildSelectionCategoryPath("Vehicles", selectedGroup, selected.Branch)}{Environment.NewLine}Filters | vehicle/drone and availability stay live";
        string liveRecalc = BuildGridValue(
            ("Selected Cost", FormatNuyen(cost)),
            ("Show Drones", showDrones ? "Yes" : "No"),
            ("Availability Filter", hideOverAvail ? "On" : "Off"),
            ("Add Again", "Stays open"));
        string categoryCommands = BuildSelectionList(
        [
            $"Group | {selectedGroup}",
            $"Category | {selected.Branch}",
            $"Search Scope | {searchScope}",
            $"Data File | {selected.Book}",
            $"Show Drones | {(showDrones ? "On" : "Off")}",
            $"Used Vehicle | {(usedVehicle ? "On" : "Off")}"
        ]);
        string resultCommands = BuildSelectionList(
        [
            $"Handling, armor, and source for {selected.Name}",
            "Keep cost and used-vehicle settings visible through confirmation",
            $"Use OK once or Add & More to keep browsing {selected.Role.ToLowerInvariant()} entries"
        ]);
        string browseGrid = BuildSelectionBrowseGrid(
            filtered.Take(3).Select(option => (
                option.Name,
                option.Branch,
                option.Availability,
                FormatNuyen(freeItem ? 0m : option.BaseCost * (usedVehicle ? 1m - (usedVehicleDiscount / 100m) : 1m) * (blackMarket ? 0.9m : 1m))
            )).ToArray());

        return ReplaceDialogActions(
            ReplaceDialogFields(
                dialog,
                ("uiVehicleCategory", effectiveCategory, effectiveCategory),
                ("uiVehicleCategoryTree", categoryTree, categoryTree),
                ("uiVehicleCandidateList", candidateList, candidateList),
                ("uiVehicleBrowseGrid", browseGrid, browseGrid),
                ("uiVehicleName", selected.Name, selected.Name),
                ("uiVehicleSelectedBranch", selected.Branch, selected.Branch),
                ("uiVehicleRole", selected.Role, selected.Role),
                ("uiVehicleSource", selected.Source, selected.Source),
                ("uiVehicleSelectionDetails", details, details),
                ("uiVehicleSelectionTrail", selectionTrail, selectionTrail),
                ("uiVehicleCategoryCommands", categoryCommands, categoryCommands),
                ("uiVehicleFilterSummary", filterSummary, filterSummary),
                ("uiVehicleLiveRecalc", liveRecalc, liveRecalc),
                ("uiVehicleResultCommands", resultCommands, resultCommands),
                ("uiVehicleCost", decimal.Round(cost, 0).ToString(CultureInfo.InvariantCulture), decimal.Round(cost, 0).ToString(CultureInfo.InvariantCulture))),
            ("focus_category", BuildSelectionCategoryActionLabel(effectiveCategory, selected.Branch), false),
            ("toggle_search_scope", BuildSelectionSearchActionLabel(searchInCategoryOnly), false),
            ("add", $"Add {selected.Name}", true),
            ("add_more", $"Add & More {selected.Name}", false));
    }

    private static DesktopDialogState RebuildQualitySelectionDialog(DesktopDialogState dialog)
    {
        bool showNegative = DesktopDialogFieldValueParser.ParseBool(dialog, "uiQualityShowNegative", true);
        bool metagenicOnly = DesktopDialogFieldValueParser.ParseBool(dialog, "uiQualityMetagenicOnly", false);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityBookFilter") ?? "Core Rulebook";
        string requestedType = DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityType") ?? "Positive";
        string search = (DesktopDialogFieldValueParser.GetValue(dialog, "uiQualitySearch") ?? string.Empty).Trim();
        string requestedName = DesktopDialogFieldValueParser.GetValue(dialog, "uiQualityName") ?? "First Impression";

        var options = new[]
        {
            new { Name = "First Impression", Type = "Positive", Karma = 11, Book = "Core Rulebook", Source = "Core Rulebook p. 73", Branch = "Positive", Tag = "Social", Metagenic = false },
            new { Name = "Toughness", Type = "Positive", Karma = 9, Book = "Core Rulebook", Source = "Core Rulebook p. 79", Branch = "Positive", Tag = "Physical", Metagenic = false },
            new { Name = "Allergy (Common, Mild)", Type = "Negative", Karma = -10, Book = "Core Rulebook", Source = "Core Rulebook p. 78", Branch = "Negative", Tag = "Health", Metagenic = false },
            new { Name = "Blandness", Type = "Negative", Karma = -8, Book = "Core Rulebook", Source = "Core Rulebook p. 80", Branch = "Negative", Tag = "Social", Metagenic = false },
            new { Name = "Glamour", Type = "Metatype", Karma = 8, Book = "Runner's Companion", Source = "Runner's Companion p. 45", Branch = "Metatype", Tag = "Metagenic", Metagenic = true }
        };

        var filtered = options
            .Where(option => string.IsNullOrWhiteSpace(requestedType)
                || string.Equals(requestedType, "Show All", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Type, requestedType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Branch, requestedType, StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(dataFile)
                || string.Equals(option.Book, dataFile, StringComparison.OrdinalIgnoreCase))
            .Where(option => !metagenicOnly || option.Metagenic)
            .Where(option => showNegative || !string.Equals(option.Type, "Negative", StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(search)
                || option.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || option.Tag.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (filtered.Length == 0)
        {
            filtered = options
                .Where(option => showNegative || !string.Equals(option.Type, "Negative", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (filtered.Length == 0)
        {
            filtered = options;
        }

        var selected = filtered.FirstOrDefault(option => string.Equals(option.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            ?? filtered[0];

        string effectiveType = requestedType;
        if (!string.IsNullOrWhiteSpace(requestedType)
            && !string.Equals(requestedType, "Show All", StringComparison.OrdinalIgnoreCase)
            && !filtered.Any(option => string.Equals(option.Type, requestedType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Branch, requestedType, StringComparison.OrdinalIgnoreCase)))
        {
            effectiveType = "Show All";
        }

        string categoryTree = BuildSelectionGroupedBranchTree(
            "Qualities",
            options.Select(option => (option.Type, option.Branch)),
            selected.Branch);
        string candidateList = BuildSelectionList(filtered.Select(option =>
            $"{(string.Equals(option.Name, selected.Name, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {option.Name} · {option.Type} · {option.Karma} Karma"));
        string details = BuildGridValue(
            ("Selected", selected.Name),
            ("Type", selected.Type),
            ("Karma", selected.Karma.ToString(CultureInfo.InvariantCulture)),
            ("Source", selected.Source),
            ("Book", selected.Book),
            ("Tag", selected.Tag));
        string trail = BuildGridValue(
            ("Category Path", BuildSelectionCategoryPath("Qualities", selected.Type, selected.Branch, selected.Name)),
            ("Selected Entry", selected.Name),
            ("Filters", metagenicOnly ? "metagenic-only" : "full catalog"),
            ("Follow-through", "Add & More keeps the selector open"));
        string filterSummary =
            $"Filtered Catalog | {filtered.Length} shown / {options.Length} total{Environment.NewLine}" +
            $"Category Path | {BuildSelectionCategoryPath("Qualities", selected.Type, selected.Branch)}{Environment.NewLine}" +
            $"Negative Qualities | {(showNegative ? "included" : "hidden")}";
        string resultCommands = BuildSelectionList(
        [
            $"Karma, tag, and source for {selected.Name}",
            "Use Add once or Add & More to keep browsing",
            "Keep type and metagenic filters visible while confirming"
        ]);

        return ReplaceDialogActions(
            ReplaceDialogFields(
                dialog,
                ("uiQualityType", effectiveType, effectiveType),
                ("uiQualityBookFilter", selected.Book, selected.Book),
                ("uiQualityName", selected.Name, selected.Name),
                ("uiQualityCandidateList", candidateList, candidateList),
                ("uiQualityKarma", selected.Karma.ToString(CultureInfo.InvariantCulture), selected.Karma.ToString(CultureInfo.InvariantCulture)),
                ("uiQualitySelectionDetails", details, details),
                ("uiQualitySelectionTrail", trail, trail),
                ("uiQualityFilterSummary", filterSummary, filterSummary),
                ("uiQualityResultCommands", resultCommands, resultCommands),
                ("uiQualityCategoryTree", categoryTree, categoryTree)),
            ("add", $"Add {selected.Name}", true),
            ("add_more", $"Add & More {selected.Name}", false));
    }

    private static DesktopDialogState RebuildCyberwareEditDialog(DesktopDialogState dialog)
    {
        string grade = DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditGrade") ?? "Standard";
        decimal rating = ParseDecimalField(dialog, "uiCyberwareEditRating", 4m);
        decimal cost = ParseDecimalField(dialog, "uiCyberwareEditCost", 16000m);
        decimal essence = 0.10m * rating * ResolveGradeEssenceMultiplier(grade);

        string details = BuildGridValue(
            ("Selected", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditName") ?? "Cybereyes Rating 4"),
            ("Category", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditCategory") ?? "Headware"),
            ("Grade", grade),
            ("Rating", rating.ToString("0", CultureInfo.InvariantCulture)),
            ("Essence", essence.ToString("0.00", CultureInfo.InvariantCulture)),
            ("Source", DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareEditSource") ?? "Core Rulebook p. 455"));
        string liveSummary = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Recalculated Essence", essence.ToString("0.00", CultureInfo.InvariantCulture)),
            ("Posture", "legacy edit utility"),
            ("Follow-through", "use implant tabs for payloads"));

        return ReplaceDialogField(
            ReplaceDialogField(
                ReplaceDialogField(dialog, "uiCyberwareEditDetails", details),
                "uiCyberwareEditLiveSummary",
                liveSummary),
            "uiCyberwareEditEssence",
            essence.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private static DesktopDialogState RebuildGearEditDialog(DesktopDialogState dialog)
    {
        decimal quantity = ParseDecimalField(dialog, "uiGearEditQuantity", 1m);
        decimal rating = ParseDecimalField(dialog, "uiGearEditRating", 0m);
        decimal cost = ParseDecimalField(dialog, "uiGearEditCost", 1000m);

        string details = BuildGridValue(
            ("Selected", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditName") ?? "Armor Jacket"),
            ("Category", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditCategory") ?? "Armor"),
            ("Quantity", quantity.ToString("0", CultureInfo.InvariantCulture)),
            ("Rating", rating.ToString("0", CultureInfo.InvariantCulture)),
            ("Availability", "12"),
            ("Source", DesktopDialogFieldValueParser.GetValue(dialog, "uiGearEditSource") ?? "Core Rulebook p. 437"));
        string liveSummary = BuildGridValue(
            ("Total Cost", FormatNuyen(cost * Math.Max(quantity, 1m))),
            ("Wireless", "n/a"),
            ("Legality", "Restricted carry not required"),
            ("Posture", "legacy edit utility"));

        return ReplaceDialogField(
            ReplaceDialogField(dialog, "uiGearEditDetails", details),
            "uiGearEditLiveSummary",
            liveSummary);
    }

    private static DesktopDialogState RebuildVehicleEditDialog(DesktopDialogState dialog)
    {
        decimal handling = ParseDecimalField(dialog, "uiVehicleEditHandling", 3m);
        decimal speed = ParseDecimalField(dialog, "uiVehicleEditSpeed", 4m);
        decimal body = ParseDecimalField(dialog, "uiVehicleEditBody", 18m);
        decimal armor = ParseDecimalField(dialog, "uiVehicleEditArmor", 16m);

        string details = BuildGridValue(
            ("Selected", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditName") ?? "GMC Roadmaster"),
            ("Role", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditRole") ?? "Truck"),
            ("Handling", handling.ToString("0", CultureInfo.InvariantCulture)),
            ("Speed", speed.ToString("0", CultureInfo.InvariantCulture)),
            ("Body / Armor", $"{body:0} / {armor:0}"),
            ("Source", DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleEditSource") ?? "Core Rulebook p. 466"));
        string liveSummary = BuildGridValue(
            ("Control Posture", "manual + rigger ready"),
            ("Damage Soak", $"{body + armor:0}"),
            ("Seats", "6"),
            ("Posture", "legacy edit utility"));

        return ReplaceDialogField(
            ReplaceDialogField(dialog, "uiVehicleEditDetails", details),
            "uiVehicleEditLiveSummary",
            liveSummary);
    }

    private static DesktopDialogField BuildSelectionTreeField(string id, string label, string tree)
    {
        string displayLabel = string.Equals(label, "Navigation", StringComparison.Ordinal)
            && id.EndsWith("CategoryTree", StringComparison.Ordinal)
                ? "Categories"
                : label;

        return new DesktopDialogField(
            id,
            displayLabel,
            tree,
            tree,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Tree);
    }

    private static DesktopDialogField BuildSelectionTrailField(string id, string categoryPath, string selectedEntry, string followThrough)
    {
        string trail = BuildGridValue(
            ("Category Path", categoryPath),
            ("Selected Entry", selectedEntry),
            ("Follow-through", followThrough));

        return new DesktopDialogField(
            id,
            "Selection Trail",
            trail,
            trail,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Grid,
            LayoutSlot: DesktopDialogFieldLayoutSlots.Right);
    }

    private static DesktopDialogField BuildSelectionCommandsField(string id, string label, params string[] commands)
    {
        string value = string.Join(Environment.NewLine, commands);
        return new DesktopDialogField(
            id,
            label,
            value,
            value,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.List);
    }

    private static string BuildSelectionSearchScope(bool searchInCategoryOnly)
        => searchInCategoryOnly ? "current category only" : "all categories";

    private static bool IsShowAllSelectionCategory(string? category)
        => string.IsNullOrWhiteSpace(category)
            || string.Equals(category, "All", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "Show All", StringComparison.OrdinalIgnoreCase);

    private static string BuildSelectionCategoryActionLabel(string? category, string selectedBranch)
        => IsShowAllSelectionCategory(category)
            ? $"Focus {selectedBranch}"
            : "Show All Categories";

    private static string BuildSelectionSearchActionLabel(bool searchInCategoryOnly)
        => searchInCategoryOnly ? "Search All Categories" : "Search Current Category";

    private static string BuildSelectionBrowseGrid(params (string Name, string Category, string Availability, string Cost)[] rows)
    {
        if (rows.Length == 0)
        {
            return "Name | Category | Avail | Cost" + Environment.NewLine + "(no results) | - | - | -";
        }

        return string.Join(
            Environment.NewLine,
            new[] { "Name | Category | Avail | Cost" }.Concat(
                rows.Select(row => $"{row.Name} | {row.Category} | {row.Availability} | {row.Cost}")));
    }

    private static string BuildGridValue(params (string Key, string Value)[] rows)
    {
        return string.Join(
            Environment.NewLine,
            rows.Select(row => string.Concat(row.Key, " | ", row.Value)));
    }

    private static string NormalizeGridValue(string value)
    {
        string[] lines = value.Split([Environment.NewLine], StringSplitOptions.None);
        return string.Join(
            Environment.NewLine,
            lines.Select(line => line.Contains(" | ", StringComparison.Ordinal) || !line.Contains(": ", StringComparison.Ordinal)
                ? line
                : line.Replace(": ", " | ", StringComparison.Ordinal)));
    }

    private static DesktopDialogField BuildUtilitySectionsField(string id, string first = "Summary", string second = "Details", string third = "Notes")
    {
        string sections = first + Environment.NewLine + second + Environment.NewLine + third;
        return new DesktopDialogField(
            id,
            "Sections",
            sections,
            first,
            IsReadOnly: true,
            IsMultiline: true,
            VisualKind: DesktopDialogFieldVisualKinds.Tabs);
    }

    private static IReadOnlyList<DesktopDialogAction> BuildAddAndMoreActions(string primaryLabel = "OK")
    {
        return
        [
            new DesktopDialogAction("add", primaryLabel, true),
            new DesktopDialogAction("add_more", "Add & More"),
            new DesktopDialogAction("cancel", "Cancel")
        ];
    }

    private static IReadOnlyList<DesktopDialogAction> BuildLegacySelectionActions(string primaryLabel = "OK")
    {
        return
        [
            new DesktopDialogAction("add", primaryLabel, true),
            new DesktopDialogAction("add_more", "Add & More"),
            new DesktopDialogAction("focus_category", "Show All Categories"),
            new DesktopDialogAction("toggle_search_scope", "Search All Categories"),
            new DesktopDialogAction("cancel", "Cancel")
        ];
    }

    private static IReadOnlyList<DesktopDialogAction> BuildSelectionConfirmationActions(string primaryLabel = "OK")
    {
        return
        [
            new DesktopDialogAction("add", primaryLabel, true),
            new DesktopDialogAction("add_more", "Add & More"),
            new DesktopDialogAction("cancel", "Cancel")
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildCyberwareSelectionFields()
    {
        string categoryTree = BuildSelectionGroupedBranchTree(
            "Cyberware",
            [
                ("Core Systems", "Accessories"),
                ("Core Systems", "Bodyware"),
                ("Augmentation Frames", "Cyberlimbs"),
                ("Core Systems", "Headware")
            ],
            "Bodyware");
        string candidateList =
            "Wired Reflexes 2 · Initiative boost · Essence 3.00" + Environment.NewLine +
            "Cybereyes Rating 4 · Sensor suite · Essence 0.40" + Environment.NewLine +
            "Cyberarm Basic · Capacity shell · Essence 1.00";
        string selectionDetails = BuildGridValue(
            ("Selected", "Wired Reflexes 2"),
            ("Grade", "Standard"),
            ("Availability", "12R"),
            ("Cost", "¥149,000"),
            ("Essence", "3.00"),
            ("Capacity", "n/a"),
            ("Book", "Core Rulebook"));
        string selectionTrailPath = BuildSelectionCategoryPath("Cyberware", "Core Systems", "Bodyware", "Wired Reflexes 2");
        string notes =
            "Grade modifiers, essence/cost deltas, and source details are surfaced here before the implant is added." + Environment.NewLine +
            "Grade, book, and availability filters stay visible like the old selection form while Add & More remains available.";

        return
        [
            BuildSelectionSectionsField("uiCyberwareSections"),
            BuildSelectionTreeField("uiCyberwareCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiCyberwareSearch", "Search", string.Empty, "Search cyberware"),
            new DesktopDialogField("uiCyberwareCategory", "Category", "Show All", "Show All"),
            new DesktopDialogField("uiCyberwareSelectedBranch", "Selected Branch", "Bodyware", "Bodyware", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            BuildFilterToggleField("uiCyberwareSearchInCategoryOnly", "Search In Category Only", true),
            new DesktopDialogField("uiCyberwareBookFilter", "Data File", "All Books", "All Books"),
            new DesktopDialogField("uiCyberwareName", "Cyberware", "Wired Reflexes 2", "Wired Reflexes 2"),
            new DesktopDialogField("uiCyberwareCandidateList", "Available Cyberware", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiCyberwareBrowseGrid", "Catalog Grid", BuildSelectionBrowseGrid(("Wired Reflexes 2", "Bodyware", "12R", "¥149,000"), ("Reaction Enhancers 2", "Bodyware", "8R", "¥26,000"), ("Cybereyes Rating 4", "Headware", "12", "¥16,000")), "Name | Category | Avail | Cost", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiCyberwareGrade", "Grade", "Standard", "Standard"),
            BuildFilterToggleField("uiCyberwareHideBannedGrades", "Hide Banned Grades", true),
            BuildFilterToggleField("uiCyberwareHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiCyberwarePrototypeTranshuman", "Prototype Transhuman", false),
            BuildFilterToggleField("uiCyberwareBlackMarketDiscount", "Black Market Discount", false),
            new DesktopDialogField("uiCyberwareEssDiscount", "Essence Discount %", "0.00", "0.00", InputType: "number"),
            new DesktopDialogField("uiCyberwareSlot", "Location", "Body", "Body"),
            new DesktopDialogField("uiCyberwareRating", "Rating", "2", "2", InputType: "number"),
            new DesktopDialogField("uiCyberwareMarkup", "Markup %", "0", "0", InputType: "number"),
            new DesktopDialogField("uiCyberwareDiscount", "Discount %", "0", "0", InputType: "number"),
            new DesktopDialogField("uiCyberwareEssence", "Essence", "3.00", "3.00", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareCapacity", "Capacity", "n/a", "n/a", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareCost", "Cost", "149000", "149000", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareSource", "Source", "Core Rulebook p. 461", "Core Rulebook p. 461", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiCyberwareSelectionTrail", selectionTrailPath, "Wired Reflexes 2", "Add & More keeps the selector open"),
            BuildSelectionCommandsField("uiCyberwareCategoryCommands", "Category Commands",
                "Group | Core Systems",
                "Category | Bodyware",
                "Data File | Core Rulebook",
                "Move the tree without losing grade or availability",
                "Suites and accessories after picking the base implant"),
            new DesktopDialogField("uiCyberwareFilterSummary", "Filter Summary", "Filtered Catalog | 3 shown / 9 total" + Environment.NewLine + "Category Path | Cyberware > Core Systems > Bodyware" + Environment.NewLine + "Filters | grade, availability, and source stay live", "Filtered Catalog | 3 shown / 9 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiCyberwareLiveRecalc", "Live Recalculation", "Recalculated Cost | ¥149,000" + Environment.NewLine + "Recalculated Essence | 3.00" + Environment.NewLine + "Black Market | No" + Environment.NewLine + "Add Again | Stays open", "Recalculated Cost | ¥149,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiCyberwareResultCommands", "Result Commands",
                "Compare source, cost, and essence on the right before adding",
                "Use OK for one add or Add & More to keep the selector open",
                "Open source detail after confirming the right implant"),
            new DesktopDialogField("uiCyberwareNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildCyberwareEditFields()
    {
        string details =
            "Selected | Cybereyes Rating 4" + Environment.NewLine +
            "Category | Headware" + Environment.NewLine +
            "Availability | 12" + Environment.NewLine +
            "Essence | 0.40" + Environment.NewLine +
            "Source | Core Rulebook p. 455";
        string notes =
            "Keep grade, rating, essence, and source visible while editing the installed implant." + Environment.NewLine +
            "Use the runner implant tabs for accessories and modular payload follow-up changes.";

        return
        [
            BuildSelectionSectionsField("uiCyberwareEditSections"),
            new DesktopDialogField("uiCyberwareEditContextTree", "Navigation", "[Cyberware]" + Environment.NewLine + "├─ Headware" + Environment.NewLine + "└─ > Cybereyes Rating 4", "[Cyberware]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiCyberwareEditNeighborList", "Installed Ware", "Datajack" + Environment.NewLine + "> Cybereyes Rating 4" + Environment.NewLine + "Image Link", "Datajack", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiCyberwareEditName", "Cyberware", "Cybereyes Rating 4", "Cybereyes Rating 4"),
            new DesktopDialogField("uiCyberwareEditCategory", "Category", "Headware", "Headware", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareEditGrade", "Grade", "Standard", "Standard"),
            new DesktopDialogField("uiCyberwareEditRating", "Rating", "4", "4", InputType: "number"),
            new DesktopDialogField("uiCyberwareEditCost", "Cost", "16000", "16000", InputType: "number"),
            new DesktopDialogField("uiCyberwareEditEssence", "Essence", "0.40", "0.40", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareEditSource", "Source", "Core Rulebook p. 455", "Core Rulebook p. 455", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareEditDetails", "Implant Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiCyberwareEditLiveSummary", "Live Summary", "Recalculated Cost | ¥16,000" + Environment.NewLine + "Recalculated Essence | 0.40" + Environment.NewLine + "Mode | edit installed ware" + Environment.NewLine + "Follow-through | use implant tabs for payloads", "Recalculated Cost | ¥16,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiCyberwareEditCommands", "Commands", "Adjust grade, rating, or cost while details stay visible" + Environment.NewLine + "Keep implant list context visible" + Environment.NewLine + "Return to cyberware tabs for payload follow-through", "Adjust grade, rating, or cost while details stay visible", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiCyberwareEditNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildGearSelectionFields()
    {
        IReadOnlyList<DesktopDialogFieldOption> categoryOptions = BuildSelectionCategoryOptions(
            "Show All",
            "Armor",
            "Visual",
            "Pistols",
            "Medical");
        IReadOnlyList<DesktopDialogFieldOption> dataFileOptions = BuildSelectionDataFileOptions(
            "All Books",
            "Core Rulebook");
        string categoryTree = BuildSelectionGroupedBranchTree(
            "Gear",
            [
                ("Armor", "Armor"),
                ("Electronics", "Visual"),
                ("Firearms", "Pistols"),
                ("General", "Medical")
            ],
            "Pistols");
        string candidateList =
            "Ares Predator V · Pistol · ¥725" + Environment.NewLine +
            "Armor Jacket · Armor · ¥1000" + Environment.NewLine +
            "Medkit Rating 6 · Gear · ¥1500";
        string selectionDetails = BuildGridValue(
            ("Selected", "Ares Predator V"),
            ("Category", "Firearms"),
            ("Availability", "5R"),
            ("Cost", "¥725"),
            ("Book", "Core Rulebook"));
        return
        [
            BuildSelectionSectionsField("uiGearSections"),
            BuildSelectionTreeField("uiGearCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiGearSearch", "Search", string.Empty, "Search gear"),
            new DesktopDialogField("uiGearCategory", "Category", "Show All", "Show All", InputType: "select", Options: categoryOptions),
            new DesktopDialogField("uiGearSelectedBranch", "Selected Branch", "Pistols", "Pistols", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            BuildFilterToggleField("uiGearSearchInCategoryOnly", "Search In Category Only", true),
            new DesktopDialogField("uiGearBookFilter", "Data File", "All Books", "All Books", InputType: "select", Options: dataFileOptions),
            new DesktopDialogField("uiGearName", "Gear Name", "Ares Predator V", "Ares Predator V"),
            new DesktopDialogField("uiGearCandidateList", "Available Gear", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiGearBrowseGrid", "Catalog Grid", BuildSelectionBrowseGrid(("Ares Predator V", "Pistols", "5R", "¥725"), ("Armor Jacket", "Armor", "12", "¥1,000"), ("Medkit Rating 6", "Medical", "8", "¥1,500")), "Name | Category | Avail | Cost", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildFilterToggleField("uiGearHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiGearShowOnlyAffordItems", "Show Only Items I Can Afford", false),
            BuildFilterToggleField("uiGearBlackMarketDiscount", "Black Market Discount", false),
            BuildFilterToggleField("uiGearDoItYourself", "Do It Yourself", false),
            BuildFilterToggleField("uiGearStack", "Stack", true),
            BuildFilterToggleField("uiGearFreeItem", "Free Item", false),
            new DesktopDialogField("uiGearRating", "Rating", "0", "0", InputType: "number"),
            new DesktopDialogField("uiGearQuantity", "Quantity", "1", "1", InputType: "number"),
            new DesktopDialogField("uiGearMarkup", "Markup %", "0", "0", InputType: "number"),
            new DesktopDialogField("uiGearCost", "Cost", "725", "725", IsReadOnly: true),
            new DesktopDialogField("uiGearSource", "Source", "Core Rulebook p. 424", "Core Rulebook p. 424", IsReadOnly: true),
            new DesktopDialogField("uiGearSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiGearSelectionTrail", BuildSelectionCategoryPath("Gear", "Firearms", "Pistols", "Ares Predator V"), "Ares Predator V", "Stack and discount stay live"),
            BuildSelectionCommandsField("uiGearCategoryCommands", "Category Commands",
                "Group | Firearms",
                "Category | Pistols",
                "Data File | Core Rulebook",
                "Move the tree without losing source or legality",
                "Keep Do It Yourself and Stack visible while browsing"),
            new DesktopDialogField("uiGearFilterSummary", "Filter Summary", "Filtered Catalog | 6 shown / 8 total" + Environment.NewLine + "Category Path | Gear > Firearms > Pistols" + Environment.NewLine + "Filters | availability, source, and pricing stay live", "Filtered Catalog | 6 shown / 8 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiGearLiveRecalc", "Live Recalculation", "Recalculated Cost | ¥725" + Environment.NewLine + "Free Item | No" + Environment.NewLine + "Black Market | No" + Environment.NewLine + "Add Again | Stays open", "Recalculated Cost | ¥725", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiGearResultCommands", "Result Commands",
                "Compare cost, rating, and legality on the right before adding",
                "Use OK for one add or Add & More to keep shopping",
                "Keep markup, quantity, and source visible through confirmation"),
            new DesktopDialogField("uiGearNotes", "Notes", "Use gear details to confirm legality, source, rating, and discount before adding.", "Use gear details to confirm legality, source, rating, and discount before adding.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildGearEditFields()
    {
        string details =
            "Selected | Armor Jacket" + Environment.NewLine +
            "Category | Armor" + Environment.NewLine +
            "Availability | 12" + Environment.NewLine +
            "Wireless | n/a" + Environment.NewLine +
            "Legality | Restricted carry not required";
        string notes =
            "Edit quantity, rating, and cost adjustments while keeping the summary visible." + Environment.NewLine +
            "Use the runner gear tabs for nested accessories after confirming the base item.";

        return
        [
            BuildSelectionSectionsField("uiGearEditSections"),
            new DesktopDialogField("uiGearEditContextTree", "Navigation", "[Inventory]" + Environment.NewLine + "├─ Armor" + Environment.NewLine + "└─ > Armor Jacket", "[Inventory]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiGearEditNeighborList", "Current List", "Armor Vest" + Environment.NewLine + "> Armor Jacket" + Environment.NewLine + "Actioneer Business Clothes", "Armor Vest", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiGearEditName", "Gear Name", "Armor Jacket", "Armor Jacket"),
            new DesktopDialogField("uiGearEditCategory", "Category", "Armor", "Armor", IsReadOnly: true),
            new DesktopDialogField("uiGearEditRating", "Rating", "0", "0", InputType: "number"),
            new DesktopDialogField("uiGearEditQuantity", "Quantity", "1", "1", InputType: "number"),
            new DesktopDialogField("uiGearEditCost", "Cost", "1000", "1000", InputType: "number"),
            new DesktopDialogField("uiGearEditSource", "Source", "Core Rulebook p. 437", "Core Rulebook p. 437", IsReadOnly: true),
            new DesktopDialogField("uiGearEditDetails", "Item Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiGearEditLiveSummary", "Live Summary", "Total Cost | ¥1,000" + Environment.NewLine + "Wireless | n/a" + Environment.NewLine + "Legality | Restricted carry not required" + Environment.NewLine + "Posture | legacy edit utility", "Total Cost | ¥1,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiGearEditCommands", "Commands", "Adjust quantity, rating, or price while details stay visible" + Environment.NewLine + "Keep inventory list context visible" + Environment.NewLine + "Return to gear tabs for accessories and mounts", "Adjust quantity, rating, or price while details stay visible", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiGearEditNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildMagicSelectionFields()
    {
        string categoryTree =
            "[Magic]" + Environment.NewLine +
            "├─ Spells" + Environment.NewLine +
            "├─ Adept Powers" + Environment.NewLine +
            "├─ Complex Forms" + Environment.NewLine +
            "└─ Summoning";
        string candidateList =
            "Stunbolt · Combat · DV F-3" + Environment.NewLine +
            "Improved Reflexes · Adept Power · 2.5 PP" + Environment.NewLine +
            "Cleaner · Complex Form · Level × 1";
        string selectionDetails = BuildGridValue(
            ("Selected", "Stunbolt"),
            ("Category", "Combat"),
            ("Drain", "F-3"),
            ("Source", "Core Rulebook p. 288"));

        return
        [
            BuildSelectionSectionsField("uiMagicSections"),
            BuildSelectionTreeField("uiMagicCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiMagicSearch", "Search", string.Empty, "Search spell or power"),
            new DesktopDialogField("uiMagicFamily", "Family", "Spell", "Spell"),
            new DesktopDialogField("uiMagicName", "Name", "Stunbolt", "Stunbolt"),
            new DesktopDialogField("uiMagicCandidateList", "Available Entries", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiMagicCategory", "Category", "Combat", "Combat"),
            new DesktopDialogField("uiMagicLevel", "Level", "1", "1", InputType: "number"),
            new DesktopDialogField("uiMagicSource", "Source", "Core Rulebook p. 288", "Core Rulebook p. 288", IsReadOnly: true),
            new DesktopDialogField("uiMagicSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiMagicNotes", "Notes", "Drain, PP, or target limits stay visible here before the selection is confirmed.", "Drain, PP, or target limits stay visible here before the selection is confirmed.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildSpellSelectionFields()
    {
        string categoryTree =
            "[Spells]" + Environment.NewLine +
            "├─ Combat" + Environment.NewLine +
            "├─ Detection" + Environment.NewLine +
            "├─ Health" + Environment.NewLine +
            "└─ Illusion";
        string candidateList =
            "Stunbolt · Combat · DV F-3" + Environment.NewLine +
            "Heal · Health · DV F-4" + Environment.NewLine +
            "Improved Invisibility · Illusion · DV F-1";
        string selectionDetails = BuildGridValue(
            ("Selected", "Stunbolt"),
            ("Category", "Combat"),
            ("Type", "Mana"),
            ("Drain", "F-3"),
            ("Book", "Core Rulebook"));

        return
        [
            BuildSelectionSectionsField("uiSpellSections"),
            BuildSelectionTreeField("uiSpellCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiSpellSearch", "Search", string.Empty, "Search spells"),
            new DesktopDialogField("uiSpellCategoryFilter", "Category Filter", "All", "All"),
            new DesktopDialogField("uiSpellBookFilter", "Data File", "All Books", "All Books"),
            new DesktopDialogField("uiSpellName", "Spell", "Stunbolt", "Stunbolt"),
            new DesktopDialogField("uiSpellCandidateList", "Available Spells", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiSpellExtendedOnly", "Extended Catalog", true),
            new DesktopDialogField("uiSpellCategory", "Category", "Combat", "Combat"),
            new DesktopDialogField("uiSpellSource", "Source", "Core Rulebook p. 288", "Core Rulebook p. 288", IsReadOnly: true),
            new DesktopDialogField("uiSpellSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiSpellNotes", "Notes", "Spell source, category, drain, and catalog scope remain visible through confirmation.", "Spell source, category, drain, and catalog scope remain visible through confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildAdeptPowerSelectionFields()
    {
        string categoryTree =
            "[Adept Powers]" + Environment.NewLine +
            "├─ Combat" + Environment.NewLine +
            "├─ Movement" + Environment.NewLine +
            "├─ Sensory" + Environment.NewLine +
            "└─ Utility";
        string candidateList =
            "Improved Reflexes · 2.5 PP" + Environment.NewLine +
            "Combat Sense · 0.5 PP/level" + Environment.NewLine +
            "Killing Hands · 0.5 PP";
        string selectionDetails = BuildGridValue(
            ("Selected", "Improved Reflexes"),
            ("Power Points", "2.5"),
            ("Level", "1"),
            ("Source", "Core Rulebook p. 309"));

        return
        [
            BuildSelectionSectionsField("uiAdeptPowerSections"),
            BuildSelectionTreeField("uiAdeptPowerCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiAdeptPowerSearch", "Search", string.Empty, "Search adept powers"),
            new DesktopDialogField("uiAdeptPowerName", "Power", "Improved Reflexes", "Improved Reflexes"),
            new DesktopDialogField("uiAdeptPowerCandidateList", "Available Powers", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiAdeptPowerLevel", "Level", "1", "1", InputType: "number"),
            new DesktopDialogField("uiAdeptPowerSource", "Source", "Core Rulebook p. 309", "Core Rulebook p. 309", IsReadOnly: true),
            new DesktopDialogField("uiAdeptPowerSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiAdeptPowerNotes", "Notes", "Power-point cost and source stay visible before confirmation.", "Power-point cost and source stay visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildDrugSelectionFields()
    {
        string categoryTree =
            "[Drugs]" + Environment.NewLine +
            "├─ Combat" + Environment.NewLine +
            "├─ Stimulants" + Environment.NewLine +
            "├─ Focus" + Environment.NewLine +
            "└─ Crash Recovery";
        string candidateList =
            "Jazz · Initiative boost · 1 dose" + Environment.NewLine +
            "Cram · Alertness boost · 1 dose" + Environment.NewLine +
            "Psyche · Sustained focus · 1 dose";
        string selectionDetails = BuildGridValue(
            ("Selected", "Jazz"),
            ("Speed", "1 Combat Turn"),
            ("Crash", "1 hour"),
            ("Source", "Core Rulebook p. 411"));

        return
        [
            BuildSelectionSectionsField("uiDrugSections"),
            BuildSelectionTreeField("uiDrugCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiDrugSearch", "Search", string.Empty, "Search drugs"),
            new DesktopDialogField("uiDrugName", "Drug", "Jazz", "Jazz"),
            new DesktopDialogField("uiDrugCandidateList", "Available Drugs", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDrugQuantity", "Quantity", "1", "1", InputType: "number"),
            new DesktopDialogField("uiDrugSource", "Source", "Core Rulebook p. 411", "Core Rulebook p. 411", IsReadOnly: true),
            new DesktopDialogField("uiDrugSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDrugNotes", "Notes", "Speed, crash, and source remain visible before the dose is added.", "Speed, crash, and source remain visible before the dose is added.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildDeleteConfirmationFields(string entityName, string summary, string notes)
    {
        string navigationTree =
            "[Current Runner]" + Environment.NewLine +
            "├─ Active Section" + Environment.NewLine +
            $"└─ {entityName}";
        string nearbyEntries =
            "Previous Entry" + Environment.NewLine +
            $"> {entityName}" + Environment.NewLine +
            "Next Entry";
        string recoveryCommands =
            "Check parent section totals" + Environment.NewLine +
            "Re-open the same picker family" + Environment.NewLine +
            "Return to the current tab";

        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Notes"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", navigationTree, navigationTree, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current List", nearbyEntries, nearbyEntries, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", entityName, entityName, IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", NormalizeGridValue(summary), NormalizeGridValue(summary), IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", "Removal Scope | current runner only" + Environment.NewLine + "Undo | re-add manually from the same utility family" + Environment.NewLine + "Neighbor Context | surrounding list remains in view", "Removal Scope | current runner only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", recoveryCommands, recoveryCommands, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildGearDeleteFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Inventory]" + Environment.NewLine + "├─ Armor" + Environment.NewLine + "└─ > Armor Jacket", "[Inventory]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current List", "Armor Vest" + Environment.NewLine + "> Armor Jacket" + Environment.NewLine + "Actioneer Business Clothes", "Armor Vest", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "Armor Jacket", "Armor Jacket", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Category", "Armor"), ("Cost", "¥1000"), ("Source", "Core Rulebook p. 437"), ("Encumbrance", "none")), "Category | Armor", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "dossier inventory only"), ("Armor Totals", "recalculate after remove"), ("Undo", "re-add from gear selector"), ("Dossier", "inventory tab stays active")), "Removal Scope | dossier inventory only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to gear tab" + Environment.NewLine + "Re-open Add Gear" + Environment.NewLine + "Armor totals and mods", "Return to gear tab", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected item will be removed from the active dossier inventory while the current gear list remains visible.", "The selected item will be removed from the active dossier inventory while the current gear list remains visible.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildCyberwareDeleteFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Cyberware]" + Environment.NewLine + "├─ Headware" + Environment.NewLine + "└─ > Cybereyes Rating 4", "[Cyberware]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Installed Ware", "Datajack" + Environment.NewLine + "> Cybereyes Rating 4" + Environment.NewLine + "Image Link", "Datajack", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "Cybereyes Rating 4", "Cybereyes Rating 4", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Category", "Headware"), ("Essence", "0.40"), ("Capacity", "16"), ("Source", "Core Rulebook p. 455")), "Category | Headware", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "installed ware only"), ("Essence Refund", "none"), ("Undo", "re-add from selector"), ("Dossier", "cyberware tab stays active")), "Removal Scope | installed ware only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to cyberware tab" + Environment.NewLine + "Re-open Add Cyberware" + Environment.NewLine + "Essence and capacity totals", "Return to cyberware tab", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected implant will be removed from the active dossier while essence and capacity stay explicit in the same utility pane.", "The selected implant will be removed from the active dossier while essence and capacity stay explicit in the same utility pane.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildVehicleDeleteFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Vehicles]" + Environment.NewLine + "├─ Cars" + Environment.NewLine + "└─ > GMC Roadmaster", "[Vehicles]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current Garage", "Hyundai Shin-Hyung" + Environment.NewLine + "> GMC Roadmaster" + Environment.NewLine + "MCT Fly-Spy", "Hyundai Shin-Hyung", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "GMC Roadmaster", "GMC Roadmaster", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Role", "Truck"), ("Armor", "16"), ("Seats", "6"), ("Source", "Core Rulebook p. 466")), "Role | Truck", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "garage only"), ("Mounted Gear", "check after remove"), ("Undo", "re-add from vehicle selector"), ("Dossier", "vehicle tab stays active")), "Removal Scope | garage only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to vehicle tab" + Environment.NewLine + "Re-open Add Vehicle / Drone" + Environment.NewLine + "Mods, mounts, and seats", "Return to vehicle tab", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected vehicle or drone will be removed while garage context remains visible for the next decision.", "The selected vehicle or drone will be removed while garage context remains visible for the next decision.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildSkillRemoveFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Skills]" + Environment.NewLine + "├─ Active" + Environment.NewLine + "└─ > Perception", "[Skills]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current List", "Etiquette" + Environment.NewLine + "> Perception" + Environment.NewLine + "Sneaking", "Etiquette", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "Perception", "Perception", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Category", "Active Skill"), ("Rating", "6"), ("Linked Attribute", "Intuition"), ("Specialization", "Visual")), "Category | Active Skill", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "skill list only"), ("Derived Dice", "recalculate after remove"), ("Undo", "re-add from skill selector"), ("Dossier", "skills tab stays active")), "Removal Scope | skill list only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to skills tab" + Environment.NewLine + "Re-open Add Skill" + Environment.NewLine + "Linked attribute totals", "Return to skills tab", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected skill will be removed while surrounding skill context remains visible like the old utility flow.", "The selected skill will be removed while surrounding skill context remains visible like the old utility flow.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildEntryDeleteFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Current List]" + Environment.NewLine + "├─ Entry Group" + Environment.NewLine + "└─ > Current Entry", "[Current List]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current List", "Previous Entry" + Environment.NewLine + "> Current Entry" + Environment.NewLine + "Next Entry", "Previous Entry", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "Current Entry", "Current Entry", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Group", "Entry Group"), ("Operation", "irreversible remove"), ("Source Posture", "current utility list"), ("Workbench", "current tab stays active")), "Group | Entry Group", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "active list only"), ("Undo", "re-create manually from the same utility"), ("Neighbor Context", "surrounding entries remain visible"), ("Focus", "selection moves to adjacent entry")), "Removal Scope | active list only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to current list" + Environment.NewLine + "Re-open Add Entry" + Environment.NewLine + "Adjacent entries", "Return to current list", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected entry will be removed while the surrounding list stays visible.", "The selected entry will be removed while the surrounding list stays visible.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildDrugDeleteFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Consumables]" + Environment.NewLine + "├─ Combat Drugs" + Environment.NewLine + "└─ > Jazz", "[Consumables]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current Ledger", "Cram" + Environment.NewLine + "> Jazz" + Environment.NewLine + "Kamikaze", "Cram", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "Jazz", "Jazz", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Quantity", "1"), ("Speed", "1 Combat Turn"), ("Crash", "Stun + fatigue"), ("Source", "Core Rulebook p. 411")), "Quantity | 1", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "dossier ledger only"), ("Crash Tracking", "check after remove"), ("Undo", "re-add from drug selector"), ("Dossier", "drugs tab stays active")), "Removal Scope | dossier ledger only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to drugs tab" + Environment.NewLine + "Re-open Add Drug" + Environment.NewLine + "Crash and addiction notes", "Return to drugs tab", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected drug entry will be removed while quantity, crash state, and nearby doses remain visible.", "The selected drug entry will be removed while quantity, crash state, and nearby doses remain visible.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildMagicDeleteFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Magic]" + Environment.NewLine + "├─ Spells" + Environment.NewLine + "└─ > Stunbolt", "[Magic]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current List", "Heal" + Environment.NewLine + "> Stunbolt" + Environment.NewLine + "Increase Reflexes", "Heal", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "Stunbolt", "Stunbolt", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Category", "Combat"), ("Drain", "F-3"), ("Type", "Mana"), ("Source", "Core Rulebook p. 288")), "Category | Combat", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "magic list only"), ("Drain Notes", "current drain options stay visible"), ("Undo", "re-learn from spell selector"), ("Dossier", "magic tab stays active")), "Removal Scope | magic list only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to magic tab" + Environment.NewLine + "Re-open Add Spell / Power" + Environment.NewLine + "Drain and category", "Return to magic tab", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected magical entry will be removed while category, drain, and neighboring spell context stay visible like the old utility flow.", "The selected magical entry will be removed while category, drain, and neighboring spell context stay visible like the old utility flow.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildContactRemoveFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Contacts]" + Environment.NewLine + "├─ Professional" + Environment.NewLine + "└─ > Mr. Johnson", "[Contacts]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current Roster", "Cecilia Vargas" + Environment.NewLine + "> Mr. Johnson" + Environment.NewLine + "Nyx", "Cecilia Vargas", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "Mr. Johnson", "Mr. Johnson", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Role", "Fixer"), ("Connection / Loyalty", "5 / 3"), ("Location", "Seattle"), ("Notes", "Premium jobs")), "Role | Fixer", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "contact roster only"), ("Linked Notes", "check after remove"), ("Undo", "re-add from contact dialog"), ("Dossier", "contacts tab stays active")), "Removal Scope | contact roster only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to contacts tab" + Environment.NewLine + "Re-open Add Contact" + Environment.NewLine + "Nearby contact notes", "Return to contacts tab", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected contact will be removed while connection, loyalty, and surrounding roster context remain visible in the same utility pane.", "The selected contact will be removed while connection, loyalty, and surrounding roster context remain visible in the same utility pane.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildQualityDeleteFields()
    {
        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Recovery"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", "[Qualities]" + Environment.NewLine + "├─ Positive" + Environment.NewLine + "└─ > First Impression", "[Qualities]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current List", "Analytical Mind" + Environment.NewLine + "> First Impression" + Environment.NewLine + "Distinctive Style", "Analytical Mind", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", "First Impression", "First Impression", IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", BuildGridValue(("Type", "Positive"), ("Karma", "11"), ("Source", "Core Rulebook p. 73"), ("Tag", "Social")), "Type | Positive", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", BuildGridValue(("Removal Scope", "quality list only"), ("Karma", "recalculate after remove"), ("Undo", "re-add from quality selector"), ("Dossier", "qualities tab stays active")), "Removal Scope | quality list only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", "Return to qualities tab" + Environment.NewLine + "Re-open Add Quality" + Environment.NewLine + "Karma totals and tags", "Return to qualities tab", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", "The selected quality will be removed while karma, source, and surrounding list context remain visible.", "The selected quality will be removed while karma, source, and surrounding list context remain visible.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildComplexFormSelectionFields()
    {
        string categoryTree =
            "[Complex Forms]" + Environment.NewLine +
            "├─ Persona" + Environment.NewLine +
            "├─ Device" + Environment.NewLine +
            "├─ File" + Environment.NewLine +
            "└─ Resonance";
        string candidateList =
            "Cleaner · Target: Persona" + Environment.NewLine +
            "Diffusion of Firewall · Target: Device" + Environment.NewLine +
            "Editor · Target: File";
        string selectionDetails = BuildGridValue(
            ("Selected", "Cleaner"),
            ("Target", "Persona"),
            ("Level", "1"),
            ("Source", "Data Trails p. 178"));

        return
        [
            BuildSelectionSectionsField("uiComplexFormSections"),
            BuildSelectionTreeField("uiComplexFormCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiComplexFormSearch", "Search", string.Empty, "Search complex forms"),
            new DesktopDialogField("uiComplexFormName", "Complex Form", "Cleaner", "Cleaner"),
            new DesktopDialogField("uiComplexFormCandidateList", "Available Forms", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiComplexFormLevel", "Level", "1", "1", InputType: "number"),
            new DesktopDialogField("uiComplexFormSource", "Source", "Data Trails p. 178", "Data Trails p. 178", IsReadOnly: true),
            new DesktopDialogField("uiComplexFormSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiComplexFormNotes", "Notes", "Targeting and source stay visible before the form is confirmed.", "Targeting and source stay visible before the form is confirmed.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildMatrixProgramSelectionFields()
    {
        string categoryTree =
            "[Programs]" + Environment.NewLine +
            "├─ Common" + Environment.NewLine +
            "├─ Hacking" + Environment.NewLine +
            "├─ Cyberdeck Items" + Environment.NewLine +
            "└─ Dongles";
        string candidateList =
            "Armor · Common Program" + Environment.NewLine +
            "Baby Monitor · Hacking Program" + Environment.NewLine +
            "Stealth Dongle · Cyberdeck Item";
        string selectionDetails = BuildGridValue(
            ("Selected", "Armor"),
            ("Slot", "Common"),
            ("Cost", "¥600"),
            ("Source", "Data Trails p. 60"),
            ("Book", "Data Trails"));

        return
        [
            BuildSelectionSectionsField("uiMatrixProgramSections"),
            BuildSelectionTreeField("uiMatrixProgramCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiMatrixProgramSearch", "Search", string.Empty, "Search programs"),
            new DesktopDialogField("uiMatrixProgramBookFilter", "Data File", "Data Trails", "Data Trails"),
            new DesktopDialogField("uiMatrixProgramName", "Program", "Armor", "Armor"),
            new DesktopDialogField("uiMatrixProgramCandidateList", "Available Programs", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiMatrixProgramHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiMatrixProgramShowDongles", "Show Dongles", true),
            new DesktopDialogField("uiMatrixProgramSlot", "Slot", "Common", "Common"),
            new DesktopDialogField("uiMatrixProgramSource", "Source", "Data Trails p. 60", "Data Trails p. 60", IsReadOnly: true),
            new DesktopDialogField("uiMatrixProgramSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiMatrixProgramNotes", "Notes", "Program slot, source, and matrix-category filters remain visible before confirmation.", "Program slot, source, and matrix-category filters remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildSkillSelectionFields()
    {
        string categoryTree =
            "[Skills]" + Environment.NewLine +
            "├─ Active" + Environment.NewLine +
            "├─ Knowledge" + Environment.NewLine +
            "├─ Language" + Environment.NewLine +
            "└─ Groups";
        string candidateList =
            "Perception · Active Skill · Linked Attribute: Intuition" + Environment.NewLine +
            "Sneaking · Active Skill · Linked Attribute: Agility" + Environment.NewLine +
            "Pilot Ground Craft · Active Skill · Linked Attribute: Reaction";
        string selectionDetails = BuildGridValue(
            ("Selected", "Perception"),
            ("Category", "Active Skill"),
            ("Attribute", "Intuition"),
            ("Defaulting", "Yes"),
            ("Book", "Core Rulebook"));

        return
        [
            BuildSelectionSectionsField("uiSkillSections"),
            BuildSelectionTreeField("uiSkillCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiSkillSearch", "Search", string.Empty, "Search skills"),
            new DesktopDialogField("uiSkillCategory", "Category", "Active", "Active"),
            new DesktopDialogField("uiSkillSelectedBranch", "Selected Branch", "Active", "Active", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("uiSkillSearchInCategoryOnly", "Search In Category Only", "true", "true", InputType: "checkbox", LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("uiSkillBookFilter", "Data File", "Core Rulebook", "Core Rulebook"),
            new DesktopDialogField("uiSkillName", "Skill", "Perception", "Perception"),
            new DesktopDialogField("uiSkillCandidateList", "Available Skills", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiSkillShowOnlyUsable", "Show Usable Skills Only", true),
            BuildFilterToggleField("uiSkillShowKnowledge", "Show Knowledge Skills", false),
            new DesktopDialogField("uiSkillRating", "Rating", "1", "1", InputType: "number"),
            new DesktopDialogField("uiSkillSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiSkillNotes", "Notes", "Skill category, linked attribute, defaulting, and skill-family filters remain visible before confirmation.", "Skill category, linked attribute, defaulting, and skill-family filters remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static DesktopDialogState RebuildSkillSelectionDialog(DesktopDialogState dialog)
    {
        string category = DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillCategory") ?? "Active";
        bool searchInCategoryOnly = DesktopDialogFieldValueParser.ParseBool(dialog, "uiSkillSearchInCategoryOnly", true);
        bool showOnlyUsable = DesktopDialogFieldValueParser.ParseBool(dialog, "uiSkillShowOnlyUsable", true);
        bool showKnowledge = DesktopDialogFieldValueParser.ParseBool(dialog, "uiSkillShowKnowledge", false);
        string search = (DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillSearch") ?? string.Empty).Trim();
        string requestedName = DesktopDialogFieldValueParser.GetValue(dialog, "uiSkillName") ?? "Perception";

        var options = new[]
        {
            new { Name = "Perception", Branch = "Active", Family = "Active Skill", Attribute = "Intuition", Defaulting = "Yes", Book = "Core Rulebook", CandidateLine = "Perception · Active Skill · Linked Attribute: Intuition", Usable = true },
            new { Name = "Sneaking", Branch = "Active", Family = "Active Skill", Attribute = "Agility", Defaulting = "Yes", Book = "Core Rulebook", CandidateLine = "Sneaking · Active Skill · Linked Attribute: Agility", Usable = true },
            new { Name = "Pilot Ground Craft", Branch = "Active", Family = "Active Skill", Attribute = "Reaction", Defaulting = "No", Book = "Core Rulebook", CandidateLine = "Pilot Ground Craft · Active Skill · Linked Attribute: Reaction", Usable = true },
            new { Name = "Seattle Street Gangs", Branch = "Knowledge", Family = "Knowledge Skill", Attribute = "Logic", Defaulting = "No", Book = "Core Rulebook", CandidateLine = "Seattle Street Gangs · Knowledge Skill · Linked Attribute: Logic", Usable = false },
            new { Name = "Sperethiel", Branch = "Language", Family = "Language Skill", Attribute = "Intuition", Defaulting = "No", Book = "Core Rulebook", CandidateLine = "Sperethiel · Language Skill · Linked Attribute: Intuition", Usable = false },
            new { Name = "Stealth Group", Branch = "Groups", Family = "Skill Group", Attribute = "Agility", Defaulting = "No", Book = "Core Rulebook", CandidateLine = "Stealth Group · Skill Group · Linked Attribute: Agility", Usable = true }
        };

        var filtered = options
            .Where(option => MatchesSelectionCategory(category, option.Branch))
            .Where(option => showKnowledge
                || option.Usable
                || string.Equals(category, "Knowledge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, "Language", StringComparison.OrdinalIgnoreCase))
            .Where(option => string.IsNullOrWhiteSpace(search)
                || option.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (!searchInCategoryOnly && option.Branch.Contains(search, StringComparison.OrdinalIgnoreCase))
                || (!searchInCategoryOnly && option.Family.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (filtered.Length == 0)
            filtered = options.Where(option => MatchesSelectionCategory(category, option.Branch)).ToArray();
        if (filtered.Length == 0)
            filtered = options;

        var selected = filtered.FirstOrDefault(option => string.Equals(option.Name, requestedName, StringComparison.OrdinalIgnoreCase))
            ?? filtered[0];
        string effectiveCategory = MatchesSelectionCategory(category, selected.Branch)
            ? category
            : selected.Branch;
        string categoryTree = BuildSelectionBranchTree("Skills", options.Select(option => option.Branch), ResolveSelectionTreeBranch(category, selected.Branch));
        string candidateList = BuildSelectionList(filtered.Select(option => $"{(string.Equals(option.Name, selected.Name, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {option.CandidateLine}"));
        string selectionDetails = BuildGridValue(
            ("Selected", selected.Name),
            ("Category", selected.Family),
            ("Attribute", selected.Attribute),
            ("Defaulting", selected.Defaulting),
            ("Search Scope", BuildSelectionSearchScope(searchInCategoryOnly)),
            ("Book", selected.Book));

        return ReplaceDialogActions(
            ReplaceDialogFields(
                dialog,
                ("uiSkillCategory", effectiveCategory, effectiveCategory),
                ("uiSkillCategoryTree", categoryTree, categoryTree),
                ("uiSkillCandidateList", candidateList, candidateList),
                ("uiSkillName", selected.Name, selected.Name),
                ("uiSkillSelectedBranch", selected.Branch, selected.Branch),
                ("uiSkillSelectionDetails", selectionDetails, selectionDetails)),
            ("focus_category", BuildSelectionCategoryActionLabel(effectiveCategory, selected.Branch), false),
            ("toggle_search_scope", BuildSelectionSearchActionLabel(searchInCategoryOnly), false),
            ("add", $"Add {selected.Name}", true),
            ("add_more", $"Add & More {selected.Name}", false));
    }

    private static IReadOnlyList<DesktopDialogField> BuildSkillSpecializationFields()
    {
        string details =
            "Selected Skill: Perception" + Environment.NewLine +
            "Current Rating: 6" + Environment.NewLine +
            "Existing Specializations: Audio" + Environment.NewLine +
            "Linked Attribute: Intuition";

        return
        [
            new DesktopDialogField("uiSkillSpecializationSkill", "Skill", "Perception", "Perception", IsReadOnly: true),
            new DesktopDialogField("uiSkillSpec", "Specialization", "Visual", "Visual"),
            new DesktopDialogField("uiSkillSpecializationDetails", "Selection Details", NormalizeGridValue(details), NormalizeGridValue(details), IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiSkillSpecializationNotes", "Notes", "Skill, existing specialization, and linked attribute remain visible before applying the specialization.", "Skill, existing specialization, and linked attribute remain visible before applying the specialization.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildInitiationSelectionFields()
    {
        string categoryTree =
            "[Initiation]" + Environment.NewLine +
            "├─ Metamagics" + Environment.NewLine +
            "├─ Echos" + Environment.NewLine +
            "├─ Ordeals" + Environment.NewLine +
            "└─ Notes";
        string candidateList =
            "Metamagic · Masking" + Environment.NewLine +
            "Metamagic · Centering" + Environment.NewLine +
            "Submersion · Echo";
        string selectionDetails = BuildGridValue(
            ("Selected", "Masking"),
            ("Track", "Initiation"),
            ("Grade", "1"),
            ("Source", "Street Grimoire p. 140"));

        return
        [
            BuildSelectionSectionsField("uiInitiationSections"),
            BuildSelectionTreeField("uiInitiationCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiInitiationTrack", "Track", "Initiation", "Initiation"),
            new DesktopDialogField("uiInitiationGrade", "Grade", "1", "1", InputType: "number"),
            new DesktopDialogField("uiInitiationCandidateList", "Available Rewards", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiInitiationReward", "Reward", "Masking", "Masking"),
            new DesktopDialogField("uiInitiationSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiInitiationNotes", "Notes", "Grade and metamagic/echo choice stay visible before confirmation.", "Grade and metamagic/echo choice stay visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildSpiritSelectionFields()
    {
        string categoryTree =
            "[Spirits]" + Environment.NewLine +
            "├─ Elemental" + Environment.NewLine +
            "├─ Watcher" + Environment.NewLine +
            "├─ Ally" + Environment.NewLine +
            "└─ Other";
        string candidateList =
            "Watcher Spirit · Spirit" + Environment.NewLine +
            "Air Spirit · Spirit" + Environment.NewLine +
            "Ally Spirit · Ally";
        string selectionDetails = BuildGridValue(
            ("Selected", "Watcher Spirit"),
            ("Force", "3"),
            ("Type", "Spirit"),
            ("Source", "Core Rulebook p. 302"));

        return
        [
            BuildSelectionSectionsField("uiSpiritSections"),
            BuildSelectionTreeField("uiSpiritCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiSpiritSearch", "Search", string.Empty, "Search spirits"),
            new DesktopDialogField("uiSpiritType", "Type", "Spirit", "Spirit"),
            new DesktopDialogField("uiSpiritName", "Name", "Watcher Spirit", "Watcher Spirit"),
            new DesktopDialogField("uiSpiritCandidateList", "Available Entries", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiSpiritForce", "Force", "3", "3", InputType: "number"),
            new DesktopDialogField("uiSpiritSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiSpiritNotes", "Notes", "Type, force, and source remain visible before confirmation.", "Type, force, and source remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildSpriteSelectionFields()
    {
        string categoryTree =
            "[Sprites]" + Environment.NewLine +
            "├─ Courier" + Environment.NewLine +
            "├─ Crack" + Environment.NewLine +
            "├─ Fault" + Environment.NewLine +
            "└─ Machine";
        string candidateList =
            "Courier Sprite · Sprite" + Environment.NewLine +
            "Machine Sprite · Sprite" + Environment.NewLine +
            "Fault Sprite · Sprite";
        string selectionDetails = BuildGridValue(
            ("Selected", "Courier Sprite"),
            ("Level", "3"),
            ("Type", "Sprite"),
            ("Source", "Core Rulebook p. 251"));

        return
        [
            BuildSelectionSectionsField("uiSpriteSections"),
            BuildSelectionTreeField("uiSpriteCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiSpriteSearch", "Search", string.Empty, "Search sprites"),
            new DesktopDialogField("uiSpriteType", "Type", "Sprite", "Sprite"),
            new DesktopDialogField("uiSpriteName", "Name", "Courier Sprite", "Courier Sprite"),
            new DesktopDialogField("uiSpriteCandidateList", "Available Entries", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiSpriteForce", "Level", "3", "3", InputType: "number"),
            new DesktopDialogField("uiSpriteSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiSpriteNotes", "Notes", "Type, level, and source remain visible before confirmation.", "Type, level, and source remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildCritterPowerSelectionFields()
    {
        string categoryTree =
            "[Critter Powers]" + Environment.NewLine +
            "├─ Passive" + Environment.NewLine +
            "├─ Active" + Environment.NewLine +
            "├─ Movement" + Environment.NewLine +
            "└─ Combat";
        string candidateList =
            "Natural Weapon · Passive" + Environment.NewLine +
            "Elemental Attack · Active" + Environment.NewLine +
            "Guard · Passive";
        string selectionDetails = BuildGridValue(
            ("Selected", "Natural Weapon"),
            ("Type", "Passive"),
            ("Rating", "1"),
            ("Source", "Core Rulebook p. 398"));

        return
        [
            BuildSelectionSectionsField("uiCritterPowerSections"),
            BuildSelectionTreeField("uiCritterPowerCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiCritterPowerSearch", "Search", string.Empty, "Search critter powers"),
            new DesktopDialogField("uiCritterPowerName", "Power", "Natural Weapon", "Natural Weapon"),
            new DesktopDialogField("uiCritterPowerCandidateList", "Available Powers", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiCritterPowerRating", "Rating", "1", "1", InputType: "number"),
            new DesktopDialogField("uiCritterPowerSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiCritterPowerNotes", "Notes", "Power type, rating, and source remain visible before confirmation.", "Power type, rating, and source remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildSourceDetailsFields()
    {
        string sourceDetails = BuildGridValue(
            ("Book", "Core Rulebook"),
            ("Page", "424"),
            ("PDF", "/books/core-rulebook.pdf#page=424"),
            ("Site Snapshot", "governed"),
            ("Reference", "core release route"));

        return
        [
            BuildUtilitySectionsField("uiSourceSections", "Source", "Details", "Notes"),
            new DesktopDialogField("uiSourceBook", "Book", "Core Rulebook", "Core Rulebook", IsReadOnly: true),
            new DesktopDialogField("uiSourcePage", "Page", "424", "424", IsReadOnly: true),
            new DesktopDialogField("uiSourceDetails", "Source Details", sourceDetails, sourceDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiSourceNotes", "Notes", "Source references stay compact and copyable without pushing the dossier view off screen.", "Source references stay compact and copyable without pushing the dossier view off screen.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildExternalLinkFields(string label, string url, string notes)
    {
        string details = BuildGridValue(
            ("Destination", label),
            ("URL", url),
            ("Action", "open in browser"));

        return
        [
            BuildUtilitySectionsField("uiLinkSections", "Link", "Details", "Notes"),
            new DesktopDialogField("uiLinkLabel", "Destination", label, label, IsReadOnly: true),
            new DesktopDialogField("uiLinkUrl", "URL", url, url, IsReadOnly: true),
            new DesktopDialogField("uiLinkDetails", "Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiLinkNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildPrintUtilityFields(string scope, string notes)
    {
        string details = BuildGridValue(
            ("Scope", scope),
            ("Output", "host print preview"),
            ("Format", "current sheet / PDF-compatible"));

        return
        [
            BuildUtilitySectionsField("uiPrintSections", "Preview", "Details", "Notes"),
            new DesktopDialogField("uiPrintScope", "Print Scope", scope, scope, IsReadOnly: true),
            new DesktopDialogField("uiPrintDetails", "Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiPrintNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildEntryEditorFields(string currentValue, bool isEdit)
    {
        string details = BuildGridValue(
            ("Operation", isEdit ? "Edit entry" : "Create entry"),
            ("Current Value", currentValue),
            ("Posture", "compact list/detail utility"));
        string navigationTree =
            "[Current List]" + Environment.NewLine +
            "├─ Previous Entry" + Environment.NewLine +
            $"└─ {currentValue}";
        string commandList =
            (isEdit ? "Apply changes to the current row" : "Add entry and keep list focus") + Environment.NewLine +
            "Keep the surrounding list visible" + Environment.NewLine +
            "Return to the same utility family";

        return
        [
            BuildUtilitySectionsField("uiEntrySections", "Entry", "Details", "Notes"),
            new DesktopDialogField("uiEntryContextTree", "Navigation", navigationTree, navigationTree, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiEntryCommandList", "Command status", commandList, commandList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField(isEdit ? "uiEditEntryName" : "uiCreateEntryName", "Entry Title", currentValue, currentValue),
            new DesktopDialogField("uiEntryDetails", "Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiEntryNotes", "Notes", "Entry creation and editing stay compact and preserve list context.", "Entry creation and editing stay compact and preserve list context.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildWindowUtilityFields(string title, string notes)
    {
        string details = BuildGridValue(
            ("Action", title),
            ("Scope", "desktop host shell"),
            ("Behavior", "host/platform specific"));

        return
        [
            BuildUtilitySectionsField("uiWindowSections", "Action", "Details", "Notes"),
            new DesktopDialogField("uiWindowAction", "Action", title, title, IsReadOnly: true),
            new DesktopDialogField("uiWindowDetails", "Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiWindowNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildNotesEditorFields(string notes)
    {
        string details = BuildGridValue(
            ("Behavior", "inline notes editing"),
            ("Save target", "active runner profile"),
            ("Posture", "compact notes utility"));

        return
        [
            new DesktopDialogField("uiNotesSections", "Sections", "Notes" + Environment.NewLine + "Metadata", "Notes", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("uiNotesEditor", "Notes", notes, "notes", true),
            new DesktopDialogField("uiNotesDetails", "Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildUpdateUtilityFields()
    {
        string manifest = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_UPDATE_MANIFEST") ?? string.Empty;
        string manifestDisplay = string.IsNullOrWhiteSpace(manifest)
            ? "Chummer public release feed"
            : manifest;
        string updateMode = ResolveUpdateUtilityMode();
        string details = BuildGridValue(
            ("Update source", manifestDisplay),
            ("Update mode", FormatUpdateUtilityMode(updateMode)),
            ("Support after update", "/account/support"));

        return
        [
            BuildUtilitySectionsField("updateSections", "Channel", "Details", "Notes"),
            new DesktopDialogField("updateManifest", "Update source", manifestDisplay, manifestDisplay, IsReadOnly: true),
            new DesktopDialogField("updateMode", "Update mode", FormatUpdateUtilityMode(updateMode), FormatUpdateUtilityMode(updateMode), IsReadOnly: true),
            new DesktopDialogField("updateSupportPath", "Support after update", "/account/support", "/account/support", IsReadOnly: true),
            new DesktopDialogField("updateDetails", "Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("updateNotes", "Notes", "This screen keeps the update source, behavior, and support path visible in one place.", "This screen keeps the update source, behavior, and support path visible in one place.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static string ResolveUpdateUtilityMode()
    {
        DesktopPreferenceState preferences = DesktopPreferenceStateRuntime.Current;
        string? configured = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_UPDATE_MODE");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string normalized = configured.Trim().ToLowerInvariant().Replace("_", "-");
            if (normalized is "full" or "auto" or "automatic" or "full-auto" or "full-autoupdate")
            {
                return "full";
            }

            if (normalized is "notify" or "notification" or "notify-only" or "manual")
            {
                return "notify";
            }

            if (normalized is "off" or "disabled" or "disable" or "none")
            {
                return "off";
            }
        }

        return DesktopPreferenceStateRuntime.NormalizeUpdateMode(preferences.UpdateMode, preferences.CheckForUpdatesOnLaunch);
    }

    private static string FormatUpdateUtilityMode(string updateMode)
    {
        return updateMode switch
        {
            "notify" => "Tell me, do not install",
            "off" => "Do not check",
            _ => "Install updates and restart"
        };
    }

    private static IReadOnlyList<DesktopDialogField> BuildActionReceiptFields(string actionLabel, string details, string notes)
    {
        return
        [
            BuildUtilitySectionsField("uiActionSections", "Action", "Impact", "Notes"),
            new DesktopDialogField("uiActionLabel", "Action", actionLabel, actionLabel, IsReadOnly: true),
            new DesktopDialogField("uiActionDetails", "Details", NormalizeGridValue(details), NormalizeGridValue(details), IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiActionImpact", "Impact", "List Context | preserved" + Environment.NewLine + "Work rhythm | compact classic utility" + Environment.NewLine + "Next step | return to the same section", "List Context | preserved", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiActionNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildVehicleSelectionFields()
    {
        string categoryTree = BuildSelectionGroupedBranchTree(
            "Vehicles",
            [
                ("Ground Vehicles", "Bikes"),
                ("Ground Vehicles", "Cars"),
                ("Drone Platforms", "Drones"),
                ("Ground Vehicles", "Trucks")
            ],
            "Cars");
        string candidateList =
            "Hyundai Shin-Hyung · Car · ¥16,000" + Environment.NewLine +
            "GMC Roadmaster · Truck · ¥74,000" + Environment.NewLine +
            "MCT Fly-Spy · Drone · ¥2,000";
        string selectionDetails = BuildGridValue(
            ("Selected", "Hyundai Shin-Hyung"),
            ("Role", "Vehicle"),
            ("Handling", "4"),
            ("Armor", "8"),
            ("Source", "Core Rulebook p. 465"),
            ("Book", "Core Rulebook"));
        string selectionTrailPath = BuildSelectionCategoryPath("Vehicles", "Ground Vehicles", "Cars", "Hyundai Shin-Hyung");

        return
        [
            BuildSelectionSectionsField("uiVehicleSections"),
            new DesktopDialogField("uiVehicleViewModes", "View Modes", "List View" + Environment.NewLine + "Browse", "Browse", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            BuildSelectionTreeField("uiVehicleCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiVehicleSearch", "Search", string.Empty, "Search vehicles"),
            new DesktopDialogField("uiVehicleCategory", "Category", "Show All", "Show All"),
            new DesktopDialogField("uiVehicleSelectedBranch", "Selected Branch", "Cars", "Cars", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            BuildFilterToggleField("uiVehicleSearchInCategoryOnly", "Search In Category Only", true),
            new DesktopDialogField("uiVehicleRole", "Role", "Vehicle", "Vehicle"),
            new DesktopDialogField("uiVehicleBookFilter", "Data File", "All Books", "All Books"),
            new DesktopDialogField("uiVehicleName", "Vehicle", "Hyundai Shin-Hyung", "Hyundai Shin-Hyung"),
            new DesktopDialogField("uiVehicleCandidateList", "Available Vehicles", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiVehicleBrowseGrid", "Catalog Grid", BuildSelectionBrowseGrid(("Hyundai Shin-Hyung", "Cars", "8", "¥16,000"), ("GMC Roadmaster", "Trucks", "12F", "¥74,000"), ("MCT Fly-Spy", "Drones", "4", "¥2,000")), "Name | Category | Avail | Cost", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildFilterToggleField("uiVehicleShowDrones", "Show Drones", true),
            BuildFilterToggleField("uiVehicleHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiVehicleShowOnlyAffordItems", "Show Only Items I Can Afford", false),
            BuildFilterToggleField("uiVehicleFreeItem", "Free Item", false),
            BuildFilterToggleField("uiVehicleBlackMarketDiscount", "Black Market Discount", false),
            BuildFilterToggleField("uiVehicleUsedVehicle", "Used Vehicle", false),
            new DesktopDialogField("uiVehicleUsedVehicleDiscount", "Used Vehicle Discount %", "25.00", "25.00", InputType: "number"),
            new DesktopDialogField("uiVehicleHandling", "Handling", "4", "4", InputType: "number"),
            new DesktopDialogField("uiVehicleCost", "Cost", "16000", "16000", IsReadOnly: true),
            new DesktopDialogField("uiVehicleSource", "Source", "Core Rulebook p. 465", "Core Rulebook p. 465", IsReadOnly: true),
            new DesktopDialogField("uiVehicleSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiVehicleSelectionTrail", selectionTrailPath, "Hyundai Shin-Hyung", "Used-vehicle and drone filters stay live"),
            BuildSelectionCommandsField("uiVehicleCategoryCommands", "Category Commands",
                "Group | Ground Vehicles",
                "Category | Cars",
                "Data File | Core Rulebook",
                "Move between chassis and drone branches without losing live filters",
                "Keep used-vehicle and availability visible while browsing"),
            new DesktopDialogField("uiVehicleFilterSummary", "Filter Summary", "Filtered Catalog | 5 shown / 8 total" + Environment.NewLine + "Category Path | Vehicles > Ground Vehicles > Cars" + Environment.NewLine + "Filters | vehicle/drone and availability stay live", "Filtered Catalog | 5 shown / 8 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiVehicleLiveRecalc", "Live Recalculation", "Selected Cost | ¥16,000" + Environment.NewLine + "Show Drones | Yes" + Environment.NewLine + "Availability Filter | On" + Environment.NewLine + "Add Again | Stays open", "Selected Cost | ¥16,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiVehicleResultCommands", "Result Commands",
                "Compare handling, armor, and source on the right before adding",
                "Use OK for one add or Add & More to keep the selector open",
                "Keep cost and used-vehicle settings visible through confirmation"),
            new DesktopDialogField("uiVehicleNotes", "Notes", "Vehicle stats, source, and vehicle/drone filters remain visible before the selection is confirmed.", "Vehicle stats, source, and vehicle/drone filters remain visible before the selection is confirmed.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildVehicleEditFields()
    {
        string details =
            "Selected | GMC Roadmaster" + Environment.NewLine +
            "Role | Truck" + Environment.NewLine +
            "Seats | 6" + Environment.NewLine +
            "Pilot | 1" + Environment.NewLine +
            "Source | Core Rulebook p. 466";
        string notes =
            "Keep the core vehicle stats visible while editing handling, speed, armor, and notes." + Environment.NewLine +
            "Use the vehicle tabs for weapon mounts and modifications after confirming the base chassis.";

        return
        [
            BuildSelectionSectionsField("uiVehicleEditSections"),
            new DesktopDialogField("uiVehicleEditContextTree", "Navigation", "[Vehicles]" + Environment.NewLine + "├─ Trucks" + Environment.NewLine + "└─ > GMC Roadmaster", "[Vehicles]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiVehicleEditNeighborList", "Current Garage", "Hyundai Shin-Hyung" + Environment.NewLine + "> GMC Roadmaster" + Environment.NewLine + "MCT Fly-Spy", "Hyundai Shin-Hyung", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiVehicleEditName", "Vehicle", "GMC Roadmaster", "GMC Roadmaster"),
            new DesktopDialogField("uiVehicleEditRole", "Role", "Truck", "Truck", IsReadOnly: true),
            new DesktopDialogField("uiVehicleEditHandling", "Handling", "3", "3", InputType: "number"),
            new DesktopDialogField("uiVehicleEditSpeed", "Speed", "4", "4", InputType: "number"),
            new DesktopDialogField("uiVehicleEditBody", "Body", "18", "18", InputType: "number"),
            new DesktopDialogField("uiVehicleEditArmor", "Armor", "16", "16", InputType: "number"),
            new DesktopDialogField("uiVehicleEditSource", "Source", "Core Rulebook p. 466", "Core Rulebook p. 466", IsReadOnly: true),
            new DesktopDialogField("uiVehicleEditDetails", "Vehicle Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiVehicleEditLiveSummary", "Live Summary", "Control Posture | manual + rigger ready" + Environment.NewLine + "Damage Soak | 34" + Environment.NewLine + "Seats | 6" + Environment.NewLine + "Posture | legacy edit utility", "Control Posture | manual + rigger ready", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiVehicleEditCommands", "Commands", "Adjust handling, speed, body, or armor while stats stay visible" + Environment.NewLine + "Keep garage list context visible" + Environment.NewLine + "Return to vehicle tabs for mounts and mods", "Adjust handling, speed, body, or armor while stats stay visible", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiVehicleEditNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildVehicleModSelectionFields()
    {
        string categoryTree =
            "[Vehicle Mods]" + Environment.NewLine +
            "├─ Body" + Environment.NewLine +
            "├─ Electronics" + Environment.NewLine +
            "├─ Powertrain" + Environment.NewLine +
            "└─ Weapon Mounts";
        string candidateList =
            "Spoof Chips · Electronics · ¥3,000" + Environment.NewLine +
            "GridLink Override · Electronics · ¥2,500" + Environment.NewLine +
            "Rigger Adaptation · Powertrain · ¥2,500";
        string selectionDetails = BuildGridValue(
            ("Selected", "Spoof Chips"),
            ("Slot", "Body"),
            ("Availability", "8"),
            ("Source", "Rigger 5.0 p. 159"));

        return
        [
            BuildSelectionSectionsField("uiVehicleModSections"),
            BuildSelectionTreeField("uiVehicleModCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiVehicleModSearch", "Search", string.Empty, "Search vehicle mods"),
            new DesktopDialogField("uiVehicleModName", "Modification", "Spoof Chips", "Spoof Chips"),
            new DesktopDialogField("uiVehicleModCandidateList", "Available Mods", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiVehicleModSlot", "Slot", "Body", "Body"),
            new DesktopDialogField("uiVehicleModSource", "Source", "Rigger 5.0 p. 159", "Rigger 5.0 p. 159", IsReadOnly: true),
            new DesktopDialogField("uiVehicleModSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiVehicleModNotes", "Notes", "Slot, availability, and source remain visible before confirmation.", "Slot, availability, and source remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildContactAddFields()
    {
        string details = BuildGridValue(
            ("Template", "Street Doc"),
            ("Archetype", "Medical"),
            ("Connection/Loyalty", "3 / 2"),
            ("Notes", "Can source restricted clinic time"));

        return
        [
            new DesktopDialogField("uiContactName", "Contact Name", "Dr. Mercy", "Dr. Mercy"),
            new DesktopDialogField("uiContactRole", "Role", "Street Doc", "Street Doc"),
            new DesktopDialogField("uiContactConnection", "Connection", "3", "3", InputType: "number"),
            new DesktopDialogField("uiContactLoyalty", "Loyalty", "2", "2", InputType: "number"),
            new DesktopDialogField("uiContactDetails", "Contact Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiContactNotes", "Notes", "Role, connection, loyalty, and summary stay visible while authoring the contact entry.", "Role, connection, loyalty, and summary stay visible while authoring the contact entry.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildContactEditFields()
    {
        string details = BuildGridValue(
            ("Selected Contact", "Mr. Johnson"),
            ("Role", "Fixer"),
            ("Connection/Loyalty", "5 / 3"),
            ("Notes", "Keeps premium jobs flowing"));

        return
        [
            new DesktopDialogField("uiContactEditName", "Name", "Mr. Johnson", "Mr. Johnson"),
            new DesktopDialogField("uiContactEditRole", "Role", "Fixer", "Fixer"),
            new DesktopDialogField("uiContactEditConnection", "Connection", "5", "5", InputType: "number"),
            new DesktopDialogField("uiContactEditLoyalty", "Loyalty", "3", "3", InputType: "number"),
            new DesktopDialogField("uiContactEditDetails", "Contact Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiContactEditNotes", "Notes", "Connection, loyalty, and contact role remain visible while editing.", "Connection, loyalty, and contact role remain visible while editing.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildContactConnectionFields()
    {
        string details =
            "Selected Contact | Mr. Johnson" + Environment.NewLine +
            "Role | Fixer" + Environment.NewLine +
            "Current Connection/Loyalty | 5 / 3";

        return
        [
            BuildUtilitySectionsField("uiContactConnectionSections", "Contact", "Details", "Notes"),
            new DesktopDialogField("uiContactConnectionName", "Contact", "Mr. Johnson", "Mr. Johnson", IsReadOnly: true),
            new DesktopDialogField("uiContactConnection", "Connection", "5", "5", InputType: "number"),
            new DesktopDialogField("uiContactLoyalty", "Loyalty", "3", "3", InputType: "number"),
            new DesktopDialogField("uiContactConnectionDetails", "Contact Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiContactConnectionNotes", "Notes", "Adjusting connection and loyalty keeps the selected contact summary visible.", "Adjusting connection and loyalty keeps the selected contact summary visible.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildQualitySelectionFields()
    {
        IReadOnlyList<DesktopDialogFieldOption> typeOptions = BuildSelectionCategoryOptions(
            "Show All",
            "Positive",
            "Negative",
            "Metatype");
        IReadOnlyList<DesktopDialogFieldOption> dataFileOptions = BuildSelectionDataFileOptions(
            "Core Rulebook",
            "Runner's Companion");
        string categoryTree =
            "[Qualities]" + Environment.NewLine +
            "├─ Positive" + Environment.NewLine +
            "├─ Negative" + Environment.NewLine +
            "├─ Metatype" + Environment.NewLine +
            "└─ Story";
        string candidateList =
            "First Impression · Positive · 11 Karma" + Environment.NewLine +
            "Allergy (Common, Mild) · Negative · -10 Karma" + Environment.NewLine +
            "Toughness · Positive · 9 Karma";
        string details = BuildGridValue(
            ("Selected", "First Impression"),
            ("Type", "Positive"),
            ("Karma", "11"),
            ("Source", "Core Rulebook p. 73"),
            ("Book", "Core Rulebook"));

        return
        [
            BuildSelectionSectionsField("uiQualitySections"),
            BuildSelectionTreeField("uiQualityCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiQualitySearch", "Search", string.Empty, "Search qualities"),
            new DesktopDialogField("uiQualityType", "Type", "Positive", "Positive", InputType: "select", Options: typeOptions),
            new DesktopDialogField("uiQualityBookFilter", "Data File", "Core Rulebook", "Core Rulebook", InputType: "select", Options: dataFileOptions),
            new DesktopDialogField("uiQualityName", "Quality", "First Impression", "First Impression"),
            new DesktopDialogField("uiQualityCandidateList", "Available Qualities", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiQualityMetagenicOnly", "Metagenic Only", false),
            BuildFilterToggleField("uiQualityShowNegative", "Show Negative", true),
            new DesktopDialogField("uiQualityKarma", "Karma", "11", "11", IsReadOnly: true),
            new DesktopDialogField("uiQualitySelectionDetails", "Selection Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiQualitySelectionTrail", BuildSelectionCategoryPath("Qualities", "Positive", "Positive", "First Impression"), "First Impression", "Add & More keeps the selector open"),
            new DesktopDialogField("uiQualityFilterSummary", "Filter Summary", "Filtered Catalog | 3 shown / 5 total" + Environment.NewLine + "Category Path | Qualities > Positive > Positive" + Environment.NewLine + "Negative Posture | included", "Filtered Catalog | 3 shown / 5 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            BuildSelectionCommandsField("uiQualityResultCommands", "Result Commands",
                "Karma, tag, and source stay visible on the right",
                "Use Add for one entry or Add & More to keep browsing",
                "Keep type and metagenic filters visible while confirming"),
            new DesktopDialogField("uiQualityNotes", "Notes", "Quality type, karma cost, source, and metagenic filters remain visible before confirmation.", "Quality type, karma cost, source, and metagenic filters remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildWeaponSelectionFields()
    {
        string categoryTree = BuildSelectionGroupedBranchTree(
            "Weapons",
            [
                ("Firearms", "Assault Rifles"),
                ("Firearms", "Heavy Pistols"),
                ("Melee", "Melee"),
                ("Firearms", "Shotguns")
            ],
            "Heavy Pistols");
        string candidateList =
            "Ares Alpha · Assault Rifle · ¥2,650" + Environment.NewLine +
            "Defiance T-250 · Shotgun · ¥450" + Environment.NewLine +
            "Colt M23 · Heavy Pistol · ¥750";
        string details = BuildGridValue(
            ("Selected", "Colt M23"),
            ("Damage", "7P"),
            ("AP", "-1"),
            ("Mode", "SA"),
            ("Source", "Core Rulebook p. 424"),
            ("Book", "Core Rulebook"));
        string selectionTrailPath = BuildSelectionCategoryPath("Weapons", "Firearms", "Heavy Pistols", "Colt M23");

        return
        [
            BuildSelectionSectionsField("uiWeaponSections"),
            new DesktopDialogField("uiWeaponViewModes", "View Modes", "List View" + Environment.NewLine + "Browse", "Browse", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            BuildSelectionTreeField("uiWeaponCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiWeaponSearch", "Search", string.Empty, "Search weapons"),
            new DesktopDialogField("uiWeaponCategory", "Category", "Show All", "Show All"),
            new DesktopDialogField("uiWeaponSelectedBranch", "Selected Branch", "Heavy Pistols", "Heavy Pistols", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            BuildFilterToggleField("uiWeaponSearchInCategoryOnly", "Search In Category Only", true),
            new DesktopDialogField("uiWeaponBookFilter", "Data File", "All Books", "All Books"),
            new DesktopDialogField("uiWeaponName", "Weapon", "Colt M23", "Colt M23"),
            new DesktopDialogField("uiWeaponCandidateList", "Available Weapons", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiWeaponBrowseGrid", "Catalog Grid", BuildSelectionBrowseGrid(("Ares Alpha", "Assault Rifles", "11F", "¥2,650"), ("Colt M23", "Heavy Pistols", "5R", "¥750"), ("Defiance T-250", "Shotguns", "4R", "¥450")), "Name | Category | Avail | Cost", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildFilterToggleField("uiWeaponHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiWeaponShowOnlyAffordItems", "Show Only Items I Can Afford", false),
            BuildFilterToggleField("uiWeaponBlackMarketDiscount", "Black Market Discount", false),
            BuildFilterToggleField("uiWeaponFreeItem", "Free Item", false),
            new DesktopDialogField("uiWeaponAccuracy", "Accuracy", "5", "5", IsReadOnly: true),
            new DesktopDialogField("uiWeaponMarkup", "Markup %", "0", "0", InputType: "number"),
            new DesktopDialogField("uiWeaponCost", "Cost", "750", "750", IsReadOnly: true),
            new DesktopDialogField("uiWeaponSource", "Source", "Core Rulebook p. 424", "Core Rulebook p. 424", IsReadOnly: true),
            new DesktopDialogField("uiWeaponSelectionDetails", "Selection Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiWeaponIncludedAccessories", "Included Accessories", "Smartgun System" + Environment.NewLine + "Top Rail Mount", "Smartgun System", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiWeaponSelectionTrail", selectionTrailPath, "Colt M23", "Add & More keeps the selector open"),
            BuildSelectionCommandsField("uiWeaponCategoryCommands", "Category Commands",
                "Group | Firearms",
                "Category | Heavy Pistols",
                "Data File | Core Rulebook",
                "Move between firearm branches without losing live filters",
                "Keep availability and discount visible while browsing"),
            new DesktopDialogField("uiWeaponFilterSummary", "Filter Summary", "Filtered Catalog | 7 shown / 10 total" + Environment.NewLine + "Category Path | Weapons > Firearms > Heavy Pistols" + Environment.NewLine + "Filters | availability, discounts, and source stay live", "Filtered Catalog | 7 shown / 10 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiWeaponLiveRecalc", "Live Recalculation", "Recalculated Cost | ¥750" + Environment.NewLine + "Accuracy | 5" + Environment.NewLine + "Black Market | No" + Environment.NewLine + "Add Again | Stays open", "Recalculated Cost | ¥750", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiWeaponResultCommands", "Result Commands",
                "Compare damage, AP, and source on the right before adding",
                "Use OK for one add or Add & More to keep the selector open",
                "Keep markup and legality visible through confirmation"),
            new DesktopDialogField("uiWeaponNotes", "Notes", "Damage, AP, firing mode, source, and pricing filters remain visible before confirmation.", "Damage, AP, firing mode, source, and pricing filters remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildArmorSelectionFields()
    {
        string categoryTree = BuildSelectionGroupedBranchTree(
            "Armor",
            [
                ("Protective Wear", "Armor"),
                ("Protective Wear", "Clothing"),
                ("Protective Accessories", "PPP"),
                ("Protective Accessories", "Shields")
            ],
            "Armor");
        string candidateList =
            "Armor Jacket · Armor 12 · ¥1000" + Environment.NewLine +
            "Actioneer Business Clothes · Armor 8 · ¥1500" + Environment.NewLine +
            "PPP System · Armor +1 · ¥250";
        string details = BuildGridValue(
            ("Selected", "Armor Jacket"),
            ("Armor", "12"),
            ("Availability", "12"),
            ("Capacity", "n/a"),
            ("Source", "Core Rulebook p. 436"),
            ("Book", "Core Rulebook"));
        string selectionTrailPath = BuildSelectionCategoryPath("Armor", "Protective Wear", "Armor", "Armor Jacket");

        return
        [
            BuildSelectionSectionsField("uiArmorSections"),
            new DesktopDialogField("uiArmorViewModes", "View Modes", "List View" + Environment.NewLine + "Browse", "Browse", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            BuildSelectionTreeField("uiArmorCategoryTree", "Categories", categoryTree),
            new DesktopDialogField("uiArmorSearch", "Search", string.Empty, "Search armor"),
            new DesktopDialogField("uiArmorCategory", "Category", "Show All", "Show All"),
            new DesktopDialogField("uiArmorSelectedBranch", "Selected Branch", "Armor", "Armor", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            BuildFilterToggleField("uiArmorSearchInCategoryOnly", "Search In Category Only", true),
            new DesktopDialogField("uiArmorBookFilter", "Data File", "All Books", "All Books"),
            new DesktopDialogField("uiArmorName", "Armor", "Armor Jacket", "Armor Jacket"),
            new DesktopDialogField("uiArmorCandidateList", "Available Armor", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiArmorBrowseGrid", "Catalog Grid", BuildSelectionBrowseGrid(("Armor Jacket", "Armor", "12", "¥1,000"), ("Actioneer Business Clothes", "Clothing", "10", "¥1,500"), ("Ballistic Shield", "Shields", "8", "¥900")), "Name | Category | Avail | Cost", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildFilterToggleField("uiArmorHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiArmorShowOnlyAffordItems", "Show Only Items I Can Afford", false),
            BuildFilterToggleField("uiArmorBlackMarketDiscount", "Black Market Discount", false),
            BuildFilterToggleField("uiArmorFreeItem", "Free Item", false),
            new DesktopDialogField("uiArmorRating", "Armor", "12", "12", IsReadOnly: true),
            new DesktopDialogField("uiArmorMarkup", "Markup %", "0", "0", InputType: "number"),
            new DesktopDialogField("uiArmorCost", "Cost", "1000", "1000", IsReadOnly: true),
            new DesktopDialogField("uiArmorSource", "Source", "Core Rulebook p. 436", "Core Rulebook p. 436", IsReadOnly: true),
            new DesktopDialogField("uiArmorSelectionDetails", "Selection Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiArmorSelectionTrail", selectionTrailPath, "Armor Jacket", "Source and markup stay visible through confirmation"),
            BuildSelectionCommandsField("uiArmorCategoryCommands", "Category Commands",
                "Group | Protective Wear",
                "Category | Armor",
                "Data File | Core Rulebook",
                "Move between armor branches without losing live filters",
                "Keep availability and free-item settings visible while browsing"),
            new DesktopDialogField("uiArmorFilterSummary", "Filter Summary", "Filtered Catalog | 5 shown / 7 total" + Environment.NewLine + "Category Path | Armor > Protective Wear > Armor" + Environment.NewLine + "Filters | availability, source, and markup stay live", "Filtered Catalog | 5 shown / 7 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiArmorLiveRecalc", "Live Recalculation", "Recalculated Cost | ¥1,000" + Environment.NewLine + "Armor | 12" + Environment.NewLine + "Free Item | No" + Environment.NewLine + "Add Again | Stays open", "Recalculated Cost | ¥1,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiArmorResultCommands", "Result Commands",
                "Compare armor, legality, and source on the right before adding",
                "Use OK for one add or Add & More to keep browsing",
                "Keep markup and capacity visible through confirmation"),
            new DesktopDialogField("uiArmorNotes", "Notes", "Armor rating, legality, source, and pricing filters remain visible before confirmation.", "Armor rating, legality, source, and pricing filters remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    public DesktopDialogState CreateUiControlDialog(
        string controlId,
        DesktopPreferenceState preferences)
    {
        if (!LegacyUiControlCatalog.IsKnown(controlId))
        {
            return HumanizeVisibleDialog(CreateGenericUiControlDialog(controlId));
        }

        return HumanizeVisibleDialog(controlId switch
        {
            "create_entry" => new DesktopDialogState(
                "dialog.ui.create_entry",
                "Add Entry",
                "Add a new entry while keeping the compact list/detail editor visible.",
                BuildEntryEditorFields("New entry", false),
                BuildAddAndMoreActions("Add")),
            "edit_entry" => new DesktopDialogState(
                "dialog.ui.edit_entry",
                "Edit Entry",
                "Edit the selected entry in the same compact list/detail editor.",
                BuildEntryEditorFields("Current Entry", true),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "delete_entry" => new DesktopDialogState(
                "dialog.ui.delete_entry",
                "Remove Current Entry",
                "Remove Current Entry from the active list?",
                BuildEntryDeleteFields(),
                [
                    new DesktopDialogAction("delete", "Remove Current Entry", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "open_notes" => new DesktopDialogState(
                "dialog.ui.open_notes",
                "Edit Notes",
                "Edit dossier notes in a compact text utility pane.",
                BuildNotesEditorFields(preferences.CharacterNotes),
                [
                    new DesktopDialogAction("save", "Save", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "move_up" => new DesktopDialogState(
                "dialog.ui.move_up",
                "Move Entry Up",
                "The reordered list stays visible in the same utility pane.",
                BuildActionReceiptFields("Move Up", "Selected entry moved one position higher in the current ordered list.", "Ordering stays compact and list-oriented like the legacy utility flows."),
                [new DesktopDialogAction("close", "Close", true)]),
            "move_down" => new DesktopDialogState(
                "dialog.ui.move_down",
                "Move Entry Down",
                "The reordered list stays visible in the same utility pane.",
                BuildActionReceiptFields("Move Down", "Selected entry moved one position lower in the current ordered list.", "Ordering stays compact and list-oriented like the legacy utility flows."),
                [new DesktopDialogAction("close", "Close", true)]),
            "toggle_free_paid" => new DesktopDialogState(
                "dialog.ui.toggle_free_paid",
                "Pricing status",
                "The new pricing state stays visible in the same utility pane.",
                BuildActionReceiptFields("Toggle Free/Paid", "Selected item pricing was toggled between free and paid.", "Pricing changes remain compact and explicit instead of disappearing into background chrome."),
                [new DesktopDialogAction("close", "Close", true)]),
            "show_source" => new DesktopDialogState(
                "dialog.ui.show_source",
                "Source",
                "Source book, page, and reference stay visible in the same compact utility rhythm as classic Chummer.",
                BuildSourceDetailsFields(),
                [new DesktopDialogAction("close", "Close", true)]),
            "gear_add" => RebuildGearSelectionDialog(
                new DesktopDialogState(
                    "dialog.ui.gear_add",
                    "Add Gear",
                    "Browse the catalog, inspect source and cost, then confirm the selected gear item.",
                    BuildGearSelectionFields(),
                    BuildLegacySelectionActions())),
            "gear_edit" => new DesktopDialogState(
                "dialog.ui.gear_edit",
                "Edit Gear",
                "Edit the selected gear item with the same browse/detail rhythm used by classic Chummer utility forms.",
                BuildGearEditFields(),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "gear_delete" => new DesktopDialogState(
                "dialog.ui.gear_delete",
                "Remove Armor Jacket",
                "Remove Armor Jacket from the current gear list?",
                BuildGearDeleteFields(),
                [
                    new DesktopDialogAction("delete", "Remove Armor Jacket", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "runner_benchmark" => new DesktopDialogState(
                "dialog.ui.runner_benchmark",
                "Runner Intelligence",
                "Compare this dossier against privacy-safe cohorts and local roster benchmarks before changing the sheet.",
                [
                    BuildUtilitySectionsField("uiRunnerBenchmarkSections", "Edge", "Exposure", "Delta"),
                    new DesktopDialogField("uiRunnerBenchmarkInitiative", "Initiative Percentile", "Top 3% of comparable runners", "Top 3%", IsReadOnly: true),
                    new DesktopDialogField("uiRunnerBenchmarkDefense", "Defense Pool", "Top 14% for street samurai cohort", "Top 14%", IsReadOnly: true),
                    new DesktopDialogField("uiRunnerBenchmarkSoak", "Soak Pool", "Above campaign median", "Above median", IsReadOnly: true)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "runner_what_if" => new DesktopDialogState(
                "dialog.ui.runner_what_if",
                "Runner Intelligence What-If",
                "Model spells, drugs, gear, and sustained effects without mutating the active dossier until the user applies a real workflow.",
                [
                    BuildUtilitySectionsField("uiRunnerWhatIfSections", "Spell", "Inventory", "Risk"),
                    new DesktopDialogField("uiRunnerWhatIfSpell", "Spell", "Increase Initiative Force 6", "Increase Initiative Force 6"),
                    new DesktopDialogField("uiRunnerWhatIfDrain", "Drain/Stun Risk", "87% chance of taking no more than 1 Stun", "87% <= 1 Stun", IsReadOnly: true),
                    new DesktopDialogField("uiRunnerWhatIfInventory", "Inventory Synergy", "Jazz from inventory can raise Initiative percentile with addiction/crash warning", "Jazz available", IsReadOnly: true)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "runner_cohort_privacy" => new DesktopDialogState(
                "dialog.ui.runner_cohort_privacy",
                "Runner Intelligence Privacy",
                "Opt-in anonymized benchmark cohorts stay separate from private dossier, owner, dossier id, XML, notes, and dossier content.",
                [
                    BuildUtilitySectionsField("uiRunnerPrivacySections", "Hosted", "Self-Host", "Excluded"),
                    new DesktopDialogField("uiRunnerPrivacyHosted", "Hosted Cohorts", "Opt-in anonymized benchmark cohorts", "Opt-in only", IsReadOnly: true),
                    new DesktopDialogField("uiRunnerPrivacyDocker", "Docker Self-Host", "Local roster and campaign benchmark pool only by default", "Local-only default", IsReadOnly: true),
                    new DesktopDialogField("uiRunnerPrivacyExcluded", "Excluded Data", "Dossier names, aliases, owner identifiers, dossier identifiers, files, XML, notes, and dossier text", "Private dossier data excluded", IsReadOnly: true)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "gear_mount" => new DesktopDialogState(
                "dialog.ui.gear_mount",
                "Mount Gear",
                "Select the host and keep the mountable gear summary visible before applying the change.",
                [
                    BuildUtilitySectionsField("uiGearMountSections", "Mount", "Details", "Notes"),
                    new DesktopDialogField("uiGearMountTarget", "Selected Gear", "Smartgun System", "Smartgun System", IsReadOnly: true),
                    new DesktopDialogField("uiGearMountHost", "Host", "Ares Predator V", "Ares Predator V"),
                    new DesktopDialogField("uiGearMountDetails", "Mount Details", "Selected Gear | Smartgun System" + Environment.NewLine + "Target Host | Ares Predator V" + Environment.NewLine + "Compatibility | Valid" + Environment.NewLine + "Source | Core Rulebook p. 433", "Selected Gear | Smartgun System" + Environment.NewLine + "Target Host | Ares Predator V" + Environment.NewLine + "Compatibility | Valid" + Environment.NewLine + "Source | Core Rulebook p. 433", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiGearMountNotes", "Notes", "Keep compatibility and source visible while mounting the selected gear.", "Keep compatibility and source visible while mounting the selected gear.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [
                    new DesktopDialogAction("apply", "Mount", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "gear_source" => new DesktopDialogState(
                "dialog.ui.gear_source",
                "Gear Source",
                "Source references stay visible in a compact utility pane.",
                BuildSourceDetailsFields(),
                [new DesktopDialogAction("close", "Close", true)]),
            "cyberware_add" => RebuildCyberwareSelectionDialog(
                new DesktopDialogState(
                    "dialog.ui.cyberware_add",
                    "Add Cyberware",
                    "Search, filter, keep source/cost/essence details visible, and confirm the selected implant.",
                    BuildCyberwareSelectionFields(),
                    BuildLegacySelectionActions())),
            "cyberware_edit" => new DesktopDialogState(
                "dialog.ui.cyberware_edit",
                "Edit Cyberware",
                "Edit the selected implant while keeping source, cost, essence, and notes visible.",
                BuildCyberwareEditFields(),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "cyberware_delete" => new DesktopDialogState(
                "dialog.ui.cyberware_delete",
                "Remove Cybereyes Rating 4",
                "Remove Cybereyes Rating 4 from installed ware?",
                BuildCyberwareDeleteFields(),
                [
                    new DesktopDialogAction("delete", "Remove Cybereyes Rating 4", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "drug_add" => new DesktopDialogState(
                "dialog.ui.drug_add",
                "Add Drug",
                "Browse drugs, inspect speed and crash state, then confirm the selected dose.",
                BuildDrugSelectionFields(),
                BuildSelectionConfirmationActions()),
            "drug_delete" => new DesktopDialogState(
                "dialog.ui.drug_delete",
                "Remove Jazz",
                "Remove Jazz from the current drug ledger?",
                BuildDrugDeleteFields(),
                [
                    new DesktopDialogAction("delete", "Remove Jazz", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "magic_add" => new DesktopDialogState(
                "dialog.ui.magic_add",
                "Add Spell/Power",
                "Choose the magical entry, keep category and drain visible, then confirm the selection.",
                BuildMagicSelectionFields(),
                BuildSelectionConfirmationActions()),
            "magic_delete" => new DesktopDialogState(
                "dialog.ui.magic_delete",
                "Remove Stunbolt",
                "Remove Stunbolt from the current magic list?",
                BuildMagicDeleteFields(),
                [
                    new DesktopDialogAction("delete", "Remove Stunbolt", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "magic_bind" => new DesktopDialogState(
                "dialog.ui.magic_bind",
                "Bind/Link",
                "Selected magical item stays visible before applying the bind/link action.",
                [
                    BuildUtilitySectionsField("uiMagicBindSections", "Binding", "Details", "Notes"),
                    new DesktopDialogField("uiMagicBindTarget", "Selected Entry", "Force 4 Focus", "Force 4 Focus", IsReadOnly: true),
                    new DesktopDialogField("uiMagicBindCost", "Binding Cost", "16", "16", IsReadOnly: true),
                    new DesktopDialogField("uiMagicBindDetails", "Bind Details", "Selected Entry | Force 4 Focus" + Environment.NewLine + "Binding Cost | 16 Karma" + Environment.NewLine + "Availability | Bound magical item" + Environment.NewLine + "Source | Core Rulebook p. 319", "Selected Entry | Force 4 Focus" + Environment.NewLine + "Binding Cost | 16 Karma" + Environment.NewLine + "Availability | Bound magical item" + Environment.NewLine + "Source | Core Rulebook p. 319", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiMagicBindNotes", "Notes", "Binding cost and source remain visible before confirmation.", "Binding cost and source remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [
                    new DesktopDialogAction("apply", "Bind", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "magic_source" => new DesktopDialogState(
                "dialog.ui.magic_source",
                "Magic Source",
                "Magical source references stay visible in a compact utility pane.",
                BuildSourceDetailsFields(),
                [new DesktopDialogAction("close", "Close", true)]),
            "spell_add" => new DesktopDialogState(
                "dialog.ui.spell_add",
                "Add Spell",
                "Search the spell list, inspect source and drain, then confirm the learned spell.",
                BuildSpellSelectionFields(),
                BuildSelectionConfirmationActions()),
            "adept_power_add" => new DesktopDialogState(
                "dialog.ui.adept_power_add",
                "Add Adept Power",
                "Search available adept powers, inspect PP cost and source, then confirm the selected power.",
                BuildAdeptPowerSelectionFields(),
                BuildSelectionConfirmationActions()),
            "complex_form_add" => new DesktopDialogState(
                "dialog.ui.complex_form_add",
                "Add Complex Form",
                "Browse complex forms, inspect target and source, then confirm the selected form.",
                BuildComplexFormSelectionFields(),
                BuildSelectionConfirmationActions()),
            "initiation_add" => new DesktopDialogState(
                "dialog.ui.initiation_add",
                "Add Initiation / Submersion",
                "Choose the reward, keep grade and track visible, then confirm the initiation or submersion step.",
                BuildInitiationSelectionFields(),
                BuildSelectionConfirmationActions()),
            "spirit_add" => new DesktopDialogState(
                "dialog.ui.spirit_add",
                "Add Spirit / Ally / Familiar",
                "Browse spirits and allies, inspect force and type, then confirm the selected entry.",
                BuildSpiritSelectionFields(),
                BuildSelectionConfirmationActions()),
            "sprite_add" => new DesktopDialogState(
                "dialog.ui.sprite_add",
                "Add Sprite",
                "Browse sprites, inspect level and type, then confirm the selected entry.",
                BuildSpriteSelectionFields(),
                BuildSelectionConfirmationActions()),
            "critter_power_add" => new DesktopDialogState(
                "dialog.ui.critter_power_add",
                "Add Critter Power",
                "Browse critter powers, inspect type and source, then confirm the selected power.",
                BuildCritterPowerSelectionFields(),
                BuildSelectionConfirmationActions()),
            "matrix_program_add" => new DesktopDialogState(
                "dialog.ui.matrix_program_add",
                "Add Program / Cyberdeck Item",
                "Browse matrix programs and cyberdeck items, inspect slot and source, then confirm the selected entry.",
                BuildMatrixProgramSelectionFields(),
                BuildSelectionConfirmationActions()),
            "skill_add" => RebuildSkillSelectionDialog(
                new DesktopDialogState(
                    "dialog.ui.skill_add",
                    "Add Skill",
                    "Browse skills, inspect category and linked attribute, then confirm the selected skill.",
                    BuildSkillSelectionFields(),
                    BuildLegacySelectionActions())),
            "skill_specialize" => new DesktopDialogState(
                "dialog.ui.skill_specialize",
                "Specialize Skill",
                "Choose the specialization while keeping the selected skill summary visible.",
                BuildSkillSpecializationFields(),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "skill_remove" => new DesktopDialogState(
                "dialog.ui.skill_remove",
                "Remove Perception",
                "Remove Perception from the current skill list?",
                BuildSkillRemoveFields(),
                [
                    new DesktopDialogAction("delete", "Remove Perception", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "skill_group" => new DesktopDialogState(
                "dialog.ui.skill_group",
                "Skill Group",
                "Skill group and ratings stay visible before assigning or breaking the group.",
                [
                    BuildUtilitySectionsField("uiSkillGroupSections", "Group", "Details", "Notes"),
                    new DesktopDialogField("uiSkillGroupName", "Group", "Stealth", "Stealth", IsReadOnly: true),
                    new DesktopDialogField("uiSkillGroupRating", "Rating", "4", "4", InputType: "number"),
                    new DesktopDialogField("uiSkillGroupDetails", "Group Details", "Group | Stealth" + Environment.NewLine + "Skills | Disguise, Palming, Sneaking" + Environment.NewLine + "Current Rating | 4", "Group | Stealth" + Environment.NewLine + "Skills | Disguise, Palming, Sneaking" + Environment.NewLine + "Current Rating | 4", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiSkillGroupNotes", "Notes", "Group composition and current rating remain visible while editing.", "Group composition and current rating remain visible while editing.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "combat_add_weapon" => RebuildWeaponSelectionDialog(
                new DesktopDialogState(
                    "dialog.ui.combat_add_weapon",
                    "Add Weapon",
                    "Browse weapons, inspect combat stats and source, then confirm the selected weapon.",
                    BuildWeaponSelectionFields(),
                    BuildLegacySelectionActions())),
            "combat_add_armor" => RebuildArmorSelectionDialog(
                new DesktopDialogState(
                    "dialog.ui.combat_add_armor",
                    "Add Armor",
                    "Browse armor, inspect protection values and source, then confirm the selected armor.",
                    BuildArmorSelectionFields(),
                    BuildLegacySelectionActions())),
            "combat_reload" => new DesktopDialogState(
                "dialog.ui.combat_reload",
                "Reload Weapon",
                "Weapon and ammo state stays visible before applying the reload.",
                [
                    BuildUtilitySectionsField("uiCombatReloadSections", "Weapon", "Details", "Notes"),
                    new DesktopDialogField("uiCombatReloadWeapon", "Weapon", "Colt M23", "Colt M23", IsReadOnly: true),
                    new DesktopDialogField("uiCombatReloadAmmo", "Ammo", "Regular Ammo (15)", "Regular Ammo (15)"),
                    new DesktopDialogField("uiCombatReloadDetails", "Reload Details", "Selected Weapon | Colt M23" + Environment.NewLine + "Current Magazine | 3 / 15" + Environment.NewLine + "Selected Ammo | Regular Ammo (15)", "Selected Weapon | Colt M23" + Environment.NewLine + "Current Magazine | 3 / 15" + Environment.NewLine + "Selected Ammo | Regular Ammo (15)", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiCombatReloadCommands", "Commands", "Reload selected weapon" + Environment.NewLine + "Keep ammo and magazine visible" + Environment.NewLine + "Return to combat tab after applying", "Reload selected weapon", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiCombatReloadNotes", "Notes", "Weapon and ammo selection remain visible while reloading.", "Weapon and ammo selection remain visible while reloading.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [
                    new DesktopDialogAction("apply", "Reload", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "combat_damage_track" => new DesktopDialogState(
                "dialog.ui.combat_damage_track",
                "Damage Track",
                "Current physical and stun tracks stay visible before applying the change.",
                [
                    BuildUtilitySectionsField("uiDamageTrackSections", "Tracks", "Details", "Notes"),
                    new DesktopDialogField("uiDamageTrackPhysical", "Physical", "3 / 10", "3 / 10", IsReadOnly: true),
                    new DesktopDialogField("uiDamageTrackStun", "Stun", "1 / 10", "1 / 10", IsReadOnly: true),
                    new DesktopDialogField("uiDamageTrackDetails", "Track Details", "Physical | 3 / 10" + Environment.NewLine + "Stun | 1 / 10" + Environment.NewLine + "Penalty | none", "Physical | 3 / 10" + Environment.NewLine + "Stun | 1 / 10" + Environment.NewLine + "Penalty | none", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiDamageTrackCommands", "Commands", "Apply current damage step" + Environment.NewLine + "Keep penalty visible" + Environment.NewLine + "Return to combat tab after applying", "Apply current damage step", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiDamageTrackNotes", "Notes", "Current track state remains visible before applying the damage step.", "Current track state remains visible before applying the damage step.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "vehicle_add" => RebuildVehicleSelectionDialog(
                new DesktopDialogState(
                    "dialog.ui.vehicle_add",
                    "Add Vehicle / Drone",
                    "Browse vehicles and drones, inspect stats and source, then confirm the selected entry.",
                    BuildVehicleSelectionFields(),
                    BuildLegacySelectionActions())),
            "vehicle_edit" => new DesktopDialogState(
                "dialog.ui.vehicle_edit",
                "Edit Vehicle / Drone",
                "Edit the selected vehicle or drone while keeping stats, source, and notes visible.",
                BuildVehicleEditFields(),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "vehicle_delete" => new DesktopDialogState(
                "dialog.ui.vehicle_delete",
                "Remove GMC Roadmaster",
                "Remove GMC Roadmaster from the current garage?",
                BuildVehicleDeleteFields(),
                [
                    new DesktopDialogAction("delete", "Remove GMC Roadmaster", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "vehicle_mod_add" => new DesktopDialogState(
                "dialog.ui.vehicle_mod_add",
                "Add Vehicle Mod",
                "Browse modifications, inspect slot, availability, and source, then confirm the selected mod.",
                BuildVehicleModSelectionFields(),
                BuildSelectionConfirmationActions()),
            "contact_add" => new DesktopDialogState(
                "dialog.ui.contact_add",
                "Add Contact",
                "Author the contact with the same dense detail layout used by classic Chummer utility forms.",
                BuildContactAddFields(),
                BuildAddAndMoreActions()),
            "contact_edit" => new DesktopDialogState(
                "dialog.ui.contact_edit",
                "Edit Contact",
                "Edit the selected contact while keeping role and connection visible.",
                BuildContactEditFields(),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "contact_remove" => new DesktopDialogState(
                "dialog.ui.contact_remove",
                "Remove Mr. Johnson",
                "Remove Mr. Johnson from the current contact roster?",
                BuildContactRemoveFields(),
                [
                    new DesktopDialogAction("delete", "Remove Mr. Johnson", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "contact_connection" => new DesktopDialogState(
                "dialog.ui.contact_connection",
                "Connection / Loyalty",
                "Adjust the selected contact while keeping the contact summary visible.",
                BuildContactConnectionFields(),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "quality_add" => RebuildQualitySelectionDialog(
                new DesktopDialogState(
                    "dialog.ui.quality_add",
                    "Add Quality",
                    "Browse qualities, inspect karma cost and source, then confirm the selected quality.",
                    BuildQualitySelectionFields(),
                    BuildSelectionConfirmationActions())),
            "quality_delete" => new DesktopDialogState(
                "dialog.ui.quality_delete",
                "Remove First Impression",
                "Remove First Impression from the current quality list?",
                BuildQualityDeleteFields(),
                [
                    new DesktopDialogAction("delete", "Remove First Impression", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "identity_license_add" => new DesktopDialogState(
                "dialog.ui.identity_license_add",
                "Add SIN / License",
                "Create a browser-safe identity, SIN, or license record while keeping rating, source, and legal status visible.",
                BuildIdentityLicenseAddFields(),
                [
                    new DesktopDialogAction("add", "Add SIN / License", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "identity_license_edit" => new DesktopDialogState(
                "dialog.ui.identity_license_edit",
                "Edit SIN / License",
                "Review the selected identity record while keeping attached licenses and source status visible.",
                BuildIdentityLicenseEditFields(),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "identity_license_delete" => new DesktopDialogState(
                "dialog.ui.identity_license_delete",
                "Remove SIN / License",
                "Remove the selected identity record only after the attached license and recovery context stays visible.",
                BuildIdentityLicenseDeleteFields(),
                [
                    new DesktopDialogAction("delete", "Remove SIN / License", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            _ => throw new InvalidOperationException($"Known legacy UI control '{controlId}' is missing a dedicated dialog mapping.")
        });
    }

    private static IReadOnlyList<DesktopDialogField> BuildIdentityLicenseAddFields()
        => [
            new DesktopDialogField("uiIdentityRecordType", "Record Type", "Fake SIN", "Fake SIN", LayoutSlot: DesktopDialogFieldLayoutSlots.Left, Options: [
                new DesktopDialogFieldOption("fake_sin", "Fake SIN"),
                new DesktopDialogFieldOption("license", "License"),
                new DesktopDialogFieldOption("lifestyle_identity", "Lifestyle Identity")
            ]),
            new DesktopDialogField("uiIdentityName", "Identity / License Name", "Taylor Mercer", "Taylor Mercer", LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiIdentityRating", "Rating", "4", "4", LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiIdentityAttachment", "Attached License", "Concealed Carry Permit", "Concealed Carry Permit", LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiIdentityCost", "Cost", "¥10,000", "¥10,000", LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiIdentitySource", "Source", "Core Rulebook p. 367", "Core Rulebook p. 367", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiIdentityPosture", "Legal status", BuildGridValue(("SIN", "rating-bound"), ("License", "attached"), ("Lifestyle", "optional cover"), ("Browser save", "explicit result continuation")), "SIN | rating-bound", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiIdentityNotes", "Notes", "Identity, fake SIN, attached license, and lifestyle-cover context stay in one compact utility pane so the browser route mirrors the desktop side workflow.", "Identity, fake SIN, attached license, and lifestyle-cover context stay in one compact utility pane so the browser route mirrors the desktop side workflow.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];

    private static IReadOnlyList<DesktopDialogField> BuildIdentityLicenseEditFields()
        => [
            new DesktopDialogField("uiIdentityCurrentList", "Current Identity Records", "Taylor Mercer - Fake SIN R4" + Environment.NewLine + "> Concealed Carry Permit R4" + Environment.NewLine + "Middle Lifestyle cover", "Taylor Mercer - Fake SIN R4", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiIdentitySelected", "Selected Record", "Concealed Carry Permit", "Concealed Carry Permit", LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiIdentitySelectedRating", "Rating", "4", "4", LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiIdentitySelectedSource", "Source", "Core Rulebook p. 367", "Core Rulebook p. 367", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiIdentityAttachedContext", "Attached Context", BuildGridValue(("Parent SIN", "Taylor Mercer R4"), ("Lifestyle", "Middle cover"), ("Linked gear", "commlink + permits"), ("Warnings", "rating mismatch check pending")), "Parent SIN | Taylor Mercer R4", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiIdentityEditNotes", "Notes", "Editing keeps the identity stack, attached license, lifestyle cover, and source reference visible before applying browser-side changes.", "Editing keeps the identity stack, attached license, lifestyle cover, and source reference visible before applying browser-side changes.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];

    private static IReadOnlyList<DesktopDialogField> BuildIdentityLicenseDeleteFields()
        => [
            new DesktopDialogField("uiIdentityDeleteList", "Current Identity Records", "Taylor Mercer - Fake SIN R4" + Environment.NewLine + "> Concealed Carry Permit R4" + Environment.NewLine + "Middle Lifestyle cover", "Taylor Mercer - Fake SIN R4", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiIdentityDeleteTarget", "Selected Record", "Concealed Carry Permit", "Concealed Carry Permit", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiIdentityDeleteImpact", "Removal Impact", BuildGridValue(("Attached SIN", "kept"), ("License", "removed"), ("Lifestyle", "unchanged"), ("Recovery", "re-add from identity utility")), "Attached SIN | kept", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiIdentityDeleteRecovery", "Recovery", "Return to profile" + Environment.NewLine + "Re-open Add SIN / License" + Environment.NewLine + "Review lifestyle cover", "Return to profile", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiIdentityDeleteNotes", "Notes", "Delete posture is explicit because identity records affect legality, lifestyle cover, and attached permits in the desktop workflow.", "Delete posture is explicit because identity records affect legality, lifestyle cover, and attached permits in the desktop workflow.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];

    private static DesktopDialogState CreateGenericUiControlDialog(string controlId)
    {
        return new DesktopDialogState(
            "dialog.ui.generic",
            "Desktop Control",
            $"Desktop control '{controlId}' triggered.",
            BuildActionReceiptFields("Desktop Control", $"Triggered control: {controlId}", "This control does not yet have a dedicated legacy-shaped utility form."),
            [new DesktopDialogAction("close", "Close", true)]);
    }

    private static IReadOnlyList<DesktopDialogField> BuildTranslatorFields(
        string language,
        MasterIndexResponse? masterIndex,
        TranslatorLanguagesResponse? translatorLanguages)
    {
        List<DesktopDialogField> fields =
        [
            new DesktopDialogField(
                "translatorRouteTitle",
                "Translator",
                "Translator",
                "Translator",
                IsReadOnly: true),
            new DesktopDialogField(
                "translatorSearch",
                "Language Search",
                string.Empty,
                DesktopLocalizationCatalog.GetRequiredString("desktop.dialog.translator.field.search_placeholder", language)),
            new DesktopDialogField(
                "translatorLanePosture",
                "Translator Lane",
                NormalizeGoverned(masterIndex?.TranslatorLanePosture),
                "governed",
                IsReadOnly: true),
            new DesktopDialogField(
                "translatorBridgePosture",
                "Translator Bridge",
                NormalizeGoverned(masterIndex?.TranslatorBridgePosture ?? translatorLanguages?.TranslatorBridgePosture),
                "governed",
                IsReadOnly: true),
            new DesktopDialogField(
                "translatorOverlayCount",
                "Enabled Language Overlays",
                (masterIndex?.EnabledLanguageOverlayCount ?? translatorLanguages?.EnabledLanguageOverlayCount ?? 0).ToString(),
                "0",
                IsReadOnly: true)
        ];

        IReadOnlyList<TranslatorLanguageEntry> languages = translatorLanguages?.Languages is { Count: > 0 }
            ? translatorLanguages.Languages
            : DesktopLocalizationCatalog.ShippingLanguages
                .Select(shippingLanguage => new TranslatorLanguageEntry(shippingLanguage.Code, shippingLanguage.Label))
                .ToArray();
        int index = 1;
        foreach (TranslatorLanguageEntry availableLanguage in languages)
        {
            fields.Add(new DesktopDialogField(
                $"lang{index}",
                availableLanguage.Name,
                availableLanguage.Code,
                availableLanguage.Code,
                IsReadOnly: true));
            index++;
        }

        return fields;
    }

    private static string NormalizeGoverned(string? value)
        => string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "missing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "stale", StringComparison.OrdinalIgnoreCase)
            ? "governed"
            : value;

    private static IReadOnlyList<DesktopDialogField> BuildMasterIndexFields(MasterIndexResponse? masterIndex)
    {
        string dataRoot = ResolveMasterIndexDataRoot(masterIndex);
        if (masterIndex is null)
        {
            List<DesktopDialogField> emptyStateFields =
            [
                new DesktopDialogField("masterIndexFileSelection", "Data File", "All", "All", InputType: "select", Options: [new DesktopDialogFieldOption("All", "All data files")], LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
                new DesktopDialogField("masterIndexSearch", "Search", string.Empty, "Search index", LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField(
                    "masterIndexActiveResultKey",
                    "Entries",
                    string.Empty,
                    "No indexed entries discovered.",
                    IsReadOnly: true,
                    InputType: "select",
                    VisualKind: DesktopDialogFieldVisualKinds.List,
                    LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                    Options: [new DesktopDialogFieldOption(string.Empty, "No indexed entries discovered.")]),
                new DesktopDialogField("masterIndexSnippetPreview", "Notes", string.Empty, string.Empty, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField("masterIndexCurrentSourcebook", "Source", string.Empty, string.Empty, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField("masterIndexSelectedSource", "Linked PDF / URL", string.Empty, string.Empty, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField("masterIndexDataRoot", "Data Root", dataRoot, dataRoot, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Full),
                new DesktopDialogField("masterIndexCurrentFile", "Current Data File", "All data files", "All data files", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexSnapshot", "Snapshot", string.Empty, string.Empty, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexActiveSourcebookId", "Active Sourcebook", string.Empty, string.Empty, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexActiveFile", "Active File", "All", "All", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexCustomDataAuthoringReceipt", "Custom Data Authoring", "missing", "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexImportOracleReceipt", "Import Oracle", "missing", "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexAdjacentSr6OracleLane", "Adjacent SR6 Oracle", "missing", "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexOnlineStorageLane", "Online Storage Lane", "missing", "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexOnlineStorageCoverage", "Online Storage Coverage", "0/2 · 0%", "0/2 · 0%", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexOnlineStorageReceipt", "Online Storage Receipt", "missing", "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexSr6SupplementLane", "SR6 Supplements", "missing", "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexSr6DesignerCoverage", "SR6 Designer Coverage", "0/0 · missing", "0/0 · missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexHouseRuleLane", "House Rules", "missing · 0 overlays", "missing · 0 overlays", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexSr6SuccessorReceipt", "SR6 Successor Receipt", "missing", "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
                new DesktopDialogField("masterIndexSettingsSummary", "Use Setting", "Current defaults", "Current defaults", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Full)
            ];

            emptyStateFields.InsertRange(10, BuildSourcebookSelectionFields(masterIndex, []));
            return emptyStateFields;
        }

        IReadOnlyList<MasterIndexFileEntry> files = NormalizeMasterIndexFiles(masterIndex.Files);
        IReadOnlyList<MasterIndexSourcebookEntry> sourcebooks = NormalizeMasterIndexSourcebooks(masterIndex.Sourcebooks);

        MasterIndexSourcebookEntry? selectedSourcebook = sourcebooks.FirstOrDefault()
            ?? new MasterIndexSourcebookEntry(
                Id: "unknown",
                Code: "UNK",
                Name: "Unknown Source",
                Permanent: false,
                ReferencePosture: "missing",
                RuleSnippetCount: 0,
                RuleSnippets: [],
                ReferenceSourcePosture: "missing");
        string selectedSourcebookId = NormalizeMasterIndexValue(selectedSourcebook?.Id, "unknown");
        string selectedSourcebookCode = NormalizeMasterIndexValue(selectedSourcebook?.Code, "UNK");
        string selectedSourcebookName = NormalizeMasterIndexValue(selectedSourcebook?.Name, "Unknown Source");
        string selectedSource = ResolveMasterIndexLinkedSource(
            selectedSourcebook?.LocalPdfPath,
            selectedSourcebook?.ReferenceUrl,
            selectedSourcebook?.ReferenceSnapshot);
        List<(MasterIndexSourcebookEntry Sourcebook, MasterIndexRuleSnippetEntry Snippet)> flattenedSnippets = sourcebooks
            .OrderBy(sourcebook => sourcebook.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sourcebook => sourcebook.Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(sourcebook => (sourcebook.RuleSnippets ?? []).Select(snippet => (Sourcebook: sourcebook, Snippet: snippet)))
            .OrderBy(entry => entry.Snippet.Page)
            .ThenBy(entry => entry.Snippet.Provenance, StringComparer.OrdinalIgnoreCase)
            .ToList();
        (MasterIndexSourcebookEntry Sourcebook, MasterIndexRuleSnippetEntry Snippet) selectedEntry = flattenedSnippets.FirstOrDefault();
        if (selectedEntry.Sourcebook is not null && selectedEntry.Snippet is not null)
        {
            selectedSourcebook = selectedEntry.Sourcebook;
            selectedSourcebookId = NormalizeMasterIndexValue(selectedSourcebook?.Id, "unknown");
            selectedSourcebookCode = NormalizeMasterIndexValue(selectedSourcebook?.Code, "UNK");
            selectedSourcebookName = NormalizeMasterIndexValue(selectedSourcebook?.Name, "Unknown Source");
            selectedSource = ResolveMasterIndexLinkedSource(
                selectedSourcebook?.LocalPdfPath,
                selectedSourcebook?.ReferenceUrl,
                selectedSourcebook?.ReferenceSnapshot);
        }

        MasterIndexRuleSnippetEntry? selectedSnippet = selectedEntry.Snippet;
        MasterIndexFileEntry? selectedFile = ResolveMasterIndexSelectedFile(files, selectedSnippet);
        string selectedFileName = selectedFile?.File ?? "All";
        string snapshot = JsonSerializer.Serialize(CreateMasterIndexDialogSnapshot(masterIndex.SettingsLanePosture, files, sourcebooks));
        string activeResultKey = selectedSnippet is null
            ? string.Empty
            : BuildMasterIndexSnippetKey(selectedSnippet.Provenance, selectedSnippet.Page);
        string selectedFileSummary = selectedFile is null
            ? "All data files"
            : $"{selectedFile.File} · {selectedFile.ElementCount} indexed entries";
        string snippetPreview = selectedSnippet is null
            ? string.Empty
            : $"Page {selectedSnippet.Page} · {selectedSnippet.Provenance}{Environment.NewLine}{selectedSnippet.Snippet}";

        IReadOnlyList<DesktopDialogFieldOption> fileOptions = BuildMasterIndexFileOptions(files);
        IReadOnlyList<DesktopDialogFieldOption> resultOptions = BuildMasterIndexResultOptions(flattenedSnippets);
        string sourcebookDisplay = $"{selectedSourcebookCode} · {selectedSourcebookName}";

        List<DesktopDialogField> fields =
        [
            new DesktopDialogField("masterIndexFileSelection", "Data File", selectedFileName, "All", InputType: "select", Options: fileOptions, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("masterIndexSearch", "Search", string.Empty, "Search index", LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField(
                "masterIndexActiveResultKey",
                "Entries",
                activeResultKey,
                activeResultKey,
                InputType: "select",
                VisualKind: DesktopDialogFieldVisualKinds.List,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Left,
                Options: resultOptions),
            new DesktopDialogField("masterIndexSnippetPreview", "Notes", snippetPreview, snippetPreview, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("masterIndexCurrentSourcebook", "Source", sourcebookDisplay, sourcebookDisplay, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("masterIndexSelectedSource", "Linked PDF / URL", selectedSource, selectedSource, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("masterIndexDataRoot", "Data Root", dataRoot, dataRoot, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Full),
            new DesktopDialogField("masterIndexCurrentFile", "Current Data File", selectedFileSummary, selectedFileSummary, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSnapshot", "Snapshot", snapshot, snapshot, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexActiveSourcebookId", "Active Sourcebook", selectedSourcebookId, selectedSourcebookId, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexActiveFile", "Active File", selectedFileName, selectedFileName, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexCustomDataAuthoringReceipt", "Custom Data Authoring", NormalizeMasterIndexValue(masterIndex.CustomDataAuthoringLaneReceipt, masterIndex.CustomDataLanePosture), "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexImportOracleReceipt", "Import Oracle", NormalizeMasterIndexValue(masterIndex.ImportOracleLaneReceipt, masterIndex.ImportOracleReceiptPosture), "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexAdjacentSr6OracleLane", "Adjacent SR6 Oracle", NormalizeMasterIndexValue(masterIndex.AdjacentSr6OracleLaneReceipt, masterIndex.AdjacentSr6OracleReceiptPosture), "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexOnlineStorageLane", "Online Storage Lane", masterIndex.OnlineStorageLanePosture, "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexOnlineStorageCoverage", "Online Storage Coverage", $"{masterIndex.OnlineStorageReceiptsCovered}/{masterIndex.OnlineStorageReceiptsExpected} · {masterIndex.OnlineStorageCoveragePercent}%", "0/2 · 0%", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexOnlineStorageReceipt", "Online Storage Receipt", NormalizeMasterIndexValue(masterIndex.OnlineStorageLaneReceipt, masterIndex.OnlineStorageReceiptPosture), "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6SupplementLane", "SR6 Supplements", masterIndex.Sr6SupplementLanePosture, "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6DesignerCoverage", "SR6 Designer Coverage", $"{masterIndex.Sr6DesignerFamiliesAvailable}/{masterIndex.Sr6DesignerFamiliesExpected} · {masterIndex.Sr6DesignerToolsPosture}", "0/0 · missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexHouseRuleLane", "House Rules", $"{masterIndex.HouseRuleLanePosture} · {masterIndex.HouseRuleOverlayCount} overlays", "missing · 0 overlays", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6SuccessorReceipt", "SR6 Successor Receipt", NormalizeMasterIndexValue(masterIndex.Sr6SuccessorLaneReceipt, masterIndex.Sr6SupplementLanePosture), "missing", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField(
                "masterIndexSettingsSummary",
                "Use Setting",
                $"Current defaults · {masterIndex.SettingsProfileCount} profiles · {masterIndex.SettingsLanePosture}",
                "Current defaults",
                IsReadOnly: true,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Full)
        ];

        fields.InsertRange(10, BuildSourcebookSelectionFields(masterIndex, sourcebooks));
        return fields;
    }

    private static string ResolveMasterIndexDataRoot(MasterIndexResponse? masterIndex)
    {
        const string fallbackRoot = "/app/data";
        IReadOnlyList<MasterIndexFileEntry> files = masterIndex?.Files ?? [];
        if (files.Count == 0)
        {
            return fallbackRoot;
        }

        return fallbackRoot;
    }

    private static DesktopDialogState RebuildCharacterRosterDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        string snapshotJson = DesktopDialogFieldValueParser.GetValue(dialog, "rosterSnapshot") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return dialog;
        }

        RosterDialogSnapshot? snapshot = JsonSerializer.Deserialize<RosterDialogSnapshot>(snapshotJson);
        if (snapshot is null)
        {
            return dialog;
        }

        string selectedRunnerId = DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedRunnerId") ?? string.Empty;
        CharacterWorkspaceId? currentWorkspace = string.IsNullOrWhiteSpace(selectedRunnerId)
            ? null
            : new CharacterWorkspaceId(selectedRunnerId);
        OpenWorkspaceState[] workspaces = snapshot.Workspaces
            .Select(workspace => new OpenWorkspaceState(
                new CharacterWorkspaceId(workspace.Id),
                workspace.Name,
                workspace.Alias,
                workspace.LastOpenedUtc,
                workspace.RulesetId,
                workspace.HasSavedWorkspace))
            .ToArray();

        DesktopDialogField[] rebuiltFields = BuildRosterFields(
                snapshot.FallbackName,
                snapshot.FallbackAlias,
                snapshot.FallbackWorkspace,
                currentWorkspace,
                workspaces,
                fallback)
            .ToArray();
        DesktopDialogAction[] rebuiltActions = BuildRosterActions(
                snapshot.FallbackName,
                snapshot.FallbackAlias,
                snapshot.FallbackWorkspace,
                currentWorkspace,
                workspaces,
                fallback)
            .ToArray();

        string requestedWatchFile = DesktopDialogFieldValueParser.GetValue(dialog, "rosterSelectedWatchFile") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requestedWatchFile))
        {
            rebuiltFields = rebuiltFields
                .Select(field => string.Equals(field.Id, "rosterSelectedWatchFile", StringComparison.Ordinal)
                    ? field with { Value = requestedWatchFile, Placeholder = requestedWatchFile }
                    : field)
                .ToArray();
        }

        return dialog with
        {
            Fields = rebuiltFields,
            Actions = rebuiltActions
        };
    }

    private static DesktopDialogState RebuildMasterIndexDialog(DesktopDialogState dialog)
    {
        string snapshotJson = DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSnapshot") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return dialog;

        MasterIndexDialogSnapshot? snapshot = JsonSerializer.Deserialize<MasterIndexDialogSnapshot>(snapshotJson);
        if (snapshot is null || snapshot.Sourcebooks.Count == 0)
            return dialog;

        string search = (DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexSearch") ?? string.Empty).Trim();
        string requestedSourcebookId = ResolveMasterIndexSelectedSourcebookId(
            snapshot,
            DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexActiveSourcebookId"),
            DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexCurrentSourcebook"));
        string requestedFile = NormalizeMasterIndexActiveFile(
            DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexActiveFile"),
            DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexFileSelection"));

        List<(MasterIndexDialogSourcebookSnapshot Sourcebook, MasterIndexDialogSnippetSnapshot Snippet)> filteredSnippets = snapshot.Sourcebooks
            .OrderBy(sourcebook => sourcebook.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sourcebook => sourcebook.Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(sourcebook => sourcebook.RuleSnippets.Select(snippet => (Sourcebook: sourcebook, Snippet: snippet)))
            .Where(entry => string.Equals(requestedFile, "All", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Snippet.File, requestedFile, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Snippet.Provenance, requestedFile, StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.IsNullOrWhiteSpace(search)
                || entry.Snippet.Snippet.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Snippet.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Sourcebook.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Sourcebook.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Snippet.Page.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Snippet.Page)
            .ThenBy(entry => entry.Snippet.Provenance, StringComparer.OrdinalIgnoreCase)
            .ToList();

        (MasterIndexDialogSourcebookSnapshot Sourcebook, MasterIndexDialogSnippetSnapshot Snippet)? selectedEntry = ResolveMasterIndexSelectedEntry(
            filteredSnippets,
            requestedSourcebookId,
            DesktopDialogFieldValueParser.GetValue(dialog, "masterIndexActiveResultKey"));
        MasterIndexDialogSourcebookSnapshot selectedSourcebook = selectedEntry?.Sourcebook
            ?? snapshot.Sourcebooks.FirstOrDefault(sourcebook => string.Equals(sourcebook.Id, requestedSourcebookId, StringComparison.Ordinal))
            ?? snapshot.Sourcebooks[0];
        MasterIndexDialogSnippetSnapshot? selectedSnippet = selectedEntry?.Snippet;
        MasterIndexDialogFileSnapshot? selectedFile = ResolveMasterIndexSelectedFileSnapshot(
            snapshot.Files,
            requestedFile,
            selectedSnippet);

        string selectedFileName = selectedFile?.File ?? "All";
        string selectedFileSummary = selectedFile is null
            ? "All data files"
            : $"{selectedFile.File} · {selectedFile.ElementCount} indexed entries";
        string selectedSource = ResolveMasterIndexLinkedSource(
            selectedSourcebook.LocalPdfPath,
            selectedSourcebook.ReferenceUrl,
            selectedSourcebook.ReferenceSnapshot);
        string snippetPreview = selectedSnippet is null
            ? string.Empty
            : $"Page {selectedSnippet.Page} · {selectedSnippet.Provenance}{Environment.NewLine}{selectedSnippet.Snippet}";

        DesktopDialogField[] rebuiltFields = dialog.Fields
            .Select(field => field.Id switch
            {
                "masterIndexActiveSourcebookId" => field with { Value = selectedSourcebook.Id, Placeholder = selectedSourcebook.Id },
                "masterIndexActiveFile" => field with { Value = selectedFileName, Placeholder = selectedFileName },
                "masterIndexActiveResultKey" => field with
                {
                    Label = "Entries",
                    Value = selectedSnippet is null ? string.Empty : BuildMasterIndexSnippetKey(selectedSnippet.Provenance, selectedSnippet.Page),
                    Placeholder = selectedSnippet is null ? string.Empty : BuildMasterIndexSnippetKey(selectedSnippet.Provenance, selectedSnippet.Page),
                    InputType = "select",
                    VisualKind = DesktopDialogFieldVisualKinds.List,
                    IsReadOnly = false,
                    LayoutSlot = DesktopDialogFieldLayoutSlots.Left,
                    Options = BuildMasterIndexResultOptions(filteredSnippets)
                },
                "masterIndexCurrentSourcebook" => field with
                {
                    Label = "Source",
                    Value = $"{selectedSourcebook.Code} · {selectedSourcebook.Name}",
                    Placeholder = $"{selectedSourcebook.Code} · {selectedSourcebook.Name}",
                    LayoutSlot = DesktopDialogFieldLayoutSlots.Right
                },
                "masterIndexFileSelection" => field with
                {
                    Value = selectedFileName,
                    Placeholder = "All",
                    Options = BuildMasterIndexFileOptions(snapshot.Files.Select(file => new MasterIndexFileEntry(file.File, string.Empty, file.ElementCount)).ToArray())
                },
                "masterIndexCurrentFile" => field with { Value = selectedFileSummary, Placeholder = selectedFileSummary },
                "masterIndexSnippetPreview" => field with
                {
                    Value = snippetPreview,
                    Placeholder = snippetPreview,
                    LayoutSlot = DesktopDialogFieldLayoutSlots.Right
                },
                "masterIndexSelectedSource" => field with
                {
                    Label = "Linked PDF / URL",
                    Value = selectedSource,
                    Placeholder = selectedSource,
                    LayoutSlot = DesktopDialogFieldLayoutSlots.Right
                },
                _ => field
            })
            .ToArray();

        return dialog with { Fields = rebuiltFields };
    }

    private static IReadOnlyList<DesktopDialogAction> BuildMasterIndexActions(MasterIndexResponse? masterIndex)
        => [
            new DesktopDialogAction("open_source", "Open Linked Source", true),
            new DesktopDialogAction("close", "Close")
        ];

    private static IReadOnlyList<DesktopDialogFieldOption> BuildMasterIndexFileOptions(IReadOnlyList<MasterIndexFileEntry> files)
    {
        List<DesktopDialogFieldOption> options = [new("All", "All data files")];
        options.AddRange(files
            .OrderBy(file => file.File, StringComparer.OrdinalIgnoreCase)
            .Select(file => new DesktopDialogFieldOption(file.File, $"{file.File} · {file.ElementCount} entries")));
        return options;
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildMasterIndexResultOptions(
        IEnumerable<(MasterIndexSourcebookEntry Sourcebook, MasterIndexRuleSnippetEntry Snippet)> snippets)
    {
        DesktopDialogFieldOption[] options = snippets
            .Select(entry => new DesktopDialogFieldOption(
                BuildMasterIndexSnippetKey(entry.Snippet.Provenance, entry.Snippet.Page),
                $"{entry.Sourcebook.Code} p. {entry.Snippet.Page} · {BuildMasterIndexSnippetLabel(entry.Snippet.Snippet)} · {entry.Snippet.Provenance}"))
            .DistinctBy(option => option.Value, StringComparer.Ordinal)
            .ToArray();

        return options.Length == 0
            ? [new DesktopDialogFieldOption(string.Empty, "No indexed entries discovered.")]
            : options;
    }

    private static IReadOnlyList<DesktopDialogFieldOption> BuildMasterIndexResultOptions(
        IEnumerable<(MasterIndexDialogSourcebookSnapshot Sourcebook, MasterIndexDialogSnippetSnapshot Snippet)> snippets)
    {
        DesktopDialogFieldOption[] options = snippets
            .Select(entry => new DesktopDialogFieldOption(
                BuildMasterIndexSnippetKey(entry.Snippet.File, entry.Snippet.Page),
                $"{entry.Sourcebook.Code} p. {entry.Snippet.Page} · {BuildMasterIndexSnippetLabel(entry.Snippet.Snippet)} · {entry.Snippet.Provenance}"))
            .DistinctBy(option => option.Value, StringComparer.Ordinal)
            .ToArray();

        return options.Length == 0
            ? [new DesktopDialogFieldOption(string.Empty, "No indexed entries discovered.")]
            : options;
    }

    private static string BuildSourcebookSelectionSummary(IReadOnlyList<MasterIndexSourcebookEntry> sourcebooks)
    {
        if (sourcebooks.Count == 0)
            return "No sourcebooks discovered.";

        int permanentCount = sourcebooks.Count(sourcebook => sourcebook.Permanent);
        int selectableCount = sourcebooks.Count - permanentCount;
        int linkedSourceCount = sourcebooks.Count(sourcebook =>
            !string.IsNullOrWhiteSpace(sourcebook.LocalPdfPath)
            || !string.IsNullOrWhiteSpace(sourcebook.ReferenceUrl)
            || !string.IsNullOrWhiteSpace(sourcebook.ReferenceSnapshot));
        int localPdfCount = sourcebooks.Count(sourcebook => !string.IsNullOrWhiteSpace(sourcebook.LocalPdfPath));

        return $"{sourcebooks.Count} sourcebooks ({selectableCount} selectable, {permanentCount} permanent); linked sources on {linkedSourceCount}/{sourcebooks.Count}, local PDFs on {localPdfCount}/{sourcebooks.Count}.";
    }

    private static IReadOnlyList<DesktopDialogField> BuildSourcebookSelectionFields(
        MasterIndexResponse? masterIndex,
        IReadOnlyList<MasterIndexSourcebookEntry> sourcebooks)
    {
        string sourcebookSelectionSummary = BuildSourcebookSelectionSummary(sourcebooks);
        string sourceSelectionReceipt = masterIndex is null
            ? "All"
            : NormalizeMasterIndexValue(masterIndex.SourceSelectionLaneReceipt, masterIndex.SourceToggleLanePosture);
        string referenceSourceReceipt = masterIndex is null
            ? "missing"
            : NormalizeMasterIndexValue(masterIndex.ReferenceSourceLaneReceipt, sourcebookSelectionSummary);

        return
        [
            new DesktopDialogField("masterIndexSourceSelectionReceipt", "Source Selection", sourceSelectionReceipt, sourcebookSelectionSummary, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexReferenceSourceReceipt", "Reference Sources", referenceSourceReceipt, sourcebookSelectionSummary, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden)
        ];
    }

    private static string BuildMasterIndexReferenceTargetKinds(MasterIndexDialogSourcebookSnapshot sourcebook)
    {
        List<string> kinds = [];
        if (!string.IsNullOrWhiteSpace(sourcebook.LocalPdfPath))
            kinds.Add("pdf");
        if (!string.IsNullOrWhiteSpace(sourcebook.ReferenceUrl))
            kinds.Add("url");
        if (!string.IsNullOrWhiteSpace(sourcebook.ReferenceSnapshot))
            kinds.Add("snapshot");

        return kinds.Count == 0 ? "none" : string.Join("+", kinds);
    }

    private static string BuildImportOracleMatrix(MasterIndexResponse masterIndex)
    {
        return $"Chummer4 fixtures {masterIndex.LegacyChummer4FixtureCount}, Chummer5a fixtures {masterIndex.LegacyChummer5FixtureCount}, Hero Lab fixtures {masterIndex.HeroLabFixtureCount}, adjacent SR6 sources {masterIndex.AdjacentSr6OracleSourcesCovered}/{masterIndex.AdjacentSr6OracleSourcesExpected}.";
    }

    private static MasterIndexDialogSnapshot CreateMasterIndexDialogSnapshot(
        string? settingsLanePosture,
        IReadOnlyList<MasterIndexFileEntry> files,
        IReadOnlyList<MasterIndexSourcebookEntry> sourcebooks)
        => new(
            NormalizeMasterIndexValue(settingsLanePosture, "missing"),
            files
                .OrderBy(file => file.File, StringComparer.OrdinalIgnoreCase)
                .Select(file => new MasterIndexDialogFileSnapshot(file.File, file.ElementCount))
                .ToArray(),
            sourcebooks
                .OrderBy(sourcebook => sourcebook.Code, StringComparer.OrdinalIgnoreCase)
                .ThenBy(sourcebook => sourcebook.Name, StringComparer.OrdinalIgnoreCase)
                .Select(sourcebook => new MasterIndexDialogSourcebookSnapshot(
                    sourcebook.Id,
                    sourcebook.Code,
                    sourcebook.Name,
                    sourcebook.ReferencePosture,
                    sourcebook.ReferenceSourcePosture,
                    sourcebook.LocalPdfPath,
                    sourcebook.ReferenceUrl,
                    sourcebook.ReferenceSnapshot,
                    (sourcebook.RuleSnippets ?? [])
                        .OrderBy(snippet => snippet.Page)
                        .ThenBy(snippet => snippet.Provenance, StringComparer.OrdinalIgnoreCase)
                        .Select(snippet => new MasterIndexDialogSnippetSnapshot(
                            snippet.Provenance,
                            snippet.Provenance,
                            snippet.Page,
                            snippet.Snippet))
                        .ToArray()))
                .ToArray());

    private static IReadOnlyList<MasterIndexFileEntry> NormalizeMasterIndexFiles(IReadOnlyList<MasterIndexFileEntry>? files)
        => (files ?? [])
            .Where(static file => file is not null)
            .Select(file => new MasterIndexFileEntry(
                NormalizeMasterIndexValue(file.File, "unknown.xml"),
                NormalizeMasterIndexValue(file.Root),
                Math.Max(file.ElementCount, 0)))
            .DistinctBy(file => file.File, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<MasterIndexSourcebookEntry> NormalizeMasterIndexSourcebooks(IReadOnlyList<MasterIndexSourcebookEntry>? sourcebooks)
        => (sourcebooks ?? [])
            .Where(static sourcebook => sourcebook is not null)
            .Select(sourcebook =>
            {
                MasterIndexRuleSnippetEntry[] normalizedSnippets = NormalizeMasterIndexSnippets(sourcebook.RuleSnippets).ToArray();
                return new MasterIndexSourcebookEntry(
                    NormalizeMasterIndexValue(sourcebook.Id, "unknown"),
                    NormalizeMasterIndexValue(sourcebook.Code, "UNK"),
                    NormalizeMasterIndexValue(sourcebook.Name, "Unknown Source"),
                    sourcebook.Permanent,
                    NormalizeMasterIndexValue(sourcebook.ReferencePosture, "missing"),
                    Math.Max(sourcebook.RuleSnippetCount, normalizedSnippets.Length),
                    normalizedSnippets,
                    NormalizeMasterIndexValue(sourcebook.ReferenceSourcePosture, "missing"),
                    NormalizeMasterIndexValue(sourcebook.LocalPdfPath),
                    NormalizeMasterIndexValue(sourcebook.ReferenceUrl),
                    NormalizeMasterIndexValue(sourcebook.ReferenceSnapshot),
                    NormalizeMasterIndexValue(sourcebook.ReferenceSnapshotPosture, "missing"));
            })
            .DistinctBy(sourcebook => sourcebook.Id, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<MasterIndexRuleSnippetEntry> NormalizeMasterIndexSnippets(IReadOnlyList<MasterIndexRuleSnippetEntry>? snippets)
        => (snippets ?? [])
            .Where(static snippet => snippet is not null)
            .Select(snippet => new MasterIndexRuleSnippetEntry(
                NormalizeMasterIndexValue(snippet.Language, "en-US"),
                Math.Max(snippet.Page, 0),
                NormalizeMasterIndexValue(snippet.Snippet),
                NormalizeMasterIndexValue(snippet.Provenance, "unknown")))
            .ToArray();

    private static string ResolveMasterIndexLinkedSource(params string?[] candidates)
        => candidates
            .Select(candidate => NormalizeMasterIndexValue(candidate))
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))
        ?? string.Empty;

    private static string NormalizeMasterIndexValue(string? value, string fallback = "")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string NormalizeAdjacentSr6OracleReceipt(string? value, string fallback = "")
    {
        string normalized = NormalizeMasterIndexValue(value, fallback);
        if (string.IsNullOrWhiteSpace(normalized))
            return normalized;

        if (string.Equals(normalized, "missing", StringComparison.OrdinalIgnoreCase))
            return "No Adjacent SR6 compatibility coverage was found for Genesis/CommLink6.";

        if (normalized.StartsWith("adjacent SR6 oracle", StringComparison.OrdinalIgnoreCase))
            return "Adjacent SR6 oracle" + normalized["adjacent SR6 oracle".Length..];

        return normalized.StartsWith("No adjacent SR6 oracle", StringComparison.OrdinalIgnoreCase)
            ? "No Adjacent SR6 oracle" + normalized["No adjacent SR6 oracle".Length..]
            : normalized;
    }

    private static MasterIndexFileEntry? ResolveMasterIndexSelectedFile(
        IReadOnlyList<MasterIndexFileEntry> files,
        MasterIndexRuleSnippetEntry? selectedSnippet)
    {
        if (files.Count == 0)
            return null;

        if (selectedSnippet is not null)
        {
            MasterIndexFileEntry? matchingFile = files.FirstOrDefault(file =>
                string.Equals(file.File, selectedSnippet.Provenance, StringComparison.OrdinalIgnoreCase));
            if (matchingFile is not null)
                return matchingFile;
        }

        return files[0];
    }

    private static string BuildMasterIndexFileSelection(
        IReadOnlyList<MasterIndexFileEntry> files,
        MasterIndexFileEntry? selectedFile)
    {
        if (files.Count == 0)
            return "All" + Environment.NewLine + "books.xml · 0 entries";

        IEnumerable<string> lines = files
            .OrderBy(file => file.File, StringComparer.OrdinalIgnoreCase)
            .Select(file =>
                $"{(selectedFile is not null && string.Equals(file.File, selectedFile.File, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {file.File} · {file.ElementCount} entries");

        return "All" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static MasterIndexDialogFileSnapshot? ResolveMasterIndexSelectedFileSnapshot(
        IReadOnlyList<MasterIndexDialogFileSnapshot> files,
        string? requestedFile,
        MasterIndexDialogSnippetSnapshot? selectedSnippet)
    {
        if (files.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(requestedFile)
            && !string.Equals(requestedFile, "All", StringComparison.OrdinalIgnoreCase))
        {
            MasterIndexDialogFileSnapshot? matchingRequestedFile = files.FirstOrDefault(file =>
                string.Equals(file.File, requestedFile, StringComparison.OrdinalIgnoreCase));
            if (matchingRequestedFile is not null)
                return matchingRequestedFile;
        }

        if (selectedSnippet is not null)
        {
            MasterIndexDialogFileSnapshot? matchingSnippetFile = files.FirstOrDefault(file =>
                string.Equals(file.File, selectedSnippet.File, StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.File, selectedSnippet.Provenance, StringComparison.OrdinalIgnoreCase));
            if (matchingSnippetFile is not null)
                return matchingSnippetFile;
        }

        return files[0];
    }

    private static string BuildMasterIndexFileSelectionSnapshot(
        IReadOnlyList<MasterIndexDialogFileSnapshot> files,
        MasterIndexDialogFileSnapshot? selectedFile)
    {
        if (files.Count == 0)
            return "All" + Environment.NewLine + "books.xml · 0 entries";

        IEnumerable<string> lines = files
            .OrderBy(file => file.File, StringComparer.OrdinalIgnoreCase)
            .Select(file =>
                $"{(selectedFile is not null && string.Equals(file.File, selectedFile.File, StringComparison.OrdinalIgnoreCase) ? ">" : " ")} {file.File} · {file.ElementCount} entries");

        return "All" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static string BuildMasterIndexSnippetLabel(MasterIndexRuleSnippetEntry snippet)
        => BuildMasterIndexSnippetLabel(snippet.Snippet);

    private static string BuildMasterIndexSnippetLabel(string? snippetText)
    {
        if (string.IsNullOrWhiteSpace(snippetText))
        {
            return "(no note text)";
        }

        string normalized = string.Join(" ", snippetText
            .Split([Environment.NewLine, "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length <= 64)
        {
            return normalized;
        }

        return normalized[..61].TrimEnd() + "...";
    }

    private static string BuildMasterIndexSnippetKey(string? provenance, int page)
        => $"{provenance ?? string.Empty}|{page}";

    private static string NormalizeMasterIndexActiveFile(string? activeFile, string? fileSelectionField)
    {
        if (!string.IsNullOrWhiteSpace(activeFile))
            return activeFile.Trim();

        if (string.IsNullOrWhiteSpace(fileSelectionField))
            return "All";

        string firstLine = fileSelectionField
            .Split([Environment.NewLine, "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "All";

        if (string.Equals(firstLine, "All", StringComparison.OrdinalIgnoreCase))
            return "All";

        string normalized = firstLine.TrimStart('>', ' ').Trim();
        int separatorIndex = normalized.IndexOf(" · ", StringComparison.Ordinal);
        return separatorIndex >= 0 ? normalized[..separatorIndex] : normalized;
    }

    private static string ResolveMasterIndexSelectedSourcebookId(
        MasterIndexDialogSnapshot snapshot,
        string? requestedSourcebookId,
        string? currentSourcebook)
    {
        if (!string.IsNullOrWhiteSpace(requestedSourcebookId)
            && snapshot.Sourcebooks.Any(sourcebook => string.Equals(sourcebook.Id, requestedSourcebookId, StringComparison.Ordinal)))
        {
            return requestedSourcebookId;
        }

        if (!string.IsNullOrWhiteSpace(currentSourcebook))
        {
            MasterIndexDialogSourcebookSnapshot? matchingSourcebook = snapshot.Sourcebooks.FirstOrDefault(sourcebook =>
                string.Equals($"{sourcebook.Code} · {sourcebook.Name}", currentSourcebook, StringComparison.Ordinal));
            if (matchingSourcebook is not null)
                return matchingSourcebook.Id;
        }

        return snapshot.Sourcebooks[0].Id;
    }

    private static (MasterIndexDialogSourcebookSnapshot Sourcebook, MasterIndexDialogSnippetSnapshot Snippet)? ResolveMasterIndexSelectedEntry(
        IReadOnlyList<(MasterIndexDialogSourcebookSnapshot Sourcebook, MasterIndexDialogSnippetSnapshot Snippet)> snippets,
        string requestedSourcebookId,
        string? requestedKey)
    {
        if (!string.IsNullOrWhiteSpace(requestedKey))
        {
            (MasterIndexDialogSourcebookSnapshot Sourcebook, MasterIndexDialogSnippetSnapshot Snippet) matchingSnippet = snippets.FirstOrDefault(snippet =>
                string.Equals(BuildMasterIndexSnippetKey(snippet.Snippet.File, snippet.Snippet.Page), requestedKey, StringComparison.Ordinal)
                || string.Equals(BuildMasterIndexSnippetKey(snippet.Snippet.Provenance, snippet.Snippet.Page), requestedKey, StringComparison.Ordinal));
            if (matchingSnippet.Sourcebook is not null && matchingSnippet.Snippet is not null)
                return matchingSnippet;
        }

        (MasterIndexDialogSourcebookSnapshot Sourcebook, MasterIndexDialogSnippetSnapshot Snippet) matchingSourcebookSnippet = snippets
            .FirstOrDefault(snippet => string.Equals(snippet.Sourcebook.Id, requestedSourcebookId, StringComparison.Ordinal));
        if (matchingSourcebookSnippet.Sourcebook is not null && matchingSourcebookSnippet.Snippet is not null)
            return matchingSourcebookSnippet;

        return snippets.FirstOrDefault();
    }

    private sealed record MasterIndexDialogSnapshot(
        string SettingsLanePosture,
        IReadOnlyList<MasterIndexDialogFileSnapshot> Files,
        IReadOnlyList<MasterIndexDialogSourcebookSnapshot> Sourcebooks);

    private sealed record MasterIndexDialogFileSnapshot(
        string File,
        int ElementCount);

    private sealed record MasterIndexDialogSourcebookSnapshot(
        string Id,
        string Code,
        string Name,
        string ReferencePosture,
        string ReferenceSourcePosture,
        string? LocalPdfPath,
        string? ReferenceUrl,
        string? ReferenceSnapshot,
        IReadOnlyList<MasterIndexDialogSnippetSnapshot> RuleSnippets);

    private sealed record MasterIndexDialogSnippetSnapshot(
        string File,
        string Provenance,
        int Page,
        string Snippet);

    private sealed record RosterDialogSnapshot(
        string FallbackAlias,
        string FallbackName,
        string FallbackWorkspace,
        IReadOnlyList<RosterDialogWorkspaceSnapshot> Workspaces,
        IReadOnlyList<string> WatchedFiles,
        RosterHierarchyState Hierarchy,
        string HierarchySource);

    private sealed record RosterDialogWorkspaceSnapshot(
        string Id,
        string Name,
        string Alias,
        DateTimeOffset LastOpenedUtc,
        string RulesetId,
        bool HasSavedWorkspace);

    private sealed record OriginBuildRecommendation(
        string Archetype,
        string ArchetypeLabel,
        string BuildMethod,
        string MetatypeCategory,
        string Metatype,
        string QualityFocus,
        string PathSummary,
        string GmRequirementSummary,
        string OriginSummary,
        string BuildSummary);
}
