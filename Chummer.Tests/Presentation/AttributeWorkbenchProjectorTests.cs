using System.Collections.Generic;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AttributeWorkbenchProjectorTests
{
    [TestMethod]
    public void BuildRows_parses_legacy_limits_strings_when_explicit_range_fields_are_missing()
    {
        IReadOnlyList<AttributeWorkbenchRow> rows = AttributeWorkbenchProjector.BuildRows(
            "attributes",
            """
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Agility",
      "base": 4,
      "karma": 1,
      "value": 5,
      "limits": "2 / 7 (10)",
      "baseUnlocked": true
    }
  ]
}
""");

        Assert.AreEqual(1, rows.Count);

        AttributeWorkbenchRow row = rows[0];
        Assert.AreEqual("Agility", row.DisplayName);
        Assert.AreEqual(2, row.MetatypeMin);
        Assert.AreEqual(7, row.MetatypeMax);
        Assert.AreEqual(10, row.MetatypeAugMax);
        Assert.AreEqual(6, row.EffectiveKarmaMaximum);
        Assert.AreEqual(7, row.EffectiveBaseMaximum);
    }

    [TestMethod]
    public void BuildRows_projects_career_mode_karma_and_improve_metadata()
    {
        IReadOnlyList<AttributeWorkbenchRow> rows = AttributeWorkbenchProjector.BuildRows(
            "attributes",
            """
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Edge",
      "baseValue": 1,
      "karmaValue": 0,
      "totalValue": 1,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 6,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": false,
      "created": true,
      "availableKarma": 15,
      "upgradeKarmaCost": 10,
      "canCareerUpgrade": true
    }
  ]
}
""");

        Assert.AreEqual(1, rows.Count);

        AttributeWorkbenchRow row = rows[0];
        Assert.IsTrue(row.CareerMode);
        Assert.IsFalse(row.BaseUnlocked);
        Assert.AreEqual(15, row.AvailableKarma);
        Assert.AreEqual(10, row.UpgradeKarmaCost);
        Assert.IsTrue(row.CanCareerUpgrade);
        Assert.IsTrue(AttributeWorkbenchProjector.CanCareerAdvance(row));
        Assert.IsTrue(AttributeWorkbenchProjector.CanBurnEdge(row));
    }
}
