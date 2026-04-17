using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Chummer.Avalonia.Controls;

public partial class SectionHostControl : UserControl
{
    private BuildLabConceptIntakeState? _buildLab;
    private BrowseWorkspaceState? _browseWorkspace;
    public event EventHandler<string>? QuickActionRequested;

    public SectionHostControl()
    {
        InitializeComponent();
        BrowseResultsList.SelectionChanged += BrowseResultsList_OnSelectionChanged;
    }

    public string XmlInputText => XmlInputBox.Text ?? string.Empty;

    public void SetState(SectionHostState state)
    {
        SetNotice(state.Notice);
        SetClassicCharacterSheet(state.SectionId, state.PreviewJson, state.Rows);
        SetSectionPreview(state.PreviewJson, state.Rows);
        SetSectionQuickActions(state.QuickActions);
        SetBuildLab(state.BuildLab);
        SetBrowseWorkspace(state.BrowseWorkspace);
        SetContactGraph(state.ContactGraph);
        SetDowntimePlanner(state.DowntimePlanner);
        SetNpcPersonaStudio(state.NpcPersonaStudio);
    }

    public void SetNotice(string notice)
    {
        NoticeText.Text = notice;
    }

    public void SetSectionPreview(string previewJson, IEnumerable<SectionRowDisplayItem> rows)
    {
        SectionPreviewBox.Text = previewJson;
        SectionRowsList.ItemsSource = rows.ToArray();
    }

