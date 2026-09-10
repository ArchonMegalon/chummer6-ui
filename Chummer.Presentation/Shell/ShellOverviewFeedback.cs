using Chummer.Application.Owners;
using System.Text.Json.Serialization;

namespace Chummer.Presentation.Shell;

public sealed record ShellOverviewFeedback(
    IReadOnlyList<ShellWorkspaceState> OpenWorkspaces,
    string? Notice,
    string? Error,
    string? LastCommandId)
{
    // Each facet carries its producer's provenance. A roster and a delayed
    // mutation notice must not borrow the active display's owner from each other.
    [JsonIgnore]
    public OwnerContextStamp? RosterOwnerContext { get; init; }
    [JsonIgnore]
    public OwnerContextStamp? FeedbackOwnerContext { get; init; }
}
