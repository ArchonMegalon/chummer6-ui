using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
[DoNotParallelize]
public sealed class DesktopWindowContrastTests
{
    private static readonly object HeadlessInitLock = new();
    private static bool _headlessInitialized;
    private const int HeadlessSessionAttempts = 3;

    [TestMethod]
    public void Install_linking_window_keeps_idle_input_controls_readable_in_dark_mode()
    {
        WithStandaloneInstallLinkingWindow(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleInputControlContrast(window, "install linking window dark mode", minimumVisibleInputControls: 1);
        });
    }

    [TestMethod]
    public void Report_issue_window_keeps_idle_input_controls_readable_in_dark_mode()
    {
        WithStandaloneReportIssueWindow(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleInputControlContrast(window, "report issue window dark mode", minimumVisibleInputControls: 4);
        });
    }

    [TestMethod]
    public void Update_window_keeps_action_controls_readable_in_dark_mode()
    {
        WithStandaloneUpdateWindow(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleTextBlockContrast(window, "update window dark mode", minimumVisibleTextBlocks: 8);
            AssertVisibleButtonContrast(window, "update window dark mode", minimumVisibleButtons: 4);
        });
    }

    [TestMethod]
    public void Startup_update_window_keeps_status_and_progress_readable_in_dark_mode()
    {
        WithStandaloneStartupUpdateWindow(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleTextBlockContrast(window, "startup update window dark mode", minimumVisibleTextBlocks: 2);
            AssertVisibleProgressBarTheme(window, "startup update window dark mode", minimumVisibleProgressBars: 1);
        });
    }

    [TestMethod]
    public void Devices_access_window_keeps_action_controls_readable_in_dark_mode()
    {
        WithStandaloneDevicesAccessWindow(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleButtonContrast(window, "devices access window dark mode", minimumVisibleButtons: 4);
        });
    }

    [TestMethod]
    public void Add_quality_dialog_keeps_dense_selection_controls_readable_in_dark_mode()
    {
        DesktopDialogState dialog = new DesktopDialogFactory().CreateUiControlDialog("quality_add", DesktopPreferenceState.Default);
        WithBoundDialogWindow(dialog, window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleInputControlContrast(window, "add quality dialog dark mode", minimumVisibleInputControls: 3);
            AssertVisibleButtonContrast(window, "add quality dialog dark mode", minimumVisibleButtons: 2);
        });
    }

    [TestMethod]
    public void Global_settings_dialog_keeps_idle_controls_readable_in_dark_mode()
    {
        DesktopDialogState dialog = new DesktopDialogFactory().CreateCommandDialog(
            "global_settings",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(dialog, window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleInputControlContrast(window, "global settings dialog dark mode", minimumVisibleInputControls: 4);
            AssertVisibleButtonContrast(window, "global settings dialog dark mode", minimumVisibleButtons: 2);
        });
    }

    [TestMethod]
    public void Origin_dossier_preview_keeps_story_and_book_text_readable_in_dark_mode()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);
        DesktopDialogState originBuild = DesktopDialogFactory.BuildNewCharacterOriginBuildDialog(originWizard);

        WithBoundDialogWindow(originWizard, window =>
        {
            Border storyPreview = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => string.Equals(border.Name, "LegacyStoryPreviewSummaryCard", StringComparison.Ordinal));

            Assert.IsTrue(storyPreview.IsVisible, "The Origin Dossier story preview must be visible for dark-mode contrast proof.");
            AssertVisibleInputControlContrast(window, "origin dossier wizard dark mode", minimumVisibleInputControls: 2);
            AssertVisibleTextBlockContrast(storyPreview, "origin dossier story preview dark mode", minimumVisibleTextBlocks: 3);
        }, requestedTheme: ThemeVariant.Dark);

        WithBoundDialogWindow(originBuild, window =>
        {
            Border bookPreview = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => string.Equals(border.Name, "OriginBookPreviewPanel", StringComparison.Ordinal));

            Assert.IsTrue(bookPreview.IsVisible, "The Origin Dossier book preview must be visible for dark-mode contrast proof.");
            AssertVisibleTextBlockContrast(bookPreview, "origin dossier book preview dark mode", minimumVisibleTextBlocks: 2);
        }, requestedTheme: ThemeVariant.Dark);
    }

    [TestMethod]
    public void Shell_theme_helpers_keep_idle_input_controls_readable_in_dark_mode()
    {
        WithStandaloneShellInputThemeWindow(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleInputControlContrast(window, "shell theme helper dark mode", minimumVisibleInputControls: 4);
            AssertVisibleSelectedListItemContrast(window, "shell theme helper dark mode", minimumSelectedItems: 1);
            AssertVisibleChoiceTextContrast(window, "shell theme helper dark mode", minimumVisibleChoiceTexts: 2);
        });
    }

    [TestMethod]
    public void Character_create_attributes_port_keeps_attribute_list_readable_in_dark_mode()
    {
        WithStandaloneCharacterCreateClassicPort(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            ListBox attributesList = window.GetVisualDescendants()
                .OfType<ListBox>()
                .Single(listBox => string.Equals(listBox.Name, "CreateAttributesList", StringComparison.Ordinal));

            Assert.IsTrue(attributesList.IsVisible, "The character-create Attributes list must be visible for dark-mode contrast proof.");
            Assert.IsTrue(attributesList.ItemCount >= 4, "The character-create Attributes list must contain enough attribute rows for a meaningful proof.");
            attributesList.SelectedIndex = 0;
            PumpUi();
            AssertVisibleInputControlContrast(window, "character-create attributes dark mode", minimumVisibleInputControls: 1);
            AssertVisibleSelectedListItemContrast(window, "character-create attributes dark mode", minimumSelectedItems: 1);
            AssertVisibleChoiceTextContrast(window, "character-create attributes dark mode", minimumVisibleChoiceTexts: 4);
        });
    }

    private static void EnsureHeadlessPlatform()
    {
        lock (HeadlessInitLock)
        {
            if (_headlessInitialized)
            {
                return;
            }

            _headlessInitialized = true;
        }
    }

    private static void WithStandaloneInstallLinkingWindow(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            Window window = new DesktopInstallLinkingWindow(CreateInstallLinkingStartupContext())
                            {
                                Width = 880,
                                Height = 540
                            };
                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                SafeDisposeHeadlessSession(session);
            }
        }

        throw new AssertFailedException("Avalonia install-linking headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static void WithStandaloneReportIssueWindow(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            ConstructorInfo constructor = typeof(DesktopReportIssueWindow).GetConstructor(
                                BindingFlags.Instance | BindingFlags.NonPublic,
                                binder: null,
                                [
                                    typeof(DesktopInstallLinkingState),
                                    typeof(DesktopUpdateClientStatus),
                                    typeof(DesktopPreferenceState)
                                ],
                                modifiers: null)
                                ?? throw new AssertFailedException("DesktopReportIssueWindow private constructor was not found.");

                            Window window = (Window)constructor.Invoke(
                                [
                                    CreateInstallState(status: "claimed"),
                                    CreateUpdateStatus(),
                                    DesktopPreferenceState.Default
                                ]);

                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                SafeDisposeHeadlessSession(session);
            }
        }

        throw new AssertFailedException("Avalonia report-issue headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static DesktopInstallLinkingStartupContext CreateInstallLinkingStartupContext()
    {
        return new DesktopInstallLinkingStartupContext(
            State: CreateInstallState(status: "guest") with
            {
                ClaimedAtUtc = null,
                LinkedEmail = null,
                LastClaimCode = "RUNNER-CLAIM-42"
            },
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "fresh_install");
    }

    private static void WithStandaloneUpdateWindow(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            ConstructorInfo constructor = typeof(DesktopUpdateWindow).GetConstructor(
                                BindingFlags.Instance | BindingFlags.NonPublic,
                                binder: null,
                                [
                                    typeof(DesktopInstallLinkingState),
                                    typeof(DesktopUpdateClientStatus),
                                    typeof(DesktopPreferenceState)
                                ],
                                modifiers: null)
                                ?? throw new AssertFailedException("DesktopUpdateWindow private constructor was not found.");

                            Window window = (Window)constructor.Invoke(
                                [
                                    CreateInstallState(status: "guest"),
                                    CreateUpdateStatus(),
                                    DesktopPreferenceState.Default
                                ]);

                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                SafeDisposeHeadlessSession(session);
            }
        }

        throw new AssertFailedException("Avalonia update-window headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static void WithStandaloneStartupUpdateWindow(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            ConstructorInfo constructor = typeof(DesktopStartupUpdateWindow).GetConstructor(
                                BindingFlags.Instance | BindingFlags.NonPublic,
                                binder: null,
                                [typeof(string), typeof(string[])],
                                modifiers: null)
                                ?? throw new AssertFailedException("DesktopStartupUpdateWindow private constructor was not found.");

                            Window window = (Window)constructor.Invoke(["avalonia", Array.Empty<string>()]);
                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                SafeDisposeHeadlessSession(session);
            }
        }

        throw new AssertFailedException("Avalonia startup-update headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static void WithStandaloneDevicesAccessWindow(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            ConstructorInfo constructor = typeof(DesktopDevicesAccessWindow).GetConstructor(
                                BindingFlags.Instance | BindingFlags.NonPublic,
                                binder: null,
                                [
                                    typeof(DesktopInstallLinkingState),
                                    typeof(DesktopUpdateClientStatus),
                                    typeof(DesktopPreferenceState),
                                    typeof(DesktopInstallLinkingSummaryProjection),
                                    typeof(AccountCampaignSummary)
                                ],
                                modifiers: null)
                                ?? throw new AssertFailedException("DesktopDevicesAccessWindow private constructor was not found.");

                            Window window = (Window)constructor.Invoke(
                                [
                                    CreateInstallState(status: "guest"),
                                    CreateUpdateStatus(),
                                    DesktopPreferenceState.Default,
                                    DesktopInstallLinkingSummaryProjection.Empty,
                                    null!
                                ]);

                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                SafeDisposeHeadlessSession(session);
            }
        }

        throw new AssertFailedException("Avalonia devices-access headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static void WithBoundDialogWindow(
        DesktopDialogState dialog,
        Action<DesktopDialogWindow> assertion,
        ThemeVariant? requestedTheme = null)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            ThemeVariant? priorAppTheme = global::Avalonia.Application.Current?.RequestedThemeVariant;
                            if (requestedTheme is not null && global::Avalonia.Application.Current is not null)
                            {
                                global::Avalonia.Application.Current.RequestedThemeVariant = requestedTheme;
                            }

                            DesktopDialogWindow window = new()
                            {
                                Width = 1080,
                                Height = 900,
                                RequestedThemeVariant = requestedTheme ?? ThemeVariant.Default
                            };
                            try
                            {
                                window.BindDialog(dialog);
                                window.Show();
                                PumpUi();
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                if (requestedTheme is not null && global::Avalonia.Application.Current is not null && priorAppTheme is not null)
                                {
                                    global::Avalonia.Application.Current.RequestedThemeVariant = priorAppTheme;
                                }

                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                SafeDisposeHeadlessSession(session);
            }
        }

        throw new AssertFailedException($"Avalonia dialog {dialog.Id} headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static void WithStandaloneShellInputThemeWindow(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            TextBox textBox = new() { Name = "ShellThemeTextBox", Text = "Runner note" };
                            DesktopShellTheme.ApplyShellTextInputTheme(textBox);
                            ComboBox comboBox = new()
                            {
                                Name = "ShellThemeComboBox",
                                ItemsSource = new[] { "Street samurai", "Mage" },
                                SelectedIndex = 0
                            };
                            DesktopShellTheme.ApplyShellComboBoxTheme(comboBox);
                            ListBox listBox = new()
                            {
                                Name = "ShellThemeListBox",
                                ItemsSource = new[] { "Ares Predator", "Armor jacket" },
                                SelectedIndex = 0
                            };
                            DesktopShellTheme.ApplyShellListBoxTheme(listBox);
                            NumericUpDown numericUpDown = new()
                            {
                                Name = "ShellThemeNumericUpDown",
                                Value = 3
                            };
                            DesktopShellTheme.ApplyShellNumericUpDownTheme(numericUpDown);

                            Window window = new()
                            {
                                Width = 520,
                                Height = 320,
                                Content = DesktopShellTheme.CreateWindowSurface(new StackPanel
                                {
                                    Spacing = 8,
                                    Children =
                                    {
                                        textBox,
                                        comboBox,
                                        listBox,
                                        numericUpDown
                                    }
                                })
                            };
                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                SafeDisposeHeadlessSession(session);
            }
        }

        throw new AssertFailedException("Avalonia shell input theme headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static void WithStandaloneCharacterCreateClassicPort(Action<Window> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContrastHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            CharacterCreateClassicPort port = new()
                            {
                                Width = 760,
                                Height = 520
                            };
                            string previewJson = """
                                {
                                  "ruleset": "sr5",
                                  "buildMethod": "Priority",
                                  "metatype": "Human",
                                  "attributes": [
                                    { "name": "Body", "baseValue": 3 },
                                    { "name": "Agility", "baseValue": 5 },
                                    { "name": "Reaction", "baseValue": 4 },
                                    { "name": "Logic", "baseValue": 3 }
                                  ]
                                }
                                """;
                            ClassicFormPortDocument document = ClassicFormPortDocument.CreateFromPreview(previewJson, "character_create");
                            port.SetState(new ClassicFormPortState(
                                SurfaceId: "character_create",
                                RuntimeSectionId: "character_create",
                                ActiveTabId: "Attributes",
                                ActiveActionId: null,
                                Notice: "Ready.",
                                PreviewJson: previewJson,
                                Rows: [],
                                QuickActions: [],
                                NavigationTabs: [],
                                SectionActions: [],
                                Document: document));

                            Window window = new()
                            {
                                Width = 820,
                                Height = 580,
                                Content = DesktopShellTheme.CreateWindowSurface(port)
                            };
                            window.Show();
                            PumpUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpUi();
                            }
                        },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return;
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                SafeDisposeHeadlessSession(session);
            }
        }

        throw new AssertFailedException("Avalonia character-create port headless session did not stabilize for contrast proof.", lastFailure);
    }

    private static DesktopInstallLinkingState CreateInstallState(string status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopInstallLinkingState(
            InstallationId: "install-contrast-test",
            HeadId: "avalonia",
            ApplicationVersion: "run-test",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            Status: status,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 1,
            LastStartedAtUtc: now,
            ClaimedAtUtc: string.Equals(status, "claimed", StringComparison.Ordinal) ? now : null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private",
            GrantToken: "grant-token");
    }

    private static DesktopUpdateClientStatus CreateUpdateStatus()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopUpdateClientStatus(
            HeadId: "avalonia",
            InstalledVersion: "run-test",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: true,
            ManifestLocation: "/tmp/chummer-release.json",
            LastCheckedAtUtc: now,
            LastManifestVersion: "run-test",
            LastManifestPublishedAtUtc: now,
            LastError: null,
            Status: "current",
            RecommendedAction: "Continue.");
    }

    private static void AssertVisibleInputControlContrast(Control root, string context, int minimumVisibleInputControls)
    {
        Control[] inputControls = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.IsVisible)
            .Where(static control => control is ComboBox or ListBox or TextBox or NumericUpDown)
            .ToArray();

        Assert.IsTrue(
            inputControls.Length >= minimumVisibleInputControls,
            $"{context} should expose enough themed input controls for a meaningful non-hover readability check.");

        foreach (Control control in inputControls)
        {
            (IBrush? foregroundBrush, IBrush? backgroundBrush) = control switch
            {
                ComboBox comboBox => (comboBox.Foreground, comboBox.Background),
                ListBox listBox => (listBox.Foreground, listBox.Background),
                TextBox textBox => (textBox.Foreground, textBox.Background),
                NumericUpDown numericUpDown => (numericUpDown.Foreground, numericUpDown.Background),
                _ => (null, null)
            };

            Color foreground = ResolveSolidColor(foregroundBrush, control, "foreground", context);
            Color background = ResolveBackgroundColor(backgroundBrush, control, context);
            string controlName = string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name!;
            AssertContrastAtLeast(foreground, background, 4.5d, $"{context} {controlName} non-hover text");
        }
    }

    private static void AssertVisibleSelectedListItemContrast(Control root, string context, int minimumSelectedItems)
    {
        ListBoxItem[] selectedItems = root.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Where(static item => item.IsVisible && item.IsSelected)
            .ToArray();

        Assert.IsTrue(
            selectedItems.Length >= minimumSelectedItems,
            $"{context} should expose enough selected list items for a meaningful dark-mode readability check.");

        foreach (ListBoxItem item in selectedItems)
        {
            Color foreground = ResolveSolidColor(item.Foreground, item, "foreground", context);
            Color background = ResolveBackgroundColor(item.Background, item, context);
            string controlName = string.IsNullOrWhiteSpace(item.Name) ? item.GetType().Name : item.Name!;
            AssertContrastAtLeast(foreground, background, 4.5d, $"{context} {controlName} selected list item text");
        }
    }

    private static void AssertVisibleChoiceTextContrast(Control root, string context, int minimumVisibleChoiceTexts)
    {
        TextBlock[] choiceTexts = root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static textBlock => textBlock.IsVisible)
            .Where(static textBlock => !string.IsNullOrWhiteSpace(textBlock.Text))
            .Where(static textBlock => textBlock.GetVisualAncestors().Any(static ancestor => ancestor is ListBoxItem or ComboBoxItem))
            .ToArray();

        Assert.IsTrue(
            choiceTexts.Length >= minimumVisibleChoiceTexts,
            $"{context} should expose enough rendered choice text for a meaningful dark-mode readability check.");

        foreach (TextBlock textBlock in choiceTexts)
        {
            Color foreground = ResolveSolidColor(textBlock.Foreground, textBlock, "foreground", context);
            Color background = ResolveBackgroundColor(textBlock.Background, textBlock, context);
            string controlName = string.IsNullOrWhiteSpace(textBlock.Name) ? textBlock.Text!.Trim() : textBlock.Name!;
            AssertContrastAtLeast(foreground, background, 4.5d, $"{context} {controlName} rendered choice text");
        }
    }

    private static void AssertVisibleButtonContrast(Control root, string context, int minimumVisibleButtons)
    {
        Button[] buttons = root.GetVisualDescendants()
            .OfType<Button>()
            .Where(static control => control.IsVisible)
            .Where(ShouldMeasureButtonTextContrast)
            .ToArray();

        Assert.IsTrue(
            buttons.Length >= minimumVisibleButtons,
            $"{context} should expose enough visible buttons for a meaningful non-hover readability check.");

        foreach (Button button in buttons)
        {
            Color foreground = ResolveSolidColor(button.Foreground, button, "foreground", context);
            Color background = ResolveBackgroundColor(button.Background, button, context);
            string controlName = string.IsNullOrWhiteSpace(button.Name) ? button.GetType().Name : button.Name!;
            AssertContrastAtLeast(foreground, background, 4.5d, $"{context} {controlName} non-hover button text");
        }
    }

    private static bool ShouldMeasureButtonTextContrast(Button button)
    {
        if (button is CheckBox)
        {
            return false;
        }

        return true;
    }

    private static void AssertVisibleProgressBarTheme(Control root, string context, int minimumVisibleProgressBars)
    {
        ProgressBar[] progressBars = root.GetVisualDescendants()
            .OfType<ProgressBar>()
            .Where(static control => control.IsVisible)
            .ToArray();

        Assert.IsTrue(
            progressBars.Length >= minimumVisibleProgressBars,
            $"{context} should expose enough visible progress bars for a meaningful update-progress readability check.");

        foreach (ProgressBar progressBar in progressBars)
        {
            Color foreground = ResolveSolidColor(progressBar.Foreground, progressBar, "foreground", context);
            Color background = ResolveBackgroundColor(progressBar.Background, progressBar, context);
            string controlName = string.IsNullOrWhiteSpace(progressBar.Name) ? progressBar.GetType().Name : progressBar.Name!;
            AssertContrastAtLeast(foreground, background, 3.0d, $"{context} {controlName} progress indicator");
        }
    }

    private static void AssertVisibleTextBlockContrast(Control root, string context, int minimumVisibleTextBlocks)
    {
        TextBlock[] textBlocks = root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static control => control.IsVisible)
            .Where(static control => !string.IsNullOrWhiteSpace(control.Text))
            .Where(static control => !IsProgressBarOwnedTextBlock(control))
            .Where(static control => !IsChoiceOwnedTextBlock(control))
            .ToArray();

        Assert.IsTrue(
            textBlocks.Length >= minimumVisibleTextBlocks,
            $"{context} should expose enough visible text for a meaningful non-hover readability check.");

        foreach (TextBlock textBlock in textBlocks)
        {
            Color foreground = ResolveSolidColor(textBlock.Foreground, textBlock, "foreground", context);
            Color background = ResolveBackgroundColor(textBlock.Background, textBlock, context);
            string controlName = string.IsNullOrWhiteSpace(textBlock.Name) ? textBlock.Text!.Trim() : textBlock.Name!;
            AssertContrastAtLeast(foreground, background, 4.5d, $"{context} {controlName} text");
        }
    }

    private static bool IsProgressBarOwnedTextBlock(TextBlock textBlock)
        => textBlock.GetVisualAncestors().OfType<ProgressBar>().Any();

    private static bool IsChoiceOwnedTextBlock(TextBlock textBlock)
        => textBlock.GetVisualAncestors().Any(static ancestor => ancestor is ListBoxItem or ComboBoxItem or ComboBox);

    private static Color ResolveSolidColor(IBrush? brush, Control control, string role, string context)
    {
        if (brush is ISolidColorBrush solidBrush)
        {
            return solidBrush.Color;
        }

        throw new AssertFailedException($"{context} {control.Name ?? control.GetType().Name} must expose a solid {role} brush.");
    }

    private static Color ResolveBackgroundColor(IBrush? brush, Control control, string context)
    {
        if (TryResolveOpaqueSolidColor(brush, out Color ownColor))
        {
            return ownColor;
        }

        foreach (Visual visual in control.GetVisualAncestors())
        {
            switch (visual)
            {
                case Border border when TryResolveOpaqueSolidColor(border.Background, out Color borderColor):
                    return borderColor;
                case TemplatedControl templatedControl when TryResolveOpaqueSolidColor(templatedControl.Background, out Color controlColor):
                    return controlColor;
            }
        }

        throw new AssertFailedException($"{context} {control.Name ?? control.GetType().Name} must expose or inherit an opaque solid background brush.");
    }

    private static bool TryResolveOpaqueSolidColor(IBrush? brush, out Color color)
    {
        if (brush is ISolidColorBrush solidBrush && solidBrush.Color.A > 0)
        {
            color = solidBrush.Color;
            return true;
        }

        color = default;
        return false;
    }

    private static void AssertContrastAtLeast(Color foreground, Color background, double minimum, string context)
    {
        double ratio = ContrastRatio(foreground, background);
        Assert.IsTrue(ratio >= minimum, $"Expected {context} contrast to be at least {minimum:0.0}, but was {ratio:0.00}.");
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        double foregroundLuminance = RelativeLuminance(foreground);
        double backgroundLuminance = RelativeLuminance(background);
        double lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        double darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(double value)
        {
            double normalized = value / 255d;
            return normalized <= 0.03928d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        double red = Channel(color.R);
        double green = Channel(color.G);
        double blue = Channel(color.B);
        return (0.2126d * red) + (0.7152d * green) + (0.0722d * blue);
    }

    private static bool IsTransientHeadlessFailure(Exception ex)
    {
        string message = ex.ToString();
        return message.Contains("The visual is not attached to a visual tree", StringComparison.Ordinal)
            || message.Contains("Call from invalid thread", StringComparison.Ordinal)
            || message.Contains("Operation is not valid due to the current state of the object", StringComparison.Ordinal);
    }

    private static void SafeDisposeHeadlessSession(HeadlessUnitTestSession? session)
    {
        try
        {
            session?.Dispose();
        }
        catch (NullReferenceException)
        {
            // Avalonia Headless can throw during teardown after assertions complete.
        }
    }

    private static void PumpUi()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class ContrastHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .ConfigureFonts(static fontManager => fontManager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "fonts:Inter#Inter"
                })
                .WithInterFont();
        }
    }

    private sealed class ThemeScope : IDisposable
    {
        private readonly ThemeVariant _priorAppTheme;
        private readonly ThemeVariant _priorWindowTheme;
        private readonly Window _window;

        private ThemeScope(Window window)
        {
            _window = window;
            _priorAppTheme = global::Avalonia.Application.Current?.RequestedThemeVariant ?? ThemeVariant.Light;
            _priorWindowTheme = window.RequestedThemeVariant;
        }

        public static ThemeScope Dark(Window window)
        {
            ThemeScope scope = new(window);
            if (global::Avalonia.Application.Current is not null)
            {
                global::Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }

            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.InvalidateVisual();
            PumpUi();
            return scope;
        }

        public void Dispose()
        {
            if (global::Avalonia.Application.Current is not null)
            {
                global::Avalonia.Application.Current.RequestedThemeVariant = _priorAppTheme;
            }

            _window.RequestedThemeVariant = _priorWindowTheme;
            _window.InvalidateVisual();
            PumpUi();
        }
    }
}
