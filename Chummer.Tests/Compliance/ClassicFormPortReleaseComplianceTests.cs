#nullable enable annotations

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class ClassicFormPortReleaseComplianceTests
{
    [TestMethod]
    public void Classic_form_port_bridge_requires_real_command_bindings()
    {
        string repoRoot = FindRepoRoot();
        string bridgePath = Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "ClassicFormPortViewModelBridge.cs");
        string bridgeText = File.ReadAllText(bridgePath);

        Assert.IsFalse(
            bridgeText.Contains("ClassicFormPortActionCommands? commands = null", System.StringComparison.Ordinal),
            "Classic FormPort release surfaces must not default to null command bindings.");
        Assert.IsFalse(
            bridgeText.Contains("commands ??=", System.StringComparison.Ordinal),
            "Classic FormPort release surfaces must not silently downgrade to fallback command bindings.");
        Assert.IsFalse(
            bridgeText.Contains("ClassicFormPortActionCommands.NoOp", System.StringComparison.Ordinal),
            "Classic FormPort release surfaces must not expose a NoOp command fallback.");
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
