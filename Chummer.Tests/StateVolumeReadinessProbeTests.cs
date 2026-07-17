#nullable enable

using Chummer.Api.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class StateVolumeReadinessProbeTests
{
    [TestMethod]
    public void Private_writable_state_volume_passes_a_write_read_delete_round_trip()
    {
        string root = CreatePrivateDirectory();
        try
        {
            StateVolumeReadinessResult result = CreateProbe(root).Check();

            Assert.IsTrue(result.IsReady);
            Assert.AreEqual("ready", result.Reason);
            Assert.HasCount(0, Directory.GetFiles(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Missing_state_volume_fails_closed()
    {
        string root = Path.Combine(Path.GetTempPath(), $"chummer-missing-state-{Guid.NewGuid():N}");

        StateVolumeReadinessResult result = CreateProbe(root).Check();

        Assert.IsFalse(result.IsReady);
        Assert.AreEqual("state_volume_missing", result.Reason);
    }

    [TestMethod]
    public void Non_private_state_volume_permissions_fail_closed_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreatePrivateDirectory();
        try
        {
            File.SetUnixFileMode(
                root,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);

            StateVolumeReadinessResult result = CreateProbe(root).Check();

            Assert.IsFalse(result.IsReady);
            Assert.AreEqual("state_volume_permissions_not_private", result.Reason);
        }
        finally
        {
            File.SetUnixFileMode(
                root,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, recursive: true);
        }
    }

    private static StateVolumeReadinessProbe CreateProbe(string root)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [StateVolumeReadinessProbe.StatePathConfigurationKey] = root
            })
            .Build();
        return new StateVolumeReadinessProbe(configuration);
    }

    private static string CreatePrivateDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), $"chummer-state-readiness-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                root,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return root;
    }
}
