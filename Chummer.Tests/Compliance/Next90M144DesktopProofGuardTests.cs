#nullable enable annotations

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M144DesktopProofGuardTests
{
    [TestMethod]
    public void M144_cross_platform_desktop_proof_materializer_records_current_blockers_honestly()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m144-ui-startup-smoke-and-executable-gate-check.sh");
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json");

        RunScript(repoRoot, scriptPath);

        Assert.IsTrue(File.Exists(receiptPath), "M144 proof receipt should be materialized.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = document.RootElement;

        Assert.AreEqual("chummer6-ui.next90_m144_startup_smoke_and_executable_gate", root.GetProperty("contract_name").GetString());
        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        CollectionAssert.AreEqual(
            new[]
            {
                "avalonia:linux-x64:linux",
                "avalonia:win-x64:windows",
            },
            root.GetProperty("crossPlatformTupleGoals").EnumerateArray().Select(item => item.GetString()).ToArray());

        JsonElement proofs = root.GetProperty("proofs");
        JsonElement windows = FindTupleProof(proofs, "avalonia:win-x64:windows");
        JsonElement linux = FindTupleProof(proofs, "avalonia:linux-x64:linux");

        Assert.AreEqual(0, root.GetProperty("blockingFindings").GetArrayLength());

        Assert.IsTrue(windows.GetProperty("releaseChannelArtifactPresent").GetBoolean());
        Assert.IsTrue(windows.GetProperty("startupSmokeReceiptPresent").GetBoolean());
        Assert.IsTrue(windows.GetProperty("startupSmokeAcceptedAsIncompatibleHostSkip").GetBoolean());
        Assert.IsTrue(windows.GetProperty("startupSmokeVersionMatchesReleaseChannel").GetBoolean());
        Assert.IsTrue(windows.GetProperty("startupSmokeChannelMatchesReleaseChannel").GetBoolean());
        Assert.IsTrue(windows.GetProperty("executableGatePresent").GetBoolean());
        Assert.IsTrue(windows.GetProperty("executableGateVersionMatchesReleaseChannel").GetBoolean());

        Assert.IsTrue(linux.GetProperty("releaseChannelArtifactPresent").GetBoolean());
        Assert.IsTrue(linux.GetProperty("startupSmokeReceiptPresent").GetBoolean());
        Assert.IsTrue(linux.GetProperty("executableGatePresent").GetBoolean());
        Assert.IsTrue(linux.GetProperty("startupSmokeArtifactDigestMatchesLocalArtifact").GetBoolean());
    }

    private static JsonElement FindTupleProof(JsonElement proofs, string tupleId)
    {
        foreach (JsonElement proof in proofs.EnumerateArray())
        {
            if (string.Equals(proof.GetProperty("tupleId").GetString(), tupleId, StringComparison.Ordinal))
            {
                return proof;
            }
        }

        Assert.Fail($"Missing tuple proof row for {tupleId}.");
        return default;
    }

    private static void RunScript(string repoRoot, string scriptPath)
    {
        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/env",
            Arguments = $"bash \"{scriptPath}\"",
            WorkingDirectory = repoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Assert.Fail($"Script exited {process.ExitCode}.{Environment.NewLine}stdout:{Environment.NewLine}{stdout}{Environment.NewLine}stderr:{Environment.NewLine}{stderr}");
        }
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.Tests", "Chummer.Tests.csproj")))
            {
                return current;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
