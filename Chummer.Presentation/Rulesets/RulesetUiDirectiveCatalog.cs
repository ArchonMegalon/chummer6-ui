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
        HomeSpotlight: "Shared home cockpit still needs a grounded ruleset before build, rules, or release claims are trustworthy.",
        ResumeLaneSummary: "Shared shell resume lane still needs a grounded ruleset before the next handoff is trustworthy.",
        OpenWorkspaceLabel: "Open grounded workspace",
        BuildFollowThroughLabel: "Open build follow-through",
        WorkspaceFollowThroughLabel: "Open workspace follow-through",
        NextActionPrefix: "Grounded lane",
        DefaultSectionSummary: "Select or restore a grounded ruleset before you trust rules, build, or export depth.",
        BuildLabSectionSummary: "Build Lab stays generic until a grounded ruleset drives starter paths, browse affordances, and runtime compatibility.",
        RulesSectionSummary: "Rules and validation stay generic until a grounded ruleset restores the matching runtime and capability diagnostics.",
        UngroundedHomeSummary: "the first restored workspace still needs to pin a real runtime fingerprint before build, explain, and export depth are trustworthy.",
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
        PostureLabel: "preview/oracle-first",
        FileExtension: ".chum4",
        HomeSpotlight: "SR4 home cockpit foregrounds oracle import, codec parity, and preview-safe rules evidence before any full workbench claim.",
        ResumeLaneSummary: "Resume the SR4 oracle-preview dossier with parity receipts and codec-backed import evidence visible.",
        OpenWorkspaceLabel: "Open SR4 oracle preview",
        BuildFollowThroughLabel: "Open SR4 import follow-through",
        WorkspaceFollowThroughLabel: "Open SR4 dossier follow-through",
        NextActionPrefix: "SR4 import",
        DefaultSectionSummary: "SR4 stays preview-first: favor oracle import, codec review, and parity receipts over assuming full workbench parity.",
        BuildLabSectionSummary: "SR4 Build Lab is an oracle-import preview lane, so starter paths and export affordances should stay constrained to codec-backed flows.",
        RulesSectionSummary: "SR4 rules and validation stay preview-only until deterministic providers replace the experimental host.",
        UngroundedHomeSummary: "oracle import, codec proof, and parity receipts come before full workbench claims.",
        PinnedRuntimeHomeSummary: "a preview fingerprint is pinned, but codec and parity proof still gate trustworthy rules and workflow depth.",
        GroundedHomeSummary: "oracle-backed preview posture keeps import/export and explain honest while parity proof is still growing.",
        BuildExplainWatchouts:
        [
            "SR4 remains preview-only, so import/export and parity receipts are safer than assuming full workbench parity.",
            "SR4 rules and validation still depend on experimental-provider gaps being made explicit."
        ]);

    private static readonly RulesetUiDirective Sr5 = new(
        RulesetId: RulesetDefaults.Sr5,
        DisplayName: "Shadowrun 5",
        PostureLabel: "primary/workbench",
        FileExtension: ".chum5",
        HomeSpotlight: "SR5 home cockpit foregrounds the flagship workbench, provider truth, and release-safe .chum5 continuity.",
        ResumeLaneSummary: "Resume the SR5 workbench dossier with runtime/provider truth and full workbench follow-through visible.",
        OpenWorkspaceLabel: "Open SR5 workbench",
        BuildFollowThroughLabel: "Open SR5 workbench follow-through",
        WorkspaceFollowThroughLabel: "Open SR5 dossier follow-through",
        NextActionPrefix: "SR5 workbench",
        DefaultSectionSummary: "SR5 is the flagship workbench lane. Keep runtime/profile status visible instead of implying the deterministic host gap is already closed.",
        BuildLabSectionSummary: "SR5 Build Lab is the flagship lane, but runtime/profile compatibility still gates safe apply, export, and campaign return.",
        RulesSectionSummary: "SR5 rules and validation must surface provider-unavailable or rebind diagnostics honestly until the deterministic host is complete.",
        UngroundedHomeSummary: "the primary workbench lane still needs a grounded runtime fingerprint before build, explain, and support closure are trustworthy.",
        PinnedRuntimeHomeSummary: "a runtime fingerprint is pinned, but provider truth and refreshed rules sections still gate trustworthy workbench claims.",
        GroundedHomeSummary: "the flagship workbench lane keeps provider and runtime truth visible while deterministic-host completion is still in flight.",
        BuildExplainWatchouts:
        [
            "SR5 is the primary lane, but runtime/profile honesty still matters for explain, validate, and runtime inspector flows.",
            "SR5 import, export, and print affordances should keep .chum5 reality explicit instead of implying cross-ruleset parity."
        ]);

    private static readonly RulesetUiDirective Sr6 = new(
        RulesetId: RulesetDefaults.Sr6,
        DisplayName: "Shadowrun 6",
        PostureLabel: "beta/edge-first",
        FileExtension: ".chum6",
        HomeSpotlight: "SR6 home cockpit foregrounds starter kits, edge-first beta guidance, and experimental-host honesty before parity claims.",
        ResumeLaneSummary: "Resume the SR6 starter lane with beta/runtime honesty and curated kit guidance visible.",
        OpenWorkspaceLabel: "Open SR6 starter lane",
        BuildFollowThroughLabel: "Open SR6 starter follow-through",
        WorkspaceFollowThroughLabel: "Open SR6 dossier follow-through",
        NextActionPrefix: "SR6 starter",
        DefaultSectionSummary: "SR6 stays beta-first: curated starters and preview packets are safe, but the experimental host still limits depth.",
        BuildLabSectionSummary: "SR6 Build Lab should emphasize curated starter kits, edge-first flows, and preview-safe guidance rather than claiming full parity.",
        RulesSectionSummary: "SR6 rules and validation stay beta/preview until deterministic providers replace the experimental host.",
        UngroundedHomeSummary: "starter-kit proof, runtime honesty, and preview-safe guidance come before full parity claims.",
        PinnedRuntimeHomeSummary: "a beta fingerprint is pinned, but experimental-host honesty still gates trustworthy rules and workflow depth.",
        GroundedHomeSummary: "beta posture keeps curated starter flows and experimental-host honesty visible while deterministic providers are still landing.",
        BuildExplainWatchouts:
        [
            "SR6 remains beta/preview until deterministic providers replace the experimental host.",
            "SR6 starter, rules, and export affordances should keep .chum6 preview posture explicit."
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
            RulesetDefaults.Sr4 => "Desktop Summary · SR4 Preview",
            RulesetDefaults.Sr5 => "Desktop Summary · SR5 Workbench",
            RulesetDefaults.Sr6 => "Desktop Summary · SR6 Starter",
            _ => "Desktop Summary Header"
        };
    }

    public static string BuildDesktopMarqueeEyebrow(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Oracle intake desk",
            RulesetDefaults.Sr5 => "Flagship desktop workbench",
            RulesetDefaults.Sr6 => "Starter and beta desk",
            _ => "Grounded desktop shell"
        };
    }

    public static string BuildDesktopMarqueeTitle(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Shadowrun 4 preview import cockpit",
            RulesetDefaults.Sr5 => "Shadowrun 5 flagship workbench",
            RulesetDefaults.Sr6 => "Shadowrun 6 guided starter cockpit",
            _ => "Grounded shared-shell cockpit"
        };
    }

    public static string BuildOpenWorkspacesHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Preview Dossiers",
            RulesetDefaults.Sr5 => "SR5 Workbench Dossiers",
            RulesetDefaults.Sr6 => "SR6 Starter Dossiers",
            _ => "Open Characters"
        };
    }

    public static string BuildWorkspaceStripEmptyState(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "No open SR4 oracle preview",
            RulesetDefaults.Sr5 => "No open SR5 workbench",
            RulesetDefaults.Sr6 => "No open SR6 starter lane",
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
            RulesetDefaults.Sr4 => "SR4 Preview Tabs",
            RulesetDefaults.Sr5 => "SR5 Workbench Tabs",
            RulesetDefaults.Sr6 => "SR6 Starter Tabs",
            _ => "Workspace Tabs"
        };
    }

    public static string BuildSectionActionsHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Preview Actions",
            RulesetDefaults.Sr5 => "SR5 Workbench Actions",
            RulesetDefaults.Sr6 => "SR6 Starter Actions",
            _ => "Section Actions"
        };
    }

    public static string BuildWorkflowSurfacesHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Preview Flows",
            RulesetDefaults.Sr5 => "SR5 Workbench Flows",
            RulesetDefaults.Sr6 => "SR6 Starter Flows",
            _ => "Workflow Surfaces"
        };
    }

    public static string BuildImportHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Import SR4 Oracle File",
            RulesetDefaults.Sr5 => "Import SR5 Workbench File",
            RulesetDefaults.Sr6 => "Import SR6 Starter File",
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
            RulesetDefaults.Sr4 => "Primary lane: .chum4 oracle preview with parity-safe XML fallback.",
            RulesetDefaults.Sr5 => "Primary lane: .chum5 flagship workbench continuity with XML fallback for governed restores.",
            RulesetDefaults.Sr6 => "Primary lane: .chum6 starter and beta intake with preview-safe XML fallback.",
            _ => "Accept native ruleset files or raw XML when the active ruleset is still unresolved."
        };
    }

    public static string BuildImportFilePlaceholder(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "(no SR4 preview file selected)",
            RulesetDefaults.Sr5 => "(no SR5 workbench file selected)",
            RulesetDefaults.Sr6 => "(no SR6 starter file selected)",
            _ => "(none)"
        };
    }

    public static string BuildImportDebugHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Oracle Debug Import",
            RulesetDefaults.Sr5 => "SR5 Workbench XML Import",
            RulesetDefaults.Sr6 => "SR6 Starter XML Import",
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
            RulesetDefaults.Sr4 => "SR4 Preview Commands",
            RulesetDefaults.Sr5 => "SR5 Workbench Commands",
            RulesetDefaults.Sr6 => "SR6 Starter Commands",
            _ => "Commands"
        };
    }

    public static string BuildCommandEmptyHint(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "No SR4 preview commands are currently available.",
            RulesetDefaults.Sr5 => "No SR5 workbench commands are currently available.",
            RulesetDefaults.Sr6 => "No SR6 starter commands are currently available.",
            _ => "No commands are currently available."
        };
    }

    public static string BuildResultHeading(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 Preview Result",
            RulesetDefaults.Sr5 => "SR5 Workbench Result",
            RulesetDefaults.Sr6 => "SR6 Starter Result",
            _ => "Result"
        };
    }

    public static string BuildResultPostureHint(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "Shadowrun 4 stays on the preview/oracle-first lane; keep codec and parity evidence explicit.",
            RulesetDefaults.Sr5 => "Shadowrun 5 stays on the primary/workbench lane; keep provider truth and .chum5 continuity explicit.",
            RulesetDefaults.Sr6 => "Shadowrun 6 stays on the beta/edge-first lane; keep starter-kit and runtime honesty explicit.",
            _ => "Shared shell posture is still unresolved; keep runtime and ruleset evidence explicit."
        };
    }

    public static string BuildResultReadyNotice(string? rulesetId)
    {
        RulesetUiDirective directive = Resolve(rulesetId);
        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 => "SR4 preview lane is ready; keep oracle import and parity evidence explicit.",
            RulesetDefaults.Sr5 => "SR5 workbench lane is ready; keep provider truth and .chum5 continuity explicit.",
            RulesetDefaults.Sr6 => "SR6 starter lane is ready; keep beta/runtime honesty explicit.",
            _ => "Ready."
        };
    }

    public static string FormatNavigationTabLabel(string? rulesetId, string? tabId, string fallbackLabel)
    {
        string normalizedTabId = RulesetDefaults.NormalizeOptional(tabId) ?? string.Empty;
        RulesetUiDirective directive = Resolve(rulesetId);

        return directive.RulesetId switch
        {
            RulesetDefaults.Sr4 when string.Equals(normalizedTabId, "tab-create", StringComparison.Ordinal) => "Oracle Intake",
            RulesetDefaults.Sr4 when string.Equals(normalizedTabId, "tab-info", StringComparison.Ordinal) => "Persona",
            RulesetDefaults.Sr4 when string.Equals(normalizedTabId, "tab-gear", StringComparison.Ordinal) => "Gear Preview",
            RulesetDefaults.Sr4 when string.Equals(normalizedTabId, "tab-rules", StringComparison.Ordinal) => "Rules Preview",

            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-create", StringComparison.Ordinal) => "Workbench",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-info", StringComparison.Ordinal) => "Runner",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-gear", StringComparison.Ordinal) => "Gear & Ware",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-rules", StringComparison.Ordinal) => "Rules & Validation",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-calendar", StringComparison.Ordinal) => "Career Log",
            RulesetDefaults.Sr5 when string.Equals(normalizedTabId, "tab-improvements", StringComparison.Ordinal) => "Career Track",

            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-create", StringComparison.Ordinal) => "Starter Kits",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-info", StringComparison.Ordinal) => "Runner",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-gear", StringComparison.Ordinal) => "Loadout",
            RulesetDefaults.Sr6 when string.Equals(normalizedTabId, "tab-rules", StringComparison.Ordinal) => "Rules Beta",
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
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-create.intake", StringComparison.Ordinal) => "Oracle Intake",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-info.summary", StringComparison.Ordinal) => "Preview Summary",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Parity Check",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-info.metadata", StringComparison.Ordinal) => "Stamp Metadata",
            RulesetDefaults.Sr4 when string.Equals(normalizedActionId, "tab-rules.rules", StringComparison.Ordinal) => "Rules Preview",
            RulesetDefaults.Sr4 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Gear Preview",

            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-create.intake", StringComparison.Ordinal) => "Workbench Intake",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.summary", StringComparison.Ordinal) => "Runner Summary",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Validate & Rebind",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.rules", StringComparison.Ordinal) => "Rules & Provider",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.build", StringComparison.Ordinal) => "Build Plan",
            RulesetDefaults.Sr5 when string.Equals(normalizedActionId, "tab-info.progress", StringComparison.Ordinal) => "Career Track",
            RulesetDefaults.Sr5 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Loadout",

            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-create.intake", StringComparison.Ordinal) => "Starter Intake",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.summary", StringComparison.Ordinal) => "Runner Summary",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.validate", StringComparison.Ordinal) => "Beta Safety Check",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-info.profile", StringComparison.Ordinal) => "Runner Card",
            RulesetDefaults.Sr6 when string.Equals(normalizedActionId, "tab-rules.rules", StringComparison.Ordinal) => "Rules Beta",
            RulesetDefaults.Sr6 when string.Equals(normalizedTargetId, "inventory", StringComparison.Ordinal) => "Loadout",
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
        return $"Ruleset posture: {directive.DisplayName} is on the {directive.PostureLabel} lane. {sectionSummary} Runtime posture: {runtimeQualifier}.";
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
        string buildMethod = string.IsNullOrWhiteSpace(summary.BuildMethod) ? "build lane unresolved" : summary.BuildMethod;
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
            RulesetDefaults.Sr4 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Preview Parity Flow",
            RulesetDefaults.Sr4 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Preview Summary Flow",

            RulesetDefaults.Sr5 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Workbench Validation Flow",
            RulesetDefaults.Sr5 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Workbench Summary Flow",

            RulesetDefaults.Sr6 when normalizedActionId.Contains("validate", StringComparison.Ordinal) => "Starter Safety Flow",
            RulesetDefaults.Sr6 when normalizedActionId.Contains("summary", StringComparison.Ordinal) => "Starter Summary Flow",
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
                : "preview runtime remains parity-gated",
            RulesetDefaults.Sr5 => hasWarnings
                ? "primary lane with runtime/provider attention required"
                : "primary lane keeps provider truth explicit",
            RulesetDefaults.Sr6 => hasWarnings
                ? "beta lane with active runtime warnings"
                : "beta lane keeps experimental-host honesty visible",
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
            || label.Contains("starter", StringComparison.OrdinalIgnoreCase);
    }
}
