using System.Globalization;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerReputationEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    int StreetCred,
    int Notoriety,
    int PublicAwareness,
    bool AstralReputationVisible,
    int AstralReputation,
    bool WildReputationVisible,
    int WildReputation)
{
    public int BurntStreetCred { get; init; }

    public int TotalStreetCred { get; init; }

    public bool CanBurnStreetCred { get; init; }

    public string? BurnStreetCredUnavailableReason { get; init; }
}

public sealed record CareerReputationEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    int StreetCred,
    int Notoriety,
    int PublicAwareness,
    int? AstralReputation,
    int? WildReputation);

public sealed record BurnStreetCredRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision);

internal static class CareerReputationEditorProjector
{
    public static CareerReputationEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing reputation.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Reputation can only be changed for a created/career runner.");
        }

        ICharacterSourceDataContext? sourceData = sourceDataResolver?.TryCreateContext(xml);
        bool forbiddenArcana = IsBookEnabled(sourceData, "FA");
        bool streetGrimoire = IsBookEnabled(sourceData, "SG");
        CareerStreetCredProjection streetCred = CareerStreetCredRules.Project(root);
        return new CareerReputationEditorState(
            workspaceId,
            contentRevision,
            ParseInt(root.Element("streetcred")?.Value),
            ParseInt(root.Element("notoriety")?.Value),
            ParseInt(root.Element("publicawareness")?.Value),
            AstralReputationVisible: forbiddenArcana || streetGrimoire,
            AstralReputation: ParseInt(root.Element("baseastralreputation")?.Value),
            WildReputationVisible: forbiddenArcana,
            WildReputation: ParseInt(root.Element("basewildreputation")?.Value))
        {
            BurntStreetCred = streetCred.BurntStreetCred,
            TotalStreetCred = streetCred.TotalStreetCred,
            CanBurnStreetCred = streetCred.CanBurn,
            BurnStreetCredUnavailableReason = streetCred.UnavailableReason
        };
    }

    internal static bool IsBookEnabled(ICharacterSourceDataContext? sourceData, string sourceCode)
        => sourceData is not null
            && sourceData.TryIsBookEnabled(sourceCode, out bool enabled)
            && enabled;

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out bool parsed) && parsed;

    private static int ParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
}

internal sealed record CareerStreetCredProjection(
    int BurntStreetCred,
    int TotalStreetCred,
    bool CanBurn,
    string? UnavailableReason);

internal static class CareerStreetCredRules
{
    private sealed record ImprovementValue(
        string ImprovedName,
        string UniqueName,
        decimal Value,
        bool Custom);

    public static CareerStreetCredProjection Project(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        int burntStreetCred = ParseInt(root.Element("burntstreetcred")?.Value);
        if (!ParseBool(root.Element("created")?.Value))
        {
            return new CareerStreetCredProjection(
                burntStreetCred,
                TotalStreetCred: 0,
                CanBurn: false,
                UnavailableReason: "Street Cred can only be burned by a created/career runner.");
        }

        try
        {
            int careerKarma = 0;
            foreach (XElement expense in root.Element("expenses")?.Elements("expense") ?? [])
            {
                string type = expense.Element("type")?.Value ?? string.Empty;
                if (string.Equals(type, "Nuyen", StringComparison.OrdinalIgnoreCase)
                    || ParseBool(expense.Element("refund")?.Value))
                {
                    continue;
                }

                decimal amount = ParseDecimal(expense.Element("amount")?.Value);
                if (amount > 0 || ParseBool(expense.Element("forcecareervisible")?.Value))
                {
                    careerKarma = checked(careerKarma + StandardRound(amount));
                }
            }

            int multiplier = StandardRound(SumImprovementValues(root, "StreetCredMultiplier"));
            int divisor = checked(10 + multiplier);
            if (divisor == 0)
            {
                return new CareerStreetCredProjection(
                    burntStreetCred,
                    TotalStreetCred: 0,
                    CanBurn: false,
                    UnavailableReason: "Street Cred cannot be calculated because its career Karma divisor is zero.");
            }

            int calculatedStreetCred = checked(careerKarma / divisor - burntStreetCred);
            int awardedStreetCred = ParseInt(root.Element("streetcred")?.Value);
            int improvementStreetCred = StandardRound(SumImprovementValues(root, "StreetCred"));
            int totalStreetCred = Math.Max(
                checked(calculatedStreetCred + awardedStreetCred + improvementStreetCred),
                0);
            return new CareerStreetCredProjection(
                burntStreetCred,
                totalStreetCred,
                CanBurn: totalStreetCred >= 2,
                UnavailableReason: null);
        }
        catch (OverflowException)
        {
            return new CareerStreetCredProjection(
                burntStreetCred,
                TotalStreetCred: 0,
                CanBurn: false,
                UnavailableReason: "Street Cred cannot be calculated because a saved value is outside the supported range.");
        }
    }

