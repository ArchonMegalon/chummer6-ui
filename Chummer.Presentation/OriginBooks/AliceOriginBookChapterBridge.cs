using Chummer.Application.LifeModules;
using Chummer.Application.Owners;
using Chummer.Contracts.LifeModules;
using Chummer.Contracts.Workspaces;
using Chummer.Run.Contracts.Community;

namespace Chummer.Presentation.OriginBooks;

/// <summary>
/// Read-only seam between the Core owner-bound life-module book authority and
/// the Alice origin-dossier drafting flow. It resolves the retained canonical
/// chapter projection through <see cref="IOwnerBoundLifeModuleBookService"/>
/// and converts it into the provider input the authoring source allows. The
/// bridge never fabricates facts, never writes a workspace, and never derives
/// legality: when the Core authority cannot serve a book, callers fall back to
/// the pre-existing drafting path unchanged.
/// </summary>
public interface IAliceOriginBookChapterBridge
{
    /// <summary>
    /// Resolves the authoring provider input for the retained book of
    /// <paramref name="workspaceId"/>, or <c>null</c> when Core reports the
    /// book as missing/unavailable (no failure surfaced to drafting). The
    /// owner stamp is captured from the live owner authority at call time.
    /// </summary>
    OriginChapterSource? TryResolveChapterSource(
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        long savedRevision);
}

public sealed record AliceOriginBookChapterResolution(
    OriginStoryArcSeed Book,
    OriginNarrativeChapterProjection Chapter,
    OriginChapterSource ProviderInput);

public sealed class AliceOriginBookChapterBridge : IAliceOriginBookChapterBridge
{
    private readonly IOwnerBoundLifeModuleBookService _bookService;
    private readonly IOwnerContextAccessor _owners;

    public AliceOriginBookChapterBridge(
        IOwnerBoundLifeModuleBookService bookService,
        IOwnerContextAccessor owners)
    {
        _bookService = bookService ?? throw new ArgumentNullException(nameof(bookService));
        _owners = owners ?? throw new ArgumentNullException(nameof(owners));
    }

    public OriginChapterSource? TryResolveChapterSource(
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        long savedRevision)
    {
        if (string.IsNullOrWhiteSpace(_owners.Current.Value)
            || workspaceId.Value.Length == 0)
        {
            return null;
        }

        var owner = new OwnerContextStamp(_owners.Current, "presentation-read", 0);
        LifeModuleOriginDossierResult<OriginStoryArcSeed> book =
            _bookService.Load(owner, workspaceId, contentRevision, savedRevision);
        if (book.Outcome != LifeModuleOriginDossierOutcomes.Success
            || book.Value is not OriginStoryArcSeed seed
            || seed.VisibleChapters.Count == 0)
        {
            return null;
        }

        // The retained head chapter is the continuation point for drafting.
        OriginNarrativeChapterProjection chapter = seed.VisibleChapters[^1];
        try
        {
            return OriginBookAuthoringSource.Create(seed, chapter);
        }
        catch (InvalidOperationException)
        {
            // A Core-validated book must always yield approved facts; a throw
            // means the projection drifted, so fail closed to "no book".
            return null;
        }
    }
}
