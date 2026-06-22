using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using System.Globalization;

namespace Chummer.Avalonia;

internal sealed class DesktopHorizonsWindow : Window
{
    internal static DesktopHorizonsWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly string _headId;
    private readonly DesktopPreferenceState _preferences;
    private readonly AccountCampaignSummary? _campaignSummary;
    private readonly TextBlock _introText;
    private readonly TextBlock _postureText;
    private readonly TextBlock _filterStatusText;
    private readonly TextBox _searchBox;
    private readonly StackPanel _catalogStack;

    private DesktopHorizonsWindow(string headId, DesktopPreferenceState preferences, AccountCampaignSummary? campaignSummary)
    {
        _headId = headId;
        _preferences = preferences;
        _campaignSummary = campaignSummary;

        Title = S("desktop.horizons.title");
        Width = 920;
        Height = 680;
        MinWidth = 820;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = S("desktop.horizons.intro"),
            TextWrapping = TextWrapping.Wrap
        };

        _postureText = new TextBlock
        {
            Name = "HorizonsPostureText",
            Text = BuildPostureSummary(),
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#4B6278")
        };

        _searchBox = new TextBox
        {
            Name = "HorizonsSearchBox",
            Watermark = "Filter product areas",
            MinWidth = 240
        };
        DesktopShellTheme.ApplyShellTextInputTheme(_searchBox);
        _searchBox.TextChanged += (_, _) => BuildCatalog();

        _filterStatusText = new TextBlock
        {
            Name = "HorizonsFilterStatusText",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#4B6278")
        };

        _catalogStack = new StackPanel
        {
            Name = "HorizonsCatalogStack",
            Spacing = 10
        };

        BuildCatalog();

