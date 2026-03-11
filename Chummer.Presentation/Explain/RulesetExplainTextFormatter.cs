using System.Text;

namespace Chummer.Presentation.Explain;

public static class RulesetExplainTextFormatter
{
    public static string Format(
        LocalizedRulesetExplainTrace? trace,
        LocalizedExplainChrome chrome)
    {
        if (trace is null)
        {
            return chrome.Empty.Text;
        }

        StringBuilder builder = new();
        builder.AppendLine(chrome.Title.Text);

        if (trace.Messages.Count > 0)
        {
            foreach (LocalizedExplainText message in trace.Messages)
            {
                builder.Append("- ").AppendLine(message.Text);
            }
        }

        foreach (LocalizedRulesetExplainProvider provider in trace.Providers)
        {
            builder.Append(chrome.ProviderLabel.Text)
                .Append(": ")
                .AppendLine(provider.ProviderId.Text);
            builder.Append(chrome.CapabilityLabel.Text)
                .Append(": ")
                .AppendLine(provider.CapabilityId.Text);

            if (provider.PackId is not null)
            {
                builder.Append(chrome.PackLabel.Text)
                    .Append(": ")
                    .AppendLine(provider.PackId.Text);
            }

            if (provider.Message is not null)
            {
                builder.Append("- ").AppendLine(provider.Message.Text);
            }

            if (provider.Diffs.Count > 0)
            {
                builder.Append(chrome.DiffLabel.Text)
                    .Append(':')
                    .AppendLine();

                foreach (LocalizedExplainDiff diff in provider.Diffs)
                {
                    builder.Append("  ")
                        .Append(diff.Label.Text)
                        .Append(": ")
                        .Append(chrome.BeforeLabel.Text)
                        .Append(' ')
                        .Append(diff.Before.Text)
                        .Append(" -> ")
                        .Append(chrome.AfterLabel.Text)
                        .Append(' ')
                        .AppendLine(diff.After.Text);
                }
            }

            if (provider.Steps.Count > 0)
            {
                builder.Append(chrome.TraceStepsLabel.Text)
                    .Append(':')
                    .AppendLine();

                foreach (LocalizedExplainStep step in provider.Steps)
                {
                    builder.Append("  ")
                        .Append(step.Index)
                        .Append(". ")
                        .Append(step.Title.Text)
                        .Append(": ")
                        .AppendLine(step.Value.Text);

                    foreach (LocalizedExplainFact fact in step.Facts)
                    {
                        builder.Append("     ")
                            .Append(fact.Label.Text)
                            .Append(": ")
                            .AppendLine(fact.Value.Text);
                    }
                }
            }

        }

        return builder.ToString().TrimEnd();
    }
}
