using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        StringAssert.Contains(source, "case ListBox _:");
        StringAssert.Contains(source, "case ComboBox _:");
        StringAssert.Contains(source, "EnsureThemedListDraw(objControl);");
        StringAssert.Contains(source, "await objControl.DoThreadSafeAsync(x => EnsureThemedListDraw(x), token).ConfigureAwait(false);");
        StringAssert.Contains(source, "objControl is ComboBox comboBox");
        StringAssert.Contains(source, "comboBox.DropDownStyle != ComboBoxStyle.DropDownList");
        StringAssert.Contains(source, "return;");
        StringAssert.Contains(source, "public static Color EnsureReadableForeground(Color objForeColor, Color objBackColor)");
        StringAssert.Contains(source, "GetContrastRatio(objForeColor, objBackColor) >= 4.5d");
        StringAssert.Contains(source, "GetRelativeLuminance(Color objColor)");
        StringAssert.Contains(source, "return GetRelativeLuminance(objBackColor) > 0.5d");
        StringAssert.Contains(source, "? Color.Black");
        StringAssert.Contains(source, ": Color.White;");
        StringAssert.Contains(source, "IsSameColor(objColor, SystemColors.ControlText)");
        StringAssert.Contains(source, "IsSameColor(objColor, SystemColors.Control)");
        StringAssert.Contains(source, "IsSameColor(objColor, SystemColors.ControlLight)");
        StringAssert.Contains(source, "IsSameColor(objColor, SystemColors.Window)");
        StringAssert.Contains(source, "IsControlTextColor(x.ForeColor)");
        StringAssert.Contains(source, "IsControlSurfaceColor(x.BackColor)");
        StringAssert.Contains(source, "IsWindowSurfaceColor(x.BackColor)");
        StringAssert.Contains(source, "x.ForeColor = EnsureReadableForeground(x.ForeColor, x.BackColor, blnLightMode);");
        StringAssert.Contains(source, "objForeColor = EnsureReadableForeground(objForeColor, objBackColor);");
        StringAssert.Contains(source, "objForeColor = EnsureReadableForeground(objForeColor, objBackColor, blnLightMode);");
        StringAssert.Contains(elasticComboSource, "if (e.Index == -1 && DropDownStyle != ComboBoxStyle.DropDownList)");
        StringAssert.Contains(elasticComboSource, "ColorManager.EnsureReadableForeground(objForeColor, objBackColor);");
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
    public void Sr4_metatype_selection_forms_filter_ai_character_options_when_ai_features_are_disabled()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string runtimeFilterSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopAiFeaturePreferenceFilter.cs"));
        string selectMetatypeKarmaSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypeKarma.cs"));
        string selectMetatypePrioritySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypePriority.cs"));

        StringAssert.Contains(runtimeFilterSource, "OverviewCommandPolicy.IsAiFeatureCharacterOrCompanionOption(value)");
        StringAssert.Contains(runtimeFilterSource, "\"avalonia\", \"winforms\"");

        StringAssert.Contains(selectMetatypeKarmaSource, "DesktopAiFeaturePreferenceFilter.AreAiCharacterOptionsDisabled();");
        StringAssert.Contains(selectMetatypeKarmaSource, "ShouldHideAiFeatureOption(strInnerText)");
        StringAssert.Contains(selectMetatypeKarmaSource, "!ShouldHideAiFeatureNode(objXmlMetavariant, token)");
        StringAssert.Contains(selectMetatypeKarmaSource, "!ShouldHideAiFeatureNode(xmlMetatype, token)");

        StringAssert.Contains(selectMetatypePrioritySource, "DesktopAiFeaturePreferenceFilter.AreAiCharacterOptionsDisabled();");
        StringAssert.Contains(selectMetatypePrioritySource, "ShouldHideAiFeatureOption(strTalentValue, strTalentName, strTalentTranslate)");
        StringAssert.Contains(selectMetatypePrioritySource, "ShouldHideAiFeatureNode(objXmlMetavariant, token)");
        StringAssert.Contains(selectMetatypePrioritySource, "ShouldHideAiFeatureNode(objXmlMetatype, token)");
        StringAssert.Contains(selectMetatypePrioritySource, "ShouldHideAiFeatureOption(objXmlCategory.Value)");
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
    public void Add_select_and_create_designer_files_do_not_pin_input_colors_against_the_shell_theme()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string formsRoot = Path.Combine(repoRoot, "Chummer", "Forms");
        Regex pinnedInputColorRegex = new(
            @"\.(BackColor|ForeColor)\s*=\s*(System\.Drawing\.Color\.(White|Black|WhiteSmoke)|SystemColors\.(Window|WindowText|Control|ControlText|Highlight|HighlightText)|Color\.(White|Black|WhiteSmoke))",
            RegexOptions.Compiled);

        string[] pinnedDesignerColors = Directory
            .EnumerateFiles(formsRoot, "*.Designer.cs", SearchOption.AllDirectories)
            .Where(static path =>
            {
                string fileName = Path.GetFileName(path);
                return fileName.StartsWith("Add", StringComparison.Ordinal)
                    || fileName.StartsWith("Create", StringComparison.Ordinal)
                    || fileName.StartsWith("Select", StringComparison.Ordinal);
            })
            .SelectMany(path =>
            {
                string relativePath = Path.GetRelativePath(repoRoot, path);
                return File.ReadLines(path)
                    .Select((line, index) => new { line, index })
                    .Where(item => pinnedInputColorRegex.IsMatch(item.line))
                    .Select(item => $"{relativePath}:{item.index + 1}: {item.line.Trim()}");
            })
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        Assert.AreEqual(
            0,
            pinnedDesignerColors.Length,
            "Add/Select/Create dialogs must not pin white, black, or system input colors in designers. "
            + "Let ColorManager own textbox/combobox/readability state. Offenders: "
            + string.Join(", ", pinnedDesignerColors));
    }

    [TestMethod]
    public void Core_add_dialogs_keep_chummer5a_selection_contract_and_shared_theme()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string selectionRoot = Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms");
        string[] addMoreDialogs =
        [
            "SelectArmor",
            "SelectArmorMod",
            "SelectComplexForm",
            "SelectCyberware",
            "SelectDrug",
            "SelectGear",
            "SelectLifestyle",
            "SelectLifestyleQuality",
            "SelectMartialArt",
            "SelectPower",
            "SelectQuality",
            "SelectSpell",
            "SelectVehicle",
            "SelectVehicleMod",
            "SelectWeapon",
            "SelectWeaponAccessory",
        ];
        string[] doubleClickPickerDialogs =
        [
            "SelectArmor",
            "SelectArmorMod",
            "SelectComplexForm",
            "SelectCyberware",
            "SelectDrug",
            "SelectGear",
            "SelectLifestyleQuality",
            "SelectMartialArt",
            "SelectPower",
            "SelectQuality",
            "SelectSpell",
            "SelectVehicleMod",
            "SelectWeapon",
            "SelectWeaponAccessory",
        ];

        foreach (string dialog in addMoreDialogs)
        {
            string source = File.ReadAllText(Path.Combine(selectionRoot, dialog + ".cs"));
            string designer = File.ReadAllText(Path.Combine(selectionRoot, dialog + ".Designer.cs"));

            StringAssert.Contains(source, "this.UpdateLightDarkMode();", dialog + " must opt into shared light/dark styling.");
            StringAssert.Contains(source, "this.UpdateParentForToolTipControls();", dialog + " must keep Chummer5A tooltip parenting.");
            StringAssert.Contains(designer, "this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;", dialog + " must support normal Cancel/Escape behavior.");
            StringAssert.Contains(designer, "this.AcceptButton = this.cmdOK;", dialog + " must accept the current highlighted selection with Enter.");
            StringAssert.Contains(designer, "this.CancelButton = this.cmdCancel;", dialog + " must cancel with Escape.");
            StringAssert.Contains(designer, "this.cmdOKAdd.Tag = \"String_AddMore\";", dialog + " must expose the Chummer5A Add & More workflow.");
            StringAssert.Contains(designer, "this.cmdOKAdd.Text = \"&Add && More\";", dialog + " must keep the familiar Add & More button text.");
            StringAssert.Contains(designer, "this.cmdOKAdd.Click += new System.EventHandler(this.cmdOKAdd_Click);", dialog + " must wire Add & More explicitly.");
            Assert.IsTrue(
                source.Contains("public bool AddAgain", StringComparison.Ordinal)
                    || source.Contains("public bool AddAgain =>", StringComparison.Ordinal),
                dialog + " must expose whether Add & More was used.");
            if (doubleClickPickerDialogs.Contains(dialog, StringComparer.Ordinal))
            {
                Assert.IsTrue(
                    designer.Contains(".DoubleClick += new System.EventHandler(", StringComparison.Ordinal),
                    dialog + " must preserve Chummer5A double-click-to-add behavior.");
            }
        }

        string selectSkillSource = File.ReadAllText(Path.Combine(selectionRoot, "SelectSkill.cs"));
        string selectSkillDesigner = File.ReadAllText(Path.Combine(selectionRoot, "SelectSkill.Designer.cs"));
        StringAssert.Contains(selectSkillSource, "this.UpdateLightDarkMode();");
        StringAssert.Contains(selectSkillSource, "this.UpdateParentForToolTipControls();");
        StringAssert.Contains(selectSkillDesigner, "this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;");
        StringAssert.Contains(selectSkillDesigner, "this.AcceptButton = this.cmdOK;");
        StringAssert.Contains(selectSkillDesigner, "this.CancelButton = this.cmdCancel;");
    }

    [TestMethod]
    public void Legacy_system_color_seeds_in_add_dialogs_are_normalized_by_color_manager()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string colorManagerSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Backend", "Static", "Managers", "ColorManager.cs"));
        string selectionRoot = Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms");
        Regex legacySeedRegex = new(
            @"System\.Drawing\.SystemColors\.(Control|ControlLight|ControlText|Highlight|HighlightText|Window|WindowText)",
            RegexOptions.Compiled);

        string[] seededDialogDesigners = Directory
            .EnumerateFiles(selectionRoot, "Select*.Designer.cs", SearchOption.TopDirectoryOnly)
            .Where(path => legacySeedRegex.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetFileName(path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.IsSubsetOf(
            seededDialogDesigners,
            new[]
            {
                "SelectArmor.Designer.cs",
                "SelectVehicle.Designer.cs",
                "SelectWeapon.Designer.cs",
            },
            "Only the DataGrid/tabbed legacy add dialogs should seed system colors; ColorManager owns the final rendered colors.");
        StringAssert.Contains(colorManagerSource, "IsSameColor(objColor, SystemColors.Control)");
        StringAssert.Contains(colorManagerSource, "IsSameColor(objColor, SystemColors.ControlLight)");
        StringAssert.Contains(colorManagerSource, "IsSameColor(objColor, SystemColors.ControlText)");
        StringAssert.Contains(colorManagerSource, "IsSameColor(objColor, SystemColors.Highlight)");
        StringAssert.Contains(colorManagerSource, "IsSameColor(objColor, SystemColors.HighlightText)");
        StringAssert.Contains(colorManagerSource, "IsSameColor(objColor, SystemColors.Window)");
        StringAssert.Contains(colorManagerSource, "IsSameColor(objColor, SystemColors.WindowText)");
        StringAssert.Contains(colorManagerSource, "IsControlTextColor(x.ForeColor)");
        StringAssert.Contains(colorManagerSource, "IsControlSurfaceColor(x.BackColor)");
        StringAssert.Contains(colorManagerSource, "IsWindowSurfaceColor(x.BackColor)");
    }

    [TestMethod]
    public void Utility_dashboard_and_table_surfaces_apply_light_dark_mode_at_creation()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        (string Path, string[] Markers)[] themedSurfaces =
        [
            (Path.Combine("Chummer", "Forms", "Utility Forms", "About.cs"), ["this.UpdateLightDarkMode();"]),
            (Path.Combine("Chummer", "Forms", "DesktopInstallLinkingGateForm.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Forms", "Utility Forms", "TestDataEntries.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Forms", "Utility Forms", "InitiativeTracker.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Controls", "Dashboards", "ConditionMonitorUserControl.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Controls", "Dashboards", "InitiativeUserControl.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Controls", "Charts", "ExpenseChart.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Controls", "Shared", "ObservableCollectionDisplay.cs"), ["this.UpdateLightDarkMode();", "x.UpdateLightDarkMode();", "x.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Controls", "Shared", "BindingListDisplay.cs"), ["this.UpdateLightDarkMode();", "x.UpdateLightDarkMode();", "x.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Controls", "Table", "TableView.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Controls", "Table", "TableCell.cs"), ["this.UpdateLightDarkMode();"]),
            (Path.Combine("Chummer", "Controls", "Table", "TextTableCell.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Controls", "Table", "SpinnerTableCell.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Forms", "DummyForm.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Forms", "Chummy.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
            (Path.Combine("Chummer", "Forms", "Character Forms", "CharacterShared.cs"), ["this.UpdateLightDarkMode();", "this.UpdateParentForToolTipControls();"]),
        ];

        foreach ((string relativePath, string[] markers) in themedSurfaces)
        {
            string source = File.ReadAllText(Path.Combine(repoRoot, relativePath));
            foreach (string marker in markers)
            {
                StringAssert.Contains(source, marker, $"{relativePath} must apply the shared WinForms theme at construction.");
            }
        }
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
    public void Desktop_text_inputs_and_combo_boxes_have_explicit_shell_readability_states()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string appTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml"));
        string shellTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopShellTheme.cs"));

        Assert.AreEqual(
            1,
            Regex.Matches(appTheme, "<Style Selector=\"TextBox\">").Count,
            "TextBox must have one canonical base style so hover/focus fixes are not split across duplicate selectors.");
        StringAssert.Contains(shellTheme, "textBox.Classes.Add(\"shell-input\");");
        StringAssert.Contains(shellTheme, "comboBox.Classes.Add(\"shell-combo\");");
        StringAssert.Contains(shellTheme, "ResolveThemeBrush(\"ComboBoxBackground\", \"#FBFCFE\")");
        StringAssert.Contains(shellTheme, "ResolveThemeBrush(\"ComboBoxForeground\", \"#111827\")");
        StringAssert.Contains(shellTheme, "ResolveThemeBrush(\"ComboBoxBorderBrush\", \"#B5C0CF\")");

        foreach (string selector in new[]
                 {
                     "<Style Selector=\"TextBox.shell-input\">",
                     "<Style Selector=\"TextBox.shell-input:pointerover\">",
                     "<Style Selector=\"TextBox.shell-input:focus\">",
                     "<Style Selector=\"TextBox.shell-input:disabled\">",
                     "<Style Selector=\"ComboBox.shell-combo\">",
                     "<Style Selector=\"ComboBox.shell-combo:pointerover\">",
                     "<Style Selector=\"ComboBox.shell-combo:focus\">",
                     "<Style Selector=\"ComboBox.shell-combo:disabled\">",
                     "<Style Selector=\"ComboBox:pointerover /template/ TextBlock\">",
                     "<Style Selector=\"ComboBox:focus /template/ TextBlock\">",
                     "<Style Selector=\"ComboBox:disabled /template/ TextBlock\">",
                     "<Style Selector=\"TextBox:pointerover /template/ TextBlock\">",
                     "<Style Selector=\"TextBox:focus /template/ TextBlock\">",
                     "<Style Selector=\"TextBox:disabled /template/ TextBlock\">"
                 })
        {
            StringAssert.Contains(appTheme, selector);
        }

        foreach (string resource in new[]
                 {
                     "TextControlCaretBrush",
                     "TextControlSelectionForeground",
                     "TextControlBackgroundPointerOver",
                     "TextControlForegroundPointerOver",
                     "TextControlBorderBrushFocused",
                     "ComboBoxBackgroundPointerOver",
                     "ComboBoxForegroundPointerOver",
                     "ComboBoxBorderBrushPressed",
                     "ComboBoxDropDownGlyphForegroundPointerOver",
                     "ComboBoxItemBackgroundSelected",
                     "ComboBoxItemForegroundSelected"
                 })
        {
            Assert.AreEqual(
                2,
                Regex.Matches(appTheme, $"x:Key=\"{resource}\"").Count,
                $"{resource} must be defined for both light and dark dictionaries.");
        }

        StringAssert.Contains(appTheme, "<SolidColorBrush x:Key=\"ComboBoxItemBackgroundSelected\" Color=\"#2C5FB8\" />");
        StringAssert.Contains(appTheme, "<SolidColorBrush x:Key=\"ComboBoxItemForegroundSelected\" Color=\"#FFFFFF\" />");
        StringAssert.Contains(appTheme, "<SolidColorBrush x:Key=\"ComboBoxItemBackgroundSelected\" Color=\"#1D4ED8\" />");
        StringAssert.Contains(appTheme, "<SolidColorBrush x:Key=\"ComboBoxItemForegroundSelected\" Color=\"#F8FAFC\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"Background\" Value=\"{DynamicResource ComboBoxItemBackgroundSelected}\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"Foreground\" Value=\"{DynamicResource ComboBoxItemForegroundSelected}\" />");
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
    public void Avalonia_shell_brush_references_are_defined_in_both_theme_dictionaries()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string avaloniaRoot = Path.Combine(repoRoot, "Chummer.Avalonia");
        string appTheme = File.ReadAllText(Path.Combine(avaloniaRoot, "App.axaml"));
        Regex shellBrushRegex = new(@"ChummerShell[A-Za-z0-9]+Brush", RegexOptions.Compiled);
        Regex resourceKeyRegex = new(@"x:Key=""(?<key>ChummerShell[A-Za-z0-9]+Brush)""", RegexOptions.Compiled);

        HashSet<string> lightKeys = ExtractThemeKeys(appTheme, "Light", resourceKeyRegex);
        HashSet<string> darkKeys = ExtractThemeKeys(appTheme, "Dark", resourceKeyRegex);
        string[] lightOnly = lightKeys.Except(darkKeys, StringComparer.Ordinal).OrderBy(static key => key, StringComparer.Ordinal).ToArray();
        string[] darkOnly = darkKeys.Except(lightKeys, StringComparer.Ordinal).OrderBy(static key => key, StringComparer.Ordinal).ToArray();

        Assert.AreEqual(0, lightOnly.Length, "Light-only shell brushes must be mirrored in the dark dictionary: " + string.Join(", ", lightOnly));
        Assert.AreEqual(0, darkOnly.Length, "Dark-only shell brushes must be mirrored in the light dictionary: " + string.Join(", ", darkOnly));

        HashSet<string> definedKeys = lightKeys.Intersect(darkKeys, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        List<string> missingReferences = [];
        foreach (string path in Directory.EnumerateFiles(avaloniaRoot, "*.*", SearchOption.AllDirectories)
                     .Where(static path => path.EndsWith(".axaml", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal))
                     .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            string source = File.ReadAllText(path);
            foreach (string key in shellBrushRegex.Matches(source).Select(static match => match.Value).Distinct(StringComparer.Ordinal))
            {
                if (!definedKeys.Contains(key))
                {
                    missingReferences.Add($"{Path.GetRelativePath(repoRoot, path)} -> {key}");
                }
            }
        }

        string classicHost = File.ReadAllText(Path.Combine(avaloniaRoot, "Controls", "ClassicFormPortHostControl.axaml"));
        StringAssert.Contains(classicHost, "ChummerShellWindowBackgroundBrush");
        Assert.IsFalse(classicHost.Contains("ChummerShellPanelBackgroundBrush", StringComparison.Ordinal));
        Assert.AreEqual(
            0,
            missingReferences.Count,
            "Every ChummerShell brush reference must resolve in both theme dictionaries. Missing: "
            + string.Join(", ", missingReferences.OrderBy(static item => item, StringComparer.Ordinal)));
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
        string localizationSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopLocalizationCatalog.cs"));

        StringAssert.Contains(reportSource, "Content = DesktopShellTheme.CreateWindowSurface(");
        StringAssert.Contains(devicesSource, "Content = DesktopShellTheme.CreateWindowSurface(");
        StringAssert.Contains(reportSource, "CreateIntroText(S(\"desktop.report.intro\"))");
        StringAssert.Contains(reportSource, "CreateIntroText(S(\"desktop.report.private_split\"))");
        StringAssert.Contains(reportSource, "_statusText = new TextBlock");
        StringAssert.Contains(reportSource, "_contextText = new TextBlock");
        Assert.IsFalse(reportSource.Contains("Text = BuildContextBody(),\n            IsVisible = false", StringComparison.Ordinal));
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.bug.title_label\"), S(\"desktop.report.bug.title_watermark\"), _bugTitleBox)");
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.bug.expected_label\"), S(\"desktop.report.bug.expected_watermark\"), _bugExpectedBox)");
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.bug.actual_label\"), S(\"desktop.report.bug.actual_watermark\"), _bugActualBox)");
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.bug.repro_label\"), S(\"desktop.report.bug.repro_watermark\"), _bugReproStepsBox)");
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.bug.evidence_label\"), S(\"desktop.report.bug.evidence_watermark\"), _bugEvidenceBox)");
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.feedback.summary_label\"), S(\"desktop.report.feedback.summary_watermark\"), _feedbackSummaryBox)");
        StringAssert.Contains(reportSource, "CreateField(S(\"desktop.report.feedback.detail_label\"), S(\"desktop.report.feedback.detail_watermark\"), _feedbackDetailBox)");
        StringAssert.Contains(reportSource, "new TextBlock");
        StringAssert.Contains(reportSource, "Text = label");
        StringAssert.Contains(reportSource, "Text = hint");
        StringAssert.Contains(reportSource, "Classes = { \"shell-caption\" }");
        StringAssert.Contains(reportSource, "$\"{input.Name}Hint\"");
        StringAssert.Contains(reportSource, "AutomationProperties.SetName(hintBlock, $\"{label} hint\")");
        StringAssert.Contains(reportSource, "AutomationProperties.SetHelpText(hintBlock, hint)");
        StringAssert.Contains(reportSource, "Watermark = tooltip");
        StringAssert.Contains(reportSource, "Name = name");
        StringAssert.Contains(reportSource, "ReportBugTitleBox");
        StringAssert.Contains(reportSource, "ReportBugTitleBoxLabel");
        StringAssert.Contains(reportSource, "AutomationProperties.SetName(labelBlock, label)");
        StringAssert.Contains(reportSource, "AutomationProperties.SetHelpText(labelBlock, hint)");
        StringAssert.Contains(reportSource, "AutomationProperties.SetName(box, automationName)");
        StringAssert.Contains(reportSource, "AutomationProperties.SetHelpText(box, $\"{automationName}. {tooltip}\")");
        StringAssert.Contains(reportSource, "ToolTip.SetTip(box, null);");
        foreach (string labelKey in new[]
                 {
                     "desktop.report.bug.title_label",
                     "desktop.report.bug.expected_label",
                     "desktop.report.bug.actual_label",
                     "desktop.report.bug.repro_label",
                     "desktop.report.bug.evidence_label",
                     "desktop.report.feedback.summary_label",
                     "desktop.report.feedback.detail_label"
                 })
        {
            StringAssert.Contains(localizationSource, $"[\"{labelKey}\"] = ");
        }

        StringAssert.Contains(localizationSource, "localized[\"desktop.report.section.context\"] = \"Desktop-Kontext\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.report.section.bug\"] = \"Fehlerbericht\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.report.section.feedback\"] = \"Feedback\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.report.context.supportability\"] = \"Supportstatus: {0}\"");
        StringAssert.Contains(localizationSource, "[\"desktop.devices.button.reload\"] = \"Check status\"");
        StringAssert.Contains(localizationSource, "[\"desktop.devices.button.manage_linked_copies\"] = \"Manage linked copies\"");
        StringAssert.Contains(localizationSource, "[\"desktop.install_link.preference.visible_choice\"] = \"Show assistant features\"");
        StringAssert.Contains(localizationSource, "[\"desktop.install_link.preference.hidden_choice\"] = \"Hide assistant features\"");
        Assert.IsFalse(localizationSource.Contains("localized[\"desktop.report.context.supportability\"] = \"Supportability-Posture", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("scared caveman", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(localizationSource.Contains("localized[\"desktop.report.bug.intro\"] = \"Nutzen Sie diese Spur", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("localized[\"desktop.report.section.feedback\"] = \"Leichtgewichtiges Feedback\"", StringComparison.Ordinal));

        Assert.IsFalse(reportSource.Contains("Child = new StackPanel", StringComparison.Ordinal));
        Assert.IsFalse(devicesSource.Contains("Child = new StackPanel", StringComparison.Ordinal));
        Assert.IsFalse(reportSource.Contains("CreateField(S(\"desktop.report.bug.title_watermark\"", StringComparison.Ordinal));
        Assert.IsFalse(reportSource.Contains("CreateField(S(\"desktop.report.feedback.detail_watermark\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Support_and_update_windows_do_not_leak_internal_release_jargon()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string updateSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopUpdateWindow.cs"));
        string supportCaseSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopSupportCaseWindow.cs"));
        string combined = updateSource + "\n" + supportCaseSource;

        foreach (string forbidden in new[]
                 {
                     "release lane",
                     "support lane",
                     "proof posture",
                     "release truth",
                     "verification:",
                     "VerificationSummary: \"Use the signed-in support lane",
                 })
        {
            Assert.IsFalse(
                combined.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"Visible desktop copy must not contain '{forbidden}'.");
        }

        StringAssert.Contains(updateSource, "release path move this install forward");
        StringAssert.Contains(updateSource, "Update, release, or rollout status needs review");
        StringAssert.Contains(updateSource, "configured update path");
        StringAssert.Contains(supportCaseSource, "reporter-ready release path");
        StringAssert.Contains(supportCaseSource, "signed-in support");
        StringAssert.Contains(supportCaseSource, "Confirmation:");
    }

    [TestMethod]
    public void Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string factorySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopDialogFactory.cs"));
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
        StringAssert.Contains(originSurfaceSource, "OriginDossierStandaloneAdvancedStoryControlsExpander");
        StringAssert.Contains(originSurfaceSource, "Header = \"Advanced story controls\"");
        StringAssert.Contains(originSurfaceSource, "IsExpanded = false");
        StringAssert.Contains(originSurfaceSource, "Pick only the basics, then build the story. Advanced controls are optional.");
        StringAssert.Contains(desktopDialogSource, "\"Story Preview\"");
        StringAssert.Contains(desktopDialogSource, "\"Book Preview\"");
        StringAssert.Contains(desktopDialogSource, "CreateBookPreviewPanel(field.Value)");
        StringAssert.Contains(factorySource, "new DesktopDialogAction(\"generate_fitting_build\", \"Build story\", true)");
        StringAssert.Contains(factorySource, "newCharacterOriginBookPreview");
        StringAssert.Contains(factorySource, "VisualKind: DesktopDialogFieldVisualKinds.Book");
        StringAssert.Contains(factorySource, "new DesktopDialogAction(\"open_origin_guided_chargen\", \"Start character creation\", true)");
        Assert.IsFalse(originSurfaceSource.Contains("Color.Parse", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Brushes.White", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Brushes.Black", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("new SolidColorBrush", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Background = Brushes", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Foreground = Brushes", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("newCharacterOriginGmConstraintPreset\", \"GM Constraint\", \"none\", \"none\"", StringComparison.Ordinal));
        Assert.IsFalse(factorySource.Contains("Review story and build", StringComparison.Ordinal));
        Assert.IsFalse(factorySource.Contains("Open guided character creation", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("build lane", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(factorySource.Contains("matrix-first lane", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(factorySource.Contains("magic-forward lane", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(factorySource.Contains("current run lane", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(desktopDialogSource.Contains("\"ALICE Handoff\"", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("ALICE translates the story", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_explain_home_copy_uses_build_method_not_lane_jargon()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string projectorSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopHomeBuildExplainProjector.cs"));

        StringAssert.Contains(projectorSource, "buildMethodLabel");
        StringAssert.Contains(projectorSource, "inspect the current {buildMethodLabel} build");
        StringAssert.Contains(projectorSource, "Explain focus: {buildMethodLabel} build");
        StringAssert.Contains(projectorSource, "Campaign rules cap this build");
        StringAssert.Contains(projectorSource, "current story path");
        StringAssert.Contains(projectorSource, "for this workspace yet");
        Assert.IsFalse(projectorSource.Contains("buildLane", StringComparison.Ordinal));
        Assert.IsFalse(projectorSource.Contains("grounded {buildLane} lane", StringComparison.Ordinal));
        Assert.IsFalse(projectorSource.Contains("current dossier lane", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(projectorSource.Contains("desktop lane", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Alice_origin_dossier_keeps_story_and_book_before_media_generation()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string aliceSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));

        StringAssert.Contains(aliceSource, "CreateButton(\"Build story\", StartOriginDossierAsync, isPrimary: true, name: \"AliceOriginStartDossierButton\")");
        StringAssert.Contains(aliceSource, "CreateButton(\"Open story\", () => DesktopCrashRuntime.TryOpenPathInShell(_originDraftMarkdownPath), isPrimary: true, name: \"AliceOriginOpenDraftStoryButton\")");
        StringAssert.Contains(aliceSource, "use the story as Alice's seed for later build guidance.");

        int approveStart = aliceSource.IndexOf("Task ApproveOriginCanonAsync()", StringComparison.Ordinal);
        int renderPdfStart = aliceSource.IndexOf("Task RenderOriginDossierPdfAsync()", StringComparison.Ordinal);
        Assert.IsTrue(approveStart >= 0 && renderPdfStart > approveStart, "Approve-origin source must be discoverable.");
        string approveSource = aliceSource[approveStart..renderPdfStart];

        int openBookIndex = approveSource.IndexOf("\"Open book\"", StringComparison.Ordinal);
        int openStoryIndex = approveSource.IndexOf("\"Open story\"", StringComparison.Ordinal);
        int portraitIndex = approveSource.IndexOf("\"Create portraits\"", StringComparison.Ordinal);
        int voiceIndex = approveSource.IndexOf("\"Create audiobook script\"", StringComparison.Ordinal);
        int videoIndex = approveSource.IndexOf("\"Create dossier video\"", StringComparison.Ordinal);
        Assert.IsTrue(openBookIndex >= 0, "Approved Origin Dossier must expose the book action.");
        Assert.IsTrue(openStoryIndex > openBookIndex, "Story must stay adjacent to the book action.");
        Assert.IsTrue(portraitIndex > openStoryIndex, "Portraits should not outrank the story/book.");
        Assert.IsTrue(voiceIndex > portraitIndex, "Audiobook scripts should follow the story/book and portrait preparation.");
        Assert.IsTrue(videoIndex > voiceIndex, "Video should follow the book, story, and voice-script preparation.");

        int idleStart = aliceSource.IndexOf("void ApplyIdleState()", StringComparison.Ordinal);
        int showOriginBundleStateStart = aliceSource.IndexOf("void ShowOriginBundleState(", StringComparison.Ordinal);
        Assert.IsTrue(idleStart >= 0 && showOriginBundleStateStart > idleStart, "Idle-state source must be discoverable.");
        string idleSource = aliceSource[idleStart..showOriginBundleStateStart];
        int emptyOriginIndex = idleSource.IndexOf("CreateButton(\"Build story\", StartOriginDossierAsync", StringComparison.Ordinal);
        Assert.IsTrue(emptyOriginIndex >= 0, "Empty Origin Dossier state must start with Build story.");
        string emptyOriginSource = idleSource[emptyOriginIndex..];
        Assert.IsFalse(emptyOriginSource.Contains("\"Create dossier video\"", StringComparison.Ordinal));
        Assert.IsFalse(emptyOriginSource.Contains("\"Render audiobook now\"", StringComparison.Ordinal));
        Assert.IsFalse(emptyOriginSource.Contains("\"Create portraits\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Desktop_media_and_install_link_surfaces_use_named_theme_brushes_instead_of_raw_white_black()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string[] themedMediaSources =
        [
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopInstallLinkingWindow.cs"))
        ];

        foreach (string source in themedMediaSources)
        {
            StringAssert.Contains(source, "ChummerShellMediaOverlayForegroundBrush");
            Assert.IsFalse(source.Contains("Brushes.White", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Brushes.Black", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Background = Brushes", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Foreground = Brushes", StringComparison.Ordinal));
        }
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
        StringAssert.Contains(desktopDialogSource, "CreateRowLabel(\"Show Metatypes:\"");
        StringAssert.Contains(factorySource, "new DesktopDialogFieldOption(\"Standard\", \"Core choices\")");
        StringAssert.Contains(factorySource, "new DesktopDialogFieldOption(\"Metahuman\", \"Non-human choices\")");
        StringAssert.Contains(factorySource, "new DesktopDialogFieldOption(\"Show All\", \"All playable options\")");
        StringAssert.Contains(factorySource, "\"Remaining Karma | tracked when the character opens\"");
        StringAssert.Contains(factorySource, "\"start_from_origin\", \"Start Origin Dossier\"");
        Assert.IsFalse(newCharacterPaneSource.Contains("ExecuteCommandAsync(\"character_settings\"", StringComparison.Ordinal));
        Assert.IsFalse(factorySource.Contains("\"Metatype Category\"", StringComparison.Ordinal));
        Assert.IsFalse(factorySource.Contains("\"Metatype Filter\"", StringComparison.Ordinal));
        Assert.IsFalse(factorySource.Contains("legacy metatype continuation", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Attribute_editor_keeps_clear_column_headers_visible()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string sectionHostMarkup = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml"));

        StringAssert.Contains(sectionHostMarkup, "Text=\"Attribute\" Classes=\"shell-section-title\"");
        StringAssert.Contains(sectionHostMarkup, "Text=\"Start\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "Text=\"Add\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "Text=\"Total\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "Text=\"Limits\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "ColumnDefinitions=\"*,128,128,72,120\"");
        Assert.IsFalse(sectionHostMarkup.Contains("Text=\"Base\" Classes=\"shell-caption\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostMarkup.Contains("Text=\"Karma bump\" Classes=\"shell-caption\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostMarkup.Contains("Text=\"Val (Aug)\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostMarkup.Contains("Text=\"Points\" Classes=\"shell-caption\" HorizontalAlignment=\"Right\" IsVisible=\"False\"", StringComparison.Ordinal));

        string sectionHostSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml.cs"));
        StringAssert.Contains(sectionHostSource, "CreateAttributeValueStepper(");
        StringAssert.Contains(sectionHostSource, "$\"AttributeBaseEditor_{ShortAttributeLabel(row.AttributeName)}\"");
        StringAssert.Contains(sectionHostSource, "$\"AttributeKarmaEditor_{ShortAttributeLabel(row.AttributeName)}\"");
        StringAssert.Contains(sectionHostSource, "$\"{row.DisplayName} starting value\"");
        StringAssert.Contains(sectionHostSource, "$\"{row.DisplayName} added value\"");
        StringAssert.Contains(sectionHostSource, "static next => next.ToString(CultureInfo.InvariantCulture)");
        StringAssert.Contains(sectionHostSource, "AutomationProperties.SetName(stepper, accessibleName)");
        StringAssert.Contains(sectionHostSource, "ColumnDefinitions = new ColumnDefinitions(\"*,128,128,72,120\")");
        StringAssert.Contains(sectionHostSource, "ColumnDefinitions = new ColumnDefinitions(\"28,18,*,18,28\")");
        StringAssert.Contains(sectionHostSource, "MinWidth = 72");
        StringAssert.Contains(sectionHostSource, "Margin = new Thickness(14d, 0d)");
        StringAssert.Contains(sectionHostSource, "Width = 24");
        Assert.IsFalse(sectionHostSource.Contains("$\"{row.DisplayName} base allocation\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("$\"{row.DisplayName} karma adjustment\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("$\"Base {next}\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("\"Karma 0\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("$\"Karma +{next}\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("Text = label", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("\"B\"", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("\"K\"", StringComparison.Ordinal));
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
    public void ComboBoxes_textboxes_and_numeric_inputs_keep_readable_non_hover_colors_in_kde_dark_mode()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string appTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml"));
        string shellTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopShellTheme.cs"));
        string classicPortSurface = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "ClassicFormPortSurfaceControl.cs"));
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string[] fluentControlResourceKeys =
        [
            "TextControlBackground",
            "TextControlBackgroundPointerOver",
            "TextControlBackgroundFocused",
            "TextControlBackgroundDisabled",
            "TextControlForeground",
            "TextControlForegroundPointerOver",
            "TextControlForegroundFocused",
            "TextControlForegroundDisabled",
            "TextControlCaretBrush",
            "TextControlSelectionForeground",
            "TextControlBorderBrush",
            "TextControlBorderBrushPointerOver",
            "TextControlBorderBrushFocused",
            "TextControlBorderBrushDisabled",
            "TextControlPlaceholderForeground",
            "TextControlPlaceholderForegroundPointerOver",
            "TextControlPlaceholderForegroundFocused",
            "TextControlPlaceholderForegroundDisabled",
            "ComboBoxBackground",
            "ComboBoxBackgroundPointerOver",
            "ComboBoxBackgroundPressed",
            "ComboBoxBackgroundDisabled",
            "ComboBoxForeground",
            "ComboBoxForegroundPointerOver",
            "ComboBoxForegroundPressed",
            "ComboBoxForegroundDisabled",
            "ComboBoxDropDownGlyphForeground",
            "ComboBoxDropDownGlyphForegroundPointerOver",
            "ComboBoxDropDownGlyphForegroundPressed",
            "ComboBoxDropDownGlyphForegroundDisabled",
            "ComboBoxBorderBrush",
            "ComboBoxBorderBrushPointerOver",
            "ComboBoxBorderBrushPressed",
            "ComboBoxBorderBrushDisabled",
            "ComboBoxDropDownBackground",
            "ComboBoxDropDownBorderBrush",
            "ComboBoxItemBackground",
            "ComboBoxItemBackgroundPointerOver",
            "ComboBoxItemBackgroundPressed",
            "ComboBoxItemBackgroundSelected",
            "ComboBoxItemBackgroundDisabled",
            "ComboBoxItemForeground",
            "ComboBoxItemForegroundPointerOver",
            "ComboBoxItemForegroundPressed",
            "ComboBoxItemForegroundSelected",
            "ComboBoxItemForegroundDisabled",
            "FlyoutPresenterBackground",
            "FlyoutPresenterForeground",
            "FlyoutPresenterBorderBrush",
            "MenuFlyoutPresenterBackground",
            "MenuFlyoutPresenterForeground",
            "MenuFlyoutPresenterBorderBrush",
            "MenuItemBackground",
            "MenuItemBackgroundPointerOver",
            "MenuItemBackgroundSelected",
            "MenuItemForeground",
            "MenuItemForegroundPointerOver",
            "MenuItemForegroundSelected",
            "MenuItemForegroundDisabled"
        ];

        foreach (string resourceKey in fluentControlResourceKeys)
        {
            Assert.AreEqual(
                2,
                Regex.Matches(appTheme, $"x:Key=\"{Regex.Escape(resourceKey)}\"").Count,
                $"{resourceKey} must be defined once for Light and once for Dark so Fluent template parts cannot inherit OS colors.");
        }

        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover\">");
        StringAssert.Contains(appTheme, "<Setter Property=\"Background\" Value=\"{DynamicResource ComboBoxItemBackgroundSelected}\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"Foreground\" Value=\"{DynamicResource ComboBoxItemForegroundSelected}\" />");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:pointerover /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected:pointerover /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected TextBlock.shell-option-label\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover TextBlock.shell-option-label\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected TextBlock.shell-option-meta\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover TextBlock.shell-option-meta\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox /template/ TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pointerover /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pointerover /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pointerover /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:focus /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:focus /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:focus /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:disabled /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:disabled /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:disabled /template/ Border\">");
        StringAssert.Contains(appTheme, "<Setter Property=\"TextElement.Foreground\" Value=\"{DynamicResource ComboBoxForeground}\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"Background\" Value=\"{DynamicResource ComboBoxBackground}\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"BorderBrush\" Value=\"{DynamicResource ComboBoxBorderBrush}\" />");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:focus\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBox\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected:pointerover TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected:pointerover TextBlock.shell-option-label\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected:pointerover TextBlock.shell-option-meta\">");
        StringAssert.Contains(appTheme, "<Setter Property=\"Background\" Value=\"{DynamicResource TextControlBackground}\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"Foreground\" Value=\"{DynamicResource TextControlForeground}\" />");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox /template/ TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:pointerover /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:pointerover /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:focus /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:focus /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:disabled /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:disabled /template/ Border\">");
        StringAssert.Contains(appTheme, "<Setter Property=\"TextElement.Foreground\" Value=\"{DynamicResource TextControlForeground}\" />");
        StringAssert.Contains(appTheme, "<Style Selector=\"FlyoutPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"MenuFlyoutPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ContextMenu\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"MenuItem:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"MenuItem:selected\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"MenuItem TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"MenuItem:pointerover TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"MenuItem:selected TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"MenuItem.menu-root.active-menu TextBlock\">");
        StringAssert.Contains(shellTheme, "textBox.Background = ResolveThemeBrush(\"TextControlBackground\", \"#FFFFFF\");");
        StringAssert.Contains(shellTheme, "textBox.Foreground = ResolveThemeBrush(\"TextControlForeground\", \"#111111\");");
        StringAssert.Contains(classicPortSurface, "DesktopShellTheme.ApplyShellComboBoxTheme(comboBox);");
        StringAssert.Contains(shellTheme, "ApplyShellListBoxTheme(ListBox listBox)");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:focus\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown /template/ TextBox\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:pointerover /template/ TextBox\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:focus /template/ TextBox\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:disabled /template/ TextBox\">");
        StringAssert.Contains(shellTheme, "numericUpDown.Background = ResolveThemeBrush(\"TextControlBackground\", \"#FFFFFF\");");
        StringAssert.Contains(shellTheme, "numericUpDown.Foreground = ResolveThemeBrush(\"TextControlForeground\", \"#111111\");");
        StringAssert.Contains(shellTheme, "numericUpDown.BorderBrush = ResolveThemeBrush(\"TextControlBorderBrush\", \"#B5C0CF\");");
        AssertSelectorAfter(
            appTheme,
            "<Style Selector=\"ListBoxItem:pointerover TextBlock.shell-option-label\">",
            "<Style Selector=\"ListBoxItem:selected:pointerover TextBlock.shell-option-label\">",
            "Selected list rows must keep selected foreground even when hovered.");
        AssertSelectorAfter(
            appTheme,
            "<Style Selector=\"ListBoxItem:pointerover TextBlock.shell-option-meta\">",
            "<Style Selector=\"ListBoxItem:selected:pointerover TextBlock.shell-option-meta\">",
            "Selected list metadata must keep selected foreground even when hovered.");
        AssertSelectorAfter(
            appTheme,
            "<Style Selector=\"ComboBoxItem:pointerover TextBlock.shell-option-label\">",
            "<Style Selector=\"ComboBoxItem:selected:pointerover TextBlock.shell-option-label\">",
            "Selected combo rows must keep selected foreground even when hovered.");
        AssertSelectorAfter(
            appTheme,
            "<Style Selector=\"ComboBoxItem:pointerover TextBlock.shell-option-meta\">",
            "<Style Selector=\"ComboBoxItem:selected:pointerover TextBlock.shell-option-meta\">",
            "Selected combo metadata must keep selected foreground even when hovered.");
        StringAssert.Contains(shellTheme, "ApplyShellNumericUpDownTheme(NumericUpDown numericUpDown)");
        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ApplyShellNumericUpDownTheme(numericUpDown);");

        Assert.IsFalse(appTheme.Contains(
            "<Style Selector=\"ComboBoxItem:selected TextBlock\">\n      <Setter Property=\"Foreground\" Value=\"{DynamicResource ChummerShellSelectionForegroundBrush}\" />",
            StringComparison.Ordinal));
        Assert.IsFalse(appTheme.Contains(
            "<Style Selector=\"ComboBoxItem:selected TextBlock.shell-option-label\">\n      <Setter Property=\"Foreground\" Value=\"{DynamicResource ChummerShellSelectionForegroundBrush}\" />",
            StringComparison.Ordinal));
        Assert.IsFalse(appTheme.Contains(
            "<Style Selector=\"ComboBoxItem:selected TextBlock.shell-option-meta\">\n      <Setter Property=\"Foreground\" Value=\"{DynamicResource ChummerShellSelectionForegroundBrush}\" />",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void Avalonia_shell_control_resource_pairs_have_readable_contrast()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string appTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml"));
        (string Foreground, string Background, double MinimumRatio)[] requiredPairs =
        [
            ("TextControlForeground", "TextControlBackground", 4.5d),
            ("TextControlForegroundPointerOver", "TextControlBackgroundPointerOver", 4.5d),
            ("TextControlForegroundFocused", "TextControlBackgroundFocused", 4.5d),
            ("TextControlForegroundDisabled", "TextControlBackgroundDisabled", 4.5d),
            ("TextControlPlaceholderForeground", "TextControlBackground", 4.5d),
            ("TextControlPlaceholderForegroundPointerOver", "TextControlBackgroundPointerOver", 4.5d),
            ("TextControlPlaceholderForegroundFocused", "TextControlBackgroundFocused", 4.5d),
            ("TextControlPlaceholderForegroundDisabled", "TextControlBackgroundDisabled", 4.5d),
            ("TextControlSelectionForeground", "ChummerShellSelectionBrush", 4.5d),
            ("ComboBoxForeground", "ComboBoxBackground", 4.5d),
            ("ComboBoxForegroundPointerOver", "ComboBoxBackgroundPointerOver", 4.5d),
            ("ComboBoxForegroundPressed", "ComboBoxBackgroundPressed", 4.5d),
            ("ComboBoxForegroundDisabled", "ComboBoxBackgroundDisabled", 4.5d),
            ("ComboBoxItemForeground", "ComboBoxItemBackground", 4.5d),
            ("ComboBoxItemForegroundPointerOver", "ComboBoxItemBackgroundPointerOver", 4.5d),
            ("ComboBoxItemForegroundPressed", "ComboBoxItemBackgroundPressed", 4.5d),
            ("ComboBoxItemForegroundSelected", "ComboBoxItemBackgroundSelected", 4.5d),
            ("ComboBoxItemForegroundDisabled", "ComboBoxItemBackgroundDisabled", 4.5d),
            ("FlyoutPresenterForeground", "FlyoutPresenterBackground", 4.5d),
            ("MenuFlyoutPresenterForeground", "MenuFlyoutPresenterBackground", 4.5d),
            ("MenuItemForeground", "MenuItemBackground", 4.5d),
            ("MenuItemForegroundPointerOver", "MenuItemBackgroundPointerOver", 4.5d),
            ("MenuItemForegroundSelected", "MenuItemBackgroundSelected", 4.5d),
            ("MenuItemForegroundDisabled", "MenuItemBackground", 4.5d),
            ("ChummerShellForegroundBrush", "ChummerShellWindowBackgroundBrush", 4.5d),
            ("ChummerShellForegroundBrush", "ChummerShellSurfaceBrush", 4.5d),
            ("ChummerShellForegroundBrush", "ChummerShellSurfaceAltBrush", 4.5d),
            ("ChummerShellMutedForegroundBrush", "ChummerShellWindowBackgroundBrush", 4.5d),
            ("ChummerShellTextMutedBrush", "ChummerShellSurfaceBrush", 4.5d),
            ("ChummerShellSelectionForegroundBrush", "ChummerShellSelectionBrush", 4.5d),
            ("ChummerShellAccentButtonForegroundBrush", "ChummerShellAccentButtonBrush", 4.5d)
        ];

        foreach (string themeName in new[] { "Light", "Dark" })
        {
            Dictionary<string, string> themeColors = ExtractThemeColors(appTheme, themeName);
            foreach ((string foregroundKey, string backgroundKey, double minimumRatio) in requiredPairs)
            {
                Assert.IsTrue(themeColors.TryGetValue(foregroundKey, out string? foreground), $"{themeName} theme is missing {foregroundKey}.");
                Assert.IsTrue(themeColors.TryGetValue(backgroundKey, out string? background), $"{themeName} theme is missing {backgroundKey}.");

                double contrastRatio = GetContrastRatio(foreground, background);
                Assert.IsTrue(
                    contrastRatio >= minimumRatio,
                    $"{themeName} {foregroundKey}/{backgroundKey} contrast is {contrastRatio:0.00}; expected at least {minimumRatio:0.0}. "
                    + $"Foreground {foreground}, background {background}.");
            }
        }
    }

    private static void AssertSelectorAfter(string source, string earlierSelector, string laterSelector, string message)
    {
        int earlierIndex = source.IndexOf(earlierSelector, StringComparison.Ordinal);
        int laterIndex = source.IndexOf(laterSelector, StringComparison.Ordinal);

        Assert.IsTrue(earlierIndex >= 0, $"Missing selector: {earlierSelector}");
        Assert.IsTrue(laterIndex >= 0, $"Missing selector: {laterSelector}");
        Assert.IsTrue(laterIndex > earlierIndex, message);
    }

    [TestMethod]
    public void Desktop_lists_comboboxes_textboxes_and_numeric_inputs_are_explicitly_bound_to_shell_theme_helpers()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string avaloniaRoot = Path.Combine(repoRoot, "Chummer.Avalonia");
        Regex typedCreation = new(@"(?<type>ListBox|ComboBox|TextBox|NumericUpDown)\s+(?<name>[_A-Za-z][_A-Za-z0-9]*)\s*=\s*new(?:\s+(ListBox|ComboBox|TextBox|NumericUpDown))?\b", RegexOptions.Compiled);
        Regex assignedCreation = new(@"(?<name>[_A-Za-z][_A-Za-z0-9]*)\s*=\s*new\s+(?<type>ListBox|ComboBox|TextBox|NumericUpDown)\b", RegexOptions.Compiled);
        List<string> unthemedControls = [];

        foreach (string path in Directory.EnumerateFiles(avaloniaRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(static path => !path.EndsWith("DesktopShellTheme.cs", StringComparison.Ordinal)))
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                foreach ((string Type, string Name) creation in FindControlCreations(lines[index], typedCreation, assignedCreation))
                {
                    string lookahead = string.Join('\n', lines.Skip(index).Take(45));
                    bool themed = string.Equals(creation.Type, "ComboBox", StringComparison.Ordinal)
                        ? lookahead.Contains($"ApplyShellComboBoxTheme({creation.Name}", StringComparison.Ordinal)
                        : string.Equals(creation.Type, "ListBox", StringComparison.Ordinal)
                            ? lookahead.Contains($"ApplyShellListBoxTheme({creation.Name}", StringComparison.Ordinal)
                        : string.Equals(creation.Type, "NumericUpDown", StringComparison.Ordinal)
                            ? lookahead.Contains($"ApplyShellNumericUpDownTheme({creation.Name}", StringComparison.Ordinal)
                            : lookahead.Contains($"ApplyShellTextInputTheme({creation.Name}", StringComparison.Ordinal)
                              || lookahead.Contains($"ApplyTextBoxAccessibility({creation.Name}", StringComparison.Ordinal);

                    if (!themed)
                    {
                        string relativePath = Path.GetRelativePath(repoRoot, path);
                        unthemedControls.Add($"{relativePath}:{index + 1} {creation.Type} {creation.Name}");
                    }
                }
            }
        }

        Assert.AreEqual(
            0,
            unthemedControls.Count,
            "Every Avalonia desktop ListBox/ComboBox/TextBox/NumericUpDown must opt into the shell theme helper near creation so KDE/dark-mode cannot produce white-on-white controls. Missing: "
            + string.Join(", ", unthemedControls));
    }

    [TestMethod]
    public void Selection_add_surfaces_do_not_label_readonly_context_as_navigation()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string factorySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopDialogFactory.cs"));
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        string commandDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CommandDialogPaneControl.axaml.cs"));

        StringAssert.Contains(factorySource, "id.EndsWith(\"CategoryTree\", StringComparison.Ordinal)");
        StringAssert.Contains(factorySource, "? \"Categories\"");
        StringAssert.Contains(desktopDialogSource, "ResolveSelectionNavigationTitle(navigationField)");
        StringAssert.Contains(desktopDialogSource, "private static string ResolveSelectionNavigationTitle(DesktopDialogField field)");
        StringAssert.Contains(commandDialogSource, "ResolveSelectionNavigationTitle(navigationField)");
        StringAssert.Contains(commandDialogSource, "private static string ResolveSelectionNavigationTitle(DialogFieldDisplayItem field)");
        StringAssert.Contains(desktopDialogSource, "? \"Categories\"");
        StringAssert.Contains(desktopDialogSource, ": \"Current selection\"");
        StringAssert.Contains(commandDialogSource, "? \"Categories\"");
        StringAssert.Contains(commandDialogSource, ": \"Current selection\"");
        StringAssert.Contains(desktopDialogSource, "Cursor = categoryFieldId is null ? null : new Cursor(StandardCursorType.Hand)");
        StringAssert.Contains(commandDialogSource, "using Avalonia.Input;");
        StringAssert.Contains(commandDialogSource, "Cursor = categoryFieldId is null ? null : new Cursor(StandardCursorType.Hand)");
        Assert.IsFalse(factorySource.Contains("BuildSelectionTreeField(\"uiSkillCategoryTree\", \"Navigation\"", StringComparison.Ordinal));
        Assert.IsFalse(desktopDialogSource.Contains("CreateSelectionSurfaceCard(navigationField.Label", StringComparison.Ordinal));
        Assert.IsFalse(commandDialogSource.Contains("CreateSelectionSurfaceCard(navigationField.Label", StringComparison.Ordinal));
    }

    private static IEnumerable<(string Type, string Name)> FindControlCreations(
        string line,
        Regex typedCreation,
        Regex assignedCreation)
    {
        foreach (Match match in typedCreation.Matches(line))
        {
            yield return (match.Groups["type"].Value, match.Groups["name"].Value);
        }

        foreach (Match match in assignedCreation.Matches(line))
        {
            yield return (match.Groups["type"].Value, match.Groups["name"].Value);
        }
    }

    private static Dictionary<string, string> ExtractThemeColors(string appTheme, string themeName)
    {
        Regex resourceColorRegex = new(@"x:Key=""(?<key>[^""]+)""\s+Color=""(?<color>#[0-9A-Fa-f]{6,8})""", RegexOptions.Compiled);
        string marker = $"<ResourceDictionary x:Key=\"{themeName}\">";
        int start = appTheme.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"{themeName} theme dictionary must exist.");
        int end = appTheme.IndexOf("</ResourceDictionary>", start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, $"{themeName} theme dictionary must close.");
        string themeSource = appTheme[start..end];
        return resourceColorRegex.Matches(themeSource)
            .ToDictionary(
                static match => match.Groups["key"].Value,
                static match => match.Groups["color"].Value,
                StringComparer.Ordinal);
    }

    private static double GetContrastRatio(string foregroundHex, string backgroundHex)
    {
        double foregroundLuminance = GetRelativeLuminance(foregroundHex);
        double backgroundLuminance = GetRelativeLuminance(backgroundHex);
        double lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        double darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double GetRelativeLuminance(string hexColor)
    {
        (int red, int green, int blue) = ParseRgb(hexColor);
        return 0.2126d * ConvertChannel(red)
               + 0.7152d * ConvertChannel(green)
               + 0.0722d * ConvertChannel(blue);

        static double ConvertChannel(int channel)
        {
            double normalized = channel / 255.0d;
            return normalized <= 0.03928d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }
    }

    private static (int Red, int Green, int Blue) ParseRgb(string hexColor)
    {
        string normalized = hexColor.TrimStart('#');
        if (normalized.Length == 8)
        {
            normalized = normalized[2..];
        }

        Assert.AreEqual(6, normalized.Length, $"Expected #RRGGBB or #AARRGGBB color, got {hexColor}.");
        return (
            Convert.ToInt32(normalized[0..2], 16),
            Convert.ToInt32(normalized[2..4], 16),
            Convert.ToInt32(normalized[4..6], 16));
    }

    private static HashSet<string> ExtractThemeKeys(string appTheme, string themeName, Regex resourceKeyRegex)
    {
        string marker = $"<ResourceDictionary x:Key=\"{themeName}\">";
        int start = appTheme.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"{themeName} theme dictionary must exist.");
        int end = appTheme.IndexOf("</ResourceDictionary>", start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, $"{themeName} theme dictionary must close.");
        string themeSource = appTheme[start..end];
        return resourceKeyRegex.Matches(themeSource)
            .Select(static match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }
}
