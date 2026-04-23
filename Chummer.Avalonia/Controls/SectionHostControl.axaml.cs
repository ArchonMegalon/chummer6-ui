using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    private bool _suppressNavigationTabSelectionChanged;
    private bool _suppressSectionActionSelectionChanged;

    public event EventHandler<string>? NavigationTabSelected;
    public event EventHandler<string>? SectionActionSelected;
    public event EventHandler<string>? QuickActionRequested;

    public SectionHostControl()
    {
        InitializeComponent();
    }

    public string XmlInputText => XmlInputBox.Text ?? string.Empty;

    public void SetState(SectionHostState state)
    {
        SetNavigationTabs(state.NavigationTabs, state.ActiveTabId);
        SetSectionActions(state.SectionActions, state.ActiveActionId);
        SetNotice(state.Notice);
        SetClassicCharacterSheet(state.SectionId, state.PreviewJson, state.Rows);
        SetSectionPreview(state.SectionId, state.PreviewJson, state.Rows);
        SetSectionQuickActions(state.QuickActions);
        SetBuildLab(state.BuildLab);
        SetBrowseWorkspace(state.BrowseWorkspace);
        SetContactGraph(state.ContactGraph);
        SetDowntimePlanner(state.DowntimePlanner);
        SetNpcPersonaStudio(state.NpcPersonaStudio);
        SetSectionContext(state.SectionId, state.PreviewJson, state.Rows, state.QuickActions);
    }

    public void SetNavigationTabs(IReadOnlyList<NavigatorTabItem> navigationTabs, string? activeTabId)
    {
        NavigatorTabItem[] visibleTabs = navigationTabs
            .Where(tab => tab.Enabled)
            .ToArray();

        LoadedRunnerTabStripBorder.IsVisible = visibleTabs.Length > 0;
        _suppressNavigationTabSelectionChanged = true;
        try
        {
            LoadedRunnerTabStrip.ItemsSource = visibleTabs;
            LoadedRunnerTabStrip.SelectedItem = visibleTabs.FirstOrDefault(tab =>
                string.Equals(tab.Id, activeTabId, StringComparison.Ordinal));
        }
        finally
        {
            _suppressNavigationTabSelectionChanged = false;
        }
    }

    public void SetSectionActions(IReadOnlyList<NavigatorSectionActionItem> sectionActions, string? activeActionId)
    {
        NavigatorSectionActionItem[] visibleActions = sectionActions
            .Where(action => !string.IsNullOrWhiteSpace(action.Id))
            .ToArray();

        bool showSectionActions = visibleActions.Length > 1;
        SectionActionTabStripBorder.IsVisible = showSectionActions;
        _suppressSectionActionSelectionChanged = true;
        try
        {
            SectionActionTabStrip.ItemsSource = showSectionActions ? visibleActions : Array.Empty<NavigatorSectionActionItem>();
            SectionActionTabStrip.SelectedItem = showSectionActions
                ? visibleActions.FirstOrDefault(action => string.Equals(action.Id, activeActionId, StringComparison.Ordinal))
                : null;
        }
        finally
        {
            _suppressSectionActionSelectionChanged = false;
        }

        UpdateSectionRowsHeight();
    }

    public void SetNotice(string notice)
    {
        // Chummer5a parity posture: do not spend persistent workbench height on shell notices.
        NoticeText.Text = string.Empty;
        NoticeBorder.IsVisible = false;
    }

    public void SetSectionPreview(string? sectionId, string previewJson, IEnumerable<SectionRowDisplayItem> rows)
    {
        SectionRowDisplayItem[] rowArray = rows.ToArray();
        string previewText = BuildSectionPreviewText(sectionId, previewJson, rowArray);
        SectionPreviewBox.Text = previewText;
        SectionReviewExpander.Header = BuildSectionPreviewHeader(sectionId, previewJson);
        SectionReviewExpander.IsVisible = !string.IsNullOrWhiteSpace(previewText);
        SectionReviewExpander.IsExpanded = true;
        SectionRowsList.ItemsSource = null;
        SectionRowsList.ItemsSource = rowArray;
    }

    public void SetSectionContext(
        string? sectionId,
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows,
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        SectionRowDisplayItem[] rowArray = rows.ToArray();
        bool showContext = !ClassicCharacterSheetBorder.IsVisible
            && (!string.IsNullOrWhiteSpace(sectionId) || rowArray.Length > 0 || quickActions.Count > 0);

        SectionContextBorder.IsVisible = showContext;
        SectionContextTitleText.Text = showContext ? BuildSectionTitle(sectionId, previewJson) : string.Empty;
        SectionContextSummaryText.Text = showContext ? BuildSectionSummary(sectionId, previewJson, rowArray, quickActions) : string.Empty;
        UpdateSectionRowsHeight();
    }

    public void SetClassicCharacterSheet(string? sectionId, string previewJson, IEnumerable<SectionRowDisplayItem> rows)
    {
        ClassicCharacterFactsPanel.Children.Clear();
        ClassicAttributeFactsPanel.Children.Clear();

        IReadOnlyList<ClassicSheetFactDisplayItem> summaryFacts = BuildCharacterSummaryFacts(previewJson);
        IReadOnlyList<ClassicSheetFactDisplayItem> attributeFacts = BuildCharacterAttributeFacts(previewJson, rows);
        ClassicCharacterSummaryTitle.Text = BuildClassicSheetTitle(sectionId, summaryFacts, attributeFacts);

        foreach (ClassicSheetFactDisplayItem fact in summaryFacts)
        {
            ClassicCharacterFactsPanel.Children.Add(CreateClassicFactCard(fact, emphasizeValue: false));
        }

        foreach (ClassicSheetFactDisplayItem fact in attributeFacts)
        {
            ClassicAttributeFactsPanel.Children.Add(CreateClassicFactCard(fact, emphasizeValue: true));
        }

        ClassicCharacterSheetBorder.IsVisible = summaryFacts.Count > 0 || attributeFacts.Count > 0;
        UpdateSectionRowsHeight();
    }

    public void SetSectionQuickActions(IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        SectionQuickActionsHost.Children.Clear();

        foreach (SectionQuickActionDisplayItem quickAction in quickActions)
        {
            SectionQuickActionsHost.Children.Add(CreateQuickActionButton(quickAction));
        }

        SectionQuickActionsBorder.IsVisible = quickActions.Count > 0;
        UpdateSectionRowsHeight();
    }

    private void LoadedRunnerTabStrip_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressNavigationTabSelectionChanged)
        {
            return;
        }

        if (sender is SelectingItemsControl { SelectedItem: NavigatorTabItem tab }
            && !string.IsNullOrWhiteSpace(tab.Id))
        {
            NavigationTabSelected?.Invoke(this, tab.Id);
        }
    }

    private void SectionActionTabStrip_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSectionActionSelectionChanged)
        {
            return;
        }

        if (sender is SelectingItemsControl { SelectedItem: NavigatorSectionActionItem action }
            && !string.IsNullOrWhiteSpace(action.Id))
        {
            SectionActionSelected?.Invoke(this, action.Id);
        }
    }

    public void SetBuildLab(BuildLabConceptIntakeState? buildLab)
    {
        // Chummer5a parity posture: remove synthetic build-lab scaffolding.
    }

    public void SetBrowseWorkspace(BrowseWorkspaceState? browseWorkspace)
    {
        // Chummer5a parity posture: remove synthetic browse-workspace scaffolding.
    }

    public void SetContactGraph(ContactRelationshipGraphState? contactGraph)
    {
        // Chummer5a parity posture: remove synthetic contact-graph scaffolding.
    }

    public void SetNpcPersonaStudio(NpcPersonaStudioState? npcPersonaStudio)
    {
        // Chummer5a parity posture: remove synthetic NPC-persona scaffolding.
    }

    public void SetDowntimePlanner(DowntimePlannerState? downtimePlanner)
    {
        // Chummer5a parity posture: remove synthetic downtime-planner scaffolding.
    }

    private void BrowseResultsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Synthetic browse-workspace routing removed for shell parity.
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
            // m104: avalonia_build_blocker_receipts
            lines.Add($"Build blocker receipt: {BuildBuildBlockerBadge(buildLab)}");
            lines.Add($"Explain receipt: {BuildBuildBlockerExplainReceipt(buildLab)}");
            lines.Add($"Rule environment: {buildLab.RulesetId} / {buildLab.BuildMethod}");
            lines.Add($"Environment diff: {BuildBuildBlockerBefore(buildLab)} -> {BuildBuildBlockerAfter(buildLab)}");
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

    private static string BuildBuildBlockerExplainReceipt(BuildLabConceptIntakeState buildLab)
        => FirstNonBlank(
            buildLab.ExplainEntryId,
            buildLab.SourceDocumentId,
            $"{buildLab.RulesetId}/{buildLab.BuildMethod} blocker receipt");

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
            IsVisible = true,
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
        AppendFact(facts, "Alias", ReadString(root, "alias"));
        AppendFact(facts, "Metatype", ReadString(root, "metatype"));
        AppendFact(facts, "Concept", ReadString(root, "concept"));
        AppendFact(facts, "Role", ReadString(root, "role"));
        AppendFact(facts, "Build", FirstNonBlank(
            ReadString(root, "buildMethod"),
            ReadString(root, "buildmethod"),
            ReadString(root, "priority")));
        AppendFact(facts, "Ruleset", FirstNonBlank(
            ReadString(root, "gameEdition"),
            ReadString(root, "ruleset"))?.ToUpperInvariant());
        AppendFact(facts, "Karma", ReadScalar(root, "karma"));
        AppendFact(facts, "Nuyen", ReadScalar(root, "nuyen"));

        JsonObject? combat = ReadObject(root, "combat");
        if (combat is not null)
        {
            AppendFact(facts, "Init", ReadString(combat, "initiative"));
            AppendFact(facts, "Armor", ReadScalar(combat, "armor"));
            AppendFact(facts, "Essence", ReadScalar(combat, "essence"));
        }

        return facts.Take(6).ToArray();
    }

    private static IReadOnlyList<ClassicSheetFactDisplayItem> BuildCharacterAttributeFacts(
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        List<ClassicSheetFactDisplayItem> facts = [];

        if (ReadArray(root, "attributes") is { Count: > 0 } attributeArray)
        {
            foreach (JsonNode? node in attributeArray)
            {
                if (node is not JsonObject attribute)
                {
                    continue;
                }

                string name = FirstNonBlank(
                    ReadString(attribute, "name"),
                    ReadString(attribute, "label"));
                string value = FirstNonBlank(
                    ReadScalar(attribute, "totalValue"),
                    ReadScalar(attribute, "baseValue"),
                    ReadScalar(attribute, "value"),
                    ReadScalar(attribute, "base"));
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                {
                    facts.Add(new ClassicSheetFactDisplayItem(ShortAttributeLabel(name), value));
                }
            }
        }

        if (facts.Count == 0 && ReadObject(root, "attributes") is { } attributesObject)
        {
            foreach (string key in new[] { "Body", "Agility", "Reaction", "Strength", "Willpower", "Logic", "Intuition", "Charisma", "Edge", "Magic", "Resonance" })
            {
                string? value = ReadScalar(attributesObject, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    facts.Add(new ClassicSheetFactDisplayItem(ShortAttributeLabel(key), value));
                }
            }
        }

        if (facts.Count == 0)
        {
            foreach (SectionRowDisplayItem row in rows)
            {
                if (!row.Path.StartsWith("attributes.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string attributeName = row.Path["attributes.".Length..];
                if (!string.IsNullOrWhiteSpace(row.DisplayValue))
                {
                    facts.Add(new ClassicSheetFactDisplayItem(ShortAttributeLabel(attributeName), row.DisplayValue));
                }
            }
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

    private static string BuildSectionTitle(string? sectionId, string previewJson)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        string? previewSection = root is not null ? FirstNonBlank(ReadString(root, "section"), ReadString(root, "sectionId")) : null;
        string rawSection = string.IsNullOrWhiteSpace(sectionId) ? previewSection ?? "Section" : sectionId;
        return rawSection.Trim().ToLowerInvariant() switch
        {
            "summary" => "Runner Summary",
            "profile" => "Profile",
            "cyberwares" => "Cyberware",
            "attributedetails" => "Attributes",
            "complexforms" => "Complex Forms",
            "initiationgrades" => "Initiation & Submersion",
            "mentorspirits" => "Mentor Spirits",
            "progress" => "Karma Journal",
            _ => FormatSectionName(rawSection)
        };
    }

    private static string BuildSectionSummary(
        string? sectionId,
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows,
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        SectionRowDisplayItem[] rowArray = rows.ToArray();
        List<string> parts = [];
        JsonObject? root = TryParseRootObject(previewJson);
        string title = BuildSectionTitle(sectionId, previewJson);
        int? recordedCount = ReadCount(root);

        if (recordedCount is > 0)
        {
            parts.Add(recordedCount == 1 ? "1 visible entry" : $"{recordedCount} visible entries");
        }
        else if (rowArray.Length > 0)
        {
            parts.Add(rowArray.Length == 1 ? "1 visible entry" : $"{rowArray.Length} visible entries");
        }

        if (rowArray.Length > 0)
        {
            string leadPath = rowArray[0].DisplayPath.Trim();
            string leadValue = rowArray[0].DisplayValue.Trim();
            if (!string.IsNullOrWhiteSpace(leadValue) || !string.IsNullOrWhiteSpace(leadPath))
            {
                parts.Add(string.IsNullOrWhiteSpace(leadValue) ? leadPath : $"{leadPath}: {leadValue}");
            }
        }

        if (quickActions.Count > 0)
        {
            string actionSummary = string.Join(", ", quickActions.Take(2).Select(action => action.Label));
            if (quickActions.Count > 2)
            {
                actionSummary = $"{actionSummary}, +{quickActions.Count - 2} more";
            }

            parts.Add($"Actions: {actionSummary}");
        }

        return parts.Count == 0
            ? BuildEmptySectionSummary(sectionId, title, quickActions)
            : string.Join("  •  ", parts);
    }

    private static string FormatSectionName(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return "Section";
        }

        string normalized = sectionName.Replace('_', ' ').Replace('-', ' ').Trim();
        normalized = InsertWordBoundaries(normalized);
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
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

    private static void AppendFact(List<ClassicSheetFactDisplayItem> facts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            facts.Add(new ClassicSheetFactDisplayItem(label, value));
        }
    }

    private static string? ReadString(JsonObject source, string propertyName)
        => TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? SanitizeJsonValue(node)
            : null;

    private static string? ReadScalar(JsonObject source, string propertyName)
        => TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? SanitizeJsonValue(node)
            : null;

    private static JsonObject? ReadObject(JsonObject? source, string propertyName)
        => source is not null
            && TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? node as JsonObject
            : null;

    private static JsonArray? ReadArray(JsonObject? source, string propertyName)
        => source is not null
            && TryGetPropertyValueIgnoreCase(source, propertyName, out JsonNode? node)
            ? node as JsonArray
            : null;

    private static bool TryGetPropertyValueIgnoreCase(JsonObject source, string propertyName, out JsonNode? node)
    {
        foreach ((string key, JsonNode? value) in source)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                node = value;
                return true;
            }
        }

        node = null;
        return false;
    }

    private static int? ReadCount(JsonObject? root)
    {
        if (root is null)
        {
            return null;
        }

        foreach (string key in new[]
                 {
                     "count",
                     "gearCount",
                     "weaponCount",
                     "armorCount",
                     "cyberwareCount",
                     "vehicleCount",
                     "knowledgeCount"
                 })
        {
            if (int.TryParse(ReadScalar(root, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            {
                return count;
            }
        }

        return null;
    }

    private static string BuildClassicSheetTitle(
        string? sectionId,
        IReadOnlyList<ClassicSheetFactDisplayItem> summaryFacts,
        IReadOnlyList<ClassicSheetFactDisplayItem> attributeFacts)
    {
        string title = string.IsNullOrWhiteSpace(sectionId)
            ? "Runner Summary"
            : BuildSectionTitle(sectionId, "{}");
        if (attributeFacts.Count > 0
            && (string.Equals(sectionId, "profile", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionId, "summary", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionId, "attributes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sectionId, "attributedetails", StringComparison.OrdinalIgnoreCase)))
        {
            return "Runner Summary";
        }

        return summaryFacts.Count > 0 || attributeFacts.Count > 0
            ? $"{title} Overview"
            : title;
    }

    private static string BuildSectionPreviewText(
        string? sectionId,
        string previewJson,
        IEnumerable<SectionRowDisplayItem> rows)
    {
        JsonObject? root = TryParseRootObject(previewJson);
        SectionRowDisplayItem[] rowArray = rows.ToArray();
        List<string> lines = [];
        string title = BuildSectionTitle(sectionId, previewJson);

        if (!string.IsNullOrWhiteSpace(title))
        {
            lines.Add(title);
        }

        AppendPreviewScalarLine(lines, "Name", root, "name");
        AppendPreviewScalarLine(lines, "Alias", root, "alias");
        AppendPreviewScalarLine(lines, "Metatype", root, "metatype");
        AppendPreviewScalarLine(lines, "Concept", root, "concept");
        AppendPreviewScalarLine(lines, "Build Method", root, "buildMethod", "buildmethod");
        AppendPreviewScalarLine(lines, "Ruleset", root, "gameEdition", "ruleset");
        AppendPreviewScalarLine(lines, "Karma", root, "karma");
        AppendPreviewScalarLine(lines, "Nuyen", root, "nuyen");
        AppendPreviewScalarLine(lines, "Street Cred", root, "streetCred");
        AppendPreviewScalarLine(lines, "Notoriety", root, "notoriety");
        AppendPreviewScalarLine(lines, "Public Awareness", root, "publicAwareness");

        if (ReadObject(root, "combat") is { } combat)
        {
            AppendPreviewScalarLine(lines, "Initiative", combat, "initiative");
            AppendPreviewScalarLine(lines, "Armor", combat, "armor");
            AppendPreviewScalarLine(lines, "Essence", combat, "essence");
        }

        if (lines.Count > 1 && rowArray.Length > 0)
        {
            lines.Add(string.Empty);
        }

        if (rowArray.Length > 0)
        {
            foreach (SectionRowDisplayItem row in rowArray.Take(10))
            {
                string label = row.DisplayPath.Trim();
                string value = row.DisplayValue.Trim();
                if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                lines.Add(string.IsNullOrWhiteSpace(value)
                    ? label
                    : $"{label}: {value}");
            }

            if (rowArray.Length > 10)
            {
                lines.Add($"+{rowArray.Length - 10} more entries");
            }
        }
        else if (lines.Count == 1)
        {
            lines.Add(BuildEmptySectionReviewLine(sectionId));
        }

        string normalizedPayload = previewJson.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPayload))
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("Payload");
            lines.Add(normalizedPayload);
        }

        return string.Join(Environment.NewLine, lines.Where(static line => line is not null)).Trim();
    }

    private static string BuildSectionPreviewHeader(string? sectionId, string previewJson)
    {
        string title = BuildSectionTitle(sectionId, previewJson);
        return sectionId?.Trim().ToLowerInvariant() switch
        {
            "summary" or "profile" or "attributes" or "attributedetails" => $"{title} Review",
            "skills" or "qualities" or "contacts" => $"{title} Review",
            "gear" or "inventory" or "weapons" or "armors" or "cyberwares" or "vehicles" => $"{title} Loadout Review",
            "progress" or "calendar" or "expenses" or "improvements" => $"{title} Journal Review",
            "rules" => $"{title} Snapshot",
            _ => $"{title} Review"
        };
    }

    private static string BuildEmptySectionSummary(
        string? sectionId,
        string title,
        IReadOnlyList<SectionQuickActionDisplayItem> quickActions)
    {
        string? primaryActionLabel = quickActions
            .FirstOrDefault(action => action.IsPrimary)?.Label
            ?? quickActions.FirstOrDefault()?.Label;
        string emptySummary = NormalizeSectionId(sectionId) switch
        {
            "attributes" or "attributedetails" => "No attribute values are recorded yet.",
            "skills" => "No active or knowledge skills are recorded yet.",
            "qualities" => "No positive or negative qualities are recorded yet.",
            "contacts" => "No contacts are recorded yet.",
            "gear" or "inventory" => "No carried gear is recorded yet.",
            "weapons" => "No weapons are recorded yet.",
            "armors" => "No armor pieces are recorded yet.",
            "cyberwares" => "No cyberware or bioware is recorded yet.",
            "vehicles" => "No vehicles are recorded yet.",
            "spells" => "No spells are recorded yet.",
            "powers" => "No adept powers are recorded yet.",
            "complexforms" => "No complex forms or programs are recorded yet.",
            "drugs" => "No drugs or consumables are recorded yet.",
            "progress" or "calendar" => "No journal entries are recorded yet.",
            "initiationgrades" => "No initiation or submersion grades are recorded yet.",
            "profile" => "Runner identity details are still blank.",
            "rules" => "Rules and provider selections are still blank.",
            _ => $"{title} is ready."
        };

        if (!string.IsNullOrWhiteSpace(primaryActionLabel))
        {
            return $"{emptySummary} Use {primaryActionLabel}.";
        }

        return emptySummary;
    }

    private static string BuildEmptySectionReviewLine(string? sectionId)
    {
        return NormalizeSectionId(sectionId) switch
        {
            "attributes" or "attributedetails" => "No attribute values are recorded yet.",
            "skills" => "No skills are recorded yet.",
            "qualities" => "No qualities are recorded yet.",
            "contacts" => "No contacts are recorded yet.",
            "gear" or "inventory" => "No gear entries are recorded yet.",
            "weapons" => "No weapons are recorded yet.",
            "armors" => "No armor entries are recorded yet.",
            "cyberwares" => "No cyberware entries are recorded yet.",
            "vehicles" => "No vehicles are recorded yet.",
            "spells" => "No spells are recorded yet.",
            "powers" => "No adept powers are recorded yet.",
            "complexforms" => "No complex forms are recorded yet.",
            "drugs" => "No consumables are recorded yet.",
            "progress" or "calendar" => "No karma journal entries are recorded yet.",
            "initiationgrades" => "No initiation entries are recorded yet.",
            "profile" => "No profile details are recorded yet.",
            "rules" => "No ruleset selections are recorded yet.",
            _ => "No recorded entries yet."
        };
    }

    private static string? NormalizeSectionId(string? sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            return null;
        }

        return sectionId.Trim().ToLowerInvariant();
    }

    private static void AppendPreviewScalarLine(
        List<string> lines,
        string label,
        JsonObject? source,
        params string[] propertyNames)
    {
        if (source is null)
        {
            return;
        }

        string value = FirstNonBlank(propertyNames.Select(propertyName => ReadScalar(source, propertyName)).ToArray());
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }

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
            Margin = new Thickness(0d, 0d, 4d, 4d),
            Padding = emphasizeValue ? new Thickness(3d, 2d) : new Thickness(4d, 3d),
            MinWidth = emphasizeValue ? 38d : 76d,
            MinHeight = emphasizeValue ? 28d : 32d,
            Background = new SolidColorBrush(Color.Parse(emphasizeValue ? "#FFF2F2F2" : "#FFF7F4EB")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FF8D8D8D")),
            BorderThickness = new Thickness(1d)
        };

        StackPanel stack = new()
        {
            Spacing = 0d
        };
        stack.Children.Add(new TextBlock
        {
            Text = fact.Label,
            FontSize = emphasizeValue ? 8d : 9d,
            FontWeight = FontWeight.Medium,
            TextAlignment = emphasizeValue ? TextAlignment.Center : TextAlignment.Left
        });
        stack.Children.Add(new TextBlock
        {
            Text = fact.Value,
            FontSize = emphasizeValue ? 15d : 12d,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = emphasizeValue ? TextAlignment.Center : TextAlignment.Left
        });
        card.Child = stack;
        return card;
    }

    private void UpdateSectionRowsHeight()
    {
        bool denseChromeVisible = ClassicCharacterSheetBorder.IsVisible
            || SectionContextBorder.IsVisible
            || SectionActionTabStripBorder.IsVisible
            || SectionQuickActionsBorder.IsVisible;
        SectionRowsList.Height = denseChromeVisible ? 176d : 212d;
    }
}

