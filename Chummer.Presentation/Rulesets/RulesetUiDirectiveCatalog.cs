using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;

namespace Chummer.Presentation.Rulesets;

public sealed record RulesetUiDirective(
    string RulesetId,
    string DisplayName,
    string PostureLabel,
    string FileExtension,
    string HomeSpotlight,
    string ResumeLaneSummary,
    string OpenWorkspaceLabel,
    string BuildFollowThroughLabel,
    string WorkspaceFollowThroughLabel,
    string NextActionPrefix,
    string DefaultSectionSummary,
    string BuildLabSectionSummary,
    string RulesSectionSummary,
    string UngroundedHomeSummary,
    string PinnedRuntimeHomeSummary,
    string GroundedHomeSummary,
    IReadOnlyList<string> BuildExplainWatchouts);

public static class RulesetUiDirectiveCatalog
{
    private static readonly HashSet<string> CatalogOnlyLoadedRunnerTabIds = new(StringComparer.Ordinal)
    {
        "tab-create",
        "tab-rules"
    };

    private static readonly RulesetUiDirective Generic = new(
        RulesetId: "shared",
        DisplayName: "Shared shell",
        PostureLabel: "cross-ruleset",
        FileExtension: "ruleset codecs",
        HomeSpotlight: "Select a ruleset before using build, rules, export, or release tools.",
        ResumeLaneSummary: "Resume the current dossier after ruleset and runtime status are loaded.",
        OpenWorkspaceLabel: "Open dossier",
        BuildFollowThroughLabel: "Open build details",
        WorkspaceFollowThroughLabel: "Open dossier details",
        NextActionPrefix: "Next step",
        DefaultSectionSummary: "Select or restore a ruleset before using rules, build, or export tools.",
        BuildLabSectionSummary: "Choose a ruleset to load the matching builder and browser.",
        RulesSectionSummary: "Choose a ruleset to load the matching rules diagnostics.",
        UngroundedHomeSummary: "the first restored dossier still needs runtime status before build, rules, and export tools are ready.",
        PinnedRuntimeHomeSummary: "runtime status is loaded; open a ruleset section to continue.",
        GroundedHomeSummary: "shared shell selected; restore a ruleset-specific dossier to continue.",
        BuildExplainWatchouts:
        [
            "Pick a ruleset before using edition-specific tools.",
            "Restore a dossier before running build, import, export, or print actions."
        ]);

    private static readonly RulesetUiDirective Sr4 = new(
        RulesetId: RulesetDefaults.Sr4,
        DisplayName: "Shadowrun 4",
        PostureLabel: "import tools",
        FileExtension: ".chum4",
        HomeSpotlight: "SR4 opens to import intake and character review before full editing.",
        ResumeLaneSummary: "Resume the SR4 runner with import details visible.",
        OpenWorkspaceLabel: "Open SR4 runner",
        BuildFollowThroughLabel: "Open SR4 intake details",
        WorkspaceFollowThroughLabel: "Open SR4 runner details",
        NextActionPrefix: "SR4 intake",
        DefaultSectionSummary: "SR4 starts with import review before full editing.",
        BuildLabSectionSummary: "SR4 intake focuses on imported runners, safe export paths, and codec-backed flows.",
        RulesSectionSummary: "SR4 rules and validation are limited until the SR4 engine is complete.",
        UngroundedHomeSummary: "import details come before full editor claims.",
        PinnedRuntimeHomeSummary: "SR4 runtime status is loaded; review import details before editing.",
        GroundedHomeSummary: "SR4 import tools keep intake, export, and rules review clear.",
        BuildExplainWatchouts:
        [
            "SR4 is import-first, so review import and export details before editing.",
            "SR4 rules and validation are still limited."
        ]);

