using System;
using System.Collections.Generic;
using System.IO;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Owner-bound Magic/Resonance creation interaction. Core owns legality and pricing; this
/// presenter only adapts the owner-bound Core read to the static Presentation workflow
/// (<see cref="CharacterCreationMagicResonanceWorkflow"/>: Project / CreateDraft / Review /
/// Confirm) and enforces the interaction-level discipline the skills stage established:
/// one dispatched confirmation, a fresh-context re-read at confirm time, and fail-closed
/// outcomes on every ambiguous path. No character XML is read or written here.
/// </summary>
public sealed class CharacterCreationMagicResonanceInteractionPresenter
{
    private readonly IOwnerBoundCharacterCreationMagicResonanceService _service;
    private OwnerContextStamp? _owner;
    private CharacterCreationMagicResonanceBinding? _binding;
    private bool _dispatched;

    public CharacterCreationMagicResonanceInteractionPresenter(
        IOwnerBoundCharacterCreationMagicResonanceService service)
        => _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>True when the loaded binding still matches the live overview.</summary>
    public bool IsCurrentContext(CharacterOverviewState overview)
        => _binding is { } binding && _owner is { IsValid: true }
            && overview.DisplayOwnerContext == _owner
            && overview.WorkspaceId == binding.WorkspaceId
            && overview.ActiveWorkspace?.Id == binding.WorkspaceId
            && overview.ContentRevision == binding.ContentRevision
            && overview.SavedRevision == binding.SavedRevision
            && overview.Profile?.Created == false;

    public CharacterCreationMagicResonanceInteractionLoadResult Load(CharacterOverviewState overview)
        => TryLoadState(overview, out CharacterCreationMagicResonanceInteractionState? state, out string? error)
            ? CharacterCreationMagicResonanceInteractionLoadResult.Ok(state)
            : CharacterCreationMagicResonanceInteractionLoadResult.Fail(
                CharacterCreationMagicResonanceInteractionBlockers.AuthorityUnavailable, error!);

