using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public static class SectionRowProjector
{
    private static readonly string[] SummaryScalarPropertyOrder =
    [
        "count",
        "knowledgeCount",
        "gearCount",
        "weaponCount",
        "armorCount",
        "cyberwareCount",
        "vehicleCount",
        "karma",
        "nuyen",
        "streetCred",
        "notoriety",
        "publicAwareness",
        "buildMethod",
        "gameEdition",
        "settings"
    ];

    public static IReadOnlyList<SectionRowState> BuildRows(JsonNode? node, int? maxRows = null)
        => BuildRows(null, node, maxRows);

    public static IReadOnlyList<SectionRowState> BuildRows(string? sectionId, JsonNode? node, int? maxRows = null)
    {
        if (node is null)
            return [];

        IReadOnlyList<SectionRowState> rows = TryBuildStructuredRows(sectionId, node) ?? BuildGenericRows(node);

        if (maxRows is int maxRowCount && rows.Count > maxRowCount)
        {
            return rows.Take(maxRowCount).ToArray();
        }

        return rows;
    }

    private static IReadOnlyList<SectionRowState> BuildGenericRows(JsonNode node)
    {
        List<SectionRowState> rows = [];
        Flatten(node, string.Empty, rows);
        return rows;
    }

    private static IReadOnlyList<SectionRowState>? TryBuildStructuredRows(string? sectionId, JsonNode node)
    {
        if (node is not JsonObject root)
        {
            return null;
        }

        string? normalizedSectionId = Normalize(sectionId);
        if (string.Equals(normalizedSectionId, "inventory", StringComparison.Ordinal))
        {
            return BuildInventoryRows(root);
        }

        string? collectionProperty = ResolveCollectionProperty(root, normalizedSectionId);
        if (collectionProperty is null)
        {
            return null;
        }

        List<SectionRowState> rows = [];
        AppendSummaryScalarRows(root, collectionProperty, rows);
        if (!TryGetPropertyValueIgnoreCase(root, collectionProperty, out _, out JsonNode? collectionNode)
            || collectionNode is not JsonArray collection)
        {
            return rows.Count > 0 ? rows : null;
        }

        if (collection.Count == 0)
        {
            rows.Add(new SectionRowState(collectionProperty, "No entries"));
            return rows;
        }

        for (int index = 0; index < collection.Count; index++)
        {
            JsonNode? item = collection[index];
            if (item is null)
            {
                continue;
            }

            rows.Add(new SectionRowState(
                $"{collectionProperty}[{index}]",
                SummarizeCollectionItem(collectionProperty, index, item)));
        }

        return rows.Count > 0 ? rows : null;
    }

    private static IReadOnlyList<SectionRowState>? BuildInventoryRows(JsonObject root)
    {
        List<SectionRowState> rows = [];
        AppendInventoryCategoryRows(root, "gear", "gearCount", "gearNames", rows);
        AppendInventoryCategoryRows(root, "weapons", "weaponCount", "weaponNames", rows);
        AppendInventoryCategoryRows(root, "armors", "armorCount", "armorNames", rows);
        AppendInventoryCategoryRows(root, "cyberwares", "cyberwareCount", "cyberwareNames", rows);
        AppendInventoryCategoryRows(root, "vehicles", "vehicleCount", "vehicleNames", rows);
        return rows.Count > 0 ? rows : null;
    }

    private static void AppendInventoryCategoryRows(
        JsonObject root,
        string itemPath,
        string countProperty,
        string namesProperty,
        List<SectionRowState> rows)
    {
        string count = ReadScalarProperty(root, countProperty);
        if (!string.IsNullOrWhiteSpace(count))
        {
            rows.Add(new SectionRowState(countProperty, count));
        }

        if (!TryGetPropertyValueIgnoreCase(root, namesProperty, out _, out JsonNode? namesNode)
            || namesNode is not JsonArray names)
        {
            return;
        }

        for (int index = 0; index < names.Count; index++)
        {
            string value = SanitizeValue(names[index]);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            rows.Add(new SectionRowState($"{itemPath}[{index}]", value));
        }
    }

    private static void AppendSummaryScalarRows(JsonObject root, string collectionProperty, List<SectionRowState> rows)
    {
        HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);
        foreach (string propertyName in SummaryScalarPropertyOrder)
        {
            if (string.Equals(propertyName, collectionProperty, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = ReadScalarProperty(root, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            rows.Add(new SectionRowState(propertyName, value));
            emitted.Add(propertyName);
        }

        foreach ((string propertyName, JsonNode? valueNode) in root)
        {
            if (valueNode is not JsonValue
                || emitted.Contains(propertyName)
                || string.Equals(propertyName, collectionProperty, StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "section", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "sectionId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = SanitizeValue(valueNode);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            rows.Add(new SectionRowState(propertyName, value));
            if (rows.Count >= 4)
            {
                return;
            }
        }
    }

    private static string? ResolveCollectionProperty(JsonObject root, string? normalizedSectionId)
    {
        foreach (string candidate in GetCollectionCandidates(normalizedSectionId))
        {
            if (TryGetPropertyValueIgnoreCase(root, candidate, out string actualName, out JsonNode? value)
                && value is JsonArray)
            {
                return actualName;
            }
        }

        foreach ((string propertyName, JsonNode? value) in root)
        {
            if (value is JsonArray)
            {
                return propertyName;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCollectionCandidates(string? normalizedSectionId)
    {
        if (!string.IsNullOrWhiteSpace(normalizedSectionId))
        {
            yield return normalizedSectionId;
        }

        switch (normalizedSectionId)
        {
            case "powers":
                yield return "adeptPowers";
                break;
            case "complexforms":
                yield return "complexForms";
                break;
            case "attributes":
            case "attributedetails":
                yield return "attributes";
                break;
            case "progress":
            case "calendar":
                yield return "diary";
                yield return "entries";
                break;
            case "initiationgrades":
                yield return "grades";
                break;
            case "mentorspirits":
                yield return "mentorSpirits";
                yield return "mentors";
                break;
            case "drugs":
                yield return "consumables";
                break;
        }
    }

    private static string SummarizeCollectionItem(string collectionProperty, int index, JsonNode item)
    {
        if (item is JsonValue scalar)
        {
            return scalar.ToJsonString();
        }

        if (item is not JsonObject obj)
        {
            return item.ToJsonString();
        }

        string? specializedSummary = Normalize(collectionProperty) switch
        {
            "attributes" => SummarizeAttribute(obj),
            "skills" => SummarizeSkill(obj),
            "qualities" => SummarizeQuality(obj),
            "contacts" => SummarizeContact(obj),
            "gear" => SummarizeGear(obj),
            "weapons" => SummarizeWeapon(obj),
            "armors" => SummarizeArmor(obj),
            "cyberwares" => SummarizeCyberware(obj),
            "vehicles" => SummarizeVehicle(obj),
            "vehiclemods" => SummarizeVehicleMod(obj),
            "spells" => SummarizeSpell(obj),
            "powers" => SummarizePower(obj),
            "complexforms" => SummarizeComplexForm(obj),
            "drugs" => SummarizeDrug(obj),
            "progress" or "calendar" or "diary" => SummarizeProgressEntry(obj),
            "initiationgrades" or "grades" => SummarizeInitiationGrade(obj),
            "mentorspirits" => SummarizeMentorSpirit(obj),
            _ => null
        };
        if (!string.IsNullOrWhiteSpace(specializedSummary))
        {
            return specializedSummary;
        }

        List<string> parts = [];
        string title = FirstNonBlank(
            ReadScalarProperty(obj, "name"),
            ReadScalarProperty(obj, "title"),
            ReadScalarProperty(obj, "label"),
            ReadScalarProperty(obj, "date"));
        parts.Add(string.IsNullOrWhiteSpace(title)
            ? $"{FormatFallbackTitle(collectionProperty)} {index + 1}"
            : title);

        foreach (string propertyName in GetPreferredSummaryProperties(collectionProperty))
        {
            if (string.Equals(propertyName, "name", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "title", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "label", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "date", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string segment = BuildSummarySegment(obj, propertyName);
            if (!string.IsNullOrWhiteSpace(segment))
            {
                parts.Add(segment);
            }
        }

        if (parts.Count == 1)
        {
            foreach ((string propertyName, JsonNode? valueNode) in obj)
            {
                if (valueNode is null
                    || string.Equals(propertyName, "guid", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(propertyName, "suid", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(propertyName, "name", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(propertyName, "title", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string segment = BuildSummarySegment(obj, propertyName);
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    parts.Add(segment);
                }

                if (parts.Count >= 4)
                {
                    break;
                }
            }
        }

        return string.Join(" · ", parts.Distinct(StringComparer.Ordinal));
    }

    private static string? SummarizeAttribute(JsonObject obj)
    {
        string name = FirstNonBlank(ReadScalarProperty(obj, "name"), ReadScalarProperty(obj, "label"));
        string baseValue = FirstNonBlank(ReadScalarProperty(obj, "baseValue"), ReadScalarProperty(obj, "base"));
        string totalValue = FirstNonBlank(ReadScalarProperty(obj, "totalValue"), ReadScalarProperty(obj, "value"));
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(name))
        {
            parts.Add(name);
        }

        if (!string.IsNullOrWhiteSpace(totalValue))
        {
            parts.Add(string.Equals(baseValue, totalValue, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(baseValue)
                ? $"Total {totalValue}"
                : $"Base {baseValue} · Total {totalValue}");
        }
        else if (!string.IsNullOrWhiteSpace(baseValue))
        {
            parts.Add($"Base {baseValue}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string? SummarizeSkill(JsonObject obj)
    {
        string name = FirstNonBlank(ReadScalarProperty(obj, "name"), ReadScalarProperty(obj, "suid"));
        List<string> parts = [name];
        string baseValue = FirstNonBlank(ReadScalarProperty(obj, "baseValue"), ReadScalarProperty(obj, "rating"), ReadScalarProperty(obj, "base"));
        if (!string.IsNullOrWhiteSpace(baseValue))
        {
            parts.Add($"Rating {baseValue}");
        }

        if (bool.TryParse(ReadScalarProperty(obj, "isKnowledge"), out bool isKnowledge) && isKnowledge)
        {
            parts.Add("Knowledge");
        }

        string category = ReadScalarProperty(obj, "category");
        if (!string.IsNullOrWhiteSpace(category))
        {
            parts.Add(category);
        }

        string specs = JoinArray(obj, "specializations");
        if (!string.IsNullOrWhiteSpace(specs))
        {
            parts.Add($"Specs {specs}");
        }

        return JoinParts(parts);
    }

    private static string? SummarizeQuality(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        string category = FirstNonBlank(ReadScalarProperty(obj, "category"), ReadScalarProperty(obj, "type"));
        if (!string.IsNullOrWhiteSpace(category))
        {
            parts.Add(category);
        }

        string bp = ReadScalarProperty(obj, "bp");
        if (!string.IsNullOrWhiteSpace(bp))
        {
            parts.Add($"{bp} BP");
        }

        string source = ReadScalarProperty(obj, "source");
        if (!string.IsNullOrWhiteSpace(source))
        {
            parts.Add(source);
        }

        return JoinParts(parts);
    }

    private static string? SummarizeContact(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        string role = ReadScalarProperty(obj, "role");
        string location = ReadScalarProperty(obj, "location");
        if (!string.IsNullOrWhiteSpace(role))
        {
            parts.Add(role);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            parts.Add(location);
        }

        string connection = ReadScalarProperty(obj, "connection");
        string loyalty = ReadScalarProperty(obj, "loyalty");
        if (!string.IsNullOrWhiteSpace(connection) || !string.IsNullOrWhiteSpace(loyalty))
        {
            parts.Add($"Conn {FirstNonBlank(connection, "0")} / Loy {FirstNonBlank(loyalty, "0")}");
        }

        return JoinParts(parts);
    }

    private static string? SummarizeGear(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendIfPresent(parts, ReadScalarProperty(obj, "category"));
        AppendLabeled(parts, "Rating", ReadScalarProperty(obj, "rating"));
        AppendLabeled(parts, "Qty", FirstNonBlank(ReadScalarProperty(obj, "quantity"), ReadScalarProperty(obj, "qty")));
        AppendLabeled(parts, "Cost", ReadScalarProperty(obj, "cost"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "location"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "source"));
        return JoinParts(parts);
    }

    private static string? SummarizeWeapon(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        string damage = ReadScalarProperty(obj, "damage");
        string ap = ReadScalarProperty(obj, "ap");
        if (!string.IsNullOrWhiteSpace(damage) || !string.IsNullOrWhiteSpace(ap))
        {
            parts.Add(string.IsNullOrWhiteSpace(ap) ? damage : $"{damage} AP {ap}".Trim());
        }

        AppendLabeled(parts, "Acc", ReadScalarProperty(obj, "accuracy"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "mode"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "category"));
        return JoinParts(parts);
    }

    private static string? SummarizeArmor(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendLabeled(parts, "Armor", FirstNonBlank(ReadScalarProperty(obj, "armorValue"), ReadScalarProperty(obj, "armor")));
        AppendLabeled(parts, "Rating", ReadScalarProperty(obj, "rating"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "category"));
        AppendLabeled(parts, "Cost", ReadScalarProperty(obj, "cost"));
        return JoinParts(parts);
    }

    private static string? SummarizeCyberware(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendLabeled(parts, "Essence", ReadScalarProperty(obj, "essence"));
        AppendLabeled(parts, "Capacity", ReadScalarProperty(obj, "capacity"));
        AppendLabeled(parts, "Rating", ReadScalarProperty(obj, "rating"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "grade"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "location"));
        return JoinParts(parts);
    }

    private static string? SummarizeVehicle(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendIfPresent(parts, ReadScalarProperty(obj, "category"));
        AppendLabeled(parts, "Armor", ReadScalarProperty(obj, "armor"));
        AppendLabeled(parts, "Handling", ReadScalarProperty(obj, "handling"));
        AppendLabeled(parts, "Speed", ReadScalarProperty(obj, "speed"));
        AppendLabeled(parts, "Seats", ReadScalarProperty(obj, "seats"));
        AppendLabeled(parts, "Cost", ReadScalarProperty(obj, "cost"));
        return JoinParts(parts);
    }

    private static string? SummarizeVehicleMod(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendIfPresent(parts, ReadScalarProperty(obj, "category"));
        AppendLabeled(parts, "Slots", ReadScalarProperty(obj, "slots"));
        AppendLabeled(parts, "Rating", ReadScalarProperty(obj, "rating"));
        return JoinParts(parts);
    }

    private static string? SummarizeSpell(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendIfPresent(parts, ReadScalarProperty(obj, "category"));
        AppendLabeled(parts, "Drain", FirstNonBlank(ReadScalarProperty(obj, "drainValue"), ReadScalarProperty(obj, "dv")));
        AppendIfPresent(parts, ReadScalarProperty(obj, "type"));
        return JoinParts(parts);
    }

    private static string? SummarizePower(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendLabeled(parts, "Rating", ReadScalarProperty(obj, "rating"));
        string pointsPerLevel = ReadScalarProperty(obj, "pointsPerLevel");
        if (!string.IsNullOrWhiteSpace(pointsPerLevel))
        {
            parts.Add($"{pointsPerLevel} PP");
        }

        AppendIfPresent(parts, ReadScalarProperty(obj, "source"));
        return JoinParts(parts);
    }

    private static string? SummarizeComplexForm(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendIfPresent(parts, ReadScalarProperty(obj, "target"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "duration"));
        AppendLabeled(parts, "Fading", FirstNonBlank(ReadScalarProperty(obj, "fadingValue"), ReadScalarProperty(obj, "fv")));
        return JoinParts(parts);
    }

    private static string? SummarizeDrug(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendIfPresent(parts, ReadScalarProperty(obj, "category"));
        AppendLabeled(parts, "Rating", ReadScalarProperty(obj, "rating"));
        AppendLabeled(parts, "Qty", FirstNonBlank(ReadScalarProperty(obj, "quantity"), ReadScalarProperty(obj, "qty")));
        AppendIfPresent(parts, ReadScalarProperty(obj, "source"));
        return JoinParts(parts);
    }

    private static string? SummarizeProgressEntry(JsonObject obj)
    {
        string title = FirstNonBlank(
            ReadScalarProperty(obj, "title"),
            ReadScalarProperty(obj, "name"),
            ReadScalarProperty(obj, "label"),
            ReadScalarProperty(obj, "date"));
        List<string> parts = [title];
        string date = ReadScalarProperty(obj, "date");
        if (!string.IsNullOrWhiteSpace(date) && !string.Equals(date, title, StringComparison.Ordinal))
        {
            parts.Add(date);
        }

        string karma = ReadScalarProperty(obj, "karma");
        if (!string.IsNullOrWhiteSpace(karma))
        {
            parts.Add($"+{karma} karma");
        }

        string nuyen = ReadScalarProperty(obj, "nuyen");
        if (!string.IsNullOrWhiteSpace(nuyen))
        {
            parts.Add($"¥{nuyen}");
        }

        return JoinParts(parts);
    }

    private static string? SummarizeInitiationGrade(JsonObject obj)
    {
        string grade = ReadScalarProperty(obj, "grade");
        List<string> parts = [string.IsNullOrWhiteSpace(grade) ? "Initiation Grade" : $"Grade {grade}"];
        if (bool.TryParse(ReadScalarProperty(obj, "res"), out bool isRes) && isRes)
        {
            parts.Add("Submersion");
        }

        if (bool.TryParse(ReadScalarProperty(obj, "group"), out bool group) && group)
        {
            parts.Add("Group");
        }

        if (bool.TryParse(ReadScalarProperty(obj, "ordeal"), out bool ordeal) && ordeal)
        {
            parts.Add("Ordeal");
        }

        if (bool.TryParse(ReadScalarProperty(obj, "schooling"), out bool schooling) && schooling)
        {
            parts.Add("Schooling");
        }

        AppendIfPresent(parts, ReadScalarProperty(obj, "reward"));
        return JoinParts(parts);
    }

    private static string? SummarizeMentorSpirit(JsonObject obj)
    {
        string name = ReadScalarProperty(obj, "name");
        List<string> parts = [name];
        AppendIfPresent(parts, ReadScalarProperty(obj, "mentorType"));
        AppendIfPresent(parts, ReadScalarProperty(obj, "source"));
        return JoinParts(parts);
    }

    private static void AppendIfPresent(List<string> parts, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value);
        }
    }

    private static void AppendLabeled(List<string> parts, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label} {value}");
        }
    }

    private static string JoinArray(JsonObject source, string propertyName)
    {
        if (!TryGetPropertyValueIgnoreCase(source, propertyName, out _, out JsonNode? node)
            || node is not JsonArray array)
        {
            return string.Empty;
        }

        return string.Join(", ", array
            .Select(SanitizeValue)
            .Where(static value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? JoinParts(IEnumerable<string> parts)
    {
        string[] filtered = parts
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return filtered.Length == 0 ? null : string.Join(" · ", filtered);
    }

    private static IEnumerable<string> GetPreferredSummaryProperties(string collectionProperty)
    {
        switch (Normalize(collectionProperty))
        {
            case "attributes":
                return ["baseValue", "totalValue", "metatypeMin", "metatypeMax", "metatypeAugMax"];
            case "skills":
                return ["category", "baseValue", "karmaValue", "isKnowledge", "specializations"];
            case "qualities":
                return ["bp", "source"];
            case "contacts":
                return ["role", "location", "connection", "loyalty"];
            case "gear":
                return ["category", "rating", "quantity", "qty", "cost", "location", "source"];
            case "weapons":
                return ["category", "damage", "ap", "accuracy", "mode"];
            case "armors":
                return ["category", "armorValue", "rating", "cost"];
            case "cyberwares":
                return ["category", "essence", "capacity", "rating", "grade", "location"];
            case "vehicles":
                return ["category", "handling", "speed", "armor", "seats"];
            case "vehiclemods":
                return ["category", "slots", "rating", "cost"];
            case "spells":
                return ["category", "type", "range", "duration", "drainValue"];
            case "powers":
                return ["rating", "pointsPerLevel", "source"];
            case "complexforms":
                return ["target", "duration", "fadingValue", "source"];
            case "drugs":
                return ["category", "quantity", "qty", "duration", "availability", "source"];
            case "calendar":
            case "diary":
            case "progress":
                return ["date", "karma", "nuyen"];
            case "initiationgrades":
            case "grades":
                return ["grade", "reward", "source"];
            default:
                return ["category", "type", "role", "rating", "level", "cost", "source", "location"];
        }
    }

    private static string BuildSummarySegment(JsonObject source, string propertyName)
    {
        if (!TryGetPropertyValueIgnoreCase(source, propertyName, out _, out JsonNode? node)
            || node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue)
        {
            string text = SanitizeValue(node);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (bool.TryParse(text, out bool boolValue))
            {
                return boolValue ? FormatSummaryLabel(propertyName) : string.Empty;
            }

            return $"{FormatSummaryLabel(propertyName)} {text}";
        }

        if (node is JsonArray array)
        {
            string[] values = array
                .Select(SanitizeValue)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (values.Length == 0)
            {
                return string.Empty;
            }

            return $"{FormatSummaryLabel(propertyName)} {string.Join(", ", values)}";
        }

        return string.Empty;
    }

    private static string FormatSummaryLabel(string propertyName)
        => Normalize(propertyName) switch
        {
            "basevalue" => "Base",
            "totalvalue" => "Total",
            "karmavalue" => "Karma",
            "isknowledge" => "Knowledge",
            "specializations" => "Specs",
            "bp" => "BP",
            "qty" => "Qty",
            "armorvalue" => "Armor",
            "drainvalue" => "Drain",
            "pointsperlevel" => "PP",
            "programtype" => "Type",
            "publicawareness" => "Public Awareness",
            "streetcred" => "Street Cred",
            _ => propertyName
        };

    private static string FormatFallbackTitle(string collectionProperty)
        => Normalize(collectionProperty) switch
        {
            "attributes" => "Attribute",
            "skills" => "Skill",
            "qualities" => "Quality",
            "contacts" => "Contact",
            "cyberwares" => "Cyberware",
            "armors" => "Armor",
            "weapons" => "Weapon",
            "vehicles" => "Vehicle",
            "complexforms" => "Complex Form",
            "initiationgrades" or "grades" => "Grade",
            _ => collectionProperty
        };

    private static string ReadScalarProperty(JsonObject source, string propertyName)
        => TryGetPropertyValueIgnoreCase(source, propertyName, out _, out JsonNode? node)
            ? SanitizeValue(node)
            : string.Empty;

    private static bool TryGetPropertyValueIgnoreCase(
        JsonObject source,
        string propertyName,
        out string actualName,
        out JsonNode? node)
    {
        foreach ((string key, JsonNode? value) in source)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                actualName = key;
                node = value;
                return true;
            }
        }

        actualName = string.Empty;
        node = null;
        return false;
    }

    private static string SanitizeValue(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            string raw = value.ToJsonString();
            return raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"'
                ? raw[1..^1]
                : raw;
        }

        return node.ToJsonString();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();

    private static string FirstNonBlank(params string[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

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
                    rows.Add(new SectionRowState(path, "No entries"));
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