    public void SetClassicCharacterSheet(string? sectionId, string previewJson, IEnumerable<SectionRowDisplayItem> rows)
    {
        ClassicCharacterFactsPanel.Children.Clear();
        ClassicAttributeFactsPanel.Children.Clear();

        IReadOnlyList<ClassicSheetFactDisplayItem> summaryFacts = BuildCharacterSummaryFacts(previewJson);
        IReadOnlyList<ClassicSheetFactDisplayItem> attributeFacts = BuildCharacterAttributeFacts(previewJson, rows);

        bool hasFacts = summaryFacts.Count > 0 || attributeFacts.Count > 0;
        bool sectionTargetsClassicCharacterSheet =
            string.Equals(sectionId, "attributes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionId, "attributedetails", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionId, "armor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionId, "cyberwares", StringComparison.OrdinalIgnoreCase)
            || summaryFacts.Count > 0;

        ClassicCharacterSheetBorder.IsVisible = hasFacts && sectionTargetsClassicCharacterSheet;
        SectionRowsList.Height = ClassicCharacterSheetBorder.IsVisible ? 96d : 160d;
        if (!ClassicCharacterSheetBorder.IsVisible)
        {
            ClassicCharacterSummaryTitle.Text = "Runner Summary";
            return;
        }

        ClassicCharacterSummaryTitle.Text = string.Equals(sectionId, "attributes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionId, "attributedetails", StringComparison.OrdinalIgnoreCase)
            ? "Attributes"
            : "Runner Summary";

        foreach (ClassicSheetFactDisplayItem fact in summaryFacts)
        {
            ClassicCharacterFactsPanel.Children.Add(CreateClassicFactCard(fact, emphasizeValue: false));
        }

        foreach (ClassicSheetFactDisplayItem fact in attributeFacts)
        {
            ClassicAttributeFactsPanel.Children.Add(CreateClassicFactCard(fact, emphasizeValue: true));
        }
    }

    public void SetSectionQuickActions(IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        SectionQuickActionsPanel.Children.Clear();
        SectionQuickActionsBorder.IsVisible = quickActions.Count > 0;

        foreach (SectionQuickActionDisplayItem quickAction in quickActions)
        {
            SectionQuickActionsPanel.Children.Add(CreateQuickActionButton(quickAction));
        }
    }

    public void SetBuildLab(BuildLabConceptIntakeState? buildLab)
    {
        _buildLab = buildLab;
        BuildLabBorder.IsVisible = buildLab is not null;

        if (buildLab is null)
        {
            BuildLabSummaryText.Text = string.Empty;
            BuildLabFieldsList.ItemsSource = Array.Empty<string>();
            BuildLabConstraintsList.ItemsSource = Array.Empty<string>();
            BuildLabProvenanceBox.Text = string.Empty;
            BuildLabVariantsList.ItemsSource = Array.Empty<string>();
            BuildLabCoverageBox.Text = string.Empty;
            BuildLabTimelinesBox.Text = string.Empty;
            BuildLabExportPayloadsBox.Text = string.Empty;
            BuildLabExportTargetsList.ItemsSource = Array.Empty<string>();
            BuildLabActionsList.ItemsSource = Array.Empty<string>();
            return;
        }

        BuildLabSummaryText.Text = $"{buildLab.Title} · {buildLab.RulesetId}/{buildLab.BuildMethod}";
        BuildLabFieldsList.ItemsSource = buildLab.IntakeFields
            .Select(field => $"{field.Label}: {field.Value}")
            .ToArray();
        BuildLabConstraintsList.ItemsSource = buildLab.ConstraintBadges.Select(badge => badge.Label).ToArray();
        BuildLabProvenanceBox.Text = BuildBuildLabProvenance(buildLab);
        BuildLabVariantsList.ItemsSource = buildLab.Variants
            .Select(BuildVariantLine)
            .ToArray();
        BuildLabCoverageBox.Text = BuildCoverageText(buildLab);
        BuildLabTimelinesBox.Text = BuildTimelineText(buildLab);
        BuildLabExportPayloadsBox.Text = BuildExportPayloadText(buildLab);
        BuildLabExportTargetsList.ItemsSource = buildLab.ExportTargets
            .Select(target => BuildExportTargetLine(buildLab, target))
            .ToArray();
        BuildLabActionsList.ItemsSource = buildLab.Actions
            .Select(action => BuildActionLine(buildLab, action))
            .ToArray();
    }

    public void SetBrowseWorkspace(BrowseWorkspaceState? browseWorkspace)
    {
        _browseWorkspace = browseWorkspace;
        BrowseWorkspaceBorder.IsVisible = browseWorkspace is not null;

        if (browseWorkspace is null)
        {
            BrowseSummaryText.Text = string.Empty;
            BrowsePresetsList.ItemsSource = Array.Empty<string>();
            BrowseFacetsList.ItemsSource = Array.Empty<string>();
            BrowseResultsList.ItemsSource = Array.Empty<BrowseResultDisplayItem>();
            BrowseSelectedItemsList.ItemsSource = Array.Empty<string>();
            BrowseDetailBox.Text = string.Empty;
            return;
        }

        BrowseSummaryText.Text = BuildBrowseSummary(browseWorkspace);
        BrowsePresetsList.ItemsSource = browseWorkspace.Presets.Select(BuildPresetLine).ToArray();
        BrowseWorkspaceFacetState[] preferredFacets = browseWorkspace.SourceFacets
            .Concat(browseWorkspace.PackFacets)
            .DistinctBy(facet => facet.FacetId, StringComparer.Ordinal)
            .ToArray();
        BrowseFacetsList.ItemsSource = preferredFacets.Length > 0
            ? preferredFacets.Select(BuildFacetLine).ToArray()
            : browseWorkspace.Facets.Take(4).Select(BuildFacetLine).ToArray();
        BrowseResultsList.ItemsSource = browseWorkspace.Results
            .Select(result => new BrowseResultDisplayItem(result.ItemId, BuildResultLine(result)))
            .ToArray();
        BrowseSelectedItemsList.ItemsSource = browseWorkspace.SelectedItems
            .Select(item => string.IsNullOrWhiteSpace(item.Detail) ? item.Title : $"{item.Title} · {item.Detail}")
            .ToArray();

        int selectedIndex = Math.Clamp(browseWorkspace.ActiveResultIndex, 0, Math.Max(0, browseWorkspace.Results.Count - 1));
        BrowseResultsList.SelectedIndex = browseWorkspace.Results.Count == 0 ? -1 : selectedIndex;
        BrowseDetailBox.Text = BuildDetailText(browseWorkspace.ActiveDetail);
    }

    public void SetContactGraph(ContactRelationshipGraphState? contactGraph)
    {
        ContactGraphBorder.IsVisible = contactGraph is not null;

        if (contactGraph is null)
        {
            ContactGraphSummaryText.Text = string.Empty;
            ContactNodeList.ItemsSource = Array.Empty<string>();
            ContactFactionStatusBox.Text = string.Empty;
            ContactHeatObligationBox.Text = string.Empty;
            ContactFavorRailBox.Text = string.Empty;
            return;
        }

        ContactGraphSummaryText.Text = $"Relationship graph · {contactGraph.Nodes.Count} contacts · {contactGraph.EdgeCount} links";
        ContactNodeList.ItemsSource = contactGraph.Nodes
            .Select(node => $"{node.Name} · {node.Faction} · heat {node.Heat} · links: {string.Join(", ", node.LinkedContactNames)}")
            .ToArray();
        ContactFactionStatusBox.Text = BuildFactionStatusText(contactGraph);
        ContactHeatObligationBox.Text = BuildHeatAndObligationText(contactGraph);
        ContactFavorRailBox.Text = BuildFavorRailText(contactGraph);
    }

    public void SetNpcPersonaStudio(NpcPersonaStudioState? npcPersonaStudio)
    {
        NpcPersonaStudioBorder.IsVisible = npcPersonaStudio is not null;

        if (npcPersonaStudio is null)
        {
            NpcPersonaSummaryText.Text = string.Empty;
            NpcPersonaList.ItemsSource = Array.Empty<string>();
            NpcPolicyList.ItemsSource = Array.Empty<string>();
            NpcEvidenceBox.Text = string.Empty;
            NpcApprovalBox.Text = string.Empty;
            return;
        }

        NpcPersonaSummaryText.Text = BuildPersonaSummary(npcPersonaStudio);
        NpcPersonaList.ItemsSource = npcPersonaStudio.Personas
            .Select(persona => BuildPersonaLine(persona, npcPersonaStudio.SelectedPersonaId))
            .ToArray();
        NpcPolicyList.ItemsSource = npcPersonaStudio.Policies
            .Select(BuildPolicyLine)
            .ToArray();
        NpcEvidenceBox.Text = string.Join(Environment.NewLine, npcPersonaStudio.EvidenceLines);
        NpcApprovalBox.Text = BuildApprovalSummary(npcPersonaStudio);
    }

    public void SetDowntimePlanner(DowntimePlannerState? downtimePlanner)
    {
        DowntimePlannerBorder.IsVisible = downtimePlanner is not null;

        if (downtimePlanner is null)
        {
            DowntimePlannerSummaryText.Text = string.Empty;
            DowntimePlannerLanesList.ItemsSource = Array.Empty<string>();
            DowntimeCalendarBox.Text = string.Empty;
            DowntimeScheduleList.ItemsSource = Array.Empty<string>();
            return;
        }

        DowntimePlannerSummaryText.Text = BuildDowntimePlannerSummary(downtimePlanner);
        DowntimePlannerLanesList.ItemsSource = downtimePlanner.PlannerLanes
            .Select(lane => $"{lane.Title}: {lane.ItemCount} items")
            .ToArray();
        DowntimeCalendarBox.Text = BuildDowntimeCalendarText(downtimePlanner);
        DowntimeScheduleList.ItemsSource = downtimePlanner.ScheduleItems
            .Select(item => $"{item.TimeWindow} · {item.Title} ({item.LaneTitle})")
            .ToArray();
    }

    private void BrowseResultsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_browseWorkspace is null)
            return;