    /// <summary>
    /// Builds the desktop draft from the proposed selections and reviews it through Core.
    /// Returns the Core-reviewed preview plus the prepared draft and idempotency key;
    /// no write happens and the saved character is untouched on every failure path.
    /// </summary>
    public CharacterCreationMagicResonanceInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationMagicResonanceSelections selections,
        int mysticAdeptPowerPoints)
    {
        try
        {
            if (!TryLoadState(overview, out CharacterCreationMagicResonanceInteractionState? state, out string? error))
                return CharacterCreationMagicResonanceInteractionPrepareResult.Fail(
                    CharacterCreationMagicResonanceInteractionBlockers.AuthorityUnavailable, error!);

            CharacterCreationMagicResonanceEditorState editor =
                CharacterCreationMagicResonanceWorkflow.Project(state.State);
            if (!editor.CanEdit)
                return CharacterCreationMagicResonanceInteractionPrepareResult.Fail(
                    CharacterCreationMagicResonanceInteractionBlockers.AuthorityUnavailable,
                    "Magic/Resonance editing is not backed by current Core authority.");

            CharacterCreationMagicResonanceDesktopDraft draft =
                CharacterCreationMagicResonanceWorkflow.CreateDraft(
                    editor,
                    selections.Tradition,
                    selections.Stream,
                    selections.AdeptPowers,
                    selections.Spells,
                    selections.ComplexForms,
                    mysticAdeptPowerPoints);
            CharacterCreationMagicResonanceReview review =
                CharacterCreationMagicResonanceWorkflow.Review(SharedService, editor, draft);
            var prepared = new CharacterCreationMagicResonanceInteractionPreparedPreview(
                state, review, draft, mysticAdeptPowerPoints);
            return CharacterCreationMagicResonanceInteractionPrepareResult.Ok(prepared);
        }
        catch (Exception errorResult) when (errorResult is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // The whole prepare operation (state read + projection + draft build + Core
            // preview) is guarded: a failure yields a recoverable result — no prepared
            // preview, confirmation stays off, and the saved character is untouched.
            return CharacterCreationMagicResonanceInteractionPrepareResult.Fail(
                "creation-magic-prepare-outcome-uncertain",
                "Magic/Resonance could not be reviewed. Your saved choices have not changed; reopen the editor and try again.");
        }
    }

    /// <summary>
    /// Confirms the Core-reviewed preview with an explicit idempotency key. The fresh
    /// overview is re-read first; a stale binding is refused without dispatching.
    /// </summary>
    public CharacterCreationMagicResonanceInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationMagicResonanceInteractionPreparedPreview prepared,
        bool explicitlyConfirmed)
    {
        if (_dispatched)
            return CharacterCreationMagicResonanceInteractionConfirmResult.Fail(
                "creation-magic-confirmation-already-dispatched",
                "A confirmation was already sent. Close this editor and reload the character to check its result.");
        if (prepared is null || prepared.Preview.CanConfirm is false)
            return CharacterCreationMagicResonanceInteractionConfirmResult.Fail(
                CharacterCreationMagicResonanceInteractionBlockers.StaleSnapshot,
                "The reviewed preview is no longer valid. Reopen the editor and review again.");
        if (!IsCurrentContext(overview))
            return CharacterCreationMagicResonanceInteractionConfirmResult.Fail(
                CharacterCreationMagicResonanceInteractionBlockers.StaleSnapshot,
                "The character changed after review. Reopen the editor and review the current character again.");
        if (!explicitlyConfirmed)
            return CharacterCreationMagicResonanceInteractionConfirmResult.Fail(
                CharacterCreationMagicResonanceInteractionBlockers.ConfirmationRequired,
                "Confirm the reviewed choices before submitting them.");

        // Chain-B pre-read: confirm only against the binding and snapshot Core reports now.
        if (!TryLoadState(overview, out CharacterCreationMagicResonanceInteractionState? fresh, out string? error))
            return CharacterCreationMagicResonanceInteractionConfirmResult.Fail(
                "creation-magic-confirm-preread-outcome-uncertain",
                error ?? "Magic/Resonance changed after review. Reopen the editor before confirming.");
        if (fresh.State.Binding != prepared.State.State.Binding
            || !string.Equals(
                fresh.State.SnapshotDigest,
                prepared.State.SnapshotDigest,
                StringComparison.Ordinal))
            return CharacterCreationMagicResonanceInteractionConfirmResult.Fail(
                CharacterCreationMagicResonanceInteractionBlockers.StaleSnapshot,
                "Magic/Resonance changed after review. Reopen the editor before confirming.");

        _dispatched = true;
        try
        {
            CharacterCreationMagicResonanceConfirmation confirmation =
                CharacterCreationMagicResonanceWorkflow.Confirm(
                    SharedService,
                    prepared.Review,
                    prepared.IdempotencyKey,
                    explicitlyConfirmed);
            if (confirmation.Receipt.WorkspaceId != prepared.State.WorkspaceId)
                return CharacterCreationMagicResonanceInteractionConfirmResult.Fail(
                    CharacterCreationMagicResonanceInteractionBlockers.ConfirmRejected,
                    "The confirmation could not be verified. Close this editor and reload the character before making another change.");
            return CharacterCreationMagicResonanceInteractionConfirmResult.Ok(
                confirmation.Receipt, confirmation.IsIdempotentReplay);
        }
        catch (Exception errorResult) when (errorResult is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return CharacterCreationMagicResonanceInteractionConfirmResult.Fail(
                "creation-magic-confirm-outcome-uncertain",
                "The confirmation result could not be read. Close this editor and reload the character to check whether the choices were applied.");
        }
    }

    /// <summary>
    /// The shared-interface view of the owner-bound service, pinned to the owner captured
    /// at Load time. The static workflow accepts only the shared interface; this adapter
    /// keeps the owner stamp on every delegated call without weakening Core's checks.
    /// </summary>
    private ICharacterCreationMagicResonanceService SharedService =>
        _sharedAdapter ??= new OwnerBoundToSharedAdapter(_service, () => _owner);
    private ICharacterCreationMagicResonanceService? _sharedAdapter;

    private bool TryLoadState(
        CharacterOverviewState overview,
        out CharacterCreationMagicResonanceInteractionState? state,
        out string? error)
    {
        state = null;
        error = "The original character context is no longer current. Reload the character before editing.";
        if (overview.DisplayOwnerContext is not { IsValid: true } owner
            || overview.ActiveWorkspace is not { } active
            || overview.WorkspaceId != active.Id
            || overview.Profile?.Created != false)
            return false;
        CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> result =
            _service.Load(owner, new CharacterCreationMagicResonanceLoadRequest(active.Id));
        if (result.Outcome != CharacterCreationFoundationOutcomes.Success
            || result.Value is not { } core
            || core.Binding.WorkspaceId != active.Id
            || core.Binding.ContentRevision != active.ContentRevision
            || core.Binding.SavedRevision != active.SavedRevision)
            return false;
        _owner ??= owner;
        _binding ??= core.Binding;
        state = new CharacterCreationMagicResonanceInteractionState(
            active.Id, owner, core.SnapshotDigest, core);
        error = null;
        return true;
    }

    private sealed class OwnerBoundToSharedAdapter : ICharacterCreationMagicResonanceService
    {
        private const string OwnerUnavailableMessage =
            "The Magic/Resonance owner context is unavailable; the workflow cannot proceed honestly.";

        private readonly IOwnerBoundCharacterCreationMagicResonanceService _inner;
        private readonly Func<OwnerContextStamp?> _owner;

        public OwnerBoundToSharedAdapter(
            IOwnerBoundCharacterCreationMagicResonanceService inner,
            Func<OwnerContextStamp?> owner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _owner = owner;
        }

        public CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> Load(
            CharacterCreationMagicResonanceLoadRequest request)
        {
            OwnerContextStamp owner = _owner()
                ?? throw new InvalidOperationException(OwnerUnavailableMessage);
            return _inner.Load(owner, request);
        }

        public CharacterCreationFoundationResult<CharacterCreationMagicResonancePreview> Preview(
            CharacterCreationMagicResonancePreviewRequest request)
        {
            OwnerContextStamp owner = _owner()
                ?? throw new InvalidOperationException(OwnerUnavailableMessage);
            return _inner.Preview(owner, request);
        }

        public CharacterCreationFoundationResult<CharacterCreationMagicResonanceReceipt> Confirm(
            CharacterCreationMagicResonanceConfirmRequest request)
        {
            OwnerContextStamp owner = _owner()
                ?? throw new InvalidOperationException(OwnerUnavailableMessage);
            return _inner.Confirm(owner, request);
        }
    }
}

