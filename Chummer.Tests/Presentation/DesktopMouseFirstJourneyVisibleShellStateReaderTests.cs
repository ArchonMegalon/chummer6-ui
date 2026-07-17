using Chummer.Presentation.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopMouseFirstJourneyVisibleShellStateReaderTests
{
    [TestMethod]
    public void ParseWorkspaceStripState_reads_active_saved_workspace_in_default_locale()
    {
        ParsedWorkspaceStripState state = DesktopMouseFirstJourneyVisibleShellStateReader.ParseWorkspaceStripState(
            "Workspace: ws-123 (open: 1, saved)",
            "en-us");

        Assert.AreEqual("ws-123", state.WorkspaceId);
        Assert.AreEqual(1, state.OpenCount);
        Assert.IsTrue(state.IsSaved);
        Assert.IsTrue(state.HasActiveWorkspace);
    }

    [TestMethod]
    public void ParseWorkspaceStripState_understands_localized_saved_state()
    {
        ParsedWorkspaceStripState state = DesktopMouseFirstJourneyVisibleShellStateReader.ParseWorkspaceStripState(
            "Arbeitsbereich: ws-de (offen: 1, gespeichert)",
            "de-de");

        Assert.AreEqual("ws-de", state.WorkspaceId);
        Assert.AreEqual(1, state.OpenCount);
        Assert.IsTrue(state.IsSaved);
    }

    [TestMethod]
    public void ParseToolStripStatusState_reads_active_saved_workspace_snapshot()
    {
        ParsedWorkspaceStripState state = DesktopMouseFirstJourneyVisibleShellStateReader.ParseToolStripStatusState(
            "State: ready, workspace=ws-tool, open=1, saved=saved, last-command=save_character",
            "en-us");

        Assert.AreEqual("ws-tool", state.WorkspaceId);
        Assert.AreEqual(1, state.OpenCount);
        Assert.IsTrue(state.IsSaved);
    }

    [TestMethod]
    public void IsCharacterLoaded_matches_localized_loaded_status()
    {
        Assert.IsTrue(DesktopMouseFirstJourneyVisibleShellStateReader.IsCharacterLoaded("Character: loaded", "en-us"));
        Assert.IsTrue(DesktopMouseFirstJourneyVisibleShellStateReader.IsCharacterLoaded("Charakter: geladen", "de-de"));
        Assert.IsFalse(DesktopMouseFirstJourneyVisibleShellStateReader.IsCharacterLoaded("Character: none", "en-us"));
    }

    [TestMethod]
    public void ParseRulesetId_reads_file_extension_from_compliance_summary()
    {
        Assert.AreEqual("sr4", DesktopMouseFirstJourneyVisibleShellStateReader.ParseRulesetId("Ruleset: Shadowrun 4 .chum4 | Workflows: 5 defs / 6 surfaces | Prefs: 100%/classic/en-us"));
        Assert.AreEqual("sr5", DesktopMouseFirstJourneyVisibleShellStateReader.ParseRulesetId("Ruleset: Shadowrun 5 .chum5 | Workflows: 5 defs / 6 surfaces | Prefs: 100%/classic/en-us"));
        Assert.AreEqual("sr6", DesktopMouseFirstJourneyVisibleShellStateReader.ParseRulesetId("Ruleset: Shadowrun 6 .chum6 | Workflows: 5 defs / 6 surfaces | Prefs: 100%/classic/en-us"));
        Assert.IsNull(DesktopMouseFirstJourneyVisibleShellStateReader.ParseRulesetId("Ruleset: loading"));
    }

    [TestMethod]
    public void Read_prefers_toolstrip_status_when_workspace_strip_is_not_yet_visible()
    {
        DesktopMouseFirstJourneyVisibleShellState state = DesktopMouseFirstJourneyVisibleShellStateReader.Read(
            workspaceStripText: string.Empty,
            toolStripStatusText: "State: ready, workspace=ws-visible, open=1, saved=saved, last-command=save_character",
            characterStateText: "Character: loaded",
            complianceStateText: "Ruleset: Shadowrun 5 .chum5 | Workflows: 5 defs / 6 surfaces | Prefs: 100%/classic/en-us",
            language: "en-us");

        Assert.AreEqual("ws-visible", state.WorkspaceId);
        Assert.AreEqual(1, state.OpenCount);
        Assert.IsTrue(state.IsSaved);
        Assert.IsTrue(state.CharacterLoaded);
        Assert.AreEqual("sr5", state.RulesetId);
    }

    [TestMethod]
    public void Read_treats_visible_loaded_character_status_as_active_workspace_evidence()
    {
        DesktopMouseFirstJourneyVisibleShellState state = DesktopMouseFirstJourneyVisibleShellStateReader.Read(
            workspaceStripText: string.Empty,
            toolStripStatusText: string.Empty,
            characterStateText: "Character: loaded",
            complianceStateText: "Ruleset: Shadowrun 5 .chum5 | Workflows: 5 defs / 6 surfaces | Prefs: 100%/classic/en-us",
            language: "en-us");

        Assert.IsNull(state.WorkspaceId);
        Assert.IsTrue(state.CharacterLoaded);
        Assert.IsTrue(state.HasActiveWorkspace);
        Assert.AreEqual("sr5", state.RulesetId);
    }
}