    private static decimal SumImprovementValues(XElement root, string improvementType)
    {
        ImprovementValue[] values = (root.Element("improvements")?.Elements("improvement") ?? [])
            .Where(node => string.Equals(
                node.Element("improvementttype")?.Value,
                improvementType,
                StringComparison.Ordinal))
            .Where(node => ParseInt(node.Element("enabled")?.Value, defaultValue: 1) > 0)
            .Where(node => ParseInt(node.Element("addtorating")?.Value) == 0)
            .Where(node =>
            {
                string condition = node.Element("condition")?.Value ?? string.Empty;
                return condition.Length == 0 || string.Equals(condition, "career", StringComparison.Ordinal);
            })
            .Select(node => new ImprovementValue(
                node.Element("improvedname")?.Value ?? string.Empty,
                node.Element("unique")?.Value ?? string.Empty,
                ParseDecimal(node.Element("val")?.Value),
                ParseBool(node.Element("custom")?.Value)))
            .ToArray();

        decimal total = 0;
        foreach (IGrouping<string, ImprovementValue> group in values.GroupBy(
                     value => value.ImprovedName,
                     StringComparer.Ordinal))
        {
            total = checked(total + SumPartition(group.Where(value => !value.Custom), custom: false));
            total = checked(total + SumPartition(group.Where(value => value.Custom), custom: true));
        }
        return total;
    }

    private static decimal SumPartition(IEnumerable<ImprovementValue> source, bool custom)
    {
        ImprovementValue[] values = source.ToArray();
        decimal result = values
            .Where(value => value.UniqueName.Length == 0)
            .Sum(value => value.Value);
        ImprovementValue[] unique = values
            .Where(value => value.UniqueName.Length > 0)
            .ToArray();
        if (unique.Length == 0)
        {
            return result;
        }

        if (!custom && unique.Any(value => value.UniqueName == "precedence0"))
        {
            decimal precedence = unique
                .Where(value => value.UniqueName == "precedence0")
                .Max(value => value.Value);
            precedence = checked(precedence + unique
                .Where(value => value.UniqueName == "precedence-1")
                .Sum(value => value.Value));
            return Math.Max(result, precedence);
        }

        if (!custom && unique.Any(value => value.UniqueName == "precedence1"))
        {
            decimal precedence = unique
                .Where(value => value.UniqueName is "precedence1" or "precedence-1")
                .Sum(value => value.Value);
            return Math.Max(result, precedence);
        }

        foreach (IGrouping<string, ImprovementValue> group in unique.GroupBy(
                     value => value.UniqueName,
                     StringComparer.Ordinal))
        {
            result = checked(result + group.Max(value => value.Value));
        }
        return result;
    }

    private static int StandardRound(decimal value)
        => decimal.ToInt32(value >= 0 ? decimal.Ceiling(value) : decimal.Floor(value));

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out bool parsed) && parsed;

    private static int ParseInt(string? value, int defaultValue = 0)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : 0;
}
