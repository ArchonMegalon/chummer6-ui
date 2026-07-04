using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
using Chummer.Contracts.Presentation;
using Chummer.Desktop.Runtime;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
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
        foreach (DesktopUpdateClientStatus updateStatus in CreateUpdateStatusMatrix())
        {
            WithStandaloneUpdateWindow(updateStatus, window =>
            {
                using ThemeScope scope = ThemeScope.Dark(window);
                AssertVisibleTextBlockContrast(window, $"update window {updateStatus.Status} dark mode", minimumVisibleTextBlocks: 8);
                AssertVisibleButtonContrast(window, $"update window {updateStatus.Status} dark mode", minimumVisibleButtons: 4);
            });
        }
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
    public void Origin_dossier_advanced_story_controls_do_not_jump_or_collapse_after_live_combo_selection()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);
        WithPresenterBoundDialogWindow(originWizard, window =>
        {
            window.Height = 420;
            PumpUi();

            Expander advancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer scrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            advancedStoryControls.IsExpanded = true;
            PumpUi();

            foreach (string fieldId in new[]
                     {
                         "newCharacterOriginMetatypePreference",
                         "newCharacterOriginArchetypeIntent",
                         "newCharacterRulesetId",
                         "newCharacterOriginBuildPreference",
                         "newCharacterOriginBackground",
                         "newCharacterOriginTurningPoint",
                         "newCharacterOriginTrainingPath",
                         "newCharacterOriginUpgradeExposure",
                         "newCharacterOriginPressureCost",
                         "newCharacterOriginMotivation",
                         "newCharacterOriginTone",
                         "newCharacterOriginGmConstraintPreset"
                     })
            {
                ComboBox comboBox = window.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .Single(control => string.Equals(control.Name, DesktopDialogAccessibility.BuildFieldInputName(fieldId), StringComparison.Ordinal));

                if (comboBox.TranslatePoint(default, scrollViewer) is { } translated)
                {
                    double nextOffsetY = Math.Max(0d, scrollViewer.Offset.Y + translated.Y - 96d);
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, nextOffsetY);
                    PumpUi();
                }

                DesktopDialogFieldOption currentOption = (DesktopDialogFieldOption)(comboBox.SelectedItem
                    ?? throw new AssertFailedException($"Origin combo '{fieldId}' did not expose a selected option."));
                DesktopDialogFieldOption nextOption = (((System.Collections.IEnumerable?)comboBox.ItemsSource)?.Cast<DesktopDialogFieldOption>() ?? Enumerable.Empty<DesktopDialogFieldOption>())
                    .First(option => !string.Equals(option.Value, currentOption.Value, StringComparison.Ordinal));
                double preservedOffsetY = scrollViewer.Offset.Y;

                comboBox.SelectedItem = nextOption;
                PumpUi();
                PumpUi();

                Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                    .OfType<Expander>()
                    .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
                ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
                ComboBox reboundComboBox = window.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .Single(control => string.Equals(control.Name, DesktopDialogAccessibility.BuildFieldInputName(fieldId), StringComparison.Ordinal));

                Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, $"Advanced story controls should stay expanded after a live combo selection refresh from '{fieldId}'.");
                Assert.AreEqual(nextOption.Value, ((DesktopDialogFieldOption)reboundComboBox.SelectedItem!).Value, $"The live combo selection should survive the dialog refresh for '{fieldId}'.");
                Assert.IsTrue(
                    Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 12d,
                    $"Origin Dossier should preserve scroll position across a live combo selection refresh from '{fieldId}'. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
            }
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_current_scroll_anchor_when_another_combo_gains_focus_before_selection()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);
        DesktopDialogField metatypePreferenceField = originWizard.Fields
            .Single(field => string.Equals(field.Id, "newCharacterOriginMetatypePreference", StringComparison.Ordinal));
        DesktopDialogFieldOption nextMetatypePreference = (metatypePreferenceField.Options ?? [])
            .First(option => !string.Equals(option.Value, metatypePreferenceField.Value, StringComparison.Ordinal));

        WithPresenterBoundDialogWindow(originWizard, window =>
        {
            window.Height = 420;
            PumpUi();

            Expander advancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer scrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            advancedStoryControls.IsExpanded = true;
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();

            ComboBox buildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));
            ComboBox metatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            buildPreferenceCombo.Focus();
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 28d);
            PumpUi();
            double preservedOffsetY = scrollViewer.Offset.Y;

            metatypePreferenceCombo.Focus();
            PumpUi();

            metatypePreferenceCombo.SelectedItem = nextMetatypePreference;
            PumpUi();
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            ComboBox reboundMetatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded when another combo gains focus before the selection refresh.");
            Assert.AreEqual(nextMetatypePreference.Value, ((DesktopDialogFieldOption)reboundMetatypePreferenceCombo.SelectedItem!).Value, "The later combo selection should survive the cross-field focus refresh.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should keep the current scroll anchor when another combo gains focus before refresh. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_combo_refresh_defers_transient_presenter_close()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        RebindingDialogPresenter presenter = new(originWizard, deferFieldUpdates: true);
        WithPresenterBoundDialogWindow(originWizard, presenter, window =>
        {
            window.Height = 420;
            PumpUi();

            Expander advancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer scrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            advancedStoryControls.IsExpanded = true;
            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();

            ComboBox refreshCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .First(control => (control.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().OfType<DesktopDialogFieldOption>().Any() == true);
            DesktopDialogFieldOption currentOption = (DesktopDialogFieldOption)(refreshCombo.SelectedItem
                ?? throw new AssertFailedException("Origin combo did not expose a selected option."));
            DesktopDialogFieldOption nextOption = (((System.Collections.IEnumerable?)refreshCombo.ItemsSource)?.Cast<DesktopDialogFieldOption>() ?? Enumerable.Empty<DesktopDialogFieldOption>())
                .First(option => !string.Equals(option.Value, currentOption.Value, StringComparison.Ordinal));

            refreshCombo.SelectedItem = nextOption;
            PumpUi();

            Assert.IsTrue(
                window.TryDeferCloseForPendingOriginWizardTransientRefresh(),
                "Origin Dossier should defer a transient presenter close while a combo refresh is preserving the current viewport.");

            presenter.ReleaseFieldUpdates();
            PumpUi();
            Assert.IsTrue(window.IsVisible, "Origin Dossier should remain visible after the deferred refresh is rebound.");
        });
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

    private static void WithStandaloneUpdateWindow(DesktopUpdateClientStatus updateStatus, Action<Window> assertion)
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
                                    updateStatus,
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

    private static void WithPresenterBoundDialogWindow(
        DesktopDialogState dialog,
        Action<DesktopDialogWindow> assertion,
        ThemeVariant? requestedTheme = null)
    {
        WithPresenterBoundDialogWindow(dialog, new RebindingDialogPresenter(dialog), assertion, requestedTheme);
    }

    private static void WithPresenterBoundDialogWindow(
        DesktopDialogState dialog,
        RebindingDialogPresenter presenter,
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

                            using CharacterOverviewViewModelAdapter adapter = new(presenter);
                            DesktopDialogWindow window = new(adapter)
                            {
                                Width = 1080,
                                Height = 900,
                                RequestedThemeVariant = requestedTheme ?? ThemeVariant.Default
                            };

                            adapter.Updated += (_, _) =>
                            {
                                if (adapter.State.ActiveDialog is DesktopDialogState activeDialog)
                                {
                                    window.BindDialog(activeDialog);
                                    PumpUi();
                                }
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

        throw new AssertFailedException($"Avalonia presenter-backed dialog {dialog.Id} headless session did not stabilize for contrast proof.", lastFailure);
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

    private static DesktopUpdateClientStatus[] CreateUpdateStatusMatrix()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return
        [
            CreateUpdateStatus(),
            CreateUpdateStatus(status: "update_staged", pendingUpdateVersion: "run-next", recommendedAction: "Restart Chummer to finish the update."),
            CreateUpdateStatus(status: "update_available", lastManifestVersion: "run-next", recommendedAction: "Install when your table is between scenes."),
            CreateUpdateStatus(status: "attention_required", lastError: "Could not reach the update source.", recommendedAction: "Open support if this keeps happening."),
            CreateUpdateStatus(status: "disabled", updatesEnabled: false, updateMode: "off", lastCheckedAtUtc: now, recommendedAction: "Use Downloads when you want a newer build.")
        ];
    }

    private static DesktopUpdateClientStatus CreateUpdateStatus(
        string status = "current",
        bool updatesEnabled = true,
        string updateMode = "full",
        string? lastManifestVersion = "run-test",
        string? pendingUpdateVersion = null,
        string? lastError = null,
        string? recommendedAction = "Continue.",
        DateTimeOffset? lastCheckedAtUtc = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopUpdateClientStatus(
            HeadId: "avalonia",
            InstalledVersion: "run-test",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: updatesEnabled,
            AutoApply: true,
            ManifestLocation: "/tmp/chummer-release.json",
            LastCheckedAtUtc: lastCheckedAtUtc ?? now,
            LastManifestVersion: lastManifestVersion,
            LastManifestPublishedAtUtc: now,
            LastError: lastError,
            Status: status,
            RecommendedAction: recommendedAction,
            UpdateMode: updateMode,
            PendingUpdateVersion: pendingUpdateVersion,
            PendingUpdateChannelId: pendingUpdateVersion is null ? null : "stable");
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

    private sealed class RebindingDialogPresenter : ICharacterOverviewPresenter
    {
        private readonly TaskCompletionSource? _fieldUpdateGate;

        public RebindingDialogPresenter(DesktopDialogState dialog, bool deferFieldUpdates = false)
        {
            if (deferFieldUpdates)
            {
                _fieldUpdateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            State = CharacterOverviewState.Empty with
            {
                ActiveDialog = dialog,
                Preferences = DesktopPreferenceState.Default
            };
        }

        public CharacterOverviewState State { get; private set; }

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => Task.CompletedTask;

        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct) => Task.CompletedTask;

        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;

        public Task HandleUiControlAsync(string controlId, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;

        public void ReleaseFieldUpdates()
        {
            _fieldUpdateGate?.TrySetResult();
        }

        public async Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct)
        {
            if (_fieldUpdateGate is not null)
            {
                await _fieldUpdateGate.Task.WaitAsync(ct);
            }

            DesktopDialogState dialog = State.ActiveDialog
                ?? throw new AssertFailedException("A dialog update was requested without an active dialog.");

            DesktopDialogField[] updatedFields = dialog.Fields
                .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                    ? field with { Value = value ?? string.Empty }
                    : field)
                .ToArray();

            MethodInfo rebuildMethod = typeof(DesktopDialogFactory).GetMethod(
                "RebuildDynamicDialog",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("RebuildDynamicDialog reflection entry point was not found.");
            DesktopDialogState nextDialog = (DesktopDialogState)(rebuildMethod.Invoke(
                null,
                new object[] { dialog with { Fields = updatedFields }, DesktopPreferenceState.Default })
                ?? throw new AssertFailedException("RebuildDynamicDialog returned null."));

            Publish(State with
            {
                ActiveDialog = nextDialog
            });

        }

        public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct) => Task.CompletedTask;

        public Task CloseDialogAsync(CancellationToken ct) => Task.CompletedTask;

        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExportAsync(CancellationToken ct) => Task.CompletedTask;

        public Task PrintAsync(CancellationToken ct) => Task.CompletedTask;

        private void Publish(CharacterOverviewState state)
        {
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
