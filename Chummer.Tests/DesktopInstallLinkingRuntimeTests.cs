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
using System.Text.Json.Nodes;
using Chummer.Hub.Registry.Contracts.InstallLinking;
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
            string versionAChannel = Property(requestA, "ChannelId");

            // Simulate the application update: the runtime metadata moves to a newer
            // version while the installation identity, key and retained request stay
            // unchanged. This is what RefreshRuntimeMetadata does on every startup.
            DesktopInstallLinkingState versionB = PersistInstallLinkingState(versionA with
            {
                ApplicationVersion = "6.1.0-preview",
                ChannelId = versionAChannel == "stable" ? "preview" : "stable"
            });
            Assert.AreNotEqual(versionAApplicationVersion, versionB.ApplicationVersion);
            Assert.IsNotNull(versionB.BrowserApprovalRequest);

            // Version B must still be able to read and poll the retained request: the
            // reader must not compare its signed version against the running version.
            object requestB = Method("ReadRetainedRemoteCallbackRequest").Invoke(null, [versionB])!;
            Assert.AreEqual(nonceA, Property(requestB, "Nonce"));
            Assert.AreEqual(operationA, Property(requestB, "OperationId"));
            Assert.AreEqual(versionAApplicationVersion, Property(requestB, "ApplicationVersion"));
            Assert.AreEqual(versionAChannel, Property(requestB, "ChannelId"));

            // Reopening the browser must approve the same target that polling signs.
            string portal = DesktopInstallLinkingRuntime.BuildAccountPortalRelativePathForInstall(versionB);
            StringAssert.Contains(portal, "applicationVersion=" + Uri.EscapeDataString(versionAApplicationVersion));
            StringAssert.Contains(portal, "releaseChannel=" + Uri.EscapeDataString(versionAChannel));
            Assert.IsFalse(portal.Contains("applicationVersion=" + Uri.EscapeDataString(versionB.ApplicationVersion), StringComparison.Ordinal));
            Assert.IsFalse(portal.Contains("releaseChannel=" + Uri.EscapeDataString(versionB.ChannelId), StringComparison.Ordinal));
            StringAssert.Contains(portal, "installLinkOperationId=" + Uri.EscapeDataString(operationA));

            // Preparation under version B must not refuse because of the version
            // refresh: the retained approval is still recoverable.
            DesktopInstallLinkingState repared = Prepare(versionB, true);
            Assert.IsNotNull(repared.BrowserApprovalRequest);

            // Recovery polling reuses the byte-identical signed request.
            object pollB = Method("CreateRemoteCallbackPollRequest").Invoke(null, [versionB])!;
            Assert.AreEqual(nonceA, Property(pollB, "Nonce"));
            Assert.AreEqual(operationA, Property(pollB, "OperationId"));
            Assert.AreEqual(versionAApplicationVersion, Property(pollB, "ApplicationVersion"));
            Assert.AreEqual(versionAChannel, Property(pollB, "ChannelId"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previous);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Headless_mode_reports_malformed_retained_approval_without_discarding_it()
    {
        string root = Path.Combine(Path.GetTempPath(), "desktop-approval-malformed", Guid.NewGuid().ToString("N"));
        string? previousRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", root);
            DesktopInstallLinkingState created = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            MethodInfo prepare = typeof(DesktopInstallLinkingRuntime).GetMethod("TryPrepareBrowserApproval",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            object?[] arguments = [created, null, null];
            Assert.AreEqual(true, prepare.Invoke(null, arguments));
            DesktopInstallLinkingState prepared = (DesktopInstallLinkingState)arguments[1]!;
            const string malformedRequest = "{invalid-retained-json";
            DesktopInstallLinkingState retained = PersistInstallLinkingState(prepared with
            {
                BrowserApprovalRequest = malformedRequest,
                BrowserApprovalUncertain = true
            });
            using StringWriter output = new();
            using StringWriter error = new();
            int? exitCode = await DesktopInstallLinkingRuntime.TryHandleHeadlessInstallLinkModeAsync(
                "avalonia", ["--install-link-headless"],
                new DesktopInstallLinkingStartupContext(retained, null, null, true, "claim_required"),
                output, error, CancellationToken.None);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error.ToString(), "An earlier browser approval still needs to be checked");
            Assert.AreEqual(malformedRequest,
                DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia").BrowserApprovalRequest);
            Assert.IsFalse(output.ToString().Contains("Open this secure approval URL", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousRoot);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [DataTestMethod]
    [DataRow("missing-approved-key", true)]
    [DataRow("matching-approved-key", true)]
    [DataRow("wrong-callback-key", false)]
    [DataRow("wrong-installation-key", false)]
    [DataRow("wrong-callback-grant-reference", false)]
    [DataRow("missing-callback-grant-reference", false)]
    [DataRow("empty-callback-grant-reference", false)]
    [DataRow("whitespace-callback-grant-reference", false)]
    [DataRow("omitted-callback-grant-reference", false)]
    public async Task ExchangeBrowserCallback_enforces_direct_approval_key_and_grant_bindings(
        string mutation, bool shouldSucceed)
    {
        MethodInfo exchangeMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "ExchangeBrowserCallbackCodeAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("The direct callback exchange should exist.");
        string root = Path.Combine(Path.GetTempPath(), "desktop-direct-callback", Guid.NewGuid().ToString("N"));
        string? previousRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        using Socket listenerSocket = new(SocketType.Stream, ProtocolType.Tcp);
        listenerSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listenerSocket.Listen(1);
        int port = ((IPEndPoint)listenerSocket.LocalEndPoint!).Port;

        Task listenerTask = Task.Run(async () =>
        {
            using Socket socket = await listenerSocket.AcceptAsync(timeout.Token);
            using NetworkStream stream = new(socket, ownsSocket: false);
            using StreamReader reader = new(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            Assert.AreEqual("POST /api/v1/install-linking/callbacks/exchange HTTP/1.1",
                await reader.ReadLineAsync(timeout.Token));
            int contentLength = 0;
            string? header;
            while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync(timeout.Token)))
            {
                if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    contentLength = int.Parse(header["Content-Length:".Length..].Trim());
            }
            Assert.IsTrue(contentLength > 0);
            char[] body = new char[contentLength];
            Assert.AreEqual(contentLength, await reader.ReadBlockAsync(body.AsMemory(), timeout.Token));
            using JsonDocument request = JsonDocument.Parse(new string(body));
            string Field(string name) => request.RootElement.GetProperty(name).GetString()!;
            Assert.AreEqual("code-recovery-1", Field("callbackCode"));

            string responseBody = mutation == "matching-approved-key"
                ? BuildRecoveryGrantResponse(Field("installationId"), Field("headId"), Field("platform"),
                    Field("arch"), Field("applicationVersion"), Field("channelId"))
                : BuildMutatedRecoveryGrantResponse(Field("installationId"), Field("headId"), Field("platform"),
                    Field("arch"), Field("applicationVersion"), Field("channelId"), mutation);
            // Hub's non-proof browser approval leaves Callback.PublicKey null while
            // the accepted installation is bound to the key sent by this exchange.
            if (mutation != "matching-approved-key" && mutation != "wrong-callback-key")
                responseBody = ReplaceInBlock(responseBody, "callback", "\"publicKey\": \"%PublicKey%\"", "\"publicKey\": null");
            responseBody = responseBody.Replace("\"%PublicKey%\"", JsonSerializer.Serialize(Field("publicKey")));
            byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json\r\n" +
                "Content-Length: " + Encoding.UTF8.GetByteCount(responseBody) + "\r\n" +
                "Connection: close\r\n\r\n" + responseBody);
            await stream.WriteAsync(response.AsMemory(), timeout.Token);
        });

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", root);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", $"http://127.0.0.1:{port}/");
            DesktopInstallLinkingState initial = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            DesktopInstallClaimResult result = await (Task<DesktopInstallClaimResult>)exchangeMethod.Invoke(
                null, ["avalonia", "code-recovery-1", initial, timeout.Token])!;
            await listenerTask;

            Assert.AreEqual(shouldSucceed, result.Succeeded, mutation);
            DesktopInstallLinkingState persisted = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            Assert.AreEqual(initial.InstallationId, persisted.InstallationId);
            Assert.AreEqual(initial.PublicKey, persisted.PublicKey);
            if (shouldSucceed)
            {
                Assert.AreEqual("claimed", persisted.Status);
                Assert.AreEqual("grant-recovered-1", persisted.GrantId);
                Assert.AreEqual("token-recovered-1", persisted.GrantToken);
                Assert.AreEqual("user-recovered-1", persisted.UserId);
                Assert.AreEqual("subject.install.recovery", persisted.SubjectId);
                Assert.IsNotNull(persisted.ClaimedAtUtc);
                Assert.IsNull(persisted.LastClaimError);
            }
            else
            {
                StringAssert.Contains(result.Message, "does not match this installation's request");
                Assert.AreNotEqual("claimed", persisted.Status);
                Assert.IsNull(persisted.GrantId);
                Assert.IsNull(persisted.GrantToken);
                Assert.IsNull(persisted.UserId);
                Assert.IsNull(persisted.SubjectId);
                Assert.IsNull(persisted.ClaimedAtUtc);
            }
        }
        finally
        {
            timeout.Cancel();
            listenerSocket.Dispose();
            try
            {
                await listenerTask;
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            finally
            {
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousRoot);
                Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }

    [TestMethod]
    public async Task PollRemoteBrowserCallback_rejects_responses_that_do_not_match_the_requested_installation()
    {
        // Regression for the R70 audit finding: a successful poll response used to be
        // applied without checking that its callback, installation and grant actually
        // belong to the requested installation. Each case mutates exactly one binding;
        // every mutated response must leave the local link state unchanged and keep
        // the retained request resolvable for a later, correct response.
        MethodInfo pollMethod = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "PollRemoteBrowserCallbackAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("The remote callback poll loop should exist.");

        string tempBase = Path.Combine(
            Path.GetTempPath(), "desktop-install-linking-integrity-tests", Guid.NewGuid().ToString("N"));
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");

        // One binding per case: (name, expected catcher comment)
        string[] mutations =
        [
            "wrong-installation-id",
            "wrong-callback-grant-reference",
            "missing-callback-grant-reference",
            "empty-callback-grant-reference",
            "whitespace-callback-grant-reference",
            "omitted-callback-grant-reference",
            "inconsistent-grant-user",
            "wrong-callback-version",
            "wrong-approved-key",
            "missing-approved-key",
            "unusable-grant-status",
            "expired-grant",
            "future-issued-grant",
            "reversed-interval-grant",
            "zero-length-interval-grant"
        ];

        using RSA installationKey = RSA.Create(2048);
        List<string> bodies = new();
        DesktopInstallLinkingState? fixtureSource = null;
        int mutationIndex = -1;

        string installationPublicKey = Convert.ToBase64String(installationKey.ExportSubjectPublicKeyInfo());
        using Socket listenerSocket = new(SocketType.Stream, ProtocolType.Tcp);
        listenerSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listenerSocket.Listen(16);
        int port = ((IPEndPoint)listenerSocket.LocalEndPoint!).Port;
        string apiBase = $"http://127.0.0.1:{port}/";

        List<Task> handlerTasks = new();
        using var serverCancellation = new CancellationTokenSource();
        Task listenerTask = Task.Run(async () =>
        {
            while (true)
            {
                Socket connection = await listenerSocket.AcceptAsync(serverCancellation.Token);
                Task handler = Task.Run(async () =>
                {
                    try
                    {
                        using Socket socket = connection;
                        byte[] buffer = new byte[65536];
                        int read = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, serverCancellation.Token);
                        string requestText = Encoding.ASCII.GetString(buffer, 0, read);
                        int bodyStart = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        string body = bodyStart >= 0 ? requestText[(bodyStart + 4)..] : string.Empty;
                        int index;
                        lock (bodies)
                        {
                            bodies.Add(body);
                            index = bodies.Count;
                        }
                        // Odd sends are the ambiguous first attempt (lost response);
                        // even sends carry the mutated grant for the current case.
                        string status = index % 2 == 1 ? "500" : "200";
                        string responseBody = "lost";
                        if (status == "200")
                        {
                            DesktopInstallLinkingState? source = fixtureSource;
                            int caseIndex;
                            lock (bodies)
                            {
                                caseIndex = mutationIndex;
                            }
                            if (source is not null && caseIndex >= 0 && caseIndex < mutations.Length)
                            {
                                responseBody = BuildMutatedRecoveryGrantResponse(
                                    source.InstallationId,
                                    source.HeadId,
                                    source.Platform,
                                    source.Arch,
                                    source.ApplicationVersion,
                                    source.ChannelId,
                                    mutations[caseIndex]);
                            }
                        }
                        responseBody = responseBody
                            .Replace("%PublicKey%", installationPublicKey)
                            .Replace("%IssuedAt%", DateTimeOffset.UtcNow.AddDays(-1).ToString("o"))
                            .Replace("%ExpiresAt%", DateTimeOffset.UtcNow.AddDays(30).ToString("o"));
                        string response = "HTTP/1.1 " + status + " Test\r\n" +
                                          "Content-Type: application/json\r\n" +
                                          "Content-Length: " + Encoding.UTF8.GetByteCount(responseBody) + "\r\n" +
                                          "Connection: close\r\n\r\n" + responseBody;
                        await socket.SendAsync(Encoding.ASCII.GetBytes(response).AsMemory(), SocketFlags.None, serverCancellation.Token);
                    }
                    catch (OperationCanceledException) when (serverCancellation.IsCancellationRequested)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (SocketException)
                    {
                    }
                });
                handlerTasks.Add(handler);
            }
        });

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", apiBase);
            for (int caseIndex = 0; caseIndex < mutations.Length; caseIndex++)
            {
                string caseRoot = Path.Combine(tempBase, "case-" + caseIndex);
                Directory.CreateDirectory(caseRoot);
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", caseRoot);
                lock (bodies)
                {
                    mutationIndex = caseIndex;
                }
                bodies.Clear();

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
                fixtureSource = prepared;

                await (Task)(pollMethod.Invoke(null, [prepared, CancellationToken.None])
                    ?? throw new AssertFailedException("The poll loop should return a task."));

                // The loop returns at the first rejected response. The critical assertion
                // is that the mutated grant was NOT adopted and the request is retained.
                DesktopInstallLinkingState finalState = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
                Assert.AreNotEqual("claimed", finalState.Status,
                    $"[{mutations[caseIndex]}] An inconsistent response must never claim the installation.");
                Assert.IsNull(finalState.GrantToken,
                    $"[{mutations[caseIndex]}] A mutated grant token must not be adopted into local state.");
                Assert.IsNull(finalState.UserId,
                    $"[{mutations[caseIndex]}] A mutated account identity must not be adopted into local state.");
                Assert.IsNotNull(finalState.BrowserApprovalRequest,
                    $"[{mutations[caseIndex]}] The retained request must survive a rejected response for later recovery.");
                // Exactly two sends per case: the ambiguous one and the mutated answer.
                Assert.AreEqual(2, bodies.Count,
                    $"[{mutations[caseIndex]}] The loop must replay the identical request exactly once before rejecting.");
            }
        }
        finally
        {
            serverCancellation.Cancel();
            listenerSocket.Dispose();
            try
            {
                await listenerTask;
            }
            catch (Exception)
            {
            }
            try
            {
                await Task.WhenAll(handlerTasks);
            }
            catch (Exception)
            {
            }
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
            if (Directory.Exists(tempBase))
            {
                Directory.Delete(tempBase, recursive: true);
            }
        }
    }

    // Each mutation changes exactly ONE binding of an otherwise valid recovery
    // response, so a rejected case identifies the specific check that caught it.
    private static string BuildMutatedRecoveryGrantResponse(
        string installationId,
        string headId,
        string platform,
        string arch,
        string version,
        string channel,
        string mutation)
    {
        // One captured timestamp shared by both fields of the zero-length case
        // (separate UtcNow calls would drift and create a tiny valid interval).
        DateTimeOffset zeroLengthBoundary = DateTimeOffset.UtcNow.AddMinutes(4);
        string json = BuildRecoveryGrantResponse(installationId, headId, platform, arch, version, channel);
        switch (mutation)
        {
            case "wrong-installation-id":
                // Every installationId mention now names a foreign installation: the
                // response is internally consistent but does not belong to this request.
                return json.Replace("\"installationId\": \"" + installationId + "\"", "\"installationId\": \"ins-foreign\"");
            case "wrong-callback-grant-reference":
                // Only the callback's grant reference drifts from the returned grant.
                return ReplaceInBlock(json, "callback", "\"grantId\": \"grant-recovered-1\"", "\"grantId\": \"grant-foreign\"");
            case "missing-callback-grant-reference":
                return ReplaceInBlock(json, "callback", "\"grantId\": \"grant-recovered-1\"", "\"grantId\": null");
            case "empty-callback-grant-reference":
                return ReplaceInBlock(json, "callback", "\"grantId\": \"grant-recovered-1\"", "\"grantId\": \"\"");
            case "whitespace-callback-grant-reference":
                return ReplaceInBlock(json, "callback", "\"grantId\": \"grant-recovered-1\"", "\"grantId\": \" \"");
            case "omitted-callback-grant-reference":
                return ReplaceInBlock(json, "callback", ",\n    \"grantId\": \"grant-recovered-1\"", "");
            case "missing-approved-key":
                return ReplaceInBlock(json, "callback", "\"publicKey\": \"%PublicKey%\"", "\"publicKey\": null");
            case "wrong-callback-key":
                return ReplaceInBlock(json, "callback", "\"publicKey\": \"%PublicKey%\"", "\"publicKey\": \"Zm9vYmFyRm9v\"");
            case "wrong-installation-key":
                return ReplaceInBlock(json, "installation", "\"publicKey\": \"%PublicKey%\"", "\"publicKey\": \"Zm9vYmFyRm9v\"");
            case "inconsistent-grant-user":
                // Only the grant's account identity drifts from callback/installation.
                return ReplaceInBlock(json, "grant", "\"userId\": \"user-recovered-1\"", "\"userId\": \"user-foreign\"");
            case "wrong-callback-version":
                // Only the callback claims a different approved version.
                return ReplaceInBlock(json, "callback", "\"version\": \"" + version + "\"", "\"version\": \"9.9.9-other\"");
            case "wrong-approved-key":
                // Both key mentions are replaced before the fixture substitutes the real
                // key, so callback and installation now carry a foreign device key.
                return json.Replace("\"publicKey\": \"%PublicKey%\"", "\"publicKey\": \"Zm9vYmFyRm9v\"");
            case "unusable-grant-status":
                // The grant is not in a state a linked client can use.
                return ReplaceInBlock(json, "grant", "\"status\": \"active\"", "\"status\": \"inactive\"");
            case "expired-grant":
                // The grant is already expired at validation time. Every other
                // binding is consistent, so only the time-validity check catches
                // this response.
                return System.Text.RegularExpressions.Regex.Replace(
                    json,
                    "\"expiresAtUtc\": \"[^\"]*\"",
                    m => "\"expiresAtUtc\": \"" + DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o") + "\"");
            case "future-issued-grant":
                // The issuance timestamp is implausibly in the future (beyond the
                // clock-skew allowance) and must be rejected as forged.
                return System.Text.RegularExpressions.Regex.Replace(
                    json,
                    "\"issuedAtUtc\": \"[^\"]*\"",
                    m => "\"issuedAtUtc\": \"" + DateTimeOffset.UtcNow.AddMinutes(30).ToString("o") + "\"");
            case "reversed-interval-grant":
                // Both timestamps pass the individual bounds (not expired, within
                // the clock tolerance) but expiry precedes issuance. Only the
                // interval-ordering check can catch this response.
                return System.Text.RegularExpressions.Regex.Replace(
                    json,
                    "\"issuedAtUtc\": \"[^\"]*\"",
                    m => "\"issuedAtUtc\": \"" + DateTimeOffset.UtcNow.AddMinutes(4).ToString("o") + "\"")
                    is string withIssued
                    ? System.Text.RegularExpressions.Regex.Replace(
                        withIssued,
                        "\"expiresAtUtc\": \"[^\"]*\"",
                        m => "\"expiresAtUtc\": \"" + DateTimeOffset.UtcNow.AddMinutes(2).ToString("o") + "\"")
                    : throw new InvalidOperationException();
            case "zero-length-interval-grant":
                // A zero-length validity interval (expiry == issuance) is not a
                // positive lifetime and must be rejected. The SAME captured
                // timestamp is written to both fields — two separate UtcNow
                // calls would drift by milliseconds and produce a tiny valid
                // interval instead.
                return System.Text.RegularExpressions.Regex.Replace(
                    json,
                    "\"issuedAtUtc\": \"[^\"]*\"",
                    m => "\"issuedAtUtc\": \"" + zeroLengthBoundary.ToString("o") + "\"")
                    is string withIssuedZero
                    ? System.Text.RegularExpressions.Regex.Replace(
                        withIssuedZero,
                        "\"expiresAtUtc\": \"[^\"]*\"",
                        m => "\"expiresAtUtc\": \"" + zeroLengthBoundary.ToString("o") + "\"")
                    : throw new InvalidOperationException();
            default:
                throw new AssertFailedException("Unknown mutation: " + mutation);
        }
    }

    private static string ReplaceInBlock(string json, string block, string oldText, string newText)
    {
        int start = json.IndexOf("\"" + block + "\": {", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "Block " + block + " missing in fixture.");
        int end = json.IndexOf('\n', start);
        Assert.IsTrue(end > start, "Block " + block + " malformed.");
        int next = json.IndexOf("\n  },", end, StringComparison.Ordinal);
        Assert.IsTrue(next > end, "Block " + block + " end missing.");
        string section = json.Substring(end, next - end);
        return json.Substring(0, end) + section.Replace(oldText, newText) + json.Substring(next);
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
    public async Task Headless_mode_completes_recovery_with_the_retained_proof_and_claims_the_installation()
    {
        // Regression for the R70 audit finding: the previous recovery test only proved
        // that the entry point survives until timeout. This test proves SUCCESSFUL
        // recovery: the headless entry point polls the retained request, the fake Hub
        // validates the received proof against the ORIGINAL operation and returns the
        // grant; the process must exit 0 with a persisted claimed state.
        string root = Path.Combine(Path.GetTempPath(), "desktop-approval-headless-success", Guid.NewGuid().ToString("N"));
        string? previousRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        string? previousOpenBrowser = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER");
        string? previousTimeout = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS");
        MethodInfo Method(string name) => typeof(DesktopInstallLinkingRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException(name);

        List<string> bodies = new();
        DesktopInstallLinkingState? fixtureSource = null;

        List<Task> handlerTasks = new();
        using var serverCancellation = new CancellationTokenSource();
        using Socket listenerSocket = new(SocketType.Stream, ProtocolType.Tcp);
        listenerSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listenerSocket.Listen(16);
        int port = ((IPEndPoint)listenerSocket.LocalEndPoint!).Port;
        string apiBase = $"http://127.0.0.1:{port}/";

        Task listenerTask = Task.Run(async () =>
        {
            while (true)
            {
                Socket connection = await listenerSocket.AcceptAsync(serverCancellation.Token);
                Task handler = Task.Run(async () =>
                {
                    try
                    {
                        using Socket socket = connection;
                        byte[] buffer = new byte[65536];
                        int read = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, serverCancellation.Token);
                        string requestText = Encoding.ASCII.GetString(buffer, 0, read);
                        int bodyStart = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        string body = bodyStart >= 0 ? requestText[(bodyStart + 4)..] : string.Empty;
                        lock (bodies)
                        {
                            bodies.Add(body);
                        }
                        DesktopInstallLinkingState? source = fixtureSource;
                        string responseBody = "{}";
                        // The fake Hub answers every poll with the ORIGINAL grant of the
                        // retained operation — the successful-recovery scenario.
                        if (source is not null)
                        {
                            responseBody = BuildRecoveryGrantResponse(
                                source.InstallationId,
                                source.HeadId,
                                source.Platform,
                                source.Arch,
                                source.ApplicationVersion,
                                source.ChannelId);
                        }
                        string responsePublicKey = "unused";
                        if (source is not null)
                        {
                            // The state stores the key as PEM; the wire format is
                            // Base64 SPKI — derive it exactly like the runtime does.
                            using RSA fixtureKey = RSA.Create();
                            fixtureKey.ImportFromPem(source.PublicKey);
                            responsePublicKey = Convert.ToBase64String(fixtureKey.ExportSubjectPublicKeyInfo());
                        }
                        responseBody = responseBody
                            .Replace("%PublicKey%", responsePublicKey)
                            .Replace("%IssuedAt%", DateTimeOffset.UtcNow.AddDays(-1).ToString("o"))
                            .Replace("%ExpiresAt%", DateTimeOffset.UtcNow.AddDays(30).ToString("o"));
                        string response = "HTTP/1.1 200 OK\r\n" +
                                          "Content-Type: application/json\r\n" +
                                          "Content-Length: " + Encoding.UTF8.GetByteCount(responseBody) + "\r\n" +
                                          "Connection: close\r\n\r\n" + responseBody;
                        await socket.SendAsync(Encoding.ASCII.GetBytes(response).AsMemory(), SocketFlags.None, serverCancellation.Token);
                    }
                    catch (OperationCanceledException) when (serverCancellation.IsCancellationRequested)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (SocketException)
                    {
                    }
                });
                handlerTasks.Add(handler);
            }
        });

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", root);
            DesktopInstallLinkingState created = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            object?[] prepareArgs = [created, null, null];
            Assert.AreEqual(true, Method("TryPrepareBrowserApproval").Invoke(null, prepareArgs));
            DesktopInstallLinkingState prepared = (DesktopInstallLinkingState)prepareArgs[1]!;
            fixtureSource = prepared;

            // Persist the request as uncertain, as if the previous process died right
            // after dispatching it.
            object retained = Method("ReadRetainedRemoteCallbackRequest").Invoke(null, [prepared])!;
            object?[] retain = [prepared, retained, true, false, null];
            Assert.AreEqual(true, Method("TryRetainRemoteCallbackRequest").Invoke(null, retain));
            DesktopInstallLinkingState uncertain = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            Assert.IsTrue(uncertain.BrowserApprovalUncertain);

            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", apiBase);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", "0");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", "30");
            using StringWriter output = new();
            using StringWriter error = new();

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

            // The retained proof recovered the original grant: exit 0, claimed state.
            Assert.AreEqual(0, exitCode,
                "Headless recovery must complete with the retained proof, not time out.");
            StringAssert.Contains(output.ToString(), "Install linked:", StringComparison.Ordinal);
            DesktopInstallLinkingState finalState = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            Assert.AreEqual("claimed", finalState.Status);
            Assert.AreEqual("grant-recovered-1", finalState.GrantId);
            Assert.AreEqual("token-recovered-1", finalState.GrantToken);
            Assert.AreEqual("user-recovered-1", finalState.UserId);
            Assert.AreEqual("subject.install.recovery", finalState.SubjectId);

            // The server received the retained proof (at least one poll), and every
            // received poll body is the ORIGINAL persisted request re-serialized —
            // byte-identical, no replacement identity, no re-signed variant.
            Assert.IsTrue(bodies.Count >= 1, "The headless path must poll the retained request.");
            System.Text.Json.JsonSerializerOptions runtimeJson = (System.Text.Json.JsonSerializerOptions)typeof(
                DesktopInstallLinkingRuntime)
                .GetField("JsonOptions", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            string expectedOriginalBody = System.Text.Json.JsonSerializer.Serialize(retained, retained.GetType(), runtimeJson);
            lock (bodies)
            {
                foreach (string body in bodies)
                {
                    Assert.AreEqual(expectedOriginalBody, body,
                        "Every poll must be the byte-identical original retained request.");
                }
            }
        }
        finally
        {
            // Test isolation (audit 2026-11-08): cancel test-owned asynchronous work,
            // stop accepting connections, AWAIT the listener and every accepted
            // connection handler, and only then restore process-wide state and remove
            // temporary files. Restoring shared configuration while a handler or poll
            // completion path is still running makes later tests timing-dependent.
            serverCancellation.Cancel();
            listenerSocket.Dispose();
            try
            {
                await listenerTask;
            }
            catch (Exception)
            {
            }
            try
            {
                await Task.WhenAll(handlerTasks);
            }
            catch (Exception)
            {
            }
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousRoot);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", previousOpenBrowser);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", previousTimeout);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestMethod]
    public async Task Headless_timeout_reports_an_unresolved_outcome_without_claiming_nothing_was_linked()
    {
        // Regression for the R70 audit finding: the headless timeout used to print
        // "Nothing was linked" even when the server-side outcome remained unknown.
        // An unresolved operation must be reported as retained-for-recovery; only a
        // conclusively rejected approval may be reported as a rejection.
        string root = Path.Combine(Path.GetTempPath(), "desktop-approval-headless-timeout", Guid.NewGuid().ToString("N"));
        string? previousRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        string? previousOpenBrowser = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER");
        string? previousTimeout = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS");
        MethodInfo Method(string name) => typeof(DesktopInstallLinkingRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException(name);

        using RSA installationKey = RSA.Create(2048);

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
                        // The fake Hub answers every poll with pending (202): the
                        // server-side outcome stays unknown for the whole window.
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

            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", apiBase);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", "0");
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", "2");
            using StringWriter output = new();
            using StringWriter error = new();

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

            Assert.AreEqual(2, exitCode, "The timeout exit code stays 2.");
            string errorText = error.ToString();
            StringAssert.Contains(errorText,
                "timed out before Chummer confirmed the handoff", StringComparison.Ordinal);
            StringAssert.Contains(errorText,
                "The approval may already have completed", StringComparison.Ordinal);
            StringAssert.Contains(errorText,
                "The original request has been retained for recovery", StringComparison.Ordinal);
            Assert.IsFalse(errorText.Contains("Nothing was linked", StringComparison.Ordinal),
                "A timeout must not claim a definitive negative outcome.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousRoot);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_OPEN_BROWSER", previousOpenBrowser);
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS_TIMEOUT_SECONDS", previousTimeout);
            // Test isolation: observe the listener loop to completion BEFORE the
            // environment is restored and the temp directory is removed; a discarded
            // task could otherwise depend on timing in later tests.
            listenerSocket.Dispose();
            try
            {
                await listenerTask;
            }
            catch (Exception)
            {
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
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
        DesktopInstallLinkingState? fixtureSource = null;
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
                                DesktopInstallLinkingState? source = fixtureSource;
                                responseBody = source is null
                                    ? "{}"
                                    : BuildRecoveryGrantResponse(
                                        source.InstallationId,
                                        source.HeadId,
                                        source.Platform,
                                        source.Arch,
                                        source.ApplicationVersion,
                                        source.ChannelId);
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
            fixtureSource = initial with
            {
                PublicKey = installationKey.ExportRSAPublicKeyPem(),
                PrivateKey = installationKey.ExportPkcs8PrivateKeyPem()
            };
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

    private static string BuildRecoveryGrantResponse(
        string installationId,
        string headId,
        string platform,
        string arch,
        string version,
        string channel)
    {
        string issuedAt = DateTimeOffset.UtcNow.ToString("o");
        string expiresAt = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        return """
{
  "callback": {
    "callbackId": "callback-recovery-1",
    "callbackCode": "code-recovery-1",
    "installationId": "%InstallationId%",
    "artifactId": "avalonia-win-x64-installer",
    "channel": "%Channel%",
    "version": "%Version%",
    "installAccessClass": "open_public",
    "status": "redeemed",
    "createdAtUtc": "%IssuedAt%",
    "expiresAtUtc": "%ExpiresAt%",
    "userId": "user-recovered-1",
    "subjectId": "subject.install.recovery",
    "publicKey": "%PublicKey%",
    "headId": "%HeadId%",
    "platform": "%Platform%",
    "arch": "%Arch%",
    "hostLabel": null,
    "callbackUri": "http://127.0.0.1/recovery",
    "grantId": "grant-recovered-1"
  },
  "installation": {
    "installationId": "%InstallationId%",
    "artifactId": "avalonia-win-x64-installer",
    "channel": "%Channel%",
    "version": "%Version%",
    "installAccessClass": "open_public",
    "status": "active",
    "createdAtUtc": "%IssuedAt%",
    "updatedAtUtc": "%ExpiresAt%",
    "userId": "user-recovered-1",
    "subjectId": "subject.install.recovery",
    "publicKey": "%PublicKey%",
    "claimTicketId": "ticket-recovery-1",
    "headId": "%HeadId%",
    "platform": "%Platform%",
    "arch": "%Arch%",
    "hostLabel": null,
    "grantId": "grant-recovered-1"
  },
  "grant": {
    "grantId": "grant-recovered-1",
    "installationId": "%InstallationId%",
    "status": "active",
    "accessToken": "token-recovered-1",
    "issuedAtUtc": "%IssuedAt%",
    "expiresAtUtc": "%ExpiresAt%",
    "userId": "user-recovered-1",
    "subjectId": "subject.install.recovery"
  },
  "alreadyClaimed": true
}
"""
            .Replace("%InstallationId%", installationId)
            .Replace("%HeadId%", headId)
            .Replace("%Platform%", platform)
            .Replace("%Arch%", arch)
            .Replace("%Version%", version)
            .Replace("%Channel%", channel)
            .Replace("%IssuedAt%", issuedAt)
            .Replace("%ExpiresAt%", expiresAt);
    }


    // Redemption-response integrity (R71 audit P2-1): a redeemed claim response
    // must bind to the installation that sent the request before any grant or
    // account field is adopted. The fake Hub derives the consistent response
    // from the captured request (as the real Hub echoes request metadata);
    // each case mutates exactly one binding. Every case must leave the client
    // unclaimed and must not adopt foreign credentials.
    private sealed record RedeemFixture(
        string InstallationId,
        string HeadId,
        string Version,
        string Channel,
        string Platform,
        string Arch,
        string PublicKey,
        string RequestBody)
    {
        public string BuildConsistentResponse(DateTimeOffset? grantExpiresAtUtc = null)
        {
            string issuedAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o");
            string expiresAt = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
            string grantExpiresAt = grantExpiresAtUtc?.ToString("o") ?? expiresAt;
            return $$"""
{
  "ticket": {
    "ticketId": "ticket-claim-1",
    "claimCode": "CLAIMCODE1",
    "installationId": "{{InstallationId}}",
    "userId": "user-claim-1",
    "subjectId": "subject.install.claim",
    "status": "redeemed",
    "createdAtUtc": "{{issuedAt}}",
    "expiresAtUtc": "{{expiresAt}}"
  },
  "installation": {
    "installationId": "{{InstallationId}}",
    "artifactId": "avalonia-win-x64-installer",
    "channel": "{{Channel}}",
    "version": "{{Version}}",
    "installAccessClass": "open_public",
    "status": "active",
    "createdAtUtc": "{{issuedAt}}",
    "updatedAtUtc": "{{issuedAt}}",
    "userId": "user-claim-1",
    "subjectId": "subject.install.claim",
    "publicKey": "{{PublicKey}}",
    "claimTicketId": "ticket-claim-1",
    "headId": "{{HeadId}}",
    "platform": "{{Platform}}",
    "arch": "{{Arch}}",
    "hostLabel": null,
    "grantId": "grant-claim-1"
  },
  "grant": {
    "grantId": "grant-claim-1",
    "installationId": "{{InstallationId}}",
    "status": "active",
    "accessToken": "token-claim-1",
    "issuedAtUtc": "{{issuedAt}}",
    "expiresAtUtc": "{{grantExpiresAt}}",
    "userId": "user-claim-1",
    "subjectId": "subject.install.claim"
  },
  "alreadyClaimed": false
}
""";
        }

        public string WithBindingField(string block, string field, string? value, bool omit)
        {
            JsonObject response = JsonNode.Parse(BuildConsistentResponse())!.AsObject();
            JsonObject target = response[block]!.AsObject();
            if (omit)
                target.Remove(field);
            else
                target[field] = value;
            return response.ToJsonString();
        }

        private static string ReplaceLast(string body, string find, string replace)
        {
            int index = body.LastIndexOf(find, StringComparison.Ordinal);
            return index < 0 ? body : body[..index] + replace + body[(index + find.Length)..];
        }

        // Every occurrence: the response is internally consistent but no longer
        // refers to the installation that sent the request.
        public string WithForeignInstallationId()
            => BuildConsistentResponse().Replace(InstallationId, "ins-other-1", StringComparison.Ordinal);

        public string WithForeignPublicKey()
            => BuildConsistentResponse().Replace(PublicKey, "Zm9yZWlnbiBrZXk=", StringComparison.Ordinal);

        public string WithForeignVersion()
            => BuildConsistentResponse().Replace(Version, "9.9.9.9", StringComparison.Ordinal);

        public string WithForeignChannel()
            => BuildConsistentResponse().Replace(Channel, "nightly", StringComparison.Ordinal);

        // The last "installationId" occurrence is the grant block: the grant
        // references a different installation than the returned installation.
        public string WithForeignGrantReference()
            => ReplaceLast(
                BuildConsistentResponse(),
                "\"installationId\": \"" + InstallationId + "\"",
                "\"installationId\": \"ins-other-1\"");

        // The last "userId" occurrence is the grant block: the grant belongs to
        // a different account than the returned installation.
        public string WithInconsistentAccountIdentity()
            => ReplaceLast(
                BuildConsistentResponse(),
                "\"userId\": \"user-claim-1\"",
                "\"userId\": \"user-other-1\"");

        // The last "active" status is the grant block: an unusable grant.
        public string WithUnusableGrantStatus()
            => ReplaceLast(
                BuildConsistentResponse(),
                "\"status\": \"active\"",
                "\"status\": \"revoked\"");

        // The last "expiresAtUtc" is the grant block: an already-expired grant.
        public string WithExpiredGrant()
        {
            string body = BuildConsistentResponse();
            int marker = body.LastIndexOf("\"expiresAtUtc\": \"", StringComparison.Ordinal);
            Assert.IsTrue(marker >= 0, "The redemption fixture must contain an expiresAtUtc value.");
            int valueStart = marker + "\"expiresAtUtc\": \"".Length;
            int valueEnd = body.IndexOf('"', valueStart);
            Assert.IsTrue(valueEnd > valueStart, "The redemption fixture must terminate the expiresAtUtc value.");
            return body[..valueStart] + "2020-01-01T00:00:00.0000000+00:00" + body[valueEnd..];
        }

        // A future-issued grant is implausible and must be rejected as well.
        public string WithFutureIssuedGrant()
        {
            string body = BuildConsistentResponse();
            int marker = body.LastIndexOf("\"issuedAtUtc\": \"", StringComparison.Ordinal);
            Assert.IsTrue(marker >= 0, "The redemption fixture must contain an issuedAtUtc value.");
            int valueStart = marker + "\"issuedAtUtc\": \"".Length;
            int valueEnd = body.IndexOf('"', valueStart);
            Assert.IsTrue(valueEnd > valueStart, "The redemption fixture must terminate the issuedAtUtc value.");
            return body[..valueStart] + DateTimeOffset.UtcNow.AddHours(2).ToString("o") + body[valueEnd..];
        }

        // Both timestamps pass the individual bounds, but expiry precedes
        // issuance. Only the interval-ordering check can catch this response.
        public string WithReversedIntervalGrant()
        {
            string body = BuildConsistentResponse();
            body = ReplaceLastTimestamp(body, "issuedAtUtc", DateTimeOffset.UtcNow.AddMinutes(4));
            return ReplaceLastTimestamp(body, "expiresAtUtc", DateTimeOffset.UtcNow.AddMinutes(2));
        }

        // A zero-length validity interval (expiry == issuance) is not a
        // positive lifetime and must be rejected. The SAME captured timestamp
        // is written to both fields; separate UtcNow calls would drift by
        // milliseconds and produce a tiny valid interval instead.
        public string WithZeroLengthIntervalGrant()
        {
            string body = BuildConsistentResponse();
            DateTimeOffset boundary = DateTimeOffset.UtcNow.AddMinutes(4);
            body = ReplaceLastTimestamp(body, "issuedAtUtc", boundary);
            return ReplaceLastTimestamp(body, "expiresAtUtc", boundary);
        }

        private static string ReplaceLastTimestamp(string body, string fieldName, DateTimeOffset value)
        {
            int marker = body.LastIndexOf("\"" + fieldName + "\": \"", StringComparison.Ordinal);
            Assert.IsTrue(marker >= 0, "The redemption fixture must contain a " + fieldName + " value.");
            int valueStart = marker + ("\"" + fieldName + "\": \"").Length;
            int valueEnd = body.IndexOf('"', valueStart);
            Assert.IsTrue(valueEnd > valueStart, "The redemption fixture must terminate the " + fieldName + " value.");
            return body[..valueStart] + value.ToString("o") + body[valueEnd..];
        }
    }

    [TestMethod]
    public async Task RedeemClaimCode_rejects_responses_that_do_not_match_the_request()
    {
        (string Name, Func<RedeemFixture, string> Mutate)[] cases =
        [
            ("wrong-installation-id", fixture => fixture.WithForeignInstallationId()),
            ("wrong-public-key", fixture => fixture.WithForeignPublicKey()),
            ("wrong-version", fixture => fixture.WithForeignVersion()),
            ("wrong-channel", fixture => fixture.WithForeignChannel()),
            ("wrong-grant-reference", fixture => fixture.WithForeignGrantReference()),
            ("inconsistent-account-identity", fixture => fixture.WithInconsistentAccountIdentity()),
            ("unusable-grant-status", fixture => fixture.WithUnusableGrantStatus()),
            ("expired-grant", fixture => fixture.WithExpiredGrant()),
            ("future-issued-grant", fixture => fixture.WithFutureIssuedGrant()),
            ("reversed-interval-grant", fixture => fixture.WithReversedIntervalGrant()),
            ("zero-length-interval-grant", fixture => fixture.WithZeroLengthIntervalGrant()),
        ];

        foreach ((string name, Func<RedeemFixture, string> mutate) in cases)
        {
            await RunRedemptionRejectionCase(name, mutate);
        }

        (string Block, string Field)[] bindings =
        [
            ("installation", "grantId"),
            ("ticket", "userId"),
            ("ticket", "subjectId")
        ];
        (string Name, string? Value, bool Omit)[] mutations =
        [
            ("foreign", "other-binding", false),
            ("null", null, false),
            ("empty", string.Empty, false),
            ("whitespace", "   ", false),
            ("omitted", null, true)
        ];
        foreach ((string block, string field) in bindings)
        {
            foreach ((string name, string? value, bool omit) in mutations)
            {
                await RunRedemptionRejectionCase($"{block}-{field}-{name}",
                    fixture => fixture.WithBindingField(block, field, value, omit));
            }
        }
    }

    private static Task<DesktopInstallClaimResult> RunRedemptionRejectionCase(
        string caseName,
        Func<RedeemFixture, string> mutate)
        => RunRedemptionCase(caseName, fixture => Task.FromResult(mutate(fixture)), shouldSucceed: false);

    private static async Task<DesktopInstallClaimResult> RunRedemptionCase(
        string caseName,
        Func<RedeemFixture, Task<string>> buildResponse,
        bool shouldSucceed)
    {
        using RSA installationKey = RSA.Create(2048);
        string tempRoot = Path.Combine(
            Path.GetTempPath(), "desktop-install-linking-redeem-tests", Guid.NewGuid().ToString("N"));
        string? previousStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        using Socket listenerSocket = new(SocketType.Stream, ProtocolType.Tcp);
        listenerSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listenerSocket.Listen(1);
        int port = ((IPEndPoint)listenerSocket.LocalEndPoint!).Port;
        string apiBase = $"http://127.0.0.1:{port}/";
        Task listenerTask = ServeRedemptionAsync(listenerSocket, buildResponse, timeout.Token);

        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", tempRoot);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", apiBase);
            DesktopInstallLinkingState created = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            PersistInstallLinkingState(created with
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

            DesktopInstallClaimResult result = await DesktopInstallLinkingRuntime.RedeemClaimCodeAsync(
                "avalonia", "claim-code-1", timeout.Token);
            // Server parsing, fixture and transport failures must fail the test.
            await listenerTask;

            DesktopInstallLinkingState finalState = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
            Assert.AreEqual(shouldSucceed, result.Succeeded, $"[{caseName}] {result.Message}");
            if (shouldSucceed)
            {
                Assert.AreEqual("claimed", finalState.Status);
                Assert.AreEqual("grant-claim-1", finalState.GrantId);
                Assert.AreEqual("token-claim-1", finalState.GrantToken);
                Assert.AreEqual("user-claim-1", finalState.UserId);
                Assert.AreEqual("subject.install.claim", finalState.SubjectId);
                Assert.AreEqual("ticket-claim-1", finalState.ClaimTicketId);
                Assert.IsNotNull(finalState.ClaimedAtUtc);
                Assert.IsNull(finalState.LastClaimError);
            }
            else
            {
                const string expectedError = "Hub accepted install claim redemption but returned a response that does not match this installation's request.";
                Assert.AreEqual(expectedError, result.Message, caseName);
                Assert.AreEqual(expectedError, finalState.LastClaimError, caseName);
                Assert.AreEqual("guest", finalState.Status, caseName);
                Assert.IsNull(finalState.GrantId, caseName);
                Assert.IsNull(finalState.GrantToken, caseName);
                Assert.IsNull(finalState.UserId, caseName);
                Assert.IsNull(finalState.SubjectId, caseName);
                Assert.IsNull(finalState.ClaimTicketId, caseName);
                Assert.IsNull(finalState.ClaimedAtUtc, caseName);
            }
            return result;
        }
        finally
        {
            timeout.Cancel();
            listenerSocket.Dispose();
            try
            {
                await listenerTask;
            }
            catch (OperationCanceledException) when (listenerTask.IsCanceled && timeout.IsCancellationRequested)
            {
                // Expected only when cleanup cancels an outstanding accept/read.
            }
            finally
            {
                Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", previousStateRoot);
                Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static async Task ServeRedemptionAsync(
        Socket listenerSocket,
        Func<RedeemFixture, Task<string>> buildResponse,
        CancellationToken cancellationToken)
    {
        using Socket socket = await listenerSocket.AcceptAsync(cancellationToken);
        using NetworkStream stream = new(socket, ownsSocket: false);
        string requestBody = await ReadRedemptionRequestBodyAsync(stream, cancellationToken);
        using JsonDocument document = JsonDocument.Parse(requestBody);
        string Field(string name) => document.RootElement.GetProperty(name).GetString()!;
        Assert.AreEqual("CLAIMCODE1", Field("claimCode"));
        // The Hub echoes the PEM key verbatim; escape its newlines for the JSON fixture.
        string publicKey = JsonSerializer.Serialize(Field("publicKey"))[1..^1];
        RedeemFixture fixture = new(
            Field("installationId"), Field("headId"), Field("applicationVersion"),
            Field("channelId"), Field("platform"), Field("arch"), publicKey, requestBody);
        string responseBody = await buildResponse(fixture);
        byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n" +
            "Content-Type: application/json\r\n" +
            "Content-Length: " + Encoding.UTF8.GetByteCount(responseBody) + "\r\n" +
            "Connection: close\r\n\r\n" + responseBody);
        await stream.WriteAsync(response.AsMemory(), cancellationToken);
    }

    private static async Task<string> ReadRedemptionRequestBodyAsync(
        NetworkStream stream, CancellationToken cancellationToken)
    {
        // Read headers as bytes so Content-Length always counts UTF-8 bytes, not chars.
        StringBuilder headers = new();
        byte[] next = new byte[1];
        while (true)
        {
            Assert.IsTrue(headers.Length < 16384, "Redemption request headers exceeded the test limit.");
            await stream.ReadExactlyAsync(next.AsMemory(), cancellationToken);
            headers.Append((char)next[0]);
            int length = headers.Length;
            if (length >= 4 && headers[length - 4] == '\r' && headers[length - 3] == '\n'
                && headers[length - 2] == '\r' && headers[length - 1] == '\n')
                break;
        }
        string[] lines = headers.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual("POST /api/v1/install-linking/redeem HTTP/1.1", lines[0]);
        string lengthHeader = lines.Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(int.TryParse(lengthHeader["Content-Length:".Length..].Trim(), out int contentLength));
        Assert.IsTrue(contentLength > 0 && contentLength <= 65536, "Redemption request body exceeded the test limit.");
        byte[] body = new byte[contentLength];
        await stream.ReadExactlyAsync(body.AsMemory(), cancellationToken);
        return Encoding.UTF8.GetString(body);
    }

    [TestMethod]
    public void IsGrantTimeValid_enforces_the_full_grant_interval_contract()
    {
        // Audit 2026-11-08: grant timestamps were individually bounded but their
        // interval was never validated. Deterministic validation time, no sockets.
        DateTimeOffset now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        MethodInfo isValid = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "IsGrantTimeValid", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertFailedException("The shared grant-time helper should exist.");

        bool Validate(string? issued, string? expires)
            => (bool)isValid.Invoke(null,
            [
                issued is null ? null : (DateTimeOffset?)DateTimeOffset.Parse(issued, System.Globalization.CultureInfo.InvariantCulture),
                expires is null ? null : (DateTimeOffset?)DateTimeOffset.Parse(expires, System.Globalization.CultureInfo.InvariantCulture),
                now
            ])!;

        // Valid interval and currently usable.
        Assert.IsTrue(Validate("2026-11-08T11:00:00.0000000+00:00", "2026-12-08T12:00:00.0000000+00:00"),
            "A usable grant with a positive interval must be accepted.");
        // Already expired.
        Assert.IsFalse(Validate("2026-11-08T11:00:00.0000000+00:00", "2026-11-08T11:59:00.0000000+00:00"),
            "An already-expired grant must be rejected.");
        // Issuance beyond the five-minute clock tolerance.
        Assert.IsFalse(Validate("2026-11-08T12:10:00.0000000+00:00", "2026-12-08T12:00:00.0000000+00:00"),
            "A future-issued grant beyond the clock tolerance must be rejected.");
        // Expiry before issuance, both within the current allowances.
        Assert.IsFalse(Validate("2026-11-08T12:04:00.0000000+00:00", "2026-11-08T12:02:00.0000000+00:00"),
            "A reversed validity interval must be rejected even when both bounds pass individually.");
        // Zero-length validity interval: no positive lifetime.
        Assert.IsFalse(Validate("2026-11-08T12:04:00.0000000+00:00", "2026-11-08T12:04:00.0000000+00:00"),
            "A zero-length validity interval must be rejected.");
        // Missing timestamps keep the optional-field contract.
        Assert.IsTrue(Validate(null, null), "Missing timestamps are optional and acceptable.");
        Assert.IsTrue(Validate("2026-11-08T11:00:00.0000000+00:00", null), "Issuance-only grants remain acceptable.");
        Assert.IsTrue(Validate(null, "2026-12-08T12:00:00.0000000+00:00"), "Expiry-only grants remain acceptable.");
        // Boundary: expiry exactly at the validation time is expired.
        Assert.IsFalse(Validate("2026-11-08T11:00:00.0000000+00:00", "2026-11-08T12:00:00.0000000+00:00"),
            "Expiry exactly at the validation time must be rejected.");
        // Boundary: issuance exactly at the tolerance edge is acceptable.
        Assert.IsTrue(Validate("2026-11-08T12:05:00.0000000+00:00", "2026-12-08T12:00:00.0000000+00:00"),
            "Issuance exactly at the five-minute tolerance edge must be accepted.");
    }

    [TestMethod]
    public void Redeemed_grant_validation_uses_the_supplied_time()
    {
        DateTimeOffset issued = DateTimeOffset.UtcNow.AddMinutes(-1);
        DateTimeOffset expires = issued.AddMinutes(11);
        var fixture = new RedeemFixture("ins-clock", "head-clock", "1.0", "internal",
            "windows", "x64", "synthetic-key", "{}");
        JsonObject response = JsonNode.Parse(fixture.BuildConsistentResponse(expires))!.AsObject();
        response["grant"]!["issuedAtUtc"] = issued.ToString("o");
        RedeemInstallClaimResponseDto accepted = JsonSerializer.Deserialize<RedeemInstallClaimResponseDto>(
            response.ToJsonString(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        MethodInfo validate = typeof(DesktopInstallLinkingRuntime).GetMethod(
            "IsConsistentRedeemedClaimResponse", BindingFlags.NonPublic | BindingFlags.Static)!;
        bool At(DateTimeOffset time) => (bool)validate.Invoke(null,
            [accepted, fixture.InstallationId, fixture.HeadId, fixture.Version, fixture.Channel,
                fixture.Platform, fixture.Arch, fixture.PublicKey, time])!;
        Assert.IsTrue(At(issued.AddMinutes(1)), "The consistent grant must be usable before expiry.");
        Assert.IsFalse(At(expires), "The redemption path must use its supplied time, not read the clock again.");
    }

    [TestMethod]
    public async Task RedeemClaimCode_accepts_a_consistent_response_and_claims_the_client()
    {
        await RunRedemptionCase("consistent", fixture => Task.FromResult(fixture.BuildConsistentResponse()),
            shouldSucceed: true);
    }

    [TestMethod]
    public async Task RedeemClaimCode_rejects_a_grant_that_expires_while_the_response_is_delayed()
    {
        DateTimeOffset requestCapturedAtUtc = default;
        DateTimeOffset expiresAtUtc = default;
        DateTimeOffset responseReturnedAtUtc = default;
        DesktopInstallClaimResult result = await RunRedemptionCase("grant-expires-in-transit", async fixture =>
        {
            requestCapturedAtUtc = DateTimeOffset.UtcNow;
            expiresAtUtc = requestCapturedAtUtc.AddMilliseconds(500);
            string response = fixture.BuildConsistentResponse(expiresAtUtc);
            await Task.Delay(TimeSpan.FromSeconds(1));
            responseReturnedAtUtc = DateTimeOffset.UtcNow;
            Assert.IsTrue(responseReturnedAtUtc >= expiresAtUtc,
                "The grant must already be expired before the server returns its response.");
            return response;
        }, shouldSucceed: false);

        Assert.IsTrue(requestCapturedAtUtc < expiresAtUtc,
            "The grant must be valid when the server captures the request.");
        Assert.IsTrue(result.State.LastClaimAttemptUtc is DateTimeOffset attemptAtUtc
            && attemptAtUtc <= requestCapturedAtUtc && attemptAtUtc < expiresAtUtc,
            "Diagnostics must retain the original attempt time, when the grant was still valid.");
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
            // Timeout=0 reports the misconfiguration cause; this state has no
            // in-flight approval request, so the definitive "nothing was linked"
            // wording is accurate here (unlike an unresolved retained operation).
            StringAssert.Contains(error.ToString(), "Install-link headless timeout is disabled or misconfigured.");
            StringAssert.Contains(error.ToString(), "No approval request was in flight; nothing was linked.");
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
