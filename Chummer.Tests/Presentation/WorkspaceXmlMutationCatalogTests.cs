#nullable enable annotations

using System;
using System.Linq;
using System.Xml.Linq;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WorkspaceXmlMutationCatalogTests
{
    [TestMethod]
    public void ApplyQuickAdd_supports_runtime_backed_aug_magic_matrix_and_advancement_kinds()
    {
        (WorkspaceQuickAddRequest Request, string[] RequiredMarkers)[] expectations =
        [
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Drug,
                    Name: "Jazz",
                    Quantity: 2,
                    Source: "Core Rulebook p. 411"),
                ["<drugs>", "<drug>", "<name>Jazz</name>", "<qty>2</qty>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Cyberware,
                    Name: "Wired Reflexes 2",
                    Category: "Bodyware",
                    Cost: "149000",
                    Rating: 2,
                    Grade: "Alpha",
                    Essence: "2.70",
                    Capacity: "n/a",
                    Location: "Body"),
                ["<cyberwares>", "<cyberware>", "<name>Wired Reflexes 2</name>", "<ess>2.70</ess>", "<grade>Alpha</grade>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Spell,
                    Name: "Stunbolt",
                    Category: "Combat",
                    Type: "Mana",
                    Range: "LOS",
                    Duration: "Instant",
                    DrainValue: "F-3",
                    Source: "Core Rulebook p. 288"),
                ["<spells>", "<spell>", "<name>Stunbolt</name>", "<dv>F-3</dv>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Power,
                    Name: "Improved Reflexes",
                    Rating: 1,
                    PointsPerLevel: 2.5m,
                    Source: "Core Rulebook p. 309"),
                ["<powers>", "<power>", "<name>Improved Reflexes</name>", "<pointsperlevel>2.5</pointsperlevel>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.ComplexForm,
                    Name: "Cleaner",
                    Target: "Persona",
                    Duration: "Sustained",
                    FadingValue: "Level 1",
                    Source: "Data Trails p. 178"),
                ["<complexforms>", "<complexform>", "<name>Cleaner</name>", "<fv>Level 1</fv>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.MatrixProgram,
                    Name: "Armor",
                    Slot: "Common",
                    Source: "Data Trails p. 60"),
                ["<aiprograms>", "<program>", "<name>Armor</name>", "<rating>Common</rating>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.InitiationGrade,
                    Name: "Masking",
                    Rating: 1,
                    Res: false),
                ["<initiationgrades>", "<initiationgrade>", "<grade>1</grade>", "<reward>Masking</reward>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.Spirit,
                    Name: "Watcher Spirit",
                    Force: 3,
                    Services: 2,
                    Bound: false),
                ["<spirits>", "<spirit>", "<name>Watcher Spirit</name>", "<force>3</force>", "<services>2</services>"]
            ),
            (
                new WorkspaceQuickAddRequest(
                    Kind: WorkspaceQuickAddKinds.CritterPower,
                    Name: "Natural Weapon",
                    Type: "Passive",
                    Range: "Self",
                    Duration: "Always",
                    Rating: 1),
                ["<critterpowers>", "<critterpower>", "<name>Natural Weapon</name>", "<range>Self</range>", "<duration>Always</duration>"]
            )
        ];

        foreach ((WorkspaceQuickAddRequest request, string[] requiredMarkers) in expectations)
        {
            string xml = WorkspaceXmlMutationCatalog.ApplyQuickAdd("<character />", request);

            foreach (string marker in requiredMarkers)
            {
                StringAssert.Contains(xml, marker, $"Missing '{marker}' for kind '{request.Kind}'.");
            }
        }
    }

    [TestMethod]
    public void ApplyAttributeEdit_updates_attribute_buckets_and_totalvalue()
    {
        const string xml = """
<character>
  <attributes>
    <attribute>
      <name>Body</name>
      <base>3</base>
      <karma>1</karma>
      <value>3</value>
      <totalvalue>4</totalvalue>
      <metatypemin>1</metatypemin>
      <metatypemax>6</metatypemax>
      <metatypeaugmax>9</metatypeaugmax>
    </attribute>
  </attributes>
</character>
""";

        string baseMutatedXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            xml,
            new AttributeEditRequest("Body", "base", 5));
        StringAssert.Contains(baseMutatedXml, "<base>5</base>");
        StringAssert.Contains(baseMutatedXml, "<karma>1</karma>");
        StringAssert.Contains(baseMutatedXml, "<value>5</value>");
        StringAssert.Contains(baseMutatedXml, "<totalvalue>6</totalvalue>");

        string karmaMutatedXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            baseMutatedXml,
            new AttributeEditRequest("Body", "karma", 9));
        StringAssert.Contains(karmaMutatedXml, "<base>5</base>");
        StringAssert.Contains(karmaMutatedXml, "<karma>4</karma>");
        StringAssert.Contains(karmaMutatedXml, "<totalvalue>9</totalvalue>");
    }

    [TestMethod]
    public void ApplyAttributeEdit_burn_decrements_edge_and_can_cross_the_floor()
    {
        const string xml = """
<character>
  <attributes>
    <attribute>
      <name>EDG</name>
      <base>1</base>
      <karma>0</karma>
      <value>1</value>
      <totalvalue>1</totalvalue>
      <metatypemin>1</metatypemin>
      <metatypemax>6</metatypemax>
      <metatypeaugmax>6</metatypeaugmax>
    </attribute>
  </attributes>
</character>
""";

        string burnedXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            xml,
            new AttributeEditRequest("Edge", "burn", 0));

        StringAssert.Contains(burnedXml, "<name>EDG</name>");
        StringAssert.Contains(burnedXml, "<base>0</base>");
        StringAssert.Contains(burnedXml, "<karma>0</karma>");
        StringAssert.Contains(burnedXml, "<metatypemin>0</metatypemin>");
        StringAssert.Contains(burnedXml, "<totalvalue>0</totalvalue>");
    }

    [TestMethod]
    public void ApplyAttributeEdit_improve_spends_root_karma_and_appends_expense()
    {
        const string xml = """
<character>
  <created>True</created>
  <karma>15</karma>
  <attributes>
    <attribute>
      <name>Body</name>
      <base>1</base>
      <karma>0</karma>
      <value>1</value>
      <totalvalue>1</totalvalue>
      <metatypemin>1</metatypemin>
      <metatypemax>6</metatypemax>
      <metatypeaugmax>9</metatypeaugmax>
    </attribute>
  </attributes>
</character>
""";

        string improvedXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            xml,
            new AttributeEditRequest("Body", "improve", 2));
        XDocument document = XDocument.Parse(improvedXml);
        XElement root = document.Root!;
        XElement attribute = root.Element("attributes")!.Elements("attribute").Single();
        XElement expense = root.Element("expenses")!.Elements("expense").Single();

        Assert.AreEqual("5", root.Element("karma")!.Value);
        Assert.AreEqual("1", attribute.Element("base")!.Value);
        Assert.AreEqual("1", attribute.Element("karma")!.Value);
        Assert.AreEqual("2", attribute.Element("totalvalue")!.Value);
        Assert.AreEqual("10", expense.Element("amount")!.Value);
        Assert.AreEqual("Improve Body", expense.Element("reason")!.Value);
        Assert.AreEqual("Karma", expense.Element("type")!.Value);
        Assert.AreEqual("False", expense.Element("refund")!.Value);
    }

    [TestMethod]
    public void ApplyAttributeEdit_improve_restores_burned_edge_before_adding_karma()
    {
        const string xml = """
<character>
  <created>True</created>
  <karma>15</karma>
  <attributes>
    <attribute>
      <name>EDG</name>
      <base>0</base>
      <karma>0</karma>
      <value>0</value>
      <totalvalue>0</totalvalue>
      <metatypemin>0</metatypemin>
      <metatypemax>6</metatypemax>
      <metatypeaugmax>6</metatypeaugmax>
    </attribute>
  </attributes>
</character>
""";

        string restoredXml = WorkspaceXmlMutationCatalog.ApplyAttributeEdit(
            xml,
            new AttributeEditRequest("Edge", "improve", 1));
        XDocument document = XDocument.Parse(restoredXml);
        XElement root = document.Root!;
        XElement attribute = root.Element("attributes")!.Elements("attribute").Single();
        XElement expense = root.Element("expenses")!.Elements("expense").Single();

        Assert.AreEqual("10", root.Element("karma")!.Value);
        Assert.AreEqual("1", attribute.Element("base")!.Value);
        Assert.AreEqual("0", attribute.Element("karma")!.Value);
        Assert.AreEqual("1", attribute.Element("metatypemin")!.Value);
        Assert.AreEqual("1", attribute.Element("totalvalue")!.Value);
        Assert.AreEqual("5", expense.Element("amount")!.Value);
        Assert.AreEqual("Improve Edge", expense.Element("reason")!.Value);
    }
}
