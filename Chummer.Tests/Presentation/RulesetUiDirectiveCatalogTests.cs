#nullable enable annotations

using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Rulesets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class RulesetUiDirectiveCatalogTests
{
    [TestMethod]
    public void BuildComplianceRulesetSummary_distinguishes_sr4_sr5_and_sr6_posture()
    {
        string sr4 = RulesetUiDirectiveCatalog.BuildComplianceRulesetSummary(
            RulesetDefaults.Sr4,
            activeRuntime: null);
        string sr5 = RulesetUiDirectiveCatalog.BuildComplianceRulesetSummary(
            RulesetDefaults.Sr5,
            new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr5.core",
                Title: "Official SR5 Core",
                RulesetId: RulesetDefaults.Sr5,
                RuntimeFingerprint: "sha256:sr5",
                InstallState: ArtifactInstallStates.Available,
                WarningCount: 1));
        string sr6 = RulesetUiDirectiveCatalog.BuildComplianceRulesetSummary(
            RulesetDefaults.Sr6,
            new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr6.core",
                Title: "Official SR6 Core",
                RulesetId: RulesetDefaults.Sr6,
                RuntimeFingerprint: "sha256:sr6",
                InstallState: ArtifactInstallStates.Available,
                WarningCount: 0));

        StringAssert.Contains(sr4, "import workbench");
        StringAssert.Contains(sr4, ".chum4");
        StringAssert.Contains(sr5, "main editor");
        StringAssert.Contains(sr5, ".chum5");
        StringAssert.Contains(sr5, "runtime/provider attention required");
        StringAssert.Contains(sr6, "setup workbench");
        StringAssert.Contains(sr6, ".chum6");
        StringAssert.Contains(sr6, "experimental-host honesty visible");
    }

    [TestMethod]
    public void BuildSectionNotice_uses_ruleset_specific_copy_for_rules_and_build_lab_surfaces()
    {
        string sr4Rules = RulesetUiDirectiveCatalog.BuildSectionNotice(RulesetDefaults.Sr4, "rules", "tab-rules.rules", activeRuntime: null);
        string sr5BuildLab = RulesetUiDirectiveCatalog.BuildSectionNotice(RulesetDefaults.Sr5, "build-lab", "tab-create.intake", activeRuntime: null);
        string sr6Rules = RulesetUiDirectiveCatalog.BuildSectionNotice(
            RulesetDefaults.Sr6,
            "validate",
            "tab-info.validate",
            new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr6.core",
                Title: "Official SR6 Core",
                RulesetId: RulesetDefaults.Sr6,
                RuntimeFingerprint: "sha256:sr6",
                InstallState: ArtifactInstallStates.Installed,
                WarningCount: 1));

        StringAssert.Contains(sr4Rules, "Shadowrun 4");
        StringAssert.Contains(sr4Rules, "parity-gated");
        StringAssert.Contains(sr5BuildLab, "main desktop editor");
        StringAssert.Contains(sr5BuildLab, "campaign return");
        StringAssert.Contains(sr6Rules, "setup-gated");
        StringAssert.Contains(sr6Rules, "runtime warnings remain active");
    }

    [TestMethod]
    public void BuildRulePosture_strings_keep_ruleset_specific_extensions_and_lane_labels()
    {
        string sr4 = RulesetUiDirectiveCatalog.BuildUngroundedRulePosture(RulesetDefaults.Sr4);
        string sr5 = RulesetUiDirectiveCatalog.BuildPinnedRuntimeRulePosture(RulesetDefaults.Sr5, "sha256:sr5");
        string sr6 = RulesetUiDirectiveCatalog.BuildGroundedRulePosture(
            RulesetDefaults.Sr6,
            gameEdition: "Shadowrun 6",
            settings: "Seattle Nights",
            gameplayMode: "Prime runner preview",
            runtimeFingerprint: "sha256:sr6",
            installState: ArtifactInstallStates.Installed);

        StringAssert.Contains(sr4, ".chum4");
        StringAssert.Contains(sr4, "import workbench");
        StringAssert.Contains(sr5, ".chum5");
        StringAssert.Contains(sr5, "main editor");
        StringAssert.Contains(sr6, ".chum6");
        StringAssert.Contains(sr6, "setup workbench");
        StringAssert.Contains(sr6, "Seattle Nights");
    }

    [TestMethod]
    public void DesktopHomeDirectives_distinguish_ruleset_spotlights_resume_copy_and_action_labels()
    {
        CharacterFileSummary summary = new(
            Name: "Apex",
            Alias: "Ghost",
            Metatype: "Human",
            BuildMethod: "Priority",
            CreatedVersion: "6.0",
            AppVersion: "6.0",
            Karma: 0,
            Nuyen: 0,
            Created: true);

        string sr4Spotlight = RulesetUiDirectiveCatalog.BuildHomeSpotlight(RulesetDefaults.Sr4);
        string sr5Resume = RulesetUiDirectiveCatalog.BuildWorkspaceResumeSummary(
            RulesetDefaults.Sr5,
            summary,
            DateTimeOffset.Parse("2026-03-31T08:55:00+00:00"));
        string sr6Open = RulesetUiDirectiveCatalog.BuildOpenWorkspaceActionLabel(RulesetDefaults.Sr6, "Open workspace");
        string sr4FollowThrough = RulesetUiDirectiveCatalog.BuildBuildFollowThroughActionLabel(RulesetDefaults.Sr4, "Open build follow-through");
        string sr6WorkspaceFollowThrough = RulesetUiDirectiveCatalog.BuildWorkspaceFollowThroughActionLabel(RulesetDefaults.Sr6, "Open workspace follow-through");
        string? sr5Prefix = RulesetUiDirectiveCatalog.BuildNextActionPrefix(RulesetDefaults.Sr5);

        StringAssert.Contains(sr4Spotlight, "import intake");
        StringAssert.Contains(sr5Resume, "Shadowrun 5 resume");
        StringAssert.Contains(sr5Resume, "SR5 character");
        StringAssert.Contains(sr5Resume, "Apex / Ghost");
        StringAssert.Contains(sr6Open, "SR6 character");
        StringAssert.Contains(sr4FollowThrough, "SR4 import details");
        StringAssert.Contains(sr6WorkspaceFollowThrough, "SR6 character details");
        Assert.AreEqual("SR5", sr5Prefix);
    }

    [TestMethod]
    public void ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset()
    {
        string sr4MarqueeEyebrow = RulesetUiDirectiveCatalog.BuildDesktopMarqueeEyebrow(RulesetDefaults.Sr4);
        string sr5MarqueeTitle = RulesetUiDirectiveCatalog.BuildDesktopMarqueeTitle(RulesetDefaults.Sr5);
        string sr6MarqueeEyebrow = RulesetUiDirectiveCatalog.BuildDesktopMarqueeEyebrow(RulesetDefaults.Sr6);
        string sr4Summary = RulesetUiDirectiveCatalog.BuildSummaryHeading(RulesetDefaults.Sr4);
        string sr5Dossiers = RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(RulesetDefaults.Sr5);
        string sr6EmptyStrip = RulesetUiDirectiveCatalog.BuildWorkspaceStripEmptyState(RulesetDefaults.Sr6);
        string sr5StripTitle = RulesetUiDirectiveCatalog.BuildWorkspaceStripTitle(RulesetDefaults.Sr5, "ws-1", hasSavedWorkspace: false);
        string sr4Tabs = RulesetUiDirectiveCatalog.BuildNavigationTabsHeading(RulesetDefaults.Sr4);
        string sr5Actions = RulesetUiDirectiveCatalog.BuildSectionActionsHeading(RulesetDefaults.Sr5);
        string sr6Flows = RulesetUiDirectiveCatalog.BuildWorkflowSurfacesHeading(RulesetDefaults.Sr6);
        string sr4Import = RulesetUiDirectiveCatalog.BuildImportHeading(RulesetDefaults.Sr4);
        string sr5ImportAccept = RulesetUiDirectiveCatalog.BuildImportAcceptAttribute(RulesetDefaults.Sr5);
        string sr6ImportHint = RulesetUiDirectiveCatalog.BuildImportHint(RulesetDefaults.Sr6);
        string sr4ImportDebug = RulesetUiDirectiveCatalog.BuildImportDebugHeading(RulesetDefaults.Sr4);
        string sr6ImportAction = RulesetUiDirectiveCatalog.BuildImportRawActionLabel(RulesetDefaults.Sr6);
        string sr4Commands = RulesetUiDirectiveCatalog.BuildCommandHeading(RulesetDefaults.Sr4);
        string sr6CommandHint = RulesetUiDirectiveCatalog.BuildCommandEmptyHint(RulesetDefaults.Sr6);
        string sr5Result = RulesetUiDirectiveCatalog.BuildResultHeading(RulesetDefaults.Sr5);
        string sr5ResultHint = RulesetUiDirectiveCatalog.BuildResultPostureHint(RulesetDefaults.Sr5);
        string sr4Ready = RulesetUiDirectiveCatalog.BuildResultReadyNotice(RulesetDefaults.Sr4);
        string sr4Create = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr4, "tab-create", "Create");
        string sr5Info = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr5, "tab-info", "Info");
        string sr6Rules = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-rules", "Rules");
        string sr4Validate = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr4, "tab-info.validate", "validate", "Validate");
        string sr5Build = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr5, "tab-info.build", "build", "Build");
        string sr6Inventory = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr6, "tab-gear.inventory", "inventory", "Inventory");
        string sr5Workspace = RulesetUiDirectiveCatalog.BuildWorkspaceNavigatorLabel(RulesetDefaults.Sr5, "Apex", "Ghost", hasSavedWorkspace: true);
        string sr6Workflow = RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel(RulesetDefaults.Sr6, "tab-info.validate", "Refresh Summary");
        string preservedWorkflow = RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel(RulesetDefaults.Sr6, "tab-info.validate", "SR6 Matrix Action");

        Assert.AreEqual("SR4 import workbench", sr4MarqueeEyebrow);
        Assert.AreEqual("Shadowrun 5 character editor", sr5MarqueeTitle);
        Assert.AreEqual("SR6 setup workbench", sr6MarqueeEyebrow);
        Assert.AreEqual("Desktop Summary · SR4 Import Workbench", sr4Summary);
        Assert.AreEqual("SR5 Characters", sr5Dossiers);
        Assert.AreEqual("No open SR6 character", sr6EmptyStrip);
        StringAssert.Contains(sr5StripTitle, "Shadowrun 5");
        StringAssert.Contains(sr5StripTitle, "main editor");
        StringAssert.Contains(sr5StripTitle, "unsaved");
        Assert.AreEqual("SR4 Import Workbench Tabs", sr4Tabs);
        Assert.AreEqual("SR5 Editor Actions", sr5Actions);
        Assert.AreEqual("SR6 Setup Flows", sr6Flows);
        Assert.AreEqual("Import SR4 Character File", sr4Import);
        Assert.AreEqual(".chum5,.chum4,.chum6,.xml,text/xml,application/xml", sr5ImportAccept);
        StringAssert.Contains(sr6ImportHint, ".chum6");
        Assert.AreEqual("SR4 Oracle Debug Import", sr4ImportDebug);
        Assert.AreEqual("Import SR6 Raw XML", sr6ImportAction);
        Assert.AreEqual("SR4 Import Tools", sr4Commands);
        Assert.AreEqual("No SR6 setup-workbench tools are currently available.", sr6CommandHint);
        Assert.AreEqual("SR5 Editor Result", sr5Result);
        StringAssert.Contains(sr5ResultHint, ".chum5");
        StringAssert.Contains(sr4Ready, "import");
        Assert.AreEqual("Import", sr4Create);
        Assert.AreEqual("Runner", sr5Info);
        Assert.AreEqual("Rules", sr6Rules);
        Assert.AreEqual("Parity Check", sr4Validate);
        Assert.AreEqual("Build Plan", sr5Build);
        Assert.AreEqual("Gear", sr6Inventory);
        StringAssert.Contains(sr5Workspace, "Shadowrun 5");
        StringAssert.Contains(sr5Workspace, "main editor");
        StringAssert.Contains(sr5Workspace, "saved");
        Assert.AreEqual("Validation", sr6Workflow);
        Assert.AreEqual("SR6 Matrix Action", preservedWorkflow);
    }

    [TestMethod]
    public void FormatDialogNotice_applies_ruleset_specific_dialog_prefixes()
    {
        Assert.AreEqual(
            "SR4 import workbench: Gear 'Ares Alpha' added.",
            RulesetUiDirectiveCatalog.FormatDialogNotice(RulesetDefaults.Sr4, "Gear 'Ares Alpha' added."));
        Assert.AreEqual(
            "SR5 editor: Cyberware 'Wired Reflexes 2' added.",
            RulesetUiDirectiveCatalog.FormatDialogNotice(RulesetDefaults.Sr5, "Cyberware 'Wired Reflexes 2' added."));
        Assert.AreEqual(
            "SR6 setup workbench: Program 'Armor' added.",
            RulesetUiDirectiveCatalog.FormatDialogNotice(RulesetDefaults.Sr6, "Program 'Armor' added."));
        Assert.AreEqual(
            "Generic notice.",
            RulesetUiDirectiveCatalog.FormatDialogNotice("shared", "Generic notice."));
    }
}
