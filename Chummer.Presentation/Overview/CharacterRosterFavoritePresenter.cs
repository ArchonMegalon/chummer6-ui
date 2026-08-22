using Chummer.Application.Tools;
using Chummer.Contracts.Api;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Revision-safe presentation boundary for Chummer5 CharacterRoster favorite metadata.
/// </summary>
public sealed class CharacterRosterFavoritePresenter
{
    private readonly ICharacterRosterFavoriteStore _store;

    public CharacterRosterFavoritePresenter(ICharacterRosterFavoriteStore store)
    {
        _store = store;
    }

    public CharacterRosterFavoriteState Load()
        => _store.Load();

    public CharacterRosterFavoriteState Apply(CharacterRosterFavoriteMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        CharacterRosterFavoriteState current = _store.Load();
        CharacterRosterFavoriteState updated = CharacterRosterFavoriteRules.Apply(current, mutation);
        if (updated.Revision == current.Revision)
            return current;

        _store.Save(mutation.ExpectedRevision, updated);
        return updated;
    }

    public CharacterRosterFavoriteState ApplySort(CharacterRosterSortMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        CharacterRosterFavoriteState current = _store.Load();
        CharacterRosterFavoriteState updated = CharacterRosterFavoriteRules.ApplySort(current, mutation);
        if (updated.Revision == current.Revision)
            return current;

        _store.Save(mutation.ExpectedRevision, updated);
        return updated;
    }

    public CharacterRosterFavoriteState ApplyRemove(CharacterRosterRemoveMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        CharacterRosterFavoriteState current = _store.Load();
        CharacterRosterFavoriteState updated = CharacterRosterFavoriteRules.ApplyRemove(current, mutation);
        if (updated.Revision == current.Revision)
            return current;

        _store.Save(mutation.ExpectedRevision, updated);
        return updated;
    }
}
