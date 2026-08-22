#nullable enable annotations

using System.Text.Json.Nodes;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WorkspaceLocationEditorProjectorTests
{
    [TestMethod]
    public void TryProject_preserves_typed_kind_stable_id_and_exact_text_for_all_location_sections()
    {
        (string SectionId, WorkspaceLocationKind Kind)[] sections =
        [
            ("gearlocations", WorkspaceLocationKind.Gear),
            ("weaponlocations", WorkspaceLocationKind.Weapon),
            ("armorlocations", WorkspaceLocationKind.Armor),
            ("vehiclelocations", WorkspaceLocationKind.Vehicle)
        ];

        foreach ((string sectionId, WorkspaceLocationKind kind) in sections)
        {
            JsonObject payload = new()
            {
                ["count"] = 1,
                ["locations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["guid"] = "11111111-1111-1111-1111-111111111111",
                        ["name"] = "  Exact location  ",
                        ["notes"] = "Preserve notes"
                    }
                }
            };

            WorkspaceLocationEditorState? result =
                WorkspaceLocationEditorProjector.TryProject(sectionId.ToUpperInvariant(), payload);

            Assert.IsNotNull(result);
            Assert.AreEqual(kind, result.Kind);
            Assert.AreEqual(sectionId, result.SectionId);
            Assert.HasCount(1, result.Items);
            Assert.AreEqual(Guid.Parse("11111111-1111-1111-1111-111111111111"), result.Items[0].Id);
            Assert.AreEqual("  Exact location  ", result.Items[0].Name);
            Assert.AreEqual("Preserve notes", result.Items[0].Notes);
        }
    }

    [TestMethod]
    public void TryProject_fails_closed_on_wrong_section_count_malformed_or_duplicate_identity()
    {
        JsonObject valid = Payload(
            new JsonObject
            {
                ["guid"] = "11111111-1111-1111-1111-111111111111",
                ["name"] = "Valid",
                ["notes"] = ""
            });
        Assert.IsNull(WorkspaceLocationEditorProjector.TryProject("contacts", valid));

        JsonObject wrongCount = Payload(
            new JsonObject
            {
                ["guid"] = "11111111-1111-1111-1111-111111111111",
                ["name"] = "Valid",
                ["notes"] = ""
            });
        wrongCount["count"] = 2;
        Assert.IsNull(WorkspaceLocationEditorProjector.TryProject("gearlocations", wrongCount));

        JsonObject malformed = Payload(
            new JsonObject
            {
                ["guid"] = "not-a-guid",
                ["name"] = "Invalid",
                ["notes"] = ""
            });
        Assert.IsNull(WorkspaceLocationEditorProjector.TryProject("gearlocations", malformed));

        JsonObject duplicate = new()
        {
            ["count"] = 2,
            ["locations"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = "11111111-1111-1111-1111-111111111111",
                    ["name"] = "First",
                    ["notes"] = ""
                },
                new JsonObject
                {
                    ["guid"] = "11111111-1111-1111-1111-111111111111",
                    ["name"] = "Second",
                    ["notes"] = ""
                }
            }
        };
        Assert.IsNull(WorkspaceLocationEditorProjector.TryProject("gearlocations", duplicate));
    }

    private static JsonObject Payload(JsonObject location)
        => new()
        {
            ["count"] = 1,
            ["locations"] = new JsonArray { location }
        };
}
