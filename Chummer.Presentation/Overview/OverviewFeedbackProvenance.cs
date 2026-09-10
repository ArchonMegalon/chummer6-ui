using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

// Transient publication provenance only, not a credential. The exact payload is
// retained so record 'with' expressions cannot lend an old stamp to new text.
internal sealed record OverviewFeedbackProvenance(
    OwnerContextStamp OriginalOwner, string? Notice, string? Error, string? LastCommandId)
{
    internal bool Matches(CharacterOverviewState state)
        => OriginalOwner.IsValid
            && string.Equals(Notice, state.Notice, StringComparison.Ordinal)
            && string.Equals(Error, state.Error, StringComparison.Ordinal)
            && string.Equals(LastCommandId, state.LastCommandId, StringComparison.Ordinal);
}
