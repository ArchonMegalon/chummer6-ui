using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceSectionRenderer : IWorkspaceSectionRenderer
{
    private static readonly JsonSerializerOptions WriteIndentedOptions = new() { WriteIndented = true };

    public async Task<WorkspaceSectionRenderResult> RenderSectionAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        string sectionId,
        string? tabId,
        string? actionId,
        string? currentTabId,
        string? currentActionId,
        CancellationToken ct)
    {
        JsonNode section;
        try
        {
            section = await client.GetSectionAsync(workspaceId, sectionId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (!ct.IsCancellationRequested && string.Equals(sectionId, "build-lab", StringComparison.Ordinal))
        {
            // Older runtime hosts do not surface the flagship create-lane contract yet.
            section = CreateBuildLabFallbackSectionPayload();
        }

        BuildLabConceptIntakeState? buildLab = string.Equals(sectionId, "build-lab", StringComparison.Ordinal)
            ? BuildLabConceptIntakeProjector.TryProject(section)
            : null;
        if (string.Equals(sectionId, "build-lab", StringComparison.Ordinal) && buildLab is null)
        {
            section = CreateBuildLabFallbackSectionPayload();
            buildLab = BuildLabConceptIntakeProjector.TryProject(section);
        }

        BrowseWorkspaceState? browseWorkspace = string.Equals(sectionId, "browse", StringComparison.Ordinal)
            ? BrowseWorkspaceProjector.TryProject(section)
            : null;
        NpcPersonaStudioState? npcPersonaStudio = string.Equals(sectionId, "persona-studio", StringComparison.Ordinal)
            ? NpcPersonaStudioProjector.TryProject(section)
            : null;
        return new WorkspaceSectionRenderResult(
            ActiveTabId: tabId ?? currentTabId,
            ActiveActionId: actionId ?? currentActionId,
            ActiveSectionId: sectionId,
            ActiveSectionJson: section.ToJsonString(WriteIndentedOptions),
            ActiveSectionRows: SectionRowProjector.BuildRows(section),
            ActiveBuildLab: buildLab,
            ActiveBrowseWorkspace: browseWorkspace,
            ActiveNpcPersonaStudio: npcPersonaStudio);
    }

    public async Task<WorkspaceSectionRenderResult> RenderSummaryAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct)
    {
        CharacterFileSummary summary = await client.GetSummaryAsync(workspaceId, ct);
        JsonNode? summaryNode = JsonSerializer.SerializeToNode(summary);
        return new WorkspaceSectionRenderResult(
            ActiveTabId: action.TabId,
            ActiveActionId: action.Id,
            ActiveSectionId: "summary",
            ActiveSectionJson: JsonSerializer.Serialize(summary, WriteIndentedOptions),
            ActiveSectionRows: SectionRowProjector.BuildRows(summaryNode),
            ActiveBuildLab: null,
            ActiveBrowseWorkspace: null,
            ActiveNpcPersonaStudio: null);
    }

    public async Task<WorkspaceSectionRenderResult> RenderValidationAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        WorkspaceSurfaceActionDefinition action,
        CancellationToken ct)
    {
        CharacterValidationResult validation = await client.ValidateAsync(workspaceId, ct);
        JsonNode? validationNode = JsonSerializer.SerializeToNode(validation);
        return new WorkspaceSectionRenderResult(
            ActiveTabId: action.TabId,
            ActiveActionId: action.Id,
            ActiveSectionId: "validate",
            ActiveSectionJson: JsonSerializer.Serialize(validation, WriteIndentedOptions),
            ActiveSectionRows: SectionRowProjector.BuildRows(validationNode),
            ActiveBuildLab: null,
            ActiveBrowseWorkspace: null,
            ActiveNpcPersonaStudio: null);
    }

    private static JsonNode CreateBuildLabFallbackSectionPayload()
    {
        BuildLabConceptIntakeProjection projection = new(
            WorkspaceId: "build-lab",
            WorkflowId: "workflow.build-lab",
            Title: "Build Lab Intake",
            Summary: "Local fallback intake keeps the create lane available while the connected host catches up to the flagship build-lab contract.",
            RulesetId: RulesetDefaults.Sr5,
            BuildMethod: "Priority",
            IntakeFields: new[]
            {
                new BuildLabIntakeField(
                    FieldId: "concept",
                    Label: "Concept",
                    Kind: BuildLabFieldKinds.Text,
                    Value: "Street operator with low bookkeeping",
                    Placeholder: "Street operator with low bookkeeping",
                    HelpText: "Capture the high-level runner concept before live variant generation.",
                    Required: true,
                    ReadOnly: true),
                new BuildLabIntakeField(
                    FieldId: "table_constraints",
                    Label: "Table Constraints",
                    Kind: BuildLabFieldKinds.Multiline,
                    Value: "Keep matrix load light and preserve crew coverage.",
                    Placeholder: "Keep matrix load light and preserve crew coverage.",
                    HelpText: "The fallback keeps the same handoff posture across both UI heads.",
                    Required: false,
                    ReadOnly: true)
            },
            RoleBadges: new[]
            {
                new BuildLabBadge("face", "Face", BuildLabBadgeKinds.Role, true),
                new BuildLabBadge("legwork", "Legwork", BuildLabBadgeKinds.Role)
            },
            ConstraintBadges: new[]
            {
                new BuildLabBadge("low-bookkeeping", "Low Bookkeeping", BuildLabBadgeKinds.Constraint)
            },
            ProvenanceBadges: new[]
            {
                new BuildLabBadge("fallback", "Local Fallback", BuildLabBadgeKinds.Provenance, true)
            },
            Variants: new[]
            {
                new BuildLabVariantProjection(
                    VariantId: "variant.social",
                    Label: "Social Operator",
                    Summary: "Fastest route to a playable operator while the host API is behind the UI contract.",
                    TableFit: "Ops-first tables",
                    RoleBadges: new[]
                    {
                        new BuildLabBadge("face", "Face", BuildLabBadgeKinds.Role, true)
                    },
                    Metrics: new[]
                    {
                        new BuildLabVariantMetric("complexity", "Complexity", "Low", emphasized: true),
                        new BuildLabVariantMetric("handoff", "Handoff", "Ready", emphasized: true)
                    },
                    Warnings: new[]
                    {
                        new BuildLabVariantWarning("fallback-static", "Static preview", "Live API-backed intake is unavailable on this host, so this preview stays deterministic.", BuildLabWarningKinds.Trap, true)
                    },
                    OverlapBadges: new[]
                    {
                        new BuildLabBadge("light-overlap", "Light overlap", BuildLabBadgeKinds.Overlap)
                    },
                    Actions: new[]
                    {
                        new BuildLabActionDescriptor("inspect-social", "Inspect Timeline", BuildLabSurfaceIds.ProgressionTimelineRail, true)
                    },
                    ExplainEntryId: "buildlab.variant.social")
            },
            ProgressionTimelines: new[]
            {
                new BuildLabProgressionTimeline(
                    TimelineId: "timeline.social",
                    Title: "Social Operator Ladder",
                    Summary: "Three deterministic checkpoints keep the create lane populated even without server-side build-lab data.",
                    VariantId: "variant.social",
                    Steps: new[]
                    {
                        new BuildLabProgressionStep(
                            StepId: "social-25",
                            KarmaTarget: 25,
                            Label: "Opener",
                            Summary: "Table-ready lead with low bookkeeping.",
                            Outcomes: new[]
                            {
                                new BuildLabVariantMetric("tempo", "Tempo", "Fast", emphasized: true)
                            },
                            MilestoneBadges: new[]
                            {
                                new BuildLabBadge("25", "25 Karma", BuildLabBadgeKinds.Milestone, true)
                            },
                            RiskBadges: Array.Empty<BuildLabBadge>()),
                        new BuildLabProgressionStep(
                            StepId: "social-50",
                            KarmaTarget: 50,
                            Label: "Reliability",
                            Summary: "Fallback lanes solidify for longer campaign arcs.",
                            Outcomes: new[]
                            {
                                new BuildLabVariantMetric("coverage", "Coverage", "Stable", emphasized: true)
                            },
                            MilestoneBadges: new[]
                            {
                                new BuildLabBadge("50", "50 Karma", BuildLabBadgeKinds.Milestone, true)
                            },
                            RiskBadges: Array.Empty<BuildLabBadge>()),
                        new BuildLabProgressionStep(
                            StepId: "social-100",
                            KarmaTarget: 100,
                            Label: "Anchor",
                            Summary: "Campaign-ready anchor until the live host catches up.",
                            Outcomes: new[]
                            {
                                new BuildLabVariantMetric("support", "Support Closure", "Ready", emphasized: true)
                            },
                            MilestoneBadges: new[]
                            {
                                new BuildLabBadge("100", "100 Karma", BuildLabBadgeKinds.Milestone, true)
                            },
                            RiskBadges: new[]
                            {
                                new BuildLabBadge("upgrade-host", "Upgrade Host", BuildLabBadgeKinds.Risk)
                            })
                    },
                    SourceDocumentId: "fallback.build-lab.timeline")
            },
            ExportPayloads: new[]
            {
                new BuildLabExportPayload(
                    PayloadId: "payload.social",
                    Title: "Fallback Social Operator",
                    Summary: "Deterministic handoff payload for downstream build idea and template flows.",
                    PayloadKind: "build-lab-handoff",
                    Fields: new[]
                    {
                        new BuildLabExportField("concept", "Concept", "Street operator with low bookkeeping", true),
                        new BuildLabExportField("table_fit", "Table Fit", "Ops-first", true)
                    },
                    VariantId: "variant.social",
                    TimelineId: "timeline.social",
                    QueryText: "street operator low bookkeeping")
            },
            ExportTargets: new[]
            {
                new BuildLabExportTarget(
                    TargetId: "target.build-idea-card",
                    Label: "Build Idea Card",
                    TargetKind: BuildLabExportTargetKinds.BuildIdeaCard,
                    WorkflowId: "workflow.coach.build-ideas",
                    Enabled: true,
                    Description: "Carry the fallback intake into the downstream build-idea workflow.",
                    PayloadId: "payload.social",
                    ActionId: "handoff-build-idea",
                    Badges: new[]
                    {
                        new BuildLabBadge("searchable", "Searchable", BuildLabBadgeKinds.Export, true)
                    })
            },
            Actions: new[]
            {
                new BuildLabActionDescriptor("next-variants", "Compare Variants", BuildLabSurfaceIds.ProgressionTimelineRail, true, "variant.social"),
                new BuildLabActionDescriptor("handoff-build-idea", "Hand Off", BuildLabSurfaceIds.ExportRail, true, "target.build-idea-card")
            },
            ExplainEntryId: "buildlab.intake.fallback",
            SourceDocumentId: "fallback.build-lab",
            CanContinue: true,
            NextSafeAction: "next-variants",
            RuntimeCompatibilitySummary: "Fallback intake is active because the connected host does not expose build-lab sections yet.",
            CampaignFitSummary: "Use the fallback intake to preserve parity and handoff posture across both desktop heads.",
            SupportClosureSummary: "Upgrade the host API for live build-lab data once the compatibility tree catches up.",
            Watchouts: new[]
            {
                "Fallback build-lab content is static until the connected host supports build-lab sections."
            },
            TeamCoverage: new BuildLabTeamCoverageProjection(
                Summary: "Fallback crew coverage keeps face and legwork visible while astral coverage stays intentionally open.",
                CoverageSummary: "Face and legwork are covered before handoff.",
                RolePressureSummary: "Astral support remains the only missing lane in the deterministic fallback.",
                MissingRoleTags: new[] { "astral" },
                CoveredRoleTags: new[] { "face", "legwork" },
                DuplicateRoleTags: Array.Empty<string>(),
                ExplainEntryId: "buildlab.teamcoverage.fallback"));

        return JsonSerializer.SerializeToNode(projection) ?? new JsonObject();
    }
}
