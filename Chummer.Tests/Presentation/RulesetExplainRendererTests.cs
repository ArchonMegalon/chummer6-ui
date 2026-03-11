#nullable enable annotations

using System;
using System.Collections.Generic;
using Bunit;
using Chummer.Blazor.Components.Shared;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Explain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class RulesetExplainRendererTests
{
    [TestMethod]
    public void Project_resolves_trace_and_chrome_through_shared_localization_contract()
    {
        MapExplainTextLocalization localization = CreateLocalization();

        LocalizedRulesetExplainTrace? trace = RulesetExplainRenderer.Project(CreateTrace(), localization);
        LocalizedExplainChrome chrome = RulesetExplainRenderer.CreateChrome(localization);

        Assert.IsNotNull(trace);
        Assert.AreEqual("Explain Trace", chrome.Title.Text);
        Assert.AreEqual("Runtime Evaluator", trace.SubjectId.Text);
        Assert.AreEqual("Provider", chrome.ProviderLabel.Text);
        Assert.AreEqual("Street Magic Overlay", trace.Providers[0].PackId?.Text);
        Assert.AreEqual("Base", trace.Providers[0].Fragments[0].Label?.Text);
        Assert.AreEqual("10", trace.Providers[0].Fragments[0].Value?.Text);
        Assert.AreEqual("Active traits and status modifiers are included.", trace.Providers[0].Fragments[0].Reason?.Text);
        Assert.AreEqual("What Changed", chrome.DiffLabel.Text);
        Assert.AreEqual(2, trace.Providers[0].Steps.Count);
        Assert.AreEqual("Modified", trace.Providers[0].Steps[1].Title.Text);
        Assert.AreEqual("12", trace.Providers[0].Steps[1].Value.Text);
        Assert.AreEqual("Base", trace.Providers[0].Diffs[0].Label.Text);
        Assert.AreEqual("10", trace.Providers[0].Diffs[0].Before.Text);
        Assert.AreEqual("12", trace.Providers[0].Diffs[0].After.Text);
    }

    [TestMethod]
    public void Project_throws_when_trace_contains_missing_localization_key_in_strict_mode()
    {
        MapExplainTextLocalization localization = CreateLocalization();
        RulesetExplainTrace trace = RulesetExplainContractFactory.CreateTrace(
            new ExplainTraceSeed(
                Subject: new ExplainTextSeed("provider.runtime"),
                Providers:
                [
                    new ExplainProviderSeed(
                        Provider: new ExplainProvenanceSeed("provider.runtime", new ExplainTextSeed("provider.runtime")),
                        Capability: new ExplainProvenanceSeed("capability.skill_check", new ExplainTextSeed("capability.skill_check")),
                        Pack: new ExplainProvenanceSeed("pack.street_magic", new ExplainTextSeed("pack.street_magic")),
                        Success: true,
                        Fragments:
                        [
                            new ExplainFragmentSeed(
                                Label: new ExplainTextSeed("fragment.label.base"),
                                Value: "10",
                                Reason: new ExplainTextSeed("fragment.reason.active_traits"),
                                Pack: new ExplainProvenanceSeed("pack.street_magic", new ExplainTextSeed("pack.street_magic")),
                                Provider: new ExplainProvenanceSeed("provider.runtime", new ExplainTextSeed("provider.runtime")))
                        ],
                        GasUsage: new RulesetGasUsage(2, 3, 128),
                        Messages:
                        [
                            new ExplainTextSeed("explain.message.provider_delta")
                        ])
                ],
                Messages:
                [
                    new ExplainTextSeed("missing.explain.message")
                ],
                AggregateGasUsage: new RulesetGasUsage(2, 3, 128)));

        Assert.ThrowsException<KeyNotFoundException>(() => RulesetExplainRenderer.Project(trace, localization));
    }

    [TestMethod]
    public void ExplainTracePanel_renders_shared_chrome_without_baked_fallback_prose()
    {
        using var context = new BunitContext();
        LocalizedExplainChrome chrome = new(
            Title: new LocalizedExplainText("test.title", "TRACE-TITLE"),
            Empty: new LocalizedExplainText("test.empty", "TRACE-EMPTY"),
            ValueLabel: new LocalizedExplainText("test.value", "TRACE-VALUE"),
            MissingValue: new LocalizedExplainText("test.missing", "TRACE-NONE"),
            ReasonLabel: new LocalizedExplainText("test.reason", "TRACE-REASON"),
            ProviderLabel: new LocalizedExplainText("test.provider", "TRACE-PROVIDER"),
            CapabilityLabel: new LocalizedExplainText("test.capability", "TRACE-CAPABILITY"),
            PackLabel: new LocalizedExplainText("test.pack", "TRACE-PACK"),
            TraceStepsLabel: new LocalizedExplainText("test.steps", "TRACE-STEPS"),
            DiffLabel: new LocalizedExplainText("test.diff", "TRACE-DIFF"),
            BeforeLabel: new LocalizedExplainText("test.before", "TRACE-BEFORE"),
            AfterLabel: new LocalizedExplainText("test.after", "TRACE-AFTER"),
            CloseAction: new LocalizedExplainText("test.close", "TRACE-CLOSE"));

        IRenderedComponent<ExplainTracePanel> cut = context.Render<ExplainTracePanel>(parameters => parameters
            .Add(component => component.Chrome, chrome)
            .Add(component => component.Trace, null));

        StringAssert.Contains(cut.Markup, "TRACE-TITLE");
        StringAssert.Contains(cut.Markup, "TRACE-EMPTY");
        Assert.IsFalse(cut.Markup.Contains("No explain payload is available for this selection.", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Explain Trace", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TextFormatter_uses_shared_chrome_for_desktop_projection()
    {
        MapExplainTextLocalization localization = CreateLocalization();
        LocalizedRulesetExplainTrace? trace = RulesetExplainRenderer.Project(CreateTrace(), localization);
        LocalizedExplainChrome chrome = RulesetExplainRenderer.CreateChrome(localization);

        string formatted = RulesetExplainTextFormatter.Format(trace, chrome);

        StringAssert.Contains(formatted, "Explain Trace");
        StringAssert.Contains(formatted, "What Changed:");
        StringAssert.Contains(formatted, "Base: Before 10 -> After 12");
        StringAssert.Contains(formatted, "Trace Steps:");
        StringAssert.Contains(formatted, "2. Modified: 12");
        StringAssert.Contains(formatted, "Provider: Runtime Evaluator");
        StringAssert.Contains(formatted, "Capability: Skill Check");
        StringAssert.Contains(formatted, "Pack: Street Magic Overlay");
        StringAssert.Contains(formatted, "Reason: Active traits and status modifiers are included.");
    }

    [TestMethod]
    public void ExplainTracePanel_renders_diff_and_trace_step_drilldown_with_localized_labels()
    {
        using var context = new BunitContext();
        MapExplainTextLocalization localization = CreateLocalization();
        LocalizedRulesetExplainTrace? trace = RulesetExplainRenderer.Project(CreateTrace(), localization);
        LocalizedExplainChrome chrome = RulesetExplainRenderer.CreateChrome(localization);

        IRenderedComponent<ExplainTracePanel> cut = context.Render<ExplainTracePanel>(parameters => parameters
            .Add(component => component.Chrome, chrome)
            .Add(component => component.Trace, trace));

        StringAssert.Contains(cut.Markup, "What Changed");
        StringAssert.Contains(cut.Markup, "Before: 10");
        StringAssert.Contains(cut.Markup, "After: 12");
        StringAssert.Contains(cut.Markup, "Trace Steps");
        StringAssert.Contains(cut.Markup, "#2");
        StringAssert.Contains(cut.Markup, "Modified");
        StringAssert.Contains(cut.Markup, "Street Magic Overlay");
    }

    private static MapExplainTextLocalization CreateLocalization()
        => new(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["explain.chrome.title"] = "Explain Trace",
                ["explain.chrome.empty"] = "No explain payload is available for this selection.",
                ["explain.chrome.value_label"] = "Value",
                ["explain.chrome.missing_value"] = "(none)",
                ["explain.chrome.reason_label"] = "Reason",
                ["explain.chrome.provider_label"] = "Provider",
                ["explain.chrome.capability_label"] = "Capability",
                ["explain.chrome.pack_label"] = "Pack",
                ["explain.chrome.trace_steps_label"] = "Trace Steps",
                ["explain.chrome.diff_label"] = "What Changed",
                ["explain.chrome.before_label"] = "Before",
                ["explain.chrome.after_label"] = "After",
                ["explain.chrome.close_action"] = "Close",
                ["provider.runtime"] = "Runtime Evaluator",
                ["capability.skill_check"] = "Skill Check",
                ["pack.street_magic"] = "Street Magic Overlay",
                ["explain.message.base_value"] = "Base value loaded from the runtime projection.",
                ["explain.message.provider_delta"] = "Provider delta applied from the active rule bundle.",
                ["fragment.label.base"] = "Base",
                ["fragment.label.modified"] = "Modified",
                ["fragment.reason.active_traits"] = "Active traits and status modifiers are included."
            },
            throwOnMissing: true);

    private static RulesetExplainTrace CreateTrace()
        => RulesetExplainContractFactory.CreateTrace(
            new ExplainTraceSeed(
                Subject: new ExplainTextSeed("provider.runtime"),
                Providers:
                [
                    new ExplainProviderSeed(
                        Provider: new ExplainProvenanceSeed("provider.runtime", new ExplainTextSeed("provider.runtime")),
                        Capability: new ExplainProvenanceSeed("capability.skill_check", new ExplainTextSeed("capability.skill_check")),
                        Pack: new ExplainProvenanceSeed("pack.street_magic", new ExplainTextSeed("pack.street_magic")),
                        Success: true,
                        Fragments:
                        [
                            new ExplainFragmentSeed(
                                Label: new ExplainTextSeed("fragment.label.base"),
                                Value: "10",
                                Reason: new ExplainTextSeed("fragment.reason.active_traits"),
                                Pack: new ExplainProvenanceSeed("pack.street_magic", new ExplainTextSeed("pack.street_magic")),
                                Provider: new ExplainProvenanceSeed("provider.runtime", new ExplainTextSeed("provider.runtime"))),
                            new ExplainFragmentSeed(
                                Label: new ExplainTextSeed("fragment.label.modified"),
                                Value: "12",
                                Reason: new ExplainTextSeed("fragment.reason.active_traits"),
                                Pack: new ExplainProvenanceSeed("pack.street_magic", new ExplainTextSeed("pack.street_magic")),
                                Provider: new ExplainProvenanceSeed("provider.runtime", new ExplainTextSeed("provider.runtime")))
                        ],
                        GasUsage: new RulesetGasUsage(2, 3, 128),
                        Messages:
                        [
                            new ExplainTextSeed("explain.message.provider_delta")
                        ])
                ],
                Messages:
                [
                    new ExplainTextSeed("explain.message.base_value")
                ],
                AggregateGasUsage: new RulesetGasUsage(2, 3, 128)));
}
