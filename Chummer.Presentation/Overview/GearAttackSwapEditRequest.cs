using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GearAttackSwapEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid RootGearId,
    IReadOnlyList<CharacterGearAttackSwapState> Nodes);

public sealed record GearAttackSwapEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterGearAttackSwapIdentity Identity,
    string ExpectedNodeRevision,
    CharacterGearAttackSwapTarget Target);

internal static class GearAttackSwapEditorProjector
{
    private static readonly HashSet<string> LegacyNoSwapCyberdecks = new(StringComparer.Ordinal)
    {
        "MCT Trainee", "C-K Analyst", "Aztechnology Emissary", "Yak Killer",
        "Ring of Light Special", "Ares Echo Unlimited"
    };

    public static GearAttackSwapEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid rootGearId)
    {
        if (contentRevision <= 0)
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before swapping Gear Attack.");
        return new GearAttackSwapEditorState(
            workspaceId, contentRevision, rootGearId, ProjectValue(xml, rootGearId));
    }

    internal static IReadOnlyList<CharacterGearAttackSwapState> ProjectValue(string xml, Guid rootGearId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (rootGearId == Guid.Empty)
            throw new InvalidOperationException("Gear Attack swapping requires a stable root Gear Guid.");

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool created = ReadRequiredBoolean(root, "created", "creation/career state");
        XElement selectedRoot = FindUniqueDirectByGuid(ReadRequiredContainer(root, "gears"), rootGearId, "root Gear");
        var states = new List<CharacterGearAttackSwapState>();
        var seenIds = new HashSet<Guid>();
        AddStateRecursive(selectedRoot, [], null, created, states, seenIds);
        if (states.Count == 0)
            throw new InvalidOperationException("The selected Gear tree has no item eligible to swap Matrix attributes.");
        return states.ToArray();
    }

    internal static XElement FindNode(XElement root, CharacterGearAttackSwapIdentity identity)
    {
        if (!CharacterGearAttackSwapRules.IsValidIdentity(identity))
            throw new InvalidOperationException("The selected Gear hierarchy is invalid.");
        XElement current = FindUniqueDirectByGuid(ReadRequiredContainer(root, "gears"), identity.GearPath[0], "root Gear");
        for (int index = 1; index < identity.GearPath.Count; index++)
            current = FindUniqueDirectByGuid(ReadRequiredContainer(current, "children"), identity.GearPath[index], "nested Gear");
        return current;
    }

    private static void AddStateRecursive(
        XElement gear,
        IReadOnlyList<Guid> parentPath,
        string? parentDisplayPath,
        bool created,
        ICollection<CharacterGearAttackSwapState> states,
        ISet<Guid> seenIds)
    {
        Guid gearId = ReadGuid(gear);
        if (!seenIds.Add(gearId))
            throw new InvalidOperationException("Gear Attack swapping requires unique stable Guid identity throughout the selected tree.");
        Guid[] path = [.. parentPath, gearId];
        string name = DisplayName(gear);
        string displayPath = parentDisplayPath is null ? name : $"{parentDisplayPath} > {name}";
        bool canSwap = ReadCanSwapAttributes(gear, name);
        if (canSwap)
        {
            var identity = new CharacterGearAttackSwapIdentity(path);
            if (!CharacterGearAttackSwapRules.TryCreateState(
                    identity, created, true, displayPath,
                    ReadRequiredSingleText(gear, "attack"),
                    ReadRequiredSingleText(gear, "sleaze"),
                    ReadRequiredSingleText(gear, "dataprocessing"),
                    ReadRequiredSingleText(gear, "firewall"),
                    out CharacterGearAttackSwapState state))
                throw new InvalidOperationException("Gear Attack swapping requires exact saved raw Matrix attributes.");
            states.Add(state);
        }

        XElement? children = ReadOptionalContainer(gear, "children");
        if (children is null) return;
        foreach (XElement child in children.Elements("gear"))
            AddStateRecursive(child, path, displayPath, created, states, seenIds);
    }

    private static bool ReadCanSwapAttributes(XElement gear, string name)
    {
        XElement[] values = gear.Elements("canswapattributes").Take(2).ToArray();
        if (values.Length > 1)
            throw new InvalidOperationException("A Gear node has duplicate <canswapattributes> values.");
        if (values.Length == 1)
        {
            if (!bool.TryParse(values[0].Value.Trim(), out bool parsed))
                throw new InvalidOperationException("A Gear node has an invalid <canswapattributes> Boolean.");
            return parsed;
        }
        return string.Equals(ReadOptionalSingleText(gear, "category"), "Cyberdecks", StringComparison.Ordinal)
            && !LegacyNoSwapCyberdecks.Contains(name);
    }

    private static bool ReadRequiredBoolean(XElement parent, string name, string kind)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
            throw new InvalidOperationException($"Gear Attack swapping requires one exact saved {kind} Boolean.");
        return value;
    }

    private static string ReadRequiredSingleText(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || values[0].Value.Length == 0)
            throw new InvalidOperationException($"Gear Attack swapping requires one exact saved <{name}> value.");
        return values[0].Value;
    }

    private static string ReadOptionalSingleText(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => string.Empty,
            1 => values[0].Value,
            _ => throw new InvalidOperationException($"The saved node has duplicate <{name}> values.")
        };
    }

    private static Guid ReadGuid(XElement element)
    {
        XElement[] values = element.Elements("guid").Take(2).ToArray();
        if (values.Length != 1 || !Guid.TryParseExact(values[0].Value.Trim(), "D", out Guid id) || id == Guid.Empty)
            throw new InvalidOperationException("Gear requires one stable Guid.");
        return id;
    }

    private static XElement FindUniqueDirectByGuid(XElement container, Guid id, string kind)
    {
        XElement[] matches = container.Elements("gear")
            .Where(candidate => Guid.TryParseExact(candidate.Elements("guid").SingleOrDefault()?.Value.Trim(), "D", out Guid candidateId)
                && candidateId == id)
            .Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(
            $"{kind} identity is missing or ambiguous in the saved hierarchy.");
    }

    private static XElement ReadRequiredContainer(XElement parent, string name)
        => ReadOptionalContainer(parent, name) ?? throw new InvalidOperationException($"The saved hierarchy is missing <{name}>.");

    private static XElement? ReadOptionalContainer(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => null,
            1 => values[0],
            _ => throw new InvalidOperationException($"The saved hierarchy has duplicate <{name}> collections.")
        };
    }

    private static string DisplayName(XElement element)
    {
        XElement[] names = element.Elements("name").Take(2).ToArray();
        return names.Length switch
        {
            0 => "Unnamed Gear",
            1 when names[0].Value.Length != 0 => names[0].Value,
            1 => "Unnamed Gear",
            _ => throw new InvalidOperationException("A Gear node has duplicate <name> values.")
        };
    }
}
