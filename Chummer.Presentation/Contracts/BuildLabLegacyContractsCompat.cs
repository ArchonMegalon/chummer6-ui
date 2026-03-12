using Chummer.Contracts.Rulesets;

namespace Chummer.Contracts.Presentation
{
    // Compatibility shims for Build Lab projection DTOs that are still consumed by
    // presentation and test surfaces while contracts package coverage catches up.
    public static class BuildLabFieldKinds
    {
        public const string Text = "text";
        public const string Multiline = "multiline";
        public const string SingleSelect = "single_select";
        public const string MultiSelect = "multi_select";
    }

    public static class BuildLabBadgeKinds
    {
        public const string Role = "role";
        public const string Constraint = "constraint";
        public const string Provenance = "provenance";
        public const string Overlap = "overlap";
        public const string Milestone = "milestone";
        public const string Risk = "risk";
        public const string Export = "export";
    }

    public static class BuildLabWarningKinds
    {
        public const string Trap = "trap";
        public const string RoleOverlap = "role_overlap";
    }

    public static class BuildLabSurfaceIds
    {
        public const string ProgressionTimelineRail = "progression_timeline_rail";
        public const string ExportRail = "export_rail";
    }

    public static class BuildLabExportTargetKinds
    {
        public const string BuildIdeaCard = "build_idea_card";
        public const string CharacterTemplate = "character_template";
        public const string Workflow = "workflow";
    }

    public sealed record BuildLabFieldOption(
        string Value,
        string Label,
        bool Selected = false);

    public sealed record BuildLabIntakeField(
        string FieldId,
        string Label,
        string Kind,
        string Value,
        string? Placeholder = null,
        string? HelpText = null,
        bool Required = false,
        bool ReadOnly = false,
        IReadOnlyList<BuildLabFieldOption>? Options = null);

    public sealed record BuildLabBadge(
        string BadgeId,
        string Label,
        string Kind,
        bool Emphasized = false);

    public sealed record BuildLabVariantMetric(
        string MetricId,
        string Label,
        string Value,
        string? Delta = null,
        bool Emphasized = false);

    public sealed record BuildLabVariantWarning(
        string WarningId,
        string Label,
        string Summary,
        string Kind,
        bool Emphasized = false,
        string? ExplainEntryId = null);

    public sealed record BuildLabActionDescriptor(
        string ActionId,
        string Label,
        string SurfaceId,
        bool Enabled = true,
        string? TargetId = null,
        string? ExplainEntryId = null);

    public sealed record BuildLabVariantProjection(
        string VariantId,
        string Label,
        string Summary,
        string TableFit,
        IReadOnlyList<BuildLabBadge> RoleBadges,
        IReadOnlyList<BuildLabVariantMetric> Metrics,
        IReadOnlyList<BuildLabVariantWarning> Warnings,
        IReadOnlyList<BuildLabBadge> OverlapBadges,
        IReadOnlyList<BuildLabActionDescriptor> Actions,
        string? ExplainEntryId = null,
        string? SourceDocumentId = null);

    public sealed record BuildLabProgressionStep(
        string StepId,
        int KarmaTarget,
        string Label,
        string Summary,
        IReadOnlyList<BuildLabVariantMetric> Outcomes,
        IReadOnlyList<BuildLabBadge> MilestoneBadges,
        IReadOnlyList<BuildLabBadge> RiskBadges,
        string? ExplainEntryId = null,
        string? SourceDocumentId = null);

    public sealed record BuildLabProgressionTimeline(
        string TimelineId,
        string Title,
        string Summary,
        string VariantId,
        IReadOnlyList<BuildLabProgressionStep> Steps,
        string? SourceDocumentId = null,
        string? ExplainEntryId = null);

    public sealed record BuildLabExportField(
        string FieldId,
        string Label,
        string Value,
        bool Emphasized = false);

    public sealed record BuildLabExportPayload(
        string PayloadId,
        string Title,
        string Summary,
        string PayloadKind,
        IReadOnlyList<BuildLabExportField> Fields,
        string? VariantId = null,
        string? TimelineId = null,
        string? QueryText = null,
        string? SourceDocumentId = null,
        string? ExplainEntryId = null);

    public sealed record BuildLabExportTarget(
        string TargetId,
        string Label,
        string TargetKind,
        string WorkflowId,
        bool Enabled,
        string Description,
        string PayloadId,
        string ActionId,
        IReadOnlyList<BuildLabBadge> Badges,
        string? ExplainEntryId = null,
        string? SourceDocumentId = null);

    public sealed record BuildLabConceptIntakeProjection(
        string WorkspaceId,
        string WorkflowId,
        string Title,
        string Summary,
        string RulesetId,
        string BuildMethod,
        IReadOnlyList<BuildLabIntakeField> IntakeFields,
        IReadOnlyList<BuildLabBadge> RoleBadges,
        IReadOnlyList<BuildLabBadge> ConstraintBadges,
        IReadOnlyList<BuildLabBadge> ProvenanceBadges,
        IReadOnlyList<BuildLabVariantProjection> Variants,
        IReadOnlyList<BuildLabProgressionTimeline> ProgressionTimelines,
        IReadOnlyList<BuildLabExportPayload>? ExportPayloads,
        IReadOnlyList<BuildLabExportTarget>? ExportTargets,
        IReadOnlyList<BuildLabActionDescriptor> Actions,
        string? ExplainEntryId,
        string? SourceDocumentId,
        bool CanContinue);
}

namespace Chummer.Contracts.Rulesets
{
    // Compatibility seam retained in presentation to avoid binding directly to
    // ruleset-host internals when contracts package surface is narrower.
    public interface IEngineEvaluator
    {
        ValueTask<RulesetCapabilityInvocationResult> EvaluateAsync(
            RulesetCapabilityInvocationRequest request,
            CancellationToken ct);
    }
}
