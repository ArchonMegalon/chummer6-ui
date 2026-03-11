#nullable enable annotations

using System.Linq;
using System.Text.Json.Nodes;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class BrowseWorkspaceProjectorTests
{
    [TestMethod]
    public void TryProject_returns_null_for_non_browse_payload()
    {
        JsonObject payload = new()
        {
            ["sectionId"] = "profile"
        };

        Assert.IsNull(BrowseWorkspaceProjector.TryProject(payload));
    }

    [TestMethod]
    public void TryProject_projects_dialog_wrapped_browse_payload_and_retains_focus()
    {
        JsonObject payload = new()
        {
            ["DialogId"] = "dlg-1",
            ["Title"] = "Browse Gear",
            ["Mode"] = "multi-select",
            ["CanConfirm"] = true,
            ["ConfirmActionId"] = "confirm",
            ["CancelActionId"] = "cancel",
            ["Workspace"] = new JsonObject
            {
                ["WorkspaceId"] = "browse-gear",
                ["WorkflowId"] = "workflow.browse",
                ["Results"] = new JsonObject
                {
                    ["Query"] = new JsonObject
                    {
                        ["QueryText"] = "gear",
                        ["FacetSelections"] = new JsonObject
                        {
                            ["source"] = new JsonArray("core"),
                            ["pack"] = new JsonArray("street")
                        },
                        ["SortId"] = "name",
                        ["SortDirection"] = "asc",
                        ["Offset"] = 0,
                        ["Limit"] = 50
                    },
                    ["Items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["ItemId"] = "smartgun",
                            ["Title"] = "Smartgun",
                            ["ColumnValues"] = new JsonObject(),
                            ["FacetValues"] = new JsonArray(),
                            ["IsSelectable"] = true
                        },
                        new JsonObject
                        {
                            ["ItemId"] = "ammo",
                            ["Title"] = "Ammo",
                            ["ColumnValues"] = new JsonObject(),
                            ["FacetValues"] = new JsonArray(),
                            ["IsSelectable"] = true
                        }
                    },
                    ["Columns"] = new JsonArray(),
                    ["Facets"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["FacetId"] = "source",
                            ["Label"] = "Source",
                            ["Kind"] = "multi-select",
                            ["MultiSelect"] = true,
                            ["Options"] = new JsonArray
                            {
                                new JsonObject { ["Value"] = "core", ["Label"] = "Core", ["Selected"] = true, ["Count"] = 2 }
                            }
                        },
                        new JsonObject
                        {
                            ["FacetId"] = "pack",
                            ["Label"] = "Pack",
                            ["Kind"] = "multi-select",
                            ["MultiSelect"] = true,
                            ["Options"] = new JsonArray
                            {
                                new JsonObject { ["Value"] = "street", ["Label"] = "Street", ["Selected"] = true, ["Count"] = 1 }
                            }
                        }
                    },
                    ["Sorts"] = new JsonArray(),
                    ["ViewPresets"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["PresetId"] = "street-kit",
                            ["Label"] = "Street Kit",
                            ["Shared"] = true,
                            ["Query"] = new JsonObject
                            {
                                ["QueryText"] = "gear",
                                ["FacetSelections"] = new JsonObject
                                {
                                    ["source"] = new JsonArray("core"),
                                    ["pack"] = new JsonArray("street")
                                },
                                ["SortId"] = "name",
                                ["SortDirection"] = "asc",
                                ["Offset"] = 0,
                                ["Limit"] = 50
                            }
                        }
                    },
                    ["DisableReasons"] = new JsonArray(),
                    ["TotalCount"] = 2
                },
                ["Sections"] = new JsonArray(),
                ["SelectedItems"] = new JsonArray(),
                ["ActiveDetail"] = new JsonObject
                {
                    ["ItemId"] = "smartgun",
                    ["Title"] = "Smartgun",
                    ["SummaryLines"] = new JsonArray("Wireless-ready")
                }
            }
        };

        BrowseWorkspaceState previous = new(
            WorkspaceId: "browse-gear",
            WorkflowId: "workflow.browse",
            DialogId: "dlg-1",
            DialogTitle: "Browse Gear",
            DialogMode: "multi-select",
            CanConfirm: true,
            ConfirmActionId: "confirm",
            CancelActionId: "cancel",
            QueryText: "gear",
            SortId: "name",
            SortDirection: "asc",
            TotalCount: 2,
            Presets: [],
            Facets: [],
            Results: [],
            SelectedItems: [],
            ActiveDetail: null,
            ActiveResultIndex: 1,
            ActiveResultItemId: "ammo");

        BrowseWorkspaceState? projected = BrowseWorkspaceProjector.TryProject(payload, previous);

        Assert.IsNotNull(projected);
        Assert.AreEqual("dlg-1", projected.DialogId);
        Assert.IsTrue(projected.CanConfirm);
        Assert.AreEqual("street-kit", projected.Presets.Single(preset => preset.IsActive).PresetId);
        Assert.AreEqual("core", projected.SourceFacets[0].SelectedOptions[0].Value);
        Assert.AreEqual("street", projected.PackFacets[0].SelectedOptions[0].Value);
        Assert.AreEqual("ammo", projected.ActiveResultItemId);
        Assert.AreEqual(1, projected.ActiveResultIndex);
        Assert.AreEqual(0, projected.QueryOffset);
        Assert.AreEqual(50, projected.QueryLimit);
        Assert.IsTrue(projected.UsesResultWindowing);
    }
}
