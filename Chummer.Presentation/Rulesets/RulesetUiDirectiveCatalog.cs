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
    private static readonly RulesetUiDirective Generic = new(
        RulesetId: "shared",
        DisplayName: "Shared shell",
        PostureLabel: "cross-ruleset",
        FileExtension: "ruleset codecs",
        HomeSpotlight: "Select a ruleset before trusting build, rules, export, or release results.",
        ResumeLaneSummary: "Resume the current character only after ruleset and runtime status are confirmed.",
        OpenWorkspaceLabel: "Open grounded workspace",
        BuildFollowThroughLabel: "Open build details",
        WorkspaceFollowThroughLabel: "Open character details",
        NextActionPrefix: "Next step",
        DefaultSectionSummary: "Select or restore a grounded ruleset before you trust rules, build, or export depth.",
        BuildLabSectionSummary: "Build stays generic until a grounded ruleset drives creation, browsing, and runtime compatibility.",
        RulesSectionSummary: "Rules and validation stay generic until a grounded ruleset restores the matching runtime and capability diagnostics.",
        UngroundedHomeSummary: "the first restored character still needs a real runtime fingerprint before build, explain, and export depth are trustworthy.",
        PinnedRuntimeHomeSummary: "a runtime fingerprint is pinned, but the rules-aware surface still needs grounded section state before it is trustworthy.",
        GroundedHomeSummary: "shared-shell posture only; restore a grounded ruleset before claiming per-ruleset completion.",
        BuildExplainWatchouts:
        [
            "Shared shell posture alone is not proof that any one ruleset is fully implemented.",
            "Restore a grounded ruleset and runtime before you trust build, explain, import, export, or print depth."
        ]);

    private static readonly RulesetUiDirective Sr4 = new(
        RulesetId: RulesetDefaults.Sr4,
        DisplayName: "Shadowrun 4",
        PostureLabel: "import tools",
        FileExtension: ".chum4",
        HomeSpotlight: "SR4 opens to import intake, parity receipts, and character review before any full editor claim.",
        ResumeLaneSummary: "Resume the SR4 runner with codec proof, import receipts, and parity evidence visible.",
        OpenWorkspaceLabel: "Open SR4 runner",
        BuildFollowThroughLabel: "Open SR4 intake details",
        WorkspaceFollowThroughLabel: "Open SR4 runner details",
        NextActionPrefix: "SR4 intake",
        DefaultSectionSummary: "SR4 stays import-tools first: favor import review and parity receipts over assuming full editor parity.",
        BuildLabSectionSummary: "SR4 intake focuses on imported runners, safe export paths, and codec-backed flows.",
        RulesSectionSummary: "SR4 rules and validation stay parity-gated until deterministic providers replace the experimental host.",
        UngroundedHomeSummary: "import receipts, codec proof, and parity evidence come before full editor claims.",
        PinnedRuntimeHomeSummary: "an intake/runtime fingerprint is pinned, but codec and parity proof still gate trustworthy rules and workflow depth.",
        GroundedHomeSummary: "import-tools posture keeps intake, export, and explain honest while parity proof is still growing.",
        BuildExplainWatchouts:
        [
            "SR4 remains parity-gated, so import/export and parity receipts are safer than assuming full editor parity.",
            "SR4 rules and validation still depend on experimental-provider gaps being made explicit."
        ]);

    private static readonly RulesetUiDirective Sr5 = new(
        RulesetId: RulesetDefaults.Sr5,
        DisplayName: "Shadowrun 5",
        PostureLabel: "main editor",
        FileExtension: ".chum5",
        HomeSpotlight: "SR5 opens to the main character editor with runtime and provider truth visible.",
        ResumeLaneSummary: "Resume the SR5 character with runtime and provider truth visible.",
        OpenWorkspaceLabel: "Open SR5 character",
        BuildFollowThroughLabel: "Open SR5 build details",
        WorkspaceFollowThroughLabel: "Open SR5 character details",
        NextActionPrefix: "SR5",
        DefaultSectionSummary: "SR5 is the main desktop editor. Keep runtime and profile status visible instead of implying the deterministic host gap is already closed.",
        BuildLabSectionSummary: "SR5 Build stays grounded in the main desktop editor with runtime and profile compatibility visible before apply, export, and campaign return.",
        RulesSectionSummary: "SR5 rules and validation must surface provider-unavailable or rebind diagnostics honestly until the deterministic host is complete.",
        UngroundedHomeSummary: "the main editor still needs a grounded runtime fingerprint before build, explain, and support closure are trustworthy.",
        PinnedRuntimeHomeSummary: "a runtime fingerprint is pinned, but provider truth and refreshed rules sections still gate trustworthy editor claims.",
        GroundedHomeSummary: "the main desktop editor keeps provider and runtime truth visible while deterministic-host completion is still in flight.",
        BuildExplainWatchouts:
        [
            "SR5 is the primary desktop editor, but runtime/profile honesty still matters for explain, validate, and runtime inspector flows.",
            "SR5 import, export, and print affordances should keep .chum5 reality explicit instead of implying cross-ruleset parity."
        ]);

    private static readonly RulesetUiDirective Sr6 = new(
        RulesetId: RulesetDefaults.Sr6,
        DisplayName: "Shadowrun 6",
        PostureLabel: "setup tools",
        FileExtension: ".chum6",
        HomeSpotlight: "SR6 opens to guided setup, starter kits, and explicit runtime honesty before parity claims.",
        ResumeLaneSummary: "Resume the SR6 runner with runtime honesty and starter-kit guidance visible.",
        OpenWorkspaceLabel: "Open SR6 runner",
        BuildFollowThroughLabel: "Open SR6 setup details",
        WorkspaceFollowThroughLabel: "Open SR6 runner details",
        NextActionPrefix: "SR6",
        DefaultSectionSummary: "SR6 stays setup-tools first: curated starts are safe, but the experimental host still limits depth.",
        BuildLabSectionSummary: "SR6 build emphasizes curated setup, edge-first flows, and guided follow-through rather than claiming full parity.",
        RulesSectionSummary: "SR6 rules and validation stay setup-gated until deterministic providers replace the experimental host.",
        UngroundedHomeSummary: "starter-kit proof, runtime honesty, and preview-safe guidance come before full parity claims.",
        PinnedRuntimeHomeSummary: "a guided-runtime fingerprint is pinned, but experimental-host honesty still gates trustworthy rules and workflow depth.",
        GroundedHomeSummary: "setup-tools posture keeps curated setup and experimental-host honesty visible while deterministic providers are still landing.",
        BuildExplainWatchouts:
        [
            "SR6 remains setup-gated until deterministic providers replace the experimental host.",
            "SR6 starter, rules, and export affordances should keep .chum6 setup-tools posture explicit."
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
        return $"{directive.RulesetId} ({directive.PostureLabel}; {directive.FileExtension}; {runtimeQualifier})";
    }

    public static string BuildHomeSpotlight(string? rulesetId)
        => Resolve(rulesetId).HomeSpotlight;

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
            RulesetDefaults.Sr5 => "SR5 Characters",
            RulesetDefaults.Sr6 => "SR6 Roster",
            _ => "Open Characters"
        };
    }

    public static string BuildWorkspaceStripEmptyState(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "No open SR4 character",
            RulesetDefaults.Sr5 => "No open SR5 character",
            RulesetDefaults.Sr6 => "No open SR6 character",
            _ => "No open character"
        };
    }

    public static string BuildWorkspaceStripTitle(string? rulesetId, string workspaceId, bool hasSavedWorkspace)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        string saveState = hasSavedWorkspace ? "saved" : "unsaved";
        return $"{directive.DisplayName} {directive.PostureLabel} workspace {workspaceId} is {saveState}.";
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
            RulesetDefaults.Sr4 => "Import SR4 Character File",
            RulesetDefaults.Sr5 => "Import SR5 Character File",
            RulesetDefaults.Sr6 => "Import SR6 Character File",
            _ => "Import Character File"
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
            RulesetDefaults.Sr4 => "Primary format: .chum4 with parity-safe XML fallback.",
            RulesetDefaults.Sr5 => "Primary format: .chum5 with XML fallback for governed restores.",
            RulesetDefaults.Sr6 => "Primary format: .chum6 with preview-safe XML fallback.",
            _ => "Accept native ruleset files or raw XML when the active ruleset is still unresolved."
        };
    }

    public static string BuildImportFilePlaceholder(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "(no SR4 character file selected)",
            RulesetDefaults.Sr5 => "(no SR5 character file selected)",
            RulesetDefaults.Sr6 => "(no SR6 character file selected)",
            _ => "(none)"
        };
    }

    public static string BuildImportDebugHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Oracle Debug Import",
            RulesetDefaults.Sr5 => "SR5 XML Import",
            RulesetDefaults.Sr6 => "SR6 XML Import",
            _ => "Raw XML Debug Import"
        };
    }

    public static string BuildImportRawActionLabel(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Import SR4 Raw XML",
            RulesetDefaults.Sr5 => "Import SR5 Raw XML",
            RulesetDefaults.Sr6 => "Import SR6 Raw XML",
            _ => "Import Raw XML"
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
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Shadowrun 4 stays in import-tools mode; keep codec and parity evidence explicit.",
            RulesetDefaults.Sr5 => "Shadowrun 5 stays on the main desktop editor; keep provider truth and .chum5 continuity explicit.",
            RulesetDefaults.Sr6 => "Shadowrun 6 stays in setup-tools mode; keep starter-kit and runtime honesty explicit.",
            _ => "Shared shell posture is still unresolved; keep runtime and ruleset evidence explicit."
        };
    }

    public static string BuildResultReadyNotice(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 import tools are ready; keep import and parity evidence explicit.",
            RulesetDefaults.Sr5 => "SR5 editor is ready; keep provider truth and .chum5 continuity explicit.",
            RulesetDefaults.Sr6 => "SR6 setup tools are ready; keep runtime honesty explicit.",
            _ => "Ready."
        };
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
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-rules", StringComparison.Ordinal) => "Rules & Validation",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-calendar", StringComparison.Ordinal) => "Career Log",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-improvements", StringComparison.Ordinal) => "Career Track",

            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-create", StringComparison.Ordinal) => "Create",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-info", StringComparison.Ordinal) => "Character",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-gear", StringComparison.Ordinal) => "Gear",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-rules", StringComparison.Ordinal) => "Rules",
            _ => fallbackLabel
        };
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
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Parity Check",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-info.metadata", StringComparison.Ordinal) => "Edit Metadata",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-rules.rules", StringComparison.Ordinal) => "Rules",
            RulesetDefaults.Sr4 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Gear",

            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-create.intake", StringComparison.Ordinal) => "Create Character",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.summary", StringComparison.Ordinal) => "Character Summary",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Validate & Rebind",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.rules", StringComparison.Ordinal) => "Rules & Provider",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.build", StringComparison.Ordinal) => "Build Plan",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.progress", StringComparison.Ordinal) => "Career Track",
            RulesetDefaults.Sr5 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Gear",

            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-create.intake", StringComparison.Ordinal) => "Create Character",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.summary", StringComparison.Ordinal) => "Character Summary",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Safety Check",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.profile", StringComparison.Ordinal) => "Character Card",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-rules.rules", StringComparison.Ordinal) => "Rules",
            RulesetDefaults.Sr6 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Gear",
            _ => fallbackLabel
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
        return $"Ruleset posture: {directive.DisplayName} is in {directive.PostureLabel} mode. {sectionSummary} Runtime posture: {runtimeQualifier}.";
    }

    public static string BuildUngroundedRulePosture(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return $"Rule posture: {directive.DisplayName} · {directive.PostureLabel} · {directive.FileExtension} · {directive.UngroundedHomeSummary}";
    }

    public static string BuildPinnedRuntimeRulePosture(string? rulesetId, string runtimeFingerprint)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return $"Rule posture: {directive.DisplayName} · {directive.PostureLabel} · {directive.FileExtension} · {directive.PinnedRuntimeHomeSummary} · fingerprint {runtimeFingerprint}.";
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
        string resolvedGameplayMode = string.IsNullOrWhiteSpace(gameplayMode) ? "default gameplay posture" : gameplayMode;
        return $"Rule posture: {directive.DisplayName} · {directive.PostureLabel} · {resolvedSettings} · {resolvedGameplayMode} · {directive.FileExtension} · {directive.GroundedHomeSummary} · fingerprint {runtimeFingerprint} · install {installState}.";
    }

    public static IReadOnlyList<string> BuildBuildExplainWatchouts(string? rulesetId)
    {
        return Resolve(rulesetId).BuildExplainWatchouts;
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
        return $"{directive.DisplayName} resume: {name}{alias} · {metatype} · {buildMethod} · {directive.ResumeLaneSummary} Updated {updatedAt} UTC.";
    }

    public static string BuildWorkspaceNavigatorLabel(string? rulesetId, string? name, string? alias, bool hasSavedWorkspace)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        string resolvedName = string.IsNullOrWhiteSpace(name) ? "Unnamed runner" : name;
        string resolvedAlias = string.IsNullOrWhiteSpace(alias) ? string.Empty : $" ({alias})";
        string saveState = hasSavedWorkspace ? "saved" : "unsaved";
        return $"{resolvedName}{resolvedAlias} · {directive.DisplayName} · {directive.PostureLabel} · {saveState}";
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
            RulesetDefaults.Sr4 => $"SR4 import tools: {notice}",
            RulesetDefaults.Sr5 => $"SR5 editor: {notice}",
            RulesetDefaults.Sr6 => $"SR6 setup tools: {notice}",
            _ => notice
        };
    }

    public static string FormatWorkflowSurfaceLabel(string? rulesetId, string? actionId, string fallbackLabel)
    {
        if (HasRulesetSpecificLabel(fallbackLabel))
        {
            return fallbackLabel;
        }

        string normalizedActionId = RulesetDefaults.NormalizeOptional(actionId) ?? string.Empty;
        RulesetUiDirective directive = Resolve(rulesetId);

        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Parity Check",
            RulesetDefaults.Sr4 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Character Summary",

            RulesetDefaults.Sr5 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Validation",
            RulesetDefaults.Sr5 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Character Summary",

            RulesetDefaults.Sr6 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Validation",
            RulesetDefaults.Sr6 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Character Summary",
            _ => fallbackLabel
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
                : "import-tools runtime remains parity-gated",
            RulesetDefaults.Sr5 => hasWarnings
                ? "runtime/provider attention required"
                : "provider truth remains explicit",
            RulesetDefaults.Sr6 => hasWarnings
                ? "runtime warnings remain active"
                : "setup-tools runtime honesty remains visible",
            _ => hasWarnings
                ? "runtime attention required"
                : "ruleset still unresolved"
        };
    }

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
