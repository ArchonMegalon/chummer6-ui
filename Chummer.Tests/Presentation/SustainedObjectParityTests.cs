using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class SustainedObjectParityTests
{
    private static readonly Guid SpellId = Guid.Parse("82111111-8211-8211-8211-821111111111");
    private static readonly Guid CritterPowerId = Guid.Parse("82222222-8222-8222-8222-822222222222");
    private static readonly CharacterWorkspaceId WorkspaceId = new("sustained-object-tests");

    [TestMethod]
    public void Projector_uses_linked_type_guid_and_occurrence_for_duplicate_casts()
    {
        SustainedObjectsEditorState editor = SustainedObjectsEditorProjector.Project(
            DuplicateSpellXml,
            WorkspaceId,
            7);

        Assert.HasCount(2, editor.Objects);
        Assert.AreEqual(new CharacterSustainedObjectIdentity("Spell", SpellId, 0), editor.Objects[0].Identity);
        Assert.AreEqual(new CharacterSustainedObjectIdentity("Spell", SpellId, 1), editor.Objects[1].Identity);
        Assert.AreEqual(6, editor.Objects[1].Force);
        Assert.IsFalse(editor.Objects[1].SelfSustained);
    }

    [TestMethod]
    public void Update_targets_exact_duplicate_and_preserves_unrelated_xml()
    {
        CharacterSustainedObjectState selected = SustainedObjectsEditorProjector.Project(
            DuplicateSpellXml,
            WorkspaceId,
            7).Objects[1];

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplySustainedObjectEdit(
            DuplicateSpellXml,
            new SustainedObjectEditRequest(
                WorkspaceId,
                7,
                selected,
                CharacterSustainedObjectAction.Update,
                8,
                5,
                true,
                Confirmed: false))).Root!;

        XElement[] sustained = root.Element("sustainedobjects")!.Elements("sustainedobject").ToArray();
        Assert.AreEqual("4", sustained[0].Element("force")!.Value);
        Assert.AreEqual("8", sustained[1].Element("force")!.Value);
        Assert.AreEqual("5", sustained[1].Element("nethits")!.Value);
        Assert.AreEqual("True", sustained[1].Element("self")!.Value);
        Assert.AreEqual("preserve me", sustained[1].Element("notes")!.Value);
    }

    [TestMethod]
    public void Critter_power_hides_self_sustained_and_rejects_cross_wire()
    {
        const string xml = """
<character>
  <critterpowers><critterpower><guid>82222222-8222-8222-8222-822222222222</guid><name>Fear</name></critterpower></critterpowers>
  <sustainedobjects><sustainedobject><linkedobject>82222222-8222-8222-8222-822222222222</linkedobject><linkedobjecttype>CritterPower</linkedobjecttype><force>3</force><nethits>1</nethits><self>True</self></sustainedobject></sustainedobjects>
</character>
""";
        CharacterSustainedObjectState selected = SustainedObjectsEditorProjector.Project(
            xml,
            WorkspaceId,
            4).Objects.Single();
        Assert.IsFalse(selected.SelfSustainedEditable);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplySustainedObjectEdit(
                xml,
                new SustainedObjectEditRequest(
                    WorkspaceId,
                    4,
                    selected,
                    CharacterSustainedObjectAction.Update,
                    4,
                    2,
                    false,
                    Confirmed: false)));
    }

    [TestMethod]
    public void Delete_requires_confirmation_and_removes_only_the_selected_occurrence()
    {
        CharacterSustainedObjectState selected = SustainedObjectsEditorProjector.Project(
            DuplicateSpellXml,
            WorkspaceId,
            7).Objects[0];
        SustainedObjectEditRequest unconfirmed = new(
            WorkspaceId,
            7,
            selected,
            CharacterSustainedObjectAction.Delete,
            selected.Force,
            selected.NetHits,
            selected.SelfSustained,
            Confirmed: false);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplySustainedObjectEdit(DuplicateSpellXml, unconfirmed));

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplySustainedObjectEdit(
            DuplicateSpellXml,
            unconfirmed with { Confirmed = true })).Root!;
        XElement remaining = root.Element("sustainedobjects")!.Elements("sustainedobject").Single();
        Assert.AreEqual("6", remaining.Element("force")!.Value);
        Assert.AreEqual("preserve me", remaining.Element("notes")!.Value);
    }

    private const string DuplicateSpellXml = """
<character>
  <spells><spell><guid>82111111-8211-8211-8211-821111111111</guid><name>Increase Reflexes</name></spell></spells>
  <sustainedobjects>
    <sustainedobject><linkedobject>82111111-8211-8211-8211-821111111111</linkedobject><linkedobjecttype>Spell</linkedobjecttype><force>4</force><nethits>2</nethits><self>True</self></sustainedobject>
    <sustainedobject><linkedobject>82111111-8211-8211-8211-821111111111</linkedobject><linkedobjecttype>Spell</linkedobjecttype><force>6</force><nethits>3</nethits><self>False</self><notes>preserve me</notes></sustainedobject>
  </sustainedobjects>
</character>
""";
}
