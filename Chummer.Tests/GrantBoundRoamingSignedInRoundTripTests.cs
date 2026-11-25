#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

/// <summary>
/// WIN-009 gen12 T1005: signed-in amended export and return sync.
/// Two independent desktop devices (each with its own CHUMMER_DESKTOP_STATE_ROOT) share a
/// single local Hub stub that speaks the exact continuation-workspace protocol. Both devices
/// run the ORDINARY startup DI composition (AddChummerLocalRuntimeClient), not direct
/// construction: claimed install-linking state resolves the grant-bound sync under the
/// install-bound owner lease; the amendment is a real content edit (karma) followed by an
/// outbound push, a cross-device inbound pull, and the return sync.
/// </summary>
[TestClass]
public sealed class GrantBoundRoamingSignedInRoundTripTests
{
    private const string ApiBaseUrlEnvironmentVariable = "CHUMMER_API_BASE_URL";
    private const string DesktopStateRootEnvironmentVariable = "CHUMMER_DESKTOP_STATE_ROOT";
    private const string StatePathEnvironmentVariable = "CHUMMER_STATE_PATH";
    private const string ClientModeEnvironmentVariable = "CHUMMER_CLIENT_MODE";
    private const string DesktopClientModeEnvironmentVariable = "CHUMMER_DESKTOP_CLIENT_MODE";
    private const string ApiKeyEnvironmentVariable = "CHUMMER_API_KEY";
    private const string WebBaseUrlEnvironmentVariable = "CHUMMER_WEB_BASE_URL";

    private static readonly object EnvironmentLock = new();

    public TestContext TestContext { get; set; } = null!;

    private string ContentRoot
    {
        get
        {
            TestContext.Properties.TryGetValue("ChummerCoreContentRoot", out object? value);
            Assert.IsTrue(value is string path && Path.IsPathFullyQualified(path)
                && File.Exists(Path.Combine(path, "Chummer", "data", "lifemodules.xml")),
                "Supply the exact ChummerCoreContentRoot for real runtime composition.");
            return (string)value!;
        }
    }

