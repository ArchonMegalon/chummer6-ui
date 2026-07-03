using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Chummer.Contracts.Rulesets;

namespace Chummer.Presentation.Overview;

public static class AttributeWorkbenchProjector
{
    public static bool IsAttributeSection(string? sectionId)
        => string.Equals(NormalizeSectionId(sectionId), "attributes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeSectionId(sectionId), "attributedetails", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<AttributeWorkbenchRow> BuildRows(string? sectionId, string previewJson)
    {
        if (!IsAttributeSection(sectionId) || string.IsNullOrWhiteSpace(previewJson))
        {
            return [];
        }

        JsonObject? root = TryParseRootObject(previewJson);
        return BuildRows(root);
    }

    public static IReadOnlyList<AttributeWorkbenchRow> BuildRows(JsonObject? root)
    {
        if (root is null)
        {
            return [];
        }

        List<AttributeWorkbenchRow> projectedRows = [];
        if (ReadArray(root, "attributes") is { Count: > 0 } attributeArray)
        {
            foreach (JsonNode? node in attributeArray)
            {
                if (node is JsonObject attribute
                    && TryReadAttributeRow(attribute, out AttributeWorkbenchRow row))
                {
                    projectedRows.Add(row);
                }
            }
        }
        else if (ReadObject(root, "attributes") is { } attributesObject)
        {
            foreach ((string name, JsonNode? valueNode) in attributesObject)
            {
                if (valueNode is not JsonValue
                    || !int.TryParse(valueNode.ToJsonString().Trim('"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    continue;
                }

                projectedRows.Add(new AttributeWorkbenchRow(
                    AttributeName: name,
                    DisplayName: FormatFullLabel(name),
                    CompactLabel: FormatCompactLabel(name),
                    BaseValue: value,
                    KarmaValue: 0,
                    TotalValue: value,
                    MetatypeMin: 1,
                    MetatypeMax: Math.Max(6, value),
                    MetatypeAugMax: Math.Max(9, value),
                    PriorityMaximum: Math.Max(6, value),
                    KarmaMaximum: Math.Max(0, Math.Max(9, value) - value),
                    BaseUnlocked: true,
                    CareerMode: false,
                    AvailableKarma: 0,
                    UpgradeKarmaCost: -1,
                    CanCareerUpgrade: false));
            }
        }

        return projectedRows
            .OrderBy(static row => GetAttributeSortOrder(row.AttributeName))
            .ThenBy(static row => row.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public static string FormatCompactLabel(string attributeName)
    {
        string trimmed = attributeName.Trim();
        return NormalizeAttributeToken(attributeName) switch
        {
            "body" => "BOD",
            "agility" => "AGI",
            "reaction" => "REA",
            "strength" => "STR",
            "willpower" => "WIL",
            "logic" => "LOG",
            "intuition" => "INT",
            "charisma" => "CHA",
            "edge" => "EDG",
            "magic" => "MAG",
            "resonance" => "RES",
            _ => trimmed.Length <= 3 ? trimmed.ToUpperInvariant() : trimmed[..Math.Min(3, trimmed.Length)].ToUpperInvariant()
        };
    }

    public static string FormatFullLabel(string attributeName)
        => NormalizeAttributeToken(attributeName) switch
        {
            "body" => "Body",
            "agility" => "Agility",
            "reaction" => "Reaction",
            "strength" => "Strength",
            "willpower" => "Willpower",
            "logic" => "Logic",
            "intuition" => "Intuition",
            "charisma" => "Charisma",
            "edge" => "Edge",
            "magic" => "Magic",
            "resonance" => "Resonance",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(attributeName.Trim().ToLowerInvariant())
        };

    public static bool IsEdgeAttribute(string attributeName)
        => string.Equals(NormalizeAttributeToken(attributeName), "edge", StringComparison.Ordinal);

    public static bool CanBurnEdge(AttributeWorkbenchRow row)
        => row.CareerMode && IsEdgeAttribute(row.AttributeName) && row.TotalValue > 0;

    public static bool CanCareerAdvance(AttributeWorkbenchRow row)
        => row.CareerMode && row.CanCareerUpgrade && row.UpgradeKarmaCost > 0;

    public static string FormatDisplayLabel(string attributeName, string? rulesetId)
        => IsSr6Ruleset(rulesetId)
            ? FormatFullLabel(attributeName)
            : FormatCompactLabel(attributeName);

    public static string FormatLimitsDisplay(AttributeWorkbenchRow row, string? rulesetId)
        => $"{row.MetatypeMin} / {row.MetatypeMax} ({row.MetatypeAugMax})";

    public static bool IsSr6Ruleset(string? rulesetId)
        => string.Equals(RulesetDefaults.NormalizeOptional(rulesetId), RulesetDefaults.Sr6, StringComparison.Ordinal);

    private static string NormalizeAttributeToken(string attributeName)
    {
        string normalized = attributeName.Trim().ToLowerInvariant();
        return normalized switch
        {
            "body" or "bod" => "body",
            "agility" or "agi" => "agility",
            "reaction" or "rea" => "reaction",
            "strength" or "str" => "strength",
            "willpower" or "wil" => "willpower",
            "logic" or "log" => "logic",
            "intuition" or "int" => "intuition",
            "charisma" or "cha" => "charisma",
            "edge" or "edg" => "edge",
            "magic" or "mag" => "magic",
            "resonance" or "res" => "resonance",
            _ => normalized
        };
    }

    private static string? NormalizeSectionId(string? sectionId)
        => string.IsNullOrWhiteSpace(sectionId)
            ? null
            : sectionId.Trim().ToLowerInvariant();

    private static JsonObject? TryParseRootObject(string previewJson)
    {
        try
        {
            return JsonNode.Parse(previewJson) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static JsonArray? ReadArray(JsonObject source, string propertyName)
        => source.TryGetPropertyValue(propertyName, out JsonNode? node)
            ? node as JsonArray
            : null;

    private static JsonObject? ReadObject(JsonObject source, string propertyName)
        => source.TryGetPropertyValue(propertyName, out JsonNode? node)
            ? node as JsonObject
            : null;

    private static bool TryReadAttributeRow(JsonObject attribute, out AttributeWorkbenchRow row)
    {
        string attributeName = FirstNonBlank(
            ReadString(attribute, "name"),
            ReadString(attribute, "label"));
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            row = default!;
            return false;
        }

        int baseValue = ReadInt(attribute, "baseValue", ReadInt(attribute, "base", 0));
        int karmaValue = ReadInt(attribute, "karmaValue", ReadInt(attribute, "karma", 0));
        int totalValue = ReadInt(attribute, "totalValue", ReadInt(attribute, "value", baseValue + karmaValue));
        if (karmaValue == 0 && totalValue >= baseValue)
        {
            karmaValue = totalValue - baseValue;
        }

        bool hasMetatypeMin = TryReadInt(attribute, out int parsedMetatypeMin, "metatypeMin", "metatypemin");
        bool hasMetatypeMax = TryReadInt(attribute, out int parsedMetatypeMax, "metatypeMax", "metatypemax");
        bool hasMetatypeAugMax = TryReadInt(attribute, out int parsedMetatypeAugMax, "metatypeAugMax", "metatypeaugmax");
        bool parsedLimits = TryParseLimits(
            FirstNonBlank(
                ReadString(attribute, "limits"),
                ReadString(attribute, "range"),
                ReadString(attribute, "naturalRange")),
            out int limitsMin,
            out int limitsMax,
            out int limitsAugMax);
        int metatypeMin = hasMetatypeMin
            ? parsedMetatypeMin
            : parsedLimits
                ? limitsMin
                : 1;
        int metatypeMax = hasMetatypeMax
            ? parsedMetatypeMax
            : parsedLimits
                ? limitsMax
                : Math.Max(6, totalValue);
        int metatypeAugMax = hasMetatypeAugMax
            ? parsedMetatypeAugMax
            : parsedLimits
                ? Math.Max(limitsAugMax, metatypeMax)
                : Math.Max(metatypeMax + 3, totalValue);
        int priorityMaximum = ReadInt(attribute, "priorityMaximum", ReadInt(attribute, "prioritymaximum", Math.Max(baseValue, metatypeMax)));
        int karmaMaximum = ReadInt(attribute, "karmaMaximum", ReadInt(attribute, "karmamaximum", Math.Max(0, metatypeAugMax - baseValue)));
        bool baseUnlocked = ReadBool(attribute, "baseUnlocked", defaultValue: true);
        bool careerMode = ReadBool(attribute, "created", defaultValue: false);
        int availableKarma = ReadInt(attribute, "availableKarma", 0);
        int upgradeKarmaCost = ReadInt(attribute, "upgradeKarmaCost", careerMode ? ComputeCareerAttributeUpgradeCost(totalValue, metatypeAugMax) : -1);
        bool canCareerUpgrade = ReadBool(
            attribute,
            "canCareerUpgrade",
            defaultValue: careerMode && upgradeKarmaCost > 0 && availableKarma >= upgradeKarmaCost);
        int totalCap = Math.Max(metatypeMax, metatypeAugMax);
        int baseMinimum = Math.Max(0, metatypeMin);

        baseValue = Math.Clamp(baseValue, baseMinimum, Math.Max(baseMinimum, Math.Max(0, priorityMaximum)));
        karmaValue = Math.Clamp(karmaValue, 0, Math.Max(0, karmaMaximum));
        karmaValue = Math.Min(karmaValue, Math.Max(0, totalCap - baseValue));
        baseValue = Math.Clamp(
            baseValue,
            baseMinimum,
            Math.Max(baseMinimum, Math.Min(Math.Max(0, priorityMaximum), totalCap - karmaValue)));

        row = new AttributeWorkbenchRow(
            AttributeName: attributeName,
            DisplayName: FormatFullLabel(attributeName),
            CompactLabel: FormatCompactLabel(attributeName),
            BaseValue: baseValue,
            KarmaValue: karmaValue,
            TotalValue: baseValue + karmaValue,
            MetatypeMin: metatypeMin,
            MetatypeMax: metatypeMax,
            MetatypeAugMax: metatypeAugMax,
            PriorityMaximum: priorityMaximum,
            KarmaMaximum: karmaMaximum,
            BaseUnlocked: baseUnlocked,
            CareerMode: careerMode,
            AvailableKarma: availableKarma,
            UpgradeKarmaCost: upgradeKarmaCost,
            CanCareerUpgrade: canCareerUpgrade);
        return true;
    }

    private static string? ReadString(JsonObject source, string propertyName)
        => source.TryGetPropertyValue(propertyName, out JsonNode? node)
            ? ReadNodeValue(node)
            : null;

    private static int ReadInt(JsonObject source, string propertyName, int defaultValue)
    {
        string? value = ReadString(source, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    private static bool TryReadInt(JsonObject source, out int value, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            string? candidate = ReadString(source, propertyName);
            if (int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool ReadBool(JsonObject source, string propertyName, bool defaultValue)
    {
        string? value = ReadString(source, propertyName);
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static string? ReadNodeValue(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        string raw = node.ToJsonString();
        return raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"'
            ? raw[1..^1]
            : raw;
    }

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static int ComputeCareerAttributeUpgradeCost(int currentValue, int totalMaximum)
    {
        if (currentValue >= totalMaximum)
        {
            return -1;
        }

        int nextRank = Math.Max(1, currentValue + 1);
        return nextRank * 5;
    }

    private static bool TryParseLimits(string? raw, out int minimum, out int maximum, out int augmentedMaximum)
    {
        minimum = 0;
        maximum = 0;
        augmentedMaximum = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string normalized = raw.Trim();
        int slashIndex = normalized.IndexOf('/');
        if (slashIndex <= 0)
        {
            return false;
        }

        if (!TryParseLimitInteger(normalized.AsSpan(0, slashIndex), out minimum))
        {
            return false;
        }

        ReadOnlySpan<char> remainder = normalized.AsSpan(slashIndex + 1).Trim();
        int parenIndex = remainder.IndexOf('(');
        if (parenIndex >= 0)
        {
            ReadOnlySpan<char> maxSpan = remainder[..parenIndex].Trim();
            int closeParenIndex = remainder.IndexOf(')');
            if (!TryParseLimitInteger(maxSpan, out maximum))
            {
                return false;
            }

            if (closeParenIndex <= parenIndex + 1)
            {
                augmentedMaximum = maximum;
                return true;
            }

            ReadOnlySpan<char> augSpan = remainder[(parenIndex + 1)..closeParenIndex].Trim();
            if (!TryParseLimitInteger(augSpan, out augmentedMaximum))
            {
                return false;
            }

            return true;
        }

        if (!TryParseLimitInteger(remainder, out maximum))
        {
            return false;
        }

        augmentedMaximum = maximum;
        return true;
    }

    private static bool TryParseLimitInteger(ReadOnlySpan<char> text, out int value)
        => int.TryParse(text.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static int GetAttributeSortOrder(string attributeName)
        => attributeName.Trim().ToLowerInvariant() switch
        {
            "body" => 0,
            "agility" => 1,
            "reaction" => 2,
            "strength" => 3,
            "willpower" => 4,
            "logic" => 5,
            "intuition" => 6,
            "charisma" => 7,
            "edge" => 8,
            "magic" => 9,
            "resonance" => 10,
            "essence" => 11,
            "initiative" => 12,
            _ => int.MaxValue
        };
}

public sealed record AttributeWorkbenchRow(
    string AttributeName,
    string DisplayName,
    string CompactLabel,
    int BaseValue,
    int KarmaValue,
    int TotalValue,
    int MetatypeMin,
    int MetatypeMax,
    int MetatypeAugMax,
    int PriorityMaximum,
    int KarmaMaximum,
    bool BaseUnlocked,
    bool CareerMode,
    int AvailableKarma,
    int UpgradeKarmaCost,
    bool CanCareerUpgrade)
{
    public int TotalCap => Math.Max(MetatypeMax, MetatypeAugMax);

    public int EffectiveBaseMinimum => Math.Max(0, MetatypeMin);

    public int EffectiveBaseMaximum => Math.Max(
        EffectiveBaseMinimum,
        Math.Min(Math.Max(0, PriorityMaximum), TotalCap - Math.Max(0, KarmaValue)));

    public int EffectiveKarmaMaximum => Math.Max(
        0,
        Math.Min(Math.Max(0, KarmaMaximum), TotalCap - Math.Max(EffectiveBaseMinimum, BaseValue)));
}
