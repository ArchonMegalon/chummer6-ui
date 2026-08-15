using System.Xml.Linq;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class StarterWorkspaceXmlFactoryTests
{
    [TestMethod]
    public void CreateCharacterXml_assigns_unique_stable_ids_to_every_seeded_editable_collection_item()
    {
        XDocument document = XDocument.Parse(StarterWorkspaceXmlFactory.CreateCharacterXml(
            RulesetDefaults.Sr5,
            "New runner",
            "Runner",
            "Priority"));

        XElement root = document.Root!;
        XElement[] editableItems =
        [
            .. root.Element("newskills")!.Element("skills")!.Elements("skill"),
            .. root.Element("qualities")!.Elements("quality"),
            .. root.Element("contacts")!.Elements("contact"),
            .. root.Element("gears")!.Elements("gear"),
            .. root.Element("weapons")!.Elements("weapon"),
            .. root.Element("armors")!.Elements("armor"),
            .. root.Element("cyberwares")!.Elements("cyberware"),
            .. root.Element("vehicles")!.Elements("vehicle")
        ];

        string[] stableIds = editableItems
            .Select(item => item.Element("guid")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();

        Assert.HasCount(editableItems.Length, stableIds);
        Assert.HasCount(stableIds.Length, stableIds.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
