#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M142DirectWorkflowProofGuardTests
{
    [TestMethod]
    public void M142_direct_workflow_proof_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string guardScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m142-ui-direct-workflow-proof-check.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));

        StringAssert.Contains(verifyScript, "checking next-90 M142 direct workflow proof guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh");

        StringAssert.Contains(guardScript, "PACKAGE_ID = \"next90-m142-ui-close-direct-screenshot-and-runtime-proof-for-dense-builder-and-career-fl\"");
        StringAssert.Contains(guardScript, "FRONTIER_ID = 9095697868");
        StringAssert.Contains(guardScript, "WORK_TASK_ID = \"142.1\"");
        StringAssert.Contains(guardScript, "EXPECTED_STATUS = \"complete\"");
        StringAssert.Contains(guardScript, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(guardScript, "M142 chummer6-ui dense builder/career, dice/initiative, and contacts/lifestyles/notes direct proof is complete;");
        StringAssert.Contains(guardScript, "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json");
        StringAssert.Contains(guardScript, "next90-m142-ui-direct-workflow-proof-check.sh");
        StringAssert.Contains(guardScript, "Next90M142DirectWorkflowProofGuardTests");
        StringAssert.Contains(guardScript, "\"family:dense_builder_and_career_workflows\"");
        StringAssert.Contains(guardScript, "\"family:dice_initiative_and_table_utilities\"");
        StringAssert.Contains(guardScript, "\"family:identity_contacts_lifestyles_history\"");
        StringAssert.Contains(guardScript, "\"menu:dice_roller_or_workflow:initiative_screenshot\"");
        StringAssert.Contains(guardScript, "\"10-contacts-section-light.png\"");
        StringAssert.Contains(guardScript, "\"11-diary-dialog-light.png\"");
        StringAssert.Contains(guardScript, "\"14-advancement-dialog-light.png\"");
        StringAssert.Contains(guardScript, "/docker/chummercomplete/chummer-core-engine/docs/NEXT90_M142_DENSE_WORKBENCH_RECEIPTS.md");

        StringAssert.Contains(projectText, "Compliance\\Next90M142DirectWorkflowProofGuardTests.cs");
    }

    [TestMethod]
    public void M142_direct_workflow_proof_receipt_proves_route_local_family_coverage()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual(0, root.GetProperty("unresolved").GetArrayLength());
        Assert.AreEqual("chummer6-ui.next90_m142_ui_direct_workflow_proof", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m142-ui-close-direct-screenshot-and-runtime-proof-for-dense-builder-and-career-fl", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(9095697868, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(142, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("142.1", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W22P", evidence.GetProperty("wave").GetString());
        Assert.AreEqual("chummer6-ui", evidence.GetProperty("repo").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "close_direct_screenshot_and_runtime_proof_for_dense_buil:ui" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement queueChecks = evidence.GetProperty("queueChecks");
        AssertAllBooleansAreTrue(queueChecks);

        JsonElement familyChecks = evidence.GetProperty("familyChecks");
        AssertAllBooleansAreTrue(familyChecks.GetProperty("family:dense_builder_and_career_workflows"));
        AssertAllBooleansAreTrue(familyChecks.GetProperty("family:dice_initiative_and_table_utilities"));
        AssertAllBooleansAreTrue(familyChecks.GetProperty("family:identity_contacts_lifestyles_history"));

        JsonElement receiptChecks = evidence.GetProperty("receiptChecks");
        AssertAllBooleansAreTrue(receiptChecks);

        JsonElement sourceChecks = evidence.GetProperty("sourceChecks");
        foreach (JsonProperty sourceCheck in sourceChecks.EnumerateObject())
        {
            AssertAllBooleansAreTrue(sourceCheck.Value);
        }

        string publishedRepoRoot = Directory.Exists(Path.Combine(Directory.GetParent(repoRoot)?.FullName ?? repoRoot, "chummer6-ui"))
            ? Path.Combine(Directory.GetParent(repoRoot)?.FullName ?? repoRoot, "chummer6-ui")
            : repoRoot;
        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(publishedRepoRoot, "Chummer.Tests", "Compliance", "Next90M142DirectWorkflowProofGuardTests.cs"),
                Path.Combine(publishedRepoRoot, "Chummer.Tests", "Chummer.Tests.csproj"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "milestones", "next90-m142-ui-direct-workflow-proof-check.sh"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "milestones", "chummer5a-screenshot-review-gate.sh"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh"),
                Path.Combine(publishedRepoRoot, "scripts", "ai", "verify.sh"),
                Path.Combine(publishedRepoRoot, ".codex-studio", "published", "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"),
                Path.Combine(publishedRepoRoot, ".codex-studio", "published", "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"),
                Path.Combine(publishedRepoRoot, ".codex-studio", "published", "DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"),
                Path.Combine(publishedRepoRoot, ".codex-studio", "published", "SECTION_HOST_RULESET_PARITY.generated.json"),
                Path.Combine(publishedRepoRoot, ".codex-studio", "published", "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json"),
                Path.Combine(publishedRepoRoot, ".codex-studio", "published", "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));
        CollectionAssert.AreEquivalent(
            new[]
            {
                "bash scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh",
                "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M142DirectWorkflowProofGuardTests\" --no-restore",
            },
            ReadStringArray(evidence.GetProperty("proofCommands")));
    }

    private static void AssertAllBooleansAreTrue(JsonElement element)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.True:
                    break;
                case JsonValueKind.Object:
                    AssertAllBooleansAreTrue(property.Value);
                    break;
                default:
                    Assert.Fail($"Expected '{property.Name}' to be true.");
                    break;
            }
        }
    }

    private static string[] ReadStringArray(JsonElement element)
    {
        var values = new List<string>();
        foreach (JsonElement item in element.EnumerateArray())
        {
            values.Add(item.GetString() ?? string.Empty);
        }

        return values.ToArray();
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")) || Directory.Exists(Path.Combine(current, "scripts")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Unable to locate repository root.");
        return string.Empty;
    }
}
