#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M112CampaignMemoryGuardTests
{
    [TestMethod]
    public void M112_campaign_memory_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m112-ui-campaign-memory-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(verifyScript, "checking next-90 M112 campaign memory and return-loop desktop guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m112-ui-campaign-memory-check.sh");
        StringAssert.Contains(projectText, "Compliance\\Next90M112CampaignMemoryGuardTests.cs");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m112-ui-campaign-memory\"");
        StringAssert.Contains(scriptText, "TITLE = \"Surface campaign memory and consequences on desktop\"");
        StringAssert.Contains(scriptText, "TASK = \"Make campaign consequences, stale state, and next-session return actions visible on the promoted desktop route.\"");
        StringAssert.Contains(scriptText, "owner: chummer6-ui");
        StringAssert.Contains(scriptText, "EXPECTED_ALLOWED_PATHS = [");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "EXPECTED_SURFACES = [");
        StringAssert.Contains(scriptText, "\"campaign_workspace:memory\"");
        StringAssert.Contains(scriptText, "\"campaign_return_loop:desktop\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m112-ui-campaign-memory-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M112CampaignMemoryGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"FullyQualifiedName~AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_DESIGN_QUEUE_PATH = \"/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml\"");
        StringAssert.Contains(scriptText, "\"scripts/ai/verify.sh\": [");
        StringAssert.Contains(scriptText, "\"checking next-90 M112 campaign memory and return-loop desktop guard\"");
        StringAssert.Contains(scriptText, "\"bash scripts/ai/milestones/next90-m112-ui-campaign-memory-check.sh\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Chummer.Tests.csproj\": [");
        StringAssert.Contains(scriptText, "\"Compliance\\\\Next90M112CampaignMemoryGuardTests.cs\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopHomeWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"BuildCampaignConsequenceVisibilitySummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignMemoryVisibilitySummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignConsequenceEvidenceSummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignNextSessionReturnActionSummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignStaleStateVisibilitySummary()\"");
        StringAssert.Contains(scriptText, "\"CreateCampaignMemoryActions()\"");
        StringAssert.Contains(scriptText, "\"ResolveCampaignMemoryEvidence()\"");
        StringAssert.Contains(scriptText, "\"\\\"Review Campaign Memory\\\"\"");
        StringAssert.Contains(scriptText, "\"OpenWorkspaceSupport\"");
        StringAssert.Contains(scriptText, "\"OpenCurrentWorkspace\"");
        StringAssert.Contains(scriptText, "\"OpenDevicesAccessWindowAsync\"");
        StringAssert.Contains(scriptText, "\"Review campaign consequences\"");
        StringAssert.Contains(scriptText, "\"Review next-session return\"");
        StringAssert.Contains(scriptText, "\"Campaign consequence proof:\"");
        StringAssert.Contains(scriptText, "\"Campaign memory stale-state check:\"");
        StringAssert.Contains(scriptText, "\"Next-session return actions:\"");
        StringAssert.Contains(scriptText, "\"Stale state: server continuity is unavailable\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"BuildCampaignConsequenceVisibilitySummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignMemoryVisibilitySummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignNextSessionReturnActionSummary()\"");
        StringAssert.Contains(scriptText, "\"BuildRestoreStaleStateVisibilitySummary()\"");
        StringAssert.Contains(scriptText, "\"BuildRestoreConflictChoiceSummary()\"");
        StringAssert.Contains(scriptText, "\"CreateReadinessActions()\"");
        StringAssert.Contains(scriptText, "\"CreateRestoreActions()\"");
        StringAssert.Contains(scriptText, "\"ResolveCampaignMemorySummary()\"");
        StringAssert.Contains(scriptText, "\"ResolveCampaignMemoryReturnSummary()\"");
        StringAssert.Contains(scriptText, "\"ResolveCampaignMemoryEvidence()\"");
        StringAssert.Contains(scriptText, "\"\\\"Open Rule Environment Studio\\\"\"");
        StringAssert.Contains(scriptText, "\"OpenWorkspaceSupport\"");
        StringAssert.Contains(scriptText, "\"OpenDevicesAccessWindowAsync\"");
        StringAssert.Contains(scriptText, "\"Campaign memory stale-state check:\"");
        StringAssert.Contains(scriptText, "\"Next-session return actions:\"");
        StringAssert.Contains(scriptText, "\"Stale state: server continuity is unavailable\"");
        StringAssert.Contains(scriptText, "\"Conflict choices:\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs\": [");
        StringAssert.Contains(scriptText, "\"DesktopHome_promotes_campaign_memory_and_return_actions()\"");
        StringAssert.Contains(scriptText, "\"DesktopCampaignWorkspace_keeps_restore_conflict_choices_visible()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignConsequenceEvidenceSummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignNextSessionReturnActionSummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignStaleStateVisibilitySummary()\"");
        StringAssert.Contains(scriptText, "\"\\\"Review Campaign Memory\\\"\"");
        StringAssert.Contains(scriptText, "\"OpenWorkspaceSupport\"");
        StringAssert.Contains(scriptText, "\"OpenCurrentWorkspace\"");
        StringAssert.Contains(scriptText, "\"OpenDevicesAccessWindowAsync\"");
        StringAssert.Contains(scriptText, "\"Campaign memory stale-state check:\"");
        StringAssert.Contains(scriptText, "\"Conflict choices:\"");
        StringAssert.Contains(scriptText, "\"registry_has_m112_ui_task\"");
        StringAssert.Contains(scriptText, "\"queue_package_unique\"");
        StringAssert.Contains(scriptText, "\"design_queue_package_unique\"");
        StringAssert.Contains(scriptText, "\"allowed_paths_exact\"");
        StringAssert.Contains(scriptText, "\"owned_surfaces_exact\"");
        StringAssert.Contains(scriptText, "\"design_queue_path_matches\"");
        StringAssert.Contains(scriptText, "\"proofCommands\"");
        StringAssert.Contains(scriptText, "\"proofFiles\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not failed else \"fail\"");
        StringAssert.Contains(scriptText, "comparable_receipt.pop(\"generatedAt\", None)");
        StringAssert.Contains(scriptText, "receipt[\"generatedAt\"] = existing_receipt[\"generatedAt\"]");
        StringAssert.Contains(scriptText, "json.dumps(receipt, indent=2, sort_keys=True)");
    }

    [TestMethod]
    public void M112_campaign_memory_receipt_proves_desktop_route()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M112_UI_CAMPAIGN_MEMORY.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("next90-m112-ui-campaign-memory", root.GetProperty("packageId").GetString());
        Assert.AreEqual("Surface campaign memory and consequences on desktop", root.GetProperty("title").GetString());
        Assert.AreEqual("Make campaign consequences, stale state, and next-session return actions visible on the promoted desktop route.", root.GetProperty("task").GetString());

        JsonElement checks = root.GetProperty("checks");
        Assert.IsTrue(checks.GetProperty("registry_has_m112_ui_task").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_path_matches").GetBoolean());

        JsonElement sourceChecks = root.GetProperty("sourceChecks");
        AssertSourceMarkersPass(sourceChecks.GetProperty("scripts/ai/verify.sh"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Chummer.Tests.csproj"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopHomeWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M112_UI_CAMPAIGN_MEMORY.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m112-ui-campaign-memory-check.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "verify.sh"),
                Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M112CampaignMemoryGuardTests.cs"),
            },
            ReadStringArray(root.GetProperty("proofFiles")));

        JsonElement proofCommands = root.GetProperty("proofCommands");
        Assert.AreEqual(
            "bash scripts/ai/milestones/next90-m112-ui-campaign-memory-check.sh",
            proofCommands.GetProperty("directProofCommand").GetString());
        Assert.AreEqual(
            "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M112CampaignMemoryGuardTests\" --no-restore",
            proofCommands.GetProperty("targetedTestCommand").GetString());
        Assert.AreEqual(
            "dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"FullyQualifiedName~AccessibilitySignoffSmokeTests\" --no-restore",
            proofCommands.GetProperty("presentationTestCommand").GetString());
    }

    [TestMethod]
    public void M112_campaign_memory_receipt_keeps_generatedAt_when_semantics_are_unchanged()
    {
        string repoRoot = FindRepoRoot();
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"m112-proof-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string receiptPath = Path.Combine(tempDirectory, "NEXT90_M112_UI_CAMPAIGN_MEMORY.generated.json");
            string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m112-ui-campaign-memory-check.sh");

            RunProofScript(repoRoot, scriptPath, receiptPath);
            string firstGeneratedAt = ReadGeneratedAt(receiptPath);

            RunProofScript(repoRoot, scriptPath, receiptPath);
            string secondGeneratedAt = ReadGeneratedAt(receiptPath);

            Assert.AreEqual(firstGeneratedAt, secondGeneratedAt);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static void AssertSourceMarkersPass(JsonElement sourceChecks)
    {
        foreach (JsonProperty markerCheck in sourceChecks.EnumerateObject())
        {
            Assert.IsTrue(markerCheck.Value.GetBoolean(), $"Expected source marker to pass: {markerCheck.Name}");
        }
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Could not locate repository root from deployment directory.");
        return string.Empty;
    }

    private static string[] ReadStringArray(JsonElement element)
    {
        List<string> values = new();
        foreach (JsonElement item in element.EnumerateArray())
        {
            string? value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private static void RunProofScript(string repoRoot, string scriptPath, string receiptPath)
    {
        ProcessStartInfo startInfo = new("bash", scriptPath)
        {
            WorkingDirectory = repoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.Environment["CHUMMER_NEXT90_M112_UI_RECEIPT_PATH"] = receiptPath;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the M112 proof script.");
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string stderr = process.StandardError.ReadToEnd();
            string stdout = process.StandardOutput.ReadToEnd();
            throw new AssertFailedException($"M112 proof script failed with exit code {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
        }
    }

    private static string ReadGeneratedAt(string receiptPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(receiptPath));
        return document.RootElement.GetProperty("generatedAt").GetString()
            ?? throw new AssertFailedException("Receipt missing generatedAt.");
    }
}
