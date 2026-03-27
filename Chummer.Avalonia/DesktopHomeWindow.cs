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
    private readonly DesktopInstallLinkingState _installState;
    private readonly DesktopUpdateClientStatus _updateStatus;
    private readonly DesktopPreferenceState _preferences;
    private readonly IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private readonly DesktopHomeBuildExplainProjection _buildExplainProjection;

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
                    new TextBlock
                    {
                        Text = BuildIntro(),
                        TextWrapping = TextWrapping.Wrap
                    },
                    CreateSection(
                        "Install and support",
                        BuildInstallSummary(),
                        [
                            CreateButton("Open account", static () => DesktopInstallLinkingRuntime.TryOpenAccountPortal()),
                            CreateButton("Open support", static () => DesktopInstallLinkingRuntime.TryOpenSupportPortal())
                        ]),
                    CreateSection(
                        "Update posture",
                        BuildUpdateSummary(),
                        [
                            CreateButton("Open downloads", static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
                        ]),
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
        string linked = DesktopInstallLinkingRuntime.IsClaimed(_installState)
            ? $"Linked to account. Grant expires {_installState.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")} UTC."
            : "Not linked yet. Support and closure stay stronger once the install is claimed.";
        return $"Install ID: {_installState.InstallationId}\nHead: {_installState.HeadId}\nVersion: {_installState.ApplicationVersion}\nChannel: {_installState.ChannelId}\nPlatform: {_installState.Platform}/{_installState.Arch}\nStatus: {linked}";
    }

    private string BuildUpdateSummary()
    {
        string lastChecked = _updateStatus.LastCheckedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? "Never";
        string manifestVersion = string.IsNullOrWhiteSpace(_updateStatus.LastManifestVersion)
            ? "Unknown"
            : _updateStatus.LastManifestVersion;
        string error = string.IsNullOrWhiteSpace(_updateStatus.LastError)
            ? "None"
            : _updateStatus.LastError;
        return $"Status: {_updateStatus.Status}\nInstalled: {_updateStatus.InstalledVersion}\nManifest: {manifestVersion}\nChannel: {_updateStatus.ChannelId}\nLast checked: {lastChecked} UTC\nAuto apply: {_updateStatus.AutoApply}\nRecommended action: {_updateStatus.RecommendedAction}\nLast error: {error}";
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

        foreach (string watchout in _buildExplainProjection.Watchouts)
        {
            lines.Add($"Watchout: {watchout}");
        }

        return string.Join("\n", lines);
    }

    private static Border CreateSection(string title, string body, IReadOnlyList<Button> actions)
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
                new TextBlock
                {
                    Text = body,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        if (actions.Count > 0)
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

            content.Children.Add(actionRow);
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

    private static Button CreateButton(string label, Func<bool> action, bool closeWindow = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 120
        };
        button.Click += (_, _) =>
        {
            action();
            if (closeWindow && TopLevel.GetTopLevel(button) is Window window)
            {
                window.Close();
            }
        };
        return button;
    }
}
