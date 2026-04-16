using System.Globalization;
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
    public static RulesetExplainTrace CreateTrace(ExplainTraceSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        ExplainTextSeed summarySeed = seed.Messages.FirstOrDefault() ?? seed.Subject;

        return new RulesetExplainTrace(
            TargetKey: seed.Subject.Key,
            FinalValue: null,
            SummaryKey: summarySeed.Key,
            SummaryParameters: BuildExplainParameters(summarySeed.Parameters),
            Providers: seed.Providers.Select(BuildProviderTrace).ToArray(),
            AggregateGasUsage: seed.AggregateGasUsage);
    }

    private static RulesetProviderTrace BuildProviderTrace(ExplainProviderSeed seed)
    {
        RulesetTraceStep[] steps = seed.Fragments
            .Select(fragment => BuildTraceStep(seed, fragment))
            .ToArray();

        return new RulesetProviderTrace(
            ProviderId: seed.Provider.Id,
            CapabilityId: seed.Capability.Id,
            PackId: seed.Pack?.Id,
            Success: seed.Success,
            Steps: steps,
            GasUsage: seed.GasUsage);
    }

    private static RulesetTraceStep BuildTraceStep(ExplainProviderSeed providerSeed, ExplainFragmentSeed fragmentSeed)
    {
        return new RulesetTraceStep(
            ProviderId: fragmentSeed.Provider?.Id ?? providerSeed.Provider.Id,
            CapabilityId: providerSeed.Capability.Id,
            PackId: fragmentSeed.Pack?.Id ?? providerSeed.Pack?.Id,
            ExplanationKey: fragmentSeed.Reason?.Key ?? fragmentSeed.Label.Key,
            ExplanationParameters: BuildStepParameters(providerSeed, fragmentSeed),
            Category: "explain-fragment",
            Modifier: ParseModifier(fragmentSeed.Value),
            Certain: ParseCertain(fragmentSeed.Value));
    }

    private static RulesetExplainParameter[] BuildStepParameters(ExplainProviderSeed providerSeed, ExplainFragmentSeed fragmentSeed)
    {
        List<RulesetExplainParameter> parameters = new();

        AddParameter(parameters, "labelKey", fragmentSeed.Label.Key);
        AddParameter(parameters, "labelText", fragmentSeed.Label.FallbackText);
        AddParameter(parameters, "reasonKey", fragmentSeed.Reason?.Key);
        AddParameter(parameters, "reasonText", fragmentSeed.Reason?.FallbackText);
        AddParameter(parameters, "providerMessageKey", providerSeed.Messages.FirstOrDefault()?.Key);
        AddParameter(parameters, "providerMessageText", providerSeed.Messages.FirstOrDefault()?.FallbackText);

        IEnumerable<KeyValuePair<string, string>> labelParameters = fragmentSeed.Label.Parameters is { Count: > 0 }
            ? fragmentSeed.Label.Parameters
            : Array.Empty<KeyValuePair<string, string>>();
        foreach (KeyValuePair<string, string> parameter in labelParameters)
        {
            AddParameter(parameters, $"label:{parameter.Key}", parameter.Value);
        }

        IEnumerable<KeyValuePair<string, string>> reasonParameters = fragmentSeed.Reason?.Parameters is { Count: > 0 }
            ? fragmentSeed.Reason.Parameters
            : Array.Empty<KeyValuePair<string, string>>();
        foreach (KeyValuePair<string, string> parameter in reasonParameters)
        {
            AddParameter(parameters, $"reason:{parameter.Key}", parameter.Value);
        }

        return parameters.ToArray();
    }

    private static RulesetExplainParameter[] BuildExplainParameters(IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return [];
        }

        return parameters
            .Select(parameter => new RulesetExplainParameter(parameter.Key, RulesetCapabilityBridge.FromObject(parameter.Value)))
            .ToArray();
    }

    private static void AddParameter(ICollection<RulesetExplainParameter> parameters, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        parameters.Add(new RulesetExplainParameter(name, RulesetCapabilityBridge.FromObject(value)));
    }

    private static decimal? ParseModifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal modifier)
            ? modifier
            : null;
    }

    private static bool? ParseCertain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return bool.TryParse(value, out bool certain)
            ? certain
            : null;
    }
}