    private static readonly RulesetUiDirective Sr5 = new(
        RulesetId: RulesetDefaults.Sr5,
        DisplayName: "Shadowrun 5",
        PostureLabel: "main editor",
        FileExtension: ".chum5",
        HomeSpotlight: "SR5 opens to the main character editor with runtime status visible.",
        ResumeLaneSummary: "Resume the SR5 character with runtime status visible.",
        OpenWorkspaceLabel: "Open SR5 character",
        BuildFollowThroughLabel: "Open SR5 build details",
        WorkspaceFollowThroughLabel: "Open SR5 character details",
        NextActionPrefix: "SR5",
        DefaultSectionSummary: "SR5 is the main desktop editor. Keep runtime and profile status visible.",
        BuildLabSectionSummary: "SR5 Build stays grounded in the main desktop editor with runtime and profile compatibility visible before apply, export, and campaign return.",
        RulesSectionSummary: "SR5 rules and validation show unavailable or rebind diagnostics until the rules engine is complete.",
        UngroundedHomeSummary: "the main editor still needs runtime status before build, rules, and support actions are ready.",
        PinnedRuntimeHomeSummary: "runtime status is loaded; open the refreshed rules section before making rules changes.",
        GroundedHomeSummary: "the main desktop editor keeps runtime status visible while rules work continues.",
        BuildExplainWatchouts:
        [
            "SR5 is the primary desktop editor, but runtime and profile status still matter for rules and validation.",
            "SR5 import, export, and print actions should keep .chum5 behavior explicit."
        ]);

    private static readonly RulesetUiDirective Sr6 = new(
        RulesetId: RulesetDefaults.Sr6,
        DisplayName: "Shadowrun 6",
        PostureLabel: "setup tools",
        FileExtension: ".chum6",
        HomeSpotlight: "SR6 opens to guided setup and starter kits before full editing.",
        ResumeLaneSummary: "Resume the SR6 runner with runtime status and starter-kit guidance visible.",
        OpenWorkspaceLabel: "Open SR6 runner",
        BuildFollowThroughLabel: "Open SR6 setup details",
        WorkspaceFollowThroughLabel: "Open SR6 runner details",
        NextActionPrefix: "SR6",
        DefaultSectionSummary: "SR6 starts with guided setup while deeper editor work continues.",
        BuildLabSectionSummary: "SR6 build emphasizes curated setup, edge-first flows, and guided follow-through before full editor depth.",
        RulesSectionSummary: "SR6 rules and validation stay guided until the SR6 engine is complete.",
        UngroundedHomeSummary: "starter kits and guided setup come before full editing.",
        PinnedRuntimeHomeSummary: "SR6 runtime status is loaded; use guided setup before deeper rules edits.",
        GroundedHomeSummary: "SR6 setup tools keep curated setup and rules review clear.",
        BuildExplainWatchouts:
        [
            "SR6 remains setup-first while deeper rules work continues.",
            "SR6 starter, rules, and export actions should keep .chum6 setup behavior explicit."
        ]);

    public static RulesetUiDirective Resolve(string? rulesetId)
    {
        return RulesetDefaults.NormalizeOptional(rulesetId) switch
        {
            RulesetDefaults.Sr4 => Sr4,
            RulesetDefaults.Sr5 => Sr5,
            RulesetDefaults.Sr6 => Sr6,
            _ => Generic
        };
    }

    public static string BuildComplianceRulesetSummary(string? rulesetId, ActiveRuntimeStatusProjection? activeRuntime)
    {
        RulesetUiDirective directive = Resolve(rulesetId ?? activeRuntime?.RulesetId);
        string runtimeQualifier = BuildRuntimeQualifier(directive, activeRuntime);
        return Clean($"{directive.RulesetId} ({directive.PostureLabel}; {directive.FileExtension}; {runtimeQualifier})");
    }

    public static string BuildHomeSpotlight(string? rulesetId)
        => Clean(Resolve(rulesetId).HomeSpotlight);

