using System.Diagnostics.CodeAnalysis;
using Chummer.Infrastructure.Owners;
using Chummer.Application.LifeModules;
using Chummer.Application.Owners;
using Chummer.Contracts.LifeModules;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.OriginBooks;
using Chummer.Run.Contracts.Community;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// The Alice book bridge must fail closed: only a Core-validated retained book
/// with approved chapter facts becomes a provider input; everything else stays
/// null so drafting falls back to its pre-bridge path unchanged.
/// </summary>
[TestClass]
public sealed class AliceOriginBookChapterBridgeTests
{
    private const string Workspace = "ws-bridge-1";

    [TestMethod]
    public void Resolve_returns_null_when_the_book_authority_blocks()
    {
        FakeBookService service = new()
        {
            Result = new(LifeModuleOriginDossierOutcomes.Blocked, null,
                [LifeModuleOriginDossierBlockers.AuthorityInvalid]),
        };
        AliceOriginBookChapterBridge bridge = CreateBridge(service, new FakeOwners("owner-1"));

        Assert.IsNull(bridge.TryResolveChapterSource(new CharacterWorkspaceId(Workspace), 7, 6));
        Assert.AreEqual(1, service.Loads.Count);
        Assert.AreEqual(7, service.Loads[0].contentRevision);
        Assert.AreEqual(6, service.Loads[0].savedRevision);
    }

    [TestMethod]
    public void Resolve_returns_null_when_the_book_is_missing()
    {
        FakeBookService service = new()
        {
            Result = new(LifeModuleOriginDossierOutcomes.Missing, null, ["life-module-origin-history-unavailable"]),
        };
        AliceOriginBookChapterBridge bridge = CreateBridge(service, new FakeOwners("owner-1"));

        Assert.IsNull(bridge.TryResolveChapterSource(new CharacterWorkspaceId(Workspace), 7, 6));
    }

    [TestMethod]
    public void Resolve_projects_the_head_chapter_facts_into_the_provider_input()
    {
        FakeBookService service = new() { Result = new(LifeModuleOriginDossierOutcomes.Success, Seed(), []) };
        AliceOriginBookChapterBridge bridge = CreateBridge(service, new FakeOwners("owner-1"));

        OriginChapterSource? source = bridge.TryResolveChapterSource(
            new CharacterWorkspaceId(Workspace), 7, 6);

        Assert.IsNotNull(source);
        Assert.AreEqual(Workspace, source.WorkspaceId);
        Assert.AreEqual("chapter-head", source.ChapterId);
        Assert.AreEqual("decision-head", source.AcceptedDecisionId);
        Assert.AreEqual("Runner", source.RunnerName);
        // Facts beyond the head chapter boundary are not exported.
        CollectionAssert.AreEquivalent(
            new[] { "fact-metatype", "fact-module" },
            source.Facts.Select(f => f.FactId).ToArray());
        Assert.IsTrue(source.Facts.All(f => f.DecisionId == "decision-head"
            || f.DecisionId == "decision-first"));
    }

    [TestMethod]
    public void Resolve_returns_null_when_the_seed_has_no_chapters()
    {
        FakeBookService service = new()
        {
            Result = new(LifeModuleOriginDossierOutcomes.Success, Seed() with { VisibleChapters = [] }, []),
        };
        AliceOriginBookChapterBridge bridge = CreateBridge(service, new FakeOwners("owner-1"));

        Assert.IsNull(bridge.TryResolveChapterSource(new CharacterWorkspaceId(Workspace), 7, 6));
    }

    [TestMethod]
    public void Resolve_returns_null_without_an_owner_scope()
    {
        FakeBookService service = new() { Result = new(LifeModuleOriginDossierOutcomes.Success, Seed(), []) };
        AliceOriginBookChapterBridge bridge = CreateBridge(service, new FakeOwners(""));

        Assert.IsNull(bridge.TryResolveChapterSource(new CharacterWorkspaceId(Workspace), 7, 6));
        Assert.AreEqual(0, service.Loads.Count);
    }

    [TestMethod]
    public void Resolve_binds_the_revision_pin_from_the_caller()
    {
        FakeBookService service = new() { Result = new(LifeModuleOriginDossierOutcomes.Success, Seed(), []) };
        AliceOriginBookChapterBridge bridge = CreateBridge(service, new FakeOwners("owner-1"));

        bridge.TryResolveChapterSource(new CharacterWorkspaceId(Workspace), 41, 40);

        Assert.AreEqual(Workspace, service.Loads[0].workspaceId.Value);
        Assert.AreEqual(41, service.Loads[0].contentRevision);
        Assert.AreEqual(40, service.Loads[0].savedRevision);
    }

    private static AliceOriginBookChapterBridge CreateBridge(FakeBookService service, IOwnerContextLeaseAccessor owners)
    {
        service.Authority = owners;
        return new(service, owners);
    }

    [TestMethod]
    public void Resolve_uses_the_real_local_authoritys_exact_stamp()
    {
        var owners = new LocalOwnerContextAccessor();
        var service = new FakeBookService { Result = new(LifeModuleOriginDossierOutcomes.Success, Seed(), []) };
        var bridge = CreateBridge(service, owners);
        Assert.IsNotNull(bridge.TryResolveChapterSource(new(Workspace), 7, 6));
        Assert.AreEqual(owners.Capture(), service.LastStamp);
        Assert.IsFalse(owners.TryAcquire(new(owners.Current, "presentation-read", 0), out _));
    }

