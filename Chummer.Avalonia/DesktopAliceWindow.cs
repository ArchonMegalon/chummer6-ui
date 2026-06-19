using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chummer.Contracts.AI;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Content;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace Chummer.Avalonia;

internal sealed class DesktopAliceWindow : Window
{
    private const string BuildHelpMode = "Build help";
    private const string RulesCoachMode = "Rules coach";
    private const string OriginDossierMode = "Origin Dossier";
    private const string LegacyOriginDraftMode = "Origin draft";
    private const string OriginNarrationRequestPathEnv = "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_REQUEST_PATH";
    private const string OriginVideoRequestPathEnv = "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_VIDEO_REQUEST_PATH";
    private const string MediaFactoryRepoRootEnv = "CHUMMER_MEDIA_FACTORY_REPO_ROOT";
    private const string MediaFactoryNarrationCliProjectEnv = "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_NARRATION_CLI_PROJECT";
    private const string MediaFactoryVideoCliProjectEnv = "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_VIDEO_CLI_PROJECT";
    private const string MediaFactoryNarrationCliProjectRelative = "tools/OriginDossierNarrationRequestCli/Chummer.Media.Factory.OriginDossierNarrationRequestCli.csproj";
    private const string MediaFactoryVideoCliProjectRelative = "tools/OriginDossierVideoRequestCli/Chummer.Media.Factory.OriginDossierVideoRequestCli.csproj";
    internal static DesktopAliceWindow? LastOpenedWindowForTesting { get; private set; }
    private readonly AccountCampaignSummary? _campaignSummary;
    private readonly IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private readonly IReadOnlyList<DesktopBuildPathCandidate> _buildPathCandidates;
    private readonly IAvaloniaCoachSidecarClient? _coachSidecarClient;
    private readonly string? _rulesetId;
    private readonly string? _workspaceId;
    private readonly string? _preferredConversationMode;
    private readonly string _coachConversationId = $"alice-coach-{Guid.NewGuid():N}";
    private readonly string _buildConversationId = $"alice-build-{Guid.NewGuid():N}";
    private string _gmAllowanceNotes = string.Empty;
    private string _originArchetypeSelection = "Use current character";
    private string _originBuildFrameSelection = "Use current ruleset";
    private string _originPressureSelection = "Street-level survival";
    private BuildLabHandoffProjection? _selectedHandoff;
    private DesktopBuildPathCandidate? _selectedBuildPath;
    private Action? _refreshAssistantContext;
    private CharacterNarrativePacket? _originPacket;
    private CharacterNarrativeDraft? _originDraft;
    private OriginDossierBundle? _originBundle;
    private bool HasHandoffContext => (_campaignSummary?.BuildLabHandoffs.Count ?? 0) > 0;
    private bool HasBuildPathContext => _buildPathCandidates.Count > 0;

    private DesktopAliceWindow(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        IReadOnlyList<DesktopBuildPathCandidate> buildPathCandidates,
        IAvaloniaCoachSidecarClient? coachSidecarClient,
        string? rulesetId,
        string? preferredConversationMode)
    {
        _campaignSummary = campaignSummary;
        _recentWorkspaces = recentWorkspaces;
        _buildPathCandidates = buildPathCandidates;
        _coachSidecarClient = coachSidecarClient;
        _rulesetId = RulesetDefaults.NormalizeOptional(rulesetId);
        _workspaceId = recentWorkspaces.FirstOrDefault()?.Id.Value;
        _preferredConversationMode = preferredConversationMode;
        _selectedHandoff = campaignSummary?.BuildLabHandoffs.OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefault();
        _selectedBuildPath = buildPathCandidates.FirstOrDefault();

        Title = "Alice";
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
                            Text = "Alice",
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "Alice helps with rules, build choices, and origin dossiers inside the desktop client.",
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
                                CreateButton("Open web Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice")),
                                CreateButton("Close", static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner, string headId)
        => await ShowAsync(owner, headId, preferredConversationMode: null).ConfigureAwait(true);

    public static async Task ShowOriginDraftAsync(Window owner, string headId)
        => await ShowAsync(owner, headId, OriginDossierMode).ConfigureAwait(true);

    private static async Task ShowAsync(Window owner, string headId, string? preferredConversationMode)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        if (DesktopPreferenceRuntime.LoadOrCreateState(headId).DisableAiFeatures)
        {
            return;
        }

        DesktopAliceWindow dialog = await CreateAsync(preferredConversationMode).ConfigureAwait(true);
        LastOpenedWindowForTesting = dialog;
        dialog.Closed += static (_, _) => LastOpenedWindowForTesting = null;
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopAliceWindow> CreateAsync(string? preferredConversationMode)
    {
        AccountCampaignSummary? summary = null;
        IReadOnlyList<WorkspaceListItem> workspaces = Array.Empty<WorkspaceListItem>();
        IReadOnlyList<DesktopBuildPathCandidate> buildPathCandidates = Array.Empty<DesktopBuildPathCandidate>();
        IAvaloniaCoachSidecarClient? coachSidecarClient = App.Services?.GetService(typeof(IAvaloniaCoachSidecarClient)) as IAvaloniaCoachSidecarClient;
        string? effectiveRulesetId = null;
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop Alice requires an IChummerClient instance."));
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

        return new DesktopAliceWindow(summary, workspaces, buildPathCandidates, coachSidecarClient, effectiveRulesetId, preferredConversationMode);
    }

    private static string NormalizeConversationMode(string? mode)
    {
        if (string.Equals(mode, RulesCoachMode, StringComparison.Ordinal))
        {
            return RulesCoachMode;
        }

        if (IsOriginDossierMode(mode))
        {
            return OriginDossierMode;
        }

        return BuildHelpMode;
    }

    private static bool IsOriginDossierMode(string? mode)
        => string.Equals(mode, OriginDossierMode, StringComparison.Ordinal)
            || string.Equals(mode, LegacyOriginDraftMode, StringComparison.Ordinal);

