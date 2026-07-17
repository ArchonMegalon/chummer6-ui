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
using Chummer.Contracts.Workspaces;
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
    public void Origin_dossier_advanced_story_controls_stay_expanded_after_dialog_rebind()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
        {
            Expander advancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));

            advancedStoryControls.IsExpanded = true;
            PumpUi();

            DesktopDialogState updatedWizard = originWizard with
            {
                Fields = originWizard.Fields
                    .Select(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal)
                        ? field with { Value = "LifeModule" }
                        : field)
                    .ToArray()
            };

            window.BindDialog(updatedWizard);
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ComboBox buildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded after a select-driven dialog refresh.");
            Assert.IsNotNull(buildPreferenceCombo.SelectedItem, "The build-preference combo should keep a selected item after rebinding.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_scroll_position_after_dialog_rebind()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
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
            double preservedOffsetY = scrollViewer.Offset.Y;

            Assert.IsTrue(preservedOffsetY >= 120d, "Origin Dossier should be scrollable once the advanced story controls are expanded.");

            DesktopDialogState updatedWizard = originWizard with
            {
                Fields = originWizard.Fields
                    .Select(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal)
                        ? field with { Value = "LifeModule" }
                        : field)
                    .ToArray()
            };

            window.BindDialog(updatedWizard);
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded after a select-driven dialog refresh.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should preserve scroll position across combo-driven dialog refreshes. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_scroll_position_during_dialog_rebind()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
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
            double preservedOffsetY = scrollViewer.Offset.Y;

            DesktopDialogState updatedWizard = originWizard with
            {
                Fields = originWizard.Fields
                    .Select(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal)
                        ? field with { Value = "LifeModule" }
                        : field)
                    .ToArray()
            };

            window.BindDialog(updatedWizard);

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded during the same-tick dialog rebind.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should keep scroll position stable during the same-tick combo-driven dialog rebind. Before={preservedOffsetY:F1}, During={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_viewport_anchor_after_dialog_rebind_changes_content_above_them()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
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

            Point? preservedAnchor = advancedStoryControls.TranslatePoint(default, scrollViewer);
            Assert.IsNotNull(preservedAnchor, "The expanded advanced story controls should expose a stable viewport anchor before rebinding.");

            DesktopDialogState updatedWizard = originWizard with
            {
                Fields = originWizard.Fields
                    .Select(field => field.Id switch
                    {
                        "newCharacterOriginMetatypePreference" => field with { Value = "troll" },
                        "newCharacterOriginPathSummary" => field with
                        {
                            Value = "Street exile corridor" + Environment.NewLine
                                + "Brokered SIN fragments, debt markers, and burned cover identities now frame the path summary."
                        },
                        _ => field
                    })
                    .ToArray()
            };

            window.BindDialog(updatedWizard);
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            Point? reboundAnchor = reboundAdvancedStoryControls.TranslatePoint(default, reboundScrollViewer);

            Assert.IsNotNull(reboundAnchor, "The expanded advanced story controls should still expose a viewport anchor after rebinding.");
            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded after content above them changes during rebinding.");
            Assert.IsTrue(
                Math.Abs(reboundAnchor.Value.Y - preservedAnchor.Value.Y) <= 8d,
                $"Origin Dossier should preserve the advanced controls viewport anchor when content above them changes. Before={preservedAnchor.Value.Y:F1}, After={reboundAnchor.Value.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_combo_scroll_preservation_survives_transient_missing_expander_tree()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
        {
            PumpUi();

            Expander advancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            advancedStoryControls.IsExpanded = true;
            PumpUi();

            StackPanel dialogFieldsPanel = window.FindControl<StackPanel>("DialogFieldsPanel")!;
            dialogFieldsPanel.Children.Clear();

            FieldInfo expandedStateField = typeof(DesktopDialogWindow).GetField(
                "_originWizardAdvancedStoryControlsExpanded",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Expanded-state backing field was not found.");
            expandedStateField.SetValue(window, true);

            MethodInfo preservationMethod = typeof(DesktopDialogWindow).GetMethod(
                "ShouldPreserveOriginWizardComboInteractionScroll",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Combo scroll preservation guard was not found.");
            bool shouldPreserve = (bool)(preservationMethod.Invoke(window, null)
                ?? throw new AssertFailedException("Combo scroll preservation guard returned null."));

            Assert.IsTrue(
                shouldPreserve,
                "Origin Dossier combo refreshes should keep the advanced controls armed even while the old expander tree is temporarily absent during dialog rebuild.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_scroll_position_when_dialog_rebind_cannot_restore_combo_focus()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
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
            double preservedOffsetY = scrollViewer.Offset.Y;

            window.Focus();
            PumpUi();

            DesktopDialogState updatedWizard = originWizard with
            {
                Fields = originWizard.Fields
                    .Select(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal)
                        ? field with { Value = "LifeModule" }
                        : field)
                    .ToArray()
            };

            window.BindDialog(updatedWizard);
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded even when the dialog cannot recover combo focus from a select popup.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should preserve scroll position when a combo-driven refresh cannot recover focused combo state. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
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
            ComboBox metatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            advancedStoryControls.IsExpanded = true;
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();
            double preservedOffsetY = scrollViewer.Offset.Y;

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

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded after a live combo selection refresh.");
            Assert.AreEqual(nextMetatypePreference.Value, ((DesktopDialogFieldOption)reboundMetatypePreferenceCombo.SelectedItem!).Value, "The live combo selection should survive the dialog refresh.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should preserve scroll position across a live combo selection refresh. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_ignore_same_refresh_collapse_after_live_combo_selection()
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

            Expander staleAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer scrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            ComboBox metatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            staleAdvancedStoryControls.IsExpanded = true;
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();
            double preservedOffsetY = scrollViewer.Offset.Y;

            metatypePreferenceCombo.SelectedItem = nextMetatypePreference;
            staleAdvancedStoryControls.IsExpanded = false;
            PumpUi();
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            ComboBox reboundMetatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            Assert.IsTrue(
                reboundAdvancedStoryControls.IsExpanded,
                "Combo-driven Origin Dossier refreshes must ignore collapse events from the pre-refresh advanced controls tree.");
            Assert.AreEqual(
                nextMetatypePreference.Value,
                ((DesktopDialogFieldOption)reboundMetatypePreferenceCombo.SelectedItem!).Value,
                "The live combo selection should survive the same-refresh collapse attempt.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should preserve scroll position even if the pre-refresh advanced controls collapse during the combo refresh. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_ignore_transient_stale_collapse_during_same_dialog_rebind()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
        {
            Expander staleAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));

            FieldInfo expandedStateField = typeof(DesktopDialogWindow).GetField(
                "_originWizardAdvancedStoryControlsExpanded",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Expanded-state backing field was not found.");
            FieldInfo suppressCollapseField = typeof(DesktopDialogWindow).GetField(
                "_suppressOriginWizardAdvancedStoryControlsCollapseDuringComboRefresh",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Combo-refresh collapse suppression backing field was not found.");

            staleAdvancedStoryControls.IsExpanded = true;
            PumpUi();

            expandedStateField.SetValue(window, true);
            suppressCollapseField.SetValue(window, true);
            staleAdvancedStoryControls.IsExpanded = false;
            PumpUi();

            DesktopDialogState updatedWizard = originWizard with
            {
                Fields = originWizard.Fields
                    .Select(field => string.Equals(field.Id, "newCharacterOriginBackground", StringComparison.Ordinal)
                        ? field with { Value = "corporate" }
                        : field)
                    .ToArray()
            };

            window.BindDialog(updatedWizard);
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));

            Assert.IsTrue(
                reboundAdvancedStoryControls.IsExpanded,
                "Origin Dossier should keep advanced story controls expanded when a same-dialog refresh observes a stale collapsed expander during a combo-preservation pass.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_ignore_transient_pending_collapse_before_same_dialog_rebind()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
        {
            Expander staleAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));

            FieldInfo expandedStateField = typeof(DesktopDialogWindow).GetField(
                "_originWizardAdvancedStoryControlsExpanded",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Expanded-state backing field was not found.");
            FieldInfo transientPendingField = typeof(DesktopDialogWindow).GetField(
                "_originWizardTransientRefreshPending",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Transient-refresh backing field was not found.");

            staleAdvancedStoryControls.IsExpanded = true;
            PumpUi();

            expandedStateField.SetValue(window, true);
            transientPendingField.SetValue(window, true);
            staleAdvancedStoryControls.IsExpanded = false;
            PumpUi();

            Assert.IsTrue(
                (bool)(expandedStateField.GetValue(window) ?? false),
                "Origin Dossier should ignore stale collapse events while a combo-triggered transient refresh is still pending.");

            DesktopDialogState updatedWizard = originWizard with
            {
                Fields = originWizard.Fields
                    .Select(field => string.Equals(field.Id, "newCharacterOriginBackground", StringComparison.Ordinal)
                        ? field with { Value = "corporate" }
                        : field)
                    .ToArray()
            };

            window.BindDialog(updatedWizard);
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));

            Assert.IsTrue(
                reboundAdvancedStoryControls.IsExpanded,
                "Origin Dossier should stay expanded when a stale collapse lands during the transient-refresh window before the same-dialog rebind completes.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_only_commit_collapsed_state_when_the_live_expander_is_still_collapsed()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);

        WithBoundDialogWindow(originWizard, window =>
        {
            Expander advancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));

            FieldInfo expandedStateField = typeof(DesktopDialogWindow).GetField(
                "_originWizardAdvancedStoryControlsExpanded",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Expanded-state backing field was not found.");
            FieldInfo bindVersionField = typeof(DesktopDialogWindow).GetField(
                "_dialogBindVersion",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Dialog bind-version backing field was not found.");
            MethodInfo commitCollapseMethod = typeof(DesktopDialogWindow).GetMethod(
                "CommitOriginWizardAdvancedStoryControlsCollapsedStateIfCurrentExpanderStillCollapsed",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Deferred collapse-commit helper was not found.");

            advancedStoryControls.IsExpanded = true;
            PumpUi();

            expandedStateField.SetValue(window, true);
            int bindVersion = (int)(bindVersionField.GetValue(window) ?? 0);

            commitCollapseMethod.Invoke(window, [bindVersion]);
            PumpUi();

            Assert.IsTrue(
                (bool)(expandedStateField.GetValue(window) ?? false),
                "Transient combo noise must not close advanced story controls while the live expander is still open.");

            advancedStoryControls.IsExpanded = false;
            PumpUi();

            commitCollapseMethod.Invoke(window, [bindVersion]);
            PumpUi();

            Assert.IsFalse(
                (bool)(expandedStateField.GetValue(window) ?? true),
                "An explicit user collapse should still be committed once the live expander is actually closed.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_restore_pre_combo_scroll_anchor_after_popup_like_combo_shift()
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
            ComboBox metatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            advancedStoryControls.IsExpanded = true;
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();
            double preservedOffsetY = scrollViewer.Offset.Y;

            window.Focus();
            metatypePreferenceCombo.Focus();
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 28d);
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

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded after a combo interaction nudges scroll before the selection refresh.");
            Assert.AreEqual(nextMetatypePreference.Value, ((DesktopDialogFieldOption)reboundMetatypePreferenceCombo.SelectedItem!).Value, "The live combo selection should survive the popup-like interaction refresh.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should restore the pre-combo scroll anchor when a combo interaction nudges the dialog before refresh. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");

            Thread.Sleep(440);
            PumpUi();

            Expander settledAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer settledScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            Assert.IsTrue(settledAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded after delayed settle passes following a popup-like combo interaction refresh.");
            Assert.IsTrue(
                Math.Abs(settledScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should not drift after delayed settle passes following a popup-like combo interaction refresh. Before={preservedOffsetY:F1}, After={settledScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_scroll_anchor_during_combo_focus_interaction()
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
            ComboBox metatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            advancedStoryControls.IsExpanded = true;
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();
            double preservedOffsetY = scrollViewer.Offset.Y;

            metatypePreferenceCombo.Focus();
            scrollViewer.Offset = new Vector(0d, 28d);
            PumpUi();

            Assert.IsTrue(advancedStoryControls.IsExpanded, "Advanced story controls should stay expanded while a combo interaction is active.");
            Assert.IsTrue(
                Math.Abs(scrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should restore the pre-combo scroll anchor during combo focus interactions. Before={preservedOffsetY:F1}, After={scrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_do_not_jump_or_collapse_after_live_combo_selection_inside_advanced_controls()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);
        DesktopDialogField buildPreferenceField = originWizard.Fields
            .Single(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal));
        DesktopDialogFieldOption nextBuildPreference = (buildPreferenceField.Options ?? [])
            .First(option => !string.Equals(option.Value, buildPreferenceField.Value, StringComparison.Ordinal));

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
            double preservedOffsetY = scrollViewer.Offset.Y;

            ComboBox buildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));

            buildPreferenceCombo.SelectedItem = nextBuildPreference;
            PumpUi();
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            ComboBox reboundBuildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded after a live combo selection from inside the advanced section.");
            Assert.AreEqual(nextBuildPreference.Value, ((DesktopDialogFieldOption)reboundBuildPreferenceCombo.SelectedItem!).Value, "The in-section combo selection should survive the dialog refresh.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should preserve scroll position across an in-section live combo selection refresh. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_do_not_jump_or_collapse_after_any_live_combo_selection()
    {
        string[] renderedOriginSelectFieldIds =
        [
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
        ];

        foreach (string fieldId in renderedOriginSelectFieldIds)
        {
            DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
                "new_character_origin",
                profile: null,
                DesktopPreferenceState.Default,
                activeSectionJson: null,
                currentWorkspace: null,
                rulesetId: RulesetDefaults.Sr5);
            DesktopDialogField field = originWizard.Fields.Single(candidate => string.Equals(candidate.Id, fieldId, StringComparison.Ordinal));
            DesktopDialogFieldOption nextOption = (field.Options ?? [])
                .First(option => !string.Equals(option.Value, field.Value, StringComparison.Ordinal));

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

                ComboBox? comboBox = window.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .SingleOrDefault(combo => string.Equals(combo.Name, DesktopDialogAccessibility.BuildFieldInputName(fieldId), StringComparison.Ordinal));
                Assert.IsNotNull(comboBox, $"Expected an Origin Dossier combo for {fieldId} before the live selection refresh.");

                scrollViewer.Offset = new Vector(0d, 180d);
                PumpUi();
                double preservedOffsetY = scrollViewer.Offset.Y;

                comboBox.SelectedItem = nextOption;
                PumpUi();
                PumpUi();

                Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                    .OfType<Expander>()
                    .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
                ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
                ComboBox? reboundComboBox = window.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .SingleOrDefault(combo => string.Equals(combo.Name, DesktopDialogAccessibility.BuildFieldInputName(fieldId), StringComparison.Ordinal));
                Assert.IsNotNull(reboundComboBox, $"Expected an Origin Dossier combo for {fieldId} after the live selection refresh.");

                Assert.IsTrue(
                    reboundAdvancedStoryControls.IsExpanded,
                    $"Advanced story controls should stay expanded after a live combo selection refresh for {fieldId}.");
                Assert.AreEqual(
                    nextOption.Value,
                    ((DesktopDialogFieldOption)reboundComboBox.SelectedItem!).Value,
                    $"The live combo selection should survive the dialog refresh for {fieldId}.");
                Assert.IsTrue(
                    Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                    $"Origin Dossier should preserve scroll position across a live combo selection refresh for {fieldId}. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
            });
        }
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_do_not_jump_or_collapse_across_sequential_live_combo_selections()
    {
        string[] sequentialFieldIds =
        [
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
        ];

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

            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();
            double preservedOffsetY = scrollViewer.Offset.Y;
            Dictionary<string, string> appliedValues = new(StringComparer.Ordinal);

            foreach (string fieldId in sequentialFieldIds)
            {
                ComboBox comboBox = window.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .Single(combo => string.Equals(combo.Name, DesktopDialogAccessibility.BuildFieldInputName(fieldId), StringComparison.Ordinal));
                DesktopDialogFieldOption currentOption = (DesktopDialogFieldOption)comboBox.SelectedItem!;
                DesktopDialogFieldOption nextOption = ((IEnumerable<DesktopDialogFieldOption>?)comboBox.ItemsSource ?? [])
                    .First(option => !string.Equals(option.Value, currentOption.Value, StringComparison.Ordinal));

                comboBox.SelectedItem = nextOption;
                appliedValues[fieldId] = nextOption.Value;
                PumpUi();
                PumpUi();

                Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                    .OfType<Expander>()
                    .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
                ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
                ComboBox reboundComboBox = window.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .Single(combo => string.Equals(combo.Name, DesktopDialogAccessibility.BuildFieldInputName(fieldId), StringComparison.Ordinal));

                Assert.IsTrue(
                    reboundAdvancedStoryControls.IsExpanded,
                    $"Advanced story controls should stay expanded after the sequential live combo refresh for {fieldId}.");
                foreach ((string expectedFieldId, string expectedValue) in appliedValues)
                {
                    ComboBox reboundExpectedComboBox = window.GetVisualDescendants()
                        .OfType<ComboBox>()
                        .Single(combo => string.Equals(combo.Name, DesktopDialogAccessibility.BuildFieldInputName(expectedFieldId), StringComparison.Ordinal));
                    Assert.AreEqual(
                        expectedValue,
                        ((DesktopDialogFieldOption)reboundExpectedComboBox.SelectedItem!).Value,
                        $"Sequential live Origin Dossier combo refreshes should preserve the updated value for {expectedFieldId}.");
                }
                Assert.IsTrue(
                    Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                    $"Origin Dossier should preserve scroll position across sequential live combo refreshes for {fieldId}. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
            }

            Thread.Sleep(440);
            PumpUi();

            Expander settledAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer settledScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            Assert.IsTrue(
                settledAdvancedStoryControls.IsExpanded,
                "Advanced story controls should stay expanded after delayed settle passes following sequential live combo refreshes.");
            Assert.IsTrue(
                Math.Abs(settledScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should not keep shifting after sequential live combo refreshes. Before={preservedOffsetY:F1}, After={settledScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_ignore_stale_expander_events_after_live_combo_rebind()
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

            Expander staleAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer scrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            staleAdvancedStoryControls.IsExpanded = true;
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();
            double preservedOffsetY = scrollViewer.Offset.Y;

            ComboBox metatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));
            DesktopDialogFieldOption nextMetatypePreference = ((IEnumerable<DesktopDialogFieldOption>?)metatypePreferenceCombo.ItemsSource ?? [])
                .First(option => !string.Equals(option.Value, ((DesktopDialogFieldOption)metatypePreferenceCombo.SelectedItem!).Value, StringComparison.Ordinal));

            metatypePreferenceCombo.SelectedItem = nextMetatypePreference;
            PumpUi();
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should still be expanded after the first live combo rebind.");

            staleAdvancedStoryControls.IsExpanded = false;
            PumpUi();

            ComboBox buildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));
            DesktopDialogFieldOption nextBuildPreference = ((IEnumerable<DesktopDialogFieldOption>?)buildPreferenceCombo.ItemsSource ?? [])
                .First(option => !string.Equals(option.Value, ((DesktopDialogFieldOption)buildPreferenceCombo.SelectedItem!).Value, StringComparison.Ordinal));

            buildPreferenceCombo.SelectedItem = nextBuildPreference;
            PumpUi();
            PumpUi();

            Expander settledAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer settledScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

            Assert.IsTrue(
                settledAdvancedStoryControls.IsExpanded,
                "Stale expander collapse events from a prior Origin Dossier rebind must not collapse the current advanced story controls.");
            Assert.IsTrue(
                Math.Abs(settledScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should preserve scroll position even if a stale expander instance fires after rebind. Before={preservedOffsetY:F1}, After={settledScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_ignore_stale_combo_selection_events_after_live_combo_rebind()
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

            scrollViewer.Offset = new Vector(0d, 180d);
            PumpUi();
            double preservedOffsetY = scrollViewer.Offset.Y;

            ComboBox staleMetatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));
            DesktopDialogFieldOption originalMetatypePreference = (DesktopDialogFieldOption)staleMetatypePreferenceCombo.SelectedItem!;
            DesktopDialogFieldOption nextMetatypePreference = ((IEnumerable<DesktopDialogFieldOption>?)staleMetatypePreferenceCombo.ItemsSource ?? [])
                .First(option => !string.Equals(option.Value, originalMetatypePreference.Value, StringComparison.Ordinal));

            staleMetatypePreferenceCombo.SelectedItem = nextMetatypePreference;
            PumpUi();
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ComboBox reboundMetatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should still be expanded after the live combo rebind.");
            Assert.AreEqual(
                nextMetatypePreference.Value,
                ((DesktopDialogFieldOption)reboundMetatypePreferenceCombo.SelectedItem!).Value,
                "The live combo rebind should keep the first updated metatype preference selection.");

            staleMetatypePreferenceCombo.SelectedItem = originalMetatypePreference;
            PumpUi();
            PumpUi();

            Expander settledAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer settledScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            ComboBox settledMetatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            Assert.IsTrue(
                settledAdvancedStoryControls.IsExpanded,
                "Stale combo selection events from a prior Origin Dossier rebind must not collapse the current advanced story controls.");
            Assert.AreEqual(
                nextMetatypePreference.Value,
                ((DesktopDialogFieldOption)settledMetatypePreferenceCombo.SelectedItem!).Value,
                "Stale combo selection events from a prior Origin Dossier rebind must not overwrite the current metatype preference.");
            Assert.IsTrue(
                Math.Abs(settledScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should preserve scroll position even if a stale combo instance changes selection after rebind. Before={preservedOffsetY:F1}, After={settledScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_stay_stable_between_immediate_and_delayed_combo_restore_passes()
    {
        string[] renderedOriginSelectFieldIds =
        [
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
        ];

        foreach (string fieldId in renderedOriginSelectFieldIds)
        {
            DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
                "new_character_origin",
                profile: null,
                DesktopPreferenceState.Default,
                activeSectionJson: null,
                currentWorkspace: null,
                rulesetId: RulesetDefaults.Sr5);
            DesktopDialogField field = originWizard.Fields.Single(candidate => string.Equals(candidate.Id, fieldId, StringComparison.Ordinal));
            DesktopDialogFieldOption nextOption = (field.Options ?? [])
                .First(option => !string.Equals(option.Value, field.Value, StringComparison.Ordinal));

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

                ComboBox comboBox = window.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .Single(combo => string.Equals(combo.Name, DesktopDialogAccessibility.BuildFieldInputName(fieldId), StringComparison.Ordinal));

                comboBox.SelectedItem = nextOption;
                PumpUi();

                Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                    .OfType<Expander>()
                    .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
                ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
                double immediateOffsetY = reboundScrollViewer.Offset.Y;

                Thread.Sleep(440);
                PumpUi();

                Expander settledAdvancedStoryControls = window.GetVisualDescendants()
                    .OfType<Expander>()
                    .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
                ScrollViewer settledScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;

                Assert.IsTrue(
                    reboundAdvancedStoryControls.IsExpanded,
                    $"Advanced story controls should still be expanded immediately after the live combo refresh for {fieldId}.");
                Assert.IsTrue(
                    settledAdvancedStoryControls.IsExpanded,
                    $"Advanced story controls should still be expanded after delayed combo-settle passes for {fieldId}.");
                Assert.IsTrue(
                    Math.Abs(settledScrollViewer.Offset.Y - immediateOffsetY) <= 2d,
                    $"Origin Dossier should not keep shifting after the immediate combo refresh for {fieldId}. Immediate={immediateOffsetY:F1}, Settled={settledScrollViewer.Offset.Y:F1}.");
            });
        }
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_active_combo_viewport_anchor_after_live_combo_selection_inside_advanced_controls()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);
        DesktopDialogField buildPreferenceField = originWizard.Fields
            .Single(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal));
        DesktopDialogFieldOption nextBuildPreference = (buildPreferenceField.Options ?? [])
            .First(option => !string.Equals(option.Value, buildPreferenceField.Value, StringComparison.Ordinal));

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

            buildPreferenceCombo.Focus();
            PumpUi();

            Point? preservedAnchor = buildPreferenceCombo.TranslatePoint(default, scrollViewer);
            Assert.IsNotNull(preservedAnchor, "The active advanced combo should expose a viewport anchor before the live selection refresh.");

            buildPreferenceCombo.SelectedItem = nextBuildPreference;
            PumpUi();
            PumpUi();

            ComboBox reboundBuildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            Point? reboundAnchor = reboundBuildPreferenceCombo.TranslatePoint(default, reboundScrollViewer);

            Assert.IsNotNull(reboundAnchor, "The active advanced combo should still expose a viewport anchor after the live selection refresh.");
            Assert.IsTrue(
                Math.Abs(reboundAnchor.Value.Y - preservedAnchor.Value.Y) <= 8d,
                $"Origin Dossier should keep the active advanced combo anchored through live combo refreshes. Before={preservedAnchor.Value.Y:F1}, After={reboundAnchor.Value.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_active_combo_viewport_anchor_after_live_combo_selection_in_story_controls()
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

            scrollViewer.Offset = new Vector(0d, 56d);
            PumpUi();

            ComboBox metatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));

            metatypePreferenceCombo.Focus();
            PumpUi();

            Point? preservedAnchor = metatypePreferenceCombo.TranslatePoint(default, scrollViewer);
            Assert.IsNotNull(preservedAnchor, "The active story combo should expose a viewport anchor before the live selection refresh.");

            metatypePreferenceCombo.SelectedItem = nextMetatypePreference;
            PumpUi();
            PumpUi();

            ComboBox reboundMetatypePreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginMetatypePreference"), StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            Point? reboundAnchor = reboundMetatypePreferenceCombo.TranslatePoint(default, reboundScrollViewer);
            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));

            Assert.IsNotNull(reboundAnchor, "The active story combo should still expose a viewport anchor after the live selection refresh.");
            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded after a story-lane combo refresh.");
            Assert.IsTrue(
                Math.Abs(reboundAnchor.Value.Y - preservedAnchor.Value.Y) <= 8d,
                $"Origin Dossier should keep the active story combo anchored through live combo refreshes. Before={preservedAnchor.Value.Y:F1}, After={reboundAnchor.Value.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_first_pre_selection_scroll_anchor_when_combo_regains_focus_before_selection()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);
        DesktopDialogField buildPreferenceField = originWizard.Fields
            .Single(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal));
        DesktopDialogFieldOption nextBuildPreference = (buildPreferenceField.Options ?? [])
            .First(option => !string.Equals(option.Value, buildPreferenceField.Value, StringComparison.Ordinal));

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
            double preservedOffsetY = scrollViewer.Offset.Y;

            ComboBox buildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));

            buildPreferenceCombo.Focus();
            PumpUi();

            scrollViewer.Focus();
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 28d);
            PumpUi();

            buildPreferenceCombo.Focus();
            PumpUi();

            buildPreferenceCombo.SelectedItem = nextBuildPreference;
            PumpUi();
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            ComboBox reboundBuildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded when a combo regains focus before the selection refresh.");
            Assert.AreEqual(nextBuildPreference.Value, ((DesktopDialogFieldOption)reboundBuildPreferenceCombo.SelectedItem!).Value, "The in-section combo selection should survive the focus-return refresh.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should keep the first pre-selection scroll anchor when combo focus returns before refresh. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_first_pre_selection_scroll_anchor_when_combo_dropdown_reopens_before_selection()
    {
        DesktopDialogState originWizard = new DesktopDialogFactory().CreateCommandDialog(
            "new_character_origin",
            profile: null,
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: null,
            rulesetId: RulesetDefaults.Sr5);
        DesktopDialogField buildPreferenceField = originWizard.Fields
            .Single(field => string.Equals(field.Id, "newCharacterOriginBuildPreference", StringComparison.Ordinal));
        DesktopDialogFieldOption nextBuildPreference = (buildPreferenceField.Options ?? [])
            .First(option => !string.Equals(option.Value, buildPreferenceField.Value, StringComparison.Ordinal));

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
            double preservedOffsetY = scrollViewer.Offset.Y;

            ComboBox buildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));

            buildPreferenceCombo.Focus();
            PumpUi();

            scrollViewer.Offset = new Vector(0d, 28d);
            PumpUi();

            buildPreferenceCombo.IsDropDownOpen = true;
            PumpUi();
            buildPreferenceCombo.IsDropDownOpen = false;
            PumpUi();

            buildPreferenceCombo.SelectedItem = nextBuildPreference;
            PumpUi();
            PumpUi();

            Expander reboundAdvancedStoryControls = window.GetVisualDescendants()
                .OfType<Expander>()
                .Single(expander => string.Equals(expander.Name, "OriginDossierStandaloneAdvancedStoryControlsExpander", StringComparison.Ordinal));
            ScrollViewer reboundScrollViewer = window.FindControl<ScrollViewer>("DialogScrollViewer")!;
            ComboBox reboundBuildPreferenceCombo = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterOriginBuildPreference"), StringComparison.Ordinal));

            Assert.IsTrue(reboundAdvancedStoryControls.IsExpanded, "Advanced story controls should stay expanded when a combo dropdown reopens before the selection refresh.");
            Assert.AreEqual(nextBuildPreference.Value, ((DesktopDialogFieldOption)reboundBuildPreferenceCombo.SelectedItem!).Value, "The in-section combo selection should survive the dropdown-reopen refresh.");
            Assert.IsTrue(
                Math.Abs(reboundScrollViewer.Offset.Y - preservedOffsetY) <= 8d,
                $"Origin Dossier should keep the first pre-selection scroll anchor when a combo dropdown reopens before refresh. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Origin_dossier_advanced_story_controls_keep_first_pre_selection_scroll_anchor_when_another_combo_gains_focus_before_selection()
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
            double preservedOffsetY = scrollViewer.Offset.Y;

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
                $"Origin Dossier should keep the first pre-selection scroll anchor when another combo gains focus before refresh. Before={preservedOffsetY:F1}, After={reboundScrollViewer.Offset.Y:F1}.");
        });
    }

    [TestMethod]
    public void Metatype_continuation_dialogs_keep_labels_and_inputs_readable_in_dark_mode()
    {
        DesktopDialogState priorityDialog = BuildNewCharacterContinuationDialogForTesting("Priority");
        priorityDialog = RebuildNewCharacterContinuationDialogField(priorityDialog, "newCharacterPriorityTalent", "B");
        priorityDialog = RebuildNewCharacterContinuationDialogField(priorityDialog, "newCharacterPriorityTalentChoice", "Magician");

        WithBoundDialogWindow(priorityDialog, window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            ListBox metatypeList = window.GetVisualDescendants()
                .OfType<ListBox>()
                .Single(listBox => string.Equals(listBox.Name, DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetatype"), StringComparison.Ordinal));
            metatypeList.SelectedIndex = Math.Max(0, metatypeList.SelectedIndex);
            PumpUi();
            AssertVisibleInputControlContrast(window, "priority metatype continuation dark mode", minimumVisibleInputControls: 7);
            AssertVisibleSelectedListItemContrast(window, "priority metatype continuation dark mode", minimumSelectedItems: 1);
            AssertVisibleChoiceTextContrast(window, "priority metatype continuation dark mode", minimumVisibleChoiceTexts: 2);
            AssertVisibleTextBlockContrast(window, "priority metatype continuation dark mode", minimumVisibleTextBlocks: 18);
        }, requestedTheme: ThemeVariant.Dark);

        DesktopDialogState karmaDialog = BuildNewCharacterContinuationDialogForTesting("Karma");
        WithBoundDialogWindow(karmaDialog, window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            AssertVisibleInputControlContrast(window, "karma metatype continuation dark mode", minimumVisibleInputControls: 2);
            AssertVisibleTextBlockContrast(window, "karma metatype continuation dark mode", minimumVisibleTextBlocks: 5);
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

    [TestMethod]
    public void Character_create_priorities_port_keeps_notice_selector_and_priority_list_readable_in_dark_mode()
    {
        WithStandaloneCharacterCreateClassicPort(window =>
        {
            using ThemeScope scope = ThemeScope.Dark(window);
            ComboBox prioritySelector = window.GetVisualDescendants()
                .OfType<ComboBox>()
                .Single(comboBox => string.Equals(comboBox.Name, "CreatePrioritySelector", StringComparison.Ordinal));
            ListBox prioritiesList = window.GetVisualDescendants()
                .OfType<ListBox>()
                .Single(listBox => string.Equals(listBox.Name, "CreatePrioritiesList", StringComparison.Ordinal));
            TextBlock noticeText = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(textBlock => string.Equals(textBlock.Name, "CreateNoticeText", StringComparison.Ordinal));

            Assert.IsTrue(prioritySelector.IsVisible, "The character-create Priorities selector must be visible for dark-mode contrast proof.");
            Assert.IsTrue(prioritiesList.IsVisible, "The character-create Priorities list must be visible for dark-mode contrast proof.");
            Assert.IsTrue(noticeText.IsVisible, "The character-create lead notice must stay visible for dark-mode contrast proof.");

            prioritiesList.SelectedIndex = 0;
            PumpUi();
            AssertVisibleInputControlContrast(window, "character-create priorities dark mode", minimumVisibleInputControls: 2);
            AssertVisibleSelectedListItemContrast(window, "character-create priorities dark mode", minimumSelectedItems: 1);
            AssertVisibleChoiceTextContrast(window, "character-create priorities dark mode", minimumVisibleChoiceTexts: 4);
            AssertVisibleTextBlockContrast(window, "character-create priorities dark mode", minimumVisibleTextBlocks: 8);
        }, activeTabId: "Priorities");
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

    private static DesktopDialogState BuildNewCharacterContinuationDialogForTesting(string buildMethod)
    {
        MethodInfo method = typeof(DesktopDialogFactory)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate =>
                string.Equals(candidate.Name, "BuildNewCharacterContinuationDialog", StringComparison.Ordinal)
                && candidate.GetParameters().Length == 5);

        return (DesktopDialogState)(method.Invoke(null, [RulesetDefaults.Sr5, buildMethod, true, "Nova", "Cipher"])
            ?? throw new AssertFailedException("BuildNewCharacterContinuationDialog returned null."));
    }

    private static DesktopDialogState RebuildNewCharacterContinuationDialogField(DesktopDialogState dialog, string fieldId, string value)
    {
        MethodInfo method = typeof(DesktopDialogFactory).GetMethod(
            "RebuildDynamicDialog",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("RebuildDynamicDialog reflection entry point was not found.");

        DesktopDialogField[] updatedFields = dialog.Fields
            .Select(field =>
            {
                if (string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                {
                    return field with { Value = value };
                }

                if (string.Equals(field.Id, "newCharacterPriorityLastChangedFieldId", StringComparison.Ordinal))
                {
                    return field with { Value = fieldId };
                }

                return field;
            })
            .ToArray();

        return (DesktopDialogState)(method.Invoke(null, [dialog with { Fields = updatedFields }, DesktopPreferenceState.Default])
            ?? throw new AssertFailedException("RebuildDynamicDialog returned null."));
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

                            RebindingDialogPresenter presenter = new(dialog);
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

    private static void WithStandaloneCharacterCreateClassicPort(
        Action<Window> assertion,
        string activeTabId = "Attributes",
        string notice = "Ready.",
        string? previewJson = null)
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
                            string effectivePreviewJson = previewJson ?? """
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
                            ClassicFormPortDocument document = ClassicFormPortDocument.CreateFromPreview(effectivePreviewJson, "character_create");
                            port.SetState(new ClassicFormPortState(
                                SurfaceId: "character_create",
                                RuntimeSectionId: "character_create",
                                ActiveTabId: activeTabId,
                                ActiveActionId: null,
                                Notice: notice,
                                PreviewJson: effectivePreviewJson,
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
        public RebindingDialogPresenter(DesktopDialogState dialog)
        {
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

        public Task HandleUiControlAsync(string controlId, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct)
        {
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
                [dialog with { Fields = updatedFields }, DesktopPreferenceState.Default])
                ?? throw new AssertFailedException("RebuildDynamicDialog returned null."));

            Publish(State with
            {
                ActiveDialog = nextDialog
            });

            return Task.CompletedTask;
        }

        public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct) => Task.CompletedTask;

        public Task CloseDialogAsync(CancellationToken ct) => Task.CompletedTask;

        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;

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
