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
