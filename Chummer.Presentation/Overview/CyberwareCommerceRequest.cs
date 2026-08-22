using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CyberwareCommerceEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid CyberwareId,
    string CyberwareName,
    CharacterCyberwareCommerceSemantics Semantics);

public sealed record CyberwareCommerceRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid CyberwareId,
    CharacterCyberwareCommerceAction Action,
    string GradeId,
    int Rating,
    decimal RefundPercentage,
    bool FreeCost,
    bool Confirmed,
    string QuoteDigest);
