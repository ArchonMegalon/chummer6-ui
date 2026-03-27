using Chummer.Contracts.Presentation;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public sealed record BuildLabConceptIntakeState(
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
    IReadOnlyList<BuildLabExportPayload> ExportPayloads,
    IReadOnlyList<BuildLabExportTarget> ExportTargets,
    IReadOnlyList<BuildLabActionDescriptor> Actions,
    string? ExplainEntryId,
    string? SourceDocumentId,
    bool CanContinue,
    string? NextSafeAction = null,
    string? RuntimeCompatibilitySummary = null,
    string? CampaignFitSummary = null,
    string? SupportClosureSummary = null,
    IReadOnlyList<string>? Watchouts = null);

public static class BuildLabConceptIntakeProjector
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static BuildLabConceptIntakeState? TryProject(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        BuildLabConceptIntakeProjection? projection = node.Deserialize<BuildLabConceptIntakeProjection>(SerializerOptions);
        if (projection is null || string.IsNullOrWhiteSpace(projection.WorkspaceId))
        {
            return null;
        }

        return new BuildLabConceptIntakeState(
            WorkspaceId: projection.WorkspaceId,
            WorkflowId: projection.WorkflowId,
            Title: projection.Title,
            Summary: projection.Summary,
            RulesetId: projection.RulesetId,
            BuildMethod: projection.BuildMethod,
            IntakeFields: projection.IntakeFields.ToArray(),
            RoleBadges: projection.RoleBadges.ToArray(),
            ConstraintBadges: projection.ConstraintBadges.ToArray(),
            ProvenanceBadges: projection.ProvenanceBadges.ToArray(),
            Variants: projection.Variants.ToArray(),
            ProgressionTimelines: projection.ProgressionTimelines.ToArray(),
            ExportPayloads: projection.ExportPayloads?.ToArray() ?? [],
            ExportTargets: projection.ExportTargets?.ToArray() ?? [],
            Actions: projection.Actions?.ToArray() ?? [],
            ExplainEntryId: projection.ExplainEntryId,
            SourceDocumentId: projection.SourceDocumentId,
            CanContinue: projection.CanContinue,
            NextSafeAction: projection.NextSafeAction,
            RuntimeCompatibilitySummary: projection.RuntimeCompatibilitySummary,
            CampaignFitSummary: projection.CampaignFitSummary,
            SupportClosureSummary: projection.SupportClosureSummary,
            Watchouts: projection.Watchouts?.ToArray() ?? []);
    }
}