    [TestMethod]
    public void SignedIn_amended_export_reaches_second_device_and_returns()
    {
        lock (EnvironmentLock)
        {
            string persistenceRootA = CreateTempDirectory();
            string persistenceRootB = CreateTempDirectory();
            string stateRootA = CreateTempDirectory();
            string stateRootB = CreateTempDirectory();
            try
            {
                using FakeContinuationHub hub = new();
                Uri hubBase = hub.BaseAddress;

                string karmaXmlA = RoundTripXml("RoundTrip Runner", 15, 2500);
                string workspaceIdValue = string.Empty;

                // ---- Device A (Windows, head "avalonia"): import, amend, push ----
                using (DeviceScope scopeA = OpenDevice(persistenceRootA, stateRootA, "avalonia", hubBase, claimed: true))
                {
                    IChummerClient clientA = scopeA.Provider.GetRequiredService<IChummerClient>();
                    WorkspaceImportResult imported = clientA.ImportAsync(
                        new WorkspaceImportDocument(karmaXmlA, RulesetDefaults.Sr6, WorkspaceDocumentFormat.NativeXml),
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.IsNotNull(imported);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(imported.Id.Value));
                    workspaceIdValue = imported.Id.Value;

                    // The ordinary in-process client auto-roams a freshly imported
                    // workspace post-commit (SynchronizeImportedWorkspaceAsync), so the
                    // import itself already published revision 1 to the Hub.
                    InProcessChummerClient inProcessA = Assert.IsInstanceOfType<InProcessChummerClient>(
                        clientA, "ordinary DI composition must resolve InProcessChummerClient");
                    DesktopWorkspaceRoamingResult importPush = inProcessA.LastWorkspaceRoamingResult;
                    Assert.IsNotNull(importPush);
                    Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Applied, importPush.Outcome);

                    // Real amended edit through the ordinary owner-scoped store mutation
                    // (expected-revision replace).
                    WorkspaceStoreReadResult read = scopeA.Store.Get(scopeA.Owner, imported.Id);
                    Assert.IsTrue(read.Success, read.Error);
                    WorkspaceDocument document = read.Value!.Document;
                    Assert.IsTrue(document.Content.Contains("RoundTrip Runner", StringComparison.Ordinal),
                        "imported content must round-trip");
                    string amendedKarmaXml = RoundTripXml("RoundTrip Runner", 27, 2500);
                    WorkspaceStoreMutationResult replace = scopeA.Store.ReplaceWorkspaceDocument(
                        scopeA.Owner,
                        imported.Id,
                        read.Value!.ContentRevision,
                        new WorkspaceDocument(
                            amendedKarmaXml,
                            document.RulesetId,
                            document.Format));
                    Assert.IsTrue(replace.Success, replace.Error);

                    // Signed-in outbound push over the ordinary DI-registered sync.
                    DesktopWorkspaceRoamingResult push = scopeA.Sync.SynchronizeOutboundAsync(
                        scopeA.Owner,
                        imported.Id,
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Applied, push.Outcome);
                }

                // Two pushes accepted by the Hub by the time we assert: the import
                // auto-push (revision 1, karma 15) plus the amended push (revision 2,
                // karma 27).
                hub.AssertSingleSnapshot(workspaceIdValue, "RoundTrip Runner", 27m, 2L);

                // ---- Device B (Android stand-in, head "blazor-desktop"): pull the amendment ----
                using (DeviceScope scopeB = OpenDevice(persistenceRootB, stateRootB, "blazor-desktop", hubBase, claimed: true))
                {
                    DesktopWorkspaceRoamingResult pull = scopeB.Sync.SynchronizeInboundAsync(
                        scopeB.Owner,
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Applied, pull.Outcome);

                    WorkspaceStoreReadResult read = scopeB.Store.Get(scopeB.Owner, new CharacterWorkspaceId(workspaceIdValue));
                    Assert.IsTrue(read.Success, read.Error);
                    StringAssert.Contains(read.Value!.Document.Content, "RoundTrip Runner");
                    StringAssert.Contains(read.Value!.Document.Content, "<karma>27</karma>");

                    CharacterFileSummary? summary = scopeB.WorkspaceService.GetSummary(
                        scopeB.Owner, new CharacterWorkspaceId(workspaceIdValue));
                    Assert.IsNotNull(summary);
                    Assert.AreEqual(27m, summary.Karma);

                    // ---- Return sync: device B amends and pushes; device A pulls back ----
                    WorkspaceStoreReadResult readBeforeAmend = scopeB.Store.Get(
                        scopeB.Owner, new CharacterWorkspaceId(workspaceIdValue));
                    Assert.IsTrue(readBeforeAmend.Success, readBeforeAmend.Error);
                    string returnKarmaXml = RoundTripXml("RoundTrip Runner", 31, 2500);
                    WorkspaceStoreMutationResult amendB = scopeB.Store.ReplaceWorkspaceDocument(
                        scopeB.Owner,
                        new CharacterWorkspaceId(workspaceIdValue),
                        readBeforeAmend.Value!.ContentRevision,
                        new WorkspaceDocument(
                            returnKarmaXml,
                            readBeforeAmend.Value!.Document.RulesetId,
                            readBeforeAmend.Value!.Document.Format));
                    Assert.IsTrue(amendB.Success, amendB.Error);

                    DesktopWorkspaceRoamingResult pushB = scopeB.Sync.SynchronizeOutboundAsync(
                        scopeB.Owner,
                        new CharacterWorkspaceId(workspaceIdValue),
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Applied, pushB.Outcome);
                }

                // ---- Device A pulls the return amendment ----
                using (DeviceScope scopeA2 = OpenDevice(persistenceRootA, stateRootA, "avalonia", hubBase, claimed: true))
                {
                    DesktopWorkspaceRoamingResult pull = scopeA2.Sync.SynchronizeInboundAsync(
                        scopeA2.Owner,
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Applied, pull.Outcome);

                    WorkspaceStoreReadResult read = scopeA2.Store.Get(scopeA2.Owner, new CharacterWorkspaceId(workspaceIdValue));
                    Assert.IsTrue(read.Success, read.Error);
                    StringAssert.Contains(read.Value!.Document.Content, "<karma>31</karma>");

                    // Re-running inbound stays stable: the local document is now byte-equal
                    // to the remote payload, so no further revision churn occurs.
                    DesktopWorkspaceRoamingResult again = scopeA2.Sync.SynchronizeInboundAsync(
                        scopeA2.Owner,
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.AreEqual(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, again.Outcome);
                }
            }
            finally
            {
                DeleteTempDirectory(persistenceRootA);
                DeleteTempDirectory(persistenceRootB);
                DeleteTempDirectory(stateRootA);
                DeleteTempDirectory(stateRootB);
            }
        }
    }

