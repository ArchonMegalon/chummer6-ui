using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public static class SectionRowProjector
{
    public static IReadOnlyList<SectionRowState> BuildRows(JsonNode? node, int? maxRows = null)
    {
        if (node is null)
            return [];

        List<SectionRowState> rows = [];
        Flatten(node, string.Empty, rows);

        if (maxRows is int maxRowCount && rows.Count > maxRowCount)
        {
            return rows.Take(maxRowCount).ToArray();
        }

        return rows;
    }

    private static void Flatten(JsonNode node, string path, List<SectionRowState> rows)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string key, JsonNode? child) in obj)
                {
                    if (child is null)
                        continue;

                    string nextPath = string.IsNullOrWhiteSpace(path) ? key : $"{path}.{key}";
                    Flatten(child, nextPath, rows);
                }

                return;
            case JsonArray array:
                if (array.Count == 0)
                {
                    rows.Add(new SectionRowState(path, "[]"));
                    return;
                }

                bool simpleArray = array.All(item => item is null or JsonValue);
                if (simpleArray)
                {
                    string value = string.Join(", ", array.Select(item => item?.ToJsonString() ?? "null"));
                    rows.Add(new SectionRowState(path, value));
                    return;
                }

                for (int index = 0; index < array.Count; index++)
                {
                    JsonNode? child = array[index];
                    if (child is null)
                        continue;

                    string nextPath = $"{path}[{index}]";
                    Flatten(child, nextPath, rows);
                }

                return;
            case JsonValue value:
                rows.Add(new SectionRowState(path, value.ToJsonString()));
                return;
        }
    }
}
