#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Bunit;
using Chummer.Blazor.Components.Shell;
using Chummer.Blazor.Services;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class BuildPwaWorkspaceTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("build-pwa-test");

    private static readonly OpenWorkspaceState OpenWorkspace = new(
        WorkspaceId,
        "Responsive Runner",
        "Switchback",
        DateTimeOffset.UtcNow,
        RulesetDefaults.Sr5);

    private static readonly IReadOnlyList<NavigationTabDefinition> NavigationTabs =
    [
        new("tab-create", "Create", "build-lab", "character", true, true, RulesetDefaults.Sr5),
        new("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5),
        new("tab-skills", "Skills", "skills", "character", true, true, RulesetDefaults.Sr5)
    ];

    [TestMethod]
    public async Task Recovery_interop_deadline_cancels_only_the_invocation_not_the_component_lifetime()
    {
        using var lifetime = new CancellationTokenSource();
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool invocationCancelled = false;
        bool timedOut = false;
        try
        {
            _ = await RecoveryInteropDeadlineRuntime.RunAsync(
                token =>
                {
                    token.Register(() =>
                    {
                        invocationCancelled = true;
                        pending.TrySetCanceled(token);
                    });
                    return pending.Task;
                },
                TimeSpan.FromMilliseconds(20),
                lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
        }

        Assert.IsTrue(SpinWait.SpinUntil(() => invocationCancelled, TimeSpan.FromSeconds(1)));
        Assert.IsTrue(timedOut);
        Assert.IsFalse(lifetime.IsCancellationRequested);
    }

    [TestMethod]
    public async Task Recovery_observation_deadline_keeps_the_host_lifetime_owned_and_usable()
    {
        using var lifetime = new CancellationTokenSource();
        using var lifetimeCallback = new ManualResetEventSlim();
        using CancellationTokenRegistration registration = lifetime.Token.Register(lifetimeCallback.Set);
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            RecoveryInteropDeadlineRuntime.WaitAsync(
                pending.Task,
                TimeSpan.FromMilliseconds(20),
                lifetime.Token));

        Assert.IsFalse(lifetime.IsCancellationRequested);
        lifetime.Cancel();
        Assert.IsTrue(lifetimeCallback.Wait(TimeSpan.FromSeconds(1)),
            "The deadline must not dispose or consume callbacks owned by the host lifetime token.");
    }

    [TestMethod]
    public async Task Dispose_uses_an_independent_deadline_when_unregister_never_completes()
    {
        using var context = new BunitContext();
        var jsRuntime = new HangingUnregisterJsRuntime();
        context.Services.AddSingleton<IJSRuntime>(jsRuntime);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(context, "tab-info");

        await cut.Instance.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsTrue(
            jsRuntime.UnregisterCancellationObserved.Wait(TimeSpan.FromSeconds(1)),
            "The teardown deadline must cancel the hanging unregister invocation.");
    }

    [TestMethod]
    public void Responsive_workspace_renders_one_editor_and_accessible_layout_controls()
    {
        using var context = new BunitContext();
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(context, "tab-info");

        Assert.AreEqual(1, cut.FindAll("#chummer-workspace-main").Count);
        Assert.AreEqual(1, cut.FindAll(".build-pwa-editor > .section-preview").Count);
        Assert.AreEqual(1, cut.FindAll(".build-pwa-summary-rail .desktop-summary").Count);
        Assert.AreEqual(3, cut.FindAll("[data-build-pwa-layout-choice]").Count);
        Assert.AreEqual(3, cut.FindAll("[data-nav-tab]").Count);
        Assert.AreEqual("step", cut.Find("[data-nav-tab='tab-info']").GetAttribute("aria-current"));
        Assert.AreEqual("chummer-workspace-main", cut.Find("[data-nav-tab='tab-info']").GetAttribute("aria-controls"));
        Assert.AreEqual("polite", cut.Find("#build-pwa-layout-status").GetAttribute("aria-live"));
        Assert.AreEqual("browser-measured-geometry", cut.Find("[data-build-pwa-layout]").GetAttribute("data-build-pwa-layout-source"));
        Assert.AreEqual("H1", cut.Find("#build-pwa-compact-title").TagName);
    }

    [TestMethod]
    public void Responsive_workspace_exposes_named_landmarks_and_stable_focus_targets()
    {
        using var context = new BunitContext();
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(context, "tab-info");

        Assert.AreEqual(
            "build-pwa-compact-title",
            cut.Find(".build-pwa-compact-context").GetAttribute("aria-labelledby"));
        Assert.AreEqual(
            "build-pwa-step-heading",
            cut.Find(".build-pwa-step-rail").GetAttribute("aria-labelledby"));
        Assert.AreEqual(
            "Builder step actions",
            cut.Find(".build-pwa-mobile-dock").GetAttribute("aria-label"));

        IElement editor = cut.Find("#chummer-workspace-main");
        Assert.AreEqual("-1", editor.GetAttribute("tabindex"));
        Assert.IsFalse(string.IsNullOrWhiteSpace(editor.GetAttribute("aria-label")));

        IElement summary = cut.Find("#build-pwa-summary");
        Assert.AreEqual("-1", summary.GetAttribute("tabindex"));
        Assert.AreEqual("Builder summary and actions", summary.GetAttribute("aria-label"));

        IElement progress = cut.Find(".build-pwa-compact-context progress");
        Assert.AreEqual("2", progress.GetAttribute("value"));
        Assert.AreEqual("3", progress.GetAttribute("max"));
        Assert.AreEqual("Builder section position", progress.GetAttribute("aria-label"));

        foreach (IElement choice in cut.FindAll("[data-build-pwa-layout-choice]"))
            Assert.AreEqual("build-pwa-layout-status", choice.GetAttribute("aria-describedby"));

        Assert.AreEqual(
            "Section 2 of 3: Runner",
            cut.Find("[data-nav-tab='tab-info']").GetAttribute("aria-label"));
    }

    [TestMethod]
    public void Privacy_lifecycle_disclosure_is_bound_to_the_review_required_runtime_contract()
    {
        using var context = new BunitContext();
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(context, "tab-info");

        HostedBuildPrivacyLifecycleSnapshot snapshot =
            HostedBuildPrivacyLifecycleCapabilities.Instance.Current;
        IElement disclosure = cut.Find("[data-hosted-build-privacy-lifecycle]");
        Assert.AreEqual(snapshot.ContractName, disclosure.GetAttribute("data-hosted-build-privacy-lifecycle"));
        Assert.AreEqual(
            HostedBuildPrivacyLifecycleCapabilities.ReviewRequiredStatus,
            disclosure.GetAttribute("data-privacy-launch-status"));
        Assert.AreEqual("true", disclosure.GetAttribute("data-privacy-launch-review-required"));
        StringAssert.Contains(disclosure.TextContent, "Privacy review required");
        Assert.AreEqual(snapshot.Facts.Count, disclosure.QuerySelectorAll("[data-hosted-build-privacy-capability]").Length);
        foreach (HostedBuildPrivacyLifecycleFact fact in snapshot.Facts)
        {
            IElement rendered = disclosure.QuerySelector(
                $"[data-hosted-build-privacy-capability='{fact.Id}']")
                ?? throw new AssertFailedException($"Privacy lifecycle fact '{fact.Id}' was not rendered.");
            StringAssert.Contains(rendered.TextContent, fact.Label);
            StringAssert.Contains(rendered.TextContent, fact.Disclosure);
        }

        foreach (string prohibitedClaim in snapshot.ProhibitedClaims)
        {
            Assert.IsNull(disclosure.QuerySelector(
                $"[data-hosted-build-privacy-capability='{prohibitedClaim}']"));
        }

        Assert.IsFalse(disclosure.TextContent.Contains("permanently deletes", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(disclosure.TextContent.Contains("durable recovery is available", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(disclosure.TextContent.Contains("erase your account", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Circuit_loss_during_layout_bridge_keeps_the_accessible_server_rendered_workspace()
    {
        using var context = new BunitContext();
        SetupPwaInterop(context, registrationDisconnected: true);

        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(context, "tab-info");

        Assert.AreEqual(1, cut.FindAll("#chummer-workspace-main").Count);
        Assert.AreEqual("step", cut.Find("[data-nav-tab='tab-info']").GetAttribute("aria-current"));
        Assert.AreEqual("polite", cut.Find("#build-pwa-layout-status").GetAttribute("aria-live"));
    }

    [TestMethod]
    public async Task Retry_release_renders_reenters_and_stale_finally_cannot_clear_the_new_timer_owner()
    {
        using var context = new BunitContext();
        SetupPwaInterop(context, registrationDisconnected: true);
        using IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(context, "tab-info");
        BuildPwaWorkspace component = cut.Instance;
        Type componentType = component.GetType();
        FieldInfo ownerField = componentType.GetField(
            "_integrityRetryOwner",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Retry owner field was not found on the rendered component.");
        FieldInfo attemptsField = componentType.GetField(
            "_integrityRegistrationAttempts",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Retry attempt field was not found on the rendered component.");
        MethodInfo retryMethod = componentType.GetMethod(
            "RetryIntegrityBridgeRegistrationAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("Retry worker was not found on the rendered component.");

        cut.WaitForAssertion(() => Assert.IsGreaterThan(
            0L,
            (long)(ownerField.GetValue(component) ?? 0L)));
        long staleOwner = (long)(ownerField.GetValue(component) ?? 0L);

        // Keep the replacement timer alive long enough to inspect. The stale
        // worker below must win its real owner CAS, request a real render, and
        // re-enter OnAfterRenderAsync, where failed JS registration schedules
        // the replacement generation before the stale finally block runs.
        attemptsField.SetValue(component, 7);

        Task staleWorker = (Task)(retryMethod.Invoke(
            component,
            [TimeSpan.Zero, staleOwner])
            ?? throw new AssertFailedException("Retry worker did not return a task."));
        await staleWorker.WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
        {
            long replacementOwner = (long)(ownerField.GetValue(component) ?? 0L);
            Assert.IsGreaterThan(0L, replacementOwner);
            Assert.AreNotEqual(staleOwner, replacementOwner);
            Assert.IsGreaterThanOrEqualTo(8, (int)(attemptsField.GetValue(component) ?? 0));
        });
    }

    [TestMethod]
    public async Task Dirty_external_revision_keeps_edits_and_surfaces_an_accessible_conflict_without_reload()
    {
        int loadRequests = 0;
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(
            context,
            "tab-info",
            state: BuildState("tab-info", contentRevision: 5, savedRevision: 4),
            loadWorkspace: _ => loadRequests++);

        await cut.InvokeAsync(() => cut.Instance.HandleExternalWorkspaceRevisionAsync(
            WorkspaceId.Value,
            6,
            "checkpoint"));

        cut.WaitForAssertion(() =>
        {
            IElement alert = cut.Find("[data-build-pwa-integrity-conflict]");
            Assert.AreEqual("alert", alert.GetAttribute("role"));
            Assert.AreEqual("-1", alert.GetAttribute("tabindex"));
            Assert.IsTrue(cut.Find("[data-build-pwa-integrity-status]").TextContent.Contains(
                "kept your current edits",
                StringComparison.OrdinalIgnoreCase));
        });
        Assert.AreEqual(0, loadRequests);
    }

    [TestMethod]
    public async Task Same_revision_delete_closes_a_clean_sibling_and_marks_it_deleted_until_close_completes()
    {
        int closeRequests = 0;
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(
            context,
            "tab-info",
            state: BuildState("tab-info", contentRevision: 5, savedRevision: 5),
            closeWorkspace: _ => closeRequests++);

        await cut.InvokeAsync(() => cut.Instance.HandleExternalWorkspaceRevisionAsync(
            WorkspaceId.Value,
            5,
            "delete"));

        cut.WaitForAssertion(() =>
        {
            IElement alert = cut.Find("[data-build-pwa-integrity-conflict]");
            Assert.AreEqual("true", alert.GetAttribute("data-build-pwa-integrity-deleted"));
            Assert.IsTrue(alert.TextContent.Contains("deleted elsewhere", StringComparison.OrdinalIgnoreCase));
        });
        Assert.AreEqual(1, closeRequests);
    }

    [TestMethod]
    public async Task Dirty_deleted_sibling_preserves_the_max_revision_and_offers_save_copy_only()
    {
        string? executedCommand = null;
        int loadRequests = 0;
        var recovery = new FakeRecoveryCopySource();
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(
            context,
            "tab-info",
            state: BuildState("tab-info", contentRevision: 5, savedRevision: 4),
            loadWorkspace: _ => loadRequests++,
            executeCommand: command => executedCommand = command,
            recoveryCopySource: recovery);

        await cut.InvokeAsync(() => cut.Instance.HandleExternalWorkspaceRevisionAsync(
            WorkspaceId.Value,
            9,
            "checkpoint"));
        await cut.InvokeAsync(() => cut.Instance.HandleExternalWorkspaceRevisionAsync(
            WorkspaceId.Value,
            7,
            "delete"));

        cut.WaitForAssertion(() =>
        {
            IElement alert = cut.Find("[data-build-pwa-integrity-conflict]");
            Assert.AreEqual("9", alert.GetAttribute("data-build-pwa-integrity-revision"));
            Assert.AreEqual("true", alert.GetAttribute("data-build-pwa-integrity-deleted"));
            Assert.HasCount(0, cut.FindAll("[data-build-pwa-integrity-reload]"));
            Assert.HasCount(1, cut.FindAll("[data-build-pwa-integrity-save-copy]"));
        });

        cut.Find("[data-build-pwa-integrity-save-copy]").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(0, cut.FindAll("[data-build-pwa-integrity-close-recovery]"));
            Assert.HasCount(0, cut.FindAll("[data-build-pwa-integrity-confirm-recovery]"));
        });
        Assert.AreEqual(1, recovery.ExportCalls);
        Assert.IsTrue(recovery.ExportPrepared);
        Assert.IsNull(executedCommand);
        Assert.AreEqual(0, loadRequests);

        recovery.MarkFallbackDispatched();
        await cut.InvokeAsync(() => cut.Instance.HandleExternalWorkspaceRevisionAsync(
            WorkspaceId.Value,
            9,
            "delete"));
        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(1, cut.FindAll("[data-build-pwa-integrity-confirm-recovery]"));
            Assert.HasCount(0, cut.FindAll("[data-build-pwa-integrity-close-recovery]"));
        });
        cut.Find("[data-build-pwa-integrity-confirm-recovery]").Click();
        cut.WaitForAssertion(() => Assert.HasCount(1, cut.FindAll("[data-build-pwa-integrity-close-recovery]")));
        Assert.AreEqual(1, recovery.AcknowledgeCalls);

        cut.Find("[data-build-pwa-integrity-close-recovery]").Click();
        Assert.AreEqual(1, recovery.CloseCalls);
    }

    [TestMethod]
    public async Task Dirty_deleted_sibling_without_complete_payload_fails_closed_and_keeps_unload_protection()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(
            context,
            "tab-info",
            state: BuildState("tab-info", contentRevision: 5, savedRevision: 4));

        await cut.InvokeAsync(() => cut.Instance.HandleExternalWorkspaceRevisionAsync(
            WorkspaceId.Value,
            5,
            "delete"));

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(0, cut.FindAll("[data-build-pwa-integrity-save-copy]"));
            Assert.HasCount(0, cut.FindAll("[data-build-pwa-integrity-close-recovery]"));
            Assert.IsTrue(cut.Find("[data-build-pwa-integrity-conflict]").TextContent.Contains(
                "Recovery copy unavailable",
                StringComparison.OrdinalIgnoreCase));
        });
        BuildPwaWorkspace.BuildPwaIntegritySnapshot snapshot = await cut.Instance.GetBuildPwaIntegrityStateAsync();
        Assert.IsTrue(snapshot.HasConflict);
    }

    [TestMethod]
    public void Dirty_workspace_surfaces_recovery_capacity_readiness_before_cross_tab_deletion()
    {
        var recovery = new FakeRecoveryCopySource
        {
            Available = false,
            UnavailableReason = "Recovery vault capacity is occupied by protected payloads."
        };
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        SetupPwaInterop(context);

        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(
            context,
            "tab-info",
            state: BuildState("tab-info", contentRevision: 5, savedRevision: 4),
            recoveryCopySource: recovery);

        IElement readiness = cut.Find("[data-build-pwa-recovery-readiness='blocked']");
        StringAssert.Contains(readiness.TextContent, recovery.UnavailableReason);
        Assert.HasCount(0, cut.FindAll("[data-build-pwa-integrity-conflict]"));
    }

    [TestMethod]
    public async Task Recovery_export_callback_failure_keeps_preserved_runner_open_and_never_offers_close()
    {
        var recovery = new FakeRecoveryCopySource { ThrowOnExport = true };
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = RenderWorkspace(
            context,
            "tab-info",
            state: BuildState("tab-info", contentRevision: 5, savedRevision: 4),
            recoveryCopySource: recovery);

        await cut.InvokeAsync(() => cut.Instance.HandleExternalWorkspaceRevisionAsync(
            WorkspaceId.Value,
            5,
            "delete"));
        cut.Find("[data-build-pwa-integrity-save-copy]").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.HasCount(0, cut.FindAll("[data-build-pwa-integrity-close-recovery]"));
            Assert.IsTrue(cut.Find("[data-build-pwa-integrity-status]").TextContent.Contains(
                "unavailable",
                StringComparison.OrdinalIgnoreCase));
        });
        Assert.AreEqual(1, recovery.ExportCalls);
        Assert.AreEqual(0, recovery.CloseCalls);
    }

    [TestMethod]
    public void Compact_previous_and_next_actions_reuse_the_shared_tab_callback()
    {
        string? requestedTab = null;
        using var context = new BunitContext();
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> firstStep = RenderWorkspace(
            context,
            "tab-create",
            tabId => requestedTab = tabId);

        IElement previous = firstStep.Find("[data-build-pwa-previous]");
        Assert.IsTrue(previous.HasAttribute("disabled"));
        firstStep.Find("[data-build-pwa-next]").Click();
        Assert.AreEqual("tab-info", requestedTab);

        requestedTab = null;
        IRenderedComponent<BuildPwaWorkspace> middleStep = RenderWorkspace(
            context,
            "tab-info",
            tabId => requestedTab = tabId);

        middleStep.Find("[data-build-pwa-previous]").Click();
        Assert.AreEqual("tab-create", requestedTab);
    }

    [TestMethod]
    public void Mobile_command_menu_reuses_the_shared_command_callback()
    {
        string? executedCommand = null;
        AppCommandDefinition saveCommand = new(
            "save_character",
            "command.save",
            "file",
            true,
            true,
            RulesetDefaults.Sr5);

        using var context = new BunitContext();
        SetupPwaInterop(context);
        IRenderedComponent<BuildPwaWorkspace> cut = context.Render<BuildPwaWorkspace>(parameters => parameters
            .Add(component => component.State, BuildState("tab-create"))
            .Add(component => component.ShellSurface, BuildShellSurface("tab-create"))
            .Add(component => component.OpenWorkspaces, new[] { OpenWorkspace })
            .Add(component => component.ActiveWorkspaceId, WorkspaceId)
            .Add(component => component.ActiveTabId, "tab-create")
            .Add(component => component.NavigationTabs, NavigationTabs)
            .Add(component => component.MobileCommands, new[] { saveCommand })
            .Add(component => component.IsNavigationTabEnabled, _ => true)
            .Add(component => component.IsCommandEnabled, _ => true)
            .Add(component => component.ExecuteCommandRequested,
                (Action<string>)(commandId => executedCommand = commandId)));

        cut.Find("[data-build-pwa-command='save_character']").Click();
        Assert.AreEqual("save_character", executedCommand);
    }

    [TestMethod]
    public void Reopened_workspace_reapplies_layout_for_each_component_instance()
    {
        using var context = new BunitContext();
        SetupPwaInterop(context);

        using (IRenderedComponent<BuildPwaWorkspace> first = RenderWorkspace(context, "tab-create"))
        {
            context.JSInterop.VerifyInvoke("chummerBuildPwaLayout.applyAll", calledTimes: 1);
        }

        using IRenderedComponent<BuildPwaWorkspace> reopened = RenderWorkspace(context, "tab-info");
        context.JSInterop.VerifyInvoke("chummerBuildPwaLayout.applyAll", calledTimes: 2);
    }

    private static IRenderedComponent<BuildPwaWorkspace> RenderWorkspace(
        BunitContext context,
        string activeTabId,
        Action<string>? selectTab = null,
        CharacterOverviewState? state = null,
        Action<string>? loadWorkspace = null,
        Action<string>? closeWorkspace = null,
        Action<string>? executeCommand = null,
        IWorkspaceRecoveryCopySource? recoveryCopySource = null)
    {
        context.Services.TryAddSingleton<IWorkspacePrivacyLifecycleCapabilities>(
            HostedBuildPrivacyLifecycleCapabilities.Instance);
        CharacterOverviewState currentState = state ?? BuildState(activeTabId);
        return context.Render<BuildPwaWorkspace>(parameters => parameters
            .Add(component => component.State, currentState)
            .Add(component => component.ShellSurface, BuildShellSurface(activeTabId))
            .Add(component => component.OpenWorkspaces, currentState.OpenWorkspaces)
            .Add(component => component.ActiveWorkspaceId, WorkspaceId)
            .Add(component => component.ActiveTabId, activeTabId)
            .Add(component => component.NavigationTabs, NavigationTabs)
            .Add(component => component.IsNavigationTabEnabled, _ => true)
            .Add(component => component.IsCommandEnabled, _ => true)
            .Add(component => component.SelectTabRequested,
                (Action<string>)(tabId => selectTab?.Invoke(tabId)))
            .Add(component => component.LoadWorkspaceRequested,
                (Action<string>)(workspaceId => loadWorkspace?.Invoke(workspaceId)))
            .Add(component => component.CloseWorkspaceRequested,
                (Action<string>)(workspaceId => closeWorkspace?.Invoke(workspaceId)))
            .Add(component => component.ExecuteCommandRequested,
                (Action<string>)(commandId => executeCommand?.Invoke(commandId)))
            .Add(component => component.RecoveryCopySource, recoveryCopySource));
    }

    private static void SetupPwaInterop(
        BunitContext context,
        bool registrationDisconnected = false)
    {
        context.Services.TryAddSingleton<IWorkspacePrivacyLifecycleCapabilities>(
            HostedBuildPrivacyLifecycleCapabilities.Instance);
        const string registrationToken = "00112233445566778899aabbccddeeff";
        context.JSInterop.SetupVoid("chummerBuildPwaLayout.applyAll").SetVoidResult();
        context.JSInterop.SetupVoid("chummerBuildPwaIntegrity.publishDelete", _ => true).SetVoidResult();
        var registration = context.JSInterop
            .Setup<string?>("chummerBuildPwaIntegrity.registerBridge", _ => true);
        if (registrationDisconnected)
        {
            registration.SetException(new Microsoft.JSInterop.JSDisconnectedException("test circuit loss"));
        }
        else
        {
            registration.SetResult(registrationToken);
        }
        context.JSInterop
            .Setup<BuildPwaWorkspace.BuildPwaIntegritySnapshot>(
                "chummerBuildPwaIntegrity.updateState",
                _ => true)
            .SetResult(new BuildPwaWorkspace.BuildPwaIntegritySnapshot(
                WorkspaceId.Value,
                0,
                0,
                false,
                false,
                false,
                true));
        context.JSInterop
            .Setup<bool>("chummerBuildPwaIntegrity.unregisterBridge", _ => true)
            .SetResult(true);
    }

    private static CharacterOverviewState BuildState(
        string activeTabId,
        long contentRevision = 0,
        long savedRevision = 0)
    {
        OpenWorkspaceState workspace = OpenWorkspace with
        {
            ContentRevision = contentRevision,
            SavedRevision = savedRevision
        };
        return CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(WorkspaceId, [workspace], [WorkspaceId]),
            WorkspaceId = WorkspaceId,
            OpenWorkspaces = [workspace],
            ActiveTabId = activeTabId
        };
    }

    private static ShellSurfaceState BuildShellSurface(string activeTabId)
        => new(
            Commands: [],
            MenuRoots: [],
            NavigationTabs: NavigationTabs,
            WorkspaceActions: [],
            ActiveWorkflowSurfaceActions: [],
            OpenWorkspaces: [OpenWorkspace],
            ActiveRulesetId: RulesetDefaults.Sr5,
            PreferredRulesetId: RulesetDefaults.Sr5,
            ActiveWorkspaceId: WorkspaceId,
            ActiveTabId: activeTabId,
            LastCommandId: null);

    private sealed class HangingUnregisterJsRuntime : IJSRuntime
    {
        private const string RegistrationToken = "00112233445566778899aabbccddeeff";

        public ManualResetEventSlim UnregisterCancellationObserved { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            if (identifier == "chummerBuildPwaIntegrity.unregisterBridge")
                return new ValueTask<TValue>(WaitForUnregisterCancellationAsync<TValue>(cancellationToken));

            object? result = identifier switch
            {
                "chummerBuildPwaIntegrity.registerBridge" => RegistrationToken,
                "chummerBuildPwaIntegrity.updateState" => new BuildPwaWorkspace.BuildPwaIntegritySnapshot(
                    WorkspaceId.Value,
                    0,
                    0,
                    false,
                    false,
                    false,
                    true),
                _ => default(TValue)
            };
            return new ValueTask<TValue>(result is null ? default! : (TValue)result);
        }

        private async Task<TValue> WaitForUnregisterCancellationAsync<TValue>(
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<TValue>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                UnregisterCancellationObserved.Set();
                completion.TrySetCanceled(cancellationToken);
            });
            return await completion.Task.ConfigureAwait(false);
        }
    }

    private sealed class FakeRecoveryCopySource : IWorkspaceRecoveryCopySource
    {
        public bool Available { get; set; } = true;
        public string UnavailableReason { get; set; } = "Recovery unavailable.";
        public bool ExportSucceeds { get; set; } = true;
        public bool ThrowOnExport { get; set; }
        public bool ExportConfirmed { get; private set; }
        public bool ExportPrepared { get; private set; }
        public bool AwaitingExplicitUserAck { get; private set; }
        public int ExportCalls { get; private set; }
        public int AcknowledgeCalls { get; private set; }
        public int CloseCalls { get; private set; }

        public WorkspaceRecoveryCopyAvailability GetRecoveryCopyAvailability(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision)
            => Available
                ? new(
                Available: true,
                SourceRevision: expectedSourceRevision,
                LocalGeneration: 17,
                FileName: "build-pwa-test.recovery.chum5",
                ContentType: "application/xml",
                DocumentLength: 128,
                ExportPrepared: ExportPrepared,
                ExportConfirmed: ExportConfirmed,
                AwaitingExplicitUserAck: AwaitingExplicitUserAck)
                : WorkspaceRecoveryCopyAvailability.Unavailable(
                    expectedSourceRevision,
                    UnavailableReason);

        public Task<WorkspaceRecoveryCopyExportResult> PrepareRecoveryCopyAsync(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision,
            long expectedLocalGeneration,
            CancellationToken ct)
        {
            ExportCalls++;
            if (ThrowOnExport)
                throw new InvalidOperationException("Simulated recovery export callback failure.");

            ExportPrepared = ExportSucceeds;
            ExportConfirmed = false;
            AwaitingExplicitUserAck = false;

            return Task.FromResult(new WorkspaceRecoveryCopyExportResult(
                Success: ExportSucceeds,
                expectedSourceRevision,
                expectedLocalGeneration,
                FileName: ExportSucceeds ? "build-pwa-test.recovery.chum5" : null,
                ContentType: ExportSucceeds ? "application/xml" : null,
                DocumentLength: ExportSucceeds ? 128 : 0,
                Error: ExportSucceeds ? null : "Recovery copy unavailable."));
        }

        public void MarkFallbackDispatched()
        {
            if (!ExportPrepared)
                throw new InvalidOperationException("Prepare the recovery export first.");

            AwaitingExplicitUserAck = true;
        }

        public bool AcknowledgeRecoveryCopySaved(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision,
            long expectedLocalGeneration)
        {
            AcknowledgeCalls++;
            if (!AwaitingExplicitUserAck || expectedLocalGeneration != 17)
                return false;

            AwaitingExplicitUserAck = false;
            ExportPrepared = false;
            ExportConfirmed = true;
            return true;
        }

        public Task<WorkspaceRecoveryCloseResult> CloseExportedRecoveryCopyAsync(
            CharacterWorkspaceId workspaceId,
            long expectedSourceRevision,
            long expectedLocalGeneration,
            CancellationToken ct)
        {
            CloseCalls++;
            return Task.FromResult(new WorkspaceRecoveryCloseResult(true));
        }
    }
}
