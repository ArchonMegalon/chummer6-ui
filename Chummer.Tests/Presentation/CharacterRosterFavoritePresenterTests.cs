using Chummer.Application.Tools;
using Chummer.Contracts.Api;
using Chummer.Contracts.Owners;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestFixture]
public sealed class CharacterRosterFavoritePresenterTests
{
    [Test]
    public void Apply_passes_expected_revision_to_atomic_store()
    {
        RecordingStore store = new();
        CharacterRosterFavoritePresenter presenter = new(store);

        CharacterRosterFavoriteState result = presenter.Apply(new CharacterRosterFavoriteMutation(
            new CharacterRosterDocumentIdentity("content://runner/alpha", "Alpha"),
            IsFavorite: true,
            ExpectedRevision: 0));

        Assert.AreEqual(1, result.Revision);
        Assert.AreEqual(0, store.LastExpectedRevision);
        Assert.AreEqual("content://runner/alpha", store.State.Favorites.Single().Locator);
    }

    [Test]
    public void Apply_fails_closed_when_editor_revision_is_stale()
    {
        RecordingStore store = new()
        {
            State = new CharacterRosterFavoriteState(
                3,
                [new CharacterRosterDocumentIdentity("content://runner/existing", "Existing")],
                [])
        };
        CharacterRosterFavoritePresenter presenter = new(store);

        Assert.Throws<InvalidOperationException>(() => presenter.Apply(new CharacterRosterFavoriteMutation(
            new CharacterRosterDocumentIdentity("content://runner/alpha", "Alpha"),
            IsFavorite: true,
            ExpectedRevision: 2)));
        Assert.AreEqual(0, store.SaveCount);
    }

    [Test]
    public void ApplySort_persists_selected_collection_with_expected_revision()
    {
        RecordingStore store = new()
        {
            State = new CharacterRosterFavoriteState(
                4,
                [
                    new CharacterRosterDocumentIdentity("content://runner/zed", "Alpha display"),
                    new CharacterRosterDocumentIdentity("content://runner/alpha", "Zulu display")
                ],
                [new CharacterRosterDocumentIdentity("content://runner/recent", "Recent")])
        };
        CharacterRosterFavoritePresenter presenter = new(store);

        CharacterRosterFavoriteState result = presenter.ApplySort(new CharacterRosterSortMutation(
            CharacterRosterSortTarget.Favorites,
            ExpectedRevision: 4));

        Assert.AreEqual(5, result.Revision);
        Assert.AreEqual(4, store.LastExpectedRevision);
        Assert.AreEqual("content://runner/alpha", result.Favorites[0].Locator);
        Assert.AreEqual("content://runner/recent", result.Recent.Single().Locator);
        Assert.AreEqual(1, store.SaveCount);
    }

    [Test]
    public void ApplySort_fails_closed_without_save_for_stale_revision_or_unknown_target()
    {
        RecordingStore store = new()
        {
            State = new CharacterRosterFavoriteState(
                3,
                [new CharacterRosterDocumentIdentity("content://runner/existing", "Existing")],
                [])
        };
        CharacterRosterFavoritePresenter presenter = new(store);

        Assert.Throws<InvalidOperationException>(() => presenter.ApplySort(new CharacterRosterSortMutation(
            CharacterRosterSortTarget.Favorites,
            ExpectedRevision: 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => presenter.ApplySort(new CharacterRosterSortMutation(
            (CharacterRosterSortTarget)99,
            ExpectedRevision: 3)));
        Assert.AreEqual(0, store.SaveCount);
    }

    private sealed class RecordingStore : ICharacterRosterFavoriteStore
    {
        public CharacterRosterFavoriteState State { get; set; } = CharacterRosterFavoriteState.Empty;
        public int SaveCount { get; private set; }
        public long LastExpectedRevision { get; private set; } = -1;

        public CharacterRosterFavoriteState Load() => State;
        public CharacterRosterFavoriteState Load(OwnerScope owner) => State;

        public void Save(long expectedRevision, CharacterRosterFavoriteState state)
        {
            SaveCount++;
            LastExpectedRevision = expectedRevision;
            State = state;
        }

        public void Save(OwnerScope owner, long expectedRevision, CharacterRosterFavoriteState state)
            => Save(expectedRevision, state);
    }
}
