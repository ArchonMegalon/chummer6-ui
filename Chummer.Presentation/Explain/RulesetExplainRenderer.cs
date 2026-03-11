using Chummer.Contracts.Rulesets;
using System.Collections;
using System.Reflection;

namespace Chummer.Presentation.Explain;

public interface IExplainTextLocalization
{
    string Resolve(string key, IReadOnlyDictionary<string, string>? parameters = null);
}

public sealed class MapExplainTextLocalization : IExplainTextLocalization
{
    private readonly IReadOnlyDictionary<string, string> _entries;
    private readonly bool _throwOnMissing;

    public MapExplainTextLocalization(
        IReadOnlyDictionary<string, string> entries,
        bool throwOnMissing = false)
    {
        _entries = entries;
        _throwOnMissing = throwOnMissing;
    }

    public string Resolve(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (!_entries.TryGetValue(key, out string? resolved))
        {
            if (_throwOnMissing)
            {
                throw new KeyNotFoundException($"Missing explain localization key '{key}'.");
            }

            resolved = key;
        }

        if (parameters is null or { Count: 0 })
        {
            return resolved;
        }

        string result = resolved;
        foreach (KeyValuePair<string, string> parameter in parameters)
        {
            result = result.Replace($"{{{parameter.Key}}}", parameter.Value, StringComparison.Ordinal);
        }

        return result;
    }
}

public sealed class IdentityExplainTextLocalization : IExplainTextLocalization
{
    public string Resolve(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (parameters is null or { Count: 0 })
        {
            return key;
        }

        string result = key;
        foreach (KeyValuePair<string, string> parameter in parameters)
        {
            result = result.Replace($"{{{parameter.Key}}}", parameter.Value, System.StringComparison.Ordinal);
        }

        return result;
    }
}

