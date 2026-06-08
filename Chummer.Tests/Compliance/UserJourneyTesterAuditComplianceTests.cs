#nullable enable annotations

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class UserJourneyTesterAuditComplianceTests
{
    [TestMethod]
    public void User_journey_tester_audit_gate_is_fail_closed_and_workflow_backed()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "user-journey-tester-audit.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "chummer6-ui.user_journey_tester_audit");
        StringAssert.Contains(scriptText, "chummer6-ui.user_journey_tester_trace");
        StringAssert.Contains(scriptText, "USER_JOURNEY_TESTER_AUDIT.generated.json");
        StringAssert.Contains(scriptText, "UI_LINUX_DESKTOP_EXIT_GATE.generated.json");
        StringAssert.Contains(scriptText, "UI_FLAGSHIP_RELEASE_GATE.generated.json");
        StringAssert.Contains(scriptText, "master_index_search_focus_stability");
        StringAssert.Contains(scriptText, "file_new_character_visible_workspace");
        StringAssert.Contains(scriptText, "minimal_character_build_save_reload");
        StringAssert.Contains(scriptText, "mouse_first_journey");
        StringAssert.Contains(scriptText, "mouse_first_live_binary");
        StringAssert.Contains(scriptText, "Linux mouse_first_journey primary receipt must publish five screenshot-backed review frames.");
        StringAssert.Contains(scriptText, "Linux mouse_first_journey primary receipt must publish observed input events.");
        StringAssert.Contains(scriptText, "Linux mouse_first_journey primary receipt must fail closed on combo selection fallback.");
        StringAssert.Contains(scriptText, "Linux mouse_first_journey primary receipt must fail closed on forced combo dropdown open.");
        StringAssert.Contains(scriptText, "Linux mouse_first_journey primary receipt directTextMutationCount must be zero.");
        StringAssert.Contains(scriptText, "Linux mouse_first_journey primary receipt must publish a tracePath.");
        StringAssert.Contains(scriptText, "\"linux_gate_mouse_first_journey_screenshot_count\"");
        StringAssert.Contains(scriptText, "major_navigation_sanity");
        StringAssert.Contains(scriptText, "validation_or_export_smoke");
        StringAssert.Contains(scriptText, "focus_preserved_after_typing");
        StringAssert.Contains(scriptText, "new_character_action_opened_visible_workspace");
        StringAssert.Contains(scriptText, "starter_attributes_match_seeded_workspace");
        StringAssert.Contains(scriptText, "section_preview_omits_review_copy");
        StringAssert.Contains(scriptText, "runtimeBackedNewCharacterFileWorkflow");
        StringAssert.Contains(scriptText, "Runtime_backed_new_character_starter_attributes_match_seeded_workspace_and_omit_review_copy");
        StringAssert.Contains(scriptText, "tester_shard_id and fix_shard_id must both be present and different");
        StringAssert.Contains(scriptText, "used_internal_apis=false");
        StringAssert.Contains(scriptText, "PNG_SIGNATURE");
        StringAssert.Contains(scriptText, "MIN_SCREENSHOT_BYTES = 1024");
        StringAssert.Contains(scriptText, "screenshot is too small to count as credible review evidence");
        StringAssert.Contains(scriptText, "CHUMMER_USER_JOURNEY_TESTER_RUN_LINUX_GATE");
        StringAssert.Contains(scriptText, "linux_gate_temp_path=\"\"");
        StringAssert.Contains(scriptText, "trap cleanup EXIT");
        StringAssert.Contains(scriptText, "if [[ -z \"${CHUMMER_USER_JOURNEY_TESTER_LINUX_GATE_PATH:-}\" ]]; then");
        StringAssert.Contains(scriptText, "linux_gate_temp_path=\"$(mktemp)\"");
        StringAssert.Contains(scriptText, "CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH=\"$linux_gate_path\" \\");
    }

    [TestMethod]
    public void Mouse_first_live_binary_runner_uses_file_menu_save_instead_of_internal_save_shortcut()
    {
        string repoRoot = FindRepoRoot();
        string runnerPath = Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopMouseFirstJourneyRunner.cs");
        string runnerText = File.ReadAllText(runnerPath);

        StringAssert.Contains(runnerText, "ClickFileMenuCommandAsync(window, \"save_character\"");
        StringAssert.Contains(runnerText, "WriteObservedInputTrace");
        StringAssert.Contains(runnerText, "ObservedInputTraceCollector");
        Assert.IsFalse(
            runnerText.Contains("SaveWorkspaceForAutomationAsync", System.StringComparison.Ordinal),
            "The live-binary mouse-first journey should save through the visible shell command route, not the internal save helper.");
        Assert.IsFalse(
            runnerText.Contains("textBox.Text =", System.StringComparison.Ordinal),
            "The live-binary mouse-first journey should use routed text input instead of direct TextBox mutation.");
        Assert.IsFalse(
            runnerText.Contains("Button.ClickEvent", System.StringComparison.Ordinal),
            "The live-binary mouse-first journey should fail closed instead of forcing button click events.");
        Assert.IsFalse(
            runnerText.Contains("IsSubMenuOpen = true", System.StringComparison.Ordinal),
            "The live-binary mouse-first journey should fail closed instead of forcing submenu visibility.");
        Assert.IsFalse(
            runnerText.Contains("PeekDialogWindowForTesting", System.StringComparison.Ordinal),
            "The live-binary mouse-first journey should resolve the visible dialog window instead of relying on a test-only dialog hook.");
        Assert.IsFalse(
            runnerText.Contains("SnapshotStateForAutomation", System.StringComparison.Ordinal),
            "The live-binary mouse-first journey should prove completion from visible shell state instead of internal adapter snapshots.");
        StringAssert.Contains(runnerText, "TextInputEventArgs");
        StringAssert.Contains(runnerText, "InputElement.TextInputEvent");
        StringAssert.Contains(runnerText, "ReadWorkspaceStripTextAsync");
        StringAssert.Contains(runnerText, "WorkspaceText");
        StringAssert.Contains(runnerText, "ReadComplianceStateText");
        StringAssert.Contains(runnerText, "ReadVisibleShellState(window, language)");
        StringAssert.Contains(runnerText, "ResolveVisibleDialogWindow()");
        StringAssert.Contains(runnerText, "HasOpenedCharacterEvidence(window, language, expectedCharacterName, expectedCharacterAlias, expectedRulesetId)");
        StringAssert.Contains(runnerText, "ReadWindowTextSnapshot(window)");
        StringAssert.Contains(runnerText, "DesktopMouseFirstJourneyVisibleShellStateReader.Read(");
    }

    [TestMethod]
    public void User_journey_tester_audit_gate_stays_in_standard_verify_and_queue_truth()
    {
        string repoRoot = FindRepoRoot();
        string verifyText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string worklistText = File.ReadAllText(Path.Combine(repoRoot, "WORKLIST.md"));
        string milestoneScriptText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "ui-milestone-coverage-check.sh"));
        string queueText = File.ReadAllText(Path.Combine(repoRoot, ".codex-studio", "published", "QUEUE.generated.yaml"));

        StringAssert.Contains(verifyText, "checking adversarial Linux user-journey tester audit");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/user-journey-tester-audit.sh");

        StringAssert.Contains(worklistText, "| B16 Adversarial user-journey tester gate | done |");
        StringAssert.Contains(worklistText, "| WL-221 | done | P1 | Publish the adversarial Linux user-journey tester gate");
        StringAssert.Contains(queueText, "items: []");
        StringAssert.Contains(queueText, "source_queue_fingerprint:");

        StringAssert.Contains(milestoneScriptText, "B16 user-journey tester milestone row");
        StringAssert.Contains(milestoneScriptText, "WL-221 runnable backlog entry");

        Assert.IsFalse(queueText.Contains("package_id: ui-user-journey-tester-audit", System.StringComparison.Ordinal));
        Assert.IsFalse(queueText.Contains("desktop_client:user_journey_tester", System.StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        string directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "Chummer.sln")) &&
                Directory.Exists(Path.Combine(directory, "scripts")))
            {
                return directory;
            }

            string? parent = Directory.GetParent(directory)?.FullName;
            if (string.Equals(parent, directory, System.StringComparison.Ordinal))
            {
                break;
            }

            directory = parent ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
