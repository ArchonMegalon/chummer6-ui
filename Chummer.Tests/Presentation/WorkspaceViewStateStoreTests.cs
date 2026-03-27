#nullable enable annotations

using System.Collections.Generic;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class WorkspaceViewStateStoreTests
{
    [TestMethod]
    public void Capture_and_restore_round_trips_workspace_view_state()
    {
        var store = new WorkspaceViewStateStore();
        var workspaceId = new CharacterWorkspaceId("ws-a");
        var state = CharacterOverviewState.Empty with
        {
            ActiveTabId = "tab-skills",
            ActiveActionId = "tab-skills.skills",
            ActiveSectionId = "skills",
            ActiveSectionJson = "{\"sectionId\":\"skills\"}",
            ActiveSectionRows =
            [
                new SectionRowState("skills[0].name", "Pistols"),
                new SectionRowState("skills[1].name", "Sneaking")
            ],
            ActiveBuildLab = new BuildLabConceptIntakeState(
                WorkspaceId: "build-lab",
                WorkflowId: "workflow.build-lab",
                Title: "Build Lab Intake",
                Summary: "Contract-first intake",
                RulesetId: "sr5",
                BuildMethod: "Priority",
                IntakeFields:
                [
                    new BuildLabIntakeField("concept", "Concept", BuildLabFieldKinds.Text, "Street sam")
                ],
                RoleBadges: [],
                ConstraintBadges: [],
                ProvenanceBadges:
                [
                    new BuildLabBadge("provenance", "Runtime-backed", BuildLabBadgeKinds.Provenance, true)
                ],
                Variants: [],
                ProgressionTimelines: [],
                ExportPayloads: [],
                ExportTargets: [],
                Actions: [],
                ExplainEntryId: "buildlab.intake",
                SourceDocumentId: "source.profile",
                CanContinue: true,
                NextSafeAction: "Review runtime drift before export.",
                RuntimeCompatibilitySummary: "One runtime binding still needs review.",
                CampaignFitSummary: "Best fit is an ops-first crew.",
                SupportClosureSummary: "Support can reuse the same runtime fingerprint.",
                Watchouts: ["Missing recap-safe output"]),
            ActiveBrowseWorkspace = new BrowseWorkspaceState(
                WorkspaceId: "browse-ws",
                WorkflowId: "workflow.browse",
                DialogId: null,
                DialogTitle: null,
                DialogMode: null,
                CanConfirm: false,
                ConfirmActionId: null,
                CancelActionId: null,
                QueryText: "armor",
                SortId: "name",
                SortDirection: "asc",
                TotalCount: 1,
                Presets: [new BrowseWorkspacePresetState("preset.core", "Core", false, true)],
                Facets:
                [
                    new BrowseWorkspaceFacetState(
                        "source",
                        "Source",
                        BrowseFacetKinds.MultiSelect,
                        true,
                        [new BrowseWorkspaceFacetOptionState("core", "Core", 1, true, null)])
                ],
                Results:
                [
                    new BrowseWorkspaceResultItemState(
                        "armor-jacket",
                        "Armor Jacket",
                        true,
                        null,
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["Availability"] = "8R" },
                        true)
                ],
                SelectedItems: [],
                ActiveDetail: null,
                ActiveResultIndex: 0,
                ActiveResultItemId: "armor-jacket",
                QueryOffset: 150,
                QueryLimit: 50),
            HasSavedWorkspace = true
        };

        store.Capture(workspaceId, state);
        WorkspaceViewState? restored = store.Restore(workspaceId);

        Assert.IsNotNull(restored);
        Assert.AreEqual("tab-skills", restored.ActiveTabId);
        Assert.AreEqual("tab-skills.skills", restored.ActiveActionId);
        Assert.AreEqual("skills", restored.ActiveSectionId);
        Assert.AreEqual("{\"sectionId\":\"skills\"}", restored.ActiveSectionJson);
        Assert.HasCount(2, restored.ActiveSectionRows);
        Assert.AreEqual("skills[0].name", restored.ActiveSectionRows[0].Path);
        Assert.AreEqual("skills[1].name", restored.ActiveSectionRows[1].Path);
        Assert.IsNotNull(restored.ActiveBuildLab);
        Assert.AreEqual("Street sam", restored.ActiveBuildLab.IntakeFields[0].Value);
        Assert.AreEqual("Review runtime drift before export.", restored.ActiveBuildLab.NextSafeAction);
        Assert.AreEqual("One runtime binding still needs review.", restored.ActiveBuildLab.RuntimeCompatibilitySummary);
        Assert.IsNotNull(restored.ActiveBuildLab.Watchouts);
        Assert.AreEqual("Missing recap-safe output", restored.ActiveBuildLab.Watchouts[0]);
        Assert.IsNotNull(restored.ActiveBrowseWorkspace);
        Assert.AreEqual("armor", restored.ActiveBrowseWorkspace.QueryText);
        Assert.AreEqual("source", restored.ActiveBrowseWorkspace.Facets[0].FacetId);
        Assert.AreEqual(150, restored.ActiveBrowseWorkspace.QueryOffset);
        Assert.AreEqual(50, restored.ActiveBrowseWorkspace.QueryLimit);
        Assert.IsTrue(restored.HasSavedWorkspace);
    }

    [TestMethod]
    public void Remove_clears_workspace_view_state_for_single_workspace()
    {
        var store = new WorkspaceViewStateStore();
        var workspaceId = new CharacterWorkspaceId("ws-a");
        store.Capture(workspaceId, CharacterOverviewState.Empty with { ActiveTabId = "tab-info" });

        store.Remove(workspaceId);

        Assert.IsNull(store.Restore(workspaceId));
    }

    [TestMethod]
    public void Clear_removes_workspace_view_state_for_all_workspaces()
    {
        var store = new WorkspaceViewStateStore();
        store.Capture(new CharacterWorkspaceId("ws-a"), CharacterOverviewState.Empty with { ActiveTabId = "tab-info" });
        store.Capture(new CharacterWorkspaceId("ws-b"), CharacterOverviewState.Empty with { ActiveTabId = "tab-skills" });

        store.Clear();

        Assert.IsNull(store.Restore(new CharacterWorkspaceId("ws-a")));
        Assert.IsNull(store.Restore(new CharacterWorkspaceId("ws-b")));
    }
}
