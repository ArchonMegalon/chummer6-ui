using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopThemeManagerTests
{
    [TestMethod]
    public void ColorManager_source_themes_standard_combo_dropdowns_without_reintroducing_editable_text_overlap()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Backend", "Static", "Managers", "ColorManager.cs"));
        string elasticComboSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Controls", "Shared", "Components", "ElasticComboBox.cs"));

        StringAssert.Contains(source, "case ComboBox comboBox when comboBox is not ElasticComboBox:");
        StringAssert.Contains(source, "comboBox.DrawMode = DrawMode.OwnerDrawFixed;");
        StringAssert.Contains(source, "comboBox.DrawItem += handler;");
        StringAssert.Contains(source, "objControl is ComboBox comboBox");
        StringAssert.Contains(source, "comboBox.DropDownStyle != ComboBoxStyle.DropDownList");
        StringAssert.Contains(source, "return;");
        StringAssert.Contains(elasticComboSource, "if (e.Index == -1 && DropDownStyle != ComboBoxStyle.DropDownList)");
        StringAssert.Contains(elasticComboSource, "ColorManager.Highlight");
        StringAssert.Contains(elasticComboSource, "ColorManager.HighlightText");
        StringAssert.Contains(elasticComboSource, "TextFormatFlags.EndEllipsis");
    }

    [TestMethod]
    public void Sr4_metatype_and_add_quality_selection_forms_use_themed_elastic_combos_and_light_dark_mode()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string selectQualitySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectQuality.cs"));
        string selectQualityDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectQuality.Designer.cs"));
        string selectMetatypeKarmaSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypeKarma.cs"));
        string selectMetatypeKarmaDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypeKarma.Designer.cs"));
        string selectMetatypePrioritySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypePriority.cs"));
        string selectMetatypePriorityDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypePriority.Designer.cs"));

        StringAssert.Contains(selectQualitySource, "this.UpdateLightDarkMode();");
        StringAssert.Contains(selectMetatypeKarmaSource, "this.UpdateLightDarkMode();");
        StringAssert.Contains(selectMetatypePrioritySource, "this.UpdateLightDarkMode();");
        StringAssert.Contains(selectQualityDesigner, "this.cboCategory = new Chummer.ElasticComboBox();");
        StringAssert.Contains(selectQualityDesigner, "this.txtSearch = new Chummer.ElasticComboBox();");
        StringAssert.Contains(selectMetatypeKarmaDesigner, "this.cboCategory = new Chummer.ElasticComboBox();");
        StringAssert.Contains(selectMetatypeKarmaDesigner, "this.cboMetavariant = new Chummer.ElasticComboBox();");
        StringAssert.Contains(selectMetatypePriorityDesigner, "this.cboCategory = new Chummer.ElasticComboBox();");
        StringAssert.Contains(selectMetatypePriorityDesigner, "this.cboMetavariant = new Chummer.ElasticComboBox();");
        StringAssert.Contains(selectMetatypePriorityDesigner, "this.cboHeritage = new Chummer.ElasticComboBox();");
        Assert.IsFalse(selectQualityDesigner.Contains("SystemColors.Highlight", StringComparison.Ordinal));
        Assert.IsFalse(selectMetatypeKarmaDesigner.Contains("SystemColors.Highlight", StringComparison.Ordinal));
        Assert.IsFalse(selectMetatypePriorityDesigner.Contains("SystemColors.Highlight", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Add_select_and_create_winforms_dialogs_participate_in_light_dark_theming()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string formsRoot = Path.Combine(repoRoot, "Chummer", "Forms");
        string[] unthemedDialogs = Directory
            .EnumerateFiles(formsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(static path =>
            {
                string fileName = Path.GetFileName(path);
                return fileName.StartsWith("Add", StringComparison.Ordinal)
                    || fileName.StartsWith("Create", StringComparison.Ordinal)
                    || fileName.StartsWith("Select", StringComparison.Ordinal);
            })
            .Where(static path =>
            {
                string source = File.ReadAllText(path);
                return !source.Contains("UpdateLightDarkMode(", StringComparison.Ordinal)
                    && !source.Contains("ColorManager.", StringComparison.Ordinal)
                    && !source.Contains("LightDark", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(
            0,
            unthemedDialogs.Length,
            "Every Add/Select/Create dialog must opt into theming. Missing: " + string.Join(", ", unthemedDialogs));
    }

    [TestMethod]
    public void Horizon_desktop_surfaces_use_shared_neutral_shell_fallbacks_instead_of_legacy_warm_cards()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string scaffoldSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHorizonWindowScaffold.cs"));
        string blackLedgerSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopBlackLedgerWindow.cs"));
        string runControlSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunControlWindow.cs"));

        StringAssert.Contains(scaffoldSource, "DesktopShellTheme.ResolveThemeBrush(resourceKey, fallbackHex)");
        StringAssert.Contains(scaffoldSource, "ResolveThemeBrush(\"ChummerShellSurfaceAltBrush\", \"#F2F5FA\")");
        StringAssert.Contains(scaffoldSource, "ResolveThemeBrush(\"ChummerShellChromeAccentBrush\", \"#DEE8F6\")");
        StringAssert.Contains(scaffoldSource, "DesktopShellTheme.ApplyPrimaryButton(button);");
        Assert.IsFalse(scaffoldSource.Contains("#F7F4EC", StringComparison.Ordinal));
        Assert.IsFalse(scaffoldSource.Contains("#E6E2DA", StringComparison.Ordinal));
        Assert.IsFalse(scaffoldSource.Contains("#1C4A2D", StringComparison.Ordinal));
        Assert.IsFalse(blackLedgerSource.Contains("#F7F4EC", StringComparison.Ordinal));
        Assert.IsFalse(runControlSource.Contains("#F7F4EC", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Trust_and_classic_desktop_surfaces_use_shell_semantic_brushes_instead_of_legacy_amber_and_gray_literals()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string trustPanelSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopTrustPanelFactory.cs"));
        string explainLauncherSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopExplainCompanionLauncher.cs"));
        string sectionHostSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml.cs"));
        string classicSurfaceSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "ClassicFormPortSurfaceControl.cs"));

        StringAssert.Contains(trustPanelSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellSelectionPanelBrush\", \"#F8FAFC\")");
        StringAssert.Contains(trustPanelSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellWarningBrush\", \"#9A6700\")");
        StringAssert.Contains(explainLauncherSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellWarningBrush\", \"#9A6700\")");
        StringAssert.Contains(explainLauncherSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellSelectionInsetBrush\", \"#F1F5F9\")");
        StringAssert.Contains(sectionHostSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellWarningBrush\", \"#9A6700\")");
        StringAssert.Contains(sectionHostSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#B5C0CF\")");
        StringAssert.Contains(classicSurfaceSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#475569\")");
        Assert.IsFalse(trustPanelSource.Contains("#FFF6E1", StringComparison.Ordinal));
        Assert.IsFalse(trustPanelSource.Contains("#D9B05F", StringComparison.Ordinal));
        Assert.IsFalse(explainLauncherSource.Contains("#D9B05F", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("#4F3C16", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("#FFF7F4EB", StringComparison.Ordinal));
        Assert.IsFalse(classicSurfaceSource.Contains("#3f4b53", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Utility_workbenches_and_alice_detail_cards_use_shell_border_brush_instead_of_pale_literal_borders()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string aliceSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));
        string runbookPressSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunbookPressWindow.cs"));
        string creatorOsSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorOsWindow.cs"));
        string nexusPanSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopNexusPanWindow.cs"));
        string runsiteSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunsiteWindow.cs"));

        StringAssert.Contains(aliceSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#B5C0CF\")");
        StringAssert.Contains(runbookPressSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#B5C0CF\")");
        StringAssert.Contains(creatorOsSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#B5C0CF\")");
        StringAssert.Contains(nexusPanSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#B5C0CF\")");
        StringAssert.Contains(runsiteSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#B5C0CF\")");
        Assert.IsFalse(aliceSource.Contains("#D3DCE5", StringComparison.Ordinal));
        Assert.IsFalse(runbookPressSource.Contains("#D3DCE5", StringComparison.Ordinal));
        Assert.IsFalse(creatorOsSource.Contains("#D3DCE5", StringComparison.Ordinal));
        Assert.IsFalse(nexusPanSource.Contains("#D3DCE5", StringComparison.Ordinal));
        Assert.IsFalse(runsiteSource.Contains("#D3DCE5", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Selection_candidate_rows_do_not_paint_inner_pale_cards_that_break_selected_state_contrast()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string commandDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CommandDialogPaneControl.axaml.cs"));
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string appTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml"));

        Assert.IsFalse(commandDialogSource.Contains("Background = ResolveThemeBrush(\"ChummerShellSelectionInsetBrush\", \"#F1F5F9\")", StringComparison.Ordinal));
        Assert.IsFalse(commandDialogSource.Contains("BorderBrush = ResolveThemeBrush(\"ChummerShellSelectionTitleBorderBrush\", \"#CBD5E1\")", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("Background = ResolveThemeBrush(\"ChummerShellSelectionInsetBrush\", \"#F1F5F9\")", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("BorderBrush = ResolveThemeBrush(\"ChummerShellSelectionTitleBorderBrush\", \"#CBD5E1\")", StringComparison.Ordinal));
        StringAssert.Contains(commandDialogSource, "Classes = { \"shell-option-label\" }");
        StringAssert.Contains(commandDialogSource, "DesktopShellTheme.CreateOptionMetaText(meta)");
        StringAssert.Contains(desktopDialogSource, "Classes = { \"shell-option-label\" }");
        StringAssert.Contains(desktopDialogSource, "CreateOptionMetaText(meta)");
        StringAssert.Contains(appTheme, "ListBoxItem:selected TextBlock.shell-option-meta");
        StringAssert.Contains(appTheme, "ComboBoxItem:selected TextBlock.shell-option-meta");
    }

    [TestMethod]
    public void Generic_dialog_visual_fields_do_not_reintroduce_hard_gray_or_legacy_info_color_fallbacks()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string commandDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CommandDialogPaneControl.axaml.cs"));
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));

        StringAssert.Contains(commandDialogSource, "ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#B5C0CF\")");
        StringAssert.Contains(desktopDialogSource, "ResolveThemeBrush(\"ChummerShellBorderBrush\", \"#B5C0CF\")");
        Assert.IsFalse(commandDialogSource.Contains("Brushes.Gray", StringComparison.Ordinal));
        Assert.IsFalse(commandDialogSource.Contains("#808080", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("#808080", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("#1E90FF", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("#483D8B", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("#F7FAFD", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("#C7D2E1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Desktop_selectable_and_readonly_surfaces_use_shell_theme_instead_of_transparent_default_colors()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string appTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml"));
        string shellTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopShellTheme.cs"));
        string aliceSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));
        string scaffoldSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHorizonWindowScaffold.cs"));
        string localCoProcessorSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopLocalCoProcessorWindow.cs"));
        string runnerPassportSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunnerPassportWindow.cs"));
        string versionHistorySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopVersionHistoryWindow.cs"));
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string commandDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CommandDialogPaneControl.axaml.cs"));
        string sectionHost = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml"));
        string classicPortSurface = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "ClassicFormPortSurfaceControl.cs"));

        StringAssert.Contains(shellTheme, "ApplyShellListBoxTheme(ListBox listBox)");
        StringAssert.Contains(shellTheme, "CreateOptionText(string text");
        StringAssert.Contains(shellTheme, "CreateOptionMetaText(string text");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBox\">");
        StringAssert.Contains(appTheme, "<Setter Property=\"Background\" Value=\"{DynamicResource ChummerShellSurfaceBrush}\" />");
        StringAssert.Contains(appTheme, "TextBlock.shell-option-label");
        StringAssert.Contains(appTheme, "TextBlock.shell-option-meta");
        StringAssert.Contains(aliceSource, "DesktopShellTheme.ApplyShellListBoxTheme(conversationList);");
        StringAssert.Contains(aliceSource, "DesktopShellTheme.ApplyShellListBoxTheme(evidenceList);");
        StringAssert.Contains(scaffoldSource, "Background = ResolveThemeBrush(\"ChummerShellWindowBackgroundBrush\", \"#E3EAF3\")");
        StringAssert.Contains(localCoProcessorSource, "DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);");
        StringAssert.Contains(runnerPassportSource, "DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);");
        StringAssert.Contains(versionHistorySource, "DesktopShellTheme.ApplyShellTextInputTheme(historyBox);");
        StringAssert.Contains(desktopDialogSource, "ApplyShellListBoxTheme(listBox);");
        StringAssert.Contains(commandDialogSource, "DesktopShellTheme.ApplyShellListBoxTheme(listBox);");
        StringAssert.Contains(sectionHost, "Background=\"{DynamicResource ChummerShellSurfaceBrush}\"");
        StringAssert.Contains(classicPortSurface, "Background = DesktopShellTheme.ResolveThemeBrush(\"ChummerShellSurfaceBrush\", \"#FBFCFE\")");
        StringAssert.Contains(appTheme, "ChummerShellActiveMenuBorderBrush");
        StringAssert.Contains(appTheme, "#1C4A2D");
        StringAssert.Contains(appTheme, "#90C39A");
        Assert.IsFalse(desktopDialogSource.Contains("Background = Brushes.Transparent", StringComparison.Ordinal));
        Assert.IsFalse(commandDialogSource.Contains("Background = Brushes.Transparent", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Avalonia_shell_uses_app_owned_theme_by_default_instead_of_partial_os_dark_mode()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string appTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml"));

        StringAssert.Contains(appTheme, "RequestedThemeVariant=\"Light\"");
        Assert.IsFalse(
            appTheme.Contains("RequestedThemeVariant=\"Default\"", StringComparison.Ordinal),
            "The app must not inherit partial OS dark-mode colors without switching its full shell palette.");
    }

    [TestMethod]
    public void Home_and_horizons_shell_surfaces_route_sections_and_buttons_through_shared_shell_theme_helpers()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string homeSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"));
        string horizonsSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHorizonsWindow.cs"));

        StringAssert.Contains(homeSource, "DesktopShellTheme.CreateSection(");
        StringAssert.Contains(homeSource, "DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 92)");
        StringAssert.Contains(homeSource, "DesktopShellTheme.CreateStackActionRow(CreateInstallActions(), spacing: 8)");
        StringAssert.Contains(homeSource, "DesktopShellTheme.ResetActionRow(_installActionsRow, CreateInstallActions())");
        Assert.IsFalse(homeSource.Contains("ResolveThemeBrush(\"ChummerShellAccentButtonBrush\", \"#163A59\")", StringComparison.Ordinal));
        Assert.IsFalse(homeSource.Contains("ResolveThemeBrush(\"ChummerShellActiveMenuBorderBrush\", \"#7FB3DA\")", StringComparison.Ordinal));

        StringAssert.Contains(horizonsSource, "DesktopShellTheme.CreateWrapActionRow(actions, new Thickness(0, 0, 8, 8))");
        StringAssert.Contains(horizonsSource, "DesktopShellTheme.CreateSection(");
        Assert.IsFalse(horizonsSource.Contains("Background = DesktopShellTheme.ResolveThemeBrush(\"ChummerShellSurfaceAltBrush\", \"#F2F5FA\")", StringComparison.Ordinal));
    }

    [TestMethod]
    public void About_window_uses_read_only_shell_panels_instead_of_input_boxes_for_prose()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string aboutSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAboutWindow.cs"));

        StringAssert.Contains(aboutSource, "Content = DesktopShellTheme.CreateWindowSurface(");
        StringAssert.Contains(aboutSource, "DesktopShellTheme.CreateUtilityPanel(content, padding: 12, cornerRadius: 6)");
        StringAssert.Contains(aboutSource, "BuildReadOnlyPanel(projection.Description)");
        StringAssert.Contains(aboutSource, "DesktopShellTheme.CreateUtilityPanel(");
        Assert.IsFalse(aboutSource.Contains("private static TextBox BuildReadOnlyBox", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Report_and_account_windows_use_explicit_shell_window_surfaces()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string reportSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopReportIssueWindow.cs"));
        string devicesSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDevicesAccessWindow.cs"));

        StringAssert.Contains(reportSource, "Content = DesktopShellTheme.CreateWindowSurface(");
        StringAssert.Contains(devicesSource, "Content = DesktopShellTheme.CreateWindowSurface(");
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.bug.title_label\"), _bugTitleBox)");
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.feedback.detail_label\"), _feedbackDetailBox)");
        Assert.IsFalse(reportSource.Contains("Child = new StackPanel", StringComparison.Ordinal));
        Assert.IsFalse(devicesSource.Contains("Child = new StackPanel", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        int wizardStart = desktopDialogSource.IndexOf("private Control CreateLegacyOriginWizardPane", StringComparison.Ordinal);
        int buildStart = desktopDialogSource.IndexOf("private Control CreateLegacyOriginBuildPane", StringComparison.Ordinal);
        int summaryStart = desktopDialogSource.IndexOf("private static Control CreateOriginSummaryStrip", StringComparison.Ordinal);
        int nextMethodStart = desktopDialogSource.IndexOf("private Control CreateLegacyPriorityWorkflowPane", StringComparison.Ordinal);
        Assert.IsTrue(wizardStart >= 0 && buildStart > wizardStart, "Origin wizard source must be discoverable for the theme gate.");
        Assert.IsTrue(summaryStart > buildStart && nextMethodStart > summaryStart, "Origin summary strip source must be discoverable for the theme gate.");
        string originSurfaceSource = desktopDialogSource[wizardStart..nextMethodStart];

        StringAssert.Contains(desktopDialogSource, "CreateLegacyOriginWizardPane(fields)");
        StringAssert.Contains(desktopDialogSource, "CreateLegacyOriginBuildPane(fields)");
        StringAssert.Contains(desktopDialogSource, "CreateOriginSummaryStrip(");
        StringAssert.Contains(originSurfaceSource, "Classes = { \"shell-panel\" }");
        StringAssert.Contains(originSurfaceSource, "Classes = { \"shell-kicker\" }");
        StringAssert.Contains(originSurfaceSource, "Foreground = ResolveThemeBrush(\"ChummerShellForegroundBrush\", \"#111827\")");
        StringAssert.Contains(desktopDialogSource, "newCharacterOriginGmConstraintPreset");
        StringAssert.Contains(desktopDialogSource, "build plan");
        StringAssert.Contains(desktopDialogSource, "\"Story Preview\"");
        StringAssert.Contains(desktopDialogSource, "\"Origin Story\"");
        Assert.IsFalse(originSurfaceSource.Contains("Color.Parse", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Brushes.White", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Brushes.Black", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("new SolidColorBrush", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Background = Brushes", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Foreground = Brushes", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("newCharacterOriginGmConstraintPreset\", \"GM Constraint\", \"none\", \"none\"", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("build lane", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(desktopDialogSource.Contains("\"ALICE Handoff\"", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("ALICE translates the story", StringComparison.Ordinal));
    }

    [TestMethod]
    public void New_character_dialog_keeps_options_inline_and_uses_player_facing_copy()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string factorySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopDialogFactory.cs"));
        int methodStart = desktopDialogSource.IndexOf("private Control CreateLegacyNewCharacterPane", StringComparison.Ordinal);
        int methodEnd = desktopDialogSource.IndexOf("private Control CreateLegacyOriginWizardPane", StringComparison.Ordinal);
        Assert.IsTrue(methodStart >= 0 && methodEnd > methodStart, "New-character pane source must be discoverable for the polish gate.");
        string newCharacterPaneSource = desktopDialogSource[methodStart..methodEnd];

        StringAssert.Contains(newCharacterPaneSource, "Content = \"Options\"");
        StringAssert.Contains(newCharacterPaneSource, "optionsPanel.IsVisible = !optionsPanel.IsVisible;");
        StringAssert.Contains(newCharacterPaneSource, "Text = \"Build method:\"");
        StringAssert.Contains(desktopDialogSource, "CreateRowLabel(\"Metatype Filter:\"");
        StringAssert.Contains(factorySource, "new DesktopDialogFieldOption(\"Standard\", \"Core metatypes\")");
        StringAssert.Contains(factorySource, "new DesktopDialogFieldOption(\"Metahuman\", \"Metahumans only\")");
        StringAssert.Contains(factorySource, "new DesktopDialogFieldOption(\"Show All\", \"All available\")");
        StringAssert.Contains(factorySource, "\"Remaining Karma | tracked when the character opens\"");
        StringAssert.Contains(factorySource, "new DesktopDialogAction(\"start_from_origin\", \"Start Origin Dossier\")");
        Assert.IsFalse(newCharacterPaneSource.Contains("ExecuteCommandAsync(\"character_settings\"", StringComparison.Ordinal));
        Assert.IsFalse(factorySource.Contains("\"Metatype Category\"", StringComparison.Ordinal));
        Assert.IsFalse(factorySource.Contains("legacy metatype continuation", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Attribute_editor_keeps_column_headers_visible()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string sectionHostMarkup = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml"));

        StringAssert.Contains(sectionHostMarkup, "Text=\"Attribute\" Classes=\"shell-section-title\"");
        StringAssert.Contains(sectionHostMarkup, "Text=\"Base\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "Text=\"Karma\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "Text=\"Total\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "Text=\"Limits\" Classes=\"shell-caption\"");
        Assert.IsFalse(sectionHostMarkup.Contains("Text=\"Val (Aug)\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostMarkup.Contains("Text=\"Points\" Classes=\"shell-caption\" HorizontalAlignment=\"Right\" IsVisible=\"False\"", StringComparison.Ordinal));

        string sectionHostSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml.cs"));
        StringAssert.Contains(sectionHostSource, "CreateAttributeValueStepper(");
        StringAssert.Contains(sectionHostSource, "$\"AttributeBaseEditor_{ShortAttributeLabel(row.AttributeName)}\"");
        StringAssert.Contains(sectionHostSource, "$\"AttributeKarmaEditor_{ShortAttributeLabel(row.AttributeName)}\"");
        StringAssert.Contains(sectionHostSource, "\"B\"");
        StringAssert.Contains(sectionHostSource, "\"K\"");
        StringAssert.Contains(sectionHostSource, "$\"{row.DisplayName} base allocation\"");
        StringAssert.Contains(sectionHostSource, "$\"{row.DisplayName} karma adjustment\"");
        StringAssert.Contains(sectionHostSource, "AutomationProperties.SetName(stepper, accessibleName)");
        Assert.IsFalse(sectionHostSource.Contains("NumericUpDown", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Dialog_textboxes_keep_accessibility_but_do_not_show_duplicate_hover_text()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string commandDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CommandDialogPaneControl.axaml.cs"));
        string sectionHostMarkup = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml"));
        string sectionHostSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml.cs"));

        StringAssert.Contains(desktopDialogSource, "ApplyTextBoxAccessibility(textBox");
        StringAssert.Contains(commandDialogSource, "ApplyTextBoxAccessibility(textBox");
        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ApplyShellTextInputTheme(textBox);");
        StringAssert.Contains(commandDialogSource, "DesktopShellTheme.ApplyShellTextInputTheme(textBox);");
        StringAssert.Contains(sectionHostSource, "DesktopShellTheme.ApplyShellTextInputTheme(SectionPreviewBox);");
        StringAssert.Contains(sectionHostSource, "DesktopShellTheme.ApplyShellTextInputTheme(XmlInputBox);");
        StringAssert.Contains(File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopShellTheme.cs")), "ToolTip.SetTip(textBox, null);");
        StringAssert.Contains(desktopDialogSource, "ToolTip.SetTip(textBox, null);");
        StringAssert.Contains(commandDialogSource, "ToolTip.SetTip(textBox, null);");
        Assert.IsFalse(sectionHostMarkup.Contains("ToolTip.Tip=\"Paste raw XML", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Sr4_metatype_priority_detail_values_pin_shell_foreground_so_kde_dark_mode_cannot_hide_numbers()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));

        StringAssert.Contains(desktopDialogSource, "AddLabeledValueRow(rightFactsGrid, 1, \"Karma:\", new TextBlock { Text = runtimeState.MetatypeKarma });");
        StringAssert.Contains(desktopDialogSource, "AddLabeledValueRow(rightFactsGrid, 2, \"Special Attributes:\", new TextBlock { Text = runtimeState.SpecialAttributes });");
        StringAssert.Contains(desktopDialogSource, "valueText.Foreground = ResolveThemeBrush(\"ChummerShellForegroundBrush\", \"#111827\");");
        StringAssert.Contains(desktopDialogSource, "valueText.FontWeight = FontWeight.SemiBold;");
        StringAssert.Contains(desktopDialogSource, "Text = attribute.Value,");
        StringAssert.Contains(desktopDialogSource, "Foreground = ResolveThemeBrush(\"ChummerShellForegroundBrush\", \"#111827\"),");
    }

    [TestMethod]
    public void Selection_add_surfaces_do_not_label_readonly_context_as_navigation()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));

        StringAssert.Contains(desktopDialogSource, "ResolveSelectionNavigationTitle(navigationField)");
        StringAssert.Contains(desktopDialogSource, "private static string ResolveSelectionNavigationTitle(DesktopDialogField field)");
        StringAssert.Contains(desktopDialogSource, "? \"Categories\"");
        StringAssert.Contains(desktopDialogSource, ": \"Current selection\"");
        Assert.IsFalse(desktopDialogSource.Contains("CreateSelectionSurfaceCard(navigationField.Label", StringComparison.Ordinal));
    }
}
