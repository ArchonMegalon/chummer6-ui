using System;
using System.Linq;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Avalonia;
using Chummer.Contracts.Characters;

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
    [TestMethod]
    [DataRow("unchanged", true)]
    [DataRow("workspace-transition", false)]
    [DataRow("owner-transition", false)]
    [DataRow("owner-aba", false)]
    [DataRow("wrong-response", false)]
    [DataRow("wrong-ruleset", false)]
    [DataRow("wrong-edition", false)]
    [DataRow("duplicate-edition", false)]
    [DataRow("unavailable", false)]
    public async Task Async_read_attributes_only_the_exact_created_runner_and_unchanged_owner(string change, bool valid)
    {
        CharacterWorkspaceId id = new("created-runner");
        CharacterWorkspaceId? active = id;
        OwnerContextStamp owner = new(new OwnerScope("install-account:starter-test"), "test-authority", 1);
        OwnerContextStamp original = owner;
        var pending = new TaskCompletionSource<CommandResult<WorkspaceDocumentSnapshot>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        Task<DesktopMouseFirstJourneyRunner.StarterAttributeEvidence> observed =
            DesktopMouseFirstJourneyRunner.ReadWorkspaceNaturalAttributeTotalAsync(
                "sr5", id.Value, () => active, () => owner,
                (capturedOwner, capturedId, _) =>
                {
                    Assert.AreEqual(original, capturedOwner);
                    Assert.AreEqual(id, capturedId);
                    reads++;
                    return pending.Task;
                }, CancellationToken.None);
        Assert.IsFalse(observed.IsCompleted, "The identity change must occur across an actual pending read.");
        if (change == "workspace-transition") active = new("another-runner");
        if (change == "owner-transition") owner = owner with { Owner = new OwnerScope("install-account:another"), TransitionRevision = 2 };
        if (change == "owner-aba") owner = owner with { TransitionRevision = 3 };
        XElement xml = CharacterRoot(change == "wrong-edition" ? "6" : "5", AllEight(3));
        if (change == "duplicate-edition") xml.Add(new XElement("gameedition", "6"));
        WorkspaceDocumentSnapshot snapshot = new(
            change == "wrong-response" ? new CharacterWorkspaceId("another-runner") : id,
            new WorkspaceDocument(xml.ToString(), change == "wrong-ruleset" ? "sr6" : "sr5"),
            DateTimeOffset.UtcNow, 1, 1);
        pending.SetResult(new(true, snapshot, null,
            change == "unavailable" ? WorkspaceOperationOutcome.Unavailable : WorkspaceOperationOutcome.Success));
        var result = await observed;
        Assert.AreEqual(1, reads);
        Assert.AreEqual(valid, result.IsValid, result.Diagnostics);
        Assert.AreEqual(valid ? 24 : -1, result.Sum);
    }

    [TestMethod]
    [DataRow(null, "created-runner", "sr5")]
    [DataRow("", "created-runner", "sr5")]
    [DataRow("created-runner", "other-runner", "sr5")]
    [DataRow("created-runner", "created-runner", "sr6")]
    public async Task Missing_or_wrong_created_identity_is_rejected_before_read(string? expected, string active, string ruleset)
    {
        var result = await DesktopMouseFirstJourneyRunner.ReadWorkspaceNaturalAttributeTotalAsync(
            ruleset, expected, () => new CharacterWorkspaceId(active),
            () => new OwnerContextStamp(new OwnerScope("install-account:starter-test"), "test-authority", 1),
            (_, _, _) => throw new AssertFailedException("An unbound runner must never be read."), CancellationToken.None);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    [DataRow("sr50")]
    [DataRow("5garbage")]
    [DataRow("unknown")]
    public void Ambiguous_edition_prefixes_do_not_establish_SR5_identity(string value)
    {
        Assert.IsFalse(DesktopMouseFirstJourneyRunner.RulesetFamilyMatches(value, "sr5", out _));
        Assert.IsFalse(DesktopMouseFirstJourneyRunner.DocumentEditionMatches(value, "sr5", out _));
        Assert.IsFalse(DesktopMouseFirstJourneyRunner.RulesetFamilyMatches(value, value, out _));
    }

    [TestMethod]
    [DataRow("containers")]
    [DataRow("names")]
    [DataRow("values")]
    [DataRow("nested-value")]
    [DataRow("negative")]
    [DataRow("overflow")]
    public void Ambiguous_or_unrepresentable_natural_values_fail_without_partial_evidence(string shape)
    {
        XElement root = CharacterRoot("5", AllEight(3));
        XElement attributes = root.Element("attributes")!;
        XElement first = attributes.Element("attribute")!;
        if (shape == "containers") root.Add(new XElement(attributes));
        if (shape == "names") first.Add(new XElement("name", "AGI"));
        if (shape == "values") first.Add(new XElement("totalvalue", "4"));
        if (shape == "nested-value") first.Element("totalvalue")!.ReplaceNodes(new XElement("value", "3"));
        if (shape == "negative") first.Element("totalvalue")!.Value = "-1";
        if (shape == "overflow")
            foreach (XElement value in attributes.Descendants("totalvalue")) value.Value = int.MaxValue.ToString();
        var result = DesktopMouseFirstJourneyRunner.SumNaturalAttributes(root);
        Assert.IsFalse(result.IsValid, result.Diagnostics);
        Assert.AreEqual(-1, result.Sum);
    }

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

    // ----- WIN-009 gen2 (T747): exact-character parser + classifier -----

    private static string ExactRow(string name, int total, int minimum) =>
        "<attribute><name>" + name + "</name><totalvalue>" + total.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + "</totalvalue><metatypemin>" + minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + "</metatypemin><metatypemax>6</metatypemax><metatypeaugmax>10</metatypeaugmax></attribute>";

    // 8 naturals, all minimum 1, each spent 1 normal point: 8 total/used.
    private static string AllEightExact() =>
        string.Concat(new[] { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL" }
            .Select(n => ExactRow(n, 2, 1)));

    private static CharacterCreationPriorityHeritageSelection DwarfHeritage(string metatypeSourceId) => new(
        SelectionId: "metatype:dwarf-selection",
        Kind: "metatype",
        PrioritySourceId: "priority-table-heritage-c",
        MetatypeSourceId: metatypeSourceId,
        MetavariantSourceId: null,
        MetatypeName: "Dwarf",
        MetavariantName: null,
        SpecialAttributePoints: 1,
        KarmaCost: 0,
        HalvesNormalAttributePoints: false,
        Attributes: new[]
        {
            new CharacterCreationMetatypeAttributeProjection("BOD", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("AGI", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("REA", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("STR", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("CHA", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("INT", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("LOG", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("WIL", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("EDG", 1, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("MAG", 0, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("RES", 0, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("ESS", 0, 6, 10),
            new CharacterCreationMetatypeAttributeProjection("DEP", 0, 0, 0),
        },
        PriorityChildNodeDigest: "sha256:prioc",
        MetatypeSourceNodeDigest: "sha256:dwarfnode",
        SourceAnchorIds: new[] { "metatypes.xml#metatype:dwarf" });

    private static CharacterCreationPriorityAssignment HeritageAssignment(string rank) => new(
        Order: 0,
        CategoryId: "heritage",
        Rank: rank,
        SourceId: "priority-table-heritage-c",
        SourceNodeDigest: "sha256:prioc",
        SumToTenValue: 0,
        BaseNormalAttributePoints: 8,
        SourceAnchorIds: new[] { "priority.xml#heritage:c" });

    private static CharacterCreationPriorityAssignment MetatypeAssignment => new(
        Order: 1,
        CategoryId: "metatype",
        Rank: "Dwarf",
        SourceId: "metatypes.xml#dwarf",
        SourceNodeDigest: "sha256:dwarfnode",
        SumToTenValue: 0,
        BaseNormalAttributePoints: null,
        SourceAnchorIds: new[] { "metatypes.xml#metatype:dwarf" });

    private static CharacterCreationPrerequisiteDraft ExactPrerequisite(
        CharacterCreationPriorityHeritageSelection heritage,
        string rank,
        int effectiveNormal,
        int totalSpecial) => new(
        Schema: CharacterCreationPrerequisiteSchemas.DraftV1,
        WorkspaceId: new CharacterWorkspaceId("exact-character-workspace"),
        DraftRevision: 1,
        BaseContentRevision: 1,
        BaseRawCharacterXmlDigest: "sha256:raw",
        AuthorityDigest: "sha256:authority",
        BuildMethod: "Priority",
        SettingsProfileId: "profile",
        PriorityTable: "SR5",
        PriorityArray: new[] { "C", "C", "D", "E", "E" },
        SumToTenTarget: null,
        Assignments: new[] { HeritageAssignment(rank), MetatypeAssignment },
        CreationKarmaTotal: 25,
        CreationKarmaUsed: 0,
        SourceAnchorIds: new[] { "priority.xml" },
        DraftDigest: "sha256:prereq-draft")
    {
        HeritageSelection = heritage,
        EffectiveNormalAttributePoints = effectiveNormal,
        TotalSpecialAttributePoints = totalSpecial,
    };

    private static CharacterCreationAttributesDraft ExactAttributes(
        string metatypeSourceId,
        int normalTotal,
        int specialTotal,
        int specialUsed,
        int dwarfSpecialMinimum) => new(
        Schema: CharacterCreationAttributesSchemas.DraftV1,
        WorkspaceId: new CharacterWorkspaceId("exact-character-workspace"),
        DraftRevision: 1,
        BaseContentRevision: 1,
        BaseRawCharacterXmlDigest: "sha256:raw",
        PrerequisiteDraftRevision: 1,
        PrerequisiteDraftDigest: "sha256:prereq-draft",
        PrerequisiteAuthorityDigest: "sha256:authority",
        MetatypeSourceId: metatypeSourceId,
        MetatypeSourceNodeDigest: "sha256:dwarfnode",
        HalvesNormalAttributePoints: false,
        NormalPointTotal: normalTotal,
        NormalPointUsed: normalTotal,
        SpecialPointTotal: specialTotal,
        SpecialPointUsed: specialUsed,
        CreationKarmaTotal: 25,
        CreationKarmaUsed: 0,
        Allocations: FullAllocations(edgPoints: 0),
        Attributes: FullProjections(edgMinimum: dwarfSpecialMinimum, edgCurrent: dwarfSpecialMinimum),
        SourceAnchorIds: new[] { "attributes.xml" },
        CharacterEffectsApplied: true,
        DraftDigest: "sha256:attr-draft");

    private static CharacterCreationAttributeProjection Projection(string id, string category, int minimum, int current) => new(
        AttributeId: id,
        Category: category,
        Minimum: minimum,
        Maximum: 6,
        AugmentedMaximum: 10,
        Current: current,
        PriorityPointsSpent: 0,
        KarmaLevels: 0,
        PriorityPointCost: 0,
        KarmaCost: 0,
        IsEnabled: true,
        DisableReasons: Array.Empty<string>(),
        SourceAnchorIds: Array.Empty<string>());

    private static readonly string[] NaturalIds =
        { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL" };

    /// <summary>
    /// WIN-009 gen3 (T772): the real creation contract (Core
    /// CharacterCreationAttributesService.BuildDraft) emits exactly one
    /// allocation per projected heritage range, with the normal budget
    /// spend carried by the natural rows' PriorityPoints.
    /// </summary>
    private static List<CharacterCreationAttributeAllocation> FullAllocations(int edgPoints) => new()
    {
        new CharacterCreationAttributeAllocation("BOD", 1, 0),
        new CharacterCreationAttributeAllocation("AGI", 1, 0),
        new CharacterCreationAttributeAllocation("REA", 1, 0),
        new CharacterCreationAttributeAllocation("STR", 1, 0),
        new CharacterCreationAttributeAllocation("CHA", 1, 0),
        new CharacterCreationAttributeAllocation("INT", 1, 0),
        new CharacterCreationAttributeAllocation("LOG", 1, 0),
        new CharacterCreationAttributeAllocation("WIL", 1, 0),
        new CharacterCreationAttributeAllocation("EDG", edgPoints, 0),
        new CharacterCreationAttributeAllocation("MAG", 0, 0),
        new CharacterCreationAttributeAllocation("RES", 0, 0),
        new CharacterCreationAttributeAllocation("ESS", 0, 0),
        new CharacterCreationAttributeAllocation("DEP", 0, 0),
    };

    private static List<CharacterCreationAttributeProjection> FullProjections(int edgMinimum, int edgCurrent) => new()
    {
        Projection("BOD", "normal", 1, 2), Projection("AGI", "normal", 1, 2),
        Projection("REA", "normal", 1, 2), Projection("STR", "normal", 1, 2),
        Projection("CHA", "normal", 1, 2), Projection("INT", "normal", 1, 2),
        Projection("LOG", "normal", 1, 2), Projection("WIL", "normal", 1, 2),
        Projection("EDG", "special", edgMinimum, edgCurrent),
        Projection("MAG", "special", 0, 0),
        Projection("RES", "special", 0, 0),
        Projection("ESS", "special", 0, 6),
        Projection("DEP", "special", 0, 0),
    };

    /// <summary>XML tail: EDG plus the non-natural special rows a real
    /// finalized Dwarf workspace carries (MAG/RES/DEP at minimum, ESS 6/0).</summary>
    private static string SpecialTailRows(int edgTotal) =>
        ExactRow("EDG", edgTotal, 1) + ExactRow("MAG", 0, 0) + ExactRow("RES", 0, 0)
        + ExactRow("ESS", 6, 0) + ExactRow("DEP", 0, 0);

    private static string FullDwarfRows() => AllEightExact() + SpecialTailRows(1);

    private static string FullDwarfRowsEdgRaised() => AllEightExact() + SpecialTailRows(2);

    private const string ExactCharacterTemplate =
        "<character><gameedition>5</gameedition><metatype>Dwarf</metatype>"
        + "<attributes>{rows}</attributes></character>";

    private static XElement ExactCharacterRoot(string rows) =>
        XElement.Parse(ExactCharacterTemplate.Replace("{rows}", rows, StringComparison.Ordinal));

    private static (IReadOnlyDictionary<string, int>, IReadOnlyDictionary<string, int>, string) ParseRows(XElement root)
    {
        var parsed = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ParseExactCharacterRows(root);
        Assert.IsTrue(parsed.Succeeded, parsed.Diagnostics);
        return (parsed.Totals, parsed.Minimums, parsed.MetatypeName);
    }

    private static void AssertFailedClassify(
        (IReadOnlyDictionary<string, int>, IReadOnlyDictionary<string, int>, string) parsed,
        string? expectedRank,
        string? expectedMetatype,
        CharacterCreationPrerequisiteDraft prerequisite,
        CharacterCreationAttributesDraft attributes,
        string expectedFragment)
    {
        string? verdict = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyExactCharacterAgainstCreationDrafts(
            parsed.Item1, parsed.Item2, parsed.Item3, expectedRank, expectedMetatype, prerequisite, attributes, "exact-character-workspace");
        Assert.IsNotNull(verdict);
        Assert.IsTrue(verdict.Contains(expectedFragment, StringComparison.Ordinal), verdict);
    }

    [TestMethod]
    public void ParseExactCharacterRows_parses_the_full_dwarf_row_set()
    {
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var rows = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ParseExactCharacterRows(root);
        Assert.IsTrue(rows.Succeeded, rows.Diagnostics);
        Assert.AreEqual("Dwarf", rows.MetatypeName);
        Assert.AreEqual(13, rows.Totals.Count);
        Assert.AreEqual(2, rows.Totals["BOD"]);
        Assert.AreEqual(1, rows.Minimums["AGI"]);
        Assert.AreEqual(1, rows.Minimums["EDG"]);
        Assert.AreEqual(0, rows.Minimums["ESS"]);
    }

    [TestMethod]
    public void ParseExactCharacterRows_rejects_a_duplicate_metatype_element()
    {
        XElement root = ExactCharacterRoot(AllEightExact() + ExactRow("EDG", 1, 1));
        root.Add(new XElement("metatype", "Elf"));
        var rows = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ParseExactCharacterRows(root);
        Assert.IsFalse(rows.Succeeded);
        Assert.IsTrue(rows.Diagnostics.Contains("exactly one scalar metatype", StringComparison.Ordinal), rows.Diagnostics);
    }

    [TestMethod]
    public void ParseExactCharacterRows_rejects_missing_or_malformed_metatypemin()
    {
        XElement root = ExactCharacterRoot(AllEightExact() + ExactRow("EDG", 1, 1));
        XElement bod = root.Element("attributes")!.Elements("attribute")
            .First(a => a.Element("name")!.Value == "BOD");
        bod.Element("metatypemin")!.Remove();
        var rows = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ParseExactCharacterRows(root);
        Assert.IsFalse(rows.Succeeded);
        Assert.IsTrue(rows.Diagnostics.Contains("BOD has missing or malformed metatypemin", StringComparison.Ordinal), rows.Diagnostics);
    }

    [TestMethod]
    public void Classify_exact_matches_when_every_fact_agrees()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        string? verdict = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyExactCharacterAgainstCreationDrafts(
            parsed.Item1, parsed.Item2, parsed.Item3, "C", "Dwarf", prerequisite, attributes, "exact-character-workspace");
        Assert.IsNull(verdict);
    }

    [TestMethod]
    public void Classify_rejects_a_metatype_name_mismatch()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Elf", prerequisite, attributes,
            "does not match the plan's metatype 'Elf'");
    }

    [TestMethod]
    public void Classify_rejects_a_metatype_source_mismatch()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var attributes = ExactAttributes("other-source", 8, 1, 0, 1);
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "does not match the heritage selection source");
    }

    [TestMethod]
    public void Classify_rejects_a_heritage_rank_mismatch()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "D", "Dwarf", prerequisite, attributes,
            "heritage assignment rank 'C' does not match the plan's heritage rank 'D'");
    }

    [TestMethod]
    public void Classify_rejects_a_per_attribute_total_mismatch()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        XElement root = ExactCharacterRoot(FullDwarfRows());
        XElement bod = root.Element("attributes")!.Elements("attribute")
            .First(a => a.Element("name")!.Value == "BOD");
        bod.Element("totalvalue")!.Value = "4";
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "BOD total 4 does not match draft current 2");
    }

    [TestMethod]
    public void Classify_rejects_a_budget_that_is_not_fully_spent()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        // All rows match the draft (per-attribute checks pass); the draft
        // itself under-states the total so the budget is not fully spent.
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1)
            with { NormalPointTotal = 9 };
        string rows = string.Concat(NaturalIds.Select(n => ExactRow(n, 2, 1)))
            + SpecialTailRows(1);
        XElement root = ExactCharacterRoot(rows);
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "not fully spent");
    }

    [TestMethod]
    public void Classify_rejects_a_special_budget_overspend()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        // Draft says the single special point WAS allocated to EDG...
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 1, 1);
        // ...but the XML shows EDG still at its metatype minimum.
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "special attribute budget spent 0 does not match the draft's used 1");
    }

    [TestMethod]
    public void Classify_counts_special_spend_from_allocations_not_essence()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        // Draft allocates the single special point to EDG; the XML raises EDG
        // by exactly 1. Essence (ESS 6/0) is a metatype fact with NO
        // allocation, so it must NOT count as special-point spend even though
        // its XML delta is 6.
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 1, 1)
            with
            {
                Allocations = FullAllocations(edgPoints: 1),
                Attributes = FullProjections(edgMinimum: 1, edgCurrent: 2),
            };
        XElement root = ExactCharacterRoot(FullDwarfRowsEdgRaised());
        var parsed = ParseRows(root);
        string? verdict = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyExactCharacterAgainstCreationDrafts(
            parsed.Item1, parsed.Item2, parsed.Item3, "C", "Dwarf", prerequisite, attributes, "exact-character-workspace");
        Assert.IsNull(verdict);
    }

    [TestMethod]
    public void Classify_accepts_core_emitted_essence_allocation_with_zero_points()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        // REVIEW8664 regression: Core emits a metatype-granted special projection
        // for essence (ESS 6/0) WITH a zero-point allocation row alongside the
        // EDG allocation. The pre-a0b59338c delta-based classifier iterated
        // allocation rows and counted the ESS XML delta (6) as special spend,
        // refusing every legitimately finalized Dwarf. The allocation-backed
        // classifier counts PriorityPoints (0 for ESS, 1 for EDG) and must
        // accept. This fixture fails on the OLD implementation and passes on
        // the NEW one.
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 1, 1)
            with
            {
                Allocations = FullAllocations(edgPoints: 1),
                Attributes = FullProjections(edgMinimum: 1, edgCurrent: 2),
            };
        XElement root = ExactCharacterRoot(FullDwarfRowsEdgRaised());
        var parsed = ParseRows(root);
        string? verdict = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyExactCharacterAgainstCreationDrafts(
            parsed.Item1, parsed.Item2, parsed.Item3, "C", "Dwarf", prerequisite, attributes, "exact-character-workspace");
        Assert.IsNull(verdict);
    }
    [TestMethod]
    public void Classify_rejects_an_allocation_not_backed_by_the_document()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        // Draft allocates one special point to EDG but the document leaves
        // EDG at its metatype minimum: the allocation is not backed by the
        // XML facts.
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 1, 1)
            with { Allocations = FullAllocations(edgPoints: 1) };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "shows 0 raised points but the draft allocated 1");
    }

    [TestMethod]
    public void Classify_rejects_missing_drafts()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        XElement root = ExactCharacterRoot(AllEightExact() + ExactRow("EDG", 1, 1));
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", null, null,
            "carries no prerequisite draft");
        var attributesOnly = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        AssertFailedClassify(parsed, "C", "Dwarf", null, attributesOnly,
            "carries no prerequisite draft");
    }

    [TestMethod]
    public void Classify_rejects_a_heritage_selection_missing_from_the_draft()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1)
            with { HeritageSelection = null };
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "carries no heritage selection");
    }

    // ----- StarterAttributeEvidence contract -----

    [TestMethod]
    public void Classify_rejects_a_prerequisite_draft_from_a_foreign_workspace()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1)
            with { WorkspaceId = new CharacterWorkspaceId("some-other-workspace") };
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "prerequisite draft workspace 'some-other-workspace' does not match the loaded workspace");
    }

    [TestMethod]
    public void Classify_rejects_an_attributes_draft_from_a_foreign_workspace()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1)
            with { WorkspaceId = new CharacterWorkspaceId("unrelated-workspace") };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "attributes draft workspace 'unrelated-workspace' does not match the loaded workspace");
    }

    [TestMethod]
    public void Classify_rejects_an_attributes_draft_linked_to_a_different_prerequisite()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        // Same workspace, same overall shape, but the attributes draft points at
        // a DIFFERENT prerequisite chain (revision bumped): the linkage fields
        // must be bound to the exact archived prerequisite.
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1)
            with { PrerequisiteDraftRevision = 2 };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "does not match the archived prerequisite revision 1");
    }

    [TestMethod]
    public void Classify_rejects_an_attributes_draft_with_a_foreign_prerequisite_digest()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1)
            with { PrerequisiteDraftDigest = "sha256:other-chain" };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "does not match the archived prerequisite digest 'sha256:prereq-draft'");
    }

    [TestMethod]
    public void Classify_rejects_an_attributes_draft_with_a_foreign_authority_digest()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var attributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1)
            with { PrerequisiteAuthorityDigest = "sha256:other-authority" };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "does not match the archived prerequisite authority 'sha256:authority'");
    }
    [TestMethod]
    public void Classify_rejects_an_empty_archived_projection_set()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes
            with { Attributes = Array.Empty<CharacterCreationAttributeProjection>() };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "carries an empty attribute projection set");
    }

    [TestMethod]
    public void Classify_rejects_a_duplicate_projection_row()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes
            with { Attributes = baseAttributes.Attributes.Append(Projection("BOD", "normal", 1, 2)).ToList() };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "duplicate draft attribute projection rows for BOD");
    }

    [TestMethod]
    public void Classify_rejects_a_projection_set_missing_a_natural_attribute()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes
            with { Attributes = baseAttributes.Attributes.Where(a => a.AttributeId != "REA").ToList() };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "draft attribute projection set is missing natural attributes REA");
    }

    [TestMethod]
    public void Classify_rejects_an_allocation_whose_row_the_projection_set_omits()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes
            with
            {
                Allocations = baseAttributes.Allocations
                    .Append(new CharacterCreationAttributeAllocation("ZZZ", 0, 0))
                    .ToList(),
            };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "draft allocation references attribute ZZZ that the archived projection set omits");
    }

    [TestMethod]
    public void Classify_rejects_special_spend_without_a_special_allocation()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        // Special budget records spend, but every allocation row is zero-point:
        // no special allocation carries the spend, so the budget is unbound.
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 1, 1);
        var attributes = baseAttributes
            with
            {
                Allocations = new List<CharacterCreationAttributeAllocation>(),
                Attributes = baseAttributes.Attributes
                    .Where(a => a.Category == "normal")
                    .ToList(),
            };
        XElement root = ExactCharacterRoot(FullDwarfRowsEdgRaised());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "draft records special budget used 1 with no special allocation binding");
    }
    [TestMethod]
    public void Classify_rejects_a_zero_spend_special_omission_from_the_draft()
    {
        // WIN-009 gen3 (T772/U1): eight naturals plus EMPTY special
        // projections/allocations must NOT pass — the applicable special set
        // is bound to the heritage ranges, so an omitted EDG row is refused
        // even at zero spend.
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            Allocations = baseAttributes.Allocations
                .Where(a => NaturalIds.Contains(a.AttributeId, StringComparer.Ordinal))
                .ToList(),
            Attributes = baseAttributes.Attributes
                .Where(a => NaturalIds.Contains(a.AttributeId, StringComparer.Ordinal))
                .ToList(),
        };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "heritage attribute ranges have no draft projection rows: EDG");
    }

    [TestMethod]
    public void Classify_rejects_a_special_row_not_backed_by_heritage_ranges()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            Attributes = baseAttributes.Attributes
                .Append(Projection("XYZ", "special", 0, 0))
                .ToList(),
        };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "are not backed by the heritage attribute ranges");
    }

    [TestMethod]
    public void Classify_rejects_points_on_a_nonspendable_special()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        // Essence is a metatype fact, never a spendable special: an
        // allocation carrying points on it is refused by the contract-bound
        // special loop.
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            Allocations = baseAttributes.Allocations
                .Select(a => string.Equals(a.AttributeId, "ESS", StringComparison.Ordinal)
                    ? new CharacterCreationAttributeAllocation(a.AttributeId, 2, 0)
                    : a)
                .ToList(),
        };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "ESS is not spendable in the creation contract but the draft allocated 2");
    }

    [TestMethod]
    public void Classify_rejects_a_case_variant_duplicate_projection_row()
    {
        // WIN-009 gen3 (T772/U2): duplicate detection and XML matching must
        // share one identity rule — a case-variant duplicate (bod vs BOD) is
        // a duplicate, not a second independent row.
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            Attributes = baseAttributes.Attributes
                .Append(Projection("bod", "normal", 1, 2))
                .ToList(),
        };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "duplicate draft attribute projection rows");
    }

    [TestMethod]
    public void Classify_rejects_a_case_variant_duplicate_allocation_row()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            Allocations = baseAttributes.Allocations
                .Append(new CharacterCreationAttributeAllocation("edg", 0, 0))
                .ToList(),
        };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "duplicate draft allocation rows");
    }

    // WIN-009 gen4 (REVIEW8769 U3): MAGAdept requires the source-admitted
    // separate-magic contract. The only Core route folding the Mystic Adept
    // power-point policy anchors into the attributes draft is
    // state.SeparateMagicPolicy, which is assigned only when the prerequisite
    // talent is Mystic Adept AND the policy has UsesSeparateMagicAttribute
    // true. The proof helper therefore requires the full policy anchor triple
    // against one settings base; a Mundane draft (no talent) with invented
    // MAGAdept rows refuses even at zero points on the projection side, and a
    // genuine Mystic Adept draft with the policy anchors passes.

    private const string SeparateMagicSettingsBase = "settings.xml#setting:profile";

    private static readonly string[] SeparateMagicAnchors =
    {
        SeparateMagicSettingsBase + ":karmacost/karmamysadpp",
        SeparateMagicSettingsBase + ":mysadeptsecondmagattribute",
        SeparateMagicSettingsBase + ":priorityspellsasadeptpowers",
    };

    private static CharacterCreationPrerequisiteDraft MysticAdeptPrerequisite(
        CharacterCreationPriorityHeritageSelection heritage,
        string rank,
        int effectiveNormal,
        int totalSpecial) => ExactPrerequisite(heritage, rank, effectiveNormal, totalSpecial) with
    {
        TalentSelection = new CharacterCreationPriorityTalentSelection(
            SelectionId: "metatype:dwarf-selection:talent:0",
            PrioritySourceId: "priority-table-heritage-c",
            Name: "Talent",
            Value: "Mystic Adept",
            SpecialAttributePoints: 1,
            Magic: 4,
            Resonance: null,
            Depth: null,
            GrantedQualities: new[] { "Mystic Adept" },
            PriorityChildNodeDigest: "sha256:prioc-talent",
            SourceAnchorIds: new[] { "priority.xml#heritage:c:talent:0" })
    };

    [TestMethod]
    public void Classify_rejects_mundane_invented_magadept_projection_rows()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            Attributes = baseAttributes.Attributes
                .Append(Projection("MAGAdept", "special", 0, 0))
                .ToList(),
        };
        XElement root = ExactCharacterRoot(FullDwarfRows());
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "carry no source-admitted separate-magic policy proof");
    }

    [TestMethod]
    public void Classify_rejects_mundane_invented_magadept_allocation_points()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            Attributes = baseAttributes.Attributes
                .Append(Projection("MAGAdept", "special", 0, 1))
                .ToList(),
            Allocations = baseAttributes.Allocations
                .Append(new CharacterCreationAttributeAllocation("MAGAdept", 1, 0))
                .ToList(),
        };
        XElement root = ExactCharacterRoot(FullDwarfRows() + ExactRow("MAGAdept", 1, 0));
        var parsed = ParseRows(root);
        // With NO proof anchors at all the projection-side proof gate fires
        // before the spend gate; the spend gate itself is covered by the
        // forged-anchors test below.
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "carry no source-admitted separate-magic policy proof");
    }

    [TestMethod]
    public void Classify_rejects_magadept_points_without_full_policy_anchor_triple()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        // Forged shape: the draft carries the FULL policy anchor triple but the
        // prerequisite talent is Mundane - Core would never bind the policy
        // anchors for a Mundane talent, so the spend gate must refuse on the
        // talent side even with proof-shaped anchors present.
        var prerequisite = ExactPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            SourceAnchorIds = new[] { "attributes.xml" }.Concat(SeparateMagicAnchors).ToArray(),
            Attributes = baseAttributes.Attributes
                .Append(Projection("MAGAdept", "special", 0, 1))
                .ToList(),
            Allocations = baseAttributes.Allocations
                .Append(new CharacterCreationAttributeAllocation("MAGAdept", 1, 0))
                .ToList(),
        };
        XElement root = ExactCharacterRoot(FullDwarfRows() + ExactRow("MAGAdept", 1, 0));
        var parsed = ParseRows(root);
        AssertFailedClassify(parsed, "C", "Dwarf", prerequisite, attributes,
            "is not spendable: the separate-magic contract is not source-admitted");
    }

    [TestMethod]
    public void Classify_accepts_source_admitted_separate_magic_magadept()
    {
        var heritage = DwarfHeritage("08f2c9cc-f9f8-4f1a-9efb-63555af71788");
        var prerequisite = MysticAdeptPrerequisite(heritage, "C", 8, 1);
        var baseAttributes = ExactAttributes(heritage.MetatypeSourceId, 8, 1, 0, 1);
        var attributes = baseAttributes with
        {
            SourceAnchorIds = new[] { "attributes.xml" }.Concat(SeparateMagicAnchors).ToArray(),
            Attributes = baseAttributes.Attributes
                .Append(Projection("MAGAdept", "special", 0, 1))
                .ToList(),
            Allocations = baseAttributes.Allocations
                .Append(new CharacterCreationAttributeAllocation("MAGAdept", 1, 0))
                .ToList(),
            SpecialPointUsed = 1,
        };
        XElement root = ExactCharacterRoot(FullDwarfRows() + ExactRow("MAGAdept", 1, 0));
        var parsed = ParseRows(root);
        string? verdict = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.ClassifyExactCharacterAgainstCreationDrafts(
            parsed.Item1, parsed.Item2, parsed.Item3, "C", "Dwarf", prerequisite, attributes, "exact-character-workspace");
        Assert.IsNull(verdict);
    }

    [TestMethod]
    public void StarterAttributeEvidence_is_invalid_only_for_negative_sums()
    {
        Assert.IsTrue(new Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence(0, "zero").IsValid);
        Assert.IsTrue(new Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence(28, "seeded").IsValid);
        Assert.IsFalse(new Chummer.Avalonia.DesktopMouseFirstJourneyRunner.StarterAttributeEvidence(-1, "rejected").IsValid);
    }

    [TestMethod]
    public void Career_identity_classifier_accepts_the_created_identity()
    {
        string? error = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.BuildCareerWorkspaceIdentityError(
            new CharacterWorkspaceId("created-workspace"),
            CareerProfile("T781 Dwarf Qualifier", "T78D", created: true),
            "created-workspace",
            "T781 Dwarf Qualifier",
            "T78D");
        Assert.IsNull(error);
    }

    [TestMethod]
    public void Career_identity_classifier_refuses_a_sample_workspace_swap()
    {
        string? error = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.BuildCareerWorkspaceIdentityError(
            new CharacterWorkspaceId("bundled-sample"),
            CareerProfile("T781 Dwarf Qualifier", "T78D", created: true),
            "created-workspace",
            "T781 Dwarf Qualifier",
            "T78D");
        Assert.IsNotNull(error);
        Assert.IsTrue(error.Contains("not the created workspace", StringComparison.Ordinal), error);
    }

    [TestMethod]
    public void Career_identity_classifier_refuses_alias_name_and_created_mismatches()
    {
        string? aliasError = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.BuildCareerWorkspaceIdentityError(
            new CharacterWorkspaceId("created-workspace"),
            CareerProfile("T781 Dwarf Qualifier", "WRONG-ALIAS", created: true),
            "created-workspace",
            "T781 Dwarf Qualifier",
            "T78D");
        Assert.IsTrue(aliasError!.Contains("alias", StringComparison.Ordinal), aliasError);

        string? nameError = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.BuildCareerWorkspaceIdentityError(
            new CharacterWorkspaceId("created-workspace"),
            CareerProfile("Soma Career Sample", "T78D", created: true),
            "created-workspace",
            "T781 Dwarf Qualifier",
            "T78D");
        Assert.IsTrue(nameError!.Contains("name", StringComparison.Ordinal), nameError);

        string? createdError = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.BuildCareerWorkspaceIdentityError(
            new CharacterWorkspaceId("created-workspace"),
            CareerProfile("T781 Dwarf Qualifier", "T78D", created: false),
            "created-workspace",
            "T781 Dwarf Qualifier",
            "T78D");
        Assert.IsTrue(createdError!.Contains("not marked created", StringComparison.Ordinal), createdError);
    }

    [TestMethod]
    public void Career_identity_classifier_refuses_missing_workspace_or_profile()
    {
        string? noWorkspace = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.BuildCareerWorkspaceIdentityError(
            null,
            CareerProfile("T781 Dwarf Qualifier", "T78D", created: true),
            "created-workspace",
            "T781 Dwarf Qualifier",
            "T78D");
        Assert.IsTrue(noWorkspace!.Contains("not the created workspace", StringComparison.Ordinal), noWorkspace);

        string? noProfile = Chummer.Avalonia.DesktopMouseFirstJourneyRunner.BuildCareerWorkspaceIdentityError(
            new CharacterWorkspaceId("created-workspace"),
            null,
            "created-workspace",
            "T781 Dwarf Qualifier",
            "T78D");
        Assert.IsTrue(noProfile!.Contains("no profile section", StringComparison.Ordinal), noProfile);
    }

    private static CharacterProfileSection CareerProfile(string name, string alias, bool created) =>
        new(
            name,
            alias,
            "journey-player",
            "Dwarf",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "6.0",
            "6.0",
            "Priority",
            string.Empty,
            created,
            false,
            false,
            false,
            false,
            0,
            0);
}
