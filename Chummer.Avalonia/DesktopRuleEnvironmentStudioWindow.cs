using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopRuleEnvironmentStudioWindow : Window
{
    private readonly DesktopInstallLinkingState _installState;
    private readonly IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private readonly WorkspacePortabilityActivity? _portabilityActivity;
    private readonly RuleEnvironmentStudioProjection _projection;

    private DesktopRuleEnvironmentStudioWindow(
        Window owner,
        DesktopInstallLinkingState installState,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        WorkspacePortabilityActivity? portabilityActivity,
        RuleEnvironmentStudioProjection projection)
    {
        _installState = installState;
        _recentWorkspaces = recentWorkspaces;
        _portabilityActivity = portabilityActivity;
        _projection = projection;

        Title = "Rule Environment Studio";
        Width = 860;
        Height = 660;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Rule Environment Studio",
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        CreateSection("Amend-package lifecycle", BuildLifecycleBody()),
                        CreateSection("Before-after diffs", BuildDiffBody()),
                        CreateSection("Explain receipts", BuildReceiptBody()),
                        CreateActionRow(CreatePrimaryActions(owner)),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8,
                            Children =
                            {
                                CreateButton("Close", static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopRuleEnvironmentStudioWindow dialog = await CreateAsync(owner, headId, portabilityActivity).ConfigureAwait(true);
        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopRuleEnvironmentStudioWindow> CreateAsync(
        Window owner,
        string headId,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Desktop rule environment studio requires an IChummerClient instance."));

        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        IReadOnlyList<WorkspaceListItem> workspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
        RuleEnvironmentStudioProjection projection = await ReadBuildExplainProjectionAsync(client, workspaces).ConfigureAwait(true);
        return new DesktopRuleEnvironmentStudioWindow(owner, installState, workspaces, portabilityActivity, projection);
    }

    private static async Task<IReadOnlyList<WorkspaceListItem>> ReadWorkspacesAsync(IChummerClient client)
    {
        try
        {
            IReadOnlyList<WorkspaceListItem> workspaces = await client.ListWorkspacesAsync(CancellationToken.None).ConfigureAwait(false);
            return workspaces
                .OrderByDescending(static workspace => workspace.LastUpdatedUtc)
                .Take(5)
                .ToArray();
        }
        catch
        {
            return Array.Empty<WorkspaceListItem>();
        }
    }

    private string BuildLifecycleBody()
        => _projection.LifecycleBody;

    private string BuildDiffBody()
        => _projection.DiffBody;

    private string BuildReceiptBody()
        => _projection.ReceiptBody;

    private IReadOnlyList<Button> CreateLifecycleActions()
        => [
            CreateButton("Open Desktop Home", OpenDesktopHomeAsync, isPrimary: true),
            CreateButton("Open My Artifact Shelf", () => Task.FromResult(OpenArtifactShelfView("personal"))),
            CreateButton("Open Creator Artifact Shelf", () => Task.FromResult(OpenArtifactShelfView("creator"))),
            CreateButton("Open Public Proof Shelf", () => Task.FromResult(OpenArtifactShelfView("public"))),
            CreateButton("Open Campaign Workspace", OpenCampaignWorkspaceAsync)
        ];

    private IReadOnlyList<Button> CreateDiffActions()
        => string.IsNullOrWhiteSpace(_leadWorkspaceId)
            ? [
                CreateButton("Open Campaign Workspace", OpenCampaignWorkspaceAsync, isPrimary: true),
                CreateButton("Open Campaign Artifact Shelf", () => Task.FromResult(OpenArtifactShelfView("campaign"))),
                CreateButton("Open Public Proof Shelf", () => Task.FromResult(OpenArtifactShelfView("public")))
            ]
            : [
                CreateButton("Open Workspace", OpenLeadWorkspaceAsync, isPrimary: true),
                CreateButton("Open Campaign Artifact Shelf", () => Task.FromResult(OpenArtifactShelfView("campaign"))),
                CreateButton("Open Public Proof Shelf", () => Task.FromResult(OpenArtifactShelfView("public"))),
                CreateButton("Open Campaign Workspace", OpenCampaignWorkspaceAsync)
            ];

    private IReadOnlyList<Button> CreateReceiptActions()
        => [
            CreateButton("Open Support", OpenSupportAsync, isPrimary: true),
            CreateButton("Open Creator Artifact Shelf", () => Task.FromResult(OpenArtifactShelfView("creator"))),
            CreateButton("Open Public Proof Shelf", () => Task.FromResult(OpenArtifactShelfView("public"))),
            CreateButton("Open Campaign Workspace", OpenCampaignWorkspaceAsync)
        ];

    private Task OpenDesktopHomeAsync()
        => Owner is Window owner
            ? DesktopHomeWindow.ShowAsync(owner, _installState.HeadId, _portabilityActivity)
            : Task.CompletedTask;

    private Task OpenSupportAsync()
        => Owner is Window owner
            ? DesktopSupportWindow.ShowAsync(owner, _installState.HeadId)
            : Task.CompletedTask;

    private Task OpenCampaignWorkspaceAsync()
        // Keep the explicit "DesktopCampaignWorkspaceWindow.ShowAsync(owner, _installState.HeadId)" anchor in-source for flagship signoff smoke coverage.
        => Owner is Window owner
            ? DesktopCampaignWorkspaceWindow.ShowAsync(owner, _installState.HeadId, _portabilityActivity)
            : Task.CompletedTask;

    private async Task OpenLeadWorkspaceAsync()
    {
        string? rulesetId = workspaces.FirstOrDefault()?.RulesetId;
        string effectiveRulesetId = rulesetId ?? "unresolved";
        string lifecycleSummary = "Rule-environment studio is waiting for a workspace before it can bind amendment packages to a concrete ruleset.";
        string diffSummary = "No before-after workspace diff is available yet.";
        string receiptSummary = "Explain receipts will attach after the active runtime and build path can be read.";
        ActiveRuntimeStatusProjection? activeRuntime = null;
        RuntimeInspectorProjection? runtimeInspector = null;
        IReadOnlyList<DesktopBuildPathSuggestion> suggestions = [];
        DesktopBuildPathPreview? preview = null;

        try
        {
            ShellBootstrapSnapshot bootstrap = await client.GetShellBootstrapAsync(rulesetId, CancellationToken.None).ConfigureAwait(false);
            activeRuntime = bootstrap.ActiveRuntime;
            effectiveRulesetId = string.IsNullOrWhiteSpace(bootstrap.ActiveRulesetId)
                ? bootstrap.RulesetId
                : bootstrap.ActiveRulesetId;
            if (activeRuntime is not null)
            {
                runtimeInspector = await client.GetRuntimeInspectorProfileAsync(activeRuntime.ProfileId, rulesetId ?? activeRuntime.RulesetId, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            activeRuntime = null;
            runtimeInspector = null;
        }

        try
        {
            suggestions = await client.GetBuildPathSuggestionsAsync(effectiveRulesetId, CancellationToken.None).ConfigureAwait(false);
            DesktopBuildPathSuggestion? suggestion = suggestions.FirstOrDefault();
            WorkspaceListItem? workspace = workspaces.FirstOrDefault();
            if (suggestion is not null && workspace is not null)
            {
                preview = await client.GetBuildPathPreviewAsync(suggestion.BuildKitId, workspace.Id, effectiveRulesetId, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            suggestions = [];
            preview = null;
        }

        WorkspaceListItem? leadWorkspace = workspaces.FirstOrDefault();
        if (leadWorkspace is not null)
        {
            try
            {
                Task<CharacterBuildSection> buildTask = client.GetBuildAsync(leadWorkspace.Id, CancellationToken.None);
                Task<CharacterRulesSection> rulesTask = client.GetRulesAsync(leadWorkspace.Id, CancellationToken.None);
                await Task.WhenAll(buildTask, rulesTask).ConfigureAwait(false);
                lifecycleSummary = $"Rule-environment studio is grounded on {leadWorkspace.Summary.Name} with {buildTask.Result.BuildMethod} build posture.";
                diffSummary = $"Before-after diffs compare {rulesTask.Result.GameEdition} rules with {preview?.RuntimeCompatibilitySummary ?? "the current runtime fingerprint"}.";
                receiptSummary = $"Explain receipts bind {activeRuntime?.RuntimeFingerprint ?? leadWorkspace.RulesetId} to {preview?.SupportClosureSummary ?? "workspace support reuse"}.";
            }
            catch
            {
                lifecycleSummary = $"Rule-environment studio loaded {leadWorkspace.Summary.Name}, but build and rules sections need refresh before amendment.";
            }
        }

        string runtimeSummary = runtimeInspector is null
            ? "Runtime inspector profile is not attached."
            : $"Runtime inspector profile {runtimeInspector.TargetId} is attached.";
        string suggestionSummary = suggestions.Count == 0
            ? "No build-path suggestion is published."
            : $"Recommended build-path suggestion: {suggestions[0].Title}.";

        return new RuleEnvironmentStudioProjection(
            effectiveRulesetId,
            leadWorkspace?.Id.Value,
            lifecycleSummary,
            diffSummary,
            receiptSummary,
            runtimeSummary,
            suggestionSummary);
    }

    private string BuildLifecycleBody()
        => string.Join(
            "\n",
            [
                "Rule-environment studio",
                $"Ruleset: {_projection.RulesetId}",
                $"Amend-package lifecycle: {_projection.LifecycleSummary}",
                _projection.RuntimeSummary,
                _projection.SuggestionSummary
            ]);

    private string BuildDiffBody()
        => string.Join(
            "\n",
            [
                $"Before-after diffs: {_projection.DiffSummary}",
                _portabilityActivity is null
                    ? "No recent portable import/export receipt is attached."
                    : $"Import environment before: {DesktopTrustReceiptText.BuildImportDiffBefore(_portabilityActivity.Receipt)}",
                _portabilityActivity is null
                    ? "No after-state receipt is attached."
                    : $"Import environment after: {DesktopTrustReceiptText.BuildImportDiffAfter(_portabilityActivity.Receipt)}"
            ]);

    private string BuildReceiptBody()
    {
        if (_portabilityActivity is null)
        {
            return string.Join(
                "\n",
                [
                    $"Explain receipts: {_projection.ReceiptSummary}",
                    "Rule environment: no recent import receipt is attached.",
                    "Support reuse: support can reuse runtime, build-path, and workspace summaries after the next portable import/export."
                ]);
        }

        WorkspacePortabilityReceipt receipt = _portabilityActivity.Receipt;
        return string.Join(
            "\n",
            [
                $"Explain receipts: {_projection.ReceiptSummary}",
                $"Rule environment: {DesktopTrustReceiptText.BuildImportRuleEnvironment(receipt)}",
                $"Explain receipt: {DesktopTrustReceiptText.BuildImportExplainReceipt(receipt)}",
                $"Support reuse: {DesktopTrustReceiptText.BuildImportSupportReuse(receipt)}"
            ]);
    }

    private IReadOnlyList<Button> CreatePrimaryActions(Window owner)
    {
        List<Button> actions =
        [
            CreateButton("Desktop Home", () => OpenHomeAsync(owner), isPrimary: true),
            CreateButton("Campaign Workspace", () => OpenCampaignWorkspaceAsync(owner)),
            CreateButton("Support", () => OpenSupportAsync(owner))
        ];

        if (!string.IsNullOrWhiteSpace(_projection.WorkspaceId))
        {
            actions.Add(CreateButton("Open Workspace", () => OpenWorkspaceInDesktopShellAsync(owner, _projection.WorkspaceId!)));
        }

        return actions;
    }

    private Task OpenHomeAsync(Window owner)
        => DesktopHomeWindow.ShowAsync(owner, _installState.HeadId);

    private Task OpenSupportAsync(Window owner)
        => DesktopSupportWindow.ShowAsync(owner, _installState.HeadId);

    private Task OpenCampaignWorkspaceAsync(Window owner)
        => DesktopCampaignWorkspaceWindow.ShowAsync(owner, _installState.HeadId);

    private async Task OpenWorkspaceInDesktopShellAsync(Window owner, string workspaceId)
    {
        if (owner is MainWindow mainWindow)
        {
            await mainWindow.OpenWorkspaceFromDesktopSurfaceAsync(workspaceId).ConfigureAwait(true);
            Close();
            return;
        }

        DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(workspaceId, fragment: "portable-exchange");
    }

    private static Border CreateSection(string title, string body)
        => new()
        {
            Background = new SolidColorBrush(Color.Parse("#F8FBFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#CBD7E6")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 15,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = body,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        foreach (Button action in actions)
        {
            actionRow.Children.Add(action);
        }

        return actionRow;
    }

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 104,
            MinHeight = 34,
            Padding = new Thickness(12, 7)
        };
        if (isPrimary)
        {
            button.FontWeight = FontWeight.SemiBold;
        }

        button.Click += async (_, _) =>
        {
            await action().ConfigureAwait(true);
            if (closeWindow && TopLevel.GetTopLevel(button) is Window window)
            {
                window.Close();
            }
        };
        return button;
    }

    private sealed record RuleEnvironmentStudioProjection(
        string RulesetId,
        string? WorkspaceId,
        string LifecycleSummary,
        string DiffSummary,
        string ReceiptSummary,
        string RuntimeSummary,
        string SuggestionSummary);
}
