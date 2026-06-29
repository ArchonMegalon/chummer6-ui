#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Blazor.Services;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class BlazorPublicEdgeWarmupServiceTests
{
    [TestMethod]
    public async Task StartAsync_warms_shell_overview_and_ruleset_bootstrap()
    {
        var shellPresenter = new RecordingShellPresenter();
        var overviewPresenter = new RecordingOverviewPresenter();
        var bootstrapProvider = new RecordingBootstrapProvider();

        BlazorPublicEdgeWarmupService service = CreateService(shellPresenter, overviewPresenter, bootstrapProvider);

        await service.WarmAsync(CancellationToken.None);

        Assert.AreEqual(1, shellPresenter.InitializeCalls);
        Assert.AreEqual(1, overviewPresenter.InitializeCalls);
        CollectionAssert.AreEqual(
            BlazorPublicEdgeWarmupService.WarmedRulesetIds,
            bootstrapProvider.RequestedRulesetIds.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_fails_open_when_warmup_dependency_throws()
    {
        var shellPresenter = new RecordingShellPresenter { ThrowOnInitialize = true };
        var overviewPresenter = new RecordingOverviewPresenter();
        var bootstrapProvider = new RecordingBootstrapProvider();

        BlazorPublicEdgeWarmupService service = CreateService(shellPresenter, overviewPresenter, bootstrapProvider);

        await service.WarmAsync(CancellationToken.None);

        Assert.AreEqual(1, shellPresenter.InitializeCalls);
        Assert.AreEqual(0, overviewPresenter.InitializeCalls);
        Assert.HasCount(0, bootstrapProvider.RequestedRulesetIds);
    }

    [TestMethod]
    public async Task StartAsync_returns_without_waiting_for_warmup_to_finish()
    {
        var shellPresenter = new RecordingShellPresenter { HoldInitializeUntilReleased = true };
        var overviewPresenter = new RecordingOverviewPresenter();
        var bootstrapProvider = new RecordingBootstrapProvider();
        BlazorPublicEdgeWarmupService service = CreateService(shellPresenter, overviewPresenter, bootstrapProvider);

        Task startTask = service.StartAsync(CancellationToken.None);

        Task completedTask = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromMilliseconds(250)));
        Assert.AreSame(startTask, completedTask);
        Assert.IsTrue(startTask.IsCompleted);
        await WaitUntilAsync(() => shellPresenter.InitializeCalls == 1);
        Assert.AreEqual(0, overviewPresenter.InitializeCalls);

        shellPresenter.ReleaseInitialize();
        await service.StopAsync(CancellationToken.None);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!predicate())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
        }
    }

    private static BlazorPublicEdgeWarmupService CreateService(
        RecordingShellPresenter shellPresenter,
        RecordingOverviewPresenter overviewPresenter,
        RecordingBootstrapProvider bootstrapProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IShellPresenter>(shellPresenter);
        services.AddSingleton<ICharacterOverviewPresenter>(overviewPresenter);
        services.AddSingleton<IShellBootstrapDataProvider>(bootstrapProvider);
        services.AddLogging();

        ServiceProvider provider = services.BuildServiceProvider();
        return new BlazorPublicEdgeWarmupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<BlazorPublicEdgeWarmupService>>());
    }

    private sealed class RecordingShellPresenter : IShellPresenter
    {
        public ShellState State { get; private set; } = ShellState.Empty;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public int InitializeCalls { get; private set; }

        public bool ThrowOnInitialize { get; set; }

        public bool HoldInitializeUntilReleased { get; set; }

        private readonly TaskCompletionSource _releaseInitialize = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("warmup shell failed");
            }

            if (HoldInitializeUntilReleased)
            {
                await _releaseInitialize.Task.WaitAsync(ct);
            }
        }

        public void ReleaseInitialize()
        {
            _releaseInitialize.TrySetResult();
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct) => Task.CompletedTask;

        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;

        public Task ToggleMenuAsync(string menuId, CancellationToken ct) => Task.CompletedTask;

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct) => Task.CompletedTask;

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RecordingOverviewPresenter : ICharacterOverviewPresenter
    {
        public CharacterOverviewState State { get; private set; } = CharacterOverviewState.Empty;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public int InitializeCalls { get; private set; }

        public Task InitializeAsync(CancellationToken ct)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => Task.CompletedTask;

        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct) => Task.CompletedTask;

        public Task HandleUiControlAsync(string controlId, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct) => Task.CompletedTask;

        public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct) => Task.CompletedTask;

        public Task CloseDialogAsync(CancellationToken ct) => Task.CompletedTask;

        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExportAsync(CancellationToken ct) => Task.CompletedTask;

        public Task PrintAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RecordingBootstrapProvider : IShellBootstrapDataProvider
    {
        public List<string> RequestedRulesetIds { get; } = [];

        public Task<ShellBootstrapData> GetAsync(CancellationToken ct)
            => Task.FromResult(CreateBootstrap(RulesetDefaults.Sr5));

        public Task<ShellBootstrapData> GetAsync(string? rulesetId, CancellationToken ct)
        {
            RequestedRulesetIds.Add(rulesetId ?? string.Empty);
            return Task.FromResult(CreateBootstrap(rulesetId ?? RulesetDefaults.Sr5));
        }

        private static ShellBootstrapData CreateBootstrap(string rulesetId)
            => new(
                RulesetId: rulesetId,
                Commands: [],
                NavigationTabs: [],
                Workspaces: [],
                PreferredRulesetId: RulesetDefaults.Sr5,
                ActiveRulesetId: rulesetId);
    }
}
