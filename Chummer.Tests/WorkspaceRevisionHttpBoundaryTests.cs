#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Api.Endpoints;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Api;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Infrastructure.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Presentation;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class WorkspaceRevisionHttpBoundaryTests
{
    [TestMethod]
    public async Task Stale_http_mutations_conflict_and_never_trigger_outbound_sync()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            await using TestHarness harness = await CreateHarnessAsync(stateDirectory);
            WorkspaceImportResult imported = harness.Service.Import(harness.Owner, new WorkspaceImportDocument(
                CharacterXml("Reader A"),
                RulesetDefaults.Sr5));
            string path = $"/api/workspaces/{imported.Id.Value}";

            using HttpResponseMessage readA = await harness.Http.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.OK, readA.StatusCode);
            Assert.AreEqual("\"1\"", readA.Headers.ETag?.Tag);

            using HttpResponseMessage writerB = await SendConditionalAsync(
                harness.Http,
                HttpMethod.Put,
                path,
                "\"1\"",
                DocumentBody("Writer B"));
            Assert.AreEqual(HttpStatusCode.OK, writerB.StatusCode);
            Assert.AreEqual("\"2\"", writerB.Headers.ETag?.Tag);
            Assert.AreEqual(1, harness.Roaming.OutboundCount);

            using HttpResponseMessage staleReplace = await SendConditionalAsync(
                harness.Http,
                HttpMethod.Put,
                path,
                "\"1\"",
                DocumentBody("Stale replacement"));
            using HttpResponseMessage staleMetadata = await SendConditionalAsync(
                harness.Http,
                HttpMethod.Patch,
                $"{path}/metadata",
                "\"1\"",
                new JsonObject { ["name"] = "Stale metadata" });
            using HttpResponseMessage staleSave = await SendConditionalAsync(
                harness.Http,
                HttpMethod.Post,
                $"{path}/save",
                "\"1\"",
                new JsonObject());
            using HttpResponseMessage staleDelete = await SendConditionalAsync(
                harness.Http,
                HttpMethod.Delete,
                path,
                "\"1\"");

            Assert.AreEqual(HttpStatusCode.Conflict, staleReplace.StatusCode);
            Assert.AreEqual(HttpStatusCode.Conflict, staleMetadata.StatusCode);
            Assert.AreEqual(HttpStatusCode.Conflict, staleSave.StatusCode);
            Assert.AreEqual(HttpStatusCode.Conflict, staleDelete.StatusCode);
            Assert.AreEqual(1, harness.Roaming.OutboundCount, "A stale mutation attempted outbound roaming sync.");

            CommandResult<WorkspaceDocumentSnapshot> persisted = harness.Service.GetWorkspace(harness.Owner, imported.Id);
            Assert.IsTrue(persisted.Success, persisted.Error);
            Assert.AreEqual(2L, persisted.Value?.ContentRevision);
            Assert.AreEqual(0L, persisted.Value?.SavedRevision);
            StringAssert.Contains(persisted.Value?.Document.Content ?? string.Empty, "Writer B");
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task If_match_is_strong_single_required_and_two_http_writers_have_one_winner_across_restart()
    {
        string stateDirectory = CreateStateDirectory();
        CharacterWorkspaceId id;
        try
        {
            await using (TestHarness first = await CreateHarnessAsync(stateDirectory))
            {
                WorkspaceImportResult imported = first.Service.Import(first.Owner, new WorkspaceImportDocument(
                    CharacterXml("Original"),
                    RulesetDefaults.Sr5));
                id = imported.Id;
                string savePath = $"/api/workspaces/{id.Value}/save";

                using HttpResponseMessage missing = await first.Http.PostAsJsonAsync(savePath, new { });
                Assert.AreEqual((HttpStatusCode)428, missing.StatusCode);

                foreach (string invalid in new[] { "*", "W/\"1\"", "\"1\", \"2\"", "1", "\"0\"" })
                {
                    using HttpResponseMessage rejected = await SendConditionalAsync(
                        first.Http,
                        HttpMethod.Post,
                        savePath,
                        invalid,
                        new JsonObject());
                    Assert.AreEqual(HttpStatusCode.BadRequest, rejected.StatusCode, invalid);
                }

                string workspacePath = $"/api/workspaces/{id.Value}";
                Task<HttpResponseMessage> firstWriter = SendConditionalAsync(
                    first.Http,
                    HttpMethod.Put,
                    workspacePath,
                    "\"1\"",
                    DocumentBody("First winner"));
                Task<HttpResponseMessage> secondWriter = SendConditionalAsync(
                    first.Http,
                    HttpMethod.Put,
                    workspacePath,
                    "\"1\"",
                    DocumentBody("Second winner"));
                HttpResponseMessage[] responses = await Task.WhenAll(firstWriter, secondWriter);
                using HttpResponseMessage responseA = responses[0];
                using HttpResponseMessage responseB = responses[1];
                CollectionAssert.AreEquivalent(
                    new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
                    responses.Select(static response => response.StatusCode).ToArray());
                Assert.AreEqual(1, first.Roaming.OutboundCount);
            }

            await using TestHarness restarted = await CreateHarnessAsync(stateDirectory);
            using HttpResponseMessage read = await restarted.Http.GetAsync($"/api/workspaces/{id.Value}");
            Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
            Assert.AreEqual("\"2\"", read.Headers.ETag?.Tag);
            JsonObject payload = JsonNode.Parse(await read.Content.ReadAsStringAsync())?.AsObject()
                ?? throw new AssertFailedException("Workspace response was not JSON.");
            Assert.AreEqual(2L, payload["contentRevision"]?.GetValue<long>());

            using HttpResponseMessage missingWorkspace = await restarted.Http.GetAsync("/api/workspaces/missing-workspace");
            string missingBody = await missingWorkspace.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.NotFound, missingWorkspace.StatusCode);
            Assert.AreEqual("{\"error\":\"workspace_not_found\"}", missingBody);
            Assert.IsFalse(missingBody.Contains(stateDirectory, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Corrupt_and_unavailable_workspace_reads_use_stable_path_free_http_errors()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            SwitchableReadOutcomeStore store = new();
            await using TestHarness harness = await CreateHarnessAsync(stateDirectory, store);
            WorkspaceImportResult imported = harness.Service.Import(harness.Owner, new WorkspaceImportDocument(
                CharacterXml("Error boundary"),
                RulesetDefaults.Sr5));
            string path = $"/api/workspaces/{imported.Id.Value}";

            store.ForcedOutcome = WorkspaceOperationOutcome.Corrupt;
            store.ForcedError = $"corrupt record at {stateDirectory}/secret.json";
            using HttpResponseMessage corrupt = await harness.Http.GetAsync(path);
            string corruptBody = await corrupt.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.UnprocessableEntity, corrupt.StatusCode);
            Assert.AreEqual("{\"error\":\"workspace_corrupt\"}", corruptBody);
            Assert.IsFalse(corruptBody.Contains(stateDirectory, StringComparison.Ordinal));

            store.ForcedOutcome = WorkspaceOperationOutcome.Unavailable;
            store.ForcedError = $"permission failure at {stateDirectory}/secret.json";
            using HttpResponseMessage unavailable = await harness.Http.GetAsync(path);
            string unavailableBody = await unavailable.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
            Assert.AreEqual("{\"error\":\"workspace_unavailable\"}", unavailableBody);
            Assert.IsFalse(unavailableBody.Contains(stateDirectory, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Build_ghost_analysis_is_locale_canonical_owner_bound_and_digest_receipted()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            await using TestHarness harness = await CreateHarnessAsync(stateDirectory);
            WorkspaceImportResult imported = harness.Service.Import(harness.Owner, new WorkspaceImportDocument(
                CharacterXml("Rook boundary"),
                RulesetDefaults.Sr5));
            string path = $"/api/workspaces/{imported.Id.Value}/build-ghost/analysis";

            using HttpResponseMessage accepted = await harness.Http.PostAsJsonAsync(
                path,
                new BuildGhostAnalysisClientContext("DE-de", ["ignored-by-server"], "untrusted fallback"));

            Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
            Assert.AreEqual("\"1\"", accepted.Headers.ETag?.Tag);
            string packetDigest = accepted.Headers.GetValues("X-Chummer-Build-Ghost-Packet-Digest").Single();
            StringAssert.StartsWith(packetDigest, "sha256:");
            JsonObject packet = JsonNode.Parse(await accepted.Content.ReadAsStringAsync())?.AsObject()
                ?? throw new AssertFailedException("Build Ghost response was not JSON.");
            Assert.AreEqual(harness.Owner.NormalizedValue, packet["ownerId"]?.GetValue<string>());
            Assert.AreEqual(imported.Id.Value, packet["workspaceId"]?.GetValue<string>());
            Assert.AreEqual("de-DE", packet["locale"]?.GetValue<string>());
            Assert.AreEqual(packetDigest, packet["packetDigest"]?.GetValue<string>());
            CollectionAssert.AreEqual(
                new[] { "de-DE", "en-US", "fr-FR", "ja-JP", "pt-BR", "zh-CN" },
                packet["supportedLocales"]!.AsArray().Select(static locale => locale!.GetValue<string>()).ToArray());

            using HttpResponseMessage unsupported = await harness.Http.PostAsJsonAsync(
                path,
                new BuildGhostAnalysisClientContext("es-ES", [], "ignored"));
            Assert.AreEqual(HttpStatusCode.BadRequest, unsupported.StatusCode);
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Build_ghost_analysis_rejects_a_packet_owned_by_another_boundary_owner()
    {
        string stateDirectory = CreateStateDirectory();
        try
        {
            OwnerScope boundaryOwner = new("different-boundary-owner@example.com");
            await using TestHarness harness = await CreateHarnessAsync(
                stateDirectory,
                apiOwnerOverride: boundaryOwner);
            WorkspaceImportResult imported = harness.Service.Import(harness.Owner, new WorkspaceImportDocument(
                CharacterXml("Rook owner boundary"),
                RulesetDefaults.Sr5));

            using HttpResponseMessage response = await harness.Http.PostAsJsonAsync(
                $"/api/workspaces/{imported.Id.Value}/build-ghost/analysis",
                new BuildGhostAnalysisClientContext("en-US", [], "ignored"));

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.AreEqual("{\"error\":\"build_ghost_owner_forbidden\"}", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    private static async Task<TestHarness> CreateHarnessAsync(
        string stateDirectory,
        IWorkspaceStore? workspaceStore = null,
        OwnerScope? apiOwnerOverride = null)
    {
        WorkspaceService service = CreateWorkspaceService(
            workspaceStore ?? new FileWorkspaceStore(stateDirectory));
        CountingRoamingSync roaming = new();
        OwnerScope owner = new("http-boundary-owner@example.com");
        InProcessChummerClient client = new(
            service,
            new RulesetShellCatalogResolverService(new RulesetPluginRegistry([])),
            ownerContextAccessor: new FixedOwnerContextAccessor(owner),
            workspaceRoamingSync: roaming);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<IChummerClient>(client);
        builder.Services.AddSingleton<IOwnerContextAccessor>(
            new FixedOwnerContextAccessor(apiOwnerOverride ?? owner));
        WebApplication app = builder.Build();
        app.MapWorkspaceEndpoints();
        await app.StartAsync();
        return new TestHarness(app, app.GetTestClient(), service, roaming, owner);
    }

    private static WorkspaceService CreateWorkspaceService(IWorkspaceStore workspaceStore)
    {
        CharacterFileService fileService = new();
        Sr5WorkspaceCodec codec = new(
            new XmlCharacterFileQueries(fileService),
            new XmlCharacterSectionQueries(new CharacterSectionService()),
            new XmlCharacterMetadataCommands(fileService));
        return new WorkspaceService(
            workspaceStore,
            new RulesetWorkspaceCodecResolver([codec]),
            new WorkspaceImportRulesetDetector());
    }

    private static async Task<HttpResponseMessage> SendConditionalAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string ifMatch,
        JsonObject? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static JsonObject DocumentBody(string name)
        => new()
        {
            ["contentBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(CharacterXml(name))),
            ["format"] = WorkspaceDocumentFormat.NativeXml.ToString(),
            ["rulesetId"] = RulesetDefaults.Sr5,
            ["schemaVersion"] = 1,
            ["payloadKind"] = "workspace"
        };

    private static string CharacterXml(string name)
        => $"<character><name>{name}</name><alias>{name}</alias><metatype>Human</metatype><buildmethod>Priority</buildmethod><createdversion>1.0</createdversion><appversion>1.0</appversion><karma>0</karma><nuyen>0</nuyen><created>True</created></character>";

    private static string CreateStateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "chummer-http-revision-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class CountingRoamingSync : IDesktopWorkspaceRoamingSync
    {
        private int _outboundCount;

        public int OutboundCount => Volatile.Read(ref _outboundCount);

        public Task<DesktopWorkspaceRoamingResult> SynchronizeInboundAsync(OwnerScope owner, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(DesktopWorkspaceRoamingResult.AlreadyCurrent());
        }

        public Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(
            OwnerScope owner,
            CharacterWorkspaceId workspaceId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _outboundCount);
            return Task.FromResult(new DesktopWorkspaceRoamingResult(
                DesktopWorkspaceRoamingOutcome.Applied,
                workspaceId));
        }
    }

    private sealed class FixedOwnerContextAccessor : IOwnerContextAccessor
    {
        public FixedOwnerContextAccessor(OwnerScope owner)
        {
            Current = owner;
        }

        public OwnerScope Current { get; }
    }

    private sealed class SwitchableReadOutcomeStore : IWorkspaceStore
    {
        private readonly InMemoryWorkspaceStore _inner = new();

        public WorkspaceOperationOutcome? ForcedOutcome { get; set; }

        public string? ForcedError { get; set; }

        public WorkspaceStoreMutationResult CreateWorkspaceDocument(WorkspaceDocument document)
            => _inner.CreateWorkspaceDocument(document);

        public WorkspaceStoreMutationResult CreateWorkspaceDocument(OwnerScope owner, WorkspaceDocument document)
            => _inner.CreateWorkspaceDocument(owner, document);

        public WorkspaceStoreMutationResult CreateWorkspaceDocument(CharacterWorkspaceId id, WorkspaceDocument document)
            => _inner.CreateWorkspaceDocument(id, document);

        public WorkspaceStoreMutationResult CreateWorkspaceDocument(
            OwnerScope owner,
            CharacterWorkspaceId id,
            WorkspaceDocument document)
            => _inner.CreateWorkspaceDocument(owner, id, document);

        public IReadOnlyList<WorkspaceStoreEntry> List() => _inner.List();

        public IReadOnlyList<WorkspaceStoreEntry> List(OwnerScope owner) => _inner.List(owner);

        public WorkspaceStoreReadResult Get(CharacterWorkspaceId id)
            => ForcedOutcome is WorkspaceOperationOutcome outcome
                ? new WorkspaceStoreReadResult(outcome, Error: ForcedError)
                : _inner.Get(id);

        public WorkspaceStoreReadResult Get(OwnerScope owner, CharacterWorkspaceId id)
            => ForcedOutcome is WorkspaceOperationOutcome outcome
                ? new WorkspaceStoreReadResult(outcome, Error: ForcedError)
                : _inner.Get(owner, id);

        public WorkspaceStoreMutationResult ReplaceWorkspaceDocument(
            CharacterWorkspaceId id,
            long expectedContentRevision,
            WorkspaceDocument document)
            => _inner.ReplaceWorkspaceDocument(id, expectedContentRevision, document);

        public WorkspaceStoreMutationResult ReplaceWorkspaceDocument(
            OwnerScope owner,
            CharacterWorkspaceId id,
            long expectedContentRevision,
            WorkspaceDocument document)
            => _inner.ReplaceWorkspaceDocument(owner, id, expectedContentRevision, document);

        public WorkspaceStoreMutationResult SaveCheckpoint(CharacterWorkspaceId id, long expectedContentRevision)
            => _inner.SaveCheckpoint(id, expectedContentRevision);

        public WorkspaceStoreMutationResult SaveCheckpoint(
            OwnerScope owner,
            CharacterWorkspaceId id,
            long expectedContentRevision)
            => _inner.SaveCheckpoint(owner, id, expectedContentRevision);

        public WorkspaceStoreMutationResult Delete(CharacterWorkspaceId id, long expectedContentRevision)
            => _inner.Delete(id, expectedContentRevision);

        public WorkspaceStoreMutationResult Delete(
            OwnerScope owner,
            CharacterWorkspaceId id,
            long expectedContentRevision)
            => _inner.Delete(owner, id, expectedContentRevision);
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        public TestHarness(
            WebApplication app,
            HttpClient http,
            WorkspaceService service,
            CountingRoamingSync roaming,
            OwnerScope owner)
        {
            App = app;
            Http = http;
            Service = service;
            Roaming = roaming;
            Owner = owner;
        }

        private WebApplication App { get; }

        public HttpClient Http { get; }

        public WorkspaceService Service { get; }

        public CountingRoamingSync Roaming { get; }

        public OwnerScope Owner { get; }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await App.DisposeAsync();
        }
    }
}
