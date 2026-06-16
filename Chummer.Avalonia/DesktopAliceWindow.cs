using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
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
    private bool HasHandoffContext => (_campaignSummary?.BuildLabHandoffs.Count ?? 0) > 0;
    private bool HasBuildPathContext => _buildPathCandidates.Count > 0;

    private DesktopAliceWindow(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        IReadOnlyList<DesktopBuildPathCandidate> buildPathCandidates)
    {
        _campaignSummary = campaignSummary;
        _recentWorkspaces = recentWorkspaces;
        _buildPathCandidates = buildPathCandidates;

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
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop ALICE requires an IChummerClient instance."));
            summary = await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(true);
            workspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
            string? effectiveRulesetId = ResolveRulesetId(workspaces);
            IReadOnlyList<DesktopBuildPathSuggestion> suggestions = await client.GetBuildPathSuggestionsAsync(effectiveRulesetId, CancellationToken.None).ConfigureAwait(true);
            buildPathCandidates = await ReadBuildPathCandidatesAsync(client, effectiveRulesetId, workspaces, suggestions).ConfigureAwait(true);
        }
        catch
        {
            summary = null;
            workspaces = Array.Empty<WorkspaceListItem>();
            buildPathCandidates = Array.Empty<DesktopBuildPathCandidate>();
        }

        return new DesktopAliceWindow(summary, workspaces, buildPathCandidates);
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
            BorderBrush = new SolidColorBrush(Color.Parse("#BBC7D4")),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.Parse("#F7FAFD")),
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
            button.Background = new SolidColorBrush(Color.Parse("#24527A"));
            button.Foreground = Brushes.White;
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
            button.Background = new SolidColorBrush(Color.Parse("#24527A"));
            button.Foreground = Brushes.White;
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
}
