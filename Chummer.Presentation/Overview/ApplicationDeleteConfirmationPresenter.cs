using Chummer.Application.Tools;
using Chummer.Contracts.Api;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Revision-safe presentation boundary for Chummer5 application settings, including the two
/// independent index-visibility and selection-behavior pairs.
/// UI drafts remain local until Apply is called by the explicit Save action.
/// </summary>
public sealed class ApplicationDeleteConfirmationPresenter
{
    private readonly IApplicationDeleteConfirmationStore _store;

    public ApplicationDeleteConfirmationPresenter(IApplicationDeleteConfirmationStore store)
    {
        _store = store;
    }

    public ApplicationDeleteConfirmationState Load()
        => _store.Load();

    public ApplicationDeleteConfirmationState Apply(ApplicationDeleteConfirmationMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ApplicationDeleteConfirmationState current = _store.Load();
        ApplicationDeleteConfirmationState updated = ApplicationDeleteConfirmationRules.Apply(current, mutation);
        if (updated.Revision == current.Revision)
            return current;

        _store.Save(mutation.ExpectedRevision, updated);
        return updated;
    }

    public ApplicationDeleteConfirmationState ApplySnapshot(ApplicationConfirmationSettingsMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ApplicationDeleteConfirmationState current = _store.Load();
        ApplicationDeleteConfirmationState updated = ApplicationDeleteConfirmationRules.ApplySnapshot(current, mutation);
        if (updated.Revision == current.Revision)
            return current;

        _store.Save(mutation.ExpectedRevision, updated);
        return updated;
    }

    public ApplicationDeleteConfirmationState ApplyDateTimeSnapshot(ApplicationDateTimeSettingsMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ApplicationDeleteConfirmationState current = _store.Load();
        ApplicationDeleteConfirmationState updated =
            ApplicationDeleteConfirmationRules.ApplyDateTimeSnapshot(current, mutation);
        if (updated.Revision == current.Revision)
            return current;

        _store.Save(mutation.ExpectedRevision, updated);
        return updated;
    }

    public ApplicationDeleteConfirmationState ApplySettingsSnapshot(ApplicationSettingsSnapshotMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ApplicationDeleteConfirmationState current = _store.Load();
        ApplicationDeleteConfirmationState updated =
            ApplicationDeleteConfirmationRules.ApplySettingsSnapshot(current, mutation);
        if (updated.Revision == current.Revision)
            return current;

        _store.Save(mutation.ExpectedRevision, updated);
        return updated;
    }
}
