using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopInstallLinkingWindow : Window
{
    private DesktopInstallLinkingState _state;
    private readonly DesktopUpdateClientStatus _updateStatus;
    private DesktopPreferenceState _preferences;
    private readonly string _language;
    private readonly TextBlock _summaryText;
    private readonly TextBlock _linkStateText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _matrixHandoffStateText;
    private readonly TextBlock _matrixVaultNameText;
    private readonly TextBlock _matrixVaultFamilyText;
    private readonly TextBlock _matrixEmailClaimText;
    private readonly TextBlock _matrixPrivacyText;
    private readonly TextBlock _matrixMonitorStateText;
    private readonly TextBlock _matrixMonitorEmailText;
    private readonly TextBlock _matrixMonitorLinkText;
    private readonly TextBlock _claimCodeHintText;
    private readonly TextBlock _claimCodeLabelText;
    private readonly TextBox _claimCodeTextBox;
    private readonly StackPanel _claimCodeEntryRow;
    private readonly Control _browserFallbackPanel;
    private readonly TextBlock _browserFallbackHeadingText;
    private readonly TextBlock _browserFallbackSummaryText;
    private readonly TextBlock _browserFallbackDetailText;
    private readonly TextBlock _browserFallbackUrlLabelText;
    private readonly TextBlock _browserFallbackUrlText;
    private readonly TextBlock _guidedPreferenceStatusText;
    private readonly RadioButton _guidedToolsRadioButton;
    private readonly RadioButton _quietToolsRadioButton;
    private readonly TextBlock _moreToolsHeading;
    private readonly WrapPanel _moreToolsPanel;
    private readonly Button _followThroughButton;
    private readonly Button _accountButton;
    private readonly Button _copyLoginUrlButton;
    private readonly Button _redeemClaimCodeButton;
    private readonly Button _exitButton;
    private CancellationTokenSource? _handoffPollCancellation;
    private readonly bool _loginVideoPreview;
    private bool _automaticHandoffStarted;
    private bool _browserFallbackVisible;
    private string? _lastLoginUrl;

    public DesktopInstallLinkingWindow(DesktopInstallLinkingStartupContext context, bool loginVideoPreview = false)
    {
        ArgumentNullException.ThrowIfNull(context);

        _state = context.State;
        _loginVideoPreview = loginVideoPreview;
        _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(context.State.HeadId);
        _preferences = DesktopPreferenceRuntime.LoadOrCreateState(context.State.HeadId);
        _language = _preferences.Language;
        Title = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.title", _language);
        Width = 880;
        Height = 540;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _summaryText = new TextBlock
        {
            Text = BuildSummary(_state, _updateStatus, _language),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _linkStateText = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        _statusText = new TextBlock
        {
            IsVisible = false,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155"),
            TextWrapping = TextWrapping.Wrap
        };

        _matrixHandoffStateText = new TextBlock
        {
            Text = "Awaiting account handoff",
            FontWeight = FontWeight.SemiBold,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMediaOverlayForegroundBrush", "#F8FAFC"),
            TextWrapping = TextWrapping.Wrap
        };

        _matrixVaultNameText = new TextBlock
        {
            Text = "profile visible if provider allows",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#C7FBE8")),
            TextWrapping = TextWrapping.Wrap
        };

        _matrixVaultFamilyText = new TextBlock
        {
            Text = "given/family optional",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#C7FBE8")),
            TextWrapping = TextWrapping.Wrap
        };

        _matrixEmailClaimText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#FFF1A8")),
            TextWrapping = TextWrapping.Wrap
        };

        _matrixPrivacyText = new TextBlock
        {
            Text = "Only the verified email claim leaves the host.",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#D7FBE8")),
            TextWrapping = TextWrapping.Wrap
        };

        _matrixMonitorStateText = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#8EF9E4")),
            TextWrapping = TextWrapping.Wrap
        };

        _matrixMonitorEmailText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#FFF1A8")),
            TextWrapping = TextWrapping.Wrap
        };

        _matrixMonitorLinkText = new TextBlock
        {
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.Parse("#D7FBE8")),
            TextWrapping = TextWrapping.Wrap
        };

        _claimCodeHintText = new TextBlock
        {
            Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.prompt_guest_claim", _language),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _claimCodeLabelText = new TextBlock
        {
            Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.claim_code_label", _language),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        _claimCodeTextBox = new TextBox
        {
            Watermark = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.claim_code_watermark", _language),
            MinWidth = 280,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        DesktopShellTheme.ApplyShellTextInputTheme(_claimCodeTextBox);

        _followThroughButton = CreateButton(string.Empty, OpenFollowThroughAsync, isDefault: true);
        _accountButton = CreateButton(string.Empty, OpenAccountAsync);
        _copyLoginUrlButton = CreateButton(
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.copy_login_url", _language),
            CopyLoginUrlAsync);
        _redeemClaimCodeButton = CreateButton(
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.redeem_claim_code", _language),
            RedeemClaimCodeAsync);
        _exitButton = CreateButton(
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.continue_unlinked", _language),
            ContinueUnlinkedAsync);
        _claimCodeEntryRow = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 6,
            IsVisible = false,
            Children =
            {
                _claimCodeTextBox,
                _redeemClaimCodeButton
            }
        };
        _browserFallbackHeadingText = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _browserFallbackSummaryText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };
        _browserFallbackDetailText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };
        _browserFallbackUrlLabelText = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _browserFallbackUrlText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellForegroundBrush", "#0f172a")
        };
        _browserFallbackPanel = DesktopShellTheme.CreateUtilityPanel(
            new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    _browserFallbackHeadingText,
                    _browserFallbackSummaryText,
                    _browserFallbackDetailText,
                    _browserFallbackUrlLabelText,
                    _browserFallbackUrlText
                }
            });
        _browserFallbackPanel.IsVisible = false;
        _guidedPreferenceStatusText = new TextBlock
        {
            Text = BuildGuidedToolsPreferenceStatus(_preferences, _language),
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155"),
            TextWrapping = TextWrapping.Wrap
        };
        _guidedToolsRadioButton = new RadioButton
        {
            Name = "InstallLinkGuidedToolsVisibleOption",
            GroupName = "InstallLinkFeatureVisibility",
            Content = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.visible_choice", _language),
            IsChecked = !_preferences.DisableAiFeatures
        };
        ToolTip.SetTip(
            _guidedToolsRadioButton,
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.tip", _language));
        AutomationProperties.SetName(
            _guidedToolsRadioButton,
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.visible_choice", _language));
        AutomationProperties.SetHelpText(
            _guidedToolsRadioButton,
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.tip", _language));
        _guidedToolsRadioButton.IsCheckedChanged += (_, _) =>
        {
            if (_guidedToolsRadioButton.IsChecked == true)
            {
                ApplyGuidedFeaturePreference(disableAiFeatures: false);
            }
        };
        _quietToolsRadioButton = new RadioButton
        {
            Name = "InstallLinkGuidedToolsHiddenOption",
            GroupName = "InstallLinkFeatureVisibility",
            Content = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.hidden_choice", _language),
            IsChecked = _preferences.DisableAiFeatures
        };
        ToolTip.SetTip(
            _quietToolsRadioButton,
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.tip", _language));
        AutomationProperties.SetName(
            _quietToolsRadioButton,
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.hidden_choice", _language));
        AutomationProperties.SetHelpText(
            _quietToolsRadioButton,
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.tip", _language));
        _quietToolsRadioButton.IsCheckedChanged += (_, _) =>
        {
            if (_quietToolsRadioButton.IsChecked == true)
            {
                ApplyGuidedFeaturePreference(disableAiFeatures: true);
            }
        };
        _moreToolsHeading = new TextBlock
        {
            Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.more_tools", _language),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _moreToolsPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemHeight = 32,
            Children =
            {
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _language), OpenAccountAsync),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.copy_install_id", _language), CopyInstallIdAsync),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _language), OpenDownloadsAsync),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_support", _language), OpenSupportAsync),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_report_issue", _language), OpenReportIssueAsync)
            }
        };
        foreach (Control tool in _moreToolsPanel.Children)
        {
            tool.Margin = new Thickness(0, 0, 6, 6);
        }

        Content = DesktopShellTheme.CreateWindowSurface(
            new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("320,*"),
                ColumnSpacing = 14,
                Children =
                {
                    CreateMatrixUplinkHero(),
                    CreateInstallLinkingPanel().At(0, 1)
                }
            },
            padding: 12);

        Closing += OnClosing;
        Opened += (_, _) =>
        {
            if (context.ClaimResult is not null)
            {
                SetStatus(context.ClaimResult.Message);
                _state = context.ClaimResult.State;
                RefreshSummary();
                RefreshActionState();
                if (!_loginVideoPreview && !DesktopInstallLinkingRuntime.IsClaimed(_state))
                {
                    BeginAutomaticHandoffAsync();
                }
            }
            else if (_loginVideoPreview)
            {
                SetStatus("Login video preview. The browser will not open unless you press the claim button.");
                UpdateMatrixHandoffState("Login video preview");
            }
            else if (!DesktopInstallLinkingRuntime.IsClaimed(_state))
            {
                SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.guest_status", _language));
                BeginAutomaticHandoffAsync();
            }
        };

        RefreshActionState();
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopInstallLinkingState state = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopInstallLinkingStartupContext context = new(
            State: state,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_shell");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(owner);
    }

    public static async Task ShowLoginVideoAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopInstallLinkingState state = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopInstallLinkingStartupContext context = new(
            State: state,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_help_login_video");

        DesktopInstallLinkingWindow dialog = new(context, loginVideoPreview: true);
        await dialog.ShowDialog(owner);
    }

    public static async Task ShowIfNeededAsync(Window owner, DesktopInstallLinkingStartupContext context)
    {
        if (!context.ShouldPrompt)
        {
            return;
        }

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(owner);
    }

    private Control CreateMatrixUplinkHero()
    {
        Grid visual = new()
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        Bitmap? render = TryLoadMatrixUplinkRender();
        if (render is not null)
        {
            visual.Children.Add(new Image
            {
                Name = "InstallLinkMatrixUplinkRender",
                Source = render,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            }.At(0, 0, rowSpan: 3));
        }

        visual.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0d, 0d, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1d, 1d, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Color.Parse("#AA020617"), 0d),
                    new GradientStop(Color.Parse("#330F766E"), 0.48d),
                    new GradientStop(Color.Parse("#BB020617"), 1d)
                ]
            }
        }.At(0, 0, rowSpan: 3));

        visual.Children.Add(new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "CHUMMER MATRIX UPLINK",
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#8EF9E4")),
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.title", _language),
                    FontSize = 28,
                    FontWeight = FontWeight.Bold,
                    Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMediaOverlayForegroundBrush", "#F8FAFC"),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        }.At(0, 0));

        visual.Children.Add(CreateMatrixIdentityVaultOverlay().At(1, 0));

        visual.Children.Add(new StackPanel
        {
            Margin = new Thickness(16, 0, 16, 16),
            VerticalAlignment = VerticalAlignment.Bottom,
            Spacing = 8,
            Children =
            {
                CreateMatrixSignalRail(),
                CreateMatrixJackOutMonitor(),
                new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#B0021110")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#668EF9E4")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Child = new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            _matrixHandoffStateText,
                            new TextBlock
                            {
                                Text = "Local callback listening. Browser fallback remains available.",
                                Foreground = new SolidColorBrush(Color.Parse("#D7FBE8")),
                                TextWrapping = TextWrapping.Wrap
                            }
                        }
                    }
                }
            }
        }.At(2, 0));

        return new Border
        {
            Name = "InstallLinkMatrixUplinkHero",
            Background = new SolidColorBrush(Color.Parse("#06130F")),
            BorderBrush = new SolidColorBrush(Color.Parse("#33F5D37A")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = visual
        };
    }

    private Control CreateMatrixIdentityVaultOverlay()
        => new Border
        {
            Name = "InstallLinkMatrixIdentityVault",
            Margin = new Thickness(16, 8),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#B0021110")),
            BorderBrush = new SolidColorBrush(Color.Parse("#66F5D37A")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = "HOST VAULT DOSSIER",
                        FontSize = 10,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#8EF9E4")),
                        TextWrapping = TextWrapping.Wrap
                    },
                    CreateMatrixDossierRow("Name", _matrixVaultNameText, "NOT IMPORTED"),
                    CreateMatrixDossierRow("Given/family", _matrixVaultFamilyText, "NOT IMPORTED"),
                    CreateMatrixDossierRow("Email claim", _matrixEmailClaimText, "EXTRACTED"),
                    _matrixPrivacyText
                }
            }
        };

    private static Control CreateMatrixDossierRow(string label, TextBlock value, string stamp)
        => new Border
        {
            Background = new SolidColorBrush(Color.Parse("#80081714")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3346F6D1")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(7, 5),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("82,*,72"),
                ColumnSpacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        FontSize = 10,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#8EF9E4")),
                        TextWrapping = TextWrapping.Wrap
                    }.At(0, 0),
                    value.At(0, 1),
                    new TextBlock
                    {
                        Text = stamp,
                        FontSize = 9,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = string.Equals(stamp, "EXTRACTED", StringComparison.Ordinal)
                            ? new SolidColorBrush(Color.Parse("#FFF1A8"))
                            : new SolidColorBrush(Color.Parse("#B6C7D1")),
                        TextAlignment = TextAlignment.Right,
                        TextWrapping = TextWrapping.Wrap
                    }.At(0, 2)
                }
            }
        };

    private Control CreateMatrixJackOutMonitor()
        => new Border
        {
            Name = "InstallLinkMatrixJackOutMonitor",
            Background = new SolidColorBrush(Color.Parse("#CC020617")),
            BorderBrush = new SolidColorBrush(Color.Parse("#88F5D37A")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock
                    {
                        Text = "SAFEHOUSE MONITOR",
                        FontSize = 9,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#8EF9E4")),
                        TextWrapping = TextWrapping.Wrap
                    },
                    _matrixMonitorStateText,
                    _matrixMonitorEmailText,
                    _matrixMonitorLinkText
                }
            }
        };

    private Control CreateInstallLinkingPanel()
    {
        WrapPanel primaryActions = DesktopShellTheme.CreateWrapActionRow(
        [
            _followThroughButton,
            _accountButton,
            _copyLoginUrlButton,
            _exitButton
        ]);

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                _summaryText,
                new TextBlock
                {
                    Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.heading", _language),
                    FontSize = 20,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                _linkStateText,
                _statusText,
                CreateGuidedToolsPreferencePanel(),
                DesktopShellTheme.CreateUtilityPanel(
                    new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.next_step", _language),
                                FontWeight = FontWeight.SemiBold,
                                TextWrapping = TextWrapping.Wrap
                            },
                            _browserFallbackPanel,
                            _claimCodeHintText,
                            _claimCodeLabelText,
                            _claimCodeEntryRow,
                            primaryActions,
                            _moreToolsHeading,
                            _moreToolsPanel
                        }
                    })
            }
        };
    }

    private Control CreateGuidedToolsPreferencePanel()
        => DesktopShellTheme.CreateUtilityPanel(
            new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.title", _language),
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.preference.summary", _language),
                        Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            _guidedToolsRadioButton,
                            _quietToolsRadioButton
                        }
                    },
                    _guidedPreferenceStatusText
                }
            });

    private static Control CreateMatrixSignalRail()
        => new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = 6,
            Children =
            {
                CreateMatrixSignalNode("Callback", 0.88d).At(0, 0),
                CreateMatrixSignalNode("Account", 0.68d).At(0, 1),
                CreateMatrixSignalNode("Grant", 0.46d).At(0, 2)
            }
        };

    private static Control CreateMatrixSignalNode(string label, double fillWidth)
    {
        Grid track = new()
        {
            Height = 30,
            RowDefinitions = new RowDefinitions("*,Auto")
        };
        track.Children.Add(new Border
        {
            Height = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#466577"))
        }.At(0, 0));
        track.Children.Add(new Border
        {
            Height = 4,
            Width = Math.Max(26d, 82d * fillWidth),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#8EF9E4"))
        }.At(0, 0));
        track.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#D7FBE8")),
            TextAlignment = TextAlignment.Center
        }.At(1, 0));
        return track;
    }

    private static Bitmap? TryLoadMatrixUplinkRender()
    {
        try
        {
            using Stream stream = AssetLoader.Open(new Uri("avares://Chummer.Avalonia/Assets/install-link/matrix-uplink-login.png"));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private Button CreateButton(string label, Func<Task> action, bool isDefault = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 112,
            Padding = new Thickness(10, 4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("shell-action");
        ToolTip.SetTip(button, label);

        if (isDefault)
        {
            button.FontWeight = FontWeight.SemiBold;
            DesktopShellTheme.ApplyPrimaryButton(button);
        }

        button.Click += async (_, _) => await action();
        return button;
    }

    private void BeginAutomaticHandoffAsync()
    {
        if (_automaticHandoffStarted || DesktopInstallLinkingRuntime.IsClaimed(_state))
        {
            return;
        }

        _automaticHandoffStarted = true;
        _ = RunAutomaticHandoffAsync();
    }

    private async Task RunAutomaticHandoffAsync()
    {
        try
        {
            UpdateMatrixHandoffState("Preparing local callback");
            await Task.Delay(250).ConfigureAwait(true);
            if (DesktopInstallLinkingRuntime.IsClaimed(_state))
            {
                return;
            }

            bool opened = DesktopInstallLinkingRuntime.TryOpenClaimPortalForInstall(
                _state,
                out string loginUrl,
                out string? failureReason);
            if (opened)
            {
                _browserFallbackVisible = false;
                SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_account", _language));
                StartClaimPolling("Waiting for browser callback");
            }
            else
            {
                _state = DesktopInstallLinkingRuntime.LoadOrCreateState(_state.HeadId);
                await ShowManualBrowserFallbackAsync(loginUrl, failureReason).ConfigureAwait(true);
                UpdateMatrixHandoffState("Browser fallback ready");
            }
        }
        catch (Exception ex)
        {
            UpdateMatrixHandoffState("Browser fallback ready");
            SetStatus(ex.Message);
        }
    }

    private void StartClaimPolling(string stateLabel)
    {
        _handoffPollCancellation?.Cancel();
        _handoffPollCancellation?.Dispose();
        _handoffPollCancellation = new CancellationTokenSource();
        UpdateMatrixHandoffState(stateLabel);
        _ = PollForClaimedInstallAsync(_handoffPollCancellation.Token);
    }

    private async Task PollForClaimedInstallAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                await Task.Delay(750, cancellationToken).ConfigureAwait(true);
                DesktopInstallLinkingState current = DesktopInstallLinkingRuntime.LoadOrCreateState(_state.HeadId);
                if (!DesktopInstallLinkingRuntime.IsClaimed(current))
                {
                    continue;
                }

                _state = current;
                RefreshSummary();
                RefreshActionState();
                if (Owner is MainWindow ownerWindow)
                {
                    ownerWindow.ApplyInstallLinkingChrome(_state);
                }

                SetStatus(!string.IsNullOrWhiteSpace(_state.LastClaimMessage)
                    ? _state.LastClaimMessage
                    : FormatClaimStatus(_state, _language));
                return;
            }

            if (!DesktopInstallLinkingRuntime.IsClaimed(_state))
            {
                UpdateMatrixHandoffState("Browser claim pending");
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CopyInstallIdAsync()
    {
        if (Clipboard is null)
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.clipboard_unavailable", _language));
            return;
        }

        await Clipboard.SetTextAsync(_state.InstallationId);
        SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.install_id_copied", _language));
    }

    private async Task CopyLoginUrlAsync()
    {
        string loginUrl = _lastLoginUrl ?? string.Empty;
        if (string.IsNullOrWhiteSpace(loginUrl))
        {
            loginUrl = DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(_state);
            _lastLoginUrl = loginUrl;
        }

        if (Clipboard is null)
        {
            ShowManualBrowserFallback(loginUrl, null);
            SetStatus($"{DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.clipboard_unavailable", _language)} {loginUrl}");
            return;
        }

        await Clipboard.SetTextAsync(loginUrl);
        ShowManualBrowserFallback(loginUrl, null);
        SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.login_url_copied", _language));
    }

    private async Task RedeemClaimCodeAsync()
    {
        string claimCode = _claimCodeTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(claimCode))
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.claim_code_required", _language));
            return;
        }

        SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.linking", _language));
        DesktopInstallClaimResult result = await DesktopInstallLinkingRuntime.RedeemClaimCodeAsync(
            _state.HeadId,
            claimCode,
            CancellationToken.None).ConfigureAwait(true);

        _state = result.State;
        _claimCodeTextBox.Text = result.Succeeded ? string.Empty : claimCode;
        RefreshSummary();
        RefreshActionState();
        if (Owner is MainWindow ownerWindow)
        {
            ownerWindow.ApplyInstallLinkingChrome(_state);
        }

        SetStatus(result.Message);
    }

    private Task OpenFollowThroughAsync()
    {
        if (DesktopInstallLinkingRuntime.IsClaimed(_state))
        {
            Window? ownerWindow = Owner as Window;
            if (ownerWindow is not null)
            {
                Close();
                return DesktopCampaignWorkspaceWindow.ShowAsync(ownerWindow, _state.HeadId);
            }

            return DesktopCampaignWorkspaceWindow.ShowAsync(this, _state.HeadId);
        }

        Window? followThroughOwner = Owner as Window;
        if (followThroughOwner is not null)
        {
            Close();
            return DesktopDevicesAccessWindow.ShowAsync(followThroughOwner, _state.HeadId);
        }

        return DesktopDevicesAccessWindow.ShowAsync(this, _state.HeadId);
    }

    private Task OpenDownloadsAsync()
    {
        if (DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_downloads", _language));
        }
        else
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.unable_open_downloads", _language));
        }

        return Task.CompletedTask;
    }

    private Task OpenSupportAsync()
    {
        return DesktopSupportWindow.ShowAsync(this, _state.HeadId);
    }

    private Task OpenReportIssueAsync()
    {
        return DesktopReportIssueWindow.ShowAsync(this, _state.HeadId);
    }

    private Task OpenAccountAsync()
    {
        string accountUrl;
        string? failureReason;
        bool opened = DesktopInstallLinkingRuntime.IsClaimed(_state)
            ? DesktopInstallLinkingRuntime.TryOpenAccountPortalForInstall(_state, out accountUrl, out failureReason)
            : DesktopInstallLinkingRuntime.TryOpenClaimPortalForInstall(_state, out accountUrl, out failureReason);

        if (opened)
        {
            _browserFallbackVisible = false;
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_account", _language));
            if (!DesktopInstallLinkingRuntime.IsClaimed(_state))
            {
                StartClaimPolling("Waiting for browser callback");
            }
        }
        else
        {
            _state = DesktopInstallLinkingRuntime.LoadOrCreateState(_state.HeadId);
            return ShowManualBrowserFallbackAsync(accountUrl, failureReason);
        }

        return Task.CompletedTask;
    }

    private Task ContinueUnlinkedAsync()
    {
        if (!DesktopInstallLinkingRuntime.IsClaimed(_state))
        {
            DesktopInstallLinkingRuntime.MarkPromptDismissed(_state.HeadId);
        }

        Close();
        return Task.CompletedTask;
    }

    private void ApplyGuidedFeaturePreference(bool disableAiFeatures)
    {
        DesktopPreferenceState nextPreferences = DesktopPreferenceStateRuntime.Normalize(
            _preferences with { DisableAiFeatures = disableAiFeatures });
        _preferences = nextPreferences;
        DesktopPreferenceRuntime.SaveState(_state.HeadId, nextPreferences);
        DesktopPreferenceStateRuntime.SetCurrent(nextPreferences);
        _guidedPreferenceStatusText.Text = BuildGuidedToolsPreferenceStatus(nextPreferences, _language);
        if (_guidedToolsRadioButton.IsChecked != !disableAiFeatures)
        {
            _guidedToolsRadioButton.IsChecked = !disableAiFeatures;
        }

        if (_quietToolsRadioButton.IsChecked != disableAiFeatures)
        {
            _quietToolsRadioButton.IsChecked = disableAiFeatures;
        }

        if (Owner is MainWindow ownerWindow)
        {
            ownerWindow.ApplyExternalPreferenceState(nextPreferences);
        }

        SetStatus(DesktopLocalizationCatalog.GetRequiredString(
            disableAiFeatures
                ? "desktop.install_link.status.guided_tools_hidden"
                : "desktop.install_link.status.guided_tools_visible",
            _language));
    }

    private void RefreshSummary()
    {
        _summaryText.Text = BuildSummary(_state, _updateStatus, _language);
    }

    private void RefreshActionState()
    {
        bool claimed = DesktopInstallLinkingRuntime.IsClaimed(_state);
        _linkStateText.Text = FormatClaimStatus(_state, _language);
        UpdateMatrixHandoffState(claimed ? "Grant accepted" : "Waiting for account claim");
        RefreshMatrixIdentityOverlay();
        _followThroughButton.Content = claimed
            ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_work", _language)
            : DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_devices_access", _language);
        _accountButton.Content = BuildAccountButtonLabel(claimed, _browserFallbackVisible, _language);
        _followThroughButton.IsVisible = claimed;
        _copyLoginUrlButton.IsVisible = !claimed && _browserFallbackVisible;
        _exitButton.IsVisible = !claimed;
        if (_loginVideoPreview)
        {
            _exitButton.Content = "Close";
        }
        else if (!claimed)
        {
            _exitButton.Content = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.continue_unlinked", _language);
        }

        RefreshButtonTip(_followThroughButton);
        RefreshButtonTip(_accountButton);
        RefreshButtonTip(_copyLoginUrlButton);
        RefreshButtonTip(_exitButton);

        _claimCodeHintText.IsVisible = !claimed && _browserFallbackVisible;
        _claimCodeLabelText.IsVisible = !claimed && _browserFallbackVisible;
        _claimCodeEntryRow.IsVisible = !claimed && _browserFallbackVisible;
        _browserFallbackPanel.IsVisible = !claimed && _browserFallbackVisible;
        _moreToolsHeading.IsVisible = claimed;
        _moreToolsPanel.IsVisible = claimed;
    }

    private void RefreshMatrixIdentityOverlay()
    {
        _matrixEmailClaimText.Text = BuildEmailClaimDisplay(_state);
        _matrixMonitorStateText.Text = BuildMonitorStateDisplay(_state);
        _matrixMonitorEmailText.Text = BuildMonitorEmailDisplay(_state);
        _matrixMonitorLinkText.Text = BuildMonitorLinkDisplay(_state);
        ToolTip.SetTip(_matrixEmailClaimText, "Live client overlay. The render asset contains no user email or profile data.");
        ToolTip.SetTip(_matrixMonitorEmailText, "Live client overlay. Chummer links only the verified email claim.");
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _handoffPollCancellation?.Cancel();

        if (!DesktopInstallLinkingRuntime.IsClaimed(_state) && !_loginVideoPreview)
        {
            DesktopInstallLinkingRuntime.MarkPromptDismissed(_state.HeadId);
        }
    }

    private void SetStatus(string message)
    {
        _statusText.Text = message;
        _statusText.IsVisible = !string.IsNullOrWhiteSpace(message);
        ToolTip.SetTip(_statusText, message);
    }

    private async Task ShowManualBrowserFallbackAsync(string loginUrl, string? failureReason)
    {
        ShowManualBrowserFallback(loginUrl, failureReason);
        if (Clipboard is not null && !string.IsNullOrWhiteSpace(loginUrl))
        {
            try
            {
                await Clipboard.SetTextAsync(loginUrl);
                SetStatus(BuildBrowserFallbackStatus(_language, copiedToClipboard: true));
                return;
            }
            catch
            {
                // The visible fallback remains available below.
            }
        }

        SetStatus(BuildBrowserFallbackStatus(_language, copiedToClipboard: false));
    }

    private void ShowManualBrowserFallback(string loginUrl, string? failureReason)
    {
        _browserFallbackVisible = true;
        _lastLoginUrl = loginUrl;
        _browserFallbackHeadingText.Text = BuildBrowserFallbackHeading(_language);
        _browserFallbackSummaryText.Text = BuildBrowserFallbackSummary(_language);
        _browserFallbackDetailText.Text = BuildBrowserFallbackDetail(_language, failureReason);
        _browserFallbackUrlLabelText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.fallback.claim_url_label", _language);
        _browserFallbackUrlText.Text = loginUrl;
        _claimCodeHintText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.prompt_guest_claim", _language);
        _claimCodeHintText.IsVisible = true;
        ToolTip.SetTip(_browserFallbackUrlText, loginUrl);
        ToolTip.SetTip(_claimCodeHintText, DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.prompt_guest_claim", _language));
        UpdateMatrixHandoffState("Browser fallback ready");
        RefreshSummary();
        RefreshActionState();
    }

    internal static string BuildBrowserFallbackHeading(string language)
        => DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.fallback.heading", language);

    internal static string BuildBrowserFallbackSummary(string language)
        => DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.fallback.summary", language);

    internal static string BuildBrowserFallbackDetail(string language, string? failureReason)
        => string.IsNullOrWhiteSpace(failureReason)
            ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.fallback.detail", language)
            : DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.install_link.fallback.detail_with_reason",
                language,
                failureReason.Trim());

    internal static string BuildBrowserFallbackStatus(string language, bool copiedToClipboard)
        => copiedToClipboard
            ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.login_url_copied", language)
            : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.manual_login_url", language);

    internal static string BuildAccountButtonLabel(bool claimed, bool browserFallbackVisible, string language)
    {
        if (claimed)
        {
            return DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", language);
        }

        return browserFallbackVisible
            ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_claim_link", language)
            : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.login_website", language);
    }

    private static void RefreshButtonTip(Button button)
    {
        if (button.Content is string text && !string.IsNullOrWhiteSpace(text))
        {
            ToolTip.SetTip(button, text);
        }
    }

    internal static string BuildGuidedToolsPreferenceStatus(DesktopPreferenceState preferences, string language)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return DesktopLocalizationCatalog.GetRequiredString(
            preferences.DisableAiFeatures
                ? "desktop.install_link.preference.off"
                : "desktop.install_link.preference.on",
            language);
    }

    private void UpdateMatrixHandoffState(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _matrixHandoffStateText.Text = message.Trim();
    }

    internal static string BuildEmailClaimDisplay(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string? email = ResolveEmailForOverlay(state);
        if (!DesktopInstallLinkingRuntime.IsClaimed(state) || string.IsNullOrWhiteSpace(email))
        {
            return "email claim sealed";
        }

        return $"{BuildMaskedEmailForOverlay(email)} -> {CompactOverlayValue(email, 34)}";
    }

    internal static string BuildMonitorEmailDisplay(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string? email = ResolveEmailForOverlay(state);
        return DesktopInstallLinkingRuntime.IsClaimed(state) && !string.IsNullOrWhiteSpace(email)
            ? CompactOverlayValue(email, 36)
            : "email pending";
    }

    internal static string BuildMonitorStateDisplay(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (DesktopInstallLinkingRuntime.IsClaimed(state))
        {
            return "CHUMMER UPLINK COMPLETE";
        }

        return string.IsNullOrWhiteSpace(state.LastClaimError)
            ? "UPLINK WAITING"
            : "UPLINK LOST";
    }

    internal static string BuildMonitorLinkDisplay(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return DesktopInstallLinkingRuntime.IsClaimed(state)
            ? "LOCAL INSTALL LINKED"
            : "LOCAL INSTALL PENDING";
    }

    internal static string BuildMaskedEmailForOverlay(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "sealed";
        }

        string normalized = email.Trim();
        int at = normalized.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at >= normalized.Length - 1)
        {
            return "sealed";
        }

        string local = normalized[..at];
        string domain = normalized[(at + 1)..];
        int dot = domain.LastIndexOf('.');
        string domainStem = dot > 0 ? domain[..dot] : domain;
        string suffix = dot > 0 ? domain[dot..] : string.Empty;
        char localHead = local[0];
        char domainHead = domainStem.Length > 0 ? domainStem[0] : '*';
        return $"{localHead}****@{domainHead}****{suffix}";
    }

    internal static string? ResolveEmailForOverlay(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (LooksLikeEmail(state.LinkedEmail))
        {
            return state.LinkedEmail!.Trim();
        }

        if (LooksLikeEmail(state.UserId))
        {
            return state.UserId!.Trim();
        }

        if (LooksLikeEmail(state.SubjectId))
        {
            return state.SubjectId!.Trim();
        }

        return null;
    }

    internal static string CompactOverlayValue(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim();
        if (normalized.Length <= maxLength || maxLength < 12)
        {
            return normalized;
        }

        int suffixLength = Math.Max(5, maxLength / 2);
        int prefixLength = Math.Max(4, maxLength - suffixLength - 3);
        return string.Concat(normalized[..prefixLength], "...", normalized[^suffixLength..]);
    }

    private static bool LooksLikeEmail(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains('@', StringComparison.Ordinal)
           && value.Trim().IndexOf('@', StringComparison.Ordinal) > 0
           && value.Trim().IndexOf('@', StringComparison.Ordinal) < value.Trim().Length - 1;

    private static string FormatClaimStatus(DesktopInstallLinkingState state, string language)
        => DesktopInstallLinkingRuntime.IsClaimed(state)
            ? DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.install_link.summary.linked_status",
                language,
                state.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? "Unknown")
            : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.guest_status", language);

    private static string BuildSummary(DesktopInstallLinkingState state, DesktopUpdateClientStatus updateStatus, string language)
    {
        string claimStatus = FormatClaimStatus(state, language);

        List<string> lines =
        [
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.installation_id", language, state.InstallationId),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.head", language, state.HeadId),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.version", language, state.ApplicationVersion),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.channel", language, state.ChannelId),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.platform", language, state.Platform, state.Arch),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.status", language, claimStatus)
        ];

        if (state.LastClaimAttemptUtc is not null)
        {
            lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.install_link.summary.last_claim_attempt",
                language,
                state.LastClaimAttemptUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
        }

        if (!string.IsNullOrWhiteSpace(state.LastClaimMessage))
        {
            lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.hub_message", language, state.LastClaimMessage));
        }

        if (!string.IsNullOrWhiteSpace(state.LastClaimError))
        {
            lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.claim_error", language, state.LastClaimError));
        }

        if (state.LastBrowserDispatchAttemptUtc is not null)
        {
            lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.install_link.summary.browser_open_attempt",
                language,
                state.LastBrowserDispatchAttemptUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
        }

        if (!string.IsNullOrWhiteSpace(state.LastBrowserDispatchFailure))
        {
            lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.install_link.summary.browser_open_error",
                language,
                state.LastBrowserDispatchFailure));
        }

        lines.AddRange(DesktopSurfacePostureText.BuildLines(updateStatus));
        lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.install_link.shipping_locales",
            language,
            DesktopLocalizationCatalog.BuildSupportedLanguageSummary()));
        lines.Add(
            DesktopInstallLinkingRuntime.IsClaimed(state)
                ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.next_safe_action_claimed", language)
                : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.next_safe_action_guest", language));

        return string.Join("\n", lines);
    }
}
