using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

/// <summary>Presentation-side blockers surfaced by the attributes interaction boundary.</summary>
public static class CharacterCreationAttributesInteractionBlockers
{
    public const string OverviewAuthorityRequired = "creation-attributes-overview-authority-required";
    public const string BindingMismatch = "creation-attributes-binding-mismatch";
    public const string PreparedPreviewMismatch = "creation-attributes-prepared-preview-mismatch";
    public const string PreviewNotConfirmable = "creation-attributes-preview-not-confirmable";
    public const string PreviewDigestMismatch = "creation-attributes-preview-digest-mismatch";
    public const string ExplicitConfirmationRequired = "creation-attributes-explicit-confirmation-required";
    public const string AuthorityUnavailable = "creation-attributes-authority-unavailable";
}

/// <summary>
/// Renderer-neutral boundary for the SR5 Priority/Sum-to-Ten Attributes step.
/// Presentation passes exact Core allocations (attribute id, priority points,
/// karma levels) through preview and explicit confirmation; it never computes
/// attribute ratings and never writes character XML.
/// </summary>
public interface ICharacterCreationAttributesInteractionPresenter
{
    CharacterCreationAttributesInteractionLoadResult Load(CharacterOverviewState overview);

    CharacterCreationAttributesInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        IReadOnlyList<CharacterCreationAttributeAllocation> allocations);

    CharacterCreationAttributesInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationAttributesConfirmation confirmation);
}