public sealed record SectionHostState(
    string? SectionId,
    NavigatorTabItem[] NavigationTabs,
    string? ActiveTabId,
    NavigatorSectionActionItem[] SectionActions,
    string? ActiveActionId,
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
        string bareLeaf = RemoveIndexer(leaf);
        string bareSection = RemoveIndexer(section);
        if (string.Equals(section, "attributes", StringComparison.OrdinalIgnoreCase))
        {
            return FormatAttributeLabel(bareLeaf);
        }

        if (string.Equals(section, "combat", StringComparison.OrdinalIgnoreCase))
        {
            string combatKey = bareLeaf.Trim().ToLowerInvariant();
            return combatKey switch
            {
                "initiative" => "Init",
                "armor" => "Armor",
                "essence" => "Essence",
                _ => FormatDesktopLabel(leaf)
            };
        }

        if (string.Equals(bareLeaf, bareSection, StringComparison.OrdinalIgnoreCase))
        {
            return FormatCollectionLabel(bareSection, leaf);
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

    private static string FormatCollectionLabel(string section, string token)
    {
        string normalizedSection = section.Trim().ToLowerInvariant() switch
        {
            "attributes" => "attribute",
            "skills" => "skill",
            "qualities" => "quality",
            "gear" => "gear",
            "weapons" => "weapon",
            "armors" => "armor",
            "cyberwares" => "cyberware",
            "vehicles" => "vehicle",
            "contacts" => "contact",
            "spells" => "spell",
            "powers" => "power",
            "drugs" => "drug",
            "aiprograms" => "program",
            "expenses" => "expense",
            "improvements" => "improvement",
            "complexforms" => "complex form",
            "initiationgrades" => "initiation grade",
            "mentorspirits" => "mentor spirit",
            "progress" => "entry",
            "calendar" => "entry",
            _ => RemoveIndexer(token).Replace('_', ' ').Replace('-', ' ')
        };

        string title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalizedSection);
        int bracketIndex = token.IndexOf('[');
        if (bracketIndex >= 0)
        {
            int closingBracketIndex = token.IndexOf(']', bracketIndex + 1);
            if (closingBracketIndex > bracketIndex + 1
                && int.TryParse(token[(bracketIndex + 1)..closingBracketIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return $"{title} {index + 1}";
            }
        }

        return title;
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
