using System;
using System.Linq;
using System.Xml.Linq;
using Chummer.Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Focused tests for the Root review corrections on PR #601: the seeded
/// starter-attribute evidence must (1) bind to the expected created workspace
/// and ruleset family and reject post-await workspace/owner transitions, and
/// (2) require exactly one valid totalvalue per each of the eight natural
/// attributes before summing. The pure helpers under test are the same code
/// path the journey runner executes.
/// </summary>
[TestClass]
public sealed class DesktopMouseFirstJourneyStarterAttributeBindingTests
{
    private const string CharacterTemplate =
        "<character><gameedition>{edition}</gameedition><attributes>{rows}</attributes></character>";

    private static XElement CharacterRoot(string edition, string rows)
    {
        return XElement.Parse(
            CharacterTemplate
                .Replace("{edition}", edition, StringComparison.Ordinal)
                .Replace("{rows}", rows, StringComparison.Ordinal));
    }

    private static string AttributeRow(string name, string totalValue)
    {
        return "<attribute><name>" + name + "</name><totalvalue>" + totalValue + "</totalvalue></attribute>";
    }

    private static string AllEight(int baseValue)
    {
        string[] names = { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL" };
        return string.Concat(names.Select(n => AttributeRow(n, baseValue.ToString(System.Globalization.CultureInfo.InvariantCulture))));
    }

    // ----- NormalizeRulesetFamily -----

    [TestMethod]
    [DataRow("sr5", "sr5")]
    [DataRow("SR5", "sr5")]
    [DataRow("sr6", "sr6")]
    [DataRow("5", "sr5")]
    [DataRow("6", "sr6")]
    [DataRow("SR5-CORE", "sr5")]
    public void NormalizeRulesetFamily_maps_common_ids_to_their_family(string input, string expected)
    {
        Assert.AreEqual(expected, Chummer.Avalonia.DesktopMouseFirstJourneyRunner.NormalizeRulesetFamily(input));
    }

    [TestMethod]
    public void NormalizeRulesetFamily_blank_input_maps_to_empty()
    {
        Assert.AreEqual(string.Empty, Chummer.Avalonia.DesktopMouseFirstJourneyRunner.NormalizeRulesetFamily(null));
        Assert.AreEqual(string.Empty, Chummer.Avalonia.DesktopMouseFirstJourneyRunner.NormalizeRulesetFamily("   "));
    }

    // ----- RulesetFamilyMatches -----

    [TestMethod]
    public void RulesetFamilyMatches_accepts_same_family_and_variant_labels()
    {
        Assert.IsTrue(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.RulesetFamilyMatches(
            "sr5", "sr5", out string diagnostic));
        Assert.AreEqual(string.Empty, diagnostic);
        Assert.IsTrue(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.RulesetFamilyMatches(
            "SR5", "sr5", out diagnostic));
        Assert.IsTrue(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.RulesetFamilyMatches(
            "sr5-core", "sr5", out diagnostic));
    }

    [TestMethod]
    public void RulesetFamilyMatches_rejects_cross_edition_documents()
    {
        Assert.IsFalse(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.RulesetFamilyMatches(
            "sr6", "sr5", out string diagnostic));
        Assert.IsTrue(diagnostic.Contains("does not match expected journey ruleset", StringComparison.Ordinal), diagnostic);
    }

    [TestMethod]
    public void RulesetFamilyMatches_rejects_empty_sides()
    {
        Assert.IsFalse(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.RulesetFamilyMatches(
            null, "sr5", out string nullDocument));
        Assert.IsTrue(nullDocument.Contains("no ruleset id", StringComparison.Ordinal), nullDocument);

        Assert.IsFalse(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.RulesetFamilyMatches(
            "sr5", string.Empty, out string emptyExpected));
        Assert.IsTrue(emptyExpected.Contains("expected ruleset family is empty", StringComparison.Ordinal), emptyExpected);
    }

    // ----- DocumentEditionMatches -----

    [TestMethod]
    public void DocumentEditionMatches_accepts_matching_edition_marker()
    {
        Assert.IsTrue(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.DocumentEditionMatches(
            "5", "sr5", out string diagnostic));
        Assert.AreEqual(string.Empty, diagnostic);
        Assert.IsTrue(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.DocumentEditionMatches(
            "5th", "sr5", out diagnostic));
    }

    [TestMethod]
    public void DocumentEditionMatches_requires_a_present_edition_marker()
    {
        Assert.IsFalse(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.DocumentEditionMatches(
            null, "sr5", out string missing));
        Assert.IsTrue(missing.Contains("no gameedition element", StringComparison.Ordinal), missing);

        Assert.IsFalse(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.DocumentEditionMatches(
            "  ", "sr5", out string blank));
        Assert.IsTrue(blank.Contains("no gameedition element", StringComparison.Ordinal), blank);
    }

    [TestMethod]
    public void DocumentEditionMatches_rejects_mismatched_edition_marker()
    {
        Assert.IsFalse(Chummer.Avalonia.DesktopMouseFirstJourneyRunner.DocumentEditionMatches(
            "6", "sr5", out string diagnostic));
        Assert.IsTrue(diagnostic.Contains("does not match expected journey ruleset", StringComparison.Ordinal), diagnostic);
    }

    // ----- SumNaturalAttributes (exactly-one-valid-value-per-attribute) -----

    [TestMethod]
    public void SumNaturalAttributes_accepts_exactly_one_valid_total_per_attribute()
    {
        XElement root = CharacterRoot("5", AllEight(3) + AttributeRow("EDG", "1"));
        Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence evidence =
            Chummer.Avalonia.DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsTrue(evidence.IsValid, evidence.Diagnostics);
        Assert.AreEqual(24, evidence.Sum);
        Assert.IsTrue(evidence.Diagnostics.Contains("exactly one valid totalvalue", StringComparison.Ordinal), evidence.Diagnostics);
    }

    [TestMethod]
    public void SumNaturalAttributes_rejects_a_missing_natural_attribute()
    {
        string rows = string.Concat(
            new[] { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG" }.Select(n => AttributeRow(n, "3")));
        XElement root = CharacterRoot("5", rows);
        Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence evidence =
            Chummer.Avalonia.DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsFalse(evidence.IsValid);
        Assert.AreEqual(-1, evidence.Sum);
        Assert.IsTrue(evidence.Diagnostics.Contains("missing natural attributes: WIL", StringComparison.Ordinal), evidence.Diagnostics);
    }

    [TestMethod]
    public void SumNaturalAttributes_rejects_a_duplicate_natural_attribute()
    {
        XElement root = CharacterRoot("5", AllEight(3) + AttributeRow("BOD", "4"));
        Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence evidence =
            Chummer.Avalonia.DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsFalse(evidence.IsValid);
        Assert.IsTrue(evidence.Diagnostics.Contains("duplicate natural attribute entry: BOD", StringComparison.Ordinal), evidence.Diagnostics);
    }

    [TestMethod]
    public void SumNaturalAttributes_rejects_a_malformed_totalvalue()
    {
        string rows = string.Concat(
            new[] { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL" }
                .Select(n => n == "WIL" ? AttributeRow(n, "three") : AttributeRow(n, "3")));
        XElement root = CharacterRoot("5", rows);
        Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence evidence =
            Chummer.Avalonia.DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsFalse(evidence.IsValid);
        Assert.IsTrue(evidence.Diagnostics.Contains("WIL has missing or malformed totalvalue", StringComparison.Ordinal), evidence.Diagnostics);
    }

    [TestMethod]
    public void SumNaturalAttributes_rejects_a_natural_attribute_entry_with_no_totalvalue_element()
    {
        XElement root = CharacterRoot(
            "5",
            AllEight(3) + "<attribute><name>WIL</name></attribute>");
        Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence evidence =
            Chummer.Avalonia.DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsFalse(evidence.IsValid);
        Assert.IsTrue(evidence.Diagnostics.Contains("WIL has missing or malformed totalvalue", StringComparison.Ordinal), evidence.Diagnostics);
    }

    [TestMethod]
    public void SumNaturalAttributes_ignores_special_attributes_but_still_requires_the_eight()
    {
        XElement root = CharacterRoot("5", AllEight(3) + AttributeRow("MAG", "0") + AttributeRow("EDG", "2"));
        Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence evidence =
            Chummer.Avalonia.DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsTrue(evidence.IsValid, evidence.Diagnostics);
        Assert.AreEqual(24, evidence.Sum);
    }

    [TestMethod]
    public void SumNaturalAttributes_rejects_blank_attribute_names()
    {
        XElement root = CharacterRoot(
            "5",
            AllEight(3) + "<attribute><name>  </name><totalvalue>5</totalvalue></attribute>");
        Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence evidence =
            Chummer.Avalonia.DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsFalse(evidence.IsValid);
        Assert.IsTrue(evidence.Diagnostics.Contains("blank name", StringComparison.Ordinal), evidence.Diagnostics);
    }

    [TestMethod]
    public void SumNaturalAttributes_without_an_attributes_section_is_rejected()
    {
        XElement root = XElement.Parse("<character><gameedition>5</gameedition></character>");
        Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence evidence =
            Chummer.Avalonia.DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsFalse(evidence.IsValid);
        Assert.IsTrue(evidence.Diagnostics.Contains("missing natural attributes", StringComparison.Ordinal), evidence.Diagnostics);
    }

    // ----- StarterAttributeEvidence contract -----

    [TestMethod]
    public void StarterAttributeEvidence_is_invalid_only_for_negative_sums()
    {
        Assert.IsTrue(new Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence(0, "zero").IsValid);
        Assert.IsTrue(new Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence(28, "seeded").IsValid);
        Assert.IsFalse(new Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence(-1, "rejected").IsValid);
    }
}
