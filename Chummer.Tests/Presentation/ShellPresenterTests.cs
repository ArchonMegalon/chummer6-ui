#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
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
using Chummer.Rulesets.Hosting.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class ShellPresenterTests
{
    [TestMethod]
    public async Task InitializeAsync_loads_shell_contract_and_restores_workspaces_without_auto_selecting_active_workspace()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-old", "Old Character", "OLD", now.AddMinutes(-25)),
                CreateWorkspace("ws-new", "New Character", "NEW", now.AddMinutes(-5))
            ]
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.IsFalse(presenter.State.IsBusy);
        Assert.IsNull(presenter.State.Error);
        Assert.HasCount(2, presenter.State.OpenWorkspaces);
        Assert.IsNull(presenter.State.ActiveWorkspaceId);
        Assert.AreEqual("ws-new", presenter.State.OpenWorkspaces[0].Id.Value);
        Assert.AreEqual("file", presenter.State.MenuRoots[0].Id);
        Assert.AreEqual(RulesetDefaults.Sr5, presenter.State.ActiveRulesetId);
        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        StringAssert.Contains(presenter.State.Notice ?? string.Empty, "Restored 2 workspace(s).");
    }

    [TestMethod]
    public async Task InitializeAsync_restores_persisted_active_workspace_from_bootstrap_session()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-older", "Older Character", "OLD", now.AddMinutes(-25)),
                CreateWorkspace("ws-newer", "Newer Character", "NEW", now.AddMinutes(-5))
            ],
            Preferences = new ShellPreferences(RulesetDefaults.Sr5),
            Session = new ShellSessionState("ws-older")
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.AreEqual("ws-older", presenter.State.ActiveWorkspaceId?.Value);
    }

    [TestMethod]
    public async Task InitializeAsync_uses_active_workspace_ruleset_for_shell_contract()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr5", "SR5 Character", "SR5", now.AddMinutes(-25), RulesetDefaults.Sr5),
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now.AddMinutes(-5), "sr6")
            ],
            Session = new ShellSessionState("ws-sr6")
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.AreEqual("sr6", presenter.State.ActiveRulesetId);
        CollectionAssert.Contains(client.RequestedCommandRulesets, "sr6");
        CollectionAssert.Contains(client.RequestedNavigationRulesets, "sr6");
    }

    [TestMethod]
    public async Task InitializeAsync_requests_catalogs_only_for_active_ruleset()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr5", "SR5 Character", "SR5", now.AddMinutes(-25), RulesetDefaults.Sr5),
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now.AddMinutes(-5), "sr6")
            ],
            Session = new ShellSessionState("ws-sr6")
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        string?[] expectedSr6Rulesets = ["sr6"];
        CollectionAssert.AreEqual(expectedSr6Rulesets, client.RequestedCommandRulesets);
        CollectionAssert.AreEqual(expectedSr6Rulesets, client.RequestedNavigationRulesets);
    }

    [TestMethod]
    public async Task InitializeAsync_supplements_missing_classic_menu_roots_when_ruleset_catalog_is_sparse()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now.AddMinutes(-5), RulesetDefaults.Sr6)
            ],
            Session = new ShellSessionState("ws-sr6"),
            Commands =
            [
                new AppCommandDefinition("file", "command.file", "menu", false, true, RulesetDefaults.Sr6),
                new AppCommandDefinition("tools", "command.tools", "menu", false, true, RulesetDefaults.Sr6),
                new AppCommandDefinition("help", "command.help", "menu", false, true, RulesetDefaults.Sr6),
                new AppCommandDefinition("open_character", "command.open_character", "file", false, true, RulesetDefaults.Sr6),
                new AppCommandDefinition("save_character", "command.save_character", "file", true, true, RulesetDefaults.Sr6)
            ]
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        string[] menuIds = presenter.State.MenuRoots.Select(command => command.Id).ToArray();

        CollectionAssert.AreEqual(new[] { "file", "edit", "special", "tools", "windows", "help" }, menuIds);
        Assert.IsTrue(
            presenter.State.Commands.Any(command => string.Equals(command.Id, "copy", StringComparison.Ordinal)),
            "Classic edit commands must be synthesized when the active ruleset catalog omits them.");
        Assert.IsTrue(
            presenter.State.Commands.Any(command => string.Equals(command.Id, "report_bug", StringComparison.Ordinal)),
            "Classic help fallbacks must stay reachable even when the active ruleset only defines a sparse shell.");
    }

    [TestMethod]
    public async Task SyncWorkspaceContextAsync_switches_ruleset_when_active_workspace_changes()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr5", "SR5 Character", "SR5", now.AddMinutes(-5), RulesetDefaults.Sr5),
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now.AddMinutes(-25), "sr6")
            ]
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        client.Workspaces =
        [
            CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now.AddMinutes(-1), "sr6"),
            CreateWorkspace("ws-sr5", "SR5 Character", "SR5", now.AddMinutes(-20), RulesetDefaults.Sr5)
        ];
        await presenter.SyncWorkspaceContextAsync(new CharacterWorkspaceId("ws-sr6"), CancellationToken.None);

        Assert.AreEqual("ws-sr6", presenter.State.ActiveWorkspaceId?.Value);
        Assert.AreEqual("sr6", presenter.State.ActiveRulesetId);
        CollectionAssert.Contains(client.RequestedCommandRulesets, "sr6");
        CollectionAssert.Contains(client.RequestedNavigationRulesets, "sr6");
    }

    [TestMethod]
    public async Task ToggleMenuAsync_toggles_open_and_closed_state()
    {
        var presenter = new ShellPresenter(new ShellClientStub());
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.ToggleMenuAsync("file", CancellationToken.None);
        Assert.AreEqual("file", presenter.State.OpenMenuId);
        Assert.IsNull(presenter.State.Notice);

        await presenter.ToggleMenuAsync("file", CancellationToken.None);
        Assert.IsNull(presenter.State.OpenMenuId);
        Assert.IsNull(presenter.State.Notice);
    }

    [TestMethod]
    public async Task ToggleMenuAsync_noop_for_menu_with_no_visible_commands()
    {
        var client = new ShellClientStub
        {
            Commands =
            [
                new AppCommandDefinition("file", "command.file", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("archive", "command.archive", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("help", "command.help", "menu", false, true, RulesetDefaults.Sr5)
            ]
        };

        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.ToggleMenuAsync("archive", CancellationToken.None);

        Assert.IsNull(presenter.State.OpenMenuId);
        Assert.IsNull(presenter.State.LastCommandId);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNull(presenter.State.Notice);
    }

    [TestMethod]
    public async Task ToggleMenuAsync_opens_special_menu_when_switch_ruleset_is_visible()
    {
        var client = new ShellClientStub
        {
            Commands =
            [
                new AppCommandDefinition("file", "command.file", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("special", "command.special", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("switch_ruleset", "command.switch_ruleset", "special", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("help", "command.help", "menu", false, true, RulesetDefaults.Sr5)
            ]
        };

        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.ToggleMenuAsync("special", CancellationToken.None);

        Assert.AreEqual("special", presenter.State.OpenMenuId);
        Assert.IsNull(presenter.State.LastCommandId);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNull(presenter.State.Notice);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_noop_for_menu_root_with_no_visible_commands()
    {
        var client = new ShellClientStub
        {
            Commands =
            [
                new AppCommandDefinition("file", "command.file", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("archive", "command.archive", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("help", "command.help", "menu", false, true, RulesetDefaults.Sr5)
            ]
        };

        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.ExecuteCommandAsync("archive", CancellationToken.None);

        Assert.IsNull(presenter.State.OpenMenuId);
        Assert.IsNull(presenter.State.LastCommandId);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNull(presenter.State.Notice);
    }

    [TestMethod]
    public async Task ToggleMenuAsync_opens_edit_menu_when_workspace_scoped_copy_is_enabled()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-active", "Runner", "R", now.AddMinutes(-5))
            ],
            Session = new ShellSessionState("ws-active"),
            Commands =
            [
                new AppCommandDefinition("file", "command.file", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("edit", "command.edit", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("copy", "command.copy", "edit", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("paste", "command.paste", "edit", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("help", "command.help", "menu", false, true, RulesetDefaults.Sr5)
            ]
        };

        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.ToggleMenuAsync("edit", CancellationToken.None);

        Assert.AreEqual("edit", presenter.State.OpenMenuId);
        Assert.IsNull(presenter.State.LastCommandId);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNull(presenter.State.Notice);
    }

    [TestMethod]
    public async Task ToggleMenuAsync_opens_menu_with_only_disabled_commands_so_classic_root_stays_visible()
    {
        var client = new ShellClientStub
        {
            Commands =
            [
                new AppCommandDefinition("edit", "command.edit", "menu", false, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("copy", "command.copy", "edit", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("paste", "command.paste", "edit", true, true, RulesetDefaults.Sr5),
                new AppCommandDefinition("help", "command.help", "menu", false, true, RulesetDefaults.Sr5)
            ]
        };

        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.ToggleMenuAsync("edit", CancellationToken.None);

        Assert.AreEqual("edit", presenter.State.OpenMenuId);
        Assert.IsNull(presenter.State.LastCommandId);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNull(presenter.State.Notice);
    }

    [TestMethod]
    public async Task SelectTabAsync_rejects_disabled_tabs()
    {
        var client = new ShellClientStub
        {
            NavigationTabs =
            [
                new NavigationTabDefinition("tab-enabled", "Enabled", "profile", "character", true, true, RulesetDefaults.Sr5),
                new NavigationTabDefinition("tab-disabled", "Disabled", "profile", "character", true, false, RulesetDefaults.Sr5)
            ]
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SelectTabAsync("tab-disabled", CancellationToken.None);

        Assert.AreEqual("Tab 'tab-disabled' is disabled.", presenter.State.Error);
        Assert.AreEqual("tab-enabled", presenter.State.ActiveTabId);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_requires_workspace_for_workspace_scoped_commands()
    {
        var presenter = new ShellPresenter(new ShellClientStub());
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.ExecuteCommandAsync("save_character", CancellationToken.None);

        Assert.AreEqual("Command 'save_character' is disabled in the current shell state.", presenter.State.Error);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_menu_command_updates_open_menu_and_last_command()
    {
        var presenter = new ShellPresenter(new ShellClientStub());
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.ExecuteCommandAsync("file", CancellationToken.None);

        Assert.AreEqual("file", presenter.State.OpenMenuId);
        Assert.AreEqual("file", presenter.State.LastCommandId);
        Assert.IsNull(presenter.State.Error);
        Assert.IsNull(presenter.State.Notice);
    }

    [TestMethod]
    public async Task SetPreferredRulesetAsync_updates_active_ruleset_when_no_workspace_is_open()
    {
        var client = new ShellClientStub
        {
            Workspaces = Array.Empty<WorkspaceListItem>()
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SetPreferredRulesetAsync("sr6", CancellationToken.None);

        Assert.AreEqual("sr6", presenter.State.PreferredRulesetId);
        Assert.AreEqual("sr6", presenter.State.ActiveRulesetId);
        CollectionAssert.Contains(client.RequestedBootstrapRulesets, "sr6");
        CollectionAssert.Contains(client.RequestedCommandRulesets, "sr6");
        CollectionAssert.Contains(client.RequestedNavigationRulesets, "sr6");
        Assert.AreEqual("sr6", client.Preferences.PreferredRulesetId);
    }

    [TestMethod]
    public async Task SetPreferredRulesetAsync_does_not_override_active_workspace_ruleset()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr5", "SR5 Character", "SR5", now, RulesetDefaults.Sr5)
            ],
            Session = new ShellSessionState(ActiveWorkspaceId: "ws-sr5")
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SetPreferredRulesetAsync("sr6", CancellationToken.None);

        Assert.AreEqual("sr6", presenter.State.PreferredRulesetId);
        Assert.AreEqual("sr5", presenter.State.ActiveRulesetId);
        string[] expectedSr5BootstrapRulesets = ["sr5"];
        string?[] expectedSr5Rulesets = ["sr5"];
        CollectionAssert.AreEqual(expectedSr5BootstrapRulesets, client.RequestedBootstrapRulesets);
        CollectionAssert.AreEqual(expectedSr5Rulesets, client.RequestedCommandRulesets);
        CollectionAssert.AreEqual(expectedSr5Rulesets, client.RequestedNavigationRulesets);
        Assert.AreEqual("sr6", client.Preferences.PreferredRulesetId);
    }

    [TestMethod]
    public async Task InitializeAsync_uses_saved_preferred_ruleset_when_no_workspace_is_open()
    {
        var client = new ShellClientStub
        {
            Workspaces = Array.Empty<WorkspaceListItem>(),
            Preferences = new ShellPreferences("sr6")
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.AreEqual("sr6", presenter.State.PreferredRulesetId);
        Assert.AreEqual("sr6", presenter.State.ActiveRulesetId);
        string[] expectedSr6BootstrapRulesets = ["sr6"];
        CollectionAssert.AreEqual(expectedSr6BootstrapRulesets, client.RequestedBootstrapRulesets);
    }

    [TestMethod]
    public async Task InitializeAsync_restores_active_tab_from_bootstrap_session()
    {
        var client = new ShellClientStub
        {
            Workspaces = Array.Empty<WorkspaceListItem>(),
            NavigationTabs =
            [
                new NavigationTabDefinition("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5),
                new NavigationTabDefinition("tab-rules", "Rules", "rules", "character", true, true, RulesetDefaults.Sr5)
            ],
            Session = new ShellSessionState(ActiveTabId: "tab-rules")
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.AreEqual("tab-rules", presenter.State.ActiveTabId);
    }

    [TestMethod]
    public async Task InitializeAsync_keeps_shell_only_tab_when_workspace_list_is_open_without_active_selection()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now, RulesetDefaults.Sr6)
            ],
            Session = new ShellSessionState(ActiveTabId: "tab-rules")
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.IsNull(presenter.State.ActiveWorkspaceId);
        Assert.AreEqual("tab-rules", presenter.State.ActiveTabId);
        Assert.AreEqual(RulesetDefaults.Sr5, presenter.State.ActiveRulesetId);
        Assert.HasCount(1, presenter.State.OpenWorkspaces);
        Assert.AreEqual("ws-sr6", presenter.State.OpenWorkspaces[0].Id.Value);
    }

    [TestMethod]
    public async Task InitializeAsync_replaces_shell_only_workspace_tab_with_runner_visible_default()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now, RulesetDefaults.Sr6)
            ],
            Session = new ShellSessionState(
                ActiveWorkspaceId: "ws-sr6",
                ActiveTabId: "tab-rules",
                ActiveTabsByWorkspace: new Dictionary<string, string>
                {
                    ["ws-sr6"] = "tab-rules"
                })
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.AreEqual("ws-sr6", presenter.State.ActiveWorkspaceId?.Value);
        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.IsNotEmpty(client.SavedSessions);
        Assert.AreEqual("ws-sr6", client.SavedSessions[^1].ActiveWorkspaceId);
        Assert.AreEqual("tab-info", client.SavedSessions[^1].ActiveTabId);
        Assert.IsNotNull(client.SavedSessions[^1].ActiveTabsByWorkspace);
        Assert.AreEqual("tab-info", client.SavedSessions[^1].ActiveTabsByWorkspace!["ws-sr6"]);
    }

    [TestMethod]
    public async Task InitializeAsync_projects_workflow_metadata_from_bootstrap_snapshot()
    {
        var client = new ShellClientStub
        {
            WorkflowDefinitions =
            [
                new WorkflowDefinition(
                    WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
                    Title: "Career Workbench",
                    SurfaceIds: ["career.main"],
                    RequiresOpenWorkspace: true)
            ],
            WorkflowSurfaces =
            [
                new WorkflowSurfaceDefinition(
                    SurfaceId: "career.main",
                    WorkflowId: WorkflowDefinitionIds.CareerWorkbench,
                    Kind: WorkflowSurfaceKinds.Workbench,
                    RegionId: ShellRegionIds.SectionPane,
                    LayoutToken: WorkflowLayoutTokens.CareerWorkbench,
                    ActionIds: ["career.refresh"])
            ]
        };
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.IsNotNull(presenter.State.WorkflowDefinitions);
        Assert.IsNotNull(presenter.State.WorkflowSurfaces);
        Assert.HasCount(1, presenter.State.WorkflowDefinitions);
        Assert.HasCount(1, presenter.State.WorkflowSurfaces);
        Assert.AreEqual(WorkflowDefinitionIds.CareerWorkbench, presenter.State.WorkflowDefinitions[0].WorkflowId);
        Assert.AreEqual(WorkflowDefinitionIds.CareerWorkbench, presenter.State.WorkflowSurfaces[0].WorkflowId);
    }

    [TestMethod]
    public async Task InitializeAsync_projects_active_runtime_from_bootstrap_snapshot()
    {
        var client = new ShellClientStub();
        client.ActiveRuntimesByRuleset[RulesetDefaults.Sr5] = new ActiveRuntimeStatusProjection(
            ProfileId: "official.sr5.core",
            Title: "Official SR5 Core",
            RulesetId: RulesetDefaults.Sr5,
            RuntimeFingerprint: "sha256:sr5-runtime",
            InstallState: ArtifactInstallStates.Available,
            RulePackCount: 1,
            ProviderBindingCount: 2,
            WarningCount: 1);
        var presenter = new ShellPresenter(client);

        await presenter.InitializeAsync(CancellationToken.None);

        Assert.IsNotNull(presenter.State.ActiveRuntime);
        Assert.AreEqual("official.sr5.core", presenter.State.ActiveRuntime.ProfileId);
        Assert.AreEqual("sha256:sr5-runtime", presenter.State.ActiveRuntime.RuntimeFingerprint);
        Assert.AreEqual(1, presenter.State.ActiveRuntime.WarningCount);
    }

    [TestMethod]
    public async Task SetPreferredRulesetAsync_persists_preference_via_runtime_client()
    {
        var client = new ShellClientStub
        {
            Workspaces = Array.Empty<WorkspaceListItem>()
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SetPreferredRulesetAsync("sr6", CancellationToken.None);

        Assert.HasCount(1, client.SavedPreferences);
        Assert.AreEqual("sr6", client.SavedPreferences[0].PreferredRulesetId);
    }

    [TestMethod]
    public async Task SetPreferredRulesetAsync_keeps_shell_only_tab_when_workspace_list_is_open_without_active_selection()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now, RulesetDefaults.Sr6)
            ],
            Session = new ShellSessionState(ActiveTabId: "tab-rules")
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SetPreferredRulesetAsync("sr6", CancellationToken.None);

        Assert.IsNull(presenter.State.ActiveWorkspaceId);
        Assert.AreEqual(RulesetDefaults.Sr6, presenter.State.PreferredRulesetId);
        Assert.AreEqual(RulesetDefaults.Sr6, presenter.State.ActiveRulesetId);
        Assert.AreEqual("tab-rules", presenter.State.ActiveTabId);
        Assert.IsEmpty(client.SavedSessions);
        Assert.AreEqual(RulesetDefaults.Sr6, client.Preferences.PreferredRulesetId);
        CollectionAssert.Contains(client.RequestedBootstrapRulesets, RulesetDefaults.Sr6);
    }

    [TestMethod]
    public async Task SelectTabAsync_persists_active_tab_in_shell_session()
    {
        var client = new ShellClientStub();
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SelectTabAsync("tab-info", CancellationToken.None);

        Assert.IsNotEmpty(client.SavedSessions);
        Assert.AreEqual("tab-info", client.SavedSessions[^1].ActiveTabId);
    }

    [TestMethod]
    public async Task SelectTabAsync_persists_workspace_tab_map_for_active_workspace()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-1", "Runner", "R1", now)
            ],
            Session = new ShellSessionState(ActiveWorkspaceId: "ws-1")
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SelectTabAsync("tab-gear", CancellationToken.None);

        Assert.IsNotEmpty(client.SavedSessions);
        Assert.IsNotNull(client.SavedSessions[^1].ActiveTabsByWorkspace);
        Assert.AreEqual("tab-gear", client.SavedSessions[^1].ActiveTabsByWorkspace!["ws-1"]);
    }

    [TestMethod]
    public async Task SelectTabAsync_rejects_shell_only_tab_when_workspace_is_active()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-1", "Runner", "R1", now)
            ],
            Session = new ShellSessionState(ActiveWorkspaceId: "ws-1")
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);
        int savedSessionCount = client.SavedSessions.Count;

        await presenter.SelectTabAsync("tab-rules", CancellationToken.None);

        Assert.AreEqual("Tab 'tab-rules' is unavailable while a dossier is active.", presenter.State.Error);
        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.AreEqual(savedSessionCount, client.SavedSessions.Count);
    }

    [TestMethod]
    public async Task SyncWorkspaceContextAsync_restores_workspace_specific_tab_when_switching_workspaces()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-1", "One", "ONE", now.AddMinutes(-5)),
                CreateWorkspace("ws-2", "Two", "TWO", now.AddMinutes(-1))
            ],
            Session = new ShellSessionState(
                ActiveWorkspaceId: "ws-1",
                ActiveTabId: "tab-info",
                ActiveTabsByWorkspace: new Dictionary<string, string>
                {
                    ["ws-1"] = "tab-info",
                    ["ws-2"] = "tab-gear"
                })
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SyncWorkspaceContextAsync(new CharacterWorkspaceId("ws-2"), CancellationToken.None);

        Assert.AreEqual("tab-gear", presenter.State.ActiveTabId);
    }

    [TestMethod]
    public async Task SyncWorkspaceContextAsync_does_not_persist_shell_only_tab_when_activating_workspace_from_shell_posture()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now, RulesetDefaults.Sr6)
            ],
            Session = new ShellSessionState(ActiveTabId: "tab-rules")
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        await presenter.SyncWorkspaceContextAsync(new CharacterWorkspaceId("ws-sr6"), CancellationToken.None);

        Assert.AreEqual("ws-sr6", presenter.State.ActiveWorkspaceId?.Value);
        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.IsNotEmpty(client.SavedSessions);
        Assert.AreEqual("tab-info", client.SavedSessions[^1].ActiveTabId);
        Assert.IsNotNull(client.SavedSessions[^1].ActiveTabsByWorkspace);
        Assert.AreEqual("tab-info", client.SavedSessions[^1].ActiveTabsByWorkspace!["ws-sr6"]);
    }

    [TestMethod]
    public async Task SyncWorkspaceContextAsync_clears_missing_active_workspace_and_restores_preferred_sr6_shell()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-sr5", "SR5 Character", "SR5", now, RulesetDefaults.Sr5)
            ],
            Preferences = new ShellPreferences("sr6"),
            Session = new ShellSessionState(
                ActiveWorkspaceId: "ws-sr5",
                ActiveTabId: "tab-attributes",
                ActiveTabsByWorkspace: new Dictionary<string, string>
                {
                    ["ws-sr5"] = "tab-attributes"
                })
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        client.Workspaces = Array.Empty<WorkspaceListItem>();

        await presenter.SyncWorkspaceContextAsync(new CharacterWorkspaceId("ws-sr5"), CancellationToken.None);

        Assert.IsNull(presenter.State.ActiveWorkspaceId);
        Assert.AreEqual("sr6", presenter.State.PreferredRulesetId);
        Assert.AreEqual("sr6", presenter.State.ActiveRulesetId);
        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.IsEmpty(presenter.State.OpenWorkspaces);
        Assert.IsNotEmpty(client.SavedSessions);
        Assert.IsNull(client.SavedSessions[^1].ActiveWorkspaceId);
        Assert.AreEqual("tab-info", client.SavedSessions[^1].ActiveTabId);
        Assert.AreEqual("sr6", client.RequestedBootstrapRulesets[^1]);
        Assert.AreEqual("sr6", client.RequestedCommandRulesets[^1]);
        Assert.AreEqual("sr6", client.RequestedNavigationRulesets[^1]);
    }

    [TestMethod]
    public async Task SyncWorkspaceContextAsync_does_not_auto_select_replacement_workspace_when_requested_workspace_is_missing()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-old", "Old Character", "OLD", now.AddMinutes(-10), RulesetDefaults.Sr5),
                CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now.AddMinutes(-5), "sr6")
            ],
            Session = new ShellSessionState(
                ActiveWorkspaceId: "ws-old",
                ActiveTabId: "tab-attributes",
                ActiveTabsByWorkspace: new Dictionary<string, string>
                {
                    ["ws-old"] = "tab-attributes",
                    ["ws-sr6"] = "tab-rules"
                })
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        client.Workspaces =
        [
            CreateWorkspace("ws-sr6", "SR6 Character", "SR6", now.AddMinutes(-1), "sr6")
        ];

        await presenter.SyncWorkspaceContextAsync(new CharacterWorkspaceId("ws-old"), CancellationToken.None);

        Assert.IsNull(presenter.State.ActiveWorkspaceId);
        Assert.AreEqual(RulesetDefaults.Sr5, presenter.State.ActiveRulesetId);
        Assert.AreEqual("tab-info", presenter.State.ActiveTabId);
        Assert.HasCount(1, presenter.State.OpenWorkspaces);
        Assert.AreEqual("ws-sr6", presenter.State.OpenWorkspaces[0].Id.Value);
        Assert.IsNotEmpty(client.SavedSessions);
        Assert.IsNull(client.SavedSessions[^1].ActiveWorkspaceId);
        Assert.AreEqual("tab-info", client.SavedSessions[^1].ActiveTabId);
        Assert.AreEqual(RulesetDefaults.Sr5, client.RequestedBootstrapRulesets[^1]);
    }

    [TestMethod]
    public async Task SyncOverviewFeedback_updates_shell_feedback_and_saved_workspace_status()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new ShellClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-1", "Runner", "R1", now, hasSavedWorkspace: false)
            ],
            Session = new ShellSessionState(ActiveWorkspaceId: "ws-1")
        };
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(CancellationToken.None);

        presenter.SyncOverviewFeedback(new ShellOverviewFeedback(
            OpenWorkspaces:
            [
                new ShellWorkspaceState(
                    Id: new CharacterWorkspaceId("ws-1"),
                    Name: "Runner",
                    Alias: "R1",
                    LastOpenedUtc: now,
                    RulesetId: RulesetDefaults.Sr5,
                    HasSavedWorkspace: true)
            ],
            Notice: "Workspace saved.",
            Error: null,
            LastCommandId: "save_character"));

        Assert.IsTrue(presenter.State.OpenWorkspaces[0].HasSavedWorkspace);
        Assert.AreEqual("Workspace saved.", presenter.State.Notice);
        Assert.AreEqual("save_character", presenter.State.LastCommandId);
        Assert.AreEqual("ws-1", presenter.State.ActiveWorkspaceId?.Value);
    }

    [TestMethod]
    [DataRow("owner-b")]
    [DataRow("owner-aba")]
    [DataRow("unchanged")]
    public async Task Initialize_retains_original_bootstrap_owner_through_delayed_response(string transition)
    {
        BoundShellClient client = new() { HoldBootstrap = true };
        ShellPresenter presenter = new(client);
        OwnerContextStamp original = client.CaptureOwnerContext();
        Task pending = presenter.InitializeAsync(CancellationToken.None);
        try
        {
            await client.BootstrapReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(pending.IsCompleted);
            client.SwitchOwner(transition);
            client.ReleaseBootstrap.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (transition == "unchanged")
            {
                Assert.IsNull(presenter.State.Error);
                Assert.AreEqual(original, presenter.State.OwnerContext);
                Assert.HasCount(1, presenter.State.OpenWorkspaces);
                Assert.HasCount(1, client.SavedSessions);
            }
            else
            {
                Assert.IsNotNull(presenter.State.Error);
                Assert.IsNull(presenter.State.OwnerContext);
                Assert.IsEmpty(presenter.State.OpenWorkspaces);
                Assert.IsEmpty(client.SavedSessions);
            }
        }
        finally
        {
            client.ReleaseBootstrap.TrySetResult();
            await ObserveOwnerTaskAsync(pending);
        }
    }

    [TestMethod]
    [DataRow("owner-b")]
    [DataRow("owner-aba")]
    [DataRow("unchanged")]
    public async Task Initial_session_save_acquires_the_original_bootstrap_epoch_after_admission(string transition)
    {
        BoundShellClient client = new() { HoldSave = true };
        ShellPresenter presenter = new(client);
        OwnerContextStamp original = client.CaptureOwnerContext();
        Task pending = presenter.InitializeAsync(CancellationToken.None);
        try
        {
            await client.SaveReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(pending.IsCompleted);
            client.SwitchOwner(transition);
            client.ReleaseSave.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(original, client.RequestedSaveOwner);
            if (transition == "unchanged")
            {
                Assert.HasCount(1, client.SavedSessions);
                Assert.AreEqual(original, presenter.State.OwnerContext);
                Assert.IsNull(presenter.State.Error);
            }
            else
            {
                Assert.IsEmpty(client.SavedSessions, "No new owner may receive an earlier owner's derived session.");
                Assert.IsNull(presenter.State.OwnerContext);
                Assert.IsEmpty(presenter.State.OpenWorkspaces);
                Assert.IsNotNull(presenter.State.Error);
            }
        }
        finally
        {
            client.ReleaseSave.TrySetResult();
            await ObserveOwnerTaskAsync(pending);
        }
    }

    [TestMethod]
    [DataRow("tab", "owner-b")]
    [DataRow("tab", "owner-aba")]
    [DataRow("preferences", "owner-b")]
    [DataRow("preferences", "owner-aba")]
    [DataRow("workspace", "owner-b")]
    [DataRow("workspace", "owner-aba")]
    public async Task Display_derived_shell_actions_reject_changed_owner_and_clear_stale_state(string action, string transition)
    {
        BoundShellClient client = new();
        ShellPresenter presenter = new(client);
        await presenter.InitializeAsync(CancellationToken.None);
        Assert.IsNull(presenter.State.Error);
        OwnerContextStamp originalOwner = client.CaptureOwnerContext();
        int originalSaves = client.SavedSessions.Count;
        client.SwitchOwner(transition);
        await Assert.ThrowsAsync<InvalidOperationException>(() => action switch
        {
            "tab" => presenter.SelectTabAsync("tab-rules", CancellationToken.None),
            "preferences" => presenter.SetPreferredRulesetAsync("sr6", CancellationToken.None),
            _ => presenter.SyncWorkspaceContextAsync(originalOwner, new CharacterWorkspaceId("shared-workspace"), CancellationToken.None)
        });
        Assert.AreEqual(originalSaves, client.SavedSessions.Count);
        Assert.IsEmpty(client.SavedPreferences);
        Assert.IsEmpty(presenter.State.OpenWorkspaces);
        Assert.IsNull(presenter.State.OwnerContext);

        await presenter.InitializeAsync(CancellationToken.None);
        Assert.IsNull(presenter.State.Error, "Explicit reload must recover without preserving the old owner projection.");
        Assert.AreEqual(client.CaptureOwnerContext(), presenter.State.OwnerContext);
    }

    [TestMethod]
    [DataRow("tab", "owner-aba")]
    [DataRow("preferences", "owner-aba")]
    [DataRow("tab", "unchanged")]
    [DataRow("preferences", "unchanged")]
    public async Task Delayed_display_derived_save_does_not_recapture_the_new_owner(string action, string transition)
    {
        BoundShellClient client = new();
        ShellPresenter presenter = new(client);
        await presenter.InitializeAsync(CancellationToken.None);
        OwnerContextStamp original = client.CaptureOwnerContext();
        int originalSaves = client.SavedSessions.Count;
        client.HoldSave = true;
        Task pending = action == "tab"
            ? presenter.SelectTabAsync("tab-info", CancellationToken.None)
            : presenter.SetPreferredRulesetAsync("sr6", CancellationToken.None);
        try
        {
            await client.SaveReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(pending.IsCompleted);
            client.SwitchOwner(transition);
            client.ReleaseSave.TrySetResult();
            if (transition == "unchanged")
                await pending.WaitAsync(TimeSpan.FromSeconds(5));
            else
                await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(original, client.RequestedSaveOwner);
            if (transition != "unchanged")
            {
                Assert.AreEqual(originalSaves, client.SavedSessions.Count);
                Assert.IsEmpty(client.SavedPreferences);
                Assert.IsNull(presenter.State.OwnerContext);
                Assert.IsEmpty(presenter.State.OpenWorkspaces);
                Assert.IsNotNull(presenter.State.Error);
            }
            else if (action == "tab")
                Assert.AreEqual(originalSaves + 1, client.SavedSessions.Count);
            else
                Assert.HasCount(1, client.SavedPreferences);
        }
        finally
        {
            client.ReleaseSave.TrySetResult();
            await ObserveOwnerTaskAsync(pending);
        }
    }

    [TestMethod]
    [DataRow("owner-b")]
    [DataRow("owner-aba")]
    [DataRow("unchanged")]
    [DataRow("missing-stamp")]
    [DataRow("wrong-authority")]
    public async Task Presenter_itself_rejects_stale_or_unproven_bootstrap_from_custom_provider(string transition)
    {
        BoundShellClient client = new();
        OwnerContextStamp original = client.CaptureOwnerContext();
        ShellBootstrapSnapshot snapshot = await client.GetShellBootstrapAsync(original, null, CancellationToken.None);
        ShellBootstrapData data = new(snapshot.RulesetId, snapshot.Commands, snapshot.NavigationTabs,
            snapshot.Workspaces, snapshot.PreferredRulesetId, snapshot.ActiveRulesetId,
            snapshot.ActiveWorkspaceId, snapshot.ActiveTabId, snapshot.ActiveTabsByWorkspace)
        {
            OwnerContext = transition == "missing-stamp" ? null
                : transition == "wrong-authority" ? original with { AuthorityInstanceId = "another-install" }
                : original
        };
        HeldBootstrapProvider provider = new(data);
        ShellPresenter presenter = new(client, provider);
        Task pending = presenter.InitializeAsync(CancellationToken.None);
        try
        {
            await provider.Ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (transition is "owner-b" or "owner-aba") client.SwitchOwner(transition);
            provider.Release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(provider.ReturnedOriginalPayload, "The custom provider must actually return stale data; it has no owner check.");
            if (transition == "unchanged")
            {
                Assert.IsNull(presenter.State.Error);
                Assert.AreEqual(original, presenter.State.OwnerContext);
                Assert.HasCount(1, client.SavedSessions);
                await presenter.InitializeAsync(CancellationToken.None);
                Assert.AreEqual(original, presenter.State.OwnerContext, "Same-owner explicit reload remains supported.");
                Assert.IsNull(presenter.State.Error);
            }
            else
            {
                Assert.IsNotNull(presenter.State.Error);
                Assert.IsNull(presenter.State.OwnerContext);
                Assert.IsEmpty(presenter.State.OpenWorkspaces);
                Assert.IsEmpty(client.SavedSessions);
            }
        }
        finally
        {
            provider.Release.TrySetResult();
            await ObserveOwnerTaskAsync(pending);
        }
    }

    [TestMethod]
    [DataRow("tab", "owner-b", false)]
    [DataRow("tab", "owner-aba", false)]
    [DataRow("tab", "unchanged", false)]
    [DataRow("tab", "owner-b", true)]
    [DataRow("tab", "owner-aba", true)]
    [DataRow("tab", "unchanged", true)]
    [DataRow("preferences", "owner-b", false)]
    [DataRow("preferences", "owner-aba", false)]
    [DataRow("preferences", "unchanged", false)]
    [DataRow("preferences", "owner-b", true)]
    [DataRow("preferences", "owner-aba", true)]
    [DataRow("preferences", "unchanged", true)]
    public async Task Post_commit_owner_transition_clears_old_display_without_claiming_rollback(string action, string transition, bool throwAfterCommit)
    {
        BoundShellClient client = new();
        ShellPresenter presenter = new(client);
        await presenter.InitializeAsync(CancellationToken.None);
        OwnerContextStamp original = client.CaptureOwnerContext();
        int priorSessions = client.SavedSessions.Count;
        client.HoldAfterCommit = true;
        client.ThrowAfterCommit = throwAfterCommit;
        Task pending = action == "tab"
            ? presenter.SelectTabAsync("tab-info", CancellationToken.None)
            : presenter.SetPreferredRulesetAsync("sr6", CancellationToken.None);
        try
        {
            await client.CommitCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(pending.IsCompleted, "The actual write is complete but the save continuation is held.");
            Assert.AreEqual(original, client.CommittedOwner);
            Assert.AreEqual(priorSessions + (action == "tab" ? 1 : 0), client.SavedSessions.Count);
            Assert.AreEqual(action == "preferences" ? 1 : 0, client.SavedPreferences.Count);
            client.SwitchOwner(transition);
            client.ReleaseCommittedResponse.TrySetResult();
            if (transition != "unchanged")
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.IsNull(presenter.State.OwnerContext);
                Assert.IsEmpty(presenter.State.OpenWorkspaces);
                Assert.IsNotNull(presenter.State.Error);
            }
            else
            {
                if (throwAfterCommit)
                    await Assert.ThrowsAsync<System.IO.IOException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                else
                    await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(original, presenter.State.OwnerContext);
                Assert.HasCount(1, presenter.State.OpenWorkspaces);
            }
            // Completed original-owner writes are not undone or reported as no effect.
            Assert.AreEqual(priorSessions + (action == "tab" ? 1 : 0), client.SavedSessions.Count);
            Assert.AreEqual(action == "preferences" ? 1 : 0, client.SavedPreferences.Count);
        }
        finally
        {
            client.ReleaseCommittedResponse.TrySetResult();
            await ObserveOwnerTaskAsync(pending);
        }
    }

    [TestMethod]
    [DataRow("owner-b")]
    [DataRow("owner-aba")]
    [DataRow("unchanged")]
    public async Task Workspace_navigation_requires_the_IDs_origin_epoch_even_after_new_owner_shell_reload(string transition)
    {
        BoundShellClient client = new();
        ShellPresenter presenter = new(client);
        await presenter.InitializeAsync(CancellationToken.None);
        OwnerContextStamp originatingOwner = client.CaptureOwnerContext();
        CharacterWorkspaceId? originatingId = presenter.State.ActiveWorkspaceId;
        client.SwitchOwner(transition);
        await presenter.InitializeAsync(CancellationToken.None);
        int savedBefore = client.SavedSessions.Count;
        int listedBefore = client.BoundListCalls;
        if (transition == "unchanged")
        {
            await presenter.SyncWorkspaceContextAsync(originatingOwner, activeWorkspaceId: null, CancellationToken.None);
            Assert.IsNull(presenter.State.ActiveWorkspaceId, "Same-owner last-tab closure must still save and display the empty selection.");
            Assert.AreEqual(savedBefore + 1, client.SavedSessions.Count);
            await presenter.SyncWorkspaceContextAsync(originatingOwner, originatingId, CancellationToken.None);
            Assert.AreEqual(originatingId, presenter.State.ActiveWorkspaceId);
            Assert.AreEqual(savedBefore + 2, client.SavedSessions.Count);
            Assert.AreEqual(listedBefore + 2, client.BoundListCalls);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.SyncWorkspaceContextAsync(originatingOwner, originatingId, CancellationToken.None));
            Assert.AreEqual(savedBefore, client.SavedSessions.Count);
            Assert.AreEqual(listedBefore, client.BoundListCalls, "A matching workspace ID under the new owner must not authorize a stale navigation argument.");
        }
    }

    [TestMethod]
    public async Task Local_navigation_without_original_owner_does_not_infer_authority_from_shell_state()
    {
        BoundShellClient client = new();
        ShellPresenter presenter = new(client);
        await presenter.InitializeAsync(CancellationToken.None);
        int savedBefore = client.SavedSessions.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.SyncWorkspaceContextAsync(presenter.State.ActiveWorkspaceId, CancellationToken.None));
        Assert.AreEqual(0, client.BoundListCalls);
        Assert.AreEqual(savedBefore, client.SavedSessions.Count);
    }

    private sealed class HeldBootstrapProvider(ShellBootstrapData payload) : IShellBootstrapDataProvider
    {
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ReturnedOriginalPayload { get; private set; }
        public async Task<ShellBootstrapData> GetAsync(CancellationToken ct)
        {
            Ready.TrySetResult();
            await Release.Task.WaitAsync(ct).ConfigureAwait(false);
            ReturnedOriginalPayload = true;
            return payload;
        }
    }

    [TestMethod]
    [DataRow("owner-b")]
    [DataRow("owner-aba")]
    public async Task Late_old_initialization_cannot_clear_a_completed_new_owner_shell(string transition)
    {
        BoundShellClient client = new();
        OwnerContextStamp original = client.CaptureOwnerContext();
        ShellBootstrapSnapshot first = await client.GetShellBootstrapAsync(original, null, default);
        var held = new TaskCompletionSource<ShellBootstrapData>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        var provider = new CallbackBootstrapProvider(async () =>
        {
            if (Interlocked.Increment(ref calls) == 1) return await held.Task;
            OwnerContextStamp owner = client.CaptureOwnerContext();
            return ToBoundData(await client.GetShellBootstrapAsync(owner, null, default), owner);
        });
        var presenter = new ShellPresenter(client, provider);
        Task old = presenter.InitializeAsync(default);
        try
        {
            Assert.IsFalse(old.IsCompleted);
            client.SwitchOwner(transition);
            await presenter.InitializeAsync(default);
            ShellState current = presenter.State;
            Assert.AreEqual(client.CaptureOwnerContext(), current.OwnerContext);
            Assert.IsNull(current.Error);
            held.TrySetResult(ToBoundData(first, original));
            await old.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(current, presenter.State, "A stale failure must not erase the successful new-owner initialization.");
        }
        finally { held.TrySetResult(ToBoundData(first, original)); await ObserveOwnerTaskAsync(old); }
    }

    [TestMethod]
    public async Task Unbound_overview_feedback_cannot_inherit_the_new_shell_owners_stamp()
    {
        BoundShellClient client = new();
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(default);
        ShellState first = presenter.State;
        client.SwitchOwner("owner-b");
        await presenter.InitializeAsync(default);
        ShellState current = presenter.State;
        presenter.SyncOverviewFeedback(new ShellOverviewFeedback(first.OpenWorkspaces,
            "Owner A private mutation", "Owner A failure", "owner-a-command"));
        Assert.AreSame(current, presenter.State, "Unproven feedback was relabeled as the active owner.");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Unavailable_owner_feedback_retires_private_shell_without_throwing(bool throws)
    {
        BoundShellClient client = new();
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(default);
        ShellState original = presenter.State;
        Assert.IsNotEmpty(original.OpenWorkspaces);
        client.OwnerUnavailable = true;
        client.ThrowOnUnavailable = throws;
        presenter.SyncOverviewFeedback(new ShellOverviewFeedback(original.OpenWorkspaces,
            "Private old notice", "Private old failure", "private-command")
        {
            RosterOwnerContext = original.OwnerContext,
            FeedbackOwnerContext = original.OwnerContext
        });
        Assert.IsEmpty(presenter.State.OpenWorkspaces);
        Assert.IsNull(presenter.State.OwnerContext);
        Assert.IsNull(presenter.State.ActiveWorkspaceId);
        Assert.IsFalse(presenter.State.IsBusy);
        Assert.AreNotEqual("Private old failure", presenter.State.Error);
        client.OwnerUnavailable = false;
        client.SwitchOwner("owner-aba");
        await presenter.InitializeAsync(default);
        Assert.AreEqual(client.CaptureOwnerContext(), presenter.State.OwnerContext);
        Assert.IsNull(presenter.State.Error);
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public async Task Overview_feedback_facets_keep_independent_original_owner_provenance(bool rosterCurrent, bool feedbackCurrent)
    {
        BoundShellClient client = new();
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(default);
        OwnerContextStamp first = client.CaptureOwnerContext();
        client.SwitchOwner("owner-b");
        await presenter.InitializeAsync(default);
        ShellState current = presenter.State;
        OwnerContextStamp live = client.CaptureOwnerContext();
        ShellWorkspaceState row = current.OpenWorkspaces.Single() with { Name = "A new bound roster projection" };
        var feedback = new ShellOverviewFeedback([row], "A bound notice", "A bound error", "bound-command")
        {
            RosterOwnerContext = rosterCurrent ? live : first,
            FeedbackOwnerContext = feedbackCurrent ? live : first
        };
        presenter.SyncOverviewFeedback(feedback);
        Assert.AreEqual(rosterCurrent ? row : current.OpenWorkspaces.Single(), presenter.State.OpenWorkspaces.Single());
        Assert.AreEqual(feedbackCurrent ? feedback.Notice : current.Notice, presenter.State.Notice);
        Assert.AreEqual(feedbackCurrent ? feedback.Error : current.Error, presenter.State.Error);
        Assert.AreEqual(feedbackCurrent ? feedback.LastCommandId : current.LastCommandId, presenter.State.LastCommandId);
        Assert.AreEqual(live, presenter.State.OwnerContext);
    }

    [TestMethod]
    public async Task Stale_navigation_rejection_preserves_the_fresh_new_owner_shell()
    {
        BoundShellClient client = new();
        var presenter = new ShellPresenter(client);
        await presenter.InitializeAsync(default);
        OwnerContextStamp original = client.CaptureOwnerContext();
        var oldId = presenter.State.ActiveWorkspaceId;
        client.SwitchOwner("owner-b");
        await presenter.InitializeAsync(default);
        ShellState current = presenter.State;
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.SyncWorkspaceContextAsync(original, oldId, default));
        Assert.AreSame(current, presenter.State, "Rejecting old navigation erased another owner's valid shell.");
    }

    private static ShellBootstrapData ToBoundData(ShellBootstrapSnapshot snapshot, OwnerContextStamp owner)
        => new(snapshot.RulesetId, snapshot.Commands, snapshot.NavigationTabs, snapshot.Workspaces,
            snapshot.PreferredRulesetId, snapshot.ActiveRulesetId, snapshot.ActiveWorkspaceId,
            snapshot.ActiveTabId, snapshot.ActiveTabsByWorkspace, snapshot.WorkflowDefinitions,
            snapshot.WorkflowSurfaces, snapshot.ActiveRuntime) { OwnerContext = owner };

    private sealed class CallbackBootstrapProvider(Func<Task<ShellBootstrapData>> get) : IShellBootstrapDataProvider
    {
        public Task<ShellBootstrapData> GetAsync(CancellationToken ct) => get();
    }

    private static async Task ObserveOwnerTaskAsync(Task pending)
    {
        try { await pending.ConfigureAwait(false); }
        catch { /* Always join the underlying work without hiding the original assertion. */ }
    }

    // This fixture owns every transition and synchronous read/write under one gate.
    // Delays occur only outside that gate, before admission or after materialization.
    private sealed class BoundShellClient : ShellClientStub, IOwnerBoundShellStateClient
    {
        private readonly object _gate = new();
        private OwnerContextStamp _owner = new(new OwnerScope("owner-a"), "shell-test-install", 0);
        public bool HoldBootstrap { get; set; }
        public bool HoldSave { get; set; }
        public bool HoldAfterCommit { get; set; }
        public bool ThrowAfterCommit { get; set; }
        public bool OwnerUnavailable { get; set; }
        public bool ThrowOnUnavailable { get; set; }
        public int BoundListCalls { get; private set; }
        public OwnerContextStamp? CommittedOwner { get; private set; }
        public TaskCompletionSource CommitCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseCommittedResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public OwnerContextStamp? RequestedSaveOwner { get; private set; }
        public TaskCompletionSource BootstrapReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseBootstrap { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SaveReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BoundShellClient()
        {
            Workspaces = [CreateWorkspace("shared-workspace", "Owner A runner", "A", DateTimeOffset.UtcNow)];
            Session = new ShellSessionState("shared-workspace", "unknown-tab");
        }

        public OwnerContextStamp CaptureOwnerContext()
        {
            lock (_gate)
            {
                if (OwnerUnavailable && ThrowOnUnavailable) throw new InvalidOperationException("Owner unavailable.");
                return OwnerUnavailable ? default : _owner;
            }
        }

        public void SwitchOwner(string transition)
        {
            lock (_gate)
            {
                if (transition == "unchanged") return;
                _owner = new(new OwnerScope("owner-b"), _owner.AuthorityInstanceId, checked(_owner.TransitionRevision + 1));
                if (transition == "owner-aba")
                    _owner = new(new OwnerScope("owner-a"), _owner.AuthorityInstanceId, checked(_owner.TransitionRevision + 1));
                Workspaces = [CreateWorkspace("shared-workspace", $"{_owner.Owner.Value}:{_owner.TransitionRevision}", "", DateTimeOffset.UtcNow)];
            }
        }

        private void RequireOwner(OwnerContextStamp expected)
        {
            if (!expected.IsValid || expected != _owner)
                throw new InvalidOperationException("Original live owner lease is no longer available.");
        }

        public async Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(OwnerContextStamp ownerContext, string? rulesetId, CancellationToken ct)
        {
            ShellBootstrapSnapshot snapshot;
            lock (_gate)
            {
                RequireOwner(ownerContext);
                // Base fixture operations are all completed synchronous Tasks.
                snapshot = base.GetShellBootstrapAsync(rulesetId, ct).GetAwaiter().GetResult();
            }
            if (HoldBootstrap)
            {
                BootstrapReady.TrySetResult();
                await ReleaseBootstrap.Task.WaitAsync(ct).ConfigureAwait(false);
            }
            return snapshot;
        }

        public Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(OwnerContextStamp ownerContext, CancellationToken ct)
        {
            lock (_gate) { RequireOwner(ownerContext); BoundListCalls++; return Task.FromResult(Workspaces); }
        }

        public async Task SaveShellSessionAsync(OwnerContextStamp ownerContext, ShellSessionState session, CancellationToken ct)
        {
            await AwaitSaveAdmissionAsync(ownerContext, ct).ConfigureAwait(false);
            lock (_gate) { RequireOwner(ownerContext); base.SaveShellSessionAsync(session, ct).GetAwaiter().GetResult(); }
            await AwaitCommittedResponseAsync(ownerContext).ConfigureAwait(false);
        }

        public async Task SaveShellPreferencesAsync(OwnerContextStamp ownerContext, ShellPreferences preferences, CancellationToken ct)
        {
            await AwaitSaveAdmissionAsync(ownerContext, ct).ConfigureAwait(false);
            lock (_gate) { RequireOwner(ownerContext); base.SaveShellPreferencesAsync(preferences, ct).GetAwaiter().GetResult(); }
            await AwaitCommittedResponseAsync(ownerContext).ConfigureAwait(false);
        }

        private async Task AwaitCommittedResponseAsync(OwnerContextStamp ownerContext)
        {
            if (!HoldAfterCommit) return;
            CommittedOwner = ownerContext;
            CommitCompleted.TrySetResult();
            await ReleaseCommittedResponse.Task.ConfigureAwait(false);
            if (ThrowAfterCommit) throw new System.IO.IOException("Synthetic post-commit response failure; the write is already complete.");
        }

        private async Task AwaitSaveAdmissionAsync(OwnerContextStamp ownerContext, CancellationToken ct)
        {
            RequestedSaveOwner = ownerContext;
            if (!HoldSave) return;
            SaveReady.TrySetResult();
            await ReleaseSave.Task.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    private static WorkspaceListItem CreateWorkspace(
        string id,
        string name,
        string alias,
        DateTimeOffset lastUpdatedUtc,
        string rulesetId = RulesetDefaults.Sr5,
        bool hasSavedWorkspace = false)
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
            RulesetId: rulesetId,
            HasSavedWorkspace: hasSavedWorkspace);
    }

    private class ShellClientStub : IChummerClient
    {
        public IReadOnlyList<AppCommandDefinition> Commands { get; set; } = AppCommandCatalog.All;

        public IReadOnlyList<NavigationTabDefinition> NavigationTabs { get; set; } = NavigationTabCatalog.All;

        public IReadOnlyList<WorkflowDefinition> WorkflowDefinitions { get; set; } = [];

        public IReadOnlyList<WorkflowSurfaceDefinition> WorkflowSurfaces { get; set; } = [];

        public Dictionary<string, ActiveRuntimeStatusProjection> ActiveRuntimesByRuleset { get; } = new(StringComparer.Ordinal);

        public IReadOnlyList<WorkspaceListItem> Workspaces { get; set; } = Array.Empty<WorkspaceListItem>();

        public ShellPreferences Preferences { get; set; } = new(RulesetDefaults.Sr5);

        public ShellSessionState Session { get; set; } = ShellSessionState.Default;

        public List<ShellPreferences> SavedPreferences { get; } = new();

        public List<ShellSessionState> SavedSessions { get; } = new();

        public List<string?> RequestedCommandRulesets { get; } = new();

        public List<string?> RequestedNavigationRulesets { get; } = new();

        public List<string?> RequestedBootstrapRulesets { get; } = new();

        public Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct)
        {
            RequestedCommandRulesets.Add(rulesetId);
            return Task.FromResult(Commands);
        }

        public Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct)
        {
            RequestedNavigationRulesets.Add(rulesetId);
            return Task.FromResult(NavigationTabs);
        }

        public Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct) => Task.FromResult(Workspaces);

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

        public Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct)
            => Task.FromResult(Preferences);

        public Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct)
        {
            Preferences = new ShellPreferences(
                PreferredRulesetId: RulesetDefaults.NormalizeOptional(preferences.PreferredRulesetId) ?? string.Empty);
            SavedPreferences.Add(Preferences);
            return Task.CompletedTask;
        }

        public Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct)
            => Task.FromResult(Session);

        public Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct)
        {
            Session = new ShellSessionState(
                ActiveWorkspaceId: NormalizeWorkspaceId(session.ActiveWorkspaceId),
                ActiveTabId: NormalizeTabId(session.ActiveTabId),
                ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace));
            SavedSessions.Add(Session);
            return Task.CompletedTask;
        }

        public async Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct)
        {
            IReadOnlyList<WorkspaceListItem> workspaces = await ListWorkspacesAsync(ct);
            CharacterWorkspaceId? activeWorkspaceId = ResolveActiveWorkspaceId(workspaces, Session.ActiveWorkspaceId);
            string preferredRulesetId = RulesetDefaults.NormalizeOptional(Preferences.PreferredRulesetId) ?? string.Empty;
            string activeRulesetId = activeWorkspaceId is null
                ? preferredRulesetId
                : RulesetDefaults.NormalizeOptional(
                    workspaces.First(workspace => string.Equals(workspace.Id.Value, activeWorkspaceId.Value.Value, StringComparison.Ordinal)).RulesetId) ?? string.Empty;
            string effectiveRulesetId = string.IsNullOrWhiteSpace(rulesetId)
                ? activeRulesetId
                : RulesetDefaults.NormalizeRequired(rulesetId);
            RequestedBootstrapRulesets.Add(effectiveRulesetId);
            IReadOnlyList<AppCommandDefinition> commands = await GetCommandsAsync(effectiveRulesetId, ct);
            IReadOnlyList<NavigationTabDefinition> tabs = await GetNavigationTabsAsync(effectiveRulesetId, ct);
            ActiveRuntimeStatusProjection? activeRuntime = ActiveRuntimesByRuleset.GetValueOrDefault(effectiveRulesetId);
            return new ShellBootstrapSnapshot(
                RulesetId: effectiveRulesetId,
                Commands: commands,
                NavigationTabs: tabs,
                Workspaces: workspaces,
                PreferredRulesetId: preferredRulesetId,
                ActiveRulesetId: activeRulesetId,
                ActiveWorkspaceId: activeWorkspaceId,
                ActiveTabId: NormalizeTabId(Session.ActiveTabId),
                ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(Session.ActiveTabsByWorkspace),
                WorkflowDefinitions: WorkflowDefinitions,
                WorkflowSurfaces: WorkflowSurfaces,
                ActiveRuntime: activeRuntime);
        }

        public Task<RuntimeInspectorProjection?> GetRuntimeInspectorProfileAsync(string profileId, string? rulesetId, CancellationToken ct)
        {
            return Task.FromResult<RuntimeInspectorProjection?>(null);
        }

        public Task<MasterIndexResponse> GetMasterIndexAsync(CancellationToken ct)
            => Task.FromResult(new MasterIndexResponse(0, DateTimeOffset.UtcNow, [], "missing", 0, []));

        public Task<TranslatorLanguagesResponse> GetTranslatorLanguagesAsync(CancellationToken ct)
            => Task.FromResult(new TranslatorLanguagesResponse(0, []));

        public Task<IReadOnlyList<DesktopBuildPathSuggestion>> GetBuildPathSuggestionsAsync(string? rulesetId, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<DesktopBuildPathSuggestion>>([]);
        }

        public Task<DesktopBuildPathPreview?> GetBuildPathPreviewAsync(string buildKitId, CharacterWorkspaceId workspaceId, string? rulesetId, CancellationToken ct)
        {
            return Task.FromResult<DesktopBuildPathPreview?>(null);
        }

        public Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => throw new NotImplementedException();

        public Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterFileSummary> GetSummaryAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterValidationResult> ValidateAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterProfileSection> GetProfileAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterProgressSection> GetProgressAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterSkillsSection> GetSkillsAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterRulesSection> GetRulesAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterBuildSection> GetBuildAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterMovementSection> GetMovementAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterAwakeningSection> GetAwakeningAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(CharacterWorkspaceId id, UpdateWorkspaceMetadata command, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        private static string? NormalizeWorkspaceId(string? workspaceId)
        {
            return string.IsNullOrWhiteSpace(workspaceId)
                ? null
                : workspaceId.Trim();
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
            IReadOnlyList<WorkspaceListItem> workspaces,
            string? preferredWorkspaceId)
        {
            if (string.IsNullOrWhiteSpace(preferredWorkspaceId))
                return null;

            WorkspaceListItem? matchingWorkspace = workspaces.FirstOrDefault(workspace =>
                string.Equals(workspace.Id.Value, preferredWorkspaceId, StringComparison.Ordinal));
            return matchingWorkspace?.Id;
        }
    }
}
