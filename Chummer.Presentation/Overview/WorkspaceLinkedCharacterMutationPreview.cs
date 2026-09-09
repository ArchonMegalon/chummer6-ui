using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Creates the in-memory document replacement used by the presenter for a linked
/// Contact or Pet attachment/removal. This is not a workspace write, a commit
/// receipt, an authorization decision, or verification of the referenced file.
/// </summary>
public static class WorkspaceLinkedCharacterMutationPreview
{
    public static WorkspaceDocument Create(
        WorkspaceDocument document,
        WorkspaceCollectionMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Target);

        if (document.Format != WorkspaceDocumentFormat.NativeXml
            || document.State is null
            || string.IsNullOrWhiteSpace(document.RulesetId)
            || document.SchemaVersion <= 0
            || string.IsNullOrWhiteSpace(document.PayloadKind))
        {
            throw new InvalidOperationException("A linked runner preview requires an identified native XML workspace document.");
        }

        if (request is not (WorkspaceSetLinkedCharacterRequest or WorkspaceRemoveLinkedCharacterRequest))
        {
            throw new InvalidOperationException("A linked runner preview supports only attachment and removal requests.");
        }

        // The shared catalog owns target/path/identity validation and exact XML
        // serialization. Linked-character mutations do not consume source data.
        string payload = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(document.Content, request);
        return document with { State = document.State with { Payload = payload } };
    }
}
