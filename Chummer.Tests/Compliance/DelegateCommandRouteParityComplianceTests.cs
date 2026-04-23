#nullable enable annotations

using System;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class DelegateCommandRouteParityComplianceTests
{
    [TestMethod]
    public void Delegate_command_route_guard_pins_full_head_delegate_surface()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "delegate-command-route-parity-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "DELEGATE_COMMAND_ROUTE_PARITY.generated.json");
        StringAssert.Contains(scriptText, "EXPECTED_CONTRACT_METHODS");
        StringAssert.Contains(scriptText, "\"UpdateMetadataAsync\"");
        StringAssert.Contains(scriptText, "\"SaveAsync\"");
        StringAssert.Contains(scriptText, "\"ExportAsync\"");
        StringAssert.Contains(scriptText, "\"PrintAsync\"");
        StringAssert.Contains(scriptText, "Name~delegates_to_presenter");
        StringAssert.Contains(scriptText, "Name~CoordinateAsync_");
        StringAssert.Contains(scriptText, "Name~Avalonia_and_Blazor_");
        StringAssert.Contains(scriptText, "ExecuteCommandAsync_all_catalog_commands_are_handled");
        StringAssert.Contains(scriptText, "HandleUiControlAsync_all_catalog_controls_are_non_generic");
        StringAssert.Contains(scriptText, "Avalonia_and_Blazor_shell_surfaces_expose_identical_ids");
        StringAssert.Contains(scriptText, "_commandDispatcher.DispatchAsync(commandId, context, ct);");
        StringAssert.Contains(scriptText, "_dialogCoordinator.CoordinateAsync(actionId, context, ct);");
        StringAssert.Contains(scriptText, "ActiveDialog = _dialogFactory.CreateUiControlDialog(controlId, State.Preferences)");
        StringAssert.Contains(scriptText, "updatedDialog = DesktopDialogFactory.RebuildDynamicDialog(updatedDialog, State.Preferences);");
        StringAssert.Contains(scriptText, "Delegate-route parity guard is not wired into scripts/ai/verify.sh.");
        StringAssert.Contains(scriptText, "\"scripts/ai/test.sh\"");
        StringAssert.Contains(scriptText, "contract_surface_reasons");
        StringAssert.Contains(scriptText, "bridge_adapter_reasons");
        StringAssert.Contains(scriptText, "lifecycle_event_reasons");
        StringAssert.Contains(scriptText, "presenter_route_reasons");
        StringAssert.Contains(scriptText, "dual_head_reasons");
        StringAssert.Contains(scriptText, "verify_wiring_reasons");
        StringAssert.Contains(scriptText, "execution_reasons");
        StringAssert.Contains(scriptText, "\"contractSurfaceReview\"");
        StringAssert.Contains(scriptText, "\"bridgeAdapterReview\"");
        StringAssert.Contains(scriptText, "\"lifecycleEventReview\"");
        StringAssert.Contains(scriptText, "\"presenterRouteReview\"");
        StringAssert.Contains(scriptText, "\"dualHeadParityReview\"");
        StringAssert.Contains(scriptText, "\"verifyWiringReview\"");
        StringAssert.Contains(scriptText, "\"executionReview\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not contract_surface_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not bridge_adapter_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not lifecycle_event_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not presenter_route_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not dual_head_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not verify_wiring_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not execution_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "evidence[\"failureCount\"] = len(reasons)", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Delegate_command_route_guard_stays_in_standard_verify_path()
    {
        string repoRoot = FindRepoRoot();
        string verifyPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyText = File.ReadAllText(verifyPath);

        StringAssert.Contains(verifyText, "checking delegate and command-route parity guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/delegate-command-route-parity-check.sh");
    }

    [TestMethod]
    public void Delegate_command_route_receipt_records_passed_delegate_parity_surface()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "DELEGATE_COMMAND_ROUTE_PARITY.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.delegate_command_route_parity", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual(16, evidence.GetProperty("contractMethodCount").GetInt32());
        Assert.AreEqual(16, evidence.GetProperty("bridgeMethodCount").GetInt32());
        Assert.AreEqual(16, evidence.GetProperty("adapterMethodCount").GetInt32());
        Assert.IsTrue(evidence.GetProperty("wiredIntoStandardVerify").GetBoolean());
        Assert.AreEqual(0, evidence.GetProperty("reasonCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());

        JsonElement reviews = root.GetProperty("reviews");
        Assert.AreEqual("pass", reviews.GetProperty("contractSurfaceReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", reviews.GetProperty("executionReview").GetProperty("status").GetString());

        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"UpdateMetadataAsync\"");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"SaveAsync\"");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"ExportAsync\"");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"PrintAsync\"");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"buildExitCode\": 0");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"Name~delegates_to_presenter\"");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"Name~CoordinateAsync_\"");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"Name~Avalonia_and_Blazor_\"");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"exitCode\": 0");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"noMatches\": false");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"Avalonia_and_Blazor_dialog_workflow_keeps_shell_regions_in_parity\": true");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"CoordinateAsync_apply_ruleset_calls_delegate_and_closes_dialog_on_success\": true");
        StringAssert.Contains(receipt.RootElement.GetRawText(), "\"ExecuteCommandAsync_all_catalog_commands_are_handled\": true");
    }

    private static string FindRepoRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Could not locate Chummer.sln from the current test directory.");
        return string.Empty;
    }
}