public sealed record CharacterCreationAttributesConfirmation(
    IReadOnlyList<CharacterCreationAttributeAllocation> Allocations,
    string PreviewDigest,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationAttributesInteractionState(
    CharacterCreationAttributesBinding Binding,
    CharacterCreationPrerequisiteDraft? PrerequisiteDraft,
    CharacterCreationAttributesDraft? PendingDraft,
    IReadOnlyList<CharacterCreationAttributeProjection> Attributes,
    CharacterCreationBudgetState NormalPointBudget,
    CharacterCreationBudgetState SpecialPointBudget,
    CharacterCreationBudgetState CreationKarmaBudget,
    IReadOnlyList<string> Blockers,
    bool CanEdit,
    string SnapshotDigest);

public sealed record CharacterCreationAttributesPreparedPreview(
    string StateSnapshotDigest,
    CharacterCreationAttributesBinding Binding,
    CharacterCreationAttributesDraft? Before,
    IReadOnlyList<CharacterCreationAttributeProjection> Attributes,
    CharacterCreationBudgetState NormalPointBudget,
    CharacterCreationBudgetState SpecialPointBudget,
    CharacterCreationBudgetState CreationKarmaBudget,
    string PreviewDigest,
    bool RequiresExplicitConfirmation,
    bool CanConfirm,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationAttributesInteractionLoadResult(
    string Outcome,
    CharacterCreationAttributesInteractionState? State,
    string? Error)
{
    public static CharacterCreationAttributesInteractionLoadResult Ok(
        CharacterCreationAttributesInteractionState state) => new("ok", state, null);

    public static CharacterCreationAttributesInteractionLoadResult Fail(
        string outcome,
        string error) => new(outcome, null, error);
}

public sealed record CharacterCreationAttributesInteractionPrepareResult(
    string Outcome,
    CharacterCreationAttributesPreparedPreview? PreparedPreview,
    CharacterCreationAttributesInteractionState? State,
    string? Error)
{
    public static CharacterCreationAttributesInteractionPrepareResult Ok(
        CharacterCreationAttributesPreparedPreview prepared,
        CharacterCreationAttributesInteractionState state) => new("ok", prepared, state, null);

    public static CharacterCreationAttributesInteractionPrepareResult Fail(
        string outcome,
        CharacterCreationAttributesInteractionState? state,
        string error) => new(outcome, null, state, error);
}

public sealed record CharacterCreationAttributesInteractionConfirmResult(
    string Outcome,
    CharacterCreationAttributesInteractionState? State,
    string? Error)
{
    public static CharacterCreationAttributesInteractionConfirmResult Ok(
        CharacterCreationAttributesInteractionState state) => new("ok", state, null);

    public static CharacterCreationAttributesInteractionConfirmResult Fail(
        string outcome,
        CharacterCreationAttributesInteractionState? state,
        string error) => new(outcome, state, error);
}

public sealed class CharacterCreationAttributesInteractionPresenter : ICharacterCreationAttributesInteractionPresenter
{
    private readonly IOwnerBoundCharacterCreationAttributesService _attributesService;
    private OwnerContextStamp? _owner;
    private CharacterCreationAttributesBinding? _binding;
    private CharacterCreationAttributesPreparedPreview? _prepared;
    private CharacterCreationAttributeAllocation[] _allocations = [];
    private bool _dispatched;

    public bool ConfirmationDispatched => _dispatched;

    public bool IsCurrentContext(CharacterOverviewState overview)
        => _binding is { } binding && _owner is { IsValid: true }
            && overview.DisplayOwnerContext == _owner
            && overview.WorkspaceId == binding.WorkspaceId
            && overview.ActiveWorkspace?.Id == binding.WorkspaceId
            && overview.ContentRevision == binding.ContentRevision
            && overview.SavedRevision == binding.SavedRevision
            && overview.Profile?.Created == false;

    public CharacterCreationAttributesInteractionPresenter(
        IOwnerBoundCharacterCreationAttributesService attributesService)
    {
        _attributesService = attributesService ?? throw new ArgumentNullException(nameof(attributesService));
    }

    public CharacterCreationAttributesInteractionLoadResult Load(CharacterOverviewState overview)
    {
        if (TryLoadState(overview, out var state, out var error))
        {
            return CharacterCreationAttributesInteractionLoadResult.Ok(state);
        }

        return CharacterCreationAttributesInteractionLoadResult.Fail(
            CharacterCreationAttributesInteractionBlockers.AuthorityUnavailable,
            error ?? "attributes authority unavailable");
    }

    public CharacterCreationAttributesInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        IReadOnlyList<CharacterCreationAttributeAllocation> allocations)
    {
        if (_dispatched)
            return CharacterCreationAttributesInteractionPrepareResult.Fail(
                CharacterCreationAttributesInteractionBlockers.PreparedPreviewMismatch, null,
                "A confirmation was already sent. Close this editor and reload the saved character to check its result.");
        _prepared = null;
        CharacterCreationAttributesInteractionState? state = null;
        try
        {
            if (!TryLoadState(overview, out var loadedState, out var error))
            {
                return CharacterCreationAttributesInteractionPrepareResult.Fail(
                    CharacterCreationAttributesInteractionBlockers.AuthorityUnavailable,
                    null,
                    error ?? "attributes authority unavailable");
            }
            state = loadedState;

            CharacterCreationAttributeAllocation[] selected = allocations.ToArray();
            CharacterCreationAttributesPreviewRequest request = new(state.Binding, selected);
            CharacterCreationFoundationResult<CharacterCreationAttributesPreview> result =
                overview.DisplayOwnerContext is { IsValid: true } owner
                    ? _attributesService.Preview(owner, request)
                    : DeniedPreview();

            if (result.Outcome is not (CharacterCreationFoundationOutcomes.Success or CharacterCreationFoundationOutcomes.Blocked)
                || result.Value is null || result.Value.Binding != state.Binding)
            {
                return CharacterCreationAttributesInteractionPrepareResult.Fail(
                    result.Outcome,
                    state,
                    DescribeFailure("preview", result));
            }

            CharacterCreationAttributesPreview preview = result.Value;
            var prepared = new CharacterCreationAttributesPreparedPreview(
                state.SnapshotDigest,
                state.Binding,
                state.PendingDraft,
                preview.Attributes,
                preview.NormalPointBudget,
                preview.SpecialPointBudget,
                preview.CreationKarmaBudget,
                preview.PreviewDigest,
                preview.RequiresExplicitConfirmation,
                preview.CanConfirm,
                preview.Blockers);
            _prepared = prepared;
            _allocations = selected;
            return CharacterCreationAttributesInteractionPrepareResult.Ok(prepared, state);
        }
        catch (Exception failure) when (failure is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Audit 2026-11-04 P2-2: an expected service read failure during the
            // whole prepare operation yields a recoverable result. The prepared
            // result stays invalid (_prepared = null), confirmation stays off, no
            // write was attempted, and the caller's unsaved input is untouched.
            return CharacterCreationAttributesInteractionPrepareResult.Fail(
                "attributes-prepare-outcome-uncertain",
                state,
                "The character could not be read to prepare this adjustment. The saved allocation has not changed; reload or review the character and try again.");
        }
    }

    public CharacterCreationAttributesInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationAttributesConfirmation confirmation)
    {
        if (_dispatched || _prepared is not { CanConfirm: true } reviewed
            || !IsCurrentContext(overview)
            || confirmation.PreviewDigest != reviewed.PreviewDigest
            || !confirmation.Allocations.SequenceEqual(_allocations))
            return CharacterCreationAttributesInteractionConfirmResult.Fail(
                CharacterCreationAttributesInteractionBlockers.PreparedPreviewMismatch, null,
                "The reviewed character or allocation changed. Close this editor and review the current character again.");
        CharacterCreationAttributesInteractionState? state = null;
        try
        {
            if (!TryLoadState(overview, out var loadedState, out var error))
            {
                return CharacterCreationAttributesInteractionConfirmResult.Fail(
                    CharacterCreationAttributesInteractionBlockers.AuthorityUnavailable,
                    null,
                    error ?? "attributes authority unavailable");
            }
            state = loadedState;
        }
        catch (Exception failure) when (failure is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Audit 2026-11-04 P2-2: the pre-confirm re-read is part of the guarded
            // operation. A read failure before dispatch leaves the saved character
            // untouched, keeps confirmation unavailable, and preserves the editor
            // input (no exception escapes the interaction boundary).
            return CharacterCreationAttributesInteractionConfirmResult.Fail(
                "attributes-confirm-preread-outcome-uncertain",
                null,
                "The character could not be re-read before confirmation. The saved allocation has not changed and nothing was dispatched; reload the character and try again.");
        }

        if (state.Binding != reviewed.Binding || state.SnapshotDigest != reviewed.StateSnapshotDigest)
            return CharacterCreationAttributesInteractionConfirmResult.Fail(
                CharacterCreationAttributesInteractionBlockers.BindingMismatch, null,
                "The character changed after review. Reload before confirming.");

        if (!confirmation.ExplicitlyConfirmed)
        {
            return CharacterCreationAttributesInteractionConfirmResult.Fail(
                CharacterCreationAttributesInteractionBlockers.ExplicitConfirmationRequired,
                null,
                "explicit confirmation required before Core confirm");
        }

        CharacterCreationAttributesConfirmRequest request = new(
            reviewed.Binding,
            _allocations,
            reviewed.PreviewDigest,
            confirmation.ExplicitlyConfirmed);
        _dispatched = true;
        try
        {
            CharacterCreationFoundationResult<CharacterCreationAttributesReceipt> result =
                overview.DisplayOwnerContext is { IsValid: true } owner
                    ? _attributesService.Confirm(owner, request)
                    : DeniedReceipt();

            if (result.Outcome != CharacterCreationFoundationOutcomes.Success || result.Value is not { } receipt
                || receipt.WorkspaceId != reviewed.Binding.WorkspaceId
                || receipt.PreviousContentRevision != reviewed.Binding.ContentRevision)
            {
                return CharacterCreationAttributesInteractionConfirmResult.Fail(
                    result.Outcome,
                    null,
                    DescribeFailure("confirm", result));
            }

            // Core confirm returns a receipt; the refreshed state comes from a fresh Load.
            CharacterCreationFoundationResult<CharacterCreationAttributesState> reloaded =
                overview.DisplayOwnerContext is { IsValid: true } confirmedOwner
                    ? _attributesService.Load(confirmedOwner, new CharacterCreationAttributesLoadRequest(state.Binding.WorkspaceId))
                    : DeniedState();
            if (reloaded.Outcome != CharacterCreationFoundationOutcomes.Success || reloaded.Value is null)
            {
                return CharacterCreationAttributesInteractionConfirmResult.Fail(
                    CharacterCreationFoundationOutcomes.Conflict,
                    state,
                    "confirm succeeded but reload failed: " + DescribeFailure("load", reloaded));
            }

            return CharacterCreationAttributesInteractionConfirmResult.Ok(Project(reloaded.Value));
        }
        catch (Exception failure) when (failure is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return CharacterCreationAttributesInteractionConfirmResult.Fail(
                "confirmation-outcome-uncertain", null,
                "The confirmation result could not be read. Close this editor and reload the saved character before making another change.");
        }
    }

    private bool TryLoadState(
        CharacterOverviewState overview,
        out CharacterCreationAttributesInteractionState state,
        out string? error)
    {
        state = null!;
        error = null;
        if (overview.DisplayOwnerContext is not { IsValid: true } owner
            || overview.ActiveWorkspace is not { } active || overview.WorkspaceId != active.Id
            || overview.Profile?.Created != false
            || (_binding is not null && !IsCurrentContext(overview)))
        {
            error = "overview display owner context is not valid";
            return false;
        }

        CharacterCreationAttributesLoadRequest request = new(active.Id);
        CharacterCreationFoundationResult<CharacterCreationAttributesState> result =
            _attributesService.Load(owner, request);
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success || result.Value is not { } loaded
            || loaded.Binding.WorkspaceId != active.Id
            || loaded.Binding.ContentRevision != active.ContentRevision
            || loaded.Binding.SavedRevision != active.SavedRevision
            || (_binding is not null && loaded.Binding != _binding))
        {
            error = DescribeFailure("load", result);
            return false;
        }

        _owner ??= owner;
        _binding ??= loaded.Binding;
        state = Project(loaded);
        return true;
    }

    private static CharacterCreationAttributesInteractionState Project(
        CharacterCreationAttributesState state)
    {
        return new CharacterCreationAttributesInteractionState(
            state.Binding,
            state.PrerequisiteDraft,
            state.PendingDraft,
            state.Attributes,
            state.NormalPointBudget,
            state.SpecialPointBudget,
            state.CreationKarmaBudget,
            state.Blockers,
            state.CanEdit,
            state.SnapshotDigest);
    }

    private static CharacterCreationFoundationResult<CharacterCreationAttributesPreview> DeniedPreview()
    {
        return new CharacterCreationFoundationResult<CharacterCreationAttributesPreview>(
            CharacterCreationAttributesInteractionBlockers.OverviewAuthorityRequired,
            null,
            Array.Empty<string>());
    }

    private static CharacterCreationFoundationResult<CharacterCreationAttributesState> DeniedState()
    {
        return new CharacterCreationFoundationResult<CharacterCreationAttributesState>(
            CharacterCreationAttributesInteractionBlockers.OverviewAuthorityRequired,
            null,
            Array.Empty<string>());
    }

    private static CharacterCreationFoundationResult<CharacterCreationAttributesReceipt> DeniedReceipt()
    {
        return new CharacterCreationFoundationResult<CharacterCreationAttributesReceipt>(
            CharacterCreationAttributesInteractionBlockers.OverviewAuthorityRequired,
            null,
            Array.Empty<string>());
    }

    private static string DescribeFailure(string stage, object result)
    {
        var outcomeProperty = result.GetType().GetProperty("Outcome");
        var blockersProperty = result.GetType().GetProperty("Blockers");
        string outcome = outcomeProperty?.GetValue(result) as string ?? "unknown";
        var blockers = blockersProperty?.GetValue(result) as System.Collections.Generic.IReadOnlyList<string>
            ?? Array.Empty<string>();
        return stage + " outcome=" + outcome
            + (blockers.Count > 0 ? " blockers=[" + string.Join(",", blockers) + "]" : string.Empty);
    }
}
