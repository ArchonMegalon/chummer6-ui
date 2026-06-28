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
using Chummer.Presentation.OriginBooks;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Run.Contracts.Billing;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using CharacterNarrativeDraft = Chummer.Presentation.OriginBooks.OriginBookCanonDraft;
using CharacterNarrativePacket = Chummer.Presentation.OriginBooks.OriginBookSourcePacket;
using OriginDossierBundle = Chummer.Presentation.OriginBooks.OriginBookProject;

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
    private const string MediaFactoryAllowLiveExecutionEnv = "CHUMMER_MEDIA_FACTORY_ALLOW_LIVE_EXECUTION";
    private const string OriginPremiumAllowLiveConsumptionEnv = "CHUMMER_ORIGIN_ALLOW_LIVE_PREMIUM_CONSUMPTION";
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
    private readonly HashSet<string> _originSelectedGmConstraints = new(StringComparer.Ordinal);
    private string _originEditionSelection = "Narrative Origin";
    private string _originMetatypeSelection = "Human";
    private string _originArchetypeSelection = "Use current character";
    private string _originBuildFrameSelection = "Use current ruleset";
    private string _originPressureSelection = "Street-level survival";
    private string _originBookSurfaceSelection = "PDF book and MyFirstBook presentation";
    private string _originPrimaryVoiceSelection = "Measured dossier";
    private string _originAlternateVoiceSelection = "Cinematic narration";
    private string _originPortraitStyleSelection = "Noir Ink";
    private string _originVideoStyleSelection = "Grounded dossier";
    private BuildLabHandoffProjection? _selectedHandoff;
    private DesktopBuildPathCandidate? _selectedBuildPath;
    private Action? _refreshAssistantContext;
    private CharacterNarrativePacket? _originPacket;
    private CharacterNarrativeDraft? _originDraft;
    private string? _originDraftDirectory;
    private string? _originDraftMarkdownPath;
    private string? _originDraftMyFirstBookPacketPath;
    private string? _originDraftMyFirstBookPresentationPath;
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
                                CreateButton("Open browser guide", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice")),
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
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush()
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
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush()
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
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush()
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

        ComboBox originMetatypeCombo = new()
        {
            Name = "AliceOriginMetatypeCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginMetatypeOptions(),
            SelectedItem = ResolveOriginMetatypeDefault()
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originMetatypeCombo);
        _originMetatypeSelection = originMetatypeCombo.SelectedItem?.ToString() ?? _originMetatypeSelection;

        ComboBox originArchetypeCombo = new()
        {
            Name = "AliceOriginArchetypeCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginArchetypeOptions(),
            SelectedItem = _recentWorkspaces.Count == 0 ? "Decker" : "Use current character role"
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

        ComboBox originEditionCombo = new()
        {
            Name = "AliceOriginEditionCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginEditionOptions(),
            SelectedItem = _originEditionSelection
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originEditionCombo);
        _originEditionSelection = originEditionCombo.SelectedItem?.ToString() ?? _originEditionSelection;

        ComboBox originBookSurfaceCombo = new()
        {
            Name = "AliceOriginBookSurfaceCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginBookSurfaceOptions(),
            SelectedItem = _originBookSurfaceSelection
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originBookSurfaceCombo);
        _originBookSurfaceSelection = originBookSurfaceCombo.SelectedItem?.ToString() ?? _originBookSurfaceSelection;

        ComboBox originPrimaryVoiceCombo = new()
        {
            Name = "AliceOriginPrimaryVoiceCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginPrimaryVoiceOptions(),
            SelectedItem = _originPrimaryVoiceSelection
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originPrimaryVoiceCombo);
        _originPrimaryVoiceSelection = originPrimaryVoiceCombo.SelectedItem?.ToString() ?? _originPrimaryVoiceSelection;

        ComboBox originAlternateVoiceCombo = new()
        {
            Name = "AliceOriginAlternateVoiceCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginAlternateVoiceOptions(),
            SelectedItem = _originAlternateVoiceSelection
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originAlternateVoiceCombo);
        _originAlternateVoiceSelection = originAlternateVoiceCombo.SelectedItem?.ToString() ?? _originAlternateVoiceSelection;

        ComboBox originPortraitStyleCombo = new()
        {
            Name = "AliceOriginPortraitStyleCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginPortraitStyleOptions(),
            SelectedItem = _originPortraitStyleSelection
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originPortraitStyleCombo);
        _originPortraitStyleSelection = originPortraitStyleCombo.SelectedItem?.ToString() ?? _originPortraitStyleSelection;

        ComboBox originVideoStyleCombo = new()
        {
            Name = "AliceOriginVideoStyleCombo",
            MinWidth = 220,
            ItemsSource = BuildOriginVideoStyleOptions(),
            SelectedItem = _originVideoStyleSelection
        };
        DesktopShellTheme.ApplyShellComboBoxTheme(originVideoStyleCombo);
        _originVideoStyleSelection = originVideoStyleCombo.SelectedItem?.ToString() ?? _originVideoStyleSelection;

        TextBlock originWizardGuideText = new()
        {
            Name = "AliceOriginWizardGuideText",
            Text = "Start with a story draft. Choose the edition first, then turn the approved draft into the matching book, presentation, voice, portrait, scene, and video handoffs without letting prose mutate the sheet.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush()
        };

        StackPanel originGmConstraintList = new()
        {
            Name = "AliceOriginGmConstraintList",
            Spacing = 6
        };

        foreach (string gmConstraint in BuildOriginGmRequirementPresetOptions())
        {
            CheckBox checkBox = new()
            {
                Name = $"AliceOriginGmConstraint_{SanitizeNameToken(gmConstraint)}",
                Content = gmConstraint,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            checkBox.IsCheckedChanged += (_, _) =>
            {
                if (checkBox.IsChecked == true)
                {
                    _originSelectedGmConstraints.Add(gmConstraint);
                }
                else
                {
                    _originSelectedGmConstraints.Remove(gmConstraint);
                }

                _refreshAssistantContext?.Invoke();
            };
            originGmConstraintList.Children.Add(checkBox);
        }

        Border originSteeringPanel = new()
        {
            Name = "AliceOriginStorySteeringPanel",
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveSurfaceAltBrush(),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Optional steering",
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Set the edition, output surface, voices, portrait look, video look, and any GM constraints before you draft or redraft the story.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = DesktopShellTheme.ResolveMutedForegroundBrush()
                    },
                    CreateFieldGrid(
                        ("Edition", originEditionCombo),
                        ("Build frame", originBuildFrameCombo)),
                    CreateFieldGrid(
                        ("Story pressure", originPressureCombo)),
                    CreateFieldGrid(
                        ("Book / presentation", originBookSurfaceCombo),
                        ("Portrait style", originPortraitStyleCombo)),
                    CreateFieldGrid(
                        ("Main audiobook voice", originPrimaryVoiceCombo),
                        ("Alternate voice", originAlternateVoiceCombo)),
                    CreateFieldGrid(
                        ("Video style", originVideoStyleCombo)),
                    new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "GM constraints",
                                FontWeight = FontWeight.SemiBold,
                                TextWrapping = TextWrapping.Wrap
                            },
                            originGmConstraintList,
                            gmAllowanceGuideText,
                            gmAllowanceBox
                        }
                    }
                }
            }
        };

        Border originWizardPanel = new()
        {
            Name = "AliceOriginWizardPanel",
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveSurfaceBrush(),
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
                    CreateFieldGrid(
                        ("Race / metatype", originMetatypeCombo),
                        ("Archetype", originArchetypeCombo)),
                    originSteeringPanel
                }
            }
        };

        originMetatypeCombo.SelectionChanged += (_, _) =>
            _originMetatypeSelection = originMetatypeCombo.SelectedItem?.ToString() ?? _originMetatypeSelection;
        originArchetypeCombo.SelectionChanged += (_, _) =>
            _originArchetypeSelection = originArchetypeCombo.SelectedItem?.ToString() ?? _originArchetypeSelection;
        originBuildFrameCombo.SelectionChanged += (_, _) =>
            _originBuildFrameSelection = originBuildFrameCombo.SelectedItem?.ToString() ?? _originBuildFrameSelection;
        originPressureCombo.SelectionChanged += (_, _) =>
            _originPressureSelection = originPressureCombo.SelectedItem?.ToString() ?? _originPressureSelection;
        originEditionCombo.SelectionChanged += (_, _) =>
            _originEditionSelection = originEditionCombo.SelectedItem?.ToString() ?? _originEditionSelection;
        originBookSurfaceCombo.SelectionChanged += (_, _) =>
            _originBookSurfaceSelection = originBookSurfaceCombo.SelectedItem?.ToString() ?? _originBookSurfaceSelection;
        originPrimaryVoiceCombo.SelectionChanged += (_, _) =>
            _originPrimaryVoiceSelection = originPrimaryVoiceCombo.SelectedItem?.ToString() ?? _originPrimaryVoiceSelection;
        originAlternateVoiceCombo.SelectionChanged += (_, _) =>
            _originAlternateVoiceSelection = originAlternateVoiceCombo.SelectedItem?.ToString() ?? _originAlternateVoiceSelection;
        originPortraitStyleCombo.SelectionChanged += (_, _) =>
            _originPortraitStyleSelection = originPortraitStyleCombo.SelectedItem?.ToString() ?? _originPortraitStyleSelection;
        originVideoStyleCombo.SelectionChanged += (_, _) =>
            _originVideoStyleSelection = originVideoStyleCombo.SelectedItem?.ToString() ?? _originVideoStyleSelection;

        TextBlock statusText = new()
        {
            Name = "AliceAssistantStatusText",
            Text = BuildIdleAssistantStatus(modeCombo.SelectedItem?.ToString()),
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush(),
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
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush(),
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
            starterPromptRow.IsVisible = !IsOriginDossierMode(modeCombo.SelectedItem?.ToString());
            if (!starterPromptRow.IsVisible)
            {
                return;
            }

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
                    if (string.IsNullOrWhiteSpace(_originBundle.DossierPdfPath))
                    {
                        actionRow.Children.Add(CreateButton("Build book", RenderOriginDossierPdfAsync, isPrimary: true, name: "AliceOriginRenderDossierPdfButton"));
                    }
                    else
                    {
                        actionRow.Children.Add(CreateButton("Open book", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.DossierPdfPath), isPrimary: true, name: "AliceOriginOpenDossierPdfButton"));
                    }
                    actionRow.Children.Add(CreateButton("Open story", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.CanonMarkdownPath), name: "AliceOriginOpenCanonStoryButton"));
                    actionRow.Children.Add(CreateButton("Open presentation", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.MyFirstBookPresentationPath), name: "AliceOriginOpenMyFirstBookPacketButton"));
                    if (!string.IsNullOrWhiteSpace(_originBundle.PremiumOutlineMarkdownPath))
                    {
                        actionRow.Children.Add(CreateButton("Open memoir outline", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.PremiumOutlineMarkdownPath), name: "AliceOriginOpenPremiumOutlineButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originBundle.PremiumChapterPlanJsonPath))
                    {
                        actionRow.Children.Add(CreateButton("Open chapter plan", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.PremiumChapterPlanJsonPath), name: "AliceOriginOpenPremiumChapterPlanButton"));
                    }
                    actionRow.Children.Add(CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.BundleDirectory), name: "AliceOriginOpenBundleFolderButton"));
                    actionRow.Children.Add(CreateButton("Create portraits", RenderOriginPortraitSetAsync, name: "AliceOriginGeneratePortraitSetButton"));
                    actionRow.Children.Add(CreateButton("Create scenes", RenderOriginSceneSetAsync, name: "AliceOriginGenerateSceneSetButton"));
                    actionRow.Children.Add(CreateButton("Open main voice setup", () => !string.IsNullOrWhiteSpace(_originBundle.InkfluencePacketPath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.InkfluencePacketPath), name: "AliceOriginOpenNarrationPacketButton"));
                    actionRow.Children.Add(CreateButton("Open alternate voice setup", () => !string.IsNullOrWhiteSpace(_originBundle.UnmixrPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.UnmixrPacketPath), name: "AliceOriginOpenAlternateNarrationPacketButton"));
                    actionRow.Children.Add(CreateButton("Open audiobook setup", () => !string.IsNullOrWhiteSpace(_originBundle.MediaFactoryNarrationRequestPath) && DesktopCrashRuntime.TryOpenPathInShell(_originBundle.MediaFactoryNarrationRequestPath), name: "AliceOriginOpenMediaFactoryNarrationRequestButton"));
                    actionRow.Children.Add(CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"));
                    if (ShouldAllowLiveMediaFactoryExecution())
                    {
                        actionRow.Children.Add(CreateButton("Create audiobook", RenderOriginAudiobookNowAsync, name: "AliceOriginRenderAudiobookNowButton"));
                        actionRow.Children.Add(CreateButton("Create video", RenderOriginDossierVideoNowAsync, name: "AliceOriginRenderDossierVideoNowButton"));
                    }
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
                        actionRow.Children.Add(CreateButton("Open audiobook details", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.MediaFactoryNarrationReceiptPath), name: "AliceOriginOpenMediaFactoryNarrationReceiptButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originBundle.MediaFactoryVideoReceiptPath))
                    {
                        actionRow.Children.Add(CreateButton("Open video details", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.MediaFactoryVideoReceiptPath), name: "AliceOriginOpenMediaFactoryVideoReceiptButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originBundle.RenderedVideoPath))
                    {
                        actionRow.Children.Add(CreateButton("Open rendered video", () => DesktopCrashRuntime.TryOpenPathInShell(_originBundle.RenderedVideoPath), name: "AliceOriginOpenRenderedVideoButton"));
                    }
                }
                else if (_originDraft is not null)
                {
                    if (!string.IsNullOrWhiteSpace(_originDraftMarkdownPath))
                    {
                        actionRow.Children.Add(CreateButton("Open story", () => DesktopCrashRuntime.TryOpenPathInShell(_originDraftMarkdownPath), isPrimary: true, name: "AliceOriginOpenDraftStoryButton"));
                    }
                    if (!string.IsNullOrWhiteSpace(_originDraftMyFirstBookPresentationPath))
                    {
                        actionRow.Children.Add(CreateButton("Open presentation", () => DesktopCrashRuntime.TryOpenPathInShell(_originDraftMyFirstBookPresentationPath), name: "AliceOriginOpenDraftMyFirstBookPacketButton"));
                    }
                    actionRow.Children.Add(CreateButton("Approve story", ApproveOriginCanonAsync, name: "AliceOriginApproveCanonButton"));
                    actionRow.Children.Add(CreateButton("Rewrite story", RewriteOriginDraftAsync, name: "AliceOriginRegenerateButton"));
                }
                else
                {
                    actionRow.Children.Add(CreateButton("Draft story", StartOriginDossierAsync, isPrimary: true, name: "AliceOriginStartDossierButton"));
                    actionRow.Children.Add(CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceAssistantOpenAccountButton"));
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
                    actionRow.Children.Add(CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceAssistantOpenAccountButton"));
                }
                else
                {
                    actionRow.Children.Add(CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceAssistantOpenAccountButton"));
                }

                actionRow.Children.Add(CreateButton("Open browser guide", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceAssistantOpenPublicButton"));
            }
            if (!string.IsNullOrWhiteSpace(_gmAllowanceNotes) || _originSelectedGmConstraints.Count > 0)
            {
                actionRow.Children.Add(CreateButton("Clear GM notes", () =>
                {
                    gmAllowanceBox.Text = string.Empty;
                    _gmAllowanceNotes = string.Empty;
                    _originSelectedGmConstraints.Clear();
                    foreach (CheckBox checkBox in originGmConstraintList.Children.OfType<CheckBox>())
                    {
                        checkBox.IsChecked = false;
                    }
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

        async Task ApproveOriginCanonAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before approving the story.";
                return;
            }

            MyFirstBookQuotaConsumeResultDto? quotaConsumeResult = null;
            MyFirstBookQuotaSnapshotDto? premiumQuotaSnapshot = null;
            bool premiumConsumptionDeferred = false;
            if (_originBundle is null && ShouldUsePremiumGuidedAuthoring(_originPacket.BookKind))
            {
                if (ShouldAllowLivePremiumConsumption())
                {
                    try
                    {
                        quotaConsumeResult = await EnsureMyFirstBookAllowanceAsync().ConfigureAwait(true);
                    }
                    catch (InvalidOperationException ex)
                    {
                        statusText.Text = "MyFirstBook is not available for this account.";
                        answerText.Text = HumanCopy(ex.Message);
                        evidenceList.ItemsSource = HumanLines(
                        [
                            "Free accounts can create 1 MyFirstBook origin book each month.",
                            "Supporter accounts can create 2 MyFirstBook origin books each month.",
                            "Link your copy and sign in before creating the book."
                        ]);
                        actionRow.Children.Clear();
                        actionRow.Children.Add(CreateButton("Open billing", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/billing"), isPrimary: true, name: "AliceOriginOpenBillingButton"));
                        actionRow.Children.Add(CreateButton("Link your copy", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/access"), name: "AliceOriginOpenAccessButton"));
                        return;
                    }
                }
                else
                {
                    premiumQuotaSnapshot = await TryGetMyFirstBookQuotaSnapshotAsync().ConfigureAwait(true);
                    premiumConsumptionDeferred = true;
                }
            }

            OriginDossierBundle bundle = EnsureOriginDossierPdf(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Origin story approved.",
                $"{bundle.Canon.Summary} The book is ready; use the story as Alice's seed for later build guidance.",
                BuildOriginBundleEvidence(bundle, quotaConsumeResult?.Quota ?? premiumQuotaSnapshot, premiumConsumptionDeferred),
                CreateButton("Open book", () => !string.IsNullOrWhiteSpace(bundle.DossierPdfPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.DossierPdfPath), isPrimary: true, name: "AliceOriginOpenDossierPdfButton"),
                CreateButton("Open story", () => DesktopCrashRuntime.TryOpenPathInShell(bundle.CanonMarkdownPath), name: "AliceOriginOpenCanonStoryButton"),
                CreateButton("Open presentation", () => DesktopCrashRuntime.TryOpenPathInShell(bundle.MyFirstBookPresentationPath), name: "AliceOriginOpenMyFirstBookPacketButton"),
                CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(bundle.BundleDirectory), name: "AliceOriginOpenBundleFolderButton"),
                CreateButton("Create portraits", RenderOriginPortraitSetAsync, name: "AliceOriginGeneratePortraitSetButton"),
                CreateButton("Create scenes", RenderOriginSceneSetAsync, name: "AliceOriginGenerateSceneSetButton"),
                CreateButton("Set up main voice", RenderOriginAudiobookPacketAsync, name: "AliceOriginGenerateAudiobookPacketButton"),
                CreateButton("Set up alternate voice", RenderOriginAlternateAudiobookPacketAsync, name: "AliceOriginGenerateAlternateAudiobookPacketButton"),
                CreateButton("Set up audiobook", RenderOriginMediaFactoryRequestAsync, name: "AliceOriginGenerateMediaFactoryNarrationRequestButton"),
                CreateButton("Create dossier video", RenderOriginDossierVideoAsync, name: "AliceOriginGenerateDossierVideoButton"));
        }

        Task RenderOriginDossierPdfAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before building the book.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureOriginDossierPdf(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Book ready.",
                $"Book: {Path.GetFileName(bundle.DossierPdfPath)}. Book notes: {Path.GetFileName(bundle.MarkupGoPacketPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open book", () => !string.IsNullOrWhiteSpace(bundle.DossierPdfPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.DossierPdfPath), isPrimary: true, name: "AliceOriginOpenDossierPdfButton"),
                CreateButton("Open book notes", () => !string.IsNullOrWhiteSpace(bundle.MarkupGoPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.MarkupGoPacketPath), name: "AliceOriginOpenMarkupGoPacketButton"),
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
                statusText.Text = "Create an origin draft before preparing the Inkfluence voice script.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureInkfluenceNarrationPacket(EnsureOriginDossierBundle());
            _originBundle = bundle;
            ShowOriginBundleState(
                "Audiobook script ready.",
                $"Script: {Path.GetFileName(bundle.InkfluenceScriptPath)}. Voice notes: {Path.GetFileName(bundle.InkfluencePacketPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open voice notes", () => !string.IsNullOrWhiteSpace(bundle.InkfluencePacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.InkfluencePacketPath), isPrimary: true, name: "AliceOriginOpenNarrationPacketButton"),
                CreateButton("Open audiobook script", () => !string.IsNullOrWhiteSpace(bundle.InkfluenceScriptPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.InkfluenceScriptPath), name: "AliceOriginOpenNarrationScriptButton"),
                CreateButton("Set up alternate voice", RenderOriginAlternateAudiobookPacketAsync, name: "AliceOriginGenerateAlternateAudiobookPacketButton"),
                CreateButton("Set up audiobook", RenderOriginMediaFactoryRequestAsync, name: "AliceOriginGenerateMediaFactoryNarrationRequestButton"),
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
                "Alternate voice ready.",
                $"Script: {Path.GetFileName(bundle.UnmixrScriptPath)}. Voice notes: {Path.GetFileName(bundle.UnmixrPacketPath)}.",
                BuildOriginBundleEvidence(bundle),
                CreateButton("Open alternate voice setup", () => !string.IsNullOrWhiteSpace(bundle.UnmixrPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.UnmixrPacketPath), isPrimary: true, name: "AliceOriginOpenAlternateNarrationPacketButton"),
                CreateButton("Open alternate script", () => !string.IsNullOrWhiteSpace(bundle.UnmixrScriptPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.UnmixrScriptPath), name: "AliceOriginOpenAlternateNarrationScriptButton"),
                CreateButton("Set up main voice", RenderOriginAudiobookPacketAsync, name: "AliceOriginGenerateAudiobookPacketButton"),
                CreateButton("Set up audiobook", RenderOriginMediaFactoryRequestAsync, name: "AliceOriginGenerateMediaFactoryNarrationRequestButton"),
                CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(bundle.BundleDirectory), name: "AliceOriginOpenBundleFolderButton"));
            return Task.CompletedTask;
        }

        Task RenderOriginMediaFactoryRequestAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before preparing the audiobook.";
                return Task.CompletedTask;
            }

            OriginDossierBundle bundle = EnsureOriginMediaFactoryNarrationRequest(EnsureOriginDossierBundle());
            _originBundle = bundle;
            List<Button> actions =
            [
                CreateButton("Open audiobook setup", () => !string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRequestPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.MediaFactoryNarrationRequestPath), isPrimary: true, name: "AliceOriginOpenMediaFactoryNarrationRequestButton"),
                CreateButton("Open audiobook brief", () => !string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRunbookPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.MediaFactoryNarrationRunbookPath), name: "AliceOriginOpenMediaFactoryNarrationRunbookButton")
            ];
            if (ShouldAllowLiveMediaFactoryExecution())
            {
                actions.Add(CreateButton("Create audiobook", RenderOriginAudiobookNowAsync, name: "AliceOriginRenderAudiobookNowButton"));
            }
            actions.Add(CreateButton("Open main voice setup", () => !string.IsNullOrWhiteSpace(bundle.InkfluencePacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.InkfluencePacketPath), name: "AliceOriginOpenNarrationPacketButton"));
            actions.Add(CreateButton("Open alternate voice setup", () => !string.IsNullOrWhiteSpace(bundle.UnmixrPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.UnmixrPacketPath), name: "AliceOriginOpenAlternateNarrationPacketButton"));
            ShowOriginBundleState(
                "Audiobook setup ready.",
                $"Audiobook setup: {Path.GetFileName(bundle.MediaFactoryNarrationRequestPath)}. Brief: {Path.GetFileName(bundle.MediaFactoryNarrationRunbookPath)}.",
                BuildOriginBundleEvidence(bundle),
                actions.ToArray());
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
            List<Button> actions =
            [
                CreateButton("Open video poster", () => !string.IsNullOrWhiteSpace(bundle.VideoPosterPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.VideoPosterPath), isPrimary: true, name: "AliceOriginOpenVideoPosterButton"),
                CreateButton("Open storyboard", () => !string.IsNullOrWhiteSpace(bundle.VideoStoryboardPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.VideoStoryboardPath), name: "AliceOriginOpenVideoStoryboardButton"),
                CreateButton("Open video plan", () => !string.IsNullOrWhiteSpace(bundle.VidBoardPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(bundle.VidBoardPacketPath), name: "AliceOriginOpenVidBoardPacketButton")
            ];
            if (ShouldAllowLiveMediaFactoryExecution())
            {
                actions.Add(CreateButton("Create video", RenderOriginDossierVideoNowAsync, name: "AliceOriginRenderDossierVideoNowButton"));
            }
            ShowOriginBundleState(
                "Dossier video plan ready.",
                $"Dossier video ready. Poster: {Path.GetFileName(bundle.VideoPosterPath)}. Plan: {Path.GetFileName(bundle.VidBoardPacketPath)}.",
                BuildOriginBundleEvidence(bundle),
                actions.ToArray());
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
                statusText.Text = "Create an origin draft before creating the audiobook.";
                return;
            }

            OriginDossierBundle bundle = EnsureOriginMediaFactoryNarrationRequest(EnsureOriginDossierBundle());
            string receiptPath = await ExecuteOriginMediaFactoryNarrationAsync(bundle).ConfigureAwait(true);
            OriginDossierBundle updatedBundle = UpdateOriginProjectArtifacts(
                bundle,
                bundle.Artifacts with
                {
                    MediaFactoryNarrationReceiptPath = receiptPath
                });
            PersistOriginBookProjectFiles(updatedBundle);
            _originBundle = updatedBundle;
            ShowOriginBundleState(
                "Audiobook ready.",
                $"Audio details: {Path.GetFileName(receiptPath)}. Default and alternate voice paths are ready to review.",
                BuildOriginBundleEvidence(updatedBundle),
                CreateButton("Open audiobook details", () => DesktopCrashRuntime.TryOpenPathInShell(receiptPath), isPrimary: true, name: "AliceOriginOpenMediaFactoryNarrationReceiptButton"),
                CreateButton("Open audiobook setup", () => !string.IsNullOrWhiteSpace(updatedBundle.MediaFactoryNarrationRequestPath) && DesktopCrashRuntime.TryOpenPathInShell(updatedBundle.MediaFactoryNarrationRequestPath), name: "AliceOriginOpenMediaFactoryNarrationRequestButton"),
                CreateButton("Open bundle folder", () => DesktopCrashRuntime.TryOpenPathInShell(updatedBundle.BundleDirectory), name: "AliceOriginOpenBundleFolderButton"));
        }

        async Task RenderOriginDossierVideoNowAsync()
        {
            if (_originDraft is null || _originPacket is null)
            {
                statusText.Text = "Create an origin draft before creating the dossier video.";
                return;
            }

            OriginDossierBundle bundle = EnsureOriginDossierVideoPacket(EnsureOriginDossierBundle());
            (string receiptPath, string renderedVideoPath) = await ExecuteOriginDossierVideoAsync(bundle).ConfigureAwait(true);
            OriginDossierBundle updatedBundle = UpdateOriginProjectArtifacts(
                bundle,
                bundle.Artifacts with
                {
                    MediaFactoryVideoReceiptPath = receiptPath,
                    RenderedVideoPath = renderedVideoPath
                });
            PersistOriginBookProjectFiles(updatedBundle);
            _originBundle = updatedBundle;
            ShowOriginBundleState(
                "Dossier video finished.",
                $"Video: {Path.GetFileName(renderedVideoPath)}. Details: {Path.GetFileName(receiptPath)}.",
                BuildOriginBundleEvidence(updatedBundle),
                CreateButton("Open video", () => DesktopCrashRuntime.TryOpenPathInShell(renderedVideoPath), isPrimary: true, name: "AliceOriginOpenRenderedVideoButton"),
                CreateButton("Open video details", () => DesktopCrashRuntime.TryOpenPathInShell(receiptPath), name: "AliceOriginOpenMediaFactoryVideoReceiptButton"),
                CreateButton("Open video plan", () => !string.IsNullOrWhiteSpace(updatedBundle.VidBoardPacketPath) && DesktopCrashRuntime.TryOpenPathInShell(updatedBundle.VidBoardPacketPath), name: "AliceOriginOpenVidBoardPacketButton"));
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
                _originDraftDirectory = null;
                _originDraftMarkdownPath = null;
                _originDraftMyFirstBookPacketPath = null;
                _originDraftMyFirstBookPresentationPath = null;
                EnsureOriginDraftReviewPacket(packet, originDraft);
                statusText.Text = "Origin draft ready.";
                answerText.Text = HumanCopy(originDraft.Prose);
                IReadOnlyList<string> quotaEvidence = ShouldUsePremiumGuidedAuthoring(packet.BookKind)
                    ? await GetMyFirstBookQuotaEvidenceAsync().ConfigureAwait(true)
                    : Array.Empty<string>();
                string[] originEvidence = BuildOriginEvidence(packet, originDraft)
                    .Concat(BuildOriginDraftReviewEvidence())
                    .Concat(quotaEvidence)
                    .ToArray();
                evidenceList.ItemsSource = originEvidence;
                string[] originActionTitles =
                [
                    "Open story",
                    "Open presentation",
                    "Approve story",
                    "Rewrite story"
                ];
                actionRow.Children.Add(CreateButton("Open story", () => !string.IsNullOrWhiteSpace(_originDraftMarkdownPath) && DesktopCrashRuntime.TryOpenPathInShell(_originDraftMarkdownPath), isPrimary: true, name: "AliceOriginOpenDraftStoryButton"));
                actionRow.Children.Add(CreateButton("Open presentation", () => !string.IsNullOrWhiteSpace(_originDraftMyFirstBookPresentationPath) && DesktopCrashRuntime.TryOpenPathInShell(_originDraftMyFirstBookPresentationPath), name: "AliceOriginOpenDraftMyFirstBookPacketButton"));
                actionRow.Children.Add(CreateButton("Approve story", ApproveOriginCanonAsync, name: "AliceOriginApproveCanonButton"));
                actionRow.Children.Add(CreateButton("Rewrite story", RewriteOriginDraftAsync, name: "AliceOriginRegenerateButton"));
                ActiveHistory().Add(BuildAssistantTurn(
                    mode,
                    statusText.Text,
                    answerText.Text,
                    originEvidence,
                    originActionTitles));
                RefreshConversationFeed();
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
                    ["Open account workspace", "Open browser guide"]));
                RefreshConversationFeed();
                actionRow.Children.Add(CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceAssistantFallbackAccountButton"));
                actionRow.Children.Add(CreateButton("Open browser guide", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceAssistantFallbackPublicButton"));
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
            _originMetatypeSelection = originMetatypeCombo.SelectedItem?.ToString() ?? _originMetatypeSelection;
            _originArchetypeSelection = originArchetypeCombo.SelectedItem?.ToString() ?? _originArchetypeSelection;
            _originBuildFrameSelection = originBuildFrameCombo.SelectedItem?.ToString() ?? _originBuildFrameSelection;
            _originPressureSelection = originPressureCombo.SelectedItem?.ToString() ?? _originPressureSelection;
            _originBookSurfaceSelection = originBookSurfaceCombo.SelectedItem?.ToString() ?? _originBookSurfaceSelection;
            _originPrimaryVoiceSelection = originPrimaryVoiceCombo.SelectedItem?.ToString() ?? _originPrimaryVoiceSelection;
            _originAlternateVoiceSelection = originAlternateVoiceCombo.SelectedItem?.ToString() ?? _originAlternateVoiceSelection;
            _originPortraitStyleSelection = originPortraitStyleCombo.SelectedItem?.ToString() ?? _originPortraitStyleSelection;
            _originVideoStyleSelection = originVideoStyleCombo.SelectedItem?.ToString() ?? _originVideoStyleSelection;

            string storyDraftPrompt = BuildOriginStarterPrompt(
                _originEditionSelection,
                _originMetatypeSelection,
                _originArchetypeSelection,
                _originBuildFrameSelection,
                _originPressureSelection,
                BuildCombinedGmAllowanceNotes());
            promptBox.Text = $"{storyDraftPrompt} Target output: {_originBookSurfaceSelection}. Edition: {_originEditionSelection}. Portrait style: {_originPortraitStyleSelection}. Main audiobook voice: {_originPrimaryVoiceSelection}. Alternate voice: {_originAlternateVoiceSelection}. Video style: {_originVideoStyleSelection}.";
            await AskAsync().ConfigureAwait(true);
        }

        async Task RewriteOriginDraftAsync()
        {
            promptBox.Text = _originPacket?.Prompt
                ?? $"{BuildOriginStarterPrompt(
                    _originEditionSelection,
                    _originMetatypeSelection,
                    _originArchetypeSelection,
                    _originBuildFrameSelection,
                    _originPressureSelection,
                    BuildCombinedGmAllowanceNotes())} Target output: {_originBookSurfaceSelection}. Edition: {_originEditionSelection}. Portrait style: {_originPortraitStyleSelection}. Main audiobook voice: {_originPrimaryVoiceSelection}. Alternate voice: {_originAlternateVoiceSelection}. Video style: {_originVideoStyleSelection}.";
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
                    BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
                    BorderThickness = new Thickness(1),
                    Background = DesktopShellTheme.ResolveSurfaceAltBrush(),
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
                    BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
                    BorderThickness = new Thickness(1),
                    Background = DesktopShellTheme.ResolveSelectionInsetBrush(),
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
                    BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
                    BorderThickness = new Thickness(1),
                    Background = DesktopShellTheme.ResolveSurfaceBrush(),
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
                CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceOpenAccountRailButton"),
                CreateButton("Open browser guide", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceOpenPublicButton"));
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
                CreateDetailText(HumanCopy(lead.NextSafeAction ?? "Review the variants and continue only when the next step looks right.")),
                CreateDetailText(lead.RuntimeCompatibilitySummary ?? "Runtime compatibility stays attached to this build link.")
            }
        };

        return CreateCard(
            lead.Title,
            lead.Summary,
            leadDetails,
            "AliceLeadHandoffCard",
            CreateButton("Open build link", () => DesktopInstallLinkingRuntime.TryOpenRelativePortal($"/account/alice/{Uri.EscapeDataString(lead.HandoffId)}"), isPrimary: true, name: "AliceOpenLeadHandoffButton"),
            CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), name: "AliceOpenAccountLaneButton"));
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
                DesktopHorizonWindowScaffold.CreateMetricBadge("AliceBadgeHandoffs", "Build links", handoffs.Count.ToString()),
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
                    Text = handoff is null ? string.Empty : HumanCopy($"{handoff.Title} [{handoff.VariantLabel}]"),
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
                    BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
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
            CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: HasHandoffContext, name: "AliceOpenAccountFromListButton"));
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
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush(),
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
                                : "Runtime and support closure remain on the default state.");
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
                            ?? $"Trust tier {selected.Suggestion.TrustTier} stays visible before anything is applied.";
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
                    BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
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
            CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: HasBuildPathContext, name: "AliceOpenAccountFromBuildPathsButton"),
            CreateButton("Open browser guide", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceOpenPublicFromBuildPathsButton"));
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
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
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

    private static Control CreateFieldGrid(params (string Label, Control Field)[] fields)
    {
        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", Math.Max(1, (fields.Length + 1) / 2)))),
            ColumnSpacing = 12,
            RowSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        for (int index = 0; index < fields.Length; index++)
        {
            Control fieldColumn = CreateFieldColumn(fields[index].Label, fields[index].Field);
            Grid.SetColumn(fieldColumn, index % 2);
            Grid.SetRow(fieldColumn, index / 2);
            grid.Children.Add(fieldColumn);
        }

        return grid;
    }

    private string ResolveOriginMetatypeDefault()
    {
        string? workspaceMetatype = _recentWorkspaces.FirstOrDefault()?.Summary.Metatype;
        return BuildOriginMetatypeOptions().Contains(workspaceMetatype, StringComparer.OrdinalIgnoreCase)
            ? workspaceMetatype!
            : "Human";
    }

    private static IReadOnlyList<string> BuildOriginMetatypeOptions()
        =>
        [
            "Human",
            "Elf",
            "Dwarf",
            "Ork",
            "Troll",
            "Use current character"
        ];

    private static IReadOnlyList<string> BuildOriginArchetypeOptions()
        =>
        [
            "Use current character role",
            "Decker",
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

    private static IReadOnlyList<string> BuildOriginEditionOptions()
        =>
        [
            "Narrative Origin",
            "Origin Dossier",
            "Runner Memoir",
            "Intelligence Casefile"
        ];

    private static IReadOnlyList<string> BuildOriginBookSurfaceOptions()
        =>
        [
            "PDF book and MyFirstBook presentation",
            "PDF book",
            "MyFirstBook presentation"
        ];

    private static IReadOnlyList<string> BuildOriginPrimaryVoiceOptions()
        =>
        [
            "Measured dossier",
            "Warm witness",
            "Quiet operator"
        ];

    private static IReadOnlyList<string> BuildOriginAlternateVoiceOptions()
        =>
        [
            "Cinematic narration",
            "Low noir tension",
            "Sharp newsroom recap"
        ];

    private static IReadOnlyList<string> BuildOriginPortraitStyleOptions()
        =>
        [
            "Noir Ink",
            "Chrome Editorial",
            "Neon Street",
            "Quiet Clinic"
        ];

    private static IReadOnlyList<string> BuildOriginVideoStyleOptions()
        =>
        [
            "Grounded dossier",
            "Cinematic tension",
            "Night city neon",
            "Cold investigation"
        ];

    private static IReadOnlyList<string> BuildOriginGmRequirementPresetOptions()
        =>
        [
            "Must be addicted to an illegal drug",
            "Must be magically active",
            "Must have Logic or Intuition 2+",
            "Extra ware allowance",
            "Restricted gear allowed",
            "Must owe a dangerous contact",
            "Must hide a legal SIN"
        ];

    private string BuildCombinedGmAllowanceNotes()
    {
        string selectedConstraints = string.Join("; ", _originSelectedGmConstraints.OrderBy(static value => value, StringComparer.Ordinal));
        if (string.IsNullOrWhiteSpace(_gmAllowanceNotes))
        {
            return selectedConstraints;
        }

        if (string.IsNullOrWhiteSpace(selectedConstraints))
        {
            return _gmAllowanceNotes;
        }

        return $"{selectedConstraints}; {_gmAllowanceNotes}";
    }

    private static string BuildOriginStarterPrompt(string edition, string metatype, string archetype, string buildFrame, string pressure, string? gmRequirement)
    {
        string resolvedMetatype = string.Equals(metatype, "Use current character", StringComparison.Ordinal)
            ? "the current character's metatype"
            : metatype;
        string resolvedArchetype = string.Equals(archetype, "Use current character role", StringComparison.Ordinal)
            || string.Equals(archetype, "Use current character", StringComparison.Ordinal)
            ? "the current character"
            : archetype;
        string runnerShape = string.Equals(resolvedArchetype, "the current character", StringComparison.Ordinal)
            ? resolvedArchetype
            : $"{resolvedMetatype} {resolvedArchetype}".Trim();
        string gmClause = string.IsNullOrWhiteSpace(gmRequirement)
            ? "No additional GM requirement."
            : $"GM requirement: {gmRequirement.Trim()}.";
        string editionClause = edition switch
        {
            "Origin Dossier" => "Keep it concise and dossier-shaped.",
            "Runner Memoir" => "Plan it like a premium memoir with chapter-ready emotional continuity and a first-person throughline.",
            "Intelligence Casefile" => "Frame it like an intelligence casefile with explicit evidence boundaries and unresolved uncertainty.",
            _ => "Plan it like a narrative origin with chapter-ready momentum."
        };

        return $"Build the {edition.ToLowerInvariant()} for {runnerShape}. Build frame: {buildFrame}. Story pressure: {pressure}. {gmClause} {editionClause} " +
               "Explain how the qualities, ware, attributes, first gear, and first contacts came from the backstory. " +
               "Alice should use the finished story as the seed for later suggestions.";
    }

    private static string HumanCopy(string? value)
        => UndetectableHumanizerCopyAdapter.Humanize(value);

    private static string[] HumanLines(IEnumerable<string> values)
        => UndetectableHumanizerCopyAdapter.HumanizeLines(values);

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

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
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            BorderThickness = new Thickness(1),
            Background = DesktopShellTheme.ResolveSurfaceAltBrush(),
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
            return "Draft the origin story first. Pick the edition before spending effort on presentation, voices, video, or later Alice follow-up.";
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
            return "Origin Book Studio starts with a story draft. Choose the edition, then set metatype, archetype, voices, and style before approving the draft into a book, memoir, or casefile path.";
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
            return "Only the edition, metatype, and archetype are needed to start. Build frame, pressure, GM constraints, voices, and style are optional. Finished characters are not changed. Runner Memoir is the premium guided manuscript path; shorter editions stay internal first.";
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
            return "Pick metatype and archetype, then draft the story.";
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

        if (!string.IsNullOrWhiteSpace(BuildCombinedGmAllowanceNotes()))
        {
            lines.Add($"GM allowances: {BuildCombinedGmAllowanceNotes()}");
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
                ?? "Open the proposal card below for the current preview.";
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

            if (!string.IsNullOrWhiteSpace(BuildCombinedGmAllowanceNotes()))
            {
                lines.Add($"GM allowances: {BuildCombinedGmAllowanceNotes()}");
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
            string? metatype = ResolveOriginMetatypeHint() ?? workspace?.Summary.Metatype;
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
        string metatype = FirstNonEmpty(ResolveOriginMetatypeHint(), workspace?.Summary.Metatype, "Unknown metatype");
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

        string combinedGmAllowanceNotes = BuildCombinedGmAllowanceNotes();
        string[] contradictionFlags = new string?[]
        {
            _selectedBuildPath?.Preview?.RequiresConfirmation == true ? "This path still requires explicit confirmation before apply." : null,
            _selectedBuildPath?.Preview?.DiagnosticMessages.Count > 0 ? string.Join(" | ", _selectedBuildPath.Preview.DiagnosticMessages.Take(2)) : null,
            _selectedHandoff?.Watchouts?.Count > 0 ? string.Join(" | ", _selectedHandoff.Watchouts.Take(2)) : null,
            !string.IsNullOrWhiteSpace(combinedGmAllowanceNotes) ? $"GM allowances: {combinedGmAllowanceNotes}" : null
        }
        .Where(static line => !string.IsNullOrWhiteSpace(line))
        .Take(3)
        .Cast<string>()
        .ToArray();

        string bookKind = ResolveOriginBookKind(_originEditionSelection);
        return new CharacterNarrativePacket(
            BookKind: bookKind,
            ProviderStrategy: ResolveOriginProviderStrategy(bookKind),
            Alias: alias,
            Metatype: metatype,
            BuildMethod: buildMethod,
            RulesetId: _rulesetId ?? workspace?.RulesetId ?? "unknown",
            ArchetypeHint: archetypeHint,
            Prompt: prompt,
            GmAllowanceNotes: string.IsNullOrWhiteSpace(combinedGmAllowanceNotes) ? null : combinedGmAllowanceNotes,
            BookSurface: _originBookSurfaceSelection,
            PrimaryVoiceStyle: _originPrimaryVoiceSelection,
            AlternateVoiceStyle: _originAlternateVoiceSelection,
            PortraitStyle: _originPortraitStyleSelection,
            VideoStyle: _originVideoStyleSelection,
            GmConstraintLabels: _originSelectedGmConstraints.OrderBy(static item => item, StringComparer.Ordinal).ToArray(),
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
            || string.Equals(_originArchetypeSelection, "Use current character role", StringComparison.Ordinal)
            || string.Equals(_originArchetypeSelection, "Custom from prompt", StringComparison.Ordinal)
                ? null
                : _originArchetypeSelection;

    private string? ResolveOriginMetatypeHint()
        => string.IsNullOrWhiteSpace(_originMetatypeSelection)
            || string.Equals(_originMetatypeSelection, "Use current character", StringComparison.Ordinal)
                ? null
                : _originMetatypeSelection;

    private string? ResolveOriginBuildFrameHint()
        => string.IsNullOrWhiteSpace(_originBuildFrameSelection)
            || string.Equals(_originBuildFrameSelection, "Use current ruleset", StringComparison.Ordinal)
                ? null
                : _originBuildFrameSelection;

    private static string ResolveOriginBookKind(string editionSelection)
        => editionSelection switch
        {
            "Origin Dossier" => OriginBookProjectKinds.OriginDossier,
            "Runner Memoir" => OriginBookProjectKinds.RunnerMemoir,
            "Intelligence Casefile" => OriginBookProjectKinds.IntelligenceCasefile,
            _ => OriginBookProjectKinds.NarrativeOrigin
        };

    private static bool ShouldUsePremiumGuidedAuthoring(string bookKind)
        => string.Equals(bookKind, OriginBookProjectKinds.RunnerMemoir, StringComparison.Ordinal);

    private static string ResolveOriginProviderStrategy(string bookKind)
        => bookKind switch
        {
            OriginBookProjectKinds.RunnerMemoir => OriginBookProviderStrategies.PremiumGuidedAuthoring,
            OriginBookProjectKinds.IntelligenceCasefile => OriginBookProviderStrategies.YoubooksGroundedDrafting,
            OriginBookProjectKinds.OriginDossier => OriginBookProviderStrategies.YoubooksGroundedDrafting,
            OriginBookProjectKinds.NarrativeOrigin => OriginBookProviderStrategies.InkfluenceNarrativeEdition,
            _ => OriginBookProviderStrategies.InkfluenceNarrativeEdition
        };

    private static string ResolveOriginProjectPhase(string bookKind)
        => ShouldUsePremiumGuidedAuthoring(bookKind)
            ? OriginBookProjectPhases.PremiumManuscriptQueued
            : OriginBookProjectPhases.ProviderAuthoringQueued;

    private static string ResolveOriginReviewState(string bookKind)
        => ShouldUsePremiumGuidedAuthoring(bookKind)
            ? OriginBookReviewStates.PremiumOutlineReviewRequired
            : OriginBookReviewStates.ProviderManuscriptReviewRequired;

    private static OriginBookPremiumManuscriptPlan BuildOriginPremiumPlan(string bookKind)
        => ShouldUsePremiumGuidedAuthoring(bookKind)
            ? new OriginBookPremiumManuscriptPlan(
                PremiumGuidedAuthoringRequired: true,
                QueueStatus: "queued_for_operator_guided_authoring",
                Provider: "First Book AI",
                ManuscriptTarget: "25000-35000 words / 8 chapter premium memoir",
                OutlinePosture: "outline_approval_required_before_credit_use",
                HumanChapterReviewRequired: true)
            : new OriginBookPremiumManuscriptPlan(
                PremiumGuidedAuthoringRequired: false,
                QueueStatus: "queued_for_operator_authoring",
                Provider: ResolveOriginAuthoringProvider(bookKind),
                ManuscriptTarget: ResolveOriginManuscriptTarget(bookKind),
                OutlinePosture: "chummer_packet_then_provider_authoring_then_humanizer_post_step",
                HumanChapterReviewRequired: true);

    private static string ResolveOriginAuthoringProvider(string bookKind)
        => bookKind switch
        {
            OriginBookProjectKinds.OriginDossier => "Youbooks",
            OriginBookProjectKinds.IntelligenceCasefile => "Youbooks",
            OriginBookProjectKinds.RunnerMemoir => "First Book AI",
            _ => "Inkfluence"
        };

    private static string ResolveOriginManuscriptTarget(string bookKind)
        => bookKind switch
        {
            OriginBookProjectKinds.OriginDossier => "source-grounded origin dossier draft from approved Chummer packet",
            OriginBookProjectKinds.IntelligenceCasefile => "source-grounded intelligence casefile draft from approved Chummer packet",
            OriginBookProjectKinds.RunnerMemoir => "25000-35000 words / 8 chapter premium memoir",
            _ => "cinematic narrative origin edition from approved Chummer packet"
        };

    private static string ResolveOriginAuthoringRole(string bookKind)
        => bookKind switch
        {
            OriginBookProjectKinds.OriginDossier => "source-grounded dossier authoring",
            OriginBookProjectKinds.IntelligenceCasefile => "source-grounded casefile authoring",
            OriginBookProjectKinds.RunnerMemoir => "premium guided manuscript authoring",
            _ => "cinematic narrative edition authoring"
        };

    private static OriginBookCanonAudit BuildOriginCanonAudit(CharacterNarrativePacket packet)
    {
        string[] probableConflicts = packet.ContradictionFlags
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(3)
            .Select(static line => UndetectableHumanizerCopyAdapter.Humanize(line))
            .ToArray();
        string[] privacyFindings = packet.GmConstraintLabels
            .Where(label => label.Contains("private", StringComparison.OrdinalIgnoreCase)
                || label.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || label.Contains("gm-only", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .Select(static label => $"Review whether '{label}' is excluded from the export packet.")
            .ToArray();
        string[] inventedGameEffects = ShouldUsePremiumGuidedAuthoring(packet.BookKind)
            ? ["No manuscript has been imported yet; block any new skills, gear, contacts, debts, or qualities until chapter review is complete."]
            : [];
        string auditStatus = probableConflicts.Length > 0 || privacyFindings.Length > 0
            ? OriginBookCanonAuditStates.ReviewRequired
            : OriginBookCanonAuditStates.ProvisionalPass;

        return new OriginBookCanonAudit(
            AuditStatus: auditStatus,
            HardConflicts: [],
            ProbableConflicts: probableConflicts,
            InventedEntities: [],
            InventedGameEffects: inventedGameEffects,
            PrivacyFindings: privacyFindings);
    }

    private static OriginBookPremiumReviewArtifacts BuildOriginPremiumReviewArtifacts(
        string bundleDirectory,
        CharacterNarrativePacket packet,
        CharacterNarrativeDraft draft)
    {
        if (!ShouldUsePremiumGuidedAuthoring(packet.BookKind))
        {
            return new OriginBookPremiumReviewArtifacts(
                OutlineReviewState: OriginBookPremiumReviewStates.NotApplicable,
                ChapterReviewState: OriginBookPremiumReviewStates.NotApplicable,
                OutlineMarkdownPath: null,
                ChapterPlanJsonPath: null,
                ChapterReviewPaths: []);
        }

        string outlineMarkdownPath = Path.Combine(bundleDirectory, "premium-outline-review.md");
        string chapterPlanJsonPath = Path.Combine(bundleDirectory, "premium-chapter-plan.json");
        string chapterReviewDirectory = Path.Combine(bundleDirectory, "premium-chapter-reviews");
        Directory.CreateDirectory(chapterReviewDirectory);

        (string Title, string Objective)[] chapters =
        [
            ("The World Before", "Establish the runner's pre-shadow life and the pressure that made the old identity unsustainable."),
            ("First Bond", "Anchor the memoir in one durable relationship that shaped the runner's trust model."),
            ("First Loss", "Show the first irreversible cost that still explains a present-day scar, quality, or habit."),
            ("The Line Crossed", "Define the decision that made retreat unrealistic and tied the runner to the current path."),
            ("Consequences", "Trace the operational and emotional fallout that hardened the runner's present-day methods."),
            ("Mentor, Crew, or Found Family", "Explain which alliance taught the runner how to survive and what it still costs to maintain."),
            ("Betrayal or Irreversible Choice", "Lock in the break, betrayal, or compromise that the memoir must not soften or rewrite away."),
            ("Becoming the Runner", "Conclude with the present-day runner identity and the unresolved threads that still matter in campaign time.")
        ];

        string outlineMarkdown = string.Join(
            Environment.NewLine,
            new[]
            {
                $"# Runner Memoir Outline Review · {packet.Alias}",
                string.Empty,
                $"Canon summary: {draft.Summary}",
                $"Provider lane: {ResolveOriginProviderStrategy(packet.BookKind)}",
                "Credit rule: do not spend the premium manuscript lane until the outline, voice, and chapter objectives are approved.",
                string.Empty,
                "## Chapter spine"
            }
            .Concat(chapters.Select((chapter, index) => $"{index + 1}. {chapter.Title} - {chapter.Objective}"))
            .Concat(
            [
                string.Empty,
                "## Canon guardrails",
                "- No unapproved contacts, enemies, debts, skills, gear, or qualities.",
                "- Keep chronology aligned with the approved runner packet.",
                "- Preserve uncertainty where the packet is incomplete.",
                $"- Run imported provider prose through {OriginBookPostProcessingSteps.UndetectableHumanizer} before player-facing publication."
            ]));
        File.WriteAllText(outlineMarkdownPath, outlineMarkdown);

        List<string> chapterReviewPaths = [];
        for (int index = 0; index < chapters.Length; index++)
        {
            string chapterReviewPath = Path.Combine(chapterReviewDirectory, $"chapter-{index + 1:D2}-review.md");
            File.WriteAllText(
                chapterReviewPath,
                string.Join(
                    Environment.NewLine,
                    [
                        $"# Chapter {index + 1} Review · {chapters[index].Title}",
                        string.Empty,
                        $"Objective: {chapters[index].Objective}",
                        "Status: review_required",
                        "Canon checks:",
                        "- chronology",
                        "- relationships",
                        "- knowledge boundaries",
                        "- no invented mechanical effects"
                    ]));
            chapterReviewPaths.Add(chapterReviewPath);
        }

        File.WriteAllText(chapterPlanJsonPath, JsonSerializer.Serialize(
            new
            {
                artifactKind = "premium_runner_memoir_chapter_plan",
                chapterCount = chapters.Length,
                creditSpendingRule = "approve_outline_before_credit_use",
                chapters = chapters.Select((chapter, index) => new
                {
                    chapterNumber = index + 1,
                    title = chapter.Title,
                    objective = chapter.Objective,
                    reviewPath = chapterReviewPaths[index],
                    reviewState = "review_required"
                })
            },
            new JsonSerializerOptions { WriteIndented = true }));

        return new OriginBookPremiumReviewArtifacts(
            OutlineReviewState: OriginBookPremiumReviewStates.OutlineReviewRequired,
            ChapterReviewState: OriginBookPremiumReviewStates.ChapterReviewRequired,
            OutlineMarkdownPath: outlineMarkdownPath,
            ChapterPlanJsonPath: chapterPlanJsonPath,
            ChapterReviewPaths: chapterReviewPaths);
    }

    private static CharacterNarrativeDraft BuildOriginDraft(CharacterNarrativePacket packet)
    {
        string summary = packet.BookKind switch
        {
            OriginBookProjectKinds.RunnerMemoir => $"{packet.Alias} remembers the first night they stopped waiting for permission and chose the life that would become theirs.",
            OriginBookProjectKinds.IntelligenceCasefile => $"{packet.Alias} first appears in the record at the moment a bad job turns into a permanent identity.",
            _ => $"{packet.Alias} becomes real in the moment survival stops being theory and starts asking for a price."
        };
        string prose = string.Join(
            Environment.NewLine + Environment.NewLine,
            BuildOriginStoryParagraphs(packet));
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
            Prose: HumanCopy(prose),
            GmHooks: HumanLines(gmHooks),
            RuntimeFingerprint: packet.RuntimeFingerprint);
    }

    private static string[] BuildOriginStoryParagraphs(CharacterNarrativePacket packet)
    {
        string alias = FirstNonEmpty(packet.Alias, "The runner");
        string metatype = FirstNonEmpty(packet.Metatype, "runner");
        string archetype = FirstNonEmpty(packet.ArchetypeHint, "operator").ToLowerInvariant();
        string buildMethod = FirstNonEmpty(packet.BuildMethod, "survival").ToLowerInvariant();
        string pressure = FirstNonEmpty(packet.LeadBuildPathTitle, packet.CausalityHints.FirstOrDefault(), packet.StandoutSignals.FirstOrDefault(), "the kind of work that did not forgive hesitation").TrimEnd('.');
        string gmPressure = FirstNonEmpty(packet.GmAllowanceNotes, packet.GmConstraintLabels.FirstOrDefault(), "no one was coming to make the choice clean").TrimEnd('.');

        if (string.Equals(packet.BookKind, OriginBookProjectKinds.RunnerMemoir, StringComparison.Ordinal))
        {
            return
            [
                $"The first thing I remember is the lock giving way under my hand and the hallway holding its breath. Not silence. Silence was clean. This was the pause before shouting, before boots, before somebody decided a {metatype.ToLowerInvariant()} with a borrowed access card had become a problem worth solving permanently.",
                $"I had gone in as a nobody with a cheap plan and a cheaper backup. By the time I reached the service stair, the plan was dead, the backup was bleeding out behind a maintenance door, and every camera in the building had learned my shape. I kept moving because stopping would have made the night simple, and nothing about me had ever been simple.",
                $"That was where the {archetype} in me started to take form. Not in a classroom, not in a clean test, not in a story I could tell without leaving parts out. It started with {pressure}, with my hands shaking and my mind getting colder because fear had finally become useful.",
                $"People like to pretend a runner is made by gear, contacts, or one lucky job. They are wrong. I became one because {gmPressure}, and because the next door still had to open. Every habit I carry now came from that passage: check the exits, trust the signal last, and never let anyone else name the cost of my survival.",
                $"By dawn, the city had already started rewriting what happened. The official story got smaller. The witnesses got quieter. I kept the parts that mattered: the sound of the lock, the weight of the choice, and the knowledge that the life ahead of me was not waiting to be found. It had to be taken."
            ];
        }

        if (string.Equals(packet.BookKind, OriginBookProjectKinds.IntelligenceCasefile, StringComparison.Ordinal))
        {
            return
            [
                $"The first verified sighting of {alias} begins with a service corridor, a failed security rotation, and three minutes missing from the building feed. The subject enters frame as a {metatype.ToLowerInvariant()} with no visible support team and leaves it as the only person still moving with purpose.",
                $"Witness statements disagree on the small details. One remembers blood on the floor. Another remembers a burned access panel. A third insists the subject stopped long enough to pull someone else out of the line of fire. The useful fact is simpler: {alias} did not freeze when the job collapsed.",
                $"From that point forward, the pattern holds. The {archetype} profile is not cosmetic; it appears to be learned behavior under pressure. {pressure} explains the subject's later caution, their preference for exits, and the habit of turning every tool into a contingency before trusting it as an advantage.",
                $"The unresolved question is motive. The record suggests {gmPressure}, but the file cannot prove whether that pressure made {alias} ruthless or merely awake. What it can prove is that the old civilian boundary did not survive the night.",
                $"Current assessment: {alias} should be treated as someone formed by a specific failure, not by ambition alone. The first incident did not give them a legend. It gave them a method."
            ];
        }

        return
        [
            $"The hallway outside the clinic smelled of hot plastic, antiseptic, and rainwater dragged in from the alley. {alias} stood under the dead security light with one hand on the door and the other pressed flat against the wall, feeling the vibration of footsteps through concrete before the voices reached the stairwell.",
            $"A minute earlier, there had still been a way to pretend this was a mistake. A wrong room. A bad address. One more job that could be walked back if everyone stayed calm. Then the lock clicked open, the alarm swallowed its own first note, and the person behind {alias} whispered, \"Run.\"",
            $"{alias} ran, but not blindly. The {metatype.ToLowerInvariant()} moved like someone learning the shape of a new life one decision at a time: count the doors, read the camera angles, keep the breathing quiet, leave nothing soft enough for the city to use as a handle. Whatever {buildMethod} had promised before that night, it became real in the space between the first shout and the first shot.",
            $"The choice that made the runner was not heroic. It was smaller and harder than that. {alias} could have gone back, could have waited for someone with cleaner hands to decide what happened next. Instead, {pressure} became the line on the floor, and crossing it changed the weight of every future favor, debt, scar, and name.",
            $"By morning, the rain had washed the alley clean enough for strangers to ignore. {alias} kept moving anyway. They had learned what the city teaches its useful ghosts: survival is not a talent, it is a practice, and the first real lesson always costs more than anyone admits."
        ];
    }

    private static string[] BuildOriginEvidence(CharacterNarrativePacket packet, CharacterNarrativeDraft draft)
    {
        List<string> lines =
        [
            $"Runner: {packet.Alias} · {packet.Metatype}",
            $"Edition: {packet.BookKind}",
            $"Provider strategy: {packet.ProviderStrategy}",
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

    private IReadOnlyList<string> BuildOriginDraftReviewEvidence()
    {
        List<string> lines = [];
        if (!string.IsNullOrWhiteSpace(_originDraftDirectory))
        {
            lines.Add($"Draft folder: {_originDraftDirectory}");
        }
        if (!string.IsNullOrWhiteSpace(_originDraftMarkdownPath))
        {
            lines.Add($"Story: {Path.GetFileName(_originDraftMarkdownPath)}");
        }
        if (!string.IsNullOrWhiteSpace(_originDraftMyFirstBookPresentationPath))
        {
            lines.Add($"Presentation: {Path.GetFileName(_originDraftMyFirstBookPresentationPath)}");
        }

        return HumanLines(lines);
    }

    private void EnsureOriginDraftReviewPacket(CharacterNarrativePacket packet, CharacterNarrativeDraft draft)
    {
        if (!string.IsNullOrWhiteSpace(_originDraftMarkdownPath)
            && File.Exists(_originDraftMarkdownPath)
            && !string.IsNullOrWhiteSpace(_originDraftMyFirstBookPacketPath)
            && File.Exists(_originDraftMyFirstBookPacketPath)
            && !string.IsNullOrWhiteSpace(_originDraftMyFirstBookPresentationPath)
            && File.Exists(_originDraftMyFirstBookPresentationPath))
        {
            return;
        }

        string aliasToken = string.IsNullOrWhiteSpace(packet.Alias) ? "runner" : SanitizeNameToken(packet.Alias).ToLowerInvariant();
        string timestampToken = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        string draftDirectory = Path.Combine(Path.GetTempPath(), "chummer-origin-dossier-drafts", $"{timestampToken}-{aliasToken}");
        Directory.CreateDirectory(draftDirectory);

        string markdownPath = Path.Combine(draftDirectory, "origin-story-draft.md");
        string myFirstBookPacketPath = Path.Combine(draftDirectory, "myfirstbook-origin-story.packet.json");
        string myFirstBookPresentationPath = Path.Combine(draftDirectory, "myfirstbook-origin-story.presentation.html");
        File.WriteAllText(markdownPath, BuildOriginCanonMarkdown(packet, draft));
        File.WriteAllText(myFirstBookPacketPath, BuildMyFirstBookOriginStoryPacket(packet, draft, markdownPath, "draft_review", DateTimeOffset.UtcNow));
        File.WriteAllText(myFirstBookPresentationPath, BuildMyFirstBookOriginPresentationHtml(packet, draft, "Draft story", markdownPath));

        _originDraftDirectory = draftDirectory;
        _originDraftMarkdownPath = markdownPath;
        _originDraftMyFirstBookPacketPath = myFirstBookPacketPath;
        _originDraftMyFirstBookPresentationPath = myFirstBookPresentationPath;
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static async Task<MyFirstBookQuotaConsumeResultDto> EnsureMyFirstBookAllowanceAsync()
    {
        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Link your copy before creating a MyFirstBook origin book."));
        return await client.ConsumeMyFirstBookQuotaAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private static async Task<MyFirstBookQuotaSnapshotDto?> TryGetMyFirstBookQuotaSnapshotAsync()
    {
        if (App.Services?.GetService(typeof(IChummerClient)) is not IChummerClient client)
        {
            return null;
        }

        try
        {
            return await client.GetMyFirstBookQuotaAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<string>> GetMyFirstBookQuotaEvidenceAsync()
    {
        MyFirstBookQuotaSnapshotDto? quota = await TryGetMyFirstBookQuotaSnapshotAsync().ConfigureAwait(true);
        if (quota is null)
        {
            return Array.Empty<string>();
        }

        string planLabel = quota.SupporterActive ? "Supporter" : "Free";
        return HumanLines(
        [
            $"MyFirstBook left this month: {quota.MonthlyRemaining} of {quota.MonthlyLimit} ({planLabel})"
        ]);
    }

    private static bool ShouldAllowLivePremiumConsumption()
        => string.Equals(Environment.GetEnvironmentVariable(OriginPremiumAllowLiveConsumptionEnv), "1", StringComparison.Ordinal);

    private string BuildSeededAssistantMessage(string mode, string message)
    {
        string combinedGmAllowanceNotes = BuildCombinedGmAllowanceNotes();
        string allowancePrefix = string.IsNullOrWhiteSpace(combinedGmAllowanceNotes)
            ? string.Empty
            : $"GM allowances: {combinedGmAllowanceNotes}{Environment.NewLine}";

        if (IsOriginDossierMode(mode))
        {
            if (_originBundle is not null)
            {
                return $"{allowancePrefix}Approved origin story summary: {_originBundle.Canon.Summary}{Environment.NewLine}Approved origin story prose: {_originBundle.Canon.Prose}{Environment.NewLine}User request: {message}";
            }

            if (_originDraft is not null)
            {
                return $"{allowancePrefix}Origin story draft summary: {_originDraft.Summary}{Environment.NewLine}Origin story draft prose: {_originDraft.Prose}{Environment.NewLine}User request: {message}";
            }

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
               $"Early gear and ware: {gear}. " +
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

        if (!string.IsNullOrWhiteSpace(BuildCombinedGmAllowanceNotes()))
        {
            lines.Add($"GM allowances: {BuildCombinedGmAllowanceNotes()}");
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
        => string.IsNullOrWhiteSpace(BuildCombinedGmAllowanceNotes())
            ? detail
            : $"{detail} GM allowances: {BuildCombinedGmAllowanceNotes()}.";

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
        string projectArchiveJsonPath = Path.Combine(bundleDirectory, "origin-book-project.json");
        string myFirstBookPacketPath = Path.Combine(bundleDirectory, "myfirstbook-origin-story.packet.json");
        string myFirstBookPresentationPath = Path.Combine(bundleDirectory, "myfirstbook-origin-story.presentation.html");
        File.WriteAllText(canonMarkdownPath, BuildOriginCanonMarkdown(_originPacket, _originDraft));
        File.WriteAllText(myFirstBookPacketPath, BuildMyFirstBookOriginStoryPacket(_originPacket, _originDraft, canonMarkdownPath, "approved_story", DateTimeOffset.UtcNow));
        File.WriteAllText(myFirstBookPresentationPath, BuildMyFirstBookOriginPresentationHtml(_originPacket, _originDraft, "Approved story", canonMarkdownPath));

        DateTimeOffset approvedAtUtc = DateTimeOffset.UtcNow;
        OriginBookCanonAudit canonAudit = BuildOriginCanonAudit(_originPacket);
        OriginBookPremiumReviewArtifacts premiumReview = BuildOriginPremiumReviewArtifacts(bundleDirectory, _originPacket, _originDraft);
        OriginBookArtifactSet artifacts = new(
            BundleDirectory: bundleDirectory,
            CanonJsonPath: canonJsonPath,
            CanonMarkdownPath: canonMarkdownPath,
            ProjectArchiveJsonPath: projectArchiveJsonPath,
            MyFirstBookPacketPath: myFirstBookPacketPath,
            MyFirstBookPresentationPath: myFirstBookPresentationPath,
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
            InkfluencePacketPath: null,
            InkfluenceScriptPath: null,
            UnmixrPacketPath: null,
            UnmixrScriptPath: null,
            MediaFactoryNarrationRequestPath: null,
            MediaFactoryNarrationRunbookPath: null,
            MediaFactoryNarrationReceiptPath: null,
            VidBoardPacketPath: null,
            VideoStoryboardPath: null,
            VideoPosterPath: null,
            MediaFactoryVideoReceiptPath: null,
            RenderedVideoPath: null);

        _originBundle = new OriginDossierBundle(
            ProjectId: $"origin-book:{aliasToken}:{approvedAtUtc.UtcDateTime:yyyyMMddHHmmss}",
            BookKind: _originPacket.BookKind,
            ProjectStatus: OriginBookProjectStatuses.ApprovedStory,
            Packet: _originPacket,
            Canon: _originDraft,
            CanonAudit: canonAudit,
            Approval: new OriginBookApprovalState(
                ProjectPhase: ResolveOriginProjectPhase(_originPacket.BookKind),
                ReviewState: ResolveOriginReviewState(_originPacket.BookKind),
                ApprovedAtUtc: approvedAtUtc,
                ApprovedBy: "desktop_alice_player_approval"),
            PremiumPlan: BuildOriginPremiumPlan(_originPacket.BookKind),
            PremiumReview: premiumReview,
            Publication: OriginBookGoldPublication.Pending(),
            Artifacts: artifacts,
            GmAllowanceNotes: _originPacket.GmAllowanceNotes,
            RuntimeFingerprint: _originDraft.RuntimeFingerprint);
        PersistOriginBookProjectFiles(_originBundle);
        return _originBundle;
    }

    private static OriginDossierBundle UpdateOriginProjectArtifacts(OriginDossierBundle project, OriginBookArtifactSet artifacts)
        => project with { Artifacts = artifacts };

    private static void PersistOriginBookProjectFiles(OriginDossierBundle project)
    {
        File.WriteAllText(project.CanonJsonPath, JsonSerializer.Serialize(
            new
            {
                artifactKind = "origin_canon",
                originBookProject = true,
                projectId = project.ProjectId,
                bookKind = project.BookKind,
                providerStrategy = project.Packet.ProviderStrategy,
                projectStatus = project.ProjectStatus,
                projectPhase = project.ProjectPhase,
                reviewState = project.ReviewState,
                auditStatus = project.AuditStatus,
                approvedAtUtc = project.ApprovedAtUtc,
                canonAudit = project.CanonAudit,
                premiumPlan = project.PremiumPlan,
                premiumReview = project.PremiumReview,
                goldPublicationReady = project.Publication.IsGoldReady,
                goldPublicationMissing = project.Publication.MissingGoldRequirements,
                publication = project.Publication,
                gmAllowanceNotes = project.GmAllowanceNotes,
                packet = project.Packet,
                canon = project.Canon,
                providerLanes = new
                {
                    document = "MarkupGo",
                    portraits = "First-party render",
                    scenes = "First-party render",
                    narrationDefault = "Inkfluence",
                    narrationAlternate = "Unmixr",
                    dossierVideo = "vidBoard"
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(project.ProjectArchiveJsonPath, JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
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
            HumanCopy(bundle.Canon.Prose),
            string.Empty,
            $"Authoring provider: {ResolveOriginAuthoringProvider(bundle.BookKind)}",
            $"Authoring role: {ResolveOriginAuthoringRole(bundle.BookKind)}",
            $"Post step: {OriginBookPostProcessingSteps.UndetectableHumanizer}",
            $"Origin summary: {HumanCopy(bundle.Canon.Summary)}",
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
            .. bundle.Canon.GmHooks.Select(static hook => $"- {UndetectableHumanizerCopyAdapter.Humanize(hook)}"),
            string.Empty,
            "Open questions:",
            .. bundle.Packet.ContradictionFlags.DefaultIfEmpty("None found in the current character context.").Select(static line => $"- {UndetectableHumanizerCopyAdapter.Humanize(line)}")
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
                authoringProvider = ResolveOriginAuthoringProvider(bundle.BookKind),
                authoringRole = ResolveOriginAuthoringRole(bundle.BookKind),
                postProcessingRequired = OriginBookPostProcessingSteps.UndetectableHumanizer,
                sections = dossierLines,
                sourceCanon = new
                {
                    bundle.CanonMarkdownPath,
                    bundle.CanonJsonPath
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
            {
                DossierPdfPath = pdfPath,
                MarkupGoPacketPath = markupGoPacketPath
            });
        PersistOriginBookProjectFiles(updated);
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

        OriginPortraitCandidate[] candidates = new OriginPortraitCandidate[]
        {
            new("portrait-candidate-01", "Noir Ink", "Grounded dossier portrait with low-noise contrast.", "#0F172A", "#1D4ED8", "#E2E8F0", "#93C5FD"),
            new("portrait-candidate-02", "Chrome Editorial", "Sharper editorial framing with brighter chrome accents.", "#111827", "#0F766E", "#F8FAFC", "#67E8F9"),
            new("portrait-candidate-03", "Neon Street", "Street-lit version with stronger nightlife saturation.", "#1F1630", "#7C3AED", "#F5F3FF", "#C084FC"),
            new("portrait-candidate-04", "Quiet Clinic", "Cold medical scene with controlled sterile highlights.", "#17202A", "#475569", "#F8FAFC", "#CBD5E1")
        }
        .OrderByDescending(candidate => string.Equals(candidate.StyleLabel, _originPortraitStyleSelection, StringComparison.Ordinal))
        .ToArray();

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

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
            {
                PortraitSetJsonPath = portraitSetJsonPath,
                PortraitContactSheetPath = contactSheetPath,
                PortraitCandidatePaths = portraitPaths,
                SelectedPortraitPath = portraitPaths[0]
            });
        PersistOriginBookProjectFiles(updated);
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

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
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
            });

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

        PersistOriginBookProjectFiles(updated);
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
            new("scene-candidate-02", "Clinic Memory", "A sterile upgrade scene that explains the cost of implants and quality drift.", "#111827", "#0F766E"),
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

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
            {
                SceneBriefMarkdownPath = sceneBriefMarkdownPath,
                SceneSetJsonPath = sceneSetJsonPath,
                SceneCandidatePaths = scenePaths,
                SelectedScenePath = scenePaths[0]
            });
        PersistOriginBookProjectFiles(updated);
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

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
            {
                SelectedScenePath = bundle.SceneCandidatePaths[index],
                VidBoardPacketPath = null,
                VideoStoryboardPath = null,
                VideoPosterPath = null,
                MediaFactoryVideoReceiptPath = null,
                RenderedVideoPath = null
            });

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

        PersistOriginBookProjectFiles(updated);
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
                videoStyle = _originVideoStyleSelection,
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

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
            {
                VidBoardPacketPath = packetPath,
                VideoStoryboardPath = storyboardPath,
                VideoPosterPath = posterPath,
                MediaFactoryVideoReceiptPath = null,
                RenderedVideoPath = null
            });
        PersistOriginBookProjectFiles(updated);
        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle EnsureInkfluenceNarrationPacket(OriginDossierBundle bundle)
    {
        if (!string.IsNullOrWhiteSpace(bundle.InkfluencePacketPath)
            && !string.IsNullOrWhiteSpace(bundle.InkfluenceScriptPath)
            && File.Exists(bundle.InkfluencePacketPath)
            && File.Exists(bundle.InkfluenceScriptPath))
        {
            return bundle;
        }

        string scriptPath = Path.Combine(bundle.BundleDirectory, "inkfluence-origin-reading.txt");
        string packetPath = Path.Combine(bundle.BundleDirectory, "inkfluence-origin-reading.packet.json");
        string script = BuildInkfluenceNarrationScript(bundle);
        File.WriteAllText(scriptPath, script);
        File.WriteAllText(packetPath, JsonSerializer.Serialize(
            new
            {
                tool = "Inkfluence",
                artifactKind = "origin_audiobook_inkfluence_voice",
                approvedAtUtc = bundle.ApprovedAtUtc,
                source = "first_party_origin_canon",
                title = $"{bundle.Packet.Alias} Origin Reading",
                narrationMode = "inkfluence_audiobook_chapter",
                preferredVoice = _originPrimaryVoiceSelection,
                scriptPath,
                durationTargetSeconds = 75,
                sourceCanon = new
                {
                    bundle.CanonMarkdownPath,
                    bundle.CanonJsonPath
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
            {
                InkfluencePacketPath = packetPath,
                InkfluenceScriptPath = scriptPath
            });
        PersistOriginBookProjectFiles(updated);
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
                preferredVoice = _originAlternateVoiceSelection,
                scriptPath,
                durationTargetSeconds = 75,
                sourceCanon = new
                {
                    bundle.CanonMarkdownPath,
                    bundle.CanonJsonPath
                }
            },
            new JsonSerializerOptions { WriteIndented = true }));

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
            {
                UnmixrPacketPath = packetPath,
                UnmixrScriptPath = scriptPath
            });
        PersistOriginBookProjectFiles(updated);
        _originBundle = updated;
        return updated;
    }

    private OriginDossierBundle EnsureOriginMediaFactoryNarrationRequest(OriginDossierBundle bundle)
    {
        bundle = EnsureInkfluenceNarrationPacket(bundle);
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
                    @default = "Inkfluence",
                    alternate = "Unmixr"
                },
                narrationArtifacts = new object[]
                {
                    new
                    {
                        role = "audio",
                        provider = "Inkfluence",
                        providerState = "promoted",
                        outputFormat = "mp3",
                        variant = "inkfluence_voice",
                        preferredVoice = _originPrimaryVoiceSelection,
                        companionRef = $"{approvedOriginPacketId}/audio/inkfluence",
                        scriptPath = bundle.InkfluenceScriptPath,
                        packetPath = bundle.InkfluencePacketPath,
                        captionRefs = new[] {$"{approvedOriginPacketId}/caption/inkfluence"},
                        previewRefs = new[] {$"{approvedOriginPacketId}/preview/inkfluence"}
                    },
                    new
                    {
                        role = "audio",
                        provider = "Unmixr",
                        providerState = "candidate",
                        outputFormat = "mp3",
                        variant = "alternate_voice",
                        preferredVoice = _originAlternateVoiceSelection,
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

        OriginDossierBundle updated = UpdateOriginProjectArtifacts(
            bundle,
            bundle.Artifacts with
            {
                MediaFactoryNarrationRequestPath = requestPath,
                MediaFactoryNarrationRunbookPath = runbookPath,
                MediaFactoryNarrationReceiptPath = null
            });
        PersistOriginBookProjectFiles(updated);
        _originBundle = updated;
        return updated;
    }

    private IReadOnlyList<string> BuildOriginBundleEvidence(
        OriginDossierBundle bundle,
        MyFirstBookQuotaSnapshotDto? quota = null,
        bool premiumConsumptionDeferred = false)
    {
        List<string> lines =
        [
            $"Dossier folder: {bundle.BundleDirectory}",
            $"Project archive: {Path.GetFileName(bundle.ProjectArchiveJsonPath)}",
            $"Edition: {bundle.BookKind}",
            $"Phase: {bundle.ProjectPhase}",
            $"Review: {bundle.ReviewState}",
            $"Canon audit: {bundle.AuditStatus}",
            $"Story: {Path.GetFileName(bundle.CanonMarkdownPath)}",
            $"Presentation: {Path.GetFileName(bundle.MyFirstBookPresentationPath)}",
            $"Story data: {Path.GetFileName(bundle.CanonJsonPath)}",
            $"Book surface: {_originBookSurfaceSelection}",
            $"Main voice: {_originPrimaryVoiceSelection}",
            $"Alternate voice: {_originAlternateVoiceSelection}",
            $"Portrait style: {_originPortraitStyleSelection}",
            $"Video style: {_originVideoStyleSelection}"
        ];

        if (quota is not null)
        {
            string planLabel = quota.SupporterActive ? "Supporter" : "Free";
            lines.Add($"MyFirstBook left this month: {quota.MonthlyRemaining} of {quota.MonthlyLimit} ({planLabel})");
        }

        if (bundle.PremiumPlan.PremiumGuidedAuthoringRequired)
        {
            lines.Add($"Premium manuscript queue: {bundle.PremiumPlan.QueueStatus}");
            lines.Add($"Premium provider: {bundle.PremiumPlan.Provider}");
            lines.Add($"Memoir target: {bundle.PremiumPlan.ManuscriptTarget}");
            lines.Add($"Outline posture: {bundle.PremiumPlan.OutlinePosture}");
            lines.Add($"Outline review: {bundle.OutlineReviewState}");
            lines.Add($"Chapter review: {bundle.ChapterReviewState}");
            if (premiumConsumptionDeferred)
            {
                lines.Add("Premium credit spend: deferred until live premium authoring is explicitly enabled.");
            }
        }

        foreach (string conflict in bundle.CanonAudit.ProbableConflicts.Take(2))
        {
            lines.Add($"Canon tension: {conflict}");
        }

        foreach (string finding in bundle.CanonAudit.PrivacyFindings.Take(2))
        {
            lines.Add($"Privacy review: {finding}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.GmAllowanceNotes))
        {
            lines.Add($"GM allowances: {bundle.GmAllowanceNotes}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.PremiumOutlineMarkdownPath))
        {
            lines.Add($"Memoir outline: {Path.GetFileName(bundle.PremiumOutlineMarkdownPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.PremiumChapterPlanJsonPath))
        {
            lines.Add($"Chapter plan: {Path.GetFileName(bundle.PremiumChapterPlanJsonPath)}");
        }

        if (bundle.PremiumChapterReviewPaths.Count > 0)
        {
            lines.Add($"Chapter review packets: {bundle.PremiumChapterReviewPaths.Count}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.DossierPdfPath))
        {
            lines.Add($"Book: {Path.GetFileName(bundle.DossierPdfPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MarkupGoPacketPath))
        {
            lines.Add($"Book source: {Path.GetFileName(bundle.MarkupGoPacketPath)}");
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

        if (!string.IsNullOrWhiteSpace(bundle.InkfluenceScriptPath))
        {
            lines.Add($"Inkfluence voice script: {Path.GetFileName(bundle.InkfluenceScriptPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.InkfluencePacketPath))
        {
            lines.Add($"Inkfluence voice notes: {Path.GetFileName(bundle.InkfluencePacketPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.UnmixrScriptPath))
        {
            lines.Add($"Alternate voice script: {Path.GetFileName(bundle.UnmixrScriptPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.UnmixrPacketPath))
        {
            lines.Add($"Alternate voice notes: {Path.GetFileName(bundle.UnmixrPacketPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRequestPath))
        {
            lines.Add($"Audiobook setup: {Path.GetFileName(bundle.MediaFactoryNarrationRequestPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRunbookPath))
        {
            lines.Add($"Audiobook brief: {Path.GetFileName(bundle.MediaFactoryNarrationRunbookPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationReceiptPath))
        {
            lines.Add($"Audio details: {Path.GetFileName(bundle.MediaFactoryNarrationReceiptPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.VideoStoryboardPath))
        {
            lines.Add($"Video storyboard: {Path.GetFileName(bundle.VideoStoryboardPath)}");
        }

        if (!string.IsNullOrWhiteSpace(bundle.VidBoardPacketPath))
        {
            lines.Add($"Video plan: {Path.GetFileName(bundle.VidBoardPacketPath)}");
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

        lines.Add($"Gold publication: {(bundle.Publication.IsGoldReady ? "ready" : "not_ready")}");
        foreach (string missing in bundle.Publication.MissingGoldRequirements.Take(4))
        {
            lines.Add($"Gold missing: {missing}");
        }

        if (!ShouldAllowLiveMediaFactoryExecution())
        {
            lines.Add($"Live render locked: set {MediaFactoryAllowLiveExecutionEnv}=1 only when real provider execution is approved.");
        }

        return HumanLines(lines);
    }

    private static async Task<string> ExecuteOriginMediaFactoryNarrationAsync(OriginDossierBundle bundle)
    {
        if (string.IsNullOrWhiteSpace(bundle.MediaFactoryNarrationRequestPath))
        {
            throw new InvalidOperationException("Origin dossier bundle is missing the media-factory narration request.");
        }
        if (!ShouldAllowLiveMediaFactoryExecution())
        {
            throw new InvalidOperationException($"Live Origin Dossier audiobook rendering is disabled. Set {MediaFactoryAllowLiveExecutionEnv}=1 only after provider execution is approved.");
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
        if (!ShouldAllowLiveMediaFactoryExecution())
        {
            throw new InvalidOperationException($"Live Origin Dossier video rendering is disabled. Set {MediaFactoryAllowLiveExecutionEnv}=1 only after provider execution is approved.");
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

    private static bool ShouldAllowLiveMediaFactoryExecution()
        => string.Equals(Environment.GetEnvironmentVariable(MediaFactoryAllowLiveExecutionEnv), "1", StringComparison.Ordinal);

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
                                Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMediaOverlayForegroundBrush", "#F8FAFC")
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
                                    Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMediaOverlayForegroundBrush", "#F8FAFC")
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
                                    Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMediaOverlayForegroundBrush", "#F8FAFC"),
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
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellMediaBackdropBrush", "#020617"),
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
                                Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMediaOverlayForegroundBrush", "#F8FAFC")
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
        builder.AppendLine("- Main: Inkfluence");
        builder.AppendLine("- Alternate: Unmixr");
        builder.AppendLine("- Visual packet: vidBoard");
        return builder.ToString().TrimEnd();
    }

    private static string BuildOriginCanonMarkdown(CharacterNarrativePacket packet, CharacterNarrativeDraft draft)
    {
        StringBuilder builder = new();
        builder.AppendLine($"# {packet.Alias} Origin Story");
        builder.AppendLine();
        builder.AppendLine(HumanCopy(draft.Prose));
        builder.AppendLine();
        builder.AppendLine("## Production Notes");
        builder.AppendLine($"- Authoring provider: {ResolveOriginAuthoringProvider(packet.BookKind)}");
        builder.AppendLine($"- Authoring role: {ResolveOriginAuthoringRole(packet.BookKind)}");
        builder.AppendLine($"- Provider strategy: {packet.ProviderStrategy}");
        builder.AppendLine($"- Post step: {OriginBookPostProcessingSteps.UndetectableHumanizer}");
        builder.AppendLine($"- Ruleset: {packet.RulesetId}");
        builder.AppendLine($"- Metatype: {packet.Metatype}");
        builder.AppendLine($"- Build method: {packet.BuildMethod}");
        builder.AppendLine($"- Archetype hint: {packet.ArchetypeHint}");
        builder.AppendLine($"- Summary: {HumanCopy(draft.Summary)}");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(packet.GmAllowanceNotes))
        {
            builder.AppendLine("## GM Notes");
            builder.AppendLine(HumanCopy(packet.GmAllowanceNotes));
            builder.AppendLine();
        }
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

    private static string BuildMyFirstBookOriginStoryPacket(
        CharacterNarrativePacket packet,
        CharacterNarrativeDraft draft,
        string storyMarkdownPath,
        string status,
        DateTimeOffset generatedAtUtc)
        => JsonSerializer.Serialize(
            new
            {
                tool = "MyFirstBook",
                artifactKind = "origin_story_presentation_handoff",
                contractName = "chummer.origin_dossier.myfirstbook_presentation_handoff.v1",
                status,
                generatedAtUtc,
                bookKind = packet.BookKind,
                providerStrategy = packet.ProviderStrategy,
                title = $"{packet.Alias} Origin Story",
                source = "chummer_origin_story",
                sourceStoryMarkdownPath = storyMarkdownPath,
                sourceStorySha256 = ComputeSha256(storyMarkdownPath),
                publicationAllowed = false,
                reviewRequired = true,
                playerFacing = true,
                rulesTruth = "not_authoritative",
                privacyBoundary = "local_character_story_review",
                presentationSurface = "myfirstbook",
                authoringProvider = ResolveOriginAuthoringProvider(packet.BookKind),
                authoringRole = ResolveOriginAuthoringRole(packet.BookKind),
                postProcessingRequired = OriginBookPostProcessingSteps.UndetectableHumanizer,
                premiumGuidedManuscript = string.Equals(packet.ProviderStrategy, OriginBookProviderStrategies.PremiumGuidedAuthoring, StringComparison.Ordinal),
                summary = HumanCopy(draft.Summary),
                gmAllowanceNotes = HumanCopy(packet.GmAllowanceNotes),
                creativeDirection = new
                {
                    bookSurface = packet.BookSurface,
                    primaryVoiceStyle = packet.PrimaryVoiceStyle,
                    alternateVoiceStyle = packet.AlternateVoiceStyle,
                    portraitStyle = packet.PortraitStyle,
                    videoStyle = packet.VideoStyle,
                    gmConstraints = packet.GmConstraintLabels
                }
            },
            new JsonSerializerOptions { WriteIndented = true });

    private static string BuildMyFirstBookOriginPresentationHtml(
        CharacterNarrativePacket packet,
        CharacterNarrativeDraft draft,
        string stageLabel,
        string storyMarkdownPath)
    {
        static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
        static string Paragraphs(string? value)
            => string.Join(
                Environment.NewLine,
                (value ?? string.Empty)
                    .Split(["\r\n", "\n"], StringSplitOptions.None)
                    .Where(static line => !string.IsNullOrWhiteSpace(line))
                    .Select(static line => $"<p>{WebUtility.HtmlEncode(line.Trim())}</p>"));

        string gmNotesBlock = string.IsNullOrWhiteSpace(packet.GmAllowanceNotes)
            ? string.Empty
            : $"<section><h2>GM Notes</h2><p>{Html(HumanCopy(packet.GmAllowanceNotes))}</p></section>";

        string creativeDirectionBlock = $$"""
<section>
  <h2>Creative Direction</h2>
  <div class="meta">
    <div><span class="label">Surface</span>{{Html(FirstNonEmpty(packet.BookSurface, "MyFirstBook presentation"))}}</div>
    <div><span class="label">Primary voice</span>{{Html(FirstNonEmpty(packet.PrimaryVoiceStyle, "Measured dossier"))}}</div>
    <div><span class="label">Alternate voice</span>{{Html(FirstNonEmpty(packet.AlternateVoiceStyle, "Cinematic narration"))}}</div>
    <div><span class="label">Portrait style</span>{{Html(FirstNonEmpty(packet.PortraitStyle, "Noir Ink"))}}</div>
    <div><span class="label">Video style</span>{{Html(FirstNonEmpty(packet.VideoStyle, "Grounded dossier"))}}</div>
  </div>
</section>
""";

        string constraintsBlock = packet.GmConstraintLabels.Count == 0
            ? string.Empty
            : $$"""
<section>
  <h2>GM Constraints</h2>
  <ul>
    {{string.Join(Environment.NewLine, packet.GmConstraintLabels.Select(static item => $"<li>{WebUtility.HtmlEncode(item)}</li>"))}}
  </ul>
</section>
""";

        string hooksBlock = string.Join(
            Environment.NewLine,
            draft.GmHooks.Select(static hook => $"<li>{WebUtility.HtmlEncode(HumanCopy(hook))}</li>"));

        string questionsBlock = string.Join(
            Environment.NewLine,
            packet.ContradictionFlags.DefaultIfEmpty("None found in the current character context.")
                .Select(static item => $"<li>{WebUtility.HtmlEncode(HumanCopy(item))}</li>"));

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{Html(packet.Alias)}} Origin Story</title>
  <style>
    :root { color-scheme: light; --page:#efe8db; --surface:#fbf7ef; --surface-2:#f4eddf; --text:#211b15; --muted:#6d6052; --line:rgba(57,45,32,.14); --accent:#86623f; }
    * { box-sizing:border-box; }
    body { margin:0; font-family:"Iowan Old Style", "Palatino Linotype", "Book Antiqua", Georgia, serif; background:var(--page); color:var(--text); }
    .shell { max-width:920px; margin:0 auto; padding:28px 18px 56px; }
    .hero, section { background:var(--surface); border:1px solid var(--line); border-radius:16px; padding:24px 26px; box-shadow:0 1px 0 rgba(57,45,32,.05); }
    .hero { margin-bottom:14px; }
    .eyebrow, .label { font-family:"Avenir Next", "Segoe UI", sans-serif; }
    .eyebrow { color:var(--accent); font-size:12px; text-transform:uppercase; letter-spacing:.08em; margin:0 0 12px; }
    h1, h2 { margin:0 0 12px; font-weight:600; color:var(--text); }
    h1 { font-size:36px; line-height:1.08; letter-spacing:-0.01em; }
    h2 { font-size:20px; line-height:1.2; }
    p, li { color:var(--text); line-height:1.75; font-size:18px; }
    .hero > p:last-of-type { max-width:42rem; }
    .prose p { margin:0 0 1rem; }
    .meta { display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); gap:12px; margin-top:18px; }
    .meta div { background:var(--surface-2); border-radius:12px; padding:13px 14px; min-height:84px; }
    .label { display:block; color:var(--muted); font-size:11px; margin-bottom:6px; text-transform:uppercase; letter-spacing:.08em; }
    .meta div, .meta span, .meta strong, .meta p { overflow-wrap:anywhere; }
    .muted { color:var(--muted); }
    section { margin-top:14px; }
    ul { margin:0; padding-left:20px; }
    .footer { margin-top:16px; color:var(--muted); font-size:14px; }
    a { color:var(--accent); }
  </style>
</head>
<body>
  <main class="shell">
    <section class="hero">
      <p class="eyebrow">MyFirstBook presentation</p>
      <h1>{{Html(packet.Alias)}} Origin Story</h1>
      <p>{{Html(HumanCopy(draft.Summary))}}</p>
      <div class="meta">
        <div><span class="label">Status</span>{{Html(stageLabel)}}</div>
        <div><span class="label">Edition</span>{{Html(packet.BookKind)}}</div>
        <div><span class="label">Authoring</span>{{Html(ResolveOriginAuthoringProvider(packet.BookKind))}}</div>
        <div><span class="label">Post step</span>{{Html(OriginBookPostProcessingSteps.UndetectableHumanizer)}}</div>
        <div><span class="label">Ruleset</span>{{Html(packet.RulesetId)}}</div>
        <div><span class="label">Metatype</span>{{Html(packet.Metatype)}}</div>
        <div><span class="label">Archetype</span>{{Html(packet.ArchetypeHint)}}</div>
        <div><span class="label">Build method</span>{{Html(packet.BuildMethod)}}</div>
      </div>
    </section>
    {{gmNotesBlock}}
    {{creativeDirectionBlock}}
    {{constraintsBlock}}
    <section>
      <h2>Origin</h2>
      <div class="prose">{{Paragraphs(HumanCopy(draft.Prose))}}</div>
    </section>
    <section>
      <h2>GM Hooks</h2>
      <ul>
        {{hooksBlock}}
      </ul>
    </section>
    <section>
      <h2>Open Questions</h2>
      <ul>
        {{questionsBlock}}
      </ul>
    </section>
    <p class="footer">Source story: {{Html(Path.GetFileName(storyMarkdownPath))}}</p>
  </main>
</body>
</html>
""";
    }

    private static string BuildInkfluenceNarrationScript(OriginDossierBundle bundle)
    {
        StringBuilder builder = new();
        builder.AppendLine($"Inkfluence audiobook brief for {bundle.Packet.Alias}");
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
        builder.AppendLine($"- Inkfluence voice script: {bundle.InkfluenceScriptPath}");
        builder.AppendLine($"- Alternate voice script: {bundle.UnmixrScriptPath}");
        builder.AppendLine($"- Render request: {requestPath}");
        if (!string.IsNullOrWhiteSpace(bundle.GmAllowanceNotes))
        {
            builder.AppendLine($"- GM notes: {HumanCopy(bundle.GmAllowanceNotes)}");
        }
        builder.AppendLine();
        builder.AppendLine("## Voices");
        builder.AppendLine("- Main: Inkfluence");
        builder.AppendLine("- Alternate: Unmixr");
        builder.AppendLine();
        builder.AppendLine("## Boundary");
        builder.AppendLine("- Audio output does not change rules or the character sheet.");
        builder.AppendLine("- If the alternate voice fails, keep the default voice.");
        builder.AppendLine();
        builder.AppendLine("## Expected outputs");
        builder.AppendLine("- one default audiobook file");
        builder.AppendLine("- one alternate audiobook file");
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
        string provider = HumanCopy(response.RouteDecision.ProviderId);
        string routeReason = response.RouteDecision.Reason;
        return HumanCopy($"{provider} · {confidence} · {routeReason}");
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
            buttons.Add(CreateButton("Open account workspace", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/account/alice"), isPrimary: true, name: "AliceSuggestedActionFallbackAccount"));
            buttons.Add(CreateButton("Open browser guide", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal("/alice"), name: "AliceSuggestedActionFallbackPublic"));
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
                    Foreground = DesktopShellTheme.ResolveTextMutedBrush(),
                    TextWrapping = TextWrapping.Wrap
                });
        }

        if (entry.SuggestedActionTitles.Count > 0)
        {
            stack.Children.Add(
                new TextBlock
                {
                    Text = "Next: " + string.Join(" · ", entry.SuggestedActionTitles),
                    Foreground = DesktopShellTheme.ResolveMutedForegroundBrush(),
                    TextWrapping = TextWrapping.Wrap
                });
        }

        return new Border
        {
            Name = $"AliceConversationTurn_{entry.Kind}",
            BorderBrush = DesktopShellTheme.ResolveBorderBrush(),
            BorderThickness = new Thickness(1),
            Background = entry.Kind == AliceConversationTurnKind.User
                ? DesktopShellTheme.ResolveSelectionInsetBrush()
                : DesktopShellTheme.ResolveSurfaceAltBrush(),
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
                "Choose metatype and archetype, then draft the story. The PDF book or MyFirstBook presentation comes before audio, portrait, scene, or video work.",
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
                "Build the origin story for a Troll Decker.",
                "Build the origin story for an Elf Combat mage.",
                "Use the approved story to explain the qualities, ware, and first gear."
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
        IReadOnlyList<string> SuggestedActionTitles)
    {
        public string RoleLabel { get; init; } = RoleLabel;
        public string Title { get; init; } = Kind == AliceConversationTurnKind.Assistant ? HumanCopy(Title) : Title;
        public string Body { get; init; } = Kind == AliceConversationTurnKind.Assistant ? HumanCopy(Body) : Body;
        public IReadOnlyList<string> EvidenceLines { get; init; } = Kind == AliceConversationTurnKind.Assistant ? HumanLines(EvidenceLines) : EvidenceLines;
        public IReadOnlyList<string> SuggestedActionTitles { get; init; } = Kind == AliceConversationTurnKind.Assistant ? HumanLines(SuggestedActionTitles) : SuggestedActionTitles;
    }

    private sealed record AliceAssistantContextProjection(
        string Title,
        string Summary,
        string Detail)
    {
        public string Title { get; init; } = HumanCopy(Title);
        public string Summary { get; init; } = HumanCopy(Summary);
        public string Detail { get; init; } = HumanCopy(Detail);
    }

    private sealed record OriginPortraitCandidate(
        string CandidateId,
        string StyleLabel,
        string Summary,
        string BackgroundHex,
        string AccentHex,
        string ForegroundHex,
        string HighlightHex)
    {
        public string StyleLabel { get; init; } = HumanCopy(StyleLabel);
        public string Summary { get; init; } = HumanCopy(Summary);
    }

    private sealed record OriginSceneCandidate(
        string SceneId,
        string Title,
        string Summary,
        string BackgroundHex,
        string AccentHex)
    {
        public string Title { get; init; } = HumanCopy(Title);
        public string Summary { get; init; } = HumanCopy(Summary);
    }

}
