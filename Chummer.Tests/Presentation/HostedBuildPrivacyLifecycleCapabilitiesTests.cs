#nullable enable annotations

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Chummer.Blazor.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class HostedBuildPrivacyLifecycleCapabilitiesTests
{
    [TestMethod]
    public void V2_file_contract_exposes_only_observed_lifecycle_facts_and_stays_review_required()
    {
        HostedBuildPrivacyLifecycleSnapshot snapshot =
            HostedBuildPrivacyLifecycleCapabilities.Instance.Current;

        Assert.AreEqual(HostedBuildPrivacyLifecycleCapabilities.ContractName, snapshot.ContractName);
        Assert.AreEqual(HostedBuildPrivacyLifecycleCapabilities.ContractVersion, snapshot.ContractVersion);
        Assert.AreEqual(HostedBuildPrivacyLifecycleCapabilities.ReviewRequiredStatus, snapshot.Status);
        Assert.IsTrue(snapshot.ReviewRequired);
        Assert.IsTrue(snapshot.BlocksLaunch);
        CollectionAssert.AreEqual(
            new[]
            {
                HostedBuildPrivacyLifecycleCapabilities.ActiveRecordDelete,
                HostedBuildPrivacyLifecycleCapabilities.MemoryOnlyRecovery,
                HostedBuildPrivacyLifecycleCapabilities.NoDeleteReplay,
                HostedBuildPrivacyLifecycleCapabilities.NoOwnerErasure,
                HostedBuildPrivacyLifecycleCapabilities.ProductionRecoveryUnverified
            },
            snapshot.Facts.Select(static fact => fact.Id).ToArray());
    }

    [TestMethod]
    public void V2_postgres_contract_reports_shipped_replay_without_claiming_account_erasure()
    {
        HostedBuildPrivacyLifecycleSnapshot snapshot =
            new HostedBuildPrivacyLifecycleCapabilities(
                new HostedBuildWorkspaceStoreSelection(
                    "postgresql",
                    MultiInstanceSafe: true,
                    "shared_transactional_postgresql")).Current;

        CollectionAssert.AreEqual(
            new[]
            {
                HostedBuildPrivacyLifecycleCapabilities.ActiveRecordDelete,
                HostedBuildPrivacyLifecycleCapabilities.AtomicDeletionJournal,
                HostedBuildPrivacyLifecycleCapabilities.AutomaticDeletionReplay,
                HostedBuildPrivacyLifecycleCapabilities.OwnerWorkspaceErasure,
                HostedBuildPrivacyLifecycleCapabilities.MemoryOnlyRecovery,
                HostedBuildPrivacyLifecycleCapabilities.ProductionRecoveryUnverified
            },
            snapshot.Facts.Select(static fact => fact.Id).ToArray());

        CollectionAssert.AreEqual(
            new[]
            {
                HostedBuildPrivacyLifecycleCapabilities.PermanentDeleteClaim,
                HostedBuildPrivacyLifecycleCapabilities.DurableRecoveryClaim,
                HostedBuildPrivacyLifecycleCapabilities.AccountErasureClaim
            },
            snapshot.ProhibitedClaims.ToArray());

        string disclosure = string.Join(
            " ",
            snapshot.Facts.Select(static fact => $"{fact.Label} {fact.Disclosure}").Prepend(snapshot.Summary));
        StringAssert.Contains(disclosure, "content-free receipt");
        StringAssert.Contains(disclosure, "Memory-only");
        StringAssert.Contains(disclosure, "replayed before");
        StringAssert.Contains(disclosure, "not whole-account erasure");
        StringAssert.Contains(disclosure, "not yet proved");
        Assert.IsFalse(disclosure.Contains("permanently deletes", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(disclosure.Contains("durable recovery is available", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(disclosure.Contains("erase your account", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Run_services_launch_gate_machine_contract_matches_v2_postgres_capability_authority()
    {
        HostedBuildPrivacyLifecycleSnapshot snapshot =
            new HostedBuildPrivacyLifecycleCapabilities(
                new HostedBuildWorkspaceStoreSelection(
                    "postgresql",
                    MultiInstanceSafe: true,
                    "shared_transactional_postgresql")).Current;
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(ResolveRunServicesPrivacyLaunchGatePath()));
        JsonElement root = document.RootElement;

        Assert.AreEqual("chummer.privacy_launch_gate", root.GetProperty("contractName").GetString());
        Assert.AreEqual(2, root.GetProperty("contractVersion").GetInt32());
        Assert.AreEqual(snapshot.ContractName, root.GetProperty("capabilityContractName").GetString());
        Assert.AreEqual(snapshot.ContractVersion, root.GetProperty("capabilityContractVersion").GetInt32());
        Assert.AreEqual(snapshot.Status, root.GetProperty("status").GetString());
        Assert.AreEqual(snapshot.ReviewRequired, root.GetProperty("reviewRequired").GetBoolean());
        Assert.AreEqual(snapshot.BlocksLaunch, root.GetProperty("blocksLaunch").GetBoolean());
        CollectionAssert.AreEqual(
            snapshot.Facts.Select(static fact => fact.Id).ToArray(),
            JsonStrings(root, "facts"));
        CollectionAssert.AreEqual(
            snapshot.ProhibitedClaims.ToArray(),
            JsonStrings(root, "prohibitedClaims"));
        CollectionAssert.AreEqual(
            new[]
            {
                "flagship_launch",
                "public_release_supportability",
                "hosted_build_recovery_and_erasure"
            },
            JsonStrings(root, "blockedClaims"));
    }

    private static string[] JsonStrings(JsonElement root, string propertyName) =>
        root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(static item => item.GetString() ?? string.Empty)
            .ToArray();

    private static string ResolveRunServicesPrivacyLaunchGatePath()
    {
        string? configuredRoot = Environment.GetEnvironmentVariable("CHUMMER_RUN_SERVICES_ROOT");
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            Assert.Inconclusive(
                "Cross-repository privacy compatibility was not requested; set CHUMMER_RUN_SERVICES_ROOT to run the proof lane.");
            return string.Empty;
        }

        string configuredPath = Path.Combine(
            configuredRoot,
            ".codex-design",
            "product",
            "PRIVACY_LAUNCH_GATE.json");
        return File.Exists(configuredPath)
            ? configuredPath
            : throw new AssertFailedException(
                $"CHUMMER_RUN_SERVICES_ROOT does not contain the privacy launch-gate contract: {configuredPath}");
    }
}
