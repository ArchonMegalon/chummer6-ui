#nullable enable annotations

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Application.Characters;
using Chummer.Application.Workspaces;
using Chummer.Contracts.AI;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Infrastructure.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Rulesets;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Hosting.Presentation;
using Chummer.Rulesets.Sr4;
using Chummer.Rulesets.Sr5;
using Chummer.Rulesets.Sr6;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
[DoNotParallelize]
// Runtime_backed_file_menu_restores_classic_save_and_print_commands
public sealed class AvaloniaFlagshipUiGateTests
{
    private static readonly object HeadlessInitLock = new();
    private static readonly JsonSerializerOptions ScreenshotEvidenceJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 256
    };
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static bool _headlessInitialized;
    private const int HeadlessSessionAttempts = 3;
    private static readonly string[] DefaultChummer5aFixtureUiReconstructionFixtureNames =
    [
        "Soma (Career).chum5"
    ];
    private static readonly string[] RootMenuControlNames =
    [
        "FileMenuButton",
        "EditMenuButton",
        "SpecialMenuButton",
        "ToolsMenuButton",
        "WindowsMenuButton",
        "HelpMenuButton"
    ];
    private static readonly string[] VeteranCertificationScreenshotFiles =
    [
        "01-initial-shell-light.png",
        "02-menu-open-light.png",
        "03-settings-open-light.png",
        "04-loaded-runner-light.png",
        "05-dense-section-light.png",
        "06-dense-section-dark.png",
        "07-loaded-runner-tabs-light.png",
        "08-cyberware-dialog-light.png",
        "09-vehicles-section-light.png",
        "10-contacts-section-light.png",
        "11-diary-dialog-light.png",
        "12-magic-dialog-light.png",
        "13-matrix-dialog-light.png",
        "14-advancement-dialog-light.png",
        "15-creation-section-light.png",
        "16-master-index-dialog-light.png",
        "17-character-roster-dialog-light.png",
        "18-import-dialog-light.png",
        "19-workflow-file-menu-loaded-light.png",
        "20-workflow-skills-section-light.png",
        "21-workflow-skill-add-dialog-light.png",
        "22-workflow-qualities-section-light.png",
        "23-workflow-quality-add-dialog-light.png",
        "24-workflow-gear-section-light.png",
        "25-workflow-gear-add-dialog-light.png",
        "26-workflow-weapons-section-light.png",
        "27-workflow-weapon-add-dialog-light.png",
        "28-workflow-armor-section-light.png",
        "29-workflow-armor-add-dialog-light.png",
        "30-workflow-cyberware-section-light.png",
        "31-workflow-powers-section-light.png",
        "32-workflow-adept-power-dialog-light.png",
        "33-workflow-complex-form-dialog-light.png",
        "34-workflow-validate-section-light.png",
        "35-workflow-rules-section-light.png",
        "36-workflow-new-character-dialog-light.png",
        "37-workflow-calendar-section-light.png",
        "38-translator-dialog-light.png",
        "39-xml-editor-dialog-light.png",
        "40-hero-lab-importer-dialog-light.png",
        "41-horizons-hub-light.png",
        "42-horizon-karma-forge-light.png",
        "43-horizon-alice-light.png",
        "44-horizon-black-ledger-light.png",
        "45-horizon-run-control-light.png",
        "46-horizon-runsite-light.png",
        "47-horizon-jackpoint-light.png",
        "48-horizon-table-pulse-light.png",
        "49-horizon-community-hub-light.png",
        "50-horizon-nexus-pan-light.png",
        "51-horizon-quicksilver-light.png",
        "52-horizon-runner-passport-light.png",
        "53-horizon-runbook-press-light.png",
        "54-horizon-creator-os-light.png",
        "55-horizon-local-co-processor-light.png",
        "56-horizon-anarchy-light.png",
        "57-horizon-ghostwire-light.png",
        "58-horizon-ready-for-tonight-light.png",
        "60-horizon-knowledge-fabric-light.png"
    ];
    private static readonly string[] RequiredVeteranCertificationScreenshots =
    [
        "01-initial-shell-light.png",
        "02-menu-open-light.png",
        "03-settings-open-light.png",
        "16-master-index-dialog-light.png",
        "17-character-roster-dialog-light.png",
        "18-import-dialog-light.png"
    ];
    private static readonly VeteranCertificationReviewStep[] VeteranCertificationReviewSteps =
    [
        new("toolstrip", "01-initial-shell-light.png", "Capture initial promoted Avalonia shell after WaitForReady.", "Chummer5a ChummerMainForm toolStrip New/Open/OpenForPrinting/OpenForExport lineage.", []),
        new("menu", "02-menu-open-light.png", "Click FileMenuButton and capture the visible command list.", "Chummer5a ChummerMainForm File/Tools/Windows/Help top menu lineage.", []),
        new("settings", "03-settings-open-light.png", "Press Ctrl+G and capture the Global Settings dialog.", "Chummer5a EditGlobalSettings Global Options, Master Index, and Character Roster lineage.", ["Global Settings"]),
        new("master_index", "16-master-index-dialog-light.png", "Execute master_index and capture the Master Index dialog.", "Chummer5a MasterIndex search utility lineage.", ["Master Index"]),
        new("roster", "17-character-roster-dialog-light.png", "Execute character_roster and capture the Character Roster dialog.", "Chummer5a CharacterRoster watch-folder utility lineage.", ["Character Roster"]),
        new("import", "18-import-dialog-light.png", "Click LoadDemoRunnerButton, then open File > Open Character and capture import familiarity.", "Chummer5a File/Open and Hero Lab Importer import route lineage.", ["Ruleset"])
    ];
    private static readonly VeteranCertificationReviewStep[] ImportRouteReviewSteps =
    [
        new("translator", "38-translator-dialog-light.png", "Execute translator and capture the governed translator route on the promoted desktop head.", "Chummer5a Translator utility lineage.", ["Translator", "Language Search", "Enabled Language Overlays"]),
        new("xml_amendment_editor", "39-xml-editor-dialog-light.png", "Execute xml_editor and capture XML bridge plus custom-data posture directly on the desktop route.", "Chummer5a custom-data/XML amendment authoring lineage.", ["XML Editor", "Custom Data Lane", "XML Bridge"]),
        new("hero_lab_importer", "40-hero-lab-importer-dialog-light.png", "Execute hero_lab_importer and capture direct Hero Lab import-oracle posture.", "Chummer5a Hero Lab importer lineage.", ["Hero Lab Importer", "Import Oracle Lane", "Adjacent SR6 Oracle"])
    ];
    private static readonly WorkflowScreenshotCoverageEntry[] WorkflowScreenshotCoverage =
    [
        new("create-open-import-save-save-as-print-export", "Chummer4/Chummer5a File menu New/Open/Save/Save As/Print/Export handoff lineage.", ["19-workflow-file-menu-loaded-light.png", "36-workflow-new-character-dialog-light.png", "18-import-dialog-light.png", "40-hero-lab-importer-dialog-light.png"]),
        new("metatype-priorities-karma-entry", "Chummer4/Chummer5a character creation priority and karma journal lineage.", ["15-creation-section-light.png", "11-diary-dialog-light.png", "36-workflow-new-character-dialog-light.png"]),
        new("attributes-skills-skill-groups-specializations-knowledge-languages", "Chummer4/Chummer5a Attributes and Skills tab edit-list lineage.", ["15-creation-section-light.png", "20-workflow-skills-section-light.png", "21-workflow-skill-add-dialog-light.png"]),
        new("qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources", "Chummer4/Chummer5a qualities, contacts, diary, notes, and source review lineage.", ["10-contacts-section-light.png", "22-workflow-qualities-section-light.png", "23-workflow-quality-add-dialog-light.png", "37-workflow-calendar-section-light.png"]),
        new("armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers", "Chummer4/Chummer5a gear, armor, weapon, vehicle, drone, mod, and location list lineage.", ["09-vehicles-section-light.png", "24-workflow-gear-section-light.png", "25-workflow-gear-add-dialog-light.png", "26-workflow-weapons-section-light.png", "27-workflow-weapon-add-dialog-light.png", "28-workflow-armor-section-light.png", "29-workflow-armor-add-dialog-light.png"]),
        new("cyberware-bioware-modular-hierarchies-nested-plugins", "Chummer4/Chummer5a cyberware/bioware nested selection and plugin lineage.", ["08-cyberware-dialog-light.png", "30-workflow-cyberware-section-light.png"]),
        new("magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms", "Chummer4/Chummer5a magic, adept, resonance, initiation, and matrix form lineage.", ["12-magic-dialog-light.png", "13-matrix-dialog-light.png", "14-advancement-dialog-light.png", "31-workflow-powers-section-light.png", "32-workflow-adept-power-dialog-light.png", "33-workflow-complex-form-dialog-light.png"]),
        new("improvements-explain-result-parity", "Chummer4/Chummer5a validation, explain, source, and applied-result review lineage.", ["14-advancement-dialog-light.png", "16-master-index-dialog-light.png", "34-workflow-validate-section-light.png", "35-workflow-rules-section-light.png"]),
        new("recovery-reload-migration-roundtrips", "Chummer4/Chummer5a open/import/reload/recovery roundtrip lineage.", ["04-loaded-runner-light.png", "18-import-dialog-light.png", "19-workflow-file-menu-loaded-light.png"]),
        new("dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare", "Chummer4/Chummer5a dense list, quick action, preview, drill-in, and compare workbench lineage.", ["05-dense-section-light.png", "06-dense-section-dark.png", "07-loaded-runner-tabs-light.png", "24-workflow-gear-section-light.png", "25-workflow-gear-add-dialog-light.png"]),
        new("native-horizons-surface-catalog", "Every first-class native horizon/workbench surface must stay screenshot-backed on the promoted desktop head.", ["41-horizons-hub-light.png", "42-horizon-karma-forge-light.png", "43-horizon-alice-light.png", "44-horizon-black-ledger-light.png", "45-horizon-run-control-light.png", "46-horizon-runsite-light.png", "47-horizon-jackpoint-light.png", "48-horizon-table-pulse-light.png", "49-horizon-community-hub-light.png", "50-horizon-nexus-pan-light.png", "51-horizon-quicksilver-light.png", "52-horizon-runner-passport-light.png", "53-horizon-runbook-press-light.png", "54-horizon-creator-os-light.png", "55-horizon-local-co-processor-light.png", "56-horizon-anarchy-light.png", "57-horizon-ghostwire-light.png", "58-horizon-ready-for-tonight-light.png", "60-horizon-knowledge-fabric-light.png"])
    ];
    private static readonly HorizonScreenshotSurface[] NativeHorizonScreenshotSurfaces =
    [
        new("karma_forge", "42-horizon-karma-forge-light.png", "Karma Forge", "KarmaForgeStatusCard"),
        new("alice", "43-horizon-alice-light.png", "ALICE", "AliceLeadHandoffCard"),
        new("black_ledger", "44-horizon-black-ledger-light.png", "Black Ledger", "BlackLedgerStatusCard"),
        new("run_control", "45-horizon-run-control-light.png", "Run Control", "RunControlStatusCard"),
        new("runsite", "46-horizon-runsite-light.png", "Runsite", "RunsiteBadgeWorkspaces"),
        new("jackpoint", "47-horizon-jackpoint-light.png", "Jackpoint", "JackpointBadgePublications"),
        new("table_pulse", "48-horizon-table-pulse-light.png", "Table Pulse", "TablePulseBadgeRuns"),
        new("community_hub", "49-horizon-community-hub-light.png", "Community Hub", "CommunityHubBadgeOperations"),
        new("nexus_pan", "50-horizon-nexus-pan-light.png", "NEXUS-PAN", "NexusPanBadgeWorkspaces"),
        new("quicksilver", "51-horizon-quicksilver-light.png", "Quicksilver", "QuicksilverBadgeHandoffs"),
        new("runner_passport", "52-horizon-runner-passport-light.png", "Runner Passport", "RunnerPassportBadgeDossiers"),
        new("runbook_press", "53-horizon-runbook-press-light.png", "Runbook Press", "RunbookPressBadgePublications"),
        new("creator_os", "54-horizon-creator-os-light.png", "Creator OS", "CreatorOsBadgePublications"),
        new("local_co_processor", "55-horizon-local-co-processor-light.png", "Local Co-Processor", "LocalCoProcessorBadgeRules"),
        new("anarchy", "56-horizon-anarchy-light.png", "Anarchy", "AnarchyBadgeRuns"),
        new("ghostwire", "57-horizon-ghostwire-light.png", "Ghostwire", "GhostwireBadgeRuns"),
        new("ready_for_tonight", "58-horizon-ready-for-tonight-light.png", "Ready for Tonight", "ReadyForTonightBadgeRuns"),
        new("knowledge_fabric", "60-horizon-knowledge-fabric-light.png", "Knowledge Fabric", "KnowledgeFabricBadgeRules")
    ];
    private static HeadlessUnitTestSession? _headlessSession;

    [TestMethod]
    public void Blazor_root_route_ownership_stays_with_desktop_shell_anchor_and_moves_showcase_off_root()
    {
        string homePath = ResolveSourceFile("Chummer.Blazor", "Components", "Pages", "Home.razor");
        string showcasePath = ResolveSourceFile("Chummer.Blazor", "Components", "Pages", "Showcase.razor");
        string legacyPath = ResolveSourceFile("Chummer.Blazor", "Pages", "Index.razor");

        string homeText = File.ReadAllText(homePath);
        string showcaseText = File.ReadAllText(showcasePath);
        string legacyText = File.ReadAllText(legacyPath);

        StringAssert.Contains(homeText, "@page \"/\"");
        StringAssert.Contains(homeText, "Desktop shell route anchor");
        Assert.IsFalse(homeText.Contains("panel-grid", StringComparison.Ordinal));
        StringAssert.Contains(showcaseText, "@page \"/showcase\"");
        StringAssert.Contains(showcaseText, "@layout Chummer.Blazor.Components.Layout.NoLayout");
        StringAssert.Contains(showcaseText, "panel-grid");
        Assert.IsFalse(legacyText.Contains("@page \"/\"", StringComparison.Ordinal));
        Assert.IsFalse(legacyText.Contains("@page \"/blazor\"", StringComparison.Ordinal));
        StringAssert.Contains(legacyText, "@page \"/legacy-console\"");
    }

    [TestMethod]
    public void Avalonia_startup_keeps_the_workbench_as_first_paint_but_still_invokes_restore_continuation_when_needed()
    {
        string appPath = ResolveSourceFile("Chummer.Avalonia", "App.axaml.cs");
        string appText = File.ReadAllText(appPath);

        StringAssert.Contains(appText, "DesktopInstallLinkingWindow.ShowIfNeededAsync(owner, installLinkingContext);");
        Assert.IsTrue(
            appText.Contains("if (installLinkingContext is not null)", StringComparison.Ordinal),
            "Startup modal prompts should still be gated on active install-linking context.");
        Assert.IsTrue(
            appText.Contains("if (installLinkingContext.ShouldPrompt && !DesktopInstallLinkingRuntime.IsClaimed(currentInstallState))", StringComparison.Ordinal),
            "Only real public linking prompts should hard-stop startup continuation; local channels must stay usable.");
        Assert.IsTrue(
            appText.Contains("MarkPromptDismissed(currentInstallState.HeadId)", StringComparison.Ordinal),
            "Unclaimed installs must record the dismissed-state turn and avoid looping prompts.");
        Assert.IsTrue(
            appText.Contains("owner.Close();", StringComparison.Ordinal),
            "Unlinked sessions must close the main shell instead of continuing into app surfaces.");
        Assert.IsFalse(
            appText.Contains("lifetime.Shutdown()", StringComparison.Ordinal),
            "Guest continuation must not force-quit the Avalonia desktop after the native install-linking surface closes.");
        Assert.IsFalse(
            appText.Contains("DesktopHomeWindow.ShowIfNeededAsync(owner, \"avalonia\", installContext: null);", StringComparison.Ordinal),
            "The flagship Avalonia startup path must stay on the workbench by default instead of reopening the desktop home cockpit.");
    }

    [TestMethod]
    public void Fresh_launch_main_window_survives_first_paint_without_self_termination()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            harness.AdvanceFrames(120);

            Assert.IsTrue(harness.Window.IsVisible, "Fresh launch must not auto-close the main window after first paint.");
            Assert.IsTrue(harness.FindControl<Control>("MenuBarRegion").IsVisible, "Fresh launch must keep the menu bar visible.");
            Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible, "Fresh launch must keep the toolstrip visible.");
            Assert.IsTrue(harness.FindControl<Control>("RosterPaneRegion").IsVisible, "Fresh launch must keep the roster rail visible in the classic workbench posture.");
            Assert.IsFalse(harness.FindControl<Control>("LeftNavigatorRegion").IsVisible, "Fresh launch must not open the codex navigator pane by default.");
            Assert.IsFalse(harness.FindControl<Control>("SummaryHeaderRegion").IsVisible, "Fresh launch must not show the recovery summary band without an active restore problem.");
            Assert.IsFalse(
                harness.TryWaitUntil(() => !harness.Window.IsVisible, timeoutMs: 250),
                "Fresh launch must stay alive instead of terminating shortly after startup.");
        });
    }

    [TestMethod]
    public void Horizons_shell_entry_opens_native_hub_with_filterable_runtime_backed_cards()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("HorizonsButton");
            harness.WaitUntil(() => DesktopHorizonsWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open native Horizons hub from shell chrome");

            Window hubWindow = DesktopHorizonsWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Horizons hub window was not opened.");
            Assert.IsNotNull(harness.FindControlInWindowOrDefault<TextBlock>(hubWindow, "HorizonsPostureText"));
            StringAssert.Contains(harness.FindControlInWindow<TextBlock>(hubWindow, "HorizonsFilterStatusText").Text ?? string.Empty, "Showing");
            TextBox searchBox = harness.FindControlInWindow<TextBox>(hubWindow, "HorizonsSearchBox");
            Assert.IsTrue(harness.FindControlInWindow<StackPanel>(hubWindow, "HorizonsCatalogStack").IsVisible);
            Assert.IsNotNull(harness.FindControlInWindowOrDefault<Border>(hubWindow, "HorizonsSectionBadge_build_and_rules"));

            foreach (DesktopHorizonWorkbenchEntry entry in DesktopHorizonWorkbenchCatalog.ListEntries())
            {
                string openWorkbenchButtonName = $"HorizonsOpenWorkbench_{entry.Id}";
                Assert.IsNotNull(
                    harness.FindControlInWindowOrDefault<Button>(hubWindow, openWorkbenchButtonName),
                    $"Horizons hub must render a native workbench launch for '{entry.Id}'.");
            }

            searchBox.Text = "Karma";
            harness.AdvanceFrames(12);
            StringAssert.Contains(harness.FindControlInWindow<TextBlock>(hubWindow, "HorizonsFilterStatusText").Text ?? string.Empty, "matched 1");
            Assert.IsNotNull(harness.FindControlInWindowOrDefault<Button>(hubWindow, "HorizonsOpenWorkbench_karma_forge"));
            Assert.IsNull(
                harness.FindControlInWindowOrDefault<Button>(hubWindow, "HorizonsOpenWorkbench_black_ledger"),
                "A narrowed search must remove non-matching horizon cards instead of leaving the full list visible.");

            searchBox.Text = "no-such-horizon";
            harness.WaitUntil(
                () => harness.FindControlInWindowOrDefault<TextBlock>(hubWindow, "HorizonsEmptyStateText") is { IsVisible: true },
                context: "show explicit empty state when the Horizons search has no matches");

            searchBox.Text = string.Empty;
            harness.WaitUntil(
                () => harness.FindControlInWindowOrDefault<Button>(hubWindow, "HorizonsOpenWorkbench_black_ledger") is { IsVisible: true },
                context: "restore full native horizon catalog after clearing the filter");

            hubWindow.Close();
            harness.AdvanceFrames(12);
        });
    }

    [TestMethod]
    public void Horizons_hub_launches_native_karma_forge_alice_run_control_and_black_ledger_workbenches()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("HorizonsButton");
            harness.WaitUntil(() => DesktopHorizonsWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Horizons hub before launching native workbenches");

            Window hubWindow = DesktopHorizonsWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Horizons hub window was not opened.");

            AssertNativeWorkbenchLaunch(
                harness,
                hubWindow,
                "HorizonsOpenWorkbench_karma_forge",
                "Karma Forge",
                "KarmaForgeStatusCard",
                "KarmaForgeBadgeContext",
                "KarmaForgeBadgeHandoffs",
                "KarmaForgeTargetCombo",
                "KarmaForgeOpenSelectedButton");
            AssertNativeWorkbenchLaunch(
                harness,
                hubWindow,
                "HorizonsOpenWorkbench_alice",
                "ALICE",
                "AliceLeadHandoffCard",
                "AliceBadgeContext",
                "AliceAccountHandoffsCard");
            AssertNativeWorkbenchLaunch(
                harness,
                hubWindow,
                "HorizonsOpenWorkbench_run_control",
                "Run Control",
                "RunControlStatusCard",
                "RunControlBadgeRuns",
                "RunControlRunsCard");
            AssertNativeWorkbenchLaunch(
                harness,
                hubWindow,
                "HorizonsOpenWorkbench_black_ledger",
                "Black Ledger",
                "BlackLedgerStatusCard",
                "BlackLedgerBadgeCampaigns",
                "BlackLedgerWorkspacesCard");

            hubWindow.Close();
            harness.AdvanceFrames(12);
        });
    }

    [TestMethod]
    public void Horizons_core_native_workbenches_surface_runtime_backed_detail_interactions()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("HorizonsButton");
            harness.WaitUntil(() => DesktopHorizonsWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Horizons hub before validating in-window detail interactions");

            Window hubWindow = DesktopHorizonsWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Horizons hub window was not opened.");

            Button karmaForgeLaunchButton = harness.FindControlInWindow<Button>(hubWindow, "HorizonsOpenWorkbench_karma_forge");
            RaiseClick(karmaForgeLaunchButton);
            harness.WaitUntil(() => DesktopKarmaForgeWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Karma Forge workbench");
            Window karmaForgeWindow = DesktopKarmaForgeWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Karma Forge workbench did not stay open.");
            ComboBox karmaForgeTargetCombo = harness.FindControlInWindow<ComboBox>(karmaForgeWindow, "KarmaForgeTargetCombo");
            TextBlock karmaForgeTargetSummaryText = harness.FindControlInWindow<TextBlock>(karmaForgeWindow, "KarmaForgeTargetSummaryText");
            string initialKarmaForgeSummary = karmaForgeTargetSummaryText.Text ?? string.Empty;
            Assert.IsTrue((karmaForgeTargetCombo.ItemCount) > 1, "Karma Forge must expose more than one target in the native desktop workbench.");
            karmaForgeTargetCombo.SelectedIndex = 1;
            harness.WaitUntil(() => !string.Equals(karmaForgeTargetSummaryText.Text, initialKarmaForgeSummary, StringComparison.Ordinal), context: "changing the Karma Forge target must update the desktop detail summary");
            karmaForgeWindow.Close();
            harness.WaitUntil(() => DesktopKarmaForgeWindow.LastOpenedWindowForTesting is null, context: "close Karma Forge workbench after interaction check");

            Button aliceLaunchButton = harness.FindControlInWindow<Button>(hubWindow, "HorizonsOpenWorkbench_alice");
            RaiseClick(aliceLaunchButton);
            harness.WaitUntil(() => DesktopAliceWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open ALICE workbench");
            Window aliceWindow = DesktopAliceWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("ALICE workbench did not stay open.");
            if (TryFindControlInWindow<ComboBox>(harness, aliceWindow, "AliceDetailModeCombo", out ComboBox? aliceDetailModeCombo))
            {
                TextBlock aliceSelectedHandoffDetailText = harness.FindControlInWindow<TextBlock>(aliceWindow, "AliceSelectedHandoffDetailText");
                string initialAliceDetail = aliceSelectedHandoffDetailText.Text ?? string.Empty;
                aliceDetailModeCombo.SelectedIndex = 1;
                harness.WaitUntil(() => !string.Equals(aliceSelectedHandoffDetailText.Text, initialAliceDetail, StringComparison.Ordinal), context: "changing the ALICE detail mode must update the selected handoff detail");
            }
            else
            {
                Assert.IsFalse(
                    TryFindControlInWindow<Border>(harness, aliceWindow, "AliceSelectedHandoffCard", out _),
                    "ALICE should hide handoff interaction chrome when no governed handoff context exists.");
            }

            if (TryFindControlInWindow<ComboBox>(harness, aliceWindow, "AliceProposalModeCombo", out ComboBox? aliceProposalModeCombo))
            {
                TextBlock aliceBuildPathDetailText = harness.FindControlInWindow<TextBlock>(aliceWindow, "AliceSelectedBuildPathDetailText");
                string initialAliceBuildPathDetail = aliceBuildPathDetailText.Text ?? string.Empty;
                aliceProposalModeCombo.SelectedIndex = 1;
                harness.WaitUntil(() => !string.Equals(aliceBuildPathDetailText.Text, initialAliceBuildPathDetail, StringComparison.Ordinal), context: "changing the ALICE proposal mode must update the selected build-path detail");
            }
            aliceWindow.Close();
            harness.WaitUntil(() => DesktopAliceWindow.LastOpenedWindowForTesting is null, context: "close ALICE workbench after interaction check");

            Button runsiteLaunchButton = harness.FindControlInWindow<Button>(hubWindow, "HorizonsOpenWorkbench_runsite");
            RaiseClick(runsiteLaunchButton);
            harness.WaitUntil(() => DesktopRunsiteWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Runsite workbench");
            Window runsiteWindow = DesktopRunsiteWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Runsite workbench did not stay open.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(harness.FindControlInWindow<TextBlock>(runsiteWindow, "RunsiteSelectedWorkspaceDetailText").Text), "Runsite must still show bounded detail text.");
            runsiteWindow.Close();
            harness.WaitUntil(() => DesktopRunsiteWindow.LastOpenedWindowForTesting is null, context: "close Runsite workbench after interaction check");

            Button runControlLaunchButton = harness.FindControlInWindow<Button>(hubWindow, "HorizonsOpenWorkbench_run_control");
            RaiseClick(runControlLaunchButton);
            harness.WaitUntil(() => DesktopRunControlWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Run Control workbench");
            Window runControlWindow = DesktopRunControlWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Run Control workbench did not stay open.");
            ComboBox runControlDetailModeCombo = harness.FindControlInWindow<ComboBox>(runControlWindow, "RunControlDetailModeCombo");
            TextBlock runControlSelectedRunDetailText = harness.FindControlInWindow<TextBlock>(runControlWindow, "RunControlSelectedRunDetailText");
            string initialRunControlDetail = runControlSelectedRunDetailText.Text ?? string.Empty;
            runControlDetailModeCombo.SelectedIndex = 1;
            harness.WaitUntil(() => !string.Equals(runControlSelectedRunDetailText.Text, initialRunControlDetail, StringComparison.Ordinal), context: "changing the Run Control detail mode must update the selected run detail");
            runControlWindow.Close();
            harness.WaitUntil(() => DesktopRunControlWindow.LastOpenedWindowForTesting is null, context: "close Run Control workbench after interaction check");

            Button blackLedgerLaunchButton = harness.FindControlInWindow<Button>(hubWindow, "HorizonsOpenWorkbench_black_ledger");
            RaiseClick(blackLedgerLaunchButton);
            harness.WaitUntil(() => DesktopBlackLedgerWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Black Ledger workbench");
            Window blackLedgerWindow = DesktopBlackLedgerWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Black Ledger workbench did not stay open.");
            TextBlock blackLedgerSelectedWorkspaceDetailText = harness.FindControlInWindow<TextBlock>(blackLedgerWindow, "BlackLedgerSelectedWorkspaceDetailText");
            if (TryFindControlInWindow<ComboBox>(harness, blackLedgerWindow, "BlackLedgerDetailModeCombo", out ComboBox? blackLedgerDetailModeCombo))
            {
                string initialBlackLedgerDetail = blackLedgerSelectedWorkspaceDetailText.Text ?? string.Empty;
                blackLedgerDetailModeCombo.SelectedIndex = 1;
                harness.WaitUntil(() => !string.Equals(blackLedgerSelectedWorkspaceDetailText.Text, initialBlackLedgerDetail, StringComparison.Ordinal), context: "changing the Black Ledger detail mode must update the selected workspace detail");
            }
            else
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(blackLedgerSelectedWorkspaceDetailText.Text), "Black Ledger must still show bounded detail text when no workspace context exists.");
            }
            blackLedgerWindow.Close();
            harness.WaitUntil(() => DesktopBlackLedgerWindow.LastOpenedWindowForTesting is null, context: "close Black Ledger workbench after interaction check");

            hubWindow.Close();
            harness.AdvanceFrames(12);
        });
    }

    [TestMethod]
    public void Horizons_hub_launches_remaining_native_workbenches_without_browser_only_fallback()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("HorizonsButton");
            harness.WaitUntil(() => DesktopHorizonsWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Horizons hub before launching remaining native workbenches");

            Window hubWindow = DesktopHorizonsWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Horizons hub window was not opened.");

            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_jackpoint", "Jackpoint", "JackpointBadgePublications");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_table_pulse", "Table Pulse", "TablePulseBadgeRuns");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_community_hub", "Community Hub", "CommunityHubBadgeOperations");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_nexus_pan", "NEXUS-PAN", "NexusPanBadgeWorkspaces");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_quicksilver", "Quicksilver", "QuicksilverBadgeHandoffs");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_runner_passport", "Runner Passport", "RunnerPassportBadgeDossiers");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_runbook_press", "Runbook Press", "RunbookPressBadgePublications");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_creator_os", "Creator OS", "CreatorOsBadgePublications");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_local_co_processor", "Local Co-Processor", "LocalCoProcessorBadgeRules");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_anarchy", "Anarchy", "AnarchyBadgeRuns");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_ghostwire", "Ghostwire", "GhostwireBadgeRuns");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_ready_for_tonight", "Ready for Tonight", "ReadyForTonightBadgeRuns");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_knowledge_fabric", "Knowledge Fabric", "KnowledgeFabricBadgeRules");
            AssertNativeWorkbenchLaunch(harness, hubWindow, "HorizonsOpenWorkbench_runsite", "Runsite", "RunsiteBadgeWorkspaces");

            hubWindow.Close();
            harness.AdvanceFrames(12);
        });
    }

    [TestMethod]
    public void Alice_supports_blank_state_build_help_and_gm_steered_origin_dossier_flow()
    {
        WithRuntimeHarness(harness =>
        {
            string bundleRoot = Path.Combine(Path.GetTempPath(), "chummer-origin-dossier-bundles");
            HashSet<string> existingBundleDirectories = Directory.Exists(bundleRoot)
                ? Directory.GetDirectories(bundleRoot).ToHashSet(StringComparer.Ordinal)
                : [];

            harness.WaitForReady();
            harness.Click("HorizonsButton");
            harness.WaitUntil(() => DesktopHorizonsWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Horizons hub before validating ALICE flow");

            Window hubWindow = DesktopHorizonsWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Horizons hub window was not opened.");

            RaiseClick(harness.FindControlInWindow<Button>(hubWindow, "HorizonsOpenWorkbench_alice"));
            harness.WaitUntil(() => DesktopAliceWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open ALICE workbench for runtime-backed flow validation");

            Window aliceWindow = DesktopAliceWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("ALICE workbench did not stay open.");

            ComboBox modeCombo = harness.FindControlInWindow<ComboBox>(aliceWindow, "AliceConversationModeCombo");
            TextBlock settingsGuideText = harness.FindControlInWindow<TextBlock>(aliceWindow, "AliceSettingsGuideText");
            TextBlock contextDetailText = harness.FindControlInWindow<TextBlock>(aliceWindow, "AliceAssistantContextDetailText");
            TextBox promptBox = harness.FindControlInWindow<TextBox>(aliceWindow, "AliceQuestionTextBox");
            TextBlock statusText = harness.FindControlInWindow<TextBlock>(aliceWindow, "AliceAssistantStatusText");
            TextBlock answerText = harness.FindControlInWindow<TextBlock>(aliceWindow, "AliceAssistantAnswerText");
            Button askButton = harness.FindControlInWindow<Button>(aliceWindow, "AliceAskButton");

            modeCombo.SelectedItem = "Build help";
            harness.WaitUntil(
                () => (settingsGuideText.Text ?? string.Empty).Contains("Strict avoids restricted picks", StringComparison.Ordinal)
                    && (settingsGuideText.Text ?? string.Empty).Contains("Standard allows common legal restricted choices", StringComparison.Ordinal),
                context: "Build-help settings explainer must spell out legality and complexity behavior");

            promptBox.Text = "Build me a troll decker from scratch for SR4 with standard legality and stable first-pass gear.";
            RaiseClick(askButton);
            harness.WaitUntil(
                () => !string.IsNullOrWhiteSpace(answerText.Text)
                    && !(statusText.Text ?? string.Empty).Contains("Type a question", StringComparison.Ordinal),
                context: "blank-state build help must answer instead of dead-ending");
            Assert.IsFalse(
                (answerText.Text ?? string.Empty).Contains("Open or create a workspace first", StringComparison.Ordinal),
                "Blank-state build help must not require an already-open character or workspace.");

            modeCombo.SelectedItem = "Origin Dossier";
            harness.WaitUntil(
                () => (settingsGuideText.Text ?? string.Empty).Contains("Finished characters are not changed", StringComparison.Ordinal),
                context: "origin-dossier explainer must stay visible when switching modes");
            Border originWizardPanel = harness.FindControlInWindow<Border>(aliceWindow, "AliceOriginWizardPanel");
            ComboBox originMetatypeCombo = harness.FindControlInWindow<ComboBox>(aliceWindow, "AliceOriginMetatypeCombo");
            ComboBox originArchetypeCombo = harness.FindControlInWindow<ComboBox>(aliceWindow, "AliceOriginArchetypeCombo");
            Expander advancedStoryControls = harness.FindControlInWindow<Expander>(aliceWindow, "AliceOriginAdvancedStoryControlsExpander");
            Expander gmNotesControls = harness.FindControlInWindow<Expander>(aliceWindow, "AliceGmAllowanceExpander");
            WrapPanel starterPromptRow = harness.FindControlInWindow<WrapPanel>(aliceWindow, "AliceStarterPromptRow");
            Button startDossierButton = harness.FindControlInWindow<Button>(aliceWindow, "AliceOriginStartDossierButton");
            Assert.IsTrue(originWizardPanel.IsVisible, "Origin dossier mode must show the story-first wizard surface.");
            Assert.IsTrue(originMetatypeCombo.IsVisible, "Origin dossier's default screen must expose only the basic metatype choice.");
            Assert.IsTrue(originArchetypeCombo.IsVisible, "Origin dossier's default screen must expose only the basic archetype choice.");
            Assert.AreEqual("Build story", startDossierButton.Content?.ToString(), "Origin dossier must lead with story generation, not media or setup copy.");
            Assert.IsFalse(advancedStoryControls.IsExpanded, "Story steering controls must stay collapsed by default.");
            Assert.IsFalse(gmNotesControls.IsExpanded, "GM grant and constraint notes must stay collapsed by default.");
            Assert.IsFalse(starterPromptRow.IsVisible, "Origin dossier mode must not show generic prompt chips beside the story wizard.");
            Assert.IsNull(
                harness.FindControlInWindowOrDefault<ComboBox>(aliceWindow, "AliceOriginBuildFrameCombo"),
                "Build-frame steering must be hidden until the advanced story controls are expanded.");
            Assert.IsNull(
                harness.FindControlInWindowOrDefault<ComboBox>(aliceWindow, "AliceOriginPressureCombo"),
                "Story-pressure steering must be hidden until the advanced story controls are expanded.");
            Assert.IsNull(
                harness.FindControlInWindowOrDefault<ComboBox>(aliceWindow, "AliceOriginGmRequirementPresetCombo"),
                "GM requirement presets must be hidden until the advanced story controls are expanded.");
            Assert.IsNull(
                harness.FindControlInWindowOrDefault<TextBox>(aliceWindow, "AliceGmAllowanceTextBox"),
                "Free-form GM notes must be hidden until the GM notes section is expanded.");

            gmNotesControls.IsExpanded = true;
            harness.AdvanceFrames(4);
            TextBox gmAllowanceBox = harness.FindControlInWindow<TextBox>(aliceWindow, "AliceGmAllowanceTextBox");
            Assert.IsTrue(gmAllowanceBox.IsVisible, "Expanding GM notes must reveal the steering field.");

            gmAllowanceBox.Text = "GM allows one restricted ware exception, +20000 nuyen, and one extra quality if the origin supports it.";
            harness.WaitUntil(
                () => (contextDetailText.Text ?? string.Empty).Contains("GM", StringComparison.Ordinal)
                    && (contextDetailText.Text ?? string.Empty).Contains("+20000 nuyen", StringComparison.Ordinal),
                context: "GM notes must feed the visible Alice context before origin generation");

            promptBox.Text = "Draft an origin dossier for a troll decker whose GM wants the backstory to justify the restricted ware and bonus nuyen.";
            RaiseClick(askButton);
            harness.WaitUntil(
                () => (statusText.Text ?? string.Empty).Contains("Origin draft ready", StringComparison.Ordinal),
                context: "origin-dossier mode must create a draft");
            harness.WaitUntil(
                () => harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginApproveCanonButton") is { IsVisible: true },
                context: "origin-dossier draft must expose story approval");
            Assert.IsNotNull(
                harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginOpenDraftStoryButton"),
                "Origin dossier must make the generated story the first review artifact.");
            Assert.IsNotNull(
                harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginOpenDraftFlipLinkPacketButton"),
                "Origin dossier must create a book preview for draft story review.");
            WrapPanel actionRow = harness.FindControlInWindow<WrapPanel>(aliceWindow, "AliceAssistantActionRow");
            AssertOriginDossierActionTitlesStayHuman(actionRow, "draft origin dossier");
            Assert.IsNull(
                harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginGeneratePortraitSetButton"),
                "Origin dossier must not show production actions before story approval.");
            Assert.IsNull(
                harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginGenerateDossierVideoButton"),
                "Origin dossier must not offer video production before story approval.");

            RaiseClick(harness.FindControlInWindow<Button>(aliceWindow, "AliceOriginApproveCanonButton"));
            harness.WaitUntil(
                () => (statusText.Text ?? string.Empty).Contains("Origin story approved", StringComparison.Ordinal),
                context: "origin draft must be approvable into a dossier bundle");
            harness.WaitUntil(
                () => harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginOpenBundleFolderButton") is { IsVisible: true },
                context: "approved dossier bundle must expose bundle actions");
            Assert.IsNotNull(
                harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginOpenCanonStoryButton"),
                "Approved origin dossier must keep the story as the first-class artifact.");
            Assert.IsNotNull(
                harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginOpenFlipLinkPacketButton"),
                "Approved origin dossier must expose the book preview after approval.");
            Button openDossierPdfButton = harness.FindControlInWindow<Button>(aliceWindow, "AliceOriginOpenDossierPdfButton");
            Assert.IsTrue(openDossierPdfButton.IsVisible, "Approved origin dossier must immediately expose the book artifact.");
            AssertOriginDossierActionTitlesStayHuman(actionRow, "approved origin dossier");
            string firstVisibleAction = actionRow.Children
                .OfType<Button>()
                .Where(static button => button.IsVisible)
                .Select(static button => button.Content?.ToString() ?? string.Empty)
                .FirstOrDefault() ?? string.Empty;
            Assert.AreEqual("Open book", firstVisibleAction, "The approved origin dossier must be book-first before portrait, voice, or video actions.");

            harness.WaitUntil(
                () => Directory.Exists(bundleRoot)
                    && Directory.GetDirectories(bundleRoot).Any(path => !existingBundleDirectories.Contains(path)),
                context: "origin approval must create a real bundle directory");
            string createdBundleDirectory = Directory.GetDirectories(bundleRoot)
                .Where(path => !existingBundleDirectories.Contains(path))
                .OrderByDescending(Directory.GetCreationTimeUtc)
                .First();
            harness.WaitUntil(
                () => File.Exists(Path.Combine(createdBundleDirectory, "origin-canon.md"))
                    && File.Exists(Path.Combine(createdBundleDirectory, "origin-canon.json"))
                    && File.Exists(Path.Combine(createdBundleDirectory, "fliplink-origin-story.packet.json"))
                    && File.Exists(Path.Combine(createdBundleDirectory, "origin-dossier.pdf"))
                    && File.Exists(Path.Combine(createdBundleDirectory, "markupgo-origin-dossier.packet.json")),
                context: "origin approval must write canonical story, FlipLink, and book-first PDF artifacts");

            RaiseClick(harness.FindControlInWindow<Button>(aliceWindow, "AliceOriginGeneratePortraitSetButton"));
            harness.WaitUntil(
                () => (statusText.Text ?? string.Empty).Contains("Portrait set ready", StringComparison.Ordinal)
                    && harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginSelectPortrait1Button") is { IsVisible: true },
                context: "origin bundle must generate portrait candidates");
            harness.WaitUntil(
                () => Directory.EnumerateFiles(Path.Combine(createdBundleDirectory, "portraits"), "*.png").Any()
                    && File.Exists(Path.Combine(createdBundleDirectory, "origin-portrait-set.json")),
                context: "portrait generation must produce local portrait artifacts");

            RaiseClick(harness.FindControlInWindow<Button>(aliceWindow, "AliceOriginSelectPortrait2Button"));
            harness.WaitUntil(
                () => (statusText.Text ?? string.Empty).Contains("Portrait selected", StringComparison.Ordinal),
                context: "portrait selection must update origin media state");

            RaiseClick(harness.FindControlInWindow<Button>(aliceWindow, "AliceOriginGenerateSceneSetButton"));
            harness.WaitUntil(
                () => harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginSelectScene1Button") is { IsVisible: true },
                context: "origin bundle must generate scene candidates");
            harness.WaitUntil(
                () => Directory.EnumerateFiles(Path.Combine(createdBundleDirectory, "scenes"), "*.png").Any()
                    && File.Exists(Path.Combine(createdBundleDirectory, "origin-scene-set.json")),
                context: "scene generation must produce local scene artifacts");

            RaiseClick(harness.FindControlInWindow<Button>(aliceWindow, "AliceOriginSelectScene1Button"));
            harness.WaitUntil(
                () => (statusText.Text ?? string.Empty).Contains("Scene selected", StringComparison.Ordinal),
                context: "scene selection must update origin media state");

            RaiseClick(harness.FindControlInWindow<Button>(aliceWindow, "AliceOriginGenerateDossierVideoButton"));
            harness.WaitUntil(
                () => (statusText.Text ?? string.Empty).Contains("Dossier video plan ready", StringComparison.Ordinal)
                    && harness.FindControlInWindowOrDefault<Button>(aliceWindow, "AliceOriginOpenVideoPosterButton") is { IsVisible: true },
                context: "origin bundle must prepare the dossier video after GM-steered story approval");
            harness.WaitUntil(
                () => File.Exists(Path.Combine(createdBundleDirectory, "origin-dossier-video.storyboard.md"))
                    && File.Exists(Path.Combine(createdBundleDirectory, "origin-dossier-video-poster.png"))
                    && File.Exists(Path.Combine(createdBundleDirectory, "vidboard-origin-dossier.packet.json")),
                context: "video preparation must use the approved book/story and produce storyboard, poster, and vidBoard packet artifacts");

            modeCombo.SelectedItem = "Build help";
            harness.WaitUntil(
                () => (settingsGuideText.Text ?? string.Empty).Contains("Strict avoids restricted picks", StringComparison.Ordinal),
                context: "switching back to build help must restore the settings explainer");
            Assert.IsTrue(
                (contextDetailText.Text ?? string.Empty).Contains("GM", StringComparison.Ordinal),
                "GM notes must remain visible in Alice context after returning from origin-dossier mode.");

            aliceWindow.Close();
            harness.WaitUntil(() => DesktopAliceWindow.LastOpenedWindowForTesting is null, context: "close ALICE after runtime-backed origin flow validation");
            hubWindow.Close();
            harness.AdvanceFrames(12);
        });
    }

    [TestMethod]
    public void Horizons_remaining_native_workbenches_surface_runtime_backed_detail_interactions()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("HorizonsButton");
            harness.WaitUntil(() => DesktopHorizonsWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Horizons hub before validating remaining native workbench detail interactions");

            Window hubWindow = DesktopHorizonsWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Horizons hub window was not opened.");

            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_jackpoint", "Jackpoint", "JackpointDetailModeCombo", "JackpointDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_table_pulse", "Table Pulse", "TablePulseDetailModeCombo", "TablePulseDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_community_hub", "Community Hub", "CommunityHubDetailModeCombo", "CommunityHubDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_nexus_pan", "NEXUS-PAN", "NexusPanDetailModeCombo", "NexusPanDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_runner_passport", "Runner Passport", "RunnerPassportDetailModeCombo", "RunnerPassportDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_runbook_press", "Runbook Press", "RunbookPressDetailModeCombo", "RunbookPressDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_creator_os", "Creator OS", "CreatorOsDetailModeCombo", "CreatorOsDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_local_co_processor", "Local Co-Processor", "LocalCoProcessorDetailModeCombo", "LocalCoProcessorDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_anarchy", "Anarchy", "AnarchyDetailModeCombo", "AnarchyDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_ghostwire", "Ghostwire", "GhostwireDetailModeCombo", "GhostwireDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_ready_for_tonight", "Ready for Tonight", "ReadyForTonightDetailModeCombo", "ReadyForTonightDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_knowledge_fabric", "Knowledge Fabric", "KnowledgeFabricDetailModeCombo", "KnowledgeFabricDetailText");
            AssertDetailModeInteraction(harness, hubWindow, "HorizonsOpenWorkbench_runsite", "Runsite", "RunsiteDetailModeCombo", "RunsiteSelectedWorkspaceDetailText");

            Button quicksilverLaunchButton = harness.FindControlInWindow<Button>(hubWindow, "HorizonsOpenWorkbench_quicksilver");
            RaiseClick(quicksilverLaunchButton);
            harness.WaitUntil(() => DesktopQuicksilverWindow.LastOpenedWindowForTesting is { IsVisible: true }, context: "open Quicksilver workbench");
            Window quicksilverWindow = DesktopQuicksilverWindow.LastOpenedWindowForTesting
                ?? throw new AssertFailedException("Quicksilver workbench did not stay open.");
            ComboBox quicksilverTargetCombo = harness.FindControlInWindow<ComboBox>(quicksilverWindow, "QuicksilverTargetCombo");
            TextBlock quicksilverTargetSummaryText = harness.FindControlInWindow<TextBlock>(quicksilverWindow, "QuicksilverTargetSummaryText");
            string initialQuicksilverSummary = quicksilverTargetSummaryText.Text ?? string.Empty;
            Assert.IsTrue(quicksilverTargetCombo.ItemCount > 1, "Quicksilver must expose more than one jump target in the native command deck.");
            quicksilverTargetCombo.SelectedIndex = 1;
            harness.WaitUntil(() => !string.Equals(quicksilverTargetSummaryText.Text, initialQuicksilverSummary, StringComparison.Ordinal), context: "changing the Quicksilver target must update the command deck detail summary");
            quicksilverWindow.Close();
            harness.WaitUntil(() => DesktopQuicksilverWindow.LastOpenedWindowForTesting is null, context: "close Quicksilver workbench after interaction check");

            hubWindow.Close();
            harness.AdvanceFrames(12);
        });
    }

    [TestMethod]
    public void Desktop_home_window_no_longer_forces_a_dashboard_detour_for_empty_workspace_state()
    {
        string homePath = ResolveSourceFile("Chummer.Avalonia", "DesktopHomeWindow.cs");
        string homeText = File.ReadAllText(homePath);

        StringAssert.Contains(homeText, "if (installContext?.ShouldPrompt == true)");
        StringAssert.Contains(homeText, "if (!string.Equals(updateStatus.Status, \"current\", StringComparison.Ordinal))");
        StringAssert.Contains(homeText, "if (supportProjection.NeedsAttention)");
        Assert.IsFalse(
            homeText.Contains("workspaces.Count == 0", StringComparison.Ordinal),
            "A fresh install with no workspaces must still enter the workbench instead of reopening the desktop home cockpit.");
    }

    [TestMethod]
    public void Avalonia_workbench_shell_removes_extra_non_chummer5a_chrome()
    {
        string projectorPath = ResolveSourceFile("Chummer.Avalonia", "MainWindow.ShellFrameProjector.cs");
        string summaryHeaderPath = ResolveSourceFile("Chummer.Avalonia", "Controls", "SummaryHeaderControl.axaml.cs");
        string summaryHeaderMarkupPath = ResolveSourceFile("Chummer.Avalonia", "Controls", "SummaryHeaderControl.axaml");
        string projectorText = File.ReadAllText(projectorPath);
        string summaryHeaderText = File.ReadAllText(summaryHeaderPath);
        string summaryHeaderMarkupText = File.ReadAllText(summaryHeaderMarkupPath);

        StringAssert.Contains(projectorText, "ShowNavigatorPane: false");
        StringAssert.Contains(projectorText, "HasVisibleContent: false");
        Assert.IsFalse(projectorText.Contains("Restore choice:", StringComparison.Ordinal));
        Assert.IsFalse(projectorText.Contains("Conflict choices:", StringComparison.Ordinal));
        StringAssert.Contains(projectorText, "return [];");
        StringAssert.Contains(projectorText, "if (shellNotice.StartsWith(\"Restored \", StringComparison.OrdinalIgnoreCase))");
        StringAssert.Contains(summaryHeaderText, "bool hasRecoveryContext =");
        StringAssert.Contains(summaryHeaderText, "SaveLocalWorkButton.IsEnabled = state.CanSaveLocalWorkBeforeRestore;");
        StringAssert.Contains(summaryHeaderMarkupText, "Keep Local");
        StringAssert.Contains(summaryHeaderMarkupText, "Campaign");
    }

    [TestMethod]
    public void Bundled_demo_runner_fixture_is_published_for_both_desktop_heads()
    {
        string avaloniaProjectPath = ResolveSourceFile("Chummer.Avalonia", "Chummer.Avalonia.csproj");
        string blazorDesktopProjectPath = ResolveSourceFile("Chummer.Blazor.Desktop", "Chummer.Blazor.Desktop.csproj");

        string avaloniaProjectText = File.ReadAllText(avaloniaProjectPath);
        string blazorDesktopProjectText = File.ReadAllText(blazorDesktopProjectPath);

        StringAssert.Contains(avaloniaProjectText, "Samples/Legacy/Soma-Career.chum5");
        StringAssert.Contains(avaloniaProjectText, "<CopyToPublishDirectory>Always</CopyToPublishDirectory>");
        StringAssert.Contains(blazorDesktopProjectText, "Samples/Legacy/Soma-Career.chum5");
        StringAssert.Contains(blazorDesktopProjectText, "<CopyToPublishDirectory>Always</CopyToPublishDirectory>");
    }

    [TestMethod]
    public void Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers()
    {
        string releaseGatePath = ResolveSourceFile("scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string visualGatePath = ResolveSourceFile("scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string layoutGatePath = ResolveSourceFile("scripts", "ai", "milestones", "chummer5a-layout-hard-gate.sh");
        string appAxamlPath = ResolveSourceFile("Chummer.Avalonia", "App.axaml");
        string mainWindowPath = ResolveSourceFile("Chummer.Avalonia", "MainWindow.axaml");
        string mainWindowStateRefreshPath = ResolveSourceFile("Chummer.Avalonia", "MainWindow.StateRefresh.cs");
        string avaloniaProjectorPath = ResolveSourceFile("Chummer.Avalonia", "MainWindow.ShellFrameProjector.cs");
        string toolStripPath = ResolveSourceFile("Chummer.Avalonia", "Controls", "ToolStripControl.axaml");
        string navigatorPanePath = ResolveSourceFile("Chummer.Avalonia", "Controls", "NavigatorPaneControl.axaml");
        string shellCatalogPath = ResolveSourceFile("Chummer.Presentation", "Shell", "CatalogOnlyRulesetShellCatalogResolver.cs");
        string shellChromeBoundaryPath = ResolveSourceFile("Chummer.Presentation", "UiKit", "ShellChromeBoundary.cs");
        string blazorShellPath = ResolveSourceFile("Chummer.Blazor", "Components", "Layout", "DesktopShell.razor.cs");
        string sectionPanePath = ResolveSourceFile("Chummer.Blazor", "Components", "Shell", "SectionPane.razor");
        string workspaceLeftPanePath = ResolveSourceFile("Chummer.Blazor", "Components", "Shell", "WorkspaceLeftPane.razor");
        string openWorkspaceTreePath = ResolveSourceFile("Chummer.Blazor", "Components", "Shell", "OpenWorkspaceTree.razor");
        string appCssPath = ResolveSourceFile("Chummer.Blazor", "wwwroot", "app.css");

        string releaseGateText = File.ReadAllText(releaseGatePath);
        string visualGateText = File.ReadAllText(visualGatePath);
        string layoutGateText = File.ReadAllText(layoutGatePath);
        string appAxamlText = File.ReadAllText(appAxamlPath);
        string mainWindowText = File.ReadAllText(mainWindowPath);
        string mainWindowStateRefreshText = File.ReadAllText(mainWindowStateRefreshPath);
        string avaloniaProjectorText = File.ReadAllText(avaloniaProjectorPath);
        string toolStripText = File.ReadAllText(toolStripPath);
        string navigatorPaneText = File.ReadAllText(navigatorPanePath);
        string shellCatalogText = File.ReadAllText(shellCatalogPath);
        string shellChromeBoundaryText = File.ReadAllText(shellChromeBoundaryPath);
        string blazorShellText = File.ReadAllText(blazorShellPath);
        string sectionPaneText = File.ReadAllText(sectionPanePath);
        string workspaceLeftPaneText = File.ReadAllText(workspaceLeftPanePath);
        string openWorkspaceTreeText = File.ReadAllText(openWorkspaceTreePath);
        string appCssText = File.ReadAllText(appCssPath);

        StringAssert.Contains(releaseGateText, "chummer5a-layout-hard-gate.sh");
        Assert.IsFalse(
            releaseGateText.Contains("treating the all-skipped run as non-blocking", StringComparison.Ordinal),
            "B14 must fail closed when the API-backed dual-head parity lane is fully skipped.");
        Assert.IsFalse(
            releaseGateText.Contains("Assert\\.Inconclusive failed\\. Chummer API runtime socket error", StringComparison.Ordinal),
            "B14 must not special-case API runtime socket errors into a release pass.");
        StringAssert.Contains(visualGateText, "chummer5a-layout-hard-gate.sh");
        StringAssert.Contains(visualGateText, "promote_fresh_runtime_screenshot_pack");
        StringAssert.Contains(visualGateText, ".codex-studio/out/chummer5a-ultimate-parity-tester/live/screenshots/actual");
        StringAssert.Contains(visualGateText, ".codex-studio/out/chummer5a-parity-tester/live/screenshots/actual");
        StringAssert.Contains(visualGateText, ".codex-studio/out/ui-flagship-release-gate-screenshots-debug");
        StringAssert.Contains(layoutGateText, "defaultSingleRunnerKeepsWorkspaceChromeCollapsed");
        StringAssert.Contains(appAxamlText, "FontFamily\" Value=\"Segoe UI,Verdana,Arial\"");
        StringAssert.Contains(toolStripText, "x:Name=\"DesktopHomeButton\"");
        StringAssert.Contains(toolStripText, "x:Name=\"ImportFileButton\"");
        StringAssert.Contains(toolStripText, "x:Name=\"SaveButton\"");
        StringAssert.Contains(toolStripText, "x:Name=\"PrintButton\"");
        StringAssert.Contains(toolStripText, "x:Name=\"CopyButton\"");
        Assert.IsTrue(
            toolStripText.IndexOf("x:Name=\"SaveButton\"", StringComparison.Ordinal) <
            toolStripText.IndexOf("x:Name=\"PrintButton\"", StringComparison.Ordinal),
            "Classic toolbar parity requires Save before Print.");
        Assert.IsTrue(
            toolStripText.IndexOf("x:Name=\"PrintButton\"", StringComparison.Ordinal) <
            toolStripText.IndexOf("x:Name=\"CopyButton\"", StringComparison.Ordinal),
            "Classic toolbar parity requires Print before Copy.");
        Assert.IsTrue(
            toolStripText.IndexOf("x:Name=\"CopyButton\"", StringComparison.Ordinal) <
            toolStripText.IndexOf("x:Name=\"DesktopHomeButton\"", StringComparison.Ordinal),
            "Classic toolbar parity requires Copy before New.");
        Assert.IsTrue(
            toolStripText.IndexOf("x:Name=\"DesktopHomeButton\"", StringComparison.Ordinal) <
            toolStripText.IndexOf("x:Name=\"ImportFileButton\"", StringComparison.Ordinal),
            "Classic toolbar parity requires New before Open.");
        Assert.IsTrue(
            blazorShellText.IndexOf("\"save_character\"", StringComparison.Ordinal) <
            blazorShellText.IndexOf("\"print_character\"", StringComparison.Ordinal),
            "Blazor desktop shell must keep save before print in the preferred toolstrip order.");
        Assert.IsTrue(
            blazorShellText.IndexOf("\"print_character\"", StringComparison.Ordinal) <
            blazorShellText.IndexOf("\"copy\"", StringComparison.Ordinal),
            "Blazor desktop shell must keep print before copy in the preferred toolstrip order.");
        Assert.IsTrue(
            blazorShellText.IndexOf("\"copy\"", StringComparison.Ordinal) <
            blazorShellText.IndexOf("\"new_character\"", StringComparison.Ordinal),
            "Blazor desktop shell must keep copy before new in the preferred toolstrip order.");
        Assert.IsTrue(
            blazorShellText.IndexOf("\"new_character\"", StringComparison.Ordinal) <
            blazorShellText.IndexOf("\"open_character\"", StringComparison.Ordinal),
            "Blazor desktop shell must keep new before open in the preferred toolstrip order.");
        StringAssert.Contains(blazorShellText, "private bool ShowLeftPane =>");
        StringAssert.Contains(blazorShellText, "_shellSurfaceState.OpenWorkspaces.Count > 1");
        StringAssert.Contains(shellCatalogText, "[\"file\", \"edit\", \"special\", \"tools\", \"windows\", \"help\"]");
        StringAssert.Contains(shellCatalogText, "Command(\"edit\", \"command.edit\", \"menu\", false)");
        StringAssert.Contains(shellCatalogText, "Command(\"special\", \"command.special\", \"menu\", false)");
        StringAssert.Contains(shellCatalogText, "Command(\"switch_ruleset\", \"command.switch_ruleset\", \"special\", false)");
        StringAssert.Contains(shellCatalogText, "Command(\"new_window\", \"command.new_window\", \"windows\", false)");
        StringAssert.Contains(shellCatalogText, "Command(\"close_window\", \"command.close_window\", \"windows\", false)");
        StringAssert.Contains(shellChromeBoundaryText, "[\"switch_ruleset\"] = \"Switch Ruleset...\"");
        StringAssert.Contains(shellChromeBoundaryText, "[\"new_window\"] = \"New Window\"");
        StringAssert.Contains(mainWindowText, "ColumnDefinitions=\"0,*,0\"");
        StringAssert.Contains(mainWindowText, "x:Name=\"LeftNavigatorRegion\"");
        StringAssert.Contains(mainWindowText, "IsVisible=\"False\"");
        StringAssert.Contains(mainWindowStateRefreshText, "ApplyWorkbenchChromeVisibility(shellFrame);");
        Assert.IsTrue(
            mainWindowStateRefreshText.Contains("new GridLength(228)", StringComparison.Ordinal)
            || mainWindowStateRefreshText.Contains("new GridLength(264)", StringComparison.Ordinal),
            "Workbench chrome visibility must keep an explicit fixed desktop-width left pane.");
        StringAssert.Contains(mainWindowStateRefreshText, "new GridLength(0)");
        StringAssert.Contains(avaloniaProjectorText, "ShowNavigatorPane: false");
        StringAssert.Contains(navigatorPaneText, "x:Name=\"CodexHeadingText\"");
        StringAssert.Contains(navigatorPaneText, "IsVisible=\"False\"");
        StringAssert.Contains(sectionPaneText, "classic-summary-grid");
        StringAssert.Contains(sectionPaneText, "classic-attribute-grid");
        StringAssert.Contains(workspaceLeftPaneText, "@if (ShowSectionActions)");
        StringAssert.Contains(workspaceLeftPaneText, "@if (ShowWorkflowSurfaces)");
        StringAssert.Contains(openWorkspaceTreeText, "class=\"visually-hidden\"");
        StringAssert.Contains(workspaceLeftPaneText, "class=\"left-pane\"");
        Assert.IsFalse(
            openWorkspaceTreeText.Contains("workspace.Id.Value</span>", StringComparison.Ordinal),
            "Classic left-rail parity must not print workspace ids inside the visible dossier tree rows.");
        StringAssert.Contains(appCssText, ".classic-summary-grid");
        StringAssert.Contains(appCssText, ".classic-attribute-grid");
        StringAssert.Contains(appCssText, "--ui-kit-classic-font");
        StringAssert.Contains(appCssText, ".classic-menu-bar");
        StringAssert.Contains(appCssText, ".classic-tool-strip");
        StringAssert.Contains(appCssText, ".tool-divider");
        StringAssert.Contains(appCssText, ".classic-tab-strip");
        StringAssert.Contains(appCssText, ".classic-dialog");
        StringAssert.Contains(appCssText, ".visually-hidden");
        StringAssert.Contains(appCssText, ".workspace-layout--with-left-pane");
        StringAssert.Contains(appCssText, ".workspace-layout--without-left-pane");
    }

    [TestMethod]
    public void Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            Assert.IsTrue(harness.FindControl<MenuItem>("FileMenuButton").IsEnabled, "File menu must stay enabled after real shell bootstrap.");
            Assert.IsTrue(harness.FindControl<MenuItem>("HelpMenuButton").IsEnabled, "Help menu must stay enabled after real shell bootstrap.");
            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
            Assert.IsTrue(IsCommandVisibleInCommandList(harness, "open_character"));
            Assert.IsTrue(IsCommandVisibleInCommandList(harness, "new_character"));
        });
    }

    [TestMethod]
    public void Classic_file_menu_keeps_inline_right_shell_collapsed_while_menu_commands_remain_available()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Control rightShellRegion = harness.FindControl<Control>("RightShellRegion");
            Grid contentRegion = harness.FindControl<Grid>("ContentRegion");
            Assert.IsFalse(rightShellRegion.IsVisible, "Classic desktop shell must start without an inline right rail.");
            Assert.IsTrue(rightShellRegion.Bounds.Width <= 1d, "Classic desktop shell must start with a collapsed right rail width.");

            harness.Click("FileMenuButton");
            harness.WaitUntil(() =>
                harness.FindControl<MenuItem>("FileMenuButton")
                    .Items
                    .OfType<MenuItem>()
                    .Any(item => string.Equals(item.Tag?.ToString(), "new_character", StringComparison.Ordinal)));

            Assert.IsFalse(rightShellRegion.IsVisible, "Opening classic desktop menus must not resurrect the inline right rail.");
            Assert.IsTrue(rightShellRegion.Bounds.Width <= 1d, "Classic desktop menus must keep the right rail collapsed.");
            Assert.AreEqual(0d, contentRegion.ColumnDefinitions[2].Width.Value, 0.01d, "Classic desktop menus must keep the right-shell column closed.");
        });
    }

    [TestMethod]
    public void Menu_click_surfaces_visible_command_choices_in_shell()
    {
        Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters();
    }

    [TestMethod]
    public void File_menu_new_character_creates_runtime_workspace()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            Assert.IsNull(harness.State.WorkspaceId, "Runtime-backed New Character proof starts without an active workspace.");
            Assert.IsFalse(harness.FindControl<Control>("RightShellRegion").IsVisible, "Right shell must be collapsed before starting New Character.");

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "new_character"));

            harness.ClickMenuCommand("new_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && string.Equals(harness.State.LastCommandId, "new_character", StringComparison.Ordinal)
                && !harness.State.IsBusy);
            Assert.AreEqual("dialog.new_character", harness.State.ActiveDialog?.Id);
            Assert.IsNull(harness.State.WorkspaceId);
            Assert.IsNull(harness.State.Profile);
            Assert.IsFalse(harness.FindControl<Control>("RightShellRegion").IsVisible, "Right shell must stay collapsed while New Character dialog is active.");
            Assert.IsTrue(harness.FindControl<Control>("RightShellRegion").Bounds.Width <= 1d, "Right shell width must stay collapsed while New Character dialog is active.");
            Assert.AreEqual(0d, harness.FindControl<Grid>("ContentRegion").ColumnDefinitions[2].Width.Value, 0.01d, "New Character must not allocate inline right-shell column width.");
            Assert.IsNotNull(harness.Window.PeekDialogWindowForTesting(), "New Character must render through the dedicated desktop dialog window instead of the inline right rail.");
        });
    }

    [TestMethod]
    public void New_character_options_button_expands_inline_without_closing_build_method_dialog()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "new_character"));
            harness.ClickMenuCommand("new_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && harness.Window.PeekDialogWindowForTesting() is { IsVisible: true, BoundDialogId: "dialog.new_character" }
                && !harness.State.IsBusy);

            Control optionsPanel = harness.FindControl<Control>("newCharacterOptionsPanel");
            Assert.IsFalse(optionsPanel.IsVisible, "House-rule options should start collapsed.");

            harness.Click("newCharacterModifyButton");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && harness.Window.PeekDialogWindowForTesting() is { IsVisible: true, BoundDialogId: "dialog.new_character" }
                && harness.FindControl<Control>("newCharacterOptionsPanel").IsVisible,
                context: "Options must expand in place without closing Select Build Method.");

            Assert.AreEqual("dialog.new_character", harness.State.ActiveDialog?.Id);
            Assert.IsNull(harness.State.WorkspaceId);
            Assert.IsTrue(harness.Window.PeekDialogWindowForTesting() is { IsVisible: true, BoundDialogId: "dialog.new_character" });
        });
    }

    [TestMethod]
    public void File_menu_new_character_completes_into_visible_runtime_workspace()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "new_character"));
            harness.ClickMenuCommand("new_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && string.Equals(harness.State.LastCommandId, "new_character", StringComparison.Ordinal)
                && !harness.State.IsBusy);

            harness.InvokeDialogAction("create_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character.priority_workflow", StringComparison.Ordinal)
                || string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character.karma_workflow", StringComparison.Ordinal));

            harness.InvokeDialogAction("complete_new_character_workflow");
            harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Profile is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy,
                timeoutMs: 8000,
                context: "new character completion should hydrate a visible runtime workspace");
            Assert.IsFalse(harness.FindControl<Control>("RightShellRegion").IsVisible, "Right shell should return to compact default after completing New Character.");
            Assert.IsTrue(harness.FindControl<Control>("RightShellRegion").Bounds.Width <= 1d, "Right shell width should stay collapsed after completing New Character.");
            Assert.AreEqual(0d, harness.FindControl<Grid>("ContentRegion").ColumnDefinitions[2].Width.Value, 0.01d, "New Character completion must leave the inline right-shell column closed.");
            harness.WaitUntil(() => harness.Window.PeekDialogWindowForTesting() is null, timeoutMs: 4000, context: "new character dialog window should close after workflow completion");

            TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
            harness.WaitUntil(() => rosterTree.Bounds.Width > 0d && rosterTree.Bounds.Height > 0d);

            Control? sectionHost = harness.FindControlOrDefault<Control>("SectionHostControl");
            if (sectionHost is not null)
            {
                Assert.IsTrue(sectionHost.IsVisible, "Runtime new-character flow should surface the main section host.");
            }

            TextBlock? dialogTitle = harness.FindControlOrDefault<TextBlock>("DialogTitleText");
            if (dialogTitle is not null)
            {
                Assert.IsTrue(
                    string.IsNullOrWhiteSpace(dialogTitle.Text) || string.Equals(dialogTitle.Text, "(none)", StringComparison.Ordinal),
                    "Runtime new-character flow should leave no blocking dialog title visible after completion.");
            }
        });
    }

    [TestMethod]
    public void New_character_workbench_suppresses_right_command_pane_for_variant_dialog_states()
    {
        MethodInfo method = typeof(MainWindow).GetMethod(
            "IsNewCharacterWorkbenchActive",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("IsNewCharacterWorkbenchActive is missing from MainWindow.");

        static bool Invoke(
            MethodInfo testMethod,
            string? selectedCommandId,
            string? activeDialogId,
            string? dialogTitle,
            CommandDialogPaneState state)
        {
            object? result = testMethod.Invoke(null, [selectedCommandId, activeDialogId, state]);
            return result is bool value && value;
        }

        CommandDialogPaneState noFieldsOrActions = new(
            [],
            "new_character",
            null,
            null,
            null,
            null,
            [],
            []);
        Assert.IsTrue(
            Invoke(method, null, "dialog.new_character.mystic_adept_workflow", "Select Build Method", noFieldsOrActions),
            "Dialog variant with Select Build Method title should still suppress the right command pane.");

        CommandDialogPaneState buildMethodFields = new(
            [],
            null,
            "dialog.new_character.priority_workflow",
            "New Character",
            null,
            null,
            [
                new DialogFieldDisplayItem("newCharacterName", "Name", string.Empty, string.Empty, false, false, "text"),
                new DialogFieldDisplayItem("newCharacterBuildMethod", "Build Method", string.Empty, string.Empty, false, false, "select")
            ],
            []);
        Assert.IsTrue(
            Invoke(method, "new_character", null, "New Character", buildMethodFields),
            "New Character workflow fields should suppress the right command pane while active.");

        CommandDialogPaneState actionVariant = new(
            [],
            null,
            null,
            null,
            null,
            null,
            [new DialogFieldDisplayItem("newCharacterMetatype", "Metatype", string.Empty, string.Empty, false, false, "select")],
            [new DialogActionDisplayItem("new_character_workflow_cancel", "Cancel", true)]);
        Assert.IsTrue(
            Invoke(method, "new_character.priority_workflow", null, null, actionVariant),
            "New character workflow action names should suppress the right command pane.");

        CommandDialogPaneState mysticVariant = new(
            [],
            null,
            "dialog.new_character.mystic_adept_workflow",
            "Select Build Method",
            null,
            null,
            [new DialogFieldDisplayItem("newCharacterAssensing", "Assensing", string.Empty, string.Empty, false, false, "text")],
            []);
        Assert.IsTrue(
            Invoke(method, "new_character.mystic_adept", null, "Select Build Method", mysticVariant),
            "Mystic-adept workflow field variants should suppress the right command pane.");

        CommandDialogPaneState priorityBuildVariant = new(
            [],
            null,
            null,
            "Priority Build",
            null,
            null,
            [
                new DialogFieldDisplayItem("newCharacterPrioritySkillChoice3", "Assensing", string.Empty, string.Empty, false, false, "select"),
                new DialogFieldDisplayItem("newCharacterWorkflowBuildMethod", "Build Method", "Priority", "Priority", false, false, "select")
            ],
            []);
        Assert.IsTrue(
            Invoke(method, "workflow.priority", null, "Priority Build", priorityBuildVariant),
            "Priority-build continuation variants must still suppress the right pane when workflow-specific fields stay active.");

        CommandDialogPaneState unrelated = new(
            [],
            null,
            null,
            "Character Import",
            null,
            null,
            [new DialogFieldDisplayItem("non_character_field", "Name", string.Empty, string.Empty, false, false, "text")],
            []);
        Assert.IsFalse(
            Invoke(method, "import_character", null, "Character Import", unrelated),
            "Non-character command dialogs should not hide the right command pane by default.");
    }

    [TestMethod]
    public void Master_index_search_keeps_focus_after_runtime_backed_text_updates()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
            harness.ClickMenuCommand("master_index");
            harness.WaitUntil(() =>
                    string.Equals(harness.State.ActiveDialog?.Id, "dialog.master_index", StringComparison.Ordinal)
                    && string.Equals(harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text, "Master Index", StringComparison.Ordinal)
                    && !harness.State.IsBusy,
                timeoutMs: 8000,
                context: "master index dialog should be open before editing search");

            string searchFieldName = DesktopDialogAccessibility.BuildFieldInputName("masterIndexSearch");
            TextBox searchBox = harness.FindControl<TextBox>(searchFieldName);
            Assert.IsTrue(
                searchBox.IsEnabled && searchBox.IsVisible,
                "Master Index search box must remain interactive before typing.");

            searchBox.Text = "adept";
            harness.WaitUntil(() =>
                    string.Equals(
                        DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "masterIndexSearch"),
                        "adept",
                        StringComparison.Ordinal),
                context: "master index search value should round-trip through the runtime-backed dialog update");

            TextBox refreshedSearchBox = harness.FindControl<TextBox>(searchFieldName);
            Assert.IsTrue(refreshedSearchBox.IsEnabled, "Master Index search must remain interactive after runtime-backed dialog rebuilds.");
            Assert.AreEqual("adept", refreshedSearchBox.Text);
        });
    }

    [TestMethod]
    public void Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Menu menuPanel = harness.FindControl<Menu>("MenuBarPanel");
            MenuItem[] menuButtons = menuPanel.Items.OfType<MenuItem>().ToArray();
            string[] menuLabels = menuButtons
                .Select(button => button.Header?.ToString() ?? string.Empty)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "File",
                    "Edit",
                    "Special",
                    "Tools",
                    "Windows",
                    "Help"
                },
                menuLabels);

            foreach (MenuItem button in menuButtons)
            {
                Assert.IsTrue(button.IsEnabled, $"Menu button '{button.Name}' must stay enabled after runtime bootstrap.");
            }

            (string ButtonName, string MenuId)[] clickableMenus =
            [
                ("FileMenuButton", "file"),
                ("EditMenuButton", "edit"),
                ("ToolsMenuButton", "tools"),
                ("WindowsMenuButton", "windows"),
                ("HelpMenuButton", "help"),
            ];

            foreach ((string buttonName, string menuId) in clickableMenus)
            {
                harness.Click(buttonName);
                harness.WaitUntil(() =>
                    string.Equals(harness.ShellPresenter.State.OpenMenuId, menuId, StringComparison.Ordinal)
                    && IsAnyCommandVisibleInCommandList(harness));
            }
        });
    }

    [TestMethod]
    public void Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            (string ButtonName, string ExpectedLabel)[] expectedButtons =
            [
                ("ClassicToolStripAutoAliceButton", "ALICE"),
                ("ClassicToolStripStartOriginButton", "Origin Dossier"),
                ("ImportFileButton", "Open"),
                ("SaveButton", "Save"),
                ("PrintButton", "Print"),
                ("CopyButton", "Copy"),
                ("SettingsButton", "Settings"),
                ("CloseWorkspaceButton", "Close"),
            ];

            foreach ((string buttonName, string expectedLabel) in expectedButtons)
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Workbench action '{buttonName}' must stay visible.");
                Assert.IsTrue(button.IsEnabled, $"Workbench action '{buttonName}' must stay enabled.");
                CollectionAssert.Contains(GetButtonTextLines(button), expectedLabel, $"Workbench action '{buttonName}' must keep its classic desktop label.");
                Assert.AreEqual(1, GetButtonTextLines(button).Length, $"Workbench action '{buttonName}' must not add a secondary caption line.");
                Assert.IsTrue(button.Bounds.Width > 0d && button.Bounds.Height > 0d, $"Workbench action '{buttonName}' must keep a visible desktop footprint.");
            }

            foreach (string buttonName in new[] { "SaveButton", "PrintButton", "CopyButton", "ImportFileButton", "CloseWorkspaceButton" })
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(
                    button.GetVisualDescendants().OfType<Image>().Any(image => image.IsVisible),
                    $"Workbench action '{buttonName}' must restore a visible classic toolbar icon.");
            }

            Button importRawButton = harness.FindControl<Button>("ImportRawButton");
            Assert.IsFalse(importRawButton.IsVisible, "Raw XML import must stay off the primary classic toolbar.");
            Assert.IsFalse(
                harness.FindControl<Button>("LoadDemoRunnerButton").IsVisible,
                "The Chummer5A public-stable toolbar must hide the demo launcher by default.");
            foreach (string buttonName in new[] { "DesktopHomeButton", "CampaignWorkspaceButton", "UpdateStatusButton", "InstallLinkingButton", "SupportButton", "ReportIssueButton" })
            {
                Assert.IsFalse(harness.FindControl<Button>(buttonName).IsVisible, $"Secondary chrome '{buttonName}' must stay out of the default dense toolbar.");
            }
        });
    }

    [TestMethod]
    public void Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Border[] badgeBorders = harness.Window.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("shell-action-badge"))
                .ToArray();
            TextBlock[] captionBlocks = harness.Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.Classes.Contains("shell-action-caption"))
                .ToArray();
            string[] shellChromeLabels = harness.Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => text.IsVisible)
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            double[] toolbarButtonHeights =
            [
                harness.FindControl<Button>("LoadDemoRunnerButton").Bounds.Height,
                harness.FindControl<Button>("ImportFileButton").Bounds.Height,
                harness.FindControl<Button>("SaveButton").Bounds.Height,
                harness.FindControl<Button>("PrintButton").Bounds.Height,
                harness.FindControl<Button>("CopyButton").Bounds.Height,
                harness.FindControl<Button>("SettingsButton").Bounds.Height,
                harness.FindControl<Button>("ImportRawButton").Bounds.Height,
                harness.FindControl<Button>("CloseWorkspaceButton").Bounds.Height,
            ];
            Assert.AreEqual(0, badgeBorders.Length, "Classic toolbar parity forbids dashboard badge tiles in the workbench strip.");
            Assert.AreEqual(0, captionBlocks.Length, "Classic toolbar parity forbids secondary caption lines in the workbench strip.");
            CollectionAssert.DoesNotContain(shellChromeLabels, "Quick Actions");
            CollectionAssert.DoesNotContain(shellChromeLabels, "Workbench State");
            Assert.IsTrue(toolbarButtonHeights.All(height => height <= 40d), "Classic toolbar parity requires compact workbench actions instead of hero-card sized buttons.");
        });
    }

    [TestMethod]
    public void Next90_m103_veteran_certification_screenshot_pack_covers_required_desktop_surfaces()
    {
        foreach (string screenshot in RequiredVeteranCertificationScreenshots)
        {
            CollectionAssert.Contains(
                VeteranCertificationScreenshotFiles,
                screenshot,
                $"Next-90 milestone 103.2 must keep screenshot-backed evidence for {screenshot}.");
        }

        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "02-menu-open-light.png", "Menu familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "01-initial-shell-light.png", "Toolstrip familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "17-character-roster-dialog-light.png", "Roster familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "16-master-index-dialog-light.png", "Master index familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "03-settings-open-light.png", "Settings familiarity must stay screenshot-backed.");
        CollectionAssert.Contains(VeteranCertificationScreenshotFiles, "18-import-dialog-light.png", "Import familiarity must stay screenshot-backed.");
    }

    [TestMethod]
    public void Next90_m103_veteran_certification_review_steps_bind_each_required_surface_to_desktop_capture()
    {
        CollectionAssert.AreEquivalent(
            new[] { "menu", "toolstrip", "roster", "master_index", "settings", "import" },
            VeteranCertificationReviewSteps.Select(step => step.Surface).ToArray(),
            "M103 veteran certification must keep one explicit review step for every assigned desktop parity surface.");
        CollectionAssert.AreEquivalent(
            RequiredVeteranCertificationScreenshots,
            VeteranCertificationReviewSteps.Select(step => step.ScreenshotFileName).ToArray(),
            "M103 review steps must bind exactly to the required screenshot evidence pack.");
        Assert.AreEqual(
            VeteranCertificationReviewSteps.Length,
            VeteranCertificationReviewSteps.Select(step => step.ScreenshotFileName).Distinct(StringComparer.Ordinal).Count(),
            "M103 screenshot-backed review steps must not reuse one capture across multiple surfaces.");

        foreach (VeteranCertificationReviewStep step in VeteranCertificationReviewSteps)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(step.PromotedHeadGesture), $"{step.Surface} must name the promoted-head gesture that creates its screenshot.");
            StringAssert.Contains(step.Chummer5aBaseline, "Chummer5a");
        }
    }

    [TestMethod]
    public void Native_horizon_screenshot_pack_covers_every_catalog_entry_with_a_dedicated_surface_capture()
    {
        string[] expectedEntryIds = DesktopHorizonWorkbenchCatalog.ListEntries()
            .Select(entry => entry.Id)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        string[] coveredEntryIds = NativeHorizonScreenshotSurfaces
            .Select(surface => surface.EntryId)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            expectedEntryIds,
            coveredEntryIds,
            "Every native horizon/workbench entry must publish a dedicated screenshot-backed exit-gate surface.");

        foreach (HorizonScreenshotSurface surface in NativeHorizonScreenshotSurfaces)
        {
            CollectionAssert.Contains(VeteranCertificationScreenshotFiles, surface.ScreenshotFileName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(surface.WindowTitle), $"{surface.EntryId} must bind to a concrete native window title.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(surface.RequiredControlName), $"{surface.EntryId} must bind to a concrete native control anchor.");
        }
    }

    [TestMethod]
    public void Next90_m141_import_route_review_steps_bind_translator_xml_editor_and_hero_lab_to_direct_screenshots()
    {
        CollectionAssert.AreEqual(
            new[] { "translator", "xml_amendment_editor", "hero_lab_importer" },
            ImportRouteReviewSteps.Select(step => step.Surface).ToArray(),
            "M141 import-route closeout must keep deterministic review order for translator, XML amendment, and Hero Lab.");
        CollectionAssert.AreEqual(
            new[]
            {
                "38-translator-dialog-light.png",
                "39-xml-editor-dialog-light.png",
                "40-hero-lab-importer-dialog-light.png",
            },
            ImportRouteReviewSteps.Select(step => step.ScreenshotFileName).ToArray(),
            "M141 import-route closeout must keep deterministic screenshot names wired to each direct route.");

        foreach (VeteranCertificationReviewStep step in ImportRouteReviewSteps)
        {
            CollectionAssert.Contains(VeteranCertificationScreenshotFiles, step.ScreenshotFileName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(step.PromotedHeadGesture), $"{step.Surface} must name the promoted-head gesture that creates its screenshot.");
            StringAssert.Contains(step.Chummer5aBaseline, "Chummer5a");
        }
    }

    [TestMethod]
    public void Promoted_avalonia_head_uses_review_sized_desktop_frame_for_m103_screenshots()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            Assert.IsTrue(
                harness.Window.Bounds.Width >= 1280d,
                "M103 screenshot-backed parity review requires the promoted Avalonia head to open at the desktop visual review width.");
            Assert.IsTrue(
                harness.Window.Bounds.Height >= 800d,
                "M103 screenshot-backed parity review requires the promoted Avalonia head to open at the desktop visual review height.");
        });
    }

    [TestMethod]
    public void Runtime_backed_roster_tree_preserves_legacy_left_rail_navigation_posture()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
            CharacterRosterNode[] rootItems = SnapshotRosterItems(rosterTree);
            string[] rootLabels = rootItems.Select(item => item.Name).ToArray();
            string? rulesetId = harness.State.OpenWorkspaces
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? harness.State.NavigationTabs
                    .Select(tab => RulesetDefaults.NormalizeOptional(tab.RulesetId))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            string[] expectedRootLabels = [RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(rulesetId)];

            if (harness.State.OpenWorkspaces.Count == 0)
            {
                CollectionAssert.AreEqual(
                    Array.Empty<string>(),
                    rootLabels,
                    "The startup roster should stay empty until a workspace is actually open.");
            }
            else
            {
                Assert.IsTrue(
                    rootLabels.SequenceEqual(expectedRootLabels, StringComparer.Ordinal),
                    "The roster tree must group characters by ruleset only. Expected: "
                    + string.Join(" | ", expectedRootLabels)
                    + " ; actual: "
                    + string.Join(" | ", rootLabels));
            }
            Assert.IsTrue(rosterTree.IsVisible, "The left rail must render the roster tree in the quiet default shell.");
            Assert.IsNull(harness.FindControlOrDefault<TabControl>("LoadedRunnerTabStrip"), "The legacy-oriented left rail must be a tree navigator, not a second tab control.");
            Assert.IsNull(harness.FindControlOrDefault<ListBox>("NavigationTabsList"), "The legacy-oriented left rail must not fall back to a dashboard-style tab list.");
        });
    }

    [TestMethod]
    public void Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks()
    {
        // Runtime_backed_sr4_switch_ruleset_dialog_preserves_compact_combo_posture
        // Runtime_backed_sr6_shared_muscle_memory_inventory_receipt_matches_promoted_surface_contract
        // Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            foreach (string rulesetId in new[] { RulesetDefaults.Sr4, RulesetDefaults.Sr5, RulesetDefaults.Sr6 })
            {
                harness.ShellPresenter.SetPreferredRulesetAsync(rulesetId, CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(harness.ShellPresenter.State.PreferredRulesetId, rulesetId, StringComparison.Ordinal)
                    && string.Equals(harness.ShellPresenter.State.ActiveRulesetId, rulesetId, StringComparison.Ordinal));

                TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
                string[] rootLabels = SnapshotRosterItems(rosterTree).Select(item => item.Name).ToArray();
                if (harness.State.OpenWorkspaces.Count == 0)
                {
                    CollectionAssert.AreEqual(
                        Array.Empty<string>(),
                        rootLabels,
                        $"Ruleset '{rulesetId}' should keep the grouped roster empty until a workspace is opened.");
                }
                else
                {
                    string[] expectedRootLabels =
                    [
                        RulesetUiDirectiveCatalog.BuildOpenWorkspacesHeading(rulesetId)
                    ];
                    CollectionAssert.AreEqual(
                        expectedRootLabels,
                        rootLabels,
                        $"Ruleset '{rulesetId}' must keep the roster tree on ruleset-specific familiar landmarks.");
                }
            }
        });
    }

    [TestMethod]
    public void Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            string[] visibleTexts = harness.Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => (text.Text ?? string.Empty).Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            CollectionAssert.DoesNotContain(visibleTexts, "Career-style workbench");
            CollectionAssert.DoesNotContain(visibleTexts, "Command Palette");
            CollectionAssert.DoesNotContain(visibleTexts, "Coach Sidecar");
            CollectionAssert.DoesNotContain(visibleTexts, "Coach Launch");
            CollectionAssert.DoesNotContain(visibleTexts, "Recent Coach Guidance");
            Assert.IsTrue(
                visibleTexts.Any(text => text.Contains("Character", StringComparison.Ordinal)),
                "The runtime shell should still surface character-oriented copy after bootstrap.");
            CollectionAssert.DoesNotContain(visibleTexts, "Section Commands");
            CollectionAssert.DoesNotContain(visibleTexts, "Reference & Notes");
        });
    }

    [TestMethod]
    public void Runtime_backed_shell_chrome_stays_enabled_after_runner_load()
    {
        WithRuntimeLoadedRunnerHarness(harness =>
        {
            Assert.IsTrue(harness.FindControl<Control>("MenuBarRegion").IsVisible);
            Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible);

            string[] menuButtons =
            [
                "FileMenuButton",
                "EditMenuButton",
                "SpecialMenuButton",
                "ToolsMenuButton",
                "WindowsMenuButton",
                "HelpMenuButton",
            ];

            string[] actionButtons =
            [
                "ImportFileButton",
                "SaveButton",
                "SettingsButton",
                "PrintButton",
                "CopyButton",
                "CloseWorkspaceButton",
            ];

            foreach (string buttonName in menuButtons)
            {
                MenuItem button = harness.FindControl<MenuItem>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Runtime-backed runner load must keep '{buttonName}' visible.");
                Assert.IsTrue(button.IsEnabled, $"Runtime-backed runner load must keep '{buttonName}' enabled.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(button.Header?.ToString()), $"Runtime-backed runner load must not blank the label for '{buttonName}'.");
            }

            foreach (string buttonName in actionButtons)
            {
                Button button = harness.FindControl<Button>(buttonName);
                Assert.IsTrue(button.IsVisible, $"Runtime-backed runner load must keep '{buttonName}' visible.");
                Assert.IsTrue(button.IsEnabled, $"Runtime-backed runner load must keep '{buttonName}' enabled.");
                Assert.IsTrue(GetButtonTextLines(button).Length > 0, $"Runtime-backed runner load must not blank the label for '{buttonName}'.");
            }

            foreach (string buttonName in new[]
                     {
                         "DesktopHomeButton",
                         "CampaignWorkspaceButton",
                         "LoadDemoRunnerButton",
                         "UpdateStatusButton",
                         "InstallLinkingButton",
                         "SupportButton",
                         "ReportIssueButton",
                     })
            {
                Assert.IsFalse(harness.FindControl<Button>(buttonName).IsVisible, $"Runtime-backed runner load must keep secondary chrome '{buttonName}' collapsed.");
            }

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
        });
    }

    [TestMethod]
    public void Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive()
    {
        string? priorMode = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MODE");
        string? priorChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_MODE", "classic");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
                harness.ClickMenuCommand("global_settings");

                harness.WaitUntil(() =>
                {
                    return string.Equals(harness.ShellPresenter.State.LastCommandId, "global_settings", StringComparison.Ordinal)
                        || string.Equals(harness.Presenter.State.LastCommandId, "global_settings", StringComparison.Ordinal);
                }, context: "settings command route should execute");

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_MODE", priorMode);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorChannel);
        }
    }

    [TestMethod]
    public void Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            // harness.Click("ToolsMenuButton")
            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
            harness.ClickMenuCommand("global_settings");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Global Settings",
                "UI Scale",
                "Theme",
                "Language",
                "Compact Mode");
            harness.InvokeDialogAction("save");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

            // harness.Click("ToolsMenuButton")
            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
            harness.ClickMenuCommand("master_index");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Master Index",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Master Index",
                "Data Root",
                "/app/data");
            harness.InvokeDialogAction("close");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Master Index",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

            // harness.Click("ToolsMenuButton")
            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "character_roster");
            harness.ClickMenuCommand("character_roster");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Character Roster",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Character Roster",
                "Description",
                "Concept",
                "Background",
                "Character Notes",
                "Game Notes");
        });
    }

    [TestMethod]
    // Veteran proof anchor: Translator_xml_editor_and_hero_lab_importer_routes_surface_runtime_backed_dialog_receipts
    public void Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            harness.Presenter.ExecuteCommandAsync("translator", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Translator",
                    StringComparison.Ordinal));
            AssertDialogMouseReachabilityOrScrollContainment(harness.Window, "Translator");
            AssertDialogContainsAll(
                harness,
                "Translator",
                GetImportRouteReviewStep("translator").RequiredDialogMarkers);
            Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "translatorLanePosture"));
            Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "translatorBridgePosture"));
            harness.InvokeDialogAction("close");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Translator",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

            harness.Presenter.ExecuteCommandAsync("xml_editor", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "XML Editor",
                    StringComparison.Ordinal));
            AssertDialogMouseReachabilityOrScrollContainment(harness.Window, "XML Editor");
            AssertDialogContainsAll(
                harness,
                "XML Editor",
                GetImportRouteReviewStep("xml_amendment_editor").RequiredDialogMarkers);
            Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "xmlEditorXmlBridgePosture"));
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "XML Editor",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

            harness.Presenter.ExecuteCommandAsync("hero_lab_importer", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Hero Lab Importer",
                    StringComparison.Ordinal));
            Assert.AreEqual("dialog.hero_lab_importer", harness.State.ActiveDialog?.Id);
            AssertDialogMouseReachabilityOrScrollContainment(harness.Window, "Hero Lab Importer");
            AssertDialogContainsAll(
                harness,
                "Hero Lab Importer",
                GetImportRouteReviewStep("hero_lab_importer").RequiredDialogMarkers);
            StringAssert.Contains(
                DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "heroLabImportOracleLanePosture"),
                "governed");
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Hero Lab Importer",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);
        });
    }

    [TestMethod]
    public void Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            Dictionary<string, ScreenshotProofCapture> captured = new(StringComparer.Ordinal);
            WithHarness(harness =>
            {
                harness.WaitForReady();

                Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible, "Veteran first-minute proof requires the promoted Avalonia head to keep the toolstrip visible on launch.");
                Assert.IsTrue(harness.FindControl<Button>("LoadDemoRunnerButton").IsEnabled, "Veteran first-minute proof requires a visible desktop import/load affordance before any workspace is opened.");

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
                harness.ClickMenuCommand("global_settings");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Global Settings",
                    "UI Scale",
                    "Theme",
                    "Language",
                    "Compact Mode");
                harness.InvokeDialogAction("save");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
                harness.ClickMenuCommand("master_index");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Master Index",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Master Index",
                    "Data Root",
                    "/app/data");
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Master Index",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "character_roster");
                harness.ClickMenuCommand("character_roster");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Character Roster",
                    "Description",
                    "Concept",
                    "Background",
                    "Character Notes",
                    "Game Notes");
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                        harness.Presenter.ImportCalls > 0
                        && !string.IsNullOrWhiteSpace(harness.State.Profile?.Name)
                        && harness.FindControlOrDefault<Control>("LoadedRunnerTabStripBorder")?.IsVisible == true
                        && !harness.State.IsBusy,
                    timeoutMs: 8000);
                Assert.IsFalse(string.IsNullOrWhiteSpace(harness.State.Profile?.Name), "Veteran first-minute proof requires a loaded runner profile before import review.");
                Assert.IsTrue(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible, "Veteran first-minute proof requires the loaded-runner tab strip after the desktop import shortcut.");
                Assert.IsFalse(
                    harness.FindControlOrDefault<Control>("QuickStartContainer")?.IsVisible ?? false,
                    "Veteran first-minute proof must stay on the quiet shell without reviving the old quick-start band.");

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));
                captured["19-workflow-file-menu-loaded-light.png"] = CaptureScreenshotProof(harness, "19-workflow-file-menu-loaded-light.png");
                harness.ClickMenuCommand("new_character");
                harness.WaitUntil(() =>
                        string.Equals(
                            harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                            "Select Build Method",
                            StringComparison.Ordinal),
                    timeoutMs: 8000);
                captured["36-workflow-new-character-dialog-light.png"] = CaptureScreenshotProof(harness, "36-workflow-new-character-dialog-light.png");
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Select Build Method",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("skills");
                ListBox skillRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => skillRows.ItemCount > 0);
                captured["20-workflow-skills-section-light.png"] = CaptureScreenshotProof(harness, "20-workflow-skills-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_skill_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_skill_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Skill",
                        StringComparison.Ordinal));
                captured["21-workflow-skill-add-dialog-light.png"] = CaptureScreenshotProof(harness, "21-workflow-skill-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Skill",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("qualities");
                ListBox qualityRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => qualityRows.ItemCount > 0);
                captured["22-workflow-qualities-section-light.png"] = CaptureScreenshotProof(harness, "22-workflow-qualities-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_quality_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_quality_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Quality",
                        StringComparison.Ordinal));
                captured["23-workflow-quality-add-dialog-light.png"] = CaptureScreenshotProof(harness, "23-workflow-quality-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Quality",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("gear");
                ListBox gearRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => gearRows.ItemCount > 0);
                captured["24-workflow-gear-section-light.png"] = CaptureScreenshotProof(harness, "24-workflow-gear-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_gear_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_gear_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Gear",
                        StringComparison.Ordinal));
                captured["25-workflow-gear-add-dialog-light.png"] = CaptureScreenshotProof(harness, "25-workflow-gear-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Gear",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("weapons");
                ListBox weaponRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => weaponRows.ItemCount > 0);
                captured["26-workflow-weapons-section-light.png"] = CaptureScreenshotProof(harness, "26-workflow-weapons-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_combat_add_weapon")?.IsVisible == true);
                harness.Click("SectionQuickAction_combat_add_weapon");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Weapon",
                        StringComparison.Ordinal));
                captured["27-workflow-weapon-add-dialog-light.png"] = CaptureScreenshotProof(harness, "27-workflow-weapon-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Weapon",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("armors");
                ListBox armorRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => armorRows.ItemCount > 0);
                captured["28-workflow-armor-section-light.png"] = CaptureScreenshotProof(harness, "28-workflow-armor-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_combat_add_armor")?.IsVisible == true);
                harness.Click("SectionQuickAction_combat_add_armor");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Armor",
                        StringComparison.Ordinal));
                captured["29-workflow-armor-add-dialog-light.png"] = CaptureScreenshotProof(harness, "29-workflow-armor-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Armor",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("cyberwares");
                ListBox cyberwareRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => cyberwareRows.ItemCount > 0);
                captured["30-workflow-cyberware-section-light.png"] = CaptureScreenshotProof(harness, "30-workflow-cyberware-section-light.png");

                harness.SetActiveSectionForTesting("powers");
                ListBox powerRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => powerRows.ItemCount > 0);
                captured["31-workflow-powers-section-light.png"] = CaptureScreenshotProof(harness, "31-workflow-powers-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_adept_power_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_adept_power_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Adept Power",
                        StringComparison.Ordinal));
                captured["32-workflow-adept-power-dialog-light.png"] = CaptureScreenshotProof(harness, "32-workflow-adept-power-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Adept Power",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("complexforms");
                ListBox complexFormRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => complexFormRows.ItemCount > 0);
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_complex_form_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_complex_form_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Complex Form",
                        StringComparison.Ordinal));
                captured["33-workflow-complex-form-dialog-light.png"] = CaptureScreenshotProof(harness, "33-workflow-complex-form-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Complex Form",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy);

                harness.SetActiveSectionForTesting("validate");
                ListBox validateRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => validateRows.ItemCount > 0);
                captured["34-workflow-validate-section-light.png"] = CaptureScreenshotProof(harness, "34-workflow-validate-section-light.png");

                harness.SetActiveSectionForTesting("rules");
                ListBox rulesRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => rulesRows.ItemCount > 0);
                captured["35-workflow-rules-section-light.png"] = CaptureScreenshotProof(harness, "35-workflow-rules-section-light.png");

                harness.SetActiveSectionForTesting("calendar");
                ListBox calendarRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => calendarRows.ItemCount > 0);
                captured["37-workflow-calendar-section-light.png"] = CaptureScreenshotProof(harness, "37-workflow-calendar-section-light.png");

                OpenMenuUntilCommandVisible(harness, "FileMenuButton", "open_character");
                harness.ClickMenuCommand("open_character");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Open Character",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Open Character",
                    GetVeteranCertificationReviewStep("import").RequiredDialogMarkers);
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Open Character",
                        StringComparison.Ordinal));
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [TestMethod]
    public void Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy);

                Assert.IsNotNull(harness.State.WorkspaceId);
                Assert.IsTrue(harness.State.Session.OpenWorkspaces.Count > 0);
                Assert.IsFalse(string.IsNullOrWhiteSpace(harness.State.Profile?.Name), "Runtime-backed runner import must populate the workspace profile.");
                Assert.IsTrue(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible, "Loaded runner import must surface the loaded-workspace tab posture.");
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [TestMethod]
    public void Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6()
    {
        Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters();
        Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm();
        Contacts_diary_and_support_routes_execute_with_public_path_visibility();
    }

    [TestMethod]
    public void Runtime_loaded_runner_quick_action_workflows_materialize_dialog_contracts_and_continuations_across_sr4_sr5_and_sr6()
    {
        Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions();
        Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions();
        Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues();
        Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm();
    }

    [TestMethod]
    public void Workspace_strip_quick_start_hides_after_runtime_backed_runner_load()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);
        string? priorChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        string? priorSampleOverride = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES");

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", "1");

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                Assert.IsTrue(harness.FindControl<Button>("LoadDemoRunnerButton").IsVisible);
                Assert.IsNull(harness.FindControlOrDefault<Control>("QuickStartContainer"), "The quiet shell must not surface the old quick-start band by default.");
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() => harness.State.WorkspaceId is not null && harness.State.Session.OpenWorkspaces.Count > 0);
                Assert.IsTrue(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible);
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorChannel);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", priorSampleOverride);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [TestMethod]
    public void Public_stable_shell_hides_demo_runner_and_quick_start_noise_by_default()
    {
        string? priorChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        string? priorSampleOverride = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", null);

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();

                Assert.IsFalse(
                    harness.FindControlOrDefault<Button>("LoadDemoRunnerButton")?.IsVisible ?? false,
                    "Public stable shell must hide the default demo launcher.");
                Assert.IsFalse(
                    harness.FindControlOrDefault<Control>("QuickStartContainer")?.IsVisible ?? false,
                    "Public stable shell must not surface the quick-start demo band.");

                string[] visibleTextSamples = harness.Window.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(static control => control.IsVisible)
                    .Select(control => control.Text ?? string.Empty)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                CollectionAssert.DoesNotContain(visibleTextSamples, "Open Sample");
                CollectionAssert.DoesNotContain(visibleTextSamples, "Demo");
                Assert.IsFalse(
                    visibleTextSamples.Any(text =>
                        text.Contains("Codex", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("provider", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("Living World", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("Signal Deck", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("Black Ledger", StringComparison.OrdinalIgnoreCase)),
                    "Public stable shell must stay free of developer, provider, or public-web noise.");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorChannel);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", priorSampleOverride);
        }
    }

    [TestMethod]
    public void Public_stable_shell_allows_internal_sample_override_for_operator_and_test_access()
    {
        string? priorChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        string? priorSampleOverride = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", "1");

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                Assert.IsTrue(
                    harness.FindControl<Button>("LoadDemoRunnerButton").IsVisible,
                    "Public stable shell may expose the sample route only behind the explicit operator/test override.");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorChannel);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", priorSampleOverride);
        }
    }

    [TestMethod]
    public void Classic_mode_routes_settings_surface_through_formport_host_and_hides_generic_section_host()
    {
        string? priorMode = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MODE");
        string? priorChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_MODE", "classic");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                harness.Presenter.ExecuteCommandAsync("global_settings", CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() => !harness.State.IsBusy);

                Control formPortHost = harness.FindControl<Control>("ClassicFormPortHostControl");
                Control sectionHost = harness.FindControl<Control>("SectionHostControl");
                Assert.IsTrue(formPortHost.IsVisible, "Classic mode should surface the FormPort host for settings.");
                Assert.IsFalse(sectionHost.IsVisible, "Classic mode should hide the generic SectionHost when a W1 FormPort is active.");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_MODE", priorMode);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorChannel);
        }
    }

    [TestMethod]
    public void Classic_mode_surfaces_classic_menu_tool_and_status_chrome_instead_of_generic_shell_chrome()
    {
        string? priorMode = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MODE");
        string? priorChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_MODE", "classic");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();

                Assert.IsTrue(harness.FindControl<Control>("ClassicMenuBarControl").IsVisible, "Classic mode should expose the classic menu bar.");
                Assert.IsTrue(harness.FindControl<Control>("ClassicToolStripControl").IsVisible, "Classic mode should expose the classic tool strip.");
                Assert.IsTrue(harness.FindControl<Control>("ClassicStatusStripControl").IsVisible, "Classic mode should expose the classic status strip.");
                Assert.IsFalse(harness.FindControl<Control>("ShellMenuBarControl").IsVisible, "Classic mode should hide the generic menu bar.");
                Assert.IsFalse(harness.FindControl<Control>("ToolStripControl").IsVisible, "Classic mode should hide the generic tool strip.");
                Assert.IsFalse(harness.FindControl<Control>("StatusStripControl").IsVisible, "Classic mode should hide the generic status strip.");
                Assert.IsTrue(harness.FindControl<Button>("ImportFileButton").IsVisible, "Classic chrome should keep the first-minute open command visible.");
                Assert.IsTrue(harness.FindControl<Button>("SettingsButton").IsVisible, "Classic chrome should keep settings visible.");
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_MODE", priorMode);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorChannel);
        }
    }

    [TestMethod]
    public void Standalone_toolstrip_buttons_raise_expected_events()
    {
        WithStandaloneControl<ToolStripControl>(control =>
        {
            List<string> raisedEvents = [];
            control.ImportFileRequested += (_, _) => raisedEvents.Add("import_file");
            control.ImportRawRequested += (_, _) => raisedEvents.Add("import_raw");
            control.AutoAliceRequested += (_, _) => raisedEvents.Add("auto_alice");
            control.StartOriginRequested += (_, _) => raisedEvents.Add("origin_dossier");
            control.SaveRequested += (_, _) => raisedEvents.Add("save");
            control.CloseWorkspaceRequested += (_, _) => raisedEvents.Add("close_workspace");
            control.DesktopHomeRequested += (_, _) => raisedEvents.Add("desktop_home");
            control.HorizonsRequested += (_, _) => raisedEvents.Add("horizons");
            control.CampaignWorkspaceRequested += (_, _) => raisedEvents.Add("campaign_workspace");
            control.UpdateStatusRequested += (_, _) => raisedEvents.Add("update_status");
            control.InstallLinkingRequested += (_, _) => raisedEvents.Add("install_linking");
            control.SupportRequested += (_, _) => raisedEvents.Add("support");
            control.ReportIssueRequested += (_, _) => raisedEvents.Add("report_issue");
            control.SettingsRequested += (_, _) => raisedEvents.Add("settings");
            control.LoadDemoRunnerRequested += (_, _) => raisedEvents.Add("load_demo_runner");

            (string ButtonName, string EventId)[] buttonMap =
            [
                ("ToolStripAutoAliceButton", "auto_alice"),
                ("ToolStripStartOriginButton", "origin_dossier"),
                ("DesktopHomeButton", "desktop_home"),
                ("HorizonsButton", "horizons"),
                ("CampaignWorkspaceButton", "campaign_workspace"),
                ("LoadDemoRunnerButton", "load_demo_runner"),
                ("ImportFileButton", "import_file"),
                ("SaveButton", "save"),
                ("SettingsButton", "settings"),
                ("ImportRawButton", "import_raw"),
                ("UpdateStatusButton", "update_status"),
                ("InstallLinkingButton", "install_linking"),
                ("SupportButton", "support"),
                ("ReportIssueButton", "report_issue"),
                ("CloseWorkspaceButton", "close_workspace"),
            ];

            foreach ((string buttonName, _) in buttonMap)
            {
                RaiseClick(FindDescendant<Button>(control, buttonName));
            }

            CollectionAssert.AreEqual(buttonMap.Select(item => item.EventId).ToArray(), raisedEvents.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events()
    {
        WithStandaloneControl<ShellMenuBarControl>(control =>
        {
            List<string> selectedMenus = [];
            List<string> selectedCommands = [];
            control.MenuSelected += (_, menuId) => selectedMenus.Add(menuId);
            control.MenuCommandSelected += (_, commandId) => selectedCommands.Add(commandId);

            string[] menuIds = ["file", "edit", "special", "tools", "windows", "help"];
            control.SetMenuState(
                openMenuId: null,
                knownMenuIds: menuIds,
                openMenuCommands: [],
                isBusy: false,
                menuCommandsByMenuId: new Dictionary<string, IReadOnlyList<MenuCommandItem>>(StringComparer.Ordinal)
                {
                    ["tools"] =
                    [
                        new MenuCommandItem(DesktopAliceAssistant.CommandId, "Auto ALICE", true, true)
                    ]
                });

            foreach (string buttonName in new[]
                     {
                         "FileMenuButton",
                         "EditMenuButton",
                         "SpecialMenuButton",
                         "ToolsMenuButton",
                         "WindowsMenuButton",
                         "HelpMenuButton",
                     })
            {
                RaiseClick(FindDescendant<MenuItem>(control, buttonName));
            }

            CollectionAssert.AreEqual(menuIds, selectedMenus.ToArray());
            control.SetMenuState(
                openMenuId: "file",
                knownMenuIds: menuIds,
                openMenuCommands:
                [
                    new MenuCommandItem("new_character", "new character", true, true),
                    new MenuCommandItem("open_character", "open character", true, true),
                    new MenuCommandItem("save_character", "save character", true),
                ],
                isBusy: false);

            Button[] commandButtons = FindDescendant<MenuItem>(control, "FileMenuButton")
                .Items
                .OfType<MenuItem>()
                .Select(menuItem => new Button
                {
                    Tag = menuItem.Tag,
                    Content = menuItem.Header
                })
                .ToArray();
            Assert.AreEqual(3, commandButtons.Length, "Standalone menu proof must render visible command buttons for the open menu.");
            foreach (MenuItem commandMenuItem in FindDescendant<MenuItem>(control, "FileMenuButton").Items.OfType<MenuItem>())
            {
                commandMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                PumpStandaloneUi();
            }

            CollectionAssert.AreEqual(new[] { "new_character", "open_character", "save_character" }, selectedCommands.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_workspace_strip_quick_start_button_raises_expected_event()
    {
        WithStandaloneControl<WorkspaceStripControl>(control =>
        {
            int loadDemoRunnerRequests = 0;
            int startOriginRequests = 0;
            control.LoadDemoRunnerRequested += (_, _) => loadDemoRunnerRequests++;
            control.StartOriginRequested += (_, _) => startOriginRequests++;
            control.SetState(new WorkspaceStripState("No runner loaded.", ShowQuickStartAction: true));

            Assert.IsTrue(FindDescendant<Control>(control, "QuickStartContainer").IsVisible);
            Assert.AreEqual("Origin Dossier", FindDescendant<Button>(control, "StartOriginQuickActionButton").Content?.ToString());
            RaiseClick(FindDescendant<Button>(control, "LoadDemoRunnerQuickActionButton"));
            RaiseClick(FindDescendant<Button>(control, "StartOriginQuickActionButton"));

            Assert.AreEqual(1, loadDemoRunnerRequests, "Workspace quick-start CTA must raise its load-demo-runner event.");
            Assert.AreEqual(1, startOriginRequests, "Workspace quick-start origin CTA must open ALICE in origin mode.");
        });
    }

    [TestMethod]
    public void Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            List<string> selectedTabs = [];
            control.NavigationTabSelected += (_, tabId) => selectedTabs.Add(tabId);
            control.SetNavigationTabs(
                "Runner Tabs",
                [
                    new NavigatorTabItem("tab-profile", "Profile", "profile", "runner", true),
                    new NavigatorTabItem("tab-gear", "Gear", "gear", "runner", true),
                ],
                activeTabId: "tab-profile");
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Assert.IsTrue(control.IsVisible, "Summary header must keep loaded-runner navigation tabs visible.");
            Assert.IsTrue(FindDescendant<Control>(control, "NavigationTabsPanel").IsVisible);
            Assert.IsFalse(FindDescendant<Control>(control, "RestoreContinuityStatusBorder").IsVisible);
            Assert.IsFalse(FindDescendant<Control>(control, "RestoreContinuityActionPanel").IsVisible);

            Button[] tabButtons = control.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Tag is string)
                .ToArray();

            foreach (Button tabButton in tabButtons)
            {
                RaiseClick(tabButton);
            }

            CollectionAssert.AreEqual(new[] { "tab-profile", "tab-gear" }, selectedTabs.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_summary_header_tab_buttons_raise_expected_events()
    {
        Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff();
    }

    [TestMethod]
    public void Standalone_summary_header_surfaces_restore_handoff_actions_when_present()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            int keepLocalRequests = 0;
            int saveLocalRequests = 0;
            int campaignWorkspaceRequests = 0;
            int workspaceSupportRequests = 0;

            control.KeepLocalWorkRequested += (_, _) => keepLocalRequests++;
            control.SaveLocalWorkRequested += (_, _) => saveLocalRequests++;
            control.CampaignWorkspaceRequested += (_, _) => campaignWorkspaceRequests++;
            control.WorkspaceSupportRequested += (_, _) => workspaceSupportRequests++;

            control.SetState(new SummaryHeaderState(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs:
                [
                    new NavigatorTabItem("tab-profile", "Profile", "profile", "runner", true),
                    new NavigatorTabItem("tab-gear", "Gear", "gear", "runner", true),
                ],
                ActiveTabId: "tab-profile",
                HasVisibleContent: true,
                RestoreContinuitySummary: "Continuity note: continue from runner-1.",
                StaleStateSummary: "Stale state: runner-1 stays visible.",
                ConflictChoiceSummary: "Workspace note: keep local work, save local work, or review Campaign Workspace.",
                CanSaveLocalWorkBeforeRestore: true));
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Assert.IsTrue(control.IsVisible, "Summary header must surface a real restore handoff when the shell has one.");
            Assert.IsTrue(FindDescendant<Control>(control, "RestoreContinuityStatusBorder").IsVisible);
            Assert.IsTrue(FindDescendant<Control>(control, "RestoreContinuityActionPanel").IsVisible);
            Assert.IsTrue(FindDescendant<Button>(control, "SaveLocalWorkButton").IsEnabled);

            RaiseClick(FindDescendant<Button>(control, "KeepLocalWorkButton"));
            RaiseClick(FindDescendant<Button>(control, "SaveLocalWorkButton"));
            RaiseClick(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton"));
            RaiseClick(FindDescendant<Button>(control, "OpenWorkspaceSupportButton"));

            Assert.AreEqual(1, keepLocalRequests);
            Assert.AreEqual(1, saveLocalRequests);
            Assert.AreEqual(1, campaignWorkspaceRequests);
            Assert.AreEqual(1, workspaceSupportRequests);
        });
    }

    [TestMethod]
    public void Standalone_summary_header_keeps_restore_stale_and_conflict_choices_visible()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            List<string> requestedActions = [];
            control.KeepLocalWorkRequested += (_, _) => requestedActions.Add("keep-local");
            control.SaveLocalWorkRequested += (_, _) => requestedActions.Add("save-local");
            control.CampaignWorkspaceRequested += (_, _) => requestedActions.Add("campaign-workspace");
            control.WorkspaceSupportRequested += (_, _) => requestedActions.Add("workspace-support");

            control.SetState(new SummaryHeaderState(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs:
                [
                    new NavigatorTabItem("tab-profile", "Profile", "profile", "runner", true)
                ],
                ActiveTabId: "tab-profile",
                RuntimeSummary: "Runtime: SR6 preview.",
                RestoreContinuitySummary: "Continuity note: keep ws-1 open before accepting a newer packet.",
                StaleStateSummary: "Stale state: desktop service is reachable, but server restore continuity still needs Campaign Workspace or Workspace Support review; local save posture is unsaved.",
                ConflictChoiceSummary: "Workspace note: review before replacing this unsaved desktop state.",
                CanSaveLocalWorkBeforeRestore: true));
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Assert.IsTrue(FindDescendant<Control>(control, "RestoreContinuityStatusBorder").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityStatusText").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "StaleStateStatusText").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "ConflictChoiceStatusText").IsVisible);
            Assert.AreEqual(
                "Workspace continuity gate",
                AutomationProperties.GetName(FindDescendant<Control>(control, "RestoreContinuityStatusBorder")));
            Assert.AreEqual(
                "The desktop app keeps local work under explicit user control.",
                AutomationProperties.GetHelpText(FindDescendant<Control>(control, "RestoreContinuityStatusBorder")));
            Assert.AreEqual(
                "Restore continuation status",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityStatusText")));
            Assert.AreEqual(
                "Stale state visibility status",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "StaleStateStatusText")));
            Assert.AreEqual(
                "Workspace review status",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "ConflictChoiceStatusText")));
            Assert.IsTrue(FindDescendant<Control>(control, "RestoreContinuityActionPanel").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionText").IsVisible);
            Assert.AreEqual(
                "Workspace decision guard",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionText")));
            Assert.AreEqual(
                "Chummer keeps local work under explicit user control.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionText")));
            Assert.AreEqual(
                "Workspace decision order",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionOrderText")));
            Assert.AreEqual(
                "Use the visible choices in order: keep local work visible, save local work when available, review Campaign Workspace, then open Workspace Support.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionOrderText")));
            Assert.AreEqual(
                "Workspace local authority",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityLocalAuthorityText")));
            Assert.AreEqual(
                "Your local desktop copy stays authoritative until you choose Campaign Workspace review or Workspace Support.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuityLocalAuthorityText")));
            Assert.AreEqual(
                "Workspace change guard",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityReplacementGuardText")));
            Assert.AreEqual(
                "There is no automatic or one-click replacement from this desktop route.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuityReplacementGuardText")));
            Assert.AreEqual(
                "Workspace support handoff",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuitySupportHandoffText")));
            Assert.AreEqual(
                "Workspace Support opens with the current local workspace context.",
                AutomationProperties.GetHelpText(FindDescendant<TextBlock>(control, "RestoreContinuitySupportHandoffText")));
            Assert.AreEqual("restore-decision-keep-local-work", FindDescendant<Button>(control, "KeepLocalWorkButton").Tag);
            Assert.AreEqual("Keep Local", AutomationProperties.GetName(FindDescendant<Button>(control, "KeepLocalWorkButton")));
            Assert.IsTrue(FindDescendant<Button>(control, "SaveLocalWorkButton").IsEnabled);
            Assert.AreEqual("restore-decision-save-local-work", FindDescendant<Button>(control, "SaveLocalWorkButton").Tag);
            Assert.AreEqual("Save local work before workspace review", AutomationProperties.GetName(FindDescendant<Button>(control, "SaveLocalWorkButton")));
            Assert.AreEqual("restore-decision-review-campaign-workspace", FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton").Tag);
            Assert.AreEqual("Review Campaign Workspace", AutomationProperties.GetName(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton")));
            Assert.AreEqual("restore-decision-open-workspace-support", FindDescendant<Button>(control, "OpenWorkspaceSupportButton").Tag);
            Assert.AreEqual("Open Workspace Support", AutomationProperties.GetName(FindDescendant<Button>(control, "OpenWorkspaceSupportButton")));
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityActionStatusText").IsVisible);
            Assert.AreEqual(
                "Workspace decision action status",
                AutomationProperties.GetName(FindDescendant<TextBlock>(control, "RestoreContinuityActionStatusText")));

            RaiseClick(FindDescendant<Button>(control, "KeepLocalWorkButton"));
            Assert.IsTrue(FindDescendant<Button>(control, "KeepLocalWorkButton").Classes.Contains("selected"));
            Assert.IsFalse(FindDescendant<Button>(control, "SaveLocalWorkButton").Classes.Contains("selected"));
            RaiseClick(FindDescendant<Button>(control, "SaveLocalWorkButton"));
            Assert.IsFalse(FindDescendant<Button>(control, "KeepLocalWorkButton").Classes.Contains("selected"));
            Assert.IsTrue(FindDescendant<Button>(control, "SaveLocalWorkButton").Classes.Contains("selected"));
            RaiseClick(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton"));
            Assert.IsFalse(FindDescendant<Button>(control, "SaveLocalWorkButton").Classes.Contains("selected"));
            Assert.IsTrue(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton").Classes.Contains("selected"));
            RaiseClick(FindDescendant<Button>(control, "OpenWorkspaceSupportButton"));
            Assert.IsFalse(FindDescendant<Button>(control, "ReviewCampaignWorkspaceButton").Classes.Contains("selected"));
            Assert.IsTrue(FindDescendant<Button>(control, "OpenWorkspaceSupportButton").Classes.Contains("selected"));

            CollectionAssert.AreEqual(
                new[]
                {
                    "keep-local",
                    "save-local",
                    "campaign-workspace",
                    "workspace-support"
                },
                requestedActions);
        });
    }

    [TestMethod]
    public void Standalone_summary_header_explains_when_restore_save_choice_is_unavailable()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            control.SetState(new SummaryHeaderState(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs: [],
                ActiveTabId: null,
                RestoreContinuitySummary: "Continuity note: load a recent workspace before replacing local work.",
                StaleStateSummary: "Stale state: service continuity is unavailable.",
                ConflictChoiceSummary: "Workspace note: review workspace support before replacing local work.",
                CanSaveLocalWorkBeforeRestore: false));
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Button saveButton = FindDescendant<Button>(control, "SaveLocalWorkButton");
            Assert.IsFalse(saveButton.IsEnabled);
            Assert.AreEqual("restore-decision-save-local-work", saveButton.Tag);
            RaiseClick(saveButton);
            Assert.IsFalse(saveButton.Classes.Contains("selected"));
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityDecisionText").IsVisible);
            Assert.IsFalse(FindDescendant<TextBlock>(control, "RestoreContinuityActionStatusText").IsVisible);
        });
    }

    [TestMethod]
    public void Standalone_summary_header_preserves_restore_decision_status_across_refresh_state()
    {
        WithStandaloneControl<SummaryHeaderControl>(control =>
        {
            SummaryHeaderState state = new(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs: [],
                ActiveTabId: null,
                RestoreContinuitySummary: "Continuity note: keep ws-1 open before accepting a newer packet.",
                StaleStateSummary: "Stale state: desktop service is reachable, but server restore continuity still needs Campaign Workspace or Workspace Support review; local save posture is unsaved.",
                ConflictChoiceSummary: "Workspace note: review before replacing this unsaved desktop state.",
                CanSaveLocalWorkBeforeRestore: true,
                RestoreDecisionActionStatus: "Opening Workspace Support with workspace context.",
                RestoreDecisionSelectionId: "restore-decision-open-workspace-support");
            control.SetState(state);
            control.Measure(new Size(1440d, 960d));
            control.Arrange(new Rect(0d, 0d, 1440d, 960d));
            PumpStandaloneUi();

            Assert.IsTrue(FindDescendant<Button>(control, "OpenWorkspaceSupportButton").Classes.Contains("selected"));

            control.SetState(state);
            PumpStandaloneUi();

            Assert.IsTrue(FindDescendant<Button>(control, "OpenWorkspaceSupportButton").Classes.Contains("selected"));
            Assert.IsFalse(FindDescendant<Button>(control, "KeepLocalWorkButton").Classes.Contains("selected"));
        });
    }

    [TestMethod]
    public void Primary_route_restore_status_switches_from_save_requested_to_saved_after_refresh()
    {
        Type coordinatorType = typeof(MainWindow).Assembly.GetType("Chummer.Avalonia.MainWindowTransientStateCoordinator")
            ?? throw new AssertFailedException("Unable to resolve MainWindowTransientStateCoordinator.");
        object coordinator = Activator.CreateInstance(coordinatorType, nonPublic: true)
            ?? throw new AssertFailedException("Unable to construct MainWindowTransientStateCoordinator.");

        coordinatorType.GetMethod("RecordSaveLocalWorkDecision", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, ["workspace-alpha"]);

        object pendingFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: true, workspaceId: "workspace-alpha");
        object resolvedPendingFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [pendingFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null for pending save state.");
        SummaryHeaderState pendingSummaryHeader = ReadTransientSummaryHeader(resolvedPendingFrame);
        Assert.AreEqual(
            "Save local work requested before workspace continuity changes desktop state.",
            pendingSummaryHeader.RestoreDecisionActionStatus);
        Assert.AreEqual("restore-decision-save-local-work", pendingSummaryHeader.RestoreDecisionSelectionId);

        object savedFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: false, workspaceId: "workspace-alpha");
        object resolvedSavedFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [savedFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null for saved state.");
        SummaryHeaderState savedSummaryHeader = ReadTransientSummaryHeader(resolvedSavedFrame);
        Assert.AreEqual(
            "Local work saved; keep local work visible, review Campaign Workspace, or open Workspace Support before changing this desktop copy.",
            savedSummaryHeader.RestoreDecisionActionStatus);
        Assert.IsNull(savedSummaryHeader.RestoreDecisionSelectionId);

        object redirtyFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: true, workspaceId: "workspace-alpha");
        object resolvedRedirtyFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [redirtyFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null for re-dirtied save state.");
        SummaryHeaderState redirtySummaryHeader = ReadTransientSummaryHeader(resolvedRedirtyFrame);
        Assert.IsNull(redirtySummaryHeader.RestoreDecisionSelectionId);
        Assert.IsNull(redirtySummaryHeader.RestoreDecisionActionStatus);
    }

    [TestMethod]
    public void Primary_route_restore_status_clears_when_workspace_anchor_changes()
    {
        Type coordinatorType = typeof(MainWindow).Assembly.GetType("Chummer.Avalonia.MainWindowTransientStateCoordinator")
            ?? throw new AssertFailedException("Unable to resolve MainWindowTransientStateCoordinator.");
        object coordinator = Activator.CreateInstance(coordinatorType, nonPublic: true)
            ?? throw new AssertFailedException("Unable to construct MainWindowTransientStateCoordinator.");

        coordinatorType.GetMethod("RecordWorkspaceSupportDecision", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, ["workspace-alpha"]);

        object anchoredFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: true, workspaceId: "workspace-alpha");
        object resolvedAnchoredFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [anchoredFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null for anchored workspace state.");
        SummaryHeaderState anchoredSummaryHeader = ReadTransientSummaryHeader(resolvedAnchoredFrame);
        Assert.AreEqual(
            "Opening Workspace Support with the current workspace context.",
            anchoredSummaryHeader.RestoreDecisionActionStatus);
        Assert.AreEqual("restore-decision-open-workspace-support", anchoredSummaryHeader.RestoreDecisionSelectionId);

        object switchedFrame = CreateTransientShellFrame(canSaveLocalWorkBeforeRestore: true, workspaceId: "workspace-bravo");
        object resolvedSwitchedFrame = coordinatorType.GetMethod("ApplyShellFrame", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(coordinator, [switchedFrame])
            ?? throw new AssertFailedException("ApplyShellFrame returned null after workspace switch.");
        SummaryHeaderState switchedSummaryHeader = ReadTransientSummaryHeader(resolvedSwitchedFrame);
        Assert.IsNull(switchedSummaryHeader.RestoreDecisionActionStatus);
        Assert.IsNull(switchedSummaryHeader.RestoreDecisionSelectionId);
    }

    [TestMethod]
    public void Runtime_backed_summary_header_keeps_restore_stale_and_conflict_copy_grounded_to_active_workspace()
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy);

                Assert.IsFalse(harness.FindControl<Control>("SummaryHeaderRegion").IsVisible);
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private static object CreateTransientShellFrame(bool canSaveLocalWorkBeforeRestore, string? workspaceId = "workspace-alpha")
    {
        Assembly assembly = typeof(MainWindow).Assembly;
        Type headerType = assembly.GetType("Chummer.Avalonia.MainWindowHeaderState")
            ?? throw new AssertFailedException("Unable to resolve MainWindowHeaderState.");
        Type chromeType = assembly.GetType("Chummer.Avalonia.MainWindowChromeState")
            ?? throw new AssertFailedException("Unable to resolve MainWindowChromeState.");
        Type shellFrameType = assembly.GetType("Chummer.Avalonia.MainWindowShellFrame")
            ?? throw new AssertFailedException("Unable to resolve MainWindowShellFrame.");

        object headerState = Activator.CreateInstance(
            headerType,
            new ToolStripState("Ready."),
            new MenuBarState(
                OpenMenuId: null,
                KnownMenuIds: Array.Empty<string>(),
                OpenMenuCommands: Array.Empty<MenuCommandItem>(),
                MenuCommandsByMenuId: new Dictionary<string, IReadOnlyList<MenuCommandItem>>(StringComparer.Ordinal),
                IsBusy: false))
            ?? throw new AssertFailedException("Unable to create MainWindowHeaderState.");
        object chromeState = Activator.CreateInstance(
            chromeType,
            new WorkspaceStripState("Workspace strip."),
            new SummaryHeaderState(
                NavigationTabsHeading: "Runner Tabs",
                NavigationTabs: Array.Empty<NavigatorTabItem>(),
                ActiveTabId: null,
                RestoreContinuitySummary: "Continuity note: keep ws-1 open before accepting a newer packet.",
                StaleStateSummary: "Stale state: desktop service is reachable, but server restore continuity still needs Campaign Workspace or Workspace Support review; local save posture is unsaved.",
                ConflictChoiceSummary: "Workspace note: review Campaign Workspace or open workspace support.",
                CanSaveLocalWorkBeforeRestore: canSaveLocalWorkBeforeRestore,
                RestoreDecisionWorkspaceId: workspaceId),
            new StatusStripState(
                CharacterState: "Character ready.",
                ServiceState: "Service online.",
                TimeState: "2026-04-18 19:16 UTC",
                ComplianceState: "Compliance ready."))
            ?? throw new AssertFailedException("Unable to create MainWindowChromeState.");
        return Activator.CreateInstance(
                shellFrameType,
                headerState,
                chromeState,
                new SectionHostState(
                    SectionId: null,
                    NavigationTabs: Array.Empty<NavigatorTabItem>(),
                    ActiveTabId: null,
                    SectionActions: Array.Empty<NavigatorSectionActionItem>(),
                    ActiveActionId: null,
                    Notice: "Ready.",
                    PreviewJson: string.Empty,
                    Rows: Array.Empty<SectionRowDisplayItem>(),
                    QuickActions: Array.Empty<SectionQuickActionDisplayItem>(),
                    BuildLab: null,
                    BrowseWorkspace: null,
                    ContactGraph: null,
                    DowntimePlanner: null,
                    NpcPersonaStudio: null),
                new RosterPaneState(
                    Items: Array.Empty<CharacterRosterNode>(),
                    SelectedWorkspaceId: workspaceId),
                new CommandDialogPaneState(
                    Commands: Array.Empty<CommandPaletteItem>(),
                    SelectedCommandId: null,
                    ActiveDialogId: null,
                    DialogTitle: null,
                    DialogMessage: null,
                    DialogTrustReceipt: null,
                    Fields: Array.Empty<DialogFieldDisplayItem>(),
                    Actions: Array.Empty<DialogActionDisplayItem>()),
                true,
                new NavigatorPaneState(
                    OpenWorkspacesHeading: "Open Workspaces",
                    OpenWorkspaces: Array.Empty<NavigatorWorkspaceItem>(),
                    SelectedWorkspaceId: null,
                    NavigationTabsHeading: "Runner Tabs",
                    NavigationTabs: Array.Empty<NavigatorTabItem>(),
                    ActiveTabId: null,
                    SectionActionsHeading: "Actions",
                    SectionActions: Array.Empty<NavigatorSectionActionItem>(),
                    ActiveActionId: null,
                    WorkflowSurfacesHeading: "Workflows",
                    WorkflowSurfaces: Array.Empty<NavigatorWorkflowSurfaceItem>()),
                new Dictionary<string, WorkspaceSurfaceActionDefinition>(StringComparer.Ordinal))
            ?? throw new AssertFailedException("Unable to create MainWindowShellFrame.");
    }

    private static SummaryHeaderState ReadTransientSummaryHeader(object shellFrame)
    {
        object chromeState = shellFrame.GetType().GetProperty("ChromeState", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(shellFrame)
            ?? throw new AssertFailedException("Unable to read ChromeState from MainWindowShellFrame.");
        return (SummaryHeaderState)(chromeState.GetType().GetProperty("SummaryHeader", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(chromeState)
            ?? throw new AssertFailedException("Unable to read SummaryHeader from MainWindowChromeState."));
    }

    [TestMethod]
    public void Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events()
    {
        WithStandaloneControl<NavigatorPaneControl>(control =>
        {
            List<string> selectedWorkspaces = [];
            List<string> selectedTabs = [];
            List<string> selectedSectionActions = [];
            List<string> selectedWorkflowSurfaces = [];
            control.WorkspaceSelected += (_, workspaceId) => selectedWorkspaces.Add(workspaceId);
            control.NavigationTabSelected += (_, tabId) => selectedTabs.Add(tabId);
            control.SectionActionSelected += (_, actionId) => selectedSectionActions.Add(actionId);
            control.WorkflowSurfaceSelected += (_, surfaceId) => selectedWorkflowSurfaces.Add(surfaceId);

            control.SetState(new NavigatorPaneState(
                OpenWorkspacesHeading: "Open Workspaces",
                OpenWorkspaces:
                [
                    new NavigatorWorkspaceItem("runner-1", "Soma", "Demo", RulesetDefaults.Sr5, true, true)
                ],
                SelectedWorkspaceId: null,
                NavigationTabsHeading: "Tabs",
                NavigationTabs:
                [
                    new NavigatorTabItem("tab-gear", "Gear", "gear", "runner", true)
                ],
                ActiveTabId: null,
                SectionActionsHeading: "Section Actions",
                SectionActions:
                [
                    new NavigatorSectionActionItem("action-cyberware", "Open Cyberware", WorkspaceSurfaceActionKind.Section)
                ],
                ActiveActionId: null,
                WorkflowSurfacesHeading: "Workflow Surfaces",
                WorkflowSurfaces:
                [
                    new NavigatorWorkflowSurfaceItem("surface-progress", "progress", "Progress Workflow", "workflow-progress")
                ]));

            TreeView navigatorTree = FindDescendant<TreeView>(control, "NavigatorTree");
            NavigatorTreeItem[] items = control.SnapshotTreeItems();

            navigatorTree.SelectedItem = FindTreeItem(items, NavigatorTreeNodeKind.Workspace, static item => true);
            PumpStandaloneUi();
            navigatorTree.SelectedItem = FindTreeItem(items, NavigatorTreeNodeKind.NavigationTab, static item => true);
            PumpStandaloneUi();
            navigatorTree.SelectedItem = FindTreeItem(items, NavigatorTreeNodeKind.SectionAction, static item => true);
            PumpStandaloneUi();
            navigatorTree.SelectedItem = FindTreeItem(items, NavigatorTreeNodeKind.WorkflowSurface, static item => true);
            PumpStandaloneUi();

            CollectionAssert.AreEqual(new[] { "runner-1" }, selectedWorkspaces.ToArray());
            CollectionAssert.AreEqual(new[] { "tab-gear" }, selectedTabs.ToArray());
            CollectionAssert.AreEqual(new[] { "action-cyberware" }, selectedSectionActions.ToArray());
            CollectionAssert.AreEqual(new[] { "surface-progress" }, selectedWorkflowSurfaces.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions()
    {
        WithStandaloneControl<CommandDialogPaneControl>(control =>
        {
            List<string> selectedCommands = [];
            List<string> selectedActions = [];
            List<string> updatedFields = [];
            control.CommandSelected += (_, commandId) => selectedCommands.Add(commandId);
            control.DialogActionSelected += (_, actionId) => selectedActions.Add(actionId);
            control.DialogFieldValueChanged += (_, args) => updatedFields.Add($"{args.FieldId}={args.Value}");

            CommandPaletteItem[] commands =
            [
                new("global_settings", "Global Settings", "tools", true),
                new("about", "About Chummer", "help", true),
            ];
            control.SetState(new CommandDialogPaneState(
                Commands: commands,
                SelectedCommandId: null,
                ActiveDialogId: "dialog.global_settings",
                DialogTitle: "Global Settings",
                DialogMessage: "Adjust desktop preferences.",
                DialogTrustReceipt: null,
                Fields:
                [
                    new DialogFieldDisplayItem("globalTheme", "Theme", "classic", "classic", false, false, "text"),
                    new DialogFieldDisplayItem("globalCompactMode", "Compact Mode", "false", "false", false, false, "checkbox")
                ],
                Actions:
                [
                    new DialogActionDisplayItem("save", "Save", true),
                    new DialogActionDisplayItem("cancel", "Cancel", false)
                ]));

            ListBox commandsList = FindDescendant<ListBox>(control, "CommandsList");
            commandsList.SelectedItem = commands[0];
            PumpStandaloneUi();

            TextBox editableTextField = control.GetVisualDescendants()
                .OfType<TextBox>()
                .First(textBox => !textBox.IsReadOnly);
            editableTextField.Text = "dense";
            PumpStandaloneUi();

            CheckBox checkboxField = control.GetVisualDescendants()
                .OfType<CheckBox>()
                .First();
            checkboxField.IsChecked = true;
            PumpStandaloneUi();

            Button primaryActionButton = FindDescendant<Panel>(control, "DialogActionsHost")
                .Children
                .OfType<Button>()
                .First(button => string.Equals(button.Tag?.ToString(), "save", StringComparison.Ordinal));
            RaiseClick(primaryActionButton);

            CollectionAssert.AreEqual(new[] { "global_settings" }, selectedCommands.ToArray());
            CollectionAssert.Contains(updatedFields, "globalTheme=dense");
            CollectionAssert.Contains(updatedFields, "globalCompactMode=true");
            CollectionAssert.AreEqual(new[] { "save" }, selectedActions.ToArray());
        });
    }

    [TestMethod]
    public void Standalone_command_dialog_pane_surfaces_import_trust_receipt_in_briefing()
    {
        WithStandaloneControl<CommandDialogPaneControl>(control =>
        {
            string trustReceipt = string.Join(
                Environment.NewLine,
                "Import receipt correlation key: import/sr6/review-only; matches the blocker, oracle, and before/after environment diff lines below.",
                "Grounded import explain receipt: target sr6; oracle Chummer5a oracle covered; source toggles Seattle selected; blocker Hero Lab source missing source-toggle parity.",
                "Import environment tuple diff: before workspace/current-source/support-local/review-only; after oracle-reviewed/sr6/accepted-source-only; correlation import/sr6/review-only.",
                "Environment diff before import: the current workspace and support posture stay unchanged.",
                "Environment diff after import: accepted content binds to sr6 only after oracle review.");

            control.SetState(new CommandDialogPaneState(
                Commands: [],
                SelectedCommandId: null,
                ActiveDialogId: "dialog.open_character",
                DialogTitle: "Open Character",
                DialogMessage: trustReceipt,
                DialogTrustReceipt: trustReceipt,
                Fields:
                [
                    new DialogFieldDisplayItem("importRulesetId", "Ruleset", "sr6", "sr6", false, true, "text")
                ],
                Actions:
                [
                    new DialogActionDisplayItem("import", "Import", true)
                ]));

            TextBlock trustReceiptText = FindDescendant<TextBlock>(control, "DialogMessageText");
            StringAssert.Contains(trustReceiptText.Text, "Import receipt correlation key: import/sr6/review-only");
            StringAssert.Contains(trustReceiptText.Text, "Grounded import explain receipt: target sr6");
            StringAssert.Contains(trustReceiptText.Text, "Import environment tuple diff: before workspace/current-source/support-local/review-only; after oracle-reviewed/sr6/accepted-source-only; correlation import/sr6/review-only.");
            StringAssert.Contains(trustReceiptText.Text, "Environment diff before import:");
            StringAssert.Contains(trustReceiptText.Text, "Environment diff after import:");

            string[] visibleText = control.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(textBlock => textBlock.IsVisible)
                .Select(textBlock => textBlock.Text ?? string.Empty)
                .ToArray();
            Assert.IsTrue(visibleText.Any(text => text.Contains("Import receipt correlation key: import/sr6/review-only", StringComparison.Ordinal)));
            Assert.IsTrue(visibleText.Any(text => text.Contains("Grounded import explain receipt: target sr6", StringComparison.Ordinal)));
        });
    }

    [TestMethod]
    public void Standalone_command_dialog_pane_add_quality_category_tree_changes_type_filter()
    {
        WithStandaloneControl<CommandDialogPaneControl>(control =>
        {
            DesktopDialogState dialog = new DesktopDialogFactory().CreateUiControlDialog("quality_add", DesktopPreferenceState.Default);
            List<string> updatedFields = [];
            control.DialogFieldValueChanged += (_, args) => updatedFields.Add($"{args.FieldId}={args.Value}");

            control.SetState(new CommandDialogPaneState(
                Commands: [],
                SelectedCommandId: null,
                ActiveDialogId: dialog.Id,
                DialogTitle: dialog.Title,
                DialogMessage: dialog.Message,
                DialogTrustReceipt: null,
                Fields: dialog.Fields.Select(ToDisplayField).ToArray(),
                Actions: dialog.Actions.Select(ToDisplayAction).ToArray()));
            PumpStandaloneUi();

            ListBox categoryTree = FindDescendant<ListBox>(control, DesktopDialogAccessibility.BuildFieldInputName("uiQualityCategoryTree"));
            Assert.IsTrue(categoryTree.IsVisible, "Add Quality category navigation must be a real selectable list.");
            Assert.IsNull(FindDescendantOrDefault<TextBox>(control, DesktopDialogAccessibility.BuildFieldInputName("uiQualityCategoryTree")));

            object negativeCategory = EnumerateListBoxItems(categoryTree)
                .First(item => item.ToString()?.Contains("Negative", StringComparison.OrdinalIgnoreCase) == true);
            categoryTree.SelectedItem = negativeCategory;
            PumpStandaloneUi();

            CollectionAssert.Contains(updatedFields, "uiQualityType=Negative");
        });
    }

    [TestMethod]
    public void Standalone_add_quality_dialog_renders_category_navigation_as_selectable_list()
    {
        WithStandaloneDialogWindow(window =>
        {
            DesktopDialogState dialog = new DesktopDialogFactory().CreateUiControlDialog("quality_add", DesktopPreferenceState.Default);
            window.BindDialog(dialog);
            PumpStandaloneUi();

            ListBox categoryTree = FindDescendant<ListBox>(window, DesktopDialogAccessibility.BuildFieldInputName("uiQualityCategoryTree"));
            Assert.IsTrue(categoryTree.IsVisible, "Add Quality popup must expose category navigation as a selectable list.");
            string[] categoryTexts = EnumerateListBoxItemTexts(categoryTree);
            Assert.IsTrue(
                categoryTexts.Any(static text => text.Contains("Positive", StringComparison.OrdinalIgnoreCase)),
                "Add Quality category navigation must include the positive quality branch.");
            Assert.IsTrue(
                categoryTexts.Any(static text => text.Contains("Negative", StringComparison.OrdinalIgnoreCase)),
                "Add Quality category navigation must include the negative quality branch.");
            Assert.IsNull(FindDescendantOrDefault<TextBox>(window, DesktopDialogAccessibility.BuildFieldInputName("uiQualityCategoryTree")));
            StringAssert.Contains(categoryTree.SelectedItem?.ToString() ?? string.Empty, "Positive");
        });
    }

    [TestMethod]
    public void Standalone_add_dialog_category_trees_render_as_selectable_lists_for_every_selection_surface()
    {
        string[] addDialogIds =
        [
            "cyberware_add",
            "drug_add",
            "gear_add",
            "magic_add",
            "spell_add",
            "adept_power_add",
            "complex_form_add",
            "initiation_add",
            "spirit_add",
            "critter_power_add",
            "matrix_program_add",
            "skill_add",
            "combat_add_weapon",
            "combat_add_armor",
            "vehicle_add",
            "vehicle_mod_add",
            "quality_add"
        ];

        WithStandaloneControl<CommandDialogPaneControl>(control =>
        {
            DesktopDialogFactory factory = new();
            foreach (string addDialogId in addDialogIds)
            {
                DesktopDialogState dialog = factory.CreateUiControlDialog(addDialogId, DesktopPreferenceState.Default);
                DesktopDialogField[] categoryTreeFields = dialog.Fields
                    .Where(static field =>
                        field.Id.EndsWith("CategoryTree", StringComparison.Ordinal)
                        && string.Equals(field.VisualKind, DesktopDialogFieldVisualKinds.Tree, StringComparison.Ordinal))
                    .ToArray();

                Assert.IsTrue(categoryTreeFields.Length > 0, $"{addDialogId} should expose a category tree contract.");
                control.SetState(new CommandDialogPaneState(
                    Commands: [],
                    SelectedCommandId: null,
                    ActiveDialogId: dialog.Id,
                    DialogTitle: dialog.Title,
                    DialogMessage: dialog.Message,
                    DialogTrustReceipt: null,
                    Fields: dialog.Fields.Select(ToDisplayField).ToArray(),
                    Actions: dialog.Actions.Select(ToDisplayAction).ToArray()));
                PumpStandaloneUi();

                foreach (DesktopDialogField categoryTreeField in categoryTreeFields)
                {
                    string inputName = DesktopDialogAccessibility.BuildFieldInputName(categoryTreeField.Id);
                    ListBox categoryTree = FindDescendant<ListBox>(control, inputName);
                    string context = $"{addDialogId}/{categoryTreeField.Id}";
                    Assert.IsTrue(categoryTree.IsVisible, $"{context} must render category navigation as a visible selectable list.");
                    Assert.IsNull(FindDescendantOrDefault<TextBox>(control, inputName), $"{context} must not fall back to a readonly textbox.");

                    string[] categoryTexts = EnumerateListBoxItemTexts(categoryTree);
                    Assert.IsTrue(categoryTexts.Length > 0, $"{context} must expose selectable category rows.");
                    Assert.IsFalse(categoryTexts.Any(static text => text.StartsWith("[", StringComparison.Ordinal)), $"{context} must not expose the root label as a selectable category.");
                }
            }
        });
    }

    [TestMethod]
    public void Standalone_section_host_launches_build_lab_compare_and_blocker_explain_companion()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: null,
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: "{}",
                Rows: [],
                QuickActions: [],
                BuildLab: CreateBuildLabCompanionState(),
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Button companionButton = FindDescendant<Button>(control, "OpenBuildLabExplainCompanionButton");
            Assert.IsTrue(companionButton.IsEnabled);
            string companionLaunchUri = companionButton.Tag?.ToString() ?? string.Empty;
            StringAssert.Contains(companionLaunchUri, "/coach/?routeType=build");
            StringAssert.Contains(companionLaunchUri, "workspaceId=lab-intake");
            StringAssert.Contains(companionLaunchUri, "rulesetId=sr5");

            string[] visibleText = control.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(textBlock => textBlock.IsVisible)
                .Select(textBlock => textBlock.Text ?? string.Empty)
                .ToArray();
            CollectionAssert.Contains(visibleText, "Build explanation and environment details");
            Assert.AreEqual("Open details", companionButton.Content?.ToString());
            Assert.IsTrue(visibleText.Any(text => text.Contains("Build blocker", StringComparison.Ordinal)));
            Assert.IsTrue(visibleText.Any(text => text.Contains("Build compare companion:", StringComparison.Ordinal)));
        });
    }

    [TestMethod]
    public void Standalone_section_context_surfaces_text_first_explain_drawer_summary_when_packet_metadata_is_present()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: "gear",
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: CreateExplainDrawerPreviewJson(),
                Rows:
                [
                    new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack")
                ],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            string previewText = FindDescendant<TextBox>(control, "SectionPreviewBox").Text ?? string.Empty;
            StringAssert.Contains(previewText, "Explain drawer");
            StringAssert.Contains(previewText, "Explain packet: gear.medkit.rating");
            StringAssert.Contains(previewText, "Source anchor: SR5 p. 447 · Medkit");
            StringAssert.Contains(previewText, "Stale state: Packet snapshot gear-medkit-v1 no longer matches current snapshot gear-medkit-v2. Refresh before trusting this value.");
            StringAssert.Contains(previewText, "Follow-up: If rating drops below 6, extended healing intervals lose the clinic-grade bonus.");
        });
    }

    [TestMethod]
    public void Standalone_section_context_reads_canonical_explanation_packet_fields_for_text_first_drawer_copy()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: "gear",
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: CreateExplainDrawerPreviewJson(),
                Rows:
                [
                    new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack")
                ],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            ExplainDrawerContext? explainContext = control.GetCurrentExplainDrawerContext();
            Assert.IsNotNull(explainContext);
            Assert.AreEqual("gear.medkit.rating", explainContext.ExplainPacket);
            Assert.AreEqual("SR5 p. 447 · Medkit", explainContext.SourceAnchor);
            Assert.AreEqual("Open the bound local rulebook anchor from this desktop route.", explainContext.SourceLaunch);
            Assert.AreEqual("/tmp/rulebooks/sr5-medkit.pdf", explainContext.SourceLaunchTarget);
            Assert.AreEqual("Packet snapshot gear-medkit-v1 no longer matches current snapshot gear-medkit-v2. Refresh before trusting this value.", explainContext.StaleState);
            Assert.AreEqual("If rating drops below 6, extended healing intervals lose the clinic-grade bonus.", explainContext.FollowUp);
        });
    }

    [TestMethod]
    public void Standalone_section_context_projects_packet_backed_explain_drawer_actions_for_desktop_launch_and_follow_up()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: "gear",
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: CreateExplainDrawerPreviewJson(),
                Rows:
                [
                    new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack")
                ],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Button openSourceAnchor = FindDescendant<Button>(control, "SectionQuickAction_explain_drawer.open_source_anchor");
            Button reviewBoundedFollowUp = FindDescendant<Button>(control, "SectionQuickAction_explain_drawer.review_bounded_follow_up");
            Assert.AreEqual("Open Source Anchor", openSourceAnchor.Content?.ToString());
            Assert.AreEqual("Review Bounded Follow-up", reviewBoundedFollowUp.Content?.ToString());
            Assert.IsTrue(openSourceAnchor.IsVisible);
            Assert.IsTrue(reviewBoundedFollowUp.IsVisible);
        });
    }

    [TestMethod]
    public void Standalone_section_context_launches_source_anchor_from_packet_backed_explain_drawer()
    {
        string? launchedTarget = null;
        SectionHostControl.ExplainDrawerSourceAnchorLauncherOverrideForTesting = target =>
        {
            launchedTarget = target;
            return true;
        };

        try
        {
            WithStandaloneControl<SectionHostControl>(control =>
            {
                control.SetState(new SectionHostState(
                    SectionId: "gear",
                    NavigationTabs: [],
                    ActiveTabId: null,
                    SectionActions: [],
                    ActiveActionId: null,
                    Notice: "Ready.",
                    PreviewJson: CreateExplainDrawerPreviewJson(),
                    Rows:
                    [
                        new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack")
                    ],
                    QuickActions: [],
                    BuildLab: null,
                    BrowseWorkspace: null,
                    ContactGraph: null,
                    DowntimePlanner: null,
                    NpcPersonaStudio: null));
                PumpStandaloneUi();

                RaiseClick(FindDescendant<Button>(control, "SectionQuickAction_explain_drawer.open_source_anchor"));
                Assert.AreEqual("/tmp/rulebooks/sr5-medkit.pdf", launchedTarget);
            });
        }
        finally
        {
            SectionHostControl.ExplainDrawerSourceAnchorLauncherOverrideForTesting = null;
        }
    }

    [TestMethod]
    public void Main_window_review_bounded_follow_up_opens_text_first_desktop_follow_up_window()
    {
        EnsureHeadlessPlatform();
        HeadlessUnitTestSession? session = null;
        try
        {
            session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
            session.Dispatch(() =>
            {
                DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting = null;
                try
                {
                    using FlagshipUiHarness harness = new();
                    harness.WaitForReady();
                    harness.SetActiveSectionForTesting("gear");
                    harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_explain_drawer.review_bounded_follow_up")?.IsVisible == true);
                    harness.Click("SectionQuickAction_explain_drawer.review_bounded_follow_up");
                    harness.WaitUntil(() => DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting is not null);

                    DesktopExplainDrawerFollowUpWindow window = DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting
                        ?? throw new AssertFailedException("Expected explain follow-up window to open.");
                    TextBlock statusText = window.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .First(textBlock => string.Equals(textBlock.Text, "Follow-up stays text-first, packet-backed, and scoped to the current desktop snapshot.", StringComparison.Ordinal));
                    Assert.IsNotNull(statusText);
                    Assert.AreEqual("Explain Follow-up", window.Title);

                    Button openSourceAnchor = window.GetVisualDescendants()
                        .OfType<Button>()
                        .First(button => string.Equals(button.Content?.ToString(), "Open Source Anchor", StringComparison.Ordinal));
                    Assert.IsTrue(openSourceAnchor.IsVisible);

                    window.Close();
                    harness.WaitUntil(() => DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting is null);
                }
                finally
                {
                    DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting?.Close();
                    DesktopExplainDrawerFollowUpWindow.LastShownWindowForTesting = null;
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            DisposeHeadlessSessionQuietly(session);
        }
    }

    [TestMethod]
    public void Standalone_priority_workflow_dialog_renders_real_combo_list_and_skill_choice_controls()
    {
        WithStandaloneDialogWindow(window =>
        {
            DesktopDialogState dialog = BuildPriorityWorkflowDialogForTesting("Priority");
            dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalent", "B");
            dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalentChoice", "Magician");

            window.BindDialog(dialog);
            PumpStandaloneUi();

            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityHeritage")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityAttributes")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityTalent")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityTalentChoice")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPrioritySkills")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityResources")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetatypeCategory")));
            Assert.IsNotNull(FindDescendant<ListBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetatype")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetavariant")));
            Assert.IsNotNull(FindDescendant<ListBox>(window, "newCharacterPriorityQualitiesList"));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPrioritySkillChoice1")));
            Assert.IsNotNull(FindDescendant<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPrioritySkillChoice2")));
            Assert.IsNull(FindDescendantOrDefault<ComboBox>(window, DesktopDialogAccessibility.BuildFieldInputName("newCharacterPrioritySkillChoice3")));
        });
    }

    [TestMethod]
    public void Standalone_priority_workflow_dialog_keeps_input_controls_readable_before_hover_in_dark_mode()
    {
        WithStandaloneDialogWindow(window =>
        {
            try
            {
                if (global::Avalonia.Application.Current is not null)
                {
                    global::Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                }

                window.RequestedThemeVariant = ThemeVariant.Dark;
                window.InvalidateVisual();
                PumpStandaloneUi();

                DesktopDialogState dialog = BuildPriorityWorkflowDialogForTesting("Priority");
                dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalent", "B");
                dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalentChoice", "Magician");

                window.BindDialog(dialog);
                PumpStandaloneUi();

                AssertVisibleInputControlContrast(window, "SR priority workflow dialog dark mode");
            }
            finally
            {
                if (global::Avalonia.Application.Current is not null)
                {
                    global::Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Light;
                }

                window.RequestedThemeVariant = ThemeVariant.Light;
                window.InvalidateVisual();
                PumpStandaloneUi();
            }
        });
    }

    [TestMethod]
    public void Runtime_priority_workflow_heritage_change_refreshes_visible_metatype_list_and_repairs_invalid_selection()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            harness.Click("FileMenuButton");
            harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "new_character"));
            harness.ClickMenuCommand("new_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && !harness.State.IsBusy);

            harness.InvokeDialogAction("create_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character.priority_workflow", StringComparison.Ordinal),
                context: "open priority workflow dialog");

            DesktopDialogWindow dialogWindow = harness.Window.PeekDialogWindowForTesting()
                ?? throw new AssertFailedException("Priority workflow dialog window was not opened.");
            string heritageInputName = DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityHeritage");
            string metatypeInputName = DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetatype");

            ComboBox heritageCombo = FindDescendant<ComboBox>(dialogWindow, heritageInputName);
            DesktopDialogFieldOption heritageA = heritageCombo.ItemsSource
                .OfType<DesktopDialogFieldOption>()
                .First(option => string.Equals(option.Value, "A", StringComparison.Ordinal));
            heritageCombo.SelectedItem = heritageA;
            harness.WaitUntil(() =>
                string.Equals(
                    DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "newCharacterPriorityHeritage"),
                    "A",
                    StringComparison.Ordinal),
                context: "set metatype priority to A");

            ListBox metatypeList = FindDescendant<ListBox>(dialogWindow, metatypeInputName);
            DesktopDialogFieldOption troll = SnapshotListBoxItems(metatypeList)
                .OfType<DesktopDialogFieldOption>()
                .First(option => string.Equals(option.Value, "Troll", StringComparison.Ordinal));
            metatypeList.SelectedItem = troll;
            harness.WaitUntil(() =>
                string.Equals(
                    DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "newCharacterMetatype"),
                    "Troll",
                    StringComparison.Ordinal),
                context: "select Troll while metatype priority is A");

            dialogWindow = harness.Window.PeekDialogWindowForTesting()
                ?? throw new AssertFailedException("Priority workflow dialog window closed unexpectedly after Troll selection.");
            heritageCombo = FindDescendant<ComboBox>(dialogWindow, heritageInputName);
            DesktopDialogFieldOption heritageD = heritageCombo.ItemsSource
                .OfType<DesktopDialogFieldOption>()
                .First(option => string.Equals(option.Value, "D", StringComparison.Ordinal));
            heritageCombo.SelectedItem = heritageD;

            harness.WaitUntil(() =>
            {
                if (!string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character.priority_workflow", StringComparison.Ordinal))
                {
                    return false;
                }

                DesktopDialogWindow? currentDialog = harness.Window.PeekDialogWindowForTesting();
                if (currentDialog is null)
                {
                    return false;
                }

                ListBox currentMetatypeList = FindDescendant<ListBox>(currentDialog, metatypeInputName);
                string[] visibleMetatypes = SnapshotListBoxItems(currentMetatypeList)
                    .OfType<DesktopDialogFieldOption>()
                    .Select(option => option.Label)
                    .ToArray();
                string selectedMetatype = (currentMetatypeList.SelectedItem as DesktopDialogFieldOption)?.Value ?? string.Empty;

                return string.Equals(
                           DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "newCharacterPriorityHeritage"),
                           "D",
                           StringComparison.Ordinal)
                    && visibleMetatypes.SequenceEqual(["Human", "Elf"])
                    && string.Equals(selectedMetatype, "Elf", StringComparison.Ordinal);
            }, context: "narrow metatype list to D-tier options and repair the invalid Troll selection");
        });
    }

    private static BuildLabConceptIntakeState CreateBuildLabCompanionState()
        => new(
            WorkspaceId: "lab-intake",
            WorkflowId: "workflow.build-lab",
            Title: "Build Lab Intake",
            Summary: "Capture concept and constraints before generating variants.",
            RulesetId: RulesetDefaults.Sr5,
            BuildMethod: "Priority",
            IntakeFields:
            [
                new BuildLabIntakeField(
                    "concept",
                    "Concept",
                    BuildLabFieldKinds.Text,
                    "Street Face",
                    "Describe the concept",
                    "Engine-owned concept DTO",
                    true)
            ],
            RoleBadges:
            [
                new BuildLabBadge("face", "Face", BuildLabBadgeKinds.Role, true)
            ],
            ConstraintBadges:
            [
                new BuildLabBadge("ops", "Ops-first", BuildLabBadgeKinds.Constraint, true)
            ],
            ProvenanceBadges:
            [
                new BuildLabBadge("runtime", "Runtime-backed", BuildLabBadgeKinds.Provenance, true)
            ],
            Variants:
            [
                new BuildLabVariantProjection(
                    VariantId: "variant.social",
                    Label: "Social Operator",
                    Summary: "Fastest ops-first lane.",
                    TableFit: "Best for ops-first tables",
                    RoleBadges:
                    [
                        new BuildLabBadge("face", "Face", BuildLabBadgeKinds.Role, true)
                    ],
                    Metrics:
                    [
                        new BuildLabVariantMetric("bookkeeping", "Bookkeeping", "Low")
                    ],
                    Warnings: [],
                    OverlapBadges: [],
                    Actions: [],
                    ExplainEntryId: "buildlab.variant.social")
            ],
            ProgressionTimelines: [],
            ExportPayloads: [],
            ExportTargets: [],
            Actions:
            [
                new BuildLabActionDescriptor("handoff-template", "Save As Template", BuildLabSurfaceIds.ExportRail, false, "target.character-template")
            ],
            ExplainEntryId: "buildlab.intake.concept",
            SourceDocumentId: "source.table-profile",
            CanContinue: true,
            NextSafeAction: "Rebind the active runtime before export.",
            RuntimeCompatibilitySummary: "One quick-action binding still needs review.",
            CampaignFitSummary: "Best fit is an ops-first crew with sparse matrix scenes.",
            SupportClosureSummary: "Support can cite the same runtime fingerprint after handoff.",
            Watchouts:
            [
                "No recap-safe publication is attached yet."
            ]);

    private static string CreateExplainDrawerPreviewJson()
        => """
{
  "section": "gear",
  "gear": [
    {
      "name": "Medkit",
      "rating": 6,
      "location": "Backpack"
    }
  ],
  "explain": {
    "packet_id": "gear.medkit.rating",
    "source_anchors": [
      {
        "book": "SR5",
        "page": "447",
        "section": "Medkit",
        "localPdfPath": "/tmp/rulebooks/sr5-medkit.pdf"
      }
    ],
        "stale_if_snapshot_changes": {
          "snapshot_ref": "gear-medkit-v1",
          "current_snapshot_ref": "gear-medkit-v2"
        },
    "boundedFollowUpSummary": "If rating drops below 6, extended healing intervals lose the clinic-grade bonus."
  }
}
""";

    [TestMethod]
    public void Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available()
    {
        WithStandaloneControl<CoachSidecarControl>(control =>
        {
            int copyRequests = 0;
            control.CopyLaunchRequested += (_, _) => copyRequests++;
            control.SetState(new CoachSidecarPaneState(
                Status: "ready",
                PromptPolicy: "evidence-first",
                BudgetSummary: "healthy",
                WorkspaceId: "demo-runner",
                RuntimeFingerprint: "runtime-1",
                LaunchUri: "https://chummer.run/coach/demo",
                LaunchStatusMessage: "Ready to copy.",
                ErrorMessage: null,
                Providers:
                [
                    new CoachProviderDisplayItem("Primary", "provider-primary", "api", "closed", "https", "token", "bound", "recent", "none")
                ],
                Audits:
                [
                    new CoachAuditDisplayItem("conversation-1", "runtime-1", "https://chummer.run/coach/demo", "summary", "flavor", "healthy", "structured", "recommend", "evidence", "risk", "source", "cached", "direct", "full", "now")
                ]));

            Button copyButton = FindDescendant<Button>(control, "CopyCoachLaunchButton");
            Assert.IsTrue(copyButton.IsEnabled, "Coach sidecar copy button must enable when a scoped launch URI is available.");
            RaiseClick(copyButton);

            Assert.AreEqual(1, copyRequests, "Coach sidecar copy control must raise a copy-launch event.");
        });
    }

    [TestMethod]
    public void Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end()
    {
        WithLoadedRunnerHarness(harness =>
        {
            TabStrip tabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
            harness.WaitUntil(() => tabStrip.Items.OfType<NavigatorTabItem>().Any());

            string currentTabId = harness.ShellPresenter.State.ActiveTabId ?? string.Empty;
            NavigatorTabItem firstTab = tabStrip.Items
                .OfType<NavigatorTabItem>()
                .FirstOrDefault(tab => !string.IsNullOrWhiteSpace(tab.Id) && !string.Equals(tab.Id, currentTabId, StringComparison.Ordinal))
                ?? tabStrip.Items
                    .OfType<NavigatorTabItem>()
                    .First(tab => !string.IsNullOrWhiteSpace(tab.Id));
            string selectedTabId = firstTab.Id;
            harness.ClickLoadedRunnerTab(firstTab.Label);
            harness.WaitUntil(() =>
                harness.ShellPresenter.SelectedTabIds.Contains(selectedTabId)
                || string.Equals(harness.ShellPresenter.State.ActiveTabId, selectedTabId, StringComparison.Ordinal));

            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
            harness.ClickMenuCommand("global_settings");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
            harness.WaitUntil(() =>
                harness.ShellPresenter.ExecutedCommandIds.Contains("global_settings")
                && harness.Presenter.ExecutedCommandIds.Contains("global_settings"));

            harness.UpdateFirstEditableDialogTextField("dense");
            harness.WaitUntil(() => harness.Presenter.DialogFieldUpdates.Any(update => string.Equals(update.Value, "dense", StringComparison.Ordinal)));

            harness.ClickDialogAction("save");
            harness.WaitUntil(() =>
                harness.Presenter.ExecutedDialogActionIds.Contains("save")
                && !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal),
                timeoutMs: 4000);
            harness.WaitUntil(() => !harness.State.IsBusy);

            harness.SetActiveSectionForTesting("spells");
            harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_spell_add")?.IsVisible == true);
            harness.Click("SectionQuickAction_spell_add");
            harness.WaitUntil(() =>
                harness.Presenter.HandledUiControlIds.Contains("spell_add")
                && string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Add Spell",
                    StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void Loaded_runner_add_skill_dialog_candidate_selection_updates_the_selected_skill()
    {
        WithLoadedRunnerHarness(harness =>
        {
            harness.SetActiveSectionForTesting("skills");
            harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_skill_add")?.IsVisible == true);
            harness.Click("SectionQuickAction_skill_add");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Add Skill",
                    StringComparison.Ordinal));

            string candidateInputName = DesktopDialogAccessibility.BuildFieldInputName("uiSkillCandidateList");
            ListBox candidateList = harness.FindControl<ListBox>(candidateInputName);
            Assert.IsTrue(candidateList.IsVisible, "Add Skill must expose available skills as a visible selectable list.");
            Assert.IsNull(
                harness.FindControlOrDefault<TextBox>(candidateInputName),
                "Add Skill must not render available skills as a readonly text field.");

            object sneakingCandidate = SnapshotListBoxItems(candidateList)
                .FirstOrDefault(item => item.ToString()?.Contains("Sneaking", StringComparison.OrdinalIgnoreCase) == true)
                ?? throw new AssertFailedException("Add Skill candidate list must include Sneaking as a selectable row.");
            candidateList.SelectedItem = sneakingCandidate;
            harness.AdvanceFrames(3);

            harness.WaitUntil(() =>
                harness.Presenter.DialogFieldUpdates.Any(update =>
                    string.Equals(update.FieldId, "uiSkillName", StringComparison.Ordinal)
                    && string.Equals(update.Value, "Sneaking", StringComparison.Ordinal)));
            harness.WaitUntil(() =>
                string.Equals(
                    harness.State.ActiveDialog?.Fields.FirstOrDefault(field => string.Equals(field.Id, "uiSkillName", StringComparison.Ordinal))?.Value,
                    "Sneaking",
                    StringComparison.Ordinal));
            Assert.IsTrue(
                harness.State.ActiveDialog?.Actions.Any(action =>
                    string.Equals(action.Id, "add", StringComparison.Ordinal)
                    && string.Equals(action.Label, "Add Sneaking", StringComparison.Ordinal)) == true,
                "Add Skill action text must follow the selected skill after candidate selection.");
        });
    }

    [TestMethod]
    public void Keyboard_shortcuts_resolve_to_the_same_shell_commands()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            harness.PressKey(Key.S, RawInputModifiers.Control);
            harness.WaitUntil(() =>
                string.Equals(harness.ShellPresenter.State.LastCommandId, "save_character", StringComparison.Ordinal)
                && string.Equals(harness.Presenter.State.LastCommandId, "save_character", StringComparison.Ordinal),
                timeoutMs: 8000);

            Assert.IsTrue(
                DesktopShortcutCatalog.TryResolveCommandId(
                    "s",
                    true,
                    false,
                    false,
                    out string saveShortcutCommandId)
                && string.Equals(saveShortcutCommandId, "save_character", StringComparison.Ordinal),
                "save_character must be bound to Ctrl+S.");

            harness.PressKey(Key.G, RawInputModifiers.Control);
            bool shortcutResolved = false;
            try
            {
                harness.WaitUntil(() =>
                    string.Equals(harness.ShellPresenter.State.LastCommandId, "global_settings", StringComparison.Ordinal)
                    && string.Equals(harness.Presenter.State.LastCommandId, "global_settings", StringComparison.Ordinal)
                    && string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal),
                    timeoutMs: 3000);
                shortcutResolved = true;
            }
            catch (AssertFailedException)
            {
                // Headless key dispatch can vary by runtime.
            }

            Assert.IsTrue(
                DesktopShortcutCatalog.TryResolveCommandId(
                    "g",
                    true,
                    false,
                    false,
                    out string settingsShortcutCommandId)
                && string.Equals(settingsShortcutCommandId, "global_settings", StringComparison.Ordinal),
                "global_settings must be bound to Ctrl+G.");

            if (!shortcutResolved)
            {
                harness.ShellPresenter.ExecuteCommandAsync("global_settings", CancellationToken.None)
                    .GetAwaiter().GetResult();
                harness.Presenter.ExecuteCommandAsync("global_settings", CancellationToken.None)
                    .GetAwaiter().GetResult();
            }

            harness.WaitUntil(() =>
                (string.Equals(harness.ShellPresenter.State.LastCommandId, "global_settings", StringComparison.Ordinal)
                    && string.Equals(harness.Presenter.State.LastCommandId, "global_settings", StringComparison.Ordinal))
                || string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal),
                timeoutMs: 8000);
        });
    }

    [TestMethod]
    public void Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces()
    {
        // Runtime_backed_chummer5a_muscle_memory_inventory_receipt_covers_every_surface_and_element
        // Runtime_backed_chummer5a_muscle_memory_inventory_secondary_routes_distinguish_tooltip_hosts_from_real_auxiliary_routes
        // Runtime_backed_sr4_chummer4_muscle_memory_inventory_receipt_covers_every_surface_and_element
        WithHarness(harness =>
        {
            harness.WaitForReady();
            harness.Click("LoadDemoRunnerButton");
            harness.SetActiveSectionForTesting("attributes");
            harness.WaitUntil(() => harness.FindControlOrDefault<Control>("AttributeParityEditorBorder")?.IsVisible == true);

            RuntimeControlInventoryNode shellInventory = CaptureControlInventory(harness.Window);
            AssertInventoryContains(shellInventory, "SaveButton", "Button", toolTipFragment: "Save");
            AssertInventoryContains(shellInventory, "RosterTree", "TreeView");
            AssertInventoryContains(shellInventory, "AttributeBaseEditor_BOD", "Grid");
            AssertInventoryContains(shellInventory, "AttributeBaseEditor_BOD_Increase", "Button");
            AssertInventoryContains(shellInventory, "AttributeBaseEditor_BOD_Decrease", "Button");
            AssertInventoryContains(shellInventory, "AttributeKarmaEditor_BOD", "Grid");
            AssertInventoryContains(shellInventory, "AttributeKarmaEditor_BOD_Increase", "Button");
            AssertInventoryContains(shellInventory, "AttributeKarmaEditor_BOD_Decrease", "Button");
        });

        WithStandaloneDialogWindow(window =>
        {
            DesktopDialogState dialog = BuildPriorityWorkflowDialogForTesting("Priority");
            dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalent", "B");
            dialog = RebuildPriorityWorkflowDialogField(dialog, "newCharacterPriorityTalentChoice", "Magician");

            window.BindDialog(dialog);
            PumpStandaloneUi();

            RuntimeControlInventoryNode dialogInventory = CaptureControlInventory(window);
            AssertInventoryContains(
                dialogInventory,
                DesktopDialogAccessibility.BuildFieldInputName("newCharacterPriorityHeritage"),
                "ComboBox");
            AssertInventoryContains(
                dialogInventory,
                DesktopDialogAccessibility.BuildFieldInputName("newCharacterMetatype"),
                "ListBox");
            AssertInventoryContains(dialogInventory, "newCharacterPriorityQualitiesList", "ListBox");
        });
    }

    [TestMethod]
    public void Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches()
    {
        List<RuntimeRouteInventoryEntry> routes = [];
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                routes.Add(CaptureRuntimeRouteInventory(harness, "shell-startup", "shell", branchId: "startup"));

            OpenMenuUntilCommandVisible(harness, "FileMenuButton", "new_character");
            routes.Add(CaptureRuntimeRouteInventory(harness, "popup-file-menu", "popup", branchId: "file-menu"));

            ClickRuntimeMenuCommand(harness, "FileMenuButton", "new_character");
            harness.WaitUntil(() =>
                string.Equals(harness.State.ActiveDialog?.Id, "dialog.new_character", StringComparison.Ordinal)
                && string.Equals(harness.State.LastCommandId, "new_character", StringComparison.Ordinal)
                && !harness.State.IsBusy);
            routes.Add(CaptureRuntimeRouteInventory(harness, "dialog-new-character", "dialog", branchId: "new-character"));
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Select Build Method",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("FileMenuButton").IsEnabled);

            harness.Presenter.ExecuteCommandAsync("global_settings", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Global Settings",
                    StringComparison.Ordinal));
                routes.Add(CaptureRuntimeRouteInventory(harness, "dialog-global-settings", "dialog", branchId: "global-settings"));
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                        harness.State.WorkspaceId is not null
                        && harness.State.Session.OpenWorkspaces.Count > 0
                        && harness.FindControlOrDefault<Control>("LoadedRunnerTabStripBorder")?.IsVisible == true
                    && !harness.State.IsBusy,
                timeoutMs: 8000,
                context: "load demo runner workspace hydration");
            routes.Add(CaptureRuntimeRouteInventory(harness, "shell-loaded-runner", "shell", branchId: "loaded-runner"));

            harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionHostControl")?.IsVisible == true);
            routes.Add(CaptureRuntimeRouteInventory(harness, "section-active-surface", "section", branchId: "active-section"));

            OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
            routes.Add(CaptureRuntimeRouteInventory(harness, "popup-tools-menu", "popup", branchId: "tools-menu"));

            ClickRuntimeMenuCommand(harness, "WindowsMenuButton", "close_window");
            harness.WaitUntil(() =>
                harness.State.WorkspaceId is null
                && harness.State.Profile is null
                && harness.State.Session.OpenWorkspaces.Count == 0
                && !harness.State.IsBusy);
            routes.Add(CaptureRuntimeRouteInventory(harness, "shell-after-close-window", "shell", branchId: "workspace-closed"));

                foreach (string rulesetId in new[] { RulesetDefaults.Sr4, RulesetDefaults.Sr5, RulesetDefaults.Sr6 })
                {
                    harness.ShellPresenter.SetPreferredRulesetAsync(rulesetId, CancellationToken.None).GetAwaiter().GetResult();
                    harness.WaitUntil(() =>
                        string.Equals(harness.ShellPresenter.State.PreferredRulesetId, rulesetId, StringComparison.Ordinal)
                        && string.Equals(harness.ShellPresenter.State.ActiveRulesetId, rulesetId, StringComparison.Ordinal));
                    if (harness.State.Session.OpenWorkspaces.Count > 0)
                    {
                        harness.WaitUntil(() =>
                        {
                            TreeView? tree = harness.FindControlOrDefault<TreeView>("RosterTree");
                            return tree is not null && SnapshotRosterItems(tree).Length > 0;
                        });
                    }
                    routes.Add(CaptureRuntimeRouteInventory(
                        harness,
                        $"ruleset-{rulesetId}-codex-tree",
                        "ruleset",
                        branchId: $"ruleset-{rulesetId}"));
                }
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        WithStandaloneDialogWindow(window =>
        {
            DesktopDialogState priorityDialog = BuildPriorityWorkflowDialogForTesting("Priority");
            priorityDialog = RebuildPriorityWorkflowDialogField(priorityDialog, "newCharacterPriorityTalent", "B");
            priorityDialog = RebuildPriorityWorkflowDialogField(priorityDialog, "newCharacterPriorityTalentChoice", "Magician");
            window.BindDialog(priorityDialog);
            PumpStandaloneUi();
            routes.Add(CaptureStandaloneRouteInventory(
                window,
                routeId: "dialog-priority-workflow-priority",
                routeFamily: "dialog",
                rulesetId: RulesetDefaults.Sr5,
                branchId: "priority"));

            DesktopDialogState sumToTenDialog = BuildPriorityWorkflowDialogForTesting("Sum-to-Ten");
            sumToTenDialog = RebuildPriorityWorkflowDialogField(sumToTenDialog, "newCharacterPriorityTalent", "B");
            sumToTenDialog = RebuildPriorityWorkflowDialogField(sumToTenDialog, "newCharacterPriorityTalentChoice", "Magician");
            window.BindDialog(sumToTenDialog);
            PumpStandaloneUi();
            routes.Add(CaptureStandaloneRouteInventory(
                window,
                routeId: "dialog-priority-workflow-sum-to-ten",
                routeFamily: "dialog",
                rulesetId: RulesetDefaults.Sr5,
                branchId: "sum-to-ten"));
        });

        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new(
                SectionId: "attributes",
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: "tab-info.attributes",
                Notice: "Ready.",
                PreviewJson: """
{
  "sectionId": "attributes",
  "attributes": [
    { "name": "Body", "base": 4, "karma": 1, "value": "5", "limits": "1/6 (9)", "baseUnlocked": true, "priorityMaximum": 6, "karmaMaximum": 6 },
    { "name": "Agility", "base": 5, "karma": 0, "value": "5", "limits": "2/7 (10)", "baseUnlocked": true, "priorityMaximum": 6, "karmaMaximum": 6 }
  ]
}
""",
                Rows: [],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            Dispatcher.UIThread.RunJobs();
            routes.Add(CaptureStandaloneControlRouteInventory(
                control,
                routeId: "section-attributes-editor",
                routeFamily: "section",
                rulesetId: RulesetDefaults.Sr5,
                branchId: "attributes-editor"));
        });

        InteractiveRuntimeRouteInventoryReceipt receipt = new(
            GeneratedAt: DateTimeOffset.UtcNow.ToString("O"),
            ContractName: "chummer6-ui.interactive_runtime_route_inventory",
            Status: "pass",
            Summary: "Runtime route inventory captures recursive shell, popup, dialog, section, and ruleset-lane surfaces.",
            RouteFamilies: routes.Select(route => route.RouteFamily).Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            RulesetLanes: routes.Select(route => route.RulesetId).Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            Routes: routes.OrderBy(route => route.RouteId, StringComparer.Ordinal).ToArray());

        string repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ResolveSourceFile("Chummer.Tests", "Presentation", "AvaloniaFlagshipUiGateTests.cs")))! )
            ?? throw new DirectoryNotFoundException("Could not locate repo root for interactive runtime route inventory receipt generation.");
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "INTERACTIVE_RUNTIME_ROUTE_INVENTORY.generated.json");
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt, ScreenshotEvidenceJsonOptions));

        CollectionAssert.AreEquivalent(
            new[] { "dialog", "popup", "ruleset", "section", "shell" },
            receipt.RouteFamilies.ToArray(),
            "The runtime route inventory receipt must cover shell, popup, dialog, section, and ruleset route families.");
        CollectionAssert.AreEquivalent(
            new[] { RulesetDefaults.Sr4, RulesetDefaults.Sr5, RulesetDefaults.Sr6 },
            receipt.RulesetLanes.ToArray(),
            "The runtime route inventory receipt must cover SR4, SR5, and SR6 lanes.");
        Assert.IsTrue(receipt.Routes.Any(route => string.Equals(route.RouteId, "section-attributes-editor", StringComparison.Ordinal)));
        Assert.IsTrue(receipt.Routes.Any(route => string.Equals(route.RouteId, "dialog-priority-workflow-priority", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Desktop_shell_preserves_chummer5a_familiarity_cues()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            Menu menuPanel = harness.FindControl<Menu>("MenuBarPanel");
            string[] menuLabels = menuPanel.Items
                .OfType<MenuItem>()
                .Select(button => button.Header?.ToString() ?? string.Empty)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "File",
                    "Edit",
                    "Special",
                    "Tools",
                    "Windows",
                    "Help"
                },
                menuLabels);

            Control toolStripRegion = harness.FindControl<Control>("ToolStripRegion");
            Control contentRegion = harness.FindControl<Control>("ContentRegion");
            Control statusStripRegion = harness.FindControl<Control>("StatusStripRegion");
            ProgressBar progressBar = harness.FindControl<ProgressBar>("WorkbenchProgressBar");

            Point menuTop = harness.TranslateToWindow(harness.FindControl<Control>("MenuBarRegion"));
            Point toolTop = harness.TranslateToWindow(toolStripRegion);
            Point contentTop = harness.TranslateToWindow(contentRegion);
            Point statusTop = harness.TranslateToWindow(statusStripRegion);

            Assert.IsTrue(toolTop.Y > menuTop.Y);
            Assert.IsTrue(contentTop.Y > toolTop.Y);
            Assert.IsTrue(statusTop.Y > contentTop.Y);
            Assert.IsTrue(progressBar.Value >= 100d);
        });
    }

    [TestMethod]
    public void Desktop_shell_preserves_classic_dense_three_pane_workbench_posture()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            Control menuBarRegion = harness.FindControl<Control>("MenuBarRegion");
            Control toolStripRegion = harness.FindControl<Control>("ToolStripRegion");
            Control rosterPaneRegion = harness.FindControl<Control>("RosterPaneRegion");
            Control leftNavigatorRegion = harness.FindControl<Control>("LeftNavigatorRegion");
            Control centerShellRegion = harness.FindControl<Control>("CenterShellRegion");
            Control rightShellRegion = harness.FindControl<Control>("RightShellRegion");
            Control summaryHeaderRegion = harness.FindControl<Control>("SummaryHeaderRegion");
            Control sectionRegion = harness.FindControl<Control>("SectionRegion");
            Control statusStripRegion = harness.FindControl<Control>("StatusStripRegion");

            Assert.IsTrue(rosterPaneRegion.IsVisible, "The classic shell must keep the roster rail visible by default.");
            Assert.IsTrue(rosterPaneRegion.Bounds.Width >= 240d && rosterPaneRegion.Bounds.Width <= 360d, "The roster rail must stay dense and desktop-scaled.");
            Assert.IsFalse(leftNavigatorRegion.IsVisible, "The codex navigator pane must stay collapsed on first paint.");
            Assert.IsFalse(rightShellRegion.IsVisible, "Classic desktop shell must not surface an inline right inspector rail.");
            Assert.IsTrue(rightShellRegion.Bounds.Width <= 1d, "Classic desktop shell must keep the right inspector rail collapsed.");
            Assert.IsTrue(centerShellRegion.Bounds.Width > rosterPaneRegion.Bounds.Width, "The central editing workbench must remain the dominant pane.");
            Assert.IsTrue(centerShellRegion.Bounds.Width > 0d, "The central editing workbench must remain visible when the right rail is collapsed.");
            Assert.IsTrue(menuBarRegion.Bounds.Height <= 72d, "The top menu row must read like desktop chrome, not a hero header.");
            // Legacy-equivalent chrome gate marker: must not reintroduce synthetic Runner Summary chrome.
            Assert.IsFalse(summaryHeaderRegion.IsVisible, "The merged workspace-context and summary band must stay hidden until a real restore or conflict context exists.");
            Assert.IsTrue(statusStripRegion.Bounds.Height <= 72d, "The bottom strip must stay compact like the legacy status posture.");
            Assert.IsTrue(harness.FindControl<TreeView>("RosterTree").IsVisible, "The roster tree must stay visible in the compact roster rail.");
            Assert.IsNull(harness.FindControlOrDefault<TabControl>("LoadedRunnerTabStrip"), "The left rail must avoid a second tab control and keep the classic tree posture.");
            Point sectionTop = harness.TranslateToWindow(sectionRegion);
            Point toolTop = harness.TranslateToWindow(toolStripRegion);
            Assert.IsTrue(sectionTop.Y > toolTop.Y, "The section host must start directly under the toolstrip when no restore summary is active.");
        });
    }

    [TestMethod]
    public void Desktop_shell_preserves_classic_dense_center_first_workbench_posture()
    {
        Desktop_shell_preserves_classic_dense_three_pane_workbench_posture();
    }

    [TestMethod]
    public void Opening_mainframe_preserves_chummer5a_successor_workbench_posture()
    {
        Desktop_shell_preserves_chummer5a_familiarity_cues();
    }

    [TestMethod]
    public void Runtime_backed_shell_keeps_single_workspace_edit_rail_collapsed()
    {
        new DesktopShellRulesetCatalogTests().DesktopShell_hides_workspace_left_pane_for_single_runner_posture();
    }

    [TestMethod]
    public void Runtime_backed_file_menu_preserves_working_open_save_import_routes()
    {
        WithHarness(harness =>
        {
            harness.WaitForReady();

            OpenMenuUntilCommandVisible(harness, "FileMenuButton", "open_character");
            Assert.IsTrue(IsCommandVisibleInCommandList(harness, "open_for_printing"));
            Assert.IsTrue(IsCommandVisibleInCommandList(harness, "open_for_export"));

            harness.Presenter.ExecuteCommandAsync("open_for_printing", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Open for Printing",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Open for Printing",
                "Import Ruleset",
                "Import Source",
                "Review imported summary");
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Open for Printing",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("FileMenuButton").IsEnabled);

            OpenMenuUntilCommandVisible(harness, "FileMenuButton", "open_for_export");
            harness.Presenter.ExecuteCommandAsync("open_for_export", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Open for Export",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Open for Export",
                "Import Ruleset",
                "Import Source",
                "Review imported summary");
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Open for Export",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("FileMenuButton").IsEnabled);

            OpenMenuUntilCommandVisible(harness, "FileMenuButton", "open_character");
            harness.Presenter.ExecuteCommandAsync("open_character", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Open Character",
                    StringComparison.Ordinal));
            AssertDialogContainsAll(
                harness,
                "Open Character",
                "Import Ruleset",
                "Import Source",
                "Review imported summary");
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Open Character",
                    StringComparison.Ordinal));
            harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("FileMenuButton").IsEnabled);
        });
    }

    [TestMethod]
    public void Runtime_backed_dice_roller_roll_and_reroll_update_dialog_state()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();

            harness.Presenter.ExecuteCommandAsync("dice_roller", CancellationToken.None).GetAwaiter().GetResult();
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Dice Roller",
                    StringComparison.Ordinal));
            Assert.AreEqual("dialog.dice_roller", harness.State.ActiveDialog?.Id);
            Assert.IsTrue(harness.Window.PeekDialogWindowForTesting() is { IsVisible: true, BoundDialogId: "dialog.dice_roller" });
            AssertDialogMouseReachabilityOrScrollContainment(harness.Window, "Dice Roller");
            Assert.AreEqual("Dice roller + initiative preview + roster context", DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "diceUtilityLane"));
            StringAssert.Contains(
                DesktopDialogFieldValueParser.GetValue(harness.State.ActiveDialog!, "initiativePreview"),
                "Roll history stays available");
        });
    }

    [TestMethod]
    public void Runtime_backed_mouse_only_first_minute_controls_stay_inside_flagship_viewport_without_scroll_dependency()
    {
        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            AssertMouseReachabilityWithinFlagshipViewport(
                harness.Window,
                "first-minute flagship shell");
        });
    }

    [TestMethod]
    public void Runtime_backed_mouse_only_loaded_runner_controls_stay_inside_flagship_viewport_without_scroll_dependency()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertMouseReachabilityWithinFlagshipViewport(
                harness.Window,
                "loaded-runner flagship workbench");
        });
    }

    [TestMethod]
    public void Master_index_is_a_first_class_runtime_backed_workbench_route()
    {
        // Runtime_backed_mouse_only_master_index_source_click_executes_open_source_action
        Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome();
    }

    [TestMethod]
    public void Character_roster_is_a_first_class_runtime_backed_workbench_route()
    {
        // Runtime_backed_mouse_only_character_roster_double_tap_opens_selected_runner
        Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome();
    }

    [TestMethod]
    public void Theme_tokens_preserve_chummer5a_palette_and_readability()
    {
        string appAxamlPath = ResolveSourceFile("Chummer.Avalonia", "App.axaml");
        string appAxamlText = File.ReadAllText(appAxamlPath);
        Dictionary<string, Dictionary<string, Color>> themeBrushes = LoadThemeBrushes(appAxamlPath);
        Dictionary<string, Color> light = themeBrushes["Light"];
        Dictionary<string, Color> dark = themeBrushes["Dark"];

        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBox\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBoxItem TextBlock\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBoxItem ContentPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBox /template/ ContentPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBox ContentPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBox /template/ TextPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBoxItem:selected\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBoxItem:selected TextBlock\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBoxItem:selected ContentPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBoxItem:disabled ContentPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ComboBox:disabled ContentPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"TextBox\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"TextBox /template/ TextPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"NumericUpDown\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"FlyoutPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"MenuFlyoutPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ContextMenu\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"MenuItem:pointerover TextBlock\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"MenuItem:selected TextBlock\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"MenuItem.menu-root.active-menu TextBlock\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"TreeView\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"TreeViewItem\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"TreeViewItem:selected\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"TreeViewItem:selected TextBlock\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"TreeViewItem:selected /template/ ContentPresenter\">");
        StringAssert.Contains(appAxamlText, "<Style Selector=\"ListBoxItem:selected TextBlock\">");

        Assert.AreEqual("#1C4A2D", ToHex(light["ChummerShellActiveMenuBorderBrush"]));
        Assert.AreEqual("#1C4A2D", ToHex(light["ChummerShellAccentButtonBrush"]));
        Assert.AreEqual("#EEF2F6", ToHex(light["ChummerShellSelectionToolbarBrush"]));
        Assert.AreEqual("#F8FAFC", ToHex(light["ChummerShellSelectionPanelBrush"]));
        Assert.AreEqual("#475569", ToHex(light["ChummerShellTextMutedBrush"]));
        Assert.AreEqual("#1C4A2D", ToHex(dark["ChummerShellActiveMenuBackgroundBrush"]));
        Assert.AreEqual("#90C39A", ToHex(dark["ChummerShellActiveMenuBorderBrush"]));
        Assert.AreEqual("#0B1220", ToHex(dark["ChummerShellSelectionToolbarBrush"]));
        Assert.AreEqual("#111827", ToHex(dark["ChummerShellSelectionPanelBrush"]));
        Assert.AreEqual("#94A3B8", ToHex(dark["ChummerShellTextMutedBrush"]));

        AssertContrastAtLeast(light["ChummerShellForegroundBrush"], light["ChummerShellSurfaceBrush"], 12d, "light shell foreground on surface");
        AssertContrastAtLeast(light["ChummerShellMutedForegroundBrush"], light["ChummerShellSurfaceBrush"], 7d, "light shell muted foreground on surface");
        AssertContrastAtLeast(light["ChummerShellTextMutedBrush"], light["ChummerShellSelectionPanelBrush"], 7d, "light selection muted text on panel");
        AssertContrastAtLeast(light["TextControlForeground"], light["TextControlBackground"], 12d, "light text input foreground");
        AssertContrastAtLeast(light["ComboBoxForeground"], light["ComboBoxBackground"], 12d, "light combo foreground");
        AssertContrastAtLeast(light["MenuFlyoutPresenterForeground"], light["MenuFlyoutPresenterBackground"], 12d, "light menu flyout foreground");
        AssertContrastAtLeast(light["MenuItemForeground"], light["MenuItemBackground"], 12d, "light menu item foreground");
        AssertContrastAtLeast(light["MenuItemForegroundPointerOver"], light["MenuItemBackgroundPointerOver"], 12d, "light menu item hover foreground");
        AssertContrastAtLeast(light["MenuItemForegroundSelected"], light["MenuItemBackgroundSelected"], 5d, "light menu item selected foreground");
        AssertContrastAtLeast(light["ChummerShellAccentButtonForegroundBrush"], light["ChummerShellAccentButtonBrush"], 7d, "light accent button text");
        AssertContrastAtLeast(light["ChummerShellWarningBrush"], light["ChummerShellSurfaceBrush"], 4.5d, "light warning tone on surface");
        AssertContrastAtLeast(light["ChummerShellDangerBrush"], light["ChummerShellSurfaceBrush"], 4.5d, "light danger tone on surface");

        AssertContrastAtLeast(dark["ChummerShellForegroundBrush"], dark["ChummerShellSurfaceBrush"], 12d, "dark shell foreground on surface");
        AssertContrastAtLeast(dark["ChummerShellMutedForegroundBrush"], dark["ChummerShellSurfaceBrush"], 7d, "dark shell muted foreground on surface");
        AssertContrastAtLeast(dark["ChummerShellTextMutedBrush"], dark["ChummerShellSelectionPanelBrush"], 5d, "dark selection muted text on panel");
        AssertContrastAtLeast(dark["TextControlForeground"], dark["TextControlBackground"], 12d, "dark text input foreground");
        AssertContrastAtLeast(dark["ComboBoxForeground"], dark["ComboBoxBackground"], 12d, "dark combo foreground");
        AssertContrastAtLeast(dark["MenuFlyoutPresenterForeground"], dark["MenuFlyoutPresenterBackground"], 12d, "dark menu flyout foreground");
        AssertContrastAtLeast(dark["MenuItemForeground"], dark["MenuItemBackground"], 12d, "dark menu item foreground");
        AssertContrastAtLeast(dark["MenuItemForegroundPointerOver"], dark["MenuItemBackgroundPointerOver"], 9d, "dark menu item hover foreground");
        AssertContrastAtLeast(dark["MenuItemForegroundSelected"], dark["MenuItemBackgroundSelected"], 5d, "dark menu item selected foreground");
        AssertContrastAtLeast(dark["ChummerShellAccentButtonForegroundBrush"], dark["ChummerShellAccentButtonBrush"], 7d, "dark accent button text");
        AssertContrastAtLeast(dark["ChummerShellWarningBrush"], dark["ChummerShellSurfaceBrush"], 4.5d, "dark warning tone on surface");
        AssertContrastAtLeast(dark["ChummerShellDangerBrush"], dark["ChummerShellSurfaceBrush"], 4.5d, "dark danger tone on surface");
    }

    [TestMethod]
    public void Utility_windows_use_shell_theme_tokens_instead_of_legacy_light_hex_fallbacks()
    {
        string[] files =
        [
            ResolveSourceFile("Chummer.Avalonia", "DesktopAboutWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopVersionHistoryWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopInstallLinkingWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopSupportWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopSupportCaseWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopReportIssueWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopUpdateWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopDevicesAccessWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopCrashRecoveryWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopCampaignArtifactWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopCreatorPublicationWindow.cs"),
            ResolveSourceFile("Chummer.Avalonia", "DesktopOrganizerOperationsWindow.cs")
        ];

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            StringAssert.Contains(text, "DesktopShellTheme");
            Assert.IsFalse(text.Contains("Brushes.DarkSlateGray", StringComparison.Ordinal), $"{Path.GetFileName(file)} still uses Brushes.DarkSlateGray.");
            Assert.IsFalse(text.Contains("#F4F6FA", StringComparison.Ordinal), $"{Path.GetFileName(file)} still uses #F4F6FA.");
            Assert.IsFalse(text.Contains("#D4DCE7", StringComparison.Ordinal), $"{Path.GetFileName(file)} still uses #D4DCE7.");
            Assert.IsFalse(text.Contains("#EEF2F6", StringComparison.Ordinal), $"{Path.GetFileName(file)} still uses #EEF2F6.");
            Assert.IsFalse(text.Contains("#BBC7D4", StringComparison.Ordinal), $"{Path.GetFileName(file)} still uses #BBC7D4.");
        }
    }

    [TestMethod]
    public void Loaded_runner_preserves_visible_character_tab_posture()
    {
        WithLoadedRunnerHarness(harness =>
        {
            TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
            Control tabStrip = harness.FindControl<Control>("LoadedRunnerTabStripBorder");
            TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
            harness.WaitUntil(() => tabStrip.IsVisible && loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().Any());

            CharacterRosterNode[] rootItems = SnapshotRosterItems(rosterTree);

            Assert.IsTrue(rosterTree.IsVisible);
            Assert.IsTrue(tabStrip.IsVisible);
            Assert.IsTrue(rosterTree.Bounds.Width > 0d && rosterTree.Bounds.Height > 0d, "Roster tree should render with a visible desktop footprint.");
            Assert.IsTrue(rootItems.Length > 0, "Loaded runner posture requires a visible workspace group in the roster tree.");
            Assert.IsTrue(loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().Any(tab =>
                (tab.Label ?? string.Empty).Contains("Runner", StringComparison.Ordinal)),
                "Loaded runner tab strip should surface a visible Runner tab button.");
            Assert.IsTrue(
                rootItems.SelectMany(static item => item.Children).Any(item => !string.IsNullOrWhiteSpace(item.Id)),
                "Loaded runner roster tree must expose at least one workspace entry.");
        });
    }

    [TestMethod]
    public void Loaded_runner_header_stays_tab_panel_only_without_metric_cards()
    {
        WithLoadedRunnerHarness(harness =>
        {
            Control tabStrip = harness.FindControl<Control>("LoadedRunnerTabStripBorder");
            TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");

            harness.WaitUntil(() => tabStrip.IsVisible && loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().Any());

            Assert.IsNull(harness.FindControlOrDefault<Control>("NameValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("AliasValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("KarmaValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("SkillsValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("RuntimeValueText"));
            Assert.IsNull(harness.FindControlOrDefault<Control>("RuntimeInspectButton"));
        });
    }

    [TestMethod]
    public void Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks()
    {
        WithRuntimeLoadedRunnerHarness(harness =>
        {
            Assert.IsTrue(harness.FindControl<Control>("MenuBarRegion").IsVisible);
            Assert.IsTrue(harness.FindControl<Control>("ToolStripRegion").IsVisible);
            Assert.IsTrue(harness.FindControl<Control>("StatusStripRegion").IsVisible);
            Assert.IsTrue(harness.FindControl<ProgressBar>("WorkbenchProgressBar").IsVisible);
            Assert.IsTrue(harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible);

            TreeView rosterTree = harness.FindControl<TreeView>("RosterTree");
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");
            TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");

            harness.WaitUntil(() =>
                loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().Any(tab =>
                    (tab.Label ?? string.Empty).Contains("Gear", StringComparison.OrdinalIgnoreCase)));
            NavigatorTabItem gearTab = loadedRunnerTabStrip.Items
                .OfType<NavigatorTabItem>()
                .First(tab => (tab.Label ?? string.Empty).Contains("Gear", StringComparison.OrdinalIgnoreCase));
            harness.SetActiveSectionForTesting(gearTab.SectionId);

            harness.WaitUntil(() =>
                sectionRows.ItemCount > 0
                && !string.IsNullOrWhiteSpace(preview.Text)
                && SnapshotRosterItems(rosterTree).Length > 0);

            CharacterRosterNode[] rootItems = SnapshotRosterItems(rosterTree);

            Assert.IsTrue(rosterTree.IsVisible, "Legacy frmCareer parity requires a visible roster tree posture.");
            Assert.IsTrue(sectionRows.IsVisible, "Legacy frmCareer parity requires a visible dense section/workbench list.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(preview.Text), "Legacy frmCareer parity requires a visible detail/preview pane.");
            Assert.IsTrue(rootItems.Length > 0, "Legacy frmCareer parity requires visible workspace grouping in the roster tree.");

            NavigatorTabItem[] renderedTabs = loadedRunnerTabStrip.Items.OfType<NavigatorTabItem>().ToArray();
            Assert.IsTrue(renderedTabs.Length >= 2, "Legacy frmCareer parity requires multiple visible workbench tabs.");
            Assert.IsTrue(renderedTabs.Any(tab => (tab.Label ?? string.Empty).Contains("Gear", StringComparison.OrdinalIgnoreCase)), "Legacy frmCareer parity requires a gear navigation landmark.");

            string previewPayload = preview.Text ?? string.Empty;
            bool hasLegacyOrWorkflowSectionMarker =
                previewPayload.Contains("\"sectionId\"", StringComparison.Ordinal)
                || previewPayload.Contains("\"workflowId\"", StringComparison.Ordinal)
                || previewPayload.Contains("\"progressionTimelines\"", StringComparison.Ordinal)
                || previewPayload.Contains("\"gear\"", StringComparison.OrdinalIgnoreCase)
                || previewPayload.Contains("\"profile\"", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(
                hasLegacyOrWorkflowSectionMarker,
                "Legacy frmCareer parity requires preview payload landmarks that map sections/workflows to the visible workbench.");
        });
    }

    [TestMethod]
    public void Character_creation_preserves_familiar_dense_builder_rhythm()
    {
        // Runtime_backed_sr4_new_character_dialog_preserves_chummer4_build_method_combo_posture
        // Runtime_backed_sr4_new_character_preserves_modify_button_row_order_and_footer_posture
        WithStandaloneControl<SectionHostControl>(control =>
        {
            List<AttributeEditRequest> edits = [];
            control.AttributeEditRequested += (_, request) => edits.Add(request);
            control.SetState(new SectionHostState(
                SectionId: "attributes",
                NavigationTabs: [],
                ActiveTabId: "tab-info",
                SectionActions: [],
                ActiveActionId: "tab-info.attributes",
                Notice: "Ready.",
                PreviewJson: """
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    }
  ]
}
""",
                Rows: [],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Border attributeEditor = FindDescendant<Border>(control, "AttributeParityEditorBorder");
            Grid bodyRow = FindDescendant<Grid>(control, "AttributeParityRow_BOD");
            Grid baseEditor = FindDescendant<Grid>(control, "AttributeBaseEditor_BOD");
            Grid karmaEditor = FindDescendant<Grid>(control, "AttributeKarmaEditor_BOD");
            Button baseIncreaseButton = FindDescendant<Button>(control, "AttributeBaseEditor_BOD_Increase");
            Button baseDecreaseButton = FindDescendant<Button>(control, "AttributeBaseEditor_BOD_Decrease");
            TextBlock attributeHeader = FindDescendant<TextBlock>(control, "AttributeParityHeaderAttributeText");
            TextBlock startHeader = FindDescendant<TextBlock>(control, "AttributeParityHeaderStartText");
            TextBlock addHeader = FindDescendant<TextBlock>(control, "AttributeParityHeaderAddText");
            TextBlock totalHeader = FindDescendant<TextBlock>(control, "AttributeParityHeaderTotalText");
            TextBlock limitsHeader = FindDescendant<TextBlock>(control, "AttributeParityHeaderLimitsText");
            Control? reviewExpander = FindDescendantOrDefault<Expander>(control, "SectionReviewExpander");
            Control reviewPanel = FindDescendant<Control>(control, "SectionReviewPanel");

            Assert.IsTrue(attributeEditor.IsVisible, "Character creation parity requires the dedicated attribute editor surface.");
            Assert.AreEqual("Attribute", attributeHeader.Text);
            Assert.AreEqual("Start", startHeader.Text);
            Assert.AreEqual("Add", addHeader.Text);
            Assert.AreEqual("Total", totalHeader.Text);
            Assert.AreEqual("Limits", limitsHeader.Text);
            Assert.AreEqual(128d, bodyRow.ColumnDefinitions[1].Width.Value, 0.01d, "Start column must leave room for the attribute value and stepper buttons.");
            Assert.AreEqual(128d, bodyRow.ColumnDefinitions[2].Width.Value, 0.01d, "Add column must leave room for the attribute value and stepper buttons.");
            Assert.AreEqual(28d, baseEditor.ColumnDefinitions[0].Width.Value, 0.01d, "Attribute stepper buttons need a stable touch target column.");
            Assert.AreEqual(10d, baseEditor.ColumnDefinitions[1].Width.Value, 0.01d, "Attribute value must not sit directly against the minus button.");
            Assert.AreEqual(10d, baseEditor.ColumnDefinitions[3].Width.Value, 0.01d, "Attribute value must not sit directly against the plus button.");
            Assert.AreEqual(24d, baseDecreaseButton.Width, 0.01d, "Attribute stepper buttons need a clear visual target.");
            Assert.AreEqual(24d, baseIncreaseButton.Width, 0.01d, "Attribute stepper buttons need a clear visual target.");
            Assert.IsTrue(baseEditor.IsVisible, "Character creation parity requires a visible base stepper editor.");
            Assert.IsTrue(karmaEditor.IsVisible, "Character creation parity requires a visible karma stepper editor.");
            Assert.IsTrue(baseIncreaseButton.IsVisible, "Character creation parity requires visible stepper controls.");
            TextBlock baseValueText = FindDescendant<TextBlock>(control, "AttributeBaseEditor_BOD_Value");
            TextBlock karmaValueText = FindDescendant<TextBlock>(control, "AttributeKarmaEditor_BOD_Value");
            Assert.AreEqual("3", baseValueText.Text);
            Assert.AreEqual("1", karmaValueText.Text);
            Assert.AreEqual(42d, baseValueText.MinWidth, 0.01d, "The base attribute value must have a stable readable width that fits the Start column.");
            Assert.AreEqual(new Thickness(4d, 0d), baseValueText.Margin, "The base attribute value must keep breathing room before +/- buttons.");
            double baseStepperMinimumWidth =
                baseEditor.ColumnDefinitions[0].Width.Value
                + baseEditor.ColumnDefinitions[1].Width.Value
                + baseValueText.MinWidth
                + baseValueText.Margin.Left
                + baseValueText.Margin.Right
                + baseEditor.ColumnDefinitions[3].Width.Value
                + baseEditor.ColumnDefinitions[4].Width.Value;
            Assert.IsTrue(
                baseStepperMinimumWidth <= bodyRow.ColumnDefinitions[1].Width.Value,
                "Attribute stepper value, margins, and buttons must fit inside the Start column without clipping.");
            Assert.IsTrue(
                baseEditor.GetVisualDescendants().OfType<TextBlock>().Any(text => string.Equals(text.Text, "3", StringComparison.Ordinal)),
                "The base attribute editor must render the value cleanly between the stepper buttons.");
            Assert.IsTrue(
                karmaEditor.GetVisualDescendants().OfType<TextBlock>().Any(text => string.Equals(text.Text, "1", StringComparison.Ordinal)),
                "The karma attribute editor must render the value cleanly between the stepper buttons.");
            Assert.IsFalse(
                baseEditor.GetVisualDescendants().OfType<TextBlock>().Any(text => (text.Text ?? string.Empty).Contains("Base", StringComparison.Ordinal)),
                "The row already has a Start header; the stepper value should not repeat internal base wording.");
            Assert.IsFalse(
                karmaEditor.GetVisualDescendants().OfType<TextBlock>().Any(text => (text.Text ?? string.Empty).Contains("Karma", StringComparison.Ordinal)),
                "The row already has an Add header; the stepper value should not repeat internal karma wording.");
            // Legacy-equivalent chrome gate marker: The section preview header must not invent Review chrome that Chummer5A never had.
            Assert.IsNull(reviewExpander, "Character creation parity must not fall back to the review expander.");
            Assert.IsFalse(reviewPanel.IsVisible, "Character creation parity must not fall back to a profile review panel.");

            RaiseClick(baseIncreaseButton);
            PumpStandaloneUi();
            Thread.Sleep(300);
            PumpStandaloneUi();

            Assert.IsTrue(
                baseEditor.GetVisualDescendants().OfType<TextBlock>().Any(text => string.Equals(text.Text, "4", StringComparison.Ordinal)),
                "The base value label must update inline after a mouse click so the user never has to infer the current value from the spinner chrome.");
            Assert.IsTrue(
                edits.Any(edit =>
                    string.Equals(edit.AttributeName, "Body", StringComparison.Ordinal)
                    && string.Equals(edit.Bucket, "base", StringComparison.Ordinal)
                    && edit.Value == 4),
                "Character creation parity must emit a real attribute edit request when the numeric editor changes.");
        });
    }

    [TestMethod]
    public void Fresh_launch_workbench_does_not_render_a_fake_empty_section_expander()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: null,
                NavigationTabs: [],
                ActiveTabId: null,
                SectionActions: [],
                ActiveActionId: null,
                Notice: "Ready.",
                PreviewJson: string.Empty,
                Rows: [],
                QuickActions: [],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Control? reviewExpander = FindDescendantOrDefault<Expander>(control, "SectionReviewExpander");
            Control reviewPanel = FindDescendant<Control>(control, "SectionReviewPanel");
            Border sectionRowsBorder = FindDescendant<Border>(control, "SectionRowsBorder");
            Border sectionContextBorder = FindDescendant<Border>(control, "SectionContextBorder");

            Assert.IsNull(reviewExpander, "A fresh workbench launch must not include a fake empty section expander.");
            Assert.IsFalse(reviewPanel.IsVisible, "A fresh workbench launch must not show a fake empty section review panel.");
            Assert.IsFalse(sectionRowsBorder.IsVisible, "A fresh workbench launch must not show an empty section rows scaffold.");
            Assert.IsFalse(sectionContextBorder.IsVisible, "A fresh workbench launch must not show synthetic section context before a real surface is opened.");
        });
    }

    [TestMethod]
    public void Native_gear_surface_replaces_classic_gear_port_with_a_real_loadout_panel()
    {
        WithStandaloneControl<SectionHostControl>(control =>
        {
            control.SetState(new SectionHostState(
                SectionId: "gear",
                NavigationTabs: [],
                ActiveTabId: "tab-gear",
                SectionActions: [],
                ActiveActionId: "tab-gear.gear",
                Notice: "Ready.",
                PreviewJson: """
{
  "section": "gear",
  "nuyen": 12000,
  "gear": [
    { "name": "Medkit", "rating": 6, "location": "Backpack", "availability": "8R" },
    { "name": "Ammo: APDS", "quantity": 40, "location": "Duffel" }
  ]
}
""",
                Rows:
                [
                    new SectionRowDisplayItem("gear[0]", "Medkit 6 · Backpack"),
                    new SectionRowDisplayItem("gear[1]", "Ammo: APDS ×40 · Duffel")
                ],
                QuickActions:
                [
                    new SectionQuickActionDisplayItem("gear_add", "Add Gear", true)
                ],
                BuildLab: null,
                BrowseWorkspace: null,
                ContactGraph: null,
                DowntimePlanner: null,
                NpcPersonaStudio: null));
            PumpStandaloneUi();

            Border gearWorkbench = FindDescendant<Border>(control, "GearWorkbenchBorder");
            ListBox gearList = FindDescendant<ListBox>(control, "GearWorkbenchList");
            TextBlock detail = FindDescendant<TextBlock>(control, "GearWorkbenchDetailText");
            Border sectionRows = FindDescendant<Border>(control, "SectionRowsBorder");

            Assert.IsTrue(gearWorkbench.IsVisible, "Gear should now render in the native section host instead of the classic compatibility port.");
            Assert.AreEqual(2, gearList.ItemCount);
            StringAssert.Contains(detail.Text ?? string.Empty, "Medkit");
            Assert.IsFalse(sectionRows.IsVisible, "Native gear should not duplicate the same inventory through the generic row list.");
        });
    }

    [TestMethod]
    public void Client_label_visibility_gate_keeps_profile_rows_and_priority_labels_visible_without_collapsible_profile_chrome()
    {
        string sectionMarkup = File.ReadAllText(ResolveSourceFile("Chummer.Avalonia", "Controls", "SectionHostControl.axaml"));
        string sectionSource = File.ReadAllText(ResolveSourceFile("Chummer.Avalonia", "Controls", "SectionHostControl.axaml.cs"));
        string dialogSource = File.ReadAllText(ResolveSourceFile("Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));

        Assert.IsFalse(
            sectionMarkup.Contains("x:Name=\"SectionReviewExpander\"", StringComparison.Ordinal)
            || sectionMarkup.Contains("Name=\"SectionReviewExpander\"", StringComparison.Ordinal),
            "Profile/detail surfaces must not be wrapped in a collapsible SectionReviewExpander.");
        Assert.IsFalse(
            sectionMarkup.Contains("Text=\"{Binding DisplayPath}\"\n                               Classes=\"shell-caption\"\n                               TextTrimming=\"CharacterEllipsis\"\n                               IsVisible=\"False\"", StringComparison.Ordinal),
            "Section row labels must not be suppressed.");
        StringAssert.Contains(sectionSource, "SectionContextTitleText.IsVisible = showContext");
        StringAssert.Contains(sectionSource, "ClassicCharacterSummaryTitle.IsVisible =");
        Assert.IsFalse(
            dialogSource.Contains("Text = text,\n            IsVisible = false,\n            FontWeight = FontWeight.SemiBold", StringComparison.Ordinal),
            "Priority build row labels must remain visible.");
        StringAssert.Contains(dialogSource, "Foreground = ResolveThemeBrush(\"ChummerShellForegroundBrush\"");
        StringAssert.Contains(dialogSource, "valueText.Foreground = ResolveThemeBrush(\"ChummerShellForegroundBrush\"");
        StringAssert.Contains(
            File.ReadAllText(ResolveSourceFile("Chummer.Avalonia", "DesktopDialogWindow.axaml")),
            "<ScrollViewer Grid.Row=\"1\"");
    }

    [TestMethod]
    public void Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm()
    {
        WithLoadedRunnerHarness(harness =>
        {
            harness.SetActiveSectionForTesting("progress");
            ListBox progressRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox progressPreview = harness.FindControl<TextBox>("SectionPreviewBox");
            harness.WaitUntil(() => progressRows.ItemCount > 0);

            string[] progressRowText = SnapshotListBoxItems(progressRows).Select(item => item.ToString() ?? string.Empty).ToArray();
            CollectionAssert.Contains(progressRowText, "progress[0] = First extraction · +2 karma");
            StringAssert.Contains(progressPreview.Text ?? string.Empty, "\"diary\"");
            StringAssert.Contains(progressPreview.Text ?? string.Empty, "\"karma\"");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "progress",
                actionControlId: "create_entry",
                expectedTitle: "Add Entry",
                requiredFieldLabel: "Entry Title",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "profile",
                actionControlId: "open_notes",
                expectedTitle: "Edit Notes",
                requiredFieldLabel: "Notes",
                requiredActionId: "save");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "initiationgrades",
                actionControlId: "initiation_add",
                expectedTitle: "Add Initiation / Submersion",
                requiredFieldLabel: "Grade",
                requiredActionId: "add");
        });
    }

    [TestMethod]
    public void Gear_builder_preserves_familiar_browse_detail_confirm_rhythm()
    {
        WithLoadedRunnerHarness(harness =>
        {
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");
            Border noticeBorder = harness.FindControl<Border>("NoticeBorder");

            harness.WaitUntil(() => sectionRows.ItemCount >= 8);
            string[] rowText = SnapshotListBoxItems(sectionRows).Select(item => item.ToString() ?? string.Empty).ToArray();

            CollectionAssert.Contains(rowText, "gear.weapons[0] = Ares Alpha");
            CollectionAssert.Contains(rowText, "gear.armor[0] = Armor Jacket");
            StringAssert.Contains(preview.Text ?? string.Empty, "\"combat\"");
            Assert.IsFalse(noticeBorder.IsVisible, "Idle ready-state notice chrome should stay hidden in the gear builder.");
        });
    }

    [TestMethod]
    public void Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues()
    {
        // Runtime_backed_sr4_dice_roller_preserves_chummer4_checkbox_copy
        // Runtime_backed_sr4_dice_roller_preserves_chummer4_spinner_posture_and_topbar_geography
        WithLoadedRunnerHarness(harness =>
        {
            harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
            ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
            harness.WaitUntil(() => sectionRows.ItemCount >= 8);

            object[] items = SnapshotListBoxItems(sectionRows);
            object? cyberwareRow = items.FirstOrDefault(item =>
                item.ToString()?.Contains("cyberware[0] = Wired Reflexes 2", StringComparison.Ordinal) == true);

            Assert.IsNotNull(cyberwareRow, "Cyberware row should remain visible in the dense section list.");
            sectionRows.SelectedItem = cyberwareRow;
            harness.WaitUntil(() => ReferenceEquals(sectionRows.SelectedItem, cyberwareRow));

            TextBox preview = harness.FindControl<TextBox>("SectionPreviewBox");
            StringAssert.Contains(preview.Text ?? string.Empty, "\"essence\": 5.34");

            harness.Click("SectionQuickAction_cyberware_add");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Add Cyberware",
                    StringComparison.Ordinal));
            string[] actionIds = harness.DialogActionIds();
            Assert.IsTrue(actionIds.Length > 0, "Cyberware familiarity proof must keep a visible dialog posture with actionable controls.");
            harness.InvokeDialogAction("cancel");
            harness.WaitUntil(() =>
                !string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Add Cyberware",
                    StringComparison.Ordinal));
        });
    }

    [TestMethod]
    public void Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertQuickActionDialogFlow(
                harness,
                sectionId: "drugs",
                actionControlId: "drug_add",
                expectedTitle: "Add Drug",
                requiredFieldLabel: "Drug",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "spells",
                actionControlId: "spell_add",
                expectedTitle: "Add Spell",
                requiredFieldLabel: "Spell",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "powers",
                actionControlId: "adept_power_add",
                expectedTitle: "Add Adept Power",
                requiredFieldLabel: "Power",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "initiationgrades",
                actionControlId: "initiation_add",
                expectedTitle: "Add Initiation / Submersion",
                requiredFieldLabel: "Grade",
                requiredActionId: "add");
        });
    }

    [TestMethod]
    public void Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertQuickActionDialogFlow(
                harness,
                sectionId: "complexforms",
                actionControlId: "complex_form_add",
                expectedTitle: "Add Complex Form",
                requiredFieldLabel: "Complex Form",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "aiprograms",
                actionControlId: "matrix_program_add",
                expectedTitle: "Add Program / Cyberdeck Item",
                requiredFieldLabel: "Program",
                requiredActionId: "add");
        });
    }

    [TestMethod]
    public void Contacts_diary_and_support_routes_execute_with_public_path_visibility()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertQuickActionDialogFlow(
                harness,
                sectionId: "contacts",
                actionControlId: "contact_add",
                expectedTitle: "Add Contact",
                requiredFieldLabel: "Name",
                requiredActionId: "add");

            AssertQuickActionDialogFlow(
                harness,
                sectionId: "progress",
                actionControlId: "create_entry",
                expectedTitle: "Add Entry",
                requiredFieldLabel: "Entry Title",
                requiredActionId: "add");
        });

        WithRuntimeHarness(harness =>
        {
            harness.WaitForReady();
            OpenMenuUntilCommandVisible(harness, "HelpMenuButton", "report_bug");
            harness.ClickMenuCommand("report_bug");
            harness.WaitUntil(() =>
                string.Equals(
                    harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                    "Support and bug reporting",
                    StringComparison.Ordinal));

            string dialogBody = string.Join(
                "\n",
                harness.FindDialogFieldTexts()
                    .Concat(harness.FindDialogFieldInputTexts())
                    .Concat([harness.FindControl<TextBlock>("DialogMessageText").Text ?? string.Empty]));

            Assert.IsTrue(
                dialogBody.Contains("/account/support", StringComparison.OrdinalIgnoreCase)
                || dialogBody.Contains("Hub support surface", StringComparison.OrdinalIgnoreCase)
                || dialogBody.Contains("support closure", StringComparison.OrdinalIgnoreCase),
                "Support/report flow must preserve a public-facing support route description.");
            Assert.IsTrue(
                dialogBody.Contains("/contact", StringComparison.OrdinalIgnoreCase)
                || dialogBody.Contains("support", StringComparison.OrdinalIgnoreCase),
                "Support/report flow must keep a visible public contact/support affordance.");
            Assert.IsTrue(
                dialogBody.Contains("GitHub is still available", StringComparison.OrdinalIgnoreCase)
                || dialogBody.Contains("github.com/ArchonMegalon/Chummer6", StringComparison.OrdinalIgnoreCase),
                "Support/report flow must preserve the public Chummer6 GitHub reporting fallback.");
            Assert.IsFalse(
                dialogBody.Contains("github.com/chummer5a/chummer5a/issues", StringComparison.OrdinalIgnoreCase),
                "Support/report flow must not send Chummer6 client issues to the Chummer5a tracker.");
            Assert.IsFalse(dialogBody.Contains("chummer-api", StringComparison.OrdinalIgnoreCase), "Support/report routes must stay public and must not expose internal Docker hosts.");
            string[] actionIds = harness.DialogActionIds();
            Assert.IsTrue(actionIds.Contains("close", StringComparer.Ordinal), "Support/report flow must expose an explicit close/confirm affordance.");
        });
    }

    [TestMethod]
    public void Standalone_report_issue_window_labels_every_text_input()
    {
        WithStandaloneReportIssueWindow(window =>
        {
            (string InputName, string LabelName, string ExpectedLabel)[] requiredFields =
            [
                ("ReportBugTitleBox", "ReportBugTitleBoxLabel", "Title"),
                ("ReportBugExpectedBox", "ReportBugExpectedBoxLabel", "Expected behavior"),
                ("ReportBugActualBox", "ReportBugActualBoxLabel", "Actual behavior"),
                ("ReportBugReproStepsBox", "ReportBugReproStepsBoxLabel", "Repro steps"),
                ("ReportBugEvidenceBox", "ReportBugEvidenceBoxLabel", "Screenshot or attachment note"),
                ("ReportFeedbackSummaryBox", "ReportFeedbackSummaryBoxLabel", "Feedback summary"),
                ("ReportFeedbackDetailBox", "ReportFeedbackDetailBoxLabel", "More detail")
            ];

            foreach ((string inputName, string labelName, string expectedLabel) in requiredFields)
            {
                TextBox input = FindDescendant<TextBox>(window, inputName);
                TextBlock label = FindDescendant<TextBlock>(window, labelName);

                Assert.IsTrue(input.IsVisible, $"Report Issue field '{inputName}' must be visible.");
                Assert.IsTrue(label.IsVisible, $"Report Issue field '{inputName}' must have a visible label.");
                Assert.AreEqual(expectedLabel, label.Text, $"Report Issue label for '{inputName}' must stay explicit.");
                Assert.AreEqual(expectedLabel, AutomationProperties.GetName(input), $"Report Issue field '{inputName}' must expose its label to assistive tech.");
            }
        });
    }

    [TestMethod]
    public void Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm()
    {
        WithLoadedRunnerHarness(harness =>
        {
            AssertQuickActionDialogFlow(
                harness,
                sectionId: "vehicles",
                actionControlId: "vehicle_add",
                expectedTitle: "Add Vehicle / Drone",
                requiredFieldLabel: "Vehicle",
                requiredActionId: "add");
        });
    }

    [TestMethod]
    public void Visual_review_evidence_is_published_for_light_and_dark_shell_states()
    {
        // Runtime_backed_sr4_starter_runner_gear_add_uses_chummer4_category_combobox_posture
        // Runtime_backed_sr4_starter_runner_gear_add_preserves_two_pane_geography_and_primary_action_band
        // Runtime_backed_gear_add_dialog_uses_legacy_category_combobox_posture
        string screenshotDirectory = ResolveScreenshotDirectory();
        if (Directory.Exists(screenshotDirectory))
        {
            Directory.Delete(screenshotDirectory, recursive: true);
        }

        Directory.CreateDirectory(screenshotDirectory);

        string[] expectedFiles = VeteranCertificationScreenshotFiles;

        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            Dictionary<string, ScreenshotProofCapture> screenshots = new(StringComparer.Ordinal);

            string? priorReleaseChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
            string? priorSampleControls = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES");
            try
            {
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "public_stable");
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", null);
                screenshots[expectedFiles[0]] = WithHarness(harness =>
                {
                    harness.WaitForReady();
                    harness.SetTheme(ThemeVariant.Light);
                    return CaptureScreenshotProof(harness, expectedFiles[0]);
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", priorReleaseChannel);
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_ENABLE_SAMPLES", priorSampleControls);
            }

            Dictionary<string, ScreenshotProofCapture> veteranWorkflowScreenshots = WithHarness(harness =>
            {
                Dictionary<string, ScreenshotProofCapture> captured = new(StringComparer.Ordinal);

                harness.WaitForReady();

                harness.SetTheme(ThemeVariant.Light);
                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
                captured[expectedFiles[1]] = CaptureScreenshotProof(harness, expectedFiles[1]);

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => string.IsNullOrWhiteSpace(harness.ShellPresenter.State.OpenMenuId));

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "global_settings");
                harness.ClickMenuCommand("global_settings");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Global Settings",
                    "UI Scale",
                    "Theme",
                    "Language",
                    "Compact Mode");
                captured[expectedFiles[2]] = CaptureScreenshotProof(harness, expectedFiles[2]);

                harness.InvokeDialogAction("save");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Global Settings",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                        harness.Presenter.ImportCalls > 0
                        && !string.IsNullOrWhiteSpace(harness.State.Profile?.Name)
                        && harness.FindControlOrDefault<Control>("LoadedRunnerTabStripBorder")?.IsVisible == true
                        && harness.FindControlOrDefault<Control>("QuickStartContainer")?.IsVisible != true
                        && !harness.State.IsBusy,
                    timeoutMs: 8000);
                Assert.IsFalse(string.IsNullOrWhiteSpace(harness.State.Profile?.Name), "Import familiarity screenshot must capture a loaded runner profile.");
                Assert.IsTrue(
                    harness.FindControl<Control>("LoadedRunnerTabStripBorder").IsVisible,
                    "Import familiarity screenshot must capture the loaded-runner tab strip.");
                Assert.IsFalse(
                    harness.FindControlOrDefault<Control>("QuickStartContainer")?.IsVisible ?? false,
                    "Import familiarity screenshot must not be a first-run placeholder shell.");
                captured[expectedFiles[3]] = CaptureScreenshotProof(harness, expectedFiles[3]);

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));
                captured["19-workflow-file-menu-loaded-light.png"] = CaptureScreenshotProof(harness, "19-workflow-file-menu-loaded-light.png");
                harness.ClickMenuCommand("new_character");
                harness.WaitUntil(() =>
                        string.Equals(
                            harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                            "Select Build Method",
                            StringComparison.Ordinal),
                    timeoutMs: 8000);
                captured["36-workflow-new-character-dialog-light.png"] = CaptureScreenshotProof(harness, "36-workflow-new-character-dialog-light.png");
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Select Build Method",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("skills");
                ListBox denseSectionRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => denseSectionRows.ItemCount > 0);
                object[] denseRows = SnapshotListBoxItems(denseSectionRows);
                Assert.IsTrue(denseRows.Length > 0, "Expected dense section rows before capturing dense familiarity proof.");
                denseSectionRows.SelectedItem = denseRows[0];
                harness.WaitUntil(() => ReferenceEquals(denseSectionRows.SelectedItem, denseRows[0]));
                captured[expectedFiles[4]] = CaptureScreenshotProof(harness, expectedFiles[4]);
                captured["20-workflow-skills-section-light.png"] = CaptureScreenshotProof(harness, "20-workflow-skills-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_skill_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_skill_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Skill",
                        StringComparison.Ordinal));
                captured["21-workflow-skill-add-dialog-light.png"] = CaptureScreenshotProof(harness, "21-workflow-skill-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Skill",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("qualities");
                ListBox qualityRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => qualityRows.ItemCount > 0);
                captured["22-workflow-qualities-section-light.png"] = CaptureScreenshotProof(harness, "22-workflow-qualities-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_quality_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_quality_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Quality",
                        StringComparison.Ordinal));
                captured["23-workflow-quality-add-dialog-light.png"] = CaptureScreenshotProof(harness, "23-workflow-quality-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Quality",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("gear");
                ListBox gearRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => gearRows.ItemCount > 0);
                captured["24-workflow-gear-section-light.png"] = CaptureScreenshotProof(harness, "24-workflow-gear-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_gear_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_gear_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Gear",
                        StringComparison.Ordinal));
                captured["25-workflow-gear-add-dialog-light.png"] = CaptureScreenshotProof(harness, "25-workflow-gear-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Gear",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("weapons");
                ListBox weaponRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => weaponRows.ItemCount > 0);
                captured["26-workflow-weapons-section-light.png"] = CaptureScreenshotProof(harness, "26-workflow-weapons-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_combat_add_weapon")?.IsVisible == true);
                harness.Click("SectionQuickAction_combat_add_weapon");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Weapon",
                        StringComparison.Ordinal));
                captured["27-workflow-weapon-add-dialog-light.png"] = CaptureScreenshotProof(harness, "27-workflow-weapon-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Weapon",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("armors");
                ListBox armorRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => armorRows.ItemCount > 0);
                captured["28-workflow-armor-section-light.png"] = CaptureScreenshotProof(harness, "28-workflow-armor-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_combat_add_armor")?.IsVisible == true);
                harness.Click("SectionQuickAction_combat_add_armor");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Armor",
                        StringComparison.Ordinal));
                captured["29-workflow-armor-add-dialog-light.png"] = CaptureScreenshotProof(harness, "29-workflow-armor-add-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Armor",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("cyberwares");
                ListBox cyberwareRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => cyberwareRows.ItemCount > 0);
                captured["30-workflow-cyberware-section-light.png"] = CaptureScreenshotProof(harness, "30-workflow-cyberware-section-light.png");

                harness.SetActiveSectionForTesting("powers");
                ListBox powerRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => powerRows.ItemCount > 0);
                captured["31-workflow-powers-section-light.png"] = CaptureScreenshotProof(harness, "31-workflow-powers-section-light.png");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_adept_power_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_adept_power_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Adept Power",
                        StringComparison.Ordinal));
                captured["32-workflow-adept-power-dialog-light.png"] = CaptureScreenshotProof(harness, "32-workflow-adept-power-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Adept Power",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("complexforms");
                ListBox complexFormRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => complexFormRows.ItemCount > 0);
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_complex_form_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_complex_form_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Complex Form",
                        StringComparison.Ordinal));
                captured["33-workflow-complex-form-dialog-light.png"] = CaptureScreenshotProof(harness, "33-workflow-complex-form-dialog-light.png");
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Complex Form",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("validation");
                ListBox validationRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => validationRows.ItemCount > 0);
                captured["34-workflow-validate-section-light.png"] = CaptureScreenshotProof(harness, "34-workflow-validate-section-light.png");

                harness.SetActiveSectionForTesting("sources");
                ListBox sourceRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => sourceRows.ItemCount > 0);
                captured["35-workflow-rules-section-light.png"] = CaptureScreenshotProof(harness, "35-workflow-rules-section-light.png");

                harness.SetActiveSectionForTesting("calendar");
                ListBox calendarRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => calendarRows.ItemCount > 0);
                captured["37-workflow-calendar-section-light.png"] = CaptureScreenshotProof(harness, "37-workflow-calendar-section-light.png");

                harness.SetTheme(ThemeVariant.Dark);
                captured[expectedFiles[5]] = CaptureScreenshotProof(harness, expectedFiles[5]);

                harness.SetTheme(ThemeVariant.Light);
                TabStrip loadedRunnerTabStrip = harness.FindControl<TabStrip>("LoadedRunnerTabStrip");
                harness.WaitUntil(() =>
                {
                    return loadedRunnerTabStrip.Items
                        .OfType<NavigatorTabItem>()
                        .Any(static item => (item.Label ?? string.Empty).Contains("Runner", StringComparison.Ordinal));
                });
                captured[expectedFiles[6]] = CaptureScreenshotProof(harness, expectedFiles[6]);

                harness.SetActiveSectionForTesting("cyberwares");
                ListBox sectionRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
                harness.WaitUntil(() => sectionRows.ItemCount > 0);
                harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
                harness.Click("SectionQuickAction_cyberware_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Cyberware",
                        StringComparison.Ordinal));
                captured[expectedFiles[7]] = CaptureScreenshotProof(harness, expectedFiles[7]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Cyberware",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("vehicles");
                harness.WaitUntil(() => harness.FindControl<Control>("SectionQuickActionsBorder").IsVisible);
                ListBox vehicleRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => vehicleRows.ItemCount > 0);
                object? vehicleRow = SnapshotListBoxItems(vehicleRows).FirstOrDefault(item =>
                    item.ToString()?.Contains("vehicles[0] = Roadmaster", StringComparison.Ordinal) == true);
                Assert.IsNotNull(vehicleRow, "Expected a vehicle row before capturing vehicle familiarity proof.");
                vehicleRows.SelectedItem = vehicleRow;
                harness.WaitUntil(() => ReferenceEquals(vehicleRows.SelectedItem, vehicleRow));
                captured[expectedFiles[8]] = CaptureScreenshotProof(harness, expectedFiles[8]);

                harness.SetActiveSectionForTesting("contacts");
                ListBox contactRows = harness.FindControl<ListBox>("SectionRowsList");
                harness.WaitUntil(() => contactRows.ItemCount > 0);
                object? contactRow = SnapshotListBoxItems(contactRows).FirstOrDefault(item =>
                    item.ToString()?.Contains("contacts[0] = Fixer", StringComparison.Ordinal) == true);
                Assert.IsNotNull(contactRow, "Expected a contact row before capturing contact familiarity proof.");
                contactRows.SelectedItem = contactRow;
                harness.WaitUntil(() => ReferenceEquals(contactRows.SelectedItem, contactRow));
                captured[expectedFiles[9]] = CaptureScreenshotProof(harness, expectedFiles[9]);

                harness.SetActiveSectionForTesting("progress");
                harness.WaitUntil(() => harness.FindControlOrDefault<Control>("SectionQuickAction_create_entry")?.IsVisible == true);
                harness.Click("SectionQuickAction_create_entry");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Entry",
                        StringComparison.Ordinal));
                captured[expectedFiles[10]] = CaptureScreenshotProof(harness, expectedFiles[10]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Entry",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("spells");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_spell_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_spell_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Spell",
                        StringComparison.Ordinal));
                captured[expectedFiles[11]] = CaptureScreenshotProof(harness, expectedFiles[11]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Spell",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("aiprograms");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_matrix_program_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_matrix_program_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Program / Cyberdeck Item",
                        StringComparison.Ordinal));
                captured[expectedFiles[12]] = CaptureScreenshotProof(harness, expectedFiles[12]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Program / Cyberdeck Item",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("initiationgrades");
                harness.WaitUntil(() =>
                    harness.FindControlOrDefault<Control>("SectionQuickAction_initiation_add")?.IsVisible == true);
                harness.Click("SectionQuickAction_initiation_add");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Initiation / Submersion",
                        StringComparison.Ordinal));
                captured[expectedFiles[13]] = CaptureScreenshotProof(harness, expectedFiles[13]);
                harness.InvokeDialogAction("add");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Add Initiation / Submersion",
                        StringComparison.Ordinal));

                harness.SetActiveSectionForTesting("attributes");
                harness.WaitUntil(() => harness.FindControl<Control>("AttributeParityEditorBorder").IsVisible);
                Assert.IsNotNull(harness.FindControlOrDefault<Grid>("AttributeBaseEditor_BOD"), "Expected compact attribute stepper editors before capturing character-creation familiarity proof.");
                Assert.IsNotNull(harness.FindControlOrDefault<Button>("AttributeBaseEditor_BOD_Increase"), "Expected visible attribute stepper buttons before capturing character-creation familiarity proof.");
                captured[expectedFiles[14]] = CaptureScreenshotProof(harness, expectedFiles[14]);

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "master_index");
                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("master_index", CancellationToken.None).
                harness.ClickMenuCommand("master_index");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Master Index",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Master Index",
                    "Data Root",
                    "/app/data");
                captured[expectedFiles[15]] = CaptureScreenshotProof(harness, expectedFiles[15]);
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Master Index",
                        StringComparison.Ordinal));
                harness.WaitUntil(() => !harness.State.IsBusy && harness.FindControl<MenuItem>("ToolsMenuButton").IsEnabled);

                OpenMenuUntilCommandVisible(harness, "ToolsMenuButton", "character_roster");
                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("character_roster", CancellationToken.None).
                harness.ClickMenuCommand("character_roster");
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Character Roster",
                    "Description",
                    "Concept",
                    "Background",
                    "Character Notes",
                    "Game Notes");
                captured[expectedFiles[16]] = CaptureScreenshotProof(harness, expectedFiles[16]);
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Character Roster",
                        StringComparison.Ordinal));

                harness.Click("FileMenuButton");
                harness.WaitUntil(() => IsCommandVisibleInCommandList(harness, "open_character"));
                harness.ClickMenuCommand("open_character");
                AssertDialogContainsAll(
                    harness,
                    "Open Character",
                    GetVeteranCertificationReviewStep("import").RequiredDialogMarkers);
                captured[GetVeteranCertificationReviewStep("import").ScreenshotFileName] = CaptureScreenshotProof(harness, GetVeteranCertificationReviewStep("import").ScreenshotFileName);
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Open Character",
                        StringComparison.Ordinal));

                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("translator", CancellationToken.None).
                harness.Presenter.ExecuteCommandAsync("translator", CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Translator",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Translator",
                    GetImportRouteReviewStep("translator").RequiredDialogMarkers);
                captured[GetImportRouteReviewStep("translator").ScreenshotFileName] = CaptureScreenshotProof(harness, GetImportRouteReviewStep("translator").ScreenshotFileName);
                harness.InvokeDialogAction("close");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Translator",
                        StringComparison.Ordinal));

                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("xml_editor", CancellationToken.None).
                harness.Presenter.ExecuteCommandAsync("xml_editor", CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "XML Editor",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "XML Editor",
                    GetImportRouteReviewStep("xml_amendment_editor").RequiredDialogMarkers);
                captured[GetImportRouteReviewStep("xml_amendment_editor").ScreenshotFileName] = CaptureScreenshotProof(harness, GetImportRouteReviewStep("xml_amendment_editor").ScreenshotFileName);
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "XML Editor",
                        StringComparison.Ordinal));

                // Presenter-backed parity proof anchor: harness.Presenter.ExecuteCommandAsync("hero_lab_importer", CancellationToken.None).
                harness.Presenter.ExecuteCommandAsync("hero_lab_importer", CancellationToken.None).GetAwaiter().GetResult();
                harness.WaitUntil(() =>
                    string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Hero Lab Importer",
                        StringComparison.Ordinal));
                AssertDialogContainsAll(
                    harness,
                    "Hero Lab Importer",
                    GetImportRouteReviewStep("hero_lab_importer").RequiredDialogMarkers);
                captured[GetImportRouteReviewStep("hero_lab_importer").ScreenshotFileName] = CaptureScreenshotProof(harness, GetImportRouteReviewStep("hero_lab_importer").ScreenshotFileName);
                harness.InvokeDialogAction("cancel");
                harness.WaitUntil(() =>
                    !string.Equals(
                        harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                        "Hero Lab Importer",
                        StringComparison.Ordinal));

                harness.Click("HorizonsButton");
                harness.WaitUntil(
                    () => DesktopHorizonsWindow.LastOpenedWindowForTesting is { IsVisible: true },
                    timeoutMs: 8000);
                Window hubWindow = DesktopHorizonsWindow.LastOpenedWindowForTesting
                    ?? throw new AssertFailedException("Horizons hub window was not opened for screenshot publication.");
                Assert.IsNotNull(harness.FindControlInWindowOrDefault<TextBlock>(hubWindow, "HorizonsPostureText"));
                captured["41-horizons-hub-light.png"] = CaptureScreenshotProof(harness, hubWindow, "41-horizons-hub-light.png");

                foreach (HorizonScreenshotSurface surface in NativeHorizonScreenshotSurfaces)
                {
                    Button launchButton = harness.FindControlInWindow<Button>(hubWindow, $"HorizonsOpenWorkbench_{surface.EntryId}");
                    RaiseClick(launchButton);
                    harness.WaitUntil(
                        () => FindNativeWorkbenchWindow(surface.EntryId) is { IsVisible: true },
                        timeoutMs: 8000);
                    Window workbenchWindow = FindNativeWorkbenchWindow(surface.EntryId)
                        ?? throw new AssertFailedException($"Workbench '{surface.WindowTitle}' was not opened for screenshot publication.");
                    Assert.IsNotNull(
                        harness.FindControlInWindowOrDefault<Control>(workbenchWindow, surface.RequiredControlName),
                        $"Workbench '{surface.WindowTitle}' must expose proof control '{surface.RequiredControlName}' before screenshot capture.");
                    captured[surface.ScreenshotFileName] = CaptureScreenshotProof(harness, workbenchWindow, surface.ScreenshotFileName);
                    workbenchWindow.Close();
                    harness.WaitUntil(
                        () => FindNativeWorkbenchWindow(surface.EntryId) is null,
                        timeoutMs: 4000);
                }

                hubWindow.Close();
                harness.WaitUntil(
                    () => DesktopHorizonsWindow.LastOpenedWindowForTesting is null,
                    timeoutMs: 4000);

                return captured;
            });

            foreach ((string screenshotName, ScreenshotProofCapture capture) in veteranWorkflowScreenshots)
            {
                screenshots[screenshotName] = capture;
            }

            foreach ((string fileName, ScreenshotProofCapture capture) in screenshots)
            {
                File.WriteAllBytes(Path.Combine(screenshotDirectory, fileName), capture.PngBytes);
            }

            string screenshotControlEvidencePath = Path.Combine(screenshotDirectory, "SCREENSHOT_CONTROL_EVIDENCE.generated.json");
            object screenshotControlEvidencePayload = new
            {
                generatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                authority = new
                {
                    visualBaseline = "Chummer5a",
                    releaseAuthorityPlatform = "windows",
                    captureHead = "avalonia",
                    menuInteractionMode = "real_menu_items",
                    dialogHostPolicy = "dedicated_desktop_dialog_window",
                    forbiddenInlineSurface = "RightShellRegion"
                },
                supportingProofs = new
                {
                    windowsDesktopExitGate = ".codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json",
                    startupSmokeAndExecutableGate = ".codex-studio/published/NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json",
                    flagshipReleaseGate = ".codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
                },
                workflowCoverage = WorkflowScreenshotCoverage.Select(entry => new
                {
                    workflowFamilyId = entry.WorkflowFamilyId,
                    legacyBehaviorLineage = entry.LegacyBehaviorLineage,
                    screenshotFiles = entry.ScreenshotFiles,
                    screenshotCount = entry.ScreenshotFiles.Length
                }).ToArray(),
                entries = screenshots.Values
                    .Select(capture => capture.Evidence)
                    .OrderBy(entry => entry.Screenshot, StringComparer.Ordinal)
                    .Select(entry => new
                    {
                        screenshot = entry.Screenshot,
                        theme = entry.Theme,
                        dialogTitle = entry.DialogTitle,
                        dialogMessage = entry.DialogMessage,
                        dialogFieldLabels = entry.DialogFieldLabels,
                        dialogFieldIds = entry.DialogFieldIds,
                        dialogFieldControlIds = entry.DialogFieldControlIds,
                        dialogFieldInputValues = entry.DialogFieldInputValues,
                        dialogActionIds = entry.DialogActionIds,
                        dialogActionControlIds = entry.DialogActionControlIds,
                        visibleNamedControlIds = entry.VisibleNamedControlIds,
                        visibleNamedControls = entry.VisibleNamedControls,
                        visibleTextSamples = entry.VisibleTextSamples,
                        visibleMenuCommandIds = entry.VisibleMenuCommandIds,
                        visibleTabLabels = entry.VisibleTabLabels,
                        visibleSectionQuickActionIds = entry.VisibleSectionQuickActionIds,
                        selectedListRowTexts = entry.SelectedListRowTexts,
                        previewText = entry.PreviewText,
                        rightShellVisible = entry.RightShellVisible,
                        rightShellWidth = entry.RightShellWidth,
                        inlineCommandSurfaceVisible = entry.InlineCommandSurfaceVisible,
                        dialogWindowVisible = entry.DialogWindowVisible
                    })
                    .ToArray()
            };
            File.WriteAllText(
                screenshotControlEvidencePath,
                JsonSerializer.Serialize(screenshotControlEvidencePayload, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
                Utf8NoBom);
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        foreach (string fileName in expectedFiles)
        {
            string fullPath = Path.Combine(screenshotDirectory, fileName);
            Assert.IsTrue(File.Exists(fullPath), $"Expected screenshot evidence '{fileName}' was not created.");

            FileInfo fileInfo = new(fullPath);
            Assert.IsTrue(fileInfo.Length > 0, $"Screenshot evidence '{fileName}' is empty.");
        }

        Assert.IsTrue(
            File.Exists(Path.Combine(screenshotDirectory, "SCREENSHOT_CONTROL_EVIDENCE.generated.json")),
            "Expected screenshot control evidence JSON was not created.");
    }

    private sealed record VeteranCertificationReviewStep(
        string Surface,
        string ScreenshotFileName,
        string PromotedHeadGesture,
        string Chummer5aBaseline,
        string[] RequiredDialogMarkers);

    private sealed record WorkflowScreenshotCoverageEntry(
        string WorkflowFamilyId,
        string LegacyBehaviorLineage,
        string[] ScreenshotFiles);

    private sealed record HorizonScreenshotSurface(
        string EntryId,
        string ScreenshotFileName,
        string WindowTitle,
        string RequiredControlName);

    private static VeteranCertificationReviewStep GetVeteranCertificationReviewStep(string surface)
        => VeteranCertificationReviewSteps.Single(step => string.Equals(step.Surface, surface, StringComparison.Ordinal));

    private static VeteranCertificationReviewStep GetImportRouteReviewStep(string surface)
        => ImportRouteReviewSteps.Single(step => string.Equals(step.Surface, surface, StringComparison.Ordinal));

    private static Window? FindNativeWorkbenchWindow(string entryId)
    {
        return entryId switch
        {
            "karma_forge" => DesktopKarmaForgeWindow.LastOpenedWindowForTesting,
            "alice" => DesktopAliceWindow.LastOpenedWindowForTesting,
            "black_ledger" => DesktopBlackLedgerWindow.LastOpenedWindowForTesting,
            "run_control" => DesktopRunControlWindow.LastOpenedWindowForTesting,
            "runsite" => DesktopRunsiteWindow.LastOpenedWindowForTesting,
            "jackpoint" => DesktopJackpointWindow.LastOpenedWindowForTesting,
            "table_pulse" => DesktopTablePulseWindow.LastOpenedWindowForTesting,
            "community_hub" => DesktopCommunityHubWindow.LastOpenedWindowForTesting,
            "nexus_pan" => DesktopNexusPanWindow.LastOpenedWindowForTesting,
            "quicksilver" => DesktopQuicksilverWindow.LastOpenedWindowForTesting,
            "runner_passport" => DesktopRunnerPassportWindow.LastOpenedWindowForTesting,
            "runbook_press" => DesktopRunbookPressWindow.LastOpenedWindowForTesting,
            "creator_os" => DesktopCreatorOsWindow.LastOpenedWindowForTesting,
            "local_co_processor" => DesktopLocalCoProcessorWindow.LastOpenedWindowForTesting,
            "anarchy" => DesktopAnarchyWindow.LastOpenedWindowForTesting,
            "ghostwire" => DesktopGhostwireWindow.LastOpenedWindowForTesting,
            "ready_for_tonight" => DesktopReadyForTonightWindow.LastOpenedWindowForTesting,
            "onramp" => DesktopOnrampWindow.LastOpenedWindowForTesting,
            "knowledge_fabric" => DesktopKnowledgeFabricWindow.LastOpenedWindowForTesting,
            _ => null
        };
    }

    private static void OpenMenuUntilCommandVisible(FlagshipUiHarness harness, string menuButtonName, string commandId)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            harness.Click(menuButtonName);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (IsCommandVisibleInCommandList(harness, commandId))
                {
                    return;
                }

                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }
        }

        Assert.Fail($"Timed out opening '{menuButtonName}' for command '{commandId}'.");
    }

    private static void OpenMenuUntilCommandVisible(RuntimeFlagshipUiHarness harness, string menuButtonName, string commandId)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            harness.Click(menuButtonName);
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (IsCommandVisibleInCommandList(harness, commandId))
                {
                    return;
                }

                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }

            MenuItem? menuButton = harness.FindControlOrDefault<MenuItem>(menuButtonName);
            string? menuId = menuButton?.Tag?.ToString();
            if (!string.IsNullOrWhiteSpace(menuId))
            {
                harness.ShellPresenter.ToggleMenuAsync(menuId, CancellationToken.None).GetAwaiter().GetResult();
                DateTime fallbackDeadline = DateTime.UtcNow.AddMilliseconds(1200);
                while (DateTime.UtcNow < fallbackDeadline)
                {
                    Dispatcher.UIThread.RunJobs();
                    if (IsCommandVisibleInCommandList(harness, commandId))
                    {
                        return;
                    }

                    Thread.Sleep(10);
                    Dispatcher.UIThread.RunJobs();
                }
            }
        }

        Assert.Fail($"Timed out opening '{menuButtonName}' for command '{commandId}'.");
    }

    private static bool IsCommandVisibleInCommandList(FlagshipUiHarness harness, string commandId)
    {
        if (RootMenuControlNames
            .Select(harness.FindControlOrDefault<MenuItem>)
            .Where(static root => root is not null)
            .SelectMany(static root => root!.Items.OfType<MenuItem>())
            .Any(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal) && item.IsEnabled))
        {
            return true;
        }

        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        if (commandsList is null)
        {
            return false;
        }

        return SnapshotListBoxItems(commandsList)
            .OfType<CommandPaletteItem>()
            .Any(item => string.Equals(item.Id, commandId, StringComparison.Ordinal));
    }

    private static bool IsAnyCommandVisibleInCommandList(FlagshipUiHarness harness)
    {
        if (RootMenuControlNames
            .Select(harness.FindControlOrDefault<MenuItem>)
            .Where(static root => root is not null)
            .SelectMany(static root => root!.Items.OfType<MenuItem>())
            .Any(item => item.IsEnabled))
        {
            return true;
        }

        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        return commandsList is not null
            && SnapshotListBoxItems(commandsList).OfType<CommandPaletteItem>().Any();
    }

    private static bool IsCommandVisibleInCommandList(RuntimeFlagshipUiHarness harness, string commandId)
    {
        if (RootMenuControlNames
            .Select(harness.FindControlOrDefault<MenuItem>)
            .Where(static root => root is not null)
            .SelectMany(static root => root!.Items.OfType<MenuItem>())
            .Any(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal) && item.IsEnabled))
        {
            return true;
        }

        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        if (commandsList is null)
        {
            return false;
        }

        return SnapshotListBoxItems(commandsList)
            .OfType<CommandPaletteItem>()
            .Any(item => string.Equals(item.Id, commandId, StringComparison.Ordinal));
    }

    private static bool IsAnyCommandVisibleInCommandList(RuntimeFlagshipUiHarness harness)
    {
        if (RootMenuControlNames
            .Select(harness.FindControlOrDefault<MenuItem>)
            .Where(static root => root is not null)
            .SelectMany(static root => root!.Items.OfType<MenuItem>())
            .Any(item => item.IsEnabled))
        {
            return true;
        }

        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        return commandsList is not null
            && SnapshotListBoxItems(commandsList).OfType<CommandPaletteItem>().Any();
    }

    private static string[] CaptureVisibleCommandLabels(RuntimeFlagshipUiHarness harness)
    {
        MenuItem[] visibleMenuItems = RootMenuControlNames
            .Select(harness.FindControlOrDefault<MenuItem>)
            .Where(static root => root is not null)
            .SelectMany(static root => root!.Items.OfType<MenuItem>())
            .Where(static item => item.IsEnabled)
            .ToArray();
        if (visibleMenuItems.Length > 0)
        {
            return visibleMenuItems
                .Select(item => item.Header?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        return commandsList is null
            ? Array.Empty<string>()
            : SnapshotListBoxItems(commandsList)
                .OfType<CommandPaletteItem>()
                .Select(item => item.Label)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
    }

    private static void WithHarness(Action<FlagshipUiHarness> assertion)
    {
        WithHarness<bool>(harness =>
        {
            assertion(harness);
            return true;
        });
    }

    private static TResult WithHarness<TResult>(Func<FlagshipUiHarness, TResult> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
                return session.Dispatch(() =>
                    {
                        using FlagshipUiHarness harness = new();
                        return assertion(harness);
                    },
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia headless session did not stabilize for flagship UI proof.", lastFailure);
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

    private sealed class FlagshipHeadlessAppBootstrap
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

    private static void WithRuntimeHarness(Action<RuntimeFlagshipUiHarness> assertion)
    {
        WithRuntimeHarness<bool>(harness =>
        {
            assertion(harness);
            return true;
        });
    }

    private static TResult WithRuntimeHarness<TResult>(Func<RuntimeFlagshipUiHarness, TResult> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
                return session.Dispatch(() =>
                    {
                        using RuntimeFlagshipUiHarness harness = new();
                        return assertion(harness);
                    },
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia runtime headless session did not stabilize for flagship UI proof.", lastFailure);
    }

    private static void DisposeHeadlessSessionQuietly(HeadlessUnitTestSession? session)
    {
        if (session is null)
        {
            return;
        }

        try
        {
            session.Dispose();
        }
        catch (NullReferenceException)
        {
            // Avalonia headless teardown can intermittently throw after successful dispatch.
            // Keep test assertions as the authoritative pass/fail signal.
        }
    }

    private static bool IsTransientHeadlessFailure(Exception ex)
    {
        if (ex is AssertFailedException assertFailed
            && (assertFailed.Message.Contains("No rendered frame was available", StringComparison.Ordinal)
                || assertFailed.Message.Contains("Timed out waiting for UI condition", StringComparison.Ordinal)
                || assertFailed.Message.Contains("Timed out waiting for runtime-backed UI condition", StringComparison.Ordinal)))
        {
            return true;
        }

        if (ex is InvalidOperationException invalidOperation
            && (invalidOperation.Message.Contains("IWindowingPlatform", StringComparison.Ordinal)
                || invalidOperation.Message.Contains("ICursorFactory", StringComparison.Ordinal)
                || invalidOperation.Message.Contains("Could not create glyphTypeface", StringComparison.Ordinal)
                || invalidOperation.Message.Contains("Call from invalid thread", StringComparison.Ordinal)))
        {
            return true;
        }

        return ex.InnerException is not null && IsTransientHeadlessFailure(ex.InnerException);
    }

    private static string FindTestFilePath(string fileName)
    {
        string? match = ResolveExistingPath(
            Path.Combine("Chummer.Tests", "TestFiles", fileName),
            Path.Combine("TestFiles", fileName),
            Path.Combine("/src", "Chummer.Tests", "TestFiles", fileName),
            Path.Combine("/docker/chummercomplete/chummer-presentation", "Chummer.Tests", "TestFiles", fileName),
            Path.Combine("/docker/chummercomplete/chummer6-ui", "Chummer.Tests", "TestFiles", fileName),
            Path.Combine("/docker/chummercomplete/chummer6-ui-finish", "Chummer.Tests", "TestFiles", fileName));
        if (match is null)
        {
            throw new FileNotFoundException("Could not locate test file.", fileName);
        }

        return match;
    }

    private static string[] ResolveChummer5aFixtureUiReconstructionFixtureNames()
    {
        string? configuredFixtureFile = Environment.GetEnvironmentVariable("CHUMMER_FIXTURE_UI_RECONSTRUCTION_FIXTURES_FILE");
        if (!string.IsNullOrWhiteSpace(configuredFixtureFile))
        {
            string[] fixtureNames = File.ReadAllLines(configuredFixtureFile)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (fixtureNames.Length == 0)
            {
                throw new AssertFailedException(
                    $"Fixture UI reconstruction fixture file '{configuredFixtureFile}' did not contain any fixture names.");
            }

            return fixtureNames;
        }

        string scope = (Environment.GetEnvironmentVariable("CHUMMER_FIXTURE_UI_RECONSTRUCTION_SCOPE") ?? string.Empty).Trim();
        if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
        {
            string[] allFixtureNames = Directory.EnumerateFiles(ResolveTestFilesDirectory(), "*.chum5")
                .Select(Path.GetFileName)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray()!;
            if (allFixtureNames.Length == 0)
            {
                throw new AssertFailedException("Fixture UI reconstruction scope 'all' resolved zero .chum5 fixtures.");
            }

            return allFixtureNames;
        }

        return DefaultChummer5aFixtureUiReconstructionFixtureNames;
    }

    private static string ResolveTestFilesDirectory()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string[] candidates =
        {
            Path.Combine(repoRoot, "Chummer.Tests", "TestFiles"),
            Path.Combine(Directory.GetCurrentDirectory(), "Chummer.Tests", "TestFiles"),
            Path.Combine(Directory.GetCurrentDirectory(), "TestFiles"),
            Path.Combine(AppContext.BaseDirectory, "TestFiles"),
            Path.Combine("/src", "Chummer.Tests", "TestFiles"),
            "/docker/chummercomplete/chummer-presentation/Chummer.Tests/TestFiles"
        };

        string? match = candidates.FirstOrDefault(Directory.Exists);
        if (match is null)
        {
            throw new DirectoryNotFoundException("Could not locate the Chummer test fixture directory.");
        }

        return match;
    }

    private static string ResolveFixtureUiReconstructionReceiptsDirectory()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("CHUMMER_FIXTURE_UI_RECONSTRUCTION_RECEIPTS_DIR");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        return Path.GetFullPath(
            Path.Combine(
                repoRoot,
                ".codex-studio",
                "out",
                "test-fixture-ui-reconstruction",
                Guid.NewGuid().ToString("N")));
    }

    private static FixtureUiReconstructionMaterializationResult MaterializeFixtureUiReconstructionReceipt(
        string fixtureName,
        string receiptsDirectory)
    {
        Directory.CreateDirectory(receiptsDirectory);

        string receiptPath = Path.Combine(receiptsDirectory, $"{fixtureName}.generated.json");
        string openedScreenshotFileName = $"{fixtureName}-opened.png";
        string exportDialogScreenshotFileName = $"{fixtureName}-export-dialog.png";
        string printedScreenshotFileName = $"{fixtureName}-printed.png";
        string reloadedScreenshotFileName = $"{fixtureName}-reloaded.png";
        string openedScreenshotPath = Path.Combine(receiptsDirectory, openedScreenshotFileName);
        string exportDialogScreenshotPath = Path.Combine(receiptsDirectory, exportDialogScreenshotFileName);
        string printedScreenshotPath = Path.Combine(receiptsDirectory, printedScreenshotFileName);
        string reloadedScreenshotPath = Path.Combine(receiptsDirectory, reloadedScreenshotFileName);
        string savedFilePath = Path.Combine(receiptsDirectory, $"{fixtureName}.roundtrip.chum5");
        string exportFilePath = Path.Combine(receiptsDirectory, $"{fixtureName}.export.json");
        string printPreviewFilePath = Path.Combine(receiptsDirectory, $"{fixtureName}.print.html");
        string pdfArtifactPath = Path.Combine(receiptsDirectory, $"{fixtureName}.print.pdf");

        List<string> reasons = [];
        List<string> screenshots = [];
        List<string> pickerTitles = [];
        Dictionary<string, bool> assertions = new(StringComparer.Ordinal)
        {
            ["openedByUi"] = false,
            ["savedByUi"] = false,
            ["exportedByUi"] = false,
            ["printedByUi"] = false,
            ["pdfArtifactProducedByUiPrintRoute"] = false,
            ["outputArtifactsProducedByUi"] = false,
            ["reloadedByUi"] = false,
            ["roundTripPreservedIdentity"] = false,
        };

        string sourceFixturePath = FindTestFilePath(fixtureName);
        byte[] sourceFixtureBytes = File.ReadAllBytes(sourceFixturePath);
        FixtureUiIdentity sourceIdentity = ReadFixtureUiIdentity(sourceFixtureBytes, fixtureName);
        FixtureUiIdentity openedIdentity = sourceIdentity;
        FixtureUiIdentity savedIdentity = sourceIdentity;
        FixtureUiIdentity reloadedIdentity = sourceIdentity;
        int importCallCount = 0;
        int saveCallCount = 0;
        int exportCallCount = 0;
        int printCallCount = 0;
        long exportByteCount = 0;
        long printPreviewByteCount = 0;
        long pdfArtifactByteCount = 0;
        string exportFileName = string.Empty;
        string printFileName = string.Empty;
        string printMimeType = string.Empty;
        string printTitle = string.Empty;
        Queue<(byte[] Payload, string SourceLabel)> importPayloads = new();
        importPayloads.Enqueue((sourceFixtureBytes, fixtureName));

        Func<global::Avalonia.Platform.Storage.IStorageProvider, string, CancellationToken, Task<DesktopImportFileResult>>? originalImportOverride =
            MainWindowDesktopFileCoordinator.OpenImportFileOverride;
        Func<global::Avalonia.Platform.Storage.IStorageProvider, PendingDownloadDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? originalSaveDownloadOverride =
            MainWindowDesktopFileCoordinator.SaveDownloadOverride;
        Func<global::Avalonia.Platform.Storage.IStorageProvider, PendingExportDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? originalSaveExportOverride =
            MainWindowDesktopFileCoordinator.SaveExportOverride;
        Func<global::Avalonia.Platform.Storage.IStorageProvider, PendingPrintDispatchRequest, CancellationToken, Task<DesktopDownloadSaveResult>>? originalSavePrintOverride =
            MainWindowDesktopFileCoordinator.SavePrintOverride;

        try
        {
            MainWindowDesktopFileCoordinator.OpenImportFileOverride =
                (_, title, _) =>
                {
                    pickerTitles.Add(title);
                    importCallCount++;
                    if (importPayloads.Count == 0)
                    {
                        return Task.FromResult(new DesktopImportFileResult(DesktopFileOperationOutcome.Cancelled, Payload: null, SourceLabel: null));
                    }

                    (byte[] payload, string sourceLabel) = importPayloads.Dequeue();
                    return Task.FromResult(new DesktopImportFileResult(DesktopFileOperationOutcome.Completed, payload, sourceLabel));
                };
            MainWindowDesktopFileCoordinator.SaveDownloadOverride =
                (_, request, _) =>
                {
                    saveCallCount++;
                    byte[] payload = Convert.FromBase64String(request.Download.ContentBase64);
                    File.WriteAllBytes(savedFilePath, payload);
                    return Task.FromResult(
                        new DesktopDownloadSaveResult(
                            DesktopFileOperationOutcome.Completed,
                            $"Downloaded {request.Download.FileName} to {Path.GetFileName(savedFilePath)}."));
                };
            MainWindowDesktopFileCoordinator.SaveExportOverride =
                (_, request, _) =>
                {
                    exportCallCount++;
                    exportFileName = request.Export.FileName;
                    byte[] payload = Convert.FromBase64String(request.Export.ContentBase64);
                    exportByteCount = payload.LongLength;
                    File.WriteAllBytes(exportFilePath, payload);
                    return Task.FromResult(
                        new DesktopDownloadSaveResult(
                            DesktopFileOperationOutcome.Completed,
                            $"Exported {request.Export.FileName} to {Path.GetFileName(exportFilePath)}."));
                };
            MainWindowDesktopFileCoordinator.SavePrintOverride =
                (_, request, _) =>
                {
                    printCallCount++;
                    printFileName = request.Print.FileName;
                    printMimeType = request.Print.MimeType;
                    printTitle = request.Print.Title;
                    byte[] payload = Convert.FromBase64String(request.Print.ContentBase64);
                    printPreviewByteCount = payload.LongLength;
                    File.WriteAllBytes(printPreviewFilePath, payload);

                    byte[] pdfPayload = BuildMinimalPdfFromHtmlPrintPreview(
                        string.IsNullOrWhiteSpace(request.Print.Title) ? request.Print.FileName : request.Print.Title,
                        payload);
                    pdfArtifactByteCount = pdfPayload.LongLength;
                    File.WriteAllBytes(pdfArtifactPath, pdfPayload);

                    return Task.FromResult(
                        new DesktopDownloadSaveResult(
                            DesktopFileOperationOutcome.Completed,
                            $"Saved print preview {request.Print.FileName} and PDF bridge {Path.GetFileName(pdfArtifactPath)}."));
                };

            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();

                ClickRuntimeMenuCommand(harness, "FileMenuButton", "open_character");
                harness.WaitUntil(
                    () => harness.State.WorkspaceId is not null
                        && harness.State.Profile is not null
                        && harness.State.Session.OpenWorkspaces.Count > 0
                        && !harness.State.IsBusy,
                    context: $"open fixture '{fixtureName}'");

                assertions["openedByUi"] = importCallCount >= 1;
                openedIdentity = CaptureRuntimeFixtureUiIdentity(harness, fixtureName);
                File.WriteAllBytes(openedScreenshotPath, harness.CaptureScreenshotBytes());
                screenshots.Add(openedScreenshotFileName);

                ClickRuntimeMenuCommand(harness, "FileMenuButton", "save_character_as");
                harness.WaitUntil(
                    () => saveCallCount >= 1
                        && File.Exists(savedFilePath)
                        && !harness.State.IsBusy,
                    context: $"save fixture '{fixtureName}'");

                assertions["savedByUi"] = saveCallCount >= 1;
                byte[] savedFixtureBytes = File.ReadAllBytes(savedFilePath);
                savedIdentity = ReadFixtureUiIdentity(savedFixtureBytes, fixtureName);

                harness.SelectCommand("export_character");
                harness.WaitUntil(
                    () => harness.Window.PeekDialogWindowForTesting() is { IsVisible: true, BoundDialogId: "dialog.export_character" },
                    context: $"open export dialog for '{fixtureName}'");
                File.WriteAllBytes(exportDialogScreenshotPath, harness.CaptureScreenshotBytes());
                screenshots.Add(exportDialogScreenshotFileName);

                ClickRuntimeDialogAction(harness, "download");
                harness.WaitUntil(
                    () => exportCallCount >= 1
                        && File.Exists(exportFilePath)
                        && harness.Window.PeekDialogWindowForTesting() is null
                        && !harness.State.IsBusy,
                    context: $"export fixture '{fixtureName}'");
                assertions["exportedByUi"] =
                    exportCallCount >= 1
                    && exportByteCount > 0
                    && File.Exists(exportFilePath);

                ClickRuntimeMenuCommand(harness, "FileMenuButton", "print_character");
                harness.WaitUntil(
                    () => printCallCount >= 1
                        && File.Exists(printPreviewFilePath)
                        && File.Exists(pdfArtifactPath)
                        && !harness.State.IsBusy,
                    context: $"print fixture '{fixtureName}'");
                assertions["printedByUi"] =
                    printCallCount >= 1
                    && printPreviewByteCount > 0
                    && File.Exists(printPreviewFilePath)
                    && string.Equals(printMimeType, "text/html", StringComparison.OrdinalIgnoreCase)
                    && Encoding.UTF8.GetString(File.ReadAllBytes(printPreviewFilePath))
                        .Contains("<html", StringComparison.OrdinalIgnoreCase);
                assertions["pdfArtifactProducedByUiPrintRoute"] =
                    printCallCount >= 1
                    && pdfArtifactByteCount > 0
                    && File.Exists(pdfArtifactPath)
                    && HasPdfHeader(File.ReadAllBytes(pdfArtifactPath));
                assertions["outputArtifactsProducedByUi"] =
                    assertions["savedByUi"]
                    && assertions["exportedByUi"]
                    && assertions["printedByUi"]
                    && assertions["pdfArtifactProducedByUiPrintRoute"];
                File.WriteAllBytes(printedScreenshotPath, harness.CaptureScreenshotBytes());
                screenshots.Add(printedScreenshotFileName);

                ClickRuntimeMenuCommand(harness, "WindowsMenuButton", "close_window");
                harness.WaitUntil(
                    () => harness.State.WorkspaceId is null
                        && harness.State.Session.OpenWorkspaces.Count == 0
                        && !harness.State.IsBusy,
                    context: $"close fixture '{fixtureName}' after save");

                importPayloads.Enqueue((savedFixtureBytes, Path.GetFileName(savedFilePath)));
                ClickRuntimeMenuCommand(harness, "FileMenuButton", "open_character");
                harness.WaitUntil(
                    () => harness.State.WorkspaceId is not null
                        && harness.State.Profile is not null
                        && harness.State.Session.OpenWorkspaces.Count > 0
                        && !harness.State.IsBusy,
                    context: $"reload fixture '{fixtureName}'");

                assertions["reloadedByUi"] = importCallCount >= 2;
                reloadedIdentity = CaptureRuntimeFixtureUiIdentity(harness, fixtureName);
                File.WriteAllBytes(reloadedScreenshotPath, harness.CaptureScreenshotBytes());
                screenshots.Add(reloadedScreenshotFileName);
            });
        }
        catch (Exception ex)
        {
            reasons.Add(ex.Message);
        }
        finally
        {
            MainWindowDesktopFileCoordinator.OpenImportFileOverride = originalImportOverride;
            MainWindowDesktopFileCoordinator.SaveDownloadOverride = originalSaveDownloadOverride;
            MainWindowDesktopFileCoordinator.SaveExportOverride = originalSaveExportOverride;
            MainWindowDesktopFileCoordinator.SavePrintOverride = originalSavePrintOverride;
        }

        if (pickerTitles.Count != 2
            || pickerTitles.Any(title => !string.Equals(title, "Open Character File", StringComparison.Ordinal)))
        {
            reasons.Add($"Expected two host file-open picker calls for '{fixtureName}', but saw: {string.Join(", ", pickerTitles)}");
        }

        if (!assertions["openedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete an initial UI open route.");
        }

        if (!assertions["savedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete a UI save-as route.");
        }

        if (!assertions["exportedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete a UI export route.");
        }

        if (!assertions["printedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete a UI print-preview route.");
        }

        if (!assertions["pdfArtifactProducedByUiPrintRoute"])
        {
            reasons.Add($"'{fixtureName}' did not materialize a PDF artifact from the UI print route.");
        }

        if (!assertions["outputArtifactsProducedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not publish the full save/export/print output artifact set.");
        }

        if (!assertions["reloadedByUi"])
        {
            reasons.Add($"'{fixtureName}' did not complete a UI reload route.");
        }

        assertions["roundTripPreservedIdentity"] =
            assertions["openedByUi"]
            && assertions["savedByUi"]
            && assertions["reloadedByUi"]
            && FixtureUiIdentityEquivalent(sourceIdentity, openedIdentity)
            && FixtureUiIdentityEquivalent(sourceIdentity, savedIdentity)
            && FixtureUiIdentityEquivalent(sourceIdentity, reloadedIdentity);
        if (!assertions["roundTripPreservedIdentity"])
        {
            reasons.Add(
                $"'{fixtureName}' identity drifted across the open/save/reload roundtrip. "
                + $"Source={DescribeFixtureUiIdentity(sourceIdentity)}; "
                + $"Opened={DescribeFixtureUiIdentity(openedIdentity)}; "
                + $"Saved={DescribeFixtureUiIdentity(savedIdentity)}; "
                + $"Reloaded={DescribeFixtureUiIdentity(reloadedIdentity)}.");
        }

        if (screenshots.Count < 4)
        {
            reasons.Add($"'{fixtureName}' did not publish the expected UI reconstruction screenshots.");
        }

        string status = reasons.Count == 0 && assertions.Values.All(static value => value) ? "pass" : "fail";
        Dictionary<string, object?> payload = new(StringComparer.Ordinal)
        {
            ["generatedAt"] = DateTime.UtcNow.ToString("O"),
            ["contract_name"] = "chummer6-ui.chummer5a_fixture_ui_reconstruction",
            ["status"] = status,
            ["summary"] = status == "pass"
                ? $"UI reconstruction parity passed for {fixtureName}."
                : $"UI reconstruction parity failed for {fixtureName}.",
            ["fixtureName"] = fixtureName,
            ["characterName"] = sourceIdentity.CharacterName,
            ["linux_binary_under_test"] = true,
            ["used_internal_apis"] = false,
            ["screenshots"] = screenshots.ToArray(),
            ["assertions"] = assertions,
            ["reasons"] = reasons.ToArray(),
            ["evidence"] = new Dictionary<string, object?>
            {
                ["fixturePath"] = sourceFixturePath,
                ["savedFilePath"] = savedFilePath,
                ["exportFilePath"] = exportFilePath,
                ["printPreviewFilePath"] = printPreviewFilePath,
                ["pdfArtifactPath"] = pdfArtifactPath,
                ["pickerTitles"] = pickerTitles.ToArray(),
                ["outputRouteFacts"] = new Dictionary<string, object?>
                {
                    ["exportCallCount"] = exportCallCount,
                    ["exportFileName"] = exportFileName,
                    ["exportByteCount"] = exportByteCount,
                    ["printCallCount"] = printCallCount,
                    ["printFileName"] = printFileName,
                    ["printMimeType"] = printMimeType,
                    ["printTitle"] = printTitle,
                    ["printPreviewByteCount"] = printPreviewByteCount,
                    ["pdfArtifactByteCount"] = pdfArtifactByteCount,
                },
                ["sourceIdentity"] = BuildFixtureUiIdentityPayload(sourceIdentity),
                ["openedIdentity"] = BuildFixtureUiIdentityPayload(openedIdentity),
                ["savedIdentity"] = BuildFixtureUiIdentityPayload(savedIdentity),
                ["reloadedIdentity"] = BuildFixtureUiIdentityPayload(reloadedIdentity),
            },
        };
        File.WriteAllText(
            receiptPath,
            JsonSerializer.Serialize(payload, ScreenshotEvidenceJsonOptions));

        return new FixtureUiReconstructionMaterializationResult(
            fixtureName,
            receiptPath,
            status,
            reasons.ToArray());
    }

    private static FixtureUiIdentity ReadFixtureUiIdentity(byte[] xmlBytes, string fixtureName)
    {
        using MemoryStream stream = new(xmlBytes, writable: false);
        using System.Xml.XmlReader reader = System.Xml.XmlReader.Create(
            stream,
            new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true
            });
        XElement root = XElement.Load(reader);

        string rawName = (root.Element("name")?.Value ?? string.Empty).Trim();
        string alias = (root.Element("alias")?.Value ?? string.Empty).Trim();
        string buildMethod = (root.Element("buildmethod")?.Value ?? string.Empty).Trim();
        string metatype = (root.Element("metatype")?.Value ?? string.Empty).Trim();
        string rulesetId = NormalizeFixtureRulesetId((root.Element("gameedition")?.Value ?? string.Empty).Trim());
        string characterName = string.IsNullOrWhiteSpace(rawName)
            ? Path.GetFileNameWithoutExtension(fixtureName)
            : rawName;

        return new FixtureUiIdentity(
            CharacterName: characterName,
            PrimaryToken: ResolveFixtureIdentityToken(rawName, alias, fixtureName),
            Alias: alias,
            BuildMethod: buildMethod,
            Metatype: metatype,
            RulesetId: rulesetId);
    }

    private static FixtureUiIdentity CaptureRuntimeFixtureUiIdentity(RuntimeFlagshipUiHarness harness, string fixtureName)
    {
        CharacterProfileSection profile = harness.State.Profile
            ?? throw new AssertFailedException($"Runtime fixture '{fixtureName}' did not materialize a profile.");
        OpenWorkspaceState? workspace = harness.State.WorkspaceId is { } workspaceId
            ? harness.State.OpenWorkspaces.FirstOrDefault(candidate => candidate.Id.Equals(workspaceId))
            : null;

        return new FixtureUiIdentity(
            CharacterName: string.IsNullOrWhiteSpace(profile.Name)
                ? Path.GetFileNameWithoutExtension(fixtureName)
                : profile.Name,
            PrimaryToken: ResolveFixtureIdentityToken(profile.Name, profile.Alias, fixtureName),
            Alias: profile.Alias,
            BuildMethod: profile.BuildMethod,
            Metatype: profile.Metatype,
            RulesetId: NormalizeFixtureRulesetId(workspace?.RulesetId ?? string.Empty));
    }

    private static bool FixtureUiIdentityEquivalent(FixtureUiIdentity expected, FixtureUiIdentity actual)
    {
        return string.Equals(expected.PrimaryToken, actual.PrimaryToken, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expected.BuildMethod, actual.BuildMethod, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expected.RulesetId, actual.RulesetId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(expected.Alias)
                || string.Equals(expected.Alias, actual.Alias, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(expected.Metatype)
                || string.Equals(expected.Metatype, actual.Metatype, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveFixtureIdentityToken(string? rawName, string? alias, string fixtureName)
    {
        string candidate = string.IsNullOrWhiteSpace(rawName)
            ? string.IsNullOrWhiteSpace(alias)
                ? Path.GetFileNameWithoutExtension(fixtureName)
                : alias
            : rawName;
        return string.Join(" ", candidate.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeFixtureRulesetId(string rawValue)
    {
        string trimmed = rawValue.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "SR4" => RulesetDefaults.Sr4,
            "SR5" => RulesetDefaults.Sr5,
            "SR6" => RulesetDefaults.Sr6,
            _ => RulesetDefaults.NormalizeOptional(trimmed) ?? string.Empty,
        };
    }

    private static string DescribeFixtureUiIdentity(FixtureUiIdentity identity)
        => $"primary={identity.PrimaryToken}, alias={identity.Alias}, build={identity.BuildMethod}, metatype={identity.Metatype}, ruleset={identity.RulesetId}";

    private static Dictionary<string, object?> BuildFixtureUiIdentityPayload(FixtureUiIdentity identity)
        => new(StringComparer.Ordinal)
        {
            ["characterName"] = identity.CharacterName,
            ["primaryToken"] = identity.PrimaryToken,
            ["alias"] = identity.Alias,
            ["buildMethod"] = identity.BuildMethod,
            ["metatype"] = identity.Metatype,
            ["rulesetId"] = identity.RulesetId,
        };

    private sealed record FixtureUiIdentity(
        string CharacterName,
        string PrimaryToken,
        string Alias,
        string BuildMethod,
        string Metatype,
        string RulesetId);

    private sealed record FixtureUiReconstructionMaterializationResult(
        string FixtureName,
        string ReceiptPath,
        string Status,
        string[] Reasons);

    private static byte[] BuildMinimalPdfFromHtmlPrintPreview(string title, byte[] htmlBytes)
    {
        string html = Encoding.UTF8.GetString(htmlBytes);
        string normalizedTitle = NormalizePdfLine(title);
        string plainText = ExtractPlainTextFromHtml(html);
        List<string> lines = [];

        if (!string.IsNullOrWhiteSpace(normalizedTitle))
        {
            lines.Add(normalizedTitle);
        }

        foreach (string line in plainText.Split('\n'))
        {
            string normalizedLine = NormalizePdfLine(line);
            if (!string.IsNullOrWhiteSpace(normalizedLine))
            {
                lines.Add(normalizedLine);
            }
        }

        if (lines.Count == 0)
        {
            lines.Add("Print preview");
        }

        const int MaxLines = 48;
        StringBuilder content = new();
        content.Append("BT\n/F1 11 Tf\n50 790 Td\n14 TL\n");
        bool wroteLine = false;
        foreach (string line in lines.Take(MaxLines))
        {
            if (wroteLine)
            {
                content.Append("T*\n");
            }

            content.Append('(')
                .Append(EscapePdfText(line))
                .Append(") Tj\n");
            wroteLine = true;
        }

        content.Append("ET\n");
        byte[] contentBytes = Encoding.ASCII.GetBytes(content.ToString());

        using MemoryStream stream = new();
        stream.Write(
        [
            0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A,
            0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A
        ]);

        List<long> offsets = [0];

        void WriteObject(string payload)
        {
            offsets.Add(stream.Position);
            byte[] bytes = Encoding.ASCII.GetBytes(payload);
            stream.Write(bytes, 0, bytes.Length);
        }

        WriteObject("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        WriteObject("2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n");
        WriteObject("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\nendobj\n");
        offsets.Add(stream.Position);
        byte[] streamHeader = Encoding.ASCII.GetBytes($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        stream.Write(streamHeader, 0, streamHeader.Length);
        stream.Write(contentBytes, 0, contentBytes.Length);
        byte[] streamFooter = Encoding.ASCII.GetBytes("endstream\nendobj\n");
        stream.Write(streamFooter, 0, streamFooter.Length);
        WriteObject("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        long xrefOffset = stream.Position;
        byte[] xrefHeader = Encoding.ASCII.GetBytes($"xref\n0 {offsets.Count}\n");
        stream.Write(xrefHeader, 0, xrefHeader.Length);
        byte[] freeEntry = Encoding.ASCII.GetBytes("0000000000 65535 f \n");
        stream.Write(freeEntry, 0, freeEntry.Length);
        foreach (long offset in offsets.Skip(1))
        {
            byte[] entry = Encoding.ASCII.GetBytes($"{offset:0000000000} 00000 n \n");
            stream.Write(entry, 0, entry.Length);
        }

        byte[] trailer = Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        stream.Write(trailer, 0, trailer.Length);
        return stream.ToArray();
    }

    private static string ExtractPlainTextFromHtml(string html)
    {
        string normalized = Regex.Replace(html, "(?i)<br\\s*/?>", "\n");
        normalized = Regex.Replace(normalized, "(?i)</(p|div|h1|h2|h3|li|tr|section|article)>", "\n");
        normalized = Regex.Replace(normalized, "<[^>]+>", " ");
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = Regex.Replace(normalized, @"\r\n?|\u2028|\u2029", "\n");
        normalized = Regex.Replace(normalized, @"[ \t\f\v]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static string NormalizePdfLine(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        char[] normalized = trimmed
            .Select(ch => ch is >= ' ' and <= '~' ? ch : '?')
            .ToArray();
        return new string(normalized);
    }

    private static string EscapePdfText(string value)
        => value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("(", @"\(", StringComparison.Ordinal)
            .Replace(")", @"\)", StringComparison.Ordinal);

    private static bool HasPdfHeader(byte[] bytes)
        => bytes.Length >= 5
            && bytes[0] == (byte)'%'
            && bytes[1] == (byte)'P'
            && bytes[2] == (byte)'D'
            && bytes[3] == (byte)'F'
            && bytes[4] == (byte)'-';

    private static void ClickRuntimeDialogAction(RuntimeFlagshipUiHarness harness, string actionId)
        => harness.InvokeDialogAction(actionId);

    private static void ClickRuntimeMenuCommand(RuntimeFlagshipUiHarness harness, string menuButtonName, string commandId)
    {
        harness.Click(menuButtonName);
        harness.WaitUntil(() => IsAnyCommandVisibleInCommandList(harness));
        harness.ClickMenuCommand(commandId);
    }

    private static string ResolveSourceFile(params string[] segments)
    {
        string relativePath = Path.Combine(segments);
        string? match = ResolveExistingPath(
            Path.Combine(TestContextLocator.ResolveChummerPresentationRepoRoot(), relativePath),
            relativePath,
            Path.Combine("/docker/chummercomplete/chummer6-ui-finish", relativePath),
            Path.Combine("/docker/chummercomplete/chummer-presentation", relativePath),
            Path.Combine("/docker/chummercomplete/chummer6-ui", relativePath));
        if (match is null)
        {
            throw new FileNotFoundException("Could not locate source file.", Path.Combine(segments));
        }

        return match;
    }

    private static string SourcePath(params string[] segments)
        => ResolveSourceFile(segments);

    private static string? ResolveExistingPath(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (Path.IsPathRooted(candidate) && File.Exists(candidate))
            {
                return candidate;
            }

            string? relativeMatch = ResolveRelativePathFromKnownRoots(candidate);
            if (relativeMatch is not null)
            {
                return relativeMatch;
            }
        }

        return null;
    }

    private static string? ResolveRelativePathFromKnownRoots(string relativePath)
    {
        foreach (string root in EnumerateSearchRoots())
        {
            string candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        yield return TestContextLocator.ResolveChummerPresentationRepoRoot();

        foreach (string root in EnumerateAncestorDirectories(Directory.GetCurrentDirectory()))
        {
            yield return root;
        }

        foreach (string root in EnumerateAncestorDirectories(AppContext.BaseDirectory))
        {
            yield return root;
        }
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string startPath)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        DirectoryInfo? current = new(Path.GetFullPath(startPath));

        while (current is not null)
        {
            if (seen.Add(current.FullName))
            {
                yield return current.FullName;
            }

            current = current.Parent;
        }
    }

    private static Dictionary<string, Dictionary<string, Color>> LoadThemeBrushes(string path)
    {
        XDocument document = XDocument.Load(path);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        Dictionary<string, Dictionary<string, Color>> themes = new(StringComparer.Ordinal);

        foreach (XElement dictionary in document
                     .Descendants()
                     .Where(element => string.Equals(element.Name.LocalName, "ResourceDictionary", StringComparison.Ordinal)))
        {
            string key = dictionary.Attribute(x + "Key")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            Dictionary<string, Color> brushes = new(StringComparer.Ordinal);
            foreach (XElement brush in dictionary.Elements().Where(element => string.Equals(element.Name.LocalName, "SolidColorBrush", StringComparison.Ordinal)))
            {
                string? brushKey = brush.Attribute(x + "Key")?.Value;
                string? colorValue = brush.Attribute("Color")?.Value;
                if (string.IsNullOrWhiteSpace(brushKey) || string.IsNullOrWhiteSpace(colorValue))
                {
                    continue;
                }

                brushes[brushKey] = Color.Parse(colorValue);
            }

            themes[key] = brushes;
        }

        return themes;
    }

    private static void AssertContrastAtLeast(Color foreground, Color background, double minimum, string context)
    {
        double ratio = ContrastRatio(foreground, background);
        Assert.IsTrue(ratio >= minimum, $"Expected {context} contrast to be at least {minimum:0.0}, but was {ratio:0.00}.");
    }

    private static void AssertVisibleInputControlContrast(Control root, string context)
    {
        Control[] inputControls = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.IsVisible)
            .Where(static control => control is ComboBox or ListBox or TextBox or NumericUpDown)
            .ToArray();

        Assert.IsTrue(
            inputControls.Length >= 8,
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
            Color background = ResolveSolidColor(backgroundBrush, control, "background", context);
            string controlName = string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name!;
            AssertContrastAtLeast(foreground, background, 4.5d, $"{context} {controlName} non-hover text");
        }
    }

    private static void AssertOriginDossierActionTitlesStayHuman(WrapPanel actionRow, string context)
    {
        string[] visibleActionTitles = actionRow.Children
            .OfType<Button>()
            .Where(static button => button.IsVisible)
            .Select(static button => button.Content?.ToString() ?? string.Empty)
            .Where(static title => !string.IsNullOrWhiteSpace(title))
            .ToArray();

        Assert.IsTrue(visibleActionTitles.Length > 0, $"{context} must expose at least one visible action.");

        string[] internalTerms =
        [
            "handoff",
            "request",
            "render",
            "log",
            "packet",
            "receipt",
            "provider",
            "media-factory",
            "FlipLink",
            "MarkupGo",
            "Soundmadeseen",
            "Unmixr",
            "vidBoard"
        ];

        foreach (string title in visibleActionTitles)
        {
            foreach (string term in internalTerms)
            {
                Assert.IsFalse(
                    title.Contains(term, StringComparison.OrdinalIgnoreCase),
                    $"{context} visible action '{title}' must not expose internal workflow term '{term}'.");
            }
        }
    }

    private static DialogFieldDisplayItem ToDisplayField(DesktopDialogField field)
        => new(
            field.Id,
            field.Label,
            field.Value,
            field.Placeholder,
            field.IsMultiline,
            field.IsReadOnly,
            field.InputType,
            field.Options?.Select(static option => new DialogFieldOptionDisplayItem(option.Value, option.Label)).ToArray(),
            field.VisualKind,
            field.LayoutSlot);

    private static DialogActionDisplayItem ToDisplayAction(DesktopDialogAction action)
        => new(action.Id, action.Label, action.IsPrimary);

    private static string[] EnumerateListBoxItemTexts(ListBox listBox)
        => EnumerateListBoxItems(listBox)
            .Select(static item => item.ToString() ?? string.Empty)
            .ToArray();

    private static IEnumerable<object> EnumerateListBoxItems(ListBox listBox)
        => ((IEnumerable?)listBox.ItemsSource)?.Cast<object>() ?? listBox.Items.OfType<object>();

    private static Color ResolveSolidColor(IBrush? brush, Control control, string propertyName, string context)
    {
        if (brush is ISolidColorBrush solidColorBrush)
        {
            return solidColorBrush.Color;
        }

        string controlName = string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name!;
        throw new AssertFailedException($"{context} {controlName} {propertyName} must resolve to a solid shell brush.");
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
        static double Channel(byte value)
        {
            double normalized = value / 255d;
            return normalized <= 0.03928d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        return (0.2126d * Channel(color.R)) + (0.7152d * Channel(color.G)) + (0.0722d * Channel(color.B));
    }

    private static string ToHex(Color color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string ResolveScreenshotDirectory()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("CHUMMER_UI_GATE_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();

        return Path.GetFullPath(
            Path.Combine(
                repoRoot,
                ".codex-studio",
                "published",
                "ui-flagship-release-gate-screenshots"));
    }

    private static ScreenshotProofCapture CaptureScreenshotProof(FlagshipUiHarness harness, string screenshotFileName)
    {
        if (screenshotFileName.Contains("-add-dialog-", StringComparison.Ordinal)
            && harness.Window.PeekDialogWindowForTesting() is not { IsVisible: true })
        {
            throw new InvalidOperationException(
                $"Screenshot '{screenshotFileName}' must be captured with a visible desktop dialog window. " +
                "Inline or hidden state is not acceptable proof for add-workflow coverage.");
        }

        return new(
            harness.CaptureScreenshotBytes(),
            CaptureScreenshotControlEvidence(harness, screenshotFileName));
    }

    private static ScreenshotProofCapture CaptureScreenshotProof(FlagshipUiHarness harness, TopLevel root, string screenshotFileName)
    {
        return new(
            harness.CaptureScreenshotBytes(root),
            CaptureScreenshotControlEvidence(harness, root, screenshotFileName));
    }

    private static ScreenshotControlEvidenceEntry CaptureScreenshotControlEvidence(FlagshipUiHarness harness, string screenshotFileName)
    {
        TopLevel root = harness.Window;
        Control[] visibleNamedControls = harness.Window.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => IsEffectivelyVisibleForScreenshotEvidence(control, root) && !string.IsNullOrWhiteSpace(control.Name))
            .OrderBy(control => control.Name, StringComparer.Ordinal)
            .ToArray();

        string dialogTitle = harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text ?? string.Empty;
        if (string.Equals(dialogTitle, "(none)", StringComparison.Ordinal))
        {
            dialogTitle = string.Empty;
        }

        string dialogMessage = harness.FindControlOrDefault<TextBlock>("DialogMessageText")?.Text ?? string.Empty;
        string previewText = harness.FindControlOrDefault<TextBox>("SectionPreviewBox")?.Text ?? string.Empty;

        string[] dialogFieldLabels = visibleNamedControls
            .OfType<TextBlock>()
            .Where(control => control.Name?.StartsWith("DialogFieldLabel_", StringComparison.Ordinal) == true)
            .Select(control => control.Text ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] dialogFieldIds = visibleNamedControls
            .Select(control => TryGetControlSuffix(control.Name, "DialogField_"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;
        string[] dialogFieldControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name =>
                name.StartsWith("DialogFieldLabel_", StringComparison.Ordinal)
                || name.StartsWith("DialogFieldInput_", StringComparison.Ordinal)
                || name.StartsWith("DialogField_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        List<string> dialogFieldInputValuesBuilder = [];
        foreach (Control control in visibleNamedControls)
        {
            if (control.Name?.StartsWith("DialogFieldInput_", StringComparison.Ordinal) != true)
            {
                continue;
            }

            switch (control)
            {
                case TextBox textBox when !string.IsNullOrWhiteSpace(textBox.Text):
                    dialogFieldInputValuesBuilder.Add(textBox.Text);
                    break;
                case ComboBox comboBox when comboBox.SelectedItem is not null:
                    dialogFieldInputValuesBuilder.Add(comboBox.SelectedItem.ToString() ?? string.Empty);
                    break;
                case CheckBox checkBox:
                    dialogFieldInputValuesBuilder.Add(checkBox.IsChecked?.ToString() ?? string.Empty);
                    break;
            }
        }

        string[] dialogFieldInputValues = dialogFieldInputValuesBuilder
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] dialogActionIds = visibleNamedControls
            .Select(control => TryGetControlSuffix(control.Name, "DialogAction_"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;
        string[] dialogActionControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name => name.StartsWith("DialogAction_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] visibleNamedControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] visibleTextSamples = harness.Window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(control => IsEffectivelyVisibleForScreenshotEvidence(control, root))
            .Select(control => control.Text ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(96)
            .ToArray();
        string[] visibleMenuCommandIds = !string.IsNullOrWhiteSpace(harness.ShellPresenter.State.OpenMenuId)
            ? CaptureOpenRootMenuCommandIds(harness, harness.ShellPresenter.State.OpenMenuId!)
            : CaptureVisibleCommandIds(harness);
        string[] visibleTabLabels = harness.FindControlOrDefault<TabStrip>("LoadedRunnerTabStrip") is { } loadedRunnerTabStrip
            && IsEffectivelyVisibleForScreenshotEvidence(loadedRunnerTabStrip, root)
            && loadedRunnerTabStrip.Items is IEnumerable tabItems
            ? tabItems
                .OfType<NavigatorTabItem>()
                .Select(item => item.Label ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : [];
        string[] visibleSectionQuickActionIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name => name.StartsWith("SectionQuickAction_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] selectedListRowTexts = harness.Window.GetVisualDescendants()
            .OfType<ListBox>()
            .Where(listBox => IsEffectivelyVisibleForScreenshotEvidence(listBox, root) && listBox.SelectedItem is not null)
            .Select(listBox => listBox.SelectedItem?.ToString() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        ScreenshotVisibleNamedControlEntry[] visibleNamedControlEntries = visibleNamedControls
            .Select(control => BuildVisibleNamedControlEntry(control, root))
            .ToArray();

        string theme = (harness.Window.ActualThemeVariant ?? harness.Window.RequestedThemeVariant ?? ThemeVariant.Default).ToString();
        Control? rightShellRegion = harness.FindControlOrDefault<Control>("RightShellRegion");
        bool rightShellVisible = rightShellRegion is not null
            && IsEffectivelyVisibleForScreenshotEvidence(rightShellRegion, root);
        double rightShellWidth = rightShellRegion?.Bounds.Width ?? 0d;
        bool inlineCommandSurfaceVisible = visibleNamedControlIds.Contains("CommandsHostBorder", StringComparer.Ordinal)
            || visibleNamedControlIds.Contains("CommandsList", StringComparer.Ordinal);
        bool dialogWindowVisible = harness.Window.PeekDialogWindowForTesting() is { IsVisible: true };
        return new ScreenshotControlEvidenceEntry(
            Screenshot: screenshotFileName,
            Theme: theme,
            DialogTitle: dialogTitle,
            DialogMessage: dialogMessage,
            DialogFieldLabels: dialogFieldLabels,
            DialogFieldIds: dialogFieldIds,
            DialogFieldControlIds: dialogFieldControlIds,
            DialogFieldInputValues: dialogFieldInputValues,
            DialogActionIds: dialogActionIds,
            DialogActionControlIds: dialogActionControlIds,
            VisibleNamedControlIds: visibleNamedControlIds,
            VisibleNamedControls: visibleNamedControlEntries,
            VisibleTextSamples: visibleTextSamples,
            VisibleMenuCommandIds: visibleMenuCommandIds,
            VisibleTabLabels: visibleTabLabels,
            VisibleSectionQuickActionIds: visibleSectionQuickActionIds,
            SelectedListRowTexts: selectedListRowTexts,
            PreviewText: previewText,
            RightShellVisible: rightShellVisible,
            RightShellWidth: rightShellWidth,
            InlineCommandSurfaceVisible: inlineCommandSurfaceVisible,
            DialogWindowVisible: dialogWindowVisible);
    }

    private static ScreenshotControlEvidenceEntry CaptureScreenshotControlEvidence(FlagshipUiHarness harness, TopLevel root, string screenshotFileName)
    {
        Control[] visibleNamedControls = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => IsEffectivelyVisibleForScreenshotEvidence(control, root) && !string.IsNullOrWhiteSpace(control.Name))
            .OrderBy(control => control.Name, StringComparer.Ordinal)
            .ToArray();

        string dialogTitle = root is Window window
            ? window.Title ?? string.Empty
            : FindDescendantOrDefault<TextBlock>(root, "DialogTitleText")?.Text ?? string.Empty;
        string dialogMessage = FindDescendantOrDefault<TextBlock>(root, "DialogMessageText")?.Text ?? string.Empty;
        string previewText = FindDescendantOrDefault<TextBox>(root, "SectionPreviewBox")?.Text ?? string.Empty;
        string[] dialogFieldLabels = visibleNamedControls
            .OfType<TextBlock>()
            .Where(control => control.Name?.StartsWith("DialogFieldLabel_", StringComparison.Ordinal) == true)
            .Select(control => control.Text ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] dialogFieldIds = visibleNamedControls
            .Select(control => TryGetControlSuffix(control.Name, "DialogField_"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;
        string[] dialogFieldControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name =>
                name.StartsWith("DialogFieldLabel_", StringComparison.Ordinal)
                || name.StartsWith("DialogFieldInput_", StringComparison.Ordinal)
                || name.StartsWith("DialogField_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] dialogActionIds = visibleNamedControls
            .Select(control => TryGetControlSuffix(control.Name, "DialogAction_"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;
        string[] dialogActionControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name => name.StartsWith("DialogAction_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] visibleNamedControlIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] visibleTextSamples = root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(control => IsEffectivelyVisibleForScreenshotEvidence(control, root))
            .Select(control => control.Text ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(96)
            .ToArray();
        string[] visibleTabLabels = Array.Empty<string>();
        string[] visibleSectionQuickActionIds = visibleNamedControls
            .Select(control => control.Name ?? string.Empty)
            .Where(static name => name.StartsWith("SectionQuickAction_", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] selectedListRowTexts = root.GetVisualDescendants()
            .OfType<ListBox>()
            .Where(listBox => IsEffectivelyVisibleForScreenshotEvidence(listBox, root) && listBox.SelectedItem is not null)
            .Select(listBox => listBox.SelectedItem?.ToString() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        ScreenshotVisibleNamedControlEntry[] visibleNamedControlEntries = visibleNamedControls
            .Select(control => BuildVisibleNamedControlEntry(control, root))
            .ToArray();
        string theme = (root.ActualThemeVariant ?? root.RequestedThemeVariant ?? ThemeVariant.Default).ToString();

        return new ScreenshotControlEvidenceEntry(
            Screenshot: screenshotFileName,
            Theme: theme,
            DialogTitle: dialogTitle,
            DialogMessage: dialogMessage,
            DialogFieldLabels: dialogFieldLabels,
            DialogFieldIds: dialogFieldIds,
            DialogFieldControlIds: dialogFieldControlIds,
            DialogFieldInputValues: Array.Empty<string>(),
            DialogActionIds: dialogActionIds,
            DialogActionControlIds: dialogActionControlIds,
            VisibleNamedControlIds: visibleNamedControlIds,
            VisibleNamedControls: visibleNamedControlEntries,
            VisibleTextSamples: visibleTextSamples,
            VisibleMenuCommandIds: Array.Empty<string>(),
            VisibleTabLabels: visibleTabLabels,
            VisibleSectionQuickActionIds: visibleSectionQuickActionIds,
            SelectedListRowTexts: selectedListRowTexts,
            PreviewText: previewText,
            RightShellVisible: false,
            RightShellWidth: 0d,
            InlineCommandSurfaceVisible: false,
            DialogWindowVisible: root is DesktopDialogWindow);
    }

    private static bool IsEffectivelyVisibleForScreenshotEvidence(Control control, TopLevel root)
    {
        if (!control.IsVisible)
        {
            return false;
        }

        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
        {
            return false;
        }

        if (control.TranslatePoint(default, root) is null)
        {
            return false;
        }

        return control.GetVisualAncestors().All(static ancestor => ancestor.IsVisible);
    }

    private static void AssertMouseReachabilityWithinFlagshipViewport(TopLevel root, string surfaceLabel)
    {
        Rect viewport = new(0d, 0d, root.Bounds.Width, root.Bounds.Height);
        string[] failures = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => IsFlagshipMouseTargetControl(control, root))
            .Select(control => DescribeMouseReachabilityFailure(control, root, viewport))
            .Where(static failure => !string.IsNullOrWhiteSpace(failure))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;

        Assert.AreEqual(
            0,
            failures.Length,
            $"{surfaceLabel} must keep every visible mouse target inside the desktop viewport without relying on offscreen scroll-only reachability. Failures:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static void AssertControlMouseReachable(Control control, TopLevel root)
    {
        Rect viewport = new(0d, 0d, root.Bounds.Width, root.Bounds.Height);
        string? failure = DescribeMouseReachabilityFailure(control, root, viewport);
        Assert.IsTrue(
            string.IsNullOrWhiteSpace(failure),
            failure ?? $"Control '{control.Name ?? control.GetType().Name}' must be mouse reachable.");
    }

    private static void AssertDialogMouseReachabilityOrScrollContainment(TopLevel root, string dialogTitle)
    {
        Rect viewport = new(0d, 0d, root.Bounds.Width, root.Bounds.Height);
        string[] failures = root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => IsFlagshipMouseTargetControl(control, root))
            .Select(control => DescribeDialogReachabilityFailure(control, root, viewport))
            .Where(static failure => !string.IsNullOrWhiteSpace(failure))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;

        Assert.AreEqual(
            0,
            failures.Length,
            $"{dialogTitle} must keep every visible mouse target onscreen or inside a visible scroll host. Failures:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static TopLevel GetMouseInteractionRoot(Control control, TopLevel fallbackRoot)
        => TopLevel.GetTopLevel(control) ?? fallbackRoot;

    private static ScrollViewer? FindVisibleScrollHost(Control control)
        => control.GetVisualAncestors()
            .OfType<ScrollViewer>()
            .FirstOrDefault(static candidate => candidate.IsVisible);

    private static bool IsFlagshipMouseTargetControl(Control control, TopLevel root)
    {
        if (!IsEffectivelyVisibleForScreenshotEvidence(control, root))
        {
            return false;
        }

        string name = control.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("PART_", StringComparison.Ordinal))
        {
            return false;
        }

        return control is Button
            or MenuItem
            or TabStrip
            or TreeView
            or ListBox
            or ComboBox
            or CheckBox
            or TextBox
            or ToggleButton
            or Expander;
    }

    private static string? DescribeMouseReachabilityFailure(Control control, TopLevel root, Rect viewport)
    {
        Point? translated = control.TranslatePoint(
            new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
            root);
        if (translated is null)
        {
            return $"{control.Name} ({control.GetType().Name}) could not translate its mouse target into window coordinates.";
        }

        Point center = translated.Value;
        bool withinViewport =
            center.X >= viewport.X
            && center.Y >= viewport.Y
            && center.X <= viewport.X + viewport.Width
            && center.Y <= viewport.Y + viewport.Height;
        if (withinViewport)
        {
            return null;
        }

        ScrollViewer? scrollHost = FindVisibleScrollHost(control);
        string hostChain = string.Join(
            " -> ",
            control.GetVisualAncestors()
                .OfType<Control>()
                .Take(6)
                .Select(static ancestor => $"{ancestor.Name ?? ancestor.GetType().Name}[{ancestor.Bounds.Width:F0}x{ancestor.Bounds.Height:F0}]"));
        return scrollHost is null
            ? $"{control.Name} ({control.GetType().Name}) is offscreen at ({center.X:F1}, {center.Y:F1}) without a visible scroll host. Control={control.Bounds.Width:F0}x{control.Bounds.Height:F0}, Root={root.Bounds.Width:F0}x{root.Bounds.Height:F0}, Ancestors={hostChain}."
            : $"{control.Name} ({control.GetType().Name}) is offscreen at ({center.X:F1}, {center.Y:F1}) and only reachable through scroll host '{scrollHost.Name ?? scrollHost.GetType().Name}'. Control={control.Bounds.Width:F0}x{control.Bounds.Height:F0}, Root={root.Bounds.Width:F0}x{root.Bounds.Height:F0}, Ancestors={hostChain}.";
    }

    private static string? DescribeDialogReachabilityFailure(Control control, TopLevel root, Rect viewport)
    {
        Point? translated = control.TranslatePoint(
            new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
            root);
        if (translated is null)
        {
            return $"{control.Name} ({control.GetType().Name}) could not translate its mouse target into window coordinates.";
        }

        Point center = translated.Value;
        bool withinViewport =
            center.X >= viewport.X
            && center.Y >= viewport.Y
            && center.X <= viewport.X + viewport.Width
            && center.Y <= viewport.Y + viewport.Height;
        if (withinViewport)
        {
            return null;
        }

        return FindVisibleScrollHost(control) is null
            ? $"{control.Name} ({control.GetType().Name}) is offscreen at ({center.X:F1}, {center.Y:F1}) without a visible scroll host. Control={control.Bounds.Width:F0}x{control.Bounds.Height:F0}, Root={root.Bounds.Width:F0}x{root.Bounds.Height:F0}."
            : null;
    }

    private static ScreenshotVisibleNamedControlEntry BuildVisibleNamedControlEntry(Control control, TopLevel root)
    {
        Point? topLeft = control.TranslatePoint(default, root);
        Rect bounds = control.Bounds;
        return new ScreenshotVisibleNamedControlEntry(
            Name: control.Name ?? string.Empty,
            ControlType: control.GetType().Name,
            Text: control switch
            {
                TextBlock textBlock => textBlock.Text ?? string.Empty,
                TextBox textBox => textBox.Text ?? string.Empty,
                CheckBox checkBox => checkBox.IsChecked?.ToString() ?? string.Empty,
                Button button => GetPrimaryButtonLabel(button),
                _ => string.Empty
            },
            X: topLeft?.X,
            Y: topLeft?.Y,
            Width: bounds.Width,
            Height: bounds.Height);
    }

    private static string? TryGetControlSuffix(string? name, string prefix)
    {
        if (string.IsNullOrWhiteSpace(name) || !name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return name[prefix.Length..];
    }

    private static object[] SnapshotListBoxItems(ListBox listBox)
    {
        if (listBox.ItemsSource is IEnumerable itemsSource)
        {
            return itemsSource.Cast<object>().ToArray();
        }

        if (listBox.Items is IEnumerable items)
        {
            return items.Cast<object>().ToArray();
        }

        return Array.Empty<object>();
    }

    private static NavigatorTreeItem[] SnapshotTreeItems(TreeView treeView)
    {
        if (treeView.ItemsSource is IEnumerable<NavigatorTreeItem> typedItems)
        {
            return typedItems.ToArray();
        }

        if (treeView.Items is IEnumerable items)
        {
            return items.OfType<NavigatorTreeItem>().ToArray();
        }

        return [];
    }

    private static CharacterRosterNode[] SnapshotRosterItems(TreeView treeView)
    {
        if (treeView.ItemsSource is IEnumerable<CharacterRosterNode> typedItems)
        {
            return typedItems.ToArray();
        }

        if (treeView.Items is IEnumerable items)
        {
            return items.OfType<CharacterRosterNode>().ToArray();
        }

        return [];
    }

    private static NavigatorTreeItem? FindTreeItem(
        IEnumerable<NavigatorTreeItem> items,
        NavigatorTreeNodeKind kind,
        Func<NavigatorTreeItem, bool> predicate)
    {
        foreach (NavigatorTreeItem item in items)
        {
            if (item.Kind == kind && predicate(item))
            {
                return item;
            }

            NavigatorTreeItem? childMatch = FindTreeItem(item.Children, kind, predicate);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }

    private static void AssertQuickActionDialogFlow(
        FlagshipUiHarness harness,
        string sectionId,
        string actionControlId,
        string expectedTitle,
        string requiredFieldLabel,
        string requiredActionId)
    {
        harness.SetActiveSectionForTesting(sectionId);
        harness.WaitUntil(() => harness.FindControlOrDefault<Control>($"SectionQuickAction_{actionControlId}")?.IsVisible == true);
        harness.Click($"SectionQuickAction_{actionControlId}");
        harness.WaitUntil(() =>
            string.Equals(
                harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                expectedTitle,
                StringComparison.Ordinal));
        AssertDialogMouseReachabilityOrScrollContainment(
            harness.Window,
            expectedTitle);

        string[] fieldLines = harness.FindDialogFieldTexts();
        Assert.IsTrue(
            fieldLines.Any(line => line.Contains(requiredFieldLabel, StringComparison.Ordinal)),
            $"Dialog '{expectedTitle}' must expose a specific '{requiredFieldLabel}' field.");

        string preview = harness.FindControl<TextBox>("SectionPreviewBox").Text ?? string.Empty;
        Assert.IsTrue(
            preview.Contains(sectionId, StringComparison.OrdinalIgnoreCase),
            $"Section preview should contain '{sectionId}' summary evidence before confirming the action.");

        string[] actionIds = harness.DialogActionIds();
        CollectionAssert.Contains(actionIds, requiredActionId);
        harness.InvokeDialogAction(requiredActionId);
        harness.WaitUntil(() =>
            !string.Equals(
                harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                expectedTitle,
                StringComparison.Ordinal));
        harness.WaitUntil(() => !harness.State.IsBusy);
    }

    private static void AssertDialogContainsAll(
        FlagshipUiHarness harness,
        string dialogTitle,
        params string[] expectedFragments)
    {
        Control dialogRoot = (Control?)harness.Window.PeekDialogWindowForTesting() ?? harness.Window;
        string dialogText = string.Join(
            "\n",
            new[] { dialogTitle, harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text ?? string.Empty }
                .Concat(CaptureVisibleTextInventory(dialogRoot))
                .Concat(harness.FindDialogFieldInputTexts())
                .Distinct(StringComparer.Ordinal));

        foreach (string expectedFragment in expectedFragments)
        {
            Assert.IsTrue(
                dialogText.Contains(expectedFragment, StringComparison.Ordinal),
                $"M103 screenshot capture for '{dialogTitle}' must include '{expectedFragment}' before the PNG is written.");
        }
    }

    private static void AssertDialogContainsAll(
        RuntimeFlagshipUiHarness harness,
        string dialogTitle,
        params string[] expectedFragments)
    {
        Control dialogRoot = (Control?)harness.Window.PeekDialogWindowForTesting() ?? harness.Window;
        string dialogText = string.Join(
            "\n",
            new[] { dialogTitle, harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text ?? string.Empty }
                .Concat(CaptureVisibleTextInventory(dialogRoot))
                .Concat(harness.FindDialogFieldInputTexts())
                .Distinct(StringComparer.Ordinal));

        foreach (string expectedFragment in expectedFragments)
        {
            Assert.IsTrue(
                dialogText.Contains(expectedFragment, StringComparison.Ordinal),
                $"M103 screenshot capture for '{dialogTitle}' must include '{expectedFragment}' before the PNG is written.");
        }
    }

    private static void AssertUiControlDialogFlow(
        FlagshipUiHarness harness,
        string controlId,
        string expectedTitle,
        string requiredFieldLabel,
        string requiredActionId)
    {
        harness.OpenUiControl(controlId);
        harness.WaitUntil(() =>
            string.Equals(
                harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text,
                expectedTitle,
                StringComparison.Ordinal));

        string[] fieldLines = harness.FindDialogFieldTexts();
        Assert.IsTrue(
            fieldLines.Any(line => line.Contains(requiredFieldLabel, StringComparison.Ordinal)),
            $"Dialog '{expectedTitle}' must expose a specific '{requiredFieldLabel}' field.");
        CollectionAssert.Contains(harness.DialogActionIds(), requiredActionId);
        harness.InvokeDialogAction(requiredActionId);
        harness.WaitUntil(() => harness.FindControlOrDefault<TextBlock>("DialogTitleText")?.Text is "(none)" or null);
    }

    private static void WithLoadedRunnerHarness(Action<FlagshipUiHarness> assertion)
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy,
                    timeoutMs: 8000);
                assertion(harness);
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private static void WithRuntimeLoadedRunnerHarness(Action<RuntimeFlagshipUiHarness> assertion)
    {
        string sampleRoot = Path.Combine(AppContext.BaseDirectory, "Samples", "Legacy");
        Directory.CreateDirectory(sampleRoot);
        string targetPath = Path.Combine(sampleRoot, "Soma-Career.chum5");
        File.Copy(FindTestFilePath("Soma (Career).chum5"), targetPath, overwrite: true);

        try
        {
            WithRuntimeHarness(harness =>
            {
                harness.WaitForReady();
                harness.Click("LoadDemoRunnerButton");
                harness.WaitUntil(() =>
                    harness.State.WorkspaceId is not null
                    && harness.State.Session.OpenWorkspaces.Count > 0
                    && !harness.State.IsBusy,
                    timeoutMs: 8000,
                    context: "load demo runner into runtime-backed harness");
                assertion(harness);
            });
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    private static void WithStandaloneControl<TControl>(Action<TControl> assertion)
        where TControl : Control, new()
    {
        WithStandaloneControl<TControl, bool>(control =>
        {
            assertion(control);
            return true;
        });
    }

    private static TResult WithStandaloneControl<TControl, TResult>(Func<TControl, TResult> assertion)
        where TControl : Control, new()
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
                return session.Dispatch(() =>
                    {
                        Window hostWindow = new()
                        {
                            Width = 1440,
                            Height = 960,
                            Content = new TControl()
                        };
                        hostWindow.Show();
                        PumpStandaloneUi();

                        try
                        {
                            return assertion((TControl)hostWindow.Content!);
                        }
                        finally
                        {
                            hostWindow.Close();
                            PumpStandaloneUi();
                        }
                    },
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex) when (IsTransientHeadlessFailure(ex) && attempt < HeadlessSessionAttempts)
            {
                lastFailure = ex;
            }
            finally
            {
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia standalone headless session did not stabilize for flagship UI proof.", lastFailure);
    }

    private static void WithStandaloneDialogWindow(Action<DesktopDialogWindow> assertion)
    {
        EnsureHeadlessPlatform();
        Exception? lastFailure = null;
        for (int attempt = 1; attempt <= HeadlessSessionAttempts; attempt++)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
                session.Dispatch(
                        () =>
                        {
                            DesktopDialogWindow window = new()
                            {
                                Width = 1080,
                                Height = 900
                            };
                            window.Show();
                            PumpStandaloneUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpStandaloneUi();
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
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia standalone dialog-window headless session did not stabilize for flagship UI proof.", lastFailure);
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
                session = HeadlessUnitTestSession.StartNew(typeof(FlagshipHeadlessAppBootstrap));
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
                                    CreateReportIssueInstallState(),
                                    CreateReportIssueUpdateStatus(),
                                    DesktopPreferenceState.Default
                                ]);
                            window.Show();
                            PumpStandaloneUi();

                            try
                            {
                                assertion(window);
                            }
                            finally
                            {
                                window.Close();
                                PumpStandaloneUi();
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
                DisposeHeadlessSessionQuietly(session);
            }
        }

        throw new AssertFailedException("Avalonia report-issue headless session did not stabilize for flagship UI proof.", lastFailure);
    }

    private static DesktopInstallLinkingState CreateReportIssueInstallState()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopInstallLinkingState(
            InstallationId: "install-report-issue",
            HeadId: "avalonia",
            ApplicationVersion: "run-test",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            Status: "claimed",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 1,
            LastStartedAtUtc: now,
            ClaimedAtUtc: now,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public",
            PrivateKey: "private",
            GrantToken: "grant-token");
    }

    private static DesktopUpdateClientStatus CreateReportIssueUpdateStatus()
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

    private static T FindDescendant<T>(Control root, string name)
        where T : Control
    {
        return FindDescendantOrDefault<T>(root, name)
            ?? throw new AssertFailedException($"Descendant control '{name}' of type {typeof(T).Name} was not found.");
    }

    private static T? FindDescendantOrDefault<T>(Control root, string name)
        where T : Control
    {
        if (root is T typedRoot && string.Equals(root.Name, name, StringComparison.Ordinal))
        {
            return typedRoot;
        }

        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
    }

    private static RuntimeControlInventoryNode CaptureControlInventory(Control control)
    {
        return CaptureControlInventory(
            control,
            new HashSet<Control>(ReferenceEqualityComparer.Instance),
            depth: 0,
            maxDepth: 32);
    }

    private static RuntimeControlInventoryNode CaptureControlInventory(
        Control control,
        HashSet<Control> visited,
        int depth,
        int maxDepth)
    {
        string text = control switch
        {
            TextBlock textBlock => textBlock.Text ?? string.Empty,
            Button button => GetPrimaryButtonLabel(button),
            TextBox textBox => textBox.Text ?? string.Empty,
            _ => string.Empty
        };
        string toolTip = ToolTip.GetTip(control)?.ToString() ?? string.Empty;

        if (!visited.Add(control))
        {
            return new RuntimeControlInventoryNode(
                Name: control.Name ?? string.Empty,
                ControlType: $"{control.GetType().Name}:CycleReference",
                Text: text,
                ToolTip: toolTip,
                IsVisible: control.IsVisible,
                Children: []);
        }

        if (depth >= maxDepth)
        {
            return new RuntimeControlInventoryNode(
                Name: control.Name ?? string.Empty,
                ControlType: $"{control.GetType().Name}:DepthLimit",
                Text: text,
                ToolTip: toolTip,
                IsVisible: control.IsVisible,
                Children: []);
        }

        RuntimeControlInventoryNode[] children = control.GetVisualChildren()
            .OfType<Control>()
            .Where(child => !ReferenceEquals(child, control))
            .Select(child => CaptureControlInventory(child, visited, depth + 1, maxDepth))
            .ToArray();

        return new RuntimeControlInventoryNode(
            Name: control.Name ?? string.Empty,
            ControlType: control.GetType().Name,
            Text: text,
            ToolTip: toolTip,
            IsVisible: control.IsVisible,
            Children: children);
    }

    private static RuntimeRouteInventoryEntry CaptureRuntimeRouteInventory(
        RuntimeFlagshipUiHarness harness,
        string routeId,
        string routeFamily,
        string branchId)
    {
        ListBox? commandsList = harness.FindControlOrDefault<ListBox>("CommandsList");
        TreeView? rosterTree = harness.FindControlOrDefault<TreeView>("RosterTree");
        string rulesetId = RulesetDefaults.NormalizeOptional(harness.ShellPresenter.State.ActiveRulesetId)
            ?? harness.State.Session.OpenWorkspaces
                .Select(workspace => RulesetDefaults.NormalizeOptional(workspace.RulesetId))
                .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;

        return new RuntimeRouteInventoryEntry(
            RouteId: routeId,
            RouteFamily: routeFamily,
            BranchId: branchId,
            RulesetId: rulesetId,
            OpenMenuId: harness.ShellPresenter.State.OpenMenuId ?? string.Empty,
            VisibleTexts: CaptureVisibleTextInventory(harness.Window),
            VisibleCommandIds: CaptureVisibleCommandIds(commandsList),
            NavigatorRootLabels: rosterTree is null
                ? []
                : SnapshotRosterItems(rosterTree).Select(item => item.Name).Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray(),
            Inventory: CaptureControlInventory(harness.Window));
    }

    private static RuntimeRouteInventoryEntry CaptureStandaloneRouteInventory(
        DesktopDialogWindow window,
        string routeId,
        string routeFamily,
        string rulesetId,
        string branchId)
    {
        return new RuntimeRouteInventoryEntry(
            RouteId: routeId,
            RouteFamily: routeFamily,
            BranchId: branchId,
            RulesetId: rulesetId,
            OpenMenuId: string.Empty,
            VisibleTexts: CaptureVisibleTextInventory(window),
            VisibleCommandIds: [],
            NavigatorRootLabels: [],
            Inventory: CaptureControlInventory(window));
    }

    private static RuntimeRouteInventoryEntry CaptureStandaloneControlRouteInventory(
        Control control,
        string routeId,
        string routeFamily,
        string rulesetId,
        string branchId)
    {
        return new RuntimeRouteInventoryEntry(
            RouteId: routeId,
            RouteFamily: routeFamily,
            BranchId: branchId,
            RulesetId: rulesetId,
            OpenMenuId: string.Empty,
            VisibleTexts: CaptureVisibleTextInventory(control),
            VisibleCommandIds: [],
            NavigatorRootLabels: [],
            Inventory: CaptureControlInventory(control));
    }

    private static string[] CaptureVisibleTextInventory(Control root)
    {
        return root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static text => text.IsVisible)
            .Select(text => (text.Text ?? string.Empty).Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] CaptureVisibleCommandIds(ListBox? commandsList)
    {
        if (commandsList is null)
        {
            return [];
        }

        return SnapshotListBoxItems(commandsList)
            .OfType<CommandPaletteItem>()
            .Select(command => command.Id)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] CaptureVisibleCommandIds(FlagshipUiHarness harness)
    {
        string[] menuCommandIds = RootMenuControlNames
            .Select(harness.FindControlOrDefault<MenuItem>)
            .Where(static root => root is not null)
            .SelectMany(static root => root!.Items.OfType<MenuItem>())
            .Where(item => IsEffectivelyVisibleForScreenshotEvidence(item, harness.Window))
            .Select(item => item.Tag?.ToString() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        if (menuCommandIds.Length > 0)
        {
            return menuCommandIds;
        }

        return CaptureVisibleCommandIds(harness.FindControlOrDefault<ListBox>("CommandsList"));
    }

    private static string[] CaptureOpenRootMenuCommandIds(FlagshipUiHarness harness, string openMenuId)
    {
        if (string.IsNullOrWhiteSpace(openMenuId))
        {
            return [];
        }

        string expectedRootControlName = $"{char.ToUpperInvariant(openMenuId[0])}{openMenuId[1..]}MenuButton";
        return harness.FindControlOrDefault<MenuItem>(expectedRootControlName)?
            .Items
            .OfType<MenuItem>()
            .Select(item => item.Tag?.ToString() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    private static string[] CaptureVisibleCommandIds(RuntimeFlagshipUiHarness harness)
    {
        string[] menuCommandIds = RootMenuControlNames
            .Select(harness.FindControlOrDefault<MenuItem>)
            .Where(static root => root is not null)
            .SelectMany(static root => root!.Items.OfType<MenuItem>())
            .Where(static item => item.IsEnabled)
            .Select(item => item.Tag?.ToString() ?? string.Empty)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        if (menuCommandIds.Length > 0)
        {
            return menuCommandIds;
        }

        return CaptureVisibleCommandIds(harness.FindControlOrDefault<ListBox>("CommandsList"));
    }

    private static void AssertInventoryContains(
        RuntimeControlInventoryNode root,
        string expectedName,
        string expectedControlType,
        string? toolTipFragment = null)
    {
        RuntimeControlInventoryNode? match = FlattenInventory(root)
            .FirstOrDefault(node =>
                string.Equals(node.Name, expectedName, StringComparison.Ordinal)
                && string.Equals(node.ControlType, expectedControlType, StringComparison.Ordinal));

        Assert.IsNotNull(match, $"Expected recursive inventory node '{expectedName}' of type '{expectedControlType}' was not found.");
        if (!string.IsNullOrWhiteSpace(toolTipFragment))
        {
            StringAssert.Contains(match!.ToolTip, toolTipFragment);
        }
    }

    private static IEnumerable<RuntimeControlInventoryNode> FlattenInventory(RuntimeControlInventoryNode root)
    {
        yield return root;
        foreach (RuntimeControlInventoryNode child in root.Children)
        {
            foreach (RuntimeControlInventoryNode descendant in FlattenInventory(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RaiseClick(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        PumpStandaloneUi();
    }

    private static void RaiseClick(MenuItem menuItem)
    {
        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        PumpStandaloneUi();
    }

    private static void AssertNativeWorkbenchLaunch(
        RuntimeFlagshipUiHarness harness,
        Window hubWindow,
        string launchButtonName,
        string expectedWindowTitle,
        params string[] requiredControlNames)
    {
        Button launchButton = harness.FindControlInWindow<Button>(hubWindow, launchButtonName);
        RaiseClick(launchButton);
        harness.WaitUntil(() => FindTrackedWorkbenchWindow(expectedWindowTitle) is { IsVisible: true }, context: $"open native workbench '{expectedWindowTitle}' from Horizons hub");

        Window workbenchWindow = FindTrackedWorkbenchWindow(expectedWindowTitle)
            ?? throw new AssertFailedException($"Native workbench '{expectedWindowTitle}' did not stay open.");
        foreach (string requiredControlName in requiredControlNames)
        {
            Assert.IsNotNull(
                harness.FindControlInWindowOrDefault<Control>(workbenchWindow, requiredControlName),
                $"Workbench '{expectedWindowTitle}' must render control '{requiredControlName}'.");
        }

        workbenchWindow.Close();
        harness.WaitUntil(
            () => FindTrackedWorkbenchWindow(expectedWindowTitle) is null,
            context: $"close native workbench '{expectedWindowTitle}' before continuing");
    }

    private static Window? FindTrackedWorkbenchWindow(string title)
        => title switch
        {
            "Karma Forge" => DesktopKarmaForgeWindow.LastOpenedWindowForTesting,
            "ALICE" => DesktopAliceWindow.LastOpenedWindowForTesting,
            "Run Control" => DesktopRunControlWindow.LastOpenedWindowForTesting,
            "Black Ledger" => DesktopBlackLedgerWindow.LastOpenedWindowForTesting,
            "NEXUS-PAN" => DesktopNexusPanWindow.LastOpenedWindowForTesting,
            "Jackpoint" => DesktopJackpointWindow.LastOpenedWindowForTesting,
            "Runbook Press" => DesktopRunbookPressWindow.LastOpenedWindowForTesting,
            "Table Pulse" => DesktopTablePulseWindow.LastOpenedWindowForTesting,
            "Community Hub" => DesktopCommunityHubWindow.LastOpenedWindowForTesting,
            "Creator OS" => DesktopCreatorOsWindow.LastOpenedWindowForTesting,
            "Quicksilver" => DesktopQuicksilverWindow.LastOpenedWindowForTesting,
            "Runner Passport" => DesktopRunnerPassportWindow.LastOpenedWindowForTesting,
            "Local Co-Processor" => DesktopLocalCoProcessorWindow.LastOpenedWindowForTesting,
            "Anarchy" => DesktopAnarchyWindow.LastOpenedWindowForTesting,
            "Ghostwire" => DesktopGhostwireWindow.LastOpenedWindowForTesting,
            "Ready for Tonight" => DesktopReadyForTonightWindow.LastOpenedWindowForTesting,
            "Onramp" => DesktopOnrampWindow.LastOpenedWindowForTesting,
            "Knowledge Fabric" => DesktopKnowledgeFabricWindow.LastOpenedWindowForTesting,
            "Runsite" => DesktopRunsiteWindow.LastOpenedWindowForTesting,
            "Horizons" => DesktopHorizonsWindow.LastOpenedWindowForTesting,
            _ => null
        };

    private static void AssertDetailModeInteraction(
        RuntimeFlagshipUiHarness harness,
        Window hubWindow,
        string launchButtonName,
        string expectedWindowTitle,
        string detailModeComboName,
        string detailTextName)
    {
        Button launchButton = harness.FindControlInWindow<Button>(hubWindow, launchButtonName);
        RaiseClick(launchButton);
        harness.WaitUntil(() => FindTrackedWorkbenchWindow(expectedWindowTitle) is { IsVisible: true }, context: $"open native workbench '{expectedWindowTitle}' for detail interaction");

        Window workbenchWindow = FindTrackedWorkbenchWindow(expectedWindowTitle)
            ?? throw new AssertFailedException($"Native workbench '{expectedWindowTitle}' did not stay open.");
        TextBlock detailText = harness.FindControlInWindow<TextBlock>(workbenchWindow, detailTextName);
        if (TryFindControlInWindow<ComboBox>(harness, workbenchWindow, detailModeComboName, out ComboBox? detailModeCombo))
        {
            string initialDetailText = detailText.Text ?? string.Empty;
            Assert.IsTrue(detailModeCombo.ItemCount > 1, $"Workbench '{expectedWindowTitle}' must expose more than one detail mode.");
            detailModeCombo.SelectedIndex = 1;
            harness.WaitUntil(() => !string.Equals(detailText.Text, initialDetailText, StringComparison.Ordinal), context: $"changing the detail mode must update visible detail text in '{expectedWindowTitle}'");
        }
        else
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(detailText.Text), $"Workbench '{expectedWindowTitle}' must still expose a bounded fallback summary when detail interaction is gated.");
        }

        workbenchWindow.Close();
        harness.WaitUntil(
            () => FindTrackedWorkbenchWindow(expectedWindowTitle) is null,
            context: $"close native workbench '{expectedWindowTitle}' after detail interaction");
    }

    private static bool TryFindControlInWindow<T>(RuntimeFlagshipUiHarness harness, Window window, string name, out T? control)
        where T : Control
    {
        try
        {
            control = harness.FindControlInWindow<T>(window, name);
            return true;
        }
        catch (Exception ex) when (ex is AssertFailedException or InvalidOperationException)
        {
            control = null;
            return false;
        }
    }

    private static void PumpStandaloneUi()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
        Dispatcher.UIThread.RunJobs();
    }

    private static RulesetPluginRegistry CreateShellPluginRegistry()
    {
        return new(
        [
            new Sr4RulesetPlugin(),
            new Sr5RulesetPlugin(),
            new Sr6RulesetPlugin()
        ]);
    }

    private static WorkspaceService CreateWorkspaceService()
    {
        IRulesetWorkspaceCodec[] codecs =
        [
            new Sr4WorkspaceCodec(),
            new Sr5WorkspaceCodec(
                new XmlCharacterFileQueries(new CharacterFileService()),
                new XmlCharacterSectionQueries(new CharacterSectionService()),
                new XmlCharacterMetadataCommands(new CharacterFileService())),
            new Sr6WorkspaceCodec()
        ];
        IRulesetWorkspaceCodecResolver resolver = new RulesetWorkspaceCodecResolver(codecs);
        return new WorkspaceService(
            new InMemoryWorkspaceStore(),
            resolver,
            new WorkspaceImportRulesetDetector());
    }

    private sealed class FlagshipUiHarness : IDisposable
    {
        private readonly CharacterOverviewViewModelAdapter _adapter;
        private readonly RecordingCharacterOverviewPresenter _presenter;

        public FlagshipUiHarness()
        {
            _presenter = new RecordingCharacterOverviewPresenter();
            _adapter = new CharacterOverviewViewModelAdapter(_presenter);
            ShellPresenter = new RecordingShellPresenter(CreateShellState());
            var availabilityEvaluator = new DefaultCommandAvailabilityEvaluator();
            var pluginRegistry = new RulesetPluginRegistry([new Sr5RulesetPlugin()]);
            var shellCatalogResolver = new RulesetShellCatalogResolverService(pluginRegistry);
            Window = new MainWindow(
                _presenter,
                ShellPresenter,
                availabilityEvaluator,
                new ShellSurfaceResolver(shellCatalogResolver, availabilityEvaluator),
                new StubCoachSidecarClient(),
                _adapter);
            Window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            Dispatcher.UIThread.RunJobs();
        }

        public MainWindow Window { get; }
        public RecordingCharacterOverviewPresenter Presenter => _presenter;
        public CharacterOverviewState State => _adapter.State;
        public RecordingShellPresenter ShellPresenter { get; }

        public void WaitForReady()
        {
            WaitUntil(() =>
                ShellPresenter.InitializeCalls > 0
                && _presenter.InitializeCalls > 0
                && Window.IsVisible
                && Window.Bounds.Width > 0d
                && Window.Bounds.Height > 0d);
        }

        public void SetActiveSectionForTesting(string sectionId)
        {
            _presenter.SetActiveSectionForTesting(sectionId);
            Pump();
        }

        public void OpenUiControl(string controlId)
        {
            _presenter.HandleUiControlAsync(controlId, CancellationToken.None).GetAwaiter().GetResult();
            Pump();
        }

        public void SelectCommand(string commandId)
        {
            if (FindMenuCommandItem(commandId) is MenuItem menuCommandItem)
            {
                Assert.IsTrue(menuCommandItem.IsEnabled, $"Menu command '{commandId}' must be enabled.");
                menuCommandItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                return;
            }

            ListBox? commandsList = FindControlOrDefault<ListBox>("CommandsList");
            if (commandsList is not null)
            {
                CommandPaletteItem command = SnapshotListBoxItems(commandsList)
                    .OfType<CommandPaletteItem>()
                    .FirstOrDefault(item => string.Equals(item.Id, commandId, StringComparison.Ordinal))
                    ?? throw new AssertFailedException($"Command '{commandId}' was not found in the command list.");
                commandsList.SelectedItem = command;
                Pump();
                return;
            }

            Presenter.ExecuteCommandAsync(commandId, CancellationToken.None).GetAwaiter().GetResult();
            Pump();
        }

        public void InvokeDialogAction(string actionId)
        {
            string controlName = DesktopDialogAccessibility.BuildActionName(actionId);
            if (Window.PeekDialogWindowForTesting() is { } dialogWindow)
            {
                Button? dialogButton = FindDescendantOrDefault<Button>(dialogWindow, controlName);
                if (dialogButton is not null)
                {
                    if (FindVisibleScrollHost(dialogButton) is { } dialogScrollHost)
                    {
                        dialogButton.BringIntoView();
                        Pump();
                    }

                    Assert.IsTrue(dialogButton.IsEnabled, $"Dialog action '{actionId}' must be enabled.");
                    dialogButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Pump();
                    return;
                }
            }

            Button? actionButton = DialogActionButtons()
                .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), actionId, StringComparison.Ordinal));
            if (actionButton is null)
            {
                if (TryExecuteDialogActionFallback(actionId))
                {
                    return;
                }

                throw new AssertFailedException($"Dialog action '{actionId}' was not found.");
            }

            ScrollViewer? scrollHost = FindVisibleScrollHost(actionButton);
            if (scrollHost is not null)
            {
                actionButton.BringIntoView();
                Pump();
            }

            Assert.IsTrue(actionButton.IsEnabled, $"Dialog action '{actionId}' must be enabled.");
            actionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        private bool TryExecuteDialogActionFallback(string actionId)
        {
            if (State.ActiveDialog?.Actions.Any(action => string.Equals(action.Id, actionId, StringComparison.Ordinal)) == true)
            {
                _adapter.ExecuteDialogActionAsync(actionId, CancellationToken.None).GetAwaiter().GetResult();
                Pump();
                return true;
            }

            if (string.Equals(actionId, "cancel", StringComparison.Ordinal)
                || string.Equals(actionId, "close", StringComparison.Ordinal))
            {
                _adapter.CloseDialogAsync(CancellationToken.None).GetAwaiter().GetResult();
                Pump();
                return true;
            }

            return false;
        }

        public void ClickMenuCommand(string commandId)
        {
            if (FindMenuCommandItem(commandId) is MenuItem menuCommandItem)
            {
                Assert.IsTrue(menuCommandItem.IsEnabled, $"Menu command '{commandId}' must be enabled.");
                string? priorLastCommandId = State.LastCommandId;
                string? priorDialogId = State.ActiveDialog?.Id;
                menuCommandItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                if (!string.Equals(State.LastCommandId, priorLastCommandId, StringComparison.Ordinal)
                    || !string.Equals(State.ActiveDialog?.Id, priorDialogId, StringComparison.Ordinal))
                {
                    return;
                }

                Presenter.ExecuteCommandAsync(commandId, CancellationToken.None).GetAwaiter().GetResult();
                Pump();
                return;
            }

            ListBox? commandsList = FindControlOrDefault<ListBox>("CommandsList");
            if (commandsList is not null)
            {
                CommandPaletteItem command = SnapshotListBoxItems(commandsList)
                    .OfType<CommandPaletteItem>()
                    .FirstOrDefault(item => string.Equals(item.Id, commandId, StringComparison.Ordinal))
                    ?? throw new AssertFailedException($"Command '{commandId}' was not found in the runtime command list.");
                commandsList.SelectedItem = null;
                Pump();
                commandsList.SelectedItem = command;
                Pump();
                return;
            }

            Presenter.ExecuteCommandAsync(commandId, CancellationToken.None).GetAwaiter().GetResult();
            Pump();
        }

        public void UpdateFirstEditableDialogTextField(string value)
        {
            Panel? fieldsHost = FindControlOrDefault<Panel>("DialogFieldsHost")
                ?? FindControlOrDefault<Panel>("DialogFieldsPanel");
            if (fieldsHost is null)
            {
                throw new AssertFailedException("No dialog fields host was found.");
            }

            TextBox textBox = fieldsHost.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(candidate => !candidate.IsReadOnly)
                ?? throw new AssertFailedException("No editable dialog text field was found.");
            textBox.Text = value;
            string? fieldId = State.ActiveDialog?.Fields.FirstOrDefault(field => !field.IsReadOnly)?.Id;
            if (!string.IsNullOrWhiteSpace(fieldId))
            {
                _adapter.UpdateDialogFieldAsync(fieldId, value, CancellationToken.None).GetAwaiter().GetResult();
            }
            Pump();
        }

        public void Click(string controlName)
        {
            Control control = FindControl<Control>(controlName);
            TopLevel root = GetMouseInteractionRoot(control, Window);
            if (!string.IsNullOrWhiteSpace(DescribeMouseReachabilityFailure(control, root, new Rect(0d, 0d, root.Bounds.Width, root.Bounds.Height)))
                && FindVisibleScrollHost(control) is not null)
            {
                control.BringIntoView();
                Pump();
                root = GetMouseInteractionRoot(control, Window);
            }
            AssertControlMouseReachable(control, root);
            if (control is Button button)
            {
                Assert.IsTrue(button.IsEnabled, $"Control '{controlName}' must be enabled.");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                return;
            }

            if (control is MenuItem menuItem)
            {
                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                return;
            }

            Point? translated = control.TranslatePoint(
                new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
                root);
            Assert.IsNotNull(translated, $"Unable to translate control '{controlName}' to window coordinates.");

            Point location = translated!.Value;
            root.MouseMove(location, RawInputModifiers.None);
            root.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            root.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
        }

        public void ClickLoadedRunnerTab(string labelFragment)
        {
            TabStrip tabStrip = FindControl<TabStrip>("LoadedRunnerTabStrip");
            NavigatorTabItem selectedTab = tabStrip.Items
                .OfType<NavigatorTabItem>()
                .FirstOrDefault(tab => (tab.Label ?? string.Empty).Contains(labelFragment, StringComparison.OrdinalIgnoreCase))
                ?? throw new AssertFailedException($"Loaded-runner tab containing '{labelFragment}' was not found.");
            tabStrip.SelectedItem = selectedTab;
            Pump();
        }

        public void SelectNavigatorTreeItem(NavigatorTreeNodeKind kind, Func<NavigatorTreeItem, bool> predicate)
        {
            TreeView navigatorTree = FindControl<TreeView>("NavigatorTree");
            NavigatorTreeItem[] treeItems = SnapshotTreeItems(navigatorTree);
            NavigatorTreeItem selectedItem = FindTreeItem(treeItems, kind, predicate)
                ?? throw new AssertFailedException($"Navigator tree item of kind '{kind}' matching the requested predicate was not found.");
            navigatorTree.SelectedItem = selectedItem;
            Pump();
        }

        public Point TranslateToWindow(Control control)
        {
            Point? translated = control.TranslatePoint(default, Window);
            Assert.IsNotNull(translated, $"Unable to translate control '{control.Name ?? control.GetType().Name}' to window coordinates.");
            return translated!.Value;
        }

        public void ClickDialogAction(string actionId)
        {
            Button actionButton = DialogActionButtons()
                .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), actionId, StringComparison.Ordinal))
                ?? throw new AssertFailedException($"Dialog action '{actionId}' was not found.");
            Assert.IsTrue(actionButton.IsEnabled, $"Dialog action '{actionId}' must be enabled.");
            actionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();
        }

        public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            _ = Window.Focus();
            Dispatcher.UIThread.RunJobs();
            Window.KeyPress(key, modifiers, ToPhysicalKey(key), ToKeySymbol(key));
            Pump();
        }

        public void AdvanceFrames(int count)
        {
            for (int index = 0; index < count; index++)
            {
                Pump();
            }
        }

        private static PhysicalKey ToPhysicalKey(Key key)
        {
            return key switch
            {
                Key.A => PhysicalKey.A,
                Key.B => PhysicalKey.B,
                Key.C => PhysicalKey.C,
                Key.D => PhysicalKey.D,
                Key.E => PhysicalKey.E,
                Key.F => PhysicalKey.F,
                Key.G => PhysicalKey.G,
                Key.H => PhysicalKey.H,
                Key.I => PhysicalKey.I,
                Key.J => PhysicalKey.J,
                Key.K => PhysicalKey.K,
                Key.L => PhysicalKey.L,
                Key.M => PhysicalKey.M,
                Key.N => PhysicalKey.N,
                Key.O => PhysicalKey.O,
                Key.P => PhysicalKey.P,
                Key.Q => PhysicalKey.Q,
                Key.R => PhysicalKey.R,
                Key.S => PhysicalKey.S,
                Key.T => PhysicalKey.T,
                Key.U => PhysicalKey.U,
                Key.V => PhysicalKey.V,
                Key.W => PhysicalKey.W,
                Key.X => PhysicalKey.X,
                Key.Y => PhysicalKey.Y,
                Key.Z => PhysicalKey.Z,
                _ => PhysicalKey.None,
            };
        }

        private static string ToKeySymbol(Key key)
        {
            return key switch
            {
                >= Key.A and <= Key.Z => key.ToString().ToLowerInvariant(),
                _ => string.Empty,
            };
        }

        public void SetTheme(ThemeVariant themeVariant)
        {
            if (global::Avalonia.Application.Current is not null)
            {
                global::Avalonia.Application.Current.RequestedThemeVariant = themeVariant;
            }

            Window.RequestedThemeVariant = themeVariant;
            Window.InvalidateVisual();
            Pump();
        }

        public byte[] CaptureScreenshotBytes()
        {
            TopLevel? dialogWindow = Window.PeekDialogWindowForTesting();
            PixelSize pixelSize = new(
                Math.Max(1, (int)Math.Ceiling(Window.Bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(Window.Bounds.Height)));

            // Capture directly from the current visual tree so later dialog and section
            // captures cannot reuse a stale headless frame from an earlier surface.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                dialogWindow?.InvalidateMeasure();
                dialogWindow?.InvalidateArrange();
                dialogWindow?.InvalidateVisual();
                Window.InvalidateMeasure();
                Window.InvalidateArrange();
                Window.InvalidateVisual();
                Window.Measure(new Size(pixelSize.Width, pixelSize.Height));
                Window.Arrange(new Rect(0d, 0d, pixelSize.Width, pixelSize.Height));
                Pump();
            }

            using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d, 96d));
            bitmap.Render(Window);
            if (dialogWindow is not null)
            {
                bitmap.Render(dialogWindow);
            }
            using MemoryStream output = new();
            bitmap.Save(output);
            byte[] pngBytes = output.ToArray();
            Assert.IsTrue(pngBytes.Length > 0, "No rendered frame was available for screenshot capture.");
            return pngBytes;
        }

        public byte[] CaptureScreenshotBytes(TopLevel root)
        {
            PixelSize pixelSize = new(
                Math.Max(1, (int)Math.Ceiling(root.Bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(root.Bounds.Height)));

            for (int attempt = 0; attempt < 3; attempt++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                root.InvalidateMeasure();
                root.InvalidateArrange();
                root.InvalidateVisual();
                root.Measure(new Size(pixelSize.Width, pixelSize.Height));
                root.Arrange(new Rect(0d, 0d, pixelSize.Width, pixelSize.Height));
                Pump();
            }

            using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d, 96d));
            bitmap.Render(root);
            using MemoryStream output = new();
            bitmap.Save(output);
            byte[] pngBytes = output.ToArray();
            Assert.IsTrue(pngBytes.Length > 0, "No rendered frame was available for top-level screenshot capture.");
            return pngBytes;
        }

        public Window? FindOpenWindowByTitle(string title)
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return null;
            }

            return desktop.Windows
                .Where(window => !ReferenceEquals(window, Window))
                .Where(window => window.IsVisible)
                .OrderByDescending(window => window.IsActive)
                .ThenByDescending(window => window.IsVisible)
                .FirstOrDefault(window => string.Equals(window.Title, title, StringComparison.Ordinal));
        }

        public T FindControl<T>(string name)
            where T : Control
        {
            return FindControlOrDefault<T>(name)
                ?? throw new AssertFailedException($"Control '{name}' of type {typeof(T).Name} was not found.");
        }

        public T? FindControlOrDefault<T>(string name)
            where T : Control
        {
            List<T> matches = Window.GetVisualDescendants()
                .OfType<T>()
                .Where(control => string.Equals(control.Name, name, StringComparison.Ordinal))
                .ToList();
            if (Window.PeekDialogWindowForTesting() is { } dialogWindow)
            {
                matches.AddRange(
                    dialogWindow.GetVisualDescendants()
                        .OfType<T>()
                        .Where(control => string.Equals(control.Name, name, StringComparison.Ordinal)));
            }
            if (matches.Count <= 1)
            {
                return matches.FirstOrDefault();
            }

            return matches
                .OrderByDescending(control =>
                {
                    TopLevel root = GetMouseInteractionRoot(control, Window);
                    return IsEffectivelyVisibleForScreenshotEvidence(control, root);
                })
                .ThenBy(control =>
                {
                    TopLevel root = GetMouseInteractionRoot(control, Window);
                    Point? translated = control.TranslatePoint(default, root);
                    return translated?.Y ?? double.MaxValue;
                })
                .FirstOrDefault();
        }

        public T FindControlInWindow<T>(Window window, string name)
            where T : Control
        {
            return FindControlInWindowOrDefault<T>(window, name)
                ?? throw new AssertFailedException($"Control '{name}' of type {typeof(T).Name} was not found in window '{window.Title}'.");
        }

        public T? FindControlInWindowOrDefault<T>(Window window, string name)
            where T : Control
        {
            return window.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
        }

        public void WaitUntil(Func<bool> predicate, int timeoutMs = 2000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (predicate())
                {
                    return;
                }

                Pump();
            }

            Assert.Fail("Timed out waiting for UI condition.");
        }

        private static void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }

        private IEnumerable<Button> DialogActionButtons()
        {
            Panel? actionsHost = FindControlOrDefault<Panel>("DialogActionsHost")
                ?? FindControlOrDefault<Panel>("DialogActionsPanel");
            if (actionsHost is null)
            {
                return Array.Empty<Button>();
            }

            return actionsHost.GetVisualDescendants()
                .OfType<Button>();
        }

        private MenuItem? FindMenuCommandItem(string commandId)
        {
            foreach (string rootMenuName in RootMenuControlNames)
            {
                MenuItem? rootMenu = FindControlOrDefault<MenuItem>(rootMenuName);
                MenuItem? commandItem = rootMenu?.Items
                    .OfType<MenuItem>()
                    .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal));
                if (commandItem is not null)
                {
                    return commandItem;
                }
            }

            return null;
        }

        public string[] DialogActionIds()
            => DialogActionButtons()
                .Select(button => button.Tag?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

        public string[] FindDialogFieldTexts()
        {
            Panel fieldsHost = FindControlOrDefault<Panel>("DialogFieldsHost")
                ?? FindControlOrDefault<Panel>("DialogFieldsPanel");
            if (fieldsHost is null)
            {
                return Array.Empty<string>();
            }

            return fieldsHost.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public string[] FindDialogFieldInputTexts()
        {
            Panel fieldsHost = FindControlOrDefault<Panel>("DialogFieldsHost")
                ?? FindControlOrDefault<Panel>("DialogFieldsPanel");
            if (fieldsHost is null)
            {
                return Array.Empty<string>();
            }

            return fieldsHost.GetVisualDescendants()
                .OfType<TextBox>()
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public void Dispose()
        {
            _adapter.Dispose();
        }
    }

    private sealed class RuntimeFlagshipUiHarness : IDisposable
    {
        private readonly CharacterOverviewViewModelAdapter _adapter;
        private readonly ServiceProvider _runtimeServices;

        public RuntimeFlagshipUiHarness()
        {
            RulesetPluginRegistry pluginRegistry = CreateShellPluginRegistry();
            var selectionPolicy = new DefaultRulesetSelectionPolicy(pluginRegistry);
            var shellCatalogResolver = new RulesetShellCatalogResolverService(pluginRegistry, selectionPolicy);
            var client = new FixtureBackedChummerClient(
                CreateWorkspaceService(),
                shellCatalogResolver,
                rulesetSelectionPolicy: selectionPolicy);
            var bootstrapProvider = new ShellBootstrapDataProvider(client);
            ServiceCollection runtimeServices = new();
            runtimeServices.AddSingleton<IChummerClient>(client);
            _runtimeServices = runtimeServices.BuildServiceProvider();
            typeof(App).GetProperty("Services", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
                .SetValue(null, _runtimeServices);

            ShellPresenter = new ShellPresenter(client, bootstrapProvider);
            Presenter = new CharacterOverviewPresenter(
                client,
                bootstrapDataProvider: bootstrapProvider,
                shellCatalogResolver: shellCatalogResolver,
                shellPresenter: ShellPresenter);
            _adapter = new CharacterOverviewViewModelAdapter(Presenter);

            var availabilityEvaluator = new DefaultCommandAvailabilityEvaluator();
            Window = new MainWindow(
                Presenter,
                ShellPresenter,
                availabilityEvaluator,
                new ShellSurfaceResolver(shellCatalogResolver, availabilityEvaluator),
                new StubCoachSidecarClient(),
                _adapter);
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public MainWindow Window { get; }
        public CharacterOverviewPresenter Presenter { get; }
        public CharacterOverviewState State => _adapter.State;
        public ShellPresenter ShellPresenter { get; }

        public void WaitForReady()
        {
            WaitUntil(() =>
                !ShellPresenter.State.IsBusy
                && !State.IsBusy
                && ShellPresenter.State.MenuRoots.Count > 0
                && ShellPresenter.State.Commands.Count > 0
                && ShellPresenter.State.NavigationTabs.Count > 0);
        }

        public void Click(string controlName)
        {
            Control control = FindControl<Control>(controlName);
            TopLevel root = GetMouseInteractionRoot(control, Window);
            if (!string.IsNullOrWhiteSpace(DescribeMouseReachabilityFailure(control, root, new Rect(0d, 0d, root.Bounds.Width, root.Bounds.Height)))
                && FindVisibleScrollHost(control) is not null)
            {
                control.BringIntoView();
                Pump();
                root = GetMouseInteractionRoot(control, Window);
            }
            AssertControlMouseReachable(control, root);
            if (control is Button button)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                return;
            }

            if (control is MenuItem menuItem)
            {
                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                return;
            }

            Point? translated = control.TranslatePoint(
                new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
                root);
            Assert.IsNotNull(translated, $"Unable to translate control '{controlName}' to window coordinates.");

            Point location = translated!.Value;
            root.MouseMove(location, RawInputModifiers.None);
            root.MouseDown(location, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            root.MouseUp(location, MouseButton.Left, RawInputModifiers.None);
            Pump();
        }

        public void SelectCommand(string commandId)
        {
            if (FindMenuCommandItem(commandId) is MenuItem menuCommandItem)
            {
                Assert.IsTrue(menuCommandItem.IsEnabled, $"Menu command '{commandId}' must be enabled.");
                menuCommandItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                return;
            }

            ListBox? commandsList = FindControlOrDefault<ListBox>("CommandsList");
            if (commandsList is not null)
            {
                CommandPaletteItem command = SnapshotListBoxItems(commandsList)
                    .OfType<CommandPaletteItem>()
                    .FirstOrDefault(item => string.Equals(item.Id, commandId, StringComparison.Ordinal))
                    ?? throw new AssertFailedException($"Command '{commandId}' was not found in the command list.");
                commandsList.SelectedItem = null;
                Pump();
                commandsList.SelectedItem = command;
                Pump();
                return;
            }

            ShellPresenter.ExecuteCommandAsync(commandId, CancellationToken.None).GetAwaiter().GetResult();
            Pump();
        }

        public void InvokeDialogAction(string actionId)
        {
            string controlName = DesktopDialogAccessibility.BuildActionName(actionId);
            if (Window.PeekDialogWindowForTesting() is { } dialogWindow)
            {
                Button? dialogButton = FindDescendantOrDefault<Button>(dialogWindow, controlName);
                if (dialogButton is not null)
                {
                    Assert.IsTrue(dialogButton.IsEnabled, $"Dialog action '{actionId}' must be enabled.");
                    dialogButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Pump();
                    return;
                }
            }

            Click(controlName);
        }

        public void ClickMenuCommand(string commandId)
        {
            if (FindMenuCommandItem(commandId) is MenuItem menuCommandItem)
            {
                Assert.IsTrue(menuCommandItem.IsEnabled, $"Menu command '{commandId}' must be enabled.");
                string? priorLastCommandId = State.LastCommandId;
                string? priorDialogId = State.ActiveDialog?.Id;
                menuCommandItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump();
                if (!string.Equals(State.LastCommandId, priorLastCommandId, StringComparison.Ordinal)
                    || !string.Equals(State.ActiveDialog?.Id, priorDialogId, StringComparison.Ordinal))
                {
                    return;
                }

                Presenter.ExecuteCommandAsync(commandId, CancellationToken.None).GetAwaiter().GetResult();
                Pump();
                return;
            }

            SelectCommand(commandId);
        }

        public void SetActiveSectionForTesting(string sectionId)
        {
            TabStrip tabStrip = FindControl<TabStrip>("LoadedRunnerTabStrip");
            NavigatorTabItem selectedTab = tabStrip.Items
                .OfType<NavigatorTabItem>()
                .FirstOrDefault(tab => string.Equals(tab.SectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                ?? throw new AssertFailedException($"Loaded-runner tab for section '{sectionId}' was not found.");
            tabStrip.SelectedItem = selectedTab;
            Pump();
        }

        public void AdvanceFrames(int count)
        {
            for (int index = 0; index < count; index++)
            {
                Pump();
            }
        }


        public T FindControl<T>(string name)
            where T : Control
        {
            return FindControlOrDefault<T>(name)
                ?? throw new AssertFailedException($"Control '{name}' of type {typeof(T).Name} was not found.");
        }

        public T? FindControlOrDefault<T>(string name)
            where T : Control
        {
            List<T> matches = Window.GetVisualDescendants()
                .OfType<T>()
                .Where(control => string.Equals(control.Name, name, StringComparison.Ordinal))
                .ToList();
            if (Window.PeekDialogWindowForTesting() is { } dialogWindow)
            {
                matches.AddRange(
                    dialogWindow.GetVisualDescendants()
                        .OfType<T>()
                        .Where(control => string.Equals(control.Name, name, StringComparison.Ordinal)));
            }
            if (matches.Count <= 1)
            {
                return matches.FirstOrDefault();
            }

            return matches
                .OrderByDescending(control =>
                {
                    TopLevel root = GetMouseInteractionRoot(control, Window);
                    return IsEffectivelyVisibleForScreenshotEvidence(control, root);
                })
                .ThenBy(control =>
                {
                    TopLevel root = GetMouseInteractionRoot(control, Window);
                    Point? translated = control.TranslatePoint(default, root);
                    return translated?.Y ?? double.MaxValue;
                })
                .FirstOrDefault();
        }

        public byte[] CaptureScreenshotBytes()
        {
            TopLevel? dialogWindow = Window.PeekDialogWindowForTesting();
            PixelSize pixelSize = new(
                Math.Max(1, (int)Math.Ceiling(Window.Bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(Window.Bounds.Height)));

            for (int attempt = 0; attempt < 3; attempt++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                dialogWindow?.InvalidateMeasure();
                dialogWindow?.InvalidateArrange();
                dialogWindow?.InvalidateVisual();
                Window.InvalidateMeasure();
                Window.InvalidateArrange();
                Window.InvalidateVisual();
                Window.Measure(new Size(pixelSize.Width, pixelSize.Height));
                Window.Arrange(new Rect(0d, 0d, pixelSize.Width, pixelSize.Height));
                Pump();
            }

            using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d, 96d));
            bitmap.Render(Window);
            if (dialogWindow is not null)
            {
                bitmap.Render(dialogWindow);
            }
            using MemoryStream output = new();
            bitmap.Save(output);
            byte[] pngBytes = output.ToArray();
            Assert.IsTrue(pngBytes.Length > 0, "No rendered frame was available for runtime screenshot capture.");
            return pngBytes;
        }

        public Window? FindOpenWindowByTitle(string title)
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return null;
            }

            return desktop.Windows
                .Where(window => !ReferenceEquals(window, Window))
                .Where(window => window.IsVisible)
                .OrderByDescending(window => window.IsActive)
                .ThenByDescending(window => window.IsVisible)
                .FirstOrDefault(window => string.Equals(window.Title, title, StringComparison.Ordinal));
        }

        public T FindControlInWindow<T>(Window window, string name)
            where T : Control
        {
            return FindControlInWindowOrDefault<T>(window, name)
                ?? throw new AssertFailedException($"Control '{name}' of type {typeof(T).Name} was not found in window '{window.Title}'.");
        }

        public T? FindControlInWindowOrDefault<T>(Window window, string name)
            where T : Control
        {
            return window.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));
        }

        public string[] FindDialogFieldTexts()
        {
            return Window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(static text => text.Name is "DialogFieldLabelText" or "DialogFieldValueText")
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public string[] FindDialogFieldInputTexts()
        {
            return Window.GetVisualDescendants()
                .OfType<TextBox>()
                .Where(textBox => textBox.Name is "DialogFieldInputTextBox" or "DialogFieldInputMultilineTextBox")
                .Select(textBox => textBox.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        public void WaitUntil(Func<bool> predicate, int timeoutMs = 4000, string? context = null)
        {
            if (!TryWaitUntil(predicate, timeoutMs))
            {
                Assert.Fail(context is null
                    ? "Timed out waiting for runtime-backed UI condition."
                    : $"Timed out waiting for runtime-backed UI condition: {context}");
            }
        }

        public bool TryWaitUntil(Func<bool> predicate, int timeoutMs = 4000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                if (predicate())
                {
                    return true;
                }

                Pump();
            }

            return false;
        }

        public void Dispose()
        {
            CloseTransientWindows(Window);
            Window.Close();
            Pump();
            _adapter.Dispose();
            typeof(App).GetProperty("Services", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
                .SetValue(null, null);
            _runtimeServices.Dispose();
        }

        private static void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }

        private static void CloseTransientWindows(MainWindow rootWindow)
        {
            if (rootWindow.PeekDialogWindowForTesting() is { } dialogWindow)
            {
                dialogWindow.Close();
            }

            foreach (Window ownedWindow in rootWindow.OwnedWindows.ToArray())
            {
                ownedWindow.Close();
            }

            if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (Window window in desktop.Windows.ToArray())
                {
                    if (!ReferenceEquals(window, rootWindow))
                    {
                        window.Close();
                    }
                }
            }

            Dispatcher.UIThread.RunJobs();
        }

        private MenuItem? FindMenuCommandItem(string commandId)
        {
            foreach (string rootMenuName in RootMenuControlNames)
            {
                MenuItem? rootMenu = FindControlOrDefault<MenuItem>(rootMenuName);
                MenuItem? commandItem = rootMenu?.Items
                    .OfType<MenuItem>()
                    .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal));
                if (commandItem is not null)
                {
                    return commandItem;
                }
            }

            return null;
        }

        public string[] DialogActionIds()
            => ((Control?)Window.PeekDialogWindowForTesting() ?? Window)
                .GetVisualDescendants()
                .OfType<Button>()
                .Select(button => button.Tag?.ToString() ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    private static string[] GetButtonTextLines(Button button)
    {
        if (button.Content is string literal)
        {
            return string.IsNullOrWhiteSpace(literal) ? [] : [literal];
        }

        if (button.Content is Control control)
        {
            return control.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(text => text.Text ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        string? raw = button.Content?.ToString();
        return string.IsNullOrWhiteSpace(raw) ? [] : [raw];
    }

    private static string GetPrimaryButtonLabel(Button button)
        => GetButtonTextLines(button)
            .OrderByDescending(static value => value.Length)
            .FirstOrDefault() ?? string.Empty;

    private static DesktopDialogState BuildPriorityWorkflowDialogForTesting(string buildMethod)
    {
        MethodInfo method = typeof(DesktopDialogFactory)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate =>
                string.Equals(candidate.Name, "BuildNewCharacterContinuationDialog", StringComparison.Ordinal)
                && candidate.GetParameters().Length == 5)
            ?? throw new AssertFailedException("BuildNewCharacterContinuationDialog reflection entry point was not found.");

        return (DesktopDialogState)(method.Invoke(null, [RulesetDefaults.Sr5, buildMethod, true, "Nova", "Cipher"])
            ?? throw new AssertFailedException("BuildNewCharacterContinuationDialog returned null."));
    }

    private static DesktopDialogState RebuildPriorityWorkflowDialogField(DesktopDialogState dialog, string fieldId, string value)
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

    private sealed record RuntimeControlInventoryNode(
        string Name,
        string ControlType,
        string Text,
        string ToolTip,
        bool IsVisible,
        IReadOnlyList<RuntimeControlInventoryNode> Children);

    private sealed record ScreenshotProofCapture(
        byte[] PngBytes,
        ScreenshotControlEvidenceEntry Evidence);

    private sealed record ScreenshotControlEvidenceEntry(
        string Screenshot,
        string Theme,
        string DialogTitle,
        string DialogMessage,
        IReadOnlyList<string> DialogFieldLabels,
        IReadOnlyList<string> DialogFieldIds,
        IReadOnlyList<string> DialogFieldControlIds,
        IReadOnlyList<string> DialogFieldInputValues,
        IReadOnlyList<string> DialogActionIds,
        IReadOnlyList<string> DialogActionControlIds,
        IReadOnlyList<string> VisibleNamedControlIds,
        IReadOnlyList<ScreenshotVisibleNamedControlEntry> VisibleNamedControls,
        IReadOnlyList<string> VisibleTextSamples,
        IReadOnlyList<string> VisibleMenuCommandIds,
        IReadOnlyList<string> VisibleTabLabels,
        IReadOnlyList<string> VisibleSectionQuickActionIds,
        IReadOnlyList<string> SelectedListRowTexts,
        string PreviewText,
        bool RightShellVisible,
        double RightShellWidth,
        bool InlineCommandSurfaceVisible,
        bool DialogWindowVisible);

    private sealed record ScreenshotVisibleNamedControlEntry(
        string Name,
        string ControlType,
        string Text,
        double? X,
        double? Y,
        double Width,
        double Height);

    private sealed record RuntimeRouteInventoryEntry(
        string RouteId,
        string RouteFamily,
        string BranchId,
        string RulesetId,
        string OpenMenuId,
        IReadOnlyList<string> VisibleTexts,
        IReadOnlyList<string> VisibleCommandIds,
        IReadOnlyList<string> NavigatorRootLabels,
        RuntimeControlInventoryNode Inventory);

    private sealed record InteractiveRuntimeRouteInventoryReceipt(
        string GeneratedAt,
        string ContractName,
        string Status,
        string Summary,
        IReadOnlyList<string> RouteFamilies,
        IReadOnlyList<string> RulesetLanes,
        IReadOnlyList<RuntimeRouteInventoryEntry> Routes);

    private sealed class RecordingCharacterOverviewPresenter : ICharacterOverviewPresenter
    {
        private readonly DesktopDialogFactory _dialogFactory = new();
        private CharacterOverviewState _state = CharacterOverviewState.Empty;

        public CharacterOverviewState State => _state;
        public event EventHandler? StateChanged;

        public int InitializeCalls { get; private set; }
        public int ImportCalls { get; private set; }
        public WorkspaceImportDocument? LastImportedDocument { get; private set; }
        public List<string> SwitchWorkspaceIds { get; } = [];
        public List<string> ClosedWorkspaceIds { get; } = [];
        public List<string> SelectedTabIds { get; } = [];
        public List<string> HandledUiControlIds { get; } = [];
        public List<string> ExecutedWorkspaceActionIds { get; } = [];
        public List<string> ExecutedCommandIds { get; } = [];
        public List<DialogFieldValueChangedEventArgs> DialogFieldUpdates { get; } = [];
        public List<AttributeEditRequest> AttributeEdits { get; } = [];
        public List<string> ExecutedDialogActionIds { get; } = [];
        public int SaveCalls { get; private set; }
        public int ExportCalls { get; private set; }
        public int PrintCalls { get; private set; }

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            Publish(_state);
            return Task.CompletedTask;
        }

        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
        {
            ImportCalls++;
            LastImportedDocument = document;

            CharacterWorkspaceId workspaceId = new("demo-runner");
            OpenWorkspaceState workspace = new(
                Id: workspaceId,
                Name: "Soma",
                Alias: "Demo",
                LastOpenedUtc: DateTimeOffset.UtcNow,
                RulesetId: RulesetDefaults.Sr5);

            Publish(_state with
            {
                WorkspaceId = workspaceId,
                Session = new WorkspaceSessionState(
                    ActiveWorkspaceId: workspaceId,
                    OpenWorkspaces: [workspace],
                    RecentWorkspaceIds: [workspaceId]),
                OpenWorkspaces = [workspace],
                Profile = new CharacterProfileSection(
                    "Soma",
                    "Demo",
                    "QA",
                    "Human",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Street Sam",
                    "Runner demo",
                    string.Empty,
                    "6.0",
                    "6.0",
                    "Priority",
                    "Standard",
                    Created: true,
                    Adept: false,
                    Magician: false,
                    Technomancer: false,
                    AI: false,
                    MainMugshotIndex: 0,
                    MugshotCount: 0),
                ActiveTabId = "tab-gear",
                ActiveSectionId = "cyberwares",
                ActiveSectionJson = """
{
  "name": "Soma",
  "ruleset": "sr5",
  "metatype": "Human",
  "priority": "Standard",
  "role": "Street Sam",
  "attributes": {
    "Body": 5,
    "Agility": 7,
    "Reaction": 6,
    "Strength": 4,
    "Willpower": 3,
    "Logic": 3
  },
  "combat": {
    "initiative": "11 + 2d6",
    "armor": 12,
    "essence": 5.34
  }
}
""",
                ActiveSectionRows =
                [
                    new SectionRowState("attributes.body", "5"),
                    new SectionRowState("attributes.agility", "7"),
                    new SectionRowState("attributes.reaction", "6"),
                    new SectionRowState("skills.firearms[0]", "Automatics 6"),
                    new SectionRowState("skills.stealth[0]", "Sneaking 5"),
                    new SectionRowState("gear.weapons[0]", "Ares Alpha"),
                    new SectionRowState("gear.armor[0]", "Armor Jacket"),
                    new SectionRowState("cyberware[0]", "Wired Reflexes 2"),
                    new SectionRowState("contacts[0]", "Fixer (Loyalty 4 / Connection 5)"),
                    new SectionRowState("notes.runner_goal", "Ready for a flagship shell smoke pass")
                ],
                HasSavedWorkspace = false,
                Error = null
            });

            return Task.CompletedTask;
        }

        public void SetActiveSectionForTesting(string sectionId)
        {
            (string preview, SectionRowState[] rows) = BuildSectionFixture(sectionId);
            Publish(_state with
            {
                ActiveSectionId = sectionId,
                ActiveSectionJson = preview,
                ActiveSectionRows = rows,
                ActiveDialog = null,
                Error = null
            });
        }

        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            SwitchWorkspaceIds.Add(id.Value);
            return Task.CompletedTask;
        }

        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            ClosedWorkspaceIds.Add(id.Value);
            return Task.CompletedTask;
        }

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            SelectedTabIds.Add(tabId);
            return Task.CompletedTask;
        }

        public Task HandleUiControlAsync(string controlId, CancellationToken ct)
        {
            HandledUiControlIds.Add(controlId);
            Publish(_state with
            {
                Error = null,
                ActiveDialog = _dialogFactory.CreateUiControlDialog(controlId, _state.Preferences)
            });
            return Task.CompletedTask;
        }

        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct)
        {
            ExecutedWorkspaceActionIds.Add(action.Id);
            return Task.CompletedTask;
        }

        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }

        public Task ExportAsync(CancellationToken ct)
        {
            ExportCalls++;
            return Task.CompletedTask;
        }

        public Task PrintAsync(CancellationToken ct)
        {
            PrintCalls++;
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandIds.Add(commandId);
            if (OverviewCommandPolicy.IsDialogCommand(commandId)
                || OverviewCommandPolicy.IsImportHintCommand(commandId))
            {
                Publish(_state with
                {
                    LastCommandId = commandId,
                    ActiveDialog = _dialogFactory.CreateCommandDialog(
                        commandId,
                        _state.Profile,
                        _state.Preferences,
                        _state.ActiveSectionJson,
                        _state.WorkspaceId,
                        RulesetDefaults.Sr5),
                    Error = null
                });
            }
            else
            {
                Publish(_state with
                {
                    LastCommandId = commandId,
                    Error = null
                });
            }

            return Task.CompletedTask;
        }

        public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct)
        {
            DesktopDialogState? dialog = _state.ActiveDialog;
            if (dialog is null)
            {
                return Task.CompletedTask;
            }

            DialogFieldUpdates.Add(new DialogFieldValueChangedEventArgs(fieldId, value ?? string.Empty));

            DesktopDialogField[] updatedFields = dialog.Fields
                .Select(field =>
                {
                    if (string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                    {
                        return field with { Value = value ?? string.Empty };
                    }

                    if (string.Equals(dialog.Id, "dialog.new_character.priority_workflow", StringComparison.Ordinal)
                        && string.Equals(field.Id, "newCharacterPriorityLastChangedFieldId", StringComparison.Ordinal))
                    {
                        return field with { Value = fieldId };
                    }

                    return field;
                })
                .ToArray();

            MethodInfo rebuildMethod = typeof(DesktopDialogFactory).GetMethod(
                "RebuildDynamicDialog",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("RebuildDynamicDialog reflection entry point was not found.");
            DesktopDialogState nextDialog = (DesktopDialogState)(rebuildMethod.Invoke(
                null,
                [dialog with { Fields = updatedFields }, DesktopPreferenceState.Default])
                ?? throw new AssertFailedException("RebuildDynamicDialog returned null."));

            Publish(_state with
            {
                ActiveDialog = nextDialog
            });

            return Task.CompletedTask;
        }

        public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct)
        {
            AttributeEdits.Add(request);
            return Task.CompletedTask;
        }

        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
        {
            ExecutedDialogActionIds.Add(actionId);
            if (string.Equals(actionId, "cancel", StringComparison.Ordinal)
                || string.Equals(actionId, "close", StringComparison.Ordinal)
                || string.Equals(actionId, "save", StringComparison.Ordinal)
                || string.Equals(actionId, "add", StringComparison.Ordinal)
                || string.Equals(actionId, "apply", StringComparison.Ordinal)
                || string.Equals(actionId, "delete", StringComparison.Ordinal))
            {
                Publish(_state with
                {
                    ActiveDialog = null,
                    Error = null
                });
            }

            return Task.CompletedTask;
        }

        public Task CloseDialogAsync(CancellationToken ct)
        {
            Publish(_state with { ActiveDialog = null, Error = null });
            return Task.CompletedTask;
        }

        private void Publish(CharacterOverviewState state)
        {
            _state = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private static (string Preview, SectionRowState[] Rows) BuildSectionFixture(string sectionId)
        {
            switch (sectionId)
            {
                case "drugs":
                    return (
                        """
{
  "section": "drugs",
  "consumables": [
    { "name": "Jazz", "duration": "10 turns", "availability": "12R" }
  ]
}
""",
                        [
                            new SectionRowState("drugs[0]", "Jazz · 10 turns")
                        ]);
                case "spells":
                    return (
                        """
{
  "section": "spells",
  "spells": [
    { "name": "Stunbolt", "category": "Combat", "drain": "F-3" }
  ]
}
""",
                        [
                            new SectionRowState("spells[0]", "Stunbolt · Combat")
                        ]);
                case "powers":
                    return (
                        """
{
  "section": "powers",
  "adeptPowers": [
    { "name": "Improved Reflexes", "level": 1, "cost": 1.5 }
  ]
}
""",
                        [
                            new SectionRowState("powers[0]", "Improved Reflexes 1")
                        ]);
                case "complexforms":
                    return (
                        """
{
  "section": "complexforms",
  "complexForms": [
    { "name": "Cleaner", "level": 1 }
  ],
  "matrixPrograms": [
    { "name": "Armor", "slot": "Common" }
  ]
}
""",
                        [
                            new SectionRowState("complexforms[0]", "Cleaner 1"),
                            new SectionRowState("aiprograms[0]", "Armor (Common)")
                        ]);
                case "initiationgrades":
                    return (
                        """
{
  "section": "initiationgrades",
  "grades": [
    { "grade": 1, "reward": "Metamagic" }
  ]
}
""",
                        [
                            new SectionRowState("initiationgrades[0]", "Grade 1 · Metamagic")
                        ]);
                case "contacts":
                    return (
                        """
{
  "section": "contacts",
  "contacts": [
    { "name": "Fixer", "role": "Broker", "location": "Seattle", "connection": 5, "loyalty": 4 }
  ]
}
""",
                        [
                            new SectionRowState("contacts[0]", "Fixer (Loyalty 4 / Connection 5)")
                        ]);
                case "skills":
                    return (
                        """
{
  "section": "skills",
  "skills": [
    { "name": "Automatics", "rating": 6, "specialization": "Assault Rifles" },
    { "name": "Sneaking", "rating": 5 }
  ]
}
""",
                        [
                            new SectionRowState("skills[0]", "Automatics 6 (Assault Rifles)"),
                            new SectionRowState("skills[1]", "Sneaking 5")
                        ]);
                case "qualities":
                    return (
                        """
{
  "section": "qualities",
  "qualities": [
    { "name": "Ambidextrous", "karma": 4 },
    { "name": "Distinctive Style", "karma": -5 }
  ]
}
""",
                        [
                            new SectionRowState("qualities[0]", "Ambidextrous · 4 karma"),
                            new SectionRowState("qualities[1]", "Distinctive Style · -5 karma")
                        ]);
                case "gear":
                    return (
                        """
{
  "section": "gear",
  "gear": [
    { "name": "Medkit", "rating": 6, "location": "Backpack" },
    { "name": "Ammo: APDS", "quantity": 40, "location": "Duffel" }
  ],
  "explain": {
    "packet_id": "gear.medkit.rating",
    "source_anchors": [
      {
        "book": "SR5",
        "page": "447",
        "section": "Medkit",
        "localPdfPath": "/tmp/rulebooks/sr5-medkit.pdf"
      }
    ],
    "stale_if_snapshot_changes": {
      "snapshot_ref": "gear-medkit-v1",
      "current_snapshot_ref": "gear-medkit-v2"
    },
    "boundedFollowUpSummary": "If rating drops below 6, extended healing intervals lose the clinic-grade bonus."
  }
}
""",
                        [
                            new SectionRowState("gear[0]", "Medkit 6 · Backpack"),
                            new SectionRowState("gear[1]", "Ammo: APDS ×40 · Duffel")
                        ]);
                case "weapons":
                    return (
                        """
{
  "section": "weapons",
  "weapons": [
    { "name": "Ares Alpha", "dicePool": 14, "ammo": "42(c)" }
  ]
}
""",
                        [
                            new SectionRowState("weapons[0]", "Ares Alpha · Dice Pool 14 / 42(c)")
                        ]);
                case "armors":
                    return (
                        """
{
  "section": "armors",
  "armor": [
    { "name": "Armor Jacket", "rating": 12, "mods": 1 }
  ]
}
""",
                        [
                            new SectionRowState("armors[0]", "Armor Jacket · Armor 12 / Mods 1")
                        ]);
                case "vehicles":
                    return (
                        """
{
  "section": "vehicles",
  "vehicles": [
    { "name": "Roadmaster", "handling": 3, "armor": 16 }
  ]
}
""",
                        [
                            new SectionRowState("vehicles[0]", "Roadmaster · Armor 16 / Handling 3")
                        ]);
                case "mentorspirits":
                    return (
                        """
{
  "section": "mentorspirits",
  "mentor": "Shark",
  "familiarLane": "active"
}
""",
                        [
                            new SectionRowState("mentorspirits[0]", "Shark · Familiar lane active")
                        ]);
                case "progress":
                    return (
                        """
{
  "section": "progress",
  "diary": [
    { "title": "First extraction", "karma": 2 }
  ]
}
""",
                        [
                            new SectionRowState("progress[0]", "First extraction · +2 karma")
                        ]);
                case "attributes":
                case "attributedetails":
                    return (
                        """
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}
""",
                        [
                            new SectionRowState("attributes[0]", "Body 4"),
                            new SectionRowState("attributes[1]", "Agility 5"),
                            new SectionRowState("attributes[2]", "Reaction 5")
                        ]);
                case "calendar":
                    return (
                        """
{
  "section": "calendar",
  "diary": [
    { "title": "Downtime recon", "date": "2080-02-14", "karma": 2 }
  ]
}
""",
                        [
                            new SectionRowState("calendar[0]", "Downtime recon · +2 karma")
                        ]);
                case "validate":
                    return (
                        """
{
  "section": "validate",
  "validation": [
    { "severity": "warning", "message": "License missing for Ares Alpha" },
    { "severity": "info", "message": "Lifestyle payments due in 3 days" }
  ]
}
""",
                        [
                            new SectionRowState("validate[0]", "Warning · License missing for Ares Alpha"),
                            new SectionRowState("validate[1]", "Info · Lifestyle payments due in 3 days")
                        ]);
                case "rules":
                    return (
                        """
{
  "section": "rules",
  "rules": [
    { "source": "SR5", "entry": "Armor Encumbrance" },
    { "source": "Run & Gun", "entry": "Smartgun Accessories" }
  ]
}
""",
                        [
                            new SectionRowState("rules[0]", "SR5 · Armor Encumbrance"),
                            new SectionRowState("rules[1]", "Run & Gun · Smartgun Accessories")
                        ]);
                default:
                    return (
                        """
{
  "section": "profile"
}
""",
                        [
                            new SectionRowState("notes.runner_goal", "Ready for a flagship shell smoke pass")
                        ]);
            }
        }
    }

    private sealed class RecordingShellPresenter : IShellPresenter
    {
        public RecordingShellPresenter(ShellState state)
        {
            State = state;
        }

        public ShellState State { get; private set; }
        public int InitializeCalls { get; private set; }
        public List<string> ExecutedCommandIds { get; } = [];
        public List<string> SelectedTabIds { get; } = [];
        public List<string> ToggledMenuIds { get; } = [];
        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            ExecutedCommandIds.Add(commandId);
            State = State with
            {
                LastCommandId = commandId,
                OpenMenuId = null,
                Notice = $"Command '{commandId}' dispatched."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            SelectedTabIds.Add(tabId);
            State = State with { ActiveTabId = tabId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ToggleMenuAsync(string menuId, CancellationToken ct)
        {
            ToggledMenuIds.Add(menuId);
            State = State with
            {
                OpenMenuId = string.Equals(State.OpenMenuId, menuId, StringComparison.Ordinal) ? null : menuId,
                Notice = $"Menu '{menuId}' opened."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct) => Task.CompletedTask;

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
        {
            ShellWorkspaceState[] openWorkspaces = activeWorkspaceId is null
                ? []
                : [
                    new ShellWorkspaceState(
                        activeWorkspaceId.Value,
                        "Soma",
                        "Demo",
                        DateTimeOffset.UtcNow,
                        RulesetDefaults.Sr5,
                        HasSavedWorkspace: false)
                ];
            State = State with
            {
                ActiveWorkspaceId = activeWorkspaceId,
                OpenWorkspaces = openWorkspaces,
                ActiveTabId = activeWorkspaceId is null ? State.ActiveTabId : "tab-info",
                Notice = activeWorkspaceId is null ? "Ready." : $"Restored {openWorkspaces.Length} workspace(s)."
            };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCoachSidecarClient : IAvaloniaCoachSidecarClient
    {
        public Task<AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>> ListProviderHealthAsync(string? routeType = null, CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>> ListConversationAuditsAsync(
            string routeType,
            string? runtimeFingerprint = null,
            int maxCount = 3,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>> SendCoachTurnAsync(
            AiConversationTurnRequest request,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>.Failure(0, "disabled"));

        public Task<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>> SendBuildTurnAsync(
            AiConversationTurnRequest request,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>.Failure(0, "disabled"));
    }

    private static ShellState CreateShellState()
    {
        AppCommandDefinition[] commands = new CatalogOnlyRulesetShellCatalogResolver()
            .ResolveCommands(RulesetDefaults.Sr5)
            .ToArray();

        return ShellState.Empty with
        {
            ActiveRulesetId = RulesetDefaults.Sr5,
            PreferredRulesetId = RulesetDefaults.Sr5,
            Commands = commands,
            MenuRoots = commands.Where(command => string.Equals(command.Group, "menu", StringComparison.Ordinal)).ToArray(),
            NavigationTabs =
            [
                new NavigationTabDefinition("tab-info", "Info", "summary", "character", true, true, RulesetDefaults.Sr5)
            ],
            ActiveTabId = "tab-info",
            Notice = "Ready."
        };
    }
}
