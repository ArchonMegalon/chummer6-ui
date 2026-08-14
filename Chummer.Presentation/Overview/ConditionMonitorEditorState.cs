using System.Globalization;
using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public sealed record ConditionMonitorTrackState(
    WorkspaceConditionMonitorTrack Track,
    string Label,
    int Filled,
    int TrackMaximum,
    int Overflow,
    int EditableMaximum,
    int ThresholdOffset,
    string NaturalRecovery,
    bool ActsAsAlternateTrack);

public sealed record ConditionMonitorEditorState(
    bool CareerEditable,
    IReadOnlyList<ConditionMonitorTrackState> Tracks);

public static class ConditionMonitorEditorProjector
{
    private const int MaximumConditionBoxes = 1000;

    public static bool IsConditionMonitorSection(string? sectionId)
        => string.Equals(sectionId?.Trim(), "conditionmonitor", StringComparison.OrdinalIgnoreCase);

    public static ConditionMonitorEditorState? TryProject(string? sectionId, JsonNode? section)
    {
        if (!IsConditionMonitorSection(sectionId) || section is not JsonObject root)
        {
            return null;
        }

        int physicalTrack = ReadInt(root, "physicalTrack");
        int physicalOverflow = ReadInt(root, "physicalOverflow");
        long physicalMaximumValue = (long)Math.Max(0, physicalTrack) + Math.Max(0, physicalOverflow);
        if (physicalMaximumValue > MaximumConditionBoxes)
        {
            return null;
        }
        int physicalMaximum = (int)physicalMaximumValue;
        int physicalFilled = ReadInt(root, "physicalFilled");
        int stunTrack = ReadInt(root, "stunTrack");
        int stunFilled = ReadInt(root, "stunFilled");
        if (stunTrack > MaximumConditionBoxes)
        {
            return null;
        }
        if (physicalFilled < 0 || physicalFilled > physicalMaximum
            || stunFilled < 0 || stunFilled > Math.Max(0, stunTrack))
        {
            return null;
        }

        List<ConditionMonitorTrackState> tracks = [];
        if (physicalMaximum > 0)
        {
            bool actsAsCore = ReadBool(root, "physicalActsAsCore");
            tracks.Add(new ConditionMonitorTrackState(
                Track: WorkspaceConditionMonitorTrack.Physical,
                Label: actsAsCore ? "Core" : "Physical",
                Filled: physicalFilled,
                TrackMaximum: Math.Max(0, physicalTrack),
                Overflow: Math.Max(0, physicalOverflow),
                EditableMaximum: physicalMaximum,
                ThresholdOffset: ReadInt(root, "physicalThresholdOffset"),
                NaturalRecovery: ReadString(root, "physicalNaturalRecovery"),
                ActsAsAlternateTrack: actsAsCore));
        }

        if (stunTrack > 0)
        {
            bool actsAsMatrix = ReadBool(root, "stunActsAsMatrix");
            tracks.Add(new ConditionMonitorTrackState(
                Track: WorkspaceConditionMonitorTrack.Stun,
                Label: actsAsMatrix ? "Matrix" : "Stun",
                Filled: stunFilled,
                TrackMaximum: stunTrack,
                Overflow: 0,
                EditableMaximum: stunTrack,
                ThresholdOffset: ReadInt(root, "stunThresholdOffset"),
                NaturalRecovery: ReadString(root, "stunNaturalRecovery"),
                ActsAsAlternateTrack: actsAsMatrix));
        }

        return tracks.Count == 0
            ? null
            : new ConditionMonitorEditorState(ReadBool(root, "created"), tracks);
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

    private static int ReadInt(JsonObject source, string propertyName)
    {
        JsonNode? node = ReadNode(source, propertyName);
        if (node is JsonValue value && value.TryGetValue(out int result))
        {
            return result;
        }

        string text = node?.ToJsonString().Trim('"') ?? string.Empty;
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
    }

    private static bool ReadBool(JsonObject source, string propertyName)
    {
        JsonNode? node = ReadNode(source, propertyName);
        if (node is JsonValue value && value.TryGetValue(out bool result))
        {
            return result;
        }

        return bool.TryParse(node?.ToJsonString().Trim('"'), out bool parsed) && parsed;
    }

    private static string ReadString(JsonObject source, string propertyName)
    {
        JsonNode? node = ReadNode(source, propertyName);
        if (node is JsonValue value && value.TryGetValue(out string? result))
        {
            return result ?? string.Empty;
        }

        return node?.ToJsonString().Trim('"') ?? string.Empty;
    }
}
