using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.AI;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Content;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Avalonia;

internal sealed class DesktopAliceWindow : Window
{
    internal static DesktopAliceWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private readonly IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private readonly IReadOnlyList<DesktopBuildPathCandidate> _buildPathCandidates;
    private readonly IAvaloniaCoachSidecarClient? _coachSidecarClient;
    private readonly string? _rulesetId;
    private readonly string? _workspaceId;
    private readonly string _coachConversationId = $"alice-coach-{Guid.NewGuid():N}";
    private readonly string _buildConversationId = $"alice-build-{Guid.NewGuid():N}";
    private BuildLabHandoffProjection? _selectedHandoff;
    private DesktopBuildPathCandidate? _selectedBuildPath;
    private Action? _refreshAssistantContext;
    private CharacterNarrativeDraft? _originDraft;
    private bool HasHandoffContext => (_campaignSummary?.BuildLabHandoffs.Count ?? 0) > 0;
    private bool HasBuildPathContext => _buildPathCandidates.Count > 0;

    private DesktopAliceWindow(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        IReadOnlyList<DesktopBuildPathCandidate> buildPathCandidates,
        IAvaloniaCoachSidecarClient? coachSidecarClient,
        string? rulesetId)
    {
        _campaignSummary = campaignSummary;
        _recentWorkspaces = recentWorkspaces;
        _buildPathCandidates = buildPathCandidates;
        _coachSidecarClient = coachSidecarClient;
        _rulesetId = RulesetDefaults.NormalizeOptional(rulesetId);
        _workspaceId = recentWorkspaces.FirstOrDefault()?.Id.Value;
        _selectedHandoff = campaignSummary?.BuildLabHandoffs.OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefault();
        _selectedBuildPath = buildPathCandidates.FirstOrDefault();

        Title = "ALICE";
        Width = 940;
        Height = 680;
        MinWidth = 840;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "ALICE",
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "ALICE keeps build compare, rule diffs, tradeoffs, and apply-safe follow-through on first-party rails. This desktop bench surfaces the current handoff lane instead of forcing blind browser jumps.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        CreateAssistantCard(),
                        CreateLeadHandoffCard(),
                        CreateHandoffListCard(),
                        CreateBuildPathCard(),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton("Open public ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice")),
                                CreateButton("Close", static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopAliceWindow dialog = await CreateAsync().ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopAliceWindow> CreateAsync()
    {
        AccountCampaignSummary? summary = null;
        IReadOnlyList<WorkspaceListItem> workspaces = Array.Empty<WorkspaceListItem>();
        IReadOnlyList<DesktopBuildPathCandidate> buildPathCandidates = Array.Empty<DesktopBuildPathCandidate>();
        IAvaloniaCoachSidecarClient? coachSidecarClient = App.Services?.GetService(typeof(IAvaloniaCoachSidecarClient)) as IAvaloniaCoachSidecarClient;
        string? effectiveRulesetId = null;
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop ALICE requires an IChummerClient instance."));
            summary = await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(true);
            workspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
            effectiveRulesetId = ResolveRulesetId(workspaces);
            IReadOnlyList<DesktopBuildPathSuggestion> suggestions = await client.GetBuildPathSuggestionsAsync(effectiveRulesetId, CancellationToken.None).ConfigureAwait(true);
            buildPathCandidates = await ReadBuildPathCandidatesAsync(client, effectiveRulesetId, workspaces, suggestions).ConfigureAwait(true);
        }
        catch
        {
            summary = null;
            workspaces = Array.Empty<WorkspaceListItem>();
            buildPathCandidates = Array.Empty<DesktopBuildPathCandidate>();
        }

        return new DesktopAliceWindow(summary, workspaces, buildPathCandidates, coachSidecarClient, effectiveRulesetId);
    }

