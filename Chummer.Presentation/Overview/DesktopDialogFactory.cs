using Chummer.Contracts.Characters;
using Chummer.Contracts.Api;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Explain;
using System.Globalization;
using System.IO;

namespace Chummer.Presentation.Overview;

public sealed class DesktopDialogFactory : IDesktopDialogFactory
{
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
            Message: "Apply character metadata changes to the active workspace.",
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

        return commandId switch
        {
            OverviewCommandPolicy.RuntimeInspectorCommandId when runtimeInspector is not null => CreateRuntimeInspectorDialog(runtimeInspector),
            "open_character" => CreateOpenCharacterDialog(
                "dialog.open_character",
                "Open Character",
                "Paste Chummer XML to import into a workspace.",
                rulesetId),
            "open_for_printing" => CreateOpenCharacterDialog(
                "dialog.open_for_printing",
                "Open for Printing",
                "Paste Chummer XML to stage print workflows.",
                rulesetId),
            "open_for_export" => CreateOpenCharacterDialog(
                "dialog.open_for_export",
                "Open for Export",
                "Paste Chummer XML to stage export workflows.",
                rulesetId),
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
                "Ruleset-backed dice utility with initiative planning and current roster context.",
                BuildDiceToolFields(currentWorkspace, openWorkspaces),
                [
                    new DesktopDialogAction("roll", "Roll", true),
                    new DesktopDialogAction("derive_initiative", "Preview Initiative"),
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
            "character_settings" => new DesktopDialogState(
                "dialog.character_settings",
                "Character Settings",
                "Rules environment posture for source toggles, custom data, and XML bridge is surfaced here so the modern desktop does not hide legacy-era configuration truth.",
                [
                    new DesktopDialogField("characterPriority", "Priority System", preferences.CharacterPriority, "SumToTen"),
                    new DesktopDialogField("characterKarmaNuyen", "Karma/Nuyen Ratio", preferences.KarmaNuyenRatio.ToString(), "2", InputType: "number"),
                    new DesktopDialogField("characterHouseRulesEnabled", "Enable House Rules", preferences.HouseRulesEnabled ? "true" : "false", "false", InputType: "checkbox"),
                    new DesktopDialogField("characterNotes", "Character Notes", preferences.CharacterNotes, "notes", true),
                    new DesktopDialogField("characterSettingsLanePosture", "Settings Lane", masterIndex?.SettingsLanePosture ?? "missing", "missing", IsReadOnly: true),
                    new DesktopDialogField("characterSourceToggleLanePosture", "Source Toggle Lane", masterIndex?.SourceToggleLanePosture ?? "missing", "missing", IsReadOnly: true),
                    new DesktopDialogField("characterSourceToggleCoverage", "Source Toggle Coverage", masterIndex is null ? "0%" : $"{masterIndex.SourcebookToggleCoveragePercent}% ({masterIndex.DistinctSourcebookToggles} toggles)", "0%", IsReadOnly: true),
                    new DesktopDialogField("characterCustomDataLanePosture", "Custom Data Lane", masterIndex?.CustomDataLanePosture ?? "missing", "missing", IsReadOnly: true),
                    new DesktopDialogField("characterXmlBridgePosture", "XML Bridge", masterIndex?.XmlBridgePosture ?? "missing", "missing", IsReadOnly: true)
                ],
                [
                    new DesktopDialogAction("save", "Save", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "translator" => new DesktopDialogState(
                "dialog.translator",
                S("desktop.dialog.translator.title"),
                F("desktop.dialog.translator.message", DesktopLocalizationCatalog.BuildSupportedLanguageCodeSummary()),
                BuildTranslatorFields(language, masterIndex, translatorLanguages),
                [new DesktopDialogAction("close", S("desktop.dialog.action.close"), true)]),
            "xml_editor" => new DesktopDialogState(
                "dialog.xml_editor",
                "XML Editor",
                masterIndex is null
                    ? "Edit/import flow in this head is file-first; this preview surfaces current XML bridge posture."
                    : $"Edit/import flow stays file-first while XML bridge posture is {masterIndex.XmlBridgePosture} with {masterIndex.EnabledDataOverlayCount} enabled overlays and custom-data lane {masterIndex.CustomDataLanePosture}.",
                [
                    new DesktopDialogField("xmlEditorLanePosture", "XML Bridge", masterIndex?.XmlBridgePosture ?? "missing", "missing", IsReadOnly: true),
                    new DesktopDialogField("xmlEditorOverlayCount", "Enabled XML Overlays", (masterIndex?.EnabledDataOverlayCount ?? 0).ToString(), "0", IsReadOnly: true),
                    new DesktopDialogField("xmlEditorCustomDataLanePosture", "Custom Data Lane", masterIndex?.CustomDataLanePosture ?? "missing", "missing", IsReadOnly: true),
                    new DesktopDialogField("xmlEditorCustomDataDirectoryCount", "Custom Data Directories", (masterIndex?.DistinctCustomDataDirectoryCount ?? 0).ToString(), "0", IsReadOnly: true),
                    new DesktopDialogField("xmlEditorReceipt", "XML Bridge Receipt", masterIndex?.XmlBridgeLaneReceipt ?? "missing", "missing", IsReadOnly: true),
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
                "Open runners on the left, keep the selected runner summary on the right, and keep background plus notes compact underneath like the legacy roster utility.",
                BuildRosterFields(name, alias, workspace, currentWorkspace, openWorkspaces, preferences),
                [new DesktopDialogAction("close", "Close", true)]),
            "data_exporter" => new DesktopDialogState(
                "dialog.data_exporter",
                "Data Exporter",
                "Export pipeline is routed through API tool endpoints.",
                [new DesktopDialogField("dataExportPreview", "Export Preview", $"Workspace: {workspace}", "{}", true, true)],
                [
                    new DesktopDialogAction("download", "Download", true),
                    new DesktopDialogAction("close", "Close")
                ]),
            "export_character" => new DesktopDialogState(
                "dialog.export_character",
                "Export Character",
                "Export selected character bundle.",
                [new DesktopDialogField("dataExportPreview", "Export Preview", $"Workspace: {workspace}", "{}", true, true)],
                [
                    new DesktopDialogAction("download", "Download", true),
                    new DesktopDialogAction("close", "Close")
                ]),
            "report_bug" => new DesktopDialogState(
                "dialog.report_bug",
                "Support and bug reporting",
                "Use the signed-in Hub support surface for tracked install-aware cases. GitHub is still available for public issue reporting, but the flagship desktop flow is claim-aware support closure.",
                [
                    new DesktopDialogField("supportHub", "Tracked support", "/account/support", "/account/support", IsReadOnly: true),
                    new DesktopDialogField("supportPublic", "Guest support", "/contact", "/contact", IsReadOnly: true),
                    new DesktopDialogField("supportGithub", "Public GitHub issue form", "https://github.com/chummer5a/chummer5a/issues/new/choose", "https://github.com/chummer5a/chummer5a/issues/new/choose", IsReadOnly: true)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "about" => new DesktopDialogState(
                "dialog.about",
                "About Chummer",
                "Dual-head preview over shared presenter/API behavior path.",
                [
                    new DesktopDialogField("runtime", "Runtime", "net10.0", "net10.0", IsReadOnly: true),
                    new DesktopDialogField("workspace", "Workspace", workspace, workspace, IsReadOnly: true)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "hero_lab_importer" => new DesktopDialogState(
                "dialog.hero_lab_importer",
                "Hero Lab Importer",
                "Paste Hero Lab XML payload to import using compatibility mode.",
                [
                    new DesktopDialogField("heroLabSource", "Input File", ".por/.xml", ".por/.xml"),
                    CreateRulesetField("importRulesetId", rulesetId),
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
                BuildWindowUtilityFields("Open New Window", "A second shell stays bound to the desktop host instead of taking over the current workbench."),
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
                "https://github.com/chummer5a/chummer5a/wiki",
                BuildExternalLinkFields("Chummer Wiki", "https://github.com/chummer5a/chummer5a/wiki", "Use the legacy wiki as an external reference without displacing the current workbench."),
                [new DesktopDialogAction("close", "Close", true)]),
            "discord" => new DesktopDialogState(
                "dialog.discord",
                "Discord",
                "https://discord.gg/EV44Mya",
                BuildExternalLinkFields("Community Discord", "https://discord.gg/EV44Mya", "Community chat opens in the browser instead of replacing the desktop workbench."),
                [new DesktopDialogAction("close", "Close", true)]),
            "revision_history" => new DesktopDialogState(
                "dialog.revision_history",
                "Revision History",
                "https://github.com/chummer5a/chummer5a/releases",
                BuildExternalLinkFields("Revision History", "https://github.com/chummer5a/chummer5a/releases", "Release notes open as an external help surface."),
                [new DesktopDialogAction("close", "Close", true)]),
            "dumpshock" => new DesktopDialogState(
                "dialog.dumpshock",
                "Dumpshock Thread",
                "https://forums.dumpshock.com/index.php?showtopic=37464",
                BuildExternalLinkFields("Dumpshock Thread", "https://forums.dumpshock.com/index.php?showtopic=37464", "The legacy forum thread opens externally and stays outside the workbench shell."),
                [new DesktopDialogAction("close", "Close", true)]),
            "print_character" => new DesktopDialogState(
                "dialog.print_character",
                "Print Character",
                "Print preview is rendered by host/browser print facilities.",
                BuildPrintUtilityFields("Current runner", "Print preview stays host-driven while sheet/export context remains visible."),
                [new DesktopDialogAction("close", "Close", true)]),
            "print_multiple" => new DesktopDialogState(
                "dialog.print_multiple",
                "Print Multiple",
                "Batch print is available through roster and print endpoints.",
                BuildPrintUtilityFields("Roster batch", "Batch print remains roster-driven and uses the same compact print utility posture."),
                [new DesktopDialogAction("close", "Close", true)]),
            "update" => new DesktopDialogState(
                "dialog.update",
                "Check for Updates",
                "Desktop heads check the configured registry manifest (`CHUMMER_DESKTOP_UPDATE_MANIFEST`) at startup, stage either an in-place payload or a platform installer, and keep install linking plus support continuity intact across the relaunch boundary.",
                BuildUpdateUtilityFields(),
                [new DesktopDialogAction("close", "Close", true)]),
            _ => new DesktopDialogState(
                "dialog.generic",
                commandId,
                $"Command '{commandId}' is recognized but has no dedicated dialog template yet.",
                [],
                [new DesktopDialogAction("close", "Close", true)])
        };
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

    private static DesktopDialogState CreateOpenCharacterDialog(
        string id,
        string title,
        string message,
        string? rulesetId)
    {
        const string defaultXml = "<character><name>Imported Runner</name></character>";

        return new DesktopDialogState(
            Id: id,
            Title: title,
            Message: message,
            Fields:
            [
                CreateRulesetField("importRulesetId", rulesetId),
                new DesktopDialogField(
                    Id: "openCharacterXml",
                    Label: "Character XML",
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
        string value = RulesetDefaults.NormalizeOptional(rulesetId) ?? string.Empty;
        return new DesktopDialogField(
            Id: fieldId,
            Label: "Ruleset",
            Value: value,
            Placeholder: value);
    }

    private static IReadOnlyList<DesktopDialogField> BuildDiceToolFields(
        CharacterWorkspaceId? currentWorkspace,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces)
    {
        IReadOnlyList<OpenWorkspaceState> roster = openWorkspaces ?? Array.Empty<OpenWorkspaceState>();
        int savedCount = roster.Count(workspace => workspace.HasSavedWorkspace);
        string rosterContext = roster.Count switch
        {
            0 => "No open runners. The utility still works as a standalone dice and initiative pad.",
            _ => $"{roster.Count} open runner{(roster.Count == 1 ? string.Empty : "s")} · {savedCount} saved · active {(currentWorkspace?.Value ?? roster[0].Id.Value)}"
        };

        return
        [
            new DesktopDialogField("diceExpression", "Expression", "12d6", "12d6"),
            new DesktopDialogField("diceThreshold", "Threshold", "0", "0", InputType: "number"),
            new DesktopDialogField("diceLimit", "Limit", "0", "0", InputType: "number"),
            new DesktopDialogField("diceWoundModifier", "Wound Modifier", "0", "0", InputType: "number"),
            new DesktopDialogField("diceInitiativeBase", "Initiative Base", "10", "10", InputType: "number"),
            new DesktopDialogField("diceInitiativeDice", "Initiative Dice", "1", "1", InputType: "number"),
            new DesktopDialogField("diceCurrentPass", "Current Pass", "1", "1", InputType: "number"),
            new DesktopDialogField("diceUtilityLane", "Utility Lane", "ruleset-backed roll + initiative preview", "ruleset-backed roll + initiative preview", IsReadOnly: true),
            new DesktopDialogField("diceRosterContext", "Roster Context", rosterContext, rosterContext, IsReadOnly: true),
            new DesktopDialogField("initiativePreview", "Initiative Preview", BuildInitiativePreview(10, 1, 0, 1), BuildInitiativePreview(10, 1, 0, 1), IsReadOnly: true)
        ];
    }

    internal static DesktopDialogState BuildGlobalSettingsDialog(
        DesktopPreferenceState preferences,
        string language,
        string? activePane = null)
    {
        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(language);
        string pane = NormalizeGlobalSettingsPane(activePane);
        string S(string key) => DesktopLocalizationCatalog.GetRequiredString(key, normalizedLanguage);
        string F(string key, params object[] values) => DesktopLocalizationCatalog.GetRequiredFormattedString(key, normalizedLanguage, values);

        return new DesktopDialogState(
            "dialog.global_settings",
            S("desktop.dialog.global_settings.title"),
            F("desktop.dialog.global_settings.message", DesktopLocalizationCatalog.BuildSupportedLanguageSummary()),
            BuildGlobalSettingsFields(preferences, normalizedLanguage, S, pane),
            [
                new DesktopDialogAction("apply", "Apply"),
                new DesktopDialogAction("save", S("desktop.dialog.action.save"), true),
                new DesktopDialogAction("cancel", S("desktop.dialog.action.cancel"))
            ]);
    }

    internal static DesktopPreferenceState ParseGlobalSettingsPreferences(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
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
            UpdateChannel = DesktopDialogFieldValueParser.GetValue(dialog, "globalUpdatePolicy") ?? fallback.UpdateChannel,
            CheckForUpdatesOnLaunch = DesktopDialogFieldValueParser.ParseBool(dialog, "globalCheckForUpdates", fallback.CheckForUpdatesOnLaunch),
            CharacterRosterPath = DesktopDialogFieldValueParser.GetValue(dialog, "globalCharacterRosterPath") ?? fallback.CharacterRosterPath,
            PdfViewerPath = DesktopDialogFieldValueParser.GetValue(dialog, "globalPdfViewerPath") ?? fallback.PdfViewerPath,
            VisibleChromePolicy = DesktopDialogFieldValueParser.GetValue(dialog, "globalVisibilityPolicy") ?? fallback.VisibleChromePolicy
        });
    }

    internal static DesktopDialogState RebuildGlobalSettingsDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        DesktopPreferenceState parsedPreferences = ParseGlobalSettingsPreferences(dialog, fallback);
        string activePane = ReadGlobalSettingsActivePane(dialog);
        return BuildGlobalSettingsDialog(parsedPreferences, parsedPreferences.Language, activePane);
    }

    internal static DesktopDialogState RebuildDynamicDialog(
        DesktopDialogState dialog,
        DesktopPreferenceState fallback)
    {
        if (string.Equals(dialog.Id, "dialog.global_settings", StringComparison.Ordinal))
            return RebuildGlobalSettingsDialog(dialog, fallback);

        return dialog.Id switch
        {
            "dialog.ui.cyberware_add" => RebuildCyberwareSelectionDialog(dialog),
            "dialog.ui.gear_add" => RebuildGearSelectionDialog(dialog),
            "dialog.ui.combat_add_weapon" => RebuildWeaponSelectionDialog(dialog),
            "dialog.ui.combat_add_armor" => RebuildArmorSelectionDialog(dialog),
            "dialog.ui.vehicle_add" => RebuildVehicleSelectionDialog(dialog),
            "dialog.ui.cyberware_edit" => RebuildCyberwareEditDialog(dialog),
            "dialog.ui.gear_edit" => RebuildGearEditDialog(dialog),
            "dialog.ui.vehicle_edit" => RebuildVehicleEditDialog(dialog),
            _ => dialog
        };
    }

    internal static string ReadGlobalSettingsActivePane(DesktopDialogState dialog)
        => NormalizeGlobalSettingsPane(DesktopDialogFieldValueParser.GetValue(dialog, "globalActivePane"));

    private static string NormalizeGlobalSettingsPane(string? activePane)
        => string.Equals(activePane, "sourcebooks", StringComparison.OrdinalIgnoreCase) ? "sourcebooks"
            : string.Equals(activePane, "updates", StringComparison.OrdinalIgnoreCase) ? "updates"
            : string.Equals(activePane, "paths", StringComparison.OrdinalIgnoreCase) ? "paths"
            : "general";

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
        OpenWorkspaceState[] savedCandidates = ordered.Where(candidate => candidate.HasSavedWorkspace).ToArray();
        int watchedCount = savedCandidates.Length;
        OpenWorkspaceState? selectedRunner = ordered.FirstOrDefault(candidate => currentWorkspace is not null
            && string.Equals(candidate.Id.Value, currentWorkspace.Value.Value, StringComparison.Ordinal))
            ?? ordered.FirstOrDefault();
        string rulesetMix = ordered.Length == 0
            ? "(none)"
            : string.Join(", ", ordered
                .Select(candidate => RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId)
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.Ordinal));
        string rosterEntries = ordered.Length == 0
            ? $"{alias} · {name} · {(string.IsNullOrWhiteSpace(workspace) ? "(no workspace)" : workspace)}"
            : string.Join(
                Environment.NewLine,
                ordered.Select(candidate =>
                    $"{(selectedRunner is not null && string.Equals(candidate.Id.Value, selectedRunner.Id.Value, StringComparison.Ordinal) ? ">" : " ")} {candidate.Alias} · {candidate.Name} · {(RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId)} · {(candidate.HasSavedWorkspace ? "saved" : "unsaved")} · opened {candidate.LastOpenedUtc:MM-dd HH:mm} UTC"));
        string watchFolderTree = !watchFolderConfigured
            ? "└─ not configured"
            : savedCandidates.Length == 0
                ? $"└─ {rosterPath}{Environment.NewLine}   └─ no saved runners staged yet"
                : $"└─ {rosterPath}{Environment.NewLine}{string.Join(Environment.NewLine, savedCandidates.Select((candidate, index) => $"{(index == savedCandidates.Length - 1 ? "   └─ " : "   ├─ ")}{candidate.Alias} · {candidate.Name} · saved workspace"))}";
        string rosterTree = ordered.Length == 0
            ? $"[Open Characters]{Environment.NewLine}└─ {alias} · {name}{Environment.NewLine}[Watch Folder]{Environment.NewLine}{watchFolderTree}"
            : $"[Open Characters]{Environment.NewLine}{string.Join(Environment.NewLine, ordered.Select(candidate => $"└─ {(selectedRunner is not null && string.Equals(candidate.Id.Value, selectedRunner.Id.Value, StringComparison.Ordinal) ? "*" : "-")} {candidate.Alias} · {candidate.Name} [{(RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId)}]"))}{Environment.NewLine}[Watch Folder]{Environment.NewLine}{watchFolderTree}";
        string selectedRunnerSummary = selectedRunner is null
            ? BuildGridValue(
                ("Character Name", name),
                ("Alias", alias),
                ("Player", "Local profile"),
                ("Metatype", "Unavailable in roster summary"),
                ("Career Karma", "Unavailable in roster summary"),
                ("Essence", "Unavailable in roster summary"),
                ("File Name", string.IsNullOrWhiteSpace(workspace) ? "(no workspace)" : workspace),
                ("Settings File", "default roster setting"))
            : BuildGridValue(
                ("Character Name", selectedRunner.Name),
                ("Alias", selectedRunner.Alias),
                ("Player", "Local profile"),
                ("Metatype", RulesetDefaults.NormalizeOptional(selectedRunner.RulesetId) ?? selectedRunner.RulesetId),
                ("Career Karma", selectedRunner.HasSavedWorkspace ? "Saved runner" : "Unsaved runner"),
                ("Essence", "Unavailable in roster summary"),
                ("File Name", selectedRunner.Id.Value),
                ("Settings File", $"{(RulesetDefaults.NormalizeOptional(selectedRunner.RulesetId) ?? selectedRunner.RulesetId)} roster setting"));
        string selectedRunnerBackground = selectedRunner is null
            ? "Background details appear after a runner is opened from the roster."
            : $"Description: {selectedRunner.Name} is staged as the active roster runner.{Environment.NewLine}Concept: Dense-workbench veteran entry with compact desktop follow-through.{Environment.NewLine}Background: {(RulesetDefaults.NormalizeOptional(selectedRunner.RulesetId) ?? selectedRunner.RulesetId)} is surfaced directly on the roster.";
        string selectedRunnerNotes = selectedRunner is null
            ? "Notes and session comments appear after a runner is opened from the roster."
            : $"Character Notes: Keep the runner tabs for full editing; the roster stays dense-workbench friendly and navigation-first.{Environment.NewLine}Game Notes: {(selectedRunner.HasSavedWorkspace ? "Saved workspace is present." : "Runner has not been saved yet.")}{Environment.NewLine}Watch posture: Current runner stays selected in the roster.";
        string selectedRunnerStatus = selectedRunner is null
            ? "No runner selected."
            : $"Opened {selectedRunner.LastOpenedUtc:yyyy-MM-dd HH:mm} UTC · {(selectedRunner.HasSavedWorkspace ? "saved to disk" : "not saved yet")} · active ruleset {(RulesetDefaults.NormalizeOptional(selectedRunner.RulesetId) ?? selectedRunner.RulesetId)}";
        string portraitCandidate = BuildRosterPortraitCandidatePath(rosterPath, selectedRunner, alias, name, workspace);
        string selectionTrail = selectedRunner is null
            ? BuildGridValue(
                ("Active Runner", $"{alias} · {name}"),
                ("Save Posture", string.IsNullOrWhiteSpace(workspace) ? "not saved yet" : "workspace available"),
                ("Watch Folder", watchFolderConfigured ? rosterPath : "not configured"))
            : BuildGridValue(
                ("Active Runner", $"{selectedRunner.Alias} · {selectedRunner.Name}"),
                ("Save Posture", selectedRunner.HasSavedWorkspace ? "saved to disk" : "not saved yet"),
                ("Watch Folder", watchFolderConfigured ? rosterPath : "not configured"));
        string watchFolderStatus = BuildGridValue(
            ("Watch Folder", watchFolderConfigured ? rosterPath : "not configured"),
            ("Watcher", watchFolderConfigured ? "configured via global settings" : "inactive"),
            ("Watched Files", watchedCount.ToString(CultureInfo.InvariantCulture)),
            ("Saved Workspaces", savedCount.ToString(CultureInfo.InvariantCulture)),
            ("Scan Posture", watchFolderConfigured ? "manual refresh in this head" : "configure a roster folder first"));
        string runnerCommands =
            "Open selected runner" + Environment.NewLine +
            "Save selected runner" + Environment.NewLine +
            (selectedRunner?.HasSavedWorkspace == true ? "Open saved runner location" : "Save runner to roster folder");
        string watchFolderCommands = watchFolderConfigured
            ? "Open roster folder" + Environment.NewLine +
              "Refresh watched file list" + Environment.NewLine +
              "Open selected saved runner"
            : "Configure watch folder" + Environment.NewLine +
              "Scan watch folder now" + Environment.NewLine +
              "Open imported runner";
        string mugshotStatus =
            "Runner Portrait" + Environment.NewLine +
            $"{(selectedRunner?.Alias ?? alias)} · {(selectedRunner?.Name ?? name)}" + Environment.NewLine +
            $"Portrait Source | {portraitCandidate}" + Environment.NewLine +
            "Portrait Status | legacy portrait slot uses the configured roster path and awaits a real image pipeline.";

        return
        [
            new DesktopDialogField("rosterSectionTabs", "Sections", "Roster" + Environment.NewLine + "Details" + Environment.NewLine + "Background" + Environment.NewLine + "Notes", "Roster", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("rosterDetailTabs", "Runner Pages", "Description" + Environment.NewLine + "Concept" + Environment.NewLine + "Background" + Environment.NewLine + "Character Notes" + Environment.NewLine + "Game Notes", "Description", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("rosterOpenCount", "Open Runners", ordered.Length.ToString(), "0", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterSavedCount", "Saved Workspaces", savedCount.ToString(), "0", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterWatchedCount", "Watched Files", watchedCount.ToString(), "0", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterRulesetMix", "Ruleset Mix", string.IsNullOrWhiteSpace(rulesetMix) ? "(none)" : rulesetMix, "(none)", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterActiveWorkspace", "Active Workspace", currentWorkspace?.Value ?? workspace, workspace, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterOpsLane", "Operator Lane", "open runners + save posture + ruleset mix", "open runners + save posture + ruleset mix", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("rosterTree", "Characters", rosterTree, rosterTree, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("rosterSelectionTrail", "Selection Trail", selectionTrail, selectionTrail, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid),
            new DesktopDialogField("rosterMugshot", "Mugshot", mugshotStatus, "Runner Mugshot", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Image, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterSelectedRunner", "Selected Runner", selectedRunnerSummary, selectedRunnerSummary, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterWatchFolderStatus", "Watch Folder", watchFolderStatus, watchFolderStatus, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterRunnerCommands", "Runner Commands", runnerCommands, runnerCommands, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterWatchFolderCommands", "Watch Folder Commands", watchFolderCommands, watchFolderCommands, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("rosterSelectedRunnerStatus", "Runner Status", selectedRunnerStatus, selectedRunnerStatus, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("rosterSelectedRunnerBackground", "Background / Concept", selectedRunnerBackground, selectedRunnerBackground, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("rosterSelectedRunnerNotes", "Bio / Concept / Notes", selectedRunnerNotes, selectedRunnerNotes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("rosterEntries", "Roster Entries", rosterEntries, rosterEntries, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List)
        ];
    }

    private static string BuildRosterPortraitCandidatePath(
        string rosterPath,
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
            return $"{baseName}.png";
        }

        return Path.Combine(rosterPath, $"{baseName}.png");
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
        Func<string, string> localize,
        string activePane)
    {
        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(language);
        string normalizedSheetLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(string.IsNullOrWhiteSpace(preferences.SheetLanguage) ? normalizedLanguage : preferences.SheetLanguage);
        string pane = NormalizeGlobalSettingsPane(activePane);
        static string Marker(string targetPane, string activePaneId)
            => string.Equals(targetPane, activePaneId, StringComparison.Ordinal) ? "▶ " : string.Empty;

        string settingsTree =
            "[Global Settings]" + Environment.NewLine +
            $"├─ {Marker("general", pane)}General{Environment.NewLine}" +
            $"│  ├─ Desktop{Environment.NewLine}" +
            $"│  └─ Language{Environment.NewLine}" +
            $"├─ {Marker("sourcebooks", pane)}Sourcebooks{Environment.NewLine}" +
            $"│  └─ Build Defaults{Environment.NewLine}" +
            $"├─ {Marker("updates", pane)}Updates{Environment.NewLine}" +
            $"│  └─ Startup and Channel{Environment.NewLine}" +
            $"└─ {Marker("paths", pane)}Data Paths{Environment.NewLine}" +
            $"   └─ Roster and External Tools";
        string sections =
            $"General{Environment.NewLine}Sourcebooks{Environment.NewLine}Updates{Environment.NewLine}Data Paths";
        string legacyTopTabs =
            "Global Options" + Environment.NewLine +
            "Custom Data Directories" + Environment.NewLine +
            "GitHub Issues" + Environment.NewLine +
            "Plugins";
        string detailTabs = pane switch
        {
            "sourcebooks" => "Defaults" + Environment.NewLine + "Controls" + Environment.NewLine + "Notes",
            "updates" => "Startup" + Environment.NewLine + "Channel" + Environment.NewLine + "Notes",
            "paths" => "Roster" + Environment.NewLine + "PDFs" + Environment.NewLine + "Notes",
            _ => "Desktop" + Environment.NewLine + "Language" + Environment.NewLine + "Notes"
        };
        string detailTabDefault = pane switch
        {
            "sourcebooks" => "Defaults",
            "updates" => "Startup",
            "paths" => "Roster",
            _ => "Desktop"
        };
        string paneHeader = pane switch
        {
            "sourcebooks" => "Sourcebooks / Build Defaults",
            "updates" => "Updates / Startup",
            "paths" => "Data Paths / External Tools",
            _ => "General / Desktop Language"
        };

        string settingsGrid = pane switch
        {
            "sourcebooks" => BuildGridValue(
                ("Sourcebook Control", "Master Index / Character Settings"),
                ("Default Priority", preferences.CharacterPriority),
                ("Karma / Nuyen", preferences.KarmaNuyenRatio.ToString()),
                ("House Rules", preferences.HouseRulesEnabled ? "enabled" : "disabled"),
                ("Current Language", normalizedLanguage)),
            "updates" => BuildGridValue(
                ("Startup", preferences.StartupBehavior),
                ("Update Channel", preferences.UpdateChannel),
                ("Check on Launch", preferences.CheckForUpdatesOnLaunch ? "enabled" : "disabled"),
                ("Restart", "phase-1 language change applies on restart"),
                ("Language", normalizedLanguage)),
            "paths" => BuildGridValue(
                ("Character Roster Path", preferences.CharacterRosterPath),
                ("PDF Viewer", preferences.PdfViewerPath),
                ("Visible Heads", preferences.VisibleChromePolicy),
                ("Theme", preferences.Theme),
                ("Compact Mode", preferences.CompactMode ? "on" : "off")),
            _ => BuildGridValue(
                ("Theme", preferences.Theme),
                ("Language", normalizedLanguage),
                ("Sheet Language", normalizedSheetLanguage),
                ("Scale", $"{preferences.UiScalePercent}%"),
                ("Compact Mode", preferences.CompactMode ? "on" : "off")),
        };
        string settingsWorkflows = pane switch
        {
            "sourcebooks" => "Adjust build defaults" + Environment.NewLine + "Review sourcebook posture" + Environment.NewLine + "Return to Master Index for toggles",
            "updates" => "Choose startup behavior" + Environment.NewLine + "Set update channel" + Environment.NewLine + "Keep checks inside the desktop head",
            "paths" => "Set roster path" + Environment.NewLine + "Choose PDF helper" + Environment.NewLine + "Keep export and print routes obvious",
            _ => "Change theme and scale" + Environment.NewLine + "Set desktop language" + Environment.NewLine + "Set sheet language and compact mode"
        };
        string settingsSnippet = pane switch
        {
            "sourcebooks" => "Use this pane for rules-default posture that veterans expect near sourcebook control. Sourcebook enablement itself stays visible in Master Index and Character Settings, but the default build posture belongs here.",
            "updates" => "This pane should behave like the old utility settings form: startup and update behavior stay editable without leaving the desktop workbench or opening a browser detour.",
            "paths" => "Data paths and external helpers stay grouped here so roster/import/print workflows remain obvious and compact like the legacy utility surfaces.",
            _ => "This pane should behave like the old utility settings form: navigation stays on the left, current-pane facts stay visible on the right, and dense work continues without wasting space."
        };
        string paneTools = pane switch
        {
            "sourcebooks" => BuildGridValue(
                ("Build Defaults", "Priority + karma + house rules"),
                ("Sourcebook Control", "Master Index / Character Settings"),
                ("Toggle Posture", "governed by rules environment"),
                ("Current Language", normalizedLanguage)),
            "updates" => BuildGridValue(
                ("Startup", preferences.StartupBehavior),
                ("Update Channel", preferences.UpdateChannel),
                ("Check on Launch", preferences.CheckForUpdatesOnLaunch ? "enabled" : "disabled"),
                ("Restart Needed", "language / shell only")),
            "paths" => BuildGridValue(
                ("Character Roster Path", preferences.CharacterRosterPath),
                ("PDF Viewer", preferences.PdfViewerPath),
                ("PDF Parameters", "<page>"),
                ("PDF Offset", "0")),
            _ => BuildGridValue(
                ("Desktop Language", normalizedLanguage),
                ("Sheet Language", normalizedSheetLanguage),
                ("Theme", preferences.Theme),
                ("Scale", $"{preferences.UiScalePercent}%")),
        };
        string paneCommandList = pane switch
        {
            "sourcebooks" => "Review default priority" + Environment.NewLine + "Review karma ratio" + Environment.NewLine + "Return to source toggles",
            "updates" => "Set startup mode" + Environment.NewLine + "Choose update channel" + Environment.NewLine + "Check for updates on launch",
            "paths" => "Browse roster path" + Environment.NewLine + "Remove roster path" + Environment.NewLine + "Browse PDF application" + Environment.NewLine + "Scan folder for PDF files" + Environment.NewLine + "Test PDF helper",
            _ => "Set desktop language" + Environment.NewLine + "Set sheet language" + Environment.NewLine + "Change theme" + Environment.NewLine + "Adjust UI scale"
        };
        string restartPosture = pane switch
        {
            "updates" => "Update and startup edits apply immediately where possible; desktop language changes still call for a restart.",
            _ => "Most values apply inside the current desktop session; language and some shell chrome changes still need a restart."
        };

        string generalSlot = string.Equals(pane, "general", StringComparison.Ordinal) ? DesktopDialogFieldLayoutSlots.Full : DesktopDialogFieldLayoutSlots.Hidden;
        string sourcebooksSlot = string.Equals(pane, "sourcebooks", StringComparison.Ordinal) ? DesktopDialogFieldLayoutSlots.Full : DesktopDialogFieldLayoutSlots.Hidden;
        string updatesSlot = string.Equals(pane, "updates", StringComparison.Ordinal) ? DesktopDialogFieldLayoutSlots.Full : DesktopDialogFieldLayoutSlots.Hidden;
        string pathsSlot = string.Equals(pane, "paths", StringComparison.Ordinal) ? DesktopDialogFieldLayoutSlots.Full : DesktopDialogFieldLayoutSlots.Hidden;

        return
        [
            new DesktopDialogField("globalActivePane", "Active Pane", pane, "general", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("globalSettingsSections", "Sections", sections, "General", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("globalLegacyTabBar", "Legacy Tabs", legacyTopTabs, "Global Options", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("globalSettingsDetailTabs", "Pane Tabs", detailTabs, detailTabDefault, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("globalSettingsTree", "Navigation", settingsTree, "[Settings]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("globalSettingsPropertyGrid", "Current Pane", settingsGrid, settingsGrid, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("globalPaneTools", "Pane Tools", paneTools, paneTools, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("globalCurrentPaneHeader", "Pane Header", paneHeader, paneHeader, IsReadOnly: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("globalCurrentPaneWorkflows", "Workflow Checklist", settingsWorkflows, settingsWorkflows, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List),
            new DesktopDialogField("globalPaneCommandList", "Pane Commands", paneCommandList, paneCommandList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List),

            new DesktopDialogField("globalTheme", localize("desktop.dialog.global_settings.field.theme"), preferences.Theme, "classic", LayoutSlot: generalSlot),
            new DesktopDialogField("globalUiScale", localize("desktop.dialog.global_settings.field.ui_scale"), preferences.UiScalePercent.ToString(), "100", InputType: "number", LayoutSlot: generalSlot),
            new DesktopDialogField("globalLanguage", localize("desktop.dialog.global_settings.field.language"), normalizedLanguage, DesktopLocalizationCatalog.DefaultLanguage, LayoutSlot: generalSlot),
            new DesktopDialogField("globalSheetLanguage", "Sheet Language", normalizedSheetLanguage, normalizedSheetLanguage, LayoutSlot: generalSlot),
            new DesktopDialogField("globalCompactMode", localize("desktop.dialog.global_settings.field.compact_mode"), preferences.CompactMode ? "true" : "false", "false", InputType: "checkbox", LayoutSlot: generalSlot),

            new DesktopDialogField("globalCharacterPriority", "Default Priority", preferences.CharacterPriority, DesktopPreferenceState.Default.CharacterPriority, LayoutSlot: sourcebooksSlot),
            new DesktopDialogField("globalKarmaNuyenRatio", "Karma / Nuyen Ratio", preferences.KarmaNuyenRatio.ToString(), DesktopPreferenceState.Default.KarmaNuyenRatio.ToString(), InputType: "number", LayoutSlot: sourcebooksSlot),
            new DesktopDialogField("globalHouseRulesEnabled", "Enable House Rules", preferences.HouseRulesEnabled ? "true" : "false", "false", InputType: "checkbox", LayoutSlot: sourcebooksSlot),
            new DesktopDialogField("globalSourcebookControl", "Sourcebook Control", "Use Master Index / Character Settings for sourcebook toggles.", "Use Master Index / Character Settings for sourcebook toggles.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: sourcebooksSlot),

            new DesktopDialogField("globalStartupBehavior", "Startup", preferences.StartupBehavior, DesktopPreferenceState.Default.StartupBehavior, LayoutSlot: updatesSlot),
            new DesktopDialogField("globalUpdatePolicy", "Updates", preferences.UpdateChannel, DesktopPreferenceState.Default.UpdateChannel, LayoutSlot: updatesSlot),
            new DesktopDialogField("globalCheckForUpdates", "Check for updates on launch", preferences.CheckForUpdatesOnLaunch ? "true" : "false", "true", InputType: "checkbox", LayoutSlot: updatesSlot),

            new DesktopDialogField("globalCharacterRosterPath", "Character Roster Path", preferences.CharacterRosterPath, DesktopPreferenceState.Default.CharacterRosterPath, LayoutSlot: pathsSlot),
            new DesktopDialogField("globalPdfViewerPath", "PDF Viewer", preferences.PdfViewerPath, DesktopPreferenceState.Default.PdfViewerPath, LayoutSlot: pathsSlot),
            new DesktopDialogField("globalVisibilityPolicy", "Visible Heads", preferences.VisibleChromePolicy, DesktopPreferenceState.Default.VisibleChromePolicy, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: pathsSlot),

            new DesktopDialogField("globalCurrentPaneNotes", "Current Pane Notes", settingsSnippet, settingsSnippet, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("globalRestartPosture", "Restart Posture", restartPosture, restartPosture, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

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
        => string.Create(CultureInfo.InvariantCulture, $"¥{decimal.Round(value, 0):N0}");

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

    private static DesktopDialogState RebuildCyberwareSelectionDialog(DesktopDialogState dialog)
    {
        string grade = DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareGrade") ?? "Standard";
        decimal markupPercent = ParseDecimalField(dialog, "uiCyberwareMarkup", 0m);
        bool blackMarket = DesktopDialogFieldValueParser.ParseBool(dialog, "uiCyberwareBlackMarketDiscount", false);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiCyberwareHideOverAvailLimit", true);
        bool hideBannedGrades = DesktopDialogFieldValueParser.ParseBool(dialog, "uiCyberwareHideBannedGrades", true);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiCyberwareBookFilter") ?? "Core Rulebook";

        int shown = 9;
        if (hideOverAvail)
            shown -= 2;
        if (hideBannedGrades)
            shown -= 1;
        if (!string.Equals(dataFile, "All Books", StringComparison.Ordinal))
            shown -= 3;
        shown = Math.Max(1, shown);

        decimal baseCost = 149000m;
        decimal cost = baseCost * ResolveGradeCostMultiplier(grade) * (1m + (markupPercent / 100m));
        if (blackMarket)
            cost *= 0.9m;
        decimal essence = 3.0m * ResolveGradeEssenceMultiplier(grade);

        string details = BuildGridValue(
            ("Selected", "Wired Reflexes 2"),
            ("Grade", grade),
            ("Availability", "12R"),
            ("Cost", FormatNuyen(cost)),
            ("Essence", essence.ToString("0.00", CultureInfo.InvariantCulture)),
            ("Capacity", "n/a"),
            ("Book", dataFile));
        string filterSummary = $"Filtered Catalog | {shown} shown / 9 total{Environment.NewLine}Category Path | Cyberware > Bodyware{Environment.NewLine}Filter Posture | grade, availability, and source stay live";
        string liveRecalc = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Recalculated Essence", essence.ToString("0.00", CultureInfo.InvariantCulture)),
            ("Black Market", blackMarket ? "Yes" : "No"),
            ("Add Again", "Stays open"));

        return ReplaceDialogField(
            ReplaceDialogField(
                ReplaceDialogField(dialog, "uiCyberwareSelectionDetails", details),
                "uiCyberwareFilterSummary",
                filterSummary),
            "uiCyberwareLiveRecalc",
            liveRecalc);
    }

    private static DesktopDialogState RebuildGearSelectionDialog(DesktopDialogState dialog)
    {
        decimal markupPercent = ParseDecimalField(dialog, "uiGearMarkup", 0m);
        bool blackMarket = DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearBlackMarketDiscount", false);
        bool freeItem = DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearFreeItem", false);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiGearHideOverAvailLimit", true);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiGearBookFilter") ?? "All Books";

        int shown = hideOverAvail ? 6 : 8;
        if (!string.Equals(dataFile, "All Books", StringComparison.Ordinal))
            shown -= 3;
        shown = Math.Max(1, shown);

        decimal baseCost = 725m;
        decimal cost = freeItem ? 0m : baseCost * (1m + (markupPercent / 100m)) * (blackMarket ? 0.9m : 1m);
        string details = BuildGridValue(
            ("Selected", "Ares Predator V"),
            ("Category", "Firearms"),
            ("Availability", "5R"),
            ("Cost", FormatNuyen(cost)),
            ("Book", dataFile));
        string filterSummary = $"Filtered Catalog | {shown} shown / 8 total{Environment.NewLine}Category Path | Gear > Firearms{Environment.NewLine}Filter Posture | availability, source, and pricing stay live";
        string liveRecalc = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Free Item", freeItem ? "Yes" : "No"),
            ("Black Market", blackMarket ? "Yes" : "No"),
            ("Add Again", "Stays open"));

        return ReplaceDialogField(
            ReplaceDialogField(
                ReplaceDialogField(dialog, "uiGearSelectionDetails", details),
                "uiGearFilterSummary",
                filterSummary),
            "uiGearLiveRecalc",
            liveRecalc);
    }

    private static DesktopDialogState RebuildWeaponSelectionDialog(DesktopDialogState dialog)
    {
        decimal markupPercent = ParseDecimalField(dialog, "uiWeaponMarkup", 0m);
        bool blackMarket = DesktopDialogFieldValueParser.ParseBool(dialog, "uiWeaponBlackMarketDiscount", false);
        bool freeItem = DesktopDialogFieldValueParser.ParseBool(dialog, "uiWeaponFreeItem", false);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiWeaponHideOverAvailLimit", true);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiWeaponBookFilter") ?? "All Books";

        int shown = hideOverAvail ? 7 : 10;
        if (!string.Equals(dataFile, "All Books", StringComparison.Ordinal))
            shown -= 4;
        shown = Math.Max(1, shown);

        decimal baseCost = 750m;
        decimal cost = freeItem ? 0m : baseCost * (1m + (markupPercent / 100m)) * (blackMarket ? 0.9m : 1m);
        string details = BuildGridValue(
            ("Selected", "Colt M23"),
            ("Damage", "7P"),
            ("AP", "-1"),
            ("Mode", "SA"),
            ("Cost", FormatNuyen(cost)),
            ("Book", dataFile));
        string filterSummary = $"Filtered Catalog | {shown} shown / 10 total{Environment.NewLine}Category Path | Weapons > Heavy Pistols{Environment.NewLine}Filter Posture | availability, discounts, and source stay live";
        string liveRecalc = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Accuracy", "5"),
            ("Black Market", blackMarket ? "Yes" : "No"),
            ("Add Again", "Stays open"));

        return ReplaceDialogField(
            ReplaceDialogField(
                ReplaceDialogField(dialog, "uiWeaponSelectionDetails", details),
                "uiWeaponFilterSummary",
                filterSummary),
            "uiWeaponLiveRecalc",
            liveRecalc);
    }

    private static DesktopDialogState RebuildArmorSelectionDialog(DesktopDialogState dialog)
    {
        decimal markupPercent = ParseDecimalField(dialog, "uiArmorMarkup", 0m);
        bool freeItem = DesktopDialogFieldValueParser.ParseBool(dialog, "uiArmorFreeItem", false);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiArmorHideOverAvailLimit", true);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiArmorBookFilter") ?? "All Books";

        int shown = hideOverAvail ? 5 : 7;
        if (!string.Equals(dataFile, "All Books", StringComparison.Ordinal))
            shown -= 2;
        shown = Math.Max(1, shown);

        decimal baseCost = 1000m;
        decimal cost = freeItem ? 0m : baseCost * (1m + (markupPercent / 100m));
        string details = BuildGridValue(
            ("Selected", "Armor Jacket"),
            ("Armor", "12"),
            ("Availability", "12"),
            ("Capacity", "n/a"),
            ("Cost", FormatNuyen(cost)),
            ("Book", dataFile));
        string filterSummary = $"Filtered Catalog | {shown} shown / 7 total{Environment.NewLine}Category Path | Armor > Armor{Environment.NewLine}Filter Posture | availability, source, and markup stay live";
        string liveRecalc = BuildGridValue(
            ("Recalculated Cost", FormatNuyen(cost)),
            ("Armor", "12"),
            ("Free Item", freeItem ? "Yes" : "No"),
            ("Add Again", "Stays open"));

        return ReplaceDialogField(
            ReplaceDialogField(
                ReplaceDialogField(dialog, "uiArmorSelectionDetails", details),
                "uiArmorFilterSummary",
                filterSummary),
            "uiArmorLiveRecalc",
            liveRecalc);
    }

    private static DesktopDialogState RebuildVehicleSelectionDialog(DesktopDialogState dialog)
    {
        bool showDrones = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleShowDrones", true);
        bool hideOverAvail = DesktopDialogFieldValueParser.ParseBool(dialog, "uiVehicleHideOverAvailLimit", true);
        string dataFile = DesktopDialogFieldValueParser.GetValue(dialog, "uiVehicleBookFilter") ?? "Core Rulebook";

        int shown = 8;
        if (hideOverAvail)
            shown -= 2;
        if (!showDrones)
            shown -= 3;
        if (!string.Equals(dataFile, "All Books", StringComparison.Ordinal))
            shown -= 1;
        shown = Math.Max(1, shown);

        string details = BuildGridValue(
            ("Selected", "Hyundai Shin-Hyung"),
            ("Role", showDrones ? "Vehicle / Drone Catalog" : "Vehicle"),
            ("Handling", "4"),
            ("Armor", "8"),
            ("Source", "Core Rulebook p. 465"),
            ("Book", dataFile));
        string filterSummary = $"Filtered Catalog | {shown} shown / 8 total{Environment.NewLine}Category Path | Vehicles > Cars{Environment.NewLine}Filter Posture | vehicle/drone and availability stay live";
        string liveRecalc = BuildGridValue(
            ("Selected Cost", FormatNuyen(16000m)),
            ("Show Drones", showDrones ? "Yes" : "No"),
            ("Availability Filter", hideOverAvail ? "On" : "Off"),
            ("Add Again", "Stays open"));

        return ReplaceDialogField(
            ReplaceDialogField(
                ReplaceDialogField(dialog, "uiVehicleSelectionDetails", details),
                "uiVehicleFilterSummary",
                filterSummary),
            "uiVehicleLiveRecalc",
            liveRecalc);
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
        return new DesktopDialogField(
            id,
            label,
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

    private static IReadOnlyList<DesktopDialogField> BuildCyberwareSelectionFields()
    {
        string categoryTree =
            "[Cyberware]" + Environment.NewLine +
            "├─ Bodyware" + Environment.NewLine +
            "├─ Headware" + Environment.NewLine +
            "├─ Cyberlimbs" + Environment.NewLine +
            "└─ Accessories";
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
        string selectionTrailPath = "Cyberware > Bodyware > Wired Reflexes";
        string notes =
            "Grade modifiers, essence/cost deltas, and source details are surfaced here before the implant is added." + Environment.NewLine +
            "Grade, book, and availability filters stay visible like the old selection form while Add & More remains available.";

        return
        [
            BuildSelectionSectionsField("uiCyberwareSections"),
            BuildSelectionTreeField("uiCyberwareCategoryTree", "Navigation", categoryTree),
            new DesktopDialogField("uiCyberwareSearch", "Search", string.Empty, "Search cyberware"),
            new DesktopDialogField("uiCyberwareCategory", "Category", "Bodyware", "Bodyware"),
            new DesktopDialogField("uiCyberwareBookFilter", "Data File", "Core Rulebook", "Core Rulebook"),
            new DesktopDialogField("uiCyberwareName", "Cyberware", "Wired Reflexes 2", "Wired Reflexes 2"),
            new DesktopDialogField("uiCyberwareCandidateList", "Available Cyberware", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
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
                "Move the tree without losing grade or availability posture",
                "Review suites and accessories after picking the base implant",
                "Keep source and category scope visible while browsing"),
            new DesktopDialogField("uiCyberwareFilterSummary", "Filter Summary", "Filtered Catalog | 3 shown / 9 total" + Environment.NewLine + "Category Path | Cyberware > Bodyware" + Environment.NewLine + "Filter Posture | grade, availability, and source stay live", "Filtered Catalog | 3 shown / 9 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
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
            new DesktopDialogField("uiCyberwareEditName", "Cyberware", "Cybereyes Rating 4", "Cybereyes Rating 4"),
            new DesktopDialogField("uiCyberwareEditCategory", "Category", "Headware", "Headware", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareEditGrade", "Grade", "Standard", "Standard"),
            new DesktopDialogField("uiCyberwareEditRating", "Rating", "4", "4", InputType: "number"),
            new DesktopDialogField("uiCyberwareEditCost", "Cost", "16000", "16000", InputType: "number"),
            new DesktopDialogField("uiCyberwareEditEssence", "Essence", "0.40", "0.40", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareEditSource", "Source", "Core Rulebook p. 455", "Core Rulebook p. 455", IsReadOnly: true),
            new DesktopDialogField("uiCyberwareEditDetails", "Implant Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiCyberwareEditLiveSummary", "Live Summary", "Recalculated Cost | ¥16,000" + Environment.NewLine + "Recalculated Essence | 0.40" + Environment.NewLine + "Posture | legacy edit utility" + Environment.NewLine + "Follow-through | use implant tabs for payloads", "Recalculated Cost | ¥16,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiCyberwareEditNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildGearSelectionFields()
    {
        string categoryTree =
            "[Gear]" + Environment.NewLine +
            "├─ Firearms" + Environment.NewLine +
            "├─ Armor" + Environment.NewLine +
            "├─ Electronics" + Environment.NewLine +
            "└─ General";
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
        string selectionTrailPath = "Gear > Firearms > Pistols";

        return
        [
            BuildSelectionSectionsField("uiGearSections"),
            BuildSelectionTreeField("uiGearCategoryTree", "Navigation", categoryTree),
            new DesktopDialogField("uiGearSearch", "Search", string.Empty, "Search gear"),
            new DesktopDialogField("uiGearCategory", "Category", "All", "All"),
            new DesktopDialogField("uiGearBookFilter", "Data File", "All Books", "All Books"),
            new DesktopDialogField("uiGearName", "Gear Name", "Ares Predator V", "Ares Predator V"),
            new DesktopDialogField("uiGearCandidateList", "Available Gear", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiGearHideOverAvailLimit", "Hide over Availability", true),
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
            BuildSelectionTrailField("uiGearSelectionTrail", selectionTrailPath, "Ares Predator V", "Stack and discount posture stay live"),
            BuildSelectionCommandsField("uiGearCategoryCommands", "Category Commands",
                "Move the tree without losing source or legality posture",
                "Keep Do It Yourself and Stack visible while browsing",
                "Review accessories after locking the base item"),
            new DesktopDialogField("uiGearFilterSummary", "Filter Summary", "Filtered Catalog | 6 shown / 8 total" + Environment.NewLine + "Category Path | Gear > Firearms" + Environment.NewLine + "Filter Posture | availability, source, and pricing stay live", "Filtered Catalog | 6 shown / 8 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiGearLiveRecalc", "Live Recalculation", "Recalculated Cost | ¥725" + Environment.NewLine + "Free Item | No" + Environment.NewLine + "Black Market | No" + Environment.NewLine + "Add Again | Stays open", "Recalculated Cost | ¥725", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiGearResultCommands", "Result Commands",
                "Compare cost, rating, and legality on the right before adding",
                "Use OK for one add or Add & More to keep shopping",
                "Keep markup, quantity, and source visible through confirmation"),
            new DesktopDialogField("uiGearNotes", "Notes", "Use gear details to confirm legality, source, rating, and discount posture before adding.", "Use gear details to confirm legality, source, rating, and discount posture before adding.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
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
            "Edit quantity, rating, and cost adjustments while keeping the legacy summary posture visible." + Environment.NewLine +
            "Use the runner gear tabs for nested accessories after confirming the base item.";

        return
        [
            BuildSelectionSectionsField("uiGearEditSections"),
            new DesktopDialogField("uiGearEditName", "Gear Name", "Armor Jacket", "Armor Jacket"),
            new DesktopDialogField("uiGearEditCategory", "Category", "Armor", "Armor", IsReadOnly: true),
            new DesktopDialogField("uiGearEditRating", "Rating", "0", "0", InputType: "number"),
            new DesktopDialogField("uiGearEditQuantity", "Quantity", "1", "1", InputType: "number"),
            new DesktopDialogField("uiGearEditCost", "Cost", "1000", "1000", InputType: "number"),
            new DesktopDialogField("uiGearEditSource", "Source", "Core Rulebook p. 437", "Core Rulebook p. 437", IsReadOnly: true),
            new DesktopDialogField("uiGearEditDetails", "Item Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiGearEditLiveSummary", "Live Summary", "Total Cost | ¥1,000" + Environment.NewLine + "Wireless | n/a" + Environment.NewLine + "Legality | Restricted carry not required" + Environment.NewLine + "Posture | legacy edit utility", "Total Cost | ¥1,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
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
            BuildSelectionTreeField("uiMagicCategoryTree", "Navigation", categoryTree),
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
            BuildSelectionTreeField("uiSpellCategoryTree", "Navigation", categoryTree),
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
            BuildSelectionTreeField("uiAdeptPowerCategoryTree", "Navigation", categoryTree),
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
            BuildSelectionTreeField("uiDrugCategoryTree", "Navigation", categoryTree),
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
            "Review parent section totals" + Environment.NewLine +
            "Re-open the same picker family" + Environment.NewLine +
            "Return to the current workbench tab";

        return
        [
            BuildUtilitySectionsField("uiDeleteSections", "Target", "Impact", "Notes"),
            new DesktopDialogField("uiDeleteNavigationTree", "Navigation", navigationTree, navigationTree, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteNeighborList", "Current List", nearbyEntries, nearbyEntries, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiDeleteTarget", "Selected Item", entityName, entityName, IsReadOnly: true),
            new DesktopDialogField("uiDeleteSummary", "Details", NormalizeGridValue(summary), NormalizeGridValue(summary), IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteImpact", "Impact", "Removal Scope | current runner only" + Environment.NewLine + "Undo Posture | re-add manually from the same utility family" + Environment.NewLine + "Neighbor Context | surrounding list remains in view", "Removal Scope | current runner only", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteRecoveryCommands", "Recovery", recoveryCommands, recoveryCommands, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiDeleteNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
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
            BuildSelectionTreeField("uiComplexFormCategoryTree", "Navigation", categoryTree),
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
            BuildSelectionTreeField("uiMatrixProgramCategoryTree", "Navigation", categoryTree),
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
            BuildSelectionTreeField("uiSkillCategoryTree", "Navigation", categoryTree),
            new DesktopDialogField("uiSkillSearch", "Search", string.Empty, "Search skills"),
            new DesktopDialogField("uiSkillCategory", "Category", "Active", "Active"),
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
            new DesktopDialogField("uiSkillSpecializationNotes", "Notes", "Skill, existing specialization posture, and linked attribute remain visible before applying the specialization.", "Skill, existing specialization posture, and linked attribute remain visible before applying the specialization.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
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
            BuildSelectionTreeField("uiInitiationCategoryTree", "Navigation", categoryTree),
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
            BuildSelectionTreeField("uiSpiritCategoryTree", "Navigation", categoryTree),
            new DesktopDialogField("uiSpiritSearch", "Search", string.Empty, "Search spirits"),
            new DesktopDialogField("uiSpiritType", "Type", "Spirit", "Spirit"),
            new DesktopDialogField("uiSpiritName", "Name", "Watcher Spirit", "Watcher Spirit"),
            new DesktopDialogField("uiSpiritCandidateList", "Available Entries", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("uiSpiritForce", "Force", "3", "3", InputType: "number"),
            new DesktopDialogField("uiSpiritSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiSpiritNotes", "Notes", "Type, force, and source remain visible before confirmation.", "Type, force, and source remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
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
            BuildSelectionTreeField("uiCritterPowerCategoryTree", "Navigation", categoryTree),
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
            ("Reference posture", "canonical flagship route"));

        return
        [
            BuildUtilitySectionsField("uiSourceSections", "Source", "Details", "Notes"),
            new DesktopDialogField("uiSourceBook", "Book", "Core Rulebook", "Core Rulebook", IsReadOnly: true),
            new DesktopDialogField("uiSourcePage", "Page", "424", "424", IsReadOnly: true),
            new DesktopDialogField("uiSourceDetails", "Source Details", sourceDetails, sourceDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiSourceNotes", "Notes", "Source references stay compact and copyable without pushing the runner workbench off screen.", "Source references stay compact and copyable without pushing the runner workbench off screen.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
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
            new DesktopDialogField("uiEntryCommandList", "Command Posture", commandList, commandList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField(isEdit ? "uiEditEntryName" : "uiCreateEntryName", "Entry Name", currentValue, currentValue),
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
        string autoApply = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_UPDATE_AUTO_APPLY") ?? "true";
        string details = BuildGridValue(
            ("Manifest", manifest),
            ("Auto Apply", autoApply),
            ("Support Path", "/account/support"));

        return
        [
            BuildUtilitySectionsField("updateSections", "Channel", "Details", "Notes"),
            new DesktopDialogField("updateManifest", "Manifest", manifest, "unset", IsReadOnly: true),
            new DesktopDialogField("updateAutoApply", "Auto apply", autoApply, "true", IsReadOnly: true),
            new DesktopDialogField("updateSupportPath", "Support after update", "/account/support", "/account/support", IsReadOnly: true),
            new DesktopDialogField("updateDetails", "Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("updateNotes", "Notes", "Channel, manifest, and support route remain visible while update posture is reviewed.", "Channel, manifest, and support route remain visible while update posture is reviewed.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildActionReceiptFields(string actionLabel, string details, string notes)
    {
        return
        [
            BuildUtilitySectionsField("uiActionSections", "Action", "Impact", "Notes"),
            new DesktopDialogField("uiActionLabel", "Action", actionLabel, actionLabel, IsReadOnly: true),
            new DesktopDialogField("uiActionDetails", "Details", NormalizeGridValue(details), NormalizeGridValue(details), IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiActionImpact", "Impact", "List Context | preserved" + Environment.NewLine + "Work rhythm | compact classic utility" + Environment.NewLine + "Next step | continue in the same section", "List Context | preserved", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiActionNotes", "Notes", notes, notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildVehicleSelectionFields()
    {
        string categoryTree =
            "[Vehicles]" + Environment.NewLine +
            "├─ Cars" + Environment.NewLine +
            "├─ Trucks" + Environment.NewLine +
            "├─ Bikes" + Environment.NewLine +
            "└─ Drones";
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
        string selectionTrailPath = "Vehicles > Cars > Hyundai Shin-Hyung";

        return
        [
            BuildSelectionSectionsField("uiVehicleSections"),
            BuildSelectionTreeField("uiVehicleCategoryTree", "Navigation", categoryTree),
            new DesktopDialogField("uiVehicleSearch", "Search", string.Empty, "Search vehicles"),
            new DesktopDialogField("uiVehicleRole", "Role", "Vehicle", "Vehicle"),
            new DesktopDialogField("uiVehicleBookFilter", "Data File", "Core Rulebook", "Core Rulebook"),
            new DesktopDialogField("uiVehicleName", "Vehicle", "Hyundai Shin-Hyung", "Hyundai Shin-Hyung"),
            new DesktopDialogField("uiVehicleCandidateList", "Available Vehicles", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiVehicleShowDrones", "Show Drones", true),
            BuildFilterToggleField("uiVehicleHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiVehicleUsedVehicle", "Used Vehicle", false),
            new DesktopDialogField("uiVehicleUsedVehicleDiscount", "Used Vehicle Discount %", "25.00", "25.00", InputType: "number"),
            new DesktopDialogField("uiVehicleHandling", "Handling", "4", "4", InputType: "number"),
            new DesktopDialogField("uiVehicleCost", "Cost", "16000", "16000", IsReadOnly: true),
            new DesktopDialogField("uiVehicleSource", "Source", "Core Rulebook p. 465", "Core Rulebook p. 465", IsReadOnly: true),
            new DesktopDialogField("uiVehicleSelectionDetails", "Selection Details", selectionDetails, selectionDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiVehicleSelectionTrail", selectionTrailPath, "Hyundai Shin-Hyung", "Used-vehicle and drone posture stay live"),
            BuildSelectionCommandsField("uiVehicleCategoryCommands", "Category Commands",
                "Move between chassis and drone branches without losing live filters",
                "Keep used-vehicle and availability posture visible while browsing",
                "Review mod follow-through after choosing the chassis"),
            new DesktopDialogField("uiVehicleFilterSummary", "Filter Summary", "Filtered Catalog | 5 shown / 8 total" + Environment.NewLine + "Category Path | Vehicles > Cars" + Environment.NewLine + "Filter Posture | vehicle/drone and availability stay live", "Filtered Catalog | 5 shown / 8 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiVehicleLiveRecalc", "Live Recalculation", "Selected Cost | ¥16,000" + Environment.NewLine + "Show Drones | Yes" + Environment.NewLine + "Availability Filter | On" + Environment.NewLine + "Add Again | Stays open", "Selected Cost | ¥16,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiVehicleResultCommands", "Result Commands",
                "Compare handling, armor, and source on the right before adding",
                "Use OK for one add or Add & More to keep the selector open",
                "Keep cost and used-vehicle posture visible through confirmation"),
            new DesktopDialogField("uiVehicleNotes", "Notes", "Vehicle stats, source, and vehicle/drone filter posture remain visible before the selection is confirmed.", "Vehicle stats, source, and vehicle/drone filter posture remain visible before the selection is confirmed.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
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
            new DesktopDialogField("uiVehicleEditName", "Vehicle", "GMC Roadmaster", "GMC Roadmaster"),
            new DesktopDialogField("uiVehicleEditRole", "Role", "Truck", "Truck", IsReadOnly: true),
            new DesktopDialogField("uiVehicleEditHandling", "Handling", "3", "3", InputType: "number"),
            new DesktopDialogField("uiVehicleEditSpeed", "Speed", "4", "4", InputType: "number"),
            new DesktopDialogField("uiVehicleEditBody", "Body", "18", "18", InputType: "number"),
            new DesktopDialogField("uiVehicleEditArmor", "Armor", "16", "16", InputType: "number"),
            new DesktopDialogField("uiVehicleEditSource", "Source", "Core Rulebook p. 466", "Core Rulebook p. 466", IsReadOnly: true),
            new DesktopDialogField("uiVehicleEditDetails", "Vehicle Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiVehicleEditLiveSummary", "Live Summary", "Control Posture | manual + rigger ready" + Environment.NewLine + "Damage Soak | 34" + Environment.NewLine + "Seats | 6" + Environment.NewLine + "Posture | legacy edit utility", "Control Posture | manual + rigger ready", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
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
            BuildSelectionTreeField("uiVehicleModCategoryTree", "Navigation", categoryTree),
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
            new DesktopDialogField("uiContactName", "Name", "Dr. Mercy", "Dr. Mercy"),
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
            BuildSelectionTreeField("uiQualityCategoryTree", "Navigation", categoryTree),
            new DesktopDialogField("uiQualitySearch", "Search", string.Empty, "Search qualities"),
            new DesktopDialogField("uiQualityType", "Type", "Positive", "Positive"),
            new DesktopDialogField("uiQualityBookFilter", "Data File", "Core Rulebook", "Core Rulebook"),
            new DesktopDialogField("uiQualityName", "Quality", "First Impression", "First Impression"),
            new DesktopDialogField("uiQualityCandidateList", "Available Qualities", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiQualityMetagenicOnly", "Metagenic Only", false),
            BuildFilterToggleField("uiQualityShowNegative", "Show Negative", true),
            new DesktopDialogField("uiQualityKarma", "Karma", "11", "11", IsReadOnly: true),
            new DesktopDialogField("uiQualitySelectionDetails", "Selection Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("uiQualityNotes", "Notes", "Quality type, karma cost, source, and metagenic filters remain visible before confirmation.", "Quality type, karma cost, source, and metagenic filters remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildWeaponSelectionFields()
    {
        string categoryTree =
            "[Weapons]" + Environment.NewLine +
            "├─ Assault Rifles" + Environment.NewLine +
            "├─ Heavy Pistols" + Environment.NewLine +
            "├─ Shotguns" + Environment.NewLine +
            "└─ Melee";
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
        string selectionTrailPath = "Weapons > Heavy Pistols > Colt M23";

        return
        [
            BuildSelectionSectionsField("uiWeaponSections"),
            BuildSelectionTreeField("uiWeaponCategoryTree", "Navigation", categoryTree),
            new DesktopDialogField("uiWeaponSearch", "Search", string.Empty, "Search weapons"),
            new DesktopDialogField("uiWeaponCategory", "Category", "Firearms", "Firearms"),
            new DesktopDialogField("uiWeaponBookFilter", "Data File", "All Books", "All Books"),
            new DesktopDialogField("uiWeaponName", "Weapon", "Colt M23", "Colt M23"),
            new DesktopDialogField("uiWeaponCandidateList", "Available Weapons", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiWeaponHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiWeaponBlackMarketDiscount", "Black Market Discount", false),
            BuildFilterToggleField("uiWeaponFreeItem", "Free Item", false),
            new DesktopDialogField("uiWeaponAccuracy", "Accuracy", "5", "5", IsReadOnly: true),
            new DesktopDialogField("uiWeaponMarkup", "Markup %", "0", "0", InputType: "number"),
            new DesktopDialogField("uiWeaponCost", "Cost", "750", "750", IsReadOnly: true),
            new DesktopDialogField("uiWeaponSource", "Source", "Core Rulebook p. 424", "Core Rulebook p. 424", IsReadOnly: true),
            new DesktopDialogField("uiWeaponSelectionDetails", "Selection Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiWeaponSelectionTrail", selectionTrailPath, "Colt M23", "Add & More keeps the selector open"),
            BuildSelectionCommandsField("uiWeaponCategoryCommands", "Category Commands",
                "Move between firearm branches without losing live filters",
                "Keep availability and discount posture visible while browsing",
                "Review accessories and ammo follow-through after choosing the base weapon"),
            new DesktopDialogField("uiWeaponFilterSummary", "Filter Summary", "Filtered Catalog | 7 shown / 10 total" + Environment.NewLine + "Category Path | Weapons > Heavy Pistols" + Environment.NewLine + "Filter Posture | availability, discounts, and source stay live", "Filtered Catalog | 7 shown / 10 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiWeaponLiveRecalc", "Live Recalculation", "Recalculated Cost | ¥750" + Environment.NewLine + "Accuracy | 5" + Environment.NewLine + "Black Market | No" + Environment.NewLine + "Add Again | Stays open", "Recalculated Cost | ¥750", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiWeaponResultCommands", "Result Commands",
                "Compare damage, AP, and source on the right before adding",
                "Use OK for one add or Add & More to keep the selector open",
                "Keep markup and legality posture visible through confirmation"),
            new DesktopDialogField("uiWeaponNotes", "Notes", "Damage, AP, firing mode, source, and pricing filters remain visible before confirmation.", "Damage, AP, firing mode, source, and pricing filters remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    private static IReadOnlyList<DesktopDialogField> BuildArmorSelectionFields()
    {
        string categoryTree =
            "[Armor]" + Environment.NewLine +
            "├─ Armor" + Environment.NewLine +
            "├─ Clothing" + Environment.NewLine +
            "├─ Shields" + Environment.NewLine +
            "└─ PPP";
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
        string selectionTrailPath = "Armor > Armor > Armor Jacket";

        return
        [
            BuildSelectionSectionsField("uiArmorSections"),
            BuildSelectionTreeField("uiArmorCategoryTree", "Navigation", categoryTree),
            new DesktopDialogField("uiArmorSearch", "Search", string.Empty, "Search armor"),
            new DesktopDialogField("uiArmorCategory", "Category", "Armor", "Armor"),
            new DesktopDialogField("uiArmorBookFilter", "Data File", "All Books", "All Books"),
            new DesktopDialogField("uiArmorName", "Armor", "Armor Jacket", "Armor Jacket"),
            new DesktopDialogField("uiArmorCandidateList", "Available Armor", candidateList, candidateList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            BuildFilterToggleField("uiArmorHideOverAvailLimit", "Hide over Availability", true),
            BuildFilterToggleField("uiArmorFreeItem", "Free Item", false),
            new DesktopDialogField("uiArmorRating", "Armor", "12", "12", IsReadOnly: true),
            new DesktopDialogField("uiArmorMarkup", "Markup %", "0", "0", InputType: "number"),
            new DesktopDialogField("uiArmorCost", "Cost", "1000", "1000", IsReadOnly: true),
            new DesktopDialogField("uiArmorSource", "Source", "Core Rulebook p. 436", "Core Rulebook p. 436", IsReadOnly: true),
            new DesktopDialogField("uiArmorSelectionDetails", "Selection Details", details, details, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionTrailField("uiArmorSelectionTrail", selectionTrailPath, "Armor Jacket", "Source and markup stay visible through confirmation"),
            BuildSelectionCommandsField("uiArmorCategoryCommands", "Category Commands",
                "Move between armor branches without losing live filters",
                "Keep availability and free-item posture visible while browsing",
                "Review mods and accessories after selecting the base armor"),
            new DesktopDialogField("uiArmorFilterSummary", "Filter Summary", "Filtered Catalog | 5 shown / 7 total" + Environment.NewLine + "Category Path | Armor > Armor" + Environment.NewLine + "Filter Posture | availability, source, and markup stay live", "Filtered Catalog | 5 shown / 7 total", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("uiArmorLiveRecalc", "Live Recalculation", "Recalculated Cost | ¥1,000" + Environment.NewLine + "Armor | 12" + Environment.NewLine + "Free Item | No" + Environment.NewLine + "Add Again | Stays open", "Recalculated Cost | ¥1,000", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            BuildSelectionCommandsField("uiArmorResultCommands", "Result Commands",
                "Compare armor, legality, and source on the right before adding",
                "Use OK for one add or Add & More to keep browsing",
                "Keep markup and capacity posture visible through confirmation"),
            new DesktopDialogField("uiArmorNotes", "Notes", "Armor rating, legality, source, and pricing filters remain visible before confirmation.", "Armor rating, legality, source, and pricing filters remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
        ];
    }

    public DesktopDialogState CreateUiControlDialog(
        string controlId,
        DesktopPreferenceState preferences)
    {
        if (!LegacyUiControlCatalog.IsKnown(controlId))
        {
            return CreateGenericUiControlDialog(controlId);
        }

        return controlId switch
        {
            "create_entry" => new DesktopDialogState(
                "dialog.ui.create_entry",
                "Add Entry",
                "Add a new entry while keeping the compact list/detail editor posture.",
                BuildEntryEditorFields("New entry", false),
                BuildAddAndMoreActions("Add")),
            "edit_entry" => new DesktopDialogState(
                "dialog.ui.edit_entry",
                "Edit Entry",
                "Edit the selected entry in the same compact list/detail editor posture.",
                BuildEntryEditorFields("Current Entry", true),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "delete_entry" => new DesktopDialogState(
                "dialog.ui.delete_entry",
                "Delete Entry",
                "Delete selected entry?",
                BuildActionReceiptFields("Delete Entry", "Selected entry: Current Entry" + Environment.NewLine + "Operation: irreversible remove from the active list", "The selected entry will be removed from the current list context."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "open_notes" => new DesktopDialogState(
                "dialog.ui.open_notes",
                "Notes",
                "Edit runner notes in a compact text utility pane.",
                BuildNotesEditorFields(preferences.CharacterNotes),
                [
                    new DesktopDialogAction("save", "Save", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "move_up" => new DesktopDialogState(
                "dialog.ui.move_up",
                "Move Up",
                "Moved selection up.",
                BuildActionReceiptFields("Move Up", "Selected entry moved one position higher in the current ordered list.", "Ordering stays compact and list-oriented like the legacy utility flows."),
                [new DesktopDialogAction("close", "Close", true)]),
            "move_down" => new DesktopDialogState(
                "dialog.ui.move_down",
                "Move Down",
                "Moved selection down.",
                BuildActionReceiptFields("Move Down", "Selected entry moved one position lower in the current ordered list.", "Ordering stays compact and list-oriented like the legacy utility flows."),
                [new DesktopDialogAction("close", "Close", true)]),
            "toggle_free_paid" => new DesktopDialogState(
                "dialog.ui.toggle_free_paid",
                "Free/Paid",
                "Toggled free/paid state for selected item.",
                BuildActionReceiptFields("Toggle Free/Paid", "Selected item pricing posture was toggled between free and paid.", "Pricing state changes remain compact and explicit instead of disappearing into background chrome."),
                [new DesktopDialogAction("close", "Close", true)]),
            "show_source" => new DesktopDialogState(
                "dialog.ui.show_source",
                "Source",
                "Source book, page, and reference posture are surfaced in the same compact utility rhythm as classic Chummer.",
                BuildSourceDetailsFields(),
                [new DesktopDialogAction("close", "Close", true)]),
            "gear_add" => new DesktopDialogState(
                "dialog.ui.gear_add",
                "Add Gear",
                "Browse the catalog, inspect source and cost, then confirm the selected gear item.",
                BuildGearSelectionFields(),
                BuildAddAndMoreActions()),
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
                "Delete Gear",
                "Confirm removal of the selected gear item.",
                BuildDeleteConfirmationFields(
                    "Armor Jacket",
                    "Category: Armor" + Environment.NewLine + "Cost: ¥1000" + Environment.NewLine + "Source: Core Rulebook p. 437",
                    "The selected item will be removed from the runner inventory."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "gear_mount" => new DesktopDialogState(
                "dialog.ui.gear_mount",
                "Mount Gear",
                "Select the host and review the mountable gear summary before applying the change.",
                [
                    BuildUtilitySectionsField("uiGearMountSections", "Mount", "Details", "Notes"),
                    new DesktopDialogField("uiGearMountTarget", "Selected Gear", "Smartgun System", "Smartgun System", IsReadOnly: true),
                    new DesktopDialogField("uiGearMountHost", "Host", "Ares Predator V", "Ares Predator V"),
                    new DesktopDialogField("uiGearMountDetails", "Mount Details", "Selected Gear | Smartgun System" + Environment.NewLine + "Target Host | Ares Predator V" + Environment.NewLine + "Compatibility | Valid" + Environment.NewLine + "Source | Core Rulebook p. 433", "Selected Gear | Smartgun System" + Environment.NewLine + "Target Host | Ares Predator V" + Environment.NewLine + "Compatibility | Valid" + Environment.NewLine + "Source | Core Rulebook p. 433", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiGearMountNotes", "Notes", "Keep compatibility and source visible while mounting the selected gear.", "Keep compatibility and source visible while mounting the selected gear.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "gear_source" => new DesktopDialogState(
                "dialog.ui.gear_source",
                "Gear Source",
                "Source references stay visible in a compact utility pane.",
                BuildSourceDetailsFields(),
                [new DesktopDialogAction("close", "Close", true)]),
            "cyberware_add" => new DesktopDialogState(
                "dialog.ui.cyberware_add",
                "Add Cyberware",
                "Search, filter, review source/cost/essence details, and confirm the selected implant.",
                BuildCyberwareSelectionFields(),
                BuildAddAndMoreActions()),
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
                "Remove Cyberware",
                "Confirm removal of the selected implant.",
                BuildDeleteConfirmationFields(
                    "Cybereyes Rating 4",
                    "Category: Headware" + Environment.NewLine + "Essence: 0.40" + Environment.NewLine + "Source: Core Rulebook p. 455",
                    "The selected implant will be removed from the runner."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "drug_add" => new DesktopDialogState(
                "dialog.ui.drug_add",
                "Add Drug",
                "Browse drugs, inspect speed and crash posture, then confirm the selected dose.",
                BuildDrugSelectionFields(),
                BuildAddAndMoreActions()),
            "drug_delete" => new DesktopDialogState(
                "dialog.ui.drug_delete",
                "Remove Drug",
                "Confirm removal of the selected dose entry.",
                BuildDeleteConfirmationFields(
                    "Jazz",
                    "Quantity: 1" + Environment.NewLine + "Speed: 1 Combat Turn" + Environment.NewLine + "Source: Core Rulebook p. 411",
                    "The selected drug entry will be removed from the runner ledger."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "magic_add" => new DesktopDialogState(
                "dialog.ui.magic_add",
                "Add Spell/Power",
                "Choose the magical entry, review category and drain, then confirm the selection.",
                BuildMagicSelectionFields(),
                BuildAddAndMoreActions()),
            "magic_delete" => new DesktopDialogState(
                "dialog.ui.magic_delete",
                "Delete Spell/Power",
                "Confirm removal of the selected magical entry.",
                BuildDeleteConfirmationFields(
                    "Stunbolt",
                    "Category: Combat" + Environment.NewLine + "Drain: F-3" + Environment.NewLine + "Source: Core Rulebook p. 288",
                    "The selected magical entry will be removed from the runner."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "magic_bind" => new DesktopDialogState(
                "dialog.ui.magic_bind",
                "Bind/Link",
                "Review the selected magical item before applying the bind/link action.",
                [
                    BuildUtilitySectionsField("uiMagicBindSections", "Binding", "Details", "Notes"),
                    new DesktopDialogField("uiMagicBindTarget", "Selected Entry", "Force 4 Focus", "Force 4 Focus", IsReadOnly: true),
                    new DesktopDialogField("uiMagicBindCost", "Binding Cost", "16", "16", IsReadOnly: true),
                    new DesktopDialogField("uiMagicBindDetails", "Bind Details", "Selected Entry | Force 4 Focus" + Environment.NewLine + "Binding Cost | 16 Karma" + Environment.NewLine + "Availability | Bound magical item" + Environment.NewLine + "Source | Core Rulebook p. 319", "Selected Entry | Force 4 Focus" + Environment.NewLine + "Binding Cost | 16 Karma" + Environment.NewLine + "Availability | Bound magical item" + Environment.NewLine + "Source | Core Rulebook p. 319", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiMagicBindNotes", "Notes", "Binding cost and source remain visible before confirmation.", "Binding cost and source remain visible before confirmation.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
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
                BuildAddAndMoreActions()),
            "adept_power_add" => new DesktopDialogState(
                "dialog.ui.adept_power_add",
                "Add Adept Power",
                "Search available adept powers, inspect PP cost and source, then confirm the selected power.",
                BuildAdeptPowerSelectionFields(),
                BuildAddAndMoreActions()),
            "complex_form_add" => new DesktopDialogState(
                "dialog.ui.complex_form_add",
                "Add Complex Form",
                "Browse complex forms, inspect target and source, then confirm the selected form.",
                BuildComplexFormSelectionFields(),
                BuildAddAndMoreActions()),
            "initiation_add" => new DesktopDialogState(
                "dialog.ui.initiation_add",
                "Add Initiation / Submersion",
                "Choose the reward, review grade and track, then confirm the initiation or submersion step.",
                BuildInitiationSelectionFields(),
                BuildAddAndMoreActions()),
            "spirit_add" => new DesktopDialogState(
                "dialog.ui.spirit_add",
                "Add Spirit / Ally / Familiar",
                "Browse spirits and allies, inspect force and type, then confirm the selected entry.",
                BuildSpiritSelectionFields(),
                BuildAddAndMoreActions()),
            "critter_power_add" => new DesktopDialogState(
                "dialog.ui.critter_power_add",
                "Add Critter Power",
                "Browse critter powers, inspect type and source, then confirm the selected power.",
                BuildCritterPowerSelectionFields(),
                BuildAddAndMoreActions()),
            "matrix_program_add" => new DesktopDialogState(
                "dialog.ui.matrix_program_add",
                "Add Program / Cyberdeck Item",
                "Browse matrix programs and cyberdeck items, inspect slot and source, then confirm the selected entry.",
                BuildMatrixProgramSelectionFields(),
                BuildAddAndMoreActions()),
            "skill_add" => new DesktopDialogState(
                "dialog.ui.skill_add",
                "Add Skill",
                "Browse skills, inspect category and linked attribute, then confirm the selected skill.",
                BuildSkillSelectionFields(),
                BuildAddAndMoreActions()),
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
                "Remove Skill",
                "Confirm removal of the selected skill.",
                BuildDeleteConfirmationFields(
                    "Perception",
                    "Category: Active Skill" + Environment.NewLine + "Rating: 6" + Environment.NewLine + "Linked Attribute: Intuition",
                    "The selected skill will be removed from the runner."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "skill_group" => new DesktopDialogState(
                "dialog.ui.skill_group",
                "Skill Group",
                "Review the skill group and ratings before assigning or breaking the group.",
                [
                    BuildUtilitySectionsField("uiSkillGroupSections", "Group", "Details", "Notes"),
                    new DesktopDialogField("uiSkillGroupName", "Group", "Stealth", "Stealth", IsReadOnly: true),
                    new DesktopDialogField("uiSkillGroupRating", "Rating", "4", "4", InputType: "number"),
                    new DesktopDialogField("uiSkillGroupDetails", "Group Details", "Group | Stealth" + Environment.NewLine + "Skills | Disguise, Palming, Sneaking" + Environment.NewLine + "Current Rating | 4", "Group | Stealth" + Environment.NewLine + "Skills | Disguise, Palming, Sneaking" + Environment.NewLine + "Current Rating | 4", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiSkillGroupNotes", "Notes", "Group composition and current rating remain visible while editing.", "Group composition and current rating remain visible while editing.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "combat_add_weapon" => new DesktopDialogState(
                "dialog.ui.combat_add_weapon",
                "Add Weapon",
                "Browse weapons, inspect combat stats and source, then confirm the selected weapon.",
                BuildWeaponSelectionFields(),
                BuildAddAndMoreActions()),
            "combat_add_armor" => new DesktopDialogState(
                "dialog.ui.combat_add_armor",
                "Add Armor",
                "Browse armor, inspect protection values and source, then confirm the selected armor.",
                BuildArmorSelectionFields(),
                BuildAddAndMoreActions()),
            "combat_reload" => new DesktopDialogState(
                "dialog.ui.combat_reload",
                "Reload Weapon",
                "Review weapon and ammo state before applying the reload.",
                [
                    BuildUtilitySectionsField("uiCombatReloadSections", "Weapon", "Details", "Notes"),
                    new DesktopDialogField("uiCombatReloadWeapon", "Weapon", "Colt M23", "Colt M23", IsReadOnly: true),
                    new DesktopDialogField("uiCombatReloadAmmo", "Ammo", "Regular Ammo (15)", "Regular Ammo (15)"),
                    new DesktopDialogField("uiCombatReloadDetails", "Reload Details", "Selected Weapon | Colt M23" + Environment.NewLine + "Current Magazine | 3 / 15" + Environment.NewLine + "Selected Ammo | Regular Ammo (15)", "Selected Weapon | Colt M23" + Environment.NewLine + "Current Magazine | 3 / 15" + Environment.NewLine + "Selected Ammo | Regular Ammo (15)", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiCombatReloadNotes", "Notes", "Weapon and ammo selection remain visible while reloading.", "Weapon and ammo selection remain visible while reloading.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "combat_damage_track" => new DesktopDialogState(
                "dialog.ui.combat_damage_track",
                "Damage Track",
                "Review current physical and stun track posture before applying the change.",
                [
                    BuildUtilitySectionsField("uiDamageTrackSections", "Tracks", "Details", "Notes"),
                    new DesktopDialogField("uiDamageTrackPhysical", "Physical", "3 / 10", "3 / 10", IsReadOnly: true),
                    new DesktopDialogField("uiDamageTrackStun", "Stun", "1 / 10", "1 / 10", IsReadOnly: true),
                    new DesktopDialogField("uiDamageTrackDetails", "Track Details", "Physical | 3 / 10" + Environment.NewLine + "Stun | 1 / 10" + Environment.NewLine + "Penalty | none", "Physical | 3 / 10" + Environment.NewLine + "Stun | 1 / 10" + Environment.NewLine + "Penalty | none", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                    new DesktopDialogField("uiDamageTrackNotes", "Notes", "Current track posture remains visible before applying the damage step.", "Current track posture remains visible before applying the damage step.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet)
                ],
                [new DesktopDialogAction("close", "Close", true)]),
            "vehicle_add" => new DesktopDialogState(
                "dialog.ui.vehicle_add",
                "Add Vehicle / Drone",
                "Browse vehicles and drones, inspect stats and source, then confirm the selected entry.",
                BuildVehicleSelectionFields(),
                BuildAddAndMoreActions()),
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
                "Remove Vehicle / Drone",
                "Confirm removal of the selected vehicle or drone.",
                BuildDeleteConfirmationFields(
                    "GMC Roadmaster",
                    "Role: Truck" + Environment.NewLine + "Armor: 16" + Environment.NewLine + "Source: Core Rulebook p. 466",
                    "The selected vehicle or drone will be removed from the runner."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "vehicle_mod_add" => new DesktopDialogState(
                "dialog.ui.vehicle_mod_add",
                "Add Vehicle Mod",
                "Browse modifications, inspect slot, availability, and source, then confirm the selected mod.",
                BuildVehicleModSelectionFields(),
                BuildAddAndMoreActions()),
            "contact_add" => new DesktopDialogState(
                "dialog.ui.contact_add",
                "Add Contact",
                "Author the contact with the same dense detail posture used by classic Chummer utility forms.",
                BuildContactAddFields(),
                BuildAddAndMoreActions()),
            "contact_edit" => new DesktopDialogState(
                "dialog.ui.contact_edit",
                "Edit Contact",
                "Edit the selected contact while keeping role and connection posture visible.",
                BuildContactEditFields(),
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "contact_remove" => new DesktopDialogState(
                "dialog.ui.contact_remove",
                "Remove Contact",
                "Confirm removal of the selected contact.",
                BuildDeleteConfirmationFields(
                    "Mr. Johnson",
                    "Role: Fixer" + Environment.NewLine + "Connection/Loyalty: 5 / 3" + Environment.NewLine + "Notes: Premium jobs",
                    "The selected contact will be removed from the runner."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
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
            "quality_add" => new DesktopDialogState(
                "dialog.ui.quality_add",
                "Add Quality",
                "Browse qualities, inspect karma cost and source, then confirm the selected quality.",
                BuildQualitySelectionFields(),
                BuildAddAndMoreActions()),
            "quality_delete" => new DesktopDialogState(
                "dialog.ui.quality_delete",
                "Remove Quality",
                "Confirm removal of the selected quality.",
                BuildDeleteConfirmationFields(
                    "First Impression",
                    "Type: Positive" + Environment.NewLine + "Karma: 11" + Environment.NewLine + "Source: Core Rulebook p. 73",
                    "The selected quality will be removed from the runner."),
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            _ => throw new InvalidOperationException($"Known legacy UI control '{controlId}' is missing a dedicated dialog mapping.")
        };
    }

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
                "translatorSearch",
                DesktopLocalizationCatalog.GetRequiredString("desktop.dialog.translator.field.search", language),
                string.Empty,
                DesktopLocalizationCatalog.GetRequiredString("desktop.dialog.translator.field.search_placeholder", language)),
            new DesktopDialogField(
                "translatorLanePosture",
                "Translator Lane",
                masterIndex?.TranslatorLanePosture ?? "missing",
                "missing",
                IsReadOnly: true),
            new DesktopDialogField(
                "translatorBridgePosture",
                "Translator Bridge",
                masterIndex?.TranslatorBridgePosture ?? translatorLanguages?.TranslatorBridgePosture ?? "missing",
                "missing",
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

    private static IReadOnlyList<DesktopDialogField> BuildMasterIndexFields(MasterIndexResponse? masterIndex)
    {
        if (masterIndex is null)
        {
            return
            [
                new DesktopDialogField("root", "Data Root", "/app/data", "/app/data", IsReadOnly: true),
                new DesktopDialogField("masterIndexPaneHeader", "Pane Header", "Data File / Search / Notes", "Data File / Search / Notes", IsReadOnly: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
                new DesktopDialogField("masterIndexDetailTabs", "Pane Tabs", "Results" + Environment.NewLine + "Source" + Environment.NewLine + "Notes" + Environment.NewLine + "Setting", "Results", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
                new DesktopDialogField("masterIndexFileSelection", "Data File", "All" + Environment.NewLine + "books.xml · 0 entries", "All", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List),
                new DesktopDialogField("masterIndexCurrentFile", "Current Data File", "books.xml · 0 entries", "books.xml · 0 entries", IsReadOnly: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
                new DesktopDialogField("masterIndexSearch", "Search", string.Empty, "Search index"),
                new DesktopDialogField("masterIndexSelectionTrail", "Selection Trail", "Data File | books.xml" + Environment.NewLine + "Search | all rows" + Environment.NewLine + "Selected Result | none" + Environment.NewLine + "Open Page | unavailable", "Data File | books.xml", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid),
                new DesktopDialogField("masterIndexSourceTree", "Source Tree", "[Sourcebooks]" + Environment.NewLine + "└─ All Sources", "[Sourcebooks]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
                new DesktopDialogField("masterIndexCatalogEntries", "Items", "[Current Book]" + Environment.NewLine + "└─ No index entries discovered.", "[Current Book]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
                new DesktopDialogField("masterIndexSourceCommands", "Source Commands", "Change data file filter" + Environment.NewLine + "Modify character setting" + Environment.NewLine + "Open linked PDF", "Change data file filter", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
                new DesktopDialogField("masterIndexResultInspector", "Result Inspector", "Selected Result | none" + Environment.NewLine + "Data File | books.xml" + Environment.NewLine + "Linked Source | unavailable" + Environment.NewLine + "Open Page | unavailable" + Environment.NewLine + "Reference Posture | missing", "Selected Result | none", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField("masterIndexResultList", "Results", "No indexed entries discovered.", "No indexed entries discovered.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List),
                new DesktopDialogField("masterIndexResultCommands", "Result Commands", "Select result to refresh notes" + Environment.NewLine + "Open selected result page" + Environment.NewLine + "Keep source and notes pinned on the right", "Select result to refresh notes", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField("masterIndexSnippetInspector", "Snippet Inspector", "Snippet Count | 0" + Environment.NewLine + "Snippet Page | unavailable" + Environment.NewLine + "Snippet Provenance | unavailable" + Environment.NewLine + "Note Posture | right-pane legacy preview", "Snippet Count | 0", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField("masterIndexSourceClickReminder", "Linked Source", "No linked PDF is available for the current selection. Choose a result row to refresh source and notes.", "No linked PDF is available for the current selection.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
                new DesktopDialogField("masterIndexNotesPane", "Notes", "Select a result row to inspect notes and linked source text on the right.", "Select a result row to inspect notes and linked source text on the right.", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
                new DesktopDialogField("masterIndexCharacterSetting", "Use Setting", "Use Setting | default" + Environment.NewLine + "Modify | Modify...", "Use Setting | default", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid)
            ];
        }

        MasterIndexSourcebookEntry selectedSourcebook = masterIndex.Sourcebooks.FirstOrDefault()
            ?? new MasterIndexSourcebookEntry(
                Id: "unknown",
                Code: "UNK",
                Name: "Unknown Source",
                Permanent: false,
                ReferencePosture: "missing",
                RuleSnippetCount: 0,
                RuleSnippets: [],
                ReferenceSourcePosture: "missing");
        string referenceSources = $"{masterIndex.SourcebooksWithGovernedReferenceSources} governed / {masterIndex.SourcebooksWithStaleReferenceSources} stale / {masterIndex.SourcebooksMissingReferenceSources} missing";
        string importCoverage = $"{masterIndex.ImportOracleCoveragePercent}% ({masterIndex.ImportOracleSourcesCovered}/{masterIndex.ImportOracleSourcesExpected})";
        string adjacentOracleCoverage = $"{masterIndex.AdjacentSr6OracleSourcesCovered}/{masterIndex.AdjacentSr6OracleSourcesExpected}";
        string onlineStorageCoverage = $"{masterIndex.OnlineStorageCoveragePercent}% ({masterIndex.OnlineStorageReceiptsCovered}/{masterIndex.OnlineStorageReceiptsExpected})";
        string sr6DesignerCoverage = $"{masterIndex.Sr6DesignerFamiliesAvailable}/{masterIndex.Sr6DesignerFamiliesExpected}";
        string sr6Successor = $"{masterIndex.Sr6SupplementLanePosture}, designers {masterIndex.Sr6DesignerFamiliesAvailable}/{masterIndex.Sr6DesignerFamiliesExpected}, house rules {masterIndex.HouseRuleLanePosture}";
        string sourcebookSelectionSummary = BuildSourcebookSelectionSummary(masterIndex.Sourcebooks);
        string importOracleMatrix = BuildImportOracleMatrix(masterIndex);
        string missingImportSources = masterIndex.ImportOracleMissingSources is { Count: > 0 }
            ? string.Join(", ", masterIndex.ImportOracleMissingSources)
            : "none";
        string sourceTree = "[Sourcebooks]" + Environment.NewLine + string.Join(
            Environment.NewLine,
            masterIndex.Sourcebooks
                .OrderBy(sourcebook => sourcebook.Code, StringComparer.OrdinalIgnoreCase)
                .ThenBy(sourcebook => sourcebook.Name, StringComparer.OrdinalIgnoreCase)
                .Select(sourcebook => sourcebook.Id == selectedSourcebook.Id
                    ? $"> {sourcebook.Code} · {sourcebook.Name}"
                    : $"├─ {sourcebook.Code} · {sourcebook.Name}"));
        string selectedSource = selectedSourcebook.LocalPdfPath
            ?? selectedSourcebook.ReferenceUrl
            ?? selectedSourcebook.ReferenceSnapshot
            ?? "(no linked source)";
        MasterIndexRuleSnippetEntry? selectedSnippet = selectedSourcebook.RuleSnippets.FirstOrDefault();
        MasterIndexFileEntry? selectedFile = ResolveMasterIndexSelectedFile(masterIndex.Files, selectedSnippet);
        string selectedFileName = selectedFile?.File ?? "All";
        string selectedFileSummary = selectedFile is null
            ? "All data files"
            : $"{selectedFile.File} · {selectedFile.ElementCount} indexed entries";
        string selectedSnippetLabel = selectedSnippet is null
            ? "none"
            : BuildMasterIndexSnippetLabel(selectedSnippet);
        string resultInspector = BuildGridValue(
            ("Selected Result", selectedSnippetLabel),
            ("Data File", selectedFileName),
            ("Current Book", $"{selectedSourcebook.Code} · {selectedSourcebook.Name}"),
            ("Linked Source", selectedSource),
            ("Open Page", selectedSnippet?.Page.ToString(CultureInfo.InvariantCulture) ?? "linked source"),
            ("Activation", "select row / open source"),
            ("Reference Posture", selectedSourcebook.ReferencePosture),
            ("Use Setting", masterIndex.SettingsLanePosture));
        string selectionTrail = BuildGridValue(
            ("Data File", selectedFileName),
            ("Search", "all rows"),
            ("Selected Result", selectedSnippetLabel),
            ("Open Page", selectedSnippet?.Page.ToString(CultureInfo.InvariantCulture) ?? "linked source"));
        string sourceCommands =
            "Change data file filter" + Environment.NewLine +
            "Modify character setting" + Environment.NewLine +
            (string.IsNullOrWhiteSpace(selectedSourcebook.LocalPdfPath) ? "Open linked source snapshot" : "Open linked PDF");
        string resultCommands =
            "Select result to refresh notes" + Environment.NewLine +
            "Open selected result page" + Environment.NewLine +
            "Keep source and notes pinned on the right";
        string libraryNotes =
            $"{masterIndex.Sourcebooks.Count} active books selected. Reference links are available for most active books, and snippet coverage is {masterIndex.ReferenceCoveragePercent}%." + Environment.NewLine +
            "Use Data File on the left, pick a row in the list, and keep Source plus Notes visible on the right like the old reference utility.";
        string importNotes =
            $"Import coverage is {importCoverage}. Covered sources stay in the current utility posture; missing source families: {missingImportSources}.";
        string sr6Notes =
            $"SR6 successor posture is {masterIndex.Sr6SupplementLanePosture}. Designer tool coverage is {sr6DesignerCoverage}, and house-rule overlays remain {masterIndex.HouseRuleLanePosture}.";
        string snippetPreview = selectedSourcebook.RuleSnippets.Count == 0
            ? "No governed rule snippets are currently attached to the selected source." + Environment.NewLine + "Reference notes stay in this pane once an indexed entry is selected."
            : string.Join(
                Environment.NewLine + Environment.NewLine,
                selectedSourcebook.RuleSnippets.Take(2).Select(
                    snippet => $"Page {snippet.Page} · {snippet.Provenance}{Environment.NewLine}{snippet.Snippet}"));
        string snippetInspector = selectedSourcebook.RuleSnippets.Count == 0
            ? "Snippet Preview | unavailable" + Environment.NewLine + "Keep note text on the right like the legacy utility form."
            : BuildGridValue(
                ("Snippet Page", selectedSourcebook.RuleSnippets[0].Page.ToString(CultureInfo.InvariantCulture)),
                ("Snippet Provenance", selectedSourcebook.RuleSnippets[0].Provenance),
                ("Snippet Count", selectedSourcebook.RuleSnippetCount.ToString(CultureInfo.InvariantCulture)),
                ("Note Posture", "right-pane legacy preview"));
        string selectionProfile = BuildGridValue(
            ("Use Setting", $"{masterIndex.SettingsLanePosture} ({masterIndex.SettingsProfileCount} profiles)"),
            ("Source toggles", $"{masterIndex.SourceToggleLanePosture} · {masterIndex.DistinctSourcebookToggles} toggles"),
            ("Custom data", masterIndex.CustomDataLanePosture),
            ("Modify", "Modify..."));
        MasterIndexRuleSnippetEntry[] catalogSnippets = selectedSourcebook.RuleSnippets.Take(6).ToArray();
        string fileSelection = BuildMasterIndexFileSelection(masterIndex.Files, selectedFile);
        string catalogEntries = selectedSourcebook.RuleSnippets.Count == 0
            ? "[Current File]" + Environment.NewLine + $"└─ {selectedFileName}" + Environment.NewLine + "[Current Book]" + Environment.NewLine + $"└─ {selectedSourcebook.Name} [{selectedSourcebook.Code}]"
            : "[Current File]" + Environment.NewLine +
                $"└─ {selectedFileName}" + Environment.NewLine +
                "[Current Book]" + Environment.NewLine +
                $"└─ {selectedSourcebook.Name} [{selectedSourcebook.Code}]" + Environment.NewLine +
                string.Join(
                    Environment.NewLine,
                    catalogSnippets
                        .Select((snippet, index) =>
                            $"{(index == catalogSnippets.Length - 1 ? "   └─ " : "   ├─ ")}p. {snippet.Page} · {BuildMasterIndexSnippetLabel(snippet)}"));
        string resultList = selectedSourcebook.RuleSnippets.Count == 0
            ? "No indexed entries discovered."
            : string.Join(
                Environment.NewLine,
                selectedSourcebook.RuleSnippets
                    .Take(8)
                    .Select((snippet, index) =>
                        $"{(index == 0 ? ">" : " ")} p. {snippet.Page} · {BuildMasterIndexSnippetLabel(snippet)} · {snippet.Provenance}"));
        string paneHeader = "Data File / Search / Notes";
        string searchHints =
            "Data File filters the list on the left. Search narrows the current file, and selecting a row refreshes Source plus Notes on the right like the legacy utility form.";
        string sourceClickReminder = string.IsNullOrWhiteSpace(selectedSourcebook.LocalPdfPath)
            ? "No local PDF is attached; keep the linked source visible and use the row selection to refresh notes."
            : $"<- Click to open linked PDF at p. {selectedSnippet?.Page.ToString(CultureInfo.InvariantCulture) ?? "linked source"}";
        string sourceDetails =
            BuildGridValue(
                ("Data File", selectedFileName),
                ("Selected item", $"{selectedSourcebook.Name} ({selectedSourcebook.Code})"),
                ("Source", $"{selectedSourcebook.Code} · {selectedSourcebook.Name}"),
                ("Linked Source", selectedSource),
                ("Open Action", string.IsNullOrWhiteSpace(selectedSourcebook.LocalPdfPath) ? "open snapshot" : "open linked PDF"));

        List<DesktopDialogField> fields =
        [
            new DesktopDialogField("root", "Data Root", "/app/data", "/app/data", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSections", "Sections", "Search" + Environment.NewLine + "Results" + Environment.NewLine + "Sources" + Environment.NewLine + "Snippets", "Search", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("masterIndexDetailTabs", "Pane Tabs", "Results" + Environment.NewLine + "Source" + Environment.NewLine + "Notes" + Environment.NewLine + "Setting", "Results", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tabs),
            new DesktopDialogField("masterIndexPaneHeader", "Pane Header", paneHeader, paneHeader, IsReadOnly: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("masterIndexFileSelection", "Data File", fileSelection, "All", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List),
            new DesktopDialogField("masterIndexCurrentFile", "Current Data File", selectedFileSummary, selectedFileSummary, IsReadOnly: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("masterIndexSearch", "Search", string.Empty, "Search terms"),
            new DesktopDialogField("masterIndexSearchHints", "Search Hints", searchHints, searchHints, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("masterIndexCurrentSourcebook", "Selected Book", $"{selectedSourcebook.Code} · {selectedSourcebook.Name}", $"{selectedSourcebook.Code} · {selectedSourcebook.Name}", IsReadOnly: true),
            new DesktopDialogField("masterIndexSelectionTrail", "Selection Trail", NormalizeGridValue(selectionTrail), NormalizeGridValue(selectionTrail), IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid),
            new DesktopDialogField("masterIndexCharacterSetting", "Current Filters", NormalizeGridValue(selectionProfile), NormalizeGridValue(selectionProfile), IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid),
            new DesktopDialogField("masterIndexSourceTree", "Source Tree", sourceTree, "[Sourcebooks]", IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("masterIndexCatalogEntries", "Items", catalogEntries, catalogEntries, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Tree, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("masterIndexSourceCommands", "Source Commands", sourceCommands, sourceCommands, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Left),
            new DesktopDialogField("masterIndexDetails", "Details", sourceDetails, sourceDetails, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("masterIndexResultList", "Results", resultList, resultList, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List),
            new DesktopDialogField("masterIndexResultInspector", "Result Inspector", resultInspector, resultInspector, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("masterIndexResultCommands", "Result Commands", resultCommands, resultCommands, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.List, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("masterIndexSnippetPreview", "Snippet Preview", snippetPreview, snippetPreview, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("masterIndexSnippetInspector", "Snippet Inspector", snippetInspector, snippetInspector, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Grid, LayoutSlot: DesktopDialogFieldLayoutSlots.Right),
            new DesktopDialogField("masterIndexSourceClickReminder", "Linked Source", sourceClickReminder, sourceClickReminder, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("masterIndexNotesPane", "Notes", libraryNotes + Environment.NewLine + Environment.NewLine + snippetPreview, libraryNotes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("masterIndexSelectedSource", "Source", selectedSource, selectedSource, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("masterIndexSourceSelectionSummary", "Source Selection Summary", sourcebookSelectionSummary, sourcebookSelectionSummary, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet),
            new DesktopDialogField("masterIndexLibraryNotes", "Library Notes", libraryNotes, libraryNotes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexImportNotes", "Import Notes", importNotes, importNotes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6Notes", "SR6 Notes", sr6Notes, sr6Notes, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSourcebooks", "Sourcebooks", masterIndex.SourcebookCount.ToString(), "0", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexReferenceCoverage", "Snippet Coverage", $"{masterIndex.ReferenceCoveragePercent}% ({masterIndex.SourcebooksWithSnippets}/{masterIndex.SourcebookCount})", "0%", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexReferenceSources", "Reference Sources", referenceSources, referenceSources, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexReferenceSourceReceipt", "Reference Source Receipt", masterIndex.ReferenceSourceLaneReceipt, masterIndex.ReferenceSourceLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSettingsLane", "Settings Lane", masterIndex.SettingsLanePosture, masterIndex.SettingsLanePosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSourceToggleLane", "Source Toggle Lane", masterIndex.SourceToggleLanePosture, masterIndex.SourceToggleLanePosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSourceSelectionReceipt", "Source Selection Receipt", masterIndex.SourceSelectionLaneReceipt, masterIndex.SourceSelectionLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexCustomDataLane", "Custom Data Lane", masterIndex.CustomDataLanePosture, masterIndex.CustomDataLanePosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexCustomDataAuthoringReceipt", "Custom Data Authoring Receipt", masterIndex.CustomDataAuthoringLaneReceipt, masterIndex.CustomDataAuthoringLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexXmlBridgeReceipt", "XML Bridge Receipt", masterIndex.XmlBridgeLaneReceipt, masterIndex.XmlBridgeLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexTranslatorLane", "Translator Lane", masterIndex.TranslatorLanePosture, masterIndex.TranslatorLanePosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexTranslatorReceipt", "Translator Receipt", masterIndex.TranslatorLaneReceipt, masterIndex.TranslatorLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexImportOracleLane", "Import Oracle Lane", $"{masterIndex.ImportOracleLanePosture} ({importCoverage})", masterIndex.ImportOracleLanePosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexImportOracleReceipt", "Import Oracle Receipt", masterIndex.ImportOracleLaneReceipt, masterIndex.ImportOracleLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexImportOracleMatrix", "Import Oracle Matrix", importOracleMatrix, importOracleMatrix, IsReadOnly: true, IsMultiline: true, VisualKind: DesktopDialogFieldVisualKinds.Snippet, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexImportOracleMissingSources", "Import Oracle Missing Sources", missingImportSources, missingImportSources, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexAdjacentSr6OracleLane", "Adjacent SR6 Oracle Lane", $"{masterIndex.AdjacentSr6OracleReceiptPosture} ({adjacentOracleCoverage})", masterIndex.AdjacentSr6OracleReceiptPosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexAdjacentSr6OracleReceipt", "Adjacent SR6 Oracle Receipt", masterIndex.AdjacentSr6OracleLaneReceipt, masterIndex.AdjacentSr6OracleLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexOnlineStorageLane", "Online Storage Lane", $"{masterIndex.OnlineStorageLanePosture}/{masterIndex.OnlineStorageReceiptPosture} ({onlineStorageCoverage})", masterIndex.OnlineStorageLanePosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexOnlineStorageCoverage", "Online Storage Coverage", onlineStorageCoverage, onlineStorageCoverage, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexOnlineStorageReceipt", "Online Storage Receipt", masterIndex.OnlineStorageLaneReceipt, masterIndex.OnlineStorageLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6SuccessorLane", "SR6 Successor Lane", sr6Successor, sr6Successor, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6SupplementLane", "SR6 Supplement Lane", masterIndex.Sr6SupplementLanePosture, masterIndex.Sr6SupplementLanePosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6DesignerToolsLane", "SR6 Designer Tools Lane", masterIndex.Sr6DesignerToolsPosture, masterIndex.Sr6DesignerToolsPosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6DesignerCoverage", "SR6 Designer Coverage", sr6DesignerCoverage, sr6DesignerCoverage, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexHouseRuleLane", "House-Rule Lane", masterIndex.HouseRuleLanePosture, masterIndex.HouseRuleLanePosture, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexHouseRuleOverlayCount", "House-Rule Overlay Count", masterIndex.HouseRuleOverlayCount.ToString(), "0", IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden),
            new DesktopDialogField("masterIndexSr6SuccessorReceipt", "SR6 Successor Receipt", masterIndex.Sr6SuccessorLaneReceipt, masterIndex.Sr6SuccessorLaneReceipt, IsReadOnly: true, LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden)
        ];

        fields.AddRange(BuildSourcebookSelectionFields(masterIndex.Sourcebooks));
        return fields;
    }

    private static IReadOnlyList<DesktopDialogAction> BuildMasterIndexActions(MasterIndexResponse? masterIndex)
    {
        bool hasLinkedSource = masterIndex?.Sourcebooks.Any(sourcebook =>
            !string.IsNullOrWhiteSpace(sourcebook.LocalPdfPath)
            || !string.IsNullOrWhiteSpace(sourcebook.ReferenceUrl)
            || !string.IsNullOrWhiteSpace(sourcebook.ReferenceSnapshot)) == true;

        return
        [
            new DesktopDialogAction("open_source", hasLinkedSource ? "Open Linked PDF" : "Open Linked Source", true),
            new DesktopDialogAction("switch_file", "Change Data File"),
            new DesktopDialogAction("edit_setting", "Modify Setting"),
            new DesktopDialogAction("close", "Close")
        ];
    }

    private static string BuildSourcebookSelectionSummary(IReadOnlyList<MasterIndexSourcebookEntry> sourcebooks)
    {
        if (sourcebooks.Count == 0)
            return "No sourcebooks discovered.";

        int permanentCount = sourcebooks.Count(sourcebook => sourcebook.Permanent);
        int selectableCount = sourcebooks.Count - permanentCount;
        int governedReferenceCount = sourcebooks.Count(sourcebook => string.Equals(sourcebook.ReferencePosture, "governed", StringComparison.Ordinal));
        int governedReferenceSourceCount = sourcebooks.Count(sourcebook => string.Equals(sourcebook.ReferenceSourcePosture, "governed", StringComparison.Ordinal));

        return $"{sourcebooks.Count} sourcebooks ({selectableCount} selectable, {permanentCount} permanent); reference posture governed on {governedReferenceCount}/{sourcebooks.Count}, source provenance governed on {governedReferenceSourceCount}/{sourcebooks.Count}.";
    }

    private static string BuildImportOracleMatrix(MasterIndexResponse masterIndex)
    {
        return $"Chummer4 fixtures {masterIndex.LegacyChummer4FixtureCount}, Chummer5a fixtures {masterIndex.LegacyChummer5FixtureCount}, Hero Lab fixtures {masterIndex.HeroLabFixtureCount}, adjacent SR6 sources {masterIndex.AdjacentSr6OracleSourcesCovered}/{masterIndex.AdjacentSr6OracleSourcesExpected}.";
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

    private static string BuildMasterIndexSnippetLabel(MasterIndexRuleSnippetEntry snippet)
    {
        string normalized = string.Join(" ", snippet.Snippet
            .Split([Environment.NewLine, "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length <= 64)
        {
            return normalized;
        }

        return normalized[..61].TrimEnd() + "...";
    }

    private static IEnumerable<DesktopDialogField> BuildSourcebookSelectionFields(IReadOnlyList<MasterIndexSourcebookEntry> sourcebooks)
    {
        int index = 1;
        foreach (MasterIndexSourcebookEntry sourcebook in sourcebooks
                     .OrderBy(sourcebook => sourcebook.Code, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(sourcebook => sourcebook.Name, StringComparer.OrdinalIgnoreCase))
        {
            string label = $"Sourcebook {sourcebook.Code}";
            string snippetCoverage = sourcebook.RuleSnippetCount <= 0 ? "no snippets" : $"{sourcebook.RuleSnippetCount} snippets";
            string selectionKind = sourcebook.Permanent ? "permanent" : "selectable";
            string targetKinds = BuildReferenceTargetKinds(sourcebook);
            string value =
                $"{sourcebook.Name} [{selectionKind}] | ref {sourcebook.ReferencePosture} | source {sourcebook.ReferenceSourcePosture} | {snippetCoverage} | targets {targetKinds}";

            yield return new DesktopDialogField(
                $"masterIndexSourcebook{index}",
                label,
                value,
                value,
                IsReadOnly: true,
                LayoutSlot: DesktopDialogFieldLayoutSlots.Hidden);
            index++;
        }
    }

    private static string BuildReferenceTargetKinds(MasterIndexSourcebookEntry sourcebook)
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
}
