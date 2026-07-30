#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M113GmPrepRosterSurfaceGuardTests
{
    [TestMethod]
    public void M113_gm_prep_roster_surface_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m113-ui-gm-prep-roster-surface-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(verifyScript, "checking next-90 M113 GM prep and roster movement desktop surface guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh");
        StringAssert.Contains(projectText, "Compliance\\Next90M113GmPrepRosterSurfaceGuardTests.cs");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m113-ui-gm-prep-roster-surface\"");
        StringAssert.Contains(scriptText, "TITLE = \"Add GM prep and roster movement surfaces to the desktop workspace\"");
        StringAssert.Contains(scriptText, "TASK = \"Add GM prep and roster movement surfaces to the primary desktop workspace route.\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"gm_prep_packets:desktop\"");
        StringAssert.Contains(scriptText, "\"roster_movement:desktop\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M113GmPrepRosterSurfaceGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"FullyQualifiedName~AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "CHUMMER_NEXT90_M113_REUSE_LOCAL_RELEASE_PROOF");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"ShowGmPrepAsync\"");
        StringAssert.Contains(scriptText, "\"ShowRosterMovementAsync\"");
        StringAssert.Contains(scriptText, "\"Runboard:\"");
        StringAssert.Contains(scriptText, "\"Next session:\"");
        StringAssert.Contains(scriptText, "\"Keep Local Work\"");
        StringAssert.Contains(scriptText, "\"Save Local Work\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopHomeWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"\\\"Open GM Prep Packets\\\"\"");
        StringAssert.Contains(scriptText, "\"\\\"Open Roster Movement\\\"\"");
        StringAssert.Contains(scriptText, "\"OpenGmPrepPacketsAsync\"");
        StringAssert.Contains(scriptText, "\"OpenRosterMovementAsync\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"\\\"Open Creator Publication\\\"\"");
        StringAssert.Contains(scriptText, "\"\\\"Review Moderation Flow\\\"\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/MainWindow.EventHandlers.cs\": [");
        StringAssert.Contains(scriptText, "\"ToolStrip_OnGmPrepRequested\"");
        StringAssert.Contains(scriptText, "\"ToolStrip_OnRosterMovementRequested\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/MainWindow.axaml.cs\": [");
        StringAssert.Contains(scriptText, "\"onGmPrepRequested: ToolStrip_OnGmPrepRequested,\"");
        StringAssert.Contains(scriptText, "\"onRosterMovementRequested: ToolStrip_OnRosterMovementRequested,\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/MainWindow.ControlBinding.cs\": [");
        StringAssert.Contains(scriptText, "\"AttachToolStripHandlers(toolStrip);\"");
        StringAssert.Contains(scriptText, "\"AttachToolStripHandlers(classicToolStrip);\"");
        StringAssert.Contains(scriptText, "\"surface.GmPrepRequested += onGmPrepRequested;\"");
        StringAssert.Contains(scriptText, "\"surface.RosterMovementRequested += onRosterMovementRequested;\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/MainWindow.ShellFrameProjector.cs\": [");
        StringAssert.Contains(scriptText, "\"ShowGmPrep: showSampleControls,\"");
        StringAssert.Contains(scriptText, "\"ShowRosterMovement: showSampleControls,\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/Controls/ToolStripControl.axaml.cs\": [");
        StringAssert.Contains(scriptText, "\"public event EventHandler? GmPrepRequested;\"");
        StringAssert.Contains(scriptText, "\"public event EventHandler? RosterMovementRequested;\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/App.axaml.cs\": [");
        StringAssert.Contains(scriptText, "\"DesktopStartupSurfaceCatalog.GmPrepPackets\"");
        StringAssert.Contains(scriptText, "\"DesktopStartupSurfaceCatalog.RosterMovement\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs\": [");
        StringAssert.Contains(scriptText, "\"public const string GmPrepPackets = \\\"gm_prep_packets\\\";\"");
        StringAssert.Contains(scriptText, "\"public const string RosterMovement = \\\"roster_movement\\\";\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Chummer.Tests.csproj\": [");
        StringAssert.Contains(scriptText, "\"Compliance\\\\Next90M113GmPrepRosterSurfaceGuardTests.cs\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs\": [");
        StringAssert.Contains(scriptText, "\"DesktopCampaignWorkspace_promotes_gm_prep_packets_and_roster_movement()\"");
        StringAssert.Contains(scriptText, "\"registry_has_m113_ui_task\"");
        StringAssert.Contains(scriptText, "\"queue_package_unique\"");
        StringAssert.Contains(scriptText, "\"design_queue_package_unique\"");
        StringAssert.Contains(scriptText, "\"queue_work_task_id_matches\"");
        StringAssert.Contains(scriptText, "\"design_queue_work_task_id_matches\"");
        StringAssert.Contains(scriptText, "\"allowed_paths_exact\"");
        StringAssert.Contains(scriptText, "\"owned_surfaces_exact\"");
        StringAssert.Contains(scriptText, "\"proofCommands\"");
        StringAssert.Contains(scriptText, "\"proofFiles\"");
        StringAssert.Contains(scriptText, "\"scripts/e2e-portal.sh\": [");
        StringAssert.Contains(scriptText, "\"NEXT90_M113_RECEIPT_PATH\"");
        StringAssert.Contains(scriptText, "\"\\\"desktop_workspace_routes\\\": [\"");
        StringAssert.Contains(scriptText, "\"local_release_proof_status_pass\"");
        StringAssert.Contains(scriptText, "\"local_release_proof_receipt_path_present\"");
        StringAssert.Contains(scriptText, "\"local_release_proof_package_present\"");
        StringAssert.Contains(scriptText, "\"local_release_proof_surfaces_present\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not failed else \"fail\"");
    }

    [TestMethod]
    public void M113_gm_prep_roster_surface_receipt_proves_desktop_workspace_route()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M113_UI_GM_PREP_ROSTER_SURFACE.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("next90-m113-ui-gm-prep-roster-surface", root.GetProperty("packageId").GetString());
        Assert.AreEqual("Add GM prep and roster movement surfaces to the desktop workspace", root.GetProperty("title").GetString());
        Assert.AreEqual("Add GM prep and roster movement surfaces to the primary desktop workspace route.", root.GetProperty("task").GetString());

        JsonElement checks = root.GetProperty("checks");
        Assert.IsTrue(checks.GetProperty("registry_has_m113_ui_task").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_package_unique").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_work_task_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_title_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_task_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_work_task_id_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_allowed_paths_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_path_matches").GetBoolean());
        Assert.IsTrue(checks.GetProperty("local_release_proof_status_pass").GetBoolean());
        Assert.IsTrue(checks.GetProperty("local_release_proof_receipt_path_present").GetBoolean());
        Assert.IsTrue(checks.GetProperty("local_release_proof_package_present").GetBoolean());
        Assert.IsTrue(checks.GetProperty("local_release_proof_surfaces_present").GetBoolean());

        JsonElement sourceChecks = root.GetProperty("sourceChecks");
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopHomeWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/MainWindow.EventHandlers.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/MainWindow.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/MainWindow.ControlBinding.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/MainWindow.ShellFrameProjector.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/Controls/ToolStripControl.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/App.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Chummer.Tests.csproj"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("scripts/e2e-portal.sh"));

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M113_UI_GM_PREP_ROSTER_SURFACE.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "UI_LOCAL_RELEASE_PROOF.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m113-ui-gm-prep-roster-surface-check.sh"),
                Path.Combine(repoRoot, "scripts", "e2e-portal.sh"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCampaignWorkspaceWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.EventHandlers.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.ControlBinding.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.ShellFrameProjector.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ToolStripControl.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Avalonia", "App.axaml.cs"),
                Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopStartupSurfaceCatalog.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"),
                Path.Combine(repoRoot, "Chummer.Tests", "Presentation", "AccessibilitySignoffSmokeTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M113GmPrepRosterSurfaceGuardTests.cs"),
            },
            ReadStringArray(root.GetProperty("proofFiles")));

        JsonElement proofCommands = root.GetProperty("proofCommands");
        Assert.AreEqual(
            "bash scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh",
            proofCommands.GetProperty("directProofCommand").GetString());
        Assert.AreEqual(
            "dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M113GmPrepRosterSurfaceGuardTests\" --no-restore",
            proofCommands.GetProperty("targetedTestCommand").GetString());
        Assert.AreEqual(
            "dotnet test --project Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"FullyQualifiedName~AccessibilitySignoffSmokeTests\" --no-restore",
            proofCommands.GetProperty("presentationTestCommand").GetString());
    }

    [TestMethod]
    public void M113_gm_prep_roster_surface_receipt_keeps_generatedAt_when_semantics_are_unchanged()
    {
        string repoRoot = FindRepoRoot();
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"m113-proof-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            string receiptPath = Path.Combine(tempDirectory, "NEXT90_M113_UI_GM_PREP_ROSTER_SURFACE.generated.json");
            string localReleaseProofPath = Path.Combine(tempDirectory, "UI_LOCAL_RELEASE_PROOF.generated.json");
            string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m113-ui-gm-prep-roster-surface-check.sh");
            WriteLocalReleaseProof(localReleaseProofPath, receiptPath);

            RunProofScript(repoRoot, scriptPath, receiptPath, localReleaseProofPath);
            string firstGeneratedAt = ReadGeneratedAt(receiptPath);

            RunProofScript(repoRoot, scriptPath, receiptPath, localReleaseProofPath);
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

    private static void RunProofScript(string repoRoot, string scriptPath, string receiptPath, string localReleaseProofPath)
    {
        ProcessStartInfo startInfo = new("bash", scriptPath)
        {
            WorkingDirectory = repoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.Environment["CHUMMER_NEXT90_M113_UI_RECEIPT_PATH"] = receiptPath;
        startInfo.Environment["CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH"] = localReleaseProofPath;
        startInfo.Environment["CHUMMER_NEXT90_M113_REUSE_LOCAL_RELEASE_PROOF"] = "1";

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the M113 proof script.");
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string stderr = process.StandardError.ReadToEnd();
            string stdout = process.StandardOutput.ReadToEnd();
            throw new AssertFailedException($"M113 proof script failed with exit code {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
        }
    }

    private static string ReadGeneratedAt(string receiptPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(receiptPath));
        return document.RootElement.GetProperty("generatedAt").GetString()
            ?? throw new AssertFailedException("Receipt missing generatedAt.");
    }

    private static void WriteLocalReleaseProof(string path, string receiptPath)
    {
        var payload = new Dictionary<string, object?>
        {
            ["contract_name"] = "chummer6-ui.local_release_proof",
            ["generated_at"] = "2026-06-20T00:00:00Z",
            ["status"] = "passed",
            ["route_probe_executed"] = true,
            ["desktop_workspace_routes"] = new[]
            {
                "gm_prep_packets:desktop",
                "roster_movement:desktop",
            },
            ["receipts"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["path"] = receiptPath,
                    ["package_id"] = "next90-m113-ui-gm-prep-roster-surface",
                    ["status"] = "pass",
                    ["surface_routes"] = new[]
                    {
                        "gm_prep_packets:desktop",
                        "roster_movement:desktop",
                    },
                },
            },
        };

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
