#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopInstallLinkingRuntimeTests
{
    private static readonly string[] LongFormSeparate = ["--install-claim-code", "claim-123"];
    private static readonly string[] SlashSeparate = ["/install-claim-code", "claim-123"];
    private static readonly string[] LongFormEquals = ["--install-claim-code=claim-123"];
    private static readonly string[] LongFormColon = ["--install-claim-code:claim-123"];
    private static readonly string[] SlashEquals = ["/install-claim-code=claim-123"];
    private static readonly string[] SlashColon = ["/install-claim-code:claim-123"];
    private static readonly string[] CallbackSwitchSeparate = ["--install-link-callback", "chummer://install-link?claimCode=claim-789"];
    private static readonly string[] CallbackSwitchEquals = ["--install-link-callback=https://chummer.run/downloads/install/callback?claim=claim-789"];
    private static readonly string[] CallbackDirectUri = ["chummer://install-link?claim_code=claim-789"];
    private static readonly string[] GrantCallbackSwitchSeparate = ["--install-link-callback", "chummer://install-link?code=grant-callback-789"];
    private static readonly string[] GrantCallbackSwitchEquals = ["--install-link-callback=https://chummer.run/downloads/install/callback?callbackCode=grant-callback-789"];
    private static readonly string[] GrantCallbackDirectUri = ["chummer://install-link?installLinkCode=grant-callback-789"];

    [TestMethod]
    public void BuildSupportPortalRelativePathForInstall_includes_install_prefill_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForInstall(CreateState());

        StringAssert.Contains(path, "/contact?", StringComparison.Ordinal);
        StringAssert.Contains(path, "kind=install_help", StringComparison.Ordinal);
        StringAssert.Contains(path, "installationId=ins-avalonia-1", StringComparison.Ordinal);
        StringAssert.Contains(path, "releaseChannel=preview", StringComparison.Ordinal);
        StringAssert.Contains(path, "headId=avalonia", StringComparison.Ordinal);
        StringAssert.Contains(path, Uri.EscapeDataString("Workspace continuity: support can review claimed-install entitlement and stale-state details."), StringComparison.Ordinal);
        Assert.IsFalse(path.Contains("Restore%20posture", StringComparison.Ordinal));
        Assert.IsFalse(path.Contains("Conflict%20choices", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildAccountPortalRelativePathForInstall_includes_browser_callback_hints()
    {
        string path = DesktopInstallLinkingRuntime.BuildAccountPortalRelativePathForInstall(CreateState() with
        {
            Status = "guest",
            GrantId = null,
            GrantToken = null,
            GrantIssuedAtUtc = null,
            GrantExpiresAtUtc = null
        });

        StringAssert.Contains(path, "/account/access/install-link?", StringComparison.Ordinal);
        StringAssert.Contains(path, "installationId=ins-avalonia-1", StringComparison.Ordinal);
        StringAssert.Contains(path, "installLinkMode=browser_callback", StringComparison.Ordinal);
        StringAssert.Contains(path, "installLinkTransport=grant_callback", StringComparison.Ordinal);
        StringAssert.Contains(path, "installLinkCallbackUri=http%3A%2F%2F127.0.0.1%3A", StringComparison.Ordinal);
        StringAssert.Contains(path, "install-link%2Fcallback", StringComparison.Ordinal);
        StringAssert.Contains(path, "state%3Ddesktop", StringComparison.Ordinal);
        StringAssert.Contains(path, "headId%3Davalonia", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildAccountPortalRelativePathForInstall_uses_remote_proof_poll_for_real_installation_key()
    {
        using RSA installationKey = RSA.Create(2048);
        DesktopInstallLinkingState state = CreateState() with
        {
            Status = "guest",
            ClaimedAtUtc = null,
            PublicKey = installationKey.ExportRSAPublicKeyPem(),
            PrivateKey = installationKey.ExportPkcs8PrivateKeyPem(),
            GrantId = null,
            GrantToken = null,
            GrantIssuedAtUtc = null,
            GrantExpiresAtUtc = null
        };

        string path = DesktopInstallLinkingRuntime.BuildAccountPortalRelativePathForInstall(state);

        StringAssert.Contains(path, "installLinkTransport=proof_poll", StringComparison.Ordinal);
        StringAssert.Contains(path, "installLinkCallbackUri=chummer%3A%2F%2Finstall-link", StringComparison.Ordinal);
        StringAssert.Contains(path, "publicKey=", StringComparison.Ordinal);
        Assert.IsFalse(path.Contains("127.0.0.1", StringComparison.Ordinal));
        Assert.IsFalse(path.Contains("installLinkTransport=grant_callback", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Remote_callback_poll_request_is_signed_by_the_installation_private_key()
    {
        using RSA installationKey = RSA.Create(2048);
        DesktopInstallLinkingState state = CreateState() with
        {
            Status = "guest",
            ClaimedAtUtc = null,
            PublicKey = installationKey.ExportRSAPublicKeyPem(),
            PrivateKey = installationKey.ExportPkcs8PrivateKeyPem(),
            GrantId = null,
            GrantToken = null,
            GrantIssuedAtUtc = null,
            GrantExpiresAtUtc = null
        };
        MethodInfo method = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "CreateRemoteCallbackPollRequest",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("The remote callback proof factory should exist.");

        object request = method.Invoke(null, [state])
            ?? throw new AssertFailedException("The remote callback proof factory should return a request.");
        T ReadProperty<T>(string propertyName)
            => (T)(request.GetType().GetProperty(propertyName)?.GetValue(request)
                ?? throw new AssertFailedException($"The remote callback request should expose {propertyName}."));

        string installationId = ReadProperty<string>("InstallationId");
        string headId = ReadProperty<string>("HeadId");
        string applicationVersion = ReadProperty<string>("ApplicationVersion");
        string channelId = ReadProperty<string>("ChannelId");
        string platform = ReadProperty<string>("Platform");
        string arch = ReadProperty<string>("Arch");
        string nonce = ReadProperty<string>("Nonce");
        string signature = ReadProperty<string>("Signature");
        string publicKey = ReadProperty<string>("PublicKey");
        long issuedAtUnixSeconds = ReadProperty<long>("IssuedAtUnixSeconds");

        Assert.AreEqual(48, nonce.Length);
        byte[] payload = Encoding.UTF8.GetBytes(string.Join(
            '\n',
            "chummer.install-link.remote-callback.v1",
            installationId,
            headId,
            applicationVersion,
            channelId,
            platform,
            arch,
            issuedAtUnixSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            nonce));
        Assert.IsTrue(installationKey.VerifyData(
            payload,
            Convert.FromBase64String(signature),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        byte[] wirePublicKey = Convert.FromBase64String(publicKey);
        CollectionAssert.AreEqual(installationKey.ExportSubjectPublicKeyInfo(), wirePublicKey);
        using RSA serverKey = RSA.Create();
        serverKey.ImportSubjectPublicKeyInfo(wirePublicKey, out int bytesRead);
        Assert.AreEqual(wirePublicKey.Length, bytesRead);
        Assert.IsTrue(serverKey.VerifyData(
            payload,
            Convert.FromBase64String(signature),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        byte[] alteredPayload = (byte[])payload.Clone();
        alteredPayload[0] ^= 1;
        Assert.IsFalse(serverKey.VerifyData(
            alteredPayload,
            Convert.FromBase64String(signature),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    [TestMethod]
    public void Remote_callback_poll_respects_bounded_retry_after_and_transient_statuses()
    {
        MethodInfo retryDelayMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "ResolveRemoteCallbackRetryDelay",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("The remote callback retry policy should exist.");
        MethodInfo transientStatusMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "IsTransientRemoteCallbackStatus",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("The remote callback transient-status policy should exist.");
        using HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            TimeSpan.FromSeconds(4));

        TimeSpan delay = (TimeSpan)(retryDelayMethod.Invoke(null, [response, 3])
            ?? throw new AssertFailedException("The retry policy should return a delay."));

        Assert.AreEqual(TimeSpan.FromSeconds(4), delay);
        Assert.AreEqual(true, transientStatusMethod.Invoke(null, [HttpStatusCode.TooManyRequests]));
        Assert.AreEqual(true, transientStatusMethod.Invoke(null, [HttpStatusCode.ServiceUnavailable]));
        Assert.AreEqual(false, transientStatusMethod.Invoke(null, [HttpStatusCode.BadRequest]));
        Assert.AreEqual(true, transientStatusMethod.Invoke(null, [HttpStatusCode.NotFound]));
    }

    [TestMethod]
    public void Browser_approval_retains_its_signed_operation_across_reopen_refresh_and_uncertainty()
    {
        string root = Path.Combine(Path.GetTempPath(), "desktop-approval", Guid.NewGuid().ToString("N"));
        string? previous = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        MethodInfo Method(string name) => typeof(DesktopInstallLinkingRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException(name);
        DesktopInstallLinkingState Prepare(DesktopInstallLinkingState state, bool expected)
        {
            object?[] args = [state, null, null];
            Assert.AreEqual(expected, Method("TryPrepareBrowserApproval").Invoke(null, args));
            return (DesktopInstallLinkingState)args[1]!;
        }
        string Property(object request, string name) => (string)request.GetType().GetProperty(name)!.GetValue(request)!;
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", root);
            DesktopInstallLinkingState state = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            DesktopInstallLinkingState first = Prepare(state, true);
            Assert.IsNotNull(first.BrowserApprovalRequest);
            object request = Method("ReadRetainedRemoteCallbackRequest").Invoke(null, [first])!;
            string nonce = Property(request, "Nonce");
            string operation = Property(request, "OperationId");
            Assert.AreEqual(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)), 0, 24)
                .Replace('+', '-').Replace('/', '_').TrimEnd('='), operation);
            string browser = DesktopInstallLinkingRuntime.BuildAccountPortalRelativePathForInstall(first);
            StringAssert.Contains(browser, "installLinkOperationId=" + operation);
            StringAssert.Contains(Uri.UnescapeDataString(browser), "state=" + first.BrowserApprovalReturnState);
            Assert.IsFalse(browser.Contains(Property(request, "Signature"), StringComparison.Ordinal));

            DesktopInstallLinkingState reopened = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            DesktopInstallLinkingState same = Prepare(reopened, true);
            Assert.AreEqual(first.BrowserApprovalRequest, same.BrowserApprovalRequest);
            Assert.AreEqual(first.BrowserApprovalReturnState, same.BrowserApprovalReturnState);
            object refreshed = Method("CreateRemoteCallbackPollRequest").Invoke(null, [same])!;
            Assert.AreEqual(nonce, Property(refreshed, "Nonce"));
            Assert.AreEqual(operation, Property(refreshed, "OperationId"));

            object?[] retain = [same, request, true, false, null];
            Assert.AreEqual(true, Method("TryRetainRemoteCallbackRequest").Invoke(null, retain));
            DesktopInstallLinkingState uncertain = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            Assert.IsTrue(uncertain.BrowserApprovalUncertain);
            DesktopInstallLinkingState blocked = Prepare(uncertain, false);
            Assert.AreEqual(first.BrowserApprovalRequest, blocked.BrowserApprovalRequest);
            Assert.AreEqual(first.BrowserApprovalReturnState, blocked.BrowserApprovalReturnState);
            long issued = (long)request.GetType().GetProperty("IssuedAtUnixSeconds")!.GetValue(request)!;
            Assert.AreEqual("EnterExplicitRecovery", Method("ResolveRemoteCallbackProofReuseDecision")
                .Invoke(null, [request, true, issued + 241])!.ToString());
            Assert.AreEqual("ResignAfterDefinitiveOutcome", Method("ResolveRemoteCallbackProofReuseDecision")
                .Invoke(null, [request, false, issued + 241])!.ToString());

            // Only a definitive terminal result permits an explicit new attempt.
            retain = [uncertain, request, false, true, null];
            Assert.AreEqual(true, Method("TryRetainRemoteCallbackRequest").Invoke(null, retain));
            DesktopInstallLinkingState next = Prepare((DesktopInstallLinkingState)retain[4]!, true);
            object nextRequest = Method("ReadRetainedRemoteCallbackRequest").Invoke(null, [next])!;
            Assert.AreNotEqual(operation, Property(nextRequest, "OperationId"));
            Assert.AreNotEqual(first.BrowserApprovalReturnState, next.BrowserApprovalReturnState);
            // A response for A cannot replace B's retained request.
            retain = [uncertain, request, false, true, null];
            Assert.AreEqual(false, Method("TryRetainRemoteCallbackRequest").Invoke(null, retain));
            Assert.AreEqual(next.BrowserApprovalRequest, DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia").BrowserApprovalRequest);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previous);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }


    [TestMethod]
    public void Retained_approval_survives_an_application_version_refresh()
    {
        // Regression for the R69 audit finding: an application update refreshes the
        // runtime metadata (ApplicationVersion/ChannelId) on every state load, but the
        // retained approval is the immutable signed target of the original operation
        // and must remain recoverable with the same installation identity and key.
        string root = Path.Combine(Path.GetTempPath(), "desktop-approval-update", Guid.NewGuid().ToString("N"));
        string? previous = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        MethodInfo Method(string name) => typeof(DesktopInstallLinkingRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException(name);
        DesktopInstallLinkingState Prepare(DesktopInstallLinkingState state, bool expected)
        {
            object?[] args = [state, null, null];
            Assert.AreEqual(expected, Method("TryPrepareBrowserApproval").Invoke(null, args));
            return (DesktopInstallLinkingState)args[1]!;
        }
        string Property(object request, string name) => (string)request.GetType().GetProperty(name)!.GetValue(request)!;
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", root);
            DesktopInstallLinkingState created = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            DesktopInstallLinkingState versionA = Prepare(created, true);
            Assert.IsNotNull(versionA.BrowserApprovalRequest);
            object requestA = Method("ReadRetainedRemoteCallbackRequest").Invoke(null, [versionA])!;
            string nonceA = Property(requestA, "Nonce");
            string operationA = Property(requestA, "OperationId");
            string versionAApplicationVersion = Property(requestA, "ApplicationVersion");

            // Simulate the application update: the runtime metadata moves to a newer
            // version while the installation identity, key and retained request stay
            // unchanged. This is what RefreshRuntimeMetadata does on every startup.
            DesktopInstallLinkingState versionB = PersistInstallLinkingState(versionA with
            {
                ApplicationVersion = "6.1.0-preview"
            });
            Assert.AreNotEqual(versionAApplicationVersion, versionB.ApplicationVersion);
            Assert.IsNotNull(versionB.BrowserApprovalRequest);

            // Version B must still be able to read and poll the retained request: the
            // reader must not compare its signed version against the running version.
            object requestB = Method("ReadRetainedRemoteCallbackRequest").Invoke(null, [versionB])!;
            Assert.AreEqual(nonceA, Property(requestB, "Nonce"));
            Assert.AreEqual(operationA, Property(requestB, "OperationId"));
            Assert.AreEqual(versionAApplicationVersion, Property(requestB, "ApplicationVersion"));

            // Preparation under version B must not refuse because of the version
            // refresh: the retained approval is still recoverable.
            DesktopInstallLinkingState repared = Prepare(versionB, true);
            Assert.IsNotNull(repared.BrowserApprovalRequest);

            // Recovery polling reuses the byte-identical signed request.
            object pollB = Method("CreateRemoteCallbackPollRequest").Invoke(null, [versionB])!;
            Assert.AreEqual(nonceA, Property(pollB, "Nonce"));
            Assert.AreEqual(operationA, Property(pollB, "OperationId"));
            Assert.AreEqual(versionAApplicationVersion, Property(pollB, "ApplicationVersion"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previous);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Headless_mode_recovers_a_retained_uncertain_approval_instead_of_exiting()
    {
        // Regression for the R69 audit finding: the headless entry point used to exit
        // with code 1 when preparation refused a new approval, so a retained uncertain
        // request from a previous process was never polled.
        string root = Path.Combine(Path.GetTempPath(), "desktop-approval-headless", Guid.NewGuid().ToString("N"));
        string? previousRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        string? previousOpenBrowser = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER");
        string? previousTimeout = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS");
        MethodInfo Method(string name) => typeof(DesktopInstallLinkingRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException(name);
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", root);
            DesktopInstallLinkingState created = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            object?[] prepareArgs = [created, null, null];
            Assert.AreEqual(true, Method("TryPrepareBrowserApproval").Invoke(null, prepareArgs));
            DesktopInstallLinkingState prepared = (DesktopInstallLinkingState)prepareArgs[1]!;

            // Persist the request as uncertain, as if the previous process died right
            // after dispatching it.
            object retained = Method("ReadRetainedRemoteCallbackRequest").Invoke(null, [prepared])!;
            object?[] retain = [prepared, retained, true, false, null];
            Assert.AreEqual(true, Method("TryRetainRemoteCallbackRequest").Invoke(null, retain));
            DesktopInstallLinkingState uncertain = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            Assert.IsTrue(uncertain.BrowserApprovalUncertain);

            // A new headless process against the same state must recover, not exit.
            using Socket listenerSocket = new(SocketType.Stream, ProtocolType.Tcp);
            listenerSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listenerSocket.Listen(16);
            int port = ((IPEndPoint)listenerSocket.LocalEndPoint!).Port;
            string apiBase = $"http://127.0.0.1:{port}/";
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", apiBase);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", "0");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", "15");
            using StringWriter output = new();
            using StringWriter error = new();

            // The fake Hub answers pending (202) on every poll; the headless wait loop
            // keeps observing the durable state and eventually times out gracefully.
            _ = Task.Run(async () =>
            {
                byte[] buffer = new byte[65536];
                while (true)
                {
                    Socket connection = await listenerSocket.AcceptAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using Socket socket = connection;
                            int read = await socket.ReceiveAsync(buffer, SocketFlags.None);
                            string requestText = Encoding.ASCII.GetString(buffer, 0, read);
                            int bodyStart = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                            string body = bodyStart >= 0 ? requestText[(bodyStart + 4)..] : string.Empty;
                            // The fake Hub answers 202 pending; the headless wait loop
                            // then observes the state flip and exits successfully.
                            string responseBody = "{}";
                            string response = "HTTP/1.1 202 Pending\r\n" +
                                              "Content-Type: application/json\r\n" +
                                              "Content-Length: " + Encoding.UTF8.GetByteCount(responseBody) + "\r\n" +
                                              "Connection: close\r\n\r\n" + responseBody;
                            await socket.SendAsync(Encoding.ASCII.GetBytes(response), SocketFlags.None);
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (SocketException)
                        {
                        }
                    });
                }
            });

            DesktopInstallLinkingStartupContext context = new(
                State: uncertain,
                ClaimResult: null,
                StartupClaimCode: null,
                ShouldPrompt: true,
                PromptReason: "claim_required");

            int? exitCode = await DesktopInstallLinkingRuntime.TryHandleHeadlessInstallLinkModeAsync(
                "avalonia",
                ["--install-link-headless"],
                context,
                output,
                error,
                CancellationToken.None);

            string outputText = output.ToString();
            StringAssert.Contains(outputText,
                "Checking the result of the previous browser approval before opening a new one.",
                StringComparison.Ordinal);
            StringAssert.Contains(outputText,
                "The pending approval was created by an earlier session or version.",
                StringComparison.Ordinal);

            // The recovery entry did not exit before polling: with the fake Hub answering
            // pending for the whole window, headless mode ends in the explicit timeout
            // path (2), never in the old preparation-failure exit (1).
            Assert.AreEqual(2, exitCode);
            StringAssert.Contains(error.ToString(),
                "timed out before Chummer confirmed the handoff", StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousRoot);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", previousOpenBrowser);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", previousTimeout);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task PollRemoteBrowserCallback_replays_the_identical_signed_request_after_an_ambiguous_send()
    {
        MethodInfo pollMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "PollRemoteBrowserCallbackAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("The remote callback poll loop should exist.");

        using RSA installationKey = RSA.Create(2048);
        string tempRoot = Path.Combine(
            Path.GetTempPath(), "desktop-install-linking-recovery-tests", Guid.NewGuid().ToString("N"));
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        Task pollTask = null!;

        // The fake Hub records every poll body and decides per request how to answer.
        // Request A is accepted (the Hub commits the grant) but the response is lost.
        // The retried request must be byte-identical to A so the Hub can recover the
        // original grant through its redemption receipt.
        List<string> bodies = new();
        int grantCount = 0;
        string installationPublicKey = Convert.ToBase64String(installationKey.ExportSubjectPublicKeyInfo());
        using Socket listenerSocket = new(SocketType.Stream, ProtocolType.Tcp);
        listenerSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listenerSocket.Listen(16);
        int port = ((IPEndPoint)listenerSocket.LocalEndPoint!).Port;
        string apiBase = $"http://127.0.0.1:{port}/";

        Task listenerTask = Task.Run(async () =>
        {
            byte[] buffer = new byte[65536];
            while (true)
            {
                Socket connection = await listenerSocket.AcceptAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using Socket socket = connection;
                        int read = await socket.ReceiveAsync(buffer, SocketFlags.None);
                        string requestText = Encoding.ASCII.GetString(buffer, 0, read);
                        int bodyStart = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        string body = bodyStart >= 0 ? requestText[(bodyStart + 4)..] : string.Empty;
                        lock (bodies)
                        {
                            bodies.Add(body);
                        }
                        string status;
                        string responseBody;
                        lock (bodies)
                        {
                            if (bodies.Count == 1)
                            {
                                // First send: the Hub accepts the proof, records the
                                // redemption, and commits the grant — but the response
                                // never reaches the client.
                                grantCount++;
                                status = "500";
                                responseBody = "lost";
                            }
                            else
                            {
                                // Recovery: the identical retry gets the original grant.
                                status = "200";
                                responseBody = BuildRecoveryGrantResponse();
                            }
                        }
                        responseBody = responseBody
                            .Replace("%PublicKey%", installationPublicKey)
                            .Replace("%IssuedAt%", DateTimeOffset.UtcNow.AddDays(-1).ToString("o"))
                            .Replace("%ExpiresAt%", DateTimeOffset.UtcNow.AddDays(30).ToString("o"));
                        string response = "HTTP/1.1 " + status + " Lost Response\r\n" +
                                          "Content-Type: application/json\r\n" +
                                          "Content-Length: " + Encoding.UTF8.GetByteCount(responseBody) + "\r\n" +
                                          "Connection: close\r\n\r\n" + responseBody;
                        await socket.SendAsync(Encoding.ASCII.GetBytes(response), SocketFlags.None);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (SocketException)
                    {
                    }
                });
            }
        });

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", apiBase);
            DesktopInstallLinkingState initial = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            DesktopInstallLinkingState prepared = PersistInstallLinkingState(initial with
            {
                Status = "guest",
                ClaimedAtUtc = null,
                GrantId = null,
                GrantToken = null,
                GrantIssuedAtUtc = null,
                GrantExpiresAtUtc = null,
                PublicKey = installationKey.ExportRSAPublicKeyPem(),
                PrivateKey = installationKey.ExportPkcs8PrivateKeyPem()
            });

            pollTask = (Task)pollMethod.Invoke(null, [prepared, CancellationToken.None])
                ?? throw new AssertFailedException("The poll loop should return a task.");
            await pollTask;

            Assert.IsTrue(bodies.Count >= 2,
                "The poll loop should retry after the ambiguous first send.");
            lock (bodies)
            {
                Assert.AreEqual(bodies[0], bodies[1],
                    "The retry after an ambiguous send must be byte-identical to the original signed request.");
            }

            // Exactly one grant may exist: the Hub committed once, for request A.
            Assert.AreEqual(1, grantCount,
                "The Hub commits the grant exactly once; the client must not re-mint proofs for the same approval.");

            DesktopInstallLinkingState finalState = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            Assert.AreEqual("claimed", finalState.Status);
            Assert.AreEqual("grant-recovered-1", finalState.GrantId);
            Assert.AreEqual("token-recovered-1", finalState.GrantToken);
            Assert.AreEqual("user-recovered-1", finalState.UserId);
            Assert.AreEqual("subject.install.recovery", finalState.SubjectId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
            try
            {
                listenerSocket.Dispose();
                await listenerTask;
            }
            catch (Exception)
            {
            }
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildRecoveryGrantResponse()
    {
        string issuedAt = DateTimeOffset.UtcNow.ToString("o");
        string expiresAt = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        return """
{
  "callback": {
    "callbackId": "callback-recovery-1",
    "callbackCode": "code-recovery-1",
    "installationId": "ins-avalonia-1",
    "artifactId": "avalonia-win-x64-installer",
    "channel": "preview",
    "version": "6.0.1-preview",
    "installAccessClass": "open_public",
    "status": "redeemed",
    "createdAtUtc": "%IssuedAt%",
    "expiresAtUtc": "%ExpiresAt%",
    "userId": "user-recovered-1",
    "subjectId": "subject.install.recovery",
    "publicKey": "%PublicKey%",
    "headId": "avalonia",
    "platform": "linux",
    "arch": "x64",
    "hostLabel": null,
    "callbackUri": "http://127.0.0.1/recovery",
    "grantId": "grant-recovered-1"
  },
  "installation": {
    "installationId": "ins-avalonia-1",
    "artifactId": "avalonia-win-x64-installer",
    "channel": "preview",
    "version": "6.0.1-preview",
    "installAccessClass": "open_public",
    "status": "active",
    "createdAtUtc": "%IssuedAt%",
    "updatedAtUtc": "%ExpiresAt%",
    "userId": "user-recovered-1",
    "subjectId": "subject.install.recovery",
    "publicKey": "%PublicKey%",
    "claimTicketId": "ticket-recovery-1",
    "headId": "avalonia",
    "platform": "linux",
    "arch": "x64",
    "hostLabel": null,
    "grantId": "grant-recovered-1"
  },
  "grant": {
    "grantId": "grant-recovered-1",
    "installationId": "ins-avalonia-1",
    "status": "active",
    "accessToken": "token-recovered-1",
    "issuedAtUtc": "%IssuedAt%",
    "expiresAtUtc": "%ExpiresAt%",
    "userId": "user-recovered-1",
    "subjectId": "subject.install.recovery"
  },
  "alreadyClaimed": true
}
""";
    }

    [TestMethod]
    public void ShouldPromptForStartup_stops_after_user_dismisses_optional_claim()
    {
        DateTimeOffset dismissedAt = DateTimeOffset.Parse("2026-03-28T14:05:00+00:00");
        DesktopInstallLinkingState guestState = CreateState() with
        {
            ChannelId = "stable",
            Status = "guest",
            ClaimedAtUtc = null,
            GrantId = null,
            GrantToken = null,
            GrantIssuedAtUtc = null,
            GrantExpiresAtUtc = null,
            LastPromptDismissedAtUtc = null
        };

        Assert.IsTrue(DesktopInstallLinkingRuntime.ShouldPromptForStartup(guestState));
        Assert.IsFalse(DesktopInstallLinkingRuntime.ShouldPromptForStartup(guestState with
        {
            LastPromptDismissedAtUtc = dismissedAt
        }));
    }

    [TestMethod]
    public async Task TryHandleHeadlessInstallLinkModeAsync_ignores_normal_startup()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        DesktopInstallLinkingStartupContext context = new(
            State: CreateState() with { Status = "guest", ClaimedAtUtc = null },
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "claim_required");

        int? exitCode = await DesktopInstallLinkingRuntime.TryHandleHeadlessInstallLinkModeAsync(
            "avalonia",
            Array.Empty<string>(),
            context,
            output,
            error,
            CancellationToken.None);

        Assert.IsNull(exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task TryHandleHeadlessInstallLinkModeAsync_prints_browser_callback_url_without_gui()
    {
        string? previousOpenBrowser = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER");
        string? previousTimeout = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", "0");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", "0");
            using StringWriter output = new();
            using StringWriter error = new();
            DesktopInstallLinkingStartupContext context = new(
                State: CreateState() with
                {
                    Status = "guest",
                    ClaimedAtUtc = null,
                    GrantId = null,
                    GrantToken = null,
                    GrantIssuedAtUtc = null,
                    GrantExpiresAtUtc = null
                },
                ClaimResult: null,
                StartupClaimCode: null,
                ShouldPrompt: true,
                PromptReason: "claim_required");

            int? exitCode = await DesktopInstallLinkingRuntime.TryHandleHeadlessInstallLinkModeAsync(
                "avalonia",
                ["--install-link-headless"],
                context,
                output,
                error,
                CancellationToken.None);

            string outputText = output.ToString();
            Assert.AreEqual(2, exitCode);
            StringAssert.Contains(outputText, "Chummer claim-your-copy headless mode");
            StringAssert.Contains(outputText, "Install ID: ins-avalonia-1");
            StringAssert.Contains(outputText, "https://chummer.run/login?next=");
            StringAssert.Contains(outputText, "installLinkCallbackUri%3Dhttp%253A%252F%252F127.0.0.1%253A", StringComparison.Ordinal);
            StringAssert.Contains(outputText, "--install-link-headless --install-link-callback");
            StringAssert.Contains(outputText, "Browser auto-open disabled");
            StringAssert.Contains(error.ToString(), "timed out");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", previousOpenBrowser);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", previousTimeout);
        }
    }

    [TestMethod]
    public async Task TryHandleHeadlessInstallLinkModeAsync_persists_browser_dispatch_failure()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Linux browser handoff failure persistence is exercised on Linux.");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "desktop-install-linking-browser-failure-tests", Guid.NewGuid().ToString("N"));
        string launcherRoot = Path.Combine(tempRoot, "launchers");
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousOpenBrowser = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER");
        string? previousTimeout = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS");
        string? previousPath = Environment.GetEnvironmentVariable("PATH");
        string? previousWslDistro = Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");
        string? previousWslInterop = Environment.GetEnvironmentVariable("WSL_INTEROP");
        Directory.CreateDirectory(launcherRoot);

        try
        {
            WriteExecutable(Path.Combine(launcherRoot, "xdg-open"), "#!/bin/sh\necho 'Operation not supported' >&2\nexit 1\n");
            WriteExecutable(Path.Combine(launcherRoot, "gio"), "#!/bin/sh\necho 'Operation not supported' >&2\nexit 1\n");
            WriteExecutable(Path.Combine(launcherRoot, "python3"), "#!/bin/sh\necho 'webbrowser unavailable' >&2\nexit 1\n");

            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", "1");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", "0");
            Environment.SetEnvironmentVariable("PATH", $"{launcherRoot}:{previousPath}");
            Environment.SetEnvironmentVariable("WSL_DISTRO_NAME", null);
            Environment.SetEnvironmentVariable("WSL_INTEROP", null);

            // The dispatch receipt belongs to the current durable installation and owner epoch.
            // Keep the dummy keys so this launcher test cannot start remote proof polling.
            DesktopInstallLinkingState guestState = PersistInstallLinkingState(CreateState() with
            {
                Status = "guest",
                ClaimedAtUtc = null,
                GrantId = null,
                GrantToken = null,
                GrantIssuedAtUtc = null,
                GrantExpiresAtUtc = null
            });
            using StringWriter output = new();
            using StringWriter error = new();
            DesktopInstallLinkingStartupContext context = new(
                State: guestState,
                ClaimResult: null,
                StartupClaimCode: null,
                ShouldPrompt: true,
                PromptReason: "claim_required");

            int? exitCode = await DesktopInstallLinkingRuntime.TryHandleHeadlessInstallLinkModeAsync(
                "avalonia",
                ["--install-link-headless"],
                context,
                output,
                error,
                CancellationToken.None);

            string outputText = output.ToString();
            Assert.AreEqual(2, exitCode);
            StringAssert.Contains(outputText, "Browser claim could not be opened automatically:", StringComparison.Ordinal);
            StringAssert.Contains(outputText, "Operation not supported", StringComparison.Ordinal);
            StringAssert.Contains(outputText, "https://chummer.run/login?next=", StringComparison.Ordinal);

            string statePath = Path.Combine(
                tempRoot,
                "Chummer6",
                "install-linking",
                "avalonia",
                "linux",
                "x64",
                "state.json");
            Assert.IsTrue(File.Exists(statePath), "Headless browser dispatch should persist install-linking state.");
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastBrowserDispatchUri") ?? string.Empty, "https://chummer.run/login?next=", StringComparison.Ordinal);
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastBrowserDispatchFailure") ?? string.Empty, "Operation not supported", StringComparison.Ordinal);
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastClaimError") ?? string.Empty, "Browser claim could not open automatically", StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", previousOpenBrowser);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", previousTimeout);
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("WSL_DISTRO_NAME", previousWslDistro);
            Environment.SetEnvironmentVariable("WSL_INTEROP", previousWslInterop);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void RecordBrowserDispatchAttempt_rejects_foreign_installation_without_changing_durable_state()
        => AssertBrowserDispatchPreservesCurrentState(current => current with
        {
            InstallationId = "ins-foreign-installation"
        });

    [TestMethod]
    public void RecordBrowserDispatchAttempt_rejects_stale_owner_revision_without_changing_durable_state()
        => AssertBrowserDispatchPreservesCurrentState(current => current with
        {
            OwnerTransitionRevision = current.OwnerTransitionRevision - 1
        });

    [TestMethod]
    public async Task TryHandleHeadlessInstallLinkModeAsync_exits_cleanly_when_already_linked()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        DesktopInstallLinkingStartupContext context = new(
            State: CreateState() with { UserId = "runner@example.test" },
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: false,
            PromptReason: "none");

        int? exitCode = await DesktopInstallLinkingRuntime.TryHandleHeadlessInstallLinkModeAsync(
            "avalonia",
            ["--install-link-console"],
            context,
            output,
            error,
            CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(output.ToString(), "Install already linked: runner@example.test");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForUpdate_includes_manifest_and_error_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForUpdate(
            CreateState(),
            new DesktopUpdateClientStatus(
                HeadId: "avalonia",
                InstalledVersion: "6.0.1-preview",
                ChannelId: "preview",
                Platform: "linux",
                Arch: "x64",
                UpdatesEnabled: true,
                AutoApply: false,
                ManifestLocation: "http://127.0.0.1:8091/downloads/manifest.json",
                LastCheckedAtUtc: DateTimeOffset.Parse("2026-03-28T14:00:00+00:00"),
                LastManifestVersion: "6.0.2-preview",
                LastManifestPublishedAtUtc: DateTimeOffset.Parse("2026-03-28T13:55:00+00:00"),
                LastError: "Manifest signature mismatch.",
                Status: "attention_required",
                RecommendedAction: "Review the promoted preview and route support before retrying.",
                RolloutState: "local_docker_preview",
                SupportabilityState: "local_docker_proven",
                SupportabilitySummary: "Local proof passed for install, build, and support closure.",
                KnownIssueSummary: "Portable artifact is still preview-only on this channel.",
                FixAvailabilitySummary: "Only verify fixes after this install can see the promoted archive.",
                ProofStatus: "passed",
                ProofGeneratedAtUtc: DateTimeOffset.Parse("2026-03-28T13:56:00+00:00")));

        StringAssert.Contains(path, "title=Desktop%20update%20posture%20needs%20review%20for%20avalonia", StringComparison.Ordinal);
        StringAssert.Contains(path, "Manifest%20signature%20mismatch.", StringComparison.Ordinal);
        StringAssert.Contains(path, "applicationVersion=6.0.1-preview", StringComparison.Ordinal);
        StringAssert.Contains(path, "Supportability%3A%20local_docker_proven", StringComparison.Ordinal);
        StringAssert.Contains(path, "Local%20release%20proof%3A%20passed", StringComparison.Ordinal);
        StringAssert.Contains(path, "Fix%20availability%3A%20Only%20verify%20fixes%20after%20this%20install%20can%20see%20the%20promoted%20archive.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForWorkspace_includes_workspace_follow_through_context()
    {
        WorkspaceListItem workspace = new(
            Id: new CharacterWorkspaceId("workspace-redmond"),
            Summary: new CharacterFileSummary(
                Name: "Redmond Edge",
                Alias: "Edge",
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "SR6",
                AppVersion: "6.0.1-preview",
                Karma: 24,
                Nuyen: 18000,
                Created: true),
            LastUpdatedUtc: DateTimeOffset.Parse("2026-03-28T14:10:00+00:00"),
            RulesetId: "sr6.preview.v1",
            HasSavedWorkspace: true);

        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForWorkspace(CreateState(), workspace);

        StringAssert.Contains(path, "kind=bug_report", StringComparison.Ordinal);
        StringAssert.Contains(path, "Workspace%20follow-through%20needs%20help%20for%20Redmond%20Edge", StringComparison.Ordinal);
        StringAssert.Contains(path, "workspace-redmond", StringComparison.Ordinal);
        StringAssert.Contains(path, "sr6.preview.v1", StringComparison.Ordinal);
        StringAssert.Contains(path, Uri.EscapeDataString("Workspace continuity: support can review the current continuity packet."), StringComparison.Ordinal);
        StringAssert.Contains(path, Uri.EscapeDataString("Local workspace state stays under explicit user control."), StringComparison.Ordinal);
        Assert.IsFalse(path.Contains("Restore%20posture", StringComparison.Ordinal));
        Assert.IsFalse(path.Contains("Conflict%20choices", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildWorkspacePortalRelativePath_includes_portable_exchange_anchor()
    {
        string path = DesktopInstallLinkingRuntime.BuildWorkspacePortalRelativePath("workspace-redmond", "portable-exchange");

        Assert.AreEqual("/account/work/workspaces/workspace-redmond#portable-exchange", path);
    }

    [TestMethod]
    public void BuildWorkspacePortalRelativePath_defaults_to_work_portal_when_workspace_is_missing()
    {
        string path = DesktopInstallLinkingRuntime.BuildWorkspacePortalRelativePath(string.Empty, "portable-exchange");

        Assert.AreEqual("/account/work", path);
    }

    [TestMethod]
    public void DesktopStartupCompanionRuntime_CreateProjection_defaults_to_text_only_voice_prompt()
    {
        DesktopStartupCompanionProjection projection = DesktopStartupCompanionRuntime.CreateProjection(CreateState());

        Assert.AreEqual("You made it. If you said something, I couldn't hear you.", projection.Headline);
        Assert.AreEqual("Hard boundary: no cross-app observation", projection.BoundaryNote);
        Assert.AreEqual("Voice mode is off. Default posture is text-only until you opt in.", projection.VoiceStatus);
        Assert.AreEqual("Enable voice mode", projection.PrimaryActionLabel);
        Assert.AreEqual("Keep text only", projection.SecondaryActionLabel);
        Assert.IsFalse(projection.VoiceModeEnabled);
        Assert.IsFalse(projection.IsMacBootstrapGremlin);
    }

    [TestMethod]
    public void DesktopStartupCompanionRuntime_CreateProjection_marks_macos_bootstrap_route()
    {
        DesktopStartupCompanionProjection projection = DesktopStartupCompanionRuntime.CreateProjection(
            CreateState() with
            {
                Platform = "macos"
            },
            voiceModeEnabled: true);

        Assert.IsTrue(projection.IsMacBootstrapGremlin);
        StringAssert.Contains(projection.Body, "Mac bootstrap gremlin", StringComparison.Ordinal);
        StringAssert.Contains(projection.VoiceStatus, "Voice mode is on", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForBugReport_includes_structured_bug_fields_and_release_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForBugReport(
            CreateState(),
            CreateUpdateStatus(),
            title: "Armor mod save fails",
            expectedBehavior: "Saving the runner should preserve the armor mod selection.",
            actualBehavior: "The mod selection disappears after save and reopen.",
            reproSteps: "1. Open armor.\n2. Add modification.\n3. Save and reopen.",
            evidenceNote: "Screenshot available on request.");

        StringAssert.Contains(path, "kind=bug_report", StringComparison.Ordinal);
        StringAssert.Contains(path, "title=Armor%20mod%20save%20fails", StringComparison.Ordinal);
        StringAssert.Contains(path, "Expected%3A%20Saving%20the%20runner%20should%20preserve%20the%20armor%20mod%20selection.", StringComparison.Ordinal);
        StringAssert.Contains(path, "Actual%3A%20The%20mod%20selection%20disappears%20after%20save%20and%20reopen.", StringComparison.Ordinal);
        StringAssert.Contains(path, "Evidence%3A%20Screenshot%20available%20on%20request.", StringComparison.Ordinal);
        StringAssert.Contains(path, "Release%20status%3A%20attention_required", StringComparison.Ordinal);
        StringAssert.Contains(path, "Known%20issues%3A%20Portable%20artifact%20is%20still%20preview-only%20on%20this%20channel.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportPortalRelativePathForFeedback_includes_feedback_fields_and_install_context()
    {
        string path = DesktopInstallLinkingRuntime.BuildSupportPortalRelativePathForFeedback(
            CreateState(),
            CreateUpdateStatus(),
            summary: "Campaign workspace should remember filters",
            detail: "The current filter resets every time I reopen the workspace.");

        StringAssert.Contains(path, "kind=feedback", StringComparison.Ordinal);
        StringAssert.Contains(path, "Desktop%20feedback%3A%20Campaign%20workspace%20should%20remember%20filters", StringComparison.Ordinal);
        StringAssert.Contains(path, "Feedback%3A%20Campaign%20workspace%20should%20remember%20filters", StringComparison.Ordinal);
        StringAssert.Contains(path, "Detail%3A%20The%20current%20filter%20resets%20every%20time%20I%20reopen%20the%20workspace.", StringComparison.Ordinal);
        StringAssert.Contains(path, "installationId=ins-avalonia-1", StringComparison.Ordinal);
        StringAssert.Contains(path, "applicationVersion=6.0.1-preview", StringComparison.Ordinal);
        StringAssert.Contains(path, "Recommended%20action%3A%20Review%20the%20promoted%20preview%20and%20route%20support%20before%20retrying.", StringComparison.Ordinal);
    }

    [TestMethod]
    public void BuildSupportDiagnosticsReceiptLines_returns_grounded_before_after_support_receipt()
    {
        IReadOnlyList<string> lines = DesktopInstallLinkingRuntime.BuildSupportDiagnosticsReceiptLines(CreateState(), CreateUpdateStatus());

        Assert.IsTrue(lines.Count > 0);
        Assert.IsTrue(lines.Any(line => line.Contains("support/ins-avalonia-1/avalonia/preview", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(lines.Any(line => line.Contains("Manifest signature mismatch.", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Any(line => line.Contains("before 6.0.1-preview/attention_required", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(lines.Any(line => line.Contains("after 6.0.2-preview", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void BuildPublicPortalAbsoluteUri_uses_web_base_override()
    {
        string? previousPublicBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL");
        string? previousWebBase = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        string? previousPublicWebBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", "https://hub.example.test/root/");

            string absoluteUri = DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri("/account/work");

            Assert.AreEqual("https://hub.example.test/account/work", absoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", previousPublicBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", previousPublicWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", previousWebBase);
        }
    }

    [TestMethod]
    public void BuildPublicPortalAbsoluteUri_prefers_explicit_public_web_base_override()
    {
        string? previousPublicBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL");
        string? previousPublicWebBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL");
        string? previousWebBase = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", "https://chummer.run/");
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", "http://chummer-api:8080/");
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", "http://chummer-api:8080/");

            string absoluteUri = DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri("/account/access/install-link");

            Assert.AreEqual("https://chummer.run/account/access/install-link", absoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", previousPublicBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", previousPublicWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", previousWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
        }
    }

    [TestMethod]
    public void BuildPublicPortalAbsoluteUri_prefers_explicit_public_base_override()
    {
        string? previousPublicBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL");
        string? previousPublicWebBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL");
        string? previousWebBase = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", "https://portal.example.test/root/");
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", "https://public-web.example.test/root/");
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", "https://web.example.test/root/");

            string absoluteUri = DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri("/account/access/install-link");
            Uri baseAddress = DesktopInstallLinkingRuntime.ResolvePublicPortalBaseAddress();

            Assert.AreEqual("https://portal.example.test/account/access/install-link", absoluteUri);
            Assert.AreEqual("https://portal.example.test/root/", baseAddress.AbsoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", previousPublicBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", previousPublicWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", previousWebBase);
        }
    }

    [TestMethod]
    public void BuildPublicPortalAbsoluteUri_rejects_internal_container_host_and_falls_back_to_public_web_host()
    {
        string? previousPublicBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL");
        string? previousWebBase = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        string? previousPublicWebBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL");
        string[] blockedBaseCandidates =
        [
            "http://chummer-api:8080/",
            "http://api.chummer-api:8080/",
            "http://foo.chummer-api:8080/",
            "http://chummer-api.internal:8080/",
            "http://10.0.0.1:8080/",
            "https://chummer-web:4443/",
            "https://foo-chummer-web:4443/",
            "https://chummer-web.internal:4443/",
            "https://host.docker.internal:8080/"
        ];
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", null);
            foreach (string blockedCandidate in blockedBaseCandidates)
            {
                Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", blockedCandidate);
                Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", blockedCandidate);

                string absoluteUri = DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri("/account/access/install-link");

                Assert.AreEqual("https://chummer.run/account/access/install-link", absoluteUri, blockedCandidate);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", previousPublicBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", previousPublicWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", previousWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
        }
    }

    [TestMethod]
    public void BuildPublicPortalAbsoluteUri_allows_internal_container_host_when_internal_host_override_is_set()
    {
        string? previousPublicBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL");
        string? previousWebBase = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        string? previousPublicWebBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL");
        string? previousInternalPortalOverride = Environment.GetEnvironmentVariable("CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", "http://chummer-api:8080/");
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", "http://chummer-api:8080/");
            Environment.SetEnvironmentVariable("CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS", "1");

            string absoluteUri = DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri("/account/access/install-link");

            Assert.AreEqual("http://chummer-api:8080/account/access/install-link", absoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", previousPublicBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", previousPublicWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", previousWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS", previousInternalPortalOverride);
        }
    }

    [TestMethod]
    public void BuildPublicPortalAbsoluteUri_allows_internal_tunneled_google_auth_url_when_internal_host_override_is_set()
    {
        string? previousPublicBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL");
        string? previousWebBase = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        string? previousPublicWebBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL");
        string? previousInternalPortalOverride = Environment.GetEnvironmentVariable("CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", "http://chummer-api:8080/");
            Environment.SetEnvironmentVariable("CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS", "true");
            string claimRelativePath =
                "/auth/google/start?next=%2Faccount%2Faccess%2Finstall-link%3FinstallationId%3Dins-edbf4c698ef24c9ea6899364dc6d1a9e%26headId%3Davalonia%26applicationVersion%3Drun-20260601-070650%26releaseChannel%3Dpublic_stable%26platform%3Dwindows%26ar";

            string absoluteUri = DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri(claimRelativePath);

            Assert.AreEqual(
                "http://chummer-api:8080/auth/google/start?next=%2Faccount%2Faccess%2Finstall-link%3FinstallationId%3Dins-edbf4c698ef24c9ea6899364dc6d1a9e%26headId%3Davalonia%26applicationVersion%3Drun-20260601-070650%26releaseChannel%3Dpublic_stable%26platform%3Dwindows%26ar",
                absoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", previousPublicBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", previousPublicWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", previousWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS", previousInternalPortalOverride);
        }
    }

    [TestMethod]
    public void ResolveApiBaseAddress_falls_back_to_public_web_host_when_api_host_is_unset()
    {
        string? previousPublicBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL");
        string? previousPublicWebBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL");
        string? previousWebBase = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", null);

            MethodInfo method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ResolveApiBaseAddress",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("ResolveApiBaseAddress was not found.");

            Uri uri = (Uri)(method.Invoke(null, null)
                ?? throw new InvalidOperationException("ResolveApiBaseAddress returned null."));

            Assert.AreEqual("https://chummer.run/", uri.AbsoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", previousPublicBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", previousPublicWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", previousWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
        }
    }

    [TestMethod]
    public void Callback_fragment_extractors_read_fragment_queries_and_reject_non_install_routes()
    {
        MethodInfo? browserMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "TryExtractBrowserCallbackCodeFromCallbackUri",
            BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo? claimMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "TryExtractClaimCodeFromCallbackUri",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(browserMethod);
        Assert.IsNotNull(claimMethod);

        object?[] browserArgs = ["https://chummer.run/downloads/install/callback#?callbackCode=grant-fragment-7", null];
        object?[] claimArgs = ["chummer://install-link#?claim_code=claim-fragment-7", null];
        object?[] rejectedArgs = ["https://chummer.run/account/support?claim=ignored", null];

        Assert.AreEqual(true, browserMethod.Invoke(null, browserArgs));
        Assert.AreEqual("grant-fragment-7", browserArgs[1]);

        Assert.AreEqual(true, claimMethod.Invoke(null, claimArgs));
        Assert.AreEqual("CLAIMFRAGMENT7", claimArgs[1]);

        Assert.AreEqual(false, claimMethod.Invoke(null, rejectedArgs));
        Assert.IsNull(rejectedArgs[1]);
    }

    [TestMethod]
    public void Runtime_alias_normalizers_include_legacy_platform_and_arch_tokens()
    {
        MethodInfo? platformMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "NormalizePlatformAliases",
            BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo? architectureMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "NormalizeArchitectureAliases",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(platformMethod);
        Assert.IsNotNull(architectureMethod);

        string[] platformAliases = ((IEnumerable<string>)platformMethod.Invoke(null, ["windows"])!).ToArray();
        string[] architectureAliases = ((IEnumerable<string>)architectureMethod.Invoke(null, ["x64"])!).ToArray();

        CollectionAssert.Contains(platformAliases, "windows");
        CollectionAssert.Contains(platformAliases, "win");
        CollectionAssert.Contains(architectureAliases, "x64");
        CollectionAssert.Contains(architectureAliases, "amd64");
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_pending_installer_claim_code()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string tempRoot = Path.Combine(Path.GetTempPath(), "chummer-install-linking-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };
            string pendingPath = Path.Combine(
                tempRoot,
                "Chummer6",
                "install-linking",
                "avalonia",
                "windows",
                "x64",
                "pending-claim-code.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
            File.WriteAllText(pendingPath, "claim-123", System.Text.Encoding.UTF8);

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should remain available for installer handoff coverage.");
            object? result = method.Invoke(null, [Array.Empty<string>(), state]);

            Assert.AreEqual("CLAIM123", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_pending_installer_claim_code_from_legacy_path()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string tempRoot = Path.Combine(Path.GetTempPath(), "chummer-install-linking-legacy-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };

            string legacyStateRoot = Path.Combine(
                tempRoot,
                "Chummer6",
                "install-linking",
                "avalonia",
                "win",
                "x64",
                "pending-claim-code.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(legacyStateRoot)!);
            File.WriteAllText(legacyStateRoot, "claim-legacy", System.Text.Encoding.UTF8);

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should support legacy pending-claim paths.");
            object? result = method.Invoke(null, [Array.Empty<string>(), state]);

            Assert.AreEqual("CLAIMLEGACY", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_installer_switch_variants()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should remain available for installer handoff coverage.");

            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [LongFormSeparate, state]),
                "The long form with separate value should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [SlashSeparate, state]),
                "The legacy slash form with separate value should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [LongFormEquals, state]),
                "The equals form should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [LongFormColon, state]),
                "The colon form should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [SlashEquals, state]),
                "The legacy slash-equals form should parse.");
            Assert.AreEqual(
                "CLAIM123",
                method.Invoke(null, [SlashColon, state]),
                "The legacy slash-colon form should parse.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
        }
    }

    [TestMethod]
    public async Task InitializeForStartupAsync_without_explicit_handoff_requires_linking_on_first_launch()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        string? previousReleaseChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        string tempRoot = Path.Combine(Path.GetTempPath(), "desktop-install-linking-startup-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "preview");

            DesktopInstallLinkingStartupContext context = await DesktopInstallLinkingRuntime.InitializeForStartupAsync(
                "avalonia",
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.IsTrue(context.ShouldPrompt, "First launch without a claim or callback must enter the install-link gate.");
            Assert.AreEqual("claim_required", context.PromptReason);
            Assert.IsNull(context.ClaimResult);
            Assert.AreEqual(1, context.State.LaunchCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", previousReleaseChannel);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void InitializeForStartup_without_explicit_handoff_requires_linking_on_first_launch()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        string? previousReleaseChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        string tempRoot = Path.Combine(Path.GetTempPath(), "desktop-install-linking-startup-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "preview");

            DesktopInstallLinkingStartupContext context = DesktopInstallLinkingRuntime.InitializeForStartup(
                "avalonia",
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.IsTrue(context.ShouldPrompt, "First launch without a claim or callback must enter the install-link gate.");
            Assert.AreEqual("claim_required", context.PromptReason);
            Assert.IsNull(context.ClaimResult);
            Assert.AreEqual(1, context.State.LaunchCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", previousReleaseChannel);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task InitializeForStartupAsync_without_explicit_handoff_prompts_install_link_gate_on_local_channel()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        string? previousReleaseChannel = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL");
        string tempRoot = Path.Combine(Path.GetTempPath(), "desktop-install-linking-local-startup-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", "local");

            DesktopInstallLinkingStartupContext context = await DesktopInstallLinkingRuntime.InitializeForStartupAsync(
                "avalonia",
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.IsTrue(context.ShouldPrompt, "Local channel prompts the install-link wizard for unclaimed installs (a1e00f405 contract).");
            Assert.AreEqual("claim_required", context.PromptReason);
            Assert.IsNull(context.ClaimResult);
            Assert.AreEqual(1, context.State.LaunchCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_RELEASE_CHANNEL", previousReleaseChannel);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_install_link_callback_variants()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", "https://chummer.run/downloads/install/callback?installClaimCode=claim-789");

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should accept callback-style handoff coverage.");
            Assert.AreEqual("CLAIM789", method.Invoke(null, [Array.Empty<string>(), state]), "The callback environment variable should parse.");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);
            Assert.AreEqual("CLAIM789", method.Invoke(null, [CallbackSwitchSeparate, state]), "The callback switch with separate value should parse.");
            Assert.AreEqual("CLAIM789", method.Invoke(null, [CallbackSwitchEquals, state]), "The callback switch equals form should parse.");
            Assert.AreEqual("CLAIM789", method.Invoke(null, [CallbackDirectUri, state]), "A direct callback URI argument should parse.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
        }
    }

    [TestMethod]
    public void StartupClaimExtraction_reads_pending_install_link_callback_file()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousClaimCode = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        string tempRoot = Path.Combine(Path.GetTempPath(), "chummer-install-link-callback-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };
            string pendingPath = Path.Combine(
                tempRoot,
                "Chummer6",
                "install-linking",
                "avalonia",
                "windows",
                "x64",
                "pending-install-link-callback.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
            File.WriteAllText(pendingPath, "chummer://install-link?claimCode=claim-456", System.Text.Encoding.UTF8);

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupClaimCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The startup claim-code extractor should accept pending callback handoff coverage.");
            object? result = method.Invoke(null, [Array.Empty<string>(), state]);

            Assert.AreEqual("CLAIM456", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_CLAIM_CODE", previousClaimCode);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void StartupBrowserCallbackExtraction_reads_install_link_callback_code_variants()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousCallbackUri = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", null);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", "https://chummer.run/downloads/install/callback?code=grant-callback-789");

            DesktopInstallLinkingState state = CreateState() with
            {
                HeadId = "avalonia",
                Platform = "windows",
                Arch = "x64"
            };

            MethodInfo? method = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ExtractStartupBrowserCallbackCode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "The browser callback extractor should remain available for install-link handoff coverage.");
            Assert.AreEqual("grant-callback-789", method.Invoke(null, [Array.Empty<string>(), state]), "The callback environment variable should parse.");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", null);
            Assert.AreEqual("grant-callback-789", method.Invoke(null, [GrantCallbackSwitchSeparate, state]), "The callback switch with separate value should parse.");
            Assert.AreEqual("grant-callback-789", method.Invoke(null, [GrantCallbackSwitchEquals, state]), "The callback switch equals form should parse.");
            Assert.AreEqual("grant-callback-789", method.Invoke(null, [GrantCallbackDirectUri, state]), "A direct callback URI argument should parse.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_CALLBACK_URI", previousCallbackUri);
        }
    }

    [TestMethod]
    public void StartupAccountLaunchExtraction_reads_direct_open_uri()
    {
        bool parsed = DesktopInstallLinkingRuntime.TryExtractStartupAccountLaunchFromArguments(
            ["chummer://open?ticket=ticket-42&kind=character&id=dossier-7"],
            out DesktopStartupAccountLaunchRequest? launch);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(launch);
        Assert.AreEqual("ticket-42", launch.Ticket);
        Assert.AreEqual("character", launch.Kind);
        Assert.AreEqual("dossier-7", launch.ResourceId);
    }

    [TestMethod]
    public async Task ResolveStartupAccountLaunchAsync_falls_back_to_uri_kind_when_ticket_cannot_be_exchanged()
    {
        DesktopStartupAccountLaunchResult? launch = await DesktopInstallLinkingRuntime.ResolveStartupAccountLaunchAsync(
            "avalonia",
            ["chummer://open?ticket=ticket-42&kind=campaign&id=campaign-9"],
            CreateState() with
            {
                Status = "guest",
                ClaimedAtUtc = null,
                GrantId = null,
                GrantToken = null,
                GrantIssuedAtUtc = null,
                GrantExpiresAtUtc = null,
                UserId = null,
                SubjectId = null
            },
            CancellationToken.None);

        Assert.IsNotNull(launch);
        Assert.AreEqual("campaign", launch.Kind);
        Assert.AreEqual("campaign-9", launch.ResourceId);
        Assert.AreEqual("uri_fallback", launch.Source);
    }

    [TestMethod]
    public void LoadOrCreateState_persists_private_key_outside_state_json_on_windows()
    {
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string tempRoot = Path.Combine(Path.GetTempPath(), "desktop-install-linking-state-store-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);

            DesktopInstallLinkingState state = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            string platform = OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsMacOS()
                    ? "macos"
                    : OperatingSystem.IsLinux()
                        ? "linux"
                        : "unknown";
            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
            };

            string installRoot = Path.Combine(tempRoot, "Chummer6", "install-linking", "avalonia", platform, arch);
            string statePath = Path.Combine(installRoot, "state.json");
            string protectedKeyPath = Path.Combine(installRoot, "private-key.protected");
            Assert.IsTrue(File.Exists(statePath), "Install-linking state should be persisted after the first load.");

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(statePath));
            string? persistedPrivateKey = document.RootElement.GetProperty("privateKey").GetString();
            if (OperatingSystem.IsWindows())
            {
                Assert.IsTrue(string.IsNullOrWhiteSpace(persistedPrivateKey), "Windows state.json should not persist the install-link private key in plaintext.");
                Assert.IsTrue(File.Exists(protectedKeyPath), "Windows installs should persist the private key in the DPAPI-backed sidecar.");
            }
            else
            {
                Assert.AreEqual(state.PrivateKey, persistedPrivateKey, "Non-Windows installs should continue to persist the key inline until an OS-backed store exists.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static DesktopInstallLinkingState PersistInstallLinkingState(DesktopInstallLinkingState state)
    {
        MethodInfo saveState = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "SaveState",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("The actual install state writer should exist.");
        return saveState.Invoke(null, [state]) as DesktopInstallLinkingState
            ?? throw new AssertFailedException("The install state writer should return the committed state.");
    }

    private static void AssertBrowserDispatchPreservesCurrentState(
        Func<DesktopInstallLinkingState, DesktopInstallLinkingState> createStaleState)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "desktop-install-linking-stale-browser-tests", Guid.NewGuid().ToString("N"));
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            DesktopInstallLinkingState initial = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DesktopInstallLinkingState current = PersistInstallLinkingState(initial with
            {
                Status = "claimed",
                ClaimedAtUtc = now,
                GrantId = "current-grant",
                GrantToken = "current-token",
                GrantIssuedAtUtc = now,
                GrantExpiresAtUtc = now.AddDays(1),
                UserId = "current-user",
                SubjectId = "current-subject",
                LastBrowserDispatchAttemptUtc = now,
                LastBrowserDispatchUri = "https://chummer.run/account?current-installation=1",
                LastBrowserDispatchFailure = "Existing browser diagnostic",
                LastClaimError = "Existing claim diagnostic"
            });
            Assert.IsTrue(current.OwnerTransitionRevision > initial.OwnerTransitionRevision,
                "The fixture must advance a real durable owner transition.");
            string statePath = Path.Combine(tempRoot, "Chummer6", "install-linking",
                current.HeadId, current.Platform, current.Arch, "state.json");
            byte[] before = File.ReadAllBytes(statePath);
            MethodInfo recordAttempt = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "RecordBrowserDispatchAttempt",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new AssertFailedException("The actual browser dispatch recorder should exist.");

            recordAttempt.Invoke(null,
                [createStaleState(current), "https://chummer.run/login?next=stale-dispatch", "Stale browser failure"]);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(statePath),
                "Rejected browser diagnostics must not rewrite any durable installation bytes.");
            DesktopInstallLinkingState after = DesktopInstallLinkingRuntime.LoadOrCreateState(current.HeadId);
            Assert.AreEqual(current.InstallationId, after.InstallationId);
            Assert.AreEqual(current.StateRevision, after.StateRevision);
            Assert.AreEqual(current.OwnerTransitionRevision, after.OwnerTransitionRevision);
            Assert.AreEqual(DesktopInstallLinkingRuntime.ResolveOwnerScope(current),
                DesktopInstallLinkingRuntime.ResolveOwnerScope(after));
            Assert.AreEqual(current.UserId, after.UserId);
            Assert.AreEqual(current.SubjectId, after.SubjectId);
            Assert.AreEqual(current.LastBrowserDispatchAttemptUtc, after.LastBrowserDispatchAttemptUtc);
            Assert.AreEqual(current.LastBrowserDispatchUri, after.LastBrowserDispatchUri);
            Assert.AreEqual(current.LastBrowserDispatchFailure, after.LastBrowserDispatchFailure);
            Assert.AreEqual(current.LastClaimError, after.LastClaimError);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static DesktopInstallLinkingState CreateState()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-03-28T14:00:00+00:00");
        return new DesktopInstallLinkingState(
            InstallationId: "ins-avalonia-1",
            HeadId: "avalonia",
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            Status: "claimed",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 3,
            LastStartedAtUtc: now,
            ClaimedAtUtc: now,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: "private-key",
            ClaimTicketId: "ticket-1",
            LastClaimCode: "CLAIM1",
            LastClaimMessage: "This copy is now linked to your Hub account.",
            LastClaimError: null,
            LastClaimAttemptUtc: now,
            GrantId: "grant-1",
            GrantToken: "token-1",
            GrantIssuedAtUtc: now,
            GrantExpiresAtUtc: now.AddDays(30),
            UserId: "user-1",
            SubjectId: "subject-1");
    }

    private static string? GetStringProperty(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void WriteExecutable(string path, string content)
    {
        File.WriteAllText(path, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static DesktopUpdateClientStatus CreateUpdateStatus()
        => new(
            HeadId: "avalonia",
            InstalledVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: false,
            ManifestLocation: "http://127.0.0.1:8091/downloads/manifest.json",
            LastCheckedAtUtc: DateTimeOffset.Parse("2026-03-28T14:00:00+00:00"),
            LastManifestVersion: "6.0.2-preview",
            LastManifestPublishedAtUtc: DateTimeOffset.Parse("2026-03-28T13:55:00+00:00"),
            LastError: "Manifest signature mismatch.",
            Status: "attention_required",
            RecommendedAction: "Review the promoted preview and route support before retrying.",
            RolloutState: "local_docker_preview",
            SupportabilityState: "local_docker_proven",
            SupportabilitySummary: "Local proof passed for install, build, and support closure.",
            KnownIssueSummary: "Portable artifact is still preview-only on this channel.",
            FixAvailabilitySummary: "Only verify fixes after this install can see the promoted archive.",
            ProofStatus: "passed",
            ProofGeneratedAtUtc: DateTimeOffset.Parse("2026-03-28T13:56:00+00:00"));
}
