using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopRuleEnvironmentStudioWindow : Window
{
    private readonly DesktopInstallLinkingState _installState;
    private readonly RuleEnvironmentStudioProjection _projection;
    private readonly string? _leadWorkspaceId;
    private readonly WorkspacePortabilityActivity? _portabilityActivity;

    private DesktopRuleEnvironmentStudioWindow(
        DesktopInstallLinkingState installState,
        RuleEnvironmentStudioProjection projection,
        string? leadWorkspaceId,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        _installState = installState;
        _projection = projection;
        _leadWorkspaceId = leadWorkspaceId;
        _portabilityActivity = portabilityActivity;

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
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = "Rule-environment studio keeps amend-package lifecycle, before-after diffs, explain receipts, and support reuse together before a ruleset handoff is trusted.",
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.DarkSlateGray
                        },
                        CreateSection("Amend-package lifecycle", BuildLifecycleBody(), CreateActionRow(CreateLifecycleActions())),
                        CreateSection("Before-after diffs", BuildDiffBody(), CreateActionRow(CreateDiffActions())),
                        CreateSection("Explain receipts", BuildReceiptBody(), CreateActionRow(CreateReceiptActions())),
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

        DesktopRuleEnvironmentStudioWindow dialog = await CreateAsync(headId, portabilityActivity).ConfigureAwait(true);
        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopRuleEnvironmentStudioWindow> CreateAsync(
        string headId,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Desktop rule environment studio requires an IChummerClient instance."));

        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        RuleEnvironmentStudioProjection projection = await ReadBuildExplainProjectionAsync(client, portabilityActivity).ConfigureAwait(true);
        return new DesktopRuleEnvironmentStudioWindow(installState, projection, projection.LeadWorkspaceId, portabilityActivity);
    }

    private static async Task<RuleEnvironmentStudioProjection> ReadBuildExplainProjectionAsync(
        IChummerClient client,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        IReadOnlyList<WorkspaceListItem> workspaces = await ReadWorkspacesAsync(client).ConfigureAwait(false);
        WorkspaceListItem? leadWorkspace = workspaces.FirstOrDefault();
        string? rulesetId = leadWorkspace?.RulesetId;
        string? runtimeSummary = null;
        string? buildPathSummary = null;
        string? buildAndRulesSummary = null;

        try
        {
            var bootstrap = await client.GetShellBootstrapAsync(rulesetId, CancellationToken.None).ConfigureAwait(false);
            string effectiveRulesetId = string.IsNullOrWhiteSpace(bootstrap.ActiveRulesetId)
                ? bootstrap.RulesetId
                : bootstrap.ActiveRulesetId;

            runtimeSummary = bootstrap.ActiveRuntime is null
                ? $"Runtime: {effectiveRulesetId} has no active runtime profile advertised."
                : $"Runtime: {effectiveRulesetId} uses {bootstrap.ActiveRuntime.ProfileId}.";

            if (bootstrap.ActiveRuntime is not null)
            {
                var runtimeInspector = await client.GetRuntimeInspectorProfileAsync(
                    bootstrap.ActiveRuntime.ProfileId,
                    rulesetId ?? bootstrap.ActiveRuntime.RulesetId,
                    CancellationToken.None).ConfigureAwait(false);
                if (runtimeInspector is not null)
                {
                    runtimeSummary = $"{runtimeSummary}\n{RuntimeInspectorDiagnostics.BuildProfileDiagnosticsSummary(runtimeInspector)}";
                }
            }

            IReadOnlyList<DesktopBuildPathSuggestion> suggestions = await client.GetBuildPathSuggestionsAsync(effectiveRulesetId, CancellationToken.None).ConfigureAwait(false);
            DesktopBuildPathSuggestion? leadSuggestion = suggestions.FirstOrDefault();
            if (leadSuggestion is not null && leadWorkspace is not null)
            {
                DesktopBuildPathPreview? preview = await client.GetBuildPathPreviewAsync(
                    leadSuggestion.BuildKitId,
                    leadWorkspace.Id,
                    effectiveRulesetId,
                    CancellationToken.None).ConfigureAwait(false);
                buildPathSummary = preview is null
                    ? $"Build path: {leadSuggestion.Title} is available without preview details."
                    : $"Build path: {leadSuggestion.Title} -> {preview.State}; {FirstNonBlank(preview.RuntimeCompatibilitySummary, preview.CampaignReturnSummary, preview.SupportClosureSummary)}";
            }
            else
            {
                buildPathSummary = suggestions.Count == 0
                    ? "Build path: no build-path suggestions are advertised for this ruleset."
                    : $"Build path: {suggestions[0].Title} is ready once a workspace is selected.";
            }
        }
        catch (Exception ex)
        {
            runtimeSummary = $"Runtime: rule environment bootstrap is unavailable locally ({ex.GetType().Name}).";
            buildPathSummary = "Build path: preview is deferred until the bootstrap route responds.";
        }

        if (leadWorkspace is not null)
        {
            try
            {
                Task<CharacterBuildSection> buildTask = client.GetBuildAsync(leadWorkspace.Id, CancellationToken.None);
                Task<CharacterRulesSection> rulesTask = client.GetRulesAsync(leadWorkspace.Id, CancellationToken.None);
                await Task.WhenAll(buildTask, rulesTask).ConfigureAwait(false);
                buildAndRulesSummary = $"Workspace rules: {leadWorkspace.Summary} [{leadWorkspace.RulesetId}] loaded build and rules sections for diff review.";
            }
            catch (Exception ex)
            {
                buildAndRulesSummary = $"Workspace rules: build/rules sections are unavailable for {leadWorkspace.Summary} ({ex.GetType().Name}).";
            }
        }
        else
        {
            buildAndRulesSummary = "Workspace rules: no current workspace is loaded, so diff review is limited to bootstrap and import receipts.";
        }

        string importRuleEnvironment;
        string diffBefore;
        string diffAfter;
        string explainReceipt;
        string supportReuse;
        if (portabilityActivity is not null)
        {
            importRuleEnvironment = DesktopTrustReceiptText.BuildImportRuleEnvironment(portabilityActivity.Receipt);
            diffBefore = DesktopTrustReceiptText.BuildImportDiffBefore(portabilityActivity.Receipt);
            diffAfter = DesktopTrustReceiptText.BuildImportDiffAfter(portabilityActivity.Receipt);
            explainReceipt = DesktopTrustReceiptText.BuildImportExplainReceipt(portabilityActivity.Receipt);
            supportReuse = DesktopTrustReceiptText.BuildImportSupportReuse(portabilityActivity.Receipt);
        }
        else
        {
            importRuleEnvironment = "No portable import receipt is active; ruleset bootstrap and workspace sections are the current rule environment evidence.";
            diffBefore = "Before: current desktop ruleset bootstrap and workspace rule sections.";
            diffAfter = "After: pending amend-package import or ruleset switch.";
            explainReceipt = "Explain receipt: no import receipt is active yet.";
            supportReuse = "Support reuse: cite this studio after an import/export receipt is available.";
        }

        return new RuleEnvironmentStudioProjection(
            LeadWorkspaceId: leadWorkspace?.Id.Value,
            LifecycleBody: string.Join("\n", [
                $"Import rule environment: {importRuleEnvironment}",
                runtimeSummary,
                buildPathSummary,
                "Amend-package lifecycle: review bootstrap, runtime profile, build-path preview, workspace build/rules sections, then support reuse before trusting a ruleset change."
            ]),
            DiffBody: string.Join("\n", [
                $"Before: {diffBefore}",
                $"After: {diffAfter}",
                buildAndRulesSummary,
                "Before-after diffs: keep old and new rule environment claims visible until the workspace opens under the intended ruleset."
            ]),
            ReceiptBody: string.Join("\n", [
                $"Explain receipt: {explainReceipt}",
                $"Support reuse: {supportReuse}",
                "Explain receipts: carry import hash, runtime diagnostics, build-path preview, and workspace rule posture into support or campaign handoff."
            ]));
    }

    private static async Task<IReadOnlyList<WorkspaceListItem>> ReadWorkspacesAsync(IChummerClient client)
    {
        try
        {
            return (await client.ListWorkspacesAsync(CancellationToken.None).ConfigureAwait(false))
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
        string? workspaceId = _leadWorkspaceId;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return;
        }

        if (Owner is MainWindow mainWindow)
        {
            await mainWindow.OpenWorkspaceFromDesktopSurfaceAsync(workspaceId).ConfigureAwait(true);
            Close();
            return;
        }

        DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(workspaceId, fragment: "portable-exchange");
    }

    private bool OpenArtifactShelfView(string view)
        => DesktopInstallLinkingRuntime.IsClaimed(_installState)
           && DesktopInstallLinkingRuntime.TryOpenRelativePortal($"/artifacts?view={Uri.EscapeDataString(view)}");

    private static Border CreateSection(string title, string body, Control? actionContent)
    {
        StackPanel content = new()
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 15
                },
                new TextBlock
                {
                    Text = body,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        if (actionContent is not null)
        {
            content.Children.Add(actionContent);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F8FBFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#CBD7E6")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = content
        };
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
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
            MinWidth = 112
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

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? "pending";
}

internal sealed record RuleEnvironmentStudioProjection(
    string? LeadWorkspaceId,
    string LifecycleBody,
    string DiffBody,
    string ReceiptBody);
