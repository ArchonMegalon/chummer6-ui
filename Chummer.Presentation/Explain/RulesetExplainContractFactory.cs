using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chummer.Contracts.Rulesets;

namespace Chummer.Presentation.Explain;

public sealed record ExplainTextSeed(
    string Key,
    IReadOnlyDictionary<string, string>? Parameters = null,
    string? FallbackText = null);

public sealed record ExplainProvenanceSeed(
    string Id,
    ExplainTextSeed Label);

public sealed record ExplainFragmentSeed(
    ExplainTextSeed Label,
    string? Value,
    ExplainTextSeed? Reason = null,
    ExplainProvenanceSeed? Pack = null,
    ExplainProvenanceSeed? Provider = null);

public sealed record ExplainProviderSeed(
    ExplainProvenanceSeed Provider,
    ExplainProvenanceSeed Capability,
    ExplainProvenanceSeed? Pack,
    bool Success,
    IReadOnlyList<ExplainFragmentSeed> Fragments,
    RulesetGasUsage GasUsage,
    IReadOnlyList<ExplainTextSeed> Messages);

public sealed record ExplainTraceSeed(
    ExplainTextSeed Subject,
    IReadOnlyList<ExplainProviderSeed> Providers,
    IReadOnlyList<ExplainTextSeed> Messages,
    RulesetGasUsage AggregateGasUsage);

