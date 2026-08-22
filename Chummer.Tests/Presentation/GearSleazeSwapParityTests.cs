using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GearSleazeSwapParityTests
{
    [TestMethod]
    public void DataProcessingSwapPreservesNotificationConsumersAndRawProvenance()
    {
        Guid id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        string xml = $"""<character><created>True</created><nuyen>50</nuyen><karma>2</karma><gears><gear><guid>{id:D}</guid><name>Deck</name><category>Cyberdecks</category><attack>7</attack><sleaze>{{Rating}}</sleaze><dataprocessing>5</dataprocessing><firewall>4</firewall><attributearray>7,6,5,4</attributearray><canswapattributes>True</canswapattributes><modsleaze>3</modsleaze><moddataprocessing>9</moddataprocessing><active>True</active><homenode>True</homenode><cost>99</cost></gear></gears></character>""";
        CharacterGearMatrixSwapState state = GearSleazeSwapEditorProjector.ProjectValue(xml, id).Single();
        string changed = WorkspaceXmlMutationCatalog.ApplyGearSleazeSwapEdit(xml, new(
            new CharacterWorkspaceId("runner"), 3, state.Identity, state.Revision,
            CharacterGearMatrixAttribute.Sleaze, CharacterGearMatrixAttribute.DataProcessing));
        XElement before = XDocument.Parse(xml).Root!.Element("gears")!.Element("gear")!;
        XElement after = XDocument.Parse(changed).Root!.Element("gears")!.Element("gear")!;
        Assert.AreEqual("5", after.Element("sleaze")!.Value);
        Assert.AreEqual("{Rating}", after.Element("dataprocessing")!.Value);
        foreach (string name in new[] { "attack", "firewall", "attributearray", "canswapattributes", "modsleaze", "moddataprocessing", "active", "homenode", "cost" })
            Assert.AreEqual(before.Element(name)!.Value, after.Element(name)!.Value, name);
    }
}
