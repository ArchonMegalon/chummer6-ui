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
using System.Globalization;

namespace Chummer.Avalonia;

internal sealed class DesktopHomeWindow : Window
{
    private DesktopInstallLinkingState _installState;
    private DesktopUpdateClientStatus _updateStatus;
    private readonly DesktopPreferenceState _preferences;
    private readonly IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private readonly DesktopHomeBuildExplainProjection _buildExplainProjection;
    private readonly TextBlock _introText;
    private readonly TextBlock _installSummaryText;
    private readonly TextBlock _updateSummaryText;
    private readonly StackPanel _installActionsRow;
    private readonly StackPanel _updateActionsRow;

    private DesktopHomeWindow(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeBuildExplainProjection buildExplainProjection)
    {
        _installState = installState;
        _updateStatus = updateStatus;
        _preferences = preferences;
        _recentWorkspaces = recentWorkspaces;
        _buildExplainProjection = buildExplainProjection;

        Title = "Chummer Desktop Home";
        Width = 860;
        Height = 640;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            TextWrapping = TextWrapping.Wrap
        };

        _installSummaryText = new TextBlock
        {
            Text = BuildInstallSummary(),
            TextWrapping = TextWrapping.Wrap
        };

        _updateSummaryText = new TextBlock
        {
            Text = BuildUpdateSummary(),
            TextWrapping = TextWrapping.Wrap
        };

        _installActionsRow = CreateActionRow(CreateInstallActions());
        _updateActionsRow = CreateActionRow(CreateUpdateActions());

