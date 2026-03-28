#nullable enable annotations

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
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

        Assert.AreEqual(1, digests.Count);
        Assert.AreEqual("case-123", digests[0].CaseId);
        Assert.AreEqual("Open downloads", digests[0].PrimaryActionLabel);
        Assert.AreEqual("/downloads", digests[0].PrimaryActionHref);
        Assert.AreEqual("preview 0.6.3-smoke", digests[0].FixedReleaseLabel);
        Assert.IsTrue(digests[0].CanVerifyFix);
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
