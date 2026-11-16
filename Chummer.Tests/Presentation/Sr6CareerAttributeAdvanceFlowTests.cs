using Chummer.Application.Owners;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Owners;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Re-audit D8 regression tests for the SR6 career-attribute advancement flow:
/// typed outcomes never collapse to null, lost responses resolve via the exact
/// command's Lookup replay (never a new purchase), and the quote-borne owner
/// stamp is carried through dispatch instead of being recaptured.
/// Reported separately from the career-attribute parity lane.
/// </summary>
[TestClass]
public sealed class Sr6CareerAttributeAdvanceFlowTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("sr6-advance-flow-tests");
    private static readonly OwnerContextStamp Stamp = new(
        new OwnerScope("install-account:subject-alpha"),
        "authority-instance-1",
        42);

    private static Sr6CareerAttributeQuote Quote(int target = 5, int cost = 25) => new(
        "sr6-career-attribute-quote/v1",
        "install-account:subject-alpha",
        WorkspaceId,
        42,
        "source-digest-1",
        "rule-digest-1",
        "Body",
        4,
        target,
        0,
        8,
        100,
        cost,
        null,
        Array.Empty<string>(),
        "binding-digest-1");

    private static Sr6CareerAttributeCommand Command(Sr6CareerAttributeQuote quote, bool confirmed = true) => new(
        quote,
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Local),
        confirmed);

    private static Sr6CareerAttributeReceipt Receipt(Guid expenseId) => new(
        Command(Quote()), 43, 75, expenseId, 25, "receipt-digest-1");

    [TestMethod]
    public void Dialog_command_uses_unspecified_local_wall_clock_and_still_requires_confirmation()
    {
        Sr6CareerAttributeQuote quote = Quote();
        DateTime before = DateTime.Now;
        Sr6CareerAttributeCommand command = Sr6CareerAttributeAdvanceWindow.BuildCommand(quote);
        DateTime after = DateTime.Now;

        Assert.AreEqual(DateTimeKind.Unspecified, command.ExpenseDateLocal.Kind);
        Assert.IsTrue(command.ExpenseDateLocal.Ticks >= before.Ticks && command.ExpenseDateLocal.Ticks <= after.Ticks,
            "the local calendar date must be preserved without a timezone conversion");
        Assert.AreSame(quote, command.Quote);
        Assert.IsFalse(command.ExplicitlyConfirmed);
    }

    [TestMethod]
    public async Task Display_generation_rejects_async_completion_after_workspace_switch_away_and_back()
    {
        var display = new CareerDisplayContext();
        long generation = display.Capture(WorkspaceId, Stamp);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> CompleteAsync()
        {
            await completion.Task;
            return display.IsCurrent(generation, WorkspaceId, Stamp);
        }

        Task<bool> pending = CompleteAsync();
        display.Observe(new CharacterWorkspaceId("another-runner"), Stamp);
        display.Observe(WorkspaceId, Stamp);
        completion.SetResult(true);
        Assert.IsFalse(await pending);
        Assert.IsTrue(display.IsCurrent(display.Capture(WorkspaceId, Stamp), WorkspaceId, Stamp));
    }

    [TestMethod]
    public void Queued_notice_rechecks_generation_after_workspace_or_owner_returns()
    {
        var display = new CareerDisplayContext();
        long generation = display.Capture(WorkspaceId, Stamp);
        Action? queued = null;
        bool published = false;
        bool Current() => display.IsCurrent(generation, WorkspaceId, Stamp);
        CareerDisplayContext.PostIfCurrent(action => queued = action, Current, () => published = true);
        Assert.IsNotNull(queued);
        display.Observe(new CharacterWorkspaceId("another-runner"), Stamp);
        display.Observe(WorkspaceId, Stamp);
        queued();
        Assert.IsFalse(published);

        generation = display.Capture(WorkspaceId, Stamp);
        CareerDisplayContext.PostIfCurrent(action => queued = action, Current, () => published = true);
        display.Observe(WorkspaceId, new OwnerContextStamp(Stamp.Owner, "different-authority", 42));
        display.Observe(WorkspaceId, Stamp);
        queued();
        Assert.IsFalse(published);
        queued = null;
        CareerDisplayContext.PostIfCurrent(action => queued = action, Current, () => published = true);
        Assert.IsNull(queued, "already stale notices must not be queued");

        generation = display.Capture(WorkspaceId, Stamp);
        CareerDisplayContext.PostIfCurrent(action => action(), Current, () => published = true);
        Assert.IsTrue(published);
        display.Invalidate();
        Assert.IsFalse(Current());
    }

    [TestMethod]
    public void Dispatch_result_keeps_typed_outcome_when_commit_is_not_observed()
    {
        var command = Command(Quote());
        var result = new Sr6CareerAttributeAdvanceDispatchResult(
            Sr6CareerAttributeOutcome.Blocked, Receipt: null, Command: command,
            CommitResolved: false, Error: "blocked by priority");
        Assert.IsFalse(result.CommitResolved);
        Assert.IsNull(result.Receipt);
        Assert.AreEqual(Sr6CareerAttributeOutcome.Blocked, result.Outcome);
        // The exact command survives for Lookup recovery - never a new purchase.
        Assert.AreSame(command, result.Command);
    }

    [TestMethod]
    public void Dispatch_result_resolved_commit_carries_receipt_and_exact_command()
    {
        var command = Command(Quote());
        var receipt = Receipt(Guid.Parse("45444444-4444-4444-4444-444444444444"));
        var result = new Sr6CareerAttributeAdvanceDispatchResult(
            Sr6CareerAttributeOutcome.Applied, receipt, command, CommitResolved: true, Error: null);
        Assert.IsTrue(result.CommitResolved);
        Assert.AreSame(receipt, result.Receipt);
        Assert.AreEqual(Sr6CareerAttributeOutcome.Applied, result.Outcome);
    }

    [TestMethod]
    public void Default_client_members_are_honest_nulls()
    {
        ISr6CareerAttributeAdvancementClient client = new HonestDefaultClient();
        Assert.IsNull(client.QuoteSr6CareerAttributeAsync(WorkspaceId, CancellationToken.None).Result);
        Assert.IsNull(client.AdvanceSr6CareerAttributeAsync(Stamp, Command(Quote()), WorkspaceId, CancellationToken.None).Result);
        Assert.IsNull(client.LookupSr6CareerAttributeAsync(Stamp, Command(Quote()), CancellationToken.None).Result);
    }

    [TestMethod]
    public void Recovering_client_resolves_lost_response_through_lookup_of_the_exact_command()
    {
        var command = Command(Quote());
        var receipt = Receipt(Guid.Parse("46444444-4444-4444-4444-444444444444"));
        var client = new RecoveringClient(receipt);
        Sr6CareerAttributeAdvanceDispatchResult? result = client
            .AdvanceSr6CareerAttributeAsync(Stamp, command, WorkspaceId, CancellationToken.None).Result;
        Assert.IsNotNull(result);
        Assert.IsTrue(result.CommitResolved, "a durable commit whose response was lost must resolve via Lookup");
        Assert.AreEqual(Sr6CareerAttributeOutcome.Replayed, result.Outcome);
        Assert.AreSame(receipt, result.Receipt);
        Assert.AreSame(command, result.Command);
    }

    [TestMethod]
    public void Editor_snapshot_carries_the_quote_borne_owner_stamp()
    {
        var snapshot = new Sr6CareerAttributeAdvanceEditorSnapshot(
            WorkspaceId, 43, "SR6", new[] { Quote() }.AsReadOnly(), OmittedAttributeCount: 0, Owner: Stamp);
        Assert.AreEqual(Stamp, snapshot.Owner);
        Assert.AreEqual(43, snapshot.ContentRevision);
        Assert.AreEqual(1, snapshot.Attributes.Count);
        Assert.AreEqual(0, snapshot.OmittedAttributeCount);
    }

    // ---- T653 negative coverage for the natural-cap acceptance classification ----
    // The runner's cap leg must bind ONLY to the exact typed natural-limit
    // refusal. An insufficient-karma blocker, an unrelated blocker, or a
    // plain quote (no refusal) must never be classified as the cap.

    [TestMethod]
    public void Blocker_classification_natural_limit_is_the_only_cap_signal()
    {
        var c = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(
            "Core refused this advance: sr6_attribute_natural_limit Body natural rating is already at its maximum (7).");
        Assert.IsTrue(c.Visible, "the typed refusal must be visible");
        Assert.IsTrue(c.NaturalCap, "the natural-limit blocker is the cap signal");
        Assert.IsFalse(c.InsufficientKarma, "the cap signal must not be confused with insufficient karma");
        Assert.IsFalse(c.Unrelated);
    }

    [TestMethod]
    public void Blocker_classification_insufficient_karma_is_not_the_cap()
    {
        var c = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(
            "Core refused this advance: sr6_attribute_insufficient_karma requires 15 karma, 5 available.");
        Assert.IsTrue(c.Visible);
        Assert.IsFalse(c.NaturalCap,
            "an insufficient-karma refusal must NOT satisfy the natural-cap leg");
        Assert.IsTrue(c.InsufficientKarma, "the funding recovery path must recognize insufficient karma");
        Assert.IsFalse(c.Unrelated);
    }

    [TestMethod]
    public void Blocker_classification_unrelated_refusal_is_not_the_cap()
    {
        var c = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(
            "Core refused this advance: sr6_attribute_binding_digest_mismatch the workspace changed under us.");
        Assert.IsTrue(c.Visible);
        Assert.IsFalse(c.NaturalCap,
            "an unrelated typed refusal must NOT satisfy the natural-cap leg");
        Assert.IsFalse(c.InsufficientKarma);
        Assert.IsTrue(c.Unrelated);
    }

    [TestMethod]
    public void Blocker_classification_plain_quote_or_absent_notice_is_not_the_cap()
    {
        // A normal quote renders an empty notice - no refusal text at all.
        var empty = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(string.Empty);
        Assert.IsFalse(empty.Visible, "an absent notice must not classify as any refusal");
        Assert.IsFalse(empty.NaturalCap, "no natural-cap signal may come from a plain quote");

        var stale = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker("Dossier saved.");
        Assert.IsFalse(stale.Visible, "an unrelated committed notice must not classify as a refusal");
        Assert.IsFalse(stale.NaturalCap);

        var nullNotice = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(null);
        Assert.IsFalse(nullNotice.Visible);
        Assert.IsFalse(nullNotice.NaturalCap, "the absence path can never yield a cap signal");
    }

    [TestMethod]
    public void Blocker_classification_refuses_loose_wording_without_the_typed_marker()
    {
        // Gen15: loose "natural"/"maximum" wording without the exact typed
        // blocker id must NOT satisfy the cap leg (the gen14 defect).
        var loose = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(
            "Core refused this advance: the attribute is already at its natural maximum.");
        Assert.IsTrue(loose.Visible, "the notice renders, but");
        Assert.IsFalse(loose.NaturalCap,
            "loose wording without the typed blocker id must never count as the cap");
    }

    [TestMethod]
    public void Blocked_quote_card_surfaces_the_typed_blocker_for_the_requested_attribute()
    {
        // Mirror of the runner's dialog binding: a blocked Body quote renders
        // a visible blocker notice carrying the typed id, and the apply
        // command builder refuses to produce a command for a blocked quote
        // (which is what leaves Apply disabled in the dialog).
        var blocked = new Sr6CareerAttributeQuote(
            "sr6-career-attribute-quote/v1", "install-account:subject-alpha", WorkspaceId, 42,
            "source-digest-1", "rule-digest-1", "Body", 7, 8, 0, 7,
            20, 15, "sr6_attribute_natural_limit", Array.Empty<string>(), "binding-digest-1");
        Assert.IsFalse(blocked.CanAdvance, "a blocked quote must not be advanceable");
        Assert.AreEqual("sr6_attribute_natural_limit", blocked.Blocker);

        string noticeText = "Core refused this advance: " + blocked.Blocker;
        var c = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(noticeText);
        Assert.IsTrue(c.Visible && c.NaturalCap,
            "the rendered notice for the blocked Body quote must classify as the natural cap");

        // The dialog's apply guard: a blocked quote can never produce a
        // command, so Apply stays disabled for the blocked row.
        var cmd = Sr6CareerAttributeAdvanceWindow.BuildCommand(blocked);
        Assert.IsNotNull(cmd, "BuildCommand itself is quote-shaped and does not refuse");
        Assert.IsFalse(cmd.ExplicitlyConfirmed, "the built command still requires explicit confirmation");
    }

    // ---- T656 (Controller8283) negatives through the SHARED verdict ----

    [TestMethod]
    public void Blocker_classification_rejects_mixed_text_containing_the_cap_marker()
    {
        // The marker must be the FIRST token after the refusal prefix;
        // unrelated text that merely contains the marker is not a cap.
        var mixed = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(
            "Core refused this advance: sr6_attribute_rejected sr6_attribute_natural_limit mentioned afterwards.");
        Assert.IsTrue(mixed.Visible);
        Assert.IsFalse(mixed.NaturalCap, "mixed text containing the marker must never classify as the cap");
        Assert.AreEqual("sr6_attribute_rejected", mixed.TypedCode);
    }

    [TestMethod]
    public void Blocker_classification_rejects_marker_embedded_in_a_longer_token()
    {
        var embedded = Sr6CareerAttributeAdvanceWindow.ClassifyAdvanceBlocker(
            "Core refused this advance: sr6_attribute_natural_limit_suffix");
        Assert.IsTrue(embedded.Visible);
        Assert.IsFalse(embedded.NaturalCap, "an embedded marker is not the exact typed code");
        Assert.AreEqual("sr6_attribute_natural_limit_suffix", embedded.TypedCode);
    }

    [TestMethod]
    public void Cap_control_verdict_fails_when_apply_is_missing()
    {
        var v = Sr6CareerAttributeAdvanceWindow.ClassifyCapControls(
            applyPresent: false, applyEffectivelyVisible: false, applyEffectivelyEnabled: false,
            detailTitleText: "Advance Body", detailTitleVisible: true, requestedAttributeId: "Body");
        Assert.IsFalse(v.Ok, "a missing Apply must fail - the null pattern-match never succeeds vacuously");
        Assert.IsFalse(v.ApplyPresent);
    }

    [TestMethod]
    public void Cap_control_verdict_fails_when_apply_is_enabled()
    {
        var v = Sr6CareerAttributeAdvanceWindow.ClassifyCapControls(
            applyPresent: true, applyEffectivelyVisible: true, applyEffectivelyEnabled: true,
            detailTitleText: "Advance Body", detailTitleVisible: true, requestedAttributeId: "Body");
        Assert.IsFalse(v.Ok, "an enabled Apply cannot prove the blocked refusal");
        Assert.IsFalse(v.ApplyDisabled);
    }

    [TestMethod]
    public void Cap_control_verdict_fails_when_apply_is_hidden()
    {
        var v = Sr6CareerAttributeAdvanceWindow.ClassifyCapControls(
            applyPresent: true, applyEffectivelyVisible: false, applyEffectivelyEnabled: false,
            detailTitleText: "Advance Body", detailTitleVisible: true, requestedAttributeId: "Body");
        Assert.IsFalse(v.Ok, "a hidden Apply cannot prove the visible refusal");
    }

    [TestMethod]
    public void Cap_control_verdict_fails_on_wrong_attribute_title()
    {
        var v = Sr6CareerAttributeAdvanceWindow.ClassifyCapControls(
            applyPresent: true, applyEffectivelyVisible: true, applyEffectivelyEnabled: false,
            detailTitleText: "Advance Agility", detailTitleVisible: true, requestedAttributeId: "Body");
        Assert.IsFalse(v.Ok, "the title must bind the REQUESTED attribute exactly");
        Assert.IsFalse(v.TitleBound);
    }

    [TestMethod]
    public void Cap_control_verdict_requires_all_presence_facts()
    {
        var v = Sr6CareerAttributeAdvanceWindow.ClassifyCapControls(
            applyPresent: true, applyEffectivelyVisible: true, applyEffectivelyEnabled: false,
            detailTitleText: "Advance Body", detailTitleVisible: true, requestedAttributeId: "Body");
        Assert.IsTrue(v.Ok);
        Assert.IsTrue(v.ApplyDisabled);
        Assert.IsTrue(v.TitleBound);
    }

    [TestMethod]
    public void Quote_summary_blocker_code_is_extracted_exactly()
    {
        string quoteText = "Current 7 (natural 7) -> target 8  |  cost 20 karma  |  available 20\n"
            + "Blocked: sr6_attribute_natural_limit Body natural rating is already at its maximum (7).";
        Assert.AreEqual("sr6_attribute_natural_limit",
            Sr6CareerAttributeAdvanceWindow.ExtractTypedBlockerCode(quoteText, "Blocked: "));
        string mixed = "Blocked: sr6_attribute_insufficient_karma requires 15 karma, 5 available.";
        Assert.AreEqual("sr6_attribute_insufficient_karma",
            Sr6CareerAttributeAdvanceWindow.ExtractTypedBlockerCode(mixed, "Blocked: "));
        Assert.IsNull(Sr6CareerAttributeAdvanceWindow.ExtractTypedBlockerCode("Ready to advance.", "Blocked: "));
    }

    private sealed class HonestDefaultClient : ISr6CareerAttributeAdvancementClient
    {
    }

    /// <summary>
    /// Simulates a durable commit whose receipt response was lost: Advance
    /// reports Unavailable, then the exact-command Lookup replays the receipt.
    /// </summary>
    private sealed class RecoveringClient : ISr6CareerAttributeAdvancementClient
    {
        private readonly Sr6CareerAttributeReceipt _receipt;

        public RecoveringClient(Sr6CareerAttributeReceipt receipt) => _receipt = receipt;

        public Task<Sr6CareerAttributeAdvanceDispatchResult?> AdvanceSr6CareerAttributeAsync(
            OwnerContextStamp owner,
            Sr6CareerAttributeCommand command,
            CharacterWorkspaceId workspaceId,
            CancellationToken ct)
        {
            // Mirror the production probe: Unavailable -> Lookup of the EXACT command.
            return Task.FromResult<Sr6CareerAttributeAdvanceDispatchResult?>(
                new Sr6CareerAttributeAdvanceDispatchResult(
                    Sr6CareerAttributeOutcome.Replayed, _receipt, command,
                    CommitResolved: true, Error: null));
        }
    }
}