    public static string BuildSummaryHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Desktop Summary · SR4 Import Tools",
            RulesetDefaults.Sr5 => "Desktop Summary · SR5 Editor",
            RulesetDefaults.Sr6 => "Desktop Summary · SR6 Setup Tools",
            _ => "Desktop Summary Header"
        };
    }

    public static string BuildDesktopMarqueeEyebrow(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 import tools",
            RulesetDefaults.Sr5 => "SR5 main editor",
            RulesetDefaults.Sr6 => "SR6 setup tools",
            _ => "Desktop editor"
        };
    }

    public static string BuildDesktopMarqueeTitle(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Shadowrun 4 import character tools",
            RulesetDefaults.Sr5 => "Shadowrun 5 character editor",
            RulesetDefaults.Sr6 => "Shadowrun 6 setup character tools",
            _ => "Character editor"
        };
    }

    public static string BuildOpenWorkspacesHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Roster",
            RulesetDefaults.Sr5 => "SR5 Roster",
            RulesetDefaults.Sr6 => "SR6 Roster",
            _ => "Open Dossiers"
        };
    }

    public static string BuildWorkspaceStripEmptyState(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "No open SR4 dossier",
            RulesetDefaults.Sr5 => "No open SR5 dossier",
            RulesetDefaults.Sr6 => "No open SR6 dossier",
            _ => "No open dossier"
        };
    }

    public static string BuildWorkspaceStripTitle(string? rulesetId, string workspaceId, bool hasSavedWorkspace)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        string saveState = hasSavedWorkspace ? "saved" : "unsaved";
        return Clean($"{directive.DisplayName} {directive.PostureLabel} dossier {workspaceId} is {saveState}.");
    }

    public static string BuildNavigationTabsHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Import Tabs",
            RulesetDefaults.Sr5 => "SR5 Editor Tabs",
            RulesetDefaults.Sr6 => "SR6 Setup Tabs",
            _ => "Workspace Tabs"
        };
    }

    public static string BuildSectionActionsHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Import Actions",
            RulesetDefaults.Sr5 => "SR5 Editor Actions",
            RulesetDefaults.Sr6 => "SR6 Setup Actions",
            _ => "Section Actions"
        };
    }

    public static string BuildWorkflowSurfacesHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Import Flows",
            RulesetDefaults.Sr5 => "SR5 Editor Flows",
            RulesetDefaults.Sr6 => "SR6 Setup Flows",
            _ => "Workflow Surfaces"
        };
    }

    public static string BuildImportHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Import SR4 Dossier File",
            RulesetDefaults.Sr5 => "Import SR5 Dossier File",
            RulesetDefaults.Sr6 => "Import SR6 Dossier File",
            _ => "Import Dossier File"
        };
    }

    public static string BuildImportAcceptAttribute(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        string[] nativeExtensions = [Sr4.FileExtension, Sr5.FileExtension, Sr6.FileExtension];
        IEnumerable<string> orderedNativeExtensions = directive.FileExtension.StartsWith(".", StringComparison.Ordinal)
            ? new[] { directive.FileExtension }.Concat(nativeExtensions.Where(extension => !string.Equals(extension, directive.FileExtension, StringComparison.Ordinal)))
            : nativeExtensions;
        return string.Join(",", orderedNativeExtensions.Concat([".xml", "text/xml", "application/xml"]));
    }

    public static string BuildImportHint(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Primary format: .chum4 with XML fallback.",
            RulesetDefaults.Sr5 => "Primary format: .chum5 with XML fallback for restores.",
            RulesetDefaults.Sr6 => "Primary format: .chum6 with preview-safe XML fallback.",
            _ => "Accept native ruleset files or raw XML when the active ruleset is still unresolved."
        };
    }

    public static string BuildImportFilePlaceholder(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "(no SR4 dossier file selected)",
            RulesetDefaults.Sr5 => "(no SR5 dossier file selected)",
            RulesetDefaults.Sr6 => "(no SR6 dossier file selected)",
            _ => "(no dossier file selected)"
        };
    }

    public static string BuildImportDebugHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Dossier XML Review",
            RulesetDefaults.Sr5 => "SR5 Dossier XML Review",
            RulesetDefaults.Sr6 => "SR6 Dossier XML Review",
            _ => "Raw Dossier XML Review"
        };
    }

    public static string BuildImportRawActionLabel(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Import SR4 Dossier XML",
            RulesetDefaults.Sr5 => "Import SR5 Dossier XML",
            RulesetDefaults.Sr6 => "Import SR6 Dossier XML",
            _ => "Import Dossier XML"
        };
    }

    public static string BuildCommandHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Import Tools",
            RulesetDefaults.Sr5 => "SR5 Editor Commands",
            RulesetDefaults.Sr6 => "SR6 Setup Tools",
            _ => "Commands"
        };
    }

    public static string BuildCommandEmptyHint(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "No SR4 import tools are currently available.",
            RulesetDefaults.Sr5 => "No SR5 editor commands are currently available.",
            RulesetDefaults.Sr6 => "No SR6 setup tools are currently available.",
            _ => "No commands are currently available."
        };
    }

    public static string BuildResultHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Import Summary",
            RulesetDefaults.Sr5 => "SR5 Editor Result",
            RulesetDefaults.Sr6 => "SR6 Setup Summary",
            _ => "Result"
        };
    }

    public static string BuildResultPostureHint(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return Clean(directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Shadowrun 4 starts with import tools and character review.",
            RulesetDefaults.Sr5 => "Shadowrun 5 uses the main desktop editor.",
            RulesetDefaults.Sr6 => "Shadowrun 6 starts with guided setup tools.",
            _ => "Choose a ruleset before relying on rules or export details."
        });
    }

    public static string BuildResultReadyNotice(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return Clean(directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 import tools are ready.",
            RulesetDefaults.Sr5 => "SR5 editor is ready.",
            RulesetDefaults.Sr6 => "SR6 setup tools are ready.",
            _ => "Ready."
        });
    }

    public static string FormatNavigationTabLabel(string? rulesetId, string? tabId, string fallbackLabel)
    {
        string normalizedTabId = RulesetDefaults.NormalizeOptional(tabId) ?? string.Empty;
        RulesetUiDirective directive = Resolve(rulesetId);

        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 when string.Equals(normalizedTabId, "tab-create", StringComparison.Ordinal) => "Import",
            RulesetDefaults.Sr4 when string.Equals(normalizedTabId, "tab-info", StringComparison.Ordinal) => "Character",
            RulesetDefaults.Sr4 when string.Equals(normalizedTabId, "tab-gear", StringComparison.Ordinal) => "Gear",
            RulesetDefaults.Sr4 when string.Equals(normalizedTabId, "tab-rules", StringComparison.Ordinal) => "Rules",

            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-create", StringComparison.Ordinal) => "Character",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-info", StringComparison.Ordinal) => "Runner",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-gear", StringComparison.Ordinal) => "Gear & Ware",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-rules", StringComparison.Ordinal) => "Rules",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-calendar", StringComparison.Ordinal) => "Career Log",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-improvements", StringComparison.Ordinal) => "Career Track",

            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-create", StringComparison.Ordinal) => "Create",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-info", StringComparison.Ordinal) => "Character",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-gear", StringComparison.Ordinal) => "Gear",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-rules", StringComparison.Ordinal) => "Rules",
            _ => Clean(fallbackLabel)
        };
    }

    public static bool IsLoadedRunnerVisibleNavigationTab(string? tabId)
    {
        string? normalizedTabId = NormalizeShellId(tabId);
        return normalizedTabId is not null
            && !CatalogOnlyLoadedRunnerTabIds.Contains(normalizedTabId);
    }

    private static string? NormalizeShellId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    public static string FormatWorkspaceActionLabel(string? rulesetId, string? actionId, string? targetId, string fallbackLabel)
    {
        string normalizedActionId = RulesetDefaults.NormalizeOptional(actionId) ?? string.Empty;
        string normalizedTargetId = RulesetDefaults.NormalizeOptional(targetId) ?? string.Empty;
        RulesetUiDirective directive = Resolve(rulesetId);

        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-create.intake", StringComparison.Ordinal) => "Import",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-info.summary", StringComparison.Ordinal) => "Character Summary",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Character Review",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-info.metadata", StringComparison.Ordinal) => "Edit Metadata",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-rules.rules", StringComparison.Ordinal) => "Rules",
            RulesetDefaults.Sr4 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Gear",

            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-create.intake", StringComparison.Ordinal) => "Create Character",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.summary", StringComparison.Ordinal) => "Character Summary",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Review Character",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.rules", StringComparison.Ordinal) => "Rules",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.build", StringComparison.Ordinal) => "Build Plan",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.progress", StringComparison.Ordinal) => "Career Track",
            RulesetDefaults.Sr5 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Gear",

            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-create.intake", StringComparison.Ordinal) => "Create Character",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.summary", StringComparison.Ordinal) => "Character Summary",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Review Character",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.profile", StringComparison.Ordinal) => "Character Card",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-rules.rules", StringComparison.Ordinal) => "Rules",
            RulesetDefaults.Sr6 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Gear",
            _ => Clean(fallbackLabel)
        };
    }

    public static string BuildSectionNotice(
        string? rulesetId,
        string? sectionId,
        string? actionId,
        ActiveRuntimeStatusProjection? activeRuntime)
    {
        RulesetUiDirective directive = Resolve(rulesetId ?? activeRuntime?.RulesetId);
        string runtimeQualifier = BuildRuntimeQualifier(directive, activeRuntime);
        string sectionSummary = ResolveSectionSummary(directive, sectionId, actionId);
        return Clean($"{directive.DisplayName}: {sectionSummary} Runtime: {runtimeQualifier}.");
    }

    public static string BuildUngroundedRulePosture(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return Clean($"{directive.DisplayName} · {directive.FileExtension} · {directive.UngroundedHomeSummary}");
    }

    public static string BuildPinnedRuntimeRulePosture(string? rulesetId, string runtimeFingerprint)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return Clean($"{directive.DisplayName} · {directive.FileExtension} · {directive.PinnedRuntimeHomeSummary} · fingerprint {runtimeFingerprint}.");
    }

    public static string BuildGroundedRulePosture(
        string? rulesetId,
        string? gameEdition,
        string? settings,
        string? gameplayMode,
        string runtimeFingerprint,
        string installState)
    {
        RulesetUiDirective directive = Resolve(rulesetId ?? gameEdition);
        string resolvedSettings = string.IsNullOrWhiteSpace(settings) ? "default rules profile" : settings;
        string resolvedGameplayMode = string.IsNullOrWhiteSpace(gameplayMode) ? "default gameplay settings" : gameplayMode;
        return Clean($"{directive.DisplayName} · {resolvedSettings} · {resolvedGameplayMode} · {directive.FileExtension} · {directive.GroundedHomeSummary} · fingerprint {runtimeFingerprint} · install {installState}.");
    }

    public static IReadOnlyList<string> BuildBuildExplainWatchouts(string? rulesetId)
    {
        return UndetectableHumanizerCopyAdapter.HumanizeLines(Resolve(rulesetId).BuildExplainWatchouts);
    }

    public static string BuildWorkspaceResumeSummary(
        string? rulesetId,
        CharacterFileSummary summary,
        DateTimeOffset lastUpdatedUtc)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        string name = string.IsNullOrWhiteSpace(summary.Name) ? "Unnamed runner" : summary.Name;
        string alias = string.IsNullOrWhiteSpace(summary.Alias) ? string.Empty : $" / {summary.Alias}";
        string metatype = string.IsNullOrWhiteSpace(summary.Metatype) ? "metatype unresolved" : summary.Metatype;
        string buildMethod = string.IsNullOrWhiteSpace(summary.BuildMethod) ? "build method unresolved" : summary.BuildMethod;
        string updatedAt = lastUpdatedUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm");
        return Clean($"{directive.DisplayName} resume: {name}{alias} · {metatype} · {buildMethod} · {directive.ResumeLaneSummary} Updated {updatedAt} UTC.");
    }

    public static string BuildWorkspaceNavigatorLabel(string? rulesetId, string? name, string? alias, bool hasSavedWorkspace)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        string resolvedName = string.IsNullOrWhiteSpace(name) ? "Unnamed runner" : name;
        string resolvedAlias = string.IsNullOrWhiteSpace(alias) ? string.Empty : $" ({alias})";
        string saveState = hasSavedWorkspace ? "saved" : "unsaved";
        return Clean($"{resolvedName}{resolvedAlias} · {directive.DisplayName} · {directive.PostureLabel} · {saveState}");
    }

    public static string BuildOpenWorkspaceActionLabel(string? rulesetId, string fallbackLabel)
    {
        string label = Resolve(rulesetId).OpenWorkspaceLabel;
        return string.IsNullOrWhiteSpace(label) ? fallbackLabel : label;
    }

    public static string BuildBuildFollowThroughActionLabel(string? rulesetId, string fallbackLabel)
    {
        string label = Resolve(rulesetId).BuildFollowThroughLabel;
        return string.IsNullOrWhiteSpace(label) ? fallbackLabel : label;
    }

    public static string BuildWorkspaceFollowThroughActionLabel(string? rulesetId, string fallbackLabel)
    {
        string label = Resolve(rulesetId).WorkspaceFollowThroughLabel;
        return string.IsNullOrWhiteSpace(label) ? fallbackLabel : label;
    }

    public static string? BuildNextActionPrefix(string? rulesetId)
    {
        string prefix = Resolve(rulesetId).NextActionPrefix;
        return string.IsNullOrWhiteSpace(prefix) ? null : prefix;
    }

    public static string FormatDialogNotice(string? rulesetId, string notice)
    {
        if (string.IsNullOrWhiteSpace(notice))
        {
            return notice;
        }

        return Resolve(rulesetId).RulesetId switch
        {
            RulesetDefaults.Sr4 => Clean($"SR4 import tools: {notice}"),
            RulesetDefaults.Sr5 => Clean($"SR5 editor: {notice}"),
            RulesetDefaults.Sr6 => Clean($"SR6 setup tools: {notice}"),
            _ => Clean(notice)
        };
    }

    public static string FormatWorkflowSurfaceLabel(string? rulesetId, string? actionId, string fallbackLabel)
    {
        if (HasRulesetSpecificLabel(fallbackLabel))
        {
            return Clean(fallbackLabel);
        }

        string normalizedActionId = RulesetDefaults.NormalizeOptional(actionId) ?? string.Empty;
        RulesetUiDirective directive = Resolve(rulesetId);

        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Character Review",
            RulesetDefaults.Sr4 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Character Summary",

            RulesetDefaults.Sr5 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Review",
            RulesetDefaults.Sr5 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Character Summary",

            RulesetDefaults.Sr6 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Review",
            RulesetDefaults.Sr6 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Character Summary",
            _ => Clean(fallbackLabel)
        };
    }

    private static string ResolveSectionSummary(RulesetUiDirective directive, string? sectionId, string? actionId)
    {
        string normalizedSectionId = RulesetDefaults.NormalizeOptional(sectionId) ?? string.Empty;
        string normalizedActionId = RulesetDefaults.NormalizeOptional(actionId) ?? string.Empty;

        if (string.Equals(normalizedSectionId, "build-lab", StringComparison.Ordinal)
            || normalizedActionId.Contains(".intake", StringComparison.Ordinal))
        {
            return directive.BuildLabSectionSummary;
        }

        if (string.Equals(normalizedSectionId, "rules", StringComparison.Ordinal)
            || string.Equals(normalizedSectionId, "validate", StringComparison.Ordinal)
            || normalizedActionId.Contains("validate", StringComparison.Ordinal)
            || normalizedActionId.Contains(".rules", StringComparison.Ordinal))
        {
            return directive.RulesSectionSummary;
        }

        return directive.DefaultSectionSummary;
    }

    private static string BuildRuntimeQualifier(RulesetUiDirective directive, ActiveRuntimeStatusProjection? activeRuntime)
    {
        bool hasWarnings = activeRuntime is { WarningCount: > 0 };

        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => hasWarnings
                ? "preview runtime attention required"
                : "import runtime is limited",
            RulesetDefaults.Sr5 => hasWarnings
                ? "runtime attention required"
                : "runtime service is available",
            RulesetDefaults.Sr6 => hasWarnings
                ? "runtime warnings remain active"
                : "setup runtime is available",
            _ => hasWarnings
                ? "runtime attention required"
                : "ruleset still unresolved"
        };
    }

    private static string Clean(string value)
        => UndetectableHumanizerCopyAdapter.Humanize(value);

    private static bool HasRulesetSpecificLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        return label.Contains("SR4", StringComparison.OrdinalIgnoreCase)
            || label.Contains("SR5", StringComparison.OrdinalIgnoreCase)
            || label.Contains("SR6", StringComparison.OrdinalIgnoreCase)
            || label.Contains("Shadowrun", StringComparison.OrdinalIgnoreCase)
            || label.Contains("oracle", StringComparison.OrdinalIgnoreCase)
            || label.Contains("workbench", StringComparison.OrdinalIgnoreCase)
            || label.Contains("import tools", StringComparison.OrdinalIgnoreCase)
            || label.Contains("setup tools", StringComparison.OrdinalIgnoreCase)
            || label.Contains("starter", StringComparison.OrdinalIgnoreCase)
            || label.Contains("editor", StringComparison.OrdinalIgnoreCase)
            || label.Contains("preview", StringComparison.OrdinalIgnoreCase);
    }
}
