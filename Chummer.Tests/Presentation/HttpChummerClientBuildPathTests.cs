#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class HttpChummerClientBuildPathTests
{
    [TestMethod]
    public async Task Build_path_preview_backfills_runtime_return_and_support_from_compatibility_rows()
    {
        StrictHttpMessageHandler handler = new((request, _) =>
        {
            if (request.Method == HttpMethod.Post
                && request.RequestUri?.PathAndQuery == "/api/hub/projects/buildkit/street-sam/install-preview?ruleset=sr5")
            {
                return JsonResponse("""
{
  "state": "ready",
  "runtimeFingerprint": "sha256:campaign",
  "changes": [],
  "diagnostics": [],
  "requiresConfirmation": false
}
""");
            }

            if (request.Method == HttpMethod.Get
                && request.RequestUri?.PathAndQuery == "/api/hub/projects/buildkit/street-sam/compatibility?ruleset=sr5")
            {
                return JsonResponse("""
{
  "kind": "buildkit",
  "itemId": "street-sam",
  "rows": [
    {
      "kind": "runtime-requirements",
      "label": "Runtime Requirements",
      "state": "review-required",
      "currentValue": "1",
      "notes": "Requires a compatible campaign/profile runtime before handoff: sr5: sha256:campaign; official-errata@1.2.0."
    },
    {
      "kind": "campaign-return",
      "label": "Campaign Return",
      "state": "review-required",
      "currentValue": "review-required",
      "notes": "The emitted build receipt can return through the selected workspace or campaign lane after the target matches: sr5: sha256:campaign; official-errata@1.2.0."
    },
    {
      "kind": "support-closure",
      "label": "Support Closure",
      "state": "review-required",
      "currentValue": "review-required",
      "notes": "Support closure can reuse the same runtime and rule-pack contract after handoff: sr5: sha256:campaign; official-errata@1.2.0."
    }
  ]
}
""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        HttpChummerClient client = new(httpClient);

        DesktopBuildPathPreview? preview = await client.GetBuildPathPreviewAsync("street-sam", new CharacterWorkspaceId("workspace-1"), "sr5", CancellationToken.None);

        Assert.IsNotNull(preview);
        Assert.AreEqual("Requires a compatible campaign/profile runtime before handoff: sr5: sha256:campaign; official-errata@1.2.0.", preview.RuntimeCompatibilitySummary);
        Assert.AreEqual("The emitted build receipt can return through the selected workspace or campaign lane after the target matches: sr5: sha256:campaign; official-errata@1.2.0.", preview.CampaignReturnSummary);
        Assert.AreEqual("Support closure can reuse the same runtime and rule-pack contract after handoff: sr5: sha256:campaign; official-errata@1.2.0.", preview.SupportClosureSummary);
    }

    [TestMethod]
    public async Task Build_path_preview_keeps_preview_summaries_without_fetching_compatibility_when_already_present()
    {
        int compatibilityRequestCount = 0;
        StrictHttpMessageHandler handler = new((request, _) =>
        {
            if (request.Method == HttpMethod.Post
                && request.RequestUri?.PathAndQuery == "/api/hub/projects/buildkit/matrix-operator/install-preview?ruleset=sr5")
            {
                return JsonResponse("""
{
  "state": "ready",
  "runtimeFingerprint": "sha256:campaign",
  "changes": [],
  "diagnostics": [],
  "requiresConfirmation": false,
  "runtimeCompatibilitySummary": "Preview runtime summary.",
  "campaignReturnSummary": "Preview return summary.",
  "supportClosureSummary": "Preview support summary."
}
""");
            }

            if (request.RequestUri?.PathAndQuery == "/api/hub/projects/buildkit/matrix-operator/compatibility?ruleset=sr5")
            {
                compatibilityRequestCount++;
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        HttpChummerClient client = new(httpClient);

        DesktopBuildPathPreview? preview = await client.GetBuildPathPreviewAsync("matrix-operator", new CharacterWorkspaceId("workspace-1"), "sr5", CancellationToken.None);

        Assert.IsNotNull(preview);
        Assert.AreEqual("Preview runtime summary.", preview.RuntimeCompatibilitySummary);
        Assert.AreEqual("Preview return summary.", preview.CampaignReturnSummary);
        Assert.AreEqual("Preview support summary.", preview.SupportClosureSummary);
        Assert.AreEqual(0, compatibilityRequestCount);
    }

    [TestMethod]
    public async Task Build_path_preview_uses_session_runtime_fallback_when_runtime_requirements_summary_is_missing()
    {
        StrictHttpMessageHandler handler = new((request, _) =>
        {
            if (request.Method == HttpMethod.Post
                && request.RequestUri?.PathAndQuery == "/api/hub/projects/buildkit/shadow-brief/install-preview?ruleset=sr6")
            {
                return JsonResponse("""
{
  "state": "ready",
  "runtimeFingerprint": "sha256:campaign",
  "changes": [],
  "diagnostics": [],
  "requiresConfirmation": true
}
""");
            }

            if (request.Method == HttpMethod.Get
                && request.RequestUri?.PathAndQuery == "/api/hub/projects/buildkit/shadow-brief/compatibility?ruleset=sr6")
            {
                return JsonResponse("""
{
  "kind": "buildkit",
  "itemId": "shadow-brief",
  "rows": [
    {
      "kind": "session-runtime",
      "label": "Session Runtime Bundle",
      "state": "blocked",
      "currentValue": "workbench-first",
      "notes": "Apply this build path in the workbench first, then hand the emitted build receipt into a compatible runtime and rule environment that match: sr6: sha256:campaign; no extra rule packs. No extra prompt resolution or grounded action staging is required. Next safe action: Apply this build path in the workbench, emit the build receipt, and hand it into the selected workspace."
    },
    {
      "kind": "campaign-return",
      "label": "Campaign Return",
      "state": "compatible",
      "currentValue": "compatible",
      "notes": "The emitted build receipt can return through the selected workspace after review."
    },
    {
      "kind": "support-closure",
      "label": "Support Closure",
      "state": "compatible",
      "currentValue": "compatible",
      "notes": "Support closure can cite the same runtime and build receipt once the handoff lands."
    }
  ]
}
""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        HttpChummerClient client = new(httpClient);

        DesktopBuildPathPreview? preview = await client.GetBuildPathPreviewAsync("shadow-brief", new CharacterWorkspaceId("workspace-1"), "sr6", CancellationToken.None);

        Assert.IsNotNull(preview);
        Assert.IsNotNull(preview.RuntimeCompatibilitySummary);
        StringAssert.Contains(preview.RuntimeCompatibilitySummary, "Next safe action:");
        StringAssert.Contains(preview.RuntimeCompatibilitySummary, "hand it into the selected workspace");
        Assert.AreEqual("The emitted build receipt can return through the selected workspace after review.", preview.CampaignReturnSummary);
        Assert.AreEqual("Support closure can cite the same runtime and build receipt once the handoff lands.", preview.SupportClosureSummary);
    }

    [TestMethod]
    public async Task Support_case_digests_use_presented_home_projection_endpoint()
    {
        StrictHttpMessageHandler handler = new((request, _) =>
        {
            if (request.Method == HttpMethod.Get
                && request.RequestUri?.PathAndQuery == "/api/v1/support/cases/me/presented")
            {
                return JsonResponse("""
[
  {
    "caseId": "case-123",
    "title": "Preview update did not carry the fix",
    "summary": "Reporter still needs one final confirmation step.",
    "statusLabel": "Released",
    "stageLabel": "Released",
    "nextSafeAction": "Open downloads or update this linked install to pick up the reporter-ready fix.",
    "closureSummary": "The fix reached preview 0.6.3-smoke.",
    "verificationSummary": "After you update on the affected install, confirm whether the fix worked here.",
    "detailHref": "/account/support/case-123",
    "primaryActionLabel": "Open downloads",
    "primaryActionHref": "/downloads",
    "updatedLabel": "2026-03-28 16:05 UTC",
    "fixedReleaseLabel": "preview 0.6.3-smoke",
    "affectedInstallSummary": "This case stays attached to the linked avalonia install.",
    "followUpLaneSummary": "Follow-up stays inside Account > Support for this signed-in report.",
    "releaseProgressSummary": "The fix reached preview 0.6.3-smoke.",
    "reporterActionNeeded": false,
    "canVerifyFix": true
  }
]
""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        HttpChummerClient client = new(httpClient);

        IReadOnlyList<DesktopHomeSupportDigest> digests = await client.GetDesktopHomeSupportDigestsAsync(CancellationToken.None);

        Assert.HasCount(1, digests);
        Assert.AreEqual("case-123", digests[0].CaseId);
        Assert.AreEqual("Open downloads", digests[0].PrimaryActionLabel);
        Assert.AreEqual("/downloads", digests[0].PrimaryActionHref);
        Assert.AreEqual("preview 0.6.3-smoke", digests[0].FixedReleaseLabel);
        Assert.IsTrue(digests[0].CanVerifyFix);
    }

    [TestMethod]
    public async Task Install_linking_summary_uses_the_account_projection_endpoint()
    {
        StrictHttpMessageHandler handler = new((request, _) =>
        {
            if (request.Method == HttpMethod.Get
                && request.RequestUri?.PathAndQuery == "/api/v1/install-linking/me")
            {
                return JsonResponse("""
{
  "recentReceipts": [
    {
      "receiptId": "dlr-123",
      "artifactLabel": "Windows",
      "channel": "preview",
      "version": "0.6.3-smoke",
      "head": "avalonia",
      "platform": "windows",
      "arch": "x64",
      "kind": "installer",
      "installAccessClass": "account_recommended",
      "issuedAtUtc": "2026-03-29T12:30:00Z",
      "claimTicketId": "ticket-123",
      "claimCode": "CLAIM-123",
      "claimTicketExpiresAtUtc": "2026-04-01T12:30:00Z"
    }
  ],
  "pendingClaimTickets": [
    {
      "ticketId": "ticket-123",
      "claimCode": "CLAIM-123",
      "artifactLabel": "Windows",
      "channel": "preview",
      "version": "0.6.3-smoke",
      "installAccessClass": "account_recommended",
      "status": "pending",
      "createdAtUtc": "2026-03-29T12:30:00Z",
      "expiresAtUtc": "2026-04-01T12:30:00Z"
    }
  ],
  "claimedInstallations": [
    {
      "installationId": "install-123",
      "channel": "preview",
      "version": "0.6.3-smoke",
      "installAccessClass": "account_recommended",
      "status": "active",
      "createdAtUtc": "2026-03-28T08:00:00Z",
      "updatedAtUtc": "2026-03-29T14:00:00Z",
      "headId": "avalonia",
      "platform": "windows",
      "arch": "x64",
      "hostLabel": "Shadow Deck",
      "grantId": "grant-123"
    }
  ],
  "activeGrants": [
    {
      "grantId": "grant-123",
      "installationId": "install-123",
      "status": "active",
      "accessToken": "redacted",
      "issuedAtUtc": "2026-03-29T14:00:00Z",
      "expiresAtUtc": "2026-04-28T14:00:00Z"
    }
  ]
}
""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        HttpChummerClient client = new(httpClient);

        DesktopInstallLinkingSummaryProjection summary = await client.GetDesktopInstallLinkingSummaryAsync(CancellationToken.None);

        Assert.HasCount(1, summary.PendingClaimTickets);
        Assert.AreEqual("CLAIM-123", summary.PendingClaimTickets[0].ClaimCode);
        Assert.HasCount(1, summary.ClaimedInstallations);
        Assert.AreEqual("Shadow Deck", summary.ClaimedInstallations[0].HostLabel);
        Assert.HasCount(1, summary.ActiveGrants);
        Assert.AreEqual("grant-123", summary.ActiveGrants[0].GrantId);
        Assert.HasCount(1, summary.RecentReceipts);
        Assert.AreEqual("ticket-123", summary.RecentReceipts[0].ClaimTicketId);
    }

    [TestMethod]
    public async Task Support_case_detail_uses_the_tracked_case_detail_endpoint()
    {
        StrictHttpMessageHandler handler = new((request, _) =>
        {
            if (request.Method == HttpMethod.Get
                && request.RequestUri?.PathAndQuery == "/api/v1/support/cases/case-123")
            {
                return JsonResponse("""
{
  "caseId": "case-123",
  "clusterKey": "cluster-abc",
  "kind": "bug_report",
  "status": "released_to_reporter_channel",
  "title": "Preview update did not carry the fix",
  "summary": "Reporter still needs one final confirmation step.",
  "detail": "Open the tracked case after updating this install if the issue still reproduces.",
  "candidateOwnerRepo": "chummer-presentation",
  "designImpactSuspected": false,
  "createdAtUtc": "2026-03-27T10:30:00Z",
  "updatedAtUtc": "2026-03-29T12:45:00Z",
  "source": "desktop_feedback",
  "installationId": "install-123",
  "applicationVersion": "0.6.2-smoke",
  "releaseChannel": "preview",
  "headId": "avalonia",
  "platform": "windows",
  "arch": "x64",
  "fixedVersion": "0.6.3-smoke",
  "fixedChannel": "preview",
  "releasedToReporterChannelAtUtc": "2026-03-29T12:00:00Z",
  "userNotifiedAtUtc": "2026-03-29T12:30:00Z",
  "timeline": [
    {
      "eventId": "evt-1",
      "status": "released_to_reporter_channel",
      "summary": "The fix reached the reporter-ready preview lane.",
      "occurredAtUtc": "2026-03-29T12:00:00Z",
      "actor": "release automation"
    }
  ],
  "attachments": [
    {
      "attachmentId": "att-1",
      "fileName": "support-log.txt",
      "contentType": "text/plain",
      "sizeBytes": 2048,
      "uploadedAtUtc": "2026-03-28T09:15:00Z",
      "downloadHref": "/api/v1/support/cases/case-123/attachments/att-1"
    }
  ]
}
""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        HttpChummerClient client = new(httpClient);

        DesktopSupportCaseDetails? details = await client.GetDesktopSupportCaseDetailsAsync("case-123", CancellationToken.None);

        Assert.IsNotNull(details);
        Assert.AreEqual("case-123", details.CaseId);
        Assert.AreEqual("released_to_reporter_channel", details.Status);
        Assert.AreEqual("preview", details.FixedChannel);
        Assert.AreEqual(1, details.Timeline?.Count);
        Assert.AreEqual("evt-1", details.Timeline?[0].EventId);
        Assert.AreEqual(1, details.Attachments?.Count);
        Assert.AreEqual("/api/v1/support/cases/case-123/attachments/att-1", details.Attachments?[0].DownloadHref);
    }

    [TestMethod]
    public async Task Campaign_workspace_server_plane_uses_the_hub_projection_endpoint()
    {
        StrictHttpMessageHandler handler = new((request, _) =>
        {
            if (request.Method == HttpMethod.Get
                && request.RequestUri?.PathAndQuery == "/api/v1/campaign-spine/me/workspaces/workspace-123/server-plane")
            {
                return JsonResponse("""
{
  "workspace": {
    "workspaceId": "workspace-123"
  },
  "campaignSummary": {
    "sessionReadinessSummary": "Session return is green.",
    "restoreSummary": "Restore follows the claimed install.",
    "publicationSummary": "One recap-safe output is ready."
  },
  "rosterReadiness": {
    "summary": "One dossier is ready."
  },
  "readinessCues": [
    {
      "title": "Rule environment approved",
      "summary": "The campaign fingerprint is pinned."
    }
  ],
  "changePackets": [
    {
      "label": "Continuity snapshot",
      "summary": "The latest recap-safe snapshot is current."
    }
  ],
  "rosterTransfers": [
    {
      "runnerHandle": "APEX",
      "summary": "APEX moved into Thursday Crew Relay with governed ownership receipts attached."
    }
  ],
  "dossierFreshness": [
    {
      "runnerHandle": "APEX",
      "severity": "ready",
      "summary": "The dossier is current."
    }
  ],
  "ruleEnvironmentHealth": [
    {
      "title": "Campaign rule environment",
      "severity": "ready",
      "summary": "The rule fingerprint is approved."
    }
  ],
  "runboard": {
    "activeSceneSummary": "The active scene is still pinned.",
    "objectiveSummary": "One objective still needs attention.",
    "returnSummary": "Return through the shared campaign lane."
  },
  "continuityConflicts": [],
    "recapShelf": [
    {
      "label": "Dockside aftermath packet",
      "summary": "The same recap-safe packet now feeds campaign return and shared publication follow-through.",
      "audience": "personal,campaign,creator",
      "ownershipSummary": "Dockside keeps the same governed artifact on the signed-in account path instead of forking a shadow copy.",
      "publicationState": "preview_ready",
      "trustBand": "review-pending",
      "discoverable": false,
      "publicationSummary": "Dockside campaign packet is already attached on the publication shelf with shared visibility.",
      "nextSafeAction": "Open publication status before you widen the artifact audience."
    }
  ],
  "supportClosures": [
    {
      "stageLabel": "Released",
      "summary": "The fix reached the same claimed install."
    }
  ],
  "knownIssues": [],
  "decisionNotices": [
    {
      "kind": "install_role",
      "summary": "preview_scout stays attached to windows/avalonia on preview."
    }
  ],
  "travelMode": {
    "status": "warning",
    "summary": "Two claimed devices can reopen the campaign, but one travel lane still needs a grounded checkpoint.",
    "prefetchInventorySummary": "2 dossiers, 1 campaign, 1 rule environment, and governed prep packets stay bounded to the staged travel cache."
  },
  "firstPlayableSession": {
    "sessionId": "starter-123",
    "label": "Starter lane",
    "summary": "Starter lane is ready to land the first playable session without repo-only setup.",
    "campaignStartSummary": "The first playable session can start from Dockside without repo-only setup.",
    "ruleReadySummary": "The starter build stays legal under the approved Seattle Streets environment.",
    "returnLaneSummary": "Claimed-device restore and Dockside return stay readable after the first session.",
    "campaignReadySummary": "The same workspace is ready for the next full campaign handoff after the starter session.",
    "nextSafeAction": "Start the first playable session before you widen the workspace beyond the guided starter lane.",
    "evidenceLines": [
      "Starter build, restore packet, and campaign lane all point at the same Dockside kickoff."
    ]
  },
  "campaignMemory": {
    "label": "Long-lived campaign memory",
    "summary": "The governed memory lane keeps the Dockside handoff, the courier objective, and the downtime follow-through attached to the same workspace.",
    "returnSummary": "Return through the Dockside handoff so the same workspace can reopen the courier chase without a lossy recap jump.",
    "nextSafeAction": "Reopen the Dockside handoff before you trust a second device with the same recap lane.",
    "evidenceLines": [
      "Continuity snapshot still points at Dockside handoff.",
      "Downtime follow-through remains attached to the same recap lane."
    ]
  },
  "nextSafeAction": {
    "summary": "Open the shared campaign view."
  },
  "generatedAtUtc": "2026-03-28T18:12:00Z"
}
""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        HttpChummerClient client = new(httpClient);

        DesktopHomeCampaignServerPlane? projection = await client.GetCampaignWorkspaceServerPlaneAsync("workspace-123", CancellationToken.None);

        Assert.IsNotNull(projection);
        Assert.AreEqual("workspace-123", projection.WorkspaceId);
        Assert.AreEqual("Open the shared campaign view.", projection.NextSafeAction);
        Assert.AreEqual("Two claimed devices can reopen the campaign, but one travel lane still needs a grounded checkpoint.", projection.TravelModeSummary);
        Assert.AreEqual("The governed memory lane keeps the Dockside handoff, the courier objective, and the downtime follow-through attached to the same workspace.", projection.CampaignMemorySummary);
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Roster: One dossier is ready.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Roster transfer: APEX — APEX moved into Thursday Crew Relay with governed ownership receipts attached.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Travel mode: Two claimed devices can reopen the campaign, but one travel lane still needs a grounded checkpoint.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Travel inventory: 2 dossiers, 1 campaign, 1 rule environment, and governed prep packets stay bounded to the staged travel cache.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "First session: The first playable session can start from Dockside without repo-only setup.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Legal runner: The starter build stays legal under the approved Seattle Streets environment.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Understandable return: Claimed-device restore and Dockside return stay readable after the first session.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Campaign-ready lane: The same workspace is ready for the next full campaign handoff after the starter session.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Starter lane next: Start the first playable session before you widen the workspace beyond the guided starter lane.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "First-session proof: Starter build, restore packet, and campaign lane all point at the same Dockside kickoff.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Campaign memory: The governed memory lane keeps the Dockside handoff, the courier objective, and the downtime follow-through attached to the same workspace.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Campaign memory return: Return through the Dockside handoff so the same workspace can reopen the courier chase without a lossy recap jump.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Artifact audience: My stuff, Campaign stuff, Published stuff");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Artifact shelf views: My stuff, Campaign stuff, Published stuff stay browseable from the same governed shelf.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Artifact ownership: Dockside keeps the same governed artifact on the signed-in account path instead of forking a shadow copy.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Artifact publication: Preview Ready — Dockside campaign packet is already attached on the publication shelf with shared visibility.");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Artifact trust: Review Pending — Still bounded");
        CollectionAssert.Contains(projection.ReadinessHighlights.ToArray(), "Artifact next: Open publication status before you widen the artifact audience.");
        CollectionAssert.Contains(projection.SupportHighlights.ToArray(), "Released: The fix reached the same claimed install.");
        CollectionAssert.Contains(projection.DecisionNotices.ToArray(), "install_role: preview_scout stays attached to windows/avalonia on preview.");
        CollectionAssert.Contains(projection.Watchouts.ToArray(), "Travel mode: Two claimed devices can reopen the campaign, but one travel lane still needs a grounded checkpoint.");
        StringAssert.Contains(projection.PublicationSummary, "Artifact shelf: Dockside campaign packet is already attached on the publication shelf with shared visibility.");
    }

    [TestMethod]
    public async Task GetPortableExchangePreviewAsync_maps_receipt_formats_and_watchouts()
    {
        StrictHttpMessageHandler handler = new((request, _) =>
        {
            if (request.Method == HttpMethod.Post
                && request.RequestUri?.PathAndQuery == "/api/v1/ai/interop/export")
            {
                return JsonResponse("""
{
  "campaignId": "campaign-123",
  "manifest": {
    "characterCount": 1,
    "npcCount": 1,
    "sessionCount": 1,
    "encounterCount": 1,
    "prepCount": 2,
    "totalCount": 6
  },
  "compatibility": {
    "formatId": "chummer.portable-campaign.v1",
    "compatibilityState": "compatible-with-warnings",
    "contextSummary": "Campaign Dockside is portable, but the package does not yet pin a live session cutover.",
    "receiptSummary": "Portable dossier/campaign exchange is ready for inspect-only review or merge, while governed replace stays review-required until a live session export is pinned.",
    "nextSafeAction": "Open inspect-only first or export again with a pinned session before you authorize governed replace on another surface.",
    "supportedExchangeFormats": [
      "chummer.portable-dossier.v1",
      "chummer.portable-campaign.v1"
    ],
    "notes": [
      {
        "code": "format-identity",
        "severity": "info",
        "summary": "Package format chummer.portable-campaign.v1 stays on interop_export_v1/1.0.0."
      },
      {
        "code": "session-binding-required-for-replace",
        "severity": "warning",
        "summary": "No live session binding was requested, so replace should wait for a session-scoped export even though inspect-only and merge remain safe."
      }
    ]
  }
}
""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        HttpChummerClient client = new(httpClient);

        DesktopHomePortableExchangePreview? preview = await client.GetPortableExchangePreviewAsync("campaign-123", CancellationToken.None);

        Assert.IsNotNull(preview);
        Assert.AreEqual("campaign-123", preview.CampaignId);
        Assert.AreEqual("compatible-with-warnings", preview.CompatibilityState);
        Assert.AreEqual("Campaign Dockside is portable, but the package does not yet pin a live session cutover.", preview.ContextSummary);
        Assert.AreEqual("Portable dossier/campaign exchange is ready for inspect-only review or merge, while governed replace stays review-required until a live session export is pinned.", preview.ReceiptSummary);
        Assert.AreEqual("Open inspect-only first or export again with a pinned session before you authorize governed replace on another surface.", preview.NextSafeAction);
        Assert.AreEqual("6 portable asset(s): 1 dossier(s), 1 NPC(s), 1 session bundle(s), 1 encounter packet(s), 2 governed prep packet(s).", preview.AssetScopeSummary);
        CollectionAssert.AreEqual(
            new[] { "chummer.portable-dossier.v1", "chummer.portable-campaign.v1" },
            preview.SupportedExchangeFormats.ToArray());
        CollectionAssert.Contains(preview.Highlights.ToArray(), "Package format chummer.portable-campaign.v1 stays on interop_export_v1/1.0.0.");
        CollectionAssert.Contains(preview.Watchouts.ToArray(), "No live session binding was requested, so replace should wait for a session-scoped export even though inspect-only and merge remain safe.");
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StrictHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request, cancellationToken));
    }
}
