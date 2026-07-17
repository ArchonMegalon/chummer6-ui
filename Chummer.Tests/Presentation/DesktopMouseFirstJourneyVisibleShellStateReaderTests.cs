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
            "Dossier: ws-123 (open: 1, saved)",
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
            "Dossier: ws-de (offen: 1, gespeichert)",
            "de-de");

        Assert.AreEqual("ws-de", state.WorkspaceId);
        Assert.AreEqual(1, state.OpenCount);
        Assert.IsTrue(state.IsSaved);
    }

    [TestMethod]
    public void ParseToolStripStatusState_reads_active_saved_workspace_snapshot()
    {
        ParsedWorkspaceStripState state = DesktopMouseFirstJourneyVisibleShellStateReader.ParseToolStripStatusState(
            "State: ready, dossier=ws-tool, open=1, saved=saved, last-command=save_character",
            "en-us");

        Assert.AreEqual("ws-tool", state.WorkspaceId);
        Assert.AreEqual(1, state.OpenCount);
        Assert.IsTrue(state.IsSaved);
    }

    [TestMethod]
    public void ParseToolStripStatusState_understands_localized_dossier_snapshot()
    {
        ParsedWorkspaceStripState state = DesktopMouseFirstJourneyVisibleShellStateReader.ParseToolStripStatusState(
            "Status: bereit, dossier=ws-de, offen=1, gespeichert=gespeichert, letzter-befehl=save_character",
            "de-de");

        Assert.AreEqual("ws-de", state.WorkspaceId);
        Assert.AreEqual(1, state.OpenCount);
        Assert.IsTrue(state.IsSaved);
    }

    [TestMethod]
    public void ParseToolStripStatusState_understands_japanese_and_chinese_dossier_snapshot_tokens()
    {
        ParsedWorkspaceStripState japaneseState = DesktopMouseFirstJourneyVisibleShellStateReader.ParseToolStripStatusState(
            "状態: ready, ドシエ=ws-jp, オープン=1, 保存=保存済み, 前回コマンド=save_character",
            "ja-jp");
        ParsedWorkspaceStripState chineseState = DesktopMouseFirstJourneyVisibleShellStateReader.ParseToolStripStatusState(
            "状态: ready, 档案=ws-cn, 已打开=1, 保存=已保存, 上一命令=save_character",
            "zh-cn");

        Assert.AreEqual("ws-jp", japaneseState.WorkspaceId);
        Assert.AreEqual(1, japaneseState.OpenCount);
        Assert.IsTrue(japaneseState.IsSaved);

        Assert.AreEqual("ws-cn", chineseState.WorkspaceId);
        Assert.AreEqual(1, chineseState.OpenCount);
        Assert.IsTrue(chineseState.IsSaved);
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
            toolStripStatusText: "State: ready, dossier=ws-visible, open=1, saved=saved, last-command=save_character",
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
