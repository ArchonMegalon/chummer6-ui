using Chummer.Desktop.Runtime;
using Chummer.Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopMouseFirstJourneyRuntimeTests
{
    [TestMethod]
    public async Task Press_discipline_never_represses_after_a_possibly_delivered_press()
    {
        // Controller9539 regression: after a press whose delivery was observed
        // (delivered=true), a starved observation + failed reconciliation must
        // fail closed with EXACTLY ONE press - the pre-9539 loop re-pressed
        // after the observation timeout.
        int presses = 0;
        InvalidOperationException? failure = null;
        try
        {
            await DesktopMouseFirstJourneyRunner.PressThenObserveWithDeliveryDisciplineAsync<object>(
                async () =>
                {
                    presses++;
                    await Task.Delay(1);
                    // C#-bool casing as Finish() actually emits it.
                    return "delivered=True;isPressed=True;enabled=True;visible=True;attached;";
                },
                async () =>
                {
                    await Task.Delay(1);
                    return (object?)null; // observation starved
                },
                async () =>
                {
                    await Task.Delay(1);
                    return (object?)null; // reconciliation also finds nothing
                },
                _ => { },
                maxNotLiveRetries: 3,
                failContext: "discipline-regression",
                System.Threading.CancellationToken.None);
        }
        catch (InvalidOperationException caught)
        {
            failure = caught;
        }
        Assert.IsNotNull(failure, "a delivered press with a starved observation must fail closed");
        StringAssert.Contains(failure!.Message, "no re-press",
            "the failure must name the no-re-press discipline");
        Assert.AreEqual(1, presses,
            "exactly ONE press may be issued; a second press after possible delivery is the replay hazard");
    }

    [TestMethod]
    public async Task Press_discipline_retries_only_explicit_target_not_live_results()
    {
        // Explicit pre-press target-not-live results retry (bounded) and then
        // succeed; one press per attempt, no extra presses after delivery.
        int presses = 0;
        object? observed = await DesktopMouseFirstJourneyRunner.PressThenObserveWithDeliveryDisciplineAsync<object>(
            async () =>
            {
                presses++;
                await Task.Delay(1);
                return presses <= 2
                    // Resolver's hand-written lowercase pre-press literal.
                    ? "delivered=false;reason=target-not-live-at-press;"
                    // Finish()'s C#-bool cased delivery result.
                    : "delivered=True;isPressed=True;enabled=True;visible=True;attached;";
            },
            async () =>
            {
                await Task.Delay(1);
                return presses >= 3 ? new object() : (object?)null;
            },
            reconcileAsync: null,
            _ => { },
            maxNotLiveRetries: 3,
            failContext: "discipline-regression",
            System.Threading.CancellationToken.None);
        Assert.IsNotNull(observed);
        Assert.AreEqual(3, presses, "two bounded not-live retries then one delivered press");
    }

    [TestMethod]
    public async Task Press_discipline_fails_closed_on_other_not_delivered_results()
    {
        int presses = 0;
        InvalidOperationException? failure = null;
        try
        {
            await DesktopMouseFirstJourneyRunner.PressThenObserveWithDeliveryDisciplineAsync<object>(
                async () =>
                {
                    presses++;
                    await Task.Delay(1);
                    // C#-bool casing as Finish() actually emits it: this
                    // result is NOT delivered and must fail closed.
                    return "delivered=False;isPressed=False;enabled=True;visible=True;attached;";
                },
                async () =>
                {
                    await Task.Delay(1);
                    return new object();
                },
                reconcileAsync: null,
                _ => { },
                maxNotLiveRetries: 3,
                failContext: "discipline-regression",
                System.Threading.CancellationToken.None);
        }
        catch (InvalidOperationException caught)
        {
            failure = caught;
        }
        Assert.IsNotNull(failure, "an unexpected not-delivered result must fail closed");
        StringAssert.Contains(failure!.Message, "was not delivered");
        Assert.AreEqual(1, presses, "no re-press after an ambiguous not-delivered result");
    }

    [TestMethod]
    public async Task Press_discipline_classifies_csharp_bool_casing_case_insensitively()
    {
        // Controller9539 casing regression (exposed by the gen10i phase A run):
        // Finish() interpolates a C# bool, so the post-press result is
        // "delivered=False" (capital F) for a not-delivered press. A lowercase
        // Contains() would misclassify that as delivered and observe once -
        // the leg must instead FAIL CLOSED on the ambiguous result.
        int presses = 0;
        InvalidOperationException? failure = null;
        try
        {
            await DesktopMouseFirstJourneyRunner.PressThenObserveWithDeliveryDisciplineAsync<object>(
                async () =>
                {
                    presses++;
                    await Task.Delay(1);
                    return "delivered=False;isPressed=False;enabled=True;visible=True;attached;";
                },
                async () =>
                {
                    await Task.Delay(1);
                    return new object();
                },
                reconcileAsync: null,
                _ => { },
                maxNotLiveRetries: 3,
                failContext: "casing-regression",
                System.Threading.CancellationToken.None);
        }
        catch (InvalidOperationException caught)
        {
            failure = caught;
        }
        Assert.IsNotNull(failure, "delivered=False (C# casing) must classify as NOT delivered");
        StringAssert.Contains(failure!.Message, "was not delivered");
        Assert.AreEqual(1, presses, "no re-press after the ambiguous C#-cased result");
    }

    [TestMethod]
    public async Task Press_discipline_fails_closed_on_a_thrown_press_fault()
    {
        int presses = 0;
        InvalidOperationException? failure = null;
        try
        {
            await DesktopMouseFirstJourneyRunner.PressThenObserveWithDeliveryDisciplineAsync<object>(
                async () =>
                {
                    presses++;
                    await Task.Delay(1);
                    throw new InvalidOperationException("synthetic routing collapse");
                },
                async () =>
                {
                    await Task.Delay(1);
                    return new object();
                },
                reconcileAsync: null,
                _ => { },
                maxNotLiveRetries: 3,
                failContext: "discipline-regression",
                System.Threading.CancellationToken.None);
        }
        catch (InvalidOperationException caught)
        {
            failure = caught;
        }
        Assert.IsNotNull(failure, "a thrown press fault must fail closed");
        StringAssert.Contains(failure!.Message, "press fault at attempt 1");
        Assert.AreEqual(1, presses, "no re-press after a thrown routing fault");
    }

    [TestMethod]
    public void ParsePointerDeliveryResult_rejects_malformed_delivered_tokens()
    {
        // Controller9547: the typed parser must reject malformed delivered
        // segments ("delivered=TrueOops") instead of accepting them as
        // delivered, which a substring Contains test would do.
        DesktopMouseFirstJourneyRunner.PointerDeliveryResult malformed =
            DesktopMouseFirstJourneyRunner.ParsePointerDeliveryResult("delivered=TrueOops;enabled=True;");
        Assert.AreEqual(DesktopMouseFirstJourneyRunner.PointerDeliveryKind.Malformed, malformed.Kind);

        DesktopMouseFirstJourneyRunner.PointerDeliveryResult missing =
            DesktopMouseFirstJourneyRunner.ParsePointerDeliveryResult("no-delivered-segment");
        Assert.AreEqual(DesktopMouseFirstJourneyRunner.PointerDeliveryKind.Missing, missing.Kind);

        DesktopMouseFirstJourneyRunner.PointerDeliveryResult delivered =
            DesktopMouseFirstJourneyRunner.ParsePointerDeliveryResult("delivered=True;enabled=True;");
        Assert.AreEqual(DesktopMouseFirstJourneyRunner.PointerDeliveryKind.Delivered, delivered.Kind);

        DesktopMouseFirstJourneyRunner.PointerDeliveryResult lowerCase =
            DesktopMouseFirstJourneyRunner.ParsePointerDeliveryResult("delivered=true;");
        Assert.AreEqual(DesktopMouseFirstJourneyRunner.PointerDeliveryKind.Delivered, lowerCase.Kind);

        DesktopMouseFirstJourneyRunner.PointerDeliveryResult notDelivered =
            DesktopMouseFirstJourneyRunner.ParsePointerDeliveryResult("delivered=False;enabled=True;");
        Assert.AreEqual(DesktopMouseFirstJourneyRunner.PointerDeliveryKind.NotDelivered, notDelivered.Kind);
    }

    [TestMethod]
    public void ReadPlan_uses_defaults_when_environment_is_empty()
    {
        using EnvironmentOverride scope = new EnvironmentOverride();

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("sr5-priority", plan.ScenarioId);
        Assert.AreEqual("Mouse Journey Runner", plan.CharacterName);
        Assert.AreEqual("MouseRoute", plan.CharacterAlias);
        Assert.AreEqual("sr5", plan.RulesetId);
        Assert.AreEqual("Priority", plan.BuildMethod);
        Assert.IsNull(plan.MetatypeCategory);
        Assert.IsNull(plan.PriorityHeritage);
        Assert.IsNull(plan.Metatype);
        Assert.IsNull(plan.PriorityTalent);
        Assert.IsNull(plan.PriorityTalentChoice);
    }

    [TestMethod]
    public void ReadPlan_normalizes_priority_scenario_fields()
    {
        using EnvironmentOverride scope = new EnvironmentOverride()
            .Set(DesktopMouseFirstJourneyRuntime.RulesetIdEnvironmentVariable, "SR6")
            .Set(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable, "priority")
            .Set(DesktopMouseFirstJourneyRuntime.MetatypeCategoryEnvironmentVariable, "Show All")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityHeritageEnvironmentVariable, "a")
            .Set(DesktopMouseFirstJourneyRuntime.MetatypeEnvironmentVariable, "Shapeshifter: Vulpine")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityTalentEnvironmentVariable, "b")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityTalentChoiceEnvironmentVariable, "Mystic Adept")
            .Set(DesktopMouseFirstJourneyRuntime.CharacterNameEnvironmentVariable, "Mysad Troll")
            .Set(DesktopMouseFirstJourneyRuntime.CharacterAliasEnvironmentVariable, "MouseTroll");

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("SR6", plan.RulesetId);
        Assert.AreEqual("Priority", plan.BuildMethod);
        Assert.AreEqual("Show All", plan.MetatypeCategory);
        Assert.AreEqual("A", plan.PriorityHeritage);
        Assert.AreEqual("Shapeshifter: Vulpine", plan.Metatype);
        Assert.AreEqual("B", plan.PriorityTalent);
        Assert.AreEqual("Mystic Adept", plan.PriorityTalentChoice);
        Assert.AreEqual("Mysad Troll", plan.CharacterName);
        Assert.AreEqual("MouseTroll", plan.CharacterAlias);
        Assert.AreEqual("sr6-priority-show-all-a-shapeshifter-vulpine-mystic-adept", plan.ScenarioId);
    }

    [TestMethod]
    public void ReadPlan_clears_priority_heritage_for_non_priority_builds()
    {
        using EnvironmentOverride scope = new EnvironmentOverride()
            .Set(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable, "karma")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityHeritageEnvironmentVariable, "B");

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("Karma", plan.BuildMethod);
        Assert.IsNull(plan.PriorityHeritage);
        Assert.IsNull(plan.PriorityTalent);
        Assert.IsNull(plan.PriorityTalentChoice);
        Assert.AreEqual("sr5-karma", plan.ScenarioId);
    }

    [TestMethod]
    public void ReadPlan_retains_priority_facts_for_sum_to_ten_builds()
    {
        using EnvironmentOverride scope = new EnvironmentOverride()
            .Set(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable, "SumToTen")
            .Set(DesktopMouseFirstJourneyRuntime.PriorityHeritageEnvironmentVariable, "c")
            .Set(DesktopMouseFirstJourneyRuntime.MetatypeEnvironmentVariable, "Dwarf");

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        // WIN-009 gen2 (T748): Sum-to-Ten shares the Priority prerequisite
        // draft machinery, so the plan must retain the heritage facts and
        // drive the same prerequisite table.
        Assert.AreEqual("SumToTen", plan.BuildMethod);
        Assert.AreEqual("C", plan.PriorityHeritage);
        Assert.AreEqual("Dwarf", plan.Metatype);
    }

    [TestMethod]
    public void ReadPlan_preserves_bp_build_method_for_sr4()
    {
        using EnvironmentOverride scope = new EnvironmentOverride()
            .Set(DesktopMouseFirstJourneyRuntime.RulesetIdEnvironmentVariable, "sr4")
            .Set(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable, "bp");

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("sr4", plan.RulesetId);
        Assert.AreEqual("BP", plan.BuildMethod);
        Assert.AreEqual("sr4-bp", plan.ScenarioId);
    }

    private sealed class EnvironmentOverride : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public EnvironmentOverride()
        {
            Track(DesktopMouseFirstJourneyRuntime.ScenarioIdEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.CharacterNameEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.CharacterAliasEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.RulesetIdEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.MetatypeCategoryEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.PriorityHeritageEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.MetatypeEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.PriorityTalentEnvironmentVariable);
            Track(DesktopMouseFirstJourneyRuntime.PriorityTalentChoiceEnvironmentVariable);

            foreach (string key in _originalValues.Keys)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        public EnvironmentOverride Set(string key, string? value)
        {
            Environment.SetEnvironmentVariable(key, value);
            return this;
        }

        public void Dispose()
        {
            foreach ((string key, string? value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        private void Track(string key)
        {
            _originalValues[key] = Environment.GetEnvironmentVariable(key);
        }
    }
}
