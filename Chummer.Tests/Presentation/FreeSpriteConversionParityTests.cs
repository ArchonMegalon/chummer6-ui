using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class FreeSpriteConversionParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("free-sprite-tests");
    private static readonly Guid NewId =
        Guid.Parse("82222222-8222-8222-8222-822222222222");

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Creation_and_career_append_exact_denial_and_change_only_category(bool created)
    {
        string source = SpriteXml(created);
        FreeSpriteConversionEditorState editor = FreeSpriteConversionEditorProjector.Project(
            source, WorkspaceId, 19);
        Assert.IsTrue(CharacterFreeSpriteConversionRules.TryCreateIdentity(
            editor.Conversion,
            NewId,
            out CharacterFreeSpriteConversionIdentity identity));

        string mutated = WorkspaceXmlMutationCatalog.ApplyFreeSpriteConversion(
            source,
            new FreeSpriteConversionRequest(
                WorkspaceId,
                19,
                identity,
                editor.Conversion.Revision));
        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement root = document.Root!;
        XElement denial = root.Element("critterpowers")!.Elements("critterpower").Last();
        Assert.AreEqual("Free Sprite", root.Element("metatypecategory")!.Value);
        Assert.AreEqual(CharacterFreeSpriteConversionRules.DenialSourceId.ToString("D"), denial.Element("sourceid")!.Value);
        Assert.AreEqual(NewId.ToString("D"), denial.Element("guid")!.Value);
        Assert.AreEqual("Denial", denial.Element("name")!.Value);
        Assert.AreEqual("False", denial.Element("counttowardslimit")!.Value);
        Assert.AreEqual("Chocolate", denial.Element("notesColor")!.Value);
        Assert.AreEqual("23", root.Element("karma")!.Value);
        Assert.AreEqual("4567.89", root.Element("nuyen")!.Value);
        Assert.AreEqual("Sprite conversion sentinel", root.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Non_sprite_free_sprite_duplicate_container_and_stale_revision_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => FreeSpriteConversionEditorProjector.Project(
            SpriteXml(false).Replace("Registered Sprites", "Metahuman", StringComparison.Ordinal),
            WorkspaceId,
            19));
        Assert.ThrowsExactly<InvalidOperationException>(() => FreeSpriteConversionEditorProjector.Project(
            SpriteXml(true).Replace("Registered Sprites", "Free Sprite", StringComparison.Ordinal),
            WorkspaceId,
            19));
        Assert.ThrowsExactly<InvalidOperationException>(() => FreeSpriteConversionEditorProjector.Project(
            SpriteXml(false).Replace("<critterpowers>", "<critterpowers/><critterpowers>", StringComparison.Ordinal),
            WorkspaceId,
            19));

        FreeSpriteConversionEditorState editor = FreeSpriteConversionEditorProjector.Project(
            SpriteXml(false), WorkspaceId, 19);
        Assert.IsTrue(CharacterFreeSpriteConversionRules.TryCreateIdentity(
            editor.Conversion, NewId, out CharacterFreeSpriteConversionIdentity identity));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyFreeSpriteConversion(
                SpriteXml(false),
                new FreeSpriteConversionRequest(
                    WorkspaceId,
                    19,
                    identity,
                    new string('0', 64))));
    }

    private static string SpriteXml(bool created) => $$"""
<character>
  <created>{{created}}</created>
  <metatypecategory>Registered Sprites</metatypecategory>
  <karma>23</karma><nuyen>4567.89</nuyen>
  <critterpowers><critterpower><guid>81111111-8111-8111-8111-811111111111</guid><name>Diagnostics</name><counttowardslimit>True</counttowardslimit></critterpower></critterpowers>
  <customstate>Sprite conversion sentinel</customstate>
</character>
""";
}
