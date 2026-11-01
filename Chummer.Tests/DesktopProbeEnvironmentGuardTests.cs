#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

/// <summary>
/// Probe-scoped environment isolation coverage: a Windows persistence-probe head must scope
/// (clear) store-steering inherited variables before its DI host is built, and the command-line
/// arming must be opt-in so ordinary desktop launches keep their inherited environment.
/// </summary>
[TestClass]
public class DesktopProbeEnvironmentGuardTests
{
    private static readonly object EnvironmentLock = new();

    [TestMethod]
    public void Probe_command_line_detection_is_opt_in()
    {
        Assert.IsTrue(DesktopProbeEnvironmentGuard.IsProbeCommandLine(new[] { "--windows-persistence-probe" }));
        Assert.IsTrue(DesktopProbeEnvironmentGuard.IsProbeCommandLine(new[] { "--other", "--windows-persistence-probe", "x" }));
        Assert.IsFalse(DesktopProbeEnvironmentGuard.IsProbeCommandLine(new[] { "--windows-persistence-probe-v2" }));
        Assert.IsFalse(DesktopProbeEnvironmentGuard.IsProbeCommandLine(new string[] { }));
        Assert.IsFalse(DesktopProbeEnvironmentGuard.IsProbeCommandLine(null));
    }

    [TestMethod]
    public void Scope_clears_only_hostile_variables_and_reports_what_it_cleared()
    {
        lock (EnvironmentLock)
        {
            string uniqueMarker = $"chummer-test-{Guid.NewGuid():N}";
            try
            {
                // Seed every hostile variable plus a benign control variable.
                Environment.SetEnvironmentVariable("CHUMMER_STATE_PATH", Path.Combine(Path.GetTempPath(), uniqueMarker, "foreign"));
                Environment.SetEnvironmentVariable("CHUMMER_CLIENT_MODE", "http");
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_CLIENT_MODE", "http");
                Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", "http://127.0.0.1:9");
                Environment.SetEnvironmentVariable("CHUMMER_API_KEY", "leaked-key");
                Environment.SetEnvironmentVariable("CHUMMER_UNRELATED_TEST_VARIABLE", "keep-me");

                IReadOnlyList<string> cleared = DesktopProbeEnvironmentGuard.Scope(DesktopProbeEnvironmentGuard.HostileVariables);

                string[] allHostile =
                {
                    "CHUMMER_STATE_PATH",
                    "CHUMMER_CLIENT_MODE",
                    "CHUMMER_DESKTOP_CLIENT_MODE",
                    "CHUMMER_API_BASE_URL",
                    "CHUMMER_API_KEY"
                };
                CollectionAssert.AreEquivalent(allHostile, new List<string>(cleared));
                foreach (string name in DesktopProbeEnvironmentGuard.HostileVariables)
                {
                    Assert.IsNull(Environment.GetEnvironmentVariable(name), $"{name} should have been cleared.");
                }

                // The benign control variable must survive the guard.
                Assert.AreEqual("keep-me", Environment.GetEnvironmentVariable("CHUMMER_UNRELATED_TEST_VARIABLE"));
                // The scoped record must match what was cleared: the probe's negative gate reads it.
                CollectionAssert.AreEquivalent(allHostile, new List<string>(DesktopProbeEnvironmentGuard.LastScopedVariables));
            }
            finally
            {
                foreach (string name in DesktopProbeEnvironmentGuard.HostileVariables)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }
                Environment.SetEnvironmentVariable("CHUMMER_UNRELATED_TEST_VARIABLE", null);
                // Reset the guard's record (a Scope over a clean environment clears it).
                DesktopProbeEnvironmentGuard.Scope(DesktopProbeEnvironmentGuard.HostileVariables);
            }
        }
    }

    [TestMethod]
    public void Scope_is_noop_on_a_clean_environment()
    {
        lock (EnvironmentLock)
        {
            try
            {
                foreach (string name in DesktopProbeEnvironmentGuard.HostileVariables)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }

                IReadOnlyList<string> cleared = DesktopProbeEnvironmentGuard.Scope(DesktopProbeEnvironmentGuard.HostileVariables);

                Assert.AreEqual(0, cleared.Count);
                // A clean-environment scope must clear the record so a subsequent probe run in the
                // same process is not poisoned by an earlier hostile reading.
                Assert.AreEqual(0, DesktopProbeEnvironmentGuard.LastScopedVariables.Count);
            }
            finally
            {
                foreach (string name in DesktopProbeEnvironmentGuard.HostileVariables)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }
                // Reset the guard's record (a Scope over a clean environment clears it).
                DesktopProbeEnvironmentGuard.Scope(DesktopProbeEnvironmentGuard.HostileVariables);
            }
        }
    }
}
