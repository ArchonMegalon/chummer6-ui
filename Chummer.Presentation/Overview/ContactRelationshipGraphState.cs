using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public sealed record ContactRelationshipGraphState(
    IReadOnlyList<ContactRelationshipNodeState> Nodes,
    IReadOnlyList<ContactRelationshipFactionState> Factions,
    IReadOnlyList<ContactRelationshipHeatState> HeatRails,
    IReadOnlyList<ContactRelationshipObligationState> Obligations,
    IReadOnlyList<ContactRelationshipFavorState> UnresolvedFavors)
{
    public int EdgeCount => Nodes.Sum(node => node.LinkedContactNames.Count);
}

public sealed record ContactRelationshipNodeState(
    string Name,
    string Role,
    string Location,
    int Connection,
    int Loyalty,
    string Faction,
    string FactionStatus,
    int Heat,
    IReadOnlyList<string> LinkedContactNames);

public sealed record ContactRelationshipFactionState(
    string Name,
    string Status,
    int ContactCount,
    int AverageHeat);

public sealed record ContactRelationshipHeatState(
    string Subject,
    int Heat,
    string Status);

public sealed record ContactRelationshipObligationState(
    string Subject,
    string Summary,
    string Severity);

public sealed record ContactRelationshipFavorState(
    string Subject,
    string Summary,
    bool Overdue);

public static class ContactRelationshipGraphProjector
{
    public static ContactRelationshipGraphState? FromContacts(CharacterContactsSection? contacts)
    {
        IReadOnlyList<CharacterContactSummary> contactRows = contacts?.Contacts ?? Array.Empty<CharacterContactSummary>();
        if (contactRows.Count == 0)
        {
            return null;
        }

        IReadOnlyList<ContactRelationshipNodeState> nodes = contactRows
            .Select((contact, index) => BuildNode(contact, contactRows, index))
            .ToArray();

        IReadOnlyList<ContactRelationshipFactionState> factions = nodes
            .GroupBy(node => node.Faction, StringComparer.Ordinal)
            .Select(group =>
            {
                int averageHeat = (int)Math.Round(group.Average(node => node.Heat), MidpointRounding.AwayFromZero);
                return new ContactRelationshipFactionState(
                    Name: group.Key,
                    Status: ResolveFactionStatus(averageHeat),
                    ContactCount: group.Count(),
                    AverageHeat: averageHeat);
            })
            .OrderByDescending(faction => faction.AverageHeat)
            .ThenBy(faction => faction.Name, StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<ContactRelationshipHeatState> heatRails = nodes
            .OrderByDescending(node => node.Heat)
            .ThenBy(node => node.Name, StringComparer.Ordinal)
            .Select(node => new ContactRelationshipHeatState(node.Name, node.Heat, ResolveHeatStatus(node.Heat)))
            .ToArray();

        IReadOnlyList<ContactRelationshipObligationState> obligations = nodes
            .Where(node => node.Connection >= 4 || node.Loyalty <= 2)
            .Select(node => new ContactRelationshipObligationState(
                Subject: node.Name,
                Summary: $"{node.Faction} asks for a follow-up favor in {node.Location}.",
                Severity: node.Connection >= 6 || node.Loyalty <= 1 ? "high" : "medium"))
            .ToArray();

        IReadOnlyList<ContactRelationshipFavorState> favors = nodes
            .Where(node => node.Heat >= 4 || node.Loyalty <= 3)
            .Select(node => new ContactRelationshipFavorState(
                Subject: node.Name,
                Summary: $"Unresolved favor rail is open ({node.Role}).",
                Overdue: node.Heat >= 5 || node.Loyalty <= 2))
            .ToArray();

        return new ContactRelationshipGraphState(nodes, factions, heatRails, obligations, favors);
    }

    private static ContactRelationshipNodeState BuildNode(
        CharacterContactSummary contact,
        IReadOnlyList<CharacterContactSummary> contacts,
        int index)
    {
        int heat = Math.Clamp(contact.Connection + (5 - contact.Loyalty), 1, 6);
        string faction = ResolveFaction(contact);
        string factionStatus = ResolveFactionStatus(heat);
        string contactName = contact.Name ?? string.Empty;
        string contactRole = contact.Role ?? string.Empty;
        string contactLocation = contact.Location ?? string.Empty;

        string[] linkedContactNames = contacts
            .Where((candidate, candidateIndex) => candidateIndex != index)
            .OrderByDescending(candidate => candidate.Connection + candidate.Loyalty)
            .Take(2)
            .Select(candidate => candidate.Name ?? string.Empty)
            .ToArray();

        return new ContactRelationshipNodeState(
            Name: contactName,
            Role: contactRole,
            Location: contactLocation,
            Connection: contact.Connection,
            Loyalty: contact.Loyalty,
            Faction: faction,
            FactionStatus: factionStatus,
            Heat: heat,
            LinkedContactNames: linkedContactNames);
    }

    private static string ResolveFaction(CharacterContactSummary contact)
    {
        string role = contact.Role ?? string.Empty;
        if (role.Contains("fixer", StringComparison.OrdinalIgnoreCase))
        {
            return "Broker Network";
        }

        if (role.Contains("matrix", StringComparison.OrdinalIgnoreCase))
        {
            return "Matrix Exchange";
        }

        if (role.Contains("doc", StringComparison.OrdinalIgnoreCase))
        {
            return "Street Medicine";
        }

        return "Runner Network";
    }

    private static string ResolveFactionStatus(int averageHeat)
    {
        if (averageHeat >= 5)
        {
            return "hostile";
        }

        if (averageHeat >= 3)
        {
            return "strained";
        }

        return "stable";
    }

    private static string ResolveHeatStatus(int heat)
    {
        if (heat >= 5)
        {
            return "critical";
        }

        if (heat >= 3)
        {
            return "watch";
        }

        return "low";
    }
}
