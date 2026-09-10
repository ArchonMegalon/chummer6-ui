using System;
using System.Linq;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

/// <summary>
/// Projection/routing tests only. Synthetic blocked Core responses never claim
/// successful finalization, a live owner lease, or durable write authority.
/// </summary>
[TestClass]
public sealed class WorkspaceOverviewFinalizationOwnerTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("finalization-owner-projection");
    private static readonly OwnerContextStamp LinkedOwner = new(new("account-a"), "original-issuer", 7);
    private const string FixtureBlocker = CharacterCreationFinalizationBlockers.DraftAuthorityInvalid;

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void Valid_display_owner_uses_only_scoped_finalizer_and_keeps_exact_loaded_binding(
        bool trustedLocal, bool alsoRegisterLegacy)
    {
        OwnerContextStamp original = trustedLocal
            ? new(OwnerScope.LocalSingleUser, "trusted-local-issuer", 3) : LinkedOwner;
        var overview = Overview(original);
        var response = BlockedProjection(overview);
        var scoped = new FinalizationSpy(response);
        var legacy = new FinalizationSpy(response);
        var factory = new WorkspaceOverviewStateFactory(
            creationFinalizationService: alsoRegisterLegacy ? legacy : null,
            ownerBoundCreationFinalizationService: scoped);

        CharacterOverviewState state = Project(factory, overview);

        Assert.AreEqual(1, scoped.BoundLoads);
        Assert.AreEqual(0, scoped.UnboundLoads);
        Assert.AreEqual(0, legacy.UnboundLoads);
        Assert.AreEqual(original, scoped.LastOwner);
        Assert.AreEqual(WorkspaceId, scoped.LastWorkspace);
        Assert.AreEqual(original, state.DisplayOwnerContext);
        Assert.AreEqual(original, state.Session.OwnerContext);
        Assert.AreSame(response.Value, state.CreationFinalization);
        Assert.AreEqual(overview.ContentRevision, state.CreationFinalization!.Binding.ContentRevision);
        Assert.AreEqual(overview.SavedRevision, state.CreationFinalization.Binding.SavedRevision);
        Assert.IsFalse(state.CreationFinalization.CanReview);
        Assert.IsFalse(state.CreationWizard!.CanFinalize);
    }

    [TestMethod]
    public void Valid_stamped_display_without_scoped_service_never_uses_legacy_finalizer()
    {
        var overview = Overview(LinkedOwner);
        var legacy = new FinalizationSpy(BlockedProjection(overview));

        var state = Project(new WorkspaceOverviewStateFactory(creationFinalizationService: legacy), overview);

        Assert.AreEqual(0, legacy.UnboundLoads);
        Assert.IsNull(state.CreationFinalization);
        Assert.IsFalse(state.CreationWizard!.CanFinalize);
    }

    [TestMethod]
    [DataRow("missing", true)]
    [DataRow("invalid-default", false)]
    [DataRow("invalid-default", true)]
    [DataRow("empty-issuer", false)]
    [DataRow("empty-issuer", true)]
    [DataRow("negative-revision", false)]
    [DataRow("negative-revision", true)]
    public void Missing_or_invalid_display_authority_does_not_gain_an_unbound_fallback(
        string kind, bool registerScoped)
    {
        OwnerContextStamp? owner = kind switch
        {
            "missing" => null,
            "invalid-default" => default(OwnerContextStamp),
            "empty-issuer" => LinkedOwner with { AuthorityInstanceId = string.Empty },
            "negative-revision" => LinkedOwner with { TransitionRevision = -1 },
            _ => throw new AssertFailedException("Unknown test case.")
        };
        var overview = Overview(owner);
        var response = BlockedProjection(overview);
        var legacy = new FinalizationSpy(response);
        var scoped = new FinalizationSpy(response);

        var state = Project(new WorkspaceOverviewStateFactory(
            creationFinalizationService: legacy,
            ownerBoundCreationFinalizationService: registerScoped ? scoped : null), overview);

        Assert.AreEqual(0, legacy.UnboundLoads);
        Assert.AreEqual(0, scoped.BoundLoads);
        Assert.IsNull(state.CreationFinalization);
        Assert.IsFalse(state.CreationWizard!.CanFinalize);
    }

    [TestMethod]
    public void Genuine_unstamped_legacy_composition_keeps_its_existing_read_path()
    {
        var overview = Overview(null);
        var response = BlockedProjection(overview);
        var legacy = new FinalizationSpy(response);

        var state = Project(new WorkspaceOverviewStateFactory(creationFinalizationService: legacy), overview);

        Assert.AreEqual(1, legacy.UnboundLoads);
        Assert.AreEqual(0, legacy.BoundLoads);
        Assert.AreEqual(WorkspaceId, legacy.LastWorkspace);
        Assert.IsNull(state.DisplayOwnerContext);
        Assert.AreSame(response.Value, state.CreationFinalization);
    }

    [TestMethod]
    public void Scoped_rejection_is_not_replaced_by_a_matching_legacy_projection()
    {
        var overview = Overview(LinkedOwner);
        var legacy = new FinalizationSpy(BlockedProjection(overview));
        var scoped = new FinalizationSpy(new(CharacterCreationFinalizationOutcomes.Unavailable,
            null, [CharacterCreationFinalizationBlockers.WorkspaceUnavailable]));

        var state = Project(new WorkspaceOverviewStateFactory(
            creationFinalizationService: legacy, ownerBoundCreationFinalizationService: scoped), overview);

        Assert.AreEqual(1, scoped.BoundLoads);
        Assert.AreEqual(LinkedOwner, scoped.LastOwner);
        Assert.AreEqual(0, legacy.UnboundLoads);
        Assert.IsNull(state.CreationFinalization);
    }

    [TestMethod]
    public void Scoped_read_exception_does_not_retry_through_the_legacy_service()
    {
        var overview = Overview(LinkedOwner);
        var response = BlockedProjection(overview);
        var legacy = new FinalizationSpy(response);
        var scoped = new FinalizationSpy(response) { ThrowOnBoundLoad = true };
        var factory = new WorkspaceOverviewStateFactory(
            creationFinalizationService: legacy, ownerBoundCreationFinalizationService: scoped);

        Assert.ThrowsExactly<InvalidOperationException>(() => Project(factory, overview));
        Assert.AreEqual(1, scoped.BoundLoads);
        Assert.AreEqual(0, legacy.UnboundLoads);
    }

    [TestMethod]
    [DataRow("workspace")]
    [DataRow("content-revision")]
    [DataRow("saved-revision")]
    [DataRow("raw-digest")]
    [DataRow("auxiliary-digest")]
    [DataRow("build-method")]
    [DataRow("authority-format")]
    [DataRow("snapshot-digest")]
    [DataRow("created")]
    public void Scoped_projection_still_requires_exact_overview_binding_and_canonical_digest(string mismatch)
    {
        var overview = Overview(LinkedOwner);
        var valid = BlockedProjection(overview);
        // Establish that this precise fixture is accepted before changing one
        // binding. Reseal structural changes so a stale snapshot hash cannot be
        // the accidental reason that revision/document mismatches are rejected.
        Assert.AreSame(valid.Value, Project(new WorkspaceOverviewStateFactory(
            ownerBoundCreationFinalizationService: new FinalizationSpy(valid)), overview).CreationFinalization);
        var state = valid.Value!;
        var binding = state.Binding;
        binding = mismatch switch
        {
            "workspace" => binding with { WorkspaceId = new("other-workspace") },
            "content-revision" => binding with { ContentRevision = binding.ContentRevision + 1 },
            "saved-revision" => binding with { SavedRevision = binding.SavedRevision + 1 },
            "raw-digest" => binding with { RawCharacterXmlDigest = Digest("different XML") },
            "auxiliary-digest" => binding with { AuxiliaryStateDigest = Digest("different auxiliary state") },
            "build-method" => binding with { BuildMethod = CharacterCreationBuildMethods.Karma },
            "authority-format" => binding with { AuthorityDigest = "not-a-canonical-digest" },
            _ => binding
        };
        state = Seal(state with { Binding = binding, CharacterCreated = mismatch == "created" });
        if (mismatch == "snapshot-digest") state = state with { SnapshotDigest = Digest("different snapshot") };
        var scoped = new FinalizationSpy(valid with { Value = state });
        var legacy = new FinalizationSpy(valid);

        var projected = Project(new WorkspaceOverviewStateFactory(
            creationFinalizationService: legacy, ownerBoundCreationFinalizationService: scoped), overview);

        Assert.AreEqual(1, scoped.BoundLoads);
        Assert.AreEqual(0, legacy.UnboundLoads);
        Assert.IsNull(projected.CreationFinalization);
        Assert.IsFalse(projected.CreationWizard!.CanFinalize);
    }

    private static WorkspaceOverviewLoadResult Overview(OwnerContextStamp? owner)
    {
        var overview = CharacterCreationWizardPresentationTests.CreateOverview(false, CharacterCreationBuildMethods.Priority,
            "<character><name>Owner projection fixture</name><created>False</created></character>", revision: 7)
            with { SavedRevision = 3 };
        // Test-only blocked projection carrier, not a live loader/owner capability.
        // Keep the production provenance setter internal.
        typeof(WorkspaceOverviewLoadResult).GetProperty(nameof(WorkspaceOverviewLoadResult.DisplayOwnerContext))!
            .SetValue(overview, owner);
        return overview;
    }

    private static CharacterOverviewState Project(WorkspaceOverviewStateFactory factory, WorkspaceOverviewLoadResult overview)
    {
        var session = new WorkspaceSessionState(WorkspaceId,
            [new(WorkspaceId, "Fixture", "F", DateTimeOffset.UnixEpoch, "sr5",
                overview.ContentRevision, overview.SavedRevision)], [WorkspaceId])
            { OwnerContext = overview.DisplayOwnerContext };
        // The previous screen belongs to another epoch/account. The new load's
        // provenance must win; the factory must not recapture or borrow this one.
        var previous = CharacterOverviewState.Empty with
        {
            DisplayOwnerContext = new OwnerContextStamp(new("previous-account"), "other-issuer", 99)
        };
        return factory.CreateLoadedState(previous, WorkspaceId, session, overview, null, hasSavedWorkspace: true);
    }

    private static CharacterCreationFinalizationResult<CharacterCreationFinalizationState> BlockedProjection(
        WorkspaceOverviewLoadResult overview)
    {
        string[] steps = [CharacterCreationWizardStepIds.Method, CharacterCreationWizardStepIds.Attributes,
            CharacterCreationWizardStepIds.Skills, CharacterCreationWizardStepIds.Qualities,
            CharacterCreationWizardStepIds.MagicResonance, CharacterCreationWizardStepIds.Resources, "gear"];
        var binding = new CharacterCreationFinalizationBinding(WorkspaceId, overview.ContentRevision,
            overview.SavedRevision, Digest(overview.Document!.Content), overview.Document.AuxiliaryStateDigest,
            CharacterCreationBuildMethods.Priority, Digest("synthetic blocked projection, not admission"));
        var state = Seal(new(CharacterCreationFinalizationSchemas.StateV1, binding, CharacterCreated: false,
            steps.Select(id => new CharacterCreationFinalizationStep(id, true, false, null, [FixtureBlocker], [])).ToArray(),
            [FixtureBlocker], CanReview: false, LastReceipt: null, SnapshotDigest: string.Empty));
        return new(CharacterCreationFinalizationOutcomes.Blocked, state, [FixtureBlocker]);
    }

    private static string Digest(string value) => CharacterCreationFinalizationDigest.ComputeUtf8(value);
    private static CharacterCreationFinalizationState Seal(CharacterCreationFinalizationState state)
        => state with { SnapshotDigest = CharacterCreationFinalizationDigest.Compute(state with { SnapshotDigest = string.Empty }) };

    private sealed class FinalizationSpy(CharacterCreationFinalizationResult<CharacterCreationFinalizationState> response)
        : ICharacterCreationFinalizationService, IOwnerBoundCharacterCreationFinalizationService
    {
        public int BoundLoads { get; private set; }
        public int UnboundLoads { get; private set; }
        public OwnerContextStamp? LastOwner { get; private set; }
        public CharacterWorkspaceId? LastWorkspace { get; private set; }
        public bool ThrowOnBoundLoad { get; init; }
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationState> Load(
            CharacterCreationFinalizationLoadRequest request)
        {
            UnboundLoads++;
            LastWorkspace = request.WorkspaceId;
            return response;
        }
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationState> Load(
            OwnerContextStamp owner, CharacterCreationFinalizationLoadRequest request)
        {
            BoundLoads++;
            LastOwner = owner;
            LastWorkspace = request.WorkspaceId;
            if (ThrowOnBoundLoad) throw new InvalidOperationException("Test-only scoped read failure.");
            return response;
        }
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReview> Review(
            CharacterCreationFinalizationReviewRequest request) => throw Unexpected();
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReview> Review(
            OwnerContextStamp owner, CharacterCreationFinalizationReviewRequest request) => throw Unexpected();
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> Confirm(
            CharacterCreationFinalizationConfirmRequest request) => throw Unexpected();
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> Confirm(
            OwnerContextStamp owner, CharacterCreationFinalizationConfirmRequest request) => throw Unexpected();
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> LookupReceipt(
            CharacterCreationFinalizationReceiptLookupRequest request) => throw Unexpected();
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> LookupReceipt(
            OwnerContextStamp owner, CharacterCreationFinalizationReceiptLookupRequest request) => throw Unexpected();
        private static AssertFailedException Unexpected() => new("The state factory must only load a projection.");
    }
}
