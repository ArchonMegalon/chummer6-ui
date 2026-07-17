#nullable enable annotations

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Run.Contracts.Billing;
using Chummer.Presentation.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class CharacterOverviewPresenterTests
{
    private static readonly string[] LegacyUiControlIds = LegacyUiControlCatalog.All.ToArray();

    private static CharacterOverviewPresenter CreateTrustedPresenter(
        IChummerClient client,
        IWorkspaceSessionManager? workspaceSessionManager = null,
        IDesktopDialogFactory? dialogFactory = null,
        IWorkspaceSessionPresenter? workspaceSessionPresenter = null,
        IOverviewCommandDispatcher? commandDispatcher = null,
        IDialogCoordinator? dialogCoordinator = null,
        IWorkspaceOverviewLoader? workspaceOverviewLoader = null,
        IWorkspaceSectionRenderer? workspaceSectionRenderer = null,
        IWorkspacePersistenceService? workspacePersistenceService = null,
        IWorkspaceViewStateStore? workspaceViewStateStore = null,
        IWorkspaceShellStateFactory? workspaceShellStateFactory = null,
        IWorkspaceRemoteCloseService? workspaceRemoteCloseService = null,
        IWorkspaceSessionActivationService? workspaceSessionActivationService = null,
        IWorkspaceOverviewStateFactory? workspaceOverviewStateFactory = null,
        IWorkspaceOverviewLifecycleCoordinator? workspaceOverviewLifecycleCoordinator = null,
        IShellBootstrapDataProvider? bootstrapDataProvider = null,
        IRulesetShellCatalogResolver? shellCatalogResolver = null,
        IShellPresenter? shellPresenter = null,
        IEngineEvaluator? engineEvaluator = null,
        IWorkspaceOperationCoordinator? workspaceOperationCoordinator = null,
        IWorkspaceRecoveryPayloadStore? workspaceRecoveryPayloadStore = null,
        TimeSpan? deletionNotificationBudget = null)
        => new CharacterOverviewPresenter(
            client,
            workspaceSessionManager,
            dialogFactory,
            workspaceSessionPresenter,
            commandDispatcher,
            dialogCoordinator,
            workspaceOverviewLoader ?? WorkspaceOverviewLoader.CreateCompositionBound(client),
            workspaceSectionRenderer,
            workspacePersistenceService,
            workspaceViewStateStore,
            workspaceShellStateFactory,
            workspaceRemoteCloseService,
            workspaceSessionActivationService,
            workspaceOverviewStateFactory,
            workspaceOverviewLifecycleCoordinator,
            bootstrapDataProvider,
            shellCatalogResolver,
            shellPresenter,
            engineEvaluator,
            workspaceOperationCoordinator,
            workspaceRecoveryPayloadStore,
            deletionNotificationBudget);

    [TestMethod]
    public async Task InitializeAsync_loads_command_catalog()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.IsFalse(presenter.State.IsBusy);
        Assert.IsNull(presenter.State.Error);
        Assert.IsGreaterThan(0, presenter.State.Commands.Count);
        Assert.AreEqual("new_character", presenter.State.Commands[0].Id);
        Assert.IsGreaterThan(0, presenter.State.NavigationTabs.Count);
        Assert.AreEqual("tab-info", presenter.State.NavigationTabs[0].Id);
    }

    [TestMethod]
    public async Task InitializeAsync_restores_open_workspaces_from_service()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-legacy-1", "Legacy One", "L1", DateTimeOffset.UtcNow.AddMinutes(-10));
        client.SeedWorkspace("ws-legacy-2", "Legacy Two", "L2", DateTimeOffset.UtcNow.AddMinutes(-1));
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.HasCount(2, presenter.State.OpenWorkspaces);
        Assert.AreEqual("ws-legacy-2", presenter.State.OpenWorkspaces[0].Id.Value);
        Assert.AreEqual("ws-legacy-1", presenter.State.OpenWorkspaces[1].Id.Value);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Restored 2 runner dossiers.");
    }

    [TestMethod]
    public async Task InitializeAsync_reuses_initialized_shell_state_without_refetching_bootstrap()
    {
        var client = new FakeChummerClient();
        var shellState = ShellState.Empty with
        {
            Commands = FakeChummerClient.Commands,
            NavigationTabs = FakeChummerClient.Tabs,
            OpenWorkspaces =
            [
                new ShellWorkspaceState(
                    Id: new CharacterWorkspaceId("ws-shell-1"),
                    Name: "Shell Workspace",
                    Alias: "SHELL",
                    LastOpenedUtc: DateTimeOffset.UtcNow,
                    RulesetId: "sr6")
            ],
            ActiveRulesetId = "sr6",
            ActiveWorkspaceId = new CharacterWorkspaceId("ws-shell-1")
        };

        var presenter = CreateTrustedPresenter(
            client,
            shellPresenter: new ShellPresenterStub(shellState));

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.AreEqual(0, client.GetCommandsCalls);
        Assert.AreEqual(0, client.GetNavigationTabsCalls);
        Assert.AreEqual(0, client.ListWorkspacesCalls);
        Assert.HasCount(1, presenter.State.OpenWorkspaces);
        Assert.AreEqual("ws-shell-1", presenter.State.OpenWorkspaces[0].Id.Value);
        Assert.AreEqual("sr6", presenter.State.OpenWorkspaces[0].RulesetId);
    }

    [TestMethod]
    public async Task LoadAsync_populates_profile_progress_and_skills()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);

        Assert.IsFalse(presenter.State.IsBusy);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNotNull(presenter.State.Profile);
        Assert.IsNotNull(presenter.State.Progress);
        Assert.IsNotNull(presenter.State.Skills);
        Assert.IsNotNull(presenter.State.Rules);
        Assert.IsNotNull(presenter.State.Build);
        Assert.IsNotNull(presenter.State.Movement);
        Assert.IsNotNull(presenter.State.Awakening);
        Assert.IsTrue(string.IsNullOrWhiteSpace(presenter.State.Notice));
        Assert.IsNull(presenter.State.LatestPortabilityActivity);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("BLUE", presenter.State.Profile.Alias);
    }

    [TestMethod]
    public async Task ImportAsync_loads_workspace_and_sections()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.ImportAsync(
            new WorkspaceImportDocument("<character><name>Imported</name></character>", RulesetDefaults.Sr5, WorkspaceDocumentFormat.NativeXml),
            CancellationToken.None);

        Assert.IsFalse(presenter.State.IsBusy);
        Assert.IsNull(presenter.State.Error);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.IsNotNull(presenter.State.Profile);
        Assert.IsNotNull(presenter.State.Progress);
        Assert.IsNotNull(presenter.State.Skills);
        Assert.IsNotNull(presenter.State.Rules);
        Assert.IsNotNull(presenter.State.Build);
        Assert.IsNotNull(presenter.State.Movement);
        Assert.IsNotNull(presenter.State.Awakening);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Portable import ready:");
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Portable import completed");
        Assert.IsNotNull(presenter.State.LatestPortabilityActivity);
        Assert.AreEqual("Last portable import", presenter.State.LatestPortabilityActivity?.Title);
    }

    [TestMethod]
    public async Task LoadAsync_selects_default_surface_when_workspace_has_no_restored_view()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);

        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.AreEqual("profile", presenter.State.ActiveSectionId);
        Assert.IsGreaterThan(0, presenter.State.ActiveSectionRows.Count);
        StringAssert.Contains(presenter.State.ActiveSectionJson ?? string.Empty, "\"sectionId\": \"profile\"");
    }

    [TestMethod]
    public async Task ImportAsync_selects_default_surface_when_workspace_has_no_restored_view()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.ImportAsync(
            new WorkspaceImportDocument("<character><name>Imported</name></character>", RulesetDefaults.Sr5, WorkspaceDocumentFormat.NativeXml),
            CancellationToken.None);

        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.AreEqual("profile", presenter.State.ActiveSectionId);
        Assert.IsGreaterThan(0, presenter.State.ActiveSectionRows.Count);
        StringAssert.Contains(presenter.State.ActiveSectionJson ?? string.Empty, "\"sectionId\": \"profile\"");
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_new_character_opens_creation_dialog()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.ExecuteCommandAsync("new_character", CancellationToken.None);

        Assert.IsNull(client.LastImportedDocument);
        Assert.AreEqual("new_character", presenter.State.LastCommandId);
        Assert.AreEqual("dialog.new_character", presenter.State.ActiveDialog?.Id);
        Assert.IsNull(presenter.State.WorkspaceId);
        Assert.IsNull(presenter.State.Profile);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_new_critter_imports_starter_critter_workspace()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.ExecuteCommandAsync("new_critter", CancellationToken.None);

        Assert.IsNotNull(client.LastImportedDocument);
        Assert.AreEqual(RulesetDefaults.Sr5, client.LastImportedDocument!.RulesetId);
        StringAssert.Contains(client.LastImportedDocument.Content, "<name>New Critter</name>");
        Assert.AreEqual("new_critter", presenter.State.LastCommandId);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
    }

    [TestMethod]
    public async Task ImportAsync_resolves_ruleset_from_bootstrap_when_document_seed_is_blank()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.ImportAsync(
            new WorkspaceImportDocument("<character><name>Imported</name></character>", string.Empty, WorkspaceDocumentFormat.NativeXml),
            CancellationToken.None);

        Assert.IsNotNull(client.LastImportedDocument);
        Assert.AreEqual(RulesetDefaults.Sr5, client.LastImportedDocument!.RulesetId);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
    }

    [TestMethod]
    public async Task ImportAsync_resolves_ruleset_from_document_gameedition_when_document_seed_is_blank()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.ImportAsync(
            new WorkspaceImportDocument(
                "<character><gameedition>SR6</gameedition><name>Imported</name></character>",
                string.Empty,
                WorkspaceDocumentFormat.NativeXml),
            CancellationToken.None);

        Assert.IsNotNull(client.LastImportedDocument);
        Assert.AreEqual("sr6", client.LastImportedDocument!.RulesetId);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
    }

    [TestMethod]
    public async Task ImportAsync_resolves_sr4_alias_from_document_gameedition_when_document_seed_is_blank()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.ImportAsync(
            new WorkspaceImportDocument(
                "<character><gameedition>Shadowrun 4</gameedition><name>Imported</name></character>",
                string.Empty,
                WorkspaceDocumentFormat.NativeXml),
            CancellationToken.None);

        Assert.IsNotNull(client.LastImportedDocument);
        Assert.AreEqual(RulesetDefaults.Sr4, client.LastImportedDocument!.RulesetId);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
    }

    [TestMethod]
    public async Task LoadAsync_tracks_open_workspaces_for_multi_document_shell_state()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);

        Assert.AreEqual("ws-2", presenter.State.WorkspaceId?.Value);
        Assert.HasCount(2, presenter.State.OpenWorkspaces);
        string[] expectedWorkspaceIds = ["ws-1", "ws-2"];
        CollectionAssert.AreEquivalent(
            expectedWorkspaceIds,
            presenter.State.OpenWorkspaces.Select(workspace => workspace.Id.Value).ToArray());
    }

    [TestMethod]
    public async Task SwitchWorkspaceAsync_restores_workspace_specific_tab_and_section_context()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.SelectTabAsync("tab-info", CancellationToken.None);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);
        await presenter.SelectTabAsync("tab-gear", CancellationToken.None);

        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.AreEqual("profile", presenter.State.ActiveSectionId);

        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);
        Assert.AreEqual("ws-2", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("tab-gear", presenter.State.ActiveTabId);
        Assert.AreEqual("gear", presenter.State.ActiveSectionId);
    }

    [TestMethod]
    public async Task SwitchWorkspaceAsync_does_not_reload_when_target_workspace_is_already_active()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        int getProfileCalls = client.GetProfileCalls;

        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);

        Assert.AreEqual(getProfileCalls, client.GetProfileCalls);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("Dossier 'ws-1' is already active.", presenter.State.Notice);
    }

    [TestMethod]
    public async Task CloseWorkspaceAsync_closes_active_workspace_and_switches_to_recent_workspace()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);
        await presenter.CloseWorkspaceAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);

        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("ws-1", presenter.State.Session.ActiveWorkspaceId?.Value);
        Assert.HasCount(1, presenter.State.Session.OpenWorkspaces);
        Assert.AreEqual("ws-1", presenter.State.Session.OpenWorkspaces[0].Id.Value);
    }

    [TestMethod]
    public async Task CloseWorkspaceAsync_handles_remote_close_errors_and_keeps_local_shell_consistent()
    {
        var client = new FakeChummerClient
        {
            ThrowOnCloseWorkspace = true
        };
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);
        await presenter.CloseWorkspaceAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);

        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("ws-1", presenter.State.Session.ActiveWorkspaceId?.Value);
        Assert.HasCount(1, presenter.State.OpenWorkspaces);
        Assert.AreEqual(0, client.CloseWorkspaceCalls);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "without deleting it");
    }

    [TestMethod]
    public async Task CloseWorkspaceAsync_is_non_destructive_and_workspace_can_reopen()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-close", "Close Me", "CLOSE", contentRevision: 4, savedRevision: 4);
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-close"), CancellationToken.None);
        await presenter.CloseWorkspaceAsync(new CharacterWorkspaceId("ws-close"), CancellationToken.None);

        Assert.AreEqual(0, client.CloseWorkspaceCalls);
        Assert.IsTrue(client.ContainsWorkspace("ws-close"));
        Assert.IsNull(presenter.State.WorkspaceId);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-close"), CancellationToken.None);

        Assert.AreEqual("ws-close", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual(4L, presenter.State.ContentRevision);
        Assert.AreEqual(4L, presenter.State.SavedRevision);
        Assert.IsFalse(presenter.State.IsDirty);
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsync_requires_confirmation_and_then_deletes_clean_workspace()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-delete", "Delete Me", "DELETE", contentRevision: 3, savedRevision: 3);
        var presenter = CreateTrustedPresenter(client);
        CharacterWorkspaceId workspaceId = new("ws-delete");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        await presenter.DeleteWorkspaceAsync(workspaceId, confirmed: false, CancellationToken.None);

        Assert.AreEqual(0, client.CloseWorkspaceCalls);
        Assert.IsTrue(client.ContainsWorkspace(workspaceId.Value));
        Assert.AreEqual(
            $"Delete dossier '{workspaceId.Value}' from Chummer? It will no longer appear in your account. Files you downloaded are not affected.",
            presenter.State.Notice);

        await presenter.DeleteWorkspaceAsync(workspaceId, confirmed: true, CancellationToken.None);

        Assert.AreEqual(1, client.CloseWorkspaceCalls);
        Assert.IsFalse(client.ContainsWorkspace(workspaceId.Value));
        Assert.IsNull(presenter.State.WorkspaceId);
        Assert.HasCount(0, presenter.State.OpenWorkspaces);
        Assert.AreEqual($"Deleted dossier '{workspaceId.Value}' from Chummer.", presenter.State.Notice);
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsync_callback_failure_cannot_resurrect_receipt_backed_commit()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-delete-notify", "Delete Notify", "DELETE", contentRevision: 7, savedRevision: 7);
        var presenter = CreateTrustedPresenter(client);
        CharacterWorkspaceId workspaceId = new("ws-delete-notify");
        WorkspaceDeletionCommit? observedCommit = null;
        ((IWorkspaceDeletionCommitSource)presenter).WorkspaceDeletionCommitted += (commit, _) =>
        {
            observedCommit = commit;
            throw new InvalidOperationException("Simulated cross-tab callback failure.");
        };

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        await presenter.DeleteWorkspaceAsync(workspaceId, confirmed: true, CancellationToken.None);

        Assert.IsNotNull(observedCommit);
        Assert.AreEqual(workspaceId, observedCommit.WorkspaceId);
        Assert.AreEqual(7L, observedCommit.Revision);
        Assert.AreEqual(1, client.CloseWorkspaceCalls);
        Assert.IsFalse(client.ContainsWorkspace(workspaceId.Value));
        Assert.IsNull(presenter.State.WorkspaceId);
        Assert.HasCount(0, presenter.State.OpenWorkspaces);
        Assert.IsNull(presenter.State.Error);
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsync_conflict_or_null_receipt_emits_no_commit()
    {
        var conflictClient = new FakeChummerClient();
        conflictClient.SeedWorkspace("ws-delete-conflict", "Conflict", "CONFLICT", contentRevision: 3, savedRevision: 3);
        var conflictPresenter = CreateTrustedPresenter(conflictClient);
        CharacterWorkspaceId conflictId = new("ws-delete-conflict");
        int conflictNotifications = 0;
        ((IWorkspaceDeletionCommitSource)conflictPresenter).WorkspaceDeletionCommitted += (_, _) =>
        {
            conflictNotifications++;
            return Task.CompletedTask;
        };

        await conflictPresenter.LoadAsync(conflictId, CancellationToken.None);
        conflictClient.SeedWorkspace(conflictId.Value, "Winner", "WINNER", contentRevision: 4, savedRevision: 4);
        await conflictPresenter.DeleteWorkspaceAsync(conflictId, confirmed: true, CancellationToken.None);

        Assert.AreEqual(0, conflictNotifications);
        Assert.IsTrue(conflictClient.ContainsWorkspace(conflictId.Value));
        Assert.AreEqual(conflictId, conflictPresenter.State.WorkspaceId);

        var nullReceiptClient = new FakeChummerClient { ReturnNullDeleteReceipt = true };
        nullReceiptClient.SeedWorkspace("ws-delete-null", "Null", "NULL", contentRevision: 5, savedRevision: 5);
        var nullReceiptPresenter = CreateTrustedPresenter(nullReceiptClient);
        CharacterWorkspaceId nullReceiptId = new("ws-delete-null");
        int nullReceiptNotifications = 0;
        ((IWorkspaceDeletionCommitSource)nullReceiptPresenter).WorkspaceDeletionCommitted += (_, _) =>
        {
            nullReceiptNotifications++;
            return Task.CompletedTask;
        };

        await nullReceiptPresenter.LoadAsync(nullReceiptId, CancellationToken.None);
        await nullReceiptPresenter.DeleteWorkspaceAsync(nullReceiptId, confirmed: true, CancellationToken.None);

        Assert.AreEqual(0, nullReceiptNotifications);
        Assert.IsTrue(nullReceiptClient.ContainsWorkspace(nullReceiptId.Value));
        Assert.AreEqual(nullReceiptId, nullReceiptPresenter.State.WorkspaceId);
        StringAssert.Contains(nullReceiptPresenter.State.Error ?? string.Empty, "revision receipt");
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsync_request_cancellation_after_cas_receipt_remains_committed_success()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-delete-cancel", "Delete Cancel", "CANCEL", contentRevision: 6, savedRevision: 6);
        using var request = new CancellationTokenSource();
        client.DeleteReceiptCommitted = request.Cancel;
        var presenter = CreateTrustedPresenter(client);
        CharacterWorkspaceId workspaceId = new("ws-delete-cancel");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        await presenter.DeleteWorkspaceAsync(workspaceId, confirmed: true, request.Token);

        Assert.IsTrue(request.IsCancellationRequested);
        Assert.IsFalse(client.ContainsWorkspace(workspaceId.Value));
        Assert.IsNull(presenter.State.WorkspaceId);
        Assert.IsNull(presenter.State.Error);
        Assert.AreEqual($"Deleted dossier '{workspaceId.Value}' from Chummer.", presenter.State.Notice);
    }

    [TestMethod]
    public async Task Hung_deletion_subscriber_does_not_suppress_later_tombstones_for_responsive_subscribers()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-delete-hung-1", "First", "FIRST", contentRevision: 2, savedRevision: 2);
        client.SeedWorkspace("ws-delete-hung-2", "Second", "SECOND", contentRevision: 3, savedRevision: 3);
        using var presenter = CreateTrustedPresenter(
            client,
            deletionNotificationBudget: TimeSpan.FromMilliseconds(25));
        var never = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<string>();
        IWorkspaceDeletionCommitSource source = presenter;
        source.WorkspaceDeletionCommitted += (_, _) => never.Task;
        source.WorkspaceDeletionCommitted += (commit, _) =>
        {
            lock (received)
                received.Add(commit.WorkspaceId.Value);
            return Task.CompletedTask;
        };

        var first = new CharacterWorkspaceId("ws-delete-hung-1");
        var second = new CharacterWorkspaceId("ws-delete-hung-2");
        try
        {
            await presenter.LoadAsync(first, CancellationToken.None);
            await presenter.DeleteWorkspaceAsync(first, confirmed: true, CancellationToken.None);
            await presenter.LoadAsync(second, CancellationToken.None);
            await presenter.DeleteWorkspaceAsync(second, confirmed: true, CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { first.Value, second.Value },
                received.ToArray());
        }
        finally
        {
            // The production owner now correctly drains lifecycle subscriber
            // lanes. Release this deliberately hung fixture before the owned
            // lifecycle is disposed, including when an assertion fails.
            never.TrySetResult(true);
        }
    }

    [TestMethod]
    public async Task Slow_deletion_subscriber_receives_only_newest_bounded_pending_tombstone()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-delete-pending-1", "First", "FIRST", contentRevision: 1, savedRevision: 1);
        client.SeedWorkspace("ws-delete-pending-2", "Second", "SECOND", contentRevision: 2, savedRevision: 2);
        client.SeedWorkspace("ws-delete-pending-3", "Third", "THIRD", contentRevision: 3, savedRevision: 3);
        using var presenter = CreateTrustedPresenter(
            client,
            deletionNotificationBudget: TimeSpan.FromMilliseconds(25));
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newestObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<string>();
        IWorkspaceDeletionCommitSource source = presenter;
        source.WorkspaceDeletionCommitted += async (commit, _) =>
        {
            bool first;
            lock (received)
            {
                received.Add(commit.WorkspaceId.Value);
                first = received.Count == 1;
            }

            if (first)
            {
                firstStarted.TrySetResult(true);
                await releaseFirst.Task.ConfigureAwait(false);
            }
            else
            {
                newestObserved.TrySetResult(true);
            }
        };

        foreach (string key in new[] { "ws-delete-pending-1", "ws-delete-pending-2", "ws-delete-pending-3" })
        {
            var workspaceId = new CharacterWorkspaceId(key);
            await presenter.LoadAsync(workspaceId, CancellationToken.None);
            await presenter.DeleteWorkspaceAsync(workspaceId, confirmed: true, CancellationToken.None);
            if (key.EndsWith("-1", StringComparison.Ordinal))
                await firstStarted.Task;
        }

        releaseFirst.TrySetResult(true);
        await newestObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        lock (received)
        {
            CollectionAssert.AreEqual(
                new[] { "ws-delete-pending-1", "ws-delete-pending-3" },
                received.ToArray());
        }
    }

    [TestMethod]
    public async Task DeleteWorkspaceAsync_next_runner_load_failure_reports_committed_warning_not_deletion_failure()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-delete-next", "Next", "NEXT", contentRevision: 2, savedRevision: 2);
        client.SeedWorkspace("ws-delete-active", "Delete Active", "DELETE", contentRevision: 7, savedRevision: 7);
        var presenter = CreateTrustedPresenter(client);
        CharacterWorkspaceId nextId = new("ws-delete-next");
        CharacterWorkspaceId deletedId = new("ws-delete-active");

        await presenter.LoadAsync(nextId, CancellationToken.None);
        await presenter.LoadAsync(deletedId, CancellationToken.None);
        client.ThrowGetWorkspaceId = nextId.Value;
        await presenter.DeleteWorkspaceAsync(deletedId, confirmed: true, CancellationToken.None);

        Assert.IsFalse(client.ContainsWorkspace(deletedId.Value));
        Assert.IsTrue(client.ContainsWorkspace(nextId.Value));
        Assert.IsNull(presenter.State.WorkspaceId);
        Assert.IsNull(presenter.State.Error);
        Assert.AreEqual(
            $"Deleted dossier '{deletedId.Value}' from Chummer, but the next runner could not be opened. Select another runner to continue.",
            presenter.State.Notice);
    }

    [TestMethod]
    public async Task Deleted_dirty_workspace_streams_byte_exact_memory_copy_without_remote_reread_then_clears_on_durable_close()
    {
        const string sourceXml = "<character><name>Byte Exact</name><alias>EXACT</alias><notes>local-only</notes></character>";
        WorkspaceDocument committedDocument = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
            sourceXml,
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml));
        string exactCommittedXml = committedDocument.Content;
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-recovery", "Byte Exact", "EXACT", contentRevision: 5, savedRevision: 4);
        client.SeedDocument("ws-recovery", committedDocument);
        var recoveryStore = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(
            client,
            workspaceRecoveryPayloadStore: recoveryStore);
        CharacterWorkspaceId workspaceId = new("ws-recovery");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        WorkspaceRecoveryCopyAvailability availability = ((IWorkspaceRecoveryCopySource)presenter)
            .GetRecoveryCopyAvailability(workspaceId, 5);
        Assert.IsTrue(availability.Available);

        CommandResult<WorkspaceRevisionReceipt> deleted = await client.CloseWorkspaceAsync(
            workspaceId,
            expectedContentRevision: 5,
            ct: CancellationToken.None);
        Assert.IsTrue(deleted.Success);
        int readsAfterDelete = client.GetWorkspaceCalls;
        int downloadsAfterDelete = client.DownloadCalls;

        WorkspaceRecoveryCopyExportResult exported = await ((IWorkspaceRecoveryCopySource)presenter)
            .PrepareRecoveryCopyAsync(
                workspaceId,
                expectedSourceRevision: 5,
                expectedLocalGeneration: availability.LocalGeneration,
                ct: CancellationToken.None);

        Assert.IsTrue(exported.Success, exported.Error);
        Assert.AreEqual("application/xml", exported.ContentType);
        Assert.IsNull(presenter.State.PendingDownload);
        Assert.IsNotNull(presenter.State.PendingRecoveryExport);
        WorkspaceRecoveryExportRequest request = presenter.State.PendingRecoveryExport!;
        Assert.AreEqual(Encoding.UTF8.GetByteCount(exactCommittedXml), request.DocumentLength);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            request.ExportToken,
            "^[0-9a-f]{64}$"));
        Assert.AreEqual(readsAfterDelete, client.GetWorkspaceCalls);
        Assert.AreEqual(downloadsAfterDelete, client.DownloadCalls);

        WorkspaceRecoveryCloseResult prematureClose = await ((IWorkspaceRecoveryCopySource)presenter)
            .CloseExportedRecoveryCopyAsync(
                workspaceId,
                expectedSourceRevision: 5,
                expectedLocalGeneration: availability.LocalGeneration,
                ct: CancellationToken.None);
        Assert.IsFalse(prematureClose.Success);
        Assert.AreEqual(workspaceId, presenter.State.WorkspaceId);
        Assert.IsTrue(recoveryStore.GetAvailability(workspaceId, 5).Available);

        IWorkspaceRecoveryDownloadDispatchSink dispatch = presenter;
        Assert.IsTrue(dispatch.TryAcquireRecoveryCopyExportLease(
            request,
            out WorkspaceRecoveryPayloadLease? lease));
        Assert.IsNotNull(lease);
        await using (var copied = new MemoryStream())
        {
            using (lease)
            {
                await lease!.Stream.CopyToAsync(copied);
            }
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(exactCommittedXml), copied.ToArray());
        }
        Assert.IsTrue(dispatch.CompleteRecoveryCopyExport(
            request,
            new WorkspaceRecoveryBrowserExportOutcome(
                WorkspaceRecoveryBrowserExportOutcome.DurableSaved)));

        WorkspaceRecoveryCloseResult closed = await ((IWorkspaceRecoveryCopySource)presenter)
            .CloseExportedRecoveryCopyAsync(
                workspaceId,
                expectedSourceRevision: 5,
                expectedLocalGeneration: availability.LocalGeneration,
                ct: CancellationToken.None);

        Assert.IsTrue(closed.Success, closed.Error);
        Assert.IsNull(presenter.State.WorkspaceId);
        Assert.IsFalse(recoveryStore.GetAvailability(workspaceId, 5).Available);
        Assert.AreEqual(readsAfterDelete, client.GetWorkspaceCalls);
        Assert.AreEqual(downloadsAfterDelete, client.DownloadCalls);
    }

    [TestMethod]
    public async Task Blob_recovery_dispatch_requires_explicit_ack_before_preserved_runner_can_close()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-recovery-ack", "Recovery Ack", "ACK", contentRevision: 5, savedRevision: 4);
        client.SeedDocument("ws-recovery-ack", "<character><name>Recovery Ack</name></character>");
        var store = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        CharacterWorkspaceId workspaceId = new("ws-recovery-ack");
        IWorkspaceRecoveryCopySource source = presenter;
        IWorkspaceRecoveryDownloadDispatchSink dispatch = presenter;

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        WorkspaceRecoveryCopyAvailability availability = source.GetRecoveryCopyAvailability(workspaceId, 5);
        Assert.IsTrue(availability.Available);
        Assert.IsTrue((await client.CloseWorkspaceAsync(workspaceId, 5, CancellationToken.None)).Success);

        WorkspaceRecoveryCopyExportResult prepared = await source.PrepareRecoveryCopyAsync(
            workspaceId,
            5,
            availability.LocalGeneration,
            CancellationToken.None);
        Assert.IsTrue(prepared.Success, prepared.Error);
        WorkspaceRecoveryExportRequest request = presenter.State.PendingRecoveryExport!;
        Assert.IsTrue(dispatch.TryAcquireRecoveryCopyExportLease(request, out WorkspaceRecoveryPayloadLease? lease));
        lease!.Dispose();
        Assert.IsTrue(dispatch.CompleteRecoveryCopyExport(
            request,
            new WorkspaceRecoveryBrowserExportOutcome(
                WorkspaceRecoveryBrowserExportOutcome.DispatchedRequiresExplicitUserAck)));

        WorkspaceRecoveryCopyAvailability awaitingAck = source.GetRecoveryCopyAvailability(workspaceId, 5);
        Assert.IsTrue(awaitingAck.AwaitingExplicitUserAck);
        Assert.IsFalse(awaitingAck.ExportConfirmed);
        Assert.IsFalse((await source.CloseExportedRecoveryCopyAsync(
            workspaceId,
            5,
            availability.LocalGeneration,
            CancellationToken.None)).Success);

        Assert.IsTrue(source.AcknowledgeRecoveryCopySaved(
            workspaceId,
            5,
            availability.LocalGeneration));
        Assert.IsTrue(source.GetRecoveryCopyAvailability(workspaceId, 5).ExportConfirmed);
        Assert.IsTrue((await source.CloseExportedRecoveryCopyAsync(
            workspaceId,
            5,
            availability.LocalGeneration,
            CancellationToken.None)).Success);
        Assert.IsFalse(store.GetAvailability(workspaceId, 5).Available);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Recovery_close_callback_is_authoritative_when_boundary_fails_after_local_commit(
        bool throwAfterCommit)
    {
        var client = new FakeChummerClient();
        var workspaceId = new CharacterWorkspaceId("ws-recovery-postcommit-boundary");
        client.SeedWorkspace(
            workspaceId.Value,
            "Recovery Boundary",
            "BOUNDARY",
            contentRevision: 5,
            savedRevision: 4);
        var store = new PostCommitFailingRecoveryPayloadStore(throwAfterCommit);
        var presenter = CreateTrustedPresenter(
            client,
            workspaceRecoveryPayloadStore: store);

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        WorkspaceRecoveryCloseResult result = await presenter.CloseExportedRecoveryCopyAsync(
            workspaceId,
            expectedSourceRevision: 5,
            expectedLocalGeneration: 7,
            CancellationToken.None);

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsTrue(result.PostCommit);
        Assert.AreEqual(1, store.LocalCommitInvocations);
        Assert.IsNull(presenter.State.WorkspaceId);
        Assert.IsNull(presenter.State.Session.FindWorkspace(workspaceId));
        Assert.IsNull(presenter.State.Error);
        StringAssert.Contains(
            presenter.State.Notice ?? string.Empty,
            "recovery-vault cleanup could not be confirmed");

        await presenter.DisposeAsync();
    }

    [TestMethod]
    public async Task Cancelled_blocked_failed_and_stale_recovery_outcomes_retain_vault_and_require_fresh_one_use_tokens()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-recovery-retry", "Recovery Retry", "RETRY", contentRevision: 5, savedRevision: 4);
        client.SeedDocument("ws-recovery-retry", "<character><name>Recovery Retry</name></character>");
        var store = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        CharacterWorkspaceId workspaceId = new("ws-recovery-retry");
        IWorkspaceRecoveryCopySource source = presenter;
        IWorkspaceRecoveryDownloadDispatchSink dispatch = presenter;

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        WorkspaceRecoveryCopyAvailability availability = source.GetRecoveryCopyAvailability(workspaceId, 5);
        Assert.IsTrue(availability.Available);
        string? previousToken = null;
        foreach (string status in new[]
                 {
                     WorkspaceRecoveryBrowserExportOutcome.Cancelled,
                     WorkspaceRecoveryBrowserExportOutcome.Blocked,
                     WorkspaceRecoveryBrowserExportOutcome.Failed,
                     WorkspaceRecoveryBrowserExportOutcome.Stale
                 })
        {
            WorkspaceRecoveryCopyExportResult prepared = await source.PrepareRecoveryCopyAsync(
                workspaceId,
                5,
                availability.LocalGeneration,
                CancellationToken.None);
            Assert.IsTrue(prepared.Success, prepared.Error);
            WorkspaceRecoveryExportRequest request = presenter.State.PendingRecoveryExport!;
            Assert.AreNotEqual(previousToken, request.ExportToken);
            previousToken = request.ExportToken;
            Assert.IsTrue(dispatch.TryAcquireRecoveryCopyExportLease(request, out WorkspaceRecoveryPayloadLease? lease));
            lease!.Dispose();
            Assert.IsFalse(dispatch.CompleteRecoveryCopyExport(
                request,
                new WorkspaceRecoveryBrowserExportOutcome(status)));
            Assert.IsFalse(dispatch.TryAcquireRecoveryCopyExportLease(request, out _));

            WorkspaceRecoveryCopyAvailability retained = source.GetRecoveryCopyAvailability(workspaceId, 5);
            Assert.IsTrue(retained.Available);
            Assert.IsFalse(retained.ExportConfirmed);
            Assert.IsFalse(retained.AwaitingExplicitUserAck);
        }
    }

    [TestMethod]
    public async Task Recovery_store_concurrent_capture_keeps_newest_exact_revision_and_rejects_same_revision_divergence()
    {
        using var store = new WorkspaceRecoveryPayloadStore();
        CharacterWorkspaceId workspaceId = new("ws-concurrent-recovery");
        var start = new ManualResetEventSlim(false);
        WorkspaceDocument revisionOne = new(
            "<character><name>Revision One</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceDocument revisionTwo = new(
            "<character><name>Revision Two</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);

        Task<WorkspaceRecoveryCaptureResult> first = Task.Run(() =>
        {
            start.Wait();
            return CaptureValidated(store, workspaceId, 1, revisionOne, protectFromEviction: true);
        });
        Task<WorkspaceRecoveryCaptureResult> second = Task.Run(() =>
        {
            start.Wait();
            return CaptureValidated(store, workspaceId, 2, revisionTwo, protectFromEviction: true);
        });
        start.Set();
        await Task.WhenAll(first, second);

        WorkspaceRecoveryCopyAvailability availability = store.GetAvailability(workspaceId, 2);
        Assert.IsTrue(availability.Available);
        Assert.IsTrue(store.TryAcquireLease(
            workspaceId,
            2,
            availability.LocalGeneration,
            out WorkspaceRecoveryPayloadLease? payload));
        Assert.IsNotNull(payload);
        using WorkspaceRecoveryPayloadLease verifiedPayload = payload!;
        using var verifiedStream = new MemoryStream();
        verifiedPayload.Stream.CopyTo(verifiedStream);
        byte[] verifiedBytes = verifiedStream.ToArray();
        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes(CanonicalizeRecoveryTestDocument(revisionTwo).Content),
            verifiedBytes);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(verifiedBytes);

        WorkspaceDocument divergentDocument = new(
            "<character><name>Divergent</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceRecoveryCaptureResult divergent = CaptureValidated(
            store,
            workspaceId,
            2,
            divergentDocument,
            protectFromEviction: true);
        Assert.IsFalse(divergent.Success);
        StringAssert.Contains(divergent.Error ?? string.Empty, "Conflicting");
    }

    [TestMethod]
    public async Task Recovery_capture_rejects_well_formed_document_not_accepted_by_canonical_loader()
    {
        var client = new FakeChummerClient { ValidationIsValid = false };
        client.SeedWorkspace("ws-canonical-reject", "Looks Valid", "SYNTAX", contentRevision: 4, savedRevision: 3);
        using var presenter = CreateTrustedPresenter(client);
        var workspaceId = new CharacterWorkspaceId("ws-canonical-reject");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);

        StringAssert.Contains(presenter.State.Error ?? string.Empty, "canonical ruleset loader");
        WorkspaceRecoveryCopyAvailability availability =
            ((IWorkspaceRecoveryCopySource)presenter).GetRecoveryCopyAvailability(workspaceId, 4);
        Assert.IsFalse(availability.Available);
    }

    [TestMethod]
    public void Canonical_validation_capability_has_no_public_mint_surface()
    {
        Type capability = typeof(WorkspaceOverviewLoader.CanonicalValidationCapability);

        Assert.IsTrue(capability.IsNestedAssembly);
        Assert.HasCount(0, capability.GetConstructors());
        Assert.IsFalse(capability.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Any(method => method.ReturnType == capability));
        Assert.HasCount(0, capability.GetMethods(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.DeclaredOnly));
        Type loader = typeof(WorkspaceOverviewLoader);
        Assert.IsTrue(loader.GetConstructors().All(constructor => constructor.GetParameters().Length == 0));
        Assert.IsTrue(loader.GetMethods(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .All(method => method.Name == nameof(WorkspaceOverviewLoader.LoadAsync)));
        Assert.IsFalse(typeof(WorkspaceOverviewLoadResult).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Any(property => property.PropertyType == capability));
        Assert.IsFalse(typeof(WorkspaceOverviewLifecycleResult).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Any(property => property.PropertyType == capability));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new WorkspaceOverviewLoader.CanonicalValidationCapability(
                new object(),
                new CharacterWorkspaceId("forged"),
                1,
                new WorkspaceDocument(
                    "<character><name>Forged</name></character>",
                    RulesetDefaults.Sr5,
                    WorkspaceDocumentFormat.NativeXml)));
    }

    [DataTestMethod]
    [DataRow(RulesetDefaults.Sr4, "schema")]
    [DataRow(RulesetDefaults.Sr4, "ruleset")]
    [DataRow(RulesetDefaults.Sr4, "payload-kind")]
    [DataRow(RulesetDefaults.Sr5, "schema")]
    [DataRow(RulesetDefaults.Sr5, "ruleset")]
    [DataRow(RulesetDefaults.Sr5, "payload-kind")]
    [DataRow(RulesetDefaults.Sr6, "schema")]
    [DataRow(RulesetDefaults.Sr6, "ruleset")]
    [DataRow(RulesetDefaults.Sr6, "payload-kind")]
    public async Task Malicious_always_true_client_cannot_mint_capability_for_noncanonical_envelope(
        string rulesetId,
        string invalidField)
    {
        const string workspaceKey = "ws-invalid-envelope";
        var workspaceId = new CharacterWorkspaceId(workspaceKey);
        WorkspaceDocument baseline = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
            "<character><name>Valid Syntax</name></character>",
            rulesetId,
            WorkspaceDocumentFormat.NativeXml));
        WorkspaceDocument invalid = invalidField switch
        {
            "schema" => baseline with
            {
                State = baseline.State with { SchemaVersion = baseline.SchemaVersion + 1 }
            },
            "ruleset" => baseline with
            {
                State = baseline.State with { RulesetId = "sr999" }
            },
            "payload-kind" => baseline with
            {
                State = baseline.State with { PayloadKind = $"{rulesetId}/not-the-canonical-payload" }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidField))
        };
        var client = new FakeChummerClient { ValidationIsValid = true };
        client.SeedWorkspace(
            workspaceKey,
            "Invalid Envelope",
            "INVALID",
            rulesetId: invalid.RulesetId,
            contentRevision: 7,
            savedRevision: 6);
        client.SeedDocument(workspaceKey, invalid);
        using var store = new WorkspaceRecoveryPayloadStore();
        using var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);

        await presenter.LoadAsync(workspaceId, CancellationToken.None);

        StringAssert.Contains(presenter.State.Error ?? string.Empty, "loader-owned canonical codec authority");
        Assert.AreEqual(0, store.ActiveCaptureIntentCount);
        Assert.IsFalse(store.GetAvailability(workspaceId, 7).Available);
    }

    [TestMethod]
    public void Recovery_store_rejects_malformed_and_oversize_payloads()
    {
        using var store = new WorkspaceRecoveryPayloadStore();
        CharacterWorkspaceId malformedId = new("ws-malformed");
        WorkspaceDocument malformedDocument = new(
            "<!DOCTYPE character [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]><character>&xxe;</character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceDocument differentDocument = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
            "<character><name>Different</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml));
        WorkspaceOverviewLoader.CanonicalValidationCapability mismatchedCapability =
            LoadCanonicalValidation(malformedId, 1, differentDocument);
        Assert.IsTrue(store.TryBeginCaptureIntent(malformedId, 1, out IWorkspaceRecoveryCaptureIntent? malformedIntent));
        using (malformedIntent)
        {
            WorkspaceRecoveryCaptureResult malformed = store.Capture(
                malformedIntent!,
                malformedDocument,
                mismatchedCapability);
            Assert.IsFalse(malformed.Success);
        }

        string oversize = "<character><name>Oversize</name><notes>"
            + new string('x', WorkspaceRecoveryPayloadStore.MaxPayloadBytes)
            + "</notes></character>";
        CharacterWorkspaceId oversizeId = new("ws-oversize");
        WorkspaceDocument oversizeDocument = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
            oversize,
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml));
        WorkspaceRecoveryCaptureResult tooLarge = CaptureValidated(store, oversizeId, 1, oversizeDocument);
        Assert.IsFalse(tooLarge.Success);
        StringAssert.Contains(tooLarge.Error ?? string.Empty, "size");
    }

    [TestMethod]
    public void Recovery_store_bounds_capture_failure_memory_per_presenter_owner()
    {
        using var store = new WorkspaceRecoveryPayloadStore();
        WorkspaceDocument document = new(
            "<character><name>Bounded</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceDocument mismatched = CanonicalizeRecoveryTestDocument(document with
        {
            State = document.State with { Payload = "<character><name>Other</name></character>" }
        });
        var authorityWorkspaceId = new CharacterWorkspaceId("ws-failure-authority");
        WorkspaceOverviewLoader.CanonicalValidationCapability mismatchedCapability =
            LoadCanonicalValidation(authorityWorkspaceId, 1, mismatched);

        for (int index = 0; index < WorkspaceRecoveryPayloadStore.MaxCaptureFailures + 8; index++)
        {
            var workspaceId = new CharacterWorkspaceId($"ws-failure-{index:D3}");
            Assert.IsTrue(store.TryBeginCaptureIntent(workspaceId, 1, out IWorkspaceRecoveryCaptureIntent? intent));
            using (intent)
            {
                WorkspaceRecoveryCaptureResult result = store.Capture(
                    intent!,
                    document,
                    mismatchedCapability);
                Assert.IsFalse(result.Success);
            }
        }

        Assert.AreEqual(
            WorkspaceRecoveryPayloadStore.MaxCaptureFailures,
            store.RetainedCaptureFailureCount);
    }

    [TestMethod]
    public async Task Recovery_close_aborts_before_local_commit_when_newer_capture_has_started()
    {
        using var store = new WorkspaceRecoveryPayloadStore();
        CharacterWorkspaceId workspaceId = new("ws-close-capture-race");
        WorkspaceDocument original = new(
            "<character><name>Original</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceRecoveryCaptureResult first = CaptureValidated(store, workspaceId, 1, original);
        Assert.IsTrue(first.Success, first.Error);
        Assert.IsTrue(store.MarkExported(workspaceId, 1, first.LocalGeneration));

        WorkspaceDocument newer = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
            "<character><name>Newer</name><notes>" + new string('x', 7 * 1024 * 1024) + "</notes></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml));
        WorkspaceOverviewLoader.CanonicalValidationCapability capability =
            LoadCanonicalValidation(workspaceId, 2, newer);
        Assert.IsTrue(store.TryBeginCaptureIntent(workspaceId, 2, out IWorkspaceRecoveryCaptureIntent? captureIntent));
        var releaseCapture = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<WorkspaceRecoveryCaptureResult> capture = Task.Run(async () =>
        {
            using (captureIntent)
            {
                await releaseCapture.Task.ConfigureAwait(false);
                return store.Capture(captureIntent!, newer, capability, protectFromEviction: true);
            }
        });
        Assert.IsTrue(SpinWait.SpinUntil(
            () => store.ActiveCaptureIntentCount > 0,
            TimeSpan.FromSeconds(2)));

        bool localCommitRan = false;
        bool closed = store.TryCommitExplicitClose(
            workspaceId,
            1,
            first.LocalGeneration,
            () => localCommitRan = true);
        releaseCapture.TrySetResult(true);
        WorkspaceRecoveryCaptureResult captured = await capture;

        Assert.IsFalse(closed);
        Assert.IsFalse(localCommitRan);
        Assert.IsTrue(captured.Success, captured.Error);
        Assert.IsTrue(store.GetAvailability(workspaceId, 2).Available);
    }

    [TestMethod]
    public void Recovery_store_capacity_never_evicts_protected_payloads_and_evicts_oldest_clean_payload_deterministically()
    {
        using var store = new WorkspaceRecoveryPayloadStore();
        for (int index = 1; index <= WorkspaceRecoveryPayloadStore.MaxRetainedEntries; index++)
        {
            CharacterWorkspaceId workspaceId = new($"ws-capacity-{index}");
            WorkspaceDocument document = new(
                $"<character><name>Protected {index}</name></character>",
                RulesetDefaults.Sr5,
                WorkspaceDocumentFormat.NativeXml);
            WorkspaceRecoveryCaptureResult captured = CaptureValidated(
                store,
                workspaceId,
                1,
                document,
                protectFromEviction: true);
            Assert.IsTrue(captured.Success, captured.Error);
        }

        CharacterWorkspaceId rejectedId = new("ws-capacity-rejected");
        WorkspaceDocument rejectedDocument = new(
            "<character><name>Rejected</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceRecoveryCaptureResult rejected = CaptureValidated(
            store,
            rejectedId,
            1,
            rejectedDocument);
        Assert.IsFalse(rejected.Success);
        StringAssert.Contains(rejected.Error ?? string.Empty, "protected");
        for (int index = 1; index <= WorkspaceRecoveryPayloadStore.MaxRetainedEntries; index++)
            Assert.IsTrue(store.GetAvailability(new CharacterWorkspaceId($"ws-capacity-{index}"), 1).Available);

        Assert.IsTrue(store.SetProtected(
            new CharacterWorkspaceId("ws-capacity-1"),
            1,
            protectedFromEviction: false));
        CharacterWorkspaceId admittedId = new("ws-capacity-admitted");
        WorkspaceDocument admittedDocument = new(
            "<character><name>Admitted</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceRecoveryCaptureResult admitted = CaptureValidated(
            store,
            admittedId,
            1,
            admittedDocument);
        Assert.IsTrue(admitted.Success, admitted.Error);
        Assert.IsFalse(store.GetAvailability(new CharacterWorkspaceId("ws-capacity-1"), 1).Available);
        Assert.IsTrue(store.GetAvailability(new CharacterWorkspaceId("ws-capacity-admitted"), 1).Available);
    }

    [TestMethod]
    public void Recovery_store_aggregate_byte_pressure_fails_closed_while_all_payloads_are_protected()
    {
        using var store = new WorkspaceRecoveryPayloadStore();
        string sixMiBDocument = "<character><name>Capacity</name><notes>"
            + new string('x', 6 * 1024 * 1024)
            + "</notes></character>";
        WorkspaceDocument document = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
            sixMiBDocument,
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml));
        CharacterWorkspaceId firstId = new("ws-byte-capacity-1");
        CharacterWorkspaceId secondId = new("ws-byte-capacity-2");
        CharacterWorkspaceId thirdId = new("ws-byte-capacity-3");

        Assert.IsTrue(CaptureValidated(store, firstId, 1, document, protectFromEviction: true).Success);
        Assert.IsTrue(CaptureValidated(store, secondId, 1, document, protectFromEviction: true).Success);
        WorkspaceRecoveryCopyAvailability exactFirst = store.GetAvailability(firstId, 1);
        Assert.IsTrue(store.TryAcquireLease(
            firstId,
            1,
            exactFirst.LocalGeneration,
            out WorkspaceRecoveryPayloadLease? exactLease));
        using (exactLease)
        using (var exactReader = new StreamReader(exactLease!.Stream, Encoding.UTF8, leaveOpen: true))
        {
            Assert.AreEqual(document.Content, exactReader.ReadToEnd());
        }

        WorkspaceRecoveryCaptureResult rejected = CaptureValidated(
            store,
            thirdId,
            1,
            document,
            protectFromEviction: true);
        Assert.IsFalse(rejected.Success);
        Assert.IsTrue(store.GetAvailability(firstId, 1).Available);
        Assert.IsTrue(store.GetAvailability(secondId, 1).Available);

        Assert.IsTrue(store.SetProtected(firstId, 1, protectedFromEviction: false));
        WorkspaceRecoveryCaptureResult admitted = CaptureValidated(
            store,
            thirdId,
            1,
            document,
            protectFromEviction: true);
        Assert.IsTrue(admitted.Success, admitted.Error);
        Assert.IsFalse(store.GetAvailability(firstId, 1).Available);
        Assert.IsTrue(store.GetAvailability(secondId, 1).Available);
        Assert.IsTrue(store.GetAvailability(thirdId, 1).Available);
    }

    [TestMethod]
    public void Recovery_store_defensive_copy_and_disposal_fail_closed()
    {
        var store = new WorkspaceRecoveryPayloadStore();
        CharacterWorkspaceId workspaceId = new("ws-disposal");
        WorkspaceDocument document = new(
            "<character><name>Dispose</name></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml);
        WorkspaceRecoveryCaptureResult captured = CaptureValidated(
            store,
            workspaceId,
            1,
            document);
        Assert.IsTrue(captured.Success, captured.Error);
        Assert.IsTrue(store.TryAcquireLease(workspaceId, 1, captured.LocalGeneration, out WorkspaceRecoveryPayloadLease? payload));
        Assert.IsNotNull(payload);

        using var firstStream = new MemoryStream();
        payload!.Stream.CopyTo(firstStream);
        byte[] firstCopy = firstStream.ToArray();
        byte originalFirstByte = firstCopy[0];
        firstCopy[0] = 0;
        Assert.IsTrue(store.TryAcquireLease(workspaceId, 1, captured.LocalGeneration, out WorkspaceRecoveryPayloadLease? secondLease));
        using var secondStream = new MemoryStream();
        secondLease!.Stream.CopyTo(secondStream);
        byte[] secondCopy = secondStream.ToArray();
        Assert.AreEqual(originalFirstByte, secondCopy[0]);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(firstCopy);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(secondCopy);

        payload.Dispose();
        try
        {
            _ = payload.Stream;
            Assert.Fail("Disposed recovery payload exposed its stream.");
        }
        catch (ObjectDisposedException)
        {
            // Expected fail-closed disposal boundary.
        }
        secondLease.Dispose();
        store.Dispose();
        Assert.IsFalse(store.GetAvailability(workspaceId, 1).Available);
    }

    [TestMethod]
    public async Task Committed_metadata_mutation_refreshes_vault_to_exact_postcommit_revision()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-mutation-recovery", "Before", "BEFORE", contentRevision: 5, savedRevision: 4);
        client.SeedDocument("ws-mutation-recovery", "<character><name>Before</name></character>");
        using var store = new WorkspaceRecoveryPayloadStore();
        using var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        var workspaceId = new CharacterWorkspaceId("ws-mutation-recovery");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        await presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("After", "AFTER", null),
            CancellationToken.None);

        Assert.AreEqual(6L, presenter.State.ContentRevision);
        WorkspaceRecoveryCopyAvailability availability = store.GetAvailability(workspaceId, 6);
        Assert.IsTrue(availability.Available, availability.UnavailableReason);
        Assert.IsFalse(store.GetAvailability(workspaceId, 5).Available);
        Assert.IsTrue(store.TryAcquireLease(
            workspaceId,
            6,
            availability.LocalGeneration,
            out WorkspaceRecoveryPayloadLease? lease));
        CommandResult<WorkspaceDocumentSnapshot> canonical = await client.GetWorkspaceAsync(
            workspaceId,
            CancellationToken.None);
        using (lease)
        using (var recovered = new StreamReader(lease!.Stream, Encoding.UTF8, leaveOpen: true))
        {
            Assert.AreEqual(canonical.Value!.Document.Content, await recovered.ReadToEndAsync());
        }
    }

    [TestMethod]
    public async Task Postcommit_view_and_state_observer_failures_do_not_reclassify_metadata_commit()
    {
        var client = new FakeChummerClient();
        var workspaceId = new CharacterWorkspaceId("ws-postcommit-observer-failure");
        client.SeedWorkspace(workspaceId.Value, "Before", "BEFORE", contentRevision: 5, savedRevision: 4);
        client.SeedDocument(workspaceId.Value, "<character><name>Before</name></character>");
        var viewStore = new ThrowingWorkspaceViewStateStore();
        using var presenter = CreateTrustedPresenter(client, workspaceViewStateStore: viewStore);
        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        viewStore.ThrowOnCapture = true;
        int observerFailures = 0;
        presenter.StateChanged += (_, _) =>
        {
            if (presenter.State.ContentRevision == 6)
            {
                Interlocked.Increment(ref observerFailures);
                throw new InvalidOperationException("Simulated postcommit state observer failure.");
            }
        };

        await presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("After", "AFTER", null),
            CancellationToken.None);

        Assert.AreEqual(6L, presenter.State.ContentRevision);
        Assert.AreEqual("After", presenter.State.Profile?.Name);
        Assert.IsNull(presenter.State.Error);
        Assert.IsGreaterThanOrEqualTo(1, Volatile.Read(ref observerFailures));
        StringAssert.Contains(
            presenter.State.Notice ?? string.Empty,
            "local workspace view could not be retained");
        CommandResult<WorkspaceDocumentSnapshot> committed = await client.GetWorkspaceAsync(
            workspaceId,
            CancellationToken.None);
        Assert.IsTrue(committed.Success, committed.Error);
        Assert.AreEqual(6L, committed.Value!.ContentRevision);
    }

    [TestMethod]
    public async Task Committed_xml_mutation_reload_failure_keeps_runner_review_gated_with_exact_recovery()
    {
        const string xml = "<character>"
            + "<name>Before</name><alias>BEFORE</alias>"
            + "<metatype>Human</metatype><buildmethod>Priority</buildmethod>"
            + "<createdversion>1.0</createdversion><appversion>1.0</appversion>"
            + "<karma>0</karma><nuyen>0</nuyen><created>True</created>"
            + "<attributes><attribute><name>BOD</name><base>1</base><karma>0</karma>"
            + "<metatypemin>1</metatypemin><metatypemax>6</metatypemax>"
            + "<metatypeaugmax>9</metatypeaugmax><value>1</value><totalvalue>1</totalvalue>"
            + "</attribute></attributes></character>";
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-xml-postcommit-recovery", "Before", "BEFORE", contentRevision: 5, savedRevision: 4);
        client.SeedDocument("ws-xml-postcommit-recovery", xml);
        using var store = new WorkspaceRecoveryPayloadStore();
        using var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        var workspaceId = new CharacterWorkspaceId("ws-xml-postcommit-recovery");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        client.ThrowProfileWorkspaceId = workspaceId.Value;
        await presenter.ApplyAttributeEditAsync(
            new AttributeEditRequest("BOD", "base", 2),
            CancellationToken.None);

        Assert.AreEqual(workspaceId.Value, presenter.State.WorkspaceId?.Value);
        Assert.AreEqual(6L, presenter.State.ContentRevision);
        Assert.IsNotNull(presenter.State.ConflictState);
        Assert.AreEqual("postcommit XML refresh", presenter.State.ConflictState!.Operation);
        Assert.IsNull(presenter.State.Error);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Exact postcommit recovery is secured");
        Assert.IsTrue(presenter.State.OpenWorkspaces.Any(workspace =>
            string.Equals(workspace.Id.Value, workspaceId.Value, StringComparison.Ordinal)));

        WorkspaceRecoveryCopyAvailability availability = store.GetAvailability(workspaceId, 6);
        Assert.IsTrue(availability.Available, availability.UnavailableReason);
        Assert.IsTrue(store.TryAcquireLease(
            workspaceId,
            6,
            availability.LocalGeneration,
            out WorkspaceRecoveryPayloadLease? lease));
        CommandResult<WorkspaceDocumentSnapshot> committed = await client.GetWorkspaceAsync(
            workspaceId,
            CancellationToken.None);
        Assert.IsTrue(committed.Success, committed.Error);
        using (lease)
        using (var recovered = new StreamReader(lease!.Stream, Encoding.UTF8, leaveOpen: true))
        {
            Assert.AreEqual(committed.Value!.Document.Content, await recovered.ReadToEndAsync());
        }
    }

    [TestMethod]
    public async Task Authoritative_reread_in_flight_capture_intent_aborts_close_and_committed_mutation_wins()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-validation-close-race", "Before", "BEFORE", contentRevision: 5, savedRevision: 4);
        client.SeedDocument("ws-validation-close-race", "<character><name>Before</name></character>");
        using var store = new WorkspaceRecoveryPayloadStore();
        using var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        var workspaceId = new CharacterWorkspaceId("ws-validation-close-race");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        WorkspaceRecoveryCopyAvailability original = store.GetAvailability(workspaceId, 5);
        Assert.IsTrue(original.Available);
        Assert.IsTrue(store.MarkExported(workspaceId, 5, original.LocalGeneration));
        client.BlockRevisionedMetadata = true;

        Task mutation = presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("After", "AFTER", null),
            CancellationToken.None);
        await client.RevisionedMetadataStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        client.BlockNextWorkspaceRead(workspaceId.Value);
        client.ReleaseRevisionedMetadata.TrySetResult(true);
        await client.WorkspaceReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, store.ActiveCaptureIntentCount);

        bool localCommitRan = false;
        bool closed = store.TryCommitExplicitClose(
            workspaceId,
            5,
            original.LocalGeneration,
            () => localCommitRan = true);

        Assert.IsFalse(closed);
        Assert.IsFalse(localCommitRan);
        client.ReleaseWorkspaceRead.TrySetResult(true);
        await mutation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, store.ActiveCaptureIntentCount);
        Assert.IsTrue(store.GetAvailability(workspaceId, 6).Available);
    }

    [TestMethod]
    public async Task Disposing_a_committing_capture_intent_cannot_remove_the_close_barrier()
    {
        using var commitStarted = new ManualResetEventSlim(initialState: false);
        using var releaseCommit = new ManualResetEventSlim(initialState: false);
        int holdCommit = 0;
        using var store = new WorkspaceRecoveryPayloadStore(() =>
        {
            if (Volatile.Read(ref holdCommit) == 0)
                return;

            commitStarted.Set();
            if (!releaseCommit.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Test did not release the committing recovery capture.");
        });
        CharacterWorkspaceId workspaceId = new("ws-committing-intent-close-race");
        WorkspaceDocument original = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
            "<character><name>Before</name><alias>BEFORE</alias></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml));
        WorkspaceRecoveryCaptureResult first = CaptureValidated(store, workspaceId, 5, original);
        Assert.IsTrue(first.Success, first.Error);
        Assert.IsTrue(store.MarkExported(workspaceId, 5, first.LocalGeneration));

        WorkspaceDocument committed = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
            "<character><name>After</name><alias>AFTER</alias></character>",
            RulesetDefaults.Sr5,
            WorkspaceDocumentFormat.NativeXml));
        WorkspaceOverviewLoader.CanonicalValidationCapability capability =
            LoadCanonicalValidation(workspaceId, 6, committed);
        Assert.IsTrue(store.TryBeginCaptureIntent(
            workspaceId,
            6,
            out IWorkspaceRecoveryCaptureIntent? captureIntent));
        Assert.IsNotNull(captureIntent);

        Volatile.Write(ref holdCommit, 1);
        Task<WorkspaceRecoveryCaptureResult> captureTask = Task.Run(() => store.Capture(
            captureIntent!,
            committed,
            capability,
            protectFromEviction: true));
        try
        {
            Assert.IsTrue(commitStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, store.CommittingCaptureIntentCount);

            // Dispose races with a capture that has already crossed its commit
            // linearization point. It must not remove the close barrier.
            captureIntent!.Dispose();
            Assert.AreEqual(1, store.ActiveCaptureIntentCount);
            bool localCommitRan = false;
            Assert.IsFalse(store.TryCommitExplicitClose(
                workspaceId,
                5,
                first.LocalGeneration,
                () => localCommitRan = true));
            Assert.IsFalse(localCommitRan);
        }
        finally
        {
            releaseCommit.Set();
        }

        WorkspaceRecoveryCaptureResult captured = await captureTask;
        Assert.IsTrue(captured.Success, captured.Error);
        Assert.AreEqual(0, store.ActiveCaptureIntentCount);
        Assert.IsFalse(store.GetAvailability(workspaceId, 5).Available);
        WorkspaceRecoveryCopyAvailability availability = store.GetAvailability(workspaceId, 6);
        Assert.IsTrue(availability.Available, availability.UnavailableReason);
        Assert.IsTrue(store.TryAcquireLease(
            workspaceId,
            6,
            availability.LocalGeneration,
            out WorkspaceRecoveryPayloadLease? lease));
        using (lease)
        using (var recovered = new StreamReader(lease!.Stream, Encoding.UTF8, leaveOpen: true))
        {
            Assert.AreEqual(committed.Content, await recovered.ReadToEndAsync());
        }
    }

    [TestMethod]
    public async Task Caller_cancellation_after_commit_cannot_abort_exact_recovery_or_let_a_sibling_close_win()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-validation-cancel", "Before", "BEFORE", contentRevision: 5, savedRevision: 4);
        client.SeedDocument("ws-validation-cancel", "<character><name>Before</name></character>");
        using var store = new WorkspaceRecoveryPayloadStore();
        using var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        var workspaceId = new CharacterWorkspaceId("ws-validation-cancel");
        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        WorkspaceRecoveryCopyAvailability original = store.GetAvailability(workspaceId, 5);
        Assert.IsTrue(original.Available, original.UnavailableReason);
        Assert.IsTrue(store.MarkExported(workspaceId, 5, original.LocalGeneration));
        client.BlockRevisionedMetadata = true;
        using var request = new CancellationTokenSource();

        Task mutation = presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("After", "AFTER", null),
            request.Token);
        await client.RevisionedMetadataStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        client.BlockNextWorkspaceRead(workspaceId.Value);
        client.ReleaseRevisionedMetadata.TrySetResult(true);
        await client.WorkspaceReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, store.ActiveCaptureIntentCount);
        request.Cancel();
        bool siblingCloseCommitted = false;
        Assert.IsFalse(store.TryCommitExplicitClose(
            workspaceId,
            5,
            original.LocalGeneration,
            () => siblingCloseCommitted = true));
        Assert.IsFalse(siblingCloseCommitted);
        client.ReleaseWorkspaceRead.TrySetResult(true);
        await mutation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(0, store.ActiveCaptureIntentCount);
        Assert.AreEqual(6L, presenter.State.ContentRevision);
        Assert.IsTrue(store.GetAvailability(workspaceId, 6).Available);
    }

    [TestMethod]
    public async Task Public_presenter_constructor_with_caller_client_cannot_mint_recovery_authority()
    {
        var attacker = new FakeChummerClient();
        attacker.SeedWorkspace("ws-public-attacker", "Before", "BEFORE", contentRevision: 5, savedRevision: 4);
        attacker.SeedDocument("ws-public-attacker", "<character><name>Before</name></character>");
        using var store = new WorkspaceRecoveryPayloadStore();
        using var presenter = new CharacterOverviewPresenter(
            attacker,
            workspaceRecoveryPayloadStore: store);
        var workspaceId = new CharacterWorkspaceId("ws-public-attacker");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        await presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("After", "AFTER", null),
            CancellationToken.None);

        Assert.AreEqual(6L, presenter.State.ContentRevision);
        Assert.IsFalse(store.GetAvailability(workspaceId, 5).Available);
        Assert.IsFalse(store.GetAvailability(workspaceId, 6).Available);
    }

    [TestMethod]
    public async Task Dirty_workspace_blocks_switch_and_delete_until_save_completes()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-1", "One", "ONE", contentRevision: 1, savedRevision: 1);
        client.SeedWorkspace("ws-2", "Two", "TWO", contentRevision: 1, savedRevision: 1);
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("Edited", "ONE", null),
            CancellationToken.None);
        Assert.IsTrue(presenter.State.IsDirty);

        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Save or discard local changes");

        await presenter.DeleteWorkspaceAsync(new CharacterWorkspaceId("ws-1"), confirmed: true, CancellationToken.None);
        Assert.AreEqual(0, client.CloseWorkspaceCalls);
        Assert.IsTrue(client.ContainsWorkspace("ws-1"));

        await presenter.SaveAsync(CancellationToken.None);
        Assert.IsFalse(presenter.State.IsDirty);
        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);
        Assert.AreEqual("ws-2", presenter.State.WorkspaceId?.Value);
    }

    [TestMethod]
    public async Task Stale_workspace_load_completion_cannot_publish_after_newer_switch()
    {
        var client = new FakeChummerClient
        {
            BlockProfileWorkspaceId = "ws-slow"
        };
        client.SeedWorkspace("ws-slow", "Slow", "SLOW", contentRevision: 1, savedRevision: 1);
        client.SeedWorkspace("ws-fast", "Fast", "FAST", contentRevision: 1, savedRevision: 1);
        var presenter = CreateTrustedPresenter(client);

        Task slowLoad = presenter.LoadAsync(new CharacterWorkspaceId("ws-slow"), CancellationToken.None);
        await client.ProfileLoadStarted.Task;
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-fast"), CancellationToken.None);
        client.ReleaseProfileLoad.TrySetResult(true);
        await slowLoad;

        Assert.AreEqual("ws-fast", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("ws-fast", presenter.State.Session.ActiveWorkspaceId?.Value);
    }

    [TestMethod]
    public async Task Stale_metadata_update_completion_cannot_publish_after_workspace_switch()
    {
        var client = new FakeChummerClient
        {
            BlockRevisionedMetadata = true
        };
        client.SeedWorkspace("ws-update-slow", "Slow", "SLOW", contentRevision: 1, savedRevision: 1);
        client.SeedWorkspace("ws-update-fast", "Fast", "FAST", contentRevision: 1, savedRevision: 1);
        using var store = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        var slowWorkspace = new CharacterWorkspaceId("ws-update-slow");

        await presenter.LoadAsync(slowWorkspace, CancellationToken.None);
        WorkspaceRecoveryCopyAvailability original = store.GetAvailability(slowWorkspace, 1);
        Assert.IsTrue(original.Available);
        Task slowUpdate = presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("Stale Draft", null, null),
            CancellationToken.None);
        await client.RevisionedMetadataStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-update-fast"), CancellationToken.None);
        string? currentNotice = presenter.State.Notice;
        client.BlockNextWorkspaceRead(slowWorkspace.Value);
        client.ReleaseRevisionedMetadata.TrySetResult(true);
        await client.WorkspaceReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        bool siblingCloseCommitted = false;
        Assert.IsFalse(store.TryCommitExplicitClose(
            slowWorkspace,
            1,
            original.LocalGeneration,
            () => siblingCloseCommitted = true));
        Assert.IsFalse(siblingCloseCommitted);
        client.ReleaseWorkspaceRead.TrySetResult(true);
        await slowUpdate.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("ws-update-fast", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("ws-update-fast", presenter.State.Session.ActiveWorkspaceId?.Value);
        Assert.AreNotEqual("Stale Draft", presenter.State.Profile?.Name);
        Assert.AreEqual(currentNotice, presenter.State.Notice);
        WorkspaceRecoveryCopyAvailability captured = store.GetAvailability(slowWorkspace, 2);
        Assert.IsTrue(captured.Available,
            "A receipt-backed stale commit must retain its exact authoritative recovery revision.");
        Assert.IsTrue(store.TryAcquireLease(
            slowWorkspace,
            2,
            captured.LocalGeneration,
            out WorkspaceRecoveryPayloadLease? lease));
        using (lease)
        using (var reader = new StreamReader(lease!.Stream, Encoding.UTF8, leaveOpen: false))
        {
            StringAssert.Contains(await reader.ReadToEndAsync(), "<name>Slow</name>");
        }
    }

    [TestMethod]
    public async Task Stale_save_completion_cannot_publish_after_workspace_switch()
    {
        var client = new FakeChummerClient
        {
            BlockRevisionedSave = true
        };
        client.SeedWorkspace("ws-save-slow", "Slow", "SLOW", contentRevision: 1, savedRevision: 1);
        client.SeedWorkspace("ws-save-fast", "Fast", "FAST", contentRevision: 1, savedRevision: 1);
        using var store = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        var slowWorkspace = new CharacterWorkspaceId("ws-save-slow");

        await presenter.LoadAsync(slowWorkspace, CancellationToken.None);
        WorkspaceRecoveryCopyAvailability original = store.GetAvailability(slowWorkspace, 1);
        Assert.IsTrue(original.Available);
        Assert.IsTrue(store.MarkExported(slowWorkspace, 1, original.LocalGeneration));
        Assert.IsTrue(store.TryCommitExplicitClose(
            slowWorkspace,
            1,
            original.LocalGeneration,
            () => { }));
        Assert.IsFalse(store.GetAvailability(slowWorkspace, 1).Available);
        Task slowSave = presenter.SaveAsync(CancellationToken.None);
        await client.RevisionedSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-save-fast"), CancellationToken.None);
        string? currentNotice = presenter.State.Notice;
        client.ReleaseRevisionedSave.TrySetResult(true);
        await slowSave.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("ws-save-fast", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual("ws-save-fast", presenter.State.Session.ActiveWorkspaceId?.Value);
        Assert.AreEqual(currentNotice, presenter.State.Notice);
        Assert.IsTrue(store.GetAvailability(slowWorkspace, 1).Available,
            "A stale successful save must restore the exact receipt revision even when it cannot publish.");
    }

    [TestMethod]
    public async Task Stale_xml_edit_completion_captures_exact_receipt_without_publishing_after_switch()
    {
        const string xml = "<character><name>Slow</name><alias>SLOW</alias><attributes><attribute><name>REA</name><base>2</base><karma>0</karma><metatypemin>1</metatypemin><metatypemax>6</metatypemax><metatypeaugmax>9</metatypeaugmax><value>2</value><totalvalue>2</totalvalue></attribute></attributes><karma>10</karma><created>False</created></character>";
        var client = new FakeChummerClient
        {
            BlockRevisionedReplace = true
        };
        var slowWorkspace = new CharacterWorkspaceId("ws-edit-slow");
        client.SeedWorkspace(slowWorkspace.Value, "Slow", "SLOW", contentRevision: 1, savedRevision: 1);
        client.SeedDocument(slowWorkspace.Value, xml);
        client.SeedWorkspace("ws-edit-fast", "Fast", "FAST", contentRevision: 1, savedRevision: 1);
        using var store = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);

        await presenter.LoadAsync(slowWorkspace, CancellationToken.None);
        Task staleEdit = presenter.ApplyAttributeEditAsync(
            new AttributeEditRequest("REA", "base", 4),
            CancellationToken.None);
        await client.RevisionedReplaceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-edit-fast"), CancellationToken.None);
        string? currentNotice = presenter.State.Notice;
        client.ReleaseRevisionedReplace.TrySetResult(true);
        await staleEdit.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("ws-edit-fast", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual(currentNotice, presenter.State.Notice);
        WorkspaceRecoveryCopyAvailability captured = store.GetAvailability(slowWorkspace, 2);
        Assert.IsTrue(captured.Available);
        Assert.IsTrue(store.TryAcquireLease(
            slowWorkspace,
            2,
            captured.LocalGeneration,
            out WorkspaceRecoveryPayloadLease? lease));
        using (lease)
        using (var reader = new StreamReader(lease!.Stream, Encoding.UTF8, leaveOpen: false))
        {
            StringAssert.Contains(await reader.ReadToEndAsync(), "<base>4</base>");
        }
    }

    [TestMethod]
    public async Task Failed_exact_capture_for_stale_success_review_gates_source_without_replacing_winning_ui()
    {
        const string xml = "<character><name>Slow</name><alias>SLOW</alias><attributes><attribute><name>REA</name><base>2</base><karma>0</karma><metatypemin>1</metatypemin><metatypemax>6</metatypemax><metatypeaugmax>9</metatypeaugmax><value>2</value><totalvalue>2</totalvalue></attribute></attributes><karma>10</karma><created>False</created></character>";
        var client = new FakeChummerClient
        {
            BlockRevisionedReplace = true
        };
        var slowWorkspace = new CharacterWorkspaceId("ws-edit-stale-capture-failure");
        var fastWorkspace = new CharacterWorkspaceId("ws-edit-stale-capture-winner");
        client.SeedWorkspace(slowWorkspace.Value, "Slow", "SLOW", contentRevision: 1, savedRevision: 1);
        client.SeedDocument(slowWorkspace.Value, xml);
        client.SeedWorkspace(fastWorkspace.Value, "Fast", "FAST", contentRevision: 1, savedRevision: 1);
        using var store = new WorkspaceRecoveryPayloadStore();
        var sessionPresenter = new WorkspaceSessionPresenter(new WorkspaceSessionManager());
        var presenter = CreateTrustedPresenter(
            client,
            workspaceSessionPresenter: sessionPresenter,
            workspaceRecoveryPayloadStore: store);

        await presenter.LoadAsync(slowWorkspace, CancellationToken.None);
        Task staleEdit = presenter.ApplyAttributeEditAsync(
            new AttributeEditRequest("REA", "base", 4),
            CancellationToken.None);
        await client.RevisionedReplaceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        WorkspaceRecoveryCaptureResult conflicting = CaptureValidated(
            store,
            slowWorkspace,
            revision: 2,
            document: new WorkspaceDocument(
                "<character><name>Conflicting capture</name></character>",
                RulesetDefaults.Sr5,
                WorkspaceDocumentFormat.NativeXml),
            protectFromEviction: true);
        Assert.IsTrue(conflicting.Success);

        await presenter.SwitchWorkspaceAsync(fastWorkspace, CancellationToken.None);
        string? winningNotice = presenter.State.Notice;
        client.ReleaseRevisionedReplace.TrySetResult(true);
        await staleEdit.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(fastWorkspace.Value, presenter.State.WorkspaceId?.Value);
        Assert.AreEqual(winningNotice, presenter.State.Notice);
        Assert.IsNull(presenter.State.ConflictState,
            "The stale operation must not overwrite the active winner's UI.");
        OpenWorkspaceState? gatedSource = sessionPresenter.State.FindWorkspace(slowWorkspace);
        Assert.IsNotNull(gatedSource?.ConflictState,
            "A durable commit without an exact recovery capture must remain review-gated in session state.");
        Assert.AreEqual("stale postcommit XML recovery", gatedSource!.ConflictState!.Operation);
        Assert.AreEqual(2L, gatedSource.ConflictState.ActualContentRevision);

        await presenter.DisposeAsync();
    }

    [TestMethod]
    public async Task Synchronous_dispose_from_state_changed_defers_drain_without_deadlock_or_lost_capture()
    {
        var client = new FakeChummerClient();
        var workspaceId = new CharacterWorkspaceId("ws-dispose-reentrant");
        client.SeedWorkspace(workspaceId.Value, "Before", "BEFORE", contentRevision: 1, savedRevision: 1);
        using var store = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        var disposeReturned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int disposeCalls = 0;
        presenter.StateChanged += (_, _) =>
        {
            if (Interlocked.Exchange(ref disposeCalls, 1) != 0)
                return;

            presenter.Dispose();
            disposeReturned.TrySetResult(true);
        };

        Task mutation = Task.Run(() => presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("Committed Before Deferred Drain", null, null),
            CancellationToken.None));

        await disposeReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await mutation.WaitAsync(TimeSpan.FromSeconds(5));
        await presenter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));

        Assert.AreEqual(1, disposeCalls);
        Assert.IsTrue(store.GetAvailability(workspaceId, 2).Available,
            "Deferred self-disposal must not abandon receipt-backed recovery capture.");
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            presenter.LoadAsync(workspaceId, CancellationToken.None));
    }

    [TestMethod]
    public async Task Awaited_async_dispose_from_state_changed_begins_close_without_self_deadlock()
    {
        var client = new FakeChummerClient();
        var workspaceId = new CharacterWorkspaceId("ws-dispose-async-reentrant");
        client.SeedWorkspace(workspaceId.Value, "Before", "BEFORE", contentRevision: 1, savedRevision: 1);
        using var store = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(client, workspaceRecoveryPayloadStore: store);
        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        var reentrantDisposeReturned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int disposeCalls = 0;
        presenter.StateChanged += (_, _) =>
        {
            if (Interlocked.Exchange(ref disposeCalls, 1) != 0)
                return;

            presenter.DisposeAsync().AsTask().GetAwaiter().GetResult();
            reentrantDisposeReturned.TrySetResult(true);
        };

        Task mutation = Task.Run(() => presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("Committed Before Async Deferred Drain", null, null),
            CancellationToken.None));

        await reentrantDisposeReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await mutation.WaitAsync(TimeSpan.FromSeconds(5));
        await presenter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));

        Assert.AreEqual(1, disposeCalls);
        Assert.IsTrue(store.GetAvailability(workspaceId, 2).Available,
            "Reentrant async disposal must not abandon exact postcommit recovery.");
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            presenter.LoadAsync(workspaceId, CancellationToken.None));
    }

    [TestMethod]
    public async Task Async_dispose_from_escaped_nested_context_observes_active_outer_admission()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());
        System.Reflection.MethodInfo enterOperation = typeof(CharacterOverviewPresenter).GetMethod(
            "EnterPresenterOperation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Presenter admission method was not found.");
        System.Reflection.FieldInfo activeOperations = typeof(CharacterOverviewPresenter).GetField(
            "_activePresenterOperations",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Presenter admission counter was not found.");
        var outer = (IDisposable)(enterOperation.Invoke(presenter, [CancellationToken.None])
            ?? throw new AssertFailedException("Outer presenter admission lease was not returned."));
        var inner = (IDisposable)(enterOperation.Invoke(presenter, [CancellationToken.None])
            ?? throw new AssertFailedException("Inner presenter admission lease was not returned."));
        TaskCompletionSource<bool> childCapturedInner =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseChild =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task child = Task.Run(async () =>
        {
            childCapturedInner.TrySetResult(true);
            await releaseChild.Task.ConfigureAwait(false);
            await presenter.DisposeAsync().ConfigureAwait(false);
        });

        bool innerDisposed = false;
        bool outerDisposed = false;
        try
        {
            await childCapturedInner.Task.WaitAsync(TimeSpan.FromSeconds(2));
            inner.Dispose();
            innerDisposed = true;
            releaseChild.TrySetResult(true);

            // The child's AsyncLocal head is now the completed inner scope, but
            // its Previous link still points at the active outer admission.
            // Reentrant disposal must defer instead of waiting on that outer.
            await child.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.AreEqual(1, (int)activeOperations.GetValue(presenter)!);

            outer.Dispose();
            outerDisposed = true;
            await presenter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            releaseChild.TrySetResult(true);
            if (!innerDisposed)
                inner.Dispose();
            if (!outerDisposed)
                outer.Dispose();
            await presenter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    public async Task Close_all_preserves_postcommit_truth_when_activation_and_shell_projection_throw()
    {
        var client = new FakeChummerClient();
        var workspaceId = new CharacterWorkspaceId("ws-close-all-postcommit");
        client.SeedWorkspace(workspaceId.Value, "Close All", "CLOSE", contentRevision: 2, savedRevision: 2);
        using var coordinator = new ThrowAfterClearWorkspaceOperationCoordinator();
        var shellStateFactory = new ThrowingWorkspaceShellStateFactory();
        var presenter = CreateTrustedPresenter(
            client,
            workspaceShellStateFactory: shellStateFactory,
            workspaceOperationCoordinator: coordinator);
        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        coordinator.ThrowOnNextClear = true;
        shellStateFactory.ThrowOnCreate = true;
        int observerCalls = 0;
        presenter.StateChanged += (_, _) =>
        {
            if (Interlocked.Increment(ref observerCalls) > 1)
                throw new InvalidOperationException("Simulated postcommit state observer failure.");
        };

        await presenter.ExecuteCommandAsync("close_all", CancellationToken.None);

        Assert.IsNull(presenter.State.WorkspaceId);
        Assert.IsNull(presenter.State.Session.ActiveWorkspaceId);
        Assert.HasCount(0, presenter.State.OpenWorkspaces);
        Assert.IsNull(presenter.State.Error);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "local close remains committed");
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "shell view will refresh");
        Assert.IsFalse(coordinator.IsCurrent(workspaceId));

        await presenter.DisposeAsync();
    }

    [TestMethod]
    public async Task Reset_state_preserves_postcommit_truth_when_activation_and_shell_projection_throw()
    {
        var client = new FakeChummerClient();
        var workspaceId = new CharacterWorkspaceId("ws-reset-postcommit");
        client.SeedWorkspace(workspaceId.Value, "Reset", "RESET", contentRevision: 2, savedRevision: 2);
        using var coordinator = new ThrowAfterClearWorkspaceOperationCoordinator();
        var shellStateFactory = new ThrowingWorkspaceShellStateFactory();
        var presenter = CreateTrustedPresenter(
            client,
            workspaceShellStateFactory: shellStateFactory,
            workspaceOperationCoordinator: coordinator);
        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        coordinator.ThrowOnNextClear = true;
        shellStateFactory.ThrowOnCreate = true;
        var lifecycle = (WorkspaceOverviewLifecycleCoordinator)typeof(CharacterOverviewPresenter)
            .GetField(
                "_workspaceOverviewLifecycleCoordinator",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(presenter)!;

        WorkspaceOverviewLifecycleResult result = lifecycle.CreateResetState(
            presenter.State,
            "reset-postcommit",
            "Reset complete.");

        Assert.IsTrue(result.PostCommit);
        Assert.IsNull(result.CurrentWorkspaceId);
        Assert.IsNull(result.State.WorkspaceId);
        Assert.IsNull(result.State.Session.ActiveWorkspaceId);
        Assert.IsNull(result.State.Error);
        Assert.AreEqual("reset-postcommit", result.State.LastCommandId);
        StringAssert.Contains(result.State.Notice ?? string.Empty, "local reset remains committed");
        StringAssert.Contains(result.State.Notice ?? string.Empty, "shell view will refresh");
        Assert.IsFalse(coordinator.IsCurrent(workspaceId));

        await presenter.DisposeAsync();
    }

    [TestMethod]
    public async Task DisposeAsync_cancels_and_drains_admitted_roster_watcher_refresh_without_resurrection()
    {
        string rosterPath = Path.Combine(Path.GetTempPath(), $"chummer-roster-race-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rosterPath);
        DesktopPreferenceState retainedPreferences = DesktopPreferenceStateRuntime.Current;
        DesktopPreferenceStateRuntime.SetCurrent(retainedPreferences with
        {
            CharacterRosterPath = rosterPath
        });
        CharacterOverviewPresenter? presenter = null;
        try
        {
            presenter = CreateTrustedPresenter(new FakeChummerClient());
            await presenter.InitializeAsync(CancellationToken.None);
            await presenter.ExecuteCommandAsync(
                "character_roster",
                CancellationToken.None);

            System.Reflection.FieldInfo debounceField = typeof(CharacterOverviewPresenter).GetField(
                "_rosterWatchRefreshDebounce",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Roster watcher debounce field was not found.");
            System.Reflection.FieldInfo activeOperationsField = typeof(CharacterOverviewPresenter).GetField(
                "_activePresenterOperations",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Presenter admission counter was not found.");

            await File.WriteAllTextAsync(Path.Combine(rosterPath, "pending.chum5"), "pending");
            bool observedAdmittedRefresh = false;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                if (debounceField.GetValue(presenter) is not null
                    && (int)activeOperationsField.GetValue(presenter)! > 0)
                {
                    observedAdmittedRefresh = true;
                    break;
                }

                await Task.Delay(5);
            }

            Assert.IsTrue(observedAdmittedRefresh,
                "The real FileSystemWatcher refresh was not admitted to the presenter lifecycle.");
            await presenter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

            System.Reflection.FieldInfo watcherField = typeof(CharacterOverviewPresenter).GetField(
                "_rosterWatchFolderWatcher",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new AssertFailedException("Roster watcher runtime field was not found.");
            Assert.IsNull(watcherField.GetValue(presenter),
                "Presenter disposal must remove the FileSystemWatcher after draining callbacks.");
            Assert.IsNull(debounceField.GetValue(presenter),
                "Presenter disposal must clear the admitted debounce task.");
            Assert.AreEqual(0, (int)activeOperationsField.GetValue(presenter)!,
                "Presenter disposal returned before its watcher callback lease drained.");
            CharacterOverviewState retainedState = presenter.State;
            await File.WriteAllTextAsync(Path.Combine(rosterPath, "late.chum5"), "late");
            await Task.Delay(250);
            Assert.AreSame(retainedState, presenter.State);
        }
        finally
        {
            if (presenter is not null)
                await presenter.DisposeAsync();
            DesktopPreferenceStateRuntime.SetCurrent(retainedPreferences);
            Directory.Delete(rosterPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task Disposed_roster_debounce_before_async_entry_still_releases_presenter_admission()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());
        System.Reflection.MethodInfo enterOperation = typeof(CharacterOverviewPresenter).GetMethod(
            "EnterPresenterOperation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Presenter admission method was not found.");
        System.Reflection.MethodInfo refresh = typeof(CharacterOverviewPresenter).GetMethod(
            "DebouncedRefreshRosterDialogAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Roster debounce method was not found.");
        System.Reflection.FieldInfo activeOperations = typeof(CharacterOverviewPresenter).GetField(
            "_activePresenterOperations",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Presenter admission counter was not found.");
        object operation = enterOperation.Invoke(presenter, [CancellationToken.None])
            ?? throw new AssertFailedException("Presenter admission lease was not returned.");
        var disposedDebounce = new CancellationTokenSource();
        disposedDebounce.Dispose();

        Task refreshTask = (Task)(refresh.Invoke(
            presenter,
            [Path.GetTempPath(), disposedDebounce, operation])
            ?? throw new AssertFailedException("Roster debounce task was not returned."));
        await refreshTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(0, (int)activeOperations.GetValue(presenter)!,
            "A CTS-disposal race must not strand the presenter's lifecycle lease.");
        await presenter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task DisposeAsync_stops_admission_and_drains_a_noncooperative_committing_operation()
    {
        var client = new FakeChummerClient
        {
            BlockRevisionedMetadata = true
        };
        var workspaceId = new CharacterWorkspaceId("ws-dispose-noncooperative");
        client.SeedWorkspace(
            workspaceId.Value,
            "Drain Runner",
            "DRAIN",
            contentRevision: 1,
            savedRevision: 1);
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        Task update = presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("Committed During Drain", null, null),
            CancellationToken.None);
        await client.RevisionedMetadataStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task disposal = presenter.DisposeAsync().AsTask();
        await Task.Yield();
        Assert.IsFalse(disposal.IsCompleted);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            presenter.LoadAsync(workspaceId, CancellationToken.None));

        client.ReleaseRevisionedMetadata.TrySetResult(true);
        await update.WaitAsync(TimeSpan.FromSeconds(5));
        await disposal.WaitAsync(TimeSpan.FromSeconds(3));

        CommandResult<WorkspaceDocumentSnapshot> committed = await client.GetWorkspaceAsync(
            workspaceId,
            CancellationToken.None);
        Assert.IsTrue(committed.Success, committed.Error);
        Assert.AreEqual(2L, committed.Value!.ContentRevision,
            "Presenter teardown cannot reclassify or roll back a non-cooperative durable commit.");
    }

    [TestMethod]
    public async Task DisposeAsync_drains_postcommit_capture_and_preserves_injected_dependencies()
    {
        var client = new FakeChummerClient
        {
            BlockRevisionedMetadata = true
        };
        var workspaceId = new CharacterWorkspaceId("ws-dispose-postcommit-capture");
        client.SeedWorkspace(
            workspaceId.Value,
            "Capture Runner",
            "CAPTURE",
            contentRevision: 1,
            savedRevision: 1);
        using var coordinator = new WorkspaceOperationCoordinator();
        using var store = new WorkspaceRecoveryPayloadStore();
        var presenter = CreateTrustedPresenter(
            client,
            workspaceOperationCoordinator: coordinator,
            workspaceRecoveryPayloadStore: store);

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        Task update = presenter.UpdateMetadataAsync(
            new UpdateWorkspaceMetadata("Captured Before Teardown", null, null),
            CancellationToken.None);
        await client.RevisionedMetadataStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        client.BlockNextWorkspaceRead(workspaceId.Value);
        client.ReleaseRevisionedMetadata.TrySetResult(true);
        await client.WorkspaceReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task[] disposals = Enumerable.Range(0, 8)
            .Select(_ => presenter.DisposeAsync().AsTask())
            .ToArray();
        await Task.Yield();
        Assert.IsTrue(disposals.All(candidate => !candidate.IsCompleted));
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            presenter.GetRecoveryCopyAvailability(workspaceId, expectedSourceRevision: 2));

        client.ReleaseWorkspaceRead.TrySetResult(true);
        await update.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(disposals).WaitAsync(TimeSpan.FromSeconds(3));

        WorkspaceRecoveryCopyAvailability captured = store.GetAvailability(workspaceId, 2);
        Assert.IsTrue(captured.Available,
            "The committed revision must finish exact recovery capture before presenter teardown completes.");
        Assert.IsTrue(store.TryBeginCaptureIntent(
            workspaceId,
            sourceRevision: 3,
            out IWorkspaceRecoveryCaptureIntent? retainedStoreIntent));
        retainedStoreIntent?.Dispose();

        coordinator.SetActiveWorkspace(new CharacterWorkspaceId("retained-coordinator"));
        Assert.IsTrue(coordinator.IsCurrent(new CharacterWorkspaceId("retained-coordinator")));
    }

    [TestMethod]
    public async Task Presenter_disposes_only_its_internally_created_lifecycle_coordinator()
    {
        System.Reflection.FieldInfo lifecycleField = typeof(CharacterOverviewPresenter).GetField(
            "_workspaceOverviewLifecycleCoordinator",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Presenter lifecycle coordinator field was not found.");
        System.Reflection.FieldInfo lifecycleDisposedField = typeof(WorkspaceOverviewLifecycleCoordinator).GetField(
            "_disposeStarted",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Lifecycle disposal state was not found.");

        var ownedPresenter = CreateTrustedPresenter(new FakeChummerClient());
        var ownedLifecycle = (WorkspaceOverviewLifecycleCoordinator)lifecycleField.GetValue(ownedPresenter)!;
        await ownedPresenter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsTrue((bool)lifecycleDisposedField.GetValue(ownedLifecycle)!,
            "An internally-created lifecycle coordinator must be drained and disposed with its presenter.");

        using var externalOperationCoordinator = new WorkspaceOperationCoordinator();
        var injectedLifecycle = new WorkspaceOverviewLifecycleCoordinator(
            client: null!,
            workspaceSessionPresenter: null!,
            workspaceOverviewLoader: null!,
            workspaceViewStateStore: null!,
            workspaceShellStateFactory: null!,
            workspaceRemoteCloseService: null!,
            workspaceSessionActivationService: null!,
            workspaceOverviewStateFactory: null!,
            workspaceOperationCoordinator: externalOperationCoordinator);
        var injectedPresenter = CreateTrustedPresenter(
            new FakeChummerClient(),
            workspaceOverviewLifecycleCoordinator: injectedLifecycle,
            workspaceOperationCoordinator: externalOperationCoordinator);

        await injectedPresenter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsFalse((bool)lifecycleDisposedField.GetValue(injectedLifecycle)!,
            "An injected lifecycle coordinator remains owned by its caller.");
        await injectedLifecycle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
    }

    [TestMethod]
    public void Disposed_presenter_ignores_late_roster_watcher_callbacks()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());
        CharacterOverviewState retainedState = presenter.State;
        presenter.Dispose();

        System.Reflection.MethodInfo queueRefresh = typeof(CharacterOverviewPresenter).GetMethod(
            "QueueRosterWatchRefresh",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Roster watcher queue callback was not found.");
        System.Reflection.MethodInfo applyRefresh = typeof(CharacterOverviewPresenter).GetMethod(
            "RefreshRosterDialogFromWatcher",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Roster watcher refresh callback was not found.");

        queueRefresh.Invoke(presenter, null);
        applyRefresh.Invoke(presenter, [Path.GetTempPath()]);

        Assert.AreSame(retainedState, presenter.State);
    }

    [TestMethod]
    public async Task Failed_save_stays_dirty_and_surfaces_conflict_without_retrying_winner()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-conflict", "Conflict", "CONFLICT", contentRevision: 1, savedRevision: 1);
        var presenter = CreateTrustedPresenter(client);
        CharacterWorkspaceId workspaceId = new("ws-conflict");

        await presenter.LoadAsync(workspaceId, CancellationToken.None);
        await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Draft", null, null), CancellationToken.None);
        client.ForceSaveConflict = true;
        await presenter.SaveAsync(CancellationToken.None);

        Assert.IsTrue(presenter.State.IsDirty);
        Assert.IsNotNull(presenter.State.ConflictState);
        Assert.AreEqual(WorkspaceOperationOutcome.Conflict, presenter.State.ConflictState?.Outcome);
        Assert.AreEqual(1, client.RevisionedSaveCalls);
        Assert.AreEqual(3L, client.GetWorkspaceItem(workspaceId.Value).ContentRevision);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "newer dossier revision won");
    }

    [TestMethod]
    public async Task Attribute_edit_replaces_same_workspace_by_expected_revision()
    {
        const string xml = "<character><name>Runner</name><alias>RUN</alias><attributes><attribute><name>REA</name><base>2</base><karma>0</karma><metatypemin>1</metatypemin><metatypemax>6</metatypemax><metatypeaugmax>9</metatypeaugmax><value>2</value><totalvalue>2</totalvalue></attribute></attributes><karma>10</karma><created>False</created></character>";
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-edit", "Runner", "RUN", contentRevision: 5, savedRevision: 5);
        client.SeedDocument("ws-edit", xml);
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-edit"), CancellationToken.None);
        await presenter.ApplyAttributeEditAsync(new AttributeEditRequest("REA", "base", 4), CancellationToken.None);

        Assert.AreEqual("ws-edit", presenter.State.WorkspaceId?.Value);
        Assert.AreEqual(1, client.ReplaceWorkspaceCalls);
        Assert.AreEqual(0, client.CloseWorkspaceCalls);
        Assert.AreEqual(6L, presenter.State.ContentRevision);
        Assert.AreEqual(5L, presenter.State.SavedRevision);
        Assert.IsTrue(presenter.State.IsDirty);
    }

    [TestMethod]
    public async Task Saved_checkpoint_truth_survives_presenter_restart()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-restart", "Restart", "RESTART", contentRevision: 1, savedRevision: 1);
        CharacterWorkspaceId workspaceId = new("ws-restart");
        var first = CreateTrustedPresenter(client);

        await first.LoadAsync(workspaceId, CancellationToken.None);
        await first.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Restart Draft", null, null), CancellationToken.None);
        await first.SaveAsync(CancellationToken.None);
        long savedRevision = first.State.SavedRevision;

        var restarted = CreateTrustedPresenter(client);
        await restarted.LoadAsync(workspaceId, CancellationToken.None);

        Assert.AreEqual(savedRevision, restarted.State.ContentRevision);
        Assert.AreEqual(savedRevision, restarted.State.SavedRevision);
        Assert.IsFalse(restarted.State.IsDirty);
        Assert.IsNull(restarted.State.ConflictState);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_close_window_switches_to_previous_workspace()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);
        await presenter.ExecuteCommandAsync("close_window", CancellationToken.None);

        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.HasCount(1, presenter.State.OpenWorkspaces);
        Assert.AreEqual("ws-1", presenter.State.OpenWorkspaces[0].Id.Value);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "without deleting it");
    }

    [TestMethod]
    public async Task UpdateMetadataAsync_requires_loaded_workspace()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Name", "Alias", "Notes"), CancellationToken.None);

        Assert.AreEqual("No dossier loaded.", presenter.State.Error);
    }

    [TestMethod]
    public async Task UpdateMetadataAsync_updates_profile_when_client_succeeds()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Updated", "Alias", "Notes"), CancellationToken.None);

        Assert.IsNull(presenter.State.Error);
        Assert.AreEqual("Updated", presenter.State.Profile?.Name);
    }

    [TestMethod]
    public async Task SaveAsync_requires_loaded_workspace()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.SaveAsync(CancellationToken.None);

        Assert.AreEqual("No dossier loaded.", presenter.State.Error);
    }

    [TestMethod]
    public async Task SaveAsync_marks_workspace_as_saved_after_workspace_load()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Updated", "Alias", "Notes"), CancellationToken.None);
        await presenter.SaveAsync(CancellationToken.None);

        Assert.IsNull(presenter.State.Error);
        Assert.IsTrue(presenter.State.HasSavedWorkspace);
    }

    [TestMethod]
    public async Task SaveAsync_syncs_shell_feedback_when_shell_presenter_is_supplied()
    {
        var client = new FakeChummerClient();
        var shellPresenter = new ShellPresenterStub(ShellState.Empty);
        var presenter = CreateTrustedPresenter(client, shellPresenter: shellPresenter);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.SaveAsync(CancellationToken.None);

        Assert.IsNotNull(shellPresenter.LastOverviewFeedback);
        Assert.AreEqual("Dossier saved.", shellPresenter.LastOverviewFeedback.Notice);
        Assert.AreEqual("ws-1", shellPresenter.LastOverviewFeedback.OpenWorkspaces[0].Id.Value);
        Assert.IsTrue(shellPresenter.LastOverviewFeedback.OpenWorkspaces[0].HasSavedWorkspace);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_save_character_marks_workspace_as_saved()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.ExecuteCommandAsync("save_character", CancellationToken.None);

        Assert.AreEqual("save_character", presenter.State.LastCommandId);
        Assert.IsTrue(presenter.State.HasSavedWorkspace);
        Assert.IsNull(presenter.State.Error);
    }

    [TestMethod]
    public async Task Save_character_as_command_prepares_download_without_marking_workspace_saved()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata("Updated", "Alias", "Notes"), CancellationToken.None);
        await presenter.ExecuteCommandAsync("save_character_as", CancellationToken.None);

        Assert.AreEqual("save_character_as", presenter.State.LastCommandId);
        Assert.AreEqual(1, client.DownloadCalls);
        Assert.IsTrue(presenter.State.HasSavedWorkspace);
        Assert.IsTrue(presenter.State.IsDirty);
        Assert.AreEqual(2L, presenter.State.ContentRevision);
        Assert.AreEqual(1L, presenter.State.SavedRevision);
        Assert.IsNull(presenter.State.Error);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Download prepared:");
        Assert.IsNotNull(presenter.State.PendingDownload);
        Assert.AreEqual(1L, presenter.State.PendingDownloadVersion);
        Assert.AreEqual("ws-1.chum5", presenter.State.PendingDownload?.FileName);
    }

    [TestMethod]
    public async Task SaveAsync_clears_pending_download_after_save_as()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.ExecuteCommandAsync("save_character_as", CancellationToken.None);

        Assert.IsNotNull(presenter.State.PendingDownload);

        await presenter.SaveAsync(CancellationToken.None);

        Assert.IsNull(presenter.State.PendingDownload);
        Assert.IsNull(presenter.State.Error);
    }

    [TestMethod]
    public async Task Export_character_dialog_download_prepares_json_bundle()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.ExecuteCommandAsync("export_character", CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveDialog);

        await presenter.ExecuteDialogActionAsync("download", CancellationToken.None);

        Assert.AreEqual(1, client.ExportCalls);
        Assert.IsNull(presenter.State.ActiveDialog);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNull(presenter.State.PendingDownload);
        Assert.IsNotNull(presenter.State.PendingExport);
        Assert.AreEqual(WorkspaceDocumentFormat.Json, presenter.State.PendingExport?.Format);
        StringAssert.EndsWith(presenter.State.PendingExport?.FileName ?? string.Empty, "-export.json");
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Portable export ready:");
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Portable export is ready");
        Assert.IsNotNull(presenter.State.LatestPortabilityActivity);
        Assert.AreEqual("Last portable export", presenter.State.LatestPortabilityActivity?.Title);
        string payload = Encoding.UTF8.GetString(Convert.FromBase64String(presenter.State.PendingExport!.ContentBase64));
        StringAssert.Contains(payload, "\"Summary\"");
        StringAssert.Contains(payload, "\"Profile\"");
        StringAssert.Contains(payload, "\"Progress\"");
        StringAssert.Contains(payload, "\"Reaction\"");
        StringAssert.Contains(payload, "\"Fixer\"");
    }

    [TestMethod]
    public async Task Print_character_command_prepares_html_preview()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.ExecuteCommandAsync("print_character", CancellationToken.None);

        Assert.AreEqual(1, client.PrintCalls);
        Assert.IsNull(presenter.State.ActiveDialog);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNull(presenter.State.PendingDownload);
        Assert.IsNull(presenter.State.PendingExport);
        Assert.IsNotNull(presenter.State.PendingPrint);
        StringAssert.EndsWith(presenter.State.PendingPrint?.FileName ?? string.Empty, "-print.html");
        Assert.AreEqual("text/html", presenter.State.PendingPrint?.MimeType);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Print preview prepared:");
        string payload = Encoding.UTF8.GetString(Convert.FromBase64String(presenter.State.PendingPrint!.ContentBase64));
        StringAssert.Contains(payload, "<html");
        StringAssert.Contains(payload, "Troy Simmons");
    }

    [TestMethod]
    public async Task Save_status_is_tracked_per_workspace_when_switching()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-1", "One", "ONE", contentRevision: 4, savedRevision: 4);
        client.SeedWorkspace("ws-2", "Two", "TWO", contentRevision: 2, savedRevision: 2);
        var presenter = CreateTrustedPresenter(client);

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.SaveAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);

        OpenWorkspaceState ws1AfterSecondLoad = presenter.State.OpenWorkspaces
            .First(workspace => string.Equals(workspace.Id.Value, "ws-1", StringComparison.Ordinal));
        Assert.IsTrue(ws1AfterSecondLoad.HasSavedWorkspace);
        Assert.IsTrue(presenter.State.HasSavedWorkspace);
        Assert.AreEqual(2L, presenter.State.ContentRevision);
        Assert.AreEqual(2L, presenter.State.SavedRevision);

        await presenter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);

        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.IsTrue(presenter.State.HasSavedWorkspace);
        Assert.AreEqual(4L, presenter.State.ContentRevision);
        Assert.AreEqual(4L, presenter.State.SavedRevision);
        OpenWorkspaceState active = presenter.State.OpenWorkspaces
            .First(workspace => string.Equals(workspace.Id.Value, "ws-1", StringComparison.Ordinal));
        Assert.IsTrue(active.HasSavedWorkspace);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_unknown_command_sets_error()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.ExecuteCommandAsync("nope", CancellationToken.None);

        Assert.AreEqual("nope", presenter.State.LastCommandId);
        StringAssert.Contains(presenter.State.Error ?? string.Empty, "not implemented");
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_global_settings_opens_dialog()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.ExecuteCommandAsync("global_settings", CancellationToken.None);

        Assert.AreEqual("global_settings", presenter.State.LastCommandId);
        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.global_settings", presenter.State.ActiveDialog?.Id);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_master_index_opens_dialog_with_catalog_parity_fields()
    {
        var client = new FakeChummerClient();
        client.SeedToolCatalog(
            new MasterIndexResponse(
                Count: 4,
                GeneratedUtc: DateTimeOffset.UtcNow,
                Files: [],
                ReferenceLanePosture: "governed",
                SourcebookCount: 11,
                Sourcebooks:
                [
                    new MasterIndexSourcebookEntry(
                        Id: "core-rulebook",
                        Code: "CRB",
                        Name: "Core Rulebook",
                        Permanent: true,
                        ReferencePosture: "governed",
                        RuleSnippetCount: 12,
                        RuleSnippets: [],
                        ReferenceSourcePosture: "governed",
                        LocalPdfPath: "/books/core-rulebook.pdf"),
                    new MasterIndexSourcebookEntry(
                        Id: "firing-squad",
                        Code: "FS",
                        Name: "Firing Squad",
                        Permanent: false,
                        ReferencePosture: "partial",
                        RuleSnippetCount: 5,
                        RuleSnippets: [],
                        ReferenceSourcePosture: "stale",
                        ReferenceUrl: "https://example.test/firing-squad")
                ],
                ReferenceCoveragePercent: 73,
                SourcebooksWithSnippets: 8,
                SourcebooksWithGovernedReferenceSources: 7,
                SourcebooksWithStaleReferenceSources: 3,
                SourcebooksMissingReferenceSources: 1,
                ReferenceSourceLaneReceipt: "mixed reference-source posture",
                SettingsLanePosture: "governed",
                SourceToggleLanePosture: "governed",
                DistinctSourcebookToggles: 18,
                SourceSelectionLaneReceipt: "source selection governed",
                SourcebookToggleCoveragePercent: 64,
                CustomDataLanePosture: "partial",
                CustomDataAuthoringLaneReceipt: "custom-data authoring partial",
                XmlBridgePosture: "governed",
                XmlBridgeLaneReceipt: "xml bridge governed",
                TranslatorLanePosture: "governed",
                TranslatorLaneReceipt: "translator governed",
                TranslatorBridgePosture: "governed",
                TranslatorLanguageCount: 6,
                EnabledLanguageOverlayCount: 3,
                OnlineStorageLanePosture: "partial",
                OnlineStorageReceiptPosture: "stale",
                OnlineStorageLaneReceipt: "online storage partial",
                OnlineStorageReceiptsCovered: 1,
                OnlineStorageReceiptsExpected: 2,
                OnlineStorageCoveragePercent: 50,
                ImportOracleLanePosture: "partial",
                ImportOracleReceiptPosture: "stale",
                LegacyChummer4FixtureCount: 18,
                LegacyChummer5FixtureCount: 31,
                HeroLabFixtureCount: 0,
                AdjacentSr6OracleReceiptPosture: "partial",
                AdjacentSr6OracleSourcesCovered: 1,
                AdjacentSr6OracleSourcesExpected: 2,
                ImportOracleSourcesCovered: 3,
                ImportOracleSourcesExpected: 4,
                ImportOracleCoveragePercent: 75,
                ImportOracleMissingSources: ["Hero Lab"],
                ImportOracleLaneReceipt: "import oracle partial",
                AdjacentSr6OracleLaneReceipt: "adjacent oracle partial",
                Sr6SupplementLanePosture: "partial",
                Sr6DesignerToolsPosture: "partial",
                Sr6DesignerFamiliesAvailable: 4,
                Sr6DesignerFamiliesExpected: 5,
                HouseRuleLanePosture: "governed",
                HouseRuleOverlayCount: 3,
                Sr6SuccessorLaneReceipt: "sr6 successor partial"));
        var presenter = CreateTrustedPresenter(client);

        await presenter.ExecuteCommandAsync("master_index", CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.master_index", presenter.State.ActiveDialog?.Id);
        Assert.AreEqual("source selection governed", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexSourceSelectionReceipt"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexSettingsSummary"), "reviewed");
        Assert.AreEqual("All", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexFileSelection"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexCurrentSourcebook"), "Core Rulebook");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexImportOracleReceipt"), "import oracle partial");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexOnlineStorageLane"), "partial");
        Assert.AreEqual("1/2 · 50%", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexOnlineStorageCoverage"));
        Assert.AreEqual("partial", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexSr6SupplementLane"));
        Assert.AreEqual("4/5 · partial", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexSr6DesignerCoverage"));
        Assert.AreEqual("governed · 3 overlays", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexHouseRuleLane"));
        Assert.AreEqual("source selection governed", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "masterIndexSourceSelectionReceipt"));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture()
    {
        var client = new FakeChummerClient();
        client.SeedToolCatalog(
            new MasterIndexResponse(
                Count: 1,
                GeneratedUtc: DateTimeOffset.UtcNow,
                Files: [],
                ReferenceLanePosture: "governed",
                SourcebookCount: 1,
                Sourcebooks: [],
                TranslatorLanePosture: "governed",
                TranslatorLaneReceipt: "translator governed",
                TranslatorBridgePosture: "governed",
                TranslatorLanguageCount: 6,
                EnabledLanguageOverlayCount: 3),
            new TranslatorLanguagesResponse(
                Count: 2,
                Languages:
                [
                    new TranslatorLanguageEntry("en-us", "English"),
                    new TranslatorLanguageEntry("de-de", "Deutsch")
                ],
                TranslatorBridgePosture: "partial",
                EnabledLanguageOverlayCount: 1));
        var presenter = CreateTrustedPresenter(client);

        await presenter.ExecuteCommandAsync("translator", CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.translator", presenter.State.ActiveDialog?.Id);
        Assert.AreEqual("reviewed", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "translatorLanePosture"));
        Assert.AreEqual("reviewed", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "translatorBridgePosture"));
        Assert.AreEqual("3", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "translatorOverlayCount"));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture()
    {
        var client = new FakeChummerClient();
        client.SeedToolCatalog(
            new MasterIndexResponse(
                Count: 1,
                GeneratedUtc: DateTimeOffset.UtcNow,
                Files: [],
                ReferenceLanePosture: "governed",
                SourcebookCount: 1,
                Sourcebooks: [],
                CustomDataLanePosture: "partial",
                CustomDataAuthoringLaneReceipt: "custom-data authoring partial",
                XmlBridgePosture: "governed",
                XmlBridgeLaneReceipt: "xml bridge governed"));
        var presenter = CreateTrustedPresenter(client);

        await presenter.ExecuteCommandAsync("xml_editor", CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.xml_editor", presenter.State.ActiveDialog?.Id);
        Assert.AreEqual("partial", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "xmlEditorCustomDataLanePosture"));
        Assert.AreEqual("governed", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "xmlEditorXmlBridgePosture"));
        Assert.AreEqual("custom-data authoring partial", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "xmlEditorCustomDataAuthoringReceipt"));
        Assert.AreEqual("xml bridge governed", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "xmlEditorXmlBridgeReceipt"));
    }

    [TestMethod]
    // Veteran proof anchor: ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_compatibility_posture
    public async Task ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture()
    {
        var client = new FakeChummerClient();
        client.SeedToolCatalog(
            new MasterIndexResponse(
                Count: 1,
                GeneratedUtc: DateTimeOffset.UtcNow,
                Files: [],
                ReferenceLanePosture: "governed",
                SourcebookCount: 1,
                Sourcebooks: [],
                ImportOracleLanePosture: "partial",
                ImportOracleReceiptPosture: "stale",
                LegacyChummer4FixtureCount: 18,
                LegacyChummer5FixtureCount: 31,
                HeroLabFixtureCount: 0,
                AdjacentSr6OracleReceiptPosture: "partial",
                AdjacentSr6OracleSourcesCovered: 1,
                AdjacentSr6OracleSourcesExpected: 2,
                ImportOracleSourcesCovered: 3,
                ImportOracleSourcesExpected: 4,
                ImportOracleCoveragePercent: 75,
                ImportOracleMissingSources: ["Hero Lab"],
                ImportOracleLaneReceipt: "import oracle partial",
                AdjacentSr6OracleLaneReceipt: "adjacent oracle partial"));
        var presenter = CreateTrustedPresenter(client);

        await presenter.ExecuteCommandAsync("hero_lab_importer", CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.hero_lab_importer", presenter.State.ActiveDialog?.Id);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "heroLabImportOracleLanePosture"), "partial");
        Assert.AreEqual("import oracle partial", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "heroLabImportOracleReceipt"));
        Assert.AreEqual("adjacent oracle partial", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "heroLabAdjacentSr6OracleReceipt"));
        Assert.AreEqual("Hero Lab", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "heroLabImportOracleMissingSources"));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_switch_ruleset_opens_dialog()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.ExecuteCommandAsync("switch_ruleset", CancellationToken.None);

        Assert.AreEqual("switch_ruleset", presenter.State.LastCommandId);
        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.switch_ruleset", presenter.State.ActiveDialog?.Id);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_open_character_opens_import_dialog()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.ExecuteCommandAsync("open_character", CancellationToken.None);

        Assert.AreEqual("open_character", presenter.State.LastCommandId);
        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.open_character", presenter.State.ActiveDialog?.Id);
    }

    [TestMethod]
    public async Task ExecuteDialogActionAsync_import_imports_workspace_from_open_character_dialog()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.ExecuteCommandAsync("open_character", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("importRulesetId", " SR6 ", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("openCharacterXml", "<character><name>Dialog Import</name></character>", CancellationToken.None);
        await presenter.ExecuteDialogActionAsync("import", CancellationToken.None);

        Assert.IsNotNull(client.LastImportedDocument);
        StringAssert.Contains(client.LastImportedDocument!.Content, "Dialog Import");
        Assert.AreEqual("sr6", client.LastImportedDocument.RulesetId);
        Assert.AreEqual("ws-1", presenter.State.WorkspaceId?.Value);
        Assert.IsNull(presenter.State.ActiveDialog);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Portable import ready:");

        await presenter.ExecuteCommandAsync("open_character", CancellationToken.None);
        Assert.AreEqual("sr6", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "importRulesetId"));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_open_character_prefills_import_ruleset_from_active_workspace()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-sr6", "Ruleset Six", "RS6", rulesetId: "sr6");
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-sr6"), CancellationToken.None);
        await presenter.ExecuteCommandAsync("open_character", CancellationToken.None);

        Assert.AreEqual("sr6", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "importRulesetId"));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_open_character_prefills_import_ruleset_from_initialized_shell_contract_when_no_workspace_is_active()
    {
        var client = new FakeChummerClient();
        await client.SaveShellPreferencesAsync(new ShellPreferences("sr6"), CancellationToken.None);
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.ExecuteCommandAsync("open_character", CancellationToken.None);

        Assert.AreEqual("sr6", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "importRulesetId"));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_switch_ruleset_prefills_ruleset_from_active_workspace()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-sr6", "Ruleset Six", "RS6", rulesetId: "sr6");
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-sr6"), CancellationToken.None);
        await presenter.ExecuteCommandAsync("switch_ruleset", CancellationToken.None);

        Assert.AreEqual("sr6", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "preferredRulesetId"));
    }

    [TestMethod]
    public async Task ExecuteDialogActionAsync_apply_ruleset_calls_shell_presenter_and_closes_dialog()
    {
        var client = new FakeChummerClient();
        var shellPresenter = new ShellPresenterStub(ShellState.Empty with
        {
            Commands = AppCommandCatalog.All,
            NavigationTabs = NavigationTabCatalog.All
        });
        var presenter = CreateTrustedPresenter(
            client,
            shellPresenter: shellPresenter);

        await presenter.ExecuteCommandAsync("switch_ruleset", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("preferredRulesetId", " SR6 ", CancellationToken.None);
        await presenter.ExecuteDialogActionAsync("apply_ruleset", CancellationToken.None);

        Assert.AreEqual("sr6", shellPresenter.LastPreferredRulesetId);
        Assert.IsNull(presenter.State.ActiveDialog);
        Assert.AreEqual("Preferred ruleset set to 'sr6'.", presenter.State.Notice);
    }

    [TestMethod]
    public async Task HandleUiControlAsync_create_entry_opens_dialog()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.HandleUiControlAsync("create_entry", CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.ui.create_entry", presenter.State.ActiveDialog?.Id);
    }

    [TestMethod]
    public async Task HandleUiControlAsync_all_catalog_controls_are_non_generic()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        foreach (string controlId in LegacyUiControlIds)
        {
            await presenter.HandleUiControlAsync(controlId, CancellationToken.None);
            Assert.AreNotEqual("dialog.ui.generic", presenter.State.ActiveDialog?.Id, $"Control '{controlId}' fell back to generic dialog.");
        }
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_all_catalog_commands_are_handled()
    {
        AppCommandDefinition[] commands = AppCommandCatalog.All
            .Where(command => !string.Equals(command.Group, "menu", StringComparison.Ordinal))
            .ToArray();

        foreach (AppCommandDefinition command in commands)
        {
            var presenter = CreateTrustedPresenter(new FakeChummerClient());
            await presenter.InitializeAsync(CancellationToken.None);
            if (command.RequiresOpenCharacter)
            {
                await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
            }

            await presenter.ExecuteCommandAsync(command.Id, CancellationToken.None);

            string error = presenter.State.Error ?? string.Empty;
            Assert.IsFalse(
                error.Contains("not implemented", StringComparison.OrdinalIgnoreCase),
                $"Command '{command.Id}' fell through to not-implemented: {error}");
        }
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_dialog_commands_use_non_generic_dialog_templates()
    {
        string[] dialogCommands =
        [
            OverviewCommandPolicy.RuntimeInspectorCommandId,
            "new_window",
            "wiki",
            "discord",
            "show_login_video",
            "revision_history",
            "dumpshock",
            "print_setup",
            "print_multiple",
            "dice_roller",
            "global_settings",
            "switch_ruleset",
            "character_settings",
            "translator",
            "xml_editor",
            "master_index",
            "character_roster",
            "data_exporter",
            "export_character",
            "report_bug",
            "about",
            "hero_lab_importer",
            "update"
        ];

        foreach (string commandId in dialogCommands)
        {
            var presenter = CreateTrustedPresenter(new FakeChummerClient());
            await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
            await presenter.ExecuteCommandAsync(commandId, CancellationToken.None);

            Assert.IsNotNull(presenter.State.ActiveDialog, $"Command '{commandId}' did not open a dialog.");
            Assert.AreNotEqual("dialog.generic", presenter.State.ActiveDialog?.Id, $"Command '{commandId}' fell back to generic dialog template.");
        }
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_runtime_inspector_uses_runtime_projection_dialog()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.ExecuteCommandAsync(OverviewCommandPolicy.RuntimeInspectorCommandId, CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.AreEqual("dialog.runtime_inspector", presenter.State.ActiveDialog?.Id);
        Assert.AreEqual("official.sr5.core", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "runtimeProfileId"));
        Assert.AreEqual("sha256:sr5-runtime-fingerprint", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "runtimeFingerprint"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "runtimeRulePacks"), "official.sr5.core");
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_runtime_inspector_errors_when_no_active_runtime_exists()
    {
        var client = new FakeChummerClient
        {
            DisableActiveRuntime = true
        };
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.ExecuteCommandAsync(OverviewCommandPolicy.RuntimeInspectorCommandId, CancellationToken.None);

        Assert.IsNull(presenter.State.ActiveDialog);
        Assert.AreEqual("No active runtime profile is available for inspection.", presenter.State.Error);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_dice_roller_opens_utility_lane_with_roster_context()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-legacy-1", "Legacy One", "L1", DateTimeOffset.UtcNow.AddMinutes(-10), RulesetDefaults.Sr5);
        client.SeedWorkspace("ws-legacy-2", "Legacy Two", "L2", DateTimeOffset.UtcNow.AddMinutes(-1), RulesetDefaults.Sr6);
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.ExecuteCommandAsync("dice_roller", CancellationToken.None);

        Assert.AreEqual("dialog.dice_roller", presenter.State.ActiveDialog?.Id);
        Assert.AreEqual("Dice roller + initiative preview + roster context", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "diceUtilityLane"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "diceRosterContext"), "Open Runners | 2");
        string initiativePreview = DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "initiativePreview");
        StringAssert.Contains(initiativePreview, "L2 · Legacy Two [sr6]");
        StringAssert.Contains(initiativePreview, "Initiative preview uses the active roster runner");
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_character_roster_opens_dialog_with_workspace_summary()
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace("ws-legacy-1", "Legacy One", "L1", DateTimeOffset.UtcNow.AddMinutes(-10), RulesetDefaults.Sr5);
        client.SeedWorkspace("ws-legacy-2", "Legacy Two", "L2", DateTimeOffset.UtcNow.AddMinutes(-1), RulesetDefaults.Sr6);
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.ExecuteCommandAsync("character_roster", CancellationToken.None);

        Assert.AreEqual("dialog.character_roster", presenter.State.ActiveDialog?.Id);
        Assert.AreEqual("2", DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "rosterOpenCount"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "rosterRulesetMix"), "sr5");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(presenter.State.ActiveDialog!, "rosterRulesetMix"), "sr6");
    }

    [TestMethod]
    public async Task ExecuteWorkspaceActionAsync_summary_sets_active_summary_payload()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        WorkspaceSurfaceActionDefinition action = WorkspaceSurfaceActionCatalog.All
            .First(item => string.Equals(item.Id, "tab-info.summary", StringComparison.Ordinal));

        await presenter.ExecuteWorkspaceActionAsync(action, CancellationToken.None);

        Assert.AreEqual("summary", presenter.State.ActiveSectionId);
        Assert.AreEqual("tab-info.summary", presenter.State.ActiveActionId);
        StringAssert.Contains(presenter.State.ActiveSectionJson ?? string.Empty, "\"Name\": \"Troy Simmons\"");
        Assert.IsGreaterThan(0, presenter.State.ActiveSectionRows.Count);
    }

    [TestMethod]
    public async Task ExecuteWorkspaceActionAsync_metadata_applies_profile_updates_from_dialog()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);
        WorkspaceSurfaceActionDefinition action = WorkspaceSurfaceActionCatalog.All
            .First(item => string.Equals(item.Id, "tab-info.metadata", StringComparison.Ordinal));

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("metadataName", "Dialog Updated", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("metadataAlias", "Dialog Alias", CancellationToken.None);
        await presenter.ExecuteDialogActionAsync("apply_metadata", CancellationToken.None);

        Assert.IsNull(presenter.State.ActiveDialog);
        Assert.AreEqual("Dialog Updated", presenter.State.Profile?.Name);
        Assert.AreEqual("Dialog Alias", presenter.State.Profile?.Alias);
    }

    [TestMethod]
    public async Task ExecuteWorkspaceActionAsync_metadata_blank_notes_are_treated_as_no_change()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);
        WorkspaceSurfaceActionDefinition action = WorkspaceSurfaceActionCatalog.All
            .First(item => string.Equals(item.Id, "tab-info.metadata", StringComparison.Ordinal));

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata(null, null, "Existing Notes"), CancellationToken.None);
        await presenter.ExecuteWorkspaceActionAsync(action, CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("metadataNotes", string.Empty, CancellationToken.None);
        await presenter.ExecuteDialogActionAsync("apply_metadata", CancellationToken.None);

        Assert.IsNotNull(client.LastUpdateMetadata);
        Assert.IsNull(client.LastUpdateMetadata!.Notes);
        Assert.AreEqual("Existing Notes", presenter.State.Preferences.CharacterNotes);
    }

    [TestMethod]
    public async Task UpdateMetadataAsync_updates_preference_notes_when_notes_are_provided()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.UpdateMetadataAsync(new UpdateWorkspaceMetadata(null, null, "Desk Notes"), CancellationToken.None);

        Assert.AreEqual("Desk Notes", presenter.State.Preferences.CharacterNotes);
    }

    [TestMethod]
    public async Task ExecuteDialogActionAsync_roll_updates_dice_dialog_result_field()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.ExecuteCommandAsync("dice_roller", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("diceExpression", "3d6+2", CancellationToken.None);
        await presenter.ExecuteDialogActionAsync("roll", CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveDialog);
        Assert.IsNotNull(presenter.State.ActiveDialog?.Fields.FirstOrDefault(field => string.Equals(field.Id, "diceResultsSummary", StringComparison.Ordinal)));
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Sum");
    }

    [TestMethod]
    public async Task ExecuteDialogActionAsync_save_global_settings_updates_preferences()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.ExecuteCommandAsync("global_settings", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("globalUiScale", "125", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("globalTheme", "dark-steel", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("globalLanguage", "de-de", CancellationToken.None);
        await presenter.UpdateDialogFieldAsync("globalCompactMode", "true", CancellationToken.None);
        await presenter.ExecuteDialogActionAsync("save", CancellationToken.None);

        Assert.AreEqual(125, presenter.State.Preferences.UiScalePercent);
        Assert.AreEqual("dark-steel", presenter.State.Preferences.Theme);
        Assert.AreEqual("de-de", presenter.State.Preferences.Language);
        Assert.IsTrue(presenter.State.Preferences.CompactMode);
        Assert.IsNull(presenter.State.ActiveDialog);
    }

    [TestMethod]
    public async Task SelectTabAsync_requires_loaded_workspace()
    {
        var presenter = CreateTrustedPresenter(new FakeChummerClient());

        await presenter.SelectTabAsync("tab-info", CancellationToken.None);

        Assert.AreEqual("No dossier loaded.", presenter.State.Error);
    }

    [TestMethod]
    public async Task SelectTabAsync_loads_active_section_preview_after_workspace_load()
    {
        var client = new FakeChummerClient();
        var presenter = CreateTrustedPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);
        await presenter.LoadAsync(new CharacterWorkspaceId("ws-1"), CancellationToken.None);
        await presenter.SelectTabAsync("tab-info", CancellationToken.None);

        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.AreEqual("profile", presenter.State.ActiveSectionId);
        StringAssert.Contains(presenter.State.ActiveSectionJson ?? string.Empty, "\"sectionId\": \"profile\"");
        Assert.IsGreaterThan(0, presenter.State.ActiveSectionRows.Count);
    }

    private static WorkspaceRecoveryCaptureResult CaptureValidated(
        WorkspaceRecoveryPayloadStore store,
        CharacterWorkspaceId workspaceId,
        long revision,
        WorkspaceDocument document,
        bool protectFromEviction = false)
    {
        document = CanonicalizeRecoveryTestDocument(document);
        WorkspaceOverviewLoader.CanonicalValidationCapability capability =
            LoadCanonicalValidation(workspaceId, revision, document);
        Assert.IsTrue(store.TryBeginCaptureIntent(workspaceId, revision, out IWorkspaceRecoveryCaptureIntent? intent));
        using (intent)
        {
            return store.Capture(
                intent!,
                document,
                capability,
                protectFromEviction);
        }
    }

    private static WorkspaceDocument CanonicalizeRecoveryTestDocument(WorkspaceDocument document)
    {
        if (document.Format != WorkspaceDocumentFormat.NativeXml
            || !document.Content.Contains("</character>", StringComparison.OrdinalIgnoreCase))
        {
            return document;
        }

        string required = "<metatype>Human</metatype><buildmethod>Priority</buildmethod>"
            + "<createdversion>1.0</createdversion><appversion>1.0</appversion>"
            + "<karma>0</karma><nuyen>0</nuyen><created>True</created>";
        string payload = document.Content.Contains("<metatype>", StringComparison.OrdinalIgnoreCase)
            ? document.Content
            : document.Content.Replace("</character>", required + "</character>", StringComparison.OrdinalIgnoreCase);
        string payloadKind = document.RulesetId switch
        {
            RulesetDefaults.Sr4 => "sr4/chum4-xml",
            RulesetDefaults.Sr6 => "sr6/chum6-xml",
            _ => "sr5/chum5-xml"
        };
        return document with
        {
            State = document.State with
            {
                SchemaVersion = 1,
                PayloadKind = payloadKind,
                Payload = payload
            }
        };
    }

    private static WorkspaceOverviewLoader.CanonicalValidationCapability LoadCanonicalValidation(
        CharacterWorkspaceId workspaceId,
        long revision,
        WorkspaceDocument document)
    {
        var client = new FakeChummerClient();
        client.SeedWorkspace(
            workspaceId.Value,
            "Recovery fixture",
            "RECOVERY",
            rulesetId: document.RulesetId,
            contentRevision: revision,
            savedRevision: revision);
        client.SeedDocument(workspaceId.Value, document);
        WorkspaceOverviewLoader loader = WorkspaceOverviewLoader.CreateCompositionBound(client);
        WorkspaceRecoveryAuthoritySnapshot verified = ((IAuthoritativeWorkspaceOverviewLoader)loader)
            .LoadRecoverySnapshotAsync(workspaceId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.AreEqual(revision, verified.ContentRevision);
        Assert.AreEqual(document.Content, verified.Document.Content);
        return verified.Validation;
    }

    private sealed class ThrowingWorkspaceViewStateStore : IWorkspaceViewStateStore
    {
        public bool ThrowOnCapture { get; set; }

        public void Capture(CharacterWorkspaceId workspaceId, CharacterOverviewState state)
        {
            if (ThrowOnCapture)
                throw new InvalidOperationException("Simulated postcommit workspace view capture failure.");
        }

        public WorkspaceViewState? Restore(CharacterWorkspaceId workspaceId) => null;

        public void Remove(CharacterWorkspaceId workspaceId)
        {
        }

        public void Clear()
        {
        }
    }

    private sealed class PostCommitFailingRecoveryPayloadStore : IWorkspaceRecoveryPayloadStore
    {
        private readonly bool _throwAfterCommit;

        public PostCommitFailingRecoveryPayloadStore(bool throwAfterCommit)
        {
            _throwAfterCommit = throwAfterCommit;
        }

        public int LocalCommitInvocations { get; private set; }

        public bool TryBeginCaptureIntent(
            CharacterWorkspaceId workspaceId,
            long sourceRevision,
            out IWorkspaceRecoveryCaptureIntent? captureIntent)
        {
            captureIntent = null;
            return false;
        }

        public bool SetProtected(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision,
            bool protectedFromEviction)
            => false;

        public WorkspaceRecoveryCopyAvailability GetAvailability(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision)
            => WorkspaceRecoveryCopyAvailability.Unavailable(
                expectedSourceRevision,
                "The malicious boundary fixture has no payload.");

        public bool TryAcquireLease(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision,
            long expectedLocalGeneration,
            out WorkspaceRecoveryPayloadLease? lease)
        {
            lease = null;
            return false;
        }

        public bool MarkExported(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision,
            long expectedLocalGeneration)
            => false;

        public bool CanCloseAfterExport(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision,
            long expectedLocalGeneration)
            => true;

        public bool TryCommitExplicitClose(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision,
            long expectedLocalGeneration,
            Action localCommit)
        {
            LocalCommitInvocations++;
            localCommit();
            if (_throwAfterCommit)
                throw new InvalidOperationException("Simulated recovery boundary failure after local commit.");

            return false;
        }

        public void Dispose()
        {
        }
    }

    private sealed class ThrowAfterClearWorkspaceOperationCoordinator :
        IWorkspaceOperationCoordinator,
        IDisposable
    {
        private readonly WorkspaceOperationCoordinator _inner = new();

        public bool ThrowOnNextClear { get; set; }

        public Task<WorkspaceOperationExecution<T>> RunActivationAsync<T>(
            CharacterWorkspaceId workspaceId,
            Func<CancellationToken, Task<T>> operation,
            CancellationToken ct)
            => _inner.RunActivationAsync(workspaceId, operation, ct);

        public Task<WorkspaceOperationExecution<T>> RunCurrentAsync<T>(
            CharacterWorkspaceId workspaceId,
            Func<CancellationToken, Task<T>> operation,
            CancellationToken ct)
            => _inner.RunCurrentAsync(workspaceId, operation, ct);

        public void SetActiveWorkspace(CharacterWorkspaceId? workspaceId)
        {
            _inner.SetActiveWorkspace(workspaceId);
            if (workspaceId is null && ThrowOnNextClear)
            {
                ThrowOnNextClear = false;
                throw new InvalidOperationException("Simulated activation observer failure after clear.");
            }
        }

        public void Invalidate(CharacterWorkspaceId workspaceId)
            => _inner.Invalidate(workspaceId);

        public bool IsCurrent(CharacterWorkspaceId workspaceId)
            => _inner.IsCurrent(workspaceId);

        public void Dispose() => _inner.Dispose();
    }

    private sealed class ThrowingWorkspaceShellStateFactory : IWorkspaceShellStateFactory
    {
        private readonly WorkspaceShellStateFactory _inner = new();

        public bool ThrowOnCreate { get; set; }

        public CharacterOverviewState CreateEmptyShellState(
            CharacterOverviewState currentState,
            WorkspaceSessionState session,
            string notice,
            string? lastCommandId = null)
        {
            if (ThrowOnCreate)
                throw new InvalidOperationException("Simulated postcommit shell projection failure.");

            return _inner.CreateEmptyShellState(currentState, session, notice, lastCommandId);
        }
    }

    private sealed class FakeChummerClient : IChummerClient
    {
        private string _name = "Troy Simmons";
        private string _alias = "BLUE";
        private readonly Dictionary<string, WorkspaceListItem> _workspaces = new(StringComparer.Ordinal);
        private readonly Dictionary<string, WorkspaceDocument> _documents = new(StringComparer.Ordinal);
        private readonly Dictionary<(string ProfileId, string RulesetId), RuntimeInspectorProjection> _runtimeInspectors = new();
        private int _clock;
        private ShellPreferences _preferences = new(RulesetDefaults.Sr5);
        private ShellSessionState _session = ShellSessionState.Default;
        private MasterIndexResponse _masterIndex = new(0, DateTimeOffset.UtcNow, [], "missing", 0, []);
        private TranslatorLanguagesResponse _translatorLanguages = new(0, []);
        private int _blockNextWorkspaceRead;
        public bool DisableActiveRuntime { get; set; }
        public bool ThrowOnCloseWorkspace { get; set; }
        public bool ReturnNullDeleteReceipt { get; set; }
        public Action? DeleteReceiptCommitted { get; set; }
        public string? ThrowGetWorkspaceId { get; set; }
        public string? ThrowProfileWorkspaceId { get; set; }
        public bool ForceSaveConflict { get; set; }
        public bool BlockRevisionedSave { get; set; }
        public bool BlockRevisionedMetadata { get; set; }
        public bool BlockRevisionedReplace { get; set; }
        public bool ValidationIsValid { get; set; } = true;
        public string? BlockValidationWorkspaceId { get; set; }
        public string? BlockProfileWorkspaceId { get; set; }
        public TaskCompletionSource<bool> ProfileLoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseProfileLoad { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> RevisionedSaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseRevisionedSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ValidationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseValidation { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> WorkspaceReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseWorkspaceRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> RevisionedMetadataStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseRevisionedMetadata { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> RevisionedReplaceStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseRevisionedReplace { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DownloadCalls { get; private set; }
        public int GetCommandsCalls { get; private set; }
        public int GetNavigationTabsCalls { get; private set; }
        public int ListWorkspacesCalls { get; private set; }
        public int GetProfileCalls { get; private set; }
        public int GetWorkspaceCalls { get; private set; }
        public int ExportCalls { get; private set; }
        public int PrintCalls { get; private set; }
        public int CloseWorkspaceCalls { get; private set; }
        public int RevisionedSaveCalls { get; private set; }
        public int ReplaceWorkspaceCalls { get; private set; }
        public UpdateWorkspaceMetadata? LastUpdateMetadata { get; private set; }
        public WorkspaceImportDocument? LastImportedDocument { get; private set; }
        public static IReadOnlyList<AppCommandDefinition> Commands { get; } = CreateCommands(RulesetDefaults.Sr5);
        public static IReadOnlyList<NavigationTabDefinition> Tabs { get; } = CreateTabs(RulesetDefaults.Sr5);

        public FakeChummerClient()
        {
            SeedRuntimeInspector("official.sr5.core", RulesetDefaults.Sr5);
            SeedRuntimeInspector("official.sr6.core", RulesetDefaults.Sr6);
        }

        public void BlockNextWorkspaceRead(string workspaceId)
        {
            BlockWorkspaceReadId = workspaceId;
            Volatile.Write(ref _blockNextWorkspaceRead, 1);
        }

        private string? BlockWorkspaceReadId { get; set; }

        public Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct)
        {
            return Task.FromResult(_preferences);
        }

        public Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct)
        {
            _preferences = new ShellPreferences(
                PreferredRulesetId: RulesetDefaults.NormalizeOptional(preferences.PreferredRulesetId) ?? string.Empty);
            return Task.CompletedTask;
        }

        public Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct)
        {
            return Task.FromResult(_session);
        }

        public Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct)
        {
            _session = new ShellSessionState(
                ActiveWorkspaceId: NormalizeWorkspaceId(session.ActiveWorkspaceId),
                ActiveTabId: NormalizeTabId(session.ActiveTabId),
                ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace));
            return Task.CompletedTask;
        }

        public async Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct)
        {
            IReadOnlyList<WorkspaceListItem> workspaces = await ListWorkspacesAsync(ct);
            CharacterWorkspaceId? activeWorkspaceId = ResolveActiveWorkspaceId(workspaces, _session.ActiveWorkspaceId);
            string preferredRulesetId = RulesetDefaults.NormalizeOptional(_preferences.PreferredRulesetId) ?? string.Empty;
            string activeRulesetId = activeWorkspaceId is null
                ? preferredRulesetId
                : RulesetDefaults.NormalizeOptional(
                    workspaces.First(workspace => string.Equals(workspace.Id.Value, activeWorkspaceId.Value.Value, StringComparison.Ordinal)).RulesetId) ?? string.Empty;
            string effectiveRulesetId = string.IsNullOrWhiteSpace(rulesetId)
                ? activeRulesetId
                : RulesetDefaults.NormalizeRequired(rulesetId);
            IReadOnlyList<AppCommandDefinition> commands = await GetCommandsAsync(effectiveRulesetId, ct);
            IReadOnlyList<NavigationTabDefinition> tabs = await GetNavigationTabsAsync(effectiveRulesetId, ct);
            return new ShellBootstrapSnapshot(
                RulesetId: effectiveRulesetId,
                Commands: commands,
                NavigationTabs: tabs,
                Workspaces: workspaces,
                PreferredRulesetId: preferredRulesetId,
                ActiveRulesetId: activeRulesetId,
                ActiveWorkspaceId: activeWorkspaceId,
                ActiveTabId: NormalizeTabId(_session.ActiveTabId),
                ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(_session.ActiveTabsByWorkspace),
                ActiveRuntime: DisableActiveRuntime ? null : CreateActiveRuntime(effectiveRulesetId));
        }

        public Task<RuntimeInspectorProjection?> GetRuntimeInspectorProfileAsync(string profileId, string? rulesetId, CancellationToken ct)
        {
            string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId)
                ?? RulesetDefaults.NormalizeRequired(_preferences.PreferredRulesetId);
            _runtimeInspectors.TryGetValue((profileId, normalizedRulesetId), out RuntimeInspectorProjection? projection);
            return Task.FromResult(projection);
        }

        public Task<MasterIndexResponse> GetMasterIndexAsync(CancellationToken ct)
        {
            return Task.FromResult(_masterIndex);
        }

        public Task<TranslatorLanguagesResponse> GetTranslatorLanguagesAsync(CancellationToken ct)
        {
            return Task.FromResult(_translatorLanguages);
        }

        public Task<IReadOnlyList<DesktopBuildPathSuggestion>> GetBuildPathSuggestionsAsync(string? rulesetId, CancellationToken ct)
        {
            string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
            IReadOnlyList<DesktopBuildPathSuggestion> suggestions =
            [
                new DesktopBuildPathSuggestion(
                    BuildKitId: string.Equals(normalizedRulesetId, RulesetDefaults.Sr6, StringComparison.Ordinal) ? "edge-runner-starter" : "street-sam-starter",
                    Title: string.Equals(normalizedRulesetId, RulesetDefaults.Sr6, StringComparison.Ordinal) ? "Edge Runner Starter" : "Street Sam Starter",
                    Targets: [normalizedRulesetId],
                    TrustTier: ArtifactTrustTiers.Curated,
                    Visibility: ArtifactVisibilityModes.Public)
            ];
            return Task.FromResult(suggestions);
        }

        public Task<DesktopBuildPathPreview?> GetBuildPathPreviewAsync(string buildKitId, CharacterWorkspaceId workspaceId, string? rulesetId, CancellationToken ct)
        {
            DesktopBuildPathPreview preview = new(
                State: "ready",
                RuntimeFingerprint: "sha256:core",
                ChangeSummaries:
                [
                    "Validate a compatible runtime before you apply this BuildKit: runtime sha256:core with no extra rule packs."
                ],
                DiagnosticMessages:
                [
                    "This BuildKit is ready to flow through the workbench and into a compatible runtime receipt."
                ],
                RequiresConfirmation: true);
            return Task.FromResult<DesktopBuildPathPreview?>(preview);
        }

        public void SeedWorkspace(
            string workspaceId,
            string name,
            string alias,
            DateTimeOffset? lastUpdatedUtc = null,
            string? rulesetId = null,
            long? contentRevision = null,
            long? savedRevision = null)
        {
            string resolvedRulesetId = _workspaces.TryGetValue(workspaceId, out WorkspaceListItem? existingWorkspace)
                ? RulesetDefaults.NormalizeOptional(rulesetId ?? existingWorkspace.RulesetId) ?? string.Empty
                : RulesetDefaults.NormalizeOptional(rulesetId) ?? string.Empty;
            CharacterFileSummary summary = new(
                Name: name,
                Alias: alias,
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "1.0",
                AppVersion: "1.0",
                Karma: 0m,
                Nuyen: 0m,
                Created: true);
            DateTimeOffset timestamp = lastUpdatedUtc ?? DateTimeOffset.UtcNow.AddMinutes(++_clock);
            long resolvedContentRevision = contentRevision ?? existingWorkspace?.ContentRevision ?? 1;
            long resolvedSavedRevision = savedRevision ?? existingWorkspace?.SavedRevision ?? resolvedContentRevision;
            _workspaces[workspaceId] = new WorkspaceListItem(
                new CharacterWorkspaceId(workspaceId),
                summary,
                timestamp,
                resolvedRulesetId,
                HasSavedWorkspace: resolvedSavedRevision > 0,
                ContentRevision: resolvedContentRevision,
                SavedRevision: resolvedSavedRevision);
            if (!_documents.ContainsKey(workspaceId))
            {
                _documents[workspaceId] = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
                    $"<character><name>{name}</name><alias>{alias}</alias></character>",
                    string.IsNullOrWhiteSpace(resolvedRulesetId) ? RulesetDefaults.Sr5 : resolvedRulesetId));
            }
        }

        public void SeedToolCatalog(
            MasterIndexResponse masterIndex,
            TranslatorLanguagesResponse? translatorLanguages = null)
        {
            _masterIndex = masterIndex;
            _translatorLanguages = translatorLanguages ?? new TranslatorLanguagesResponse(0, []);
        }

        public bool ContainsWorkspace(string workspaceId) => _workspaces.ContainsKey(workspaceId);

        public WorkspaceListItem GetWorkspaceItem(string workspaceId) => _workspaces[workspaceId];

        public void SeedDocument(string workspaceId, string xml, string rulesetId = RulesetDefaults.Sr5)
        {
            if (!_workspaces.ContainsKey(workspaceId))
            {
                SeedWorkspace(workspaceId, "Seeded", "SEED", rulesetId: rulesetId);
            }

            _documents[workspaceId] = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
                xml,
                rulesetId,
                WorkspaceDocumentFormat.NativeXml));
        }

        public void SeedDocument(string workspaceId, WorkspaceDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            if (!_workspaces.ContainsKey(workspaceId))
            {
                SeedWorkspace(workspaceId, "Seeded", "SEED", rulesetId: document.RulesetId);
            }

            _documents[workspaceId] = document;
        }

        public Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct)
        {
            LastImportedDocument = document;
            SeedWorkspace("ws-1", "Imported", _alias, contentRevision: 1, savedRevision: 0);
            _documents["ws-1"] = CanonicalizeRecoveryTestDocument(new WorkspaceDocument(
                document.Content,
                RulesetDefaults.NormalizeOptional(document.RulesetId) ?? RulesetDefaults.Sr5,
                document.Format));
            DateTimeOffset importedAtUtc = DateTimeOffset.Parse("2026-03-30T12:00:00+00:00");
            WorkspaceImportResult result = new(
                Id: new CharacterWorkspaceId("ws-1"),
                Summary: new CharacterFileSummary(
                    Name: "Imported",
                    Alias: _alias,
                    Metatype: "Ork",
                    BuildMethod: "SumtoTen",
                    CreatedVersion: "1.0",
                    AppVersion: "1.0",
                    Karma: 0m,
                    Nuyen: 0m,
                    Created: true),
                RulesetId: RulesetDefaults.NormalizeOptional(document.RulesetId) ?? string.Empty,
                ImportReceiptId: "import-ws-1-abc123",
                ImportedAtUtc: importedAtUtc,
                Portability: new WorkspacePortabilityReceipt(
                    FormatId: document.Format == WorkspaceDocumentFormat.Json
                        ? WorkspacePortabilityFormatIds.PortableDossierV1
                        : WorkspacePortabilityFormatIds.NativeWorkspaceXmlV1,
                    CompatibilityState: WorkspacePortabilityCompatibilityStates.Compatible,
                    ContextSummary: "Imported runner is now governed dossier truth.",
                    ReceiptSummary: "Portable import completed as governed dossier truth and is ready for normal use or portable export.",
                    ProvenanceSummary: $"Import receipt import-ws-1-abc123 captured payload hash abc123 at {importedAtUtc:O}.",
                    PayloadSha256: "abc123",
                    NextSafeAction: "Use the workspace normally or export it when you need a governed handoff.",
                    SupportedExchangeModes:
                    [
                        WorkspacePortabilityExchangeModes.InspectOnly,
                        WorkspacePortabilityExchangeModes.Merge,
                        WorkspacePortabilityExchangeModes.Replace
                    ],
                    Notes:
                    [
                        new WorkspacePortabilityNote(
                            Code: "format-identity",
                            Severity: WorkspacePortabilityNoteSeverities.Info,
                            Summary: "Imported native workspace XML on the governed dossier rail.")
                    ]),
                ContentRevision: 1,
                SavedRevision: 0);

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct)
        {
            ListWorkspacesCalls++;
            IReadOnlyList<WorkspaceListItem> workspaces = _workspaces.Values
                .OrderByDescending(workspace => workspace.LastUpdatedUtc)
                .ToArray();
            return Task.FromResult(workspaces);
        }

        public Task<AccountCampaignSummary?> GetAccountCampaignSummaryAsync(CancellationToken ct)
            => Task.FromResult<AccountCampaignSummary?>(null);

        public Task<MyFirstBookQuotaSnapshotDto?> GetMyFirstBookQuotaAsync(CancellationToken ct)
            => Task.FromResult<MyFirstBookQuotaSnapshotDto?>(null);

        public Task<MyFirstBookQuotaConsumeResultDto> ConsumeMyFirstBookQuotaAsync(CancellationToken ct)
            => Task.FromException<MyFirstBookQuotaConsumeResultDto>(new InvalidOperationException("Not used in this test."));

        public Task<IReadOnlyList<CampaignWorkspaceDigestProjection>> GetCampaignWorkspaceDigestsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CampaignWorkspaceDigestProjection>>(Array.Empty<CampaignWorkspaceDigestProjection>());

        public Task<IReadOnlyList<DesktopHomeSupportDigest>> GetDesktopHomeSupportDigestsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DesktopHomeSupportDigest>>([]);

        public Task<DesktopSupportCaseDetails?> GetDesktopSupportCaseDetailsAsync(string caseId, CancellationToken ct)
            => Task.FromResult<DesktopSupportCaseDetails?>(null);

        public Task<DesktopInstallLinkingSummaryProjection> GetDesktopInstallLinkingSummaryAsync(CancellationToken ct)
            => Task.FromResult(DesktopInstallLinkingSummaryProjection.Empty);

        public Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            CloseWorkspaceCalls++;
            if (ThrowOnCloseWorkspace)
            {
                throw new InvalidOperationException("Simulated close failure.");
            }

            bool removed = _workspaces.Remove(id.Value);
            _documents.Remove(id.Value);
            return Task.FromResult(removed);
        }

        public Task<CommandResult<WorkspaceRevisionReceipt>> CloseWorkspaceAsync(
            CharacterWorkspaceId id,
            long expectedContentRevision,
            CancellationToken ct)
        {
            CloseWorkspaceCalls++;
            if (!_workspaces.TryGetValue(id.Value, out WorkspaceListItem? workspace))
            {
                return Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(
                    false,
                    null,
                    "Workspace not found.",
                    WorkspaceOperationOutcome.Missing));
            }

            if (workspace.ContentRevision != expectedContentRevision)
            {
                return Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(
                    false,
                    null,
                    "Workspace revision changed.",
                    WorkspaceOperationOutcome.Conflict));
            }

            if (ReturnNullDeleteReceipt)
            {
                return Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(
                    true,
                    null,
                    null));
            }

            _workspaces.Remove(id.Value);
            _documents.Remove(id.Value);
            DeleteReceiptCommitted?.Invoke();
            return Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(
                true,
                new WorkspaceRevisionReceipt(id, workspace.ContentRevision, workspace.SavedRevision),
                null));
        }

        public async Task<CommandResult<WorkspaceDocumentSnapshot>> GetWorkspaceAsync(
            CharacterWorkspaceId id,
            CancellationToken ct)
        {
            GetWorkspaceCalls++;
            if (string.Equals(BlockWorkspaceReadId, id.Value, StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref _blockNextWorkspaceRead, 0, 1) == 1)
            {
                WorkspaceReadStarted.TrySetResult(true);
                await ReleaseWorkspaceRead.Task.WaitAsync(ct).ConfigureAwait(false);
            }

            if (string.Equals(ThrowGetWorkspaceId, id.Value, StringComparison.Ordinal))
                throw new InvalidOperationException("Simulated next workspace load failure.");
            if (!_workspaces.ContainsKey(id.Value))
            {
                SeedWorkspace(id.Value, _name, _alias, rulesetId: RulesetDefaults.Sr5);
            }

            WorkspaceListItem workspace = _workspaces[id.Value];
            WorkspaceDocument document = _documents[id.Value];
            return new CommandResult<WorkspaceDocumentSnapshot>(
                true,
                new WorkspaceDocumentSnapshot(
                    id,
                    document,
                    workspace.LastUpdatedUtc,
                    workspace.ContentRevision,
                    workspace.SavedRevision),
                null);
        }

        public Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct)
        {
            GetCommandsCalls++;
            string effectiveRulesetId = RulesetDefaults.NormalizeOptional(rulesetId)
                ?? RulesetDefaults.NormalizeRequired(_preferences.PreferredRulesetId);
            return Task.FromResult(CreateCommands(effectiveRulesetId));
        }

        public Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct)
        {
            GetNavigationTabsCalls++;
            string effectiveRulesetId = RulesetDefaults.NormalizeOptional(rulesetId)
                ?? RulesetDefaults.NormalizeRequired(_preferences.PreferredRulesetId);
            return Task.FromResult(CreateTabs(effectiveRulesetId));
        }

        private void SeedRuntimeInspector(string profileId, string rulesetId)
        {
            _runtimeInspectors[(profileId, rulesetId)] = CreateRuntimeInspectorProjection(profileId, rulesetId);
        }

        private static ActiveRuntimeStatusProjection CreateActiveRuntime(string rulesetId)
        {
            string normalizedRulesetId = RulesetDefaults.NormalizeRequired(rulesetId);
            return new ActiveRuntimeStatusProjection(
                ProfileId: $"official.{normalizedRulesetId}.core",
                Title: normalizedRulesetId == RulesetDefaults.Sr6 ? "SR6 Core" : "SR5 Core",
                RulesetId: normalizedRulesetId,
                RuntimeFingerprint: $"sha256:{normalizedRulesetId}-runtime-fingerprint",
                InstallState: ArtifactInstallStates.Available,
                RulePackCount: 1,
                ProviderBindingCount: 1,
                WarningCount: 0);
        }

        private static RuntimeInspectorProjection CreateRuntimeInspectorProjection(string profileId, string rulesetId)
        {
            string normalizedRulesetId = RulesetDefaults.NormalizeRequired(rulesetId);
            return new RuntimeInspectorProjection(
                TargetKind: RuntimeInspectorTargetKinds.RuntimeLock,
                TargetId: profileId,
                RuntimeLock: new ResolvedRuntimeLock(
                    RulesetId: normalizedRulesetId,
                    ContentBundles:
                    [
                        new ContentBundleDescriptor(
                            BundleId: $"{normalizedRulesetId}.core.bundle",
                            RulesetId: normalizedRulesetId,
                            Version: "1.0.0",
                            Title: "Core Bundle",
                            Description: "Default bundle",
                            AssetPaths: ["data/core.xml"])
                    ],
                    RulePacks:
                    [
                        new ArtifactVersionReference($"official.{normalizedRulesetId}.core", "1.0.0")
                    ],
                    ProviderBindings: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [RulePackCapabilityIds.DeriveStat] = $"official.{normalizedRulesetId}.core/derive.stat"
                    },
                    EngineApiVersion: "1.0.0",
                    RuntimeFingerprint: $"sha256:{normalizedRulesetId}-runtime-fingerprint"),
                Install: new ArtifactInstallState(
                    State: ArtifactInstallStates.Available,
                    RuntimeFingerprint: $"sha256:{normalizedRulesetId}-runtime-fingerprint"),
                ResolvedRulePacks:
                [
                    new RuntimeInspectorRulePackEntry(
                        new ArtifactVersionReference($"official.{normalizedRulesetId}.core", "1.0.0"),
                        normalizedRulesetId == RulesetDefaults.Sr6 ? "SR6 Core" : "SR5 Core",
                        ArtifactVisibilityModes.LocalOnly,
                        ArtifactTrustTiers.Official,
                        [RulePackCapabilityIds.DeriveStat])
                ],
                ProviderBindings:
                [
                    new RuntimeInspectorProviderBinding(
                        CapabilityId: RulePackCapabilityIds.DeriveStat,
                        ProviderId: $"official.{normalizedRulesetId}.core/derive.stat",
                        PackId: $"official.{normalizedRulesetId}.core")
                ],
                CompatibilityDiagnostics:
                [
                    new RuntimeLockCompatibilityDiagnostic(
                        State: RuntimeLockCompatibilityStates.Compatible,
                        Message: "Runtime lock resolves against the current RuleProfile and RulePack catalog.",
                        RequiredRulesetId: normalizedRulesetId,
                        RequiredRuntimeFingerprint: $"sha256:{normalizedRulesetId}-runtime-fingerprint")
                ],
                Warnings: [],
                MigrationPreview:
                [
                    new RuntimeMigrationPreviewItem(
                        Kind: RuntimeMigrationPreviewChangeKinds.RulePackAdded,
                        Summary: $"Profile applies RulePack 'official.{normalizedRulesetId}.core@1.0.0'.",
                        SubjectId: $"official.{normalizedRulesetId}.core",
                        AfterValue: "1.0.0")
                ],
                GeneratedAtUtc: DateTimeOffset.UtcNow);
        }

        public Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct)
        {
            JsonObject section = new()
            {
                ["workspaceId"] = id.Value,
                ["sectionId"] = sectionId
            };

            return Task.FromResult<JsonNode>(section);
        }

        public Task<CharacterFileSummary> GetSummaryAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            return Task.FromResult(new CharacterFileSummary(
                Name: _name,
                Alias: _alias,
                Metatype: "Ork",
                BuildMethod: "SumtoTen",
                CreatedVersion: "1.0",
                AppVersion: "1.0",
                Karma: 12m,
                Nuyen: 5000m,
                Created: true));
        }

        public async Task<CharacterValidationResult> ValidateAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            if (string.Equals(BlockValidationWorkspaceId, id.Value, StringComparison.Ordinal))
            {
                ValidationStarted.TrySetResult(true);
                await ReleaseValidation.Task.WaitAsync(ct).ConfigureAwait(false);
            }

            return new CharacterValidationResult(
                IsValid: ValidationIsValid,
                Issues: ValidationIsValid
                    ? []
                    : [new CharacterValidationIssue("error", "canonical", "Rejected by test codec.", "workspace")]);
        }

        public async Task<CharacterProfileSection> GetProfileAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            GetProfileCalls++;
            SeedWorkspace(id.Value, _name, _alias);
            if (string.Equals(ThrowProfileWorkspaceId, id.Value, StringComparison.Ordinal))
                throw new InvalidOperationException("Simulated postcommit profile projection failure.");
            if (string.Equals(BlockProfileWorkspaceId, id.Value, StringComparison.Ordinal))
            {
                ProfileLoadStarted.TrySetResult(true);
                await ReleaseProfileLoad.Task.ConfigureAwait(false);
            }

            CharacterProfileSection profile = new(
                Name: _name,
                Alias: _alias,
                PlayerName: string.Empty,
                Metatype: "Ork",
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
                BuildMethod: "SumtoTen",
                GameplayOption: string.Empty,
                Created: true,
                Adept: false,
                Magician: false,
                Technomancer: false,
                AI: false,
                MainMugshotIndex: 0,
                MugshotCount: 0);

            return profile;
        }

        public Task<CharacterProgressSection> GetProgressAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            CharacterProgressSection progress = new(
                Karma: 12m,
                Nuyen: 5000m,
                StartingNuyen: 0m,
                StreetCred: 1,
                Notoriety: 0,
                PublicAwareness: 0,
                BurntStreetCred: 0,
                BuildKarma: 0,
                TotalAttributes: 0,
                TotalSpecial: 0,
                PhysicalCmFilled: 0,
                StunCmFilled: 0,
                TotalEssence: 6m,
                InitiateGrade: 0,
                SubmersionGrade: 0,
                MagEnabled: false,
                ResEnabled: false,
                DepEnabled: false);

            return Task.FromResult(progress);
        }

        public Task<CharacterSkillsSection> GetSkillsAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            CharacterSkillsSection skills = new(
                Count: 1,
                KnowledgeCount: 0,
                Skills:
                [
                    new CharacterSkillSummary(
                        Guid: "1",
                        Suid: string.Empty,
                        Category: "Combat",
                        IsKnowledge: false,
                        BaseValue: 6,
                        KarmaValue: 0,
                        Specializations: ["Semi-Automatics"])
                ]);

            return Task.FromResult(skills);
        }

        public Task<CharacterRulesSection> GetRulesAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            CharacterRulesSection rules = new(
                GameEdition: "SR5",
                Settings: "default.xml",
                GameplayOption: "Standard",
                GameplayOptionQualityLimit: 25,
                MaxNuyen: 10,
                MaxKarma: 25,
                ContactMultiplier: 3,
                BannedWareGrades: ["Betaware"]);

            return Task.FromResult(rules);
        }

        public Task<CharacterBuildSection> GetBuildAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            CharacterBuildSection build = new(
                BuildMethod: "SumtoTen",
                PriorityMetatype: "C,2",
                PriorityAttributes: "E,0",
                PrioritySpecial: "A,4",
                PrioritySkills: "B,3",
                PriorityResources: "D,1",
                PriorityTalent: "Mundane",
                SumToTen: 10,
                Special: 1,
                TotalSpecial: 4,
                TotalAttributes: 20,
                ContactPoints: 15,
                ContactPointsUsed: 8);

            return Task.FromResult(build);
        }

        public Task<CharacterMovementSection> GetMovementAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            CharacterMovementSection movement = new(
                Walk: "2/1/0",
                Run: "4/0/0",
                Sprint: "2/1/0",
                WalkAlt: "2/1/0",
                RunAlt: "4/0/0",
                SprintAlt: "2/1/0",
                PhysicalCmFilled: 0,
                StunCmFilled: 0);

            return Task.FromResult(movement);
        }

        public Task<CharacterAwakeningSection> GetAwakeningAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            CharacterAwakeningSection awakening = new(
                MagEnabled: false,
                ResEnabled: false,
                DepEnabled: false,
                Adept: false,
                Magician: false,
                Technomancer: false,
                AI: false,
                InitiateGrade: 0,
                SubmersionGrade: 0,
                Tradition: string.Empty,
                TraditionName: string.Empty,
                TraditionDrain: string.Empty,
                SpiritCombat: string.Empty,
                SpiritDetection: string.Empty,
                SpiritHealth: string.Empty,
                SpiritIllusion: string.Empty,
                SpiritManipulation: string.Empty,
                Stream: string.Empty,
                StreamDrain: string.Empty,
                CurrentCounterspellingDice: 0,
                SpellLimit: 0,
                CfpLimit: 0,
                AiNormalProgramLimit: 0,
                AiAdvancedProgramLimit: 0);

            return Task.FromResult(awakening);
        }

        public Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(CharacterWorkspaceId id, UpdateWorkspaceMetadata command, CancellationToken ct)
        {
            LastUpdateMetadata = command;
            _name = command.Name ?? _name;
            _alias = command.Alias ?? _alias;
            SeedWorkspace(id.Value, _name, _alias);

            CharacterProfileSection updated = new(
                Name: _name,
                Alias: _alias,
                PlayerName: string.Empty,
                Metatype: "Ork",
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
                BuildMethod: "SumtoTen",
                GameplayOption: string.Empty,
                Created: true,
                Adept: false,
                Magician: false,
                Technomancer: false,
                AI: false,
                MainMugshotIndex: 0,
                MugshotCount: 0);

            return Task.FromResult(new CommandResult<CharacterProfileSection>(
                Success: true,
                Value: updated,
                Error: null));
        }

        public async Task<CommandResult<WorkspaceMetadataResult>> UpdateMetadataAsync(
            CharacterWorkspaceId id,
            long expectedContentRevision,
            UpdateWorkspaceMetadata command,
            CancellationToken ct)
        {
            CommandResult<WorkspaceDocumentSnapshot> read = await GetWorkspaceAsync(id, ct);
            if (read.Value!.ContentRevision != expectedContentRevision)
            {
                return new CommandResult<WorkspaceMetadataResult>(
                    false,
                    null,
                    "Workspace revision changed.",
                    WorkspaceOperationOutcome.Conflict);
            }

            if (BlockRevisionedMetadata)
            {
                RevisionedMetadataStarted.TrySetResult(true);
                await ReleaseRevisionedMetadata.Task.ConfigureAwait(false);
            }

            LastUpdateMetadata = command;
            _name = command.Name ?? _name;
            _alias = command.Alias ?? _alias;
            long nextRevision = expectedContentRevision + 1;
            SeedWorkspace(
                id.Value,
                _name,
                _alias,
                rulesetId: read.Value.Document.RulesetId,
                contentRevision: nextRevision,
                savedRevision: read.Value.SavedRevision);
            CharacterProfileSection profile = await GetProfileAsync(id, ct);
            return new CommandResult<WorkspaceMetadataResult>(
                true,
                new WorkspaceMetadataResult(profile, nextRevision, read.Value.SavedRevision),
                null);
        }

        public async Task<CommandResult<WorkspaceRevisionReceipt>> ReplaceWorkspaceDocumentAsync(
            CharacterWorkspaceId id,
            long expectedContentRevision,
            WorkspaceDocument document,
            CancellationToken ct)
        {
            ReplaceWorkspaceCalls++;
            CommandResult<WorkspaceDocumentSnapshot> read = await GetWorkspaceAsync(id, ct);
            if (read.Value!.ContentRevision != expectedContentRevision)
            {
                return new CommandResult<WorkspaceRevisionReceipt>(
                    false,
                    null,
                    "Workspace revision changed.",
                    WorkspaceOperationOutcome.Conflict);
            }

            if (BlockRevisionedReplace)
            {
                RevisionedReplaceStarted.TrySetResult(true);
                await ReleaseRevisionedReplace.Task.ConfigureAwait(false);
            }

            long nextRevision = expectedContentRevision + 1;
            _documents[id.Value] = document;
            WorkspaceListItem existing = _workspaces[id.Value];
            SeedWorkspace(
                id.Value,
                existing.Summary.Name,
                existing.Summary.Alias,
                rulesetId: document.RulesetId,
                contentRevision: nextRevision,
                savedRevision: existing.SavedRevision);
            return new CommandResult<WorkspaceRevisionReceipt>(
                true,
                new WorkspaceRevisionReceipt(id, nextRevision, existing.SavedRevision),
                null);
        }

        public Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            SeedWorkspace(id.Value, _name, _alias);
            return Task.FromResult(new CommandResult<WorkspaceSaveReceipt>(
                Success: true,
                Value: new WorkspaceSaveReceipt(
                    Id: id,
                    DocumentLength: 64,
                    RulesetId: RulesetDefaults.Sr5),
                Error: null));
        }

        public async Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(
            CharacterWorkspaceId id,
            long expectedContentRevision,
            CancellationToken ct)
        {
            RevisionedSaveCalls++;
            CommandResult<WorkspaceDocumentSnapshot> read = await GetWorkspaceAsync(id, ct);
            if (read.Value!.ContentRevision != expectedContentRevision)
            {
                return new CommandResult<WorkspaceSaveReceipt>(
                    false,
                    null,
                    "Workspace revision changed.",
                    WorkspaceOperationOutcome.Conflict);
            }

            if (BlockRevisionedSave)
            {
                RevisionedSaveStarted.TrySetResult(true);
                await ReleaseRevisionedSave.Task.ConfigureAwait(false);
            }

            if (ForceSaveConflict)
            {
                WorkspaceListItem winner = _workspaces[id.Value];
                SeedWorkspace(
                    id.Value,
                    winner.Summary.Name,
                    winner.Summary.Alias,
                    rulesetId: winner.RulesetId,
                    contentRevision: winner.ContentRevision + 1,
                    savedRevision: winner.SavedRevision);
                return new CommandResult<WorkspaceSaveReceipt>(
                    false,
                    null,
                    "Workspace revision changed.",
                    WorkspaceOperationOutcome.Conflict);
            }

            WorkspaceListItem existing = _workspaces[id.Value];
            SeedWorkspace(
                id.Value,
                existing.Summary.Name,
                existing.Summary.Alias,
                rulesetId: existing.RulesetId,
                contentRevision: existing.ContentRevision,
                savedRevision: existing.ContentRevision);
            return new CommandResult<WorkspaceSaveReceipt>(
                true,
                new WorkspaceSaveReceipt(
                    id,
                    read.Value.Document.Content.Length,
                    read.Value.Document.RulesetId,
                    ContentRevision: existing.ContentRevision,
                    SavedRevision: existing.ContentRevision),
                null);
        }

        public Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            DownloadCalls++;
            SeedWorkspace(id.Value, _name, _alias);
            WorkspaceDocument document = _documents[id.Value];
            return Task.FromResult(new CommandResult<WorkspaceDownloadReceipt>(
                Success: true,
                Value: new WorkspaceDownloadReceipt(
                    Id: id,
                    Format: WorkspaceDocumentFormat.NativeXml,
                    ContentBase64: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(document.Content)),
                    FileName: $"{id.Value}.chum5",
                    DocumentLength: document.Content.Length,
                    RulesetId: document.RulesetId),
                Error: null));
        }

        public Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            ExportCalls++;
            SeedWorkspace(id.Value, _name, _alias);

            DataExportBundle bundle = new(
                Summary: new CharacterFileSummary(
                    Name: _name,
                    Alias: _alias,
                    Metatype: "Ork",
                    BuildMethod: "SumtoTen",
                    CreatedVersion: "1.0",
                    AppVersion: "1.0",
                    Karma: 12m,
                    Nuyen: 5000m,
                    Created: true),
                Profile: new CharacterProfileSection(
                    Name: _name,
                    Alias: _alias,
                    PlayerName: string.Empty,
                    Metatype: "Ork",
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
                    BuildMethod: "SumtoTen",
                    GameplayOption: string.Empty,
                    Created: true,
                    Adept: false,
                    Magician: false,
                    Technomancer: false,
                    AI: false,
                    MainMugshotIndex: 0,
                    MugshotCount: 0),
                Progress: new CharacterProgressSection(
                    Karma: 12m,
                    Nuyen: 5000m,
                    StartingNuyen: 0m,
                    StreetCred: 1,
                    Notoriety: 0,
                    PublicAwareness: 0,
                    BurntStreetCred: 0,
                    BuildKarma: 0,
                    TotalAttributes: 0,
                    TotalSpecial: 0,
                    PhysicalCmFilled: 0,
                    StunCmFilled: 0,
                    TotalEssence: 6m,
                    InitiateGrade: 0,
                    SubmersionGrade: 0,
                    MagEnabled: false,
                    ResEnabled: false,
                    DepEnabled: false),
                Attributes: new CharacterAttributesSection(
                    Count: 1,
                    Attributes:
                    [
                        new CharacterAttributeSummary("Reaction", 5, 7)
                    ]),
                Skills: new CharacterSkillsSection(
                    Count: 1,
                    KnowledgeCount: 0,
                    Skills:
                    [
                        new CharacterSkillSummary("1", string.Empty, "Combat", false, 6, 0, ["Semi-Automatics"])
                    ]),
                Inventory: new CharacterInventorySection(
                    GearCount: 1,
                    WeaponCount: 0,
                    ArmorCount: 0,
                    CyberwareCount: 0,
                    VehicleCount: 0,
                    GearNames: ["Medkit"],
                    WeaponNames: [],
                    ArmorNames: [],
                    CyberwareNames: [],
                    VehicleNames: []),
                Qualities: new CharacterQualitiesSection(
                    Count: 1,
                    Qualities:
                    [
                        new CharacterQualitySummary("First Impression", "Core", 11)
                    ]),
                Contacts: new CharacterContactsSection(
                    Count: 1,
                    Contacts:
                    [
                        new CharacterContactSummary("Fixer", "Broker", "Seattle", 4, 3)
                    ]));

            string json = JsonSerializer.Serialize(bundle);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(json);
            DateTimeOffset exportedAtUtc = DateTimeOffset.Parse("2026-03-30T12:05:00+00:00");
            return Task.FromResult(new CommandResult<WorkspaceExportReceipt>(
                Success: true,
                Value: new WorkspaceExportReceipt(
                    Id: id,
                    Format: WorkspaceDocumentFormat.Json,
                    ContentBase64: Convert.ToBase64String(payloadBytes),
                    FileName: $"{id.Value}-export.json",
                    DocumentLength: payloadBytes.Length,
                    RulesetId: RulesetDefaults.Sr5,
                    PackageId: "portable-ws-export-abc123",
                    ExportedAtUtc: exportedAtUtc,
                    Portability: new WorkspacePortabilityReceipt(
                        FormatId: WorkspacePortabilityFormatIds.PortableDossierV1,
                        CompatibilityState: WorkspacePortabilityCompatibilityStates.Compatible,
                        ContextSummary: "Runner export is packaged as a portable dossier.",
                        ReceiptSummary: "Portable export is ready for inspect-only, merge, or governed replace on a receiving surface.",
                        ProvenanceSummary: $"Portable package portable-ws-export-abc123 captured payload hash abc123 at {exportedAtUtc:O}.",
                        PayloadSha256: "abc123",
                        NextSafeAction: "Share the package or inspect it first on the receiving surface.",
                        SupportedExchangeModes:
                        [
                            WorkspacePortabilityExchangeModes.InspectOnly,
                            WorkspacePortabilityExchangeModes.Merge,
                            WorkspacePortabilityExchangeModes.Replace
                        ],
                        Notes:
                        [
                            new WorkspacePortabilityNote(
                                Code: "format-identity",
                                Severity: WorkspacePortabilityNoteSeverities.Info,
                                Summary: "Package format chummer.portable-dossier.v1 stays attached to governed dossier truth.")
                        ])),
                Error: null));
        }

        public Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            PrintCalls++;
            SeedWorkspace(id.Value, _name, _alias);

            string html = $"<!DOCTYPE html><html><head><title>{_name}</title></head><body><h1>{_name}</h1><p>{_alias}</p></body></html>";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(html);
            return Task.FromResult(new CommandResult<WorkspacePrintReceipt>(
                Success: true,
                Value: new WorkspacePrintReceipt(
                    Id: id,
                    ContentBase64: Convert.ToBase64String(payloadBytes),
                    FileName: $"{id.Value}-print.html",
                    MimeType: "text/html",
                    DocumentLength: payloadBytes.Length,
                    Title: _name,
                    RulesetId: RulesetDefaults.Sr5),
                Error: null));
        }

        private static string? NormalizeWorkspaceId(string? workspaceId)
        {
            return string.IsNullOrWhiteSpace(workspaceId)
                ? null
                : workspaceId.Trim();
        }

        private static IReadOnlyList<AppCommandDefinition> CreateCommands(string rulesetId)
        {
            return
            [
                new("new_character", "command.new_character", "file", false, true, rulesetId),
                new("save_character", "command.save_character", "file", true, true, rulesetId)
            ];
        }

        private static IReadOnlyList<NavigationTabDefinition> CreateTabs(string rulesetId)
        {
            return
            [
                new("tab-info", "Info", "profile", "character", true, true, rulesetId),
                new("tab-gear", "Gear", "gear", "character", true, true, rulesetId)
            ];
        }

        private static string? NormalizeTabId(string? tabId)
        {
            return string.IsNullOrWhiteSpace(tabId)
                ? null
                : tabId.Trim();
        }

        private static Dictionary<string, string>? NormalizeWorkspaceTabMap(IReadOnlyDictionary<string, string>? rawMap)
        {
            if (rawMap is null || rawMap.Count == 0)
            {
                return null;
            }

            Dictionary<string, string> normalized = new(StringComparer.Ordinal);
            foreach ((string workspaceId, string tabId) in rawMap)
            {
                string? normalizedWorkspaceId = NormalizeWorkspaceId(workspaceId);
                string? normalizedTabId = NormalizeTabId(tabId);
                if (normalizedWorkspaceId is null || normalizedTabId is null)
                {
                    continue;
                }

                normalized[normalizedWorkspaceId] = normalizedTabId;
            }

            return normalized.Count == 0
                ? null
                : normalized;
        }

        private static CharacterWorkspaceId? ResolveActiveWorkspaceId(
            IEnumerable<WorkspaceListItem> workspaces,
            string? preferredWorkspaceId)
        {
            WorkspaceListItem[] workspaceList = workspaces as WorkspaceListItem[] ?? workspaces.ToArray();
            if (string.IsNullOrWhiteSpace(preferredWorkspaceId))
            {
                return null;
            }

            WorkspaceListItem? matchingWorkspace = workspaceList.FirstOrDefault(workspace =>
                string.Equals(workspace.Id.Value, preferredWorkspaceId, StringComparison.Ordinal));
            return matchingWorkspace?.Id;
        }
    }

    private sealed class ShellPresenterStub : IShellPresenter
    {
        public ShellPresenterStub(ShellState state)
        {
            State = state;
        }

        public ShellState State { get; private set; }
        public string? LastPreferredRulesetId { get; private set; }
        public ShellOverviewFeedback? LastOverviewFeedback { get; private set; }

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task SelectTabAsync(string tabId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task ToggleMenuAsync(string menuId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct)
        {
            LastPreferredRulesetId = RulesetDefaults.NormalizeRequired(rulesetId);
            State = State with
            {
                PreferredRulesetId = LastPreferredRulesetId,
                ActiveRulesetId = LastPreferredRulesetId
            };
            return Task.CompletedTask;
        }

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void SyncOverviewFeedback(ShellOverviewFeedback feedback)
        {
            LastOverviewFeedback = feedback;
        }
    }
}
