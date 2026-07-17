#nullable enable annotations

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class PortalPlaySurfaceHorizonReaderTests
{
    [TestMethod]
    public void Read_returns_missing_summary_when_receipt_is_not_published()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            PlaySurfaceHorizonSummary summary = PortalPlaySurfaceHorizonReader.Read(tempRoot);

            Assert.AreEqual("missing", summary.Status);
            Assert.AreEqual("unpublished", summary.ContractName);
            Assert.AreEqual(PortalPlaySurfaceHorizonReader.PublicRelativePath, summary.ReceiptRelativePath);
            Assert.AreEqual("unknown", summary.CurrentExecutionScope);
            Assert.AreEqual(string.Empty, summary.PublicEntryRoute);
            Assert.AreEqual(string.Empty, summary.CompatibilityRouteBase);
            Assert.AreEqual(0, summary.Horizons.Count);
            StringAssert.Contains(summary.Summary, "has not been synced");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Read_projects_deployed_horizon_execution_scope_and_receipt_counts()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            string receiptPath = Path.Combine(
                tempRoot,
                "release-evidence",
                "browser-lane",
                "BLAZOR_PLAY_SURFACE_HORIZON.generated.json");
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
            File.WriteAllText(
                receiptPath,
                """
                {
                  "contract_name": "chummer6-ui.blazor_play_surface_horizon",
                  "status": "passed",
                  "generated_at": "2026-07-02T20:50:00Z",
                  "scope": "Hosted browser lane is proven; session and living-world utilities stay staged until the public edge proves them.",
                  "current_release_truth": {
                    "smoke_execution_scope": "playwright-smoke",
                    "public_entry_route": "/app",
                    "public_roster_entry_route": "/app?command=character_roster",
                    "public_blazor_root_route": "/blazor/",
                    "hosted_app_route": "/blazor/app",
                    "compatibility_route_base": "/blazor/workbench",
                    "execution_route_base": "/blazor/workbench"
                  },
                  "horizons": [
                    {
                      "id": "near_term_stabilization",
                      "title": "Near-Term Stabilization",
                      "status": "proven",
                      "evidence_tier": "runtime_proven",
                      "headline": "Hosted browser execution is already proven.",
                      "summary": "The public browser lane is live and verified.",
                      "runtime_proven_receipts": [
                        {
                          "id": "browser_lane_proof_set",
                          "label": "Aggregate browser-lane proof set",
                          "status": "passed",
                          "public_download_relative_path": "release-evidence/browser-lane/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"
                        },
                        {
                          "id": "pwa_public_edge",
                          "label": "Hosted /blazor PWA shell",
                          "status": "passed",
                          "public_download_relative_path": "release-evidence/browser-lane/BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json"
                        }
                      ],
                      "source_staged_receipts": [
                        {
                          "id": "touch_mobile_staged",
                          "label": "Touch/mobile session utility",
                          "status": "passed",
                          "public_download_relative_path": "release-evidence/browser-lane/BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.generated.json"
                        }
                      ],
                      "unproven_claims": [
                        "mobile browser execution parity",
                        "campaign persistence"
                      ],
                      "server_bound_boundaries": [
                        "runner data",
                        "session state"
                      ],
                      "documentation_sources": [
                        {
                          "id": "table_pulse_showcase",
                          "label": "Table Pulse flagship showcase",
                          "path": "/repo/docs/TABLE_PULSE_FLAGSHIP_SHOWCASE.md",
                          "status": "passed"
                        },
                        {
                          "id": "table_pulse_minigames",
                          "label": "Table Pulse remote reaction minigames",
                          "path": "/repo/docs/TABLE_PULSE_REMOTE_REACTION_MINIGAMES.md",
                          "status": "passed"
                        }
                      ]
                    }
                  ]
                }
                """);

            PlaySurfaceHorizonSummary summary = PortalPlaySurfaceHorizonReader.Read(tempRoot);

            Assert.AreEqual("passed", summary.Status);
            Assert.AreEqual("chummer6-ui.blazor_play_surface_horizon", summary.ContractName);
            Assert.AreEqual("2026-07-02T20:50:00Z", summary.GeneratedAt);
            Assert.AreEqual("playwright-smoke", summary.CurrentExecutionScope);
            Assert.AreEqual("/app", summary.PublicEntryRoute);
            Assert.AreEqual("/app?command=character_roster", summary.PublicRosterEntryRoute);
            Assert.AreEqual("/blazor/", summary.PublicBlazorRootRoute);
            Assert.AreEqual("/blazor/app", summary.HostedAppRoute);
            Assert.AreEqual("/blazor/workbench", summary.CompatibilityRouteBase);
            Assert.AreEqual("/blazor/workbench", summary.ExecutionRouteBase);
            Assert.AreEqual(1, summary.Horizons.Count);
            Assert.AreEqual(
                "Hosted browser lane is proven; session and living-world utilities stay staged until the public edge proves them.",
                summary.Summary);

            PlaySurfaceHorizonItem horizon = summary.Horizons[0];
            Assert.AreEqual("near_term_stabilization", horizon.Id);
            Assert.AreEqual("Near-Term Stabilization", horizon.Title);
            Assert.AreEqual("proven", horizon.Status);
            Assert.AreEqual("runtime_proven", horizon.EvidenceTier);
            Assert.AreEqual("Hosted browser execution is already proven.", horizon.Headline);
            Assert.AreEqual("The public browser lane is live and verified.", horizon.Summary);
            Assert.AreEqual(2, horizon.RuntimeProvenReceiptCount);
            Assert.AreEqual(1, horizon.SourceStagedReceiptCount);
            Assert.AreEqual(2, horizon.DocumentationSourceCount);
            CollectionAssert.AreEqual(
                new[] { "mobile browser execution parity", "campaign persistence" },
                horizon.UnprovenClaims.ToArray());
            CollectionAssert.AreEqual(
                new[] { "runner data", "session state" },
                horizon.ServerBoundBoundaries.ToArray());
            Assert.AreEqual("browser_lane_proof_set", horizon.RuntimeProvenReceipts[0].Id);
            Assert.AreEqual("Aggregate browser-lane proof set", horizon.RuntimeProvenReceipts[0].Label);
            Assert.AreEqual(
                "release-evidence/browser-lane/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json",
                horizon.RuntimeProvenReceipts[0].PublicRelativePath);
            Assert.AreEqual("touch_mobile_staged", horizon.SourceStagedReceipts[0].Id);
            Assert.AreEqual("table_pulse_showcase", horizon.DocumentationSources[0].Id);
            Assert.AreEqual("Table Pulse flagship showcase", horizon.DocumentationSources[0].Label);
            Assert.AreEqual("/repo/docs/TABLE_PULSE_FLAGSHIP_SHOWCASE.md", horizon.DocumentationSources[0].LocalPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Read_returns_error_summary_when_receipt_is_invalid_json()
    {
        string tempRoot = CreateTempRoot();
        try
        {
            string receiptPath = Path.Combine(
                tempRoot,
                "release-evidence",
                "browser-lane",
                "BLAZOR_PLAY_SURFACE_HORIZON.generated.json");
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
            File.WriteAllText(receiptPath, "{ invalid json", System.Text.Encoding.UTF8);

            PlaySurfaceHorizonSummary summary = PortalPlaySurfaceHorizonReader.Read(tempRoot);

            Assert.AreEqual("error", summary.Status);
            Assert.AreEqual("invalid", summary.ContractName);
            Assert.AreEqual(PortalPlaySurfaceHorizonReader.PublicRelativePath, summary.ReceiptRelativePath);
            Assert.AreEqual("unknown", summary.CurrentExecutionScope);
            Assert.AreEqual(string.Empty, summary.PublicEntryRoute);
            Assert.AreEqual(string.Empty, summary.CompatibilityRouteBase);
            Assert.AreEqual(0, summary.Horizons.Count);
            StringAssert.Contains(summary.Summary, "could not be parsed");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"chummer-portal-play-surface-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }
}