public static class RulesetExplainContractFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static RulesetExplainTrace CreateTrace(ExplainTraceSeed seed)
        => JsonSerializer.Deserialize<RulesetExplainTrace>(
            BuildTraceNode(seed).ToJsonString(),
            SerializerOptions)
        ?? throw new InvalidOperationException("Failed to materialize RulesetExplainTrace from explain seed.");

    private static JsonObject BuildTraceNode(ExplainTraceSeed seed)
    {
        PropertyInfo[] properties = typeof(RulesetExplainTrace).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        JsonObject node = [];

        SetProperty(node, properties, BuildTextNode(seed.Subject, GetPropertyType(properties, "SubjectId", "Subject"), keyedStringFallback: true), "SubjectId", "Subject");
        SetProperty(node, properties, BuildArrayNode(seed.Providers, GetPropertyType(properties, "Providers"), BuildProviderNode), "Providers");
        SetProperty(node, properties, BuildArrayNode(seed.Messages, GetPropertyType(properties, "Messages"), (item, itemType) => BuildTextNode(item, itemType, keyedStringFallback: true)), "Messages");
        SetProperty(node, properties, JsonSerializer.SerializeToNode(seed.AggregateGasUsage, SerializerOptions), "AggregateGasUsage");

        return node;
    }

    private static JsonNode? BuildProviderNode(ExplainProviderSeed seed, Type providerType)
    {
        PropertyInfo[] properties = providerType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        JsonObject node = [];

        SetProperty(node, properties, BuildProvenanceNode(seed.Provider, GetPropertyType(properties, "ProviderId", "Provider"), keyedStringFallback: true), "ProviderId", "Provider");
        SetProperty(node, properties, BuildProvenanceNode(seed.Capability, GetPropertyType(properties, "CapabilityId", "Capability"), keyedStringFallback: true), "CapabilityId", "Capability");
        SetProperty(node, properties, BuildProvenanceNode(seed.Pack, GetPropertyType(properties, "PackId", "Pack"), keyedStringFallback: true), "PackId", "Pack");
        SetProperty(node, properties, JsonValue.Create(seed.Success), "Success");
        SetProperty(node, properties, BuildArrayNode(seed.Fragments, GetPropertyType(properties, "ExplainFragments"), BuildFragmentNode), "ExplainFragments");
        SetProperty(node, properties, JsonSerializer.SerializeToNode(seed.GasUsage, SerializerOptions), "GasUsage");
        SetProperty(node, properties, BuildArrayNode(seed.Messages, GetPropertyType(properties, "Messages"), (item, itemType) => BuildTextNode(item, itemType, keyedStringFallback: true)), "Messages");

        return node;
    }

    private static JsonNode? BuildFragmentNode(ExplainFragmentSeed seed, Type fragmentType)
    {
        PropertyInfo[] properties = fragmentType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        JsonObject node = [];

        SetProperty(node, properties, BuildTextNode(seed.Label, GetPropertyType(properties, "Label"), keyedStringFallback: true), "Label");
        SetProperty(node, properties, BuildLiteralNode(seed.Value, GetPropertyType(properties, "Value")), "Value");
        SetProperty(node, properties, BuildTextNode(seed.Reason, GetPropertyType(properties, "Reason"), keyedStringFallback: true), "Reason");
        SetProperty(node, properties, BuildProvenanceNode(seed.Pack, GetPropertyType(properties, "PackId", "Pack"), keyedStringFallback: true), "PackId", "Pack");
        SetProperty(node, properties, BuildProvenanceNode(seed.Provider, GetPropertyType(properties, "ProviderId", "Provider"), keyedStringFallback: true), "ProviderId", "Provider");

        return node;
    }

    private static JsonNode? BuildArrayNode<TItem>(
        IEnumerable<TItem> items,
        Type targetCollectionType,
        Func<TItem, Type, JsonNode?> itemBuilder)
    {
        Type itemType = GetCollectionElementType(targetCollectionType);
        JsonArray array = [];
        foreach (TItem item in items)
        {
            array.Add(itemBuilder(item, itemType));
        }

        return array;
    }

    private static JsonNode? BuildTextNode(ExplainTextSeed? seed, Type targetType, bool keyedStringFallback)
    {
        if (seed is null)
        {
            return null;
        }

        if (targetType == typeof(string))
        {
            return JsonValue.Create(keyedStringFallback ? seed.Key : seed.FallbackText ?? seed.Key);
        }

        JsonObject node = [];
        bool populated = false;

        populated |= TrySetStringProperty(node, targetType, seed.Key, "LocalizationKey", "Key", "Token", "ResourceKey");
        populated |= TrySetParametersProperty(node, targetType, seed.Parameters, "Parameters", "Arguments", "Tokens");
        populated |= TrySetStringProperty(node, targetType, seed.FallbackText, "FallbackText", "Text", "Value", "DefaultText");

        return populated ? node : JsonValue.Create(seed.FallbackText ?? seed.Key);
    }

    private static JsonNode? BuildLiteralNode(string? value, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (targetType == typeof(string))
        {
            return JsonValue.Create(value);
        }

        JsonObject node = [];
        bool populated = false;
        populated |= TrySetStringProperty(node, targetType, value, "Text", "Value", "FallbackText", "DefaultText");
        return populated ? node : JsonValue.Create(value);
    }

    private static JsonNode? BuildProvenanceNode(ExplainProvenanceSeed? seed, Type targetType, bool keyedStringFallback)
    {
        if (seed is null)
        {
            return null;
        }

        if (targetType == typeof(string))
        {
            return JsonValue.Create(keyedStringFallback ? seed.Id : seed.Label.FallbackText ?? seed.Label.Key);
        }

        JsonObject node = [];
        bool populated = false;
        populated |= TrySetStringProperty(node, targetType, seed.Id, "Id", "ProviderId", "CapabilityId", "PackId", "Value");

        PropertyInfo? labelProperty = FindProperty(targetType, "Label", "DisplayName", "Name", "Title", "Text", "Localization");
        if (labelProperty is not null)
        {
            node[labelProperty.Name] = BuildTextNode(seed.Label, labelProperty.PropertyType, keyedStringFallback);
            populated = true;
        }
        else
        {
            populated |= TrySetStringProperty(node, targetType, seed.Label.FallbackText ?? seed.Label.Key, "Text", "Display");
            populated |= TrySetStringProperty(node, targetType, seed.Label.Key, "LocalizationKey", "Key", "Token");
        }

        return populated ? node : JsonValue.Create(seed.Id);
    }

    private static void SetProperty(JsonObject node, IReadOnlyList<PropertyInfo> properties, JsonNode? value, params string[] propertyNames)
    {
        PropertyInfo? property = FindProperty(properties, propertyNames);
        if (property is not null)
        {
            node[property.Name] = value;
        }
    }

    private static Type GetPropertyType(IReadOnlyList<PropertyInfo> properties, params string[] propertyNames)
        => FindProperty(properties, propertyNames)?.PropertyType
        ?? throw new InvalidOperationException($"Explain contract property '{string.Join("' or '", propertyNames)}' was not found.");

    private static PropertyInfo? FindProperty(Type type, params string[] names)
        => FindProperty(type.GetProperties(BindingFlags.Instance | BindingFlags.Public), names);

    private static PropertyInfo? FindProperty(IReadOnlyList<PropertyInfo> properties, params string[] names)
        => properties.FirstOrDefault(property => names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)));

    private static Type GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType()
                ?? throw new InvalidOperationException($"Collection type '{collectionType}' does not define an element type.");
        }

        if (collectionType.IsGenericType)
        {
            return collectionType.GetGenericArguments()[0];
        }

        Type? enumerableInterface = collectionType
            .GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerableInterface is not null)
        {
            return enumerableInterface.GetGenericArguments()[0];
        }

        throw new InvalidOperationException($"Collection type '{collectionType}' does not expose a generic element type.");
    }

    private static bool TrySetStringProperty(JsonObject node, Type targetType, string? value, params string[] propertyNames)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        PropertyInfo? property = FindProperty(targetType, propertyNames);
        if (property is null)
        {
            return false;
        }

        node[property.Name] = JsonValue.Create(value);
        return true;
    }

    private static bool TrySetParametersProperty(JsonObject node, Type targetType, IReadOnlyDictionary<string, string>? parameters, params string[] propertyNames)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return false;
        }

        PropertyInfo? property = FindProperty(targetType, propertyNames);
        if (property is null)
        {
            return false;
        }

        JsonObject parameterNode = [];
        foreach (KeyValuePair<string, string> parameter in parameters)
        {
            parameterNode[parameter.Key] = JsonValue.Create(parameter.Value);
        }

        node[property.Name] = parameterNode;
        return true;
    }
}