        if (BrowseResultsList.SelectedItem is not BrowseResultDisplayItem selected)
            return;

        BrowseWorkspaceResultItemState? result = _browseWorkspace.Results
            .FirstOrDefault(item => string.Equals(item.ItemId, selected.ItemId, StringComparison.Ordinal));
        if (result is null)
            return;

        BrowseItemDetail? detail = string.Equals(_browseWorkspace.ActiveDetail?.ItemId, result.ItemId, StringComparison.Ordinal)
            ? _browseWorkspace.ActiveDetail
            : null;
        BrowseDetailBox.Text = BuildDetailText(detail);
    }

    private static string BuildBrowseSummary(BrowseWorkspaceState browseWorkspace)
    {
        string title = string.IsNullOrWhiteSpace(browseWorkspace.DialogTitle) ? "Browse workspace" : browseWorkspace.DialogTitle!;
        return $"{title} · {browseWorkspace.TotalCount} results · {browseWorkspace.SortId}/{browseWorkspace.SortDirection}";
    }

    private static string BuildPresetLine(BrowseWorkspacePresetState preset)
    {
        string activeTag = preset.IsActive ? "[active] " : string.Empty;
        string scopeTag = preset.Shared ? "shared" : "local";
        return $"{activeTag}{preset.Label} ({scopeTag})";
    }

    private static string BuildFacetLine(BrowseWorkspaceFacetState facet)
    {
        string selectedOptions = facet.SelectedOptions.Count == 0
            ? "none selected"
            : string.Join(", ", facet.SelectedOptions.Select(option => option.Label));
        return $"{facet.Label}: {selectedOptions}";
    }

    private static string BuildResultLine(BrowseWorkspaceResultItemState result)
    {
        string columns = result.ColumnValues.Count == 0
            ? string.Empty
            : $" · {string.Join(" · ", result.ColumnValues.Select(pair => $"{pair.Key}: {pair.Value}"))}";
        string active = result.IsActive ? "[active] " : string.Empty;
        string selectable = result.IsSelectable ? string.Empty : " · unavailable";
        return $"{active}{result.Title}{columns}{selectable}";
    }

    private static string BuildDetailText(BrowseItemDetail? detail)
    {
        if (detail is null)
            return "Select a browse result to inspect its current detail payload.";

        IEnumerable<string> lines = detail.SummaryLines.Count == 0
            ? Array.Empty<string>()
            : detail.SummaryLines;
        string summary = string.Join(Environment.NewLine, lines);
        string explain = string.IsNullOrWhiteSpace(detail.ExplainEntryId)
            ? string.Empty
            : $"{Environment.NewLine}Explain: {detail.ExplainEntryId}";
        return $"{detail.Title}{Environment.NewLine}{summary}{explain}".Trim();
    }

    private static string BuildBuildLabProvenance(BuildLabConceptIntakeState buildLab)
    {
        List<string> lines = [];
        if (buildLab.ProvenanceBadges.Count > 0)
        {
            lines.Add(string.Join(" · ", buildLab.ProvenanceBadges.Select(badge => badge.Label)));
        }

        if (!string.IsNullOrWhiteSpace(buildLab.ExplainEntryId))
        {
            lines.Add($"Explain: {buildLab.ExplainEntryId}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.SourceDocumentId))
        {
            lines.Add($"Source: {buildLab.SourceDocumentId}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.NextSafeAction))
        {
            lines.Add($"Next safe action: {buildLab.NextSafeAction}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.RuntimeCompatibilitySummary))
        {
            lines.Add($"Runtime: {buildLab.RuntimeCompatibilitySummary}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.CampaignFitSummary))
        {
            lines.Add($"Campaign fit: {buildLab.CampaignFitSummary}");
        }

        if (!string.IsNullOrWhiteSpace(buildLab.SupportClosureSummary))
        {
            lines.Add($"Support: {buildLab.SupportClosureSummary}");
        }

        if (buildLab.Watchouts is { Count: > 0 })
        {
            lines.Add($"Watchouts: {string.Join(" | ", buildLab.Watchouts.Take(3))}");
        }

        if (HasBuildBlockerReceipt(buildLab))
        {
            lines.Add($"Build blocker receipt: {BuildBuildBlockerBadge(buildLab)}");
            lines.Add($"Rule environment: {buildLab.RulesetId} / {buildLab.BuildMethod}");
            lines.Add($"Before: {BuildBuildBlockerBefore(buildLab)}");
            lines.Add($"After: {BuildBuildBlockerAfter(buildLab)}");
            lines.Add($"Support reuse: {BuildBuildBlockerSupport(buildLab)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool HasBuildBlockerReceipt(BuildLabConceptIntakeState buildLab)
        => !buildLab.CanContinue
            || !string.IsNullOrWhiteSpace(buildLab.RuntimeCompatibilitySummary)
            || !string.IsNullOrWhiteSpace(buildLab.SupportClosureSummary)
            || buildLab.Watchouts is { Count: > 0 }
            || buildLab.Variants.Any(variant => variant.Warnings.Count > 0);

    private static string BuildBuildBlockerBadge(BuildLabConceptIntakeState buildLab)
    {
        int warningCount = buildLab.Variants.Sum(static variant => variant.Warnings.Count)
            + (buildLab.Watchouts?.Count ?? 0);
        return warningCount == 0 && buildLab.CanContinue ? "receipt" : $"{warningCount} blocker signal(s)";
    }

    private static string BuildBuildBlockerBefore(BuildLabConceptIntakeState buildLab)
    {
        string summary = FirstNonBlank(
            buildLab.RuntimeCompatibilitySummary,
            buildLab.CampaignFitSummary,
            buildLab.Variants
                .SelectMany(static variant => variant.Warnings)
                .Select(static warning => warning.Detail)
                .FirstOrDefault(),
            buildLab.Watchouts?.FirstOrDefault());

        return string.IsNullOrWhiteSpace(summary) ? "No blocker was emitted before this build decision." : summary;
    }

    private static string BuildBuildBlockerAfter(BuildLabConceptIntakeState buildLab)
        => FirstNonBlank(buildLab.NextSafeAction, buildLab.SupportClosureSummary, buildLab.CanContinue ? "Build can continue with the current receipt." : "Resolve the blocker before handoff.");

    private static string BuildBuildBlockerSupport(BuildLabConceptIntakeState buildLab)
        => FirstNonBlank(buildLab.SupportClosureSummary, string.IsNullOrWhiteSpace(buildLab.ExplainEntryId) ? "Support can cite the visible blocker receipt." : $"Support can cite explain receipt {buildLab.ExplainEntryId}.");

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string BuildVariantLine(BuildLabVariantProjection variant)
    {
        string metrics = variant.Metrics.Count == 0
            ? string.Empty
            : $" · {string.Join(" · ", variant.Metrics.Select(metric => $"{metric.Label}: {metric.Value}"))}";
        string warnings = variant.Warnings.Count == 0
            ? string.Empty
            : $" · {string.Join(" · ", variant.Warnings.Select(warning => warning.Label))}";
        return $"{variant.Label} ({variant.TableFit}){metrics}{warnings}";
    }

    private static string BuildCoverageText(BuildLabConceptIntakeState buildLab)
    {
        List<string> lines = [];
        AppendTeamCoverageLines(lines, buildLab.TeamCoverage);

        int optimizerReadyVariants = 0;
        foreach (BuildLabVariantProjection variant in buildLab.Variants)
        {
            List<string> signals = [];

            string coverageMetrics = string.Join(
                " · ",
                variant.Metrics
                    .Where(IsCoverageMetric)
                    .Select(metric => $"{metric.Label}: {metric.Value}"));
            if (!string.IsNullOrWhiteSpace(coverageMetrics))
            {
                signals.Add(coverageMetrics);
            }

            if (variant.OverlapBadges.Count > 0)
            {
                signals.Add($"Overlap: {string.Join(" | ", variant.OverlapBadges.Select(badge => badge.Label))}");
            }

            if (signals.Count == 0)
            {
                continue;
            }

            optimizerReadyVariants++;
            lines.Add($"{variant.Label}: {string.Join(" · ", signals)}");
        }

        foreach (BuildLabProgressionTimeline timeline in buildLab.ProgressionTimelines)
        {
            BuildLabProgressionStep? strongestCoverageStep = null;
            foreach (BuildLabProgressionStep step in timeline.Steps)
            {
                if (step.Outcomes.Any(IsCoverageMetric))
                {
                    strongestCoverageStep = step;
                }
            }

            if (strongestCoverageStep is null)
            {
                lines.Add($"{timeline.Title}: {timeline.Steps.Count} checkpoint(s) keep the planner ready for handoff.");
                continue;
            }

            string coverageOutcomes = string.Join(
                " | ",
                strongestCoverageStep.Outcomes
                    .Where(IsCoverageMetric)
                    .Select(metric => $"{metric.Label}: {metric.Value}"));
            lines.Add($"{timeline.Title}: strongest coverage checkpoint at {strongestCoverageStep.KarmaTarget} Karma · {coverageOutcomes}");
        }

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        lines.Insert(0, $"Planner + team coverage · {optimizerReadyVariants} optimizer-ready variant(s) · {buildLab.ProgressionTimelines.Count} progression timeline(s)");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendTeamCoverageLines(List<string> lines, BuildLabTeamCoverageProjection? teamCoverage)
    {
        if (teamCoverage is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(teamCoverage.CoverageSummary))
        {
            lines.Add($"Coverage: {teamCoverage.CoverageSummary}");
        }

        if (!string.IsNullOrWhiteSpace(teamCoverage.RolePressureSummary))
        {
            lines.Add($"Role pressure: {teamCoverage.RolePressureSummary}");
        }

        if (teamCoverage.CoveredRoleTags is { Count: > 0 })
        {
            lines.Add($"Covered roles: {FormatRoleTags(teamCoverage.CoveredRoleTags)}");
        }

        if (teamCoverage.MissingRoleTags.Count > 0)
        {
            lines.Add($"Missing roles: {FormatRoleTags(teamCoverage.MissingRoleTags)}");
        }

        if (teamCoverage.DuplicateRoleTags is { Count: > 0 })
        {
            lines.Add($"Duplicate roles: {FormatRoleTags(teamCoverage.DuplicateRoleTags)}");
        }

        if (!string.IsNullOrWhiteSpace(teamCoverage.ExplainEntryId))
        {
            lines.Add($"Explain: {teamCoverage.ExplainEntryId}");
        }
    }

    private static string BuildTimelineText(BuildLabConceptIntakeState buildLab)
    {
        if (buildLab.ProgressionTimelines.Count == 0)
        {
            return string.Empty;
        }

        List<string> lines = [];
        foreach (BuildLabProgressionTimeline timeline in buildLab.ProgressionTimelines)
        {
            lines.Add($"{timeline.Title} · {timeline.Summary}");
            foreach (BuildLabProgressionStep step in timeline.Steps)
            {
                string outcomes = step.Outcomes.Count == 0
                    ? string.Empty
                    : $" · {string.Join(" · ", step.Outcomes.Select(metric => $"{metric.Label}: {metric.Value}"))}";
                lines.Add($"  {step.KarmaTarget} Karma: {step.Label} · {step.Summary}{outcomes}");
            }

            if (!string.IsNullOrWhiteSpace(timeline.SourceDocumentId))
            {
                lines.Add($"  Source: {timeline.SourceDocumentId}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsCoverageMetric(BuildLabVariantMetric metric)
    {
        return metric.Label.Contains("coverage", StringComparison.OrdinalIgnoreCase)
            || metric.Label.Contains("role", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatRoleTags(IEnumerable<string> roleTags)
        => string.Join(" | ", roleTags.Select(FormatRoleTag));

    private static string FormatRoleTag(string roleTag)
    {
        string normalized = roleTag.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? roleTag
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
    }

    private static string BuildExportPayloadText(BuildLabConceptIntakeState buildLab)
    {
        if (buildLab.ExportPayloads.Count == 0)
        {
            return string.Empty;
        }

        List<string> lines = [];
        foreach (BuildLabExportPayload payload in buildLab.ExportPayloads)
        {
            lines.Add($"{payload.Title} · {payload.PayloadKind}");
            foreach (BuildLabExportField field in payload.Fields)
            {
                lines.Add($"  {field.Label}: {field.Value}");
            }

            if (!string.IsNullOrWhiteSpace(payload.QueryText))
            {
                lines.Add($"  Query: {payload.QueryText}");
            }

            if (!string.IsNullOrWhiteSpace(payload.SourceDocumentId))
            {
                lines.Add($"  Source: {payload.SourceDocumentId}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildExportTargetLine(BuildLabConceptIntakeState buildLab, BuildLabExportTarget target)
    {
        BuildLabExportPayload? payload = buildLab.ExportPayloads
            .FirstOrDefault(candidate => string.Equals(candidate.PayloadId, target.PayloadId, StringComparison.Ordinal));
        string payloadLine = payload is null ? string.Empty : $" · Payload: {payload.Title}";
        return $"{target.Label} ({target.TargetKind}) · {target.WorkflowId}{payloadLine}";
    }

    private static string BuildActionLine(BuildLabConceptIntakeState buildLab, BuildLabActionDescriptor action)
    {
        BuildLabExportTarget? target = buildLab.ExportTargets
            .FirstOrDefault(candidate => string.Equals(candidate.TargetId, action.TargetId, StringComparison.Ordinal));
        string label = target is null ? action.Label : $"{action.Label} -> {target.Label}";
        return action.Enabled ? label : $"{label} (disabled)";
    }

    private static string BuildFactionStatusText(ContactRelationshipGraphState contactGraph)
    {
        if (contactGraph.Factions.Count == 0)
        {
            return string.Empty;
        }

        IEnumerable<string> lines = contactGraph.Factions
            .Select(faction => $"{faction.Name}: {faction.Status} (contacts {faction.ContactCount}, heat {faction.AverageHeat})");
        return "Faction status rail" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static string BuildHeatAndObligationText(ContactRelationshipGraphState contactGraph)
    {
        List<string> lines = [];
        lines.Add("Heat rail");
        foreach (ContactRelationshipHeatState heat in contactGraph.HeatRails.Take(3))
        {
            lines.Add($"{heat.Subject}: {heat.Heat} ({heat.Status})");
        }

        lines.Add(string.Empty);
        lines.Add("Obligation rail");
        if (contactGraph.Obligations.Count == 0)
        {
            lines.Add("No active obligations.");
        }
        else
        {
            lines.AddRange(contactGraph.Obligations.Select(obligation => $"{obligation.Subject}: {obligation.Summary} ({obligation.Severity})"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildFavorRailText(ContactRelationshipGraphState contactGraph)
    {
        if (contactGraph.UnresolvedFavors.Count == 0)
        {
            return "Unresolved favor rail" + Environment.NewLine + "No unresolved favors.";
        }

        IEnumerable<string> lines = contactGraph.UnresolvedFavors
            .Select(favor => $"{favor.Subject}: {favor.Summary}{(favor.Overdue ? " (overdue)" : string.Empty)}");
        return "Unresolved favor rail" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static string BuildPersonaSummary(NpcPersonaStudioState personaStudio)
    {
        string defaultPersona = string.IsNullOrWhiteSpace(personaStudio.DefaultPersonaId)
            ? "none"
            : personaStudio.DefaultPersonaId;
        return $"NPC Persona Studio · default {defaultPersona} · selected {personaStudio.SelectedPersonaId}";
    }

    private static string BuildPersonaLine(NpcPersonaDescriptorState persona, string selectedPersonaId)
    {
        string selectedTag = string.Equals(persona.PersonaId, selectedPersonaId, StringComparison.Ordinal) ? "[selected] " : string.Empty;
        string evidenceTag = persona.EvidenceFirst ? "evidence-first" : "balanced";
        return $"{selectedTag}{persona.Label} ({persona.PersonaId}) · {evidenceTag} · {persona.ApprovalState}";
    }

    private static string BuildPolicyLine(NpcPersonaRoutePolicyState policy)
    {
        string provider = string.IsNullOrWhiteSpace(policy.PrimaryProviderId) ? "provider:none" : $"provider:{policy.PrimaryProviderId}";
        string routeClass = string.IsNullOrWhiteSpace(policy.RouteClassId) ? "class:none" : $"class:{policy.RouteClassId}";
        string tools = policy.AllowedToolIds.Count == 0 ? "tools:none" : $"tools:{string.Join(",", policy.AllowedToolIds)}";
        string persona = string.IsNullOrWhiteSpace(policy.PersonaId) ? "persona:none" : $"persona:{policy.PersonaId}";
        return $"{policy.RouteType} · {routeClass} · {provider} · {persona} · {policy.ApprovalState} · {tools}";
    }

    private static string BuildApprovalSummary(NpcPersonaStudioState personaStudio)
    {
        string draft = personaStudio.HasDraftPolicies ? "draft policies present" : "no draft policies";
        string approved = personaStudio.HasApprovedPolicies ? "approved policies present" : "no approved policies";
        return $"{draft}{Environment.NewLine}{approved}";
    }

    private static string BuildDowntimePlannerSummary(DowntimePlannerState downtimePlanner)
    {
        return $"Downtime planner · {downtimePlanner.PlannerLanes.Count} lanes · {downtimePlanner.ScheduleItems.Count} scheduled items";
    }

    private static string BuildDowntimeCalendarText(DowntimePlannerState downtimePlanner)
    {
        if (downtimePlanner.CalendarDays.Count == 0)
        {
            return "Calendar view" + Environment.NewLine + "No calendar entries.";
        }

        IEnumerable<string> lines = downtimePlanner.CalendarDays
            .Select(day => $"{day.Date}: {day.ItemCount} items · {day.Summary}");
        return "Calendar view" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private Button CreateQuickActionButton(SectionQuickActionDisplayItem quickAction)
    {
        Button button = new()
        {
            Name = $"SectionQuickAction_{quickAction.ControlId}",
            Content = quickAction.Label,
            Tag = quickAction.ControlId,
            Margin = new Thickness(0d, 0d, 8d, 0d)
        };
        button.Classes.Add("shell-action");
        button.Classes.Add(quickAction.IsPrimary ? "primary" : "quiet");
        button.Click += SectionQuickActionButton_OnClick;
        return button;
    }

    private void SectionQuickActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string controlId })
        {
            QuickActionRequested?.Invoke(this, controlId);
        }
    }

    private static IReadOnlyList<ClassicSheetFactDisplayItem> BuildCharacterSummaryFacts(string previewJson)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        if (root is null)
        {
            return Array.Empty<ClassicSheetFactDisplayItem>();
        }

        List<ClassicSheetFactDisplayItem> facts = [];
        AppendFact(facts, "Name", ReadString(root, "name"));
        AppendFact(facts, "Metatype", ReadString(root, "metatype"));
        AppendFact(facts, "Role", ReadString(root, "role"));
        AppendFact(facts, "Priority", ReadString(root, "priority"));
        AppendFact(facts, "Ruleset", ReadString(root, "ruleset")?.ToUpperInvariant());

        JsonObject? combat = root["combat"] as JsonObject;
        if (combat is not null)
        {
            AppendFact(facts, "Init", ReadString(combat, "initiative"));
            AppendFact(facts, "Armor", ReadScalar(combat, "armor"));
            AppendFact(facts, "Essence", ReadScalar(combat, "essence"));
        }

        return facts;
    }

    private static IReadOnlyList<ClassicSheetFactDisplayItem> BuildCharacterAttributeFacts(
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        JsonObject? attributes = root?["attributes"] as JsonObject;
        List<ClassicSheetFactDisplayItem> facts = [];

        if (attributes is not null)
        {
            foreach (string key in new[] { "Body", "Agility", "Reaction", "Strength", "Willpower", "Logic", "Intuition", "Charisma", "Edge", "Magic", "Resonance" })
            {
                string? value = ReadScalar(attributes, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    facts.Add(new ClassicSheetFactDisplayItem(ShortAttributeLabel(key), value));
                }
            }
        }

        if (facts.Count > 0)
        {
            return facts;
        }

        foreach (SectionRowDisplayItem row in rows)
        {
            if (!row.Path.StartsWith("attributes.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string attributeName = row.Path["attributes.".Length..];
            facts.Add(new ClassicSheetFactDisplayItem(ShortAttributeLabel(attributeName), row.DisplayValue));
        }

        return facts;
    }

    private static JsonObject? TryParseRootObject(string previewJson)
    {
        if (string.IsNullOrWhiteSpace(previewJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(previewJson) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static void AppendFact(List<ClassicSheetFactDisplayItem> facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Add(new ClassicSheetFactDisplayItem(label, value));
        }
    }

    private static string? ReadString(JsonObject source, string propertyName)
        => source.TryGetPropertyValue(propertyName, out JsonNode? node)
            ? SanitizeJsonValue(node)
            : null;

    private static string? ReadScalar(JsonObject source, string propertyName)
        => source.TryGetPropertyValue(propertyName, out JsonNode? node)
            ? SanitizeJsonValue(node)
            : null;

    private static string SanitizeJsonValue(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        string raw = node.ToJsonString();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
        {
            return raw[1..^1];
        }

        return raw;
    }

    private static string ShortAttributeLabel(string attributeName)
        => attributeName.Trim().ToLowerInvariant() switch
        {
            "body" => "BOD",
            "agility" => "AGI",
            "reaction" => "REA",
            "strength" => "STR",
            "willpower" => "WIL",
            "logic" => "LOG",
            "intuition" => "INT",
            "charisma" => "CHA",
            "edge" => "EDG",
            "magic" => "MAG",
            "resonance" => "RES",
            _ => attributeName.Length <= 3 ? attributeName.ToUpperInvariant() : attributeName[..Math.Min(3, attributeName.Length)].ToUpperInvariant()
        };

    private static Control CreateClassicFactCard(ClassicSheetFactDisplayItem fact, bool emphasizeValue)
    {
        Border card = new()
        {
            Margin = new Thickness(0d, 0d, 6d, 6d),
            Padding = new Thickness(5d, 3d),
            MinWidth = emphasizeValue ? 48d : 96d,
            Background = new SolidColorBrush(Color.Parse("#FFFDFDFB")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FFB7C2CF")),
            BorderThickness = new Thickness(1d)
        };

        StackPanel stack = new()
        {
            Spacing = 1d
        };
        stack.Children.Add(new TextBlock
        {
            Text = fact.Label,
            FontSize = 10d
        });
        stack.Children.Add(new TextBlock
        {
            Text = fact.Value,
            FontSize = emphasizeValue ? 16d : 13d,
            FontWeight = FontWeight.SemiBold
        });
        card.Child = stack;
        return card;
    }
}

public sealed record SectionHostState(
    string? SectionId,
    string Notice,
    string PreviewJson,
    SectionRowDisplayItem[] Rows,
    SectionQuickActionDisplayItem[] QuickActions,
    BuildLabConceptIntakeState? BuildLab,
    BrowseWorkspaceState? BrowseWorkspace,
    ContactRelationshipGraphState? ContactGraph,
    DowntimePlannerState? DowntimePlanner,
    NpcPersonaStudioState? NpcPersonaStudio);

public sealed record SectionRowDisplayItem(string Path, string Value)
{
    public string DisplayPath => BuildDisplayPath(Path);
    public string DisplayValue => SanitizeValue(Value);

    public override string ToString()
    {
        return $"{Path} = {Value}";
    }

    private static string BuildDisplayPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "(value)";
        }

        string[] segments = path
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return "(value)";
        }

        string section = segments[0];
        string leaf = segments[^1];
        if (string.Equals(section, "attributes", StringComparison.OrdinalIgnoreCase))
        {
            return FormatAttributeLabel(RemoveIndexer(leaf));
        }

        if (string.Equals(section, "combat", StringComparison.OrdinalIgnoreCase))
        {
            string combatKey = RemoveIndexer(leaf).Trim().ToLowerInvariant();
            return combatKey switch
            {
                "initiative" => "Init",
                "armor" => "Armor",
                "essence" => "Essence",
                _ => FormatDesktopLabel(leaf)
            };
        }

        return FormatDesktopLabel(leaf);
    }

    private static string SanitizeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
    }

    private static string FormatDesktopLabel(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "(value)";
        }

        string normalized = token.Trim();
        int? ordinal = null;
        int bracketIndex = normalized.IndexOf('[');
        if (bracketIndex >= 0)
        {
            int closingBracketIndex = normalized.IndexOf(']', bracketIndex + 1);
            if (closingBracketIndex > bracketIndex + 1
                && int.TryParse(normalized[(bracketIndex + 1)..closingBracketIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIndex))
            {
                ordinal = parsedIndex + 1;
            }

            normalized = normalized[..bracketIndex];
        }

        normalized = normalized.Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "(value)";
        }

        normalized = InsertWordBoundaries(normalized);
        string label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
        return ordinal is int index ? $"{label} {index}" : label;
    }

    private static string FormatAttributeLabel(string attributeName)
        => attributeName.Trim().ToLowerInvariant() switch
        {
            "body" => "BOD",
            "agility" => "AGI",
            "reaction" => "REA",
            "strength" => "STR",
            "willpower" => "WIL",
            "logic" => "LOG",
            "intuition" => "INT",
            "charisma" => "CHA",
            "edge" => "EDG",
            "magic" => "MAG",
            "resonance" => "RES",
            _ => attributeName.Length <= 3 ? attributeName.ToUpperInvariant() : attributeName[..Math.Min(3, attributeName.Length)].ToUpperInvariant()
        };

    private static string RemoveIndexer(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        int bracketIndex = token.IndexOf('[');
        return bracketIndex >= 0 ? token[..bracketIndex] : token;
    }

    private static string InsertWordBoundaries(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        StringBuilder builder = new(token.Length + 4);
        for (int i = 0; i < token.Length; i++)
        {
            char current = token[i];
            if (i > 0
                && char.IsUpper(current)
                && !char.IsWhiteSpace(token[i - 1])
                && !char.IsUpper(token[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}

public sealed record BrowseResultDisplayItem(string ItemId, string Label)
{
    public override string ToString()
    {
        return Label;
    }
}

public sealed record SectionQuickActionDisplayItem(string ControlId, string Label, bool IsPrimary);

public sealed record ClassicSheetFactDisplayItem(string Label, string Value);