/// <summary>Interaction-level outcome vocabulary for the Magic/Resonance stage.</summary>
public static class CharacterCreationMagicResonanceInteractionBlockers
{
    public const string AuthorityUnavailable = "creation-magic-resonance-authority-unavailable";
    public const string PreviewRejected = "creation-magic-resonance-preview-rejected";
    public const string ConfirmRejected = "creation-magic-resonance-confirm-rejected";
    public const string StaleSnapshot = "creation-magic-resonance-stale-snapshot";
    public const string ConfirmationRequired = "creation-magic-resonance-confirmation-required";
}

public sealed record CharacterCreationMagicResonanceInteractionState(
    CharacterWorkspaceId WorkspaceId,
    OwnerContextStamp Owner,
    string SnapshotDigest,
    CharacterCreationMagicResonanceState State);

public sealed record CharacterCreationMagicResonanceInteractionPreparedPreview(
    CharacterCreationMagicResonanceInteractionState State,
    CharacterCreationMagicResonanceReview Review,
    CharacterCreationMagicResonanceDesktopDraft Draft,
    int MysticAdeptPowerPoints)
{
    public string IdempotencyKey { get; } = Guid.NewGuid().ToString("N");
    public CharacterCreationMagicResonancePreview Preview => Review.Preview;
}

public sealed record CharacterCreationMagicResonanceInteractionLoadResult(
    string Outcome,
    CharacterCreationMagicResonanceInteractionState? State,
    string? Error)
{
    public static CharacterCreationMagicResonanceInteractionLoadResult Ok(
        CharacterCreationMagicResonanceInteractionState state) =>
        new("ok", state, null);

    public static CharacterCreationMagicResonanceInteractionLoadResult Fail(string outcome, string error) =>
        new(outcome, null, error);
}

public sealed record CharacterCreationMagicResonanceInteractionPrepareResult(
    string Outcome,
    CharacterCreationMagicResonanceInteractionPreparedPreview? Preview,
    string? Error)
{
    public static CharacterCreationMagicResonanceInteractionPrepareResult Ok(
        CharacterCreationMagicResonanceInteractionPreparedPreview preview) =>
        new("ok", preview, null);

    public static CharacterCreationMagicResonanceInteractionPrepareResult Fail(string outcome, string error) =>
        new(outcome, null, error);
}

public sealed record CharacterCreationMagicResonanceInteractionConfirmResult(
    string Outcome,
    CharacterCreationMagicResonanceReceipt? Receipt,
    bool IsIdempotentReplay,
    string? Error)
{
    public static CharacterCreationMagicResonanceInteractionConfirmResult Ok(
        CharacterCreationMagicResonanceReceipt receipt,
        bool isIdempotentReplay) =>
        new("ok", receipt, isIdempotentReplay, null);

    public static CharacterCreationMagicResonanceInteractionConfirmResult Fail(string outcome, string error) =>
        new(outcome, null, false, error);
}
