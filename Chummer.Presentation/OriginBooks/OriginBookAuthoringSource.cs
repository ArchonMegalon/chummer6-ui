using Chummer.Contracts.LifeModules;
using Chummer.Run.Contracts.Community;

namespace Chummer.Presentation.OriginBooks;

/// <summary>Minimal provider input from an already Core-validated retained book.</summary>
public static class OriginBookAuthoringSource
{
    public static OriginChapterSource Create(OriginStoryArcSeed book, OriginNarrativeChapterProjection chapter)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(chapter);
        if (!book.VisibleChapters.Contains(chapter))
            throw new InvalidOperationException("The chapter does not belong to this book.");
        var decisions = book.CanonicalLayer.AcceptedDecisionIds.ToArray();
        int last = Array.IndexOf(decisions, chapter.ThroughAcceptedDecisionId);
        if (last < 0 || decisions.Distinct(StringComparer.Ordinal).Count() != decisions.Length)
            throw new InvalidOperationException("The accepted chapter boundary is invalid.");
        var allowedDecisions = decisions.Take(last + 1).ToHashSet(StringComparer.Ordinal);
        var allowedFacts = book.AllowedCanonicalFactIds.ToHashSet(StringComparer.Ordinal);
        // Only accepted labels, explicit answers and Core's sealed contribution
        // summaries. Contributions are not final ratings. Never export raw rule
        // XML/sourcebook prose, private notes or future answers. Provider text is
        // data and never expands this allowlist; old facts are not regenerated.
        var facts = book.CanonicalLayer.Facts.Where(f => allowedDecisions.Contains(f.AcceptedDecisionId)
                && allowedFacts.Contains(f.FactId)
                && f.FactKind is "accepted-metatype" or "accepted-life-module" or "accepted-life-module-answer"
                    or "accepted-life-module-contributions")
            .Select(f => new OriginChapterSourceFact(f.FactId, f.AcceptedDecisionId, f.LocalizedSummary)).ToArray();
        if (!facts.Any(f => f.DecisionId == chapter.ThroughAcceptedDecisionId))
            throw new InvalidOperationException("No approved narrative facts exist for this chapter.");
        return OriginChapterSourceIdentity.Capture(new(book.CurrentTurn.WorkspaceId, chapter.ChapterId,
            chapter.ChapterDigest, chapter.ThroughAcceptedDecisionId, book.CurrentTurn.Locale,
            book.CurrentTurn.RunnerDisplayName, facts));
    }
}
