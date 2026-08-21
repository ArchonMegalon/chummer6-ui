using System.Globalization;
using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public enum WorkspaceLocationKind
{
    Gear,
    Weapon,
    Armor,
    Vehicle
}

public sealed record WorkspaceLocationItemState(
    Guid Id,
    string Name,
    string Notes);

public sealed record WorkspaceLocationEditorState(
    WorkspaceLocationKind Kind,
    string SectionId,
    IReadOnlyList<WorkspaceLocationItemState> Items);

public static class WorkspaceLocationEditorProjector
{
    private const int MaximumLocationCount = 4096;

    public static bool IsLocationSection(string? sectionId)
        => TryResolveKind(sectionId, out _);

    public static WorkspaceLocationEditorState? TryProject(string? sectionId, JsonNode? section)
    {
        if (!TryResolveKind(sectionId, out WorkspaceLocationKind kind)
            || section is not JsonObject root
            || ReadNode(root, "locations") is not JsonArray locations
            || locations.Count > MaximumLocationCount)
        {
            return null;
        }

        if (!TryReadInt(root, "count", out int declaredCount)
            || declaredCount != locations.Count)
        {
            return null;
        }

        List<WorkspaceLocationItemState> items = [];
        HashSet<Guid> seen = [];
        foreach (JsonNode? node in locations)
        {
            if (node is not JsonObject location
                || !TryReadString(location, "guid", out string idText)
                || !Guid.TryParseExact(idText, "D", out Guid id)
                || !seen.Add(id)
                || !TryReadString(location, "name", out string name)
                || !TryReadString(location, "notes", out string notes))
            {
                return null;
            }

            items.Add(new WorkspaceLocationItemState(id, name, notes));
        }

        return new WorkspaceLocationEditorState(
            kind,
            sectionId!.Trim().ToLowerInvariant(),
            items);
    }

    public static string SectionId(WorkspaceLocationKind kind)
        => kind switch
        {
            WorkspaceLocationKind.Gear => "gearlocations",
            WorkspaceLocationKind.Weapon => "weaponlocations",
            WorkspaceLocationKind.Armor => "armorlocations",
            WorkspaceLocationKind.Vehicle => "vehiclelocations",
            _ => throw new InvalidOperationException($"Unsupported location kind '{kind}'.")
        };

    private static bool TryResolveKind(string? sectionId, out WorkspaceLocationKind kind)
    {
        switch (sectionId?.Trim().ToLowerInvariant())
        {
            case "gearlocations":
                kind = WorkspaceLocationKind.Gear;
                return true;
            case "weaponlocations":
                kind = WorkspaceLocationKind.Weapon;
                return true;
            case "armorlocations":
                kind = WorkspaceLocationKind.Armor;
                return true;
            case "vehiclelocations":
                kind = WorkspaceLocationKind.Vehicle;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static JsonNode? ReadNode(JsonObject source, string propertyName)
    {
        foreach ((string key, JsonNode? value) in source)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryReadString(JsonObject source, string propertyName, out string value)
    {
        JsonNode? node = ReadNode(source, propertyName);
        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out string? text) && text is not null)
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadInt(JsonObject source, string propertyName, out int value)
    {
        JsonNode? node = ReadNode(source, propertyName);
        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out int number))
        {
            value = number;
            return true;
        }

        return int.TryParse(
            node?.ToJsonString().Trim('"'),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }
}