        Content = DesktopShellTheme.CreateWindowSurface(
            new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = S("desktop.horizons.heading"),
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        _introText,
                        _postureText,
                        _searchBox,
                        _filterStatusText,
                        _catalogStack,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.horizons.button.open_public_index"), static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/roadmap")),
                                CreateButton(S("desktop.dialog.action.close"), static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            },
            padding: 16);
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState(headId);
        AccountCampaignSummary? campaignSummary = await DesktopHorizonWindowScaffold.TryReadAccountCampaignSummaryAsync("Desktop tools require an IChummerClient instance.").ConfigureAwait(true);
        DesktopHorizonsWindow dialog = new(headId, preferences, campaignSummary);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private void BuildCatalog()
    {
        _catalogStack.Children.Clear();
        string query = _searchBox.Text?.Trim() ?? string.Empty;
        bool hasQuery = !string.IsNullOrWhiteSpace(query);

        IEnumerable<DesktopHorizonWorkbenchEntry> entries = DesktopHorizonWorkbenchCatalog.ListEntries()
            .Where(ShouldShowWorkbenchEntry);
        if (hasQuery)
        {
            entries = entries.Where(entry =>
                entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.PrimaryAction.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (entry.SecondaryAction?.Label?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.TertiaryAction?.Label?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        DesktopHorizonWorkbenchEntry[] filteredEntries = entries.ToArray();
        _filterStatusText.Text = BuildFilterStatus(query, filteredEntries);
        if (!hasQuery || filteredEntries.Any(static entry => string.Equals(entry.Id, "karma_forge", StringComparison.Ordinal)))
        {
            _catalogStack.Children.Add(CreateSectionHeader("Build and rules", CountGroupedEntries(filteredEntries, static entry => entry.Id is "karma_forge" or "alice" or "quicksilver" or "local_co_processor")));
            _catalogStack.Children.Add(CreateKarmaForgeCard());
        }

        AddGroupedCards(filteredEntries, "First session and participation", static entry =>
            entry.Id is "ready_for_tonight" or "nexus_pan");
        AddGroupedCards(filteredEntries, "Build and rules", static entry =>
            entry.Id is "alice" or "knowledge_fabric" or "quicksilver" or "local_co_processor");
        AddGroupedCards(filteredEntries, "Campaign operations", static entry =>
            entry.Id is "runsite" or "run_control" or "table_pulse" or "black_ledger" or "ghostwire" or "anarchy");
        AddGroupedCards(filteredEntries, "Community and identity", static entry =>
            entry.Id is "jackpoint" or "community_hub" or "runner_passport");
        AddGroupedCards(filteredEntries, "Publishing and creators", static entry =>
            entry.Id is "runbook_press" or "creator_os");

        if (_catalogStack.Children.Count == 0)
        {
            _catalogStack.Children.Add(new TextBlock
            {
                Name = "HorizonsEmptyStateText",
                Text = "No product areas match the current filter. Clear the search or try a different name.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#4B6278")
            });
        }
    }

    private void AddGroupedCards(
        IReadOnlyList<DesktopHorizonWorkbenchEntry> filteredEntries,
        string heading,
        Func<DesktopHorizonWorkbenchEntry, bool> predicate)
    {
        DesktopHorizonWorkbenchEntry[] groupedEntries = filteredEntries
            .Where(ShouldShowWorkbenchEntry)
            .Where(entry => !string.Equals(entry.Id, "karma_forge", StringComparison.Ordinal))
            .Where(predicate)
            .ToArray();

        if (groupedEntries.Length == 0)
        {
            return;
        }

        _catalogStack.Children.Add(CreateSectionHeader(heading, groupedEntries.Length));
        foreach (DesktopHorizonWorkbenchEntry entry in groupedEntries)
        {
            _catalogStack.Children.Add(CreateHorizonCard(entry));
        }
    }

    private static Control CreateSectionHeader(string text, int count)
    {
        Grid grid = new()
        {
            Margin = new Thickness(0, 8, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        grid.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold
        });

        Border badge = new()
        {
            Name = $"HorizonsSectionBadge_{SanitizeToken(text)}",
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellChromeAccentBrush", "#DEE8F6"),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock
            {
                Text = count.ToString(CultureInfo.InvariantCulture),
                Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellInfoBrush", "#173A6C"),
                FontWeight = FontWeight.SemiBold
            }
        };
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);
        return grid;
    }

    private string BuildPostureSummary()
    {
        if (_campaignSummary is null)
        {
            return "Native-first mode. Signed-in campaign context is not available in this desktop session, so summaries stay conservative.";
        }

        return $"Native-first mode. Campaigns {_campaignSummary.Campaigns.Count} | Runs {_campaignSummary.Runs.Count} | Workspaces {_campaignSummary.Workspaces.Count} | Build links {_campaignSummary.BuildLabHandoffs.Count} | Publications {_campaignSummary.CreatorPublications.Count}";
    }

    private Control CreateKarmaForgeCard()
    {
        IReadOnlyList<DesktopHorizonRouteOption> targets = DesktopHorizonWorkbenchCatalog.ListKarmaForgeTargets();
        ComboBox targetCombo = new()
        {
            Name = "HorizonsKarmaForgeTargetCombo",
            MinWidth = 240,
            ItemsSource = targets,
            SelectedIndex = 0,
            ItemTemplate = new FuncDataTemplate<DesktopHorizonRouteOption>((option, _) =>
                DesktopShellTheme.CreateComboBoxOptionText(option.Label, TextWrapping.Wrap))
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(targetCombo);

        return CreateCard(
            "Karma Forge",
            "Browse packages, open your signed-in package page, or jump straight into a new package intake.",
            CreateLeadPanel(
                BuildCompactPosture("Packages", $"Build links {_campaignSummary?.BuildLabHandoffs.Count ?? 0} | Publications {_campaignSummary?.CreatorPublications.Count ?? 0}"),
                targetCombo),
            CreateButton("Open", () => DesktopKarmaForgeWindow.ShowAsync(this, _headId), isPrimary: true, name: "HorizonsOpenWorkbench_karma_forge"),
            CreateButton("Open selected", () =>
            {
                if (targetCombo.SelectedItem is DesktopHorizonRouteOption selected)
                {
                    return DesktopInstallLinkingRuntime.TryOpenRelativePortal(selected.RelativeHref);
                }

                return false;
            }, name: "HorizonsOpenSelected_karma_forge"),
            CreateButton("Create package", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/participate/karma-forge#karma-forge-intake"), name: "HorizonsCreatePackage_karma_forge"),
            CreateButton("My packages", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/packages"), name: "HorizonsMyPackages_karma_forge"));
    }

    private Control CreateHorizonCard(DesktopHorizonWorkbenchEntry entry)
    {
        if (string.Equals(entry.Id, "alice", StringComparison.Ordinal))
        {
            return CreateAliceCard(entry);
        }

        if (string.Equals(entry.Id, "run_control", StringComparison.Ordinal))
        {
            return CreateRunControlCard(entry);
        }

        if (string.Equals(entry.Id, "black_ledger", StringComparison.Ordinal))
        {
            return CreateBlackLedgerCard(entry);
        }

        if (DesktopHorizonWorkbenchLauncher.SupportsNativeWorkbench(entry.Id))
        {
            return CreateNativeLaunchCard(entry, () => DesktopHorizonWorkbenchLauncher.OpenAsync(this, _headId, entry));
        }

        List<Button> actions =
        [
            CreateButton(entry.PrimaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.PrimaryAction.RelativeHref), isPrimary: true)
        ];

        if (entry.SecondaryAction is not null)
        {
            actions.Add(CreateButton(entry.SecondaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.SecondaryAction.RelativeHref)));
        }

        if (entry.TertiaryAction is not null)
        {
            actions.Add(CreateButton(entry.TertiaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.TertiaryAction.RelativeHref)));
        }

        return CreateCard(entry.Title, entry.Summary, null, actions.ToArray());
    }

    private bool ShouldShowWorkbenchEntry(DesktopHorizonWorkbenchEntry entry)
        => !OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon(entry.Id, _preferences);

    private Control CreateNativeLaunchCard(DesktopHorizonWorkbenchEntry entry, Func<Task> openNative)
    {
        List<Button> actions =
        [
            CreateButton("Open", openNative, isPrimary: true, name: $"HorizonsOpenWorkbench_{entry.Id}"),
            CreateButton(entry.PrimaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.PrimaryAction.RelativeHref), name: $"HorizonsPrimaryRoute_{entry.Id}")
        ];

        InsertNativeAdjunctActions(actions, entry);

        if (entry.SecondaryAction is not null)
        {
            actions.Add(CreateButton(entry.SecondaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.SecondaryAction.RelativeHref), name: $"HorizonsSecondaryRoute_{entry.Id}"));
        }

        if (entry.TertiaryAction is not null)
        {
            actions.Add(CreateButton(entry.TertiaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.TertiaryAction.RelativeHref), name: $"HorizonsTertiaryRoute_{entry.Id}"));
        }

        return CreateCard(entry.Title, entry.Summary, CreatePostureLead(entry.Id), actions.ToArray());
    }

    private void InsertNativeAdjunctActions(List<Button> actions, DesktopHorizonWorkbenchEntry entry)
    {
        if (entry.NativeActions is null)
        {
            return;
        }

        foreach (DesktopHorizonNativeAction nativeAction in entry.NativeActions)
        {
            Button? button = CreateNativeAdjunctActionButton(entry.Id, nativeAction);
            if (button is not null)
            {
                actions.Insert(actions.Count - 1, button);
            }
        }
    }

    private Button? CreateNativeAdjunctActionButton(string horizonId, DesktopHorizonNativeAction action)
        => (horizonId, action.Id) switch
        {
            ("alice", "ready_for_tonight") => CreateButton(action.Label, () => DesktopReadyForTonightWindow.ShowAsync(this, _headId), name: "HorizonsNativeReady_alice"),
            ("alice", "knowledge_fabric") => CreateButton(action.Label, () => DesktopKnowledgeFabricWindow.ShowAsync(this, _headId), name: "HorizonsNativeKnowledge_alice"),
            ("nexus_pan", "workspace") => CreateButton(action.Label, () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId), name: "HorizonsNativeWorkspace_nexus_pan"),
            ("nexus_pan", "devices_access") => CreateButton(action.Label, () => DesktopDevicesAccessWindow.ShowAsync(this, _headId), name: "HorizonsNativeDevices_nexus_pan"),
            ("run_control", "table_pulse") => CreateButton(action.Label, () => DesktopTablePulseWindow.ShowAsync(this, _headId), name: "HorizonsNativePulse_run_control"),
            ("run_control", "workspace") => CreateButton(action.Label, () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId), name: "HorizonsNativeWorkspace_run_control"),
            ("runbook_press", "publication") => CreateButton(action.Label, () => DesktopCreatorPublicationWindow.ShowAsync(this, _headId), name: "HorizonsNativePublication_runbook_press"),
            ("runbook_press", "workspace") => CreateButton(action.Label, () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId), name: "HorizonsNativeWorkspace_runbook_press"),
            ("black_ledger", "table_pulse") => CreateButton(action.Label, () => DesktopTablePulseWindow.ShowAsync(this, _headId), name: "HorizonsNativePulse_black_ledger"),
            ("black_ledger", "ghostwire") => CreateButton(action.Label, () => DesktopGhostwireWindow.ShowAsync(this, _headId), name: "HorizonsNativeGhostwire_black_ledger"),
            ("creator_os", "publication") => CreateButton(action.Label, () => DesktopCreatorPublicationWindow.ShowAsync(this, _headId), name: "HorizonsNativePublication_creator_os"),
            ("creator_os", "workspace") => CreateButton(action.Label, () => DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId), name: "HorizonsNativeWorkspace_creator_os"),
            _ => null
        };

    private Control CreateAliceCard(DesktopHorizonWorkbenchEntry entry)
    {
        List<Button> actions =
        [
            CreateButton("Open", () => DesktopAliceWindow.ShowAsync(this, _headId), isPrimary: true, name: "HorizonsOpenWorkbench_alice"),
            CreateButton(entry.PrimaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.PrimaryAction.RelativeHref), name: "HorizonsPrimaryRoute_alice")
        ];

        InsertNativeAdjunctActions(actions, entry);

        if (entry.SecondaryAction is not null)
        {
            actions.Add(CreateButton(entry.SecondaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.SecondaryAction.RelativeHref), name: "HorizonsSecondaryRoute_alice"));
        }

        if (entry.TertiaryAction is not null)
        {
            actions.Add(CreateButton(entry.TertiaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.TertiaryAction.RelativeHref), name: "HorizonsTertiaryRoute_alice"));
        }

        return CreateCard(entry.Title, entry.Summary, CreatePostureLead(entry.Id), actions.ToArray());
    }

    private Control CreateRunControlCard(DesktopHorizonWorkbenchEntry entry)
    {
        List<Button> actions =
        [
            CreateButton("Open", () => DesktopRunControlWindow.ShowAsync(this, _headId), isPrimary: true, name: "HorizonsOpenWorkbench_run_control"),
            CreateButton(entry.PrimaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.PrimaryAction.RelativeHref), name: "HorizonsPrimaryRoute_run_control")
        ];

        InsertNativeAdjunctActions(actions, entry);

        if (entry.SecondaryAction is not null)
        {
            actions.Add(CreateButton(entry.SecondaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.SecondaryAction.RelativeHref), name: "HorizonsSecondaryRoute_run_control"));
        }

        if (entry.TertiaryAction is not null)
        {
            actions.Add(CreateButton(entry.TertiaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.TertiaryAction.RelativeHref), name: "HorizonsTertiaryRoute_run_control"));
        }

        return CreateCard(entry.Title, entry.Summary, CreatePostureLead(entry.Id), actions.ToArray());
    }

    private Control CreateBlackLedgerCard(DesktopHorizonWorkbenchEntry entry)
    {
        List<Button> actions =
        [
            CreateButton("Open", () => DesktopBlackLedgerWindow.ShowAsync(this, _headId), isPrimary: true, name: "HorizonsOpenWorkbench_black_ledger"),
            CreateButton(entry.PrimaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.PrimaryAction.RelativeHref), name: "HorizonsPrimaryRoute_black_ledger")
        ];

        InsertNativeAdjunctActions(actions, entry);

        if (entry.SecondaryAction is not null)
        {
            actions.Add(CreateButton(entry.SecondaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.SecondaryAction.RelativeHref), name: "HorizonsSecondaryRoute_black_ledger"));
        }

        if (entry.TertiaryAction is not null)
        {
            actions.Add(CreateButton(entry.TertiaryAction.Label, () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(entry.TertiaryAction.RelativeHref), name: "HorizonsTertiaryRoute_black_ledger"));
        }

        return CreateCard(entry.Title, entry.Summary, CreatePostureLead(entry.Id), actions.ToArray());
    }

    private Control? CreatePostureLead(string horizonId)
    {
        string? posture = horizonId switch
        {
            "alice" => BuildCompactPosture("Build links", $"Build links {_campaignSummary?.BuildLabHandoffs.Count ?? 0} | Rules {_campaignSummary?.RulesNavigator.Count ?? 0}"),
            "ready_for_tonight" => BuildCompactPosture("Tonight verdict", $"Runs {_campaignSummary?.Runs.Count ?? 0} | Workspaces {_campaignSummary?.Workspaces.Count ?? 0}"),
            "onramp" => BuildCompactPosture("Starter path", $"Workspaces {_campaignSummary?.Workspaces.Count ?? 0} | Campaigns {_campaignSummary?.Campaigns.Count ?? 0}"),
            "nexus_pan" => BuildCompactPosture("Access and continuity", $"Campaigns {_campaignSummary?.Campaigns.Count ?? 0} | Workspaces {_campaignSummary?.Workspaces.Count ?? 0}"),
            "jackpoint" => BuildCompactPosture("Briefings and dossiers", $"Publications {_campaignSummary?.CreatorPublications.Count ?? 0} | Dossiers {_campaignSummary?.Dossiers.Count ?? 0}"),
            "knowledge_fabric" => BuildCompactPosture("Grounded explain", $"Rules {_campaignSummary?.RulesNavigator.Count ?? 0} | Campaigns {_campaignSummary?.Campaigns.Count ?? 0}"),
            "runsite" => BuildCompactPosture("Prep and workspaces", $"Workspaces {_campaignSummary?.Workspaces.Count ?? 0} | Runs {_campaignSummary?.Runs.Count ?? 0}"),
            "run_control" => BuildCompactPosture("Runboard", $"Runs {_campaignSummary?.Runs.Count ?? 0} | Campaigns {_campaignSummary?.Campaigns.Count ?? 0}"),
            "runbook_press" => BuildCompactPosture("Publishing", $"Publications {_campaignSummary?.CreatorPublications.Count ?? 0} | Campaigns {_campaignSummary?.Campaigns.Count ?? 0}"),
            "table_pulse" => BuildCompactPosture("Live and aftermath", $"Runs {_campaignSummary?.Runs.Count ?? 0} | Workspaces {_campaignSummary?.Workspaces.Count ?? 0}"),
            "black_ledger" => BuildCompactPosture("World state", $"Campaigns {_campaignSummary?.Campaigns.Count ?? 0} | Workspaces {_campaignSummary?.Workspaces.Count ?? 0}"),
            "community_hub" => BuildCompactPosture("Groups and hosts", $"Operations {_campaignSummary?.CommunityOperations.Count ?? 0} | Campaigns {_campaignSummary?.Campaigns.Count ?? 0}"),
            "creator_os" => BuildCompactPosture("Creator desk", $"Publications {_campaignSummary?.CreatorPublications.Count ?? 0} | Build links {_campaignSummary?.BuildLabHandoffs.Count ?? 0}"),
            "anarchy" => BuildCompactPosture("Rules-light play", $"Runs {_campaignSummary?.Runs.Count ?? 0} | Dossiers {_campaignSummary?.Dossiers.Count ?? 0}"),
            "ghostwire" => BuildCompactPosture("Replay and after-action", $"Runs {_campaignSummary?.Runs.Count ?? 0} | Workspaces {_campaignSummary?.Workspaces.Count ?? 0}"),
            "runner_passport" => BuildCompactPosture("Identity network", $"Dossiers {_campaignSummary?.Dossiers.Count ?? 0} | Crews {_campaignSummary?.Crews.Count ?? 0}"),
            "quicksilver" => BuildCompactPosture("Command deck", $"Rules {_campaignSummary?.RulesNavigator.Count ?? 0} | Publications {_campaignSummary?.CreatorPublications.Count ?? 0}"),
            "local_co_processor" => BuildCompactPosture("Capability and policy", $"Rules {_campaignSummary?.RulesNavigator.Count ?? 0} | Build links {_campaignSummary?.BuildLabHandoffs.Count ?? 0}"),
            _ => null
        };

        return string.IsNullOrWhiteSpace(posture) ? null : DesktopHorizonWindowScaffold.CreateDetailText(posture);
    }

    private static string BuildCompactPosture(string title, string detail)
        => $"{title}: {detail}";

    private static int CountGroupedEntries(IReadOnlyList<DesktopHorizonWorkbenchEntry> entries, Func<DesktopHorizonWorkbenchEntry, bool> predicate)
        => entries.Count(predicate);

    private static string BuildFilterStatus(string query, IReadOnlyList<DesktopHorizonWorkbenchEntry> filteredEntries)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return $"Showing {filteredEntries.Count} product area(s). Native tools stay first whenever the client has a desktop surface.";
        }

        return $"Filter '{query}' matched {filteredEntries.Count} product area(s).";
    }

    private static string SanitizeToken(string value)
        => value
            .ToLowerInvariant()
            .Replace(" ", "_", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal);

    private static Control CreateLeadPanel(string detail, Control secondaryControl)
        => new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DesktopHorizonWindowScaffold.CreateDetailText(detail),
                secondaryControl
            }
        };

    private static Border CreateCard(string title, string summary, Control? leadControl, params Button[] actions)
    {
        string cleanTitle = PlayerFacingCopyHumanizer.Clean(title);
        string cleanSummary = PlayerFacingCopyHumanizer.Clean(summary);
        StackPanel stack = new()
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = cleanTitle,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = cleanSummary,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        if (leadControl is not null)
        {
            stack.Children.Add(leadControl);
        }

        stack.Children.Add(DesktopShellTheme.CreateWrapActionRow(actions, new Thickness(0, 0, 8, 8)));

        return DesktopShellTheme.CreateSection(
            title,
            stack,
            null,
            padding: 12,
            cornerRadius: 6,
            includeHeading: false,
            spacing: 8);
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
            Content = PlayerFacingCopyHumanizer.Clean(label),
            MinWidth = 120,
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

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);
}
