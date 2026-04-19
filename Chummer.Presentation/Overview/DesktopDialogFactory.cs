using Chummer.Contracts.Characters;
using Chummer.Contracts.Api;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Explain;

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
            "global_settings" => new DesktopDialogState(
                "dialog.global_settings",
                S("desktop.dialog.global_settings.title"),
                F("desktop.dialog.global_settings.message", DesktopLocalizationCatalog.BuildSupportedLanguageSummary()),
                [
                    new DesktopDialogField("globalUiScale", S("desktop.dialog.global_settings.field.ui_scale"), preferences.UiScalePercent.ToString(), "100", InputType: "number"),
                    new DesktopDialogField("globalTheme", S("desktop.dialog.global_settings.field.theme"), preferences.Theme, "classic"),
                    new DesktopDialogField("globalLanguage", S("desktop.dialog.global_settings.field.language"), DesktopLocalizationCatalog.NormalizeOrDefault(preferences.Language), DesktopLocalizationCatalog.DefaultLanguage),
                    new DesktopDialogField("globalCompactMode", S("desktop.dialog.global_settings.field.compact_mode"), preferences.CompactMode ? "true" : "false", "false", InputType: "checkbox")
                ],
                [
                    new DesktopDialogAction("save", S("desktop.dialog.action.save"), true),
                    new DesktopDialogAction("cancel", S("desktop.dialog.action.cancel"))
                ]),
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
                    ? "Catalog data is served by the API and surfaced here in desktop parity mode."
                    : $"Catalog parity: {masterIndex.SourcebookCount} sourcebooks, {masterIndex.ReferenceCoveragePercent}% snippet coverage, settings={masterIndex.SettingsLanePosture}, import={masterIndex.ImportOracleLanePosture}.",
                BuildMasterIndexFields(masterIndex),
                [new DesktopDialogAction("close", "Close", true)]),
            "character_roster" => new DesktopDialogState(
                "dialog.character_roster",
                "Character Roster",
                "Desktop roster/operator lane over the currently open runners. Watch-folder and deeper GM dashboard closure remain in milestone 15.",
                BuildRosterFields(name, alias, workspace, currentWorkspace, openWorkspaces),
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
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "close_window" => new DesktopDialogState(
                "dialog.close_window",
                "Close Window",
                "Close-window action is host/platform specific.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "wiki" => new DesktopDialogState(
                "dialog.wiki",
                "Wiki",
                "https://github.com/chummer5a/chummer5a/wiki",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "discord" => new DesktopDialogState(
                "dialog.discord",
                "Discord",
                "https://discord.gg/EV44Mya",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "revision_history" => new DesktopDialogState(
                "dialog.revision_history",
                "Revision History",
                "https://github.com/chummer5a/chummer5a/releases",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "dumpshock" => new DesktopDialogState(
                "dialog.dumpshock",
                "Dumpshock Thread",
                "https://forums.dumpshock.com/index.php?showtopic=37464",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "print_character" => new DesktopDialogState(
                "dialog.print_character",
                "Print Character",
                "Print preview is rendered by host/browser print facilities.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "print_multiple" => new DesktopDialogState(
                "dialog.print_multiple",
                "Print Multiple",
                "Batch print is available through roster and print endpoints.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "update" => new DesktopDialogState(
                "dialog.update",
                "Check for Updates",
                "Desktop heads check the configured registry manifest (`CHUMMER_DESKTOP_UPDATE_MANIFEST`) at startup, stage either an in-place payload or a platform installer, and keep install linking plus support continuity intact across the relaunch boundary.",
                [
                    new DesktopDialogField("updateManifest", "Manifest", Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_UPDATE_MANIFEST") ?? string.Empty, "unset", IsReadOnly: true),
                    new DesktopDialogField("updateAutoApply", "Auto apply", Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_UPDATE_AUTO_APPLY") ?? "true", "true", IsReadOnly: true),
                    new DesktopDialogField("updateSupportPath", "Support after update", "/account/support", "/account/support", IsReadOnly: true)
                ],
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

    private static IReadOnlyList<DesktopDialogField> BuildRosterFields(
        string name,
        string alias,
        string workspace,
        CharacterWorkspaceId? currentWorkspace,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces)
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
                    $"{candidate.Alias} · {candidate.Name} · {(candidate.HasSavedWorkspace ? "saved" : "unsaved")} · {(RulesetDefaults.NormalizeOptional(candidate.RulesetId) ?? candidate.RulesetId)}"));

        return
        [
            new DesktopDialogField("rosterOpenCount", "Open Runners", ordered.Length.ToString(), "0", IsReadOnly: true),
            new DesktopDialogField("rosterSavedCount", "Saved Workspaces", savedCount.ToString(), "0", IsReadOnly: true),
            new DesktopDialogField("rosterRulesetMix", "Ruleset Mix", string.IsNullOrWhiteSpace(rulesetMix) ? "(none)" : rulesetMix, "(none)", IsReadOnly: true),
            new DesktopDialogField("rosterActiveWorkspace", "Active Workspace", currentWorkspace?.Value ?? workspace, workspace, IsReadOnly: true),
            new DesktopDialogField("rosterOpsLane", "Operator Lane", "open runners + save posture + ruleset mix", "open runners + save posture + ruleset mix", IsReadOnly: true),
            new DesktopDialogField("rosterEntries", "Roster Entries", rosterEntries, rosterEntries, IsReadOnly: true, IsMultiline: true)
        ];
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

    public DesktopDialogState CreateUiControlDialog(
        string controlId,
        DesktopPreferenceState preferences)
    {
        return controlId switch
        {
            "create_entry" => new DesktopDialogState(
                "dialog.ui.create_entry",
                "Add Entry",
                null,
                [new DesktopDialogField("uiCreateEntryName", "Entry Name", string.Empty, "New entry")],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "edit_entry" => new DesktopDialogState(
                "dialog.ui.edit_entry",
                "Edit Entry",
                null,
                [new DesktopDialogField("uiEditEntryName", "Entry Name", "Current Entry", "Current Entry")],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "delete_entry" => new DesktopDialogState(
                "dialog.ui.delete_entry",
                "Delete Entry",
                "Delete selected entry?",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "open_notes" => new DesktopDialogState(
                "dialog.ui.open_notes",
                "Notes",
                null,
                [new DesktopDialogField("uiNotesEditor", "Notes", preferences.CharacterNotes, "notes", true)],
                [
                    new DesktopDialogAction("save", "Save", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "move_up" => new DesktopDialogState(
                "dialog.ui.move_up",
                "Move Up",
                "Moved selection up.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "move_down" => new DesktopDialogState(
                "dialog.ui.move_down",
                "Move Down",
                "Moved selection down.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "toggle_free_paid" => new DesktopDialogState(
                "dialog.ui.toggle_free_paid",
                "Free/Paid",
                "Toggled free/paid state for selected item.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "show_source" => new DesktopDialogState(
                "dialog.ui.show_source",
                "Source",
                "Source book and page metadata is shown here.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "gear_add" => new DesktopDialogState(
                "dialog.ui.gear_add",
                "Add Gear",
                null,
                [new DesktopDialogField("uiGearName", "Gear Name", string.Empty, "Ares Predator")],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "gear_edit" => new DesktopDialogState(
                "dialog.ui.gear_edit",
                "Edit Gear",
                null,
                [new DesktopDialogField("uiGearEditName", "Gear Name", "Selected Gear", "Selected Gear")],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "gear_delete" => new DesktopDialogState(
                "dialog.ui.gear_delete",
                "Delete Gear",
                "Deleted selected gear item.",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "gear_mount" => new DesktopDialogState(
                "dialog.ui.gear_mount",
                "Mount Gear",
                "Mounted selected gear on compatible host.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "gear_source" => new DesktopDialogState(
                "dialog.ui.gear_source",
                "Gear Source",
                "Gear source references are displayed here.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "cyberware_add" => new DesktopDialogState(
                "dialog.ui.cyberware_add",
                "Add Cyberware",
                null,
                [
                    new DesktopDialogField("uiCyberwareName", "Cyberware", string.Empty, "Wired Reflexes 2"),
                    new DesktopDialogField("uiCyberwareSlot", "Location", "Body", "Body")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "cyberware_edit" => new DesktopDialogState(
                "dialog.ui.cyberware_edit",
                "Edit Cyberware",
                null,
                [
                    new DesktopDialogField("uiCyberwareEditName", "Cyberware", "Selected Cyberware", "Selected Cyberware"),
                    new DesktopDialogField("uiCyberwareRating", "Rating", "2", "2", InputType: "number")
                ],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "cyberware_delete" => new DesktopDialogState(
                "dialog.ui.cyberware_delete",
                "Remove Cyberware",
                "Removed selected cyberware item.",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "drug_add" => new DesktopDialogState(
                "dialog.ui.drug_add",
                "Add Drug",
                null,
                [
                    new DesktopDialogField("uiDrugName", "Drug", string.Empty, "Jazz"),
                    new DesktopDialogField("uiDrugQuantity", "Quantity", "1", "1", InputType: "number")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "drug_delete" => new DesktopDialogState(
                "dialog.ui.drug_delete",
                "Remove Drug",
                "Removed selected drug item.",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "magic_add" => new DesktopDialogState(
                "dialog.ui.magic_add",
                "Add Spell/Power",
                null,
                [new DesktopDialogField("uiMagicName", "Name", string.Empty, "Spell or Power")],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "magic_delete" => new DesktopDialogState(
                "dialog.ui.magic_delete",
                "Delete Spell/Power",
                "Removed selected spell/power.",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "magic_bind" => new DesktopDialogState(
                "dialog.ui.magic_bind",
                "Bind/Link",
                "Bind/link workflow started for selected magical item.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "magic_source" => new DesktopDialogState(
                "dialog.ui.magic_source",
                "Magic Source",
                "Magical source references are displayed here.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "spell_add" => new DesktopDialogState(
                "dialog.ui.spell_add",
                "Add Spell",
                null,
                [
                    new DesktopDialogField("uiSpellName", "Spell", string.Empty, "Stunbolt"),
                    new DesktopDialogField("uiSpellCategory", "Category", "Combat", "Combat")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "adept_power_add" => new DesktopDialogState(
                "dialog.ui.adept_power_add",
                "Add Adept Power",
                null,
                [
                    new DesktopDialogField("uiAdeptPowerName", "Power", string.Empty, "Improved Reflexes"),
                    new DesktopDialogField("uiAdeptPowerLevel", "Level", "1", "1", InputType: "number")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "complex_form_add" => new DesktopDialogState(
                "dialog.ui.complex_form_add",
                "Add Complex Form",
                null,
                [
                    new DesktopDialogField("uiComplexFormName", "Complex Form", string.Empty, "Cleaner"),
                    new DesktopDialogField("uiComplexFormLevel", "Level", "1", "1", InputType: "number")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "initiation_add" => new DesktopDialogState(
                "dialog.ui.initiation_add",
                "Add Initiation / Submersion",
                null,
                [
                    new DesktopDialogField("uiInitiationGrade", "Grade", "1", "1", InputType: "number"),
                    new DesktopDialogField("uiInitiationReward", "Reward", string.Empty, "Metamagic")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "spirit_add" => new DesktopDialogState(
                "dialog.ui.spirit_add",
                "Add Spirit / Ally / Familiar",
                null,
                [
                    new DesktopDialogField("uiSpiritName", "Name", string.Empty, "Watcher"),
                    new DesktopDialogField("uiSpiritType", "Type", "Spirit", "Spirit")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "critter_power_add" => new DesktopDialogState(
                "dialog.ui.critter_power_add",
                "Add Critter Power",
                null,
                [
                    new DesktopDialogField("uiCritterPowerName", "Power", string.Empty, "Natural Weapon"),
                    new DesktopDialogField("uiCritterPowerRating", "Rating", "1", "1", InputType: "number")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "matrix_program_add" => new DesktopDialogState(
                "dialog.ui.matrix_program_add",
                "Add Program / Cyberdeck Item",
                null,
                [
                    new DesktopDialogField("uiMatrixProgramName", "Program", string.Empty, "Armor"),
                    new DesktopDialogField("uiMatrixProgramSlot", "Slot", "Common", "Common")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "skill_add" => new DesktopDialogState(
                "dialog.ui.skill_add",
                "Add Skill",
                null,
                [new DesktopDialogField("uiSkillName", "Skill", string.Empty, "Perception")],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "skill_specialize" => new DesktopDialogState(
                "dialog.ui.skill_specialize",
                "Specialize Skill",
                null,
                [new DesktopDialogField("uiSkillSpec", "Specialization", string.Empty, "Visual")],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "skill_remove" => new DesktopDialogState(
                "dialog.ui.skill_remove",
                "Remove Skill",
                "Removed selected skill.",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "skill_group" => new DesktopDialogState(
                "dialog.ui.skill_group",
                "Skill Group",
                "Opened skill group assignment.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "combat_add_weapon" => new DesktopDialogState(
                "dialog.ui.combat_add_weapon",
                "Add Weapon",
                null,
                [new DesktopDialogField("uiWeaponName", "Weapon", string.Empty, "Colt M23")],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "combat_add_armor" => new DesktopDialogState(
                "dialog.ui.combat_add_armor",
                "Add Armor",
                null,
                [new DesktopDialogField("uiArmorName", "Armor", string.Empty, "Armor Jacket")],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "combat_reload" => new DesktopDialogState(
                "dialog.ui.combat_reload",
                "Reload Weapon",
                "Reloaded selected weapon.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "combat_damage_track" => new DesktopDialogState(
                "dialog.ui.combat_damage_track",
                "Damage Track",
                "Applied one damage track step.",
                [],
                [new DesktopDialogAction("close", "Close", true)]),
            "vehicle_add" => new DesktopDialogState(
                "dialog.ui.vehicle_add",
                "Add Vehicle / Drone",
                null,
                [
                    new DesktopDialogField("uiVehicleName", "Vehicle", string.Empty, "Hyundai Shin-Hyung"),
                    new DesktopDialogField("uiVehicleRole", "Role", "Vehicle", "Vehicle")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "vehicle_edit" => new DesktopDialogState(
                "dialog.ui.vehicle_edit",
                "Edit Vehicle / Drone",
                null,
                [
                    new DesktopDialogField("uiVehicleEditName", "Vehicle", "Selected Vehicle", "Selected Vehicle"),
                    new DesktopDialogField("uiVehicleHandling", "Handling", "4", "4", InputType: "number")
                ],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "vehicle_delete" => new DesktopDialogState(
                "dialog.ui.vehicle_delete",
                "Remove Vehicle / Drone",
                "Removed selected vehicle or drone.",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "vehicle_mod_add" => new DesktopDialogState(
                "dialog.ui.vehicle_mod_add",
                "Add Vehicle Mod",
                null,
                [
                    new DesktopDialogField("uiVehicleModName", "Modification", string.Empty, "Spoof Chips"),
                    new DesktopDialogField("uiVehicleModSlot", "Slot", "Body", "Body")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "contact_add" => new DesktopDialogState(
                "dialog.ui.contact_add",
                "Add Contact",
                null,
                [new DesktopDialogField("uiContactName", "Name", string.Empty, "Contact Name")],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "contact_edit" => new DesktopDialogState(
                "dialog.ui.contact_edit",
                "Edit Contact",
                null,
                [new DesktopDialogField("uiContactEditName", "Name", "Selected Contact", "Selected Contact")],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "contact_remove" => new DesktopDialogState(
                "dialog.ui.contact_remove",
                "Remove Contact",
                "Removed selected contact.",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "contact_connection" => new DesktopDialogState(
                "dialog.ui.contact_connection",
                "Connection / Loyalty",
                null,
                [
                    new DesktopDialogField("uiContactConnection", "Connection", "3", "3", InputType: "number"),
                    new DesktopDialogField("uiContactLoyalty", "Loyalty", "3", "3", InputType: "number")
                ],
                [
                    new DesktopDialogAction("apply", "Apply", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "quality_add" => new DesktopDialogState(
                "dialog.ui.quality_add",
                "Add Quality",
                null,
                [
                    new DesktopDialogField("uiQualityName", "Quality", string.Empty, "First Impression"),
                    new DesktopDialogField("uiQualityType", "Type", "Positive", "Positive")
                ],
                [
                    new DesktopDialogAction("add", "Add", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            "quality_delete" => new DesktopDialogState(
                "dialog.ui.quality_delete",
                "Remove Quality",
                "Removed selected quality.",
                [],
                [
                    new DesktopDialogAction("delete", "Delete", true),
                    new DesktopDialogAction("cancel", "Cancel")
                ]),
            _ => new DesktopDialogState(
                "dialog.ui.generic",
                "Desktop Control",
                $"Desktop control '{controlId}' triggered.",
                [],
                [new DesktopDialogAction("close", "Close", true)])
        };
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
                new DesktopDialogField("root", "Data Root", "/app/data", "/app/data", IsReadOnly: true)
            ];
        }

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

        List<DesktopDialogField> fields =
        [
            new DesktopDialogField("root", "Data Root", "/app/data", "/app/data", IsReadOnly: true),
            new DesktopDialogField("masterIndexSourcebooks", "Sourcebooks", masterIndex.SourcebookCount.ToString(), "0", IsReadOnly: true),
            new DesktopDialogField("masterIndexReferenceCoverage", "Snippet Coverage", $"{masterIndex.ReferenceCoveragePercent}% ({masterIndex.SourcebooksWithSnippets}/{masterIndex.SourcebookCount})", "0%", IsReadOnly: true),
            new DesktopDialogField("masterIndexReferenceSources", "Reference Sources", referenceSources, referenceSources, IsReadOnly: true),
            new DesktopDialogField("masterIndexReferenceSourceReceipt", "Reference Source Receipt", masterIndex.ReferenceSourceLaneReceipt, masterIndex.ReferenceSourceLaneReceipt, IsReadOnly: true),
            new DesktopDialogField("masterIndexSettingsLane", "Settings Lane", masterIndex.SettingsLanePosture, masterIndex.SettingsLanePosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexSourceToggleLane", "Source Toggle Lane", masterIndex.SourceToggleLanePosture, masterIndex.SourceToggleLanePosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexSourceSelectionReceipt", "Source Selection Receipt", masterIndex.SourceSelectionLaneReceipt, masterIndex.SourceSelectionLaneReceipt, IsReadOnly: true),
            new DesktopDialogField("masterIndexSourceSelectionSummary", "Source Selection Summary", sourcebookSelectionSummary, sourcebookSelectionSummary, IsReadOnly: true),
            new DesktopDialogField("masterIndexCustomDataLane", "Custom Data Lane", masterIndex.CustomDataLanePosture, masterIndex.CustomDataLanePosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexCustomDataAuthoringReceipt", "Custom Data Authoring Receipt", masterIndex.CustomDataAuthoringLaneReceipt, masterIndex.CustomDataAuthoringLaneReceipt, IsReadOnly: true),
            new DesktopDialogField("masterIndexXmlBridgeReceipt", "XML Bridge Receipt", masterIndex.XmlBridgeLaneReceipt, masterIndex.XmlBridgeLaneReceipt, IsReadOnly: true),
            new DesktopDialogField("masterIndexTranslatorLane", "Translator Lane", masterIndex.TranslatorLanePosture, masterIndex.TranslatorLanePosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexTranslatorReceipt", "Translator Receipt", masterIndex.TranslatorLaneReceipt, masterIndex.TranslatorLaneReceipt, IsReadOnly: true),
            new DesktopDialogField("masterIndexImportOracleLane", "Import Oracle Lane", $"{masterIndex.ImportOracleLanePosture} ({importCoverage})", masterIndex.ImportOracleLanePosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexImportOracleReceipt", "Import Oracle Receipt", masterIndex.ImportOracleLaneReceipt, masterIndex.ImportOracleLaneReceipt, IsReadOnly: true),
            new DesktopDialogField("masterIndexImportOracleMatrix", "Import Oracle Matrix", importOracleMatrix, importOracleMatrix, IsReadOnly: true),
            new DesktopDialogField("masterIndexImportOracleMissingSources", "Import Oracle Missing Sources", missingImportSources, missingImportSources, IsReadOnly: true),
            new DesktopDialogField("masterIndexAdjacentSr6OracleLane", "Adjacent SR6 Oracle Lane", $"{masterIndex.AdjacentSr6OracleReceiptPosture} ({adjacentOracleCoverage})", masterIndex.AdjacentSr6OracleReceiptPosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexAdjacentSr6OracleReceipt", "Adjacent SR6 Oracle Receipt", masterIndex.AdjacentSr6OracleLaneReceipt, masterIndex.AdjacentSr6OracleLaneReceipt, IsReadOnly: true),
            new DesktopDialogField("masterIndexOnlineStorageLane", "Online Storage Lane", $"{masterIndex.OnlineStorageLanePosture}/{masterIndex.OnlineStorageReceiptPosture} ({onlineStorageCoverage})", masterIndex.OnlineStorageLanePosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexOnlineStorageCoverage", "Online Storage Coverage", onlineStorageCoverage, onlineStorageCoverage, IsReadOnly: true),
            new DesktopDialogField("masterIndexOnlineStorageReceipt", "Online Storage Receipt", masterIndex.OnlineStorageLaneReceipt, masterIndex.OnlineStorageLaneReceipt, IsReadOnly: true),
            new DesktopDialogField("masterIndexSr6SuccessorLane", "SR6 Successor Lane", sr6Successor, sr6Successor, IsReadOnly: true),
            new DesktopDialogField("masterIndexSr6SupplementLane", "SR6 Supplement Lane", masterIndex.Sr6SupplementLanePosture, masterIndex.Sr6SupplementLanePosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexSr6DesignerToolsLane", "SR6 Designer Tools Lane", masterIndex.Sr6DesignerToolsPosture, masterIndex.Sr6DesignerToolsPosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexSr6DesignerCoverage", "SR6 Designer Coverage", sr6DesignerCoverage, sr6DesignerCoverage, IsReadOnly: true),
            new DesktopDialogField("masterIndexHouseRuleLane", "House-Rule Lane", masterIndex.HouseRuleLanePosture, masterIndex.HouseRuleLanePosture, IsReadOnly: true),
            new DesktopDialogField("masterIndexHouseRuleOverlayCount", "House-Rule Overlay Count", masterIndex.HouseRuleOverlayCount.ToString(), "0", IsReadOnly: true),
            new DesktopDialogField("masterIndexSr6SuccessorReceipt", "SR6 Successor Receipt", masterIndex.Sr6SuccessorLaneReceipt, masterIndex.Sr6SuccessorLaneReceipt, IsReadOnly: true)
        ];

        fields.AddRange(BuildSourcebookSelectionFields(masterIndex.Sourcebooks));
        return fields;
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
                IsReadOnly: true);
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
