using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CyberwareMatrixSwapEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterCyberwareMatrixSwapState Cyberware);

public sealed record CyberwareMatrixSwapEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCyberwareMatrixSwapIdentity Identity,
    string ExpectedNodeRevision,
    CharacterCyberwareMatrixStat ChangedAttribute,
    CharacterCyberwareMatrixStat TargetAttribute);

internal static class CyberwareMatrixSwapEditorProjector
{
    public static CyberwareMatrixSwapEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid cyberwareId)
    {
        if (contentRevision <= 0)
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before swapping Cyberware Matrix values.");
        return new(workspaceId, contentRevision, ProjectValue(xml, cyberwareId));
    }

    internal static CharacterCyberwareMatrixSwapState ProjectValue(string xml, Guid cyberwareId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (cyberwareId == Guid.Empty)
            throw new InvalidOperationException("Cyberware Matrix swapping requires a stable Cyberware Guid.");

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement cyberware = FindCyberwareRoot(root, cyberwareId);
        if (!CharacterCyberwareMatrixSwapRules.TryCreateState(
                new CharacterCyberwareMatrixSwapIdentity(cyberwareId),
                ReadBoolean(root, "created"),
                ReadDisplayName(cyberware),
                ReadSingle(cyberware, "attack"),
                ReadSingle(cyberware, "sleaze"),
                ReadSingle(cyberware, "dataprocessing"),
                ReadSingle(cyberware, "firewall"),
                ReadSingle(cyberware, "attributearray"),
                ReadBoolean(cyberware, "canswapattributes"),
                out CharacterCyberwareMatrixSwapState state))
        {
            throw new InvalidOperationException(
                "The selected Cyberware root is not an exact enabled CanSwapAttributes Matrix target.");
        }
        return state;
    }

    internal static XElement FindCyberwareRoot(XElement root, Guid cyberwareId)
    {
        XElement container = root.Elements("cyberwares").Single();
        XElement[] matches = container.Elements("cyberware")
            .Where(candidate => Guid.TryParse(ReadSingle(candidate, "guid"), out Guid id) && id == cyberwareId)
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                "Cyberware Guid identity is missing, ambiguous, or belongs to a descendant.");
    }

    private static string ReadDisplayName(XElement cyberware)
    {
        XElement[] customNames = cyberware.Elements("customname").Take(2).ToArray();
        if (customNames.Length > 1)
            throw new InvalidOperationException("Cyberware requires at most one <customname> element.");
        if (customNames.Length == 1 && !string.IsNullOrWhiteSpace(customNames[0].Value))
            return customNames[0].Value;
        string name = ReadSingle(cyberware, "name");
        return string.IsNullOrWhiteSpace(name) ? "Cyberware" : name;
    }

    private static string ReadSingle(XElement parent, string name)
    {
        XElement[] matches = parent.Elements(name).Take(2).ToArray();
        return matches.Length == 1
            ? matches[0].Value
            : throw new InvalidOperationException($"Cyberware requires exactly one <{name}> element.");
    }

    private static bool ReadBoolean(XElement parent, string name)
        => bool.TryParse(ReadSingle(parent, name), out bool value)
            ? value
            : throw new InvalidOperationException($"Cyberware <{name}> must be a saved Boolean.");
}
