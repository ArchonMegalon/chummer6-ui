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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class BlazorPublicEdgeWarmupServiceTests
{
    [TestMethod]
    public async Task WarmAsync_warms_owner_independent_ruleset_catalogs()
    {
        var catalogResolver = new RecordingRulesetShellCatalogResolver();
        BlazorPublicEdgeWarmupService service = CreateService(catalogResolver);

        await service.WarmAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            BlazorPublicEdgeWarmupService.WarmedRulesetIds,
            catalogResolver.CommandRulesetIds.ToArray());
        CollectionAssert.AreEqual(
            BlazorPublicEdgeWarmupService.WarmedRulesetIds,
            catalogResolver.NavigationRulesetIds.ToArray());
        CollectionAssert.AreEqual(
            BlazorPublicEdgeWarmupService.WarmedRulesetIds,
            catalogResolver.WorkflowDefinitionRulesetIds.ToArray());
        CollectionAssert.AreEqual(
            BlazorPublicEdgeWarmupService.WarmedRulesetIds,
            catalogResolver.WorkflowSurfaceRulesetIds.ToArray());
    }

    [TestMethod]
    public async Task WarmAsync_fails_open_when_owner_independent_catalog_throws()
    {
        var catalogResolver = new RecordingRulesetShellCatalogResolver { ThrowOnResolveCommands = true };
        BlazorPublicEdgeWarmupService service = CreateService(catalogResolver);

        await service.WarmAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { BlazorPublicEdgeWarmupService.WarmedRulesetIds[0] },
            catalogResolver.CommandRulesetIds.ToArray());
        Assert.HasCount(0, catalogResolver.NavigationRulesetIds);
        Assert.HasCount(0, catalogResolver.WorkflowDefinitionRulesetIds);
        Assert.HasCount(0, catalogResolver.WorkflowSurfaceRulesetIds);
    }

    [TestMethod]
    public async Task WarmAsync_never_resolves_owner_scoped_presenters_or_bootstrap_clients()
    {
        var catalogResolver = new RecordingRulesetShellCatalogResolver();
        var services = new ServiceCollection();
        services.AddSingleton<IRulesetShellCatalogResolver>(catalogResolver);
        services.AddScoped<IShellPresenter>(_ =>
            throw new AssertFailedException("Startup warm-up resolved an owner-scoped shell presenter."));
        services.AddScoped<ICharacterOverviewPresenter>(_ =>
            throw new AssertFailedException("Startup warm-up resolved an owner-scoped overview presenter."));
        services.AddScoped<IShellBootstrapDataProvider>(_ =>
            throw new AssertFailedException("Startup warm-up resolved an owner-scoped bootstrap provider."));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IHostApplicationLifetime>(new TestHostApplicationLifetime());
        services.AddLogging();
        services.AddSingleton<BlazorPublicEdgeWarmupService>();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        BlazorPublicEdgeWarmupService service =
            provider.GetRequiredService<BlazorPublicEdgeWarmupService>();
        await service.WarmAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            BlazorPublicEdgeWarmupService.WarmedRulesetIds,
            catalogResolver.CommandRulesetIds.ToArray());
    }

    [TestMethod]
    public async Task StartAsync_waits_for_host_started_before_loopback_route_warmup()
    {
        var catalogResolver = new RecordingRulesetShellCatalogResolver();
        var lifetime = new TestHostApplicationLifetime();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["urls"] = "http://127.0.0.1:1"
            })
            .Build();
        var service = new BlazorPublicEdgeWarmupService(
            catalogResolver,
            lifetime,
            configuration,
            NullLogger<BlazorPublicEdgeWarmupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Task executeTask = service.ExecuteTask
            ?? throw new AssertFailedException("The hosted warm-up did not expose its background execution task.");
        Assert.IsFalse(executeTask.IsCompleted);

        lifetime.NotifyStarted();
        await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);
    }

    private static BlazorPublicEdgeWarmupService CreateService(
        IRulesetShellCatalogResolver catalogResolver)
    {
        return new BlazorPublicEdgeWarmupService(
            catalogResolver,
            new TestHostApplicationLifetime(),
            new ConfigurationBuilder().Build(),
            NullLogger<BlazorPublicEdgeWarmupService>.Instance);
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void NotifyStarted()
            => _started.Cancel();

        public void StopApplication()
            => _stopping.Cancel();
    }

    private sealed class RecordingRulesetShellCatalogResolver : IRulesetShellCatalogResolver
    {
        public List<string> CommandRulesetIds { get; } = [];

        public List<string> NavigationRulesetIds { get; } = [];

        public List<string> WorkflowDefinitionRulesetIds { get; } = [];

        public List<string> WorkflowSurfaceRulesetIds { get; } = [];

        public bool ThrowOnResolveCommands { get; set; }

        public IReadOnlyList<AppCommandDefinition> ResolveCommands(string? rulesetId)
        {
            CommandRulesetIds.Add(rulesetId ?? string.Empty);
            if (ThrowOnResolveCommands)
                throw new InvalidOperationException("owner-independent catalog warm-up failed");
            return [];
        }

        public IReadOnlyList<NavigationTabDefinition> ResolveNavigationTabs(string? rulesetId)
        {
            NavigationRulesetIds.Add(rulesetId ?? string.Empty);
            return [];
        }

        public IReadOnlyList<WorkflowDefinition> ResolveWorkflowDefinitions(string? rulesetId)
        {
            WorkflowDefinitionRulesetIds.Add(rulesetId ?? string.Empty);
            return [];
        }

        public IReadOnlyList<WorkflowSurfaceDefinition> ResolveWorkflowSurfaces(string? rulesetId)
        {
            WorkflowSurfaceRulesetIds.Add(rulesetId ?? string.Empty);
            return [];
        }

        public IReadOnlyList<WorkspaceSurfaceActionDefinition> ResolveWorkspaceActionsForTab(
            string? tabId,
            string? rulesetId)
            => [];
    }
}
