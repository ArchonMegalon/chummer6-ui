using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public static class CharacterCreationContactsInteractionBlockers
{
    public const string OverviewAuthorityRequired = "creation-contacts-overview-authority-required";
    public const string BindingMismatch = "creation-contacts-binding-mismatch";
    public const string PreparedPreviewMismatch = "creation-contacts-prepared-preview-mismatch";
    public const string PreviewNotConfirmable = "creation-contacts-preview-not-confirmable";
    public const string IdempotencyKeyMismatch = "creation-contacts-idempotency-key-mismatch";
    public const string ReceiptMismatch = "creation-contacts-receipt-mismatch";
    public const string RefreshAuthorityRequired = "creation-contacts-refresh-authority-required";
}

/// <summary>
/// Renderer-neutral boundary for the Contacts portion of the Contacts/Lifestyles
/// creation step. Presentation carries typed user intent and projects Core authority;
/// it never derives legal values, costs, budgets, XML writes, or receipt truth.
/// </summary>
public interface ICharacterCreationContactsInteractionPresenter
{
    CharacterCreationContactsInteractionLoadResult Load(CharacterOverviewState overview);

    CharacterCreationContactsInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationContactEditInput input);

    CharacterCreationContactsInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationContactConfirmation confirmation);

    CharacterCreationContactsInteractionReceiptLookupResult LookupReceipt(
        CharacterOverviewState overview,
        string idempotencyKey);
}

public sealed record CharacterCreationContactEditInput(
    Guid ContactId,
    CharacterCreationContactIdentity? Identity = null,
    int? Connection = null,
    int? Loyalty = null,
    bool? IsGroup = null,
    bool? Free = null,
    bool? Family = null,
    bool? Blackmail = null)
{
    internal CharacterCreationContactEdit ToCoreEdit()
        => new(
            ContactId,
            Identity,
            Connection,
            Loyalty,
            IsGroup,
            Free,
            Family,
            Blackmail);
}

public sealed record CharacterCreationContactsInteractionState(
    CharacterCreationContactBinding Binding,
    IReadOnlyList<CharacterCreationContactProjection> Contacts,
    CharacterCreationContactBudget ContactBudget,
    CharacterCreationContactBudget HighPlacesBudget,
    IReadOnlyList<string> Blockers,
    bool CanEdit,
    string SnapshotDigest);

public sealed record CharacterCreationContactPreparedPreview(
    string ContactsSnapshotDigest,
    CharacterCreationContactBinding Binding,
    CharacterCreationContactEdit Edit,
    IReadOnlyList<CharacterCreationContactProjection> ContactsBefore,
    CharacterCreationContactProjection ContactBefore,
    CharacterCreationContactProjection ContactAfter,
    CharacterCreationContactBudget ContactBudgetBefore,
    CharacterCreationContactBudget ContactBudgetAfter,
    CharacterCreationContactBudget HighPlacesBudgetBefore,
    CharacterCreationContactBudget HighPlacesBudgetAfter,
    CharacterCreationContactAtomicWritePlan WritePlan,
    IReadOnlyList<string> Blockers,
    bool RequiresExplicitConfirmation,
    bool CanConfirm,
    string IdempotencyKey,
    string PreviewDigest);

public sealed record CharacterCreationContactConfirmation(
    CharacterCreationContactPreparedPreview PreparedPreview,
    string PreviewDigest,
    string IdempotencyKey,
    bool ExplicitlyConfirmed);

