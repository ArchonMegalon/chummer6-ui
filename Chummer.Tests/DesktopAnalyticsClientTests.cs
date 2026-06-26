#nullable enable annotations

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Avalonia;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopAnalyticsClientTests
{
    [TestCleanup]
    public void Cleanup()
        => DesktopPreferenceStateRuntime.SetCurrent(DesktopPreferenceState.Default);

    [TestMethod]
    public async Task TrackShellEventAsync_does_not_send_when_analytics_is_disabled()
    {
        CountingHandler handler = new();
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://chummer.run")
        };
        DesktopAnalyticsClient client = new(httpClient);
        DesktopPreferenceStateRuntime.SetCurrent(DesktopPreferenceState.Default with { AnalyticsOptIn = false });

        await client.TrackShellEventAsync("avalonia", "open", "shell", ct: CancellationToken.None);

        Assert.AreEqual(0, handler.RequestCount);
    }

    [TestMethod]
    public async Task TrackShellEventAsync_sends_when_analytics_is_enabled()
    {
        CountingHandler handler = new();
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://chummer.run")
        };
        DesktopAnalyticsClient client = new(httpClient);
        DesktopPreferenceStateRuntime.SetCurrent(DesktopPreferenceState.Default with { AnalyticsOptIn = true });

        await client.TrackShellEventAsync("avalonia", "open", "shell", ct: CancellationToken.None);

        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual("/api/desktop-analytics/track", handler.LastRequestPath);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string? LastRequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestPath = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }
}