    [TestMethod]
    public void Resolve_rejects_a_stamp_invalidated_before_admission()
    {
        var owners = new FakeOwners("owner-1") { RejectAdmission = true };
        var service = new FakeBookService { Result = new(LifeModuleOriginDossierOutcomes.Success, Seed(), []) };
        Assert.IsNull(CreateBridge(service, owners).TryResolveChapterSource(new(Workspace), 7, 6));
        Assert.AreEqual(0, service.Loads.Count);
    }

    [TestMethod]
    public void Resolve_discards_a_result_after_an_owner_transition_during_loading()
    {
        var owners = new FakeOwners("owner-1");
        var service = new FakeBookService
        {
            Result = new(LifeModuleOriginDossierOutcomes.Success, Seed(), []),
            AfterLoad = () => owners.Revision++
        };
        Assert.IsNull(CreateBridge(service, owners).TryResolveChapterSource(new(Workspace), 7, 6));
        Assert.AreEqual(1, service.Loads.Count);
    }

    [TestMethod]
    public void Canonical_context_survives_all_four_other_hints()
    {
        CollectionAssert.AreEqual(new[] { "canonical facts", "summary", "next", "campaign" },
            AliceOriginBookChapterBridge.BuildCausalityHints("canonical facts", "summary", "next", "campaign", "runtime"));
        CollectionAssert.AreEqual(new[] { "summary", "next", "campaign", "runtime" },
            AliceOriginBookChapterBridge.BuildCausalityHints(null, "summary", "next", "campaign", "runtime"));
    }

    private static OriginStoryArcSeed Seed()
    {
        var turn = new LifeModuleNarrativeTurnSeed(
            "life-module-origin-turn-seed-v1", "sr5", Workspace, 7, "owner-1",
            "runner-1", "Runner", "en-US", "journey-1", "real-life", 5,
            "turn-2", 2, "Story.", "Decide.", [], [], ["decision-first", "decision-head"],
            "prev-digest", "graph-digest", "decision-digest", "content-digest",
            "source-digest", "rules-digest", "runtime-digest", "seed-digest");
        var layer = new OriginCanonicalNarrativeLayer(
            "sr5",
            ["decision-first", "decision-head"],
            [
                new OriginCanonicalNarrativeFact("fact-metatype", "accepted-metatype",
                    "Born a troll.", "decision-first", [], "digest-1"),
                new OriginCanonicalNarrativeFact("fact-module", "accepted-life-module",
                    "Grew up in Ork underground.", "decision-head", [], "digest-2"),
                new OriginCanonicalNarrativeFact("fact-later", "accepted-life-module",
                    "Future chapter.", "decision-future", [], "digest-3"),
            ],
            [], "graph-digest", "mechanics-digest", "layer-digest");
        var head = new OriginNarrativeChapterProjection(
            "chapter-head", 1, "Head", "Head story.", "decision-head",
            "layer-digest", "player-digest", "provider-digest", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        return new OriginStoryArcSeed(
            "life-module-origin-story-arc-seed-v1", "arc-1", turn, layer, [head],
            ["fact-metatype", "fact-module"], ["choice-1"], ["street"], "seed-digest");
    }

    private sealed record LoadCall(string workspaceId, long contentRevision, long savedRevision);

    private sealed class FakeBookService : IOwnerBoundLifeModuleBookService
    {
        public required LifeModuleOriginDossierResult<OriginStoryArcSeed> Result { get; init; }
        public IOwnerContextLeaseAccessor Authority { get; set; } = null!;
        public OwnerContextStamp LastStamp { get; private set; }
        public Action? AfterLoad { get; init; }

        public List<(CharacterWorkspaceId workspaceId, long contentRevision, long savedRevision)> Loads { get; } = [];

        public LifeModuleOriginDossierResult<OriginStoryArcSeed> Load(
            OwnerContextStamp expectedOwner, CharacterWorkspaceId workspaceId,
            long contentRevision, long savedRevision)
        {
            LastStamp = expectedOwner;
            if (!Authority.TryAcquire(expectedOwner, out IOwnerContextLease? lease))
                return new(LifeModuleOriginDossierOutcomes.Blocked, null, [LifeModuleOriginDossierBlockers.AuthorityInvalid]);
            using (lease)
            {
                Loads.Add((workspaceId, contentRevision, savedRevision));
            }
            AfterLoad?.Invoke();
            return Result;
        }
    }

    private sealed class FakeOwners(string owner) : IOwnerContextLeaseAccessor
    {
        private readonly string _authority = Guid.NewGuid().ToString("N");
        private bool _leased;
        public bool RejectAdmission { get; init; }
        public long Revision { get; set; }
        public OwnerScope Current => new(owner);
        public OwnerContextStamp Capture() => new(Current, _authority, Revision);
        public bool TryAcquire(OwnerContextStamp expected, [NotNullWhen(true)] out IOwnerContextLease? lease)
        {
            lease = expected.IsValid && expected == Capture() && !RejectAdmission && !_leased
                ? new Lease(expected, () => _leased = false) : null;
            if (lease is not null) _leased = true;
            return lease is not null;
        }
        private sealed class Lease(OwnerContextStamp stamp, Action release) : IOwnerContextLease
        {
            public OwnerContextStamp Stamp => stamp;
            public void Dispose() => release();
        }
    }
}