    [TestMethod]
    public void SignedIn_round_trip_is_isolated_from_guest_devices()
    {
        lock (EnvironmentLock)
        {
            string persistenceRootA = CreateTempDirectory();
            string persistenceRootGuest = CreateTempDirectory();
            string stateRootA = CreateTempDirectory();
            string stateRootGuest = CreateTempDirectory();
            try
            {
                using FakeContinuationHub hub = new();
                Uri hubBase = hub.BaseAddress;
                string workspaceIdValue = string.Empty;

                using (DeviceScope scopeA = OpenDevice(persistenceRootA, stateRootA, "avalonia", hubBase, claimed: true))
                {
                    IChummerClient clientA = scopeA.Provider.GetRequiredService<IChummerClient>();
                    WorkspaceImportResult imported = clientA.ImportAsync(
                        new WorkspaceImportDocument(
                            RoundTripXml("Isolated Runner", 7, 1000),
                            RulesetDefaults.Sr6,
                            WorkspaceDocumentFormat.NativeXml),
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.IsNotNull(imported);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(imported.Id.Value));
                    workspaceIdValue = imported.Id.Value;

                    DesktopWorkspaceRoamingResult push = scopeA.Sync.SynchronizeOutboundAsync(
                        scopeA.Owner,
                        imported.Id,
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Applied, push.Outcome);
                }

                int requestsBefore = hub.RequestCount;
                using (DeviceScope scopeGuest = OpenDevice(persistenceRootGuest, stateRootGuest, "blazor-desktop", hubBase, claimed: false))
                {
                    DesktopWorkspaceRoamingResult pull = scopeGuest.Sync.SynchronizeInboundAsync(
                        scopeGuest.Owner,
                        CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Unavailable, pull.Outcome);

                    WorkspaceStoreReadResult read = scopeGuest.Store.Get(scopeGuest.Owner, new CharacterWorkspaceId(workspaceIdValue));
                    Assert.IsFalse(read.Success, "guest device must not receive the claimed owner's workspace");
                }
                Assert.AreEqual(requestsBefore, hub.RequestCount, "guest inbound must not touch the Hub");
            }
            finally
            {
                DeleteTempDirectory(persistenceRootA);
                DeleteTempDirectory(persistenceRootGuest);
                DeleteTempDirectory(stateRootA);
                DeleteTempDirectory(stateRootGuest);
            }
        }
    }

    private static string RoundTripXml(string name, int karma, int nuyen)
        => "<character><name>" + name + "</name><alias>" + name + "</alias>"
            + "<metatype>Human</metatype><buildmethod>Priority</buildmethod>"
            + "<createdversion>1.0</createdversion><appversion>1.0</appversion>"
            + "<karma>" + karma.ToString(CultureInfo.InvariantCulture)
            + "</karma><nuyen>" + nuyen.ToString(CultureInfo.InvariantCulture)
            + "</nuyen><created>True</created></character>";

    private static string CreateTempDirectory()
        => Path.Combine(Path.GetTempPath(), "chummer-t1005-roundtrip", Guid.NewGuid().ToString("N"));

