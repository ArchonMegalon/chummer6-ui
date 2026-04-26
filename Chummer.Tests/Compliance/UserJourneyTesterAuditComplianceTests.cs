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
        StringAssert.Contains(scriptText, "master_index_search_focus_stability");
        StringAssert.Contains(scriptText, "file_new_character_visible_workspace");
        StringAssert.Contains(scriptText, "minimal_character_build_save_reload");
        StringAssert.Contains(scriptText, "major_navigation_sanity");
        StringAssert.Contains(scriptText, "validation_or_export_smoke");
        StringAssert.Contains(scriptText, "focus_preserved_after_typing");
        StringAssert.Contains(scriptText, "new_character_action_opened_visible_workspace");
        StringAssert.Contains(scriptText, "tester_shard_id and fix_shard_id must both be present and different");
        StringAssert.Contains(scriptText, "used_internal_apis=false");
        StringAssert.Contains(scriptText, "PNG_SIGNATURE");
        StringAssert.Contains(scriptText, "CHUMMER_USER_JOURNEY_TESTER_RUN_LINUX_GATE");
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

        StringAssert.Contains(worklistText, "| B16 Adversarial user-journey tester gate | open |");
        StringAssert.Contains(worklistText, "| WL-221 | queued | P1 | Publish the adversarial Linux user-journey tester gate");
        StringAssert.Contains(worklistText, "Repo-local live queue: active (`WL-221`)");

        StringAssert.Contains(milestoneScriptText, "B16 user-journey tester milestone row");
        StringAssert.Contains(milestoneScriptText, "WL-221 runnable backlog entry");

        StringAssert.Contains(queueText, "package_id: ui-user-journey-tester-audit");
        StringAssert.Contains(queueText, "desktop_client:user_journey_tester");
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
