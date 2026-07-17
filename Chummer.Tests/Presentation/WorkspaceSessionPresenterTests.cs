using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class WorkspaceSessionPresenterTests
{
    [TestMethod]
    public void Restore_sets_active_workspace_and_recent_order()
    {
        WorkspaceSessionPresenter presenter = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        WorkspaceListItem[] workspaces =
        [
            CreateWorkspace("ws-old", "Old", "O", now.AddMinutes(-20)),
            CreateWorkspace("ws-new", "New", "N", now.AddMinutes(-5))
        ];

        WorkspaceSessionState state = presenter.Restore(workspaces);

        Assert.AreEqual("ws-new", state.ActiveWorkspaceId?.Value);
        string[] expectedOrder = ["ws-new", "ws-old"];
        CollectionAssert.AreEqual(
            expectedOrder,
            state.OpenWorkspaces.Select(workspace => workspace.Id.Value).ToArray());
        CollectionAssert.AreEqual(
            expectedOrder,
            state.RecentWorkspaceIds.Select(id => id.Value).ToArray());
        Assert.AreEqual(2L, state.ContentRevision);
        Assert.AreEqual(1L, state.SavedRevision);
        Assert.IsTrue(state.IsDirty);
        Assert.IsTrue(state.HasSavedWorkspace);
    }

    [TestMethod]
    public void Restore_prefers_explicit_active_workspace_when_available()
    {
        WorkspaceSessionPresenter presenter = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        WorkspaceListItem[] workspaces =
        [
            CreateWorkspace("ws-old", "Old", "O", now.AddMinutes(-20)),
            CreateWorkspace("ws-new", "New", "N", now.AddMinutes(-5))
        ];

        WorkspaceSessionState state = presenter.Restore(
            workspaces,
            new CharacterWorkspaceId("ws-old"));

        Assert.AreEqual("ws-old", state.ActiveWorkspaceId?.Value);
        string[] expectedRecent = ["ws-new", "ws-old"];
        CollectionAssert.AreEqual(
            expectedRecent,
            state.RecentWorkspaceIds.Select(id => id.Value).ToArray());
    }

    [TestMethod]
    public void Switch_updates_active_workspace_and_recent_order()
    {
        WorkspaceSessionPresenter presenter = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        presenter.Restore(
        [
            CreateWorkspace("ws-1", "One", "A", now.AddMinutes(-10)),
            CreateWorkspace("ws-2", "Two", "B", now.AddMinutes(-5))
        ]);

        WorkspaceSessionState switched = presenter.Switch(new CharacterWorkspaceId("ws-1"));

        Assert.AreEqual("ws-1", switched.ActiveWorkspaceId?.Value);
        string[] expectedRecent = ["ws-1", "ws-2"];
        CollectionAssert.AreEqual(
            expectedRecent,
            switched.RecentWorkspaceIds.Select(id => id.Value).ToArray());
    }

    [TestMethod]
    public void Open_activates_workspace_and_upserts_profile_label()
    {
        WorkspaceSessionPresenter presenter = new();
        CharacterWorkspaceId workspaceId = new("ws-open");
        CharacterProfileSection profile = CreateProfile("Opened Character", "OPEN");

        WorkspaceSessionState opened = presenter.Open(workspaceId, profile, rulesetId: "sr6");

        Assert.AreEqual("ws-open", opened.ActiveWorkspaceId?.Value);
        Assert.HasCount(1, opened.OpenWorkspaces);
        Assert.AreEqual("Opened Character", opened.OpenWorkspaces[0].Name);
        Assert.AreEqual("OPEN", opened.OpenWorkspaces[0].Alias);
        Assert.AreEqual("sr6", opened.OpenWorkspaces[0].RulesetId);
        string[] expectedRecent = ["ws-open"];
        CollectionAssert.AreEqual(
            expectedRecent,
            opened.RecentWorkspaceIds.Select(id => id.Value).ToArray());
    }

    [TestMethod]
    public void Close_active_workspace_selects_most_recent_remaining_workspace()
    {
        WorkspaceSessionPresenter presenter = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        presenter.Restore(
        [
            CreateWorkspace("ws-1", "One", "A", now.AddMinutes(-15)),
            CreateWorkspace("ws-2", "Two", "B", now.AddMinutes(-10)),
            CreateWorkspace("ws-3", "Three", "C", now.AddMinutes(-5))
        ]);

        presenter.Switch(new CharacterWorkspaceId("ws-1"));
        WorkspaceSessionState closed = presenter.Close(new CharacterWorkspaceId("ws-1"));

        Assert.AreEqual("ws-3", closed.ActiveWorkspaceId?.Value);
        string[] expectedOpenWorkspaces = ["ws-2", "ws-3"];
        CollectionAssert.AreEquivalent(
            expectedOpenWorkspaces,
            closed.OpenWorkspaces.Select(workspace => workspace.Id.Value).ToArray());
    }

    [TestMethod]
    public void CloseAll_clears_open_workspaces_and_active_workspace()
    {
        WorkspaceSessionPresenter presenter = new();
        presenter.Open(new CharacterWorkspaceId("ws-1"), CreateProfile("One", "A"), rulesetId: "sr5");
        presenter.Open(new CharacterWorkspaceId("ws-2"), CreateProfile("Two", "B"), rulesetId: "sr5");

        WorkspaceSessionState cleared = presenter.CloseAll();

        Assert.IsNull(cleared.ActiveWorkspaceId);
        Assert.IsEmpty(cleared.OpenWorkspaces);
        string[] expectedRecent = ["ws-2", "ws-1"];
        CollectionAssert.AreEqual(
            expectedRecent,
            cleared.RecentWorkspaceIds.Select(id => id.Value).ToArray());
    }

    [TestMethod]
    public void SetRevisions_updates_only_target_workspace_and_drives_derived_status()
    {
        WorkspaceSessionPresenter presenter = new();
        presenter.Open(new CharacterWorkspaceId("ws-1"), CreateProfile("One", "A"), rulesetId: "sr5");
        presenter.Open(new CharacterWorkspaceId("ws-2"), CreateProfile("Two", "B"), rulesetId: "sr5");

        presenter.SetRevisions(new CharacterWorkspaceId("ws-2"), contentRevision: 2, savedRevision: 2);
        WorkspaceSessionState updated = presenter.SetRevisions(
            new CharacterWorkspaceId("ws-1"),
            contentRevision: 3,
            savedRevision: 2);

        OpenWorkspaceState ws1 = updated.OpenWorkspaces.First(workspace => string.Equals(workspace.Id.Value, "ws-1", StringComparison.Ordinal));
        OpenWorkspaceState ws2 = updated.OpenWorkspaces.First(workspace => string.Equals(workspace.Id.Value, "ws-2", StringComparison.Ordinal));
        Assert.AreEqual(3L, ws1.ContentRevision);
        Assert.AreEqual(2L, ws1.SavedRevision);
        Assert.IsTrue(ws1.IsDirty);
        Assert.IsTrue(ws1.HasSavedWorkspace);
        Assert.AreEqual(2L, ws2.ContentRevision);
        Assert.AreEqual(2L, ws2.SavedRevision);
        Assert.IsFalse(ws2.IsDirty);
        Assert.IsTrue(ws2.HasSavedWorkspace);
        string[] expectedRecent = ["ws-2", "ws-1"];
        CollectionAssert.AreEqual(
            expectedRecent,
            updated.RecentWorkspaceIds.Select(id => id.Value).ToArray());
    }

    [TestMethod]
    public void Conflict_state_is_typed_and_successful_revision_update_clears_it()
    {
        WorkspaceSessionPresenter presenter = new();
        CharacterWorkspaceId workspaceId = new("ws-conflict");
        presenter.Open(workspaceId, CreateProfile("Conflict", "C"), rulesetId: "sr5");
        presenter.SetRevisions(workspaceId, contentRevision: 4, savedRevision: 3);
        WorkspaceConflictState conflict = new(
            Operation: "save",
            ExpectedContentRevision: 4,
            ActualContentRevision: 5,
            Message: "The workspace changed elsewhere.");

        WorkspaceSessionState conflicted = presenter.SetConflictState(workspaceId, conflict);

        Assert.AreSame(conflict, conflicted.ConflictState);
        Assert.AreEqual(WorkspaceOperationOutcome.Conflict, conflicted.ConflictState?.Outcome);
        Assert.AreEqual(4L, conflicted.ContentRevision);
        Assert.AreEqual(3L, conflicted.SavedRevision);

        WorkspaceSessionState resolved = presenter.SetRevisions(workspaceId, contentRevision: 6, savedRevision: 6);

        Assert.IsNull(resolved.ConflictState);
        Assert.IsFalse(resolved.IsDirty);
    }

    [TestMethod]
    public void Close_then_open_preserves_revision_and_conflict_state()
    {
        WorkspaceSessionPresenter presenter = new();
        CharacterWorkspaceId workspaceId = new("ws-reopen");
        presenter.Open(workspaceId, CreateProfile("Reopen", "R"), rulesetId: "sr5");
        presenter.SetRevisions(workspaceId, contentRevision: 7, savedRevision: 5);
        WorkspaceConflictState conflict = new(
            Operation: "metadata",
            ExpectedContentRevision: 7,
            ActualContentRevision: 8,
            Message: "The workspace changed elsewhere.");
        presenter.SetConflictState(workspaceId, conflict);

        presenter.Close(workspaceId);
        WorkspaceSessionState reopened = presenter.Open(workspaceId, profile: null, rulesetId: null);

        Assert.AreEqual(7L, reopened.ContentRevision);
        Assert.AreEqual(5L, reopened.SavedRevision);
        Assert.IsTrue(reopened.IsDirty);
        Assert.AreSame(conflict, reopened.ConflictState);
        Assert.AreEqual("Reopen", reopened.ActiveWorkspace?.Name);
    }

    [TestMethod]
    public void Forget_removes_cached_revision_and_conflict_state_after_delete()
    {
        WorkspaceSessionPresenter presenter = new();
        CharacterWorkspaceId workspaceId = new("ws-delete");
        presenter.Open(workspaceId, CreateProfile("Delete", "D"), rulesetId: "sr5");
        presenter.SetRevisions(workspaceId, contentRevision: 9, savedRevision: 8);
        presenter.SetConflictState(
            workspaceId,
            new WorkspaceConflictState("delete", 9, 10, "The workspace changed elsewhere."));

        presenter.Forget(workspaceId);
        WorkspaceSessionState reopened = presenter.Open(workspaceId, CreateProfile("Replacement", "N"), rulesetId: "sr5");

        Assert.AreEqual(0L, reopened.ContentRevision);
        Assert.AreEqual(0L, reopened.SavedRevision);
        Assert.IsNull(reopened.ConflictState);
        CollectionAssert.AreEqual(
            new[] { "ws-delete" },
            reopened.RecentWorkspaceIds.Select(id => id.Value).ToArray());
    }

    [TestMethod]
    public void Active_character_overview_state_derives_revision_truth_from_session()
    {
        WorkspaceSessionPresenter presenter = new();
        CharacterWorkspaceId workspaceId = new("ws-overview");
        presenter.Open(workspaceId, CreateProfile("Overview", "O"), rulesetId: "sr5");
        WorkspaceSessionState session = presenter.SetRevisions(workspaceId, contentRevision: 12, savedRevision: 10);

        CharacterOverviewState overview = CharacterOverviewState.Empty with
        {
            Session = session,
            WorkspaceId = workspaceId,
            OpenWorkspaces = session.OpenWorkspaces
        };

        Assert.AreEqual(12L, overview.ContentRevision);
        Assert.AreEqual(10L, overview.SavedRevision);
        Assert.IsTrue(overview.IsDirty);
        Assert.IsTrue(overview.HasSavedWorkspace);
    }

    [TestMethod]
    public void Workspace_view_state_derives_dirty_and_saved_status_from_revisions()
    {
        WorkspaceConflictState conflict = new(
            Operation: "metadata",
            ExpectedContentRevision: 3,
            ActualContentRevision: 4,
            Message: "The workspace changed elsewhere.");
        WorkspaceViewState viewState = new(
            ActiveTabId: "tab-info",
            ActiveActionId: "tab-info.summary",
            ActiveSectionId: "summary",
            ActiveSectionJson: null,
            ActiveSectionRows: [],
            ActiveBuildLab: null,
            ActiveBrowseWorkspace: null,
            ContentRevision: 3,
            SavedRevision: 2,
            ConflictState: conflict);

        Assert.AreEqual(3L, viewState.ContentRevision);
        Assert.AreEqual(2L, viewState.SavedRevision);
        Assert.IsTrue(viewState.IsDirty);
        Assert.IsTrue(viewState.HasSavedWorkspace);
        Assert.AreSame(conflict, viewState.ConflictState);
    }

    [TestMethod]
    public void Workspace_conflict_state_rejects_invalid_revision_evidence()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WorkspaceConflictState(" ", 1, 2, "Conflict"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkspaceConflictState("save", 0, 2, "Conflict"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkspaceConflictState("save", 1, 0, "Conflict"));
        Assert.ThrowsExactly<ArgumentException>(() => new WorkspaceConflictState("save", 1, 2, " "));
    }

    [TestMethod]
    public void SetRevisions_rejects_invalid_revision_relationships()
    {
        WorkspaceSessionPresenter presenter = new();
        CharacterWorkspaceId workspaceId = new("ws-invalid");
        presenter.Open(workspaceId, CreateProfile("Invalid", "I"), rulesetId: "sr5");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => presenter.SetRevisions(workspaceId, -1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => presenter.SetRevisions(workspaceId, 1, -1));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.SetRevisions(workspaceId, 1, 2));
    }

    private static WorkspaceListItem CreateWorkspace(
        string id,
        string name,
        string alias,
        DateTimeOffset lastUpdatedUtc,
        long contentRevision = 2,
        long savedRevision = 1)
    {
        return new WorkspaceListItem(
            Id: new CharacterWorkspaceId(id),
            Summary: new CharacterFileSummary(
                Name: name,
                Alias: alias,
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "5",
                AppVersion: "5",
                Karma: 0m,
                Nuyen: 0m,
                Created: true),
            LastUpdatedUtc: lastUpdatedUtc,
            RulesetId: "sr5",
            ContentRevision: contentRevision,
            SavedRevision: savedRevision);
    }

    private static CharacterProfileSection CreateProfile(string name, string alias)
    {
        return new CharacterProfileSection(
            Name: name,
            Alias: alias,
            PlayerName: string.Empty,
            Metatype: "Human",
            Metavariant: string.Empty,
            Sex: string.Empty,
            Age: string.Empty,
            Height: string.Empty,
            Weight: string.Empty,
            Hair: string.Empty,
            Eyes: string.Empty,
            Skin: string.Empty,
            Concept: string.Empty,
            Description: string.Empty,
            Background: string.Empty,
            CreatedVersion: string.Empty,
            AppVersion: string.Empty,
            BuildMethod: "Priority",
            GameplayOption: string.Empty,
            Created: true,
            Adept: false,
            Magician: false,
            Technomancer: false,
            AI: false,
            MainMugshotIndex: 0,
            MugshotCount: 0);
    }
}
