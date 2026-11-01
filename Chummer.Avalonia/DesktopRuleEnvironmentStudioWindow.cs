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

        Title = "Rules Setup";
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
                    Spacing = 10,
                    Children =
                    {
                        CreateSection("Rules package changes", BuildLifecycleBody()),
                        CreateSection("Changes", BuildDiffBody()),
                        CreateSection("Notes", BuildReceiptBody()),
                        DesktopShellTheme.CreateSection(
                            "Portable amend round trip",
                            BuildAmendSection(),
                            actionContent: null,
                            padding: 8,
                            cornerRadius: 4,
                            includeHeading: true,
                            spacing: 0),
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

    private static async Task<RuleEnvironmentStudioProjection> ReadBuildExplainProjectionAsync(
        IChummerClient client,
        IReadOnlyList<WorkspaceListItem> workspaces)
    {
        string? rulesetId = workspaces.FirstOrDefault()?.RulesetId;
        string effectiveRulesetId = rulesetId ?? "unresolved";
        string lifecycleSummary = "Rules setup is waiting for a workspace before it can match packages to a concrete ruleset.";
        string diffSummary = "No workspace change summary is available yet.";
        string receiptSummary = "Explanations will appear after the active runtime and build path can be read.";
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
                runtimeInspector = await client.GetRuntimeInspectorProfileAsync(
                    activeRuntime.ProfileId,
                    rulesetId ?? activeRuntime.RulesetId,
                    CancellationToken.None).ConfigureAwait(false);
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
                preview = await client.GetBuildPathPreviewAsync(
                    suggestion.BuildKitId,
                    workspace.Id,
                    effectiveRulesetId,
                    CancellationToken.None).ConfigureAwait(false);
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
                lifecycleSummary = $"Rules setup is using {leadWorkspace.Summary.Name} with the {buildTask.Result.BuildMethod} build method.";
                diffSummary = $"Changes compare {rulesTask.Result.GameEdition} rules with {preview?.RuntimeCompatibilitySummary ?? "the current runtime"}.";
                receiptSummary = $"Explanations connect {activeRuntime?.RuntimeFingerprint ?? leadWorkspace.RulesetId} to {preview?.SupportClosureSummary ?? "workspace support reuse"}.";
            }
            catch
            {
                lifecycleSummary = $"Rules setup loaded {leadWorkspace.Summary.Name}, but build and rules sections need a refresh before package changes.";
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
                "Rules setup",
                $"Ruleset: {_projection.RulesetId}",
                $"Package changes: {_projection.LifecycleSummary}",
                _projection.RuntimeSummary,
                _projection.SuggestionSummary
            ]);

    private string BuildDiffBody()
        => string.Join(
            "\n",
            [
                $"Changes: {_projection.DiffSummary}",
                _portabilityActivity is null
                    ? "No recent portable import or export record is attached."
                    : $"Import environment before: {DesktopTrustReceiptText.BuildImportDiffBefore(_portabilityActivity.Receipt)}",
                _portabilityActivity is null
                    ? "No after-state record is attached."
                    : $"Import environment after: {DesktopTrustReceiptText.BuildImportDiffAfter(_portabilityActivity.Receipt)}"
            ]);

    private string BuildReceiptBody()
    {
        if (_portabilityActivity is null)
        {
            return string.Join(
                "\n",
                [
                    $"Explanations: {_projection.ReceiptSummary}",
                    "Rules setup: no recent import details are attached.",
                    "Support: runtime, build-path, and workspace summaries can be reused after the next portable import or export."
                ]);
        }

        WorkspacePortabilityReceipt receipt = _portabilityActivity.Receipt;
        return string.Join(
            "\n",
            [
                $"Explanations: {_projection.ReceiptSummary}",
                $"Rules setup: {DesktopTrustReceiptText.BuildImportRuleEnvironment(receipt)}",
                "Explanation: " + DesktopTrustReceiptText.BuildImportExplainReceipt(receipt),
                $"Support: {DesktopTrustReceiptText.BuildImportSupportReuse(receipt)}"
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
            actions.Add(CreateButton("Open current workspace", () => OpenWorkspaceInDesktopShellAsync(owner, _projection.WorkspaceId!)));
            Button amendButton = CreateButton("Amend from portable export", () => RunAmendRoundTripAsync(owner));
            amendButton.Name = "AmendRoundTripButton";
            actions.Add(amendButton);
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

    private WorkspaceAmendRoundTripRecord? _lastAmendRoundTrip;

    private async Task RunAmendRoundTripAsync(Window owner)
    {
        Button? amendButton = this.FindControl<Button>("AmendRoundTripButton");
        if (amendButton is not null)
        {
            amendButton.IsEnabled = false;
        }

        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop rule environment studio requires an IChummerClient instance."));

            if (string.IsNullOrWhiteSpace(_projection.WorkspaceId))
            {
                _lastAmendRoundTrip = WorkspaceAmendRoundTripRecord.Failure(
                    "No workspace is attached to the rules environment yet, so there is nothing to amend.");
                RefreshAmendBody();
                return;
            }

            var workspaceId = new CharacterWorkspaceId(_projection.WorkspaceId);

            WorkspaceExportResult exportResult = await new WorkspacePersistenceService()
                .ExportAsync(client, workspaceId, CancellationToken.None)
                .ConfigureAwait(true);
            if (!exportResult.Success || exportResult.Receipt is null)
            {
                _lastAmendRoundTrip = WorkspaceAmendRoundTripRecord.Failure(
                    $"Amend aborted at export: {exportResult.Error ?? "unknown error"}");
                RefreshAmendBody();
                return;
            }

            byte[] exportedBytes = Convert.FromBase64String(exportResult.Receipt.ContentBase64);
            string exportedContent = System.Text.Encoding.UTF8.GetString(exportedBytes);

            WorkspaceImportResult importResult = await client.ImportAsync(
                new WorkspaceImportDocument(
                    exportedContent,
                    exportResult.Receipt.RulesetId,
                    exportResult.Receipt.Format),
                CancellationToken.None).ConfigureAwait(true);

            _lastAmendRoundTrip = WorkspaceAmendRoundTripRecord.CreateSuccess(
                exportResult.Receipt,
                importResult);
            RefreshAmendBody();
        }
        catch (Exception ex)
        {
            _lastAmendRoundTrip = WorkspaceAmendRoundTripRecord.Failure(
                $"Amend round trip failed: {ex.Message}");
            RefreshAmendBody();
        }
        finally
        {
            if (amendButton is not null)
            {
                amendButton.IsEnabled = true;
            }
        }
    }

    private void RefreshAmendBody()
    {
        StackPanel? body = this.FindControl<StackPanel>("AmendBody");
        if (body is null || _lastAmendRoundTrip is null)
        {
            return;
        }

        body.Children.Clear();
        foreach (TextBlock line in BuildAmendBodyLines(_lastAmendRoundTrip))
        {
            body.Children.Add(line);
        }
    }

    private static IReadOnlyList<TextBlock> BuildAmendBodyLines(WorkspaceAmendRoundTripRecord roundTrip)
    {
        if (!roundTrip.Success)
        {
            return
            [
                new TextBlock
                {
                    Text = $"Amend round trip failed: {roundTrip.Summary}",
                    TextWrapping = TextWrapping.Wrap
                }
            ];
        }

        var lines = new List<TextBlock>
        {
            new TextBlock
            {
                Text = $"Amend completed: portable export re-imported as workspace {roundTrip.ImportedWorkspaceId}.",
                TextWrapping = TextWrapping.Wrap
            }
        };

        if (roundTrip.ExportReceipt is not null)
        {
            WorkspacePortabilityReceipt portability = roundTrip.ExportReceipt.Portability;
            lines.Add(new TextBlock
            {
                Text = $"Export receipt: {roundTrip.ExportReceipt.Portability.ReceiptSummary} ({roundTrip.ExportReceipt.Format})",
                TextWrapping = TextWrapping.Wrap
            });
            lines.Add(new TextBlock
            {
                Text = $"Portability: {portability.CompatibilityState}; next safe action: {portability.NextSafeAction}",
                TextWrapping = TextWrapping.Wrap
            });
            foreach (string mode in portability.SupportedExchangeModes)
            {
                lines.Add(new TextBlock
                {
                    Text = $"Supported exchange mode: {mode}",
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        if (roundTrip.ImportResult is not null)
        {
            lines.Add(new TextBlock
            {
                Text = $"Import receipt: {roundTrip.ImportResult.ImportReceiptId}",
                TextWrapping = TextWrapping.Wrap
            });
        }

        return lines;
    }

    private sealed record WorkspaceAmendRoundTripRecord(
        bool Success,
        string Summary,
        WorkspaceExportReceipt? ExportReceipt = null,
        WorkspaceImportResult? ImportResult = null,
        string? ImportedWorkspaceId = null)
    {
        public static WorkspaceAmendRoundTripRecord CreateSuccess(
            WorkspaceExportReceipt exportReceipt,
            WorkspaceImportResult importResult)
            => new(
                Success: true,
                Summary: importResult.ImportReceiptId,
                ExportReceipt: exportReceipt,
                ImportResult: importResult,
                ImportedWorkspaceId: importResult.Id.Value);

        public static WorkspaceAmendRoundTripRecord Failure(string summary)
            => new(Success: false, Summary: summary);
    }

    private StackPanel BuildAmendSection()
    {
        var body = new StackPanel { Spacing = 4 };
        body.Name = "AmendBody";
        body.Children.Add(new TextBlock
        {
            Text = "Run a governed amend: export the current workspace as a portable dossier, re-import it, and record both receipts.",
            TextWrapping = TextWrapping.Wrap
        });
        if (_lastAmendRoundTrip is not null)
        {
            foreach (TextBlock line in BuildAmendBodyLines(_lastAmendRoundTrip))
            {
                body.Children.Add(line);
            }
        }

        return body;
    }

    private static Border CreateSection(string title, string body)
    {
        TextBlock bodyText = new()
        {
            Text = body,
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };
        ToolTip.SetTip(bodyText, title);
        return DesktopShellTheme.CreateSection(
            title,
            bodyText,
            actionContent: null,
            padding: 8,
            cornerRadius: 4,
            includeHeading: false,
            spacing: 0);
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
        => DesktopShellTheme.CreateStackActionRow(actions, spacing: 8);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 104);

    private sealed record RuleEnvironmentStudioProjection(
        string RulesetId,
        string? WorkspaceId,
        string LifecycleSummary,
        string DiffSummary,
        string ReceiptSummary,
        string RuntimeSummary,
        string SuggestionSummary);
}
