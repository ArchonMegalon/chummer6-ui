using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record FreeSpriteConversionEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterFreeSpriteConversionState Conversion);

public sealed record FreeSpriteConversionRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterFreeSpriteConversionIdentity Identity,
    string ExpectedConversionRevision);

internal static class FreeSpriteConversionEditorProjector
{
    public static FreeSpriteConversionEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before converting this Sprite.");
        }

        CharacterFreeSpriteConversionState state = ProjectValue(xml);
        if (!state.CanConvert)
        {
            throw new InvalidOperationException(
                "Convert to Free Sprite is available only to a non-Free Sprite.");
        }
        return new FreeSpriteConversionEditorState(workspaceId, contentRevision, state);
    }

    internal static CharacterFreeSpriteConversionState ProjectValue(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool created = ReadRequiredBool(root, "created");
        string category = ReadRequiredSingle(root, "metatypecategory").Value;
        XElement powers = FindPowers(root);
        Guid[] identities = powers.Elements("critterpower")
            .Select(power => ParseIdentity(power))
            .ToArray();
        if (!CharacterFreeSpriteConversionRules.TryCreateState(
                created,
                category,
                identities,
                out CharacterFreeSpriteConversionState state))
        {
            throw new InvalidOperationException(
                "Free Sprite conversion state is incomplete or ambiguous.");
        }
        return state;
    }

    internal static XElement FindPowers(XElement root)
    {
        XElement[] values = root.Elements("critterpowers").Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0],
            0 => throw new InvalidOperationException(
                "Free Sprite conversion requires one <critterpowers> collection."),
            _ => throw new InvalidOperationException(
                "The saved runner has duplicate <critterpowers> collections.")
        };
    }

    private static XElement ReadRequiredSingle(XElement root, string name)
    {
        XElement[] values = root.Elements(name).Take(2).ToArray();
        return values.Length == 1
            ? values[0]
            : throw new InvalidOperationException(
                $"Free Sprite conversion requires one <{name}> value.");
    }

    private static bool ReadRequiredBool(XElement root, string name)
    {
        XElement value = ReadRequiredSingle(root, name);
        return bool.TryParse(value.Value.Trim(), out bool parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Free Sprite conversion requires an exact saved <{name}> Boolean.");
    }

    private static Guid ParseIdentity(XElement power)
    {
        XElement[] values = power.Elements("guid").Take(2).ToArray();
        return values.Length == 1
            && Guid.TryParseExact(values[0].Value.Trim(), "D", out Guid parsed)
            && parsed != Guid.Empty
                ? parsed
                : throw new InvalidOperationException(
                    "Every saved Critter Power requires one stable non-empty GUID.");
    }
}
