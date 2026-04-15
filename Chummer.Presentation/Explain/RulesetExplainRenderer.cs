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

        LocalizedExplainText? summaryMessage = ResolveExplainText(trace.SummaryKey, trace.SummaryParameters, resolver);

        return new LocalizedRulesetExplainTrace(
            SubjectId: ResolveText(trace.TargetKey, resolver)
                ?? CreateLiteralText(ReadString(trace.TargetKey))
                ?? new LocalizedExplainText(null, string.Empty),
            Messages: summaryMessage is null ? [] : [summaryMessage],
            Providers: trace.Providers
                .Select(provider => new LocalizedRulesetExplainProvider(
                    ResolveProvenance(ReadProperty(provider, "Provider") ?? provider.ProviderId, resolver),
                    ResolveProvenance(ReadProperty(provider, "Capability") ?? provider.CapabilityId, resolver),
                    ResolveProvenance(ReadProperty(provider, "Pack") ?? provider.PackId, resolver),
                    ResolveProviderMessage(provider, resolver),
                    provider.Steps
                        .Select(step => new LocalizedExplainFragment(
                            ResolveStepLabel(step, resolver),
                            ResolveStepValue(step),
                            ResolveStepReason(step, resolver),
                            ResolveProvenance(step.PackId, resolver),
                            ResolveProvenance(step.ProviderId, resolver)))
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
        if (provider.Steps.Count == 0)
        {
            return [];
        }

        return provider.Steps
            .Select((step, index) =>
            {
                LocalizedExplainText title = ResolveExplainText(step.ExplanationKey, step.ExplanationParameters, localization)
                    ?? ResolveToken("explain.chrome.value_label", localization);
                title = ResolveStepLabel(step, localization) ?? title;
                LocalizedExplainText value = ResolveStepValue(step)
                    ?? ResolveToken("explain.chrome.missing_value", localization);

                List<LocalizedExplainFact> facts = [];

                if (!string.IsNullOrWhiteSpace(step.ExplanationKey))
                {
                    facts.Add(new LocalizedExplainFact(
                        ResolveToken("explain.chrome.reason_label", localization),
                        ResolveStepReason(step, localization)
                            ?? ResolveToken("explain.chrome.missing_value", localization)));
                }

                if (!string.IsNullOrWhiteSpace(step.ProviderId))
                {
                    facts.Add(new LocalizedExplainFact(
                        ResolveToken("explain.chrome.provider_label", localization),
                        ResolveProvenance(step.ProviderId, localization).Label));
                }

                if (!string.IsNullOrWhiteSpace(step.PackId))
                {
                    facts.Add(new LocalizedExplainFact(
                        ResolveToken("explain.chrome.pack_label", localization),
                        ResolveProvenance(step.PackId, localization).Label));
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
        if (provider.Steps.Count < 2)
        {
            return [];
        }

        RulesetTraceStep? before = provider.Steps.FirstOrDefault(step => step.Modifier.HasValue);
        RulesetTraceStep? after = provider.Steps.LastOrDefault(step => step.Modifier.HasValue);

        if (before is null
            || after is null
            || ReferenceEquals(before, after)
            || before.Modifier == after.Modifier)
        {
            return [];
        }

        return
        [
            new LocalizedExplainDiff(
                Label: ResolveStepLabel(before, localization)
                    ?? ResolveExplainText(before.ExplanationKey, before.ExplanationParameters, localization)
                    ?? ResolveToken("explain.chrome.value_label", localization),
                Before: ResolveStepValue(before) ?? ResolveToken("explain.chrome.missing_value", localization),
                After: ResolveStepValue(after) ?? ResolveToken("explain.chrome.missing_value", localization))
        ];
    }

    private static LocalizedExplainText? ResolveProviderMessage(
        RulesetProviderTrace provider,
        IExplainTextLocalization localization)
    {
        RulesetTraceStep? firstStep = provider.Steps.FirstOrDefault();
        string? providerMessageKey = ReadParameterValue(firstStep?.ExplanationParameters, "providerMessageKey");
        string? providerMessageText = ReadParameterValue(firstStep?.ExplanationParameters, "providerMessageText");

        if (!string.IsNullOrWhiteSpace(providerMessageKey))
        {
            return ResolveExplainText(providerMessageKey, null, localization);
        }

        if (!string.IsNullOrWhiteSpace(providerMessageText))
        {
            return new LocalizedExplainText(null, providerMessageText);
        }

        return ResolveStepReason(firstStep, localization);
    }

    private static LocalizedExplainText? ResolveStepLabel(
        RulesetTraceStep? step,
        IExplainTextLocalization localization)
    {
        if (step is null)
        {
            return null;
        }

        string? labelKey = ReadParameterValue(step.ExplanationParameters, "labelKey");
        if (!string.IsNullOrWhiteSpace(labelKey))
        {
            return ResolveScopedExplainText(labelKey, ReadScopedParameters(step.ExplanationParameters, "label:"), localization);
        }

        string? labelText = ReadParameterValue(step.ExplanationParameters, "labelText");
        if (!string.IsNullOrWhiteSpace(labelText))
        {
            return new LocalizedExplainText(null, labelText);
        }

        return ResolveExplainText(step.ExplanationKey, step.ExplanationParameters, localization);
    }

    private static LocalizedExplainText? ResolveStepReason(
        RulesetTraceStep? step,
        IExplainTextLocalization localization)
    {
        if (step is null)
        {
            return null;
        }

        string? reasonKey = ReadParameterValue(step.ExplanationParameters, "reasonKey");
        if (!string.IsNullOrWhiteSpace(reasonKey))
        {
            return ResolveScopedExplainText(reasonKey, ReadScopedParameters(step.ExplanationParameters, "reason:"), localization);
        }

        string? reasonText = ReadParameterValue(step.ExplanationParameters, "reasonText");
        if (!string.IsNullOrWhiteSpace(reasonText))
        {
            return new LocalizedExplainText(null, reasonText);
        }

        return ResolveExplainText(step.ExplanationKey, step.ExplanationParameters, localization);
    }

    private static LocalizedExplainText? ResolveExplainText(
        string? key,
        IReadOnlyList<RulesetExplainParameter>? parameters,
        IExplainTextLocalization localization)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        IReadOnlyDictionary<string, string>? mapped = parameters is null || parameters.Count == 0
            ? null
            : parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
                .ToDictionary(
                    parameter => parameter.Name,
                    parameter => ReadString(parameter.Value) ?? string.Empty,
                    StringComparer.Ordinal);

        return new LocalizedExplainText(
            Key: key,
            Text: localization.Resolve(key, mapped),
            Parameters: mapped);
    }

    private static LocalizedExplainText? ResolveScopedExplainText(
        string? key,
        IReadOnlyDictionary<string, string>? parameters,
        IExplainTextLocalization localization)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return new LocalizedExplainText(
            Key: key,
            Text: localization.Resolve(key, parameters),
            Parameters: parameters);
    }

    private static LocalizedExplainText? ResolveStepValue(RulesetTraceStep step)
    {
        if (step.Modifier.HasValue)
        {
            return CreateLiteralText(step.Modifier.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (step.Certain.HasValue)
        {
            return CreateLiteralText(step.Certain.Value ? "certain" : "uncertain");
        }

        return null;
    }

    private static string? ReadString(object? source)
        => source switch
        {
            null => null,
            string value => value,
            RulesetCapabilityValue capabilityValue => ReadCapabilityValue(capabilityValue),
            _ => ReadStringProperty(source, "Text", "Value", "FallbackText", "DefaultText", "Id", "Key", "LocalizationKey")
        };

    private static string? ReadCapabilityValue(RulesetCapabilityValue value)
    {
        if (!string.IsNullOrWhiteSpace(value.StringValue))
        {
            return value.StringValue;
        }

        if (value.DecimalValue.HasValue)
        {
            return value.DecimalValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value.IntegerValue.HasValue)
        {
            return value.IntegerValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value.NumberValue.HasValue)
        {
            return value.NumberValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value.BooleanValue.HasValue)
        {
            return value.BooleanValue.Value ? "true" : "false";
        }

        return null;
    }

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

    private static string? ReadParameterValue(IReadOnlyList<RulesetExplainParameter>? parameters, string name)
    {
        if (parameters is null)
        {
            return null;
        }

        return parameters
            .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
            is { } parameter
            ? ReadString(parameter.Value)
            : null;
    }

    private static IReadOnlyDictionary<string, string>? ReadScopedParameters(
        IReadOnlyList<RulesetExplainParameter>? parameters,
        string prefix)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return null;
        }

        Dictionary<string, string> mapped = new(StringComparer.Ordinal);
        foreach (RulesetExplainParameter parameter in parameters)
        {
            if (!parameter.Name.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string key = parameter.Name[prefix.Length..];
            string? value = ReadString(parameter.Value);
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            mapped[key] = value;
        }

        return mapped.Count == 0 ? null : mapped;
    }
}
