using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CreationLifestyleDeleteEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterCreationLifestyleDeleteState> Lifestyles);

public sealed record CreationLifestyleDeleteRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCreationLifestyleDeleteIdentity SelectedIdentity,
    string ExpectedLifestyleRevision,
    bool Confirmed);

internal sealed record CreationLifestyleDeleteProjection(
    CharacterCreationLifestyleDeleteState State,
    XElement Lifestyle,
    IReadOnlyList<XElement> Improvements);

internal static class CreationLifestyleDeleteEditorProjector
{
    private const string QualitySource = "Quality";
    private static readonly HashSet<string> PersistedCascadeImprovementTypes = new(StringComparer.Ordinal)
    {
        "SkillsoftAccess", "Activesoft", "Skillsoft", "Hardwire", "SpecialTab",
        "PrototypeTranshuman", "Adapsin", "AddContact", "Art", "Metamagic", "Echo",
        "LimitModifier", "CritterPower", "MentorSpirit", "Paragon", "Gear", "Weapon",
        "Spell", "ComplexForm", "MartialArt", "SpecialSkills", "SpecificQuality",
        "SkillSpecialization", "SkillExpertise", "AIProgram", "AdeptPowerFreeLevels",
        "AdeptPowerFreePoints", "FreeWare"
    };

    public static CreationLifestyleDeleteEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reopen before deleting a Lifestyle.");
        }
        XDocument document = ParseDocument(xml);
        return new CreationLifestyleDeleteEditorState(
            workspaceId,
            contentRevision,
            ProjectElements(document.Root!).Select(static projection => projection.State).ToArray());
    }

    internal static IReadOnlyList<CreationLifestyleDeleteProjection> ProjectElements(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        bool created = ReadRequiredBool(root, "created");
        XElement lifestyles = FindSingleContainer(root, "lifestyles");
        XElement improvements = FindSingleContainer(root, "improvements");
        var lifestyleIds = new HashSet<Guid>();
        var qualityIds = new HashSet<Guid>();
        var projections = new List<CreationLifestyleDeleteProjection>();

        foreach (XElement lifestyle in lifestyles.Elements("lifestyle"))
        {
            Guid lifestyleId = ReadIdentity(lifestyle, "Lifestyle");
            if (!lifestyleIds.Add(lifestyleId))
            {
                throw new InvalidOperationException("Lifestyle GUIDs must be unique.");
            }

            string name = ReadRequiredText(lifestyle, "name", "Lifestyle");
            string customName = ReadOptionalText(lifestyle, "extra", "Lifestyle custom name");
            XElement qualities = FindSingleContainer(lifestyle, "lifestylequalities");
            var selectedQualityIds = new List<Guid>();
            foreach (XElement quality in qualities.Elements("lifestylequality"))
            {
                Guid qualityId = ReadIdentity(quality, "Lifestyle Quality");
                if (!qualityIds.Add(qualityId))
                {
                    throw new InvalidOperationException(
                        "Lifestyle Quality GUIDs must be unique across all Lifestyles.");
                }
                selectedQualityIds.Add(qualityId);
            }

            IReadOnlyList<XElement> linkedImprovements = FindImprovements(
                improvements,
                selectedQualityIds);
            string improvementState = string.Join(
                "\n",
                linkedImprovements.Select(element => element.ToString(SaveOptions.DisableFormatting)));
            var identity = new CharacterCreationLifestyleDeleteIdentity(lifestyleId);
            if (!CharacterCreationLifestyleDeleteRules.TryCreateState(
                    identity,
                    created,
                    string.IsNullOrWhiteSpace(customName) ? name : customName,
                    selectedQualityIds.Count,
                    linkedImprovements.Count,
                    lifestyle.ToString(SaveOptions.DisableFormatting),
                    improvementState,
                    out CharacterCreationLifestyleDeleteState state))
            {
                throw new InvalidOperationException(
                    "The selected Lifestyle does not satisfy the exact Creation delete contract.");
            }
            projections.Add(new CreationLifestyleDeleteProjection(state, lifestyle, linkedImprovements));
        }
        return projections;
    }

    internal static XDocument ParseDocument(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        if (document.Root is not { Name.LocalName: "character" })
        {
            throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        }
        return document;
    }

    private static IReadOnlyList<XElement> FindImprovements(
        XElement improvements,
        IReadOnlyList<Guid> qualityIds)
    {
        string[] sources = qualityIds.Select(static id => id.ToString("D")).ToArray();
        var matches = new List<XElement>();
        foreach (XElement improvement in improvements.Elements("improvement"))
        {
            XElement[] sourceElements = improvement.Elements("improvementsource").Take(2).ToArray();
            if (sourceElements.Length != 1)
            {
                throw new InvalidOperationException(
                    "Every saved Improvement requires one exact <improvementsource> value.");
            }
            if (!string.Equals(sourceElements[0].Value, QualitySource, StringComparison.Ordinal))
            {
                continue;
            }

            XElement[] sourceNames = improvement.Elements("sourcename").Take(2).ToArray();
            if (sourceNames.Length != 1)
            {
                throw new InvalidOperationException(
                    "Quality Improvements require one exact <sourcename> value.");
            }
            string sourceName = sourceNames[0].Value;
            if (sources.Any(source =>
                    string.Equals(sourceName, source, StringComparison.Ordinal)
                    || sourceName.StartsWith(source + " ", StringComparison.Ordinal)))
            {
                EnsureNoUnportedPersistedCascade(improvement);
                matches.Add(improvement);
            }
        }
        return matches;
    }

    private static void EnsureNoUnportedPersistedCascade(XElement improvement)
    {
        string improvementType = ReadRequiredText(
            improvement,
            "improvementttype",
            "Quality Improvement type");
        if (PersistedCascadeImprovementTypes.Contains(improvementType))
        {
            throw new InvalidOperationException(
                $"Lifestyle deletion requires the exact ImprovementManager persisted-object cascade for '{improvementType}', which is not available on phone yet.");
        }
        if (!string.Equals(improvementType, "Attribute", StringComparison.Ordinal))
        {
            return;
        }

        string unique = ReadOptionalText(improvement, "unique", "Quality Improvement unique name");
        string improvedName = ReadOptionalText(improvement, "improvedname", "Quality Improvement target");
        if (string.Equals(unique, "enableattribute", StringComparison.OrdinalIgnoreCase)
            && improvedName is "MAG" or "RES" or "DEP")
        {
            throw new InvalidOperationException(
                "Lifestyle deletion requires the exact ImprovementManager special-attribute cascade, which is not available on phone yet.");
        }
    }

    private static XElement FindSingleContainer(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0],
            0 => throw new InvalidOperationException($"The saved runner requires one <{name}> collection."),
            _ => throw new InvalidOperationException($"The saved runner has duplicate <{name}> collections.")
        };
    }

    private static Guid ReadIdentity(XElement target, string label)
    {
        XElement[] values = target.Elements("guid").Take(2).ToArray();
        return values.Length == 1
            && Guid.TryParseExact(values[0].Value.Trim(), "D", out Guid parsed)
            && parsed != Guid.Empty
                ? parsed
                : throw new InvalidOperationException($"{label} requires one stable non-empty GUID.");
    }

    private static string ReadRequiredText(XElement target, string name, string label)
    {
        XElement[] values = target.Elements(name).Take(2).ToArray();
        return values.Length == 1
            ? values[0].Value
            : throw new InvalidOperationException($"{label} requires one <{name}> value.");
    }

    private static string ReadOptionalText(XElement target, string name, string label)
    {
        XElement[] values = target.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => string.Empty,
            1 => values[0].Value,
            _ => throw new InvalidOperationException($"{label} must not be duplicated.")
        };
    }

    private static bool ReadRequiredBool(XElement target, string name)
    {
        XElement[] values = target.Elements(name).Take(2).ToArray();
        return values.Length == 1 && bool.TryParse(values[0].Value.Trim(), out bool parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Lifestyle deletion requires one exact saved <{name}> Boolean.");
    }
}