public sealed record CharacterCreationContactsInteractionLoadResult(
    string Outcome,
    CharacterCreationContactsInteractionState? State,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationContactsInteractionPrepareResult(
    string Outcome,
    CharacterCreationContactsInteractionState? State,
    CharacterCreationContactPreparedPreview? PreparedPreview,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationContactsInteractionConfirmResult(
    string Outcome,
    CharacterCreationContactPreparedPreview? PreparedPreview,
    CharacterCreationContactReceipt? Receipt,
    CharacterCreationContactsInteractionState? RefreshedState,
    IReadOnlyList<string> Blockers);

public sealed record CharacterCreationContactsInteractionReceiptLookupResult(
    string Outcome,
    CharacterCreationContactReceipt? Receipt,
    CharacterCreationContactsInteractionState? CurrentState,
    IReadOnlyList<string> Blockers);

public sealed class CharacterCreationContactsInteractionPresenter
    : ICharacterCreationContactsInteractionPresenter
{
    private readonly ICharacterCreationContactsService _service;

    public CharacterCreationContactsInteractionPresenter(
        ICharacterCreationContactsService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public CharacterCreationContactsInteractionLoadResult Load(CharacterOverviewState overview)
    {
        ExactLoad load = LoadExact(overview);
        return new CharacterCreationContactsInteractionLoadResult(
            load.Outcome,
            load.State is null ? null : Project(load.State),
            load.Blockers);
    }

    public CharacterCreationContactsInteractionPrepareResult Prepare(
        CharacterOverviewState overview,
        CharacterCreationContactEditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationContactsState contacts)
        {
            return new CharacterCreationContactsInteractionPrepareResult(
                load.Outcome,
                null,
                null,
                load.Blockers);
        }

        CharacterCreationContactsInteractionState state = Project(contacts);
        if (!contacts.CanEdit || contacts.Blockers.Count != 0)
        {
            return new CharacterCreationContactsInteractionPrepareResult(
                CharacterCreationContactOutcomes.Blocked,
                state,
                null,
                NormalizeBlockers(load.Blockers.Concat(contacts.Blockers)));
        }
        if (input.ContactId == Guid.Empty)
        {
            return new CharacterCreationContactsInteractionPrepareResult(
                CharacterCreationContactOutcomes.Invalid,
                state,
                null,
                [CharacterCreationContactsBlockers.ContactInvalid]);
        }

        CharacterCreationContactEdit edit = input.ToCoreEdit();
        CharacterCreationContactResult<CharacterCreationContactPreview> result =
            _service.Preview(new CharacterCreationContactPreviewRequest(contacts.Binding, edit));
        if (result.Value is not CharacterCreationContactPreview preview)
        {
            return new CharacterCreationContactsInteractionPrepareResult(
                result.Outcome,
                state,
                null,
                NormalizeBlockers(result.Blockers));
        }

        if (!PreviewMatches(contacts, edit, preview))
        {
            return new CharacterCreationContactsInteractionPrepareResult(
                CharacterCreationContactOutcomes.Conflict,
                state,
                null,
                [CharacterCreationContactsInteractionBlockers.PreparedPreviewMismatch]);
        }

        CharacterCreationContactPreparedPreview prepared = Project(contacts, edit, preview);
        return new CharacterCreationContactsInteractionPrepareResult(
            result.Outcome,
            state,
            prepared,
            NormalizeBlockers(result.Blockers.Concat(preview.Blockers)));
    }

    public CharacterCreationContactsInteractionConfirmResult Confirm(
        CharacterOverviewState overview,
        CharacterCreationContactConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(confirmation.PreparedPreview);
        CharacterCreationContactPreparedPreview prepared = confirmation.PreparedPreview;
        if (!confirmation.ExplicitlyConfirmed)
        {
            return Failure(
                CharacterCreationContactOutcomes.Invalid,
                prepared,
                CharacterCreationContactsBlockers.ExplicitConfirmationRequired);
        }
        if (!string.Equals(confirmation.PreviewDigest, prepared.PreviewDigest, StringComparison.Ordinal)
            || !IsDigest(prepared.PreviewDigest))
        {
            return Failure(
                CharacterCreationContactOutcomes.Conflict,
                prepared,
                CharacterCreationContactsBlockers.PreviewDigestMismatch);
        }
        if (string.IsNullOrWhiteSpace(prepared.IdempotencyKey)
            || prepared.IdempotencyKey.Length > 200
            || !string.Equals(
                confirmation.IdempotencyKey,
                prepared.IdempotencyKey,
                StringComparison.Ordinal))
        {
            return Failure(
                CharacterCreationContactOutcomes.Conflict,
                prepared,
                CharacterCreationContactsInteractionBlockers.IdempotencyKeyMismatch);
        }

        ExactLoad load = LoadExact(overview);
        if (load.State is not CharacterCreationContactsState contacts)
        {
            return new CharacterCreationContactsInteractionConfirmResult(
                load.Outcome,
                prepared,
                null,
                null,
                load.Blockers);
        }
        if (!string.Equals(prepared.ContactsSnapshotDigest, contacts.SnapshotDigest, StringComparison.Ordinal)
            || !BindingEquals(prepared.Binding, contacts.Binding)
            || !PreparedContactStillMatches(contacts, prepared))
        {
            return Failure(
                CharacterCreationContactOutcomes.Conflict,
                prepared,
                BindingConflict(prepared.Binding, contacts.Binding));
        }
        if (!prepared.RequiresExplicitConfirmation
            || !prepared.CanConfirm
            || prepared.Blockers.Count != 0)
        {
            IReadOnlyList<string> blockers = prepared.Blockers.Count == 0
                ? [CharacterCreationContactsInteractionBlockers.PreviewNotConfirmable]
                : prepared.Blockers;
            return new CharacterCreationContactsInteractionConfirmResult(
                CharacterCreationContactOutcomes.Blocked,
                prepared,
                null,
                null,
                NormalizeBlockers(blockers));
        }

        CharacterCreationContactResult<CharacterCreationContactReceipt> result =
            _service.Confirm(new CharacterCreationContactConfirmRequest(
                prepared.Binding,
                prepared.Edit,
                prepared.PreviewDigest,
                confirmation.IdempotencyKey,
                ExplicitlyConfirmed: true));
        if (result.Value is not CharacterCreationContactReceipt receipt)
        {
            return new CharacterCreationContactsInteractionConfirmResult(
                result.Outcome,
                prepared,
                null,
                null,
                NormalizeBlockers(result.Blockers));
        }
        if (result.Outcome is not (CharacterCreationContactOutcomes.Applied
                or CharacterCreationContactOutcomes.Replayed)
            || !ReceiptMatches(prepared, receipt))
        {
            return new CharacterCreationContactsInteractionConfirmResult(
                CharacterCreationContactOutcomes.Conflict,
                prepared,
                receipt,
                null,
                [CharacterCreationContactsInteractionBlockers.ReceiptMismatch]);
        }

        CharacterCreationContactResult<CharacterCreationContactsState> refresh =
            _service.Load(new CharacterCreationContactsLoadRequest(receipt.WorkspaceId));
        if (refresh.Outcome != CharacterCreationContactOutcomes.Available
            || refresh.Value is not CharacterCreationContactsState refreshed
            || !RefreshedStateMatches(receipt, prepared, refreshed))
        {
            return new CharacterCreationContactsInteractionConfirmResult(
                CharacterCreationContactOutcomes.Conflict,
                prepared,
                receipt,
                null,
                NormalizeBlockers(refresh.Blockers.Append(
                    CharacterCreationContactsInteractionBlockers.RefreshAuthorityRequired)));
        }

        return new CharacterCreationContactsInteractionConfirmResult(
            result.Outcome,
            prepared,
            receipt,
            Project(refreshed),
            NormalizeBlockers(result.Blockers.Concat(refresh.Blockers)));
    }

    public CharacterCreationContactsInteractionReceiptLookupResult LookupReceipt(
        CharacterOverviewState overview,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(overview);
        if (overview.Profile?.Created == true
            || overview.CreationWizard?.CharacterCreated == true)
        {
            return new CharacterCreationContactsInteractionReceiptLookupResult(
                CharacterCreationContactOutcomes.Blocked,
                null,
                null,
                [CharacterCreationContactsBlockers.CareerModeRejected]);
        }
        if (overview.WorkspaceId is not { } workspaceId
            || overview.ActiveWorkspace is not { } activeWorkspace
            || !string.Equals(activeWorkspace.Id.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return new CharacterCreationContactsInteractionReceiptLookupResult(
                CharacterCreationContactOutcomes.Invalid,
                null,
                null,
                [CharacterCreationContactsInteractionBlockers.OverviewAuthorityRequired]);
        }

        CharacterCreationContactResult<CharacterCreationContactReceipt> lookup =
            _service.LookupReceipt(new CharacterCreationContactReceiptLookupRequest(
                workspaceId,
                idempotencyKey));
        if (lookup.Value is not CharacterCreationContactReceipt receipt)
        {
            return new CharacterCreationContactsInteractionReceiptLookupResult(
                lookup.Outcome,
                null,
                null,
                NormalizeBlockers(lookup.Blockers));
        }

        CharacterCreationContactResult<CharacterCreationContactsState> current =
            _service.Load(new CharacterCreationContactsLoadRequest(workspaceId));
        if (current.Outcome != CharacterCreationContactOutcomes.Available
            || current.Value is not CharacterCreationContactsState state
            || !ReceiptCanBelongToCurrentState(receipt, state))
        {
            return new CharacterCreationContactsInteractionReceiptLookupResult(
                CharacterCreationContactOutcomes.Conflict,
                receipt,
                null,
                NormalizeBlockers(current.Blockers.Append(
                    CharacterCreationContactsInteractionBlockers.ReceiptMismatch)));
        }

        return new CharacterCreationContactsInteractionReceiptLookupResult(
            lookup.Outcome,
            receipt,
            Project(state),
            NormalizeBlockers(lookup.Blockers.Concat(current.Blockers)));
    }

    private ExactLoad LoadExact(CharacterOverviewState overview)
    {
        ArgumentNullException.ThrowIfNull(overview);
        if (overview.Profile?.Created == true
            || overview.CreationWizard?.CharacterCreated == true
            || overview.CreationContacts?.CharacterCreated == true)
        {
            return ExactLoad.Failure(
                CharacterCreationContactOutcomes.Blocked,
                CharacterCreationContactsBlockers.CareerModeRejected);
        }
        if (overview.WorkspaceId is not { } workspaceId
            || overview.ActiveWorkspace is not { } activeWorkspace
            || overview.Profile is null
            || overview.CreationWizard is not { } wizard
            || overview.CreationContacts is not { } projectedContacts
            || !string.Equals(activeWorkspace.Id.Value, workspaceId.Value, StringComparison.Ordinal))
        {
            return ExactLoad.Failure(
                CharacterCreationContactOutcomes.Invalid,
                CharacterCreationContactsInteractionBlockers.OverviewAuthorityRequired);
        }

        CharacterCreationContactResult<CharacterCreationContactsState> result =
            _service.Load(new CharacterCreationContactsLoadRequest(workspaceId));
        if (result.Outcome != CharacterCreationContactOutcomes.Available
            || result.Value is not CharacterCreationContactsState contacts)
        {
            return new ExactLoad(result.Outcome, null, NormalizeBlockers(result.Blockers));
        }
        if (contacts.CharacterCreated)
        {
            return ExactLoad.Failure(
                CharacterCreationContactOutcomes.Blocked,
                CharacterCreationContactsBlockers.CareerModeRejected);
        }
        if (!MatchesOverview(
                workspaceId,
                activeWorkspace.ContentRevision,
                activeWorkspace.SavedRevision,
                wizard,
                projectedContacts,
                contacts))
        {
            return ExactLoad.Failure(
                CharacterCreationContactOutcomes.Conflict,
                CharacterCreationContactsInteractionBlockers.BindingMismatch);
        }

        return new ExactLoad(
            CharacterCreationContactOutcomes.Available,
            contacts,
            NormalizeBlockers(result.Blockers.Concat(contacts.Blockers)));
    }

    private static bool MatchesOverview(
        Chummer.Contracts.Workspaces.CharacterWorkspaceId workspaceId,
        long contentRevision,
        long savedRevision,
        CharacterCreationWizardSnapshot wizard,
        CharacterCreationContactsState projected,
        CharacterCreationContactsState loaded)
        => string.Equals(workspaceId.Value, loaded.Binding.WorkspaceId.Value, StringComparison.Ordinal)
           && contentRevision == loaded.Binding.ContentRevision
           && savedRevision == loaded.Binding.SavedRevision
           && loaded.Binding.WorkspaceRevision == loaded.Binding.ContentRevision
           && string.Equals(wizard.WorkspaceId, workspaceId.Value, StringComparison.Ordinal)
           && wizard.WorkspaceRevision == loaded.Binding.WorkspaceRevision
           && string.Equals(wizard.ContentDigest, loaded.Binding.ContentDigest, StringComparison.Ordinal)
           && !wizard.CharacterCreated
           && StateAuthorityShapeIsValid(projected)
           && StateAuthorityShapeIsValid(loaded)
           && BindingEquals(projected.Binding, loaded.Binding)
           && string.Equals(projected.SnapshotDigest, loaded.SnapshotDigest, StringComparison.Ordinal)
           && projected.Blockers.SequenceEqual(loaded.Blockers, StringComparer.Ordinal)
           && projected.CanEdit == loaded.CanEdit
           && projected.Contacts.Select(static contact => contact.ContactDigest)
               .SequenceEqual(loaded.Contacts.Select(static contact => contact.ContactDigest), StringComparer.Ordinal)
           && BudgetEquals(projected.ContactBudget, loaded.ContactBudget)
           && BudgetEquals(projected.HighPlacesBudget, loaded.HighPlacesBudget);

    private static CharacterCreationContactsInteractionState Project(CharacterCreationContactsState state)
        => new(
            state.Binding,
            state.Contacts,
            state.ContactBudget,
            state.HighPlacesBudget,
            state.Blockers,
            state.CanEdit,
            state.SnapshotDigest);

    private static CharacterCreationContactPreparedPreview Project(
        CharacterCreationContactsState state,
        CharacterCreationContactEdit edit,
        CharacterCreationContactPreview preview)
        => new(
            state.SnapshotDigest,
            preview.Binding,
            edit,
            state.Contacts.ToArray(),
            preview.ContactBefore,
            preview.ContactAfter,
            preview.ContactBudgetBefore,
            preview.ContactBudgetAfter,
            preview.HighPlacesBudgetBefore,
            preview.HighPlacesBudgetAfter,
            preview.WritePlan,
            preview.Blockers,
            preview.RequiresExplicitConfirmation,
            preview.CanConfirm,
            "creation-contact-" + Guid.NewGuid().ToString("N"),
            preview.PreviewDigest);

    private static bool PreviewMatches(
        CharacterCreationContactsState state,
        CharacterCreationContactEdit edit,
        CharacterCreationContactPreview preview)
        => string.Equals(preview.Schema, CharacterCreationContactsSchemas.PreviewV1, StringComparison.Ordinal)
           && string.Equals(preview.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
           && BindingEquals(preview.Binding, state.Binding)
           && preview.ContactBefore.ContactId == edit.ContactId
           && preview.ContactAfter.ContactId == edit.ContactId
           && CharacterCreationWizardProjector.ContactProjectionShapeIsValid(preview.ContactBefore)
           && CharacterCreationWizardProjector.ContactProjectionShapeIsValid(preview.ContactAfter)
           && state.Contacts.Any(contact => ContactProjectionEquals(contact, preview.ContactBefore))
           && PlanMatchesPreview(preview)
           && CharacterCreationWizardProjector.ContactBudgetShapeIsValid(preview.ContactBudgetBefore)
           && CharacterCreationWizardProjector.ContactBudgetShapeIsValid(preview.ContactBudgetAfter)
           && CharacterCreationWizardProjector.ContactBudgetShapeIsValid(preview.HighPlacesBudgetBefore)
           && CharacterCreationWizardProjector.ContactBudgetShapeIsValid(preview.HighPlacesBudgetAfter)
           && BudgetEquals(preview.ContactBudgetBefore, state.ContactBudget)
           && BudgetEquals(preview.HighPlacesBudgetBefore, state.HighPlacesBudget)
           && preview.RequiresExplicitConfirmation
           && preview.CanConfirm == (preview.Blockers.Count == 0)
           && preview.Blockers.SequenceEqual(
               preview.Blockers.Distinct(StringComparer.Ordinal)
                   .OrderBy(static blocker => blocker, StringComparer.Ordinal),
               StringComparer.Ordinal)
           && IsDigest(preview.PreviewDigest);

    private static bool PreparedContactStillMatches(
        CharacterCreationContactsState state,
        CharacterCreationContactPreparedPreview prepared)
        => ContactProjectionSetsEqual(state.Contacts, prepared.ContactsBefore)
           && state.Contacts.Any(contact => ContactProjectionEquals(contact, prepared.ContactBefore))
           && BudgetEquals(state.ContactBudget, prepared.ContactBudgetBefore)
           && BudgetEquals(state.HighPlacesBudget, prepared.HighPlacesBudgetBefore);

    private static bool PlanMatchesPreview(CharacterCreationContactPreview preview)
        => string.Equals(preview.WritePlan.Schema, CharacterCreationContactsSchemas.WritePlanV1, StringComparison.Ordinal)
           && string.Equals(preview.WritePlan.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
           && preview.WritePlan.ContactId == preview.ContactBefore.ContactId
           && string.Equals(preview.WritePlan.ContentDigestBefore, preview.Binding.ContentDigest, StringComparison.Ordinal)
           && IsDigest(preview.WritePlan.ContentDigestAfter)
           && IsDigest(preview.WritePlan.UntouchedSiblingDigestBefore)
           && IsDigest(preview.WritePlan.UntouchedSiblingDigestAfter)
           && IsDigest(preview.WritePlan.NestedStateDigestBefore)
           && IsDigest(preview.WritePlan.NestedStateDigestAfter)
           && (!preview.CanConfirm || preview.WritePlan.PreservesUntouchedSiblingState)
           && (!preview.CanConfirm || preview.WritePlan.PreservesNestedState)
           && preview.WritePlan.Operations.Select(static operation => operation.Order)
               .SequenceEqual(Enumerable.Range(1, preview.WritePlan.Operations.Count))
           && preview.WritePlan.Operations.Select(static operation => operation.FieldId)
               .Distinct(StringComparer.Ordinal).Count() == preview.WritePlan.Operations.Count
           && PlanOperationsMatchContacts(preview)
           && IsDigest(preview.WritePlan.PlanDigest);

    private static bool ReceiptMatches(
        CharacterCreationContactPreparedPreview prepared,
        CharacterCreationContactReceipt receipt)
        => string.Equals(receipt.Schema, CharacterCreationContactsSchemas.ReceiptV1, StringComparison.Ordinal)
           && string.Equals(receipt.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(receipt.ReceiptId)
           && string.Equals(receipt.WorkspaceId.Value, prepared.Binding.WorkspaceId.Value, StringComparison.Ordinal)
           && receipt.ContactId == prepared.Edit.ContactId
           && receipt.PreviousWorkspaceRevision == prepared.Binding.WorkspaceRevision
           && receipt.WorkspaceRevision == receipt.PreviousWorkspaceRevision + 1
           && receipt.PreviousContentRevision == prepared.Binding.ContentRevision
           && receipt.ContentRevision == receipt.PreviousContentRevision + 1
           && receipt.PreviousSavedRevision == prepared.Binding.SavedRevision
           && receipt.SavedRevision == receipt.ContentRevision
           && string.Equals(receipt.ContentDigestBefore, prepared.Binding.ContentDigest, StringComparison.Ordinal)
           && string.Equals(receipt.ContentDigestAfter, prepared.WritePlan.ContentDigestAfter, StringComparison.Ordinal)
           && string.Equals(receipt.SourceDigest, prepared.Binding.SourceDigest, StringComparison.Ordinal)
           && string.Equals(receipt.RulesDigest, prepared.Binding.RulesDigest, StringComparison.Ordinal)
           && string.Equals(receipt.RuntimeDigest, prepared.Binding.RuntimeDigest, StringComparison.Ordinal)
           && receipt.ContactPointsBefore == prepared.ContactBudgetBefore.Used
           && receipt.ContactPointsAfter == prepared.ContactBudgetAfter.Used
           && receipt.ContactPointsRemaining == prepared.ContactBudgetAfter.Remaining
           && receipt.HighPlacesPointsBefore == prepared.HighPlacesBudgetBefore.Used
           && receipt.HighPlacesPointsAfter == prepared.HighPlacesBudgetAfter.Used
           && receipt.HighPlacesPointsRemaining == prepared.HighPlacesBudgetAfter.Remaining
           && WritePlanEquals(receipt.WritePlan, prepared.WritePlan)
           && IsDigest(receipt.IdempotencyKeyDigest)
           && IsDigest(receipt.CommandDigest)
           && IsDigest(receipt.ReceiptDigest);

    private static bool RefreshedStateMatches(
        CharacterCreationContactReceipt receipt,
        CharacterCreationContactPreparedPreview prepared,
        CharacterCreationContactsState refreshed)
        => StateAuthorityShapeIsValid(refreshed)
           && !refreshed.CharacterCreated
           && string.Equals(refreshed.Binding.WorkspaceId.Value, receipt.WorkspaceId.Value, StringComparison.Ordinal)
           && refreshed.Binding.WorkspaceRevision == receipt.WorkspaceRevision
           && refreshed.Binding.ContentRevision == receipt.ContentRevision
           && refreshed.Binding.SavedRevision == receipt.SavedRevision
           && string.Equals(refreshed.Binding.ContentDigest, receipt.ContentDigestAfter, StringComparison.Ordinal)
           && string.Equals(refreshed.Binding.SourceDigest, receipt.SourceDigest, StringComparison.Ordinal)
           && string.Equals(refreshed.Binding.RulesDigest, receipt.RulesDigest, StringComparison.Ordinal)
           && string.Equals(refreshed.Binding.RuntimeDigest, receipt.RuntimeDigest, StringComparison.Ordinal)
           && refreshed.Contacts.Any(contact => ContactProjectionEquals(contact, prepared.ContactAfter))
           && SiblingsPreserved(prepared, refreshed)
           && BudgetEquals(refreshed.ContactBudget, prepared.ContactBudgetAfter)
           && BudgetEquals(refreshed.HighPlacesBudget, prepared.HighPlacesBudgetAfter);

    private static bool ReceiptCanBelongToCurrentState(
        CharacterCreationContactReceipt receipt,
        CharacterCreationContactsState current)
        => StateAuthorityShapeIsValid(current)
           && string.Equals(receipt.Schema, CharacterCreationContactsSchemas.ReceiptV1, StringComparison.Ordinal)
           && string.Equals(receipt.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
           && !string.IsNullOrWhiteSpace(receipt.ReceiptId)
           && string.Equals(receipt.WorkspaceId.Value, current.Binding.WorkspaceId.Value, StringComparison.Ordinal)
           && receipt.ContactId != Guid.Empty
           && receipt.PreviousWorkspaceRevision > 0
           && receipt.WorkspaceRevision == receipt.PreviousWorkspaceRevision + 1
           && receipt.ContentRevision == receipt.PreviousContentRevision + 1
           && receipt.SavedRevision == receipt.ContentRevision
           && receipt.WorkspaceRevision == current.Binding.WorkspaceRevision
           && receipt.ContentRevision == current.Binding.ContentRevision
           && receipt.SavedRevision == current.Binding.SavedRevision
           && string.Equals(receipt.ContentDigestAfter, current.Binding.ContentDigest, StringComparison.Ordinal)
           && string.Equals(receipt.SourceDigest, current.Binding.SourceDigest, StringComparison.Ordinal)
           && string.Equals(receipt.RulesDigest, current.Binding.RulesDigest, StringComparison.Ordinal)
           && string.Equals(receipt.RuntimeDigest, current.Binding.RuntimeDigest, StringComparison.Ordinal)
           && receipt.ContactPointsBefore >= 0
           && receipt.ContactPointsAfter == current.ContactBudget.Used
           && receipt.ContactPointsRemaining == current.ContactBudget.Remaining
           && receipt.HighPlacesPointsBefore >= 0
           && receipt.HighPlacesPointsAfter == current.HighPlacesBudget.Used
           && receipt.HighPlacesPointsRemaining == current.HighPlacesBudget.Remaining
           && ReceiptWritePlanMatchesCurrent(receipt, current)
           && IsDigest(receipt.IdempotencyKeyDigest)
           && IsDigest(receipt.CommandDigest)
           && IsDigest(receipt.ReceiptDigest);

    private static bool ReceiptWritePlanMatchesCurrent(
        CharacterCreationContactReceipt receipt,
        CharacterCreationContactsState current)
    {
        CharacterCreationContactAtomicWritePlan plan = receipt.WritePlan;
        CharacterCreationContactProjection? contact = current.Contacts.SingleOrDefault(candidate =>
            candidate.ContactId == receipt.ContactId);
        if (contact is null
            || !string.Equals(plan.Schema, CharacterCreationContactsSchemas.WritePlanV1, StringComparison.Ordinal)
            || !string.Equals(plan.StepId, CharacterCreationWizardStepIds.ContactsLifestyles, StringComparison.Ordinal)
            || plan.ContactId != receipt.ContactId
            || !string.Equals(plan.ContentDigestBefore, receipt.ContentDigestBefore, StringComparison.Ordinal)
            || !string.Equals(plan.ContentDigestAfter, receipt.ContentDigestAfter, StringComparison.Ordinal)
            || !IsDigest(plan.ContentDigestBefore)
            || !IsDigest(plan.ContentDigestAfter)
            || !IsDigest(plan.UntouchedSiblingDigestBefore)
            || !IsDigest(plan.UntouchedSiblingDigestAfter)
            || !IsDigest(plan.NestedStateDigestBefore)
            || !IsDigest(plan.NestedStateDigestAfter)
            || !IsDigest(plan.PlanDigest)
            || !plan.PreservesUntouchedSiblingState
            || !plan.PreservesNestedState
            || plan.Operations.Count == 0
            || !plan.Operations.Select(static operation => operation.Order)
                .SequenceEqual(Enumerable.Range(1, plan.Operations.Count))
            || plan.Operations.Select(static operation => operation.FieldId)
                .Distinct(StringComparer.Ordinal).Count() != plan.Operations.Count)
        {
            return false;
        }

        Dictionary<string, CharacterCreationContactFieldAuthority> fields =
            contact.Fields.ToDictionary(static field => field.FieldId, StringComparer.Ordinal);
        return plan.Operations.All(operation =>
            CharacterCreationContactFieldIds.All.Contains(operation.FieldId, StringComparer.Ordinal)
            && !string.Equals(operation.BeforeValue, operation.AfterValue, StringComparison.Ordinal)
            && string.Equals(operation.AfterValue, fields[operation.FieldId].SerializedValue, StringComparison.Ordinal)
            && operation.SourceAnchorIds.SequenceEqual(
                CharacterCreationContactSourceAnchors.All,
                StringComparer.Ordinal));
    }

    private static bool StateAuthorityShapeIsValid(CharacterCreationContactsState state)
        => CharacterCreationWizardProjector.ContactAuthorityShapeIsValid(state);

    private static bool BudgetEquals(
        CharacterCreationContactBudget left,
        CharacterCreationContactBudget right)
        => string.Equals(left.BudgetId, right.BudgetId, StringComparison.Ordinal)
           && left.Total == right.Total
           && left.Used == right.Used
           && left.Remaining == right.Remaining
           && left.Overspend == right.Overspend
           && left.IsExact == right.IsExact
           && left.Blockers.SequenceEqual(right.Blockers, StringComparer.Ordinal)
           && left.SourceAnchorIds.SequenceEqual(right.SourceAnchorIds, StringComparer.Ordinal);

    private static bool ContactProjectionSetsEqual(
        IReadOnlyList<CharacterCreationContactProjection> contacts,
        IReadOnlyList<CharacterCreationContactProjection> expected)
        => contacts.Count == expected.Count
           && contacts.OrderBy(static contact => contact.ContactId)
               .Zip(expected.OrderBy(static contact => contact.ContactId))
               .All(static pair => ContactProjectionEquals(pair.First, pair.Second));

    private static bool SiblingsPreserved(
        CharacterCreationContactPreparedPreview prepared,
        CharacterCreationContactsState refreshed)
    {
        if (refreshed.Contacts.Count != prepared.ContactsBefore.Count)
            return false;
        Dictionary<Guid, CharacterCreationContactProjection> after = refreshed.Contacts.ToDictionary(
            static contact => contact.ContactId,
            static contact => contact);
        foreach (CharacterCreationContactProjection before in prepared.ContactsBefore)
        {
            if (before.ContactId == prepared.Edit.ContactId)
                continue;
            if (!after.TryGetValue(before.ContactId, out CharacterCreationContactProjection? current)
                || !ContactProjectionEquals(current, before))
            {
                return false;
            }
        }
        return true;
    }

    private static bool PlanOperationsMatchContacts(CharacterCreationContactPreview preview)
    {
        Dictionary<string, CharacterCreationContactFieldAuthority> before =
            preview.ContactBefore.Fields.ToDictionary(static field => field.FieldId, StringComparer.Ordinal);
        Dictionary<string, CharacterCreationContactFieldAuthority> after =
            preview.ContactAfter.Fields.ToDictionary(static field => field.FieldId, StringComparer.Ordinal);
        string[] changedFieldIds = CharacterCreationContactFieldIds.All
            .Where(fieldId => !string.Equals(
                before[fieldId].SerializedValue,
                after[fieldId].SerializedValue,
                StringComparison.Ordinal))
            .ToArray();
        if (!preview.WritePlan.Operations.Select(static operation => operation.FieldId)
            .SequenceEqual(changedFieldIds, StringComparer.Ordinal))
        {
            return false;
        }

        return preview.WritePlan.Operations.All(operation =>
            CharacterCreationContactFieldIds.All.Contains(operation.FieldId, StringComparer.Ordinal)
            && string.Equals(operation.BeforeValue, before[operation.FieldId].SerializedValue, StringComparison.Ordinal)
            && string.Equals(operation.AfterValue, after[operation.FieldId].SerializedValue, StringComparison.Ordinal)
            && !string.Equals(operation.BeforeValue, operation.AfterValue, StringComparison.Ordinal)
            && operation.SourceAnchorIds.SequenceEqual(
                CharacterCreationContactSourceAnchors.All,
                StringComparer.Ordinal));
    }

    private static bool ContactProjectionEquals(
        CharacterCreationContactProjection left,
        CharacterCreationContactProjection right)
        => left.ContactId == right.ContactId
           && left.Identity == right.Identity
           && left.Connection == right.Connection
           && left.Loyalty == right.Loyalty
           && left.IsGroup == right.IsGroup
           && left.Free == right.Free
           && left.Family == right.Family
           && left.Blackmail == right.Blackmail
           && left.ContactPointCost == right.ContactPointCost
           && left.CountsAgainstContactBudget == right.CountsAgainstContactBudget
           && left.CountsAgainstHighPlacesBudget == right.CountsAgainstHighPlacesBudget
           && string.Equals(left.ContactDigest, right.ContactDigest, StringComparison.Ordinal)
           && left.SourceAnchorIds.SequenceEqual(right.SourceAnchorIds, StringComparer.Ordinal)
           && left.Fields.Count == right.Fields.Count
           && left.Fields.Zip(right.Fields).All(static pair =>
               ContactFieldEquals(pair.First, pair.Second));

    private static bool ContactFieldEquals(
        CharacterCreationContactFieldAuthority left,
        CharacterCreationContactFieldAuthority right)
        => string.Equals(left.FieldId, right.FieldId, StringComparison.Ordinal)
           && string.Equals(left.Label, right.Label, StringComparison.Ordinal)
           && string.Equals(left.ValueKind, right.ValueKind, StringComparison.Ordinal)
           && left.IsEditable == right.IsEditable
           && string.Equals(left.SerializedValue, right.SerializedValue, StringComparison.Ordinal)
           && left.Minimum == right.Minimum
           && left.Maximum == right.Maximum
           && left.Blockers.SequenceEqual(right.Blockers, StringComparer.Ordinal)
           && left.SourceAnchorIds.SequenceEqual(right.SourceAnchorIds, StringComparer.Ordinal)
           && left.LegalOptions.Count == right.LegalOptions.Count
           && left.LegalOptions.Zip(right.LegalOptions).All(static pair =>
               ContactOptionEquals(pair.First, pair.Second));

    private static bool ContactOptionEquals(
        CharacterCreationContactOption left,
        CharacterCreationContactOption right)
        => string.Equals(left.OptionId, right.OptionId, StringComparison.Ordinal)
           && string.Equals(left.Label, right.Label, StringComparison.Ordinal)
           && string.Equals(left.SerializedValue, right.SerializedValue, StringComparison.Ordinal)
           && left.IsEnabled == right.IsEnabled
           && left.Blockers.SequenceEqual(right.Blockers, StringComparer.Ordinal)
           && left.SourceAnchorIds.SequenceEqual(right.SourceAnchorIds, StringComparer.Ordinal);

    private static bool WritePlanEquals(
        CharacterCreationContactAtomicWritePlan left,
        CharacterCreationContactAtomicWritePlan right)
        => string.Equals(left.Schema, right.Schema, StringComparison.Ordinal)
           && string.Equals(left.StepId, right.StepId, StringComparison.Ordinal)
           && left.ContactId == right.ContactId
           && string.Equals(left.ContentDigestBefore, right.ContentDigestBefore, StringComparison.Ordinal)
           && string.Equals(left.ContentDigestAfter, right.ContentDigestAfter, StringComparison.Ordinal)
           && string.Equals(left.UntouchedSiblingDigestBefore, right.UntouchedSiblingDigestBefore, StringComparison.Ordinal)
           && string.Equals(left.UntouchedSiblingDigestAfter, right.UntouchedSiblingDigestAfter, StringComparison.Ordinal)
           && string.Equals(left.NestedStateDigestBefore, right.NestedStateDigestBefore, StringComparison.Ordinal)
           && string.Equals(left.NestedStateDigestAfter, right.NestedStateDigestAfter, StringComparison.Ordinal)
           && left.PreservesUntouchedSiblingState == right.PreservesUntouchedSiblingState
           && left.PreservesNestedState == right.PreservesNestedState
           && string.Equals(left.PlanDigest, right.PlanDigest, StringComparison.Ordinal)
           && left.Operations.Count == right.Operations.Count
           && left.Operations.Zip(right.Operations).All(static pair =>
               pair.First.Order == pair.Second.Order
               && string.Equals(pair.First.FieldId, pair.Second.FieldId, StringComparison.Ordinal)
               && string.Equals(pair.First.BeforeValue, pair.Second.BeforeValue, StringComparison.Ordinal)
               && string.Equals(pair.First.AfterValue, pair.Second.AfterValue, StringComparison.Ordinal)
               && pair.First.SourceAnchorIds.SequenceEqual(
                   pair.Second.SourceAnchorIds,
                   StringComparer.Ordinal));

    private static bool BindingEquals(
        CharacterCreationContactBinding left,
        CharacterCreationContactBinding right)
        => string.Equals(left.WorkspaceId.Value, right.WorkspaceId.Value, StringComparison.Ordinal)
           && left.WorkspaceRevision == right.WorkspaceRevision
           && left.ContentRevision == right.ContentRevision
           && left.SavedRevision == right.SavedRevision
           && string.Equals(left.ContentDigest, right.ContentDigest, StringComparison.Ordinal)
           && string.Equals(left.AuxiliaryStateDigest, right.AuxiliaryStateDigest, StringComparison.Ordinal)
           && string.Equals(left.SourceDigest, right.SourceDigest, StringComparison.Ordinal)
           && string.Equals(left.RulesDigest, right.RulesDigest, StringComparison.Ordinal)
           && string.Equals(left.RuntimeDigest, right.RuntimeDigest, StringComparison.Ordinal);

    private static string BindingConflict(
        CharacterCreationContactBinding prepared,
        CharacterCreationContactBinding current)
    {
        if (prepared.WorkspaceRevision != current.WorkspaceRevision
            || prepared.ContentRevision != current.ContentRevision
            || prepared.SavedRevision != current.SavedRevision)
        {
            return CharacterCreationContactsBlockers.StaleWorkspaceRevision;
        }
        if (!string.Equals(prepared.ContentDigest, current.ContentDigest, StringComparison.Ordinal))
            return CharacterCreationContactsBlockers.StaleContentDigest;
        if (!string.Equals(prepared.AuxiliaryStateDigest, current.AuxiliaryStateDigest, StringComparison.Ordinal))
            return CharacterCreationContactsBlockers.StaleAuxiliaryStateDigest;
        if (!string.Equals(prepared.SourceDigest, current.SourceDigest, StringComparison.Ordinal))
            return CharacterCreationContactsBlockers.StaleSourceDigest;
        if (!string.Equals(prepared.RulesDigest, current.RulesDigest, StringComparison.Ordinal))
            return CharacterCreationContactsBlockers.StaleRulesDigest;
        if (!string.Equals(prepared.RuntimeDigest, current.RuntimeDigest, StringComparison.Ordinal))
            return CharacterCreationContactsBlockers.StaleRuntimeDigest;
        return CharacterCreationContactsInteractionBlockers.PreparedPreviewMismatch;
    }

    private static bool IsDigest(string? value)
        => value is { Length: 71 }
           && value.StartsWith("sha256:", StringComparison.Ordinal)
           && value.AsSpan(7).ToString().All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string[] NormalizeBlockers(IEnumerable<string> blockers)
        => blockers
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToArray();

    private static CharacterCreationContactsInteractionConfirmResult Failure(
        string outcome,
        CharacterCreationContactPreparedPreview prepared,
        string blocker)
        => new(outcome, prepared, null, null, [blocker]);

    private sealed record ExactLoad(
        string Outcome,
        CharacterCreationContactsState? State,
        IReadOnlyList<string> Blockers)
    {
        public static ExactLoad Failure(string outcome, params string[] blockers)
            => new(outcome, null, blockers);
    }
}
