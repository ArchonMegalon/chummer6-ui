using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class TraditionSpiritCategoryParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("tradition-spirit-category-tests");
    private static readonly Guid TraditionId = Guid.Parse("91111111-9111-9111-9111-911111111111");
    private static readonly ICharacterSourceDataResolver Resolver = new FixedResolver(
        ["Spirit of Fire", "Spirit of Air", "Spirit of Water"]);

    [TestMethod]
    public void ProjectAndApply_use_five_local_revisions_and_canonicalize_blank()
    {
        string xml = CustomXml();
        TraditionSpiritCategoryEditorState editor = TraditionSpiritCategoryEditorProjector.Project(
            xml,
            WorkspaceId,
            7,
            Resolver);

        CollectionAssert.AreEqual(
            new[] { string.Empty, "Spirit of Fire", "Spirit of Air" },
            editor.Semantics.AllowedSpiritNames.ToArray());
        Assert.AreEqual(5, editor.Semantics.Fields.Count);

        string mutated = WorkspaceXmlMutationCatalog.ApplyTraditionSpiritCategoryEdit(
            xml,
            Request(editor, new Dictionary<CharacterTraditionSpiritCategory, string>
            {
                [CharacterTraditionSpiritCategory.Combat] = "Spirit of Air",
                [CharacterTraditionSpiritCategory.Detection] = string.Empty
            }),
            Resolver);
        XElement root = XDocument.Parse(mutated).Root!;
        XElement tradition = root.Element("tradition")!;
        Assert.AreEqual("Spirit of Air", tradition.Element("spiritcombat")!.Value);
        Assert.AreEqual(string.Empty, tradition.Element("spiritdetection")!.Value);
        Assert.AreEqual("Spirit of Air", tradition.Element("spirithealth")!.Value);
        Assert.AreEqual(string.Empty, tradition.Element("spiritillusion")!.Value);
        Assert.AreEqual("Spirit of Fire", tradition.Element("spiritmanipulation")!.Value);
        Assert.AreEqual("Tradition sentinel", tradition.Element("extra")!.Value);
        Assert.AreEqual("Runner sentinel", root.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Apply_rejects_one_stale_field_noop_and_custom_overlay_drift()
    {
        string xml = CustomXml();
        TraditionSpiritCategoryEditorState editor = TraditionSpiritCategoryEditorProjector.Project(
            xml,
            WorkspaceId,
            7,
            Resolver);
        TraditionSpiritCategoryEditRequest changed = Request(
            editor,
            new Dictionary<CharacterTraditionSpiritCategory, string>
            {
                [CharacterTraditionSpiritCategory.Combat] = "Spirit of Air"
            });

        TraditionSpiritCategoryFieldEdit combat = changed.Fields.Single(
            field => field.Category == CharacterTraditionSpiritCategory.Combat);
        TraditionSpiritCategoryEditRequest stale = changed with
        {
            Fields = changed.Fields.Select(field => field.Category == CharacterTraditionSpiritCategory.Combat
                ? combat with { ExpectedFieldRevision = new string('0', 64) }
                : field).ToArray()
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyTraditionSpiritCategoryEdit(xml, stale, Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyTraditionSpiritCategoryEdit(
                xml,
                Request(editor, new Dictionary<CharacterTraditionSpiritCategory, string>()),
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyTraditionSpiritCategoryEdit(
                xml,
                changed,
                new FixedResolver(
                    ["Spirit of Fire", "Spirit of Air", "Spirit of Water", "Guardian Spirit"])));
    }

    [TestMethod]
    public void Project_rejects_noncustom_resonance_missing_source_and_excluded_current()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionSpiritCategoryEditorProjector.Project(
            CustomXml().Replace(
                CharacterTraditionNameRules.CustomMagicalTraditionSourceId.ToString("D"),
                "92222222-9222-9222-9222-922222222222",
                StringComparison.Ordinal),
            WorkspaceId,
            7,
            Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionSpiritCategoryEditorProjector.Project(
            CustomXml().Replace("<magenabled>True</magenabled><resenabled>False</resenabled>",
                "<magenabled>False</magenabled><resenabled>True</resenabled>",
                StringComparison.Ordinal),
            WorkspaceId,
            7,
            Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionSpiritCategoryEditorProjector.Project(
            CustomXml(), WorkspaceId, 7, sourceDataResolver: null));
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionSpiritCategoryEditorProjector.Project(
            CustomXml().Replace("<spirithealth>Spirit of Air</spirithealth>",
                "<spirithealth>Spirit of Water</spirithealth>",
                StringComparison.Ordinal),
            WorkspaceId,
            7,
            Resolver));
    }

    private static TraditionSpiritCategoryEditRequest Request(
        TraditionSpiritCategoryEditorState editor,
        IReadOnlyDictionary<CharacterTraditionSpiritCategory, string> changes)
        => new(
            editor.WorkspaceId,
            editor.ContentRevision,
            editor.Semantics.TraditionId,
            editor.Semantics.SourceId,
            editor.Semantics.Fields.Select(field => new TraditionSpiritCategoryFieldEdit(
                field.Category,
                field.Revision,
                changes.TryGetValue(field.Category, out string? value) ? value : field.SpiritName)).ToArray());

    private static string CustomXml() => $$"""
<character>
  <magenabled>True</magenabled><resenabled>False</resenabled>
  <settings>223a11ff-80e0-428b-89a9-6ef1c243b8b6</settings>
  <improvements>
    <improvement><improvementttype>LimitSpiritCategory</improvementttype><improvedname>Spirit of Fire</improvedname><enabled>1</enabled></improvement>
    <improvement><improvementttype>LimitSpiritCategory</improvementttype><improvedname>Spirit of Air</improvedname><enabled>True</enabled></improvement>
    <improvement><improvementttype>LimitSpiritCategory</improvementttype><improvedname>Spirit of Water</improvedname><enabled>False</enabled></improvement>
  </improvements>
  <tradition>
    <guid>{{TraditionId:D}}</guid>
    <sourceid>{{CharacterTraditionNameRules.CustomMagicalTraditionSourceId:D}}</sourceid>
    <traditiontype>MAG</traditiontype>
    <name>Custom Vienna Magic</name>
    <spiritcombat>Spirit of Fire</spiritcombat>
    <spirithealth>Spirit of Air</spirithealth>
    <spiritillusion></spiritillusion>
    <spiritmanipulation>Spirit of Fire</spiritmanipulation>
    <extra>Tradition sentinel</extra>
  </tradition>
  <customstate>Runner sentinel</customstate>
</character>
""";

    private sealed class FixedResolver(IReadOnlyList<string> names) : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => new FixedContext(names);
    }

    private sealed class FixedContext(IReadOnlyList<string> names) : ICharacterSourceDataContext
    {
        public bool TryResolveSpiritCatalogNames(string entityType, out IReadOnlyList<string> values)
        {
            values = names;
            return string.Equals(entityType, "Spirit", StringComparison.Ordinal);
        }

        public bool TryResolveCyberwareGradeDeviceRating(
            string gradeName,
            string improvementSource,
            out int deviceRating)
        {
            deviceRating = 0;
            return false;
        }

        public bool TryResolveVehicleModBonuses(
            string sourceId,
            string name,
            out CharacterVehicleModSourceBonuses bonuses)
        {
            bonuses = CharacterVehicleModSourceBonuses.Empty;
            return false;
        }
    }
}
