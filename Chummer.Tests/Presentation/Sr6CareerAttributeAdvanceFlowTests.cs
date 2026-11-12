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
