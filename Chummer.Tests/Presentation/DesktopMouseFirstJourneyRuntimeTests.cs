using Chummer.Desktop.Runtime;
using Chummer.Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
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
    public void ClassifyDialogPressForRetry_represses_only_on_explicit_pre_dispatch_non_delivery()
    {
        // Controller9579 (Create retry): a re-press is justified ONLY by an
        // explicit pre-dispatch non-delivery (target-not-live-at-press:
        // nothing was raised, so a fresh press cannot double-commit).
        // delivered=True (committed once), an ambiguous delivered=False
        // without the pre-dispatch reason (probe passed, activation unseen),
        // a malformed token, and a missing segment must ALL refuse the
        // re-press - the original result is retained, fail closed.
        Assert.IsTrue(DesktopMouseFirstJourneyRunner.ClassifyDialogPressForRetry(
            "press=dialog;delivered=False;reason=target-not-live-at-press;enabled=True;"),
            "explicit pre-dispatch non-delivery is the ONLY retryable verdict");
        Assert.IsFalse(DesktopMouseFirstJourneyRunner.ClassifyDialogPressForRetry(
            "press=dialog;delivered=True;enabled=True;"),
            "a delivered press must never re-press (double-commit guard)");
        Assert.IsFalse(DesktopMouseFirstJourneyRunner.ClassifyDialogPressForRetry(
            "press=dialog;delivered=False;enabled=True;"),
            "ambiguous non-delivery without the pre-dispatch reason must not re-press");
        Assert.IsFalse(DesktopMouseFirstJourneyRunner.ClassifyDialogPressForRetry(
            "press=dialog;delivered=TrueOops;enabled=True;"),
            "malformed delivery token must not re-press");
        Assert.IsFalse(DesktopMouseFirstJourneyRunner.ClassifyDialogPressForRetry(
            "press=dialog;enabled=True;"),
            "missing delivery segment must not re-press");
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

    [TestMethod]
    public void ReadPlan_normalizes_life_modules_aliases_to_canonical_method()
    {
        // WIN-009 gen16 (T1043): the new-character dialog exposes the
        // Core-authoritative "LifeModule" id; plural/hyphenated journey
        // spellings must fold onto it so the dialog select resolves, and
        // the derived scenario id uses the canonical singular form.
        using EnvironmentOverride scope = new EnvironmentOverride()
            .Set(DesktopMouseFirstJourneyRuntime.RulesetIdEnvironmentVariable, "sr5")
            .Set(DesktopMouseFirstJourneyRuntime.BuildMethodEnvironmentVariable, "LifeModules")
            .Set(DesktopMouseFirstJourneyRuntime.MetatypeEnvironmentVariable, "Human");

        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();

        Assert.AreEqual("LifeModule", plan.BuildMethod);
        Assert.AreEqual("Human", plan.Metatype);
        Assert.AreEqual("sr5-lifemodule-human", plan.ScenarioId);
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

    [TestClass]
    public sealed class MagicReopenSidecarParsingTests
    {
        private static string WriteSidecar(string json)
        {
            string path = Path.Combine(Path.GetTempPath(), "t888-sidecar-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, json);
            return path;
        }

        [TestMethod]
        public void Parse_accepts_an_explicit_positive_integer_rating()
        {
            string path = WriteSidecar("""{"spells":[],"powers":[{"id":"sr6-power-test-a","rating":3}]}""");
            var expectations = DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path);
            Assert.HasCount(1, expectations.PowerExpectations);
            Assert.AreEqual(("sr6-power-test-a", 3), expectations.PowerExpectations[0]);
        }

        [TestMethod]
        public void Parse_refuses_a_power_entry_without_a_rating()
        {
            string path = WriteSidecar("""{"spells":[],"powers":[{"id":"sr6-power-test-a"}]}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("without a positive integer rating"),
                "the refusal names the missing-rating contract, not a coercion: " + failure.Message);
        }

        [TestMethod]
        public void Parse_refuses_a_non_number_rating()
        {
            string path = WriteSidecar("""{"spells":[],"powers":[{"id":"sr6-power-test-a","rating":"two"}]}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("without a positive integer rating"), failure.Message);
        }

        [TestMethod]
        public void Parse_refuses_a_zero_and_negative_rating()
        {
            foreach (string rating in new[] { "0", "-2" })
            {
                string path = WriteSidecar("{\"spells\":[],\"powers\":[{\"id\":\"sr6-power-test-a\",\"rating\":" + rating + "}]}");
                var failure = Assert.ThrowsExactly<InvalidOperationException>(
                    () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
                Assert.IsTrue(failure.Message.Contains("without a positive integer rating"), failure.Message);
            }
        }

        [TestMethod]
        public void Parse_refuses_a_power_entry_without_an_id()
        {
            string path = WriteSidecar("""{"spells":[],"powers":[{"rating":2}]}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("without an id"), failure.Message);
        }

        [TestMethod]
        public void Parse_still_accepts_the_historical_spell_only_shape()
        {
            string path = WriteSidecar("""{"spells":["sr6-spell-test-a"],"powers":[]}""");
            var expectations = DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path);
            Assert.HasCount(1, expectations.SpellIds);
            Assert.AreEqual("sr6-spell-test-a", expectations.SpellIds[0]);
            Assert.HasCount(0, expectations.PowerExpectations);
        }


        [TestMethod]
        public void Parse_accepts_a_tradition_only_sidecar_and_carries_the_slug()
        {
            string path = WriteSidecar("""{"spells":[],"powers":[],"tradition":"shinto"}""");
            var expectations = DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path);
            Assert.HasCount(0, expectations.SpellIds);
            Assert.HasCount(0, expectations.PowerExpectations);
            Assert.AreEqual("shinto", expectations.TraditionId);
        }

        [TestMethod]
        public void Parse_refuses_an_empty_sidecar_with_no_expectations()
        {
            string path = WriteSidecar("""{"spells":[],"powers":[],"tradition":""}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("carries no expectations"), failure.Message);
        }

        [TestMethod]
        public void Parse_refuses_duplicate_spell_ids()
        {
            string path = WriteSidecar("""{"spells":["sr6-spell-a","sr6-spell-a"],"powers":[]}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("duplicate spell ids"), failure.Message);
        }

        [TestMethod]
        public void Parse_refuses_duplicate_power_ids()
        {
            string path = WriteSidecar(
                """{"spells":[],"powers":[{"id":"sr6-power-a","rating":1},{"id":"SR6-POWER-A","rating":2}]}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("duplicate power ids"), failure.Message);
        }

        [TestMethod]
        public void Parse_refuses_a_duplicate_id_property_in_a_power_entry()
        {
            // Controller9836: TryGetProperty is last-wins, so {"id":"a","id":"b"}
            // would otherwise read as "b" with no duplicate signal at all.
            string path = WriteSidecar(
                """{"spells":[],"powers":[{"id":"sr6-power-a","id":"sr6-power-b","rating":2}]}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("duplicate JSON property name"), failure.Message);
        }

        [TestMethod]
        public void Parse_refuses_a_duplicate_rating_property_in_a_power_entry()
        {
            string path = WriteSidecar(
                """{"spells":[],"powers":[{"id":"sr6-power-a","rating":1,"rating":2}]}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("duplicate JSON property name"), failure.Message);
        }

        [TestMethod]
        public void Parse_refuses_duplicate_container_properties_at_the_root()
        {
            string path = WriteSidecar(
                """{"spells":["sr6-spell-a"],"spells":["sr6-spell-b"]}""");
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("duplicate JSON property name"), failure.Message);
        }

        [TestMethod]
        public void Parse_carries_spell_power_and_tradition_together()
        {
            string path = WriteSidecar(
                """{"spells":["sr6-spell-a"],"powers":[{"id":"sr6-power-a","rating":2}],"tradition":"qabala"}""");
            var expectations = DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path);
            Assert.HasCount(1, expectations.SpellIds);
            Assert.HasCount(1, expectations.PowerExpectations);
            Assert.AreEqual(("sr6-power-a", 2), expectations.PowerExpectations[0]);
            Assert.AreEqual("qabala", expectations.TraditionId);
        }

        [TestMethod]
        public void Parse_refuses_duplicate_properties_when_the_file_carries_a_bom()
        {
            // Controller9848: the single-read contract BOM-adjusts ONE shared
            // slice for both the duplicate scan and JsonDocument.Parse; a
            // BOM-prefixed duplicate must still be refused, not skipped.
            string json = """{"spells":[],"powers":[{"id":"sr6-power-a","id":"sr6-power-b","rating":2}]}""";
            string path = Path.Combine(Path.GetTempPath(), "t888-sidecar-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var failure = Assert.ThrowsExactly<InvalidOperationException>(
                () => DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path));
            Assert.IsTrue(failure.Message.Contains("duplicate JSON property name"), failure.Message);
        }

        [TestMethod]
        public void Parse_fails_closed_on_a_utf16_encoded_sidecar()
        {
            // Controller9848: the prior shape read the file as text for the
            // parse and as bytes for the scan, so a non-UTF-8 file could
            // yield two different views of the same bytes. The single-read
            // contract hands ONE byte slice to both lanes, so a UTF-16
            // sidecar fails closed in that lane instead of parsing.
            string json = """{"spells":["sr6-spell-a"],"powers":[]}""";
            string path = Path.Combine(Path.GetTempPath(), "t888-sidecar-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllBytes(path, System.Text.Encoding.Unicode.GetBytes(json));
            // The strict UTF-8 parser raises an internal JsonReaderException
            // (a JsonException subclass) here; that type cannot be named in
            // source, so catch the public base and pin the fail-closed
            // contract through its actual runtime type name.
            try
            {
                DesktopMouseFirstJourneyRunner.ParseMagicReopenSidecar(path);
                Assert.Fail("a UTF-16 encoded sidecar must fail closed, not parse");
            }
            catch (System.Text.Json.JsonException ex)
            {
                Assert.IsTrue(ex.GetType().Name.Contains("Json"),
                    "expected a JSON reader failure, got: " + ex.GetType().FullName);
            }
        }
    }
}