    private Control CreateAssistantCard()
    {
        IReadOnlyList<string> modes = [BuildHelpMode, RulesCoachMode, OriginDossierMode];
        List<AliceConversationTurnEntry> buildHistory = [];
        List<AliceConversationTurnEntry> rulesHistory = [];
        List<AliceConversationTurnEntry> originHistory = [];
        ComboBox modeCombo = new()
        {
            Name = "AliceConversationModeCombo",
            MinWidth = 220,
            ItemsSource = modes,
            SelectedItem = NormalizeConversationMode(_preferredConversationMode)
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(modeCombo);

        WrapPanel modeShortcutRow = new()
        {
            Name = "AliceModeShortcutRow",
            Orientation = Orientation.Horizontal,
            ItemHeight = double.NaN,
            ItemWidth = double.NaN
        };

        TextBlock modeGuideText = new()
        {
            Name = "AliceModeGuideText",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        TextBlock settingsGuideTitleText = new()
        {
            Name = "AliceSettingsGuideTitleText",
            Text = "How these settings work",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        TextBlock settingsGuideText = new()
        {
            Name = "AliceSettingsGuideText",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        ListBox conversationList = new()
        {
            Name = "AliceConversationList",
            MinHeight = 220,
            MaxHeight = 320,
            ItemTemplate = new FuncDataTemplate<AliceConversationTurnEntry>((entry, _) => BuildConversationTurnView(entry))
        };
        DesktopShellTheme.ApplyShellListBoxTheme(conversationList);

        TextBox promptBox = new()
        {
            Name = "AliceQuestionTextBox",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 78,
            Watermark = "Ask Alice about the current build, rules tradeoffs, or what to add next."
        };
        DesktopShellTheme.ApplyShellTextInputTheme(promptBox);
        global::Avalonia.Automation.AutomationProperties.SetName(promptBox, "Ask Alice");
        global::Avalonia.Automation.AutomationProperties.SetHelpText(promptBox, "Ask Alice for build help, rules explanation, or Origin Dossier guidance. Text-box hover tips are disabled so the placeholder does not overlap typed text.");
        ToolTip.SetTip(promptBox, null);

        TextBlock gmAllowanceGuideText = new()
        {
            Name = "AliceGmAllowanceGuideText",
            Text = "GM notes can require addiction, magic, attribute floors, extra ware, availability, money, gear, qualities, or table exceptions. Alice will use them as guidance, not silent sheet edits.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        TextBox gmAllowanceBox = new()
        {
            Name = "AliceGmAllowanceTextBox",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            Watermark = "Optional GM allowances or exceptions"
        };
        DesktopShellTheme.ApplyShellTextInputTheme(gmAllowanceBox);
        global::Avalonia.Automation.AutomationProperties.SetName(gmAllowanceBox, "GM notes for Alice");
        global::Avalonia.Automation.AutomationProperties.SetHelpText(gmAllowanceBox, "Optional GM allowances, requirements, or restrictions that should guide Alice and Origin Dossier suggestions.");
        ToolTip.SetTip(gmAllowanceBox, null);

        ComboBox originArchetypeCombo = new()
        {
            Name = "AliceOriginArchetypeCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginArchetypeOptions(),
            SelectedItem = _recentWorkspaces.Count == 0 ? "Troll decker" : "Use current character"
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originArchetypeCombo);
        _originArchetypeSelection = originArchetypeCombo.SelectedItem?.ToString() ?? _originArchetypeSelection;

        ComboBox originBuildFrameCombo = new()
        {
            Name = "AliceOriginBuildFrameCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginBuildFrameOptions(),
            SelectedItem = _rulesetId == RulesetDefaults.Sr4 ? "SR4 BP" : "Use current ruleset"
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originBuildFrameCombo);
        _originBuildFrameSelection = originBuildFrameCombo.SelectedItem?.ToString() ?? _originBuildFrameSelection;

        ComboBox originPressureCombo = new()
        {
            Name = "AliceOriginPressureCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginPressureOptions(),
            SelectedItem = "Street-level survival"
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originPressureCombo);
        _originPressureSelection = originPressureCombo.SelectedItem?.ToString() ?? _originPressureSelection;

        ComboBox originGmPresetCombo = new()
        {
            Name = "AliceOriginGmRequirementPresetCombo",
            MinWidth = 260,
            ItemsSource = BuildOriginGmRequirementPresetOptions(),
            SelectedItem = "No preset"
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originGmPresetCombo);

        TextBlock originWizardGuideText = new()
        {
            Name = "AliceOriginWizardGuideText",
            Text = "Pick the runner shape first, then add any GM requirement that must steer the story. Finished characters stay safe: the dossier adds story and media, it does not rewrite the sheet.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        Border originWizardPanel = new()
        {
            Name = "AliceOriginWizardPanel",
            BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Start an origin dossier",
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    originWizardGuideText,
                    new WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                        ItemHeight = double.NaN,
                        ItemWidth = double.NaN,
                        Children =
                        {
                            CreateFieldColumn("Archetype", originArchetypeCombo),
                            CreateFieldColumn("Build frame", originBuildFrameCombo),
                            CreateFieldColumn("Story pressure", originPressureCombo),
                            CreateFieldColumn("GM requirement", originGmPresetCombo)
                        }
                    }
                }
            }
        };

        originArchetypeCombo.SelectionChanged += (_, _) =>
            _originArchetypeSelection = originArchetypeCombo.SelectedItem?.ToString() ?? _originArchetypeSelection;
        originBuildFrameCombo.SelectionChanged += (_, _) =>
            _originBuildFrameSelection = originBuildFrameCombo.SelectedItem?.ToString() ?? _originBuildFrameSelection;
        originPressureCombo.SelectionChanged += (_, _) =>
            _originPressureSelection = originPressureCombo.SelectedItem?.ToString() ?? _originPressureSelection;

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
        DesktopShellTheme.ApplyShellListBoxTheme(evidenceList);

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

        gmAllowanceBox.TextChanged += (_, _) =>
        {
            _gmAllowanceNotes = gmAllowanceBox.Text?.Trim() ?? string.Empty;
            RefreshAssistantContextSummary();
            if (string.IsNullOrWhiteSpace(promptBox.Text))
            {
                evidenceList.ItemsSource = BuildIdleEvidence(modeCombo.SelectedItem?.ToString());
            }
        };

        List<AliceConversationTurnEntry> ActiveHistory()
        {
            string mode = modeCombo.SelectedItem?.ToString() ?? BuildHelpMode;
            if (IsOriginDossierMode(mode))
            {
                return originHistory;
            }

            return string.Equals(mode, RulesCoachMode, StringComparison.Ordinal)
                ? rulesHistory
                : buildHistory;
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

        void RefreshModeGuide()
        {
            modeGuideText.Text = BuildModeGuide(modeCombo.SelectedItem?.ToString());
            settingsGuideText.Text = BuildModeSettingsGuide(modeCombo.SelectedItem?.ToString());
            originWizardPanel.IsVisible = IsOriginDossierMode(modeCombo.SelectedItem?.ToString());
        }

        void RefreshModeShortcuts()
        {
            modeShortcutRow.Children.Clear();
            foreach ((string label, string mode) in BuildModeShortcuts())
            {
                bool isPrimary = string.Equals(modeCombo.SelectedItem?.ToString(), mode, StringComparison.Ordinal);
                Button button = CreateButton(
                    label,
                    () =>
                    {
                        modeCombo.SelectedItem = mode;
                        return Task.CompletedTask;
                    },
                    isPrimary: isPrimary,
                    name: $"AliceModeShortcut_{SanitizeNameToken(mode)}");
                button.MinWidth = 0;
                button.Margin = new Thickness(0, 0, 8, 8);
                modeShortcutRow.Children.Add(button);
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
            string? selectedMode = modeCombo.SelectedItem?.ToString();
            if (IsOriginDossierMode(selectedMode))
            {
                if (_originBundle is not null)
                {
                    actionRow.Children.Add(CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.BundleDirectory), isPrimary: true, name: "AliceOriginOpenBundleFolderButton"));
                    actionRow.Children.Add(CreateButton("Open dossier PDF", () => !string.IsNullOrWhiteSpace(_originBundle.DossierPdfPath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.DossierPdfPath), name: "AliceOriginOpenDossierPdfButton"));
                    actionRow.Children.Add(CreateButton("Create portraits", RenderOriginPortraitSetAsync, name: "AliceOriginGeneratePortraitSetButton"));
                    actionRow.Children.Add(CreateButton("Create scenes", RenderOriginSceneSetAsync, name: "AliceOriginGenerateSceneSetButton"));
                    actionRow.Children.Add(CreateButton("Open default voice script", () => !string.IsNullOrWhiteSpace(_originBundle.SoundmadeseenPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.SoundmadeseenPacketPath), name: "AliceOriginOpenNarrationPacketButton"));
                    actionRow.Children.Add(CreateButton("Open alternate voice script", () => !string.IsNullOrWhiteSpace(_originBundle.UnmixrPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.UnmixrPacketPath), name: "AliceOriginOpenAlternateNarrationPacketButton"));
                    actionRow.Children.Add(CreateButton("Open render request", () => !string.IsNullOrWhiteSpace(_originBundle.MediaFactoryNarrationRequestPath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.MediaFactoryNarrationRequestPath), name: "AliceOriginOpenMediaFactoryNarrationRequestButton"));
                    actionRow.Children.Add(CreateButton("Render audiobook now", RenderOriginAudiobookNowAsync, name: "AliceOriginRenderAudiobookNowButton"));
                    actionRow.Children.Add(CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"));
                    actionRow.Children.Add(CreateButton("Render dossier video now", RenderOriginDossierVideoNowAsync, name: "AliceOriginRenderDossierVideoNowButton"));
                    if (_originBundle.PortraitCandidatePaths.Count > 0)
                    {
                        actionRow.Children.Add(CreateButton("Open selected portrait", () => !string.IsNullOrWhiteSpace(_originBundle.SelectedPortraitPath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.SelectedPortraitPath), name: "AliceOriginOpenSelectedPortraitButton"));
                    }
                    if (_originBundle.SceneCandidatePaths.Count > 0)
                    {
                        actionRow.Children.Add(CreateButton("Open selected scene", () => !string.IsNullOrWhiteSpace(_originBundle.SelectedScenePath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.SelectedScenePath), name: "AliceOriginOpenSelectedSceneButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originBundle.VideoPosterPath))
                    {
                        actionRow.Children.Add(CreateButton("Open video poster", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.VideoPosterPath), name: "AliceOriginOpenVideoPosterButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originBundle.VidBoardPacketPath))
                    {
                        actionRow.Children.Add(CreateButton("Open video plan", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.VidBoardPacketPath), name: "AliceOriginOpenVidBoardPacketButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originBundle.MediaFactoryNarrationReceiptPath))
                    {
                        actionRow.Children.Add(CreateButton("Open audiobook log", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.MediaFactoryNarrationReceiptPath), name: "AliceOriginOpenMediaFactoryNarrationReceiptButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originBundle.MediaFactoryVideoReceiptPath))
                    {
                        actionRow.Children.Add(CreateButton("Open video log", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.MediaFactoryVideoReceiptPath), name: "AliceOriginOpenMediaFactoryVideoReceiptButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originBundle.RenderedVideoPath))
                    {
                        actionRow.Children.Add(CreateButton("Open rendered video", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.RenderedVideoPath), name: "AliceOriginOpenRenderedVideoButton"));
                    }
                }
                else if (_originDraft is not null)
                {
                    actionRow.Children.Add(CreateButton("Approve story", ApproveOriginCanonAsync, isPrimary: true, name: "AliceOriginApproveCanonButton"));
                    actionRow.Children.Add(CreateButton("Render dossier PDF", RenderOriginDossierPdfAsync, name: "AliceOriginRenderDossierPdfButton"));
                    actionRow.Children.Add(CreateButton("Create portraits", RenderOriginPortraitSetAsync, name: "AliceOriginGeneratePortraitSetButton"));
                    actionRow.Children.Add(CreateButton("Create scenes", RenderOriginSceneSetAsync, name: "AliceOriginGenerateSceneSetButton"));
                    actionRow.Children.Add(CreateButton("Create default voice script", RenderOriginAudiobookPacketAsync, name: "AliceOriginGenerateAudiobookPacketButton"));
                    actionRow.Children.Add(CreateButton("Create alternate voice script", RenderOriginAlternateAudiobookPacketAsync, name: "AliceOriginGenerateAlternateAudiobookPacketButton"));
                    actionRow.Children.Add(CreateButton("Prepare render request", RenderOriginMediaFactoryRequestAsync, name: "AliceOriginGenerateMediaFactoryNarrationRequestButton"));
                    actionRow.Children.Add(CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"));
                }
                else
                {
                    actionRow.Children.Add(CreateButton("Start origin dossier", StartOriginDossierAsync, isPrimary: true, name: "AliceOriginStartDossierButton"));
                    actionRow.Children.Add(CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceAssistantOpenAccountButton"));
                }
            }
            else
            {
                if (string.Equals(selectedMode, BuildHelpMode, StringComparison.Ordinal))
                {
                    actionRow.Children.Add(CreateButton("Draft from scratch", () =>
                    {
                        promptBox.Text = "Build a complete SR4 BP troll decker from scratch. Explain legality, qualities, ware, gear, and first purchases.";
                        return AskAsync();
                    }, isPrimary: true, name: "AliceDraftFromScratchButton"));
                    actionRow.Children.Add(CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceAssistantOpenAccountButton"));
                }
                else
                {
                    actionRow.Children.Add(CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceAssistantOpenAccountButton"));
                }

                actionRow.Children.Add(CreateButton("Open web Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceAssistantOpenPublicButton"));
            }
            if (!string.IsNullOrWhiteSpace(_gmAllowanceNotes))
            {
                actionRow.Children.Add(CreateButton("Clear GM notes", () =>
                {
                    gmAllowanceBox.Text = string.Empty;
                    _gmAllowanceNotes = string.Empty;
                    RefreshAssistantContextSummary();
                    evidenceList.ItemsSource = BuildIdleEvidence(modeCombo.SelectedItem?.ToString());
                    return Task.CompletedTask;
                }, name: "AliceClearGmAllowanceButton"));
            }
            RefreshAssistantContextSummary();
            RefreshStarterPrompts();
            RefreshConversationFeed();
            RefreshModeShortcuts();
        }

        void ShowOriginBundleState(
            string statusLine,
            string answer,
            IReadOnlyList<string> evidenceLines,
            params Button[] actions)
        {
            string cleanStatusLine = HumanCopy(statusLine);
            string cleanAnswer = HumanCopy(answer);
            string[] cleanEvidenceLines = HumanLines(evidenceLines);
            statusText.Text = cleanStatusLine;
            answerText.Text = cleanAnswer;
            evidenceList.ItemsSource = cleanEvidenceLines;
            actionRow.Children.Clear();
            foreach (Button action in actions)
            {
                actionRow.Children.Add(action);
            }

            ActiveHistory().Add(BuildAssistantTurn(
                OriginDossierMode,
                cleanStatusLine,
                cleanAnswer,
                cleanEvidenceLines,
                actions.Select(action => action.Content?.ToString() ?? string.Empty)
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .Take(4)
                    .ToArray()));
            RefreshConversationFeed();
        }

        Task ApproveOriginCanonAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before approving the story.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureOriginDossierBundle();
            _originBundle = bundle;
            ShowOriginBundleState(
                "Origin story approved.",
                $"{bundle.Canon.Summary} The story is ready for dossier assets and later build guidance.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(bundle.BundleDirectory), isPrimary: true, name: "AliceOriginOpenBundleFolderButton"),
                CreateButton("Render dossier PDF", RenderOriginDossierPdfAsync, name: "AliceOriginRenderDossierPdfButton"),
                CreateButton("Create portraits", RenderOriginPortraitSetAsync, name: "AliceOriginGeneratePortraitSetButton"),
                CreateButton("Create scenes", RenderOriginSceneSetAsync, name: "AliceOriginGenerateSceneSetButton"),
                CreateButton("Create default voice script", RenderOriginAudiobookPacketAsync, name: "AliceOriginGenerateAudiobookPacketButton"),
                CreateButton("Create alternate voice script", RenderOriginAlternateAudiobookPacketAsync, name: "AliceOriginGenerateAlternateAudiobookPacketButton"),
                CreateButton("Prepare render request", RenderOriginMediaFactoryRequestAsync, name: "AliceOriginGenerateMediaFactoryNarrationRequestButton"),
                CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"));
            return Task.CompletedTask;
        }

        Task RenderOriginDossierPdfAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before rendering the dossier PDF.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureOriginDossierPdf(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Dossier PDF ready.",
                $"PDF: {Path.GetFileName(bundle.DossierPdfPath)}. MarkupGo file: {Path.GetFileName(bundle.MarkupGoPacketPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open dossier PDF", () => !string.IsNullOrWhiteSpace(bundle.DossierPdfPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.DossierPdfPath), isPrimary: true, name: "AliceOriginOpenDossierPdfButton"),
                CreateButton("Open MarkupGo packet", () => !string.IsNullOrWhiteSpace(bundle.MarkupGoPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.MarkupGoPacketPath), name: "AliceOriginOpenMarkupGoPacketButton"),
                CreateButton("Create portraits", RenderOriginPortraitSetAsync, name: "AliceOriginGeneratePortraitSetButton"),
                CreateButton("Create scenes", RenderOriginSceneSetAsync, name: "AliceOriginGenerateSceneSetButton"),
                CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"),
                CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(bundle.BundleDirectory), name: "AliceOriginOpenBundleFolderButton"));
            return Task.CompletedTask;
        }

        Task RenderOriginPortraitSetAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before creating portraits.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureOriginPortraitSet(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Portrait set ready.",
                $"Portrait candidates ready. Selected portrait: {Path.GetFileName(bundle.SelectedPortraitPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open portrait contact sheet", () => !string.IsNullOrWhiteSpace(bundle.PortraitContactSheetPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.PortraitContactSheetPath), isPrimary: true, name: "AliceOriginOpenPortraitSheetButton"),
                CreateButton("Select portrait 1", () => SelectOriginPortraitAndRefresh(0), name: "AliceOriginSelectPortrait1Button"),
                CreateButton("Select portrait 2", () => SelectOriginPortraitAndRefresh(1), name: "AliceOriginSelectPortrait2Button"),
                CreateButton("Select portrait 3", () => SelectOriginPortraitAndRefresh(2), name: "AliceOriginSelectPortrait3Button"),
                CreateButton("Select portrait 4", () => SelectOriginPortraitAndRefresh(3), name: "AliceOriginSelectPortrait4Button"));
            return Task.CompletedTask;
        }

        Task RenderOriginSceneSetAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before creating scenes.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureOriginSceneSet(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Scene set ready.",
                $"Scene candidates ready. Selected scene: {Path.GetFileName(bundle.SelectedScenePath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open scene brief", () => !string.IsNullOrWhiteSpace(bundle.SceneBriefMarkdownPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.SceneBriefMarkdownPath), isPrimary: true, name: "AliceOriginOpenSceneBriefButton"),
                CreateButton("Select scene 1", () => SelectOriginSceneAndRefresh(0), name: "AliceOriginSelectScene1Button"),
                CreateButton("Select scene 2", () => SelectOriginSceneAndRefresh(1), name: "AliceOriginSelectScene2Button"),
                CreateButton("Select scene 3", () => SelectOriginSceneAndRefresh(2), name: "AliceOriginSelectScene3Button"));
            return Task.CompletedTask;
        }

        Task RenderOriginAudiobookPacketAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before preparing the default voice script.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureSoundmadeseenNarrationPacket(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Default voice script ready.",
                $"Script: {Path.GetFileName(bundle.SoundmadeseenScriptPath)}. Soundmadeseen file: {Path.GetFileName(bundle.SoundmadeseenPacketPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open default voice packet", () => !string.IsNullOrWhiteSpace(bundle.SoundmadeseenPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.SoundmadeseenPacketPath), isPrimary: true, name: "AliceOriginOpenNarrationPacketButton"),
                CreateButton("Open default voice script", () => !string.IsNullOrWhiteSpace(bundle.SoundmadeseenScriptPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.SoundmadeseenScriptPath), name: "AliceOriginOpenNarrationScriptButton"),
                CreateButton("Create alternate voice script", RenderOriginAlternateAudiobookPacketAsync, name: "AliceOriginGenerateAlternateAudiobookPacketButton"),
                CreateButton("Prepare render request", RenderOriginMediaFactoryRequestAsync, name: "AliceOriginGenerateMediaFactoryNarrationRequestButton"),
                CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(bundle.BundleDirectory), name: "AliceOriginOpenBundleFolderButton"));
            return Task.CompletedTask;
        }

        Task RenderOriginAlternateAudiobookPacketAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before preparing the alternate voice script.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureUnmixrNarrationPacket(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Alternate voice script ready.",
                $"Script: {Path.GetFileName(bundle.UnmixrScriptPath)}. Unmixr file: {Path.GetFileName(bundle.UnmixrPacketPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open alternate voice packet", () => !string.IsNullOrWhiteSpace(bundle.UnmixrPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.UnmixrPacketPath), isPrimary: true, name: "AliceOriginOpenAlternateNarrationPacketButton"),
                CreateButton("Open alternate voice script", () => !string.IsNullOrWhiteSpace(bundle.UnmixrScriptPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.UnmixrScriptPath), name: "AliceOriginOpenAlternateNarrationScriptButton"),
                CreateButton("Create default voice script", RenderOriginAudiobookPacketAsync, name: "AliceOriginGenerateAudiobookPacketButton"),
                CreateButton("Prepare render request", RenderOriginMediaFactoryRequestAsync, name: "AliceOriginGenerateMediaFactoryNarrationRequestButton"),
                CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(bundle.BundleDirectory), name: "AliceOriginOpenBundleFolderButton"));
            return Task.CompletedTask;
        }

        Task RenderOriginMediaFactoryRequestAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before preparing the audiobook render request.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureOriginMediaFactoryNarrationRequest(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Audiobook render request ready.",
                $"Request: {Path.GetFileName(bundle.MediaFactoryNarrationRequestPath)}. Notes: {Path.GetFileName(bundle.MediaFactoryNarrationRunbookPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open render request", () => !string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRequestPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.MediaFactoryNarrationRequestPath), isPrimary: true, name: "AliceOriginOpenMediaFactoryNarrationRequestButton"),
                CreateButton("Open render notes", () => !string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRunbookPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.MediaFactoryNarrationRunbookPath), name: "AliceOriginOpenMediaFactoryNarrationRunbookButton"),
                CreateButton("Render audiobook now", RenderOriginAudiobookNowAsync, name: "AliceOriginRenderAudiobookNowButton"),
                CreateButton("Open default voice packet", () => !string.IsNullOrWhiteSpace(bundle.SoundmadeseenPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.SoundmadeseenPacketPath), name: "AliceOriginOpenNarrationPacketButton"),
                CreateButton("Open alternate voice packet", () => !string.IsNullOrWhiteSpace(bundle.UnmixrPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.UnmixrPacketPath), name: "AliceOriginOpenAlternateNarrationPacketButton"));
            return Task.CompletedTask;
        }

        Task RenderOriginDossierVideoAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before preparing the dossier video.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureOriginDossierVideoPacket(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Dossier video plan ready.",
                $"Dossier video ready. Poster: {Path.GetFileName(bundle.VideoPosterPath)}. Packet: {Path.GetFileName(bundle.VidBoardPacketPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open video poster", () => !string.IsNullOrWhiteSpace(bundle.VideoPosterPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.VideoPosterPath), isPrimary: true, name: "AliceOriginOpenVideoPosterButton"),
                CreateButton("Open storyboard", () => !string.IsNullOrWhiteSpace(bundle.VideoStoryboardPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.VideoStoryboardPath), name: "AliceOriginOpenVideoStoryboardButton"),
                CreateButton("Open vidBoard packet", () => !string.IsNullOrWhiteSpace(bundle.VidBoardPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.VidBoardPacketPath), name: "AliceOriginOpenVidBoardPacketButton"),
                CreateButton("Render dossier video now", RenderOriginDossierVideoNowAsync, name: "AliceOriginRenderDossierVideoNowButton"));
            return Task.CompletedTask;
        }