    private Control CreateAssistantCard()
    {
        IReadOnlyList<string> modes = ["Build help", "Rules coach", "Origin draft"];
        List<AliceConversationTurnEntry> buildHistory = [];
        List<AliceConversationTurnEntry> rulesHistory = [];
        List<AliceConversationTurnEntry> originHistory = [];
        ComboBox modeCombo = new()
        {
            Name = "AliceConversationModeCombo",
            MinWidth = 220,
            ItemsSource = modes,
            SelectedIndex = HasBuildPathContext ? 0 : 1
        };

        ListBox conversationList = new()
        {
            Name = "AliceConversationList",
            MinHeight = 220,
            MaxHeight = 320,
            ItemTemplate = new FuncDataTemplate<AliceConversationTurnEntry>((entry, _) => BuildConversationTurnView(entry))
        };

        TextBox promptBox = new()
        {
            Name = "AliceQuestionTextBox",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 78,
            Watermark = "Ask ALICE about the current build, rules tradeoffs, or what to add next."
        };

        TextBlock statusText = new()
        {
            Name = "AliceAssistantStatusText",
            Text = BuildIdleAssistantStatus(modeCombo.SelectedItem?.ToString()),
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155"),
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock answerText = new()
        {
            Name = "AliceAssistantAnswerText",
            Text = BuildIdleAssistantAnswer(modeCombo.SelectedItem?.ToString()),
            TextWrapping = TextWrapping.Wrap
        };

        ListBox evidenceList = new()
        {
            Name = "AliceAssistantEvidenceList",
            MinHeight = 120
        };

        WrapPanel actionRow = new()
        {
            Name = "AliceAssistantActionRow",
            Orientation = Orientation.Horizontal,
            ItemHeight = double.NaN,
            ItemWidth = double.NaN
        };

        WrapPanel starterPromptRow = new()
        {
            Name = "AliceStarterPromptRow",
            Orientation = Orientation.Horizontal,
            ItemHeight = double.NaN,
            ItemWidth = double.NaN
        };

        TextBlock contextHeadingText = new()
        {
            Name = "AliceAssistantContextHeadingText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock contextSummaryText = new()
        {
            Name = "AliceAssistantContextSummaryText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock contextDetailText = new()
        {
            Name = "AliceAssistantContextDetailText",
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155"),
            TextWrapping = TextWrapping.Wrap
        };

        List<AliceConversationTurnEntry> ActiveHistory()
        {
            string mode = modeCombo.SelectedItem?.ToString() ?? "Build help";
            return mode switch
            {
                "Rules coach" => rulesHistory,
                "Origin draft" => originHistory,
                _ => buildHistory
            };
        }

        void RefreshConversationFeed()
        {
            List<AliceConversationTurnEntry> history = ActiveHistory();
            conversationList.ItemsSource = history.Count == 0
                ? [BuildWelcomeEntry(modeCombo.SelectedItem?.ToString())]
                : history.ToArray();
            if (conversationList.ItemCount > 0)
            {
                conversationList.SelectedIndex = conversationList.ItemCount - 1;
                if (conversationList.SelectedItem is { } selectedItem)
                {
                    conversationList.ScrollIntoView(selectedItem);
                }
            }
        }

        void RefreshStarterPrompts()
        {
            starterPromptRow.Children.Clear();
            foreach (string prompt in BuildStarterPrompts(modeCombo.SelectedItem?.ToString()))
            {
                Button button = CreateButton(
                    prompt,
                    () =>
                    {
                        promptBox.Text = prompt;
                        return Task.CompletedTask;
                    },
                    name: $"AliceStarterPrompt_{SanitizeNameToken(prompt)}");
                button.MinWidth = 0;
                button.Margin = new Thickness(0, 0, 8, 8);
                starterPromptRow.Children.Add(button);
            }
        }

        void RefreshAssistantContextSummary()
        {
            AliceAssistantContextProjection projection = BuildAssistantContextProjection(modeCombo.SelectedItem?.ToString());
            contextHeadingText.Text = projection.Title;
            contextSummaryText.Text = projection.Summary;
            contextDetailText.Text = projection.Detail;
        }

        void ApplyIdleState()
        {
            statusText.Text = BuildIdleAssistantStatus(modeCombo.SelectedItem?.ToString());
            answerText.Text = BuildIdleAssistantAnswer(modeCombo.SelectedItem?.ToString());
            evidenceList.ItemsSource = BuildIdleEvidence(modeCombo.SelectedItem?.ToString());
            actionRow.Children.Clear();
            if (string.Equals(modeCombo.SelectedItem?.ToString(), "Origin draft", StringComparison.Ordinal))
            {
                actionRow.Children.Add(CreateButton("Open proposal studio", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceAssistantOpenProposalStudioButton"));
                actionRow.Children.Add(CreateButton("Open account ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceAssistantOpenAccountButton"));
            }
            else
            {
                actionRow.Children.Add(CreateButton("Open account ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceAssistantOpenAccountButton"));
                actionRow.Children.Add(CreateButton("Open public ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceAssistantOpenPublicButton"));
            }
            RefreshAssistantContextSummary();
            RefreshStarterPrompts();
            RefreshConversationFeed();
        }

        async Task AskAsync()
        {
            string message = promptBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                ApplyIdleState();
                statusText.Text = "Type a grounded question before asking ALICE.";
                return;
            }

            string mode = modeCombo.SelectedItem?.ToString() ?? "Build help";
            ActiveHistory().Add(BuildUserTurn(message));
            RefreshConversationFeed();
            statusText.Text = $"ALICE is checking the {mode.ToLowerInvariant()} lane.";
            answerText.Text = "Waiting for grounded assistant output...";
            evidenceList.ItemsSource = Array.Empty<string>();
            actionRow.Children.Clear();

            if (string.Equals(mode, "Origin draft", StringComparison.Ordinal))
            {
                CharacterNarrativePacket packet = BuildNarrativePacket(message);
                CharacterNarrativeDraft originDraft = BuildOriginDraft(packet);
                _originDraft = originDraft;
                statusText.Text = "ALICE generated a grounded origin draft from the current desktop build context.";
                answerText.Text = originDraft.Prose;
                string[] originEvidence = BuildOriginEvidence(packet, originDraft);
                evidenceList.ItemsSource = originEvidence;
                string[] originActionTitles =
                [
                    "Regenerate with same facts",
                    "Focus on qualities",
                    "Focus on implants"
                ];
                ActiveHistory().Add(BuildAssistantTurn(
                    mode,
                    statusText.Text,
                    answerText.Text,
                    originEvidence,
                    originActionTitles));
                RefreshConversationFeed();
                actionRow.Children.Add(CreateButton("Regenerate origin", AskAsync, isPrimary: true, name: "AliceOriginRegenerateButton"));
                actionRow.Children.Add(CreateButton("Open proposal studio", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceOriginOpenProposalStudioButton"));
                promptBox.Text = string.Empty;
                return;
            }

            AiConversationTurnResponse? response = await TryAskAssistantAsync(mode, message).ConfigureAwait(true);
            if (response is null)
            {
                statusText.Text = "ALICE stayed local because no grounded coach route was reachable from this desktop head.";
                answerText.Text = BuildLocalFallbackAnswer(mode, message);
                string[] fallbackEvidence = BuildLocalFallbackEvidence(mode);
                evidenceList.ItemsSource = fallbackEvidence;
                ActiveHistory().Add(BuildAssistantTurn(
                    mode,
                    statusText.Text,
                    answerText.Text,
                    fallbackEvidence,
                    ["Open account ALICE", "Open public ALICE"]));
                RefreshConversationFeed();
                actionRow.Children.Add(CreateButton("Open account ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceAssistantFallbackAccountButton"));
                actionRow.Children.Add(CreateButton("Open public ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceAssistantFallbackPublicButton"));
                return;
            }

            statusText.Text = BuildStatusLine(response);
            answerText.Text = response.Answer;
            string[] evidenceLines = BuildEvidenceLines(response);
            evidenceList.ItemsSource = evidenceLines;
            string[] suggestedActionTitles = response.SuggestedActions.Take(3).Select(static action => action.Title).ToArray();
            ActiveHistory().Add(BuildAssistantTurn(
                mode,
                statusText.Text,
                answerText.Text,
                evidenceLines,
                suggestedActionTitles));
            RefreshConversationFeed();
            actionRow.Children.Clear();
            foreach (Button action in CreateSuggestedActionButtons(response))
            {
                actionRow.Children.Add(action);
            }
            promptBox.Text = string.Empty;
        }

        _refreshAssistantContext = ApplyIdleState;
        modeCombo.SelectionChanged += (_, _) => ApplyIdleState();
        ApplyIdleState();

        StackPanel body = new()
        {
            Spacing = 8,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("AliceAssistantRulesetBadge", "Ruleset", _rulesetId ?? "none"),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("AliceAssistantContextBadge", "Context", !string.IsNullOrWhiteSpace(_workspaceId) ? "workspace" : "global"),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("AliceAssistantContinuityBadge", "Continuity", HasBuildPathContext || HasHandoffContext ? "attached" : "local")),
                modeCombo,
                new Border
                {
                    Name = "AliceAssistantContextCard",
                    BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
                    BorderThickness = new Thickness(1),
                    Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSelectionInsetBrush", "#F1F5F9"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            contextHeadingText,
                            contextSummaryText,
                            contextDetailText
                        }
                    }
                },
                conversationList,
                starterPromptRow,
                promptBox,
                statusText,
                new Border
                {
                    Name = "AliceAssistantAnswerCard",
                    BorderBrush = new SolidColorBrush(Color.Parse("#D3DCE5")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = answerText
                },
                evidenceList,
                actionRow
            }
        };

        return CreateCard(
            "Assistant rail",
            "ALICE now answers grounded rules and build questions directly from the desktop instead of only handing off to a browser lane.",
            body,
            "AliceAssistantCard",
            CreateButton("Ask ALICE", AskAsync, isPrimary: true, name: "AliceAskButton"));
    }

    private Control CreateLeadHandoffCard()
    {
        BuildLabHandoffProjection? lead = _campaignSummary?.BuildLabHandoffs
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();

        if (lead is null)
        {
            return CreateCard(
                "Lead build handoff",
                "No governed build handoff is currently available in the signed-in account context.",
                null,
                "AliceLeadHandoffCard",
                CreateButton("Open ALICE account rail", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceOpenAccountRailButton"),
                CreateButton("Open public ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceOpenPublicButton"));
        }

        StackPanel leadDetails = new()
        {
            Spacing = 4,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateBadgeStrip(
                    DesktopHorizonWindowScaffold.CreateMetricBadge("AliceBadgeVariant", "Variant", lead.VariantLabel),
                    DesktopHorizonWindowScaffold.CreateMetricBadge("AliceBadgeProgression", "Progression", lead.ProgressionLabel)),
                CreateDetailText($"Variant: {lead.VariantLabel}"),
                CreateDetailText($"Progression: {lead.ProgressionLabel}"),
                CreateDetailText(lead.NextSafeAction ?? "Reviewed variants stay bounded until you deliberately continue."),
                CreateDetailText(lead.RuntimeCompatibilitySummary ?? "Runtime compatibility remains attached to the governed build handoff.")
            }
        };

        return CreateCard(
            lead.Title,
            lead.Summary,
            leadDetails,
            "AliceLeadHandoffCard",
            CreateButton("Open lead handoff", () => DesktopInstallLinkingRuntime.TryOpenRelativePortal($"/account/alice/{Uri.EscapeDataString(lead.HandoffId)}"), isPrimary: true, name: "AliceOpenLeadHandoffButton"),
            CreateButton("Open account ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceOpenAccountLaneButton"));
    }

    private Control CreateHandoffListCard()
    {
        IReadOnlyList<BuildLabHandoffProjection> handoffs = _campaignSummary?.BuildLabHandoffs
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToArray()
            ?? Array.Empty<BuildLabHandoffProjection>();
        IReadOnlyList<BuildLabHandoffProjection> listedHandoffs = handoffs.Take(6).ToArray();
        IReadOnlyList<string> detailModes = ["Summary", "Follow-through", "Context"];

        StackPanel body = new()
        {
            Spacing = 8
        };

        body.Children.Add(
            DesktopHorizonWindowScaffold.CreateBadgeStrip(
                DesktopHorizonWindowScaffold.CreateMetricBadge("AliceBadgeHandoffs", "Handoffs", handoffs.Count.ToString()),
                DesktopHorizonWindowScaffold.CreateMetricBadge("AliceBadgeContext", "Context", _campaignSummary is null ? "guest" : "account")));

        ComboBox detailModeCombo = new()
        {
            Name = "AliceDetailModeCombo",
            MinWidth = 220,
            ItemsSource = detailModes,
            SelectedIndex = 0
        };

        ListBox handoffList = new()
        {
            Name = "AliceHandoffList",
            MinHeight = 160,
            ItemsSource = listedHandoffs,
            SelectedIndex = listedHandoffs.Count > 0 ? 0 : -1,
            ItemTemplate = new FuncDataTemplate<BuildLabHandoffProjection>((handoff, _) =>
                new TextBlock
                {
                    Text = handoff is null ? string.Empty : $"{handoff.Title} [{handoff.VariantLabel}]",
                    TextWrapping = TextWrapping.Wrap
                })
        };

        TextBlock selectedTitleText = new()
        {
            Name = "AliceSelectedHandoffTitleText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedDetailText = new()
        {
            Name = "AliceSelectedHandoffDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedFollowUpText = new()
        {
            Name = "AliceSelectedHandoffFollowUpText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshSelectedHandoff()
        {
            if (handoffList.SelectedItem is BuildLabHandoffProjection selected)
            {
                _selectedHandoff = selected;
                selectedTitleText.Text = $"{selected.Title} [{selected.ProgressionLabel}]";
                string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
                switch (mode)
                {
                    case "Follow-through":
                        selectedDetailText.Text = selected.NextSafeAction
                            ?? selected.RuntimeCompatibilitySummary
                            ?? "Governed compare detail remains attached to the selected handoff.";
                        selectedFollowUpText.Text = selected.ExchangeParitySummary
                            ?? selected.CrewFitSummary
                            ?? selected.Summary;
                        break;
                    case "Context":
                        selectedDetailText.Text = selected.CampaignReturnSummary
                            ?? selected.SupportClosureSummary
                            ?? selected.Summary;
                        selectedFollowUpText.Text = selected.ConditionalStateSummary
                            ?? selected.SourceHintSummary
                            ?? "Campaign and source posture remain attached to the selected handoff.";
                        break;
                    default:
                        selectedDetailText.Text = selected.PlannerCoverageSummary
                            ?? selected.CampaignReturnSummary
                            ?? selected.ExchangeParitySummary
                            ?? selected.Summary;
                        selectedFollowUpText.Text = selected.NextSafeAction
                            ?? selected.RuntimeCompatibilitySummary
                            ?? "Governed compare detail remains attached to the selected handoff.";
                        break;
                }
            }
            else
            {
                _selectedHandoff = null;
                string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
                selectedTitleText.Text = "No selected handoff";
                switch (mode)
                {
                    case "Follow-through":
                        selectedDetailText.Text = "No governed follow-through lane is currently available.";
                        selectedFollowUpText.Text = "Create or reopen a handoff to inspect bounded next actions.";
                        break;
                    case "Context":
                        selectedDetailText.Text = "No campaign-bound ALICE context is currently available.";
                        selectedFollowUpText.Text = "Reconnect account context to inspect source and campaign posture.";
                        break;
                    default:
                        selectedDetailText.Text = "No governed handoff detail is currently available.";
                        selectedFollowUpText.Text = "Choose a handoff to inspect its bounded follow-through.";
                        break;
                }
            }

            _refreshAssistantContext?.Invoke();
        }

        handoffList.SelectionChanged += (_, _) => RefreshSelectedHandoff();
        detailModeCombo.SelectionChanged += (_, _) => RefreshSelectedHandoff();
        RefreshSelectedHandoff();

        if (handoffs.Count == 0)
        {
            body.Children.Add(CreateDetailText("No account-scoped build handoffs are available yet."));
        }

        if (HasHandoffContext)
        {
            body.Children.Add(detailModeCombo);
            body.Children.Add(handoffList);
            body.Children.Add(
                new Border
                {
                    Name = "AliceSelectedHandoffCard",
                    BorderBrush = new SolidColorBrush(Color.Parse("#D3DCE5")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            selectedTitleText,
                            selectedDetailText,
                            selectedFollowUpText
                        }
                    }
                });
        }

        return CreateCard(
            "Account handoffs",
            handoffs.Count == 0
                ? "Return here after the next governed build compare run."
                : $"{handoffs.Count} governed build handoff(s) are available on the ALICE rail.",
            body,
            "AliceAccountHandoffsCard",
            CreateButton("Open account ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: HasHandoffContext, name: "AliceOpenAccountFromListButton"));
    }

    private Control CreateBuildPathCard()
    {
        IReadOnlyList<string> proposalModes = ["Summary", "Runtime", "Warnings"];
        IReadOnlyList<DesktopBuildPathCandidate> candidates = _buildPathCandidates.Take(6).ToArray();

        StackPanel body = new()
        {
            Spacing = 8
        };

        body.Children.Add(
            DesktopHorizonWindowScaffold.CreateBadgeStrip(
                DesktopHorizonWindowScaffold.CreateMetricBadge("AliceBadgeBuildPaths", "Build paths", _buildPathCandidates.Count.ToString()),
                DesktopHorizonWindowScaffold.CreateMetricBadge("AliceBadgeWorkspaces", "Workspaces", _recentWorkspaces.Count.ToString())));

        ComboBox proposalModeCombo = new()
        {
            Name = "AliceProposalModeCombo",
            MinWidth = 220,
            ItemsSource = proposalModes,
            SelectedIndex = 0
        };

        ComboBox buildPathCombo = new()
        {
            Name = "AliceBuildPathCombo",
            MinWidth = 320,
            ItemsSource = candidates,
            SelectedIndex = candidates.Count > 0 ? 0 : -1,
            ItemTemplate = new FuncDataTemplate<DesktopBuildPathCandidate>((candidate, _) =>
                new TextBlock
                {
                    Text = candidate is null ? string.Empty : $"{candidate.Suggestion.Title} [{candidate.Suggestion.TrustTier}]",
                    TextWrapping = TextWrapping.Wrap
                })
        };

        TextBlock selectedBuildPathTitleText = new()
        {
            Name = "AliceSelectedBuildPathTitleText",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedBuildPathDetailText = new()
        {
            Name = "AliceSelectedBuildPathDetailText",
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock selectedBuildPathWarningsText = new()
        {
            Name = "AliceSelectedBuildPathWarningsText",
            TextWrapping = TextWrapping.Wrap
        };

        void RefreshSelectedBuildPath()
        {
            string mode = proposalModeCombo.SelectedItem?.ToString() ?? "Summary";
            if (buildPathCombo.SelectedItem is DesktopBuildPathCandidate selected)
            {
                _selectedBuildPath = selected;
                selectedBuildPathTitleText.Text = $"{selected.Suggestion.Title} [{selected.Suggestion.Visibility}]";
                DesktopBuildPathPreview? preview = selected.Preview;
                switch (mode)
                {
                    case "Runtime":
                        selectedBuildPathDetailText.Text = preview?.RuntimeCompatibilitySummary
                            ?? preview?.CampaignReturnSummary
                            ?? "Runtime compatibility becomes explicit once a workspace-backed preview is available.";
                        selectedBuildPathWarningsText.Text = preview?.SupportClosureSummary
                            ?? (preview?.RequiresConfirmation == true
                                ? "This build path still requires explicit confirmation before apply."
                                : "Runtime and support closure remain on the bounded default posture.");
                        break;
                    case "Warnings":
                        selectedBuildPathDetailText.Text = preview?.DiagnosticMessages.Count > 0
                            ? string.Join(" | ", preview.DiagnosticMessages)
                            : "No diagnostic warnings are currently attached to this build path.";
                        selectedBuildPathWarningsText.Text = preview?.ChangeSummaries.Count > 0
                            ? string.Join(" | ", preview.ChangeSummaries)
                            : "No change summary is currently attached to this build path.";
                        break;
                    default:
                        selectedBuildPathDetailText.Text = preview?.ChangeSummaries.Count > 0
                            ? string.Join(" | ", preview.ChangeSummaries)
                            : $"Targets: {string.Join(", ", selected.Suggestion.Targets)}";
                        selectedBuildPathWarningsText.Text = preview?.CampaignReturnSummary
                            ?? preview?.RuntimeCompatibilitySummary
                            ?? $"Trust tier {selected.Suggestion.TrustTier} stays visible before any apply-safe follow-through.";
                        break;
                }
            }
            else
            {
                _selectedBuildPath = null;
                selectedBuildPathTitleText.Text = "No selected build path";
                switch (mode)
                {
                    case "Runtime":
                        selectedBuildPathDetailText.Text = "No runtime-backed build path preview is currently available.";
                        selectedBuildPathWarningsText.Text = "Open or create a workspace to attach ALICE proposals to a governed preview lane.";
                        break;
                    case "Warnings":
                        selectedBuildPathDetailText.Text = "No diagnostic lane is currently attached.";
                        selectedBuildPathWarningsText.Text = "Reconnect a workspace-backed preview to inspect build path watchouts.";
                        break;
                    default:
                        selectedBuildPathDetailText.Text = "No governed build path suggestion is currently available.";
                        selectedBuildPathWarningsText.Text = "ALICE will surface proposal previews here once a compatible workspace and ruleset are available.";
                        break;
                }
            }

            _refreshAssistantContext?.Invoke();
        }

        buildPathCombo.SelectionChanged += (_, _) => RefreshSelectedBuildPath();
        proposalModeCombo.SelectionChanged += (_, _) => RefreshSelectedBuildPath();
        RefreshSelectedBuildPath();

        if (_buildPathCandidates.Count == 0)
        {
            body.Children.Add(CreateDetailText("No preview-backed build path suggestions are currently available for the active desktop context."));
        }

        if (HasBuildPathContext)
        {
            body.Children.Add(proposalModeCombo);
            body.Children.Add(buildPathCombo);
            body.Children.Add(
                new Border
                {
                    Name = "AliceSelectedBuildPathCard",
                    BorderBrush = new SolidColorBrush(Color.Parse("#D3DCE5")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            selectedBuildPathTitleText,
                            selectedBuildPathDetailText,
                            selectedBuildPathWarningsText
                        }
                    }
                });
        }

        return CreateCard(
            "Proposal studio",
            _buildPathCandidates.Count == 0
                ? "Build path compare stays native, but the current desktop context has no preview-backed starter proposals yet."
                : $"{_buildPathCandidates.Count} preview-backed build path candidate(s) are available on native ALICE rails.",
            body,
            "AliceBuildPathCard",
            CreateButton("Open account ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: HasBuildPathContext, name: "AliceOpenAccountFromBuildPathsButton"),
            CreateButton("Open public ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceOpenPublicFromBuildPathsButton"));
    }

    private static async Task<IReadOnlyList<WorkspaceListItem>> ReadWorkspacesAsync(IChummerClient client)
    {
        try
        {
            return await client.ListWorkspacesAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<WorkspaceListItem>();
        }
    }

    private static string? ResolveRulesetId(IReadOnlyList<WorkspaceListItem> workspaces)
        => RulesetDefaults.NormalizeOptional(workspaces.FirstOrDefault()?.RulesetId);

    private static async Task<IReadOnlyList<DesktopBuildPathCandidate>> ReadBuildPathCandidatesAsync(
        IChummerClient client,
        string? rulesetId,
        IReadOnlyList<WorkspaceListItem> workspaces,
        IReadOnlyList<DesktopBuildPathSuggestion> suggestions)
    {
        DesktopBuildPathSuggestion[] selectedSuggestions = suggestions
            .OrderByDescending(static suggestion => suggestion.BuildKitId.Contains("starter", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(static suggestion => string.Equals(suggestion.TrustTier, ArtifactTrustTiers.Curated, StringComparison.OrdinalIgnoreCase))
            .ThenBy(static suggestion => suggestion.Title, StringComparer.Ordinal)
            .Take(4)
            .ToArray();

        if (selectedSuggestions.Length == 0)
        {
            return Array.Empty<DesktopBuildPathCandidate>();
        }

        if (workspaces.Count == 0)
        {
            return selectedSuggestions
                .Select(static suggestion => new DesktopBuildPathCandidate(suggestion, Preview: null))
                .ToArray();
        }

        CharacterWorkspaceId workspaceId = workspaces[0].Id;
        Task<DesktopBuildPathCandidate>[] tasks = selectedSuggestions
            .Select(async suggestion =>
            {
                DesktopBuildPathPreview? preview;
                try
                {
                    preview = await client.GetBuildPathPreviewAsync(
                        suggestion.BuildKitId,
                        workspaceId,
                        rulesetId,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    preview = null;
                }

                return new DesktopBuildPathCandidate(suggestion, preview);
            })
            .ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static TextBlock CreateDetailText(string text)
        => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };

    private static Border CreateCard(string title, string summary, Control? leadControl, params Button[] actions)
        => CreateCard(title, summary, leadControl, null, actions);

    private static Border CreateCard(string title, string summary, Control? leadControl, string? name, params Button[] actions)
    {
        StackPanel stack = new()
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = summary,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        if (leadControl is not null)
        {
            stack.Children.Add(leadControl);
        }

        WrapPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            ItemHeight = double.NaN,
            ItemWidth = double.NaN
        };

        foreach (Button action in actions)
        {
            action.Margin = new Thickness(0, 0, 8, 8);
            actionRow.Children.Add(action);
        }

        stack.Children.Add(actionRow);

        return new Border
        {
            Name = name,
            BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#F2F5FA"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = stack
        };
    }

    private static Button CreateStaticButton(string label, Func<bool> action, bool isPrimary = false, string? name = null)
    {
        Button button = new()
        {
            Name = name,
            Content = label,
            MinWidth = 132,
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (isPrimary)
        {
            DesktopShellTheme.ApplyPrimaryButton(button);
        }

        button.Click += (_, _) => action();
        return button;
    }

    private Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false, string? name = null)
        => CreateButton(
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow,
            isPrimary,
            name);

    private Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false, string? name = null)
    {
        Button button = new()
        {
            Name = name,
            Content = label,
            MinWidth = 132,
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        if (isPrimary)
        {
            DesktopShellTheme.ApplyPrimaryButton(button);
        }

        button.Click += async (_, _) =>
        {
            await action().ConfigureAwait(true);
            if (closeWindow)
            {
                Close();
            }
        };

        return button;
    }

    private async Task<AiConversationTurnResponse?> TryAskAssistantAsync(string mode, string message)
    {
        if (_coachSidecarClient is null)
        {
            return null;
        }

        string routeType = string.Equals(mode, "Rules coach", StringComparison.Ordinal)
            ? AiRouteTypes.Coach
            : AiRouteTypes.Build;
        string conversationId = routeType == AiRouteTypes.Coach ? _coachConversationId : _buildConversationId;
        string effectiveMessage = BuildSeededAssistantMessage(mode, message);
        AiConversationTurnRequest request = new(
            Message: effectiveMessage,
            ConversationId: conversationId,
            RuntimeFingerprint: ResolveAssistantRuntimeFingerprint(),
            CharacterId: _workspaceId,
            WorkspaceId: _workspaceId);

        AvaloniaCoachSidecarCallResult<AiConversationTurnResponse> result = routeType == AiRouteTypes.Coach
            ? await _coachSidecarClient.SendCoachTurnAsync(request, CancellationToken.None).ConfigureAwait(true)
            : await _coachSidecarClient.SendBuildTurnAsync(request, CancellationToken.None).ConfigureAwait(true);
        return result.IsSuccess ? result.Payload : null;
    }

    private string BuildIdleAssistantStatus(string? mode)
        => mode switch
        {
            "Rules coach" => $"Ask a rules question for {_rulesetId ?? "the active ruleset"}.",
            "Origin draft" => "Generate an optional origin story draft grounded on the current build, handoff, and workspace context.",
            _ => HasBuildPathContext
                ? "Ask for the next grounded build move, and ALICE will stay on preview-safe rails."
                : "Ask about the next build move; ALICE will answer from the current desktop context."
        };

    private string BuildIdleAssistantAnswer(string? mode)
        => mode switch
        {
            "Rules coach" => "Try: “Explain the safe next step for an SR4 troll decker after metatype and core priorities.”",
            "Origin draft" => "Try: “Give this runner a grounded origin that explains the current metatype, build path, and tradeoffs.”",
            _ => "Try: “What should I add next for this build, and why?”"
        };

    private string[] BuildIdleEvidence(string? mode)
    {
        List<string> lines =
        [
            !string.IsNullOrWhiteSpace(_rulesetId) ? $"Ruleset: {_rulesetId}" : "Ruleset context is not pinned yet.",
            !string.IsNullOrWhiteSpace(_workspaceId) ? $"Workspace: {_workspaceId}" : "No workspace-backed context is attached yet."
        ];

        if (string.Equals(mode, "Origin draft", StringComparison.Ordinal))
        {
            lines.Add(_originDraft is null
                ? "No origin draft has been generated yet."
                : "A prior origin draft is available and can seed later ALICE suggestions.");
        }
        else if (string.Equals(mode, "Rules coach", StringComparison.Ordinal))
        {
            lines.Add(HasHandoffContext
                ? "Account build handoffs are available for grounded follow-through."
                : "No account handoff is available; ALICE will stay on bounded local guidance.");
        }
        else
        {
            lines.Add(HasBuildPathContext
                ? $"Build paths: {_buildPathCandidates.Count}"
                : "No preview-backed build path is available yet.");
        }

        return lines.ToArray();
    }

    private string BuildLocalFallbackAnswer(string mode, string message)
    {
        if (string.Equals(mode, "Origin draft", StringComparison.Ordinal))
        {
            CharacterNarrativePacket packet = BuildNarrativePacket(message);
            return BuildOriginDraft(packet).Prose;
        }

        if (string.Equals(mode, "Rules coach", StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(_rulesetId)
                ? $"ALICE could not reach the grounded coach route, so it stayed local. This head is on {_rulesetId}. Use the current ruleset and workspace surface to verify '{message}', then reopen ALICE after the AI coach route is available."
                : $"ALICE could not reach the grounded coach route, and no ruleset is pinned yet. Open or create a workspace first, then ask '{message}' again.";
        }

        if (_buildPathCandidates.Count > 0)
        {
            DesktopBuildPathCandidate lead = _buildPathCandidates[0];
            return $"ALICE could not reach the grounded build route, so it stayed local. The strongest visible candidate is '{lead.Suggestion.Title}'. {lead.Preview?.CampaignReturnSummary ?? lead.Preview?.RuntimeCompatibilitySummary ?? "Open the proposal studio card below for the current bounded preview."}";
        }

        return "ALICE could not reach the grounded build route, and there is no preview-backed build candidate yet. Open a workspace or reconnect account context first.";
    }

    private string[] BuildLocalFallbackEvidence(string mode)
    {
        if (string.Equals(mode, "Origin draft", StringComparison.Ordinal))
        {
            CharacterNarrativePacket packet = BuildNarrativePacket("fallback");
            return BuildOriginEvidence(packet, BuildOriginDraft(packet));
        }

        if (string.Equals(mode, "Rules coach", StringComparison.Ordinal))
        {
            return BuildIdleEvidence(mode);
        }

        return _buildPathCandidates.Count > 0
            ? _buildPathCandidates
                .Take(3)
                .Select(candidate => $"{candidate.Suggestion.Title} · {candidate.Preview?.RuntimeCompatibilitySummary ?? candidate.Suggestion.TrustTier}")
                .ToArray()
            : BuildIdleEvidence(mode);
    }

    private AliceAssistantContextProjection BuildAssistantContextProjection(string? mode)
    {
        WorkspaceListItem? workspace = _recentWorkspaces.FirstOrDefault();
        if (string.Equals(mode, "Origin draft", StringComparison.Ordinal))
        {
            string? alias = workspace?.Summary.Alias;
            string? metatype = workspace?.Summary.Metatype;
            string? buildMethod = workspace?.Summary.BuildMethod;
            string title = !string.IsNullOrWhiteSpace(alias) ? $"{alias} origin context" : "Origin context";
            string summary = !string.IsNullOrWhiteSpace(metatype)
                ? $"{metatype} · {buildMethod ?? "build"}"
                : "No explicit runner identity is available yet.";
            string detail = _selectedBuildPath?.Suggestion.Title is { Length: > 0 } buildTitle
                ? $"Lead build path: {buildTitle}. {_selectedHandoff?.NextSafeAction ?? _selectedHandoff?.Summary ?? "No account handoff summary is attached yet."}"
                : _selectedHandoff?.Summary ?? "The draft stays bounded to the current ruleset, workspace shell, and any visible ALICE handoff.";
            return new AliceAssistantContextProjection(title, summary, detail);
        }

        if (string.Equals(mode, "Rules coach", StringComparison.Ordinal))
        {
            return new AliceAssistantContextProjection(
                "Rules coach context",
                !string.IsNullOrWhiteSpace(_rulesetId) ? $"Pinned to {_rulesetId}" : "No explicit ruleset pin is available yet.",
                _originDraft is null
                    ? "ALICE answers from ruleset, workspace, and handoff context only."
                    : "A generated origin draft is available and can inform later ALICE guidance without changing build truth.");
        }

        return new AliceAssistantContextProjection(
            "Build continuity",
            _selectedBuildPath?.Suggestion.Title ?? "No selected build path",
            _selectedHandoff?.Summary
                ?? _selectedBuildPath?.Preview?.RuntimeCompatibilitySummary
                ?? "ALICE stays bounded to the current workspace and preview-safe build path lane.");
    }

    private CharacterNarrativePacket BuildNarrativePacket(string prompt)
    {
        WorkspaceListItem? workspace = _recentWorkspaces.FirstOrDefault();
        string alias = FirstNonEmpty(workspace?.Summary.Alias, workspace?.Summary.Name, "Unnamed runner");
        string metatype = FirstNonEmpty(workspace?.Summary.Metatype, "Unknown metatype");
        string buildMethod = FirstNonEmpty(workspace?.Summary.BuildMethod, "Unspecified build");
        string archetypeHint = _selectedBuildPath?.Suggestion.Title
            ?? _selectedHandoff?.Title
            ?? "Unclassified shadow asset";
        string[] causalityHints = new string?[]
        {
            _selectedHandoff?.Summary,
            _selectedHandoff?.NextSafeAction,
            _selectedBuildPath?.Preview?.CampaignReturnSummary,
            _selectedBuildPath?.Preview?.RuntimeCompatibilitySummary
        }
        .Where(static line => !string.IsNullOrWhiteSpace(line))
        .Take(4)
        .Cast<string>()
        .ToArray();

        string[] standoutSignals = new string?[]
        {
            $"Ruleset {_rulesetId ?? workspace?.RulesetId ?? "unknown"}",
            !string.IsNullOrWhiteSpace(workspace?.Summary.Karma.ToString()) ? $"Karma {workspace!.Summary.Karma:0}" : null,
            !string.IsNullOrWhiteSpace(workspace?.Summary.Nuyen.ToString()) ? $"Nuyen {workspace!.Summary.Nuyen:0}" : null,
            _selectedBuildPath?.Suggestion.TrustTier,
            _selectedBuildPath?.Suggestion.Visibility
        }
        .Where(static line => !string.IsNullOrWhiteSpace(line))
        .Take(5)
        .Cast<string>()
        .ToArray();

        string[] contradictionFlags = new string?[]
        {
            _selectedBuildPath?.Preview?.RequiresConfirmation == true ? "This path still requires explicit confirmation before apply." : null,
            _selectedBuildPath?.Preview?.DiagnosticMessages.Count > 0 ? string.Join(" | ", _selectedBuildPath.Preview.DiagnosticMessages.Take(2)) : null,
            _selectedHandoff?.Watchouts?.Count > 0 ? string.Join(" | ", _selectedHandoff.Watchouts.Take(2)) : null
        }
        .Where(static line => !string.IsNullOrWhiteSpace(line))
        .Take(3)
        .Cast<string>()
        .ToArray();

        return new CharacterNarrativePacket(
            Alias: alias,
            Metatype: metatype,
            BuildMethod: buildMethod,
            RulesetId: _rulesetId ?? workspace?.RulesetId ?? "unknown",
            ArchetypeHint: archetypeHint,
            Prompt: prompt,
            WorkspaceName: workspace?.Summary.Name,
            LeadBuildPathTitle: _selectedBuildPath?.Suggestion.Title,
            LeadHandoffTitle: _selectedHandoff?.Title,
            CausalityHints: causalityHints,
            StandoutSignals: standoutSignals,
            ContradictionFlags: contradictionFlags);
    }

    private static CharacterNarrativeDraft BuildOriginDraft(CharacterNarrativePacket packet)
    {
        string sentenceOne = $"{packet.Alias} reads like a {packet.Metatype.ToLowerInvariant()} operator shaped by {packet.BuildMethod.ToLowerInvariant()} pressure rather than a clean, academic career path.";
        string sentenceTwo = !string.IsNullOrWhiteSpace(packet.LeadBuildPathTitle)
            ? $"The strongest visible throughline is '{packet.LeadBuildPathTitle}', which suggests a runner who kept adapting around a specific survival plan instead of collecting random upgrades."
            : $"The current build signals a runner assembled around practical survival choices rather than ornamental flavor.";
        string sentenceThree = packet.CausalityHints.Count > 0
            ? $"That history fits the current build because {packet.CausalityHints[0].TrimEnd('.')}. Any later upgrades or qualities should feel like consequences of that same path, not disconnected add-ons."
            : $"The safest origin draft is a bounded one: each quality, augmentation, or build choice should read like the consequence of one hard life track, not unrelated cool ideas.";

        string summary = $"{packet.Alias} exists at the intersection of {packet.RulesetId}, {packet.Metatype}, and a build path that rewards focused tradeoffs.";
        string[] gmHooks = new string?[]
        {
            !string.IsNullOrWhiteSpace(packet.LeadHandoffTitle) ? $"Use '{packet.LeadHandoffTitle}' as the event that pushed the runner into the current loadout." : null,
            packet.CausalityHints.Count > 1 ? $"Follow-up hook: {packet.CausalityHints[1]}" : null,
            packet.ContradictionFlags.Count > 0 ? $"Tension: {packet.ContradictionFlags[0]}" : null
        }
        .Where(static line => !string.IsNullOrWhiteSpace(line))
        .Take(3)
        .Cast<string>()
        .ToArray();

        return new CharacterNarrativeDraft(
            Summary: summary,
            Prose: string.Join(" ", [sentenceOne, sentenceTwo, sentenceThree]),
            GmHooks: gmHooks);
    }

    private static string[] BuildOriginEvidence(CharacterNarrativePacket packet, CharacterNarrativeDraft draft)
    {
        List<string> lines =
        [
            $"Runner: {packet.Alias} · {packet.Metatype}",
            $"Ruleset: {packet.RulesetId} · Build: {packet.BuildMethod}",
            $"Archetype hint: {packet.ArchetypeHint}"
        ];

        foreach (string signal in packet.StandoutSignals.Take(3))
        {
            lines.Add($"Signal: {signal}");
        }

        foreach (string hint in packet.CausalityHints.Take(2))
        {
            lines.Add($"Cause: {hint}");
        }

        foreach (string hook in draft.GmHooks.Take(2))
        {
            lines.Add($"Hook: {hook}");
        }

        foreach (string contradiction in packet.ContradictionFlags.Take(2))
        {
            lines.Add($"Tension: {contradiction}");
        }

        return lines.ToArray();
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private string BuildSeededAssistantMessage(string mode, string message)
    {
        if (_originDraft is null || string.Equals(mode, "Origin draft", StringComparison.Ordinal))
        {
            return message;
        }

        return $"Origin seed summary: {_originDraft.Summary}{Environment.NewLine}Origin seed prose: {_originDraft.Prose}{Environment.NewLine}User request: {message}";
    }

    private string? ResolveAssistantRuntimeFingerprint()
        => _selectedBuildPath?.Preview?.RuntimeFingerprint
            ?? _originDraft?.RuntimeFingerprint
            ?? _selectedHandoff?.RuleEnvironmentDiff?.AfterFingerprint
            ?? _selectedHandoff?.RuleEnvironmentDiff?.BeforeFingerprint;

    private static string BuildStatusLine(AiConversationTurnResponse response)
    {
        string confidence = response.StructuredAnswer?.Confidence ?? AiConfidenceLevels.Scaffolded;
        string provider = response.RouteDecision.ProviderId;
        string routeReason = response.RouteDecision.Reason;
        return $"{provider} · {confidence} · {routeReason}";
    }

    private static string[] BuildEvidenceLines(AiConversationTurnResponse response)
    {
        List<string> lines = [];

        if (!string.IsNullOrWhiteSpace(response.FlavorLine))
        {
            lines.Add(response.FlavorLine);
        }

        if (response.StructuredAnswer is { } structuredAnswer)
        {
            foreach (AiRecommendation recommendation in structuredAnswer.Recommendations.Take(3))
            {
                lines.Add($"Recommend: {recommendation.Title} · {recommendation.Reason}");
            }

            foreach (AiEvidenceEntry evidence in structuredAnswer.Evidence.Take(3))
            {
                lines.Add($"Evidence: {evidence.Title} · {evidence.Summary}");
            }

            foreach (AiRiskEntry risk in structuredAnswer.Risks.Take(2))
            {
                lines.Add($"Risk: {risk.Title} · {risk.Summary}");
            }
        }

        foreach (AiCitation citation in response.Citations.Take(3))
        {
            lines.Add($"Source: {citation.Title} · {citation.ReferenceId}{(string.IsNullOrWhiteSpace(citation.Source) ? string.Empty : $" · {citation.Source}")}");
        }

        foreach (AiToolInvocation tool in response.ToolInvocations.Take(2))
        {
            lines.Add($"Tool: {tool.ToolId} · {tool.Status} · {tool.Summary}");
        }

        return lines.Count == 0
            ? ["ALICE returned an answer without extra grounded detail lines."]
            : lines.ToArray();
    }

    private Button[] CreateSuggestedActionButtons(AiConversationTurnResponse response)
    {
        List<Button> buttons = [];

        foreach (AiSuggestedAction action in response.SuggestedActions.Take(3))
        {
            bool primary = buttons.Count == 0;
            string label = action.Title;
            buttons.Add(CreateButton(label, static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: primary, name: $"AliceSuggestedAction_{action.ActionId}"));
        }

        if (buttons.Count == 0)
        {
            buttons.Add(CreateButton("Open account ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceSuggestedActionFallbackAccount"));
            buttons.Add(CreateButton("Open public ALICE", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceSuggestedActionFallbackPublic"));
        }

        return buttons.ToArray();
    }

    private static Control BuildConversationTurnView(AliceConversationTurnEntry? entry)
    {
        if (entry is null)
        {
            return new TextBlock { Text = string.Empty };
        }

        StackPanel stack = new()
        {
            Spacing = 6
        };

        stack.Children.Add(
            new TextBlock
            {
                Text = $"{entry.RoleLabel} · {entry.Title}",
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });

        stack.Children.Add(
            new TextBlock
            {
                Text = entry.Body,
                TextWrapping = TextWrapping.Wrap
            });

        if (entry.EvidenceLines.Count > 0)
        {
            stack.Children.Add(
                new TextBlock
                {
                    Text = string.Join(Environment.NewLine, entry.EvidenceLines.Select(static line => $"• {line}")),
                    Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellTextMutedBrush", "#475569"),
                    TextWrapping = TextWrapping.Wrap
                });
        }

        if (entry.SuggestedActionTitles.Count > 0)
        {
            stack.Children.Add(
                new TextBlock
                {
                    Text = "Next: " + string.Join(" · ", entry.SuggestedActionTitles),
                    Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155"),
                    TextWrapping = TextWrapping.Wrap
                });
        }

        return new Border
        {
            Name = $"AliceConversationTurn_{entry.Kind}",
            BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveThemeBrush(
                entry.Kind == AliceConversationTurnKind.User ? "ChummerShellSelectionInsetBrush" : "ChummerShellSurfaceAltBrush",
                entry.Kind == AliceConversationTurnKind.User ? "#F1F5F9" : "#F2F5FA"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = stack
        };
    }

    private static AliceConversationTurnEntry BuildWelcomeEntry(string? mode)
        => mode switch
        {
            "Rules coach" => new AliceConversationTurnEntry(
                AliceConversationTurnKind.Assistant,
                "ALICE",
                "Rules coach ready",
                "Ask for a rule explanation, tradeoff, or safe next build step tied to the active ruleset.",
                [],
                BuildStarterPrompts(mode)),
            "Origin draft" => new AliceConversationTurnEntry(
                AliceConversationTurnKind.Assistant,
                "ALICE",
                "Origin studio ready",
                "Generate an optional origin that explains why this build exists without changing any build truth.",
                [],
                BuildStarterPrompts(mode)),
            _ => new AliceConversationTurnEntry(
                AliceConversationTurnKind.Assistant,
                "ALICE",
                "Build copilot ready",
                "Ask for the next grounded move, a comparison, or a bounded build recommendation from the current desktop context.",
                [],
                BuildStarterPrompts(mode))
        };

    private static AliceConversationTurnEntry BuildUserTurn(string message)
        => new(
            AliceConversationTurnKind.User,
            "You",
            "Question",
            message,
            [],
            []);

    private static AliceConversationTurnEntry BuildAssistantTurn(
        string mode,
        string status,
        string body,
        IReadOnlyList<string> evidenceLines,
        IReadOnlyList<string> suggestedActionTitles)
        => new(
            AliceConversationTurnKind.Assistant,
            "ALICE",
            mode switch
            {
                "Rules coach" => "Rules coach",
                "Origin draft" => "Origin draft",
                _ => "Build help"
            },
            body,
            [status, .. evidenceLines.Take(5)],
            suggestedActionTitles.Take(3).ToArray());

    private static string[] BuildStarterPrompts(string? mode)
        => mode switch
        {
            "Rules coach" =>
            [
                "Explain the next safe SR4 build step.",
                "Why would I take this quality?",
                "What rule am I most likely to miss here?"
            ],
            "Origin draft" =>
            [
                "Generate a grounded origin for this runner.",
                "Explain how this build could have happened.",
                "Focus on why the qualities and upgrades fit together."
            ],
            _ =>
            [
                "Build me an SR4 troll decker.",
                "What should I add next, and why?",
                "Compare two good next-step options."
            ]
        };

    private static string SanitizeNameToken(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        int written = 0;
        foreach (char ch in value)
        {
            buffer[written++] = char.IsLetterOrDigit(ch) ? ch : '_';
        }

        return new string(buffer[..written]);
    }

    private enum AliceConversationTurnKind
    {
        User,
        Assistant
    }

    private sealed record AliceConversationTurnEntry(
        AliceConversationTurnKind Kind,
        string RoleLabel,
        string Title,
        string Body,
        IReadOnlyList<string> EvidenceLines,
        IReadOnlyList<string> SuggestedActionTitles);

    private sealed record AliceAssistantContextProjection(
        string Title,
        string Summary,
        string Detail);

    private sealed record CharacterNarrativePacket(
        string Alias,
        string Metatype,
        string BuildMethod,
        string RulesetId,
        string ArchetypeHint,
        string Prompt,
        string? WorkspaceName,
        string? LeadBuildPathTitle,
        string? LeadHandoffTitle,
        IReadOnlyList<string> CausalityHints,
        IReadOnlyList<string> StandoutSignals,
        IReadOnlyList<string> ContradictionFlags);

    private sealed record CharacterNarrativeDraft(
        string Summary,
        string Prose,
        IReadOnlyList<string> GmHooks,
        string? RuntimeFingerprint = null);
}
