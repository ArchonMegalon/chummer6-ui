using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>One owner-bound manual allocation review. Core owns legality and pricing.</summary>
public sealed class CharacterCreationSkillsInteractionPresenter
{
    private readonly IOwnerBoundCharacterCreationSkillsService _skillsService;
    private OwnerContextStamp? _owner;
    private CharacterCreationSkillsBinding? _binding;
    private CharacterCreationSkillsInteractionPreparedPreview? _prepared;
    private bool _dispatched;

    public CharacterCreationSkillsInteractionPresenter(IOwnerBoundCharacterCreationSkillsService skillsService)
        => _skillsService = skillsService ?? throw new ArgumentNullException(nameof(skillsService));

    public bool IsCurrentContext(CharacterOverviewState overview)
        => _binding is { } binding && _owner is { IsValid: true }
            && overview.DisplayOwnerContext == _owner
            && overview.WorkspaceId == binding.WorkspaceId
            && overview.ActiveWorkspace?.Id == binding.WorkspaceId
            && overview.ContentRevision == binding.ContentRevision
            && overview.SavedRevision == binding.SavedRevision
            && overview.Profile?.Created == false;

    public CharacterCreationSkillsInteractionLoadResult Load(CharacterOverviewState overview)
        => TryLoadState(overview, out var state, out var error)
            ? CharacterCreationSkillsInteractionLoadResult.Ok(state)
            : CharacterCreationSkillsInteractionLoadResult.Fail(
                CharacterCreationSkillsInteractionBlockers.AuthorityUnavailable, error!);

    public CharacterCreationSkillsInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        IReadOnlyList<CharacterCreationSkillAllocation> allocations,
        IReadOnlyList<CharacterCreationSkillGroupAllocation> groupAllocations)
    {
        if (_dispatched)
            return CharacterCreationSkillsInteractionPrepareResult.Fail("confirmation-already-dispatched",
                "A confirmation was already sent. Close this editor and reload the character to check its result.");
        _prepared = null;
        if (!TryLoadState(overview, out var state, out var error))
            return CharacterCreationSkillsInteractionPrepareResult.Fail(
                CharacterCreationSkillsInteractionBlockers.AuthorityUnavailable, error!);
        var selected = Array.AsReadOnly(allocations.ToArray());
        var groups = Array.AsReadOnly(groupAllocations.ToArray());
        try
        {
            var result = _skillsService.Preview(state.Owner, new(state.State.Binding, selected, groups));
            if (result.Outcome is not (CharacterCreationFoundationOutcomes.Success or CharacterCreationFoundationOutcomes.Blocked)
                || result.Value is not { } preview || preview.Binding != state.State.Binding)
                return CharacterCreationSkillsInteractionPrepareResult.Fail(
                    CharacterCreationSkillsInteractionBlockers.PreviewRejected, "Skills preview is unavailable. Your saved allocation has not changed.");
            _prepared = new(state, preview, selected, groups);
            return CharacterCreationSkillsInteractionPrepareResult.Ok(_prepared);
        }
        catch (Exception errorResult) when (errorResult is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return CharacterCreationSkillsInteractionPrepareResult.Fail("preview-unavailable",
                "Skills could not be reviewed. Your saved allocation has not changed; reload the character and try again.");
        }
    }

    public CharacterCreationSkillsInteractionConfirmResult Confirm(
        CharacterOverviewState overview, CharacterCreationSkillsInteractionConfirmation confirmation)
    {
        if (_dispatched || _prepared is not { Preview.CanConfirm: true } reviewed
            || !IsCurrentContext(overview)
            || confirmation.State.State.Binding != reviewed.State.State.Binding
            || confirmation.State.SnapshotDigest != reviewed.State.SnapshotDigest
            || confirmation.PreviewDigest != reviewed.Preview.PreviewDigest
            || !confirmation.Allocations.SequenceEqual(reviewed.Allocations)
            || !confirmation.GroupAllocations.SequenceEqual(reviewed.GroupAllocations))
            return CharacterCreationSkillsInteractionConfirmResult.Fail(
                CharacterCreationSkillsInteractionBlockers.StaleSnapshot,
                "The character or reviewed allocation changed. Close this editor and review the current character again.");
        if (!confirmation.ExplicitlyConfirmed)
            return CharacterCreationSkillsInteractionConfirmResult.Fail(
                CharacterCreationSkillsInteractionBlockers.ConfirmationRequired, "Confirm the reviewed allocation before submitting it.");
        if (!TryLoadState(overview, out var state, out var error)
            || state.SnapshotDigest != reviewed.State.SnapshotDigest)
            return CharacterCreationSkillsInteractionConfirmResult.Fail(
                CharacterCreationSkillsInteractionBlockers.StaleSnapshot, error ?? "Skills changed after review. Reload before confirming.");
        _dispatched = true;
        try
        {
            var result = _skillsService.Confirm(reviewed.State.Owner,
                new(reviewed.State.State.Binding, reviewed.Allocations, reviewed.GroupAllocations,
                    reviewed.Preview.PreviewDigest, reviewed.IdempotencyKey, ExplicitlyConfirmed: true));
            if (result.Outcome != CharacterCreationFoundationOutcomes.Success || result.Value is not { } receipt
                || receipt.WorkspaceId != reviewed.State.WorkspaceId
                || receipt.PreviousContentRevision != reviewed.State.State.Binding.ContentRevision
                || receipt.PreviewDigest != reviewed.Preview.PreviewDigest)
                return CharacterCreationSkillsInteractionConfirmResult.Fail(
                    CharacterCreationSkillsInteractionBlockers.ConfirmRejected,
                    "The confirmation could not be verified. Close this editor and reload the character before making another change.");
            return CharacterCreationSkillsInteractionConfirmResult.Ok(receipt);
        }
        catch (Exception errorResult) when (errorResult is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return CharacterCreationSkillsInteractionConfirmResult.Fail("confirmation-outcome-uncertain",
                "The confirmation result could not be read. Close this editor and reload the character to check the saved allocation before another change.");
        }
    }

    private bool TryLoadState(CharacterOverviewState overview,
        out CharacterCreationSkillsInteractionState state, out string? error)
    {
        state = null!;
        error = "The original character context is no longer current. Reload the character before editing.";
        if (overview.DisplayOwnerContext is not { IsValid: true } owner
            || overview.ActiveWorkspace is not { } active || overview.WorkspaceId != active.Id
            || overview.Profile?.Created != false || (_binding is not null && !IsCurrentContext(overview)))
            return false;
        var result = _skillsService.Load(owner, new(active.Id));
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success || result.Value is not { } skills
            || skills.Binding.WorkspaceId != active.Id
            || skills.Binding.ContentRevision != active.ContentRevision
            || skills.Binding.SavedRevision != active.SavedRevision
            || (_binding is not null && skills.Binding != _binding))
            return false;
        _owner ??= owner;
        _binding ??= skills.Binding;
        state = new(active.Id, owner, skills.SnapshotDigest, skills);
        error = null;
        return true;
    }
}

