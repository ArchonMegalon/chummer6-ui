#nullable enable annotations

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Infrastructure.Workspaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class GrantBoundDesktopWorkspaceRoamingSyncTests
{
    private const string ApiBaseUrlEnvironmentVariable = "CHUMMER_API_BASE_URL";
    private const string WebBaseUrlEnvironmentVariable = "CHUMMER_WEB_BASE_URL";
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);

    [TestMethod]
    public async Task SynchronizeInboundAsync_imports_remote_snapshot_when_local_workspace_is_missing()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InMemoryWorkspaceStore store = new();
                RecordingWorkspaceService workspaceService = new();
                OwnerScope owner = new("install-account:subject-alpha");
                HttpClient client = new(new StubHandler(static request =>
                {
                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/list", StringComparison.Ordinal) == true)
                    {
                        return JsonResponse("""
                            {
                              "snapshots": [
                                {
                                  "workspaceId": "ws-remote",
                                  "rulesetId": "sr6",
                                  "format": "NativeXml",
                                  "schemaVersion": 1,
                                  "payloadKind": "workspace",
                                  "payload": "<character><name>Remote Runner</name></character>",
                                  "updatedAtUtc": "2026-06-03T14:00:00Z"
                                }
                              ]
                            }
                            """);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    client,
                    () => ClaimedState());

                await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                WorkspaceStoreReadResult read = store.Get(owner, new CharacterWorkspaceId("ws-remote"));
                Assert.IsTrue(read.Success, read.Error);
                WorkspaceDocument document = read.Value!.Document;
                Assert.AreEqual("sr6", document.RulesetId);
                StringAssert.Contains(document.Content, "Remote Runner");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeOutboundAsync_posts_local_snapshot_to_hub()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InMemoryWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId workspaceId = store.CreateWorkspaceDocument(owner, new WorkspaceDocument(
                        "<character><name>Local Runner</name></character>",
                        RulesetId: "sr5",
                        Format: WorkspaceDocumentFormat.NativeXml))
                    .Entry!.Value.Id;
                RecordingWorkspaceService workspaceService = new()
                {
                    SummaryByWorkspaceId =
                    {
                        [workspaceId.Value] = new CharacterFileSummary(
                            Name: "Local Runner",
                            Alias: "Local Runner",
                            Metatype: "Human",
                            BuildMethod: "Priority",
                            CreatedVersion: "5",
                            AppVersion: "5",
                            Karma: 3,
                            Nuyen: 1000,
                            Created: true)
                    }
                };
                string? capturedPayload = null;
                HttpClient client = new(new StubHandler(request =>
                {
                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/upsert", StringComparison.Ordinal) == true)
                    {
                        capturedPayload = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                        return JsonResponse("""
                            {
                              "snapshot": {
                                "workspaceId": "ignored",
                                "rulesetId": "sr5",
                                "format": "NativeXml",
                                "schemaVersion": 1,
                                "payloadKind": "workspace",
                                "payload": "<character />",
                                "updatedAtUtc": "2026-06-03T14:00:00Z",
                                "originInstallationId": "ins-a",
                                "summary": {
                                  "name": "Local Runner",
                                  "alias": "Local Runner",
                                  "metatype": "Human",
                                  "buildMethod": "Priority",
                                  "createdVersion": "5",
                                  "appVersion": "5",
                                  "karma": 3,
                                  "nuyen": 1000,
                                  "created": true
                                }
                              }
                            }
                            """);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    client,
                    () => ClaimedState());

                await sync.SynchronizeOutboundAsync(owner, workspaceId, CancellationToken.None);

                Assert.IsNotNull(capturedPayload);
                StringAssert.Contains(capturedPayload, "\"workspaceId\":\"" + workspaceId.Value + "\"");
                StringAssert.Contains(capturedPayload, "Local Runner");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_noops_without_configured_hub_base_url()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            string? previousWebBase = Environment.GetEnvironmentVariable(WebBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(WebBaseUrlEnvironmentVariable, null);
            try
            {
                InMemoryWorkspaceStore store = new();
                RecordingWorkspaceService workspaceService = new();
                OwnerScope owner = new("install-account:subject-alpha");
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    new HttpClient(new StubHandler(static _ => throw new AssertFailedException("hub should not be called without a configured base url."))),
                    () => ClaimedState());

                await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                Assert.AreEqual(0, store.List(owner).Count);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
                Environment.SetEnvironmentVariable(WebBaseUrlEnvironmentVariable, previousWebBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeOutboundAsync_noops_for_guest_install_without_grant()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InMemoryWorkspaceStore store = new();
                OwnerScope owner = new("local-single-user");
                CharacterWorkspaceId workspaceId = store.CreateWorkspaceDocument(new WorkspaceDocument(
                        "<character><name>Guest Runner</name></character>",
                        RulesetId: "sr5",
                        Format: WorkspaceDocumentFormat.NativeXml))
                    .Entry!.Value.Id;
                RecordingWorkspaceService workspaceService = new()
                {
                    SummaryByWorkspaceId =
                    {
                        [workspaceId.Value] = new CharacterFileSummary(
                            Name: "Guest Runner",
                            Alias: "Guest Runner",
                            Metatype: "Human",
                            BuildMethod: "Priority",
                            CreatedVersion: "5",
                            AppVersion: "5",
                            Karma: 0,
                            Nuyen: 0,
                            Created: true)
                    }
                };
                bool called = false;
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    new HttpClient(new StubHandler(_ =>
                    {
                        called = true;
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    }))
                    {
                        BaseAddress = new Uri("https://hub.example/")
                    },
                    GuestState);

                await sync.SynchronizeOutboundAsync(owner, workspaceId, CancellationToken.None);

                Assert.IsFalse(called);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_pushes_local_snapshot_when_local_copy_is_newer_than_remote()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InMemoryWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId workspaceId = store.CreateWorkspaceDocument(owner, new WorkspaceDocument(
                        "<character><name>Local Newer</name></character>",
                        RulesetId: "sr6",
                        Format: WorkspaceDocumentFormat.NativeXml))
                    .Entry!.Value.Id;
                RecordingWorkspaceService workspaceService = new()
                {
                    SummaryByWorkspaceId =
                    {
                        [workspaceId.Value] = new CharacterFileSummary(
                            Name: "Local Newer",
                            Alias: "Local Newer",
                            Metatype: "Human",
                            BuildMethod: "Priority",
                            CreatedVersion: "6",
                            AppVersion: "6",
                            Karma: 5,
                            Nuyen: 2000,
                            Created: true)
                    }
                };
                int upsertCalls = 0;
                HttpClient client = new(new StubHandler(request =>
                {
                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/list", StringComparison.Ordinal) == true)
                    {
                        return JsonResponse("""
                            {
                              "snapshots": [
                                {
                                  "workspaceId": "ws-1",
                                  "rulesetId": "sr6",
                                  "format": "NativeXml",
                                  "schemaVersion": 1,
                                  "payloadKind": "workspace",
                                  "payload": "<character><name>Remote Older</name></character>",
                                  "updatedAtUtc": "2000-01-01T00:00:00Z"
                                }
                              ]
                            }
                            """);
                    }

                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/upsert", StringComparison.Ordinal) == true)
                    {
                        upsertCalls++;
                        return JsonResponse("""
                            {
                              "snapshot": {
                                "workspaceId": "ws-1",
                                "rulesetId": "sr6",
                                "format": "NativeXml",
                                "schemaVersion": 1,
                                "payloadKind": "workspace",
                                "payload": "<character><name>Local Newer</name></character>",
                                "updatedAtUtc": "2026-06-03T16:00:00Z",
                                "originInstallationId": "ins-a",
                                "summary": {
                                  "name": "Local Newer",
                                  "alias": "Local Newer",
                                  "metatype": "Human",
                                  "buildMethod": "Priority",
                                  "createdVersion": "6",
                                  "appVersion": "6",
                                  "karma": 5,
                                  "nuyen": 2000,
                                  "created": true
                                }
                              }
                            }
                            """);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    client,
                    () => ClaimedState());

                await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                Assert.AreEqual(1, upsertCalls);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_applies_normal_newer_snapshot_once_then_reports_already_current()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InMemoryWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId workspaceId = store.CreateWorkspaceDocument(owner, new WorkspaceDocument(
                        "<character><name>Local older</name></character>",
                        RulesetId: "sr6",
                        Format: WorkspaceDocumentFormat.NativeXml))
                    .Entry!.Value.Id;
                DateTimeOffset remoteUpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(1);
                HttpClient client = new(new StubHandler(request =>
                {
                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/list", StringComparison.Ordinal) == true)
                    {
                        return JsonResponse($$"""
                            {
                              "snapshots": [
                                {
                                  "workspaceId": "{{workspaceId.Value}}",
                                  "rulesetId": "sr6",
                                  "format": "NativeXml",
                                  "schemaVersion": 1,
                                  "payloadKind": "workspace",
                                  "payload": "<character><name>Remote applied once</name></character>",
                                  "updatedAtUtc": "{{remoteUpdatedAtUtc:O}}",
                                  "remoteRevision": 7
                                }
                              ]
                            }
                            """);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    new RecordingWorkspaceService(),
                    client,
                    () => ClaimedState());

                DesktopWorkspaceRoamingResult first = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);
                DesktopWorkspaceRoamingResult second = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                WorkspaceStoreReadResult read = store.Get(owner, workspaceId);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Applied, first.Outcome);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, second.Outcome);
                Assert.AreEqual(2L, read.Value?.ContentRevision);
                StringAssert.Contains(read.Value?.Document.Content ?? string.Empty, "Remote applied once");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_rejects_far_future_remote_and_later_local_edit_still_propagates()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InMemoryWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId workspaceId = store.CreateWorkspaceDocument(owner, new WorkspaceDocument(
                        "<character><name>Local Older</name></character>",
                        RulesetId: "sr6",
                        Format: WorkspaceDocumentFormat.NativeXml))
                    .Entry!.Value.Id;
                RecordingWorkspaceService workspaceService = new();
                workspaceService.SummaryByWorkspaceId[workspaceId.Value] = new CharacterFileSummary(
                    Name: "Local Older",
                    Alias: "Local Older",
                    Metatype: "Human",
                    BuildMethod: "Priority",
                    CreatedVersion: "5",
                    AppVersion: "5",
                    Karma: 0,
                    Nuyen: 0,
                    Created: true);
                int upsertCalls = 0;
                HttpClient client = new(new StubHandler(request =>
                {
                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/list", StringComparison.Ordinal) == true)
                    {
                        return JsonResponse($$"""
                            {
                              "snapshots": [
                                {
                                  "workspaceId": "{{workspaceId.Value}}",
                                  "rulesetId": "sr6",
                                  "format": "NativeXml",
                                  "schemaVersion": 1,
                                  "payloadKind": "workspace",
                                  "payload": "<character><name>Remote Newer</name></character>",
                                  "updatedAtUtc": "2999-01-01T00:00:00Z"
                                }
                              ]
                            }
                            """);
                    }

                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/upsert", StringComparison.Ordinal) == true)
                    {
                        Interlocked.Increment(ref upsertCalls);
                        return JsonResponse("{\"remoteRevision\":42,\"serverToken\":\"remote-42\"}");
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    client,
                    () => ClaimedState());

                DesktopWorkspaceRoamingResult first = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);
                DesktopWorkspaceRoamingResult second = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                WorkspaceStoreReadResult read = store.Get(owner, workspaceId);
                Assert.IsTrue(read.Success, read.Error);
                WorkspaceDocument document = read.Value!.Document;
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Conflict, first.Outcome);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Conflict, second.Outcome);
                Assert.AreEqual(1L, read.Value.ContentRevision);
                StringAssert.Contains(document.Content, "Local Older");
                Assert.AreEqual(2, upsertCalls);

                WorkspaceStoreMutationResult edited = store.ReplaceWorkspaceDocument(
                    owner,
                    workspaceId,
                    read.Value.ContentRevision,
                    new WorkspaceDocument(
                        "<character><name>Later local edit</name></character>",
                        RulesetId: "sr6",
                        Format: WorkspaceDocumentFormat.NativeXml));
                Assert.IsTrue(edited.Success, edited.Error);

                DesktopWorkspaceRoamingResult afterEdit = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Conflict, afterEdit.Outcome);
                Assert.AreEqual(3, upsertCalls);
                Assert.AreEqual(2L, store.Get(owner, workspaceId).Value?.ContentRevision);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_skips_byte_equivalent_far_future_snapshot_without_revision_churn()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                const string payload = "<character><name>Same bytes</name></character>";
                InMemoryWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId workspaceId = store.CreateWorkspaceDocument(owner, new WorkspaceDocument(
                        payload,
                        RulesetId: "sr6",
                        Format: WorkspaceDocumentFormat.NativeXml))
                    .Entry!.Value.Id;
                int upsertCalls = 0;
                HttpClient client = new(new StubHandler(request =>
                {
                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/list", StringComparison.Ordinal) == true)
                    {
                        return JsonResponse($$"""
                            {
                              "serverToken": "remote-99",
                              "snapshots": [
                                {
                                  "workspaceId": "{{workspaceId.Value}}",
                                  "rulesetId": "sr6",
                                  "format": "NativeXml",
                                  "schemaVersion": 1,
                                  "payloadKind": "workspace",
                                  "payload": "<character><name>Same bytes</name></character>",
                                  "updatedAtUtc": "2999-01-01T00:00:00Z",
                                  "remoteRevision": 99,
                                  "serverToken": "remote-99"
                                }
                              ]
                            }
                            """);
                    }

                    Interlocked.Increment(ref upsertCalls);
                    return JsonResponse("{}");
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    new RecordingWorkspaceService(),
                    client,
                    () => ClaimedState());

                DesktopWorkspaceRoamingResult first = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);
                DesktopWorkspaceRoamingResult second = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, first.Outcome);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.AlreadyCurrent, second.Outcome);
                Assert.AreEqual(99L, first.RemoteRevision);
                Assert.AreEqual("remote-99", first.ServerToken);
                Assert.AreEqual(1L, store.Get(owner, workspaceId).Value?.ContentRevision);
                Assert.AreEqual(0, upsertCalls);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_uses_web_base_url_when_api_base_url_is_missing()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            string? previousWebBase = Environment.GetEnvironmentVariable(WebBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(WebBaseUrlEnvironmentVariable, "https://web.example/root/");
            try
            {
                InMemoryWorkspaceStore store = new();
                RecordingWorkspaceService workspaceService = new();
                OwnerScope owner = new("install-account:subject-alpha");
                string? seenPath = null;
                HttpClient client = new(new StubHandler(request =>
                {
                    seenPath = request.RequestUri?.AbsoluteUri;
                    return JsonResponse("""
                        {
                          "snapshots": []
                        }
                        """);
                }));
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    client,
                    () => ClaimedState());

                await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                Assert.IsNotNull(seenPath);
                StringAssert.StartsWith(seenPath, "https://web.example/root/api/v1/install-linking/continuation/workspaces/list");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
                Environment.SetEnvironmentVariable(WebBaseUrlEnvironmentVariable, previousWebBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_keeps_local_workspace_when_hub_replies_unauthorized()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InMemoryWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId workspaceId = store.CreateWorkspaceDocument(owner, new WorkspaceDocument(
                        "<character><name>Keep Local</name></character>",
                        RulesetId: "sr5",
                        Format: WorkspaceDocumentFormat.NativeXml))
                    .Entry!.Value.Id;
                RecordingWorkspaceService workspaceService = new();
                HttpClient client = new(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    client,
                    () => ClaimedState());

                DesktopWorkspaceRoamingResult result = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                WorkspaceStoreReadResult read = store.Get(owner, workspaceId);
                Assert.IsTrue(read.Success, read.Error);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Unauthorized, result.Outcome);
                WorkspaceDocument document = read.Value!.Document;
                StringAssert.Contains(document.Content, "Keep Local");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_keeps_local_workspace_when_hub_returns_malformed_json()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InMemoryWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId workspaceId = store.CreateWorkspaceDocument(owner, new WorkspaceDocument(
                        "<character><name>Keep On Parse Failure</name></character>",
                        RulesetId: "sr5",
                        Format: WorkspaceDocumentFormat.NativeXml))
                    .Entry!.Value.Id;
                RecordingWorkspaceService workspaceService = new();
                HttpClient client = new(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ this is not valid json", Encoding.UTF8, "application/json")
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    workspaceService,
                    client,
                    () => ClaimedState());

                DesktopWorkspaceRoamingResult result = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                WorkspaceStoreReadResult read = store.Get(owner, workspaceId);
                Assert.IsTrue(read.Success, read.Error);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Unavailable, result.Outcome);
                WorkspaceDocument document = read.Value!.Document;
                StringAssert.Contains(document.Content, "Keep On Parse Failure");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_does_not_retry_over_a_concurrent_replace_winner()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InterleavingWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId id = new("wsreplaceconflict");
                Assert.IsTrue(store.CreateWorkspaceDocument(owner, id, new WorkspaceDocument(
                    "<character><name>Local Old</name></character>",
                    RulesetId: "sr6")).Success);
                store.ArmReplaceWinner(new WorkspaceDocument(
                    "<character><name>Concurrent Winner</name></character>",
                    RulesetId: "sr6"));
                HttpClient client = new(new StubHandler(request =>
                {
                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/list", StringComparison.Ordinal) == true)
                    {
                        return JsonResponse($$"""
                            {
                              "snapshots": [
                                {
                                  "workspaceId": "{{id.Value}}",
                                  "rulesetId": "sr6",
                                  "format": "NativeXml",
                                  "schemaVersion": 1,
                                  "payloadKind": "workspace",
                                  "payload": "<character><name>Remote Candidate</name></character>",
                                  "updatedAtUtc": "{{DateTimeOffset.UtcNow.AddMinutes(1):O}}"
                                }
                              ]
                            }
                            """);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    new RecordingWorkspaceService(),
                    client,
                    ClaimedState);

                DesktopWorkspaceRoamingResult result = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                WorkspaceStoreReadResult final = store.Get(owner, id);
                Assert.IsTrue(final.Success, final.Error);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Conflict, result.Outcome);
                Assert.AreEqual(2L, final.Value?.ContentRevision);
                StringAssert.Contains(final.Value?.Document.Content ?? string.Empty, "Concurrent Winner");
                Assert.AreEqual(1, store.ReplaceCallCount);
                Assert.AreEqual(1, store.ConflictCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    [TestMethod]
    public async Task SynchronizeInboundAsync_does_not_retry_over_a_concurrent_create_winner()
    {
        using (await EnterEnvironmentLockAsync().ConfigureAwait(false))
        {
            string? previousApiBase = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
            Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, "https://hub.example/");
            try
            {
                InterleavingWorkspaceStore store = new();
                OwnerScope owner = new("install-account:subject-alpha");
                CharacterWorkspaceId id = new("wscreateconflict");
                store.ArmCreateWinner(new WorkspaceDocument(
                    "<character><name>Concurrent Creator</name></character>",
                    RulesetId: "sr6"));
                HttpClient client = new(new StubHandler(request =>
                {
                    if (request.RequestUri?.AbsolutePath.EndsWith("/api/v1/install-linking/continuation/workspaces/list", StringComparison.Ordinal) == true)
                    {
                        return JsonResponse($$"""
                            {
                              "snapshots": [
                                {
                                  "workspaceId": "{{id.Value}}",
                                  "rulesetId": "sr6",
                                  "format": "NativeXml",
                                  "schemaVersion": 1,
                                  "payloadKind": "workspace",
                                  "payload": "<character><name>Remote Candidate</name></character>",
                                  "updatedAtUtc": "{{DateTimeOffset.UtcNow.AddMinutes(1):O}}"
                                }
                              ]
                            }
                            """);
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }))
                {
                    BaseAddress = new Uri("https://hub.example/")
                };
                GrantBoundDesktopWorkspaceRoamingSync sync = new(
                    "avalonia",
                    store,
                    new RecordingWorkspaceService(),
                    client,
                    ClaimedState);

                DesktopWorkspaceRoamingResult result = await sync.SynchronizeInboundAsync(owner, CancellationToken.None);

                WorkspaceStoreReadResult final = store.Get(owner, id);
                Assert.IsTrue(final.Success, final.Error);
                Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Conflict, result.Outcome);
                Assert.AreEqual(1L, final.Value?.ContentRevision);
                StringAssert.Contains(final.Value?.Document.Content ?? string.Empty, "Concurrent Creator");
                Assert.AreEqual(1, store.ConditionalCreateCallCount);
                Assert.AreEqual(1, store.ConflictCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ApiBaseUrlEnvironmentVariable, previousApiBase);
            }
        }
    }

    private static DesktopInstallLinkingState ClaimedState()
        => new(
            InstallationId: "ins-a",
            HeadId: "avalonia",
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "windows",
            Arch: "x64",
            Status: "claimed",
            CreatedAtUtc: DateTimeOffset.UtcNow.AddDays(-5),
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            LaunchCount: 3,
            LastStartedAtUtc: DateTimeOffset.UtcNow,
            ClaimedAtUtc: DateTimeOffset.UtcNow.AddDays(-4),
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: "private-key",
            GrantId: "grant-a",
            GrantToken: "token-a",
            GrantIssuedAtUtc: DateTimeOffset.UtcNow.AddHours(-1),
            GrantExpiresAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            UserId: "user-a",
            SubjectId: "subject-alpha");

    private static DesktopInstallLinkingState GuestState()
        => new(
            InstallationId: "ins-guest",
            HeadId: "avalonia",
            ApplicationVersion: "6.0.1-preview",
            ChannelId: "preview",
            Platform: "windows",
            Arch: "x64",
            Status: "guest",
            CreatedAtUtc: DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            LaunchCount: 1,
            LastStartedAtUtc: DateTimeOffset.UtcNow,
            ClaimedAtUtc: null,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: "private-key");

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static async Task<IDisposable> EnterEnvironmentLockAsync()
    {
        await EnvironmentLock.WaitAsync().ConfigureAwait(false);
        return new EnvironmentLockLease();
    }

    private sealed class EnvironmentLockLease : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                EnvironmentLock.Release();
            }
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handle;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handle)
        {
            _handle = handle;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handle(request));
    }

    private sealed class InterleavingWorkspaceStore : IWorkspaceStore
    {
        private readonly InMemoryWorkspaceStore _inner = new();
        private WorkspaceDocument? _createWinner;
        private WorkspaceDocument? _replaceWinner;
        private int _createArmed;
        private int _replaceArmed;

        public int ConditionalCreateCallCount { get; private set; }

        public int ReplaceCallCount { get; private set; }

        public int ConflictCount { get; private set; }

        public void ArmCreateWinner(WorkspaceDocument winner)
        {
            _createWinner = winner;
            Volatile.Write(ref _createArmed, 1);
        }

        public void ArmReplaceWinner(WorkspaceDocument winner)
        {
            _replaceWinner = winner;
            Volatile.Write(ref _replaceArmed, 1);
        }

        public WorkspaceStoreMutationResult CreateWorkspaceDocument(WorkspaceDocument document)
            => _inner.CreateWorkspaceDocument(document);

        public WorkspaceStoreMutationResult CreateWorkspaceDocument(OwnerScope owner, WorkspaceDocument document)
            => _inner.CreateWorkspaceDocument(owner, document);

        public WorkspaceStoreMutationResult CreateWorkspaceDocument(CharacterWorkspaceId id, WorkspaceDocument document)
        {
            ConditionalCreateCallCount++;
            return TrackConflict(_inner.CreateWorkspaceDocument(id, document));
        }

        public WorkspaceStoreMutationResult CreateWorkspaceDocument(OwnerScope owner, CharacterWorkspaceId id, WorkspaceDocument document)
        {
            ConditionalCreateCallCount++;
            return TrackConflict(_inner.CreateWorkspaceDocument(owner, id, document));
        }

        public IReadOnlyList<WorkspaceStoreEntry> List() => _inner.List();

        public IReadOnlyList<WorkspaceStoreEntry> List(OwnerScope owner) => _inner.List(owner);

        public WorkspaceStoreReadResult Get(CharacterWorkspaceId id)
            => GetCore(OwnerScope.LocalSingleUser, id, trustedLocalScope: true);

        public WorkspaceStoreReadResult Get(OwnerScope owner, CharacterWorkspaceId id)
            => GetCore(owner, id, trustedLocalScope: false);

        public WorkspaceStoreMutationResult ReplaceWorkspaceDocument(CharacterWorkspaceId id, long expectedContentRevision, WorkspaceDocument document)
        {
            ReplaceCallCount++;
            return TrackConflict(_inner.ReplaceWorkspaceDocument(id, expectedContentRevision, document));
        }

        public WorkspaceStoreMutationResult ReplaceWorkspaceDocument(OwnerScope owner, CharacterWorkspaceId id, long expectedContentRevision, WorkspaceDocument document)
        {
            ReplaceCallCount++;
            return TrackConflict(_inner.ReplaceWorkspaceDocument(owner, id, expectedContentRevision, document));
        }

        public WorkspaceStoreMutationResult SaveCheckpoint(CharacterWorkspaceId id, long expectedContentRevision)
            => _inner.SaveCheckpoint(id, expectedContentRevision);

        public WorkspaceStoreMutationResult SaveCheckpoint(OwnerScope owner, CharacterWorkspaceId id, long expectedContentRevision)
            => _inner.SaveCheckpoint(owner, id, expectedContentRevision);

        public WorkspaceStoreMutationResult Delete(CharacterWorkspaceId id, long expectedContentRevision)
            => _inner.Delete(id, expectedContentRevision);

        public WorkspaceStoreMutationResult Delete(OwnerScope owner, CharacterWorkspaceId id, long expectedContentRevision)
            => _inner.Delete(owner, id, expectedContentRevision);

        private WorkspaceStoreReadResult GetCore(
            OwnerScope owner,
            CharacterWorkspaceId id,
            bool trustedLocalScope)
        {
            WorkspaceStoreReadResult stale = trustedLocalScope
                ? _inner.Get(id)
                : _inner.Get(owner, id);
            if (Interlocked.Exchange(ref _createArmed, 0) == 1
                && stale.Outcome == WorkspaceOperationOutcome.Missing
                && _createWinner is WorkspaceDocument createWinner)
            {
                WorkspaceStoreMutationResult won = trustedLocalScope
                    ? _inner.CreateWorkspaceDocument(id, createWinner)
                    : _inner.CreateWorkspaceDocument(owner, id, createWinner);
                Assert.IsTrue(won.Success, won.Error);
            }

            if (Interlocked.Exchange(ref _replaceArmed, 0) == 1
                && stale.Value is WorkspaceStoredDocument current
                && _replaceWinner is WorkspaceDocument replaceWinner)
            {
                WorkspaceStoreMutationResult won = trustedLocalScope
                    ? _inner.ReplaceWorkspaceDocument(id, current.ContentRevision, replaceWinner)
                    : _inner.ReplaceWorkspaceDocument(owner, id, current.ContentRevision, replaceWinner);
                Assert.IsTrue(won.Success, won.Error);
            }

            return stale;
        }

        private WorkspaceStoreMutationResult TrackConflict(WorkspaceStoreMutationResult result)
        {
            if (result.Outcome == WorkspaceOperationOutcome.Conflict)
            {
                ConflictCount++;
            }

            return result;
        }
    }

    private sealed class RecordingWorkspaceService : IWorkspaceService
    {
        public Dictionary<string, CharacterFileSummary> SummaryByWorkspaceId { get; } = new(StringComparer.Ordinal);

        public WorkspaceImportResult Import(WorkspaceImportDocument document) => throw new NotSupportedException();

        public WorkspaceImportResult Import(OwnerScope owner, WorkspaceImportDocument document) => throw new NotSupportedException();

        public IReadOnlyList<WorkspaceListItem> List(int? maxCount = null) => throw new NotSupportedException();

        public IReadOnlyList<WorkspaceListItem> List(OwnerScope owner, int? maxCount = null) => throw new NotSupportedException();

        public bool Close(CharacterWorkspaceId id) => throw new NotSupportedException();

        public bool Close(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public object? GetSection(CharacterWorkspaceId id, string sectionId) => throw new NotSupportedException();

        public object? GetSection(OwnerScope owner, CharacterWorkspaceId id, string sectionId) => throw new NotSupportedException();

        public CharacterFileSummary? GetSummary(CharacterWorkspaceId id) => SummaryByWorkspaceId.GetValueOrDefault(id.Value);

        public CharacterFileSummary? GetSummary(OwnerScope owner, CharacterWorkspaceId id) => GetSummary(id);

        public CharacterValidationResult? Validate(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterValidationResult? Validate(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterProfileSection? GetProfile(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterProfileSection? GetProfile(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterProgressSection? GetProgress(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterProgressSection? GetProgress(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterSkillsSection? GetSkills(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterSkillsSection? GetSkills(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterRulesSection? GetRules(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterRulesSection? GetRules(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterBuildSection? GetBuild(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterBuildSection? GetBuild(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterMovementSection? GetMovement(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterMovementSection? GetMovement(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterAwakeningSection? GetAwakening(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CharacterAwakeningSection? GetAwakening(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CommandResult<CharacterProfileSection> UpdateMetadata(CharacterWorkspaceId id, UpdateWorkspaceMetadata command) => throw new NotSupportedException();

        public CommandResult<CharacterProfileSection> UpdateMetadata(OwnerScope owner, CharacterWorkspaceId id, UpdateWorkspaceMetadata command) => throw new NotSupportedException();

        public CommandResult<WorkspaceSaveReceipt> Save(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CommandResult<WorkspaceSaveReceipt> Save(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CommandResult<WorkspaceDownloadReceipt> Download(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CommandResult<WorkspaceDownloadReceipt> Download(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CommandResult<WorkspaceExportReceipt> Export(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CommandResult<WorkspaceExportReceipt> Export(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();

        public CommandResult<WorkspacePrintReceipt> Print(CharacterWorkspaceId id) => throw new NotSupportedException();

        public CommandResult<WorkspacePrintReceipt> Print(OwnerScope owner, CharacterWorkspaceId id) => throw new NotSupportedException();
    }
}