public sealed record LocalizedExplainText(
    string? Key,
    string Text,
    IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record LocalizedExplainChrome(
    LocalizedExplainText Title,
    LocalizedExplainText Empty,
    LocalizedExplainText ValueLabel,
    LocalizedExplainText MissingValue,
    LocalizedExplainText ReasonLabel,
    LocalizedExplainText ProviderLabel,
    LocalizedExplainText CapabilityLabel,
    LocalizedExplainText PackLabel,
    LocalizedExplainText TraceStepsLabel,
    LocalizedExplainText DiffLabel,
    LocalizedExplainText BeforeLabel,
    LocalizedExplainText AfterLabel,
    LocalizedExplainText CloseAction);

public sealed record LocalizedExplainFragment(
    LocalizedExplainText? Label,
    LocalizedExplainText? Value,
    LocalizedExplainText? Reason,
    LocalizedExplainProvenance? Pack,
    LocalizedExplainProvenance? Provider)
{
    public LocalizedExplainText? PackId => Pack?.Label;
    public LocalizedExplainText? ProviderId => Provider?.Label;
}

public sealed record LocalizedExplainProvenance(
    string? Id,
    LocalizedExplainText Label);

public sealed record LocalizedRulesetExplainProvider(
    LocalizedExplainProvenance Provider,
    LocalizedExplainProvenance Capability,
    LocalizedExplainProvenance? Pack,
    LocalizedExplainText? Message,
    IReadOnlyList<LocalizedExplainFragment> Fragments,
    IReadOnlyList<LocalizedExplainStep> Steps,
    IReadOnlyList<LocalizedExplainDiff> Diffs)
{
    public LocalizedExplainText ProviderId => Provider.Label;
    public LocalizedExplainText CapabilityId => Capability.Label;
    public LocalizedExplainText? PackId => Pack?.Label;
}

public sealed record LocalizedExplainStep(
    int Index,
    LocalizedExplainText Title,
    LocalizedExplainText Value,
    IReadOnlyList<LocalizedExplainFact> Facts);

public sealed record LocalizedExplainFact(
    LocalizedExplainText Label,
    LocalizedExplainText Value);

public sealed record LocalizedExplainDiff(
    LocalizedExplainText Label,
    LocalizedExplainText Before,
    LocalizedExplainText After);

public sealed record LocalizedRulesetExplainTrace(
    LocalizedExplainText SubjectId,
    IReadOnlyList<LocalizedExplainText> Messages,
    IReadOnlyList<LocalizedRulesetExplainProvider> Providers);

public static class RulesetExplainRenderer
{
    private static readonly IExplainTextLocalization IdentityLocalization = new IdentityExplainTextLocalization();

    public static LocalizedRulesetExplainTrace? Project(
        RulesetExplainTrace? trace,
        IExplainTextLocalization? localization = null)
    {
        if (trace is null)
        {
            return null;
        }

        IExplainTextLocalization resolver = localization ?? IdentityLocalization;

        return new LocalizedRulesetExplainTrace(
            SubjectId: ResolveText(trace.SubjectId, resolver)
                ?? CreateLiteralText(ReadString(trace.SubjectId))
                ?? new LocalizedExplainText(null, string.Empty),
            Messages: trace.Messages
                .Select(message => ResolveText(message, resolver))
                .OfType<LocalizedExplainText>()
                .ToArray(),
            Providers: trace.Providers
                .Select(provider => new LocalizedRulesetExplainProvider(
                    ResolveProvenance(ReadProperty(provider, "Provider") ?? provider.ProviderId, resolver),
                    ResolveProvenance(ReadProperty(provider, "Capability") ?? provider.CapabilityId, resolver),
                    ResolveProvenance(ReadProperty(provider, "Pack") ?? provider.PackId, resolver),
                    ResolveText(provider.Messages.FirstOrDefault(), resolver),
                    provider.ExplainFragments
                        .Select(fragment => new LocalizedExplainFragment(
                            ResolveText(fragment.Label, resolver),
                            ResolveLiteral(fragment.Value, resolver),
                            ResolveText(fragment.Reason, resolver),
                            ResolveProvenance(ReadProperty(fragment, "Pack") ?? fragment.PackId, resolver),
                            ResolveProvenance(ReadProperty(fragment, "Provider") ?? fragment.ProviderId, resolver)))
                        .ToArray(),
                    CreateSteps(provider, resolver),
                    CreateDiffs(provider, resolver)))
                .ToArray());
    }

    public static LocalizedExplainChrome CreateChrome(IExplainTextLocalization? localization = null)
    {
        IExplainTextLocalization resolver = localization ?? IdentityLocalization;

        return new LocalizedExplainChrome(
            Title: ResolveToken("explain.chrome.title", resolver),
            Empty: ResolveToken("explain.chrome.empty", resolver),
            ValueLabel: ResolveToken("explain.chrome.value_label", resolver),
            MissingValue: ResolveToken("explain.chrome.missing_value", resolver),
            ReasonLabel: ResolveToken("explain.chrome.reason_label", resolver),
            ProviderLabel: ResolveToken("explain.chrome.provider_label", resolver),
            CapabilityLabel: ResolveToken("explain.chrome.capability_label", resolver),
            PackLabel: ResolveToken("explain.chrome.pack_label", resolver),
            TraceStepsLabel: ResolveToken("explain.chrome.trace_steps_label", resolver),
            DiffLabel: ResolveToken("explain.chrome.diff_label", resolver),
            BeforeLabel: ResolveToken("explain.chrome.before_label", resolver),
            AfterLabel: ResolveToken("explain.chrome.after_label", resolver),
            CloseAction: ResolveToken("explain.chrome.close_action", resolver));
    }

    private static LocalizedExplainText ResolveToken(string key, IExplainTextLocalization localization)
        => new(
            Key: key,
            Text: localization.Resolve(key));

    private static LocalizedExplainText? ResolveText(object? source, IExplainTextLocalization localization)
    {
        if (source is null)
        {
            return null;
        }

        if (source is string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? null
                : ResolveToken(text, localization);
        }

        string? key = ReadStringProperty(source, "LocalizationKey", "Key", "Token", "ResourceKey");
        IReadOnlyDictionary<string, string>? parameters = ReadParameters(source);
        string? literalText = ReadStringProperty(source, "Text", "Value", "FallbackText", "DefaultText");

        if (!string.IsNullOrWhiteSpace(key))
        {
            return new LocalizedExplainText(
                Key: key,
                Text: localization.Resolve(key, parameters),
                Parameters: parameters);
        }

        if (string.IsNullOrWhiteSpace(literalText))
        {
            return null;
        }

        return new LocalizedExplainText(
            Key: null,
            Text: literalText,
            Parameters: parameters);
    }

    private static LocalizedExplainText? ResolveLiteral(object? source, IExplainTextLocalization localization)
        => ResolveText(source, localization) ?? CreateLiteralText(ReadString(source));

    private static LocalizedExplainText? CreateLiteralText(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : new LocalizedExplainText(
                Key: null,
                Text: value);

    private static LocalizedExplainProvenance ResolveProvenance(object? source, IExplainTextLocalization localization)
    {
        if (source is null)
        {
            return new LocalizedExplainProvenance(
                Id: null,
                Label: new LocalizedExplainText(null, string.Empty));
        }

        if (source is string value)
        {
            return new LocalizedExplainProvenance(
                Id: value,
                Label: ResolveToken(value, localization));
        }

        string? id = ReadStringProperty(source, "Id", "ProviderId", "CapabilityId", "PackId", "Value");
        object? labelSource = ReadProperty(source, "Label")
            ?? ReadProperty(source, "DisplayName")
            ?? ReadProperty(source, "Name")
            ?? ReadProperty(source, "Title")
            ?? ReadProperty(source, "Text")
            ?? ReadProperty(source, "Localization");

        LocalizedExplainText label = ResolveText(labelSource ?? source, localization)
            ?? CreateLiteralText(id)
            ?? new LocalizedExplainText(null, string.Empty);

        return new LocalizedExplainProvenance(
            Id: id,
            Label: label);
    }

    private static IReadOnlyList<LocalizedExplainStep> CreateSteps(
        RulesetProviderTrace provider,
        IExplainTextLocalization localization)
    {
        if (provider.ExplainFragments.Count == 0)
        {
            return [];
        }

        return provider.ExplainFragments
            .Select((fragment, index) =>
            {
                LocalizedExplainText title = ResolveText(fragment.Label, localization)
                    ?? ResolveToken("explain.chrome.value_label", localization);
                LocalizedExplainText value = ResolveLiteral(fragment.Value, localization)
                    ?? ResolveToken("explain.chrome.missing_value", localization);

                List<LocalizedExplainFact> facts = [];

                if (!string.IsNullOrWhiteSpace(ReadString(fragment.Reason)))
                {
                    facts.Add(new LocalizedExplainFact(
                        ResolveToken("explain.chrome.reason_label", localization),
                        ResolveText(fragment.Reason, localization)
                            ?? ResolveToken("explain.chrome.missing_value", localization)));
                }

                if (!string.IsNullOrWhiteSpace(ReadString(fragment.ProviderId) ?? ReadString(ReadProperty(fragment, "Provider"))))
                {
                    facts.Add(new LocalizedExplainFact(
                        ResolveToken("explain.chrome.provider_label", localization),
                        ResolveProvenance(ReadProperty(fragment, "Provider") ?? fragment.ProviderId, localization).Label));
                }

                if (!string.IsNullOrWhiteSpace(ReadString(fragment.PackId) ?? ReadString(ReadProperty(fragment, "Pack"))))
                {
                    facts.Add(new LocalizedExplainFact(
                        ResolveToken("explain.chrome.pack_label", localization),
                        ResolveProvenance(ReadProperty(fragment, "Pack") ?? fragment.PackId, localization).Label));
                }

                return new LocalizedExplainStep(
                    Index: index + 1,
                    Title: title,
                    Value: value,
                    Facts: facts);
            })
            .ToArray();
    }

    private static IReadOnlyList<LocalizedExplainDiff> CreateDiffs(
        RulesetProviderTrace provider,
        IExplainTextLocalization localization)
    {
        if (provider.ExplainFragments.Count < 2)
        {
            return [];
        }

        RulesetExplainFragment? before = provider.ExplainFragments
            .FirstOrDefault(fragment => !string.IsNullOrWhiteSpace(ReadString(fragment.Value)));
        RulesetExplainFragment? after = provider.ExplainFragments
            .LastOrDefault(fragment => !string.IsNullOrWhiteSpace(ReadString(fragment.Value)));

        if (before is null
            || after is null
            || ReferenceEquals(before, after)
            || string.Equals(ReadString(before.Value), ReadString(after.Value), StringComparison.Ordinal))
        {
            return [];
        }

        return
        [
            new LocalizedExplainDiff(
                Label: ResolveText(before.Label, localization)
                    ?? ResolveToken("explain.chrome.value_label", localization),
                Before: ResolveLiteral(before.Value, localization)!,
                After: ResolveLiteral(after.Value, localization)!)
        ];
    }

    private static string? ReadString(object? source)
        => source switch
        {
            null => null,
            string value => value,
            _ => ReadStringProperty(source, "Text", "Value", "FallbackText", "DefaultText", "Id", "Key", "LocalizationKey")
        };

    private static string? ReadStringProperty(object source, params string[] propertyNames)
        => ReadProperty(source, propertyNames) switch
        {
            null => null,
            string value => string.IsNullOrWhiteSpace(value) ? null : value,
            _ => null
        };

    private static object? ReadProperty(object source, params string[] propertyNames)
    {
        Type type = source.GetType();
        foreach (string propertyName in propertyNames)
        {
            PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is not null)
            {
                return property.GetValue(source);
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string>? ReadParameters(object source)
    {
        object? rawParameters = ReadProperty(source, "Parameters", "Arguments", "Tokens");
        if (rawParameters is null)
        {
            return null;
        }

        if (rawParameters is IReadOnlyDictionary<string, string> ready)
        {
            return ready.Count == 0 ? null : ready;
        }

        if (rawParameters is IDictionary dictionary)
        {
            Dictionary<string, string> mapped = new(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is null || entry.Value is null)
                {
                    continue;
                }

                mapped[entry.Key.ToString() ?? string.Empty] = entry.Value.ToString() ?? string.Empty;
            }

            return mapped.Count == 0 ? null : mapped;
        }

        if (rawParameters is IEnumerable enumerable && rawParameters is not string)
        {
            Dictionary<string, string> mapped = new(StringComparer.Ordinal);
            foreach (object? item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                string? key = ReadStringProperty(item, "Key", "Name");
                string? value = ReadStringProperty(item, "Value", "Text");
                if (!string.IsNullOrWhiteSpace(key) && value is not null)
                {
                    mapped[key] = value;
                }
            }

            return mapped.Count == 0 ? null : mapped;
        }

        return null;
    }
}
