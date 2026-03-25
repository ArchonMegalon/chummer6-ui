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
        string? priorReceiptPath = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT");
        string? priorHostClass = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS");
        string? priorCheckpoint = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT");

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT", receiptPath);
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
            Assert.IsFalse(string.IsNullOrWhiteSpace(receipt.RootElement.GetProperty("platform").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(receipt.RootElement.GetProperty("arch").GetString()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT", priorReceiptPath);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS", priorHostClass);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT", priorCheckpoint);
            if (File.Exists(receiptPath))
            {
                File.Delete(receiptPath);
            }
        }
    }

    [TestMethod]
    public async Task TryHandleAsync_throws_when_force_crash_is_enabled()
    {
        string? priorForceCrash = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FORCE_CRASH");

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FORCE_CRASH", "true");
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => DesktopStartupSmokeRuntime.TryHandleAsync(
                "avalonia",
                ["--startup-smoke"],
                CancellationToken.None)).ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STARTUP_SMOKE_FORCE_CRASH", priorForceCrash);
        }
    }
}