        Content = new Border
        {
            Padding = new Thickness(22),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Desktop home cockpit",
                        FontSize = 24,
                        FontWeight = FontWeight.SemiBold
                    },
                    _introText,
                    CreateSection(
                        "Install and support",
                        _installSummaryText,
                        _installActionsRow),
                    CreateSection(
                        "Update posture",
                        _updateSummaryText,
                        _updateActionsRow),
                    CreateSection(
                        "Build and explain next",
                        BuildBuildExplainBody(),
                        []),
                    CreateSection(
                        "Language and trust surfaces",
                        $"Language: {DesktopLocalizationCatalog.GetDisplayLabel(_preferences.Language)}\nShipping locales: {DesktopLocalizationCatalog.BuildSupportedLanguageSummary()}\nLanguage changes apply fully on restart during the current desktop wave.",
                        []),
                    CreateSection(
                        "Recent workspaces",
                        BuildWorkspaceSummary(),
                        []),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            CreateButton("Continue", static () => true, closeWindow: true)
                        }
                    }
                }
            }
        };
    }

    public static async Task ShowIfNeededAsync(Window owner, string headId, DesktopInstallLinkingStartupContext? installContext)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Desktop home requires an IChummerClient instance."));

        DesktopInstallLinkingState installState = installContext?.State ?? DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopUpdateClientStatus updateStatus = DesktopUpdateRuntime.GetCurrentStatus(headId);
        DesktopPreferenceState preferences = ReadPreferences();
        IReadOnlyList<WorkspaceListItem> workspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
        DesktopHomeBuildExplainProjection buildExplainProjection = await ReadBuildExplainProjectionAsync(client, workspaces).ConfigureAwait(true);

        if (!ShouldShow(installContext, updateStatus, workspaces))
        {
            return;
        }

        DesktopHomeWindow dialog = new(installState, updateStatus, preferences, workspaces, buildExplainProjection);
        await dialog.ShowDialog(owner);
    }

    private static bool ShouldShow(
        DesktopInstallLinkingStartupContext? installContext,
        DesktopUpdateClientStatus updateStatus,
        IReadOnlyList<WorkspaceListItem> workspaces)
    {
        if (installContext?.ShouldPrompt == true)
        {
            return true;
        }

        if (!string.Equals(updateStatus.Status, "current", StringComparison.Ordinal))
        {
            return true;
        }

        return workspaces.Count == 0;
    }

    private static DesktopPreferenceState ReadPreferences()
    {
        string cultureCode = CultureInfo.CurrentUICulture.Name.Replace('_', '-').ToLowerInvariant();
        return DesktopPreferenceState.Default with
        {
            Language = DesktopLocalizationCatalog.NormalizeOrDefault(cultureCode)
        };
    }

    private static async Task<IReadOnlyList<WorkspaceListItem>> ReadWorkspacesAsync(IChummerClient client)
    {
        IReadOnlyList<WorkspaceListItem> workspaces = await client.ListWorkspacesAsync(CancellationToken.None).ConfigureAwait(false);
        return workspaces
            .OrderByDescending(workspace => workspace.LastUpdatedUtc)
            .Take(5)
            .ToArray();
    }

    private static async Task<DesktopHomeBuildExplainProjection> ReadBuildExplainProjectionAsync(
        IChummerClient client,
        IReadOnlyList<WorkspaceListItem> workspaces)
    {
        string? rulesetId = workspaces.Count == 0 ? null : workspaces[0].RulesetId;
        ActiveRuntimeStatusProjection? activeRuntime = null;
        RuntimeInspectorProjection? runtimeInspector = null;

        try
        {
            ShellBootstrapSnapshot bootstrap = await client.GetShellBootstrapAsync(rulesetId, CancellationToken.None).ConfigureAwait(false);
            activeRuntime = bootstrap.ActiveRuntime;
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

        if (workspaces.Count == 0)
        {
            return DesktopHomeBuildExplainProjector.Create(workspaces, build: null, rules: null, activeRuntime, runtimeInspector);
        }

        WorkspaceListItem leadWorkspace = workspaces[0];
        try
        {
            Task<CharacterBuildSection> buildTask = client.GetBuildAsync(leadWorkspace.Id, CancellationToken.None);
            Task<CharacterRulesSection> rulesTask = client.GetRulesAsync(leadWorkspace.Id, CancellationToken.None);
            await Task.WhenAll(buildTask, rulesTask).ConfigureAwait(false);
            return DesktopHomeBuildExplainProjector.Create(workspaces, buildTask.Result, rulesTask.Result, activeRuntime, runtimeInspector);
        }
        catch
        {
            return DesktopHomeBuildExplainProjector.Create(workspaces, build: null, rules: null, activeRuntime, runtimeInspector);
        }
    }

    private string BuildIntro()
    {
        if (!DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
            {
                return "This flagship desktop head is still running as a guest because the last claim attempt failed. Link this copy from home before you rely on install-aware support, fix notices, or roaming continuity.";
            }

            return "This flagship desktop head is ready to continue as a guest, but the account-aware path is the recommended route if you want install-aware support, fix notices, and roaming continuity.";
        }

        if (string.Equals(_updateStatus.Status, "update_available", StringComparison.Ordinal))
        {
            return "A promoted update is ready for this install. Review the update posture before you jump back into campaign work.";
        }

        return "This desktop head is linked, current enough to continue, and ready to drop back into recent workspaces.";
    }

    private string BuildInstallSummary()
    {
        List<string> lines =
        [
            $"Install ID: {_installState.InstallationId}",
            $"Head: {_installState.HeadId}",
            $"Version: {_installState.ApplicationVersion}",
            $"Channel: {_installState.ChannelId}",
            $"Platform: {_installState.Platform}/{_installState.Arch}"
        ];

        if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            lines.Add($"Status: Linked to account. Grant expires {_installState.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")} UTC.");
        }
        else
        {
            lines.Add("Status: Not linked yet. Support and closure stay stronger once the install is claimed.");
            if (_installState.LastPromptDismissedAtUtc is not null)
            {
                lines.Add($"Last guest defer: {_installState.LastPromptDismissedAtUtc.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.");
            }
        }

        if (_installState.LastClaimAttemptUtc is not null)
        {
            lines.Add($"Last claim attempt: {_installState.LastClaimAttemptUtc.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.");
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimMessage))
        {
            lines.Add($"Hub message: {_installState.LastClaimMessage}");
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
        {
            lines.Add($"Claim error: {_installState.LastClaimError}");
        }

        return string.Join("\n", lines);
    }

    private string BuildUpdateSummary()
    {
        string lastChecked = _updateStatus.LastCheckedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? "Never";
        string manifestVersion = string.IsNullOrWhiteSpace(_updateStatus.LastManifestVersion)
            ? "Unknown"
            : _updateStatus.LastManifestVersion;
        string manifestPublished = _updateStatus.LastManifestPublishedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? "Unknown";
        string error = string.IsNullOrWhiteSpace(_updateStatus.LastError)
            ? "None"
            : _updateStatus.LastError;
        return $"Status: {_updateStatus.Status}\nInstalled: {_updateStatus.InstalledVersion}\nManifest: {manifestVersion}\nManifest published: {manifestPublished} UTC\nChannel: {_updateStatus.ChannelId}\nLast checked: {lastChecked} UTC\nAuto apply: {_updateStatus.AutoApply}\nRecommended action: {_updateStatus.RecommendedAction}\nLast error: {error}";
    }

    private string BuildWorkspaceSummary()
    {
        if (_recentWorkspaces.Count == 0)
        {
            return "No recent workspaces were restored yet. Import or create a runner to seed the campaign workspace lane.";
        }

        return string.Join(
            "\n",
            _recentWorkspaces.Select(workspace =>
                $"{workspace.Summary} · {workspace.RulesetId} · {workspace.LastUpdatedUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC"));
    }

    private string BuildBuildExplainBody()
    {
        List<string> lines =
        [
            $"Next safe action: {_buildExplainProjection.NextSafeAction}",
            _buildExplainProjection.Summary,
            _buildExplainProjection.ExplainFocus,
            _buildExplainProjection.RuntimeHealthSummary,
            _buildExplainProjection.ReturnTarget,
            _buildExplainProjection.RulePosture
        ];

        foreach (string receipt in _buildExplainProjection.CompatibilityReceipts)
        {
            lines.Add(receipt);
        }

        foreach (string watchout in _buildExplainProjection.Watchouts)
        {
            lines.Add($"Watchout: {watchout}");
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateInstallActions()
    {
        List<Button> actions =
        [
            CreateButton(
                DesktopInstallLinkingRuntime.IsClaimed(_installState) ? "Open devices and access" : "Open account",
                static () => DesktopInstallLinkingRuntime.TryOpenAccountPortal())
        ];

        if (!DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            actions.Insert(0, CreateButton("Link this copy", OpenInstallLinkingAsync, isPrimary: true));
        }

        actions.Add(CreateButton("Open support", static () => DesktopInstallLinkingRuntime.TryOpenSupportPortal()));
        return actions;
    }

    private IReadOnlyList<Button> CreateUpdateActions()
    {
        List<Button> actions =
        [
            CreateButton("Open downloads", static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        ];

        if (!string.Equals(_updateStatus.Status, "current", StringComparison.Ordinal))
        {
            actions.Add(CreateButton("Open support", static () => DesktopInstallLinkingRuntime.TryOpenSupportPortal()));
        }

        return actions;
    }

    private async Task OpenInstallLinkingAsync()
    {
        DesktopInstallLinkingStartupContext context = new(
            State: _installState,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_home");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(this);
        RefreshHomeState();
    }

    private void RefreshHomeState()
    {
        _installState = DesktopInstallLinkingRuntime.LoadOrCreateState(_installState.HeadId);
        _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(_installState.HeadId);
        _introText.Text = BuildIntro();
        _installSummaryText.Text = BuildInstallSummary();
        _updateSummaryText.Text = BuildUpdateSummary();
        ResetActionRow(_installActionsRow, CreateInstallActions());
        ResetActionRow(_updateActionsRow, CreateUpdateActions());
    }

    private static Border CreateSection(string title, string body, IReadOnlyList<Button> actions)
        => CreateSection(
            title,
            new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.Wrap
            },
            CreateActionRow(actions));

    private static Border CreateSection(string title, Control body, Control? actionContent)
    {
        StackPanel content = new()
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 18
                },
                body
            }
        };

        if (actionContent is not null)
        {
            content.Children.Add(actionContent);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F4F6FA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D4DCE7")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = content
        };
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        foreach (Button action in actions)
        {
            actionRow.Children.Add(action);
        }

        return actionRow;
    }

    private static void ResetActionRow(StackPanel actionRow, IReadOnlyList<Button> actions)
    {
        actionRow.Children.Clear();
        foreach (Button action in actions)
        {
            actionRow.Children.Add(action);
        }
    }

    private static Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => CreateButton(
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow,
            isPrimary);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 120
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
}
