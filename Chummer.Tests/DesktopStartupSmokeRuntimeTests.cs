using System.Text.Json;
using Chummer.Desktop.Runtime;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopStartupSmokeRuntimeTests
{
    [TestMethod]
    public async Task TryHandleAsync_returns_null_when_startup_smoke_switch_is_missing()
    {
        int? exitCode = await DesktopStartupSmokeRuntime.TryHandleAsync(
            "avalonia",
            [],
            CancellationToken.None).ConfigureAwait(false);

        Assert.IsNull(exitCode);
    }

    [TestMethod]
    public async Task TryHandleAsync_writes_receipt_when_requested()
    {
        string receiptPath = Path.Combine(Path.GetTempPath(), $"startup-smoke-{Guid.NewGuid():N}.json");
        string artifactDigest = $"sha256:{new string('a', 64)}";
        string? priorReceiptPath = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT");
        string? priorFailurePacketPath = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET");
        string? priorArtifactDigest = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST");
        string? priorHostClass = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS");
        string? priorCheckpoint = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT");

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT", receiptPath);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET", null);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST", artifactDigest);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS", "test-host");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT", "runtime_test_ready");

            int? exitCode = await DesktopStartupSmokeRuntime.TryHandleAsync(
                "avalonia",
                ["--startup-smoke"],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(File.Exists(receiptPath));

            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
            Assert.AreEqual("avalonia", receipt.RootElement.GetProperty("headId").GetString());
            Assert.AreEqual("test-host", receipt.RootElement.GetProperty("hostClass").GetString());
            Assert.AreEqual("runtime_test_ready", receipt.RootElement.GetProperty("readyCheckpoint").GetString());
            Assert.AreEqual(artifactDigest, receipt.RootElement.GetProperty("artifactDigest").GetString());
            Assert.AreEqual("environment", receipt.RootElement.GetProperty("artifactDigestSource").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(receipt.RootElement.GetProperty("platform").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(receipt.RootElement.GetProperty("arch").GetString()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT", priorReceiptPath);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET", priorFailurePacketPath);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST", priorArtifactDigest);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS", priorHostClass);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT", priorCheckpoint);
            if (File.Exists(receiptPath))
            {
                File.Delete(receiptPath);
            }
        }
    }

    [TestMethod]
    public async Task TryHandleAsync_returns_one_and_writes_failure_packet_when_force_crash_is_enabled()
    {
        string failurePacketPath = Path.Combine(Path.GetTempPath(), $"startup-smoke-failure-{Guid.NewGuid():N}.json");
        string? priorForceCrash = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FORCE_CRASH");
        string? priorFailurePacketPath = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET");
        string? priorArtifactDigest = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST");

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FORCE_CRASH", "true");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET", failurePacketPath);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST", new string('b', 64));
            int? exitCode = await DesktopStartupSmokeRuntime.TryHandleAsync(
                "avalonia",
                ["--startup-smoke"],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(1, exitCode);
            Assert.IsTrue(File.Exists(failurePacketPath));

            using JsonDocument packet = JsonDocument.Parse(File.ReadAllText(failurePacketPath));
            Assert.AreEqual("release_smoke_start_failure", packet.RootElement.GetProperty("signalClass").GetString());
            Assert.AreEqual("avalonia", packet.RootElement.GetProperty("headId").GetString());
            Assert.AreEqual("sha256:" + new string('b', 64), packet.RootElement.GetProperty("artifactDigest").GetString());
            Assert.AreEqual("environment", packet.RootElement.GetProperty("artifactDigestSource").GetString());
            Assert.AreEqual("freeze_or_fix_before_promotion", packet.RootElement.GetProperty("oodaRecommendation").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(packet.RootElement.GetProperty("crashFingerprint").GetString()));
            Assert.IsTrue(packet.RootElement.GetProperty("logTail").EnumerateArray().Any());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FORCE_CRASH", priorForceCrash);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET", priorFailurePacketPath);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST", priorArtifactDigest);
            if (File.Exists(failurePacketPath))
            {
                File.Delete(failurePacketPath);
            }
        }
    }

    [TestMethod]
    public async Task TryHandleAsync_returns_one_for_invalid_receipt_path_and_uses_explicit_failure_packet_fallback()
    {
        string invalidReceiptPath = "receipt.json";
        string failurePacketPath = Path.Combine(Path.GetTempPath(), $"startup-smoke-invalid-receipt-{Guid.NewGuid():N}.json");
        string? priorReceiptPath = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT");
        string? priorFailurePacketPath = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET");

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT", invalidReceiptPath);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET", failurePacketPath);
            int? exitCode = await DesktopStartupSmokeRuntime.TryHandleAsync(
                "avalonia",
                ["--startup-smoke"],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(1, exitCode);
            Assert.IsTrue(File.Exists(failurePacketPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT", priorReceiptPath);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET", priorFailurePacketPath);
            if (File.Exists(failurePacketPath))
            {
                File.Delete(failurePacketPath);
            }
        }
    }
}