/// <summary>Interaction-level outcome vocabulary for the skills stage.</summary>
public static class CharacterCreationSkillsInteractionBlockers
{
    public const string AuthorityUnavailable = "creation-skills-authority-unavailable";
    public const string PreviewRejected = "creation-skills-preview-rejected";
    public const string ConfirmRejected = "creation-skills-confirm-rejected";
    public const string StaleSnapshot = "creation-skills-stale-snapshot";
    public const string ConfirmationRequired = "creation-skills-confirmation-required";
}

public sealed record CharacterCreationSkillsInteractionState(
    CharacterWorkspaceId WorkspaceId,
    OwnerContextStamp Owner,
    string SnapshotDigest,
    CharacterCreationSkillsState State);


public sealed record CharacterCreationSkillsInteractionPreparedPreview(
    CharacterCreationSkillsInteractionState State,
    CharacterCreationSkillsPreview Preview,
    IReadOnlyList<CharacterCreationSkillAllocation> Allocations,
    IReadOnlyList<CharacterCreationSkillGroupAllocation> GroupAllocations)
{
    public string IdempotencyKey { get; } = Guid.NewGuid().ToString("N");
}

public sealed record CharacterCreationSkillsInteractionConfirmation(
    CharacterCreationSkillsInteractionState State,
    IReadOnlyList<CharacterCreationSkillAllocation> Allocations,
    IReadOnlyList<CharacterCreationSkillGroupAllocation> GroupAllocations,
    string PreviewDigest,
    string IdempotencyKey,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationSkillsInteractionLoadResult(
    string Outcome,
    CharacterCreationSkillsInteractionState? State,
    string? Error)
{
    public static CharacterCreationSkillsInteractionLoadResult Ok(
        CharacterCreationSkillsInteractionState state) =>
        new("ok", state, null);

    public static CharacterCreationSkillsInteractionLoadResult Fail(string outcome, string error) =>
        new(outcome, null, error);
}

public sealed record CharacterCreationSkillsInteractionPrepareResult(
    string Outcome,
    CharacterCreationSkillsInteractionPreparedPreview? Preview,
    string? Error)
{
    public static CharacterCreationSkillsInteractionPrepareResult Ok(
        CharacterCreationSkillsInteractionPreparedPreview preview) =>
        new("ok", preview, null);

    public static CharacterCreationSkillsInteractionPrepareResult Fail(string outcome, string error) =>
        new(outcome, null, error);
}

public sealed record CharacterCreationSkillsInteractionConfirmResult(
    string Outcome,
    CharacterCreationSkillsReceipt? Receipt,
    string? Error)
{
    public static CharacterCreationSkillsInteractionConfirmResult Ok(
        CharacterCreationSkillsReceipt receipt) =>
        new("ok", receipt, null);

    public static CharacterCreationSkillsInteractionConfirmResult Fail(string outcome, string error) =>
        new(outcome, null, error);
}