        bool SelectOriginPortraitAndRefresh(int index)
        {
            OriginDossierBundle bundle = SelectOriginPortrait(EnsureOriginDossierBundle(), index);
            ShowOriginBundleState(
                "Portrait selected.",
                $"Portrait {index + 1} will be used for future scenes and video.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open selected portrait", () => !string.IsNullOrWhiteSpace(bundle.SelectedPortraitPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.SelectedPortraitPath), isPrimary: true, name: "AliceOriginOpenSelectedPortraitButton"),
                CreateButton("Create scenes", RenderOriginSceneSetAsync, name: "AliceOriginGenerateSceneSetButton"),
                CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"));
            return true;
        }

        bool SelectOriginSceneAndRefresh(int index)
        {
            OriginDossierBundle bundle = SelectOriginScene(EnsureOriginDossierBundle(), index);
            ShowOriginBundleState(
                "Scene selected.",
                $"Scene {index + 1} will be used for the dossier video.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open selected scene", () => !string.IsNullOrWhiteSpace(bundle.SelectedScenePath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.SelectedScenePath), isPrimary: true, name: "AliceOriginOpenSelectedSceneButton"),
                CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"));
            return true;
        }

        async Task RenderOriginAudiobookNowAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before rendering the audiobook.";
                return;
            }

            OriginDossierBundle bundle = EnsureOriginMediaFactoryNarrationRequest(EnsureOriginDossierBundle());
            string receiptPath = await ExecuteOriginMediaFactoryNarrationAsync(bundle).ConfigureAwait(true);
            OriginDossierBundle updatedBundle = bundle with
            {
                MediaFactoryNarrationReceiptPath = receiptPath
            };
            _originBundle = updatedBundle;
            ShowOriginBundleState(
                "Audiobook render finished.",
                $"Audio log: {Path.GetFileName(receiptPath)}. Default and alternate voice paths were prepared for review.",
                BuildOriginBundleEvidence(updatedBundle),
                CreateButton("Open audiobook log", () => DesktopCrashRuntime.TryOpenPathInShell(receiptPath), isPrimary: true, name: "AliceOriginOpenMediaFactoryNarrationReceiptButton"),
                CreateButton("Open render request", () => !string.IsNullOrWhiteSpace(updatedBundle.MediaFactoryNarrationRequestPath) && DesktopCrashRuntime.TryOpenPathInShell(updatedBundle.MediaFactoryNarrationRequestPath), name: "AliceOriginOpenMediaFactoryNarrationRequestButton"),
                CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(updatedBundle.BundleDirectory), name: "AliceOriginOpenBundleFolderButton"));
        }

        async Task RenderOriginDossierVideoNowAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before rendering the dossier video.";
                return;
            }

            OriginDossierBundle bundle = EnsureOriginDossierVideoPacket(EnsureOriginDossierBundle());
            (string receiptPath, string renderedVideoPath) = await ExecuteOriginDossierVideoAsync(bundle).ConfigureAwait(true);
            OriginDossierBundle updatedBundle = bundle with
            {
                MediaFactoryVideoReceiptPath = receiptPath,
                RenderedVideoPath = renderedVideoPath
            };
            _originBundle = updatedBundle;
            ShowOriginBundleState(
                "Dossier video finished.",
                $"Video: {Path.GetFileName(renderedVideoPath)}. Log: {Path.GetFileName(receiptPath)}.",
                BuildOriginBundleEvidence(updatedBundle),
                CreateButton("Open rendered video", () => DesktopCrashRuntime.TryOpenPathInShell(renderedVideoPath), isPrimary: true, name: "AliceOriginOpenRenderedVideoButton"),
                CreateButton("Open video log", () => DesktopCrashRuntime.TryOpenPathInShell(receiptPath), name: "AliceOriginOpenMediaFactoryVideoReceiptButton"),
                CreateButton("Open vidBoard packet", () => !string.IsNullOrWhiteSpace(updatedBundle.VidBoardPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(updatedBundle.VidBoardPacketPath), name: "AliceOriginOpenVidBoardPacketButton"));
        }

        async Task AskAsync()
        {
            string message = promptBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                ApplyIdleState();
                statusText.Text = "Type a question before asking Alice.";
                return;
            }

            string mode = NormalizeConversationMode(modeCombo.SelectedItem?.ToString());
            ActiveHistory().Add(BuildUserTurn(message));
            RefreshConversationFeed();
            statusText.Text = $"Alice is checking {mode.ToLowerInvariant()}.";
            answerText.Text = "Waiting for the answer...";
            evidenceList.ItemsSource = Array.Empty<string>();
            actionRow.Children.Clear();

            if (IsOriginDossierMode(mode))
            {
                CharacterNarrativePacket packet = BuildNarrativePacket(message);
                CharacterNarrativeDraft originDraft = BuildOriginDraft(packet);
                _originPacket = packet;
                _originDraft = originDraft;
                _originBundle = null;
                statusText.Text = "Origin draft ready.";
                answerText.Text = HumanCopy(originDraft.Prose);
                string[] originEvidence = BuildOriginEvidence(packet, originDraft);
                evidenceList.ItemsSource = originEvidence;
                string[] originActionTitles =
                [
                    "Approve story",
                    "Render dossier PDF",
                    "Create portraits",
                    "Create scenes",
                    "Create default voice script",
                    "Create alternate voice script",
                    "Prepare render request",
                    "Create dossier video"
                ];
                ActiveHistory().Add(BuildAssistantTurn(
                    mode,
                    statusText.Text,
                    answerText.Text,
                    originEvidence,
                    originActionTitles));
                RefreshConversationFeed();
                actionRow.Children.Add(CreateButton("Approve story", ApproveOriginCanonAsync, isPrimary: true, name: "AliceOriginApproveCanonButton"));
                actionRow.Children.Add(CreateButton("Render dossier PDF", RenderOriginDossierPdfAsync, name: "AliceOriginRenderDossierPdfButton"));
                actionRow.Children.Add(CreateButton("Create portraits", RenderOriginPortraitSetAsync, name: "AliceOriginGeneratePortraitSetButton"));
                actionRow.Children.Add(CreateButton("Create scenes", RenderOriginSceneSetAsync, name: "AliceOriginGenerateSceneSetButton"));
                actionRow.Children.Add(CreateButton("Create default voice script", RenderOriginAudiobookPacketAsync, name: "AliceOriginGenerateAudiobookPacketButton"));
                actionRow.Children.Add(CreateButton("Create alternate voice script", RenderOriginAlternateAudiobookPacketAsync, name: "AliceOriginGenerateAlternateAudiobookPacketButton"));
                actionRow.Children.Add(CreateButton("Prepare render request", RenderOriginMediaFactoryRequestAsync, name: "AliceOriginGenerateMediaFactoryNarrationRequestButton"));
                actionRow.Children.Add(CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"));
                actionRow.Children.Add(CreateButton("Rewrite origin", AskAsync, name: "AliceOriginRegenerateButton"));
                promptBox.Text = string.Empty;
                return;
            }

            AiConversationTurnResponse? response = await TryAskAssistantAsync(mode, message).ConfigureAwait(true);
            if (response is null)
            {
                statusText.Text = "Alice answered locally.";
                answerText.Text = BuildLocalFallbackAnswer(mode, message);
                string[] fallbackEvidence = BuildLocalFallbackEvidence(mode);
                evidenceList.ItemsSource = fallbackEvidence;
                ActiveHistory().Add(BuildAssistantTurn(
                    mode,
                    statusText.Text,
                    answerText.Text,
                    fallbackEvidence,
                    ["Open account Alice", "Open web Alice"]));
                RefreshConversationFeed();
                actionRow.Children.Add(CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceAssistantFallbackAccountButton"));
                actionRow.Children.Add(CreateButton("Open web Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceAssistantFallbackPublicButton"));
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

        async Task StartOriginDossierAsync()
        {
            _originArchetypeSelection = originArchetypeCombo.SelectedItem?.ToString() ?? _originArchetypeSelection;
            _originBuildFrameSelection = originBuildFrameCombo.SelectedItem?.ToString() ?? _originBuildFrameSelection;
            _originPressureSelection = originPressureCombo.SelectedItem?.ToString() ?? _originPressureSelection;

            string gmPreset = originGmPresetCombo.SelectedItem?.ToString() ?? "No preset";
            if (!string.Equals(gmPreset, "No preset", StringComparison.Ordinal)
                && gmAllowanceBox.Text?.Contains(gmPreset, StringComparison.OrdinalIgnoreCase) != true)
            {
                gmAllowanceBox.Text = string.IsNullOrWhiteSpace(gmAllowanceBox.Text)
                    ? gmPreset
                    : $"{gmAllowanceBox.Text.Trim()}; {gmPreset}";
            }

            promptBox.Text = BuildOriginStarterPrompt(
                _originArchetypeSelection,
                _originBuildFrameSelection,
                _originPressureSelection,
                gmAllowanceBox.Text);
            await AskAsync().ConfigureAwait(true);
        }

        _refreshAssistantContext = ApplyIdleState;
        modeCombo.SelectionChanged += (_, _) =>
        {
            RefreshModeGuide();
            ApplyIdleState();
        };
        RefreshModeGuide();
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
                modeShortcutRow,
                modeCombo,
                modeGuideText,
                originWizardPanel,
                new Border
                {
                    Name = "AliceSettingsGuideCard",
                    BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
                    BorderThickness = new Thickness(1),
                    Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSurfaceAltBrush", "#F8FAFC"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            settingsGuideTitleText,
                            settingsGuideText
                        }
                    }
                },
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
                gmAllowanceGuideText,
                gmAllowanceBox,
                promptBox,
                statusText,
                new Border
                {
                    Name = "AliceAssistantAnswerCard",
                    BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
                    BorderThickness = new Thickness(1),
                    Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSurfaceBrush", "#FBFCFE"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Child = answerText
                },
                evidenceList,
                actionRow
            }
        };

        return CreateCard(
            "Alice",
            "Ask rules questions, compare build choices, or start an origin dossier.",
            body,
            "AliceAssistantCard",
            CreateButton("Ask Alice", AskAsync, isPrimary: true, name: "AliceAskButton"));
    }

    private Control CreateLeadHandoffCard()
    {
        BuildLabHandoffProjection? lead = _campaignSummary?.BuildLabHandoffs
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();

        if (lead is null)
        {
            return CreateCard(
                "Current build link",
                "No account build link is available yet.",
                null,
                "AliceLeadHandoffCard",
                CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceOpenAccountRailButton"),
                CreateButton("Open web Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceOpenPublicButton"));
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
                CreateDetailText(lead.RuntimeCompatibilitySummary ?? "Runtime compatibility stays attached to this build link.")
            }
        };

        return CreateCard(
            lead.Title,
            lead.Summary,
            leadDetails,
            "AliceLeadHandoffCard",
            CreateButton("Open build link", () => DesktopInstallLinkingRuntime.TryOpenRelativePortal($"/account/alice/{Uri.EscapeDataString(lead.HandoffId)}"), isPrimary: true, name: "AliceOpenLeadHandoffButton"),
            CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceOpenAccountLaneButton"));
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
        DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);

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
        DesktopShellTheme.ApplyShellListBoxTheme(handoffList);

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
                            ?? "Compare details stay attached to the selected build link.";
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
                            ?? "Campaign context stays attached to the selected build link.";
                        break;
                    default:
                        selectedDetailText.Text = selected.PlannerCoverageSummary
                            ?? selected.CampaignReturnSummary
                            ?? selected.ExchangeParitySummary
                            ?? selected.Summary;
                        selectedFollowUpText.Text = selected.NextSafeAction
                            ?? selected.RuntimeCompatibilitySummary
                            ?? "Compare details stay attached to the selected build link.";
                        break;
                }
            }
            else
            {
                _selectedHandoff = null;
                string mode = detailModeCombo.SelectedItem?.ToString() ?? "Summary";
                selectedTitleText.Text = "No selected build link";
                switch (mode)
                {
                    case "Follow-through":
                        selectedDetailText.Text = "No follow-up is available yet.";
                        selectedFollowUpText.Text = "Create or reopen a build link to inspect next actions.";
                        break;
                    case "Context":
                        selectedDetailText.Text = "No campaign context is available yet.";
                        selectedFollowUpText.Text = "Reconnect the account to inspect campaign context.";
                        break;
                    default:
                        selectedDetailText.Text = "No build-link detail is available yet.";
                        selectedFollowUpText.Text = "Choose a build link to inspect follow-up.";
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
            body.Children.Add(CreateDetailText("No account build links are available yet."));
        }

        if (HasHandoffContext)
        {
            body.Children.Add(detailModeCombo);
            body.Children.Add(handoffList);
            body.Children.Add(
                new Border
                {
                    Name = "AliceSelectedHandoffCard",
                    BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
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
            "Account builds",
            handoffs.Count == 0
                ? "Return here after the next build compare."
                : $"{handoffs.Count} account build link(s) are available.",
            body,
            "AliceAccountHandoffsCard",
            CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: HasHandoffContext, name: "AliceOpenAccountFromListButton"));
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
        DesktopShellTheme.ApplyShellComboBoxTheme(proposalModeCombo);

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
        DesktopShellTheme.ApplyShellComboBoxTheme(buildPathCombo);

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

        TextBlock buildPathGuideText = new()
        {
            Name = "AliceBuildPathGuideText",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155"),
            Text = "Summary compares the visible starter route. Runtime focuses on workspace compatibility. Warnings show watchouts before changes are applied."
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
                        selectedBuildPathWarningsText.Text = "Open or create a workspace to attach Alice proposals.";
                        break;
                    case "Warnings":
                        selectedBuildPathDetailText.Text = "No diagnostics are attached.";
                        selectedBuildPathWarningsText.Text = "Reconnect a workspace-backed preview to inspect build path watchouts.";
                        break;
                    default:
                        selectedBuildPathDetailText.Text = "No build suggestion is available yet.";
                        selectedBuildPathWarningsText.Text = "Alice will show proposal previews here once a compatible workspace and ruleset are available.";
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
            body.Children.Add(CreateDetailText("No build suggestions are available for the current desktop context."));
        }

        if (HasBuildPathContext)
        {
            body.Children.Add(proposalModeCombo);
            body.Children.Add(buildPathCombo);
            body.Children.Add(buildPathGuideText);
            body.Children.Add(
                new Border
                {
                    Name = "AliceSelectedBuildPathCard",
                    BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF"),
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
                ? "No starter proposals are available yet."
                : $"{_buildPathCandidates.Count} build path candidate(s) are available.",
            body,
            "AliceBuildPathCard",
            CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: HasBuildPathContext, name: "AliceOpenAccountFromBuildPathsButton"),
            CreateButton("Open web Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceOpenPublicFromBuildPathsButton"));
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

    private static Control CreateFieldColumn(string label, Control field)
        => new StackPanel
        {
            Width = 220,
            Margin = new Thickness(0, 0, 10, 8),
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                field
            }
        };

    private static IReadOnlyList<string> BuildOriginArchetypeOptions()
        =>
        [
            "Use current character",
            "Troll decker",
            "Street samurai",
            "Combat mage",
            "Face",
            "Rigger",
            "Adept infiltrator",
            "Technomancer",
            "Custom from prompt"
        ];

    private static IReadOnlyList<string> BuildOriginBuildFrameOptions()
        =>
        [
            "Use current ruleset",
            "SR4 BP",
            "SR5 priority",
            "SR6 priority",
            "Karma",
            "Life modules"
        ];

    private static IReadOnlyList<string> BuildOriginPressureOptions()
        =>
        [
            "Street-level survival",
            "Corporate escape",
            "Gang fallout",
            "Awakened debt",
            "Military burnout",
            "Family obligation",
            "Black clinic debt",
            "Matrix identity theft"
        ];

    private static IReadOnlyList<string> BuildOriginGmRequirementPresetOptions()
        =>
        [
            "No preset",
            "Must be addicted to an illegal drug",
            "Must be magically active",
            "Must have Logic or Intuition 2+",
            "Extra ware allowance",
            "Restricted gear allowed",
            "Must owe a dangerous contact",
            "Must hide a legal SIN"
        ];

    private static string BuildOriginStarterPrompt(string archetype, string buildFrame, string pressure, string? gmRequirement)
    {
        string runnerShape = string.Equals(archetype, "Use current character", StringComparison.Ordinal)
            ? "the current character"
            : archetype;
        string gmClause = string.IsNullOrWhiteSpace(gmRequirement)
            ? "No additional GM requirement."
            : $"GM requirement: {gmRequirement.Trim()}.";

        return $"Create an origin dossier for {runnerShape}. Build frame: {buildFrame}. Story pressure: {pressure}. {gmClause} " +
               "Explain how the qualities, ware, attributes, first gear, and first contacts came from the backstory. " +
               "Keep it useful for Alice follow-up suggestions.";
    }

    private static string HumanCopy(string? value)
        => PlayerFacingCopyHumanizer.Clean(value);

    private static string[] HumanLines(IEnumerable<string> values)
        => PlayerFacingCopyHumanizer.CleanLines(values);

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
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("shell-action");
        ToolTip.SetTip(button, label);

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
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("shell-action");
        ToolTip.SetTip(button, label);

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

        string routeType = string.Equals(mode, RulesCoachMode, StringComparison.Ordinal)
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
    {
        string normalizedMode = NormalizeConversationMode(mode);
        if (string.Equals(normalizedMode, RulesCoachMode, StringComparison.Ordinal))
        {
            return $"Ask a rules question for {_rulesetId ?? "the active ruleset"}.";
        }

        if (IsOriginDossierMode(normalizedMode))
        {
            return "Create an optional origin dossier from the current build, GM notes, and workspace context.";
        }

        return HasBuildPathContext
            ? "Ask for the next build move, and Alice will keep the suggestion reviewable."
            : "Ask about the next build move; Alice can draft a complete runner from the current settings even when no workspace is open.";
    }

    private static string BuildModeGuide(string? mode)
    {
        string normalizedMode = NormalizeConversationMode(mode);
        if (string.Equals(normalizedMode, RulesCoachMode, StringComparison.Ordinal))
        {
            return "Rules coach explains edition-specific constraints and tradeoffs. Ask about legality, qualities, ware, magic, availability, or sequencing.";
        }

        if (IsOriginDossierMode(normalizedMode))
        {
            return "Origin Dossier creates a story, portraits, scenes, audiobook, and video. Use it before creation or for a finished character.";
        }

        return "Build Help focuses on next steps. Strict avoids restricted picks, Standard allows common legal restricted choices, and Anything needs manual review. Simple stays obvious, Standard balances depth, and Deep explores tighter optimizations.";
    }

    private static string BuildModeSettingsGuide(string? mode)
    {
        string normalizedMode = NormalizeConversationMode(mode);
        if (string.Equals(normalizedMode, RulesCoachMode, StringComparison.Ordinal))
        {
            return "Use Rules coach when you need the rules explained. Ask what Strict vs Standard changes, when ware becomes a problem, whether qualities stack, or what sequence is safest.";
        }

        if (IsOriginDossierMode(normalizedMode))
        {
            return "Pick an archetype or describe the runner, add GM notes, create the story, then choose portraits, scenes, audio, or video. Finished characters are not changed.";
        }

        return "Legality controls how conservative the advice is: Strict avoids restricted picks, Standard allows common legal restricted choices, and Anything needs manual review. Complexity controls depth: Simple stays obvious, Standard balances depth, and Deep explores tighter optimizations. Ware suggestions include the rules tradeoff before anything is applied.";
    }

    private static IReadOnlyList<(string Label, string Mode)> BuildModeShortcuts()
        =>
        [
            ("Build Help", BuildHelpMode),
            ("Rules Coach", RulesCoachMode),
            ("Origin Dossier", OriginDossierMode)
        ];

    private string BuildIdleAssistantAnswer(string? mode)
    {
        string normalizedMode = NormalizeConversationMode(mode);
        if (string.Equals(normalizedMode, RulesCoachMode, StringComparison.Ordinal))
        {
            return "Try: “Explain legality Strict vs Standard rules-wise.”";
        }

        if (IsOriginDossierMode(normalizedMode))
        {
            return "Try: “Create a troll decker origin dossier with one illegal-addiction GM constraint.”";
        }

        return "Try: “Build a complete SR4 BP troll decker from scratch.”";
    }

    private string[] BuildIdleEvidence(string? mode)
    {
        List<string> lines =
        [
            !string.IsNullOrWhiteSpace(_rulesetId) ? $"Ruleset: {_rulesetId}" : "Ruleset context is not pinned yet.",
            !string.IsNullOrWhiteSpace(_workspaceId) ? $"Workspace: {_workspaceId}" : "No workspace-backed context is attached yet."
        ];

        if (IsOriginDossierMode(mode))
        {
            lines.Add(_originDraft is null
                ? "No origin dossier exists yet."
                : _originBundle is null
                    ? "An origin draft is available."
                    : "An approved origin dossier is available.");
        }
        else if (string.Equals(NormalizeConversationMode(mode), RulesCoachMode, StringComparison.Ordinal))
        {
            lines.Add(HasHandoffContext
                ? "Account build links are available for follow-up."
                : "No account link is available; Alice will answer locally.");
        }
        else
        {
            lines.Add(HasBuildPathContext
                ? $"Build paths: {_buildPathCandidates.Count}"
                : "No build path is available yet.");
        }

        if (!string.IsNullOrWhiteSpace(_gmAllowanceNotes))
        {
            lines.Add($"GM allowances: {_gmAllowanceNotes}");
        }

        return HumanLines(lines);
    }

    private string BuildLocalFallbackAnswer(string mode, string message)
    {
        if (IsOriginDossierMode(mode))
        {
            CharacterNarrativePacket packet = BuildNarrativePacket(message);
            return BuildOriginDraft(packet).Prose;
        }

        if (string.Equals(NormalizeConversationMode(mode), RulesCoachMode, StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(_rulesetId)
                ? $"Alice answered locally. This workspace is on {_rulesetId}. Use the current ruleset and workspace surface to verify '{message}', then ask again after account services are available."
                : $"Alice answered locally. Open or create a workspace first, then ask '{message}' again.";
        }

        if (_buildPathCandidates.Count > 0)
        {
            DesktopBuildPathCandidate lead = _buildPathCandidates[0];
            string leadSummary = lead.Preview?.CampaignReturnSummary
                ?? lead.Preview?.RuntimeCompatibilitySummary
                ?? "Open the proposal studio card below for the current bounded preview.";
            if (_originBundle is not null)
            {
                return HumanCopy($"Alice answered locally. Approved origin story: {_originBundle.Canon.Summary}. " +
                       $"The strongest visible candidate is '{lead.Suggestion.Title}'. {leadSummary} " +
                       $"Next additions should reinforce the approved origin instead of breaking causality. " +
                       $"{AppendGmAllowanceDetail("Any GM notes remain advisory and still require manual mechanical review")}");
            }

            if (_originDraft is not null)
            {
                return HumanCopy($"Alice answered locally. Current origin draft: {_originDraft.Summary}. " +
                       $"The strongest visible candidate is '{lead.Suggestion.Title}'. {leadSummary} " +
                       $"Treat the draft as story guidance until it is approved.");
            }

            return HumanCopy($"Alice answered locally. The strongest visible candidate is '{lead.Suggestion.Title}'. {leadSummary}");
        }

        return HumanCopy(BuildScratchCharacterAnswer(message));
    }

    private string[] BuildLocalFallbackEvidence(string mode)
    {
        if (IsOriginDossierMode(mode))
        {
            CharacterNarrativePacket packet = BuildNarrativePacket("fallback");
            return BuildOriginEvidence(packet, BuildOriginDraft(packet));
        }

        if (string.Equals(NormalizeConversationMode(mode), RulesCoachMode, StringComparison.Ordinal))
        {
            return BuildIdleEvidence(mode);
        }

        if (_buildPathCandidates.Count > 0)
        {
            List<string> lines = _buildPathCandidates
                .Take(3)
                .Select(candidate => $"{candidate.Suggestion.Title} · {candidate.Preview?.RuntimeCompatibilitySummary ?? candidate.Suggestion.TrustTier}")
                .ToList();
            if (_originBundle is not null)
            {
                lines.Insert(0, $"Approved origin story: {_originBundle.Canon.Summary}");
            }
            else if (_originDraft is not null)
            {
                lines.Insert(0, $"Origin seed: {_originDraft.Summary}");
            }

            if (!string.IsNullOrWhiteSpace(_gmAllowanceNotes))
            {
                lines.Add($"GM allowances: {_gmAllowanceNotes}");
            }

            return HumanLines(lines);
        }

        return HumanLines(BuildScratchCharacterEvidence());
    }

    private AliceAssistantContextProjection BuildAssistantContextProjection(string? mode)
    {
        WorkspaceListItem? workspace = _recentWorkspaces.FirstOrDefault();
        if (IsOriginDossierMode(mode))
        {
            string? alias = workspace?.Summary.Alias;
            string? metatype = workspace?.Summary.Metatype;
            string? buildMethod = workspace?.Summary.BuildMethod;
            string title = !string.IsNullOrWhiteSpace(alias) ? $"{alias} origin context" : "Origin context";
            string summary = !string.IsNullOrWhiteSpace(metatype)
                ? $"{metatype} · {buildMethod ?? "build"}"
                : "No explicit runner identity is available yet.";
            string detail = _selectedBuildPath?.Suggestion.Title is { Length: > 0 } buildTitle
                ? $"Lead build path: {buildTitle}. {_selectedHandoff?.NextSafeAction ?? _selectedHandoff?.Summary ?? "No account build summary is attached yet."}"
                : _selectedHandoff?.Summary ?? "The dossier uses the current ruleset, workspace shell, GM notes, and any visible Alice context.";
            if (_originBundle is not null)
            {
                detail = $"{detail} Approved origin dossier: {Path.GetFileName(_originBundle.BundleDirectory)}.";
            }
            return new AliceAssistantContextProjection(title, summary, HumanCopy(AppendGmAllowanceDetail(detail)));
        }

        if (string.Equals(NormalizeConversationMode(mode), RulesCoachMode, StringComparison.Ordinal))
        {
            return new AliceAssistantContextProjection(
                "Rules coach context",
                !string.IsNullOrWhiteSpace(_rulesetId) ? $"Pinned to {_rulesetId}" : "No explicit ruleset pin is available yet.",
                HumanCopy(AppendGmAllowanceDetail(_originDraft is null
                    ? "Alice answers from ruleset, workspace, and account context only."
                    : _originBundle is null
                        ? "An origin draft is available and can inform later guidance without changing the sheet."
                        : "An approved origin dossier is available and can inform later guidance without changing the sheet.")));
        }

        return new AliceAssistantContextProjection(
            "Build continuity",
            _selectedBuildPath?.Suggestion.Title ?? "Blank-state build start",
            HumanCopy(AppendGmAllowanceDetail(_selectedHandoff?.Summary
                ?? _selectedBuildPath?.Preview?.RuntimeCompatibilitySummary
                ?? "Alice can draft a complete runner from the current settings even when no workspace is open.")));
    }

    private CharacterNarrativePacket BuildNarrativePacket(string prompt)
    {
        WorkspaceListItem? workspace = _recentWorkspaces.FirstOrDefault();
        string alias = FirstNonEmpty(workspace?.Summary.Alias, workspace?.Summary.Name, "Unnamed runner");
        string metatype = FirstNonEmpty(workspace?.Summary.Metatype, "Unknown metatype");
        string buildMethod = FirstNonEmpty(workspace?.Summary.BuildMethod, ResolveOriginBuildFrameHint(), "Unspecified build");
        string archetypeHint = ResolveOriginArchetypeHint()
            ?? _selectedBuildPath?.Suggestion.Title
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
            !string.IsNullOrWhiteSpace(_originPressureSelection) ? $"Origin pressure: {_originPressureSelection}" : null,
            !string.IsNullOrWhiteSpace(_originBuildFrameSelection) && !string.Equals(_originBuildFrameSelection, "Use current ruleset", StringComparison.Ordinal) ? $"Origin build frame: {_originBuildFrameSelection}" : null,
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
            _selectedHandoff?.Watchouts?.Count > 0 ? string.Join(" | ", _selectedHandoff.Watchouts.Take(2)) : null,
            !string.IsNullOrWhiteSpace(_gmAllowanceNotes) ? $"GM allowances: {_gmAllowanceNotes}" : null
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
            GmAllowanceNotes: string.IsNullOrWhiteSpace(_gmAllowanceNotes) ? null : _gmAllowanceNotes,
            WorkspaceName: workspace?.Summary.Name,
            LeadBuildPathTitle: _selectedBuildPath?.Suggestion.Title,
            LeadHandoffTitle: _selectedHandoff?.Title,
            CausalityHints: causalityHints,
            StandoutSignals: standoutSignals,
            ContradictionFlags: contradictionFlags,
            RuntimeFingerprint: _selectedBuildPath?.Preview?.RuntimeFingerprint
                ?? _selectedHandoff?.RuleEnvironmentDiff?.AfterFingerprint
                ?? _selectedHandoff?.RuleEnvironmentDiff?.BeforeFingerprint);
    }

    private string? ResolveOriginArchetypeHint()
        => string.IsNullOrWhiteSpace(_originArchetypeSelection)
            || string.Equals(_originArchetypeSelection, "Use current character", StringComparison.Ordinal)
            || string.Equals(_originArchetypeSelection, "Custom from prompt", StringComparison.Ordinal)
                ? null
                : _originArchetypeSelection;

    private string? ResolveOriginBuildFrameHint()
        => string.IsNullOrWhiteSpace(_originBuildFrameSelection)
            || string.Equals(_originBuildFrameSelection, "Use current ruleset", StringComparison.Ordinal)
                ? null
                : _originBuildFrameSelection;

    private static CharacterNarrativeDraft BuildOriginDraft(CharacterNarrativePacket packet)
    {
        string sentenceOne = $"{packet.Alias} reads like a {packet.Metatype.ToLowerInvariant()} runner shaped by {packet.BuildMethod.ToLowerInvariant()} pressure rather than a clean, academic career path.";
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
            Summary: HumanCopy(summary),
            Prose: HumanCopy(string.Join(" ", [sentenceOne, sentenceTwo, sentenceThree])),
            GmHooks: HumanLines(gmHooks),
            RuntimeFingerprint: packet.RuntimeFingerprint);
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

        return HumanLines(lines);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private string BuildSeededAssistantMessage(string mode, string message)
    {
        string allowancePrefix = string.IsNullOrWhiteSpace(_gmAllowanceNotes)
            ? string.Empty
            : $"GM allowances: {_gmAllowanceNotes}{Environment.NewLine}";

        if (IsOriginDossierMode(mode))
        {
            return string.IsNullOrWhiteSpace(allowancePrefix)
                ? message
                : $"{allowancePrefix}User request: {message}";
        }

        if (_originBundle is not null)
        {
            return $"{allowancePrefix}Approved origin story summary: {_originBundle.Canon.Summary}{Environment.NewLine}Approved origin story prose: {_originBundle.Canon.Prose}{Environment.NewLine}User request: {message}";
        }

        if (_originDraft is not null)
        {
            return $"{allowancePrefix}Origin seed summary: {_originDraft.Summary}{Environment.NewLine}Origin seed prose: {_originDraft.Prose}{Environment.NewLine}User request: {message}";
        }

        return string.IsNullOrWhiteSpace(allowancePrefix)
            ? message
            : $"{allowancePrefix}User request: {message}";
    }

    private string BuildScratchCharacterAnswer(string message)
    {
        string ruleset = InferScratchRuleset(message);
        string buildTitle = _selectedBuildPath?.Suggestion.Title ?? "Custom scratch build";
        string buildMethod = InferScratchBuildMethod(message);
        string metatype = InferScratchMetatype(message);
        string role = InferScratchRole(message);
        string qualities = InferScratchQualities(role);
        string gear = InferScratchGear(role);
        string attributes = InferScratchAttributes(role);
        string skills = InferScratchSkills(role);

        return $"{buildTitle} is valid as a blank-state start. Build {metatype} on {buildMethod} for {ruleset}. " +
               $"Start from role: {role}. " +
               $"Attributes first: {attributes}. " +
               $"Core skills: {skills}. " +
               $"Early qualities or edge picks: {qualities}. " +
               $"Early gear and ware posture: {gear}. " +
               $"Use this as a complete first draft; no open character file is required.";
    }

    private string[] BuildScratchCharacterEvidence()
    {
        List<string> lines =
        [
            !string.IsNullOrWhiteSpace(_rulesetId) ? $"Ruleset: {_rulesetId}" : "Ruleset: not pinned, using the active desktop default.",
            _selectedBuildPath?.Suggestion.Title is { Length: > 0 } title
                ? $"Selected build path: {title}"
                : "Selected build path: none, using a custom scratch build.",
            "Blank-state start is supported.",
            "No open workspace is required to draft a first full build proposal."
        ];

        if (!string.IsNullOrWhiteSpace(_gmAllowanceNotes))
        {
            lines.Add($"GM allowances: {_gmAllowanceNotes}");
        }

        return lines.ToArray();
    }

    private string InferScratchRuleset(string message)
    {
        string normalized = message.ToLowerInvariant();
        if (normalized.Contains("sr4", StringComparison.Ordinal) || normalized.Contains("shadowrun 4", StringComparison.Ordinal))
        {
            return RulesetDefaults.Sr4;
        }

        if (normalized.Contains("sr5", StringComparison.Ordinal) || normalized.Contains("shadowrun 5", StringComparison.Ordinal))
        {
            return RulesetDefaults.Sr5;
        }

        if (normalized.Contains("sr6", StringComparison.Ordinal) || normalized.Contains("shadowrun 6", StringComparison.Ordinal))
        {
            return RulesetDefaults.Sr6;
        }

        return _rulesetId ?? "the active ruleset";
    }

    private static string InferScratchBuildMethod(string message)
    {
        string normalized = message.ToLowerInvariant();
        if (normalized.Contains("karma", StringComparison.Ordinal))
        {
            return "Karma";
        }

        if (normalized.Contains("sum-to-ten", StringComparison.Ordinal) || normalized.Contains("sum to ten", StringComparison.Ordinal))
        {
            return "Sum-to-Ten";
        }

        if (normalized.Contains("bp", StringComparison.Ordinal) || normalized.Contains("build point", StringComparison.Ordinal))
        {
            return "Build Points";
        }

        return "Priority";
    }

    private static string InferScratchMetatype(string message)
    {
        string normalized = message.ToLowerInvariant();
        if (normalized.Contains("troll")) return "Troll";
        if (normalized.Contains("ork")) return "Ork";
        if (normalized.Contains("dwarf")) return "Dwarf";
        if (normalized.Contains("elf")) return "Elf";
        if (normalized.Contains("human")) return "Human";
        return "Human";
    }

    private static string InferScratchRole(string message)
    {
        string normalized = message.ToLowerInvariant();
        if (normalized.Contains("decker")) return "decker";
        if (normalized.Contains("rigger")) return "rigger";
        if (normalized.Contains("face")) return "face";
        if (normalized.Contains("mage") || normalized.Contains("magician")) return "mage";
        if (normalized.Contains("adept")) return "adept";
        if (normalized.Contains("samurai")) return "street samurai";
        if (normalized.Contains("shaman")) return "shaman";
        return "generalist runner";
    }

    private static string InferScratchAttributes(string role)
        => role switch
        {
            "decker" => "LOG, INT, REA, then enough BOD/WIL to survive bad turns.",
            "rigger" => "REA, LOG, INT, then AGI if the runner may leave the vehicle.",
            "face" => "CHA, INT, WIL, then enough REA/BOD to stay standing.",
            "mage" => "MAG path first, then WIL, LOG, INT, with enough CHA or AGI to match tradition and table role.",
            "adept" => "AGI or STR first depending on offense, then REA and WIL.",
            "street samurai" => "AGI, REA, BOD, then STR or WIL depending on the combat plan.",
            "shaman" => "CHA, WIL, INT, then enough REA/BOD for table survival.",
            _ => "BOD, REA, WIL, then the primary mental or social stat for the chosen role."
        };

    private static string InferScratchSkills(string role)
        => role switch
        {
            "decker" => "Hacking, Electronic Warfare, Computer, Cybercombat, plus Perception and one social fallback.",
            "rigger" => "Pilot, Gunnery, Electronic Warfare, Mechanic coverage, plus Perception.",
            "face" => "Con, Etiquette, Negotiation, a read-the-room skill, and one reliable backup offense option.",
            "mage" => "Spellcasting, Counterspelling, Assensing, Summoning or Binding, plus one mundane survival option.",
            "adept" => "Primary attack skill, Perception, Sneaking or Athletics, then role-specific support.",
            "street samurai" => "Primary weapon skill, Perception, Sneaking, Athletics, and a clean backup weapon option.",
            "shaman" => "Spellcasting, Summoning, Assensing, Counterspelling, and one social or stealth support option.",
            _ => "Perception, one attack option, one infiltration or movement option, one social fallback, and one role-defining specialty."
        };

    private static string InferScratchQualities(string role)
        => role switch
        {
            "decker" => "qualities that improve initiative safety, matrix action economy, or planning discipline; avoid stacking flashy liabilities early.",
            "rigger" => "qualities that improve control reliability, perception, or vehicle command discipline.",
            "face" => "qualities that reinforce first-impression, negotiation reliability, or social recovery.",
            "mage" => "qualities that stabilize drain management, magical focus, or tradition identity.",
            "adept" => "qualities that support action economy, stealth, or the core combat approach.",
            "street samurai" => "qualities that reinforce toughness, initiative reliability, or target access.",
            "shaman" => "qualities that support summoning rhythm, drain safety, or spirit identity.",
            _ => "qualities that reinforce consistency and survivability before niche expression."
        };

    private static string InferScratchGear(string role)
        => role switch
        {
            "decker" => "buy the matrix core first, then initiative protection, then basic runner survival gear; ware stays conservative unless the table explicitly wants heavier augmentation.",
            "rigger" => "fund the command platform, one dependable vehicle or drone package, and repair coverage before luxuries.",
            "face" => "prioritize identity, armor that can pass socially, communication gear, and one clean escape or defense option.",
            "mage" => "stabilize magical tooling and survival basics first; ware needs strict justification because it can fight the core role.",
            "adept" => "gear should reinforce movement, concealment, and the primary offense plan before side toys.",
            "street samurai" => "weapon, armor, initiative, and medical survival first; ware should follow the chosen combat identity instead of fragmenting it.",
            "shaman" => "focus on magical tooling, survival gear, and one realistic mundane fallback.",
            _ => "buy survivability, movement, communication, and the primary role kit before flavor extras."
        };

    private string AppendGmAllowanceDetail(string detail)
        => string.IsNullOrWhiteSpace(_gmAllowanceNotes)
            ? detail
            : $"{detail} GM allowances: {_gmAllowanceNotes}.";

    private string? ResolveAssistantRuntimeFingerprint()
        => _selectedBuildPath?.Preview?.RuntimeFingerprint
            ?? _originBundle?.RuntimeFingerprint
            ?? _originDraft?.RuntimeFingerprint
            ?? _selectedHandoff?.RuleEnvironmentDiff?.AfterFingerprint
            ?? _selectedHandoff?.RuleEnvironmentDiff?.BeforeFingerprint;

    private OriginDossierBundle EnsureOriginDossierBundle()
    {
        if (_originBundle is not null)
        {
            return _originBundle;
        }

        if (_originPacket is null || _originDraft is null)
        {
            throw new InvalidOperationException("Origin canon cannot be approved without a current origin packet and draft.");
        }

        string aliasToken = string.IsNullOrWhiteSpace(_originPacket.Alias) ? "runner" : SanitizeNameToken(_originPacket.Alias).ToLowerInvariant();
        string timestampToken = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        string bundleDirectory = Path.Combine(Path.GetTempPath(), "chummer-origin-dossier-bundles", $"{timestampToken}-{aliasToken}");
        Directory.CreateDirectory(bundleDirectory);

        string canonMarkdownPath = Path.Combine(bundleDirectory, "origin-canon.md");
        string canonJsonPath = Path.Combine(bundleDirectory, "origin-canon.json");
        File.WriteAllText(canonMarkdownPath, BuildOriginCanonMarkdown(_originPacket, _originDraft));
        File.WriteAllText(canonJsonPath, JsonSerializer.Serialize(
            new
            {
                artifactKind = "origin_canon",
                originDossierBundle = true,
                approvedAtUtc = DateTimeOffset.UtcNow,
                gmAllowanceNotes = _originPacket.GmAllowanceNotes,
                packet = _originPacket,
                canon = _originDraft,
                providerLanes = new
                {
                    document = "MarkupGo",
                    portraits = "First-party render",
                    scenes = "First-party render",
                    narrationDefault = "Soundmadeseen",
                    narrationAlternate = "Unmixr",
                    dossierVideo = "vidBoard"
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        _originBundle = new OriginDossierBundle(
            Packet: _originPacket,
            Canon: _originDraft,
            ApprovedAtUtc: DateTimeOffset.UtcNow,
            BundleDirectory: bundleDirectory,
            CanonJsonPath: canonJsonPath,
            CanonMarkdownPath: canonMarkdownPath,
            DossierPdfPath: null,
            MarkupGoPacketPath: null,
            PortraitSetJsonPath: null,
            PortraitContactSheetPath: null,
            PortraitCandidatePaths: [],
            SelectedPortraitPath: null,
            SceneBriefMarkdownPath: null,
            SceneSetJsonPath: null,
            SceneCandidatePaths: [],
            SelectedScenePath: null,
            SoundmadeseenPacketPath: null,
            SoundmadeseenScriptPath: null,
            UnmixrPacketPath: null,
            UnmixrScriptPath: null,
            MediaFactoryNarrationRequestPath: null,
            MediaFactoryNarrationRunbookPath: null,
            MediaFactoryNarrationReceiptPath: null,
            VidBoardPacketPath: null,
            VideoStoryboardPath: null,
            VideoPosterPath: null,
            MediaFactoryVideoReceiptPath: null,
            RenderedVideoPath: null,
            GmAllowanceNotes: _originPacket.GmAllowanceNotes,
            RuntimeFingerprint: _originDraft.RuntimeFingerprint);
        return _originBundle;
    }

    private OriginDossierBundle EnsureOriginDossierPdf(OriginDossierBundle bundle)
    {
        if (!string.IsNullOrWhiteSpace(bundle.DossierPdfPath) && File.Exists(bundle.DossierPdfPath))
        {
            return bundle;
        }

        string pdfPath = Path.Combine(bundle.BundleDirectory, "origin-dossier.pdf");
        string markupGoPacketPath = Path.Combine(bundle.BundleDirectory, "markupgo-origin-dossier.packet.json");
        string[] dossierLines =
        [
            $"Origin summary: {HumanCopy(bundle.Canon.Summary)}",
            string.Empty,
            HumanCopy(bundle.Canon.Prose),
            string.Empty,
            $"Archetype hint: {bundle.Packet.ArchetypeHint}",
            $"Ruleset: {bundle.Packet.RulesetId}",
            $"Build method: {bundle.Packet.BuildMethod}",
            $"Metatype: {bundle.Packet.Metatype}",
            .. (!string.IsNullOrWhiteSpace(bundle.GmAllowanceNotes)
                ? new[] { $"GM notes: {HumanCopy(bundle.GmAllowanceNotes)}" }
                : Array.Empty<string>()),
            string.Empty,
            "GM hooks:",
            .. bundle.Canon.GmHooks.Select(static hook => $"- {PlayerFacingCopyHumanizer.Clean(hook)}"),
            string.Empty,
            "Open questions:",
            .. bundle.Packet.ContradictionFlags.DefaultIfEmpty("None found in the current character context.").Select(static line => $"- {PlayerFacingCopyHumanizer.Clean(line)}")
        ];
        File.WriteAllBytes(pdfPath, BuildSimplePdfDocument($"Origin Dossier · {bundle.Packet.Alias}", dossierLines));
        File.WriteAllText(markupGoPacketPath, JsonSerializer.Serialize(
            new
            {
                tool = "MarkupGo",
                artifactKind = "origin_dossier_pdf",
                approvedAtUtc = bundle.ApprovedAtUtc,
                source = "first_party_origin_canon",
                title = $"{bundle.Packet.Alias} Origin Dossier",
                sections = dossierLines,
                sourceCanon = new
                {
                    bundle.CanonMarkdownPath,
                    bundle.CanonJsonPath
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = bundle with
        {
            DossierPdfPath = pdfPath,
            MarkupGoPacketPath = markupGoPacketPath
        };
        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle EnsureOriginPortraitSet(OriginDossierBundle bundle)
    {
        bool existingPortraitsReady = !string.IsNullOrWhiteSpace(bundle.PortraitSetJsonPath)
            && !string.IsNullOrWhiteSpace(bundle.PortraitContactSheetPath)
            && File.Exists(bundle.PortraitSetJsonPath)
            && File.Exists(bundle.PortraitContactSheetPath)
            && bundle.PortraitCandidatePaths.Count == 4
            && bundle.PortraitCandidatePaths.All(File.Exists);
        if (existingPortraitsReady)
        {
            return bundle;
        }

        string portraitsDirectory = Path.Combine(bundle.BundleDirectory, "portraits");
        Directory.CreateDirectory(portraitsDirectory);

        OriginPortraitCandidate[] candidates =
        [
            new("portrait-candidate-01", "Noir Ink", "Grounded dossier portrait with low-noise contrast.", "#0F172A", "#1D4ED8", "#E2E8F0", "#93C5FD"),
            new("portrait-candidate-02", "Chrome Editorial", "Sharper editorial framing with brighter chrome accents.", "#111827", "#0F766E", "#F8FAFC", "#67E8F9"),
            new("portrait-candidate-03", "Neon Street", "Street-lit version with stronger nightlife saturation.", "#1F1630", "#7C3AED", "#F5F3FF", "#C084FC"),
            new("portrait-candidate-04", "Quiet Clinic", "Cold medical lane with controlled sterile highlights.", "#17202A", "#475569", "#F8FAFC", "#CBD5E1")
        ];

        List<string> portraitPaths = [];
        foreach (OriginPortraitCandidate candidate in candidates)
        {
            string portraitPath = Path.Combine(portraitsDirectory, $"{candidate.CandidateId}.png");
            RenderControlToPng(BuildOriginPortraitCard(bundle, candidate), 720, 960, portraitPath);
            portraitPaths.Add(portraitPath);
        }

        string contactSheetPath = Path.Combine(bundle.BundleDirectory, "origin-portrait-contact-sheet.md");
        File.WriteAllText(contactSheetPath, BuildOriginPortraitContactSheet(bundle, candidates, portraitPaths));

        string portraitSetJsonPath = Path.Combine(bundle.BundleDirectory, "origin-portrait-set.json");
        File.WriteAllText(portraitSetJsonPath, JsonSerializer.Serialize(
            new
            {
                artifactKind = "origin_dossier_portrait_set",
                approvedAtUtc = bundle.ApprovedAtUtc,
                selectedPortrait = portraitPaths[0],
                candidates = candidates.Select((candidate, index) => new
                {
                    candidate.CandidateId,
                    candidate.StyleLabel,
                    candidate.Summary,
                    file = portraitPaths[index]
                })
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = bundle with
        {
            PortraitSetJsonPath = portraitSetJsonPath,
            PortraitContactSheetPath = contactSheetPath,
            PortraitCandidatePaths = portraitPaths,
            SelectedPortraitPath = portraitPaths[0]
        };
        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle SelectOriginPortrait(OriginDossierBundle bundle, int index)
    {
        bundle = EnsureOriginPortraitSet(bundle);
        if (index < 0 || index >= bundle.PortraitCandidatePaths.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        OriginDossierBundle updated = bundle with
        {
            SelectedPortraitPath = bundle.PortraitCandidatePaths[index],
            SceneBriefMarkdownPath = null,
            SceneSetJsonPath = null,
            SceneCandidatePaths = [],
            SelectedScenePath = null,
            VidBoardPacketPath = null,
            VideoStoryboardPath = null,
            VideoPosterPath = null,
            MediaFactoryVideoReceiptPath = null,
            RenderedVideoPath = null
        };

        if (!string.IsNullOrWhiteSpace(updated.PortraitSetJsonPath))
        {
            File.WriteAllText(updated.PortraitSetJsonPath, JsonSerializer.Serialize(
                new
                {
                    artifactKind = "origin_dossier_portrait_set",
                    approvedAtUtc = updated.ApprovedAtUtc,
                    selectedPortrait = updated.SelectedPortraitPath,
                    candidates = updated.PortraitCandidatePaths
                },
                new JsonSerializerOptions { WriteIndented = true }));
        }

        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle EnsureOriginSceneSet(OriginDossierBundle bundle)
    {
        bundle = EnsureOriginPortraitSet(bundle);
        bool existingScenesReady = !string.IsNullOrWhiteSpace(bundle.SceneSetJsonPath)
            && !string.IsNullOrWhiteSpace(bundle.SceneBriefMarkdownPath)
            && File.Exists(bundle.SceneSetJsonPath)
            && File.Exists(bundle.SceneBriefMarkdownPath)
            && bundle.SceneCandidatePaths.Count == 3
            && bundle.SceneCandidatePaths.All(File.Exists);
        if (existingScenesReady)
        {
            return bundle;
        }

        string scenesDirectory = Path.Combine(bundle.BundleDirectory, "scenes");
        Directory.CreateDirectory(scenesDirectory);
        string portraitPath = bundle.SelectedPortraitPath ?? bundle.PortraitCandidatePaths.First();
        OriginSceneCandidate[] candidates =
        [
            new("scene-candidate-01", "Turning Point", "The moment the runner learned the current loadout was not optional anymore.", "#09131F", "#1D4ED8"),
            new("scene-candidate-02", "Clinic Memory", "A sterile upgrade lane that explains the cost of implants and quality drift.", "#111827", "#0F766E"),
            new("scene-candidate-03", "Before the Run", "A quiet preparation frame just before stepping onto the current role path.", "#1E1B4B", "#7C3AED")
        ];

        List<string> scenePaths = [];
        foreach (OriginSceneCandidate candidate in candidates)
        {
            string scenePath = Path.Combine(scenesDirectory, $"{candidate.SceneId}.png");
            RenderControlToPng(BuildOriginSceneCard(bundle, candidate, portraitPath), 1280, 720, scenePath);
            scenePaths.Add(scenePath);
        }

        string sceneBriefMarkdownPath = Path.Combine(bundle.BundleDirectory, "origin-scene-brief.md");
        File.WriteAllText(sceneBriefMarkdownPath, BuildOriginSceneBrief(bundle, candidates));

        string sceneSetJsonPath = Path.Combine(bundle.BundleDirectory, "origin-scene-set.json");
        File.WriteAllText(sceneSetJsonPath, JsonSerializer.Serialize(
            new
            {
                artifactKind = "origin_dossier_scene_set",
                approvedAtUtc = bundle.ApprovedAtUtc,
                portraitPath,
                selectedScene = scenePaths[0],
                candidates = candidates.Select((candidate, index) => new
                {
                    candidate.SceneId,
                    candidate.Title,
                    candidate.Summary,
                    file = scenePaths[index]
                })
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = bundle with
        {
            SceneBriefMarkdownPath = sceneBriefMarkdownPath,
            SceneSetJsonPath = sceneSetJsonPath,
            SceneCandidatePaths = scenePaths,
            SelectedScenePath = scenePaths[0]
        };
        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle SelectOriginScene(OriginDossierBundle bundle, int index)
    {
        bundle = EnsureOriginSceneSet(bundle);
        if (index < 0 || index >= bundle.SceneCandidatePaths.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        OriginDossierBundle updated = bundle with
        {
            SelectedScenePath = bundle.SceneCandidatePaths[index],
            VidBoardPacketPath = null,
            VideoStoryboardPath = null,
            VideoPosterPath = null,
            MediaFactoryVideoReceiptPath = null,
            RenderedVideoPath = null
        };

        if (!string.IsNullOrWhiteSpace(updated.SceneSetJsonPath))
        {
            File.WriteAllText(updated.SceneSetJsonPath, JsonSerializer.Serialize(
                new
                {
                    artifactKind = "origin_dossier_scene_set",
                    approvedAtUtc = updated.ApprovedAtUtc,
                    portraitPath = updated.SelectedPortraitPath,
                    selectedScene = updated.SelectedScenePath,
                    candidates = updated.SceneCandidatePaths
                },
                new JsonSerializerOptions { WriteIndented = true }));
        }

        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle EnsureOriginDossierVideoPacket(OriginDossierBundle bundle)
    {
        bundle = EnsureOriginDossierPdf(EnsureOriginSceneSet(bundle));
        if (!string.IsNullOrWhiteSpace(bundle.VidBoardPacketPath)
            && !string.IsNullOrWhiteSpace(bundle.VideoStoryboardPath)
            && !string.IsNullOrWhiteSpace(bundle.VideoPosterPath)
            && File.Exists(bundle.VidBoardPacketPath)
            && File.Exists(bundle.VideoStoryboardPath)
            && File.Exists(bundle.VideoPosterPath))
        {
            return bundle;
        }

        string storyboardPath = Path.Combine(bundle.BundleDirectory, "origin-dossier-video.storyboard.md");
        string packetPath = Path.Combine(bundle.BundleDirectory, "vidboard-origin-dossier.packet.json");
        string posterPath = Path.Combine(bundle.BundleDirectory, "origin-dossier-video-poster.png");

        File.WriteAllText(storyboardPath, BuildOriginVideoStoryboard(bundle));
        RenderControlToPng(BuildOriginVideoPoster(bundle), 1280, 720, posterPath);
        File.WriteAllText(packetPath, JsonSerializer.Serialize(
            new
            {
                tool = "vidBoard",
                artifactKind = "origin_dossier_video",
                approvedAtUtc = bundle.ApprovedAtUtc,
                source = "first_party_origin_canon",
                title = $"{bundle.Packet.Alias} Origin Dossier",
                durationTargetSeconds = 60,
                posterPath,
                storyboardPath,
                selectedPortraitPath = bundle.SelectedPortraitPath,
                selectedScenePath = bundle.SelectedScenePath,
                sourceCanon = new
                {
                    bundle.CanonMarkdownPath,
                    bundle.CanonJsonPath,
                    bundle.DossierPdfPath,
                    bundle.MediaFactoryNarrationReceiptPath
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = bundle with
        {
            VidBoardPacketPath = packetPath,
            VideoStoryboardPath = storyboardPath,
            VideoPosterPath = posterPath,
            MediaFactoryVideoReceiptPath = null,
            RenderedVideoPath = null
        };
        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle EnsureSoundmadeseenNarrationPacket(OriginDossierBundle bundle)
    {
        if (!string.IsNullOrWhiteSpace(bundle.SoundmadeseenPacketPath)
            && !string.IsNullOrWhiteSpace(bundle.SoundmadeseenScriptPath)
            && File.Exists(bundle.SoundmadeseenPacketPath)
            && File.Exists(bundle.SoundmadeseenScriptPath))
        {
            return bundle;
        }

        string scriptPath = Path.Combine(bundle.BundleDirectory, "soundmadeseen-origin-reading.txt");
        string packetPath = Path.Combine(bundle.BundleDirectory, "soundmadeseen-origin-reading.packet.json");
        string script = BuildSoundmadeseenNarrationScript(bundle);
        File.WriteAllText(scriptPath, script);
        File.WriteAllText(packetPath, JsonSerializer.Serialize(
            new
            {
                tool = "Soundmadeseen",
                artifactKind = "origin_audiobook_brief",
                approvedAtUtc = bundle.ApprovedAtUtc,
                source = "first_party_origin_canon",
                title = $"{bundle.Packet.Alias} Origin Reading",
                narrationMode = "operator_reading",
                scriptPath,
                durationTargetSeconds = 75,
                sourceCanon = new
                {
                    bundle.CanonMarkdownPath,
                    bundle.CanonJsonPath
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = bundle with
        {
            SoundmadeseenPacketPath = packetPath,
            SoundmadeseenScriptPath = scriptPath
        };
        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle EnsureUnmixrNarrationPacket(OriginDossierBundle bundle)
    {
        if (!string.IsNullOrWhiteSpace(bundle.UnmixrPacketPath)
            && !string.IsNullOrWhiteSpace(bundle.UnmixrScriptPath)
            && File.Exists(bundle.UnmixrPacketPath)
            && File.Exists(bundle.UnmixrScriptPath))
        {
            return bundle;
        }

        string scriptPath = Path.Combine(bundle.BundleDirectory, "unmixr-origin-reading.txt");
        string packetPath = Path.Combine(bundle.BundleDirectory, "unmixr-origin-reading.packet.json");
        string script = BuildUnmixrNarrationScript(bundle);
        File.WriteAllText(scriptPath, script);
        File.WriteAllText(packetPath, JsonSerializer.Serialize(
            new
            {
                tool = "Unmixr",
                artifactKind = "origin_audiobook_alternate_voice",
                approvedAtUtc = bundle.ApprovedAtUtc,
                source = "first_party_origin_canon",
                title = $"{bundle.Packet.Alias} Origin Reading",
                narrationMode = "alternate_voice_reading",
                scriptPath,
                durationTargetSeconds = 75,
                sourceCanon = new
                {
                    bundle.CanonMarkdownPath,
                    bundle.CanonJsonPath
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = bundle with
        {
            UnmixrPacketPath = packetPath,
            UnmixrScriptPath = scriptPath
        };
        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle EnsureOriginMediaFactoryNarrationRequest(OriginDossierBundle bundle)
    {
        bundle = EnsureSoundmadeseenNarrationPacket(bundle);
        bundle = EnsureUnmixrNarrationPacket(bundle);

        if (!string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRequestPath)
            && !string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRunbookPath)
            && File.Exists(bundle.MediaFactoryNarrationRequestPath)
            && File.Exists(bundle.MediaFactoryNarrationRunbookPath))
        {
            return bundle;
        }

        string requestPath = Path.Combine(bundle.BundleDirectory, "media-factory-origin-audiobook.request.json");
        string runbookPath = Path.Combine(bundle.BundleDirectory, "media-factory-origin-audiobook.runbook.md");
        string renderRequestId = $"origin-dossier-audiobook-{SanitizeNameToken(bundle.Packet.Alias).ToLowerInvariant()}-{bundle.ApprovedAtUtc.UtcDateTime:yyyyMMddHHmmss}";
        string approvedOriginPacketId = $"origin-dossier:{SanitizeNameToken(bundle.Packet.Alias).ToLowerInvariant()}:{bundle.ApprovedAtUtc.UtcDateTime:yyyyMMddHHmmss}";
        string revisionId = $"origin-canon:{bundle.RuntimeFingerprint ?? "unversioned"}";

        File.WriteAllText(requestPath, JsonSerializer.Serialize(
            new
            {
                renderRequestId,
                artifactKind = "origin_dossier_bundle_audiobook_render_request",
                ownerRepo = "chummer6-media-factory",
                source = "chummer-presentation.desktop-alice",
                approvedAtUtc = bundle.ApprovedAtUtc,
                requestedAtUtc = DateTimeOffset.UtcNow,
                approvedOriginPacketId,
                originRevisionId = revisionId,
                canonicalBundle = new
                {
                    bundle.BundleDirectory,
                    bundle.CanonMarkdownPath,
                    bundle.CanonJsonPath,
                    bundle.DossierPdfPath
                },
                providerLanes = new
                {
                    @default = "Soundmadeseen",
                    alternate = "Unmixr"
                },
                narrationArtifacts = new object[]
                {
                    new
                    {
                        role = "audio",
                        provider = "Soundmadeseen",
                        providerState = "promoted",
                        outputFormat = "mp3",
                        variant = "default_voice",
                        companionRef = $"{approvedOriginPacketId}/audio/default",
                        scriptPath = bundle.SoundmadeseenScriptPath,
                        packetPath = bundle.SoundmadeseenPacketPath,
                        captionRefs = new[] {$"{approvedOriginPacketId}/caption/default"},
                        previewRefs = new[] {$"{approvedOriginPacketId}/preview/default"}
                    },
                    new
                    {
                        role = "audio",
                        provider = "Unmixr",
                        providerState = "candidate",
                        outputFormat = "mp3",
                        variant = "alternate_voice",
                        companionRef = $"{approvedOriginPacketId}/audio/alternate",
                        scriptPath = bundle.UnmixrScriptPath,
                        packetPath = bundle.UnmixrPacketPath,
                        captionRefs = new[] {$"{approvedOriginPacketId}/caption/alternate"},
                        previewRefs = new[] {$"{approvedOriginPacketId}/preview/alternate"}
                    }
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        File.WriteAllText(runbookPath, BuildOriginMediaFactoryNarrationRunbook(bundle, requestPath));

        OriginDossierBundle updated = bundle with
        {
            MediaFactoryNarrationRequestPath = requestPath,
            MediaFactoryNarrationRunbookPath = runbookPath,
            MediaFactoryNarrationReceiptPath = null
        };
        _originBundle = updated;
        return updated;
    }

    private static IReadOnlyList<string> BuildOriginBundleEvidence(OriginDossierBundle bundle)
    {
        List<string> lines =
        [
            $"Dossier folder: {bundle.BundleDirectory}",
            $"Story: {Path.GetFileName(bundle.CanonMarkdownPath)}",
            $"Story data: {Path.GetFileName(bundle.CanonJsonPath)}",
            "Document: MarkupGo",
            "Default voice: Soundmadeseen",
            "Alternate voice: Unmixr"
        ];

        if (!string.IsNullOrWhiteSpace(bundle.GmAllowanceNotes))
        {
            lines.Add($"GM allowances: {bundle.GmAllowanceNotes}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.DossierPdfPath))
        {
            lines.Add($"PDF: {Path.GetFileName(bundle.DossierPdfPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MarkupGoPacketPath))
        {
            lines.Add($"MarkupGo file: {Path.GetFileName(bundle.MarkupGoPacketPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.PortraitSetJsonPath))
        {
            lines.Add($"Portrait set: {Path.GetFileName(bundle.PortraitSetJsonPath)}");
        }

        if (bundle.PortraitCandidatePaths.Count > 0)
        {
            lines.Add($"Portrait candidates: {bundle.PortraitCandidatePaths.Count}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.SelectedPortraitPath))
        {
            lines.Add($"Selected portrait: {Path.GetFileName(bundle.SelectedPortraitPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.SceneBriefMarkdownPath))
        {
            lines.Add($"Scene brief: {Path.GetFileName(bundle.SceneBriefMarkdownPath)}");
        }

        if (bundle.SceneCandidatePaths.Count > 0)
        {
            lines.Add($"Scene candidates: {bundle.SceneCandidatePaths.Count}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.SelectedScenePath))
        {
            lines.Add($"Selected scene: {Path.GetFileName(bundle.SelectedScenePath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.SoundmadeseenScriptPath))
        {
            lines.Add($"Default voice script: {Path.GetFileName(bundle.SoundmadeseenScriptPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.SoundmadeseenPacketPath))
        {
            lines.Add($"Soundmadeseen file: {Path.GetFileName(bundle.SoundmadeseenPacketPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.UnmixrScriptPath))
        {
            lines.Add($"Alternate voice script: {Path.GetFileName(bundle.UnmixrScriptPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.UnmixrPacketPath))
        {
            lines.Add($"Unmixr file: {Path.GetFileName(bundle.UnmixrPacketPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRequestPath))
        {
            lines.Add($"Render request: {Path.GetFileName(bundle.MediaFactoryNarrationRequestPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRunbookPath))
        {
            lines.Add($"Render notes: {Path.GetFileName(bundle.MediaFactoryNarrationRunbookPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationReceiptPath))
        {
            lines.Add($"Audio log: {Path.GetFileName(bundle.MediaFactoryNarrationReceiptPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.VideoStoryboardPath))
        {
            lines.Add($"Video storyboard: {Path.GetFileName(bundle.VideoStoryboardPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.VidBoardPacketPath))
        {
            lines.Add($"vidBoard file: {Path.GetFileName(bundle.VidBoardPacketPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.VideoPosterPath))
        {
            lines.Add($"Video poster: {Path.GetFileName(bundle.VideoPosterPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MediaFactoryVideoReceiptPath))
        {
            lines.Add($"Video log: {Path.GetFileName(bundle.MediaFactoryVideoReceiptPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.RenderedVideoPath))
        {
            lines.Add($"Rendered video: {Path.GetFileName(bundle.RenderedVideoPath)}");
        }

        return HumanLines(lines);
    }

    private static async Task<string> ExecuteOriginMediaFactoryNarrationAsync(OriginDossierBundle bundle)
    {
        if (string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRequestPath))
        {
            throw new InvalidOperationException("Origin dossier bundle is missing the media-factory narration request.");
        }

        string mediaFactoryRepoRoot = ResolveMediaFactoryRepoRoot();
        string narrationCliProject = ResolveMediaFactoryCliProject(MediaFactoryNarrationCliProjectEnv, MediaFactoryNarrationCliProjectRelative);
        if (!File.Exists(narrationCliProject))
        {
            throw new FileNotFoundException("Origin dossier narration CLI project was not found.", narrationCliProject);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = mediaFactoryRepoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(narrationCliProject);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");
        startInfo.Environment[OriginNarrationRequestPathEnv] = bundle.MediaFactoryNarrationRequestPath;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the origin dossier narration render CLI.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        string standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Origin dossier narration render failed with exit code {process.ExitCode}: {standardError.Trim()}");
        }

        string receiptPath = standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()
            ?? string.Empty;
        if (receiptPath.Length == 0 || !File.Exists(receiptPath))
        {
            throw new InvalidOperationException("Origin dossier narration render did not return a valid receipt path.");
        }

        return receiptPath;
    }

    private static async Task<(string ReceiptPath, string RenderedVideoPath)> ExecuteOriginDossierVideoAsync(OriginDossierBundle bundle)
    {
        if (string.IsNullOrWhiteSpace(bundle.VidBoardPacketPath))
        {
            throw new InvalidOperationException("Origin dossier bundle is missing the vidBoard packet.");
        }

        string mediaFactoryRepoRoot = ResolveMediaFactoryRepoRoot();
        string videoCliProject = ResolveMediaFactoryCliProject(MediaFactoryVideoCliProjectEnv, MediaFactoryVideoCliProjectRelative);
        if (!File.Exists(videoCliProject))
        {
            throw new FileNotFoundException("Origin dossier video CLI project was not found.", videoCliProject);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = mediaFactoryRepoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(videoCliProject);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");
        startInfo.Environment[OriginVideoRequestPathEnv] = bundle.VidBoardPacketPath;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the origin dossier video render CLI.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        string standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Origin dossier video render failed with exit code {process.ExitCode}: {standardError.Trim()}");
        }

        string receiptPath = standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()
            ?? string.Empty;
        if (receiptPath.Length == 0 || !File.Exists(receiptPath))
        {
            throw new InvalidOperationException("Origin dossier video render did not return a valid receipt path.");
        }

        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath).ConfigureAwait(false));
        if (!document.RootElement.TryGetProperty("renderedVideoPath", out JsonElement renderedVideoPathElement)
            || renderedVideoPathElement.ValueKind is not JsonValueKind.String)
        {
            throw new InvalidOperationException("Origin dossier video receipt is missing renderedVideoPath.");
        }

        string renderedVideoPath = renderedVideoPathElement.GetString()?.Trim() ?? string.Empty;
        if (renderedVideoPath.Length == 0 || !File.Exists(renderedVideoPath))
        {
            throw new InvalidOperationException("Origin dossier video receipt did not return a valid rendered video path.");
        }

        return (receiptPath, renderedVideoPath);
    }

    private static string ResolveMediaFactoryCliProject(string environmentVariable, string relativePath)
    {
        string? configured = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured.Trim());
        }

        return Path.GetFullPath(Path.Combine(ResolveMediaFactoryRepoRoot(), relativePath));
    }

    private static string ResolveMediaFactoryRepoRoot()
    {
        string? configured = Environment.GetEnvironmentVariable(MediaFactoryRepoRootEnv);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured.Trim());
        }

        foreach (string root in MediaFactorySearchRoots())
        {
            foreach (string relativeCandidate in new[] { "repos/chummer-media-factory", "chummer-media-factory" })
            {
                string candidate = Path.GetFullPath(Path.Combine(root, relativeCandidate));
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "chummer-media-factory"));
    }

    private static IEnumerable<string> MediaFactorySearchRoots()
    {
        foreach (string seed in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            DirectoryInfo? current = new(seed);
            while (current is not null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }
    }

    private static void RenderControlToPng(Control control, int width, int height, string outputPath)
    {
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0d, 0d, width, height));
        control.InvalidateMeasure();
        control.InvalidateArrange();
        control.InvalidateVisual();
        using RenderTargetBitmap bitmap = new(new PixelSize(width, height), new Vector(96d, 96d));
        bitmap.Render(control);
        using FileStream stream = File.Create(outputPath);
        bitmap.Save(stream);
    }

    private static Control BuildOriginPortraitCard(OriginDossierBundle bundle, OriginPortraitCandidate candidate)
    {
        Border accentPanel = new()
        {
            Background = new SolidColorBrush(Color.Parse(candidate.AccentHex)),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = candidate.StyleLabel,
                        FontSize = 28,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse(candidate.ForegroundHex))
                    },
                    new TextBlock
                    {
                        Text = bundle.Packet.Alias,
                        FontSize = 44,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse(candidate.HighlightHex))
                    },
                    new TextBlock
                    {
                        Text = candidate.Summary,
                        FontSize = 18,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse(candidate.ForegroundHex))
                    }
                }
            }
        };

        Canvas silhouette = new()
        {
            Width = 260,
            Height = 360,
            Children =
            {
                new Ellipse
                {
                    Width = 132,
                    Height = 132,
                    Fill = new SolidColorBrush(Color.Parse(candidate.HighlightHex)),
                    [Canvas.LeftProperty] = 64d,
                    [Canvas.TopProperty] = 28d
                },
                new Ellipse
                {
                    Width = 88,
                    Height = 88,
                    Fill = new SolidColorBrush(Color.Parse(candidate.BackgroundHex)),
                    [Canvas.LeftProperty] = 86d,
                    [Canvas.TopProperty] = 48d
                },
                new Border
                {
                    Width = 180,
                    Height = 172,
                    Background = new SolidColorBrush(Color.Parse(candidate.HighlightHex)),
                    CornerRadius = new CornerRadius(28, 28, 12, 12),
                    [Canvas.LeftProperty] = 40d,
                    [Canvas.TopProperty] = 156d
                },
                new Rectangle
                {
                    Width = 128,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.Parse(candidate.ForegroundHex)),
                    RadiusX = 4,
                    RadiusY = 4,
                    [Canvas.LeftProperty] = 66d,
                    [Canvas.TopProperty] = 212d
                },
                new Rectangle
                {
                    Width = 104,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.Parse(candidate.ForegroundHex)),
                    RadiusX = 4,
                    RadiusY = 4,
                    [Canvas.LeftProperty] = 78d,
                    [Canvas.TopProperty] = 232d
                }
            }
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(candidate.BackgroundHex)),
            Padding = new Thickness(28),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("1.2*,0.9*"),
                Children =
                {
                    accentPanel,
                    new Border
                    {
                        Padding = new Thickness(24, 18, 0, 18),
                        Child = new StackPanel
                        {
                            Spacing = 12,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = $"{bundle.Packet.Metatype} · {bundle.Packet.BuildMethod}",
                                    FontSize = 18,
                                    Foreground = new SolidColorBrush(Color.Parse(candidate.ForegroundHex))
                                },
                                silhouette,
                                new TextBlock
                                {
                                    Text = bundle.Canon.Summary,
                                    TextWrapping = TextWrapping.Wrap,
                                    FontSize = 16,
                                    Foreground = new SolidColorBrush(Color.Parse(candidate.ForegroundHex))
                                }
                            }
                        },
                        [Grid.ColumnProperty] = 1
                    }
                }
            }
        };
    }

    private static Control BuildOriginSceneCard(OriginDossierBundle bundle, OriginSceneCandidate candidate, string portraitPath)
    {
        Bitmap portraitBitmap = new(portraitPath);
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(candidate.BackgroundHex)),
            Padding = new Thickness(28),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("1.4*,0.85*"),
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = candidate.Title,
                                FontSize = 34,
                                FontWeight = FontWeight.Bold,
                                Foreground = Brushes.White
                            },
                            new TextBlock
                            {
                                Text = candidate.Summary,
                                FontSize = 18,
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = new SolidColorBrush(Color.Parse("#D8E3F0"))
                            }
                        }
                    },
                    new Border
                    {
                        [Grid.RowProperty] = 1,
                        Background = new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(1d, 1d, RelativeUnit.Relative),
                            GradientStops =
                            [
                                new GradientStop(Color.Parse(candidate.AccentHex), 0d),
                                new GradientStop(Color.Parse(candidate.BackgroundHex), 1d)
                            ]
                        },
                        CornerRadius = new CornerRadius(18),
                        Padding = new Thickness(22),
                        Child = new StackPanel
                        {
                            Spacing = 12,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = bundle.Canon.Prose,
                                    TextWrapping = TextWrapping.Wrap,
                                    FontSize = 20,
                                    Foreground = Brushes.White
                                },
                                new TextBlock
                                {
                                    Text = $"GM hook: {bundle.Canon.GmHooks.FirstOrDefault() ?? "Keep the character consequence-first."}",
                                    TextWrapping = TextWrapping.Wrap,
                                    FontSize = 16,
                                    Foreground = new SolidColorBrush(Color.Parse("#DBEAFE"))
                                }
                            }
                        }
                    },
                    new Border
                    {
                        [Grid.ColumnProperty] = 1,
                        [Grid.RowSpanProperty] = 2,
                        Margin = new Thickness(22, 0, 0, 0),
                        Background = new SolidColorBrush(Color.Parse("#0B1220")),
                        CornerRadius = new CornerRadius(16),
                        Padding = new Thickness(18),
                        Child = new StackPanel
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Image
                                {
                                    Source = portraitBitmap,
                                    Stretch = Stretch.UniformToFill,
                                    Width = 300,
                                    Height = 420
                                },
                                new TextBlock
                                {
                                    Text = $"{bundle.Packet.Alias} · {bundle.Packet.ArchetypeHint}",
                                    FontSize = 18,
                                    Foreground = Brushes.White,
                                    TextWrapping = TextWrapping.Wrap
                                },
                                new TextBlock
                                {
                                    Text = $"{bundle.Packet.RulesetId} · {bundle.Packet.Metatype}",
                                    FontSize = 15,
                                    Foreground = new SolidColorBrush(Color.Parse("#94A3B8"))
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static Control BuildOriginVideoPoster(OriginDossierBundle bundle)
    {
        string scenePath = bundle.SelectedScenePath ?? bundle.SceneCandidatePaths.First();
        Bitmap sceneBitmap = new(scenePath);
        return new Border
        {
            Background = Brushes.Black,
            Child = new Grid
            {
                Children =
                {
                    new Image
                    {
                        Source = sceneBitmap,
                        Stretch = Stretch.UniformToFill
                    },
                    new Border
                    {
                        Background = new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0d, 1d, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
                            GradientStops =
                            [
                                new GradientStop(Color.Parse("#DD020617"), 0d),
                                new GradientStop(Color.Parse("#00020617"), 1d)
                            ]
                        }
                    },
                    new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(42),
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Origin Dossier",
                                FontSize = 22,
                                Foreground = new SolidColorBrush(Color.Parse("#93C5FD"))
                            },
                            new TextBlock
                            {
                                Text = bundle.Packet.Alias,
                                FontSize = 42,
                                FontWeight = FontWeight.Bold,
                                Foreground = Brushes.White
                            },
                            new TextBlock
                            {
                                Text = bundle.Canon.Summary,
                                FontSize = 18,
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = new SolidColorBrush(Color.Parse("#E2E8F0"))
                            }
                        }
                    }
                }
            }
        };
    }

    private static string BuildOriginPortraitContactSheet(
        OriginDossierBundle bundle,
        IReadOnlyList<OriginPortraitCandidate> candidates,
        IReadOnlyList<string> portraitPaths)
    {
        StringBuilder builder = new();
        builder.AppendLine($"# Origin Portrait Contact Sheet · {bundle.Packet.Alias}");
        builder.AppendLine();
        builder.AppendLine("Choose one portrait before creating scenes or video.");
        builder.AppendLine();
        for (int index = 0; index < candidates.Count; index++)
        {
            builder.AppendLine($"- {candidates[index].StyleLabel}: {portraitPaths[index]}");
            builder.AppendLine($"  - {HumanCopy(candidates[index].Summary)}");
        }
        return builder.ToString().TrimEnd();
    }

    private static string BuildOriginSceneBrief(OriginDossierBundle bundle, IReadOnlyList<OriginSceneCandidate> candidates)
    {
        StringBuilder builder = new();
        builder.AppendLine($"# Origin Scene Brief · {bundle.Packet.Alias}");
        builder.AppendLine();
        builder.AppendLine("Scenes frame one consequential moment from the approved origin story.");
        builder.AppendLine();
        foreach (OriginSceneCandidate candidate in candidates)
        {
            builder.AppendLine($"## {candidate.Title}");
            builder.AppendLine(HumanCopy(candidate.Summary));
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd();
    }

    private static string BuildOriginVideoStoryboard(OriginDossierBundle bundle)
    {
        StringBuilder builder = new();
        builder.AppendLine($"# Origin Dossier Video Storyboard · {bundle.Packet.Alias}");
        builder.AppendLine();
        builder.AppendLine("1. Title card");
        builder.AppendLine($"   - {bundle.Packet.Alias} · {bundle.Packet.Metatype} · {bundle.Packet.BuildMethod}");
        builder.AppendLine("2. Portrait reveal");
        builder.AppendLine("3. Selected scene hold");
        builder.AppendLine("4. Narrated origin summary");
        builder.AppendLine("5. Build implication card");
        builder.AppendLine("6. Close card");
        builder.AppendLine();
        builder.AppendLine("Narration");
        builder.AppendLine("- Default: Soundmadeseen");
        builder.AppendLine("- Alternate: Unmixr");
        builder.AppendLine("- Visual packet: vidBoard");
        return builder.ToString().TrimEnd();
    }

    private static string BuildOriginCanonMarkdown(CharacterNarrativePacket packet, CharacterNarrativeDraft draft)
    {
        StringBuilder builder = new();
        builder.AppendLine($"# {packet.Alias} Origin Story");
        builder.AppendLine();
        builder.AppendLine($"- Ruleset: {packet.RulesetId}");
        builder.AppendLine($"- Metatype: {packet.Metatype}");
        builder.AppendLine($"- Build method: {packet.BuildMethod}");
        builder.AppendLine($"- Archetype hint: {packet.ArchetypeHint}");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(packet.GmAllowanceNotes))
        {
            builder.AppendLine("## GM Notes");
            builder.AppendLine(HumanCopy(packet.GmAllowanceNotes));
            builder.AppendLine();
        }
        builder.AppendLine("## Summary");
        builder.AppendLine(HumanCopy(draft.Summary));
        builder.AppendLine();
        builder.AppendLine("## Origin");
        builder.AppendLine(HumanCopy(draft.Prose));
        builder.AppendLine();
        builder.AppendLine("## GM Hooks");
        foreach (string hook in draft.GmHooks)
        {
            builder.AppendLine($"- {HumanCopy(hook)}");
        }

        builder.AppendLine();
        builder.AppendLine("## Open Questions");
        foreach (string contradiction in packet.ContradictionFlags.DefaultIfEmpty("None found in the current character context."))
        {
            builder.AppendLine($"- {HumanCopy(contradiction)}");
        }

        builder.AppendLine();
        builder.AppendLine("## Build Signals");
        foreach (string signal in packet.StandoutSignals)
        {
            builder.AppendLine($"- {HumanCopy(signal)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSoundmadeseenNarrationScript(OriginDossierBundle bundle)
    {
        StringBuilder builder = new();
        builder.AppendLine($"Soundmadeseen audiobook brief for {bundle.Packet.Alias}");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(bundle.GmAllowanceNotes))
        {
            builder.AppendLine($"GM notes: {HumanCopy(bundle.GmAllowanceNotes)}");
            builder.AppendLine();
        }
        builder.AppendLine(HumanCopy(bundle.Canon.Summary));
        builder.AppendLine();
        builder.AppendLine(HumanCopy(bundle.Canon.Prose));
        builder.AppendLine();
        builder.AppendLine("GM hooks:");
        foreach (string hook in bundle.Canon.GmHooks)
        {
            builder.AppendLine($"- {HumanCopy(hook)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildUnmixrNarrationScript(OriginDossierBundle bundle)
    {
        StringBuilder builder = new();
        builder.AppendLine($"Unmixr alternate voice brief for {bundle.Packet.Alias}");
        builder.AppendLine();
        builder.AppendLine("Voice direction: intimate dossier reading with slightly more expressive cadence than the default voice.");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(bundle.GmAllowanceNotes))
        {
            builder.AppendLine($"GM notes: {HumanCopy(bundle.GmAllowanceNotes)}");
            builder.AppendLine();
        }
        builder.AppendLine(HumanCopy(bundle.Canon.Summary));
        builder.AppendLine();
        builder.AppendLine(HumanCopy(bundle.Canon.Prose));
        builder.AppendLine();
        builder.AppendLine("GM hooks:");
        foreach (string hook in bundle.Canon.GmHooks)
        {
            builder.AppendLine($"- {HumanCopy(hook)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildOriginMediaFactoryNarrationRunbook(OriginDossierBundle bundle, string requestPath)
    {
        StringBuilder builder = new();
        builder.AppendLine($"# Origin Dossier Audiobook Notes · {bundle.Packet.Alias}");
        builder.AppendLine();
        builder.AppendLine("This request uses the approved origin story and the selected voice scripts.");
        builder.AppendLine();
        builder.AppendLine("## Inputs");
        builder.AppendLine($"- Story markdown: {bundle.CanonMarkdownPath}");
        builder.AppendLine($"- Story data: {bundle.CanonJsonPath}");
        builder.AppendLine($"- Default voice script: {bundle.SoundmadeseenScriptPath}");
        builder.AppendLine($"- Alternate voice script: {bundle.UnmixrScriptPath}");
        builder.AppendLine($"- Render request: {requestPath}");
        if (!string.IsNullOrWhiteSpace(bundle.GmAllowanceNotes))
        {
            builder.AppendLine($"- GM notes: {HumanCopy(bundle.GmAllowanceNotes)}");
        }
        builder.AppendLine();
        builder.AppendLine("## Voices");
        builder.AppendLine("- Default: Soundmadeseen");
        builder.AppendLine("- Alternate: Unmixr");
        builder.AppendLine();
        builder.AppendLine("## Boundary");
        builder.AppendLine("- Audio output does not change rules or the character sheet.");
        builder.AppendLine("- If the alternate voice fails, keep the default voice.");
        builder.AppendLine();
        builder.AppendLine("## Expected outputs");
        builder.AppendLine("- one default audiobook artifact");
        builder.AppendLine("- one alternate audiobook artifact");
        builder.AppendLine("- render logs");
        builder.AppendLine("- preview/audio companion refs for later dossier/video phases");
        return builder.ToString().TrimEnd();
    }

    private static byte[] BuildSimplePdfDocument(string title, IReadOnlyList<string> lines)
    {
        List<string> pdfLines =
        [
            title,
            string.Empty,
            .. lines.Take(40)
        ];

        StringBuilder content = new();
        content.AppendLine("BT");
        content.AppendLine("/F1 18 Tf");
        content.AppendLine("50 790 Td");
        content.AppendLine($"({EscapePdfText(title)}) Tj");
        content.AppendLine("0 -24 Td");
        content.AppendLine("/F1 11 Tf");
        foreach (string line in pdfLines.Skip(1))
        {
            content.AppendLine($"({EscapePdfText(line)}) Tj");
            content.AppendLine("0 -14 Td");
        }
        content.AppendLine("ET");

        List<string> objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            $"5 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream\nendobj\n"
        ];

        StringBuilder document = new();
        document.Append("%PDF-1.4\n");
        List<int> offsets = [];
        foreach (string obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(document.ToString()));
            document.Append(obj);
        }

        int crossReferenceOffset = Encoding.ASCII.GetByteCount(document.ToString());
        document.Append("xref\n");
        document.Append($"0 {objects.Count + 1}\n");
        document.Append("0000000000 65535 f \n");
        foreach (int offset in offsets)
        {
            document.Append(offset.ToString("D10"));
            document.Append(" 00000 n \n");
        }

        document.Append("trailer\n");
        document.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        document.Append("startxref\n");
        document.Append(crossReferenceOffset);
        document.Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(document.ToString());
    }

    private static string EscapePdfText(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

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
            ? ["Alice returned an answer without extra detail lines."]
            : HumanLines(lines);
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
            buttons.Add(CreateButton("Open account Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceSuggestedActionFallbackAccount"));
            buttons.Add(CreateButton("Open web Alice", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceSuggestedActionFallbackPublic"));
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
    {
        string normalizedMode = NormalizeConversationMode(mode);
        if (string.Equals(normalizedMode, RulesCoachMode, StringComparison.Ordinal))
        {
            return new AliceConversationTurnEntry(
                AliceConversationTurnKind.Assistant,
                "Alice",
                "Rules coach ready",
                "Ask for a rule explanation, tradeoff, or safe next build step tied to the active ruleset.",
                [],
                BuildStarterPrompts(normalizedMode));
        }

        if (IsOriginDossierMode(normalizedMode))
        {
            return new AliceConversationTurnEntry(
                AliceConversationTurnKind.Assistant,
                "Alice",
                "Origin Dossier ready",
                "Create an optional origin dossier that explains why this build exists without changing the sheet.",
                [],
                BuildStarterPrompts(normalizedMode));
        }

        return new AliceConversationTurnEntry(
            AliceConversationTurnKind.Assistant,
            "Alice",
            "Build copilot ready",
            "Ask for the next move, a comparison, or a complete scratch build from the current desktop settings.",
            [],
            BuildStarterPrompts(normalizedMode));
    }

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
    {
        string normalizedMode = NormalizeConversationMode(mode);
        string title = string.Equals(normalizedMode, RulesCoachMode, StringComparison.Ordinal)
            ? "Rules coach"
            : IsOriginDossierMode(normalizedMode)
                ? "Origin dossier"
                : "Build help";

        return new AliceConversationTurnEntry(
            AliceConversationTurnKind.Assistant,
            "Alice",
            title,
            body,
            [status, .. evidenceLines.Take(5)],
            suggestedActionTitles.Take(3).ToArray());
    }

    private static string[] BuildStarterPrompts(string? mode)
    {
        string normalizedMode = NormalizeConversationMode(mode);
        if (string.Equals(normalizedMode, RulesCoachMode, StringComparison.Ordinal))
        {
            return
            [
                "Explain legality Strict vs Standard rules-wise.",
                "When does ware become a rules problem?",
                "What rule am I most likely to miss here?"
            ];
        }

        if (IsOriginDossierMode(normalizedMode))
        {
            return
            [
                "Create a troll decker origin dossier with one illegal-addiction GM constraint.",
                "Create an origin dossier for a magically active survivor.",
                "Explain how the qualities, ware, and first gear fit the backstory."
            ];
        }

        return
            [
                "Build a complete SR4 BP troll decker from scratch.",
                "Build a street samurai from scratch with standard legality.",
                "Explain legality, qualities, ware, and first purchases."
            ];
    }

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
        string? GmAllowanceNotes,
        string? WorkspaceName,
        string? LeadBuildPathTitle,
        string? LeadHandoffTitle,
        IReadOnlyList<string> CausalityHints,
        IReadOnlyList<string> StandoutSignals,
        IReadOnlyList<string> ContradictionFlags,
        string? RuntimeFingerprint = null);

    private sealed record CharacterNarrativeDraft(
        string Summary,
        string Prose,
        IReadOnlyList<string> GmHooks,
        string? RuntimeFingerprint = null);

    private sealed record OriginPortraitCandidate(
        string CandidateId,
        string StyleLabel,
        string Summary,
        string BackgroundHex,
        string AccentHex,
        string ForegroundHex,
        string HighlightHex);

    private sealed record OriginSceneCandidate(
        string SceneId,
        string Title,
        string Summary,
        string BackgroundHex,
        string AccentHex);

    private sealed record OriginDossierBundle(
        CharacterNarrativePacket Packet,
        CharacterNarrativeDraft Canon,
        DateTimeOffset ApprovedAtUtc,
        string BundleDirectory,
        string CanonJsonPath,
        string CanonMarkdownPath,
        string? DossierPdfPath,
        string? MarkupGoPacketPath,
        string? PortraitSetJsonPath,
        string? PortraitContactSheetPath,
        IReadOnlyList<string> PortraitCandidatePaths,
        string? SelectedPortraitPath,
        string? SceneBriefMarkdownPath,
        string? SceneSetJsonPath,
        IReadOnlyList<string> SceneCandidatePaths,
        string? SelectedScenePath,
        string? SoundmadeseenPacketPath,
        string? SoundmadeseenScriptPath,
        string? UnmixrPacketPath,
        string? UnmixrScriptPath,
        string? MediaFactoryNarrationRequestPath,
        string? MediaFactoryNarrationRunbookPath,
        string? MediaFactoryNarrationReceiptPath,
        string? VidBoardPacketPath,
        string? VideoStoryboardPath,
        string? VideoPosterPath,
        string? MediaFactoryVideoReceiptPath,
        string? RenderedVideoPath,
        string? GmAllowanceNotes,
        string? RuntimeFingerprint = null);
}
