#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
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
public sealed class InProcessCareerQualityTests
{
    public TestContext TestContext { get; set; } = null!;

    private Fixture CreateFixture(bool linked = false)
    {
        TestContext.Properties.TryGetValue("ChummerCoreContentRoot", out object? value);
        Assert.IsTrue(value is string root && Path.IsPathFullyQualified(root)
            && File.Exists(Path.Combine(root, "Chummer", "data", "qualities.xml")),
            "Supply the exact ChummerCoreContentRoot test parameter; no ambient content or skipped fixture.");
        return new((string)value!, linked);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Actual_runtime_purchases_corrects_and_cold_reopens_exact_quality_graph(bool linked)
    {
        using var f = CreateFixture(linked);
        var before = await FromUi(() => f.Quality.ReadAsync(f.Id, default));
        Assert.IsNotNull(before);
        Assert.AreEqual(JsonSerializer.Serialize(before),
            JsonSerializer.Serialize(await f.Quality.ReadAsync(f.Id, default)), "Reload must retain selectable identities.");
        var (review, plan) = Plan(before);
        var committed = await FromUi(() => f.Quality.CommitAsync(plan, default));
        Assert.IsNotNull(committed);
        var accepted = CareerQualityWorkflow.ValidateAtomicCommit(review, plan, committed);
        Assert.AreEqual(84, accepted.Receipt.CharacterKarmaAfter);
        var xml = XElement.Parse(f.Saved().Document.Content);
        Assert.AreEqual("Erased", xml.Element("qualities")!.Element("quality")!.Element("name")!.Value);
        Assert.AreEqual("Erased", xml.Element("improvements")!.Element("improvement")!.Element("improvementttype")!.Value);
        byte[] purchased = JsonSerializer.SerializeToUtf8Bytes(f.Saved());
        var cold = (ICareerQualityAtomicWorkspace)f.CreateClient(new FileWorkspaceStore(f.Directory));
        var reopened = await cold.ReadAsync(f.Id, default);
        Assert.IsNotNull(reopened);
        Assert.AreEqual(accepted.Receipt.ReceiptDigest, CareerQualityWorkflow.Project(reopened).RecoverableReceipts.Single().ReceiptDigest);
        Assert.IsNotNull(await cold.CommitAsync(plan, default));
        CollectionAssert.AreEqual(purchased, JsonSerializer.SerializeToUtf8Bytes(f.Saved()), "Retry must not debit Karma again.");
        var correction = CareerQualityWorkflow.PlanCorrection(reopened, new(f.Id, before.Binding.OwnerId,
            reopened.Binding.WorkspaceRevision, reopened.Binding.SavedRevision, "sr5", accepted.Receipt,
            accepted.Receipt.ReceiptDigest, true, Guid.NewGuid(), "Correct accidental selection"));
        var corrected = await cold.CorrectAsync(correction, default);
        Assert.IsNotNull(corrected);
        CareerQualityWorkflow.ValidateAtomicCorrection(accepted.Receipt, correction, corrected);
        var final = await ((ICareerQualityAtomicWorkspace)f.CreateClient(new FileWorkspaceStore(f.Directory))).ReadAsync(f.Id, default);
        Assert.IsNotNull(final);
        Assert.AreEqual(0, CareerQualityWorkflow.Project(final).RecoverableReceipts.Count);
        Assert.AreEqual(84, final.PersistedReceipts.Single().ObservedPostState.AvailableKarma);
        Assert.AreEqual(100, final.PersistedCorrections.Single().ObservedRestoredState.AvailableKarma);
        Assert.AreEqual("100", XElement.Parse(f.Saved().Document.Content).Element("karma")!.Value);
        Assert.AreEqual(0, f.Owner.ActiveLeases);
        Assert.IsTrue(f.Authority.Reads > 0 && f.Authority.Opens > 0);
        Assert.IsTrue(f.Roaming.Outbound > 0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Queued_cancellation_or_owner_ABA_never_writes_a_quality(bool changeOwner)
    {
        using var f = CreateFixture();
        var before = await f.Quality.ReadAsync(f.Id, default);
        Assert.IsNotNull(before);
        var (_, plan) = Plan(before);
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(f.Saved());
        using var cancellation = new CancellationTokenSource();
        f.Roaming.BlockInbound = true;
        Task predecessor = f.Client.ListWorkspacesAsync(default);
        Task<CareerQualityAtomicCommitResult?>? pending = null;
        try
        {
            await f.Roaming.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            pending = f.Quality.CommitAsync(plan, cancellation.Token);
            Assert.IsFalse(pending.IsCompleted);
            if (changeOwner) f.Owner.TransitionAwayAndBack(); else cancellation.Cancel();
            f.Roaming.Release.TrySetResult();
            await Observe(predecessor);
            if (changeOwner) Assert.IsNull(await pending);
            else
            {
                bool canceled = false;
                try { await pending; } catch (OperationCanceledException) { canceled = true; }
                Assert.IsTrue(canceled);
            }
            CollectionAssert.AreEqual(original, JsonSerializer.SerializeToUtf8Bytes(f.Saved()));
            Assert.AreEqual(0, f.Authority.Opens);
            Assert.AreEqual(0, f.Roaming.Outbound);
        }
        finally
        {
            f.Roaming.Release.TrySetResult();
            await Observe(predecessor);
            if (pending is not null) await Observe(pending);
        }
    }

    [TestMethod]
    public async Task Cancellation_after_admission_and_failed_roaming_do_not_hide_a_local_commit()
    {
        using var f = CreateFixture();
        var before = await f.Quality.ReadAsync(f.Id, default);
        Assert.IsNotNull(before);
        var (review, plan) = Plan(before);
        using var cancellation = new CancellationTokenSource();
        f.Authority.OnOpen = cancellation.Cancel;
        f.Roaming.FailOutbound = true;
        var result = await FromUi(() => f.Quality.CommitAsync(plan, cancellation.Token));
        Assert.IsTrue(cancellation.IsCancellationRequested);
        Assert.IsNotNull(result);
        Assert.AreEqual(84, CareerQualityWorkflow.ValidateAtomicCommit(review, plan, result).Receipt.CharacterKarmaAfter);
        Assert.AreEqual(1, f.Roaming.Outbound);
        Assert.AreEqual(DesktopWorkspaceRoamingOutcome.Unavailable, f.Client.LastWorkspaceRoamingResult.Outcome);
        Assert.AreEqual(0, f.Owner.ActiveLeases);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Original_owner_session_cannot_recapture_after_a_review_wait_or_account_ABA(bool linked)
    {
        using var f = CreateFixture(linked);
        var original = ((IOwnerBoundCareerQualityAtomicWorkspace)f.Client).ForOwner(f.Client.CaptureOwnerContext());
        var before = await original.ReadAsync(f.Id, default);
        Assert.IsNotNull(before);
        var (_, plan) = Plan(before);
        byte[] unchanged = JsonSerializer.SerializeToUtf8Bytes(f.Saved());
        f.Owner.TransitionAwayAndBack();
        Assert.IsNull(await original.ReadAsync(f.Id, default));
        Assert.IsNull(await original.CommitAsync(plan, default));
        CollectionAssert.AreEqual(unchanged, JsonSerializer.SerializeToUtf8Bytes(f.Saved()));
        Assert.AreEqual(0, f.Authority.Opens);
        // An explicitly new review may continue, but its old live session may
        // not correct even an otherwise exact receipt after another transition.
        var fresh = ((IOwnerBoundCareerQualityAtomicWorkspace)f.Client).ForOwner(f.Client.CaptureOwnerContext());
        var refreshed = await fresh.ReadAsync(f.Id, default);
        Assert.IsNotNull(refreshed);
        var (review, freshPlan) = Plan(refreshed);
        var result = await fresh.CommitAsync(freshPlan, default);
        Assert.IsNotNull(result);
        var receipt = CareerQualityWorkflow.ValidateAtomicCommit(review, freshPlan, result).Receipt;
        var correction = CareerQualityWorkflow.PlanCorrection(result.PersistedSnapshot, new(f.Id, before.Binding.OwnerId,
            result.PersistedSnapshot.Binding.WorkspaceRevision, result.PersistedSnapshot.Binding.SavedRevision,
            "sr5", receipt, receipt.ReceiptDigest, true, Guid.NewGuid(), "Correct selection"));
        byte[] committed = JsonSerializer.SerializeToUtf8Bytes(f.Saved());
        f.Owner.TransitionAwayAndBack();
        Assert.IsNull(await fresh.CorrectAsync(correction, default));
        CollectionAssert.AreEqual(committed, JsonSerializer.SerializeToUtf8Bytes(f.Saved()));
        Assert.AreEqual(0, f.Owner.ActiveLeases);
    }

    [TestMethod]
    public async Task Missing_Core_capability_is_not_replaced_with_a_display_only_success()
    {
        using var f = CreateFixture();
        var before = await f.Quality.ReadAsync(f.Id, default);
        Assert.IsNotNull(before);
        var (_, plan) = Plan(before);
        var unavailable = (ICareerQualityAtomicWorkspace)f.CreateClient(f.Store, enableQuality: false);
        Assert.IsNull(await unavailable.ReadAsync(f.Id, default));
        Assert.IsNull(await unavailable.CommitAsync(plan, default));
        Assert.AreEqual(1L, f.Saved().ContentRevision);
    }

    private static (CareerQualityReview, CharacterCareerQualityPlan) Plan(CareerQualityAuthoritySnapshot snapshot)
    {
        var state = CareerQualityWorkflow.Project(snapshot);
        Assert.AreEqual(0, state.OmittedCandidateCount);
        var selected = state.Quotes.Single(row => row.Definition.Name == "Erased"
            && row.Operation == CharacterCareerQualityOperation.AcquireLevel);
        Assert.IsTrue(selected.CanApply);
        var review = CareerQualityWorkflow.Review(snapshot, CareerQualityWorkflow.CreateDraft(state, selected));
        return (review, CareerQualityWorkflow.PlanConfirmation(snapshot, review, true, Guid.NewGuid(), DateTime.Now));
    }

    private static Task<T> FromUi<T>(Func<Task<T>> start)
    {
        var old = SynchronizationContext.Current;
        try { SynchronizationContext.SetSynchronizationContext(new SynchronizationContext()); return start(); }
        finally { SynchronizationContext.SetSynchronizationContext(old); }
    }

    private static async Task Observe(Task task) { try { await task.ConfigureAwait(false); } catch { } }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = System.IO.Directory.CreateTempSubdirectory("chummer-runtime-career-quality-").FullName;
        public CharacterWorkspaceId Id { get; } = new("career-quality-runtime");
        public LeaseOwner Owner { get; }
        public FileWorkspaceStore Store { get; }
        public ObservedAuthority Authority { get; }
        public ProbeRoaming Roaming { get; }
        public InProcessChummerClient Client { get; }
        public ICareerQualityAtomicWorkspace Quality => Client;
        private readonly RulesetWorkspaceCodecResolver _codecs;
        public Fixture(string root, bool linked)
        {
            Owner = new(linked ? new("synthetic-linked-owner") : OwnerScope.LocalSingleUser);
            Store = new(Directory);
            var sources = new FileSystemCharacterSourceDataResolver(new FileSystemContentOverlayCatalogService(root, root, null));
            Authority = new(new OwnerManagedCareerQualityAuthorityResolver(sources), Owner);
            Roaming = new(Owner);
            _codecs = new([new Sr5WorkspaceCodec(new XmlCharacterFileQueries(new CharacterFileService()),
                new XmlCharacterSectionQueries(new CharacterSectionService(sources)),
                new XmlCharacterMetadataCommands(new CharacterFileService()))]);
            var document = new WorkspaceDocument("<character><created>True</created><karma>100</karma>"
                + $"<settings>{CharacterCreationBootstrapProfiles.PrioritySettingsProfileId}</settings>"
                + "<metatypeguid>a53d885d-a4a4-443d-b6a6-b0a55b0a96c7</metatypeguid><metatype>Human</metatype>"
                + "<qualities/><improvements/><expenses/><notes>retain me</notes></character>", "sr5");
            Assert.IsTrue((linked ? Store.CreateWorkspaceDocument(Owner.Current, Id, document) : Store.CreateWorkspaceDocument(Id, document)).Success);
            Assert.IsTrue((linked ? Store.SaveCheckpoint(Owner.Current, Id, 1) : Store.SaveCheckpoint(Id, 1)).Success);
            Client = CreateClient(Store);
        }
        public InProcessChummerClient CreateClient(FileWorkspaceStore store, bool enableQuality = true)
        {
            var registry = new RulesetPluginRegistry([new Sr5RulesetPlugin()]);
            var selection = new DefaultRulesetSelectionPolicy(registry);
            return new(new WorkspaceService(store, _codecs, new WorkspaceImportRulesetDetector()),
                new RulesetShellCatalogResolverService(registry, selection), rulesetSelectionPolicy: selection,
                ownerContextAccessor: Owner, workspaceRoamingSync: Roaming, workspaceStore: store,
                careerQualityService: enableQuality ? new(store, Owner, Authority) : null);
        }
        public WorkspaceStoredDocument Saved()
        {
            var cold = new FileWorkspaceStore(Directory);
            var result = Owner.Current.IsLocalSingleUser ? cold.Get(Id) : cold.Get(Owner.Current, Id);
            Assert.IsTrue(result.Success, result.Error);
            return result.Value!;
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }

    private sealed class ObservedAuthority(ICharacterCareerQualityAuthorityResolver inner, LeaseOwner owner)
        : ICharacterCareerQualityAuthorityResolver
    {
        public int Reads, Opens;
        public Action? OnOpen;
        public ICharacterCareerQualityCatalogAdmission? Read(OwnerScope scope, WorkspaceStoredDocument saved, string runtime)
        {
            Assert.IsNull(SynchronizationContext.Current);
            Assert.AreEqual(1, owner.ActiveLeases);
            Reads++;
            return inner.Read(scope, saved, runtime);
        }
        public ICharacterCareerQualityAdmission? Open(OwnerScope scope, WorkspaceStoredDocument saved,
            CharacterCareerQualityIdentity identity, CharacterCareerQualityOperation operation, string runtime)
        {
            Assert.IsNull(SynchronizationContext.Current);
            Assert.AreEqual(1, owner.ActiveLeases);
            Opens++;
            OnOpen?.Invoke();
            return inner.Open(scope, saved, identity, operation, runtime);
        }
    }

    private sealed class LeaseOwner(OwnerScope original) : IOwnerContextLeaseAccessor
    {
        private readonly object _gate = new();
        private OwnerContextStamp _stamp = new(original, Guid.NewGuid().ToString("N"), 0);
        public int ActiveLeases;
        public OwnerScope Current => Capture().Owner;
        public OwnerContextStamp Capture() { lock (_gate) return _stamp; }
        public void TransitionAwayAndBack()
        {
            lock (_gate)
            {
                Assert.AreEqual(0, ActiveLeases);
                _stamp = _stamp with { Owner = new("other-owner"), TransitionRevision = _stamp.TransitionRevision + 1 };
                _stamp = _stamp with { Owner = original, TransitionRevision = _stamp.TransitionRevision + 1 };
            }
        }
        public bool TryAcquire(OwnerContextStamp expected, [NotNullWhen(true)] out IOwnerContextLease? lease)
        {
            Assert.IsNull(SynchronizationContext.Current);
            Monitor.Enter(_gate);
            if (ActiveLeases != 0) { Monitor.Exit(_gate); throw new AssertFailedException("No nested owner lease."); }
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
            }
        }
    }

    private sealed class ProbeRoaming(LeaseOwner owner) : IDesktopWorkspaceRoamingSync, IOwnerBoundDesktopWorkspaceRoamingSync
    {
        public bool BlockInbound, FailOutbound;
        public int Outbound;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<DesktopWorkspaceRoamingResult> SynchronizeInboundAsync(OwnerScope scope, CancellationToken ct)
        {
            Assert.AreEqual(0, owner.ActiveLeases);
            Entered.TrySetResult();
            if (BlockInbound) await Release.Task.WaitAsync(ct);
            return DesktopWorkspaceRoamingResult.AlreadyCurrent();
        }
        public Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(OwnerContextStamp stamp, CharacterWorkspaceId id, CancellationToken ct)
        {
            Assert.AreEqual(0, owner.ActiveLeases);
            Assert.AreEqual(stamp, owner.Capture());
            Outbound++;
            if (FailOutbound) throw new IOException("Synthetic roaming failure after local commit");
            return Task.FromResult(DesktopWorkspaceRoamingResult.AlreadyCurrent(id));
        }
        public Task<DesktopWorkspaceRoamingResult> SynchronizeOutboundAsync(OwnerScope scope, CharacterWorkspaceId id, CancellationToken ct)
            => throw new AssertFailedException("Never fall back to owner-name-only roaming.");
    }
}