    private static void DeleteTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }
    }

    /// <summary>
    /// Opens one simulated device: its own CHUMMER_DESKTOP_STATE_ROOT (install-linking
    /// state, roaming-sync ledger and workspace store all resolve beneath it), the
    /// ordinary startup DI composition pointed at the shared local Hub, and the resolved
    /// grant-bound sync + owner scope. Mirrors the proven
    /// ServiceCollectionDesktopRuntimeExtensionsTests helpers (WriteClaimedInstallState,
    /// BuildProvider) on this machine's runtime plane.
    /// </summary>
    private DeviceScope OpenDevice(
        string persistenceRoot,
        string stateRoot,
        string headId,
        Uri hubBase,
        bool claimed)
    {
        if (claimed)
        {
            WriteClaimedInstallState(stateRoot, headId, subjectId: "subject-roundtrip", userId: "user-roundtrip");
        }
        else
        {
            WriteGuestInstallState(stateRoot, headId);
        }

        string? previousClientMode = Environment.GetEnvironmentVariable(ClientModeEnvironmentVariable);
        string? previousDesktopClientMode = Environment.GetEnvironmentVariable(DesktopClientModeEnvironmentVariable);
        string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
        string? previousApiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        string? previousWebBase = Environment.GetEnvironmentVariable(WebBaseUrlEnvironmentVariable);
        string? previousStateRoot = Environment.GetEnvironmentVariable(DesktopStateRootEnvironmentVariable);
        string? previousStatePath = Environment.GetEnvironmentVariable(StatePathEnvironmentVariable);

        Environment.SetEnvironmentVariable(ClientModeEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(DesktopClientModeEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, hubBase.ToString());
        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(WebBaseUrlEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(DesktopStateRootEnvironmentVariable, stateRoot);
        Environment.SetEnvironmentVariable(StatePathEnvironmentVariable, null);

        ServiceCollection services = new();
        services.AddChummerLocalRuntimeClient(persistenceRoot, ContentRoot, headId);
        ServiceProvider provider = services.BuildServiceProvider();

        IDesktopWorkspaceRoamingSync sync = provider.GetRequiredService<IDesktopWorkspaceRoamingSync>();
        IOwnerContextAccessor ownerAccessor = provider.GetRequiredService<IOwnerContextAccessor>();
        OwnerScope owner = ownerAccessor.Current;
        IWorkspaceStore store = provider.GetRequiredService<IWorkspaceStore>();
        IWorkspaceService workspaceService = provider.GetRequiredService<IWorkspaceService>();

        if (claimed)
        {
            Assert.IsInstanceOfType<GrantBoundDesktopWorkspaceRoamingSync>(sync);
            StringAssert.Contains(owner.NormalizedValue, "subject-roundtrip");
        }
        else
        {
            Assert.IsInstanceOfType<GrantBoundDesktopWorkspaceRoamingSync>(sync);
        }

        return new DeviceScope(provider, sync, owner, store, workspaceService, stateRoot,
            previousClientMode, previousDesktopClientMode, previousApiBase, previousApiKey,
            previousWebBase, previousStateRoot, previousStatePath);
    }

    private static void WriteClaimedInstallState(string desktopStateRoot, string headId, string subjectId, string userId)
    {
        string platform = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "macos"
            : OperatingSystem.IsLinux() ? "linux"
            : "unknown";
        string arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
        };

        string stateDirectory = Path.Combine(desktopStateRoot, "Chummer6", "install-linking", headId, platform, arch);
        Directory.CreateDirectory(stateDirectory);

        DesktopInstallLinkingState state = new(
            InstallationId: $"ins-{headId}",
            HeadId: headId,
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: platform,
            Arch: arch,
            Status: "claimed",
            CreatedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            LaunchCount: 1,
            LastStartedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            ClaimedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: OperatingSystem.IsWindows() ? string.Empty : "private-key",
            ClaimTicketId: "ticket-1",
            LastClaimCode: null,
            LastClaimMessage: "linked",
            LastClaimError: null,
            LastClaimAttemptUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            GrantId: "grant-1",
            GrantToken: "grant-token",
            GrantIssuedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            GrantExpiresAtUtc: DateTimeOffset.Parse("2026-07-03T12:00:00+00:00"),
            UserId: userId,
            SubjectId: subjectId);
        File.WriteAllText(Path.Combine(stateDirectory, "state.json"), JsonSerializer.Serialize(state));
    }

    private static void WriteGuestInstallState(string desktopStateRoot, string headId)
    {
        string platform = OperatingSystem.IsWindows() ? "windows" : "other";
        string stateDirectory = Path.Combine(desktopStateRoot, "Chummer6", "install-linking", headId, platform, "x64");
        Directory.CreateDirectory(stateDirectory);

        DesktopInstallLinkingState state = new(
            InstallationId: $"ins-{headId}",
            HeadId: headId,
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: platform,
            Arch: "x64",
            Status: "guest",
            CreatedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00",
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            LaunchCount: 1,
            LastStartedAtUtc: DateTimeOffset.Parse("2026-06-03T12:00:00+00:00"),
            ClaimedAtUtc: null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: string.Empty);
        File.WriteAllText(Path.Combine(stateDirectory, "state.json"), JsonSerializer.Serialize(state));
    }

    private sealed class DeviceScope : IDisposable
    {
        public DeviceScope(
            ServiceProvider provider,
            IDesktopWorkspaceRoamingSync sync,
            OwnerScope owner,
            IWorkspaceStore store,
            IWorkspaceService workspaceService,
            string stateRoot,
            string? previousClientMode,
            string? previousDesktopClientMode,
            string? previousApiBase,
            string? previousApiKey,
            string? previousWebBase,
            string? previousStateRoot,
            string? previousStatePath)
        {
            Provider = provider;
            Sync = sync;
            Owner = owner;
            Store = store;
            WorkspaceService = workspaceService;
            StateRoot = stateRoot;
            _previousClientMode = previousClientMode;
            _previousDesktopClientMode = previousDesktopClientMode;
            _previousApiBase = previousApiBase;
            _previousApiKey = previousApiKey;
            _previousWebBase = previousWebBase;
            _previousStateRoot = previousStateRoot;
            _previousStatePath = previousStatePath;
        }

        public ServiceProvider Provider { get; }
        public IDesktopWorkspaceRoamingSync Sync { get; }
        public OwnerScope Owner { get; }
        public IWorkspaceStore Store { get; }
        public IWorkspaceService WorkspaceService { get; }
        public string StateRoot { get; }
        private readonly string? _previousClientMode;
        private readonly string? _previousDesktopClientMode;
        private readonly string? _previousApiBase;
        private readonly string? _previousApiKey;
        private readonly string? _previousWebBase;
        private readonly string? _previousStateRoot;
        private readonly string? _previousStatePath;

        public void Dispose()
        {
            Provider.Dispose();
            Environment.SetEnvironmentVariable(ClientModeEnvironmentVariable, _previousClientMode);
            Environment.SetEnvironmentVariable(DesktopClientModeEnvironmentVariable, _previousDesktopClientMode);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, _previousApiBase);
            Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, _previousApiKey);
            Environment.SetEnvironmentVariable(WebBaseUrlEnvironmentVariable, _previousWebBase);
            Environment.SetEnvironmentVariable(DesktopStateRootEnvironmentVariable, _previousStateRoot);
            Environment.SetEnvironmentVariable(StatePathEnvironmentVariable, _previousStatePath);
        }
    }

    /// <summary>
    /// Local Hub stub listening on a real 127.0.0.1 port and speaking the exact
    /// continuation-workspace list/upsert protocol the grant-bound sync uses, so the
    /// ordinary DI-registered sync round-trips real HTTP without the production Hub.
    /// Owned by the test and shared by both devices.
    /// </summary>
    private sealed class FakeContinuationHub : IDisposable
    {
        private sealed record StoredSnapshot(
            string WorkspaceId,
            string RulesetId,
            string Format,
            int SchemaVersion,
            string PayloadKind,
            string Payload,
            DateTimeOffset UpdatedAtUtc,
            long RemoteRevision,
            string ServerToken,
            string Name,
            string Alias,
            string Metatype,
            string BuildMethod,
            string CreatedVersion,
            string AppVersion,
            decimal Karma,
            decimal Nuyen,
            bool Created,
            string OriginInstallationId);

        private readonly object _gate = new();
        private readonly Dictionary<string, StoredSnapshot> _snapshots = new(StringComparer.Ordinal);
        private readonly HttpListener _listener;
        private readonly Task _loop;
        private int _requestCount;

        public FakeContinuationHub()
        {
            int port = FreePort();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _loop = Task.Run(ListenLoop);
            BaseAddress = new Uri($"http://127.0.0.1:{port}/");
        }

        public Uri BaseAddress { get; }

        public int RequestCount
        {
            get { lock (_gate) { return _requestCount; } }
        }

        public void AssertSingleSnapshot(string workspaceId, string expectedName, decimal expectedKarma, long expectedRevision)
        {
            lock (_gate)
            {
                Assert.AreEqual(1, _snapshots.Count, "Hub must hold exactly the pushed workspace");
                Assert.IsTrue(_snapshots.TryGetValue(workspaceId, out StoredSnapshot? snapshot), "Hub must hold the pushed workspace id");
                Assert.IsNotNull(snapshot);
                Assert.AreEqual(expectedName, snapshot.Name);
                Assert.AreEqual(expectedKarma, snapshot.Karma);
                Assert.AreEqual(expectedRevision, snapshot.RemoteRevision);
            }
        }

        private static int FreePort()
        {
            TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private async Task ListenLoop()
        {
            while (_listener.IsListening)
            {
                try
                {
                    HttpListenerContext ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                    ThreadPool.QueueUserWorkItem(_ => HandleContext(ctx));
                }
                catch (Exception) when (!_listener.IsListening)
                {
                    break;
                }
                catch (Exception)
                {
                    // keep serving; a single failed accept must not kill the loop
                }
            }
        }

        private void HandleContext(HttpListenerContext ctx)
        {
            try
            {
                HttpListenerRequest req = ctx.Request;
                string body;
                using (StreamReader reader = new(req.InputStream, Encoding.UTF8))
                {
                    body = reader.ReadToEnd();
                }

                HttpResponseMessage response;
                lock (_gate)
                {
                    _requestCount++;
                    string path = req.Url?.AbsolutePath ?? string.Empty;
                    if (req.HttpMethod == "POST" && path.EndsWith(
                            "/api/v1/install-linking/continuation/workspaces/upsert",
                            StringComparison.Ordinal))
                    {
                        response = HandleUpsert(body);
                    }
                    else if (req.HttpMethod == "POST" && path.EndsWith(
                            "/api/v1/install-linking/continuation/workspaces/list",
                            StringComparison.Ordinal))
                    {
                        response = HandleList();
                    }
                    else
                    {
                        response = new HttpResponseMessage(HttpStatusCode.NotFound);
                    }
                }

                byte[] bytes = response.Content?.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                    ?? Array.Empty<byte>();
                ctx.Response.StatusCode = (int)response.StatusCode;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.OutputStream.Close();
                response.Dispose();
            }
            catch
            {
                try
                {
                    ctx.Response.StatusCode = 500;
                    ctx.Response.OutputStream.Close();
                }
                catch
                {
                    // socket already gone
                }
                throw new InvalidOperationException("hub stub failed");
            }
        }

        private HttpResponseMessage HandleUpsert(string body)
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            string workspaceId = root.GetProperty("workspaceId").GetString()!;
            string rulesetId = root.GetProperty("rulesetId").GetString()!;
            string format = root.GetProperty("format").GetString()!;
            int schemaVersion = root.GetProperty("schemaVersion").GetInt32();
            string payloadKind = root.GetProperty("payloadKind").GetString()!;
            string payload = root.GetProperty("payload").GetString()!;
            DateTimeOffset updatedAtUtc = root.GetProperty("updatedAtUtc").GetDateTimeOffset();
            string originInstallationId = root.GetProperty("originInstallationId").GetString()!;
            string name = root.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? string.Empty : string.Empty;
            string alias = root.TryGetProperty("alias", out JsonElement a) ? a.GetString() ?? string.Empty : string.Empty;
            string metatype = root.TryGetProperty("metatype", out JsonElement m) ? m.GetString() ?? string.Empty : string.Empty;
            string buildMethod = root.TryGetProperty("buildMethod", out JsonElement b) ? b.GetString() ?? string.Empty : string.Empty;
            string createdVersion = root.TryGetProperty("createdVersion", out JsonElement cv) ? cv.GetString() ?? string.Empty : string.Empty;
            string appVersion = root.TryGetProperty("appVersion", out JsonElement av) ? av.GetString() ?? string.Empty : string.Empty;
            decimal karma = root.TryGetProperty("karma", out JsonElement k) ? k.GetDecimal() : 0m;
            decimal nuyen = root.TryGetProperty("nuyen", out JsonElement ny) ? ny.GetDecimal() : 0m;
            bool created = root.TryGetProperty("created", out JsonElement c) && c.GetBoolean();
            long contentRevision = root.TryGetProperty("contentRevision", out JsonElement cr) ? cr.GetInt64() : 0L;

            long? expectedRemoteRevision = root.TryGetProperty("expectedRemoteRevision", out JsonElement er)
                && er.ValueKind == JsonValueKind.Number ? er.GetInt64() : null;

            if (_snapshots.TryGetValue(workspaceId, out StoredSnapshot? existing))
            {
                long expected = expectedRemoteRevision ?? -1L;
                if (existing.RemoteRevision != expected)
                {
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                }
            }

            long remoteRevision = (existing?.RemoteRevision ?? 0L) + 1L;
            string serverToken = "srv-" + remoteRevision.ToString(CultureInfo.InvariantCulture);
            StoredSnapshot stored = new(
                WorkspaceId: workspaceId,
                RulesetId: rulesetId,
                Format: format,
                SchemaVersion: schemaVersion,
                PayloadKind: payloadKind,
                Payload: payload,
                UpdatedAtUtc: updatedAtUtc,
                RemoteRevision: remoteRevision,
                ServerToken: serverToken,
                Name: name,
                Alias: alias,
                Metatype: metatype,
                BuildMethod: buildMethod,
                CreatedVersion: createdVersion,
                AppVersion: appVersion,
                Karma: karma,
                Nuyen: nuyen,
                Created: created,
                OriginInstallationId: originInstallationId);

            _snapshots[workspaceId] = stored;
            _ = contentRevision;

            object receipt = new
            {
                snapshot = new
                {
                    workspaceId = stored.WorkspaceId,
                    rulesetId = stored.RulesetId,
                    format = stored.Format,
                    schemaVersion = stored.SchemaVersion,
                    payloadKind = stored.PayloadKind,
                    payload = stored.Payload,
                    updatedAtUtc = stored.UpdatedAtUtc,
                    remoteRevision = stored.RemoteRevision,
                    serverToken = stored.ServerToken,
                    originInstallationId = stored.OriginInstallationId,
                    summary = new
                    {
                        name = stored.Name,
                        alias = stored.Alias,
                        metatype = stored.Metatype,
                        buildMethod = stored.BuildMethod,
                        createdVersion = stored.CreatedVersion,
                        appVersion = stored.AppVersion,
                        karma = stored.Karma,
                        nuyen = stored.Nuyen,
                        created = stored.Created
                    }
                }
            };
            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(receipt));
        }

        private HttpResponseMessage HandleList()
        {
            var snapshots = _snapshots.Values
                .OrderBy(s => s.RemoteRevision)
                .Select(s => new
                {
                    workspaceId = s.WorkspaceId,
                    rulesetId = s.RulesetId,
                    format = s.Format,
                    schemaVersion = s.SchemaVersion,
                    payloadKind = s.PayloadKind,
                    payload = s.Payload,
                    updatedAtUtc = s.UpdatedAtUtc,
                    remoteRevision = s.RemoteRevision,
                    serverToken = s.ServerToken,
                    originInstallationId = s.OriginInstallationId,
                    summary = new
                    {
                        name = s.Name,
                        alias = s.Alias,
                        metatype = s.Metatype,
                        buildMethod = s.BuildMethod,
                        createdVersion = s.CreatedVersion,
                        appVersion = s.AppVersion,
                        karma = s.Karma,
                        nuyen = s.Nuyen,
                        created = s.Created
                    }
                })
                .ToArray();
            object payload = new
            {
                snapshots,
                serverToken = snapshots.Length > 0 ? snapshots[^1].serverToken : null
            };
            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(payload));
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json)
            => new(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

        public void Dispose()
        {
            try
            {
                _listener.Stop();
            }
            catch
            {
                // already stopped
            }
            try
            {
                _loop.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // loop faults are non-fatal for the test
            }
        }
    }
}
