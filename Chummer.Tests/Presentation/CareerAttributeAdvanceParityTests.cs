using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerAttributeAdvanceParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-attribute-tests");
    private static readonly Guid ExpenseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CorrectionId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [TestMethod]
    public void Projection_is_ordered_typed_and_uses_exact_chummer5_cost_authority()
    {
        string improvements = Improvement("AttributeKarmaCost", string.Empty, value: -1m)
            + Improvement("AttributeKarmaCostMultiplier", "BOD", value: 50m);
        CareerAttributeAdvanceEditorState editor = Project(
            BuildXml(improvements: improvements),
            SettingsCatalog());

        Assert.AreEqual(WorkspaceId, editor.WorkspaceId);
        Assert.AreEqual(7, editor.ContentRevision);
        Assert.AreEqual(0, editor.OmittedAttributeCount);
        CollectionAssert.AreEqual(
            new[] { "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL", "EDG", "MAG", "MAGAdept", "RES" },
            editor.Attributes.Select(static value => value.Identity.Abbreviation).ToArray());
        CharacterCareerAttributeAdvanceQuote body = editor.Attributes[0];
        Assert.AreEqual(CharacterCareerAttributeKind.Normal, body.Identity.Kind);
        Assert.AreEqual(4, body.EffectiveValue);
        Assert.AreEqual(5, body.TargetValue);
        Assert.AreEqual(6, body.NaturalMaximum);
        Assert.AreEqual(12, body.KarmaCost, "ceil((5*5)*50%-1) must match Chummer5 StandardRound.");
        Assert.AreEqual(TimeSpan.Zero, body.ApplicationDuration);
        Assert.AreEqual(
            CharacterCareerAttributeTimeAuthority.ImmediateChummerPersistence,
            body.TimeAuthority);
        Assert.IsTrue(body.CanAdvance);
        Assert.AreEqual(64, body.LogicalRevision.Length);
        Assert.AreEqual(64, body.SourceRevision.Length);
        Assert.AreEqual(64, body.RuleDigest.Length);
    }

    [TestMethod]
    public void Projection_exposes_special_gates_and_natural_max_without_inventing_targets()
    {
        string xml = BuildXml(
            magicEnabled: false,
            resonanceEnabled: false,
            adept: false,
            magician: false,
            bodyBase: 5,
            bodyKarma: 0,
            bodyTotal: 6);
        CareerAttributeAdvanceEditorState editor = Project(xml, SettingsCatalog());

        Assert.AreEqual(
            CharacterCareerAttributeAdvanceBlocker.AtNaturalMaximum,
            Find(editor, "BOD").Blocker);
        Assert.AreEqual(
            CharacterCareerAttributeAdvanceBlocker.SpecialAttributeDisabled,
            Find(editor, "MAG").Blocker);
        Assert.AreEqual(
            CharacterCareerAttributeAdvanceBlocker.SpecialAttributeDisabled,
            Find(editor, "MAGAdept").Blocker);
        Assert.AreEqual(
            CharacterCareerAttributeAdvanceBlocker.SpecialAttributeDisabled,
            Find(editor, "RES").Blocker);
        Assert.IsFalse(editor.Attributes.Any(static value => value.Identity.Abbreviation == "DEP"));
    }

    [TestMethod]
    public void Advance_persists_exact_delta_expense_receipt_and_reopens()
    {
        string settings = SettingsCatalog();
        string xml = BuildXml();
        CareerAttributeAdvanceEditorState editor = Project(xml, settings);
        CharacterCareerAttributeAdvanceQuote selected = Find(editor, "BOD");
        CareerAttributeAdvanceRequest request = Request(selected);

        CareerAttributeAdvanceMutationResult result =
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(xml, request, settings);

        Assert.IsTrue(CharacterCareerAttributeAdvanceRules.IsCoherent(result.Receipt));
        Assert.AreEqual(ExpenseId, result.Receipt.TransactionId);
        XElement root = XDocument.Parse(result.Xml).Root!;
        XElement body = Attribute(root, "BOD");
        Assert.AreEqual("2", body.Element("karma")!.Value);
        Assert.AreEqual("5", body.Element("totalvalue")!.Value);
        Assert.AreEqual("75", root.Element("karma")!.Value);
        XElement expense = root.Element("expenses")!.Elements("expense").Single();
        Assert.AreEqual(ExpenseId.ToString("D"), expense.Element("guid")!.Value);
        Assert.AreEqual("-25", expense.Element("amount")!.Value);
        Assert.AreEqual("Attribute BOD 4 -> 5", expense.Element("reason")!.Value);
        Assert.AreEqual("ImproveAttribute", expense.Element("undo")!.Element("karmatype")!.Value);
        Assert.AreEqual("BOD", expense.Element("undo")!.Element("objectid")!.Value);
        XElement receipt = root.Element("careerattributeadvancementreceipts")!
            .Elements("receipt")
            .Single();
        Assert.AreEqual(result.Receipt.ReceiptDigest, receipt.Attribute("receiptDigest")!.Value);

        CareerAttributeAdvanceEditorState reopened = Project(result.Xml, settings);
        Assert.AreEqual(5, Find(reopened, "BOD").EffectiveValue);
        Assert.AreEqual(2, Find(reopened, "BOD").KarmaPoints);
        Assert.AreEqual(75, Find(reopened, "BOD").AvailableKarma);
        Assert.HasCount(1, reopened.RecoverableReceipts);
        Assert.AreEqual(result.Receipt, reopened.RecoverableReceipts[0]);
        Assert.AreEqual(0, reopened.OmittedReceiptCount);
    }

    [TestMethod]
    public void Edge_repair_reduces_burn_authority_without_incrementing_saved_karma()
    {
        string settings = SettingsCatalog();
        string burned = Improvement(
            "Attribute",
            "EDG",
            minimum: -1,
            source: "BurnedEdge");
        string xml = BuildXml(improvements: burned, edgeTotal: 2);
        CharacterCareerAttributeAdvanceQuote selected = Find(Project(xml, settings), "EDG");
        Assert.IsTrue(selected.RepairsBurnedEdge);
        Assert.AreEqual(1, selected.BurnedEdgePoints);

        CareerAttributeAdvanceMutationResult result =
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(
                xml,
                Request(selected),
                settings);

        XElement root = XDocument.Parse(result.Xml).Root!;
        Assert.AreEqual("0", Attribute(root, "EDG").Element("karma")!.Value);
        Assert.IsFalse(root.Element("improvements")!.Elements("improvement").Any());
        Assert.AreEqual(0, result.Receipt.BurnedEdgePointsAfter);
        Assert.AreEqual(0, result.Receipt.AttributeKarmaAfter);
        Assert.AreEqual(3, Find(Project(result.Xml, settings), "EDG").EffectiveValue);
    }

    [TestMethod]
    public void Normal_attribute_advance_preserves_unrelated_burned_edge_authority()
    {
        string settings = SettingsCatalog();
        string burned = Improvement(
            "Attribute",
            "EDG",
            minimum: -1,
            source: "BurnedEdge");
        string xml = BuildXml(improvements: burned, edgeTotal: 2);
        CharacterCareerAttributeAdvanceQuote selected = Find(Project(xml, settings), "BOD");

        CareerAttributeAdvanceMutationResult result =
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(
                xml,
                Request(selected),
                settings);

        XElement persistedBurn = XDocument.Parse(result.Xml).Root!
            .Element("improvements")!
            .Elements("improvement")
            .Single();
        Assert.AreEqual("BurnedEdge", persistedBurn.Element("improvementsource")!.Value);
        Assert.AreEqual("-1", persistedBurn.Element("min")!.Value);
        Assert.AreEqual(1, Find(Project(result.Xml, settings), "EDG").BurnedEdgePoints);
    }

    [TestMethod]
    public void Ambiguous_burn_repair_and_custom_relevant_improvement_are_omitted()
    {
        string settings = SettingsCatalog();
        string twoBurns = Improvement(
            "Attribute",
            "EDG",
            minimum: -2,
            source: "BurnedEdge");
        CareerAttributeAdvanceEditorState ambiguous = Project(
            BuildXml(improvements: twoBurns, edgeTotal: 2),
            settings);
        Assert.IsFalse(ambiguous.Attributes.Any(static value => value.Identity.Abbreviation == "EDG"));
        Assert.AreEqual(1, ambiguous.OmittedAttributeCount);

        string custom = Improvement("Attribute", "BOD", augmented: 1m, custom: true);
        CareerAttributeAdvanceEditorState unresolved = Project(
            BuildXml(improvements: custom),
            settings);
        Assert.IsFalse(unresolved.Attributes.Any(static value => value.Identity.Abbreviation == "BOD"));
        Assert.AreEqual(1, unresolved.OmittedAttributeCount);

        XElement ordinaryCyberware = XElement.Parse(BuildXml());
        ordinaryCyberware.Add(Cyberwares(limbSlot: string.Empty));
        CareerAttributeAdvanceEditorState ordinary = Project(
            ordinaryCyberware.ToString(SaveOptions.DisableFormatting),
            settings);
        Assert.IsTrue(ordinary.Attributes.Any(static value =>
            value.Identity.Abbreviation == "AGI"));
        Assert.IsTrue(ordinary.Attributes.Any(static value =>
            value.Identity.Abbreviation == "STR"));

        XElement cyberlimb = XElement.Parse(BuildXml());
        cyberlimb.Add(Cyberwares(limbSlot: "arm"));
        CareerAttributeAdvanceEditorState limb = Project(
            cyberlimb.ToString(SaveOptions.DisableFormatting),
            settings);
        Assert.IsFalse(limb.Attributes.Any(static value =>
            value.Identity.Abbreviation == "AGI"));
        Assert.IsFalse(limb.Attributes.Any(static value =>
            value.Identity.Abbreviation == "STR"));

        XElement encumbered = XElement.Parse(BuildXml(improvements: Improvement(
            "Attribute",
            "AGI",
            augmented: -3m,
            source: "ArmorEncumbrance")));
        Attribute(encumbered, "AGI").Element("totalvalue")!.Value = "0";
        Assert.IsTrue(Project(
                encumbered.ToString(SaveOptions.DisableFormatting),
                settings)
            .Attributes.Any(static value => value.Identity.Abbreviation == "AGI"));
    }

    [TestMethod]
    public void Confirmation_stale_source_stale_rule_foreign_digest_and_duplicate_expense_fail_closed()
    {
        string settings = SettingsCatalog();
        string xml = BuildXml();
        CharacterCareerAttributeAdvanceQuote selected = Find(Project(xml, settings), "BOD");
        CareerAttributeAdvanceRequest request = Request(selected);
        string original = xml;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(
                xml,
                request with { Confirmed = false },
                settings));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(
                xml,
                request with { ExpectedSourceRevision = new string('0', 64) },
                settings));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(
                xml.Replace("<name>BOD</name>", "<name>BOD</name><notes>changed</notes>", StringComparison.Ordinal),
                request,
                settings));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(
                xml,
                request,
                SettingsCatalog(karmaAttribute: 6)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(
                xml.Replace("<expenses />", ExistingExpense(ExpenseId), StringComparison.Ordinal),
                request,
                settings));
        Assert.AreSame(original, xml, "The pure mutation input must remain untouched on every failure.");
    }

    [TestMethod]
    public void Correction_is_exact_compensation_with_recovery_audit_and_replay_rejection()
    {
        string settings = SettingsCatalog();
        string xml = BuildXml();
        CharacterCareerAttributeAdvanceQuote selected = Find(Project(xml, settings), "BOD");
        CareerAttributeAdvanceMutationResult advanced =
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(
                xml,
                Request(selected),
                settings);
        CareerAttributeCorrectionRequest correctionRequest = new(
            WorkspaceId,
            ExpectedContentRevision: 8,
            advanced.Receipt,
            advanced.Receipt.ReceiptDigest,
            Confirmed: true,
            CorrectionId,
            "Operator correction");

        CareerAttributeCorrectionMutationResult corrected =
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeCorrection(
                advanced.Xml,
                correctionRequest,
                settings);

        Assert.IsTrue(CharacterCareerAttributeAdvanceRules.IsCoherent(corrected.Correction));
        XElement root = XDocument.Parse(corrected.Xml).Root!;
        Assert.AreEqual("1", Attribute(root, "BOD").Element("karma")!.Value);
        Assert.AreEqual("4", Attribute(root, "BOD").Element("totalvalue")!.Value);
        Assert.AreEqual("100", root.Element("karma")!.Value);
        Assert.IsFalse(root.Element("expenses")!.Elements("expense").Any());
        XElement audit = root.Element("careerattributeadvancementcorrections")!
            .Elements("correction")
            .Single();
        Assert.AreEqual(CorrectionId.ToString("D"), audit.Attribute("id")!.Value);
        Assert.AreEqual(
            advanced.Receipt.ReceiptDigest,
            audit.Attribute("receiptDigest")!.Value);
        CareerAttributeAdvanceEditorState reopened = Project(corrected.Xml, settings);
        Assert.AreEqual(4, Find(reopened, "BOD").EffectiveValue);
        Assert.IsEmpty(reopened.RecoverableReceipts);
        Assert.AreEqual(0, reopened.OmittedReceiptCount);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeCorrection(
                corrected.Xml,
                correctionRequest with { ExpectedContentRevision = 9 },
                settings));
        XDocument foreignCorrectionDocument = XDocument.Parse(corrected.Xml);
        foreignCorrectionDocument.Root!
            .Element("careerattributeadvancementcorrections")!
            .Element("correction")!
            .SetAttributeValue("target", "AGI");
        string foreignCorrectionLedger = foreignCorrectionDocument.ToString(
            SaveOptions.DisableFormatting);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Project(foreignCorrectionLedger, settings));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeCorrection(
                advanced.Xml,
                correctionRequest with
                {
                    OriginalReceipt = advanced.Receipt with
                    {
                        Identity = new CharacterCareerAttributeIdentity(
                            "AGI",
                            CharacterCareerAttributeKind.Normal)
                    }
                },
                settings));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeCorrection(
                advanced.Xml.Replace(
                    "<name>BOD</name>",
                    "<name>BOD</name><notes>post-state changed</notes>",
                    StringComparison.Ordinal),
                correctionRequest,
                settings));
    }

    [TestMethod]
    public void Correction_requires_confirmation_and_exact_persisted_receipt()
    {
        string settings = SettingsCatalog();
        string xml = BuildXml();
        CharacterCareerAttributeAdvanceQuote selected = Find(Project(xml, settings), "BOD");
        CareerAttributeAdvanceMutationResult advanced =
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeAdvance(xml, Request(selected), settings);
        CareerAttributeCorrectionRequest request = new(
            WorkspaceId,
            8,
            advanced.Receipt,
            advanced.Receipt.ReceiptDigest,
            Confirmed: false,
            CorrectionId,
            "Correction");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeCorrection(
                advanced.Xml,
                request,
                settings));
        string withoutReceipt = XDocument.Parse(advanced.Xml).ToString(SaveOptions.DisableFormatting)
            .Replace(
                XDocument.Parse(advanced.Xml).Root!.Element("careerattributeadvancementreceipts")!
                    .ToString(SaveOptions.DisableFormatting),
                string.Empty,
                StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerAttributeCorrection(
                withoutReceipt,
                request with { Confirmed = true },
                settings));
    }

    private static CareerAttributeAdvanceRequest Request(
        CharacterCareerAttributeAdvanceQuote selected)
        => new(
            WorkspaceId,
            ExpectedContentRevision: 7,
            selected,
            selected.LogicalRevision,
            selected.SourceRevision,
            selected.RuleDigest,
            Confirmed: true,
            ExpenseId,
            new DateTime(2081, 5, 12, 14, 30, 0));

    private static CareerAttributeAdvanceEditorState Project(string xml, string settings)
        => CareerAttributeAdvanceEditorProjector.Project(
            xml,
            WorkspaceId,
            7,
            settings);

    private static CharacterCareerAttributeAdvanceQuote Find(
        CareerAttributeAdvanceEditorState editor,
        string abbreviation)
        => editor.Attributes.Single(candidate => string.Equals(
            candidate.Identity.Abbreviation,
            abbreviation,
            StringComparison.Ordinal));

    private static XElement Attribute(XElement root, string abbreviation)
        => root.Element("attributes")!.Elements("attribute").Single(candidate =>
            candidate.Element("name")!.Value == abbreviation);

    private static string SettingsCatalog(int karmaAttribute = 5)
    {
        Chummer5CharacterSettingsCatalog catalog =
            Chummer5CharacterSettingsProfiles.ParseCatalog(null);
        Chummer5CharacterSettingsProfile profile =
            Chummer5CharacterSettingsProfiles.ActiveProfile(catalog);
        XElement settings = XElement.Parse(profile.Xml);
        Set(settings.Element("karmacost")!, "karmaattribute", karmaAttribute.ToString(CultureInfo.InvariantCulture));
        Set(settings, "alternatemetatypeattributekarma", "False");
        Set(settings, "unclampattributeminimum", "False");
        Set(settings, "mysadeptsecondmagattribute", "True");
        Set(settings, "dontusecyberlimbcalculation", "False");
        Set(settings, "excludelimbslot", string.Empty);
        Chummer5CharacterSettingsProfile updated = profile with
        {
            Xml = settings.ToString(SaveOptions.DisableFormatting)
        };
        return Chummer5CharacterSettingsProfiles.SerializeCatalog(
            new Chummer5CharacterSettingsCatalog(profile.Id, [updated]));
    }

    private static void Set(XElement parent, string name, string value)
    {
        XElement? element = parent.Element(name);
        if (element is null)
        {
            parent.Add(new XElement(name, value));
        }
        else
        {
            element.Value = value;
        }
    }

    private static string BuildXml(
        string improvements = "",
        bool magicEnabled = true,
        bool resonanceEnabled = true,
        bool adept = true,
        bool magician = true,
        int bodyBase = 2,
        int bodyKarma = 1,
        int bodyTotal = 4,
        int edgeTotal = 3)
    {
        XElement[] rows =
        [
            Row("BOD", 1, 6, 9, bodyBase, bodyKarma, bodyTotal),
            Row("AGI", 1, 6, 9, 1, 0, 2),
            Row("REA", 1, 6, 9, 1, 0, 2),
            Row("STR", 1, 6, 9, 1, 0, 2),
            Row("CHA", 1, 6, 9, 1, 0, 2),
            Row("INT", 1, 6, 9, 1, 0, 2),
            Row("LOG", 1, 6, 9, 1, 0, 2),
            Row("WIL", 1, 6, 9, 1, 0, 2),
            Row("EDG", 1, 6, 9, 2, 0, edgeTotal),
            Row("MAG", 0, 6, 9, 2, 0, 2),
            Row("MAGAdept", 0, 6, 9, 2, 0, 2),
            Row("RES", 0, 6, 9, 2, 0, 2)
        ];
        XElement root = new(
            "character",
            new XElement("created", "True"),
            new XElement("settings", "223a11ff-80e0-428b-89a9-6ef1c243b8b6"),
            new XElement("karma", "100"),
            new XElement("magenabled", magicEnabled),
            new XElement("resenabled", resonanceEnabled),
            new XElement("adept", adept),
            new XElement("magician", magician),
            new XElement("critter", "False"),
            new XElement("metatypecategory", "Metahuman"),
            new XElement("attributes", rows),
            new XElement("improvements", XElement.Parse($"<rows>{improvements}</rows>").Elements()),
            new XElement("expenses"),
            new XElement("customstate", "preserve me"));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement Row(
        string name,
        int minimum,
        int maximum,
        int augmentedMaximum,
        int basePoints,
        int karmaPoints,
        int totalValue)
        => new(
            "attribute",
            new XElement("name", name),
            new XElement("metatypemin", minimum),
            new XElement("metatypemax", maximum),
            new XElement("metatypeaugmax", augmentedMaximum),
            new XElement("base", basePoints),
            new XElement("karma", karmaPoints),
            new XElement("metatypecategory", "Metahuman"),
            new XElement("totalvalue", totalValue));

    private static string Improvement(
        string type,
        string target,
        int minimum = 0,
        int maximum = 0,
        decimal augmented = 0m,
        int augmentedMaximum = 0,
        decimal value = 0m,
        int rating = 1,
        string source = "Quality",
        bool custom = false)
        => new XElement(
            "improvement",
            new XElement("unique", string.Empty),
            new XElement("target", string.Empty),
            new XElement("improvedname", target),
            new XElement("sourcename", source),
            new XElement("min", minimum),
            new XElement("max", maximum),
            new XElement("aug", augmented),
            new XElement("augmax", augmentedMaximum),
            new XElement("val", value),
            new XElement("rating", rating),
            new XElement("exclude", string.Empty),
            new XElement("condition", "career"),
            new XElement("improvementttype", type),
            new XElement("improvementsource", source),
            new XElement("custom", custom),
            new XElement("customname", string.Empty),
            new XElement("customid", Guid.Empty),
            new XElement("customgroup", string.Empty),
            new XElement("addtorating", "False"),
            new XElement("enabled", "True"),
            new XElement("order", "0"),
            new XElement("notes", string.Empty),
            new XElement("notesColor", string.Empty))
            .ToString(SaveOptions.DisableFormatting);

    private static string ExistingExpense(Guid id)
        => new XElement(
            "expenses",
            new XElement(
                "expense",
                new XElement("guid", id.ToString("D")),
                new XElement("date", "2080-01-01T00:00:00"),
                new XElement("amount", "-1"),
                new XElement("reason", "existing"),
                new XElement("type", "Karma"),
                new XElement("refund", "False"),
                new XElement("forcecareervisible", "False")))
            .ToString(SaveOptions.DisableFormatting);

    private static XElement Cyberwares(string limbSlot)
        => new(
            "cyberwares",
            new XElement(
                "cyberware",
                new XElement("limbslot", limbSlot),
                new XElement("limbslotcount", "1"),
                new XElement("inheritattributes", "False"),
                new XElement("hasmodularmount", string.Empty),
                new XElement("plugsintomodularmount", string.Empty),
                new XElement("children")));
}
