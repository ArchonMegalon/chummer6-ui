using Chummer.Application.Tools;
using Chummer.Contracts.Api;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class ApplicationDeleteConfirmationPresenterTests
{
    [TestMethod]
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

    [TestMethod]
    public void Apply_fails_closed_without_save_when_draft_revision_is_stale()
    {
        RecordingStore store = new()
        {
            State = new ApplicationDeleteConfirmationState(4, ConfirmDelete: false)
        };
        ApplicationDeleteConfirmationPresenter presenter = new(store);

        Assert.ThrowsExactly<InvalidOperationException>(() => presenter.Apply(
            new ApplicationDeleteConfirmationMutation(
                ApplicationSettingIdentity.ConfirmDelete,
                Value: true,
                ExpectedRevision: 3)));
        Assert.AreEqual(0, store.SaveCount);
    }

    [TestMethod]
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

    [TestMethod]
    public void ApplyDateTimeSnapshot_is_the_only_date_time_persistence_boundary()
    {
        RecordingStore store = new();
        ApplicationDeleteConfirmationPresenter presenter = new(store);
        ApplicationDeleteConfirmationState draft = presenter.Load() with
        {
            CustomDateTimeFormats = true,
            CustomDateFormat = "yyyy-MM-dd",
            CustomTimeFormat = "HH:mm:ss",
            DatesIncludeTime = false
        };
        Assert.AreEqual(0, store.SaveCount, "Editing date/time UI drafts must not persist.");

        ApplicationDeleteConfirmationState result = presenter.ApplyDateTimeSnapshot(
            new ApplicationDateTimeSettingsMutation(
                new(ApplicationSettingIdentity.CustomDateTimeFormats, draft.CustomDateTimeFormats),
                new(ApplicationSettingIdentity.CustomDateFormat, draft.CustomDateFormat),
                new(ApplicationSettingIdentity.CustomTimeFormat, draft.CustomTimeFormat),
                new(ApplicationSettingIdentity.DatesIncludeTime, draft.DatesIncludeTime),
                ExpectedRevision: draft.Revision));

        Assert.AreEqual(1, result.Revision);
        Assert.IsTrue(result.CustomDateTimeFormats);
        Assert.AreEqual("yyyy-MM-dd", result.CustomDateFormat);
        Assert.AreEqual("HH:mm:ss", result.CustomTimeFormat);
        Assert.IsFalse(result.DatesIncludeTime);
        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual(0, store.LastExpectedRevision);
    }

    [TestMethod]
    public void ApplyDateTimeSnapshot_noop_does_not_write_and_stale_revision_fails_closed()
    {
        RecordingStore store = new()
        {
            State = new ApplicationDeleteConfirmationState(
                4,
                ConfirmDelete: true,
                ConfirmKarmaExpense: true,
                CustomDateTimeFormats: true,
                CustomDateFormat: "yyyy-MM-dd",
                CustomTimeFormat: "HH:mm:ss",
                DatesIncludeTime: false)
        };
        ApplicationDeleteConfirmationPresenter presenter = new(store);

        ApplicationDeleteConfirmationState unchanged = presenter.ApplyDateTimeSnapshot(
            DateTimeMutation(store.State, expectedRevision: 4));
        Assert.AreSame(store.State, unchanged);
        Assert.AreEqual(0, store.SaveCount);
        Assert.ThrowsExactly<InvalidOperationException>(() => presenter.ApplyDateTimeSnapshot(
            DateTimeMutation(store.State, expectedRevision: 3)));
        Assert.AreEqual(0, store.SaveCount);
    }

    [TestMethod]
    public void ApplySettingsSnapshot_commits_the_whole_page_once()
    {
        RecordingStore store = new();
        ApplicationDeleteConfirmationPresenter presenter = new(store);
        ApplicationDeleteConfirmationState result = presenter.ApplySettingsSnapshot(
            new ApplicationSettingsSnapshotMutation(
                ConfirmDelete: false,
                ConfirmKarmaExpense: false,
                CustomDateTimeFormats: new(ApplicationSettingIdentity.CustomDateTimeFormats, true),
                CustomDateFormat: new(ApplicationSettingIdentity.CustomDateFormat, "yyyy-MM-dd"),
                CustomTimeFormat: new(ApplicationSettingIdentity.CustomTimeFormat, "HH:mm:ss"),
                DatesIncludeTime: new(ApplicationSettingIdentity.DatesIncludeTime, false),
                HideMasterIndex: new(ApplicationSettingIdentity.HideMasterIndex, true),
                HideCharacterRoster: new(ApplicationSettingIdentity.HideCharacterRoster, true),
                SearchInCategoryOnly: new(ApplicationSettingIdentity.SearchInCategoryOnly, false),
                AllowEasterEggs: new(ApplicationSettingIdentity.AllowEasterEggs, true),
                ExpectedRevision: 0));

        Assert.AreEqual(1, result.Revision);
        Assert.IsFalse(result.ConfirmDelete);
        Assert.IsFalse(result.ConfirmKarmaExpense);
        Assert.IsTrue(result.CustomDateTimeFormats);
        Assert.AreEqual("yyyy-MM-dd", result.CustomDateFormat);
        Assert.AreEqual("HH:mm:ss", result.CustomTimeFormat);
        Assert.IsFalse(result.DatesIncludeTime);
        Assert.IsTrue(result.HideMasterIndex);
        Assert.IsTrue(result.HideCharacterRoster);
        Assert.IsFalse(result.SearchInCategoryOnly);
        Assert.IsTrue(result.AllowEasterEggs);
        Assert.AreEqual(1, store.SaveCount);
        Assert.AreEqual(0, store.LastExpectedRevision);
    }

    [TestMethod]
    public void ApplySettingsSnapshot_rejects_visibility_identity_mismatch_without_save()
    {
        RecordingStore store = new();
        ApplicationDeleteConfirmationPresenter presenter = new(store);
        ApplicationSettingsSnapshotMutation mutation = new(
            ConfirmDelete: true,
            ConfirmKarmaExpense: true,
            CustomDateTimeFormats: new(ApplicationSettingIdentity.CustomDateTimeFormats, false),
            CustomDateFormat: new(ApplicationSettingIdentity.CustomDateFormat, string.Empty),
            CustomTimeFormat: new(ApplicationSettingIdentity.CustomTimeFormat, string.Empty),
            DatesIncludeTime: new(ApplicationSettingIdentity.DatesIncludeTime, true),
            HideMasterIndex: new(ApplicationSettingIdentity.HideCharacterRoster, true),
            HideCharacterRoster: new(ApplicationSettingIdentity.HideCharacterRoster, true),
            SearchInCategoryOnly: new(ApplicationSettingIdentity.SearchInCategoryOnly, true),
            AllowEasterEggs: new(ApplicationSettingIdentity.AllowEasterEggs, false),
            ExpectedRevision: 0);

        Assert.ThrowsExactly<ArgumentException>(() => presenter.ApplySettingsSnapshot(mutation));
        Assert.AreEqual(0, store.SaveCount);
    }

    private static ApplicationDateTimeSettingsMutation DateTimeMutation(
        ApplicationDeleteConfirmationState state,
        long expectedRevision)
        => new(
            new(ApplicationSettingIdentity.CustomDateTimeFormats, state.CustomDateTimeFormats),
            new(ApplicationSettingIdentity.CustomDateFormat, state.CustomDateFormat),
            new(ApplicationSettingIdentity.CustomTimeFormat, state.CustomTimeFormat),
            new(ApplicationSettingIdentity.DatesIncludeTime, state.DatesIncludeTime),
            expectedRevision);

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
