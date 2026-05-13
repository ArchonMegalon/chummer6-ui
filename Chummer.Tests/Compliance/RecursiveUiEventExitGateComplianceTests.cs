#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class RecursiveUiEventExitGateComplianceTests
{
    [TestMethod]
    public void Recursive_ui_event_exit_gate_is_wired_into_verify_and_release_proofs()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string releaseGateScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh"));
        string gateScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "recursive-ui-event-exit-gate.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));

        StringAssert.Contains(verifyScript, "checking recursive UI event exit gate");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/recursive-ui-event-exit-gate.sh");
        StringAssert.Contains(releaseGateScript, "RECURSIVE_UI_EVENT_EXIT_GATE.generated.json");
        StringAssert.Contains(gateScript, "\"chummer6-ui.recursive_ui_event_exit_gate\"");
        StringAssert.Contains(gateScript, "dialog-new-character");
        StringAssert.Contains(gateScript, "popup-file-menu");
        StringAssert.Contains(gateScript, "attributes-skills-skill-groups-specializations-knowledge-languages");
        StringAssert.Contains(projectText, "Compliance\\RecursiveUiEventExitGateComplianceTests.cs");
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Chummer.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
