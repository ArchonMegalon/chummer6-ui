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
    public void Builder_entry_dialogs_use_clearer_ruleset_and_filter_copy()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string selectBuildMethodSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectBuildMethod.cs"));
        string selectBuildMethodDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectBuildMethod.Designer.cs"));
        string selectMetatypeKarmaSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypeKarma.cs"));
        string selectMetatypeKarmaDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypeKarma.Designer.cs"));
        string selectMetatypePrioritySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypePriority.cs"));
        string selectMetatypePriorityDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectMetatypePriority.Designer.cs"));
        string selectQualitySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectQuality.cs"));
        string selectQualityDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectQuality.Designer.cs"));

        StringAssert.Contains(selectBuildMethodDesigner, "this.Text = \"Choose Character Ruleset\";");
        StringAssert.Contains(selectBuildMethodDesigner, "this.lblCharacterSetting.Text = \"Ruleset:\";");
        StringAssert.Contains(selectBuildMethodDesigner, "this.cmdEditCharacterSetting.Text = \"Edit...\";");
        StringAssert.Contains(selectBuildMethodDesigner, "this.lblBuildMethodLabel.Text = \"Creation:\";");
        StringAssert.Contains(selectMetatypeKarmaSource, "new ListItem(\"Show All\", \"All Metatypes\")");
        StringAssert.Contains(selectMetatypeKarmaSource, "x.Enabled = lstCategories.Count > 1;");

        StringAssert.Contains(selectMetatypePrioritySource, "x.Enabled = lstCategory.Count > 1;");

        StringAssert.Contains(selectQualitySource, "new ListItem(\"Show All\", \"All Qualities\")");
        StringAssert.Contains(selectQualityDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectQualityDesigner, "this.chkMetagenic.Text = \"Only Metagenic\";");
        StringAssert.Contains(selectQualityDesigner, "this.chkLimitList.Text = \"Only Available\";");
        StringAssert.Contains(selectMetatypeKarmaDesigner, "this.lblQualitiesLabel.Text = \"Included Qualities:\";");
        StringAssert.Contains(selectMetatypePriorityDesigner, "this.lblMetavariantQualitiesLabel.Text = \"Included Qualities:\";");
    }

    [TestMethod]
    public void Major_add_dialogs_use_filter_copy_and_disable_dead_category_filters()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string selectArmorSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectArmor.cs"));
        string selectArmorDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectArmor.Designer.cs"));
        string selectGearSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectGear.cs"));
        string selectGearDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectGear.Designer.cs"));
        string selectWeaponSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectWeapon.cs"));
        string selectWeaponDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectWeapon.Designer.cs"));
        string selectCyberwareSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectCyberware.cs"));
        string selectCyberwareDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectCyberware.Designer.cs"));
        string selectVehicleSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectVehicle.cs"));
        string selectVehicleDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectVehicle.Designer.cs"));
        string selectSpellSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectSpell.cs"));
        string selectSpellDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectSpell.Designer.cs"));

        StringAssert.Contains(selectArmorDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectArmorDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectArmorDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectArmorDesigner, "this.chkShowOnlyAffordItems.Text = \"Only Affordable\";");
        StringAssert.Contains(selectArmorDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectArmorDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(selectArmorDesigner, "this.chkFreeItem.Text = \"Free\";");
        StringAssert.Contains(selectArmorSource, "new ListItem(\"Show All\", \"All Armor\")");
        StringAssert.Contains(selectArmorSource, "x.Enabled = _lstCategory.Count > 1;");

        StringAssert.Contains(selectGearDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectGearDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectGearDesigner, "this.chkDoItYourself.Text = \"Self-Made\";");
        StringAssert.Contains(selectGearDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectGearDesigner, "this.chkShowOnlyAffordItems.Text = \"Only Affordable\";");
        StringAssert.Contains(selectGearDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectGearDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(selectGearDesigner, "this.chkFreeItem.Text = \"Free\";");
        StringAssert.Contains(selectGearSource, "new ListItem(\"Show All\", \"All Gear\")");
        StringAssert.Contains(selectGearSource, "x.Enabled = _lstCategory.Count > 1;");

        StringAssert.Contains(selectWeaponDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectWeaponDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectWeaponDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectWeaponDesigner, "this.chkShowOnlyAffordItems.Text = \"Only Affordable\";");
        StringAssert.Contains(selectWeaponDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectWeaponDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(selectWeaponDesigner, "this.chkFreeItem.Text = \"Free\";");
        StringAssert.Contains(selectWeaponSource, "new ListItem(\"Show All\", \"All Weapons\")");
        StringAssert.Contains(selectWeaponSource, "x.Enabled = _lstCategory.Count > 1;");

        StringAssert.Contains(selectCyberwareDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectCyberwareDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectCyberwareDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectCyberwareDesigner, "this.chkHideBannedGrades.Text = \"Hide Banned Grades\";");
        StringAssert.Contains(selectCyberwareDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectCyberwareDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(selectCyberwareDesigner, "this.chkFree.Text = \"Free\";");
        StringAssert.Contains(selectCyberwareSource, "new ListItem(\"Show All\", \"All Cyberware\")");
        StringAssert.Contains(selectCyberwareSource, "x.Enabled = lstCategory.Count > 1;");

        StringAssert.Contains(selectVehicleDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectVehicleDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectVehicleDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectVehicleDesigner, "this.chkShowOnlyAffordItems.Text = \"Only Affordable\";");
        StringAssert.Contains(selectVehicleDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectVehicleDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(selectVehicleDesigner, "this.chkFreeItem.Text = \"Free\";");
        StringAssert.Contains(selectVehicleSource, "new ListItem(\"Show All\", \"All Vehicles\")");
        StringAssert.Contains(selectVehicleSource, "x.Enabled = _lstCategory.Count > 1;");

        StringAssert.Contains(selectSpellDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectSpellDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectSpellDesigner, "this.chkFreeBonus.Text = \"Free\";");
        StringAssert.Contains(selectSpellSource, "new ListItem(\"Show All\", \"All Spells\")");
        StringAssert.Contains(selectSpellSource, "x.Enabled = _lstCategory.Count > 1;");
    }

    [TestMethod]
    public void Secondary_add_dialogs_use_filter_copy_and_all_state_labels()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string selectAiProgramSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectAIProgram.cs"));
        string selectAiProgramDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectAIProgram.Designer.cs"));
        string selectLifestyleQualitySource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectLifestyleQuality.cs"));
        string selectLifestyleQualityDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectLifestyleQuality.Designer.cs"));
        string selectPacksKitSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectPACKSKit.cs"));
        string selectPacksKitDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectPACKSKit.Designer.cs"));
        string selectCritterPowerSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectCritterPower.cs"));
        string selectCritterPowerDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectCritterPower.Designer.cs"));

        StringAssert.Contains(selectAiProgramDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectAiProgramDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectAiProgramSource, "new ListItem(\"Show All\", \"All Programs\")");
        StringAssert.Contains(selectAiProgramSource, "x.Enabled = _lstCategory.Count > 1;");

        StringAssert.Contains(selectLifestyleQualityDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectLifestyleQualityDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectLifestyleQualityDesigner, "this.chkLimitList.Text = \"Only Available\";");
        StringAssert.Contains(selectLifestyleQualityDesigner, "this.chkFree.Text = \"Free\";");
        StringAssert.Contains(selectLifestyleQualitySource, "new ListItem(\"Show All\", \"All Lifestyle Qualities\")");

        StringAssert.Contains(selectPacksKitDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectPacksKitDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectPacksKitSource, "new ListItem(\"Show All\", \"All Kits\")");
        StringAssert.Contains(selectPacksKitSource, "x.Enabled = _lstCategory.Count > 1;");

        StringAssert.Contains(selectCritterPowerDesigner, "this.lblCategory.Text = \"Filter:\";");
        StringAssert.Contains(selectCritterPowerDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectCritterPowerSource, "new ListItem(\"Show All\", \"All Powers\")");
        StringAssert.Contains(selectCritterPowerSource, "x.Enabled = _lstCategory.Count > 1;");
    }

    [TestMethod]
    public void Mod_and_drug_selection_dialogs_use_compact_selector_copy()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string selectArmorModDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectArmorMod.Designer.cs"));
        string selectVehicleModSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectVehicleMod.cs"));
        string selectVehicleModDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectVehicleMod.Designer.cs"));
        string selectDrugDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectDrug.Designer.cs"));

        StringAssert.Contains(selectArmorModDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectArmorModDesigner, "this.chkFreeItem.Text = \"Free\";");
        StringAssert.Contains(selectArmorModDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(selectArmorModDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectArmorModDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectArmorModDesigner, "this.chkShowOnlyAffordItems.Text = \"Only Affordable\";");

        StringAssert.Contains(selectVehicleModDesigner, "this.lblCategoryLabel.Text = \"Filter:\";");
        StringAssert.Contains(selectVehicleModDesigner, "this.label1.Text = \"Filter:\";");
        StringAssert.Contains(selectVehicleModDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectVehicleModDesigner, "this.chkFreeItem.Text = \"Free\";");
        StringAssert.Contains(selectVehicleModDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(selectVehicleModDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectVehicleModDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectVehicleModDesigner, "this.chkShowOnlyAffordItems.Text = \"Only Affordable\";");
        StringAssert.Contains(selectVehicleModSource, "new ListItem(\"Show All\", \"All Vehicle Mods\")");
        StringAssert.Contains(selectVehicleModSource, "x.Enabled = _lstCategory.Count > 1;");

        StringAssert.Contains(selectDrugDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectDrugDesigner, "this.chkHideBannedGrades.Text = \"Hide Banned Grades\";");
        StringAssert.Contains(selectDrugDesigner, "this.chkShowOnlyAffordItems.Text = \"Only Affordable\";");
        StringAssert.Contains(selectDrugDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectDrugDesigner, "this.chkFree.Text = \"Free\";");
        StringAssert.Contains(selectDrugDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectDrugDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
    }

    [TestMethod]
    public void Remaining_selector_dialogs_use_compact_add_more_copy()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string selectWeaponAccessoryDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectWeaponAccessory.Designer.cs"));
        string selectQualityDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectQuality.Designer.cs"));
        string selectPowerDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectPower.Designer.cs"));
        string selectMartialArtDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectMartialArt.Designer.cs"));
        string selectMartialArtTechniqueDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectMartialArtTechnique.Designer.cs"));
        string selectLifestyleDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectLifestyle.Designer.cs"));
        string selectComplexFormDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectComplexForm.Designer.cs"));
        string selectProgramOptionDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Selection Forms", "SelectProgramOption.Designer.cs"));

        StringAssert.Contains(selectWeaponAccessoryDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectWeaponAccessoryDesigner, "this.chkFreeItem.Text = \"Free\";");
        StringAssert.Contains(selectWeaponAccessoryDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(selectWeaponAccessoryDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectWeaponAccessoryDesigner, "this.chkHideOverAvailLimit.Text = \"Hide Over Avail ({0})\";");
        StringAssert.Contains(selectWeaponAccessoryDesigner, "this.chkShowOnlyAffordItems.Text = \"Only Affordable\";");

        StringAssert.Contains(selectQualityDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectQualityDesigner, "this.chkFree.Text = \"Free\";");
        StringAssert.Contains(selectPowerDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectMartialArtDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectMartialArtTechniqueDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectLifestyleDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectComplexFormDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(selectProgramOptionDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
    }

    [TestMethod]
    public void Adjacent_creation_surfaces_use_the_same_compact_copy()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string createImprovementDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Creation Forms", "CreateImprovement.Designer.cs"));
        string createWeaponMountDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Creation Forms", "CreateWeaponMount.Designer.cs"));
        string selectLifeModuleDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Creation Forms", "SelectLifeModule.Designer.cs"));
        string characterCreateDesigner = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Forms", "CharacterCreate.Designer.cs"));

        StringAssert.Contains(createImprovementDesigner, "this.chkFree.Text = \"Free\";");
        StringAssert.Contains(createWeaponMountDesigner, "this.chkFreeItem.Text = \"Free\";");
        StringAssert.Contains(createWeaponMountDesigner, "this.lblMarkupLabel.Text = \"Price Adj.:\";");
        StringAssert.Contains(createWeaponMountDesigner, "this.chkBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(selectLifeModuleDesigner, "this.cmdOKAdd.Text = \"&Add && More\";");
        StringAssert.Contains(characterCreateDesigner, "this.chkCyberwareBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(characterCreateDesigner, "this.chkGearBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(characterCreateDesigner, "this.chkArmorBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(characterCreateDesigner, "this.chkWeaponBlackMarketDiscount.Text = \"Market -10%\";");
        StringAssert.Contains(characterCreateDesigner, "this.chkVehicleBlackMarketDiscount.Text = \"Market -10%\";");
    }

    [TestMethod]
    public void Attribute_control_keeps_compact_numeric_rows_without_cumulative_margin_drift()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Controls", "Attributes", "AttributeControl.cs"));

        StringAssert.Contains(source, "private readonly Padding _nudKarmaBaseMargin;");
        StringAssert.Contains(source, "private readonly Padding _nudBaseBaseMargin;");
        StringAssert.Contains(source, "_nudKarmaBaseMargin = nudKarma.Margin;");
        StringAssert.Contains(source, "_nudBaseBaseMargin = nudBase.Margin;");
        StringAssert.Contains(source, "RowCount = 1");
        StringAssert.Contains(source, "tlpValues.Controls.Add(nudBase, 0, 0);");
        StringAssert.Contains(source, "tlpValues.Controls.Add(nudKarma, 1, 0);");
        StringAssert.Contains(source, "_nudBaseBaseMargin.Left + Math.Max(intNudBaseWidth - x.Width, 0)");
        StringAssert.Contains(source, "_nudKarmaBaseMargin.Left + Math.Max(intNudKarmaWidth - x.Width, 0)");
        Assert.IsFalse(source.Contains("tlpValues.Controls.Add(lblBase", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("tlpValues.Controls.Add(lblKarma", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("x.Margin.Right + Math.Max(intNudBaseWidth - x.Width, 0)", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("x.Margin.Right + Math.Max(intNudKarmaWidth - x.Width, 0)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ColorManager_themes_numeric_up_down_inputs_and_their_embedded_edit_fields()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Backend", "Static", "Managers", "ColorManager.cs"));

        StringAssert.Contains(source, "private static void ApplyNumericUpDownColors(NumericUpDown objControl, bool blnLightMode)");
        StringAssert.Contains(source, "case NumericUpDown nudControl:");
        StringAssert.Contains(source, "ApplyNumericUpDownColors(x, blnLightMode)");
        StringAssert.Contains(source, "objControl.ReadOnly && IsControlSurfaceColor(objControl.BackColor)");
        StringAssert.Contains(source, "foreach (Control objChild in objControl.Controls)");
        StringAssert.Contains(source, "if (objChild is TextBoxBase objTextBox)");
        StringAssert.Contains(source, "objTextBox.ForeColor = objForeColor;");
        StringAssert.Contains(source, "objTextBox.BackColor = objBackColor;");
    }

    [TestMethod]
    public void ColorManager_themes_textboxbase_and_date_picker_inputs_in_shared_dark_mode_path()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Backend", "Static", "Managers", "ColorManager.cs"));

        StringAssert.Contains(source, "case TextBoxBase txtControl when txtControl is not RichTextBox:");
        StringAssert.Contains(source, "case DateTimePicker dtpControl:");
        StringAssert.Contains(source, "x.CalendarForeColor = WindowTextLight;");
        StringAssert.Contains(source, "x.CalendarMonthBackground = WindowLight;");
        StringAssert.Contains(source, "x.CalendarForeColor = WindowTextDark;");
        StringAssert.Contains(source, "x.CalendarMonthBackground = WindowDark;");
        StringAssert.Contains(source, "x.CalendarTitleForeColor = HighlightText;");
        StringAssert.Contains(source, "x.CalendarTitleBackColor = Highlight;");
        StringAssert.Contains(source, "x.CalendarTrailingForeColor = GrayText;");
    }

    [TestMethod]
    public void Character_career_splitter_does_not_seed_legacy_light_blue_chrome()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string designer = File.ReadAllText(Path.Combine(repoRoot, "Chummer", "Forms", "Character Forms", "CharacterCareer.Designer.cs"));

        StringAssert.Contains(designer, "this.splitKarmaNuyen.BackColor = System.Drawing.SystemColors.InactiveCaption;");
        Assert.IsFalse(designer.Contains("this.splitKarmaNuyen.BackColor = System.Drawing.Color.LightBlue;", StringComparison.Ordinal));
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
        StringAssert.Contains(scaffoldSource, "DesktopShellTheme.ResolveWindowBackgroundBrush()");
        StringAssert.Contains(scaffoldSource, "DesktopShellTheme.ResolveSurfaceAltBrush()");
        StringAssert.Contains(scaffoldSource, "DesktopShellTheme.ResolveBorderBrush()");
        StringAssert.Contains(scaffoldSource, "DesktopShellTheme.ResolveChromeAccentBrush()");
        StringAssert.Contains(scaffoldSource, "DesktopShellTheme.ResolveInfoBrush()");
        StringAssert.Contains(scaffoldSource, "DesktopShellTheme.ApplyPrimaryButton(button);");
        Assert.IsFalse(scaffoldSource.Contains("#F2F5FA", StringComparison.Ordinal));
        Assert.IsFalse(scaffoldSource.Contains("#DEE8F6", StringComparison.Ordinal));
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

        StringAssert.Contains(trustPanelSource, "DesktopShellTheme.ResolveSelectionPanelBrush()");
        StringAssert.Contains(trustPanelSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellWarningBrush\", \"#9A6700\")");
        StringAssert.Contains(explainLauncherSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellWarningBrush\", \"#9A6700\")");
        StringAssert.Contains(explainLauncherSource, "DesktopShellTheme.ResolveSelectionInsetBrush()");
        StringAssert.Contains(sectionHostSource, "DesktopShellTheme.ResolveThemeBrush(\"ChummerShellWarningBrush\", \"#9A6700\")");
        StringAssert.Contains(sectionHostSource, "DesktopShellTheme.ResolveForegroundBrush()");
        StringAssert.Contains(sectionHostSource, "DesktopShellTheme.ResolveSurfaceBrush()");
        StringAssert.Contains(sectionHostSource, "DesktopShellTheme.ResolveBorderBrush()");
        StringAssert.Contains(classicSurfaceSource, "DesktopShellTheme.ResolveSurfaceBrush()");
        StringAssert.Contains(classicSurfaceSource, "DesktopShellTheme.ResolveBorderBrush()");
        Assert.IsFalse(trustPanelSource.Contains("#FFF6E1", StringComparison.Ordinal));
        Assert.IsFalse(trustPanelSource.Contains("#D9B05F", StringComparison.Ordinal));
        Assert.IsFalse(explainLauncherSource.Contains("#D9B05F", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("#4F3C16", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("#FFF7F4EB", StringComparison.Ordinal));
        Assert.IsFalse(sectionHostSource.Contains("#B5C0CF", StringComparison.Ordinal));
        Assert.IsFalse(classicSurfaceSource.Contains("#3f4b53", StringComparison.Ordinal));
        Assert.IsFalse(classicSurfaceSource.Contains("#475569", StringComparison.Ordinal));
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

        StringAssert.Contains(aliceSource, "DesktopShellTheme.ResolveBorderBrush()");
        StringAssert.Contains(runbookPressSource, "DesktopShellTheme.ResolveBorderBrush()");
        StringAssert.Contains(creatorOsSource, "DesktopShellTheme.ResolveBorderBrush()");
        StringAssert.Contains(nexusPanSource, "DesktopShellTheme.ResolveBorderBrush()");
        StringAssert.Contains(runsiteSource, "DesktopShellTheme.ResolveBorderBrush()");
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

        StringAssert.Contains(commandDialogSource, "Background = DesktopShellTheme.ResolveSelectionToolbarBrush(),");
        StringAssert.Contains(commandDialogSource, "Background = DesktopShellTheme.ResolveSelectionPanelBrush(),");
        StringAssert.Contains(commandDialogSource, "BorderBrush = DesktopShellTheme.ResolveBorderBrush(),");
        StringAssert.Contains(desktopDialogSource, "Background = DesktopShellTheme.ResolveSelectionToolbarBrush(),");
        StringAssert.Contains(desktopDialogSource, "Background = DesktopShellTheme.ResolveSelectionPanelBrush(),");
        StringAssert.Contains(desktopDialogSource, "BorderBrush = DesktopShellTheme.ResolveBorderBrush(),");
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

        StringAssert.Contains(commandDialogSource, "DesktopShellTheme.ResolveBorderBrush()");
        StringAssert.Contains(commandDialogSource, "DesktopShellTheme.ResolveSurfaceBrush()");
        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ResolveBorderBrush()");
        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ResolveSurfaceBrush()");
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
        StringAssert.Contains(scaffoldSource, "Background = DesktopShellTheme.ResolveWindowBackgroundBrush(),");
        StringAssert.Contains(localCoProcessorSource, "DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);");
        StringAssert.Contains(runnerPassportSource, "DesktopShellTheme.ApplyShellComboBoxTheme(detailModeCombo);");
        StringAssert.Contains(versionHistorySource, "DesktopShellTheme.ApplyShellTextInputTheme(historyBox);");
        StringAssert.Contains(desktopDialogSource, "ApplyShellListBoxTheme(listBox);");
        StringAssert.Contains(commandDialogSource, "DesktopShellTheme.ApplyShellListBoxTheme(listBox);");
        StringAssert.Contains(sectionHost, "Background=\"{DynamicResource ChummerShellSurfaceBrush}\"");
        StringAssert.Contains(classicPortSurface, "Background = DesktopShellTheme.ResolveSurfaceBrush(),");
        StringAssert.Contains(classicPortSurface, "DesktopShellTheme.ApplyShellListBoxTheme(listBox);");
        StringAssert.Contains(classicPortSurface, "DesktopShellTheme.ApplyShellTreeViewTheme(treeView);");
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
        StringAssert.Contains(shellTheme, "ClearInputBrushes(textBox);");
        StringAssert.Contains(shellTheme, "ClearTemplatedBrushes(comboBox);");
        StringAssert.Contains(shellTheme, "ApplyTextControlResourceOverrides(textBox);");
        StringAssert.Contains(shellTheme, "ApplyComboBoxResourceOverrides(comboBox);");
        StringAssert.Contains(shellTheme, "ApplySelectableResourceOverrides(listBox);");
        StringAssert.Contains(shellTheme, "ApplySelectableResourceOverrides(treeView);");
        StringAssert.Contains(shellTheme, "ApplyInputBrushes(textBox);");
        StringAssert.Contains(shellTheme, "ApplyComboBoxBrushes(comboBox);");
        StringAssert.Contains(shellTheme, "ApplyInputBrushes(numericUpDown);");
        StringAssert.Contains(shellTheme, "ApplyListBrushes(listBox);");
        StringAssert.Contains(shellTheme, "ApplyListBrushes(treeView);");
        StringAssert.Contains(shellTheme, "control.Background = ResolveThemeBrush(\"ChummerShellInputBackgroundBrush\", \"#162031\");");
        StringAssert.Contains(shellTheme, "control.Foreground = ResolveThemeBrush(\"ChummerShellInputForegroundBrush\", \"#F8FAFC\");");
        Assert.IsFalse(shellTheme.Contains("control.Background = ResolveThemeBrush(\"ComboBoxBackground\"", StringComparison.Ordinal));
        Assert.IsFalse(shellTheme.Contains("control.Foreground = ResolveThemeBrush(\"ComboBoxForeground\"", StringComparison.Ordinal));
        StringAssert.Contains(shellTheme, "control.Background = ResolveThemeBrush(\"ChummerShellSurfaceBrush\", \"#111827\");");
        StringAssert.Contains(shellTheme, "control.Foreground = ResolveThemeBrush(\"ChummerShellForegroundBrush\", \"#E5E7EB\");");
        StringAssert.Contains(shellTheme, "textBox.CaretBrush = ResolveThemeBrush(\"ChummerShellInputForegroundBrush\", \"#F8FAFC\");");
        StringAssert.Contains(shellTheme, "textBox.SelectionForegroundBrush = ResolveThemeBrush(\"ChummerShellSelectionForegroundBrush\", \"#F8FAFC\");");
        StringAssert.Contains(shellTheme, "control.Resources[resourceKey] = ResolveThemeBrush(themeResourceKey, fallbackHex);");
        StringAssert.Contains(shellTheme, "SetLocalBrushResource(control, \"ComboBoxItemBackground\", \"ChummerShellSurfaceBrush\", \"#111827\");");
        StringAssert.Contains(shellTheme, "SetLocalBrushResource(control, \"ComboBoxItemForeground\", \"ChummerShellForegroundBrush\", \"#E5E7EB\");");
        StringAssert.Contains(shellTheme, "SetLocalBrushResource(control, \"TextControlBackground\", \"ChummerShellInputBackgroundBrush\", \"#162031\");");
        StringAssert.Contains(shellTheme, "SetLocalBrushResource(control, \"TextControlForeground\", \"ChummerShellInputForegroundBrush\", \"#F8FAFC\");");
        StringAssert.Contains(shellTheme, "SetLocalBrushResource(control, \"ChummerShellSelectionForegroundBrush\", \"ChummerShellSelectionForegroundBrush\", \"#F8FAFC\");");

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
                     "<Style Selector=\"ComboBox ContentPresenter\">",
                     "<Style Selector=\"ComboBox:pointerover ContentPresenter\">",
                     "<Style Selector=\"ComboBox:focus ContentPresenter\">",
                     "<Style Selector=\"ComboBox:disabled ContentPresenter\">",
                     "<Style Selector=\"ComboBoxItem ContentPresenter\">",
                     "<Style Selector=\"ComboBoxItem /template/ Border\">",
                     "<Style Selector=\"ComboBoxItem:pointerover /template/ Border\">",
                     "<Style Selector=\"ComboBoxItem:pressed /template/ Border\">",
                     "<Style Selector=\"ComboBoxItem:selected /template/ Border\">",
                     "<Style Selector=\"ComboBoxItem:selected:pointerover /template/ Border\">",
                     "<Style Selector=\"ComboBoxItem:disabled /template/ Border\">",
                     "<Style Selector=\"ComboBoxItem:pointerover ContentPresenter\">",
                     "<Style Selector=\"ComboBoxItem:selected ContentPresenter\">",
                     "<Style Selector=\"ComboBoxItem:selected:pointerover ContentPresenter\">",
                     "<Style Selector=\"ComboBoxItem:disabled\">",
                     "<Style Selector=\"ComboBoxItem:disabled ContentPresenter\">",
                     "<Style Selector=\"ListBoxItem /template/ Border\">",
                     "<Style Selector=\"ListBoxItem:pointerover /template/ Border\">",
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
    public void Avalonia_shell_uses_dark_baseline_until_theme_switching_is_owned_end_to_end()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string appTheme = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml"));

        StringAssert.Contains(appTheme, "RequestedThemeVariant=\"Dark\"");
        Assert.IsFalse(
            appTheme.Contains("RequestedThemeVariant=\"Light\"", StringComparison.Ordinal),
            "The app must not force a light shell on dark-mode operating systems.");
        Assert.IsFalse(
            appTheme.Contains("RequestedThemeVariant=\"Default\"", StringComparison.Ordinal),
            "Default can mix OS dark-mode text with Chummer light backgrounds on Linux desktops.");
        StringAssert.Contains(appTheme, "<ResourceDictionary x:Key=\"Light\">");
        StringAssert.Contains(appTheme, "<ResourceDictionary x:Key=\"Dark\">");
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

        StringAssert.Contains(horizonsSource, "Content = DesktopShellTheme.CreateWindowSurface(");
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
        StringAssert.Contains(localizationSource, "[\"desktop.devices.button.reload\"] = \"Refresh account state\"");
        StringAssert.Contains(localizationSource, "[\"desktop.devices.status.refresh_failed\"] = \"Could not refresh account state. The last loaded state is still shown.\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.devices.button.reload\"] = \"Kontostand aktualisieren\"");
        StringAssert.Contains(localizationSource, "[\"desktop.devices.button.manage_linked_copies\"] = \"Manage linked copies\"");
        StringAssert.Contains(localizationSource, "[\"desktop.install_link.preference.visible_choice\"] = \"Show Alice and Origin Dossier\"");
        StringAssert.Contains(localizationSource, "[\"desktop.install_link.preference.hidden_choice\"] = \"Hide guided story tools\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.install_link.preference.visible_choice\"] = \"Alice und Origin Dossier anzeigen\"");
        StringAssert.Contains(localizationSource, "[\"desktop.install_link.button.open_origin_dossier\"] = \"Open clean Origin Dossier route\"");
        Assert.IsFalse(localizationSource.Contains("[\"desktop.install_link.button.open_origin_dossier\"] = \"Open Origin Dossier\"", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("scared caveman", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(localizationSource.Contains("Höhlenmensch", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(localizationSource.Contains("localized[\"desktop.report.context.supportability\"] = \"Supportability-Posture", StringComparison.Ordinal));
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
                     "follow-through",
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

        StringAssert.Contains(updateSource, "This copy is not attached to a working update source yet.");
        StringAssert.Contains(updateSource, "Something about updates needs review before you treat this copy as current.");
        StringAssert.Contains(updateSource, "private static IBrush WindowBackgroundBrush => DesktopShellTheme.ResolveWindowBackgroundBrush();");
        StringAssert.Contains(updateSource, "private static IBrush SurfaceBrush => DesktopShellTheme.ResolveSurfaceBrush();");
        StringAssert.Contains(updateSource, "private static IBrush ForegroundBrush => DesktopShellTheme.ResolveForegroundBrush();");
        Assert.IsFalse(
            updateSource.Contains("private static readonly IBrush", StringComparison.Ordinal),
            "The update status window must not freeze a private brush palette outside the shell theme.");
        Assert.IsFalse(
            updateSource.Contains("Brush(\"#", StringComparison.Ordinal),
            "The update status window should use shared shell theme tokens with fallbacks, not local fixed brushes.");
        StringAssert.Contains(supportCaseSource, "Tracked case preview.");
        StringAssert.Contains(supportCaseSource, "Use account support to record final confirmation");
        StringAssert.Contains(supportCaseSource, "System details stay visible while this case still needs attention.");
    }

    [TestMethod]
    public void Localized_update_strings_do_not_use_release_posture_or_truth_jargon()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string localizationSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopLocalizationCatalog.cs"));

        StringAssert.Contains(localizationSource, "localized[\"desktop.update.section.current\"] = \"Version actuelle\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.update.intro.never_checked\"] = \"Aucun etat de mise a jour n'a encore ete charge pour cette installation.\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.update.section.current\"] = \"現在のバージョン\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.update.intro.current\"] = \"このインストールはそのまま使い続けられます。\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.update.section.current\"] = \"Versao atual\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.update.intro.never_checked\"] = \"Nenhum status de atualizacao foi carregado para esta instalacao.\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.update.section.current\"] = \"当前版本\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.update.intro.current\"] = \"此安装可以继续使用。\"");

        Assert.IsFalse(localizationSource.Contains("Posture de version actuelle", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("La verite locale de mise a jour", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("更新ステータスとリリース姿勢", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("現在のリリース姿勢", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("ローカル更新トゥルース", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("Status da atualizacao e postura de release", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("Postura da versão atual", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("A verdade local de atualizacao", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("更新状态与发布姿态", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("当前发布姿态", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("本地更新真相", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("注册表支持的发布姿态", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Campaign_world_and_publication_desktop_surfaces_do_not_use_internal_follow_through_copy()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string[] sources =
        [
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopBlackLedgerWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignArtifactWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorPublicationWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopGhostwireWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopJackpointWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopNexusPanWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopOrganizerOperationsWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunbookPressWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunnerPassportWindow.cs")),
            File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopTablePulseWindow.cs"))
        ];

        string combined = string.Join("\n", sources);
        Assert.IsFalse(combined.Contains("follow-through", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("account-bound", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("device/access", StringComparison.OrdinalIgnoreCase));
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
        StringAssert.Contains(originSurfaceSource, "Foreground = DesktopShellTheme.ResolveForegroundBrush()");
        StringAssert.Contains(desktopDialogSource, "newCharacterOriginGmConstraintPreset");
        StringAssert.Contains(desktopDialogSource, "private const string OriginWizardAdvancedStoryControlsExpanderName = \"OriginDossierStandaloneAdvancedStoryControlsExpander\";");
        StringAssert.Contains(desktopDialogSource, "string.Equals(dialogId, \"dialog.new_character.origin_wizard\", StringComparison.Ordinal)");
        StringAssert.Contains(desktopDialogSource, "string.Equals(dialogId, \"dialog.new_character.origin_build\", StringComparison.Ordinal)");
        StringAssert.Contains(originSurfaceSource, "Name = OriginWizardAdvancedStoryControlsExpanderName");
        StringAssert.Contains(originSurfaceSource, "Header = \"Advanced story controls\"");
        StringAssert.Contains(originSurfaceSource, "IsExpanded = IsOriginWizardAdvancedStoryControlsEffectivelyExpanded()");
        StringAssert.Contains(originSurfaceSource, "Optional dossier identity, life-path steering, and GM guidance for the story packet.");
        StringAssert.Contains(originSurfaceSource, "CreateLegacyFieldGroup(" + Environment.NewLine + "                        \"Dossier\",");
        Assert.IsFalse(originSurfaceSource.Contains("CreateLegacyFieldGroup(" + Environment.NewLine + "                        \"Runner\",", StringComparison.Ordinal));
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayNameField = NormalizeOriginIdentityFieldForDisplay(nameField);");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayAliasField = NormalizeOriginIdentityFieldForDisplay(aliasField);");
        StringAssert.Contains(originSurfaceSource, "IReadOnlyList<DesktopDialogField> displayFields = DesktopDialogFactory.NormalizeOriginWizardFieldsForDisplay(fields);");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displaySummaryField = FindRequiredField(displayFields, \"newCharacterOriginSummary\");");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayMetatypeField = FindRequiredField(displayFields, \"newCharacterOriginMetatype\");");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayArchetypeField = FindRequiredField(displayFields, \"newCharacterOriginArchetype\");");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayQualityFocusField = FindRequiredField(displayFields, \"newCharacterOriginQualityFocus\");");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayPathSummaryField = FindRequiredField(displayFields, \"newCharacterOriginPathSummary\");");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayGmSummaryField = FindRequiredField(displayFields, \"newCharacterOriginGmRequirementSummary\");");
        StringAssert.Contains(originSurfaceSource, "CreateSplitFieldRow(displayNameField, displayAliasField)");
        Assert.IsFalse(originSurfaceSource.Contains("CreateSplitFieldRow(nameField, aliasField)", StringComparison.Ordinal));
        StringAssert.Contains(originSurfaceSource, "(\"Metatype\", displayMetatypeField.Value)");
        StringAssert.Contains(originSurfaceSource, "(\"Archetype\", displayArchetypeField.Value)");
        StringAssert.Contains(originSurfaceSource, "(\"Path\", displayPathSummaryField.Value)");
        Assert.IsFalse(originSurfaceSource.Contains("(\"Archetype\", archetypeField.Value)", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("(\"Path\", pathSummaryField.Value)", StringComparison.Ordinal));
        StringAssert.Contains(originSurfaceSource, "CreateOriginSummaryStrip((\"Applied GM Constraint\", displayGmSummaryField.Value), (\"Pressure\", displayQualityFocusField.Value))");
        Assert.IsFalse(originSurfaceSource.Contains("CreateOriginSummaryStrip((\"Applied GM Constraint\", gmSummaryField.Value), (\"Pressure\", qualityFocusField.Value))", StringComparison.Ordinal));
        StringAssert.Contains(originSurfaceSource, "CreateNarrativePanel(displaySummaryField.Value, minHeight: 120, maxHeight: 240)");
        Assert.IsFalse(originSurfaceSource.Contains("CreateNarrativePanel(summaryField.Value, minHeight: 120, maxHeight: 240)", StringComparison.Ordinal));
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayBookField = NormalizeOriginFieldForDisplay(fields, bookField);");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayStoryField = NormalizeOriginFieldForDisplay(fields, storyField);");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayImplicationsField = NormalizeOriginFieldForDisplay(fields, implicationsField);");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayDossierLinkField = NormalizeOriginFieldForDisplay(fields, dossierLinkField);");
        StringAssert.Contains(originSurfaceSource, "DesktopDialogField displayDossierLinkNotesField = NormalizeOriginFieldForDisplay(fields, dossierLinkNotesField);");
        StringAssert.Contains(originSurfaceSource, "CreateStandaloneFieldRow(displayDossierLinkField)");
        Assert.IsFalse(originSurfaceSource.Contains("CreateStandaloneFieldRow(dossierLinkField)", StringComparison.Ordinal));
        StringAssert.Contains(originSurfaceSource, "CreateFieldControl(displayDossierLinkNotesField)");
        StringAssert.Contains(originSurfaceSource, "CreateFieldControl(displayBookField)");
        StringAssert.Contains(originSurfaceSource, "CreateNarrativePanel(displayStoryField.Value, minHeight: 144, maxHeight: 260)");
        StringAssert.Contains(originSurfaceSource, "CreateFieldControl(displayImplicationsField)");
        Assert.IsFalse(originSurfaceSource.Contains("CreateFieldControl(dossierLinkNotesField)", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("CreateFieldControl(bookField)", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("CreateNarrativePanel(storyField.Value, minHeight: 144, maxHeight: 260)", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("CreateFieldControl(implicationsField)", StringComparison.Ordinal));
        StringAssert.Contains(originSurfaceSource, "(\"Ruleset\", GetOriginRulesetLabelForDisplay(fields))");
        StringAssert.Contains(originSurfaceSource, "(\"Method\", GetOriginBuildMethodForDisplay(fields))");
        Assert.IsFalse(originSurfaceSource.Contains("(\"Ruleset\", rulesetField.Value.ToUpperInvariant())", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("(\"Method\", methodField.Value)", StringComparison.Ordinal));
        StringAssert.Contains(originSurfaceSource, "NormalizeOriginIdentityValueForDisplay(\"newCharacterWorkflowAlias\", aliasField.Value)");
        Assert.IsFalse(originSurfaceSource.Contains("(\"Dossier\", aliasField.Value)", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Optional identity and rules context for the story packet.", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Optional life-module-style steering: where the runner came from, what broke, how they trained, and what still costs them.", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Optional table permissions or requirements. These guide the story and build handoff; they do not edit a sheet by themselves.", StringComparison.Ordinal));
        StringAssert.Contains(desktopDialogSource, "private bool IsOriginWizardAdvancedStoryControlsEffectivelyExpanded()");
        StringAssert.Contains(originSurfaceSource, "private static DesktopDialogField NormalizeOriginIdentityFieldForDisplay(DesktopDialogField field)");
        StringAssert.Contains(originSurfaceSource, "private static DesktopDialogField NormalizeOriginFieldForDisplay(IReadOnlyList<DesktopDialogField> fields, DesktopDialogField field)");
        StringAssert.Contains(originSurfaceSource, "private static string NormalizeOriginIdentityValueForDisplay(string fieldId, string? value)");
        StringAssert.Contains(originSurfaceSource, "private static string BuildOriginDossierDisplayRoute(IReadOnlyList<DesktopDialogField> fields)");
        StringAssert.Contains(originSurfaceSource, "private static string BuildOriginBookPreviewDisplayValue(IReadOnlyList<DesktopDialogField> fields, string? value)");
        StringAssert.Contains(originSurfaceSource, "private static string BuildOriginBookPreviewFallbackValue(IReadOnlyList<DesktopDialogField> fields, string title)");
        StringAssert.Contains(originSurfaceSource, "private static string BuildOriginStoryDisplayValue(IReadOnlyList<DesktopDialogField> fields, string? value)");
        StringAssert.Contains(originSurfaceSource, "private static string BuildOriginImplicationsDisplayValue(IReadOnlyList<DesktopDialogField> fields, string? value)");
        StringAssert.Contains(originSurfaceSource, "private static string GetOriginRulesetLabelForDisplay(IReadOnlyList<DesktopDialogField> fields)");
        StringAssert.Contains(originSurfaceSource, "private static string GetOriginBuildMethodForDisplay(IReadOnlyList<DesktopDialogField> fields)");
        StringAssert.Contains(originSurfaceSource, "private static string? GetOriginDossierRouteQueryValue(string? route, string queryKey)");
        StringAssert.Contains(originSurfaceSource, "private static string? GetStructuredDisplayLineValue(string? value, string label)");
        StringAssert.Contains(originSurfaceSource, "GetStructuredDisplayLineValue(GetFieldValue(fields, \"newCharacterOriginImplications\"), \"Build\")");
        StringAssert.Contains(originSurfaceSource, "GetStructuredDisplayLineValue(GetFieldValue(fields, \"newCharacterOriginImplications\"), \"GM Requirements\")");
        StringAssert.Contains(desktopDialogSource, "_suppressOriginWizardAdvancedStoryControlsCollapseDuringComboRefresh");
        StringAssert.Contains(desktopDialogSource, "_originWizardTransientRefreshPending");
        StringAssert.Contains(originSurfaceSource, "Pick only the basics, then build the story. Advanced controls are optional.");
        StringAssert.Contains(desktopDialogSource, "\"Story Preview\"");
        StringAssert.Contains(desktopDialogSource, "\"Book Preview\"");
        StringAssert.Contains(desktopDialogSource, "CreateBookPreviewPanel(field.Value)");
        StringAssert.Contains(originSurfaceSource, "Use this clean route to reopen Origin Dossier without publishing the story text.");
        StringAssert.Contains(factorySource, "new DesktopDialogAction(\"generate_fitting_build\", \"Draft story\", true)");
        StringAssert.Contains(factorySource, "internal static DesktopDialogState NormalizeOriginWizardDialogForDisplay(DesktopDialogState dialog)");
        StringAssert.Contains(factorySource, "internal static IReadOnlyList<DesktopDialogField> NormalizeOriginWizardFieldsForDisplay(IReadOnlyList<DesktopDialogField> fields)");
        StringAssert.Contains(factorySource, "internal static string BuildOriginDossierLinkNotesDisplayValue()");
        StringAssert.Contains(factorySource, "newCharacterOriginBookPreview");
        StringAssert.Contains(factorySource, "VisualKind: DesktopDialogFieldVisualKinds.Book");
        StringAssert.Contains(factorySource, "new DesktopDialogAction(\"open_origin_guided_chargen\", \"Start character creation\", true)");
        Assert.IsFalse(originSurfaceSource.Contains("Color.Parse", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Brushes.White", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Brushes.Black", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("new SolidColorBrush", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Background = Brushes", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Foreground = Brushes", StringComparison.Ordinal));
        Assert.IsFalse(originSurfaceSource.Contains("Use this route to reopen the Origin Dossier workflow without publishing the story text.", StringComparison.Ordinal));
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
        StringAssert.Contains(projectorSource, "for this dossier yet");
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

        StringAssert.Contains(aliceSource, "CreateButton(\"Draft story\", StartOriginDossierAsync, isPrimary: true, name: \"AliceOriginStartDossierButton\")");
        StringAssert.Contains(aliceSource, "CreateButton(\"Open story\", () => DesktopCrashRuntime.TryOpenPathInShell(_originDraftMarkdownPath), isPrimary: true, name: \"AliceOriginOpenDraftStoryButton\")");
        StringAssert.Contains(aliceSource, "use the story as Alice's seed for later build guidance.");

        int approveStart = aliceSource.IndexOf("Task ApproveOriginCanonAsync()", StringComparison.Ordinal);
        int renderPdfStart = aliceSource.IndexOf("Task RenderOriginDossierPdfAsync()", StringComparison.Ordinal);
        Assert.IsTrue(approveStart >= 0 && renderPdfStart > approveStart, "Approve-origin source must be discoverable.");
        string approveSource = aliceSource[approveStart..renderPdfStart];

        int openBookIndex = approveSource.IndexOf("\"Open book\"", StringComparison.Ordinal);
        int openStoryIndex = approveSource.IndexOf("\"Open story\"", StringComparison.Ordinal);
        int portraitIndex = approveSource.IndexOf("\"Create portraits\"", StringComparison.Ordinal);
        int voiceIndex = approveSource.IndexOf("\"Set up main voice\"", StringComparison.Ordinal);
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
        int emptyOriginIndex = idleSource.IndexOf("CreateButton(\"Draft story\", StartOriginDossierAsync", StringComparison.Ordinal);
        Assert.IsTrue(emptyOriginIndex >= 0, "Empty Origin Dossier state must start with Draft story.");
        string emptyOriginSource = idleSource[emptyOriginIndex..];
        Assert.IsFalse(emptyOriginSource.Contains("\"Create dossier video\"", StringComparison.Ordinal));
        Assert.IsFalse(emptyOriginSource.Contains("\"Render audiobook now\"", StringComparison.Ordinal));
        Assert.IsFalse(emptyOriginSource.Contains("\"Create portraits\"", StringComparison.Ordinal));
        Assert.IsFalse(aliceSource.Contains("Reviewed variants stay bounded", StringComparison.Ordinal));
        Assert.IsFalse(aliceSource.Contains("bounded preview", StringComparison.Ordinal));
        Assert.IsFalse(aliceSource.Contains("safest origin draft is a bounded", StringComparison.Ordinal));
        Assert.IsFalse(aliceSource.Contains("audiobook artifact", StringComparison.Ordinal));
        Assert.IsFalse(aliceSource.Contains("Cold medical lane", StringComparison.Ordinal));
        Assert.IsFalse(aliceSource.Contains("sterile upgrade lane", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Alice_account_handoffs_do_not_call_the_account_a_workspace()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string aliceSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));

        StringAssert.Contains(aliceSource, "Open Chummer account");
        Assert.IsFalse(aliceSource.Contains("Open account workspace", StringComparison.Ordinal));
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
    public void Blazor_origin_story_preview_uses_shell_surface_tokens_instead_of_hardcoded_pale_cards()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string appCss = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Blazor", "wwwroot", "app.css"));
        int originStart = appCss.IndexOf(".dialog-origin-panel,", StringComparison.Ordinal);
        int originEnd = appCss.IndexOf("@media (max-width: 1080px)", originStart, StringComparison.Ordinal);

        Assert.IsTrue(originStart >= 0 && originEnd > originStart, "Origin Dossier browser styles must stay discoverable.");
        string originCss = appCss[originStart..originEnd];

        StringAssert.Contains(originCss, "background: var(--ui-kit-shell-surface-emphasis);");
        StringAssert.Contains(originCss, "background: var(--ui-kit-panel-surface);");
        StringAssert.Contains(originCss, ".dialog-origin-summary-label {");
        StringAssert.Contains(originCss, ".dialog-origin-panel > header p,");
        StringAssert.Contains(originCss, ".dialog-origin-panel .dialog-note,");
        StringAssert.Contains(originCss, ".dialog-origin-narrative,");
        StringAssert.Contains(originCss, ".dialog-origin-book,");
        StringAssert.Contains(originCss, ".dialog-origin-preview :is(p, pre, strong, span, li, em),");
        StringAssert.Contains(originCss, ".dialog-origin-preview .dialog-visual-pre,");
        StringAssert.Contains(originCss, ".dialog-origin-support-grid {");
        StringAssert.Contains(originCss, "color: var(--ui-kit-ink);");
        StringAssert.Contains(originCss, "color: var(--ui-kit-ink-strong);");
        StringAssert.Contains(originCss, "color: var(--ui-kit-muted);");
        Assert.IsFalse(originCss.Contains("#f8f4ec", StringComparison.Ordinal));
        Assert.IsFalse(originCss.Contains("rgba(255, 255, 255, 0.78)", StringComparison.Ordinal));
        Assert.IsFalse(originCss.Contains("rgba(255, 255, 255, 0.84)", StringComparison.Ordinal));
        Assert.IsFalse(originCss.Contains("color: #111111", StringComparison.Ordinal));
        Assert.IsFalse(originCss.Contains("color: #111827", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Blazor_dialog_inputs_and_notes_keep_explicit_shell_contrast_tokens()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string appCss = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Blazor", "wwwroot", "app.css"));
        int blockStart = appCss.IndexOf(".note {", StringComparison.Ordinal);
        int blockEnd = appCss.IndexOf(".dialog-tab-chip {", blockStart, StringComparison.Ordinal);

        Assert.IsTrue(blockStart >= 0 && blockEnd > blockStart, "Shared browser dialog contrast styles must stay discoverable.");
        string contrastCss = appCss[blockStart..blockEnd];

        StringAssert.Contains(contrastCss, ".dialog-input {");
        StringAssert.Contains(contrastCss, "background: var(--ui-kit-panel-surface);");
        StringAssert.Contains(contrastCss, "border: 1px solid var(--ui-kit-shell-border);");
        StringAssert.Contains(contrastCss, "color: var(--ui-kit-ink);");
        StringAssert.Contains(contrastCss, ".dialog-input::placeholder {");
        StringAssert.Contains(contrastCss, "color: var(--ui-kit-muted);");
        StringAssert.Contains(contrastCss, ".dialog-input[readonly] {");
        StringAssert.Contains(contrastCss, ".dialog-visual-pre {");
        Assert.IsFalse(contrastCss.Contains("color: #4b4b4b", StringComparison.Ordinal));
        Assert.IsFalse(contrastCss.Contains("color: #505050", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Browser_preview_shell_keeps_embedded_origin_dialogs_on_shared_contrast_tokens()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string previewCss = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Blazor", "Components", "Pages", "Preview.razor.css"));
        int blockStart = previewCss.IndexOf(".browser-preview-frame ::deep .desktop-shell,", StringComparison.Ordinal);
        int blockEnd = previewCss.IndexOf("@media (max-width: 720px)", blockStart, StringComparison.Ordinal);

        Assert.IsTrue(blockStart >= 0 && blockEnd > blockStart, "Browser preview shell contrast overrides must stay discoverable.");
        string previewShellCss = previewCss[blockStart..blockEnd];

        StringAssert.Contains(previewShellCss, ".browser-preview-frame ::deep .desktop-shell pre,");
        StringAssert.Contains(previewShellCss, ".browser-preview-frame ::deep .dialog-visual-pre {");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .desktop-dialog pre,");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-origin-narrative,");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-origin-preview,");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-origin-narrative p,");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-origin-preview p,");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-origin-preview--book {");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-origin-panel > header p,");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-origin-summary-card strong {");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-origin-summary-label {");
        StringAssert.Contains(previewShellCss, ".browser-preview-shell ::deep .dialog-note,");
        StringAssert.Contains(previewShellCss, "background: var(--ui-kit-panel-surface);");
        StringAssert.Contains(previewShellCss, "background: var(--ui-kit-shell-surface-emphasis);");
        StringAssert.Contains(previewShellCss, "color: var(--ui-kit-ink);");
        StringAssert.Contains(previewShellCss, "color: var(--ui-kit-ink-strong);");
        StringAssert.Contains(previewShellCss, "color: var(--ui-kit-muted);");
        Assert.IsFalse(previewShellCss.Contains("color: #16202b", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Browser_and_home_page_theme_layers_do_not_style_generic_hint_or_summary_classes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string previewCss = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Blazor", "Components", "Pages", "Preview.razor.css"));
        string homeCss = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Blazor", "Components", "Pages", "Home.razor.css"));

        Assert.IsFalse(previewCss.Contains(".preview-page :is(p, .muted, .hint, .summary)", StringComparison.Ordinal));
        Assert.IsFalse(homeCss.Contains(".home-page :is(p, .muted, .hint, .summary)", StringComparison.Ordinal));
        Assert.IsFalse(homeCss.Contains(".chummer-home :is(p, .muted, .hint, .summary)", StringComparison.Ordinal));
        StringAssert.Contains(previewCss, ".preview-page p {");
        StringAssert.Contains(homeCss, ".home-page p,");
        StringAssert.Contains(homeCss, ".chummer-home p {");
    }

    [TestMethod]
    public void Attribute_editor_keeps_clear_column_headers_visible()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string sectionHostMarkup = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "SectionHostControl.axaml"));

        StringAssert.Contains(sectionHostMarkup, "x:Name=\"AttributeParityHeaderAttributeText\" Text=\"Attribute\" Classes=\"shell-section-title\"");
        StringAssert.Contains(sectionHostMarkup, "x:Name=\"AttributeParityHeaderStartText\" Text=\"Start\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "x:Name=\"AttributeParityHeaderAddText\" Text=\"Add\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "x:Name=\"AttributeParityHeaderTotalText\" Text=\"Total\" Classes=\"shell-caption\"");
        StringAssert.Contains(sectionHostMarkup, "x:Name=\"AttributeParityHeaderLimitsText\" Text=\"Limits\" Classes=\"shell-caption\"");
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
        StringAssert.Contains(sectionHostSource, "ColumnDefinitions = new ColumnDefinitions(\"28,10,*,10,28\")");
        StringAssert.Contains(sectionHostSource, "Name = $\"{name}_Value\"");
        StringAssert.Contains(sectionHostSource, "MinWidth = 42");
        StringAssert.Contains(sectionHostSource, "Margin = new Thickness(4d, 0d)");
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
        StringAssert.Contains(desktopDialogSource, "valueText.Foreground = DesktopShellTheme.ResolveForegroundBrush();");
        StringAssert.Contains(desktopDialogSource, "valueText.FontWeight = FontWeight.SemiBold;");
        StringAssert.Contains(desktopDialogSource, "Text = attribute.Value,");
        StringAssert.Contains(desktopDialogSource, "Foreground = DesktopShellTheme.ResolveForegroundBrush(),");
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
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:pressed\">");
        StringAssert.Contains(appTheme, "<Setter Property=\"Background\" Value=\"{DynamicResource ComboBoxItemBackgroundSelected}\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"Foreground\" Value=\"{DynamicResource ComboBoxItemForegroundSelected}\" />");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:pressed TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:pointerover /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:pointerover ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:pressed /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:pressed ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:disabled\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:disabled TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:disabled ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:disabled /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBoxItem:selected:pointerover /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected TextBlock.shell-option-label\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover TextBlock.shell-option-label\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected TextBlock.shell-option-meta\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBoxItem:selected:pointerover TextBlock.shell-option-meta\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox /template/ TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pointerover /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pointerover ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pointerover /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pointerover /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pressed\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox.shell-combo:pressed\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pressed /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pressed ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pressed /template/ TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pressed /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:pressed /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:focus /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:focus ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:focus /template/ TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:focus /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:focus /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:disabled /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:disabled ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:disabled /template/ TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:disabled /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ComboBox:disabled /template/ Border\">");
        StringAssert.Contains(appTheme, "<Setter Property=\"TextElement.Foreground\" Value=\"{DynamicResource ComboBoxForeground}\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"Background\" Value=\"{DynamicResource ComboBoxBackground}\" />");
        StringAssert.Contains(appTheme, "<Setter Property=\"BorderBrush\" Value=\"{DynamicResource ComboBoxBorderBrush}\" />");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:focus\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"ListBox\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeView\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeViewItem\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeViewItem:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeViewItem:selected\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeViewItem:selected:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeViewItem TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeViewItem /template/ ContentPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeViewItem:selected TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TreeViewItem:selected /template/ ContentPresenter\">");
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
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:pointerover /template/ TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:pointerover /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:pointerover /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:focus /template/ TextBlock\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:focus /template/ TextPresenter\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:focus /template/ Border\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"TextBox:disabled /template/ TextBlock\">");
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
        StringAssert.Contains(shellTheme, "ClearInputBrushes(textBox);");
        StringAssert.Contains(shellTheme, "ApplyShellTreeViewTheme(TreeView treeView)");
        StringAssert.Contains(shellTheme, "ClearTemplatedBrushes(treeView);");
        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ApplyShellTreeViewTheme(treeView);");
        StringAssert.Contains(classicPortSurface, "DesktopShellTheme.ApplyShellComboBoxTheme(comboBox);");
        StringAssert.Contains(shellTheme, "ApplyShellListBoxTheme(ListBox listBox)");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:pointerover\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:focus\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown /template/ TextBox\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:pointerover /template/ TextBox\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:focus /template/ TextBox\">");
        StringAssert.Contains(appTheme, "<Style Selector=\"NumericUpDown:disabled /template/ TextBox\">");
        StringAssert.Contains(shellTheme, "ClearTemplatedBrushes(numericUpDown);");
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

    [TestMethod]
    public void Desktop_dialog_shell_surface_fallbacks_do_not_reintroduce_white_cards_in_dark_mode()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string shellThemeSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopShellTheme.cs"));
        string desktopDialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));

        foreach (string marker in new[]
                 {
                     "public static IBrush ResolveWindowBackgroundBrush()",
                     "=> ResolveThemeBrush(\"ChummerShellWindowBackgroundBrush\", \"#050B16\");",
                     "public static IBrush ResolveSurfaceBrush()",
                     "=> ResolveThemeBrush(\"ChummerShellSurfaceBrush\", \"#111827\");",
                     "public static IBrush ResolveSurfaceAltBrush()",
                     "=> ResolveThemeBrush(\"ChummerShellSurfaceAltBrush\", \"#020617\");",
                     "public static IBrush ResolveSelectionToolbarBrush()",
                     "=> ResolveThemeBrush(\"ChummerShellSelectionToolbarBrush\", \"#0B1220\");",
                     "public static IBrush ResolveSelectionPanelBrush()",
                     "=> ResolveThemeBrush(\"ChummerShellSelectionPanelBrush\", \"#111827\");",
                     "public static IBrush ResolveSelectionInsetBrush()",
                     "=> ResolveThemeBrush(\"ChummerShellSelectionInsetBrush\", \"#0F172A\");"
                 })
        {
            StringAssert.Contains(shellThemeSource, marker);
        }

        string[] forbiddenLightFallbacks =
        [
            "#FFFFFF",
            "#FBFCFE",
            "#F8FAFC",
            "#F2F5FA",
            "#F1F5F9",
            "#EEF3F8",
            "#EEF2F6"
        ];

        List<string> offenders = forbiddenLightFallbacks
            .Where(fallback => desktopDialogSource.Contains(fallback, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ResolveSelectionToolbarBrush()");
        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ResolveSelectionPanelBrush()");
        StringAssert.Contains(desktopDialogSource, "DesktopShellTheme.ResolveSurfaceBrush()");

        Assert.AreEqual(
            0,
            offenders.Count,
            "Desktop dialog shell surface fallbacks must be dark-safe because missing resources on KDE dark mode otherwise become pale cards with inherited light text. Offenders: "
            + string.Join(", ", offenders));
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
        Regex typedCreation = new(@"(?<type>ListBox|ComboBox|TextBox|NumericUpDown|TreeView)\s+(?<name>[_A-Za-z][_A-Za-z0-9]*)\s*=\s*new(?:\s+(ListBox|ComboBox|TextBox|NumericUpDown|TreeView))?\b", RegexOptions.Compiled);
        Regex assignedCreation = new(@"(?<name>[_A-Za-z][_A-Za-z0-9]*)\s*=\s*new\s+(?<type>ListBox|ComboBox|TextBox|NumericUpDown|TreeView)\b", RegexOptions.Compiled);
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
                        : string.Equals(creation.Type, "TreeView", StringComparison.Ordinal)
                            ? lookahead.Contains($"ApplyShellTreeViewTheme({creation.Name}", StringComparison.Ordinal)
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
            "Every Avalonia desktop ListBox/TreeView/ComboBox/TextBox/NumericUpDown must opt into the shell theme helper near creation so KDE/dark-mode cannot produce white-on-white controls. Missing: "
            + string.Join(", ", unthemedControls));
    }

    [TestMethod]
    public void Xaml_declared_desktop_inputs_are_themed_as_soon_as_codebehind_finds_them()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string avaloniaRoot = Path.Combine(repoRoot, "Chummer.Avalonia");
        Regex findControlInput = new(
            @"(?<field>[_A-Za-z][_A-Za-z0-9]*)\s*=\s*this\.FindControl<(?<type>ComboBox|TextBox|NumericUpDown)>\(""(?<name>[^""]+)""\);",
            RegexOptions.Compiled);
        List<string> unthemedLookups = [];

        foreach (string sourcePath in Directory.EnumerateFiles(avaloniaRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(static path => !path.EndsWith("DesktopShellTheme.cs", StringComparison.Ordinal)))
        {
            string[] lines = File.ReadAllLines(sourcePath);
            for (int index = 0; index < lines.Length; index++)
            {
                Match match = findControlInput.Match(lines[index]);
                if (!match.Success)
                {
                    continue;
                }

                string field = match.Groups["field"].Value;
                string type = match.Groups["type"].Value;
                string helper = type switch
                {
                    "ComboBox" => "ApplyShellComboBoxTheme",
                    "TextBox" => "ApplyShellTextInputTheme",
                    "NumericUpDown" => "ApplyShellNumericUpDownTheme",
                    _ => throw new InvalidOperationException($"Unexpected input type: {type}")
                };
                string lookahead = string.Join('\n', lines.Skip(index).Take(10));
                if (!lookahead.Contains($"{helper}({field}", StringComparison.Ordinal))
                {
                    string relativePath = Path.GetRelativePath(repoRoot, sourcePath);
                    unthemedLookups.Add($"{relativePath}:{index + 1} {type} {match.Groups["name"].Value}");
                }
            }
        }

        Assert.AreEqual(
            0,
            unthemedLookups.Count,
            "Named XAML desktop inputs must opt into shell theming immediately after lookup, before state/data binding. Missing: "
            + string.Join(", ", unthemedLookups.OrderBy(static item => item, StringComparer.Ordinal)));
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
