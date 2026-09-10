#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Owners;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Owners;
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
public class ShellBootstrapDataProviderTests
{
    [TestMethod]
    public async Task GetAsync_reuses_cached_payload_within_bootstrap_window()
    {
        var client = new BootstrapClientStub();
        var provider = new ShellBootstrapDataProvider(client);

        await provider.GetAsync(CancellationToken.None);
        await provider.GetAsync(CancellationToken.None);

        Assert.AreEqual(1, client.GetShellBootstrapCalls);
    }

    [TestMethod]
    public async Task GetWorkspacesAsync_caches_authoritative_bootstrap_snapshot()
    {
        var client = new BootstrapClientStub();
        var provider = new ShellBootstrapDataProvider(client);

        await provider.GetWorkspacesAsync(CancellationToken.None);
        await provider.GetWorkspacesAsync(CancellationToken.None);

        Assert.AreEqual(1, client.GetShellBootstrapCalls);
        Assert.AreEqual(0, client.ListWorkspacesCalls);
        Assert.AreEqual(0, client.GetShellPreferencesCalls);
        Assert.AreEqual(0, client.GetShellSessionCalls);
        Assert.AreEqual(0, client.GetCommandsCalls);
        Assert.AreEqual(0, client.GetNavigationTabsCalls);
    }

    [TestMethod]
    public async Task GetWorkspacesAsync_preserves_preferred_ruleset_from_bootstrap_snapshot()
    {
        var client = new BootstrapClientStub
        {
            Preferences = new ShellPreferences("sr6")
        };
        var provider = new ShellBootstrapDataProvider(client);

        await provider.GetWorkspacesAsync(CancellationToken.None);
        ShellBootstrapData bootstrap = await provider.GetAsync("sr6", CancellationToken.None);

        Assert.AreEqual(1, client.GetShellBootstrapCalls);
        Assert.AreEqual(0, client.GetShellPreferencesCalls);
        Assert.AreEqual(0, client.GetShellSessionCalls);
        Assert.AreEqual("sr6", bootstrap.PreferredRulesetId);
    }

