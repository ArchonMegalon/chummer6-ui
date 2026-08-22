using Chummer.Application.Tools;
using Chummer.Contracts.Api;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestFixture]
public sealed class ApplicationDeleteConfirmationPresenterTests
{
    [Test]
    public void Apply_is_the_only_persistence_boundary_and_passes_expected_revision()
    {
        RecordingStore store = new();
        ApplicationDeleteConfirmationPresenter presenter = new(store);

        ApplicationDeleteConfirmationState draft = presenter.Load() with { ConfirmDelete = false };
        Assert.AreEqual(0, store.SaveCount, "Editing a UI draft must not persist.");

        ApplicationDeleteConfirmationState result = presenter.Apply(
            new ApplicationDeleteConfirmationMutation(
                ApplicationSettingIdentity.ConfirmDelete,
                draft.ConfirmDelete,
                ExpectedRevision: draft.Revision));

        Assert.AreEqual(1, result.Revision);
        Assert.IsFalse(result.ConfirmDelete);
        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual(0, store.LastExpectedRevision);
    }

    [Test]
    public void Apply_fails_closed_without_save_when_draft_revision_is_stale()
    {
        RecordingStore store = new()
        {
            State = new ApplicationDeleteConfirmationState(4, ConfirmDelete: false)
        };
        ApplicationDeleteConfirmationPresenter presenter = new(store);

        Assert.Throws<InvalidOperationException>(() => presenter.Apply(
            new ApplicationDeleteConfirmationMutation(
                ApplicationSettingIdentity.ConfirmDelete,
                Value: true,
                ExpectedRevision: 3)));
        Assert.AreEqual(0, store.SaveCount);
    }

    [Test]
    public void ApplySnapshot_persists_both_confirmation_drafts_in_one_transaction()
    {
        RecordingStore store = new();
        ApplicationDeleteConfirmationPresenter presenter = new(store);
        ApplicationDeleteConfirmationState draft = presenter.Load() with
        {
            ConfirmDelete = false,
            ConfirmKarmaExpense = false
        };
        Assert.AreEqual(0, store.SaveCount, "Editing either UI draft must not persist.");

        ApplicationDeleteConfirmationState result = presenter.ApplySnapshot(
            new ApplicationConfirmationSettingsMutation(
                draft.ConfirmDelete,
                draft.ConfirmKarmaExpense,
                ExpectedRevision: draft.Revision));

        Assert.AreEqual(1, result.Revision);
        Assert.IsFalse(result.ConfirmDelete);
        Assert.IsFalse(result.ConfirmKarmaExpense);
        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual(0, store.LastExpectedRevision);
    }

    private sealed class RecordingStore : IApplicationDeleteConfirmationStore
    {
        public ApplicationDeleteConfirmationState State { get; set; } = ApplicationDeleteConfirmationState.Default;
        public int SaveCount { get; private set; }
        public long LastExpectedRevision { get; private set; } = -1;

        public ApplicationDeleteConfirmationState Load() => State;

        public void Save(long expectedRevision, ApplicationDeleteConfirmationState state)
        {
            SaveCount++;
            LastExpectedRevision = expectedRevision;
            State = state;
        }
    }
}
