#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Infrastructure.Files;
using Chummer.Infrastructure.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class InProcessWorkspaceContinuationTests
{
    private const int MaximumBytes = 512 * 1024;
    public TestContext TestContext { get; set; } = null!;

    private Fixture CreateFixture()
    {
        // Governed callers authenticate this exact Core content before and after
        // execution. Missing content must fail, never discover UI or ambient data.
        TestContext.Properties.TryGetValue("ChummerCoreContentRoot", out object? suppliedRoot);
        string? contentRoot = suppliedRoot as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(contentRoot),
            "Supply the explicit ChummerCoreContentRoot test parameter; missing Core data cannot skip this test.");
        Assert.IsTrue(Path.IsPathFullyQualified(contentRoot!)
            && File.Exists(Path.Combine(contentRoot!, "Chummer", "data", "lifemodules.xml")),
            "ChummerCoreContentRoot must identify the explicit Core checkout containing Chummer/data.");
        return new Fixture(contentRoot!);
    }

    [TestMethod]
    public async Task Real_bootstrap_roundtrip_runs_off_UI_with_own_leases_and_preserves_dirty_revisions()
    {
        using var fixture = CreateFixture();
        OwnerContextStamp original = fixture.Owner.Capture();
        var exported = await FromUi(() => fixture.SourceClient.ExportContinuationAsync(original, fixture.Id, CancellationToken.None));
        Assert.IsTrue(exported.Success, exported.Error);
        Assert.IsNotNull(exported.Value);
        Assert.AreEqual(fixture.Exported.SnapshotDigest, exported.Value.SnapshotDigest);
        using var review = await FromUi(() => fixture.Client.ReviewContinuationAsync(original,
            WorkspaceContinuationCodec.Encode(exported.Value, MaximumBytes), CancellationToken.None));
        AssertAvailable(review, fixture);

        var applied = await FromUi(() => fixture.Client.ConfirmContinuationAsync(original, review, true, CancellationToken.None));

        fixture.AssertApplied(applied);
        Assert.IsTrue(fixture.Sources.Calls > 0, "The real Core source evaluator must run, not a DTO-only admission stub.");
        Assert.AreEqual(0, fixture.Roaming.Calls, "Review/confirm must not implicitly roam or enqueue an outbound replay.");
        Assert.AreEqual(0, fixture.Owner.ActiveLeases);
        Assert.IsTrue(fixture.Owner.BackgroundAdmissions > 0);
        var cold = new FileWorkspaceStore(fixture.TargetDirectory).Get(fixture.Id).Value!;
        Assert.AreEqual(2L, cold.ContentRevision);
        Assert.AreEqual(1L, cold.SavedRevision);
        Assert.IsTrue(new FileWorkspaceStore(fixture.TargetDirectory).SaveCheckpoint(fixture.Id, cold.ContentRevision).Success,
            "The restored real workspace remains usable by the ordinary local persistence lane.");
    }

    [TestMethod]
    public async Task Caller_bytes_are_captured_before_waiting_for_the_runtime_queue()
    {
        using var fixture = CreateFixture();
        var original = fixture.Owner.Capture();
        fixture.Roaming.Block = true;
        Task predecessor = fixture.Client.ListWorkspacesAsync(original, CancellationToken.None);
        Task<IWorkspaceContinuationReview>? pending = null;
        try
        {
            await fixture.Roaming.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            byte[] callerBytes = fixture.Bytes.ToArray();
            pending = fixture.Client.ReviewContinuationAsync(original, callerBytes, CancellationToken.None);
            Assert.IsFalse(pending.IsCompleted);
            Array.Fill(callerBytes, (byte)' ');
            fixture.Roaming.Release.TrySetResult();
            await predecessor;
            using var review = await pending;
            AssertAvailable(review, fixture);
            fixture.AssertApplied(await fixture.Client.ConfirmContinuationAsync(original, review, true, CancellationToken.None));
        }
        finally
        {
            fixture.Roaming.Release.TrySetResult();
            await Observe(predecessor);
            if (pending is not null)
            {
                await Observe(pending);
                if (pending.IsCompletedSuccessfully) pending.Result.Dispose();
            }
        }
    }

    [TestMethod]
    [DataRow("review")]
    [DataRow("confirm")]
    public async Task Cancellation_before_queue_admission_does_not_touch_the_target(string operation)
    {
        using var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var original = fixture.Owner.Capture();
        using var review = operation == "confirm"
            ? await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None) : null;
        int sourceCalls = fixture.Sources.Calls;
        fixture.Roaming.Block = true;
        Task predecessor = fixture.Client.ListWorkspacesAsync(original, CancellationToken.None);
        Task? pending = null;
        try
        {
            await fixture.Roaming.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            pending = operation == "review"
                ? fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, cancellation.Token)
                : fixture.Client.ConfirmContinuationAsync(original, review!, true, cancellation.Token);
            Assert.IsFalse(pending.IsCompleted);
            cancellation.Cancel();
            fixture.Roaming.Release.TrySetResult();
            await predecessor;
            await AssertCanceled(pending);
            Assert.AreEqual(sourceCalls, fixture.Sources.Calls);
            fixture.AssertNoTarget();
        }
        finally
        {
            fixture.Roaming.Release.TrySetResult();
            await Observe(predecessor);
            if (pending is not null) await Observe(pending);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Original_owner_switch_or_ABA_cannot_confirm_or_recapture_authority(bool returnToOriginal)
    {
        using var fixture = CreateFixture();
        var original = fixture.Owner.Capture();
        using var review = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        AssertAvailable(review, fixture);
        fixture.Roaming.Block = true;
        Task predecessor = fixture.Client.ListWorkspacesAsync(original, CancellationToken.None);
        Task<WorkspaceContinuationRestoreResult>? pending = null;
        try
        {
            await fixture.Roaming.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            pending = fixture.Client.ConfirmContinuationAsync(original, review, true, CancellationToken.None);
            Assert.IsFalse(pending.IsCompleted);
            fixture.Owner.Transition(returnToOriginal);
            fixture.Roaming.Release.TrySetResult();
            await Observe(predecessor);
            Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Rejected, (await pending).Outcome);
            var staleExport = await fixture.SourceClient.ExportContinuationAsync(original, fixture.Id, CancellationToken.None);
            Assert.IsFalse(staleExport.Success);
            using var staleReview = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
            Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Rejected, staleReview.Result.Outcome);
            fixture.AssertNoTarget();
        }
        finally
        {
            fixture.Roaming.Release.TrySetResult();
            await Observe(predecessor);
            if (pending is not null) await Observe(pending);
        }
    }

    [TestMethod]
    public async Task Explicit_confirmation_disposal_expiry_and_replay_keep_the_Core_review_boundary()
    {
        using var fixture = CreateFixture();
        var original = fixture.Owner.Capture();
        using var disposed = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        AssertAvailable(disposed, fixture);
        disposed.Dispose();
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.ReviewConsumed,
            (await fixture.Client.ConfirmContinuationAsync(original, disposed, true, CancellationToken.None)).Outcome);
        using var expired = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        fixture.Clock.UtcNow = expired.ExpiresAtUtc;
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.ReviewExpired,
            (await fixture.Client.ConfirmContinuationAsync(original, expired, true, CancellationToken.None)).Outcome);
        fixture.AssertNoTarget();
        using var review = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        AssertAvailable(review, fixture);
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Rejected,
            (await fixture.Client.ConfirmContinuationAsync(original, review, false, CancellationToken.None)).Outcome);
        fixture.AssertNoTarget();
        fixture.AssertApplied(await fixture.Client.ConfirmContinuationAsync(original, review, true, CancellationToken.None));
        byte[] committed = File.ReadAllBytes(fixture.RecordPath);
        DateTime timestamp = File.GetLastWriteTimeUtc(fixture.RecordPath);
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.ReviewConsumed,
            (await fixture.Client.ConfirmContinuationAsync(original, review, true, CancellationToken.None)).Outcome);
        CollectionAssert.AreEqual(committed, File.ReadAllBytes(fixture.RecordPath));
        Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(fixture.RecordPath));
    }

    [TestMethod]
    public async Task Arbitrary_interface_and_other_client_handle_cannot_forge_admission()
    {
        using var fixture = CreateFixture();
        var original = fixture.Owner.Capture();
        using var fake = new ForgedReview();
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.ReviewConsumed,
            (await fixture.Client.ConfirmContinuationAsync(original, fake, true, CancellationToken.None)).Outcome);
        using var issued = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        AssertAvailable(issued, fixture);
        var otherClient = fixture.CreateClient(fixture.TargetStore);
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.ReviewConsumed,
            (await otherClient.ConfirmContinuationAsync(original, issued, true, CancellationToken.None)).Outcome);
        fixture.AssertNoTarget();
        fixture.AssertApplied(await fixture.Client.ConfirmContinuationAsync(original, issued, true, CancellationToken.None));
    }

    [TestMethod]
    public async Task Known_commit_survives_cancellation_and_owner_ABA_after_the_actual_lease_releases()
    {
        using var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var original = fixture.Owner.Capture();
        using var review = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        AssertAvailable(review, fixture);
        bool transitioned = false;
        fixture.Owner.AfterRelease = () =>
        {
            if (!File.Exists(fixture.RecordPath)) return;
            fixture.Owner.AfterRelease = null;
            fixture.Owner.Transition(returnToOriginal: true);
            cancellation.Cancel();
            transitioned = true;
        };

        var result = await fixture.Client.ConfirmContinuationAsync(original, review, true, cancellation.Token);

        Assert.IsTrue(transitioned, "The owner switched only after the real committed store write released its lease.");
        Assert.IsTrue(cancellation.IsCancellationRequested);
        Assert.AreNotEqual(original, fixture.Owner.Capture());
        fixture.AssertApplied(result);
        var stale = await fixture.Client.RecoverContinuationAsync(original, fixture.Id,
            review.OperationId, review.AdmissionDigest!, CancellationToken.None);
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Unavailable, stale.Outcome);
        var recovered = await fixture.Client.RecoverContinuationAsync(fixture.Owner.Capture(), fixture.Id,
            review.OperationId, review.AdmissionDigest!, CancellationToken.None);
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Recovered, recovered.Outcome);
        Assert.AreEqual(result.Receipt, recovered.Receipt);
    }

    [TestMethod]
    public async Task Presenter_activates_real_restored_default_section_without_superseding_itself()
    {
        using var fixture = CreateFixture();
        var original = fixture.Owner.Capture();
        using var review = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        var applied = await fixture.Client.ConfirmContinuationAsync(original, review, true, CancellationToken.None);
        fixture.AssertApplied(applied);
        await using var presenter = fixture.CreatePresenter();

        bool activated = await presenter.ActivateContinuationAsync(original, fixture.Exported, applied.Receipt, CancellationToken.None);
        Assert.IsTrue(activated, DescribeActivation(presenter.State));

        Assert.AreEqual(fixture.Id, presenter.State.WorkspaceId);
        Assert.AreEqual(original, presenter.State.DisplayOwnerContext);
        Assert.AreEqual(original, presenter.State.Session.OwnerContext);
        Assert.AreEqual(fixture.Id, presenter.State.Session.ActiveWorkspaceId);
        Assert.AreEqual(RulesetDefaults.Sr5, presenter.State.ActiveWorkspace!.RulesetId,
            "An empty roster must acquire the restored document's exact ruleset, not an ambient default.");
        Assert.AreEqual(2L, presenter.State.ContentRevision);
        Assert.AreEqual(1L, presenter.State.SavedRevision);
        Assert.IsFalse(string.IsNullOrWhiteSpace(presenter.State.ActiveSectionId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(presenter.State.ActiveSectionJson));
        Assert.IsNull(presenter.State.Error);
        fixture.AssertApplied(applied);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Presenter_rejects_owner_switch_or_ABA_after_a_real_overview_read(bool returnToOriginal)
    {
        using var fixture = CreateFixture();
        var original = fixture.Owner.Capture();
        using var review = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        var applied = await fixture.Client.ConfirmContinuationAsync(original, review, true, CancellationToken.None);
        fixture.AssertApplied(applied);
        var loader = new DelayedOverviewLoader(fixture.Id);
        await using var presenter = fixture.CreatePresenter(loader);
        Task<bool> activation = presenter.ActivateContinuationAsync(original, fixture.Exported, applied.Receipt, CancellationToken.None);
        try
        {
            await loader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            fixture.Owner.Transition(returnToOriginal);
            loader.Release.TrySetResult();
            Assert.IsFalse(await activation);
            Assert.IsNull(presenter.State.WorkspaceId);
            Assert.IsNull(presenter.State.DisplayOwnerContext);
            Assert.AreEqual(0, presenter.State.Session.OpenWorkspaces.Count);
            fixture.AssertApplied(applied);
        }
        finally { loader.Release.TrySetResult(); await Observe(activation); }
    }

    [TestMethod]
    public async Task Superseding_real_workspace_activation_wins_over_a_delayed_continuation()
    {
        using var fixture = CreateFixture();
        using var secondSource = CreateFixture();
        var original = fixture.Owner.Capture();
        using var firstReview = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        var first = await fixture.Client.ConfirmContinuationAsync(original, firstReview, true, CancellationToken.None);
        fixture.AssertApplied(first);
        using var secondReview = await fixture.Client.ReviewContinuationAsync(original, secondSource.Bytes, CancellationToken.None);
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Available, secondReview.Result.Outcome);
        var second = await fixture.Client.ConfirmContinuationAsync(original, secondReview, true, CancellationToken.None);
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Applied, second.Outcome);
        var loader = new DelayedOverviewLoader(fixture.Id);
        await using var presenter = fixture.CreatePresenter(loader);
        Task<bool> delayed = presenter.ActivateContinuationAsync(original, fixture.Exported, first.Receipt, CancellationToken.None);
        try
        {
            await loader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            bool activated = await presenter.ActivateContinuationAsync(original, secondSource.Exported, second.Receipt, CancellationToken.None);
            Assert.IsTrue(activated, DescribeActivation(presenter.State));
            loader.Release.TrySetResult();
            Assert.IsFalse(await delayed);
            Assert.AreEqual(secondSource.Id, presenter.State.WorkspaceId);
            Assert.AreEqual(secondSource.Id, presenter.State.Session.ActiveWorkspaceId);
            Assert.AreEqual(original, presenter.State.DisplayOwnerContext);
            Assert.IsFalse(string.IsNullOrWhiteSpace(presenter.State.ActiveSectionJson));
        }
        finally { loader.Release.TrySetResult(); await Observe(delayed); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Canceled_presenter_activation_never_erases_or_replays_the_committed_restore(bool duringLoad)
    {
        using var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var original = fixture.Owner.Capture();
        using var review = await fixture.Client.ReviewContinuationAsync(original, fixture.Bytes, CancellationToken.None);
        var applied = await fixture.Client.ConfirmContinuationAsync(original, review, true, CancellationToken.None);
        fixture.AssertApplied(applied);
        byte[] before = File.ReadAllBytes(fixture.RecordPath);
        DateTime timestamp = File.GetLastWriteTimeUtc(fixture.RecordPath);
        var loader = new DelayedOverviewLoader(fixture.Id);
        await using var presenter = fixture.CreatePresenter(loader);
        if (!duringLoad) cancellation.Cancel();
        Task<bool> activation = presenter.ActivateContinuationAsync(original, fixture.Exported, applied.Receipt, cancellation.Token);
        try
        {
            if (duringLoad)
            {
                await loader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                cancellation.Cancel();
            }
            Assert.IsFalse(await activation);
            Assert.IsNull(presenter.State.WorkspaceId);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(fixture.RecordPath));
            Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(fixture.RecordPath));
            var recovered = await fixture.Client.RecoverContinuationAsync(original, fixture.Id,
                review.OperationId, review.AdmissionDigest!, CancellationToken.None);
            Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Recovered, recovered.Outcome);
            Assert.AreEqual(applied.Receipt, recovered.Receipt);
        }
        finally { loader.Release.TrySetResult(); await Observe(activation); }
    }

    [TestMethod]
    [DataRow("export")]
    [DataRow("snapshot")]
    [DataRow("workspace")]
    [DataRow("document")]
    public async Task Malformed_nested_continuation_activation_input_fails_closed(string missing)
    {
        using var fixture = CreateFixture();
        await using var presenter = fixture.CreatePresenter();
        WorkspaceContinuationExport candidate = missing switch
        {
            "export" => null!,
            "snapshot" => fixture.Exported with { Snapshot = null! },
            "workspace" => fixture.Exported with { Snapshot = fixture.Exported.Snapshot with { Workspace = null! } },
            "document" => fixture.Exported with
            {
                Snapshot = fixture.Exported.Snapshot with
                {
                    Workspace = fixture.Exported.Snapshot.Workspace with { Document = null! }
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(missing))
        };
        Assert.IsFalse(await presenter.ActivateContinuationAsync(fixture.Owner.Capture(), candidate, null, CancellationToken.None));
        Assert.IsNull(presenter.State.WorkspaceId);
        fixture.AssertNoTarget();
    }

    private static void AssertAvailable(IWorkspaceContinuationReview review, Fixture fixture)
    {
        Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Available, review.Result.Outcome, JsonSerializer.Serialize(review.Result));
        Assert.AreEqual(fixture.Id, review.WorkspaceId);
        Assert.AreEqual(fixture.Exported.SnapshotDigest, review.SnapshotDigest);
        Assert.AreNotEqual(Guid.Empty, review.OperationId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(review.AdmissionDigest));
    }

    private static string DescribeActivation(CharacterOverviewState state)
        => JsonSerializer.Serialize(new
        {
            state.WorkspaceId, state.ContentRevision, state.SavedRevision, state.IsDirty,
            state.Error, state.ActiveSectionId, Ruleset = state.ActiveWorkspace?.RulesetId,
            TabCount = state.NavigationTabs.Count, CommandCount = state.Commands.Count,
            HasDisplayOwner = state.DisplayOwnerContext is not null,
            SessionOwnerMatches = state.DisplayOwnerContext == state.Session.OwnerContext
        });

    private static Task<T> FromUi<T>(Func<Task<T>> start)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        try { SynchronizationContext.SetSynchronizationContext(new SynchronizationContext()); return start(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static async Task AssertCanceled(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { return; }
        Assert.Fail("The canceled queued operation must never enter Core.");
    }

    private static async Task Observe(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch { /* Always release/join queue predecessors without hiding the tested outcome. */ }
    }

    // Genuine bootstrap and exact resolver/catalog setup from Core's finalization
    // fixtures, without importing their expensive full-history fixture graph.
    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("chummer-runtime-continuation-").FullName;
        private readonly ICharacterFileQueries _queries;
        private readonly RulesetWorkspaceCodecResolver _codecs;
        public LeaseOwner Owner { get; } = new();
        public TestClock Clock { get; } = new();
        public ProbeRoaming Roaming { get; }
        public ObservedResolver Sources { get; }
        public string TargetDirectory => Path.Combine(_directory, "target");
        public string RecordPath => Path.Combine(TargetDirectory, "workspaces", Id.Value + ".json");
        public FileWorkspaceStore TargetStore { get; }
        public CharacterWorkspaceId Id { get; }
        public WorkspaceContinuationExport Exported { get; }
        public byte[] Bytes { get; }
        public InProcessChummerClient SourceClient { get; }
        public InProcessChummerClient Client { get; }
        private readonly WorkspaceContinuationRestoreService _restore;

        public Fixture(string root)
        {
            ICharacterSourceDataResolver resolver = new FileSystemCharacterSourceDataResolver(
                new FileSystemContentOverlayCatalogService(root, root, null));
            _queries = new XmlCharacterFileQueries(new CharacterFileService());
            var source = new FileWorkspaceStore(Path.Combine(_directory, "source"));
            var codec = new Sr5WorkspaceCodec(_queries,
                new XmlCharacterSectionQueries(new CharacterSectionService(resolver)),
                new XmlCharacterMetadataCommands(new CharacterFileService()));
            _codecs = new RulesetWorkspaceCodecResolver([codec]);
            var bootstrap = new CharacterCreationBootstrapService(source, _codecs, _queries, resolver);
            Assert.IsTrue(CharacterCreationBootstrapProfiles.TryResolveCanonicalSettingsProfileId(
                CharacterCreationBuildMethods.Priority, out string settingsProfileId));
            var created = bootstrap.Create(new(CharacterCreationBootstrapSchemas.RequestV1,
                CharacterCreationBootstrapStages.AwaitingFoundationSelection, RulesetDefaults.Sr5,
                "Continuation Runner", "Runtime", CharacterCreationBuildMethods.Priority, settingsProfileId));
            Assert.AreEqual(CharacterCreationBootstrapOutcomes.Success, created.Outcome, string.Join(",", created.Blockers));
            Id = created.Value!.WorkspaceId;
            var stored = source.Get(Id).Value!;
            Assert.IsTrue(source.SaveCheckpoint(Id, stored.ContentRevision).Success);
            Assert.IsTrue(source.ReplaceWorkspaceDocument(Id, stored.ContentRevision, stored.Document).Success);
            var exported = new WorkspaceContinuationExportService(source, Owner).Export(Owner.Capture(), Id);
            Assert.IsTrue(exported.Success, exported.Error);
            Exported = exported.Value!;
            Bytes = WorkspaceContinuationCodec.Encode(Exported, MaximumBytes);
            TargetStore = new FileWorkspaceStore(TargetDirectory);
            Roaming = new ProbeRoaming(Owner);
            Sources = new ObservedResolver(resolver, Owner);
            _restore = new(TargetStore, Owner, Sources, _queries,
                new XmlLifeModulesCatalogService(Path.Combine(root, "Chummer", "data", "lifemodules.xml")),
                MaximumBytes, Clock);
            SourceClient = CreateClient(source);
            Client = CreateClient(TargetStore);
            Owner.RequireBackground = true;
        }

        public InProcessChummerClient CreateClient(FileWorkspaceStore store)
        {
            var registry = new RulesetPluginRegistry([new Sr5RulesetPlugin()]);
            var selection = new DefaultRulesetSelectionPolicy(registry);
            return new(new WorkspaceService(store, _codecs, new WorkspaceImportRulesetDetector()),
                new RulesetShellCatalogResolverService(registry, selection),
                rulesetSelectionPolicy: selection,
                ownerContextAccessor: Owner, workspaceRoamingSync: Roaming, workspaceStore: store,
                continuationExportService: new WorkspaceContinuationExportService(store, Owner),
                continuationRestoreService: _restore);
        }

        public CharacterOverviewPresenter CreatePresenter(IWorkspaceOverviewLoader? loader = null)
            => new(Client, workspaceOverviewLoader: loader,
                shellCatalogResolver: new RulesetShellCatalogResolverService(new RulesetPluginRegistry([new Sr5RulesetPlugin()])));

        public void AssertNoTarget()
        {
            Assert.IsFalse(File.Exists(RecordPath));
            Assert.IsFalse(new FileWorkspaceStore(TargetDirectory).Get(Id).Success);
            Assert.AreEqual(0, Owner.ActiveLeases);
        }

        public void AssertApplied(WorkspaceContinuationRestoreResult result)
        {
            Assert.AreEqual(WorkspaceContinuationRestoreOutcome.Applied, result.Outcome, JsonSerializer.Serialize(result));
            Assert.IsNotNull(result.Receipt);
            var cold = new FileWorkspaceStore(TargetDirectory).ReadContinuation(Id);
            Assert.IsTrue(cold.Success, cold.Error);
            Assert.IsNotNull(cold.Value);
            Assert.AreEqual(Exported.SnapshotDigest, WorkspaceContinuationSnapshotDigest.Compute(cold.Value));
            Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(Exported.Snapshot),
                JsonSerializer.SerializeToElement(cold.Value)), "All portable state, not only XML, must survive.");
            Assert.AreEqual(2L, result.Receipt.ContentRevision);
            Assert.AreEqual(1L, result.Receipt.SavedRevision);
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);

    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    // Delay only scheduling: projections still come from the actual owner-bound
    // loader and real runtime/store. No caller-created projection gets authority.
    private sealed class DelayedOverviewLoader(CharacterWorkspaceId delayedId) :
        IWorkspaceOverviewLoader, IOwnerBoundWorkspaceOverviewLoader
    {
        private readonly WorkspaceOverviewLoader _inner = new();
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<WorkspaceOverviewLoadResult> LoadAsync(IChummerClient client, CharacterWorkspaceId id, CancellationToken ct)
            => _inner.LoadAsync(client, id, ct);
        public async Task<WorkspaceOverviewLoadResult> LoadAsync(IChummerClient client, OwnerContextStamp owner,
            CharacterWorkspaceId id, CancellationToken ct)
        {
            var loaded = await ((IOwnerBoundWorkspaceOverviewLoader)_inner).LoadAsync(client, owner, id, ct);
            if (id == delayedId)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
            }
            return loaded;
        }
    }

    private sealed class ObservedResolver(ICharacterSourceDataResolver inner, LeaseOwner owner) : ICharacterSourceDataResolver
    {
        public int Calls;
        public ICharacterSourceDataContext? TryCreateContext(string xml)
        {
            Assert.IsNull(SynchronizationContext.Current);
            Assert.AreEqual(1, owner.ActiveLeases);
            Interlocked.Increment(ref Calls);
            return inner.TryCreateContext(xml);
        }
    }

    private sealed class LeaseOwner : IOwnerContextLeaseAccessor
    {
        private readonly object _gate = new();
        private OwnerContextStamp _stamp = new(OwnerScope.LocalSingleUser, Guid.NewGuid().ToString("N"), 0);
        public int ActiveLeases;
        public int BackgroundAdmissions;
        public bool RequireBackground;
        public Action? AfterRelease;
        public OwnerScope Current => Capture().Owner;
        public OwnerContextStamp Capture() { lock (_gate) return _stamp; }
        public void Transition(bool returnToOriginal)
        {
            lock (_gate)
            {
                Assert.AreEqual(0, ActiveLeases);
                _stamp = _stamp with { Owner = new("runtime-owner-b"), TransitionRevision = _stamp.TransitionRevision + 1 };
                if (returnToOriginal)
                    _stamp = _stamp with { Owner = OwnerScope.LocalSingleUser, TransitionRevision = _stamp.TransitionRevision + 1 };
            }
        }

        public bool TryAcquire(OwnerContextStamp expected, [NotNullWhen(true)] out IOwnerContextLease? lease)
        {
            if (RequireBackground) { Assert.IsNull(SynchronizationContext.Current); Interlocked.Increment(ref BackgroundAdmissions); }
            Monitor.Enter(_gate);
            if (ActiveLeases != 0) { Monitor.Exit(_gate); throw new AssertFailedException("The bridge must not nest Core owner leases."); }
            if (!expected.IsValid || expected != _stamp) { Monitor.Exit(_gate); lease = null; return false; }
            ActiveLeases++;
            lease = new Lease(this, expected);
            return true;
        }

        private sealed class Lease(LeaseOwner owner, OwnerContextStamp stamp) : IOwnerContextLease
        {
            private bool _disposed;
            public OwnerContextStamp Stamp => !_disposed ? stamp : throw new ObjectDisposedException(nameof(Lease));
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                owner.ActiveLeases--;
                Monitor.Exit(owner._gate);
                owner.AfterRelease?.Invoke();
            }
        }
    }

    private sealed class ProbeRoaming(LeaseOwner owner) : IDesktopWorkspaceRoamingSync
    {
        public bool Block;
        public int Calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DesktopWorkspaceRoamingResult> SynchronizeInboundAsync(OwnerScope scope, CancellationToken ct)
        {
            Assert.AreEqual(0, owner.ActiveLeases);
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            if (Block) await Release.Task.WaitAsync(ct);
            return DesktopWorkspaceRoamingResult.AlreadyCurrent();
        }
        public Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(OwnerScope scope, CharacterWorkspaceId id, CancellationToken ct)
            => throw new AssertFailedException("Continuation confirmation must not automatically roam.");
    }

    private sealed class ForgedReview : IWorkspaceContinuationReview
    {
        public OwnerContextStamp OwnerContext => throw new AssertFailedException("Untrusted review getters must not execute.");
        public CharacterWorkspaceId? WorkspaceId => throw new AssertFailedException();
        public Guid OperationId => throw new AssertFailedException();
        public string? AdmissionDigest => throw new AssertFailedException();
        public string? SnapshotDigest => throw new AssertFailedException();
        public DateTimeOffset ExpiresAtUtc => throw new AssertFailedException();
        public WorkspaceContinuationRestoreResult Result => throw new AssertFailedException();
        public void Dispose() { }
    }
}