    [TestMethod]
    public async Task GetAsync_includes_active_workspace_from_bootstrap_snapshot()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new BootstrapClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-old", now.AddMinutes(-25), RulesetDefaults.Sr5),
                CreateWorkspace("ws-new", now.AddMinutes(-5), "sr6")
            ],
            Preferences = new ShellPreferences(RulesetDefaults.Sr5),
            Session = new ShellSessionState("ws-old")
        };
        var provider = new ShellBootstrapDataProvider(client);

        ShellBootstrapData bootstrap = await provider.GetAsync(CancellationToken.None);

        Assert.AreEqual("ws-old", bootstrap.ActiveWorkspaceId?.Value);
        Assert.AreEqual(RulesetDefaults.Sr5, bootstrap.ActiveRulesetId);
    }

    [TestMethod]
    public async Task GetAsync_does_not_infer_active_workspace_from_workspace_order_when_session_is_empty()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var client = new BootstrapClientStub
        {
            Workspaces =
            [
                CreateWorkspace("ws-old", now.AddMinutes(-25), RulesetDefaults.Sr5),
                CreateWorkspace("ws-new", now.AddMinutes(-5), "sr6")
            ],
            Preferences = new ShellPreferences(RulesetDefaults.Sr5),
            Session = ShellSessionState.Default
        };
        var provider = new ShellBootstrapDataProvider(client);

        ShellBootstrapData bootstrap = await provider.GetAsync(CancellationToken.None);

        Assert.IsNull(bootstrap.ActiveWorkspaceId);
        Assert.AreEqual(RulesetDefaults.Sr5, bootstrap.ActiveRulesetId);
    }

    [TestMethod]
    public async Task GetAsync_includes_active_tab_from_bootstrap_snapshot()
    {
        var client = new BootstrapClientStub
        {
            Session = new ShellSessionState(ActiveTabId: "tab-rules")
        };
        var provider = new ShellBootstrapDataProvider(client);

        ShellBootstrapData bootstrap = await provider.GetAsync(CancellationToken.None);

        Assert.AreEqual("tab-rules", bootstrap.ActiveTabId);
    }

    [TestMethod]
    public async Task GetAsync_includes_workspace_tab_map_from_bootstrap_snapshot()
    {
        var client = new BootstrapClientStub
        {
            Session = new ShellSessionState(
                ActiveTabsByWorkspace: new Dictionary<string, string>
                {
                    ["ws-a"] = "tab-info",
                    ["ws-b"] = "tab-rules"
                })
        };
        var provider = new ShellBootstrapDataProvider(client);

        ShellBootstrapData bootstrap = await provider.GetAsync(CancellationToken.None);

        Assert.IsNotNull(bootstrap.ActiveTabsByWorkspace);
        Assert.AreEqual("tab-info", bootstrap.ActiveTabsByWorkspace!["ws-a"]);
        Assert.AreEqual("tab-rules", bootstrap.ActiveTabsByWorkspace["ws-b"]);
    }

    [TestMethod]
    public async Task GetAsync_includes_workflow_metadata_from_bootstrap_snapshot()
    {
        var client = new BootstrapClientStub
        {
            WorkflowDefinitions =
            [
                new WorkflowDefinition(
                    WorkflowId: WorkflowDefinitionIds.SessionDashboard,
                    Title: "Session Dashboard",
                    SurfaceIds: ["session.summary"],
                    RequiresOpenWorkspace: true,
                    MobileOptimized: true)
            ],
            WorkflowSurfaces =
            [
                new WorkflowSurfaceDefinition(
                    SurfaceId: "session.summary",
                    WorkflowId: WorkflowDefinitionIds.SessionDashboard,
                    Kind: WorkflowSurfaceKinds.Dashboard,
                    RegionId: ShellRegionIds.SectionPane,
                    LayoutToken: WorkflowLayoutTokens.SessionDashboard,
                    ActionIds: ["session.refresh"])
            ]
        };
        var provider = new ShellBootstrapDataProvider(client);

        ShellBootstrapData bootstrap = await provider.GetAsync(CancellationToken.None);

        Assert.IsNotNull(bootstrap.WorkflowDefinitions);
        Assert.IsNotNull(bootstrap.WorkflowSurfaces);
        Assert.HasCount(1, bootstrap.WorkflowDefinitions);
        Assert.HasCount(1, bootstrap.WorkflowSurfaces);
        Assert.AreEqual(WorkflowDefinitionIds.SessionDashboard, bootstrap.WorkflowDefinitions[0].WorkflowId);
        Assert.AreEqual(WorkflowDefinitionIds.SessionDashboard, bootstrap.WorkflowSurfaces[0].WorkflowId);
    }

    [TestMethod]
    public async Task GetAsync_includes_active_runtime_from_bootstrap_snapshot()
    {
        var client = new BootstrapClientStub
        {
            ActiveRuntime = new ActiveRuntimeStatusProjection(
                ProfileId: "official.sr5.core",
                Title: "Official SR5 Core",
                RulesetId: RulesetDefaults.Sr5,
                RuntimeFingerprint: "sha256:sr5-runtime",
                InstallState: ArtifactInstallStates.Available,
                RulePackCount: 1,
                ProviderBindingCount: 2,
                WarningCount: 1)
        };
        var provider = new ShellBootstrapDataProvider(client);

        ShellBootstrapData bootstrap = await provider.GetAsync(CancellationToken.None);

        Assert.IsNotNull(bootstrap.ActiveRuntime);
        Assert.AreEqual("official.sr5.core", bootstrap.ActiveRuntime.ProfileId);
        Assert.AreEqual("sha256:sr5-runtime", bootstrap.ActiveRuntime.RuntimeFingerprint);
        Assert.AreEqual(1, bootstrap.ActiveRuntime.WarningCount);
    }

    [TestMethod]
    public async Task Shared_provider_avoids_duplicate_startup_fetches_between_shell_and_overview()
    {
        var client = new BootstrapClientStub();
        var provider = new ShellBootstrapDataProvider(client);
        var shellPresenter = new ShellPresenter(client, provider);
        var overviewPresenter = new CharacterOverviewPresenter(client, bootstrapDataProvider: provider);

        await shellPresenter.InitializeAsync(CancellationToken.None);
        await overviewPresenter.InitializeAsync(CancellationToken.None);

        Assert.AreEqual(1, client.GetShellBootstrapCalls);
    }

    [TestMethod]
    public async Task GetAsync_caches_ruleset_scoped_bootstrap_snapshots()
    {
        var client = new BootstrapClientStub();
        var provider = new ShellBootstrapDataProvider(client);

        await provider.GetAsync("sr5", CancellationToken.None);
        await provider.GetAsync("sr6", CancellationToken.None);
        await provider.GetAsync("sr6", CancellationToken.None);

        Assert.AreEqual(2, client.GetShellBootstrapCalls);

        string[] expectedRulesets = ["sr5", "sr6"];
        CollectionAssert.AreEquivalent(
            expectedRulesets,
            client.RequestedBootstrapRulesets
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
    }

    [TestMethod]
    [DataRow("owner-b", "sr5")]
    [DataRow("owner-aba", "sr5")]
    [DataRow("owner-b", null)]
    [DataRow("owner-aba", null)]
    public async Task Cached_bootstrap_never_reuses_a_previous_owner_epoch(string transition, string? initialRuleset)
    {
        OwnerBootstrapSource source = new();
        BootstrapClientStub client = new() { OwnerSource = source };
        ShellBootstrapDataProvider provider = new(client);
        ShellBootstrapData first = await provider.GetAsync(initialRuleset, CancellationToken.None);
        AssertOwnerProjection(first, source.Capture());

        source.SwitchOwner(transition);
        OwnerContextStamp expected = source.Capture();
        ShellBootstrapData next = await provider.GetAsync("sr5", CancellationToken.None);

        AssertOwnerProjection(next, expected);
        Assert.AreNotSame(first, next, "Ruleset/default aliases cannot reuse a prior owner's cached snapshot.");
        Assert.AreEqual(2, client.GetShellBootstrapCalls);
    }

    [TestMethod]
    public async Task Unchanged_owner_reuses_its_same_ruleset_bootstrap()
    {
        OwnerBootstrapSource source = new();
        BootstrapClientStub client = new() { OwnerSource = source };
        ShellBootstrapDataProvider provider = new(client);

        ShellBootstrapData first = await provider.GetAsync("sr5", CancellationToken.None);
        ShellBootstrapData second = await provider.GetAsync("sr5", CancellationToken.None);

        AssertOwnerProjection(second, source.Capture());
        Assert.AreSame(first, second);
        Assert.AreEqual(1, client.GetShellBootstrapCalls);
    }

    [TestMethod]
    [DataRow("owner-b")]
    [DataRow("owner-aba")]
    [DataRow("unchanged")]
    public async Task Inflight_bootstrap_cannot_publish_a_snapshot_after_its_owner_epoch_changed(string transition)
    {
        OwnerBootstrapSource source = new() { HoldFirstResponse = true };
        BootstrapClientStub client = new() { OwnerSource = source };
        ShellBootstrapDataProvider provider = new(client);
        using CancellationTokenSource cancellation = new();
        Task<ShellBootstrapData> pending = provider.GetAsync("sr5", cancellation.Token);
        try
        {
            await source.FirstResponseMaterialized.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(pending.IsCompleted, "The provider must await the held, already-materialized response.");
            OwnerContextStamp original = source.Capture();
            source.SwitchOwner(transition);
            source.ReleaseFirstResponse.TrySetResult();

            if (transition == "unchanged")
            {
                ShellBootstrapData result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                AssertOwnerProjection(result, original);
                Assert.AreEqual(1, client.GetShellBootstrapCalls);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => pending.WaitAsync(TimeSpan.FromSeconds(5)),
                    "An in-flight read may not publish a response from the old owner epoch.");
                ShellBootstrapData fresh = await provider.GetAsync("sr5", cancellation.Token);
                AssertOwnerProjection(fresh, source.Capture());
                Assert.AreEqual(2, client.GetShellBootstrapCalls, "Rejected data must not seed the new epoch's cache.");
            }
        }
        finally
        {
            source.ReleaseFirstResponse.TrySetResult();
            cancellation.Cancel();
            await ObserveBootstrapCompletionAsync(pending);
        }
    }

    [TestMethod]
    [DataRow("owner-b")]
    [DataRow("owner-aba")]
    public async Task Queued_bootstrap_request_cannot_inherit_another_owner_epochs_inflight_result(string transition)
    {
        OwnerBootstrapSource source = new() { HoldFirstResponse = true };
        BootstrapClientStub client = new() { OwnerSource = source };
        ShellBootstrapDataProvider provider = new(client);
        using CancellationTokenSource cancellation = new();
        Task<ShellBootstrapData> first = provider.GetAsync("sr5", cancellation.Token);
        Task<ShellBootstrapData>? second = null;
        try
        {
            await source.FirstResponseMaterialized.Task.WaitAsync(TimeSpan.FromSeconds(5));
            source.SwitchOwner(transition);
            OwnerContextStamp expected = source.Capture();
            second = provider.GetAsync("sr5", cancellation.Token);
            Assert.IsFalse(second.IsCompleted, "The new epoch's request must be observed while the first response is held.");
            source.ReleaseFirstResponse.TrySetResult();

            // The separate in-flight test asserts rejection of the original call. Here the
            // invariant is that the queued new-epoch caller never inherits its cached result.
            await ObserveBootstrapCompletionAsync(first);
            ShellBootstrapData result = await second.WaitAsync(TimeSpan.FromSeconds(5));
            AssertOwnerProjection(result, expected);
            Assert.AreEqual(2, client.GetShellBootstrapCalls);
        }
        finally
        {
            source.ReleaseFirstResponse.TrySetResult();
            cancellation.Cancel();
            try
            {
                await ObserveBootstrapCompletionAsync(first);
            }
            finally
            {
                if (second is not null)
                    await ObserveBootstrapCompletionAsync(second);
            }
        }
    }

    [TestMethod]
    public async Task Unbound_client_does_not_cache_owner_bearing_results_or_invent_a_stamp()
    {
        BootstrapClientStub target = new();
        IChummerClient client = System.Reflection.DispatchProxy.Create<IChummerClient, UnboundClientProxy>();
        ((UnboundClientProxy)(object)client).Target = target;
        ShellBootstrapDataProvider provider = new(client);
        ShellBootstrapData first = await provider.GetAsync("sr5", CancellationToken.None);
        ShellBootstrapData second = await provider.GetAsync("sr5", CancellationToken.None);
        Assert.AreNotSame(first, second);
        Assert.IsNull(first.OwnerContext);
        Assert.IsNull(second.OwnerContext);
        Assert.AreEqual(2, target.GetShellBootstrapCalls);
    }

    [TestMethod]
    public async Task Precanceled_bootstrap_does_not_capture_or_fetch()
    {
        BootstrapClientStub client = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new ShellBootstrapDataProvider(client).GetAsync(cancellation.Token));
        Assert.AreEqual(0, client.OwnerCaptures);
        Assert.AreEqual(0, client.GetShellBootstrapCalls);
    }

    [TestMethod]
    public async Task Invalid_owner_stamp_fails_closed_without_fetching()
    {
        BootstrapClientStub client = new() { InvalidOwner = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ShellBootstrapDataProvider(client).GetAsync(CancellationToken.None));
        Assert.AreEqual(0, client.GetShellBootstrapCalls);
    }

    public class UnboundClientProxy : System.Reflection.DispatchProxy
    {
        public IChummerClient Target { get; set; } = null!;
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args)
            => method!.Invoke(Target, args);
    }

    private static void AssertOwnerProjection(ShellBootstrapData bootstrap, OwnerContextStamp expected)
    {
        Assert.HasCount(1, bootstrap.Workspaces);
        Assert.AreEqual("shared-workspace", bootstrap.Workspaces[0].Id.Value,
            "Both owners intentionally use the same workspace ID; identity alone is not ownership.");
        Assert.AreEqual(OwnerMarker(expected), bootstrap.Workspaces[0].Summary.Name,
            "The provider returned workspace content belonging to a different owner/transition revision.");
        Assert.AreEqual(OwnerMarker(expected), bootstrap.ActiveTabId,
            "Owner-scoped saved shell state must come from the same original bootstrap authority.");
        Assert.AreEqual(OwnerMarker(expected), bootstrap.ActiveTabsByWorkspace!["shared-workspace"]);
    }

    private static string OwnerMarker(OwnerContextStamp stamp)
        => $"{stamp.Owner.Value}:revision-{stamp.TransitionRevision}";

    private static async Task ObserveBootstrapCompletionAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch { /* Join without replacing the original test observation failure. */ }
    }

    // Owns its owner/epoch transitions and atomically materializes each synthetic snapshot.
    // The response is held only AFTER leaving the state lock; no fake lease or await under it.
    private sealed class OwnerBootstrapSource
    {
        private readonly object _gate = new();
        private OwnerContextStamp _stamp = new(new OwnerScope("owner-a"), "bootstrap-test-install", 0);
        private int _reads;

        public bool HoldFirstResponse { get; init; }
        public TaskCompletionSource FirstResponseMaterialized { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstResponse { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public OwnerContextStamp Capture()
        {
            lock (_gate) return _stamp;
        }

        public void SwitchOwner(string transition)
        {
            lock (_gate)
            {
                if (transition == "unchanged") return;
                _stamp = new(new OwnerScope("owner-b"), _stamp.AuthorityInstanceId, checked(_stamp.TransitionRevision + 1));
                if (transition == "owner-aba")
                    _stamp = new(new OwnerScope("owner-a"), _stamp.AuthorityInstanceId, checked(_stamp.TransitionRevision + 1));
            }
        }

        public async Task<ShellBootstrapSnapshot> ReadAsync(OwnerContextStamp expected, string? rulesetId, CancellationToken ct)
        {
            ShellBootstrapSnapshot snapshot;
            int read;
            lock (_gate)
            {
                if (!expected.IsValid || expected != _stamp)
                    throw new InvalidOperationException("Original owner authority is no longer available.");
                read = ++_reads;
                string marker = OwnerMarker(_stamp);
                WorkspaceListItem workspace = CreateWorkspace("shared-workspace", DateTimeOffset.UtcNow, "sr5");
                workspace = workspace with { Summary = workspace.Summary with { Name = marker } };
                snapshot = new ShellBootstrapSnapshot(
                    RulesetId: "sr5", Commands: [], NavigationTabs: [], Workspaces: [workspace],
                    PreferredRulesetId: "sr5", ActiveRulesetId: "sr5",
                    ActiveWorkspaceId: workspace.Id, ActiveTabId: marker,
                    ActiveTabsByWorkspace: new Dictionary<string, string> { [workspace.Id.Value] = marker });
            }

            if (HoldFirstResponse && read == 1)
            {
                FirstResponseMaterialized.TrySetResult();
                await ReleaseFirstResponse.Task.WaitAsync(ct).ConfigureAwait(false);
            }
            return snapshot;
        }
    }

    private sealed class BootstrapClientStub : IChummerClient, IOwnerBoundShellStateClient
    {
        private readonly OwnerContextStamp _fixedOwner = new(new OwnerScope("fixture-owner"), Guid.NewGuid().ToString("N"), 0);
        public OwnerBootstrapSource? OwnerSource { get; init; }
        public bool InvalidOwner { get; init; }
        public int OwnerCaptures { get; private set; }

        public OwnerContextStamp CaptureOwnerContext()
        {
            OwnerCaptures++;
            return InvalidOwner ? default : OwnerSource?.Capture() ?? _fixedOwner;
        }

        public Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(OwnerContextStamp ownerContext, string? rulesetId, CancellationToken ct)
        {
            if (OwnerSource is not null)
            {
                GetShellBootstrapCalls++;
                return OwnerSource.ReadAsync(ownerContext, rulesetId, ct);
            }
            RequireOwner(ownerContext);
            return GetShellBootstrapAsync(rulesetId, ct);
        }

        private void RequireOwner(OwnerContextStamp expected)
        {
            if (!expected.IsValid || expected != CaptureOwnerContext())
                throw new InvalidOperationException("Original owner authority is no longer available.");
        }

        public Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(OwnerContextStamp ownerContext, CancellationToken ct)
        { RequireOwner(ownerContext); return ListWorkspacesAsync(ct); }
        public Task SaveShellSessionAsync(OwnerContextStamp ownerContext, ShellSessionState session, CancellationToken ct)
        { RequireOwner(ownerContext); return SaveShellSessionAsync(session, ct); }
        public Task SaveShellPreferencesAsync(OwnerContextStamp ownerContext, ShellPreferences preferences, CancellationToken ct)
        { RequireOwner(ownerContext); return SaveShellPreferencesAsync(preferences, ct); }
        public int GetShellBootstrapCalls { get; private set; }
        public int GetCommandsCalls { get; private set; }
        public int GetNavigationTabsCalls { get; private set; }
        public int ListWorkspacesCalls { get; private set; }
        public int GetShellPreferencesCalls { get; private set; }
        public int GetShellSessionCalls { get; private set; }
        public List<string> RequestedBootstrapRulesets { get; } = new();
        public IReadOnlyList<AppCommandDefinition> Commands { get; set; } = AppCommandCatalog.All;
        public IReadOnlyList<NavigationTabDefinition> NavigationTabs { get; set; } = NavigationTabCatalog.All;
        public IReadOnlyList<WorkflowDefinition> WorkflowDefinitions { get; set; } = [];
        public IReadOnlyList<WorkflowSurfaceDefinition> WorkflowSurfaces { get; set; } = [];
        public ActiveRuntimeStatusProjection? ActiveRuntime { get; set; }
        public ShellPreferences Preferences { get; set; } = new(RulesetDefaults.Sr5);
        public ShellSessionState Session { get; set; } = ShellSessionState.Default;
        public IReadOnlyList<WorkspaceListItem> Workspaces { get; set; } = Array.Empty<WorkspaceListItem>();

        public Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct)
        {
            GetShellPreferencesCalls++;
            return Task.FromResult(Preferences);
        }

        public Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct)
        {
            Preferences = new ShellPreferences(
                PreferredRulesetId: RulesetDefaults.NormalizeOptional(preferences.PreferredRulesetId) ?? string.Empty);
            return Task.CompletedTask;
        }

        public Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct)
        {
            GetShellSessionCalls++;
            return Task.FromResult(Session);
        }

        public Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct)
        {
            Session = new ShellSessionState(
                ActiveWorkspaceId: NormalizeWorkspaceId(session.ActiveWorkspaceId),
                ActiveTabId: NormalizeTabId(session.ActiveTabId),
                ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(session.ActiveTabsByWorkspace));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct)
        {
            GetCommandsCalls++;
            return Task.FromResult(Commands);
        }

        public Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct)
        {
            GetNavigationTabsCalls++;
            return Task.FromResult(NavigationTabs);
        }

        public Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct)
        {
            ListWorkspacesCalls++;
            return Task.FromResult(Workspaces);
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

        public Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct)
        {
            GetShellBootstrapCalls++;
            if (OwnerSource is not null)
                return OwnerSource.ReadAsync(OwnerSource.Capture(), rulesetId, ct);

            IReadOnlyList<WorkspaceListItem> workspaces = Workspaces;
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

            return Task.FromResult(new ShellBootstrapSnapshot(
                effectiveRulesetId,
                Commands,
                NavigationTabs,
                workspaces,
                PreferredRulesetId: preferredRulesetId,
                ActiveRulesetId: activeRulesetId,
                ActiveWorkspaceId: activeWorkspaceId,
                ActiveTabId: NormalizeTabId(Session.ActiveTabId),
                ActiveTabsByWorkspace: NormalizeWorkspaceTabMap(Session.ActiveTabsByWorkspace),
                WorkflowDefinitions: WorkflowDefinitions,
                WorkflowSurfaces: WorkflowSurfaces,
                ActiveRuntime: ActiveRuntime));
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
                string? normalizedWorkspaceId = string.IsNullOrWhiteSpace(workspaceId)
                    ? null
                    : workspaceId.Trim();
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

    private static WorkspaceListItem CreateWorkspace(
        string id,
        DateTimeOffset lastUpdatedUtc,
        string rulesetId)
    {
        return new WorkspaceListItem(
            Id: new CharacterWorkspaceId(id),
            Summary: new CharacterFileSummary(
                Name: id,
                Alias: string.Empty,
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "5",
                AppVersion: "5",
                Karma: 0m,
                Nuyen: 0m,
                Created: true),
            LastUpdatedUtc: lastUpdatedUtc,
            RulesetId: rulesetId);
    }
}
