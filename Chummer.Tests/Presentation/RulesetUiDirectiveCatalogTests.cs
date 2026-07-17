#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Rulesets;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
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

        StringAssert.Contains(sr4, "import tools");
        StringAssert.Contains(sr4, ".chum4");
        StringAssert.Contains(sr5, "main editor");
        StringAssert.Contains(sr5, ".chum5");
        StringAssert.Contains(sr5, "runtime attention required");
        StringAssert.Contains(sr6, "character builder");
        StringAssert.Contains(sr6, ".chum6");
        StringAssert.Contains(sr6, "character tools are available");
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
        StringAssert.Contains(sr4Rules, "limited");
        StringAssert.Contains(sr5BuildLab, "main desktop editor");
        StringAssert.Contains(sr5BuildLab, "campaign return");
        StringAssert.Contains(sr6Rules, "rules and review");
        StringAssert.Contains(sr6Rules, "character tools need attention");
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
        StringAssert.Contains(sr4, "import details");
        StringAssert.Contains(sr5, ".chum5");
        StringAssert.Contains(sr5, "runtime status is loaded");
        StringAssert.Contains(sr6, ".chum6");
        StringAssert.Contains(sr6, "character build");
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
        StringAssert.Contains(sr4FollowThrough, "SR4 intake details");
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
        string sr6StripTitle = RulesetUiDirectiveCatalog.BuildWorkspaceStripTitle(RulesetDefaults.Sr6, "ws-6", hasSavedWorkspace: true);
        string sr4Tabs = RulesetUiDirectiveCatalog.BuildNavigationTabsHeading(RulesetDefaults.Sr4);
        string sr5Actions = RulesetUiDirectiveCatalog.BuildSectionActionsHeading(RulesetDefaults.Sr5);
        string sr6Flows = RulesetUiDirectiveCatalog.BuildWorkflowSurfacesHeading(RulesetDefaults.Sr6);
        string sr4Import = RulesetUiDirectiveCatalog.BuildImportHeading(RulesetDefaults.Sr4);
        string sr6Import = RulesetUiDirectiveCatalog.BuildImportHeading(RulesetDefaults.Sr6);
        string sr5ImportAccept = RulesetUiDirectiveCatalog.BuildImportAcceptAttribute(RulesetDefaults.Sr5);
        string sr6ImportHint = RulesetUiDirectiveCatalog.BuildImportHint(RulesetDefaults.Sr6);
        string sr6ImportPlaceholder = RulesetUiDirectiveCatalog.BuildImportFilePlaceholder(RulesetDefaults.Sr6);
        string sr4ImportDebug = RulesetUiDirectiveCatalog.BuildImportDebugHeading(RulesetDefaults.Sr4);
        string sr6ImportDebug = RulesetUiDirectiveCatalog.BuildImportDebugHeading(RulesetDefaults.Sr6);
        string sr6ImportAction = RulesetUiDirectiveCatalog.BuildImportRawActionLabel(RulesetDefaults.Sr6);
        string sr4Commands = RulesetUiDirectiveCatalog.BuildCommandHeading(RulesetDefaults.Sr4);
        string sr6CommandHint = RulesetUiDirectiveCatalog.BuildCommandEmptyHint(RulesetDefaults.Sr6);
        string sr5Result = RulesetUiDirectiveCatalog.BuildResultHeading(RulesetDefaults.Sr5);
        string sr5ResultHint = RulesetUiDirectiveCatalog.BuildResultPostureHint(RulesetDefaults.Sr5);
        string sr4Ready = RulesetUiDirectiveCatalog.BuildResultReadyNotice(RulesetDefaults.Sr4);
        string sr4Create = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr4, "tab-create", "Create");
        string sr5Info = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr5, "tab-info", "Info");
        string sr6Create = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-create", "Create");
        string sr6Gear = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-gear", "Gear");
        string sr6StreetGear = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-streetgear", "Street Gear");
        string sr6Rules = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-rules", "Rules");
        string sr6Relationships = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-relationships", "Relationships");
        string sr6Karma = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-karma", "Karma & Nuyen");
        string sr6Calendar = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-calendar", "Calendar");
        string sr6Improvements = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, "tab-improvements", "Improvements");
        string sr4Validate = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr4, "tab-info.validate", "validate", "Validate");
        string sr5Build = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr5, "tab-info.build", "build", "Build");
        string sr6Build = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr6, "tab-create.intake", "build", "Build");
        string sr6BuildPath = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr6, "tab-info.build", "build", "Build");
        string sr6Progress = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr6, "tab-info.progress", "progress", "Progress");
        string sr6Inventory = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(RulesetDefaults.Sr6, "tab-gear.inventory", "inventory", "Inventory");
        string sr5Workspace = RulesetUiDirectiveCatalog.BuildWorkspaceNavigatorLabel(RulesetDefaults.Sr5, "Apex", "Ghost", hasSavedWorkspace: true);
        string sr6Workflow = RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel(RulesetDefaults.Sr6, "tab-info.validate", "Refresh Summary");
        string preservedWorkflow = RulesetUiDirectiveCatalog.FormatWorkflowSurfaceLabel(RulesetDefaults.Sr6, "tab-info.validate", "SR6 Matrix Action");

        Assert.AreEqual("SR4 import tools", sr4MarqueeEyebrow);
        Assert.AreEqual("Shadowrun 5 character editor", sr5MarqueeTitle);
        Assert.AreEqual("SR6 character builder", sr6MarqueeEyebrow);
        Assert.AreEqual("Desktop Summary · SR4 Import Tools", sr4Summary);
        Assert.AreEqual("SR5 Characters", sr5Dossiers);
        Assert.AreEqual("No open SR6 character", sr6EmptyStrip);
        StringAssert.Contains(sr5StripTitle, "Shadowrun 5");
        StringAssert.Contains(sr5StripTitle, "main editor");
        StringAssert.Contains(sr5StripTitle, "unsaved");
        StringAssert.Contains(sr6StripTitle, "Shadowrun 6");
        StringAssert.Contains(sr6StripTitle, "character builder");
        StringAssert.Contains(sr6StripTitle, "workspace");
        Assert.AreEqual("SR4 Import Tabs", sr4Tabs);
        Assert.AreEqual("SR5 Editor Actions", sr5Actions);
        Assert.AreEqual("SR6 Character Flows", sr6Flows);
        Assert.AreEqual("Import SR4 Runner File", sr4Import);
        Assert.AreEqual("Import SR6 Character File", sr6Import);
        Assert.AreEqual(".chum5,.chum4,.chum6,.xml,text/xml,application/xml", sr5ImportAccept);
        StringAssert.Contains(sr6ImportHint, ".chum6");
        Assert.AreEqual("(no SR6 character file selected)", sr6ImportPlaceholder);
        Assert.AreEqual("SR4 Runner XML Review", sr4ImportDebug);
        Assert.AreEqual("SR6 Character XML Review", sr6ImportDebug);
        Assert.AreEqual("Import SR6 Character XML", sr6ImportAction);
        Assert.AreEqual("SR4 Import Tools", sr4Commands);
        Assert.AreEqual("No SR6 character commands are currently available.", sr6CommandHint);
        Assert.AreEqual("SR5 Editor Result", sr5Result);
        StringAssert.Contains(sr5ResultHint, "main desktop editor");
        StringAssert.Contains(sr4Ready, "import");
        Assert.AreEqual("Import", sr4Create);
        Assert.AreEqual("Runner", sr5Info);
        Assert.AreEqual("Build", sr6Create);
        Assert.AreEqual("Gear & Kit", sr6Gear);
        Assert.AreEqual("Street Gear", sr6StreetGear);
        Assert.AreEqual("Rules", sr6Rules);
        Assert.AreEqual("Relationships", sr6Relationships);
        Assert.AreEqual("Karma & Nuyen", sr6Karma);
        Assert.AreEqual("Timeline", sr6Calendar);
        Assert.AreEqual("Advancement", sr6Improvements);
        Assert.AreEqual("Character Review", sr4Validate);
        Assert.AreEqual("Build Plan", sr5Build);
        Assert.AreEqual("Build Character", sr6Build);
        Assert.AreEqual("Build Path", sr6BuildPath);
        Assert.AreEqual("Advancement Track", sr6Progress);
        Assert.AreEqual("Gear", sr6Inventory);
        StringAssert.Contains(sr5Workspace, "Shadowrun 5");
        StringAssert.Contains(sr5Workspace, "main editor");
        StringAssert.Contains(sr5Workspace, "saved");
        Assert.AreEqual("Review", sr6Workflow);
        Assert.AreEqual("SR6 Matrix Action", preservedWorkflow);
    }

    [TestMethod]
    public void Sr6_shell_directives_keep_authored_pendants_where_sr5_already_has_authored_labels()
    {
        List<string> missingPendants = [];

        IReadOnlyDictionary<string, NavigationTabDefinition> sr6Tabs = new Sr6RulesetShellDefinitionProvider().GetNavigationTabs()
            .ToDictionary(tab => tab.Id, StringComparer.Ordinal);
        foreach (NavigationTabDefinition sr5Tab in new Sr5RulesetShellDefinitionProvider().GetNavigationTabs())
        {
            string sr5Label = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr5, sr5Tab.Id, sr5Tab.Label);
            NavigationTabDefinition sr6Tab = sr6Tabs[sr5Tab.Id];
            string sr6Label = RulesetUiDirectiveCatalog.FormatNavigationTabLabel(RulesetDefaults.Sr6, sr6Tab.Id, sr6Tab.Label);

            if (!string.Equals(sr5Label, sr5Tab.Label, StringComparison.Ordinal)
                && string.Equals(sr6Label, sr6Tab.Label, StringComparison.Ordinal))
            {
                missingPendants.Add($"tab:{sr5Tab.Id}");
            }
        }

        IReadOnlyDictionary<string, WorkspaceSurfaceActionDefinition> sr6Actions = new Sr6RulesetCatalogProvider().GetWorkspaceActions()
            .ToDictionary(action => action.Id, StringComparer.Ordinal);
        foreach (WorkspaceSurfaceActionDefinition sr5Action in new Sr5RulesetCatalogProvider().GetWorkspaceActions())
        {
            string sr5Label = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(
                RulesetDefaults.Sr5,
                sr5Action.Id,
                sr5Action.TargetId,
                sr5Action.Label);
            WorkspaceSurfaceActionDefinition sr6Action = sr6Actions[sr5Action.Id];
            string sr6Label = RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(
                RulesetDefaults.Sr6,
                sr6Action.Id,
                sr6Action.TargetId,
                sr6Action.Label);

            if (!string.Equals(sr5Label, sr5Action.Label, StringComparison.Ordinal)
                && string.Equals(sr6Label, sr6Action.Label, StringComparison.Ordinal))
            {
                missingPendants.Add($"action:{sr5Action.Id}");
            }
        }

        Assert.IsTrue(
            missingPendants.Count == 0,
            $"SR6 is still missing authored pendants where SR5 already has authored labels: {string.Join(", ", missingPendants)}");
    }

    [TestMethod]
    public void LoadedRunnerTabFilter_keeps_edit_tabs_visible_and_hides_catalog_only_tabs()
    {
        Assert.IsFalse(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-create"));
        Assert.IsFalse(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-rules"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-info"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-attributes"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-skills"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-qualities"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-gear"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-streetgear"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-cyberware"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-adept"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-relationships"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-contacts"));
        Assert.IsTrue(RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab("tab-karma"));
    }

    [TestMethod]
    public void FormatDialogNotice_applies_ruleset_specific_dialog_prefixes()
    {
        Assert.AreEqual(
            "SR4 import tools: Gear 'Ares Alpha' added.",
            RulesetUiDirectiveCatalog.FormatDialogNotice(RulesetDefaults.Sr4, "Gear 'Ares Alpha' added."));
        Assert.AreEqual(
            "SR5 editor: Cyberware 'Wired Reflexes 2' added.",
            RulesetUiDirectiveCatalog.FormatDialogNotice(RulesetDefaults.Sr5, "Cyberware 'Wired Reflexes 2' added."));
        Assert.AreEqual(
            "SR6 character: Program 'Armor' added.",
            RulesetUiDirectiveCatalog.FormatDialogNotice(RulesetDefaults.Sr6, "Program 'Armor' added."));
        Assert.AreEqual(
            "Generic notice.",
            RulesetUiDirectiveCatalog.FormatDialogNotice("shared", "Generic notice."));
    }
}
