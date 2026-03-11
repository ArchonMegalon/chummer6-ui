#nullable enable annotations

using System;
using System.Collections.Generic;
using Chummer.Application.Content;
using Chummer.Contracts.Content;
using Chummer.Contracts.Rulesets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public class RuntimeFingerprintServiceTests
{
    [TestMethod]
    public void Runtime_fingerprint_service_tracks_asset_checksums_and_provider_bindings()
    {
        DefaultRuntimeFingerprintService service = new();
        ContentBundleDescriptor[] bundles =
        [
            new(
                BundleId: "official.sr5.base",
                RulesetId: RulesetDefaults.Sr5,
                Version: "schema-5",
                Title: "SR5 Base",
                Description: "Built-in base content.",
                AssetPaths: ["data/", "lang/"])
        ];

        string checksumFingerprint = service.ComputeResolvedRuntimeFingerprint(
            RulesetDefaults.Sr5,
            bundles,
            [CreateRulePack("house-rules", "1.0.0", "sha256:abc")],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["validate.character"] = "house-rules/validate.character"
            },
            "rulepack-v1");
        string changedChecksumFingerprint = service.ComputeResolvedRuntimeFingerprint(
            RulesetDefaults.Sr5,
            bundles,
            [CreateRulePack("house-rules", "1.0.0", "sha256:def")],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["validate.character"] = "house-rules/validate.character"
            },
            "rulepack-v1");
        string changedBindingFingerprint = service.ComputeResolvedRuntimeFingerprint(
            RulesetDefaults.Sr5,
            bundles,
            [CreateRulePack("house-rules", "1.0.0", "sha256:abc")],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["validate.character"] = "house-rules/validate.character.alt"
            },
            "rulepack-v1");

        Assert.AreNotEqual(checksumFingerprint, changedChecksumFingerprint);
        Assert.AreNotEqual(checksumFingerprint, changedBindingFingerprint);
    }

    [TestMethod]
    public void Runtime_fingerprint_service_is_deterministic_across_input_order()
    {
        DefaultRuntimeFingerprintService service = new();
        ContentBundleDescriptor bundleA = new(
            BundleId: "official.sr5.base",
            RulesetId: RulesetDefaults.Sr5,
            Version: "schema-5",
            Title: "SR5 Base",
            Description: "Built-in base content.",
            AssetPaths: ["lang/", "data/"]);
        ContentBundleDescriptor bundleB = new(
            BundleId: "campaign.seattle.assets",
            RulesetId: RulesetDefaults.Sr5,
            Version: "2026.03",
            Title: "Seattle Assets",
            Description: "Campaign bundle.",
            AssetPaths: ["media/", "data/"]);
        RulePackRegistryEntry packA = CreateRulePack("house-rules", "1.0.0", "sha256:abc");
        RulePackRegistryEntry packB = CreateRulePack("gm-overrides", "2.0.0", "sha256:def");

        string fingerprintA = service.ComputeResolvedRuntimeFingerprint(
            RulesetDefaults.Sr5,
            [bundleA, bundleB],
            [packA, packB],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["content.catalog"] = "gm-overrides/content.catalog",
                ["validate.character"] = "house-rules/validate.character"
            },
            "rulepack-v1");
        string fingerprintB = service.ComputeResolvedRuntimeFingerprint(
            RulesetDefaults.Sr5,
            [bundleB, bundleA],
            [packB, packA],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["validate.character"] = "house-rules/validate.character",
                ["content.catalog"] = "gm-overrides/content.catalog"
            },
            "rulepack-v1");

        Assert.AreEqual(fingerprintA, fingerprintB);
    }

    private static RulePackRegistryEntry CreateRulePack(string packId, string version, string checksum)
    {
        return new RulePackRegistryEntry(
            new RulePackManifest(
                PackId: packId,
                Version: version,
                Title: $"{packId} title",
                Author: "GM",
                Description: "Runtime pack.",
                Targets: [RulesetDefaults.Sr5],
                EngineApiVersion: "rulepack-v1",
                DependsOn: [],
                ConflictsWith: [],
                Visibility: ArtifactVisibilityModes.LocalOnly,
                TrustTier: ArtifactTrustTiers.LocalOnly,
                Assets:
                [
                    new RulePackAssetDescriptor(
                        Kind: RulePackAssetKinds.Xml,
                        Mode: RulePackAssetModes.MergeCatalog,
                        RelativePath: $"data/{packId}.xml",
                        Checksum: checksum)
                ],
                Capabilities:
                [
                    new RulePackCapabilityDescriptor(
                        CapabilityId: RulePackCapabilityIds.ContentCatalog,
                        AssetKind: RulePackAssetKinds.Xml,
                        AssetMode: RulePackAssetModes.MergeCatalog)
                ],
                ExecutionPolicies: []),
            new RulePackPublicationMetadata(
                OwnerId: "local-single-user",
                Visibility: ArtifactVisibilityModes.LocalOnly,
                PublicationStatus: RulePackPublicationStatuses.Published,
                Review: new RulePackReviewDecision(RulePackReviewStates.NotRequired),
                Shares: []),
            new ArtifactInstallState(ArtifactInstallStates.Installed));
    }
}
