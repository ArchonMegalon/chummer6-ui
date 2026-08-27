using System.Reflection;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class Sr5CareerWizardDesktopSessionTests
{
    [TestMethod]
    public void Projection_orders_typed_actions_and_blocks_every_missing_authority()
    {
        Sr5CareerWizardSnapshot snapshot = Sr5CareerWizardProjector.Project(
            Binding(),
            [
                Available(Sr5CareerWizardActionIds.AdvanceAttribute, H('d'), "anchor.attribute"),
                Blocked(Sr5CareerWizardActionIds.AdjustKarma, H('e'), "not-enough-context")
            ]);

        Assert.AreEqual(Sr5CareerWizardSchemas.SnapshotV1, snapshot.Schema);
        Assert.AreEqual(Sr5CareerWizardActionIds.AdvanceAttribute, snapshot.ActiveActionId);
        Assert.IsTrue(snapshot.CanOpenAnyAction);
        Assert.IsTrue(snapshot.SnapshotDigest.StartsWith("sha256:", StringComparison.Ordinal));
        CollectionAssert.AreEqual(
            Sr5CareerWizardProjector.KnownActionIds.ToArray(),
            snapshot.Families.SelectMany(static family => family.Actions)
                .Select(static action => action.ActionId)
                .ToArray());

        Sr5CareerWizardActionState attribute = Action(snapshot, Sr5CareerWizardActionIds.AdvanceAttribute);
        Assert.IsTrue(attribute.CanOpen);
        CollectionAssert.AreEqual(new[] { "anchor.attribute" }, attribute.SourceAnchorIds.ToArray());
        Assert.AreEqual(H('d'), attribute.AuthorityDigest);

        Sr5CareerWizardActionState karma = Action(snapshot, Sr5CareerWizardActionIds.AdjustKarma);
        Assert.IsFalse(karma.CanOpen);
        CollectionAssert.AreEqual(new[] { "not-enough-context" }, karma.Blockers.ToArray());
        Assert.AreEqual(H('e'), karma.AuthorityDigest);

        Sr5CareerWizardActionState missing = Action(snapshot, Sr5CareerWizardActionIds.ChangeQuality);
        Assert.IsFalse(missing.CanOpen);
        CollectionAssert.AreEqual(
            new[] { Sr5CareerWizardBlockers.AuthorityUnavailable },
            missing.Blockers.ToArray());
        Assert.AreEqual(string.Empty, missing.AuthorityDigest);
    }

    [TestMethod]
    public void Projection_is_deterministic_across_input_and_detail_order()
    {
        Sr5CareerWizardAuthorityAvailability first = Available(
            Sr5CareerWizardActionIds.AdvanceActiveSkill,
            H('e'),
            "anchor.z",
            "anchor.a");
        Sr5CareerWizardAuthorityAvailability second = Blocked(
            Sr5CareerWizardActionIds.ManageCalendarEntry,
            H('f'),
            "requires-calendar-authority",
            "requires-campaign-date");

        Sr5CareerWizardSnapshot left = Sr5CareerWizardProjector.Project(Binding(), [first, second]);
        Sr5CareerWizardSnapshot right = Sr5CareerWizardProjector.Project(
            Binding(),
            [
                second with { Blockers = second.Blockers.Reverse().ToArray() },
                first with { SourceAnchorIds = first.SourceAnchorIds.Reverse().ToArray() }
            ]);

        Assert.AreEqual(left.SnapshotDigest, right.SnapshotDigest);
        CollectionAssert.AreEqual(
            new[] { "anchor.a", "anchor.z" },
            Action(right, Sr5CareerWizardActionIds.AdvanceActiveSkill).SourceAnchorIds.ToArray());
    }

    [TestMethod]
    public void Invalid_ruleset_unknown_duplicate_and_incoherent_authorities_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Sr5CareerWizardProjector.Project(Binding() with { RulesetId = "sr6" }, []));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Sr5CareerWizardProjector.Project(
                Binding(),
                [Available("career.free-spirit.convert", H('a'), "anchor.unsafe")]));

        Sr5CareerWizardAuthorityAvailability known = Available(
            Sr5CareerWizardActionIds.AdvanceSkillGroup,
            H('b'),
            "anchor.group");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Sr5CareerWizardProjector.Project(Binding(), [known, known]));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Sr5CareerWizardProjector.Project(
                Binding(),
                [known with { IsAvailable = true, Blockers = ["contradiction"] }]));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Sr5CareerWizardProjector.Project(
                Binding(),
                [known with { AuthorityDigest = string.Empty }]));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Sr5CareerWizardProjector.Project(
                Binding(),
                [known with
                {
                    Binding = known.Binding with { WorkspaceRevision = 99 }
                }]));
    }

    [TestMethod]
    public void Session_selects_only_available_actions_and_has_no_confirmation_authority()
    {
        Sr5CareerWizardSnapshot snapshot = Sr5CareerWizardProjector.Project(
            Binding(),
            [
                Available(Sr5CareerWizardActionIds.AdvanceAttribute, H('a'), "anchor.attribute"),
                Available(Sr5CareerWizardActionIds.AdvanceActiveSkill, H('b'), "anchor.skill")
            ]);
        var session = new Sr5CareerWizardDesktopSession();
        Sr5CareerWizardDesktopState state = session.Bind(snapshot);

        Assert.AreEqual(Sr5CareerWizardActionIds.AdvanceAttribute, state.SelectedActionId);
        Assert.IsFalse(state.CanConfirm);
        Assert.IsFalse(session.TrySelectAction(Sr5CareerWizardActionIds.ChangeQuality));
        Assert.IsTrue(session.TrySelectAction(Sr5CareerWizardActionIds.AdvanceActiveSkill));
        Assert.AreEqual(Sr5CareerWizardActionIds.AdvanceActiveSkill, session.State.SelectedActionId);
        Assert.IsFalse(session.State.CanConfirm);

        string[] publicMutators = typeof(Sr5CareerWizardDesktopSession)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .Where(static name => name.Contains("Apply", StringComparison.Ordinal)
                                  || name.Contains("Confirm", StringComparison.Ordinal)
                                  || name.Contains("Commit", StringComparison.Ordinal))
            .ToArray();
        Assert.HasCount(0, publicMutators);
    }

    [TestMethod]
    public void Checkpoint_round_trips_and_stale_binding_or_action_is_not_restored()
    {
        Sr5CareerWizardSnapshot snapshot = Sr5CareerWizardProjector.Project(
            Binding(),
            [
                Available(Sr5CareerWizardActionIds.AdvanceAttribute, H('a'), "anchor.attribute"),
                Available(Sr5CareerWizardActionIds.AdvanceActiveSkill, H('b'), "anchor.skill")
            ]);
        var session = new Sr5CareerWizardDesktopSession();
        session.Bind(snapshot);
        Assert.IsTrue(session.TrySelectAction(Sr5CareerWizardActionIds.AdvanceActiveSkill));
        Sr5CareerWizardCheckpoint checkpoint = session.CreateCheckpoint();
        byte[] payload = Sr5CareerWizardDesktopSession.SerializeCheckpoint(checkpoint);
        Assert.IsTrue(Sr5CareerWizardDesktopSession.TryDeserializeCheckpoint(payload, out Sr5CareerWizardCheckpoint? parsed));

        Sr5CareerWizardDesktopState restored = new Sr5CareerWizardDesktopSession().Bind(snapshot, parsed);
        Assert.IsTrue(restored.Resume.Restored);
        Assert.AreEqual(Sr5CareerWizardActionIds.AdvanceActiveSkill, restored.SelectedActionId);

        Sr5CareerWizardBinding revisedBinding = Binding() with { WorkspaceRevision = 42 };
        Sr5CareerWizardAuthorityAvailability revisedAttribute = Available(
            Sr5CareerWizardActionIds.AdvanceAttribute,
            H('a'),
            "anchor.attribute") with
        {
            Binding = revisedBinding
        };
        Sr5CareerWizardSnapshot revised = Sr5CareerWizardProjector.Project(
            revisedBinding,
            [revisedAttribute]);
        Sr5CareerWizardDesktopState staleRevision = new Sr5CareerWizardDesktopSession().Bind(revised, parsed);
        Assert.IsFalse(staleRevision.Resume.Restored);
        Assert.AreEqual(
            Sr5CareerWizardCheckpointInvalidationReasons.WorkspaceRevisionChanged,
            staleRevision.Resume.InvalidationReason);

        Sr5CareerWizardDesktopState staleDigest = new Sr5CareerWizardDesktopSession().Bind(
            snapshot,
            checkpoint with { SnapshotDigest = H('f') });
        Assert.IsFalse(staleDigest.Resume.Restored);
        Assert.AreEqual(
            Sr5CareerWizardCheckpointInvalidationReasons.SnapshotChanged,
            staleDigest.Resume.InvalidationReason);

        Sr5CareerWizardSnapshot actionRemoved = Sr5CareerWizardProjector.Project(
            Binding(),
            [Available(Sr5CareerWizardActionIds.AdvanceAttribute, H('a'), "anchor.attribute")]);
        Sr5CareerWizardCheckpoint unavailableAction = checkpoint with
        {
            SnapshotDigest = actionRemoved.SnapshotDigest
        };
        Sr5CareerWizardDesktopState unavailable = new Sr5CareerWizardDesktopSession()
            .Bind(actionRemoved, unavailableAction);
        Assert.IsFalse(unavailable.Resume.Restored);
        Assert.AreEqual(
            Sr5CareerWizardCheckpointInvalidationReasons.ActionUnavailable,
            unavailable.Resume.InvalidationReason);
        Assert.AreEqual(Sr5CareerWizardActionIds.AdvanceAttribute, unavailable.SelectedActionId);
    }

    [TestMethod]
    public void Tampered_snapshot_catalog_and_digest_are_rejected()
    {
        Sr5CareerWizardSnapshot snapshot = Sr5CareerWizardProjector.Project(
            Binding(),
            [Available(Sr5CareerWizardActionIds.AdvanceAttribute, H('a'), "anchor.attribute")]);
        var session = new Sr5CareerWizardDesktopSession();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Bind(snapshot with { SnapshotDigest = H('f') }));
        Sr5CareerWizardFamilyState first = snapshot.Families[0];
        Sr5CareerWizardSnapshot tampered = snapshot with
        {
            Families = [first with { FamilyId = "career.generic-edit" }, .. snapshot.Families.Skip(1)]
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Bind(tampered));
    }

    private static Sr5CareerWizardBinding Binding()
        => new(
            WorkspaceId: "workspace-career-1",
            WorkspaceRevision: 41,
            SavedRevision: 19,
            RulesetId: "sr5",
            RuntimeFingerprint: H('a'),
            SourceDigest: H('b'),
            ContentDigest: H('c'));

    private static Sr5CareerWizardAuthorityAvailability Available(
        string actionId,
        string digest,
        params string[] anchors)
        => new(Binding(), actionId, true, [], anchors, digest);

    private static Sr5CareerWizardAuthorityAvailability Blocked(
        string actionId,
        string digest,
        params string[] blockers)
        => new(Binding(), actionId, false, blockers, [], digest);

    private static Sr5CareerWizardActionState Action(
        Sr5CareerWizardSnapshot snapshot,
        string actionId)
        => snapshot.Families
            .SelectMany(static family => family.Actions)
            .Single(action => string.Equals(action.ActionId, actionId, StringComparison.Ordinal));

    private static string H(char value) => "sha256:" + new string(value, 64);
}
