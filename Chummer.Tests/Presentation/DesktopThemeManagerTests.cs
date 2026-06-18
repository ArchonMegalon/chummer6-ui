using System.IO;
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

        Assert.IsFalse(commandDialogSource.Contains("Background = ResolveThemeBrush(\"ChummerShellSelectionInsetBrush\", \"#F1F5F9\")", StringComparison.Ordinal));
        Assert.IsFalse(commandDialogSource.Contains("BorderBrush = ResolveThemeBrush(\"ChummerShellSelectionTitleBorderBrush\", \"#CBD5E1\")", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("Background = ResolveThemeBrush(\"ChummerShellSelectionInsetBrush\", \"#F1F5F9\")", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("BorderBrush = ResolveThemeBrush(\"ChummerShellSelectionTitleBorderBrush\", \"#CBD5E1\")", StringComparison.Ordinal));
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
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string commandDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CommandDialogPaneControl.axaml.cs"));
        string sectionHost = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml"));
        string classicPortSurface = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "ClassicFormPortSurfaceControl.cs"));

        StringAssert.Contains(shellTheme, "ApplyShellListBoxTheme(ListBox listBox)");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBox\">");
        StringAssert.Contains(appTheme, "<Setter Property=\"Background\" Value=\"{DynamicResource ChummerShellSurfaceBrush}\" />");
        StringAssert.Contains(aliceSource, "DesktopShellTheme.ApplyShellListBoxTheme(conversationList);");
        StringAssert.Contains(aliceSource, "DesktopShellTheme.ApplyShellListBoxTheme(evidenceList);");
        StringAssert.Contains(desktopDialogSource, "ApplyShellListBoxTheme(listBox);");
        StringAssert.Contains(commandDialogSource, "DesktopShellTheme.ApplyShellListBoxTheme(listBox);");
        StringAssert.Contains(sectionHost, "Background=\"{DynamicResource ChummerShellSurfaceBrush}\"");
        StringAssert.Contains(classicPortSurface, "Background = DesktopShellTheme.ResolveThemeBrush(\"ChummerShellSurfaceBrush\", \"#FBFCFE\")");
        Assert.IsFalse(appTheme.Contains("#1C4A2D", StringComparison.Ordinal));
        Assert.IsFalse(appTheme.Contains("#90C39A", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("Background = Brushes.Transparent", StringComparison.Ordinal));
        Assert.IsFalse(commandDialogSource.Contains("Background = Brushes.Transparent", StringComparison.Ordinal));
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
    public void About_window_read_only_text_boxes_use_shell_input_theme()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string aboutSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAboutWindow.cs"));

        StringAssert.Contains(aboutSource, "Content = DesktopShellTheme.CreateWindowSurface(");
        StringAssert.Contains(aboutSource, "DesktopShellTheme.CreateUtilityPanel(content, padding: 12, cornerRadius: 6)");
        StringAssert.Contains(aboutSource, "DesktopShellTheme.ApplyShellTextInputTheme(textBox);");
    }

    [TestMethod]
    public void Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));

        StringAssert.Contains(desktopDialogSource, "CreateLegacyOriginWizardPane(fields)");
        StringAssert.Contains(desktopDialogSource, "CreateLegacyOriginBuildPane(fields)");
        StringAssert.Contains(desktopDialogSource, "CreateOriginSummaryStrip(");
        StringAssert.Contains(desktopDialogSource, "newCharacterOriginGmConstraintPreset");
        Assert.IsFalse(desktopDialogSource.Contains("newCharacterOriginGmConstraintPreset\", \"GM Constraint\", \"none\", \"none\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Dialog_textboxes_keep_accessibility_but_do_not_show_duplicate_hover_text()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string commandDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CommandDialogPaneControl.axaml.cs"));

        StringAssert.Contains(desktopDialogSource, "ApplyTextBoxAccessibility(textBox");
        StringAssert.Contains(commandDialogSource, "ApplyTextBoxAccessibility(textBox");
        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ApplyShellTextInputTheme(textBox);");
        StringAssert.Contains(commandDialogSource, "DesktopShellTheme.ApplyShellTextInputTheme(textBox);");
        StringAssert.Contains(desktopDialogSource, "ToolTip.SetTip(textBox, null);");
        StringAssert.Contains(commandDialogSource, "ToolTip.SetTip(textBox, null);");
    }
}
