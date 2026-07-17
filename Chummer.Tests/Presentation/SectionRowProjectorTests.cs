using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Chummer.Tests.Presentation;

[TestClass]
public class SectionRowProjectorTests
{
    [TestMethod]
    public void BuildRows_flattens_nested_objects_and_arrays()
    {
        JsonObject payload = new()
        {
            ["name"] = "Apex",
            ["stats"] = new JsonObject
            {
                ["body"] = 4,
                ["limits"] = new JsonArray(5, 6)
            }
        };

        IReadOnlyList<SectionRowState> rows = SectionRowProjector.BuildRows(payload);

        Assert.IsTrue(rows.Any(row => row.Path == "name" && row.Value == "\"Apex\""));
        Assert.IsTrue(rows.Any(row => row.Path == "stats.body" && row.Value == "4"));
        Assert.IsTrue(rows.Any(row => row.Path == "stats.limits" && row.Value.Contains('5')));
    }

    [TestMethod]
    public void BuildRows_respects_max_row_limit()
    {
        JsonObject payload = new();
        for (int index = 0; index < 8; index++)
        {
            payload[$"field{index}"] = index;
        }

        IReadOnlyList<SectionRowState> rows = SectionRowProjector.BuildRows(payload, maxRows: 3);

        Assert.HasCount(3, rows);
    }

    [TestMethod]
    public void BuildRows_does_not_truncate_when_no_limit_is_provided()
    {
        JsonObject payload = new();
        for (int index = 0; index < 130; index++)
        {
            payload[$"field{index}"] = index;
        }

        IReadOnlyList<SectionRowState> rows = SectionRowProjector.BuildRows(payload);

        Assert.AreEqual(130, rows.Count);
    }

    [TestMethod]
    public void BuildRows_returns_empty_for_null_node()
    {
        IReadOnlyList<SectionRowState> rows = SectionRowProjector.BuildRows(node: null);
        Assert.IsEmpty(rows);
    }

    [TestMethod]
    public void BuildRows_projects_spell_defense_metrics_into_readable_row_summaries()
    {
        JsonObject payload = new()
        {
            ["metrics"] = new JsonArray
            {
                new JsonObject
                {
                    ["label"] = "Indirect Dodge",
                    ["baseValue"] = 8,
                    ["counterspellingDice"] = 4,
                    ["totalValue"] = 12,
                    ["formula"] = "REA + INT"
                },
                new JsonObject
                {
                    ["label"] = "Detection",
                    ["baseValue"] = 6,
                    ["counterspellingDice"] = 0,
                    ["totalValue"] = 6,
                    ["formula"] = "INT + LOG + WIL"
                }
            }
        };

        IReadOnlyList<SectionRowState> rows = SectionRowProjector.BuildRows("spelldefense", payload);

        Assert.AreEqual(2, rows.Count);
        SectionRowState indirectDodge = rows.Single(row => row.Path == "metrics[0]");
        StringAssert.Contains(indirectDodge.Value, "Indirect Dodge");
        StringAssert.Contains(indirectDodge.Value, "Base 8");
        StringAssert.Contains(indirectDodge.Value, "With Counter 12");
        StringAssert.Contains(indirectDodge.Value, "REA");
        StringAssert.Contains(indirectDodge.Value, "INT");

        SectionRowState detection = rows.Single(row => row.Path == "metrics[1]");
        StringAssert.Contains(detection.Value, "Detection");
        StringAssert.Contains(detection.Value, "Base 6");
        StringAssert.Contains(detection.Value, "INT");
        StringAssert.Contains(detection.Value, "LOG");
        StringAssert.Contains(detection.Value, "WIL");
    }
}
