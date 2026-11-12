#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Chummer.Avalonia;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Behavioral outcome-matrix tests for the install-linking dialog (8 Nov
/// re-audit §4): drive the REAL <see cref="DesktopInstallLinkingWindow"/>
/// headlessly through the shared portal-open handler and the observation
/// loop instead of asserting on source text. Both entry points converge on
/// the same handler (entry wiring stays covered by the chrome source
/// tests), so the matrix drives the handler with realistic persisted state;
/// the §3 lifetime scenarios run the REAL observation loop with a
/// step-controlled delay seam instead of wall-clock sleeps.
/// </summary>
[TestClass]
public sealed class DesktopInstallLinkingOutcomeMatrixTests
{
    private const string StateRootEnv = "CHUMMER_DESKTOP_STATE_ROOT";
    private const string HeadId = "avalonia";

    private string? _stateRoot;
    private string? _priorStateRoot;
    private string? _priorApiBase;

    [TestInitialize]
    public void Initialize()
    {
        _stateRoot = Path.Combine(Path.GetTempPath(), "chummer-win076-matrix-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_stateRoot);
        _priorStateRoot = Environment.GetEnvironmentVariable(StateRootEnv);
        _priorApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        Environment.SetEnvironmentVariable(StateRootEnv, _stateRoot);
        Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", "http://127.0.0.1:9/");

        // Deterministic observation seams: a real small tick delay keeps the
        // loop's timing shape, and a step budget bounds the run without
        // wall-clock sleeps (audit demand: controllable time, no multi-minute
        // suite sleeps).
        DesktopInstallLinkingWindow.ObservationDelay = static (ms, ct) => Task.Delay(1, ct);
        DesktopInstallLinkingWindow.ObservationLifetime = static () => TimeSpan.FromMinutes(30);
        DesktopInstallLinkingWindow.ObservationMaxSteps = 200;
    }

    [TestCleanup]
    public void Cleanup()
    {
        Assert.AreEqual(0, PollTasks.Count,
            "Do not restore shared state while a runtime poll worker is still active.");
        DesktopInstallLinkingWindow.ObservationDelay = static (ms, ct) => Task.Delay(ms, ct);
        DesktopInstallLinkingWindow.ObservationNow = static () => DateTimeOffset.UtcNow;
        DesktopInstallLinkingWindow.ObservationLifetime = static () =>
            DesktopInstallLinkingRuntime.ResolveRemoteCallbackObservationLifetime();
        DesktopInstallLinkingWindow.ObservationMaxSteps = int.MaxValue;
        DesktopInstallLinkingWindow.HandoffDelay = static (ms, ct) => Task.Delay(ms, ct);
        DesktopInstallLinkingWindow.PortalOpenOverride = null;
        DesktopInstallLinkingWindow.ClipboardWriteOverride = null;
        Environment.SetEnvironmentVariable(StateRootEnv, _priorStateRoot);
        Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", _priorApiBase);
        try
        {
            if (_stateRoot is not null && Directory.Exists(_stateRoot))
            {
                Directory.Delete(_stateRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    // ------------------------------------------------------------------
    // §4 matrix — driven through the real shared handler.
    // ------------------------------------------------------------------

    [TestMethod]
    public async Task Browser_return_cold_start_resumes_same_proof_and_refreshes_existing_window()
    {
        await WithWindowAsync(async window =>
        {
            using InflightHubFixture fixture = new(pendingFirst: false, holdBody: true);
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            string uri = "chummer://install-link?state=" + before.BrowserApprovalReturnState;
            DesktopInstallLinkingStartupContext context = await DesktopInstallLinkingRuntime.InitializeForStartupAsync(
                HeadId, ["--install-link-callback", uri], CancellationToken.None);
            Assert.AreEqual("browser_return_pending", context.PromptReason);
            Assert.IsTrue(context.ShouldPrompt);
            Assert.IsFalse(DesktopInstallLinkingRuntime.IsClaimed(context.State), "A return is not approval.");
            Assert.AreEqual(before.BrowserApprovalRequest, context.State.BrowserApprovalRequest);
            Assert.AreEqual(1, PollTasks.Count);
            Task poll = System.Linq.Enumerable.Single(PollTasks.Values);

            ((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(context);
            StringAssert.Contains(GetStatus(window), "Checking the saved approval", StringComparison.Ordinal);
            DesktopInstallLinkingStartupContext repeated = DesktopInstallLinkingRuntime.HandleBrowserApprovalReturn(HeadId, uri);
            ((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(repeated);
            Assert.AreEqual(1, PollTasks.Count, "Repeated activation must not create a second proof sender.");
            Assert.AreSame(poll, System.Linq.Enumerable.Single(PollTasks.Values));

            fixture.ReleaseResponses();
            Assert.IsTrue(poll.Wait(TimeSpan.FromSeconds(5)));
            poll.GetAwaiter().GetResult();
            await StepLoopAsync(10);
            Assert.IsTrue(DesktopInstallLinkingRuntime.IsClaimed(LoadState()));
            StringAssert.Contains(GetStatus(window), "linked", StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(before.BrowserApprovalReturnState, LoadState().BrowserApprovalReturnState);
            DesktopInstallLinkingStartupContext completedRepeat = DesktopInstallLinkingRuntime.HandleBrowserApprovalReturn(HeadId, uri);
            Assert.AreEqual("browser_return_linked", completedRepeat.PromptReason);
            Assert.AreEqual(0, PollTasks.Count);
        });
    }

    [TestMethod]
    public async Task Browser_return_stopped_or_terminal_never_replays_and_shows_recovery_guidance()
    {
        await WithWindowAsync(window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            string uri = "chummer://install-link?state=" + before.BrowserApprovalReturnState;
            AgeRetainedProof();
            string? retained = LoadState().BrowserApprovalRequest;
            DesktopInstallLinkingStartupContext stopped = DesktopInstallLinkingRuntime.HandleBrowserApprovalReturn(HeadId, uri);
            Assert.AreEqual("browser_return_stopped", stopped.PromptReason);
            ((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(stopped);
            StringAssert.Contains(GetStatus(window), "has stopped", StringComparison.Ordinal);
            Assert.AreEqual(0, PollTasks.Count);
            Assert.AreEqual(retained, LoadState().BrowserApprovalRequest);

            MarkTerminal();
            DesktopInstallLinkingStartupContext terminal = DesktopInstallLinkingRuntime.HandleBrowserApprovalReturn(HeadId, uri);
            Assert.AreEqual("browser_return_terminal", terminal.PromptReason);
            ((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(terminal);
            StringAssert.Contains(GetStatus(window), "rejected", StringComparison.Ordinal);
            Assert.AreEqual(0, PollTasks.Count);
            Assert.IsFalse(DesktopInstallLinkingRuntime.IsClaimed(LoadState()));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task Browser_return_pipe_routes_only_current_state_and_has_one_listener()
    {
        DesktopInstallLinkingState before = PrepareRetainedApproval();
        string uri = "chummer://install-link?state=" + before.BrowserApprovalReturnState;
        int received = 0;
        using DesktopInstallLinkActivation? primary = DesktopInstallLinkActivation.TryRegister(HeadId, (value, token) =>
        {
            Assert.AreEqual(uri, value);
            Interlocked.Increment(ref received);
            return Task.FromResult(DesktopInstallLinkingRuntime.IsCurrentBrowserApprovalReturn(LoadState(), value));
        });
        Assert.IsNotNull(primary);
        using DesktopInstallLinkActivation? second = DesktopInstallLinkActivation.TryRegister(HeadId, (_, _) => Task.FromResult(false));
        Assert.IsNull(second);
        Assert.IsTrue(await DesktopInstallLinkActivation.TryForwardAsync(HeadId, uri));
        Assert.IsTrue(await DesktopInstallLinkActivation.TryForwardAsync(HeadId, uri));
        Assert.AreEqual(2, received);
        Assert.IsFalse(await DesktopInstallLinkActivation.TryForwardAsync(HeadId, "chummer://install-link?state=stale"));
        Assert.AreEqual(2, received, "Invalid returns must never activate the owning window.");
        Assert.AreEqual(0, PollTasks.Count, "IPC is a wake-up path, not claim authority.");
        AssertNoReplacementApproval(before);
    }

    [TestMethod]
    public void Browser_return_rejects_stale_cross_install_and_unlinked_owner_without_mutation()
    {
        DesktopInstallLinkingState before = PrepareRetainedApproval();
        string uri = "chummer://install-link?state=" + before.BrowserApprovalReturnState;
        Assert.IsTrue(DesktopInstallLinkingRuntime.IsCurrentBrowserApprovalReturn(before, uri));
        foreach (string invalid in new[]
        {
            "chummer://install-link?state=stale",
            uri + "&state=stale", uri + "#state=stale", uri + "&callbackCode=synthetic",
            uri.Replace("install-link", "open"), "chummer://install-link"
        })
        {
            Assert.AreEqual("browser_return_rejected",
                DesktopInstallLinkingRuntime.HandleBrowserApprovalReturn(HeadId, invalid).PromptReason);
        }
        Assert.IsFalse(DesktopInstallLinkingRuntime.IsCurrentBrowserApprovalReturn(before with { InstallationId = "other-install" }, uri));
        Assert.IsFalse(DesktopInstallLinkingRuntime.IsCurrentBrowserApprovalReturn(before with { HeadId = "other-head" }, uri));
        Assert.IsFalse(DesktopInstallLinkingRuntime.IsCurrentBrowserApprovalReturn(before with { PublicKey = "other-key" }, uri));
        Assert.AreEqual(before.StateRevision, LoadState().StateRevision, "Rejected returns must not mutate saved authority.");
        Assert.AreEqual(0, PollTasks.Count);

        DesktopInstallLinkingRuntime.UnlinkInstall(HeadId);
        DesktopInstallLinkingState unlinked = LoadState();
        Assert.AreEqual("browser_return_rejected", DesktopInstallLinkingRuntime.HandleBrowserApprovalReturn(HeadId, uri).PromptReason);
        Assert.AreEqual(unlinked.StateRevision, LoadState().StateRevision);
        Assert.AreEqual(0, PollTasks.Count);
    }

    [TestMethod]
    public async Task Browser_return_accepts_renewed_proof_only_for_the_original_operation()
    {
        await WithWindowAsync(window =>
        {
            DesktopInstallLinkingState prepared = PrepareRetainedApproval();
            JsonObject oldProof = JsonNode.Parse(prepared.BrowserApprovalRequest!)!.AsObject();
            oldProof["issuedAtUnixSeconds"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60;
            DesktopInstallLinkingState before = prepared with
            {
                BrowserApprovalRequest = oldProof.ToJsonString(),
                BrowserApprovalUncertain = false
            };
            DesktopInstallLinkingStartupContext accepted = new(before, null, null, true, "browser_return_pending");
            MethodInfo sign = typeof(DesktopInstallLinkingRuntime).GetMethod("CreateRemoteCallbackPollRequest",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            object renewedProof = sign.Invoke(null, [before])!;
            string Serialize(object proof) => System.Text.Json.JsonSerializer.Serialize(proof, proof.GetType(),
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            DesktopInstallLinkingState renewed = before with { BrowserApprovalRequest = Serialize(renewedProof) };
            SaveState(renewed);
            Assert.AreNotEqual(before.BrowserApprovalRequest, LoadState().BrowserApprovalRequest);
            Assert.IsTrue(DesktopInstallLinkingWindow.IsCurrentBrowserReturnContext(LoadState(), accepted));
            Assert.IsTrue(((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(accepted));

            object differentProof = sign.Invoke(null, [before with { BrowserApprovalRequest = null }])!;
            DesktopInstallLinkingState different = renewed with { BrowserApprovalRequest = Serialize(differentProof) };
            Assert.IsFalse(DesktopInstallLinkingWindow.IsCurrentBrowserReturnContext(different, accepted),
                "A fresh operation must not inherit an earlier return even if its installation and return state match.");
            Assert.IsFalse(DesktopInstallLinkingRuntime.IsSameBrowserApprovalOperation(
                renewed with { BrowserApprovalRequest = "invalid json" }, before));
            Assert.AreEqual(0, PollTasks.Count, "Rendering a renewed proof must not dispatch another claim.");
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task Browser_return_revalidates_before_rendering_after_an_owner_change()
    {
        await WithWindowAsync(window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            DesktopInstallLinkingStartupContext accepted = new(before, null, null, true, "browser_return_pending");
            string status = GetStatus(window);
            DesktopInstallLinkingRuntime.UnlinkInstall(HeadId);
            Assert.IsFalse(((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(accepted));
            Assert.AreEqual(status, GetStatus(window), "A stale return must not render another operation's state.");
            Assert.AreEqual(0, PollTasks.Count);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task Both_account_buttons_update_for_linked_state_and_language()
    {
        await WithWindowAsync(window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            DesktopInstallLinkingStartupContext accepted = new(before, null, null, true, "browser_return_pending");
            DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState(HeadId);
            DesktopPreferenceRuntime.SaveState(HeadId, preferences with { Language = "de-de" });
            Assert.IsTrue(((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(accepted));
            foreach (string field in new[] { "_accountButton", "_moreToolsAccountButton" })
                Assert.AreEqual("Installation verlinken", GetPrivateField<Button>(window, field).Content);
            SaveState(ClaimedState());
            DesktopInstallLinkingStartupContext linked = new(LoadState(), null, null, true, "browser_return_linked");
            Assert.IsTrue(((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(linked));
            foreach (string field in new[] { "_accountButton", "_moreToolsAccountButton" })
                Assert.AreEqual("Konto öffnen", GetPrivateField<Button>(window, field).Content);
            DesktopPreferenceRuntime.SaveState(HeadId, preferences with { Language = "en-us" });
            Assert.IsTrue(((DesktopInstallLinkingWindow)window).ApplyBrowserReturn(linked));
            foreach (string field in new[] { "_accountButton", "_moreToolsAccountButton" })
                Assert.AreEqual("Open Account", GetPrivateField<Button>(window, field).Content);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public void Callback_listener_registration_preserves_normal_GUI_argument_modes()
    {
        string? prior = Environment.GetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS", null);
            Assert.IsTrue(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation([]));
            Assert.IsTrue(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation(["--install-claim-code", "synthetic"]));
            Assert.IsTrue(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation(["--desktop-mode=modern"]));
            Assert.IsTrue(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation(["chummer://open?kind=workspace"]));
            Assert.IsFalse(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation(["--install-link-headless"]));
            Assert.IsFalse(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation(["/install-link-console"]));
            Assert.IsFalse(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation(["--startup-smoke"]));
            Assert.IsFalse(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation(["--windows-persistence-probe"]));
            Assert.IsFalse(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation(["--mouse-first-user-journey"]));
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS", "1");
            Assert.IsFalse(global::Chummer.Avalonia.Program.ShouldRegisterInstallLinkActivation([]));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_INSTALL_LINK_HEADLESS", prior);
        }
    }

    [TestMethod]
    public async Task Matrix_browser_opened_shows_waiting_status()
    {
        await WithWindowAsync(async window =>
        {
            await InvokeHandlerAsync(window, opened: true, "https://hub.example/approve/op-1", null);
            AssertFallbackHidden(window);
            StringAssert.Contains(GetStatus(window), "Opened the claim page", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Matrix_browser_failed_with_usable_url_shows_manual_link()
    {
        await WithWindowAsync(async window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            string url = DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(before);
            Assert.IsFalse(string.IsNullOrWhiteSpace(url));

            await InvokeHandlerAsync(window, opened: false, url, "browser launch failed");

            AssertFallbackShown(window, url);
            AssertNoReplacementApproval(before);
        });
    }

    [TestMethod]
    public async Task Matrix_browser_failed_then_delayed_claim_shows_linked()
    {
        // Audit regression: completing approval through the SAME URL must
        // reach the dialog — the manual fallback and continued observation
        // coexist on one operation.
        await WithWindowAsync(async window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            string url = DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(before);

            await InvokeHandlerAsync(window, opened: false, url, "browser launch failed");
            AssertFallbackShown(window, url);

            SaveState(ClaimedState());
            await StepLoopAsync(maxSteps: 90);

            StringAssert.Contains(GetStatus(window), "Linked. Grant expires", StringComparison.Ordinal);
            AssertNoReplacementApproval(before);
        });
    }

    [TestMethod]
    public async Task Matrix_recovering_without_url_shows_guidance_not_empty_fallback()
    {
        await WithWindowAsync(async window =>
        {
            PrepareRetainedApproval();
            MarkUncertain();

            await InvokeHandlerAsync(window, opened: false, string.Empty, null);

            AssertFallbackHidden(window);
            StringAssert.Contains(GetStatus(window), "still being checked", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Matrix_aged_uncertain_shows_stopped()
    {
        await WithWindowAsync(async window =>
        {
            PrepareRetainedApproval();
            MarkUncertain();
            AgeRetainedProof();

            await InvokeHandlerAsync(window, opened: false, string.Empty, null);

            AssertFallbackHidden(window);
            StringAssert.Contains(GetStatus(window), "has stopped", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Matrix_terminal_rejection_shows_rejection()
    {
        await WithWindowAsync(async window =>
        {
            PrepareRetainedApproval();
            MarkTerminal();

            await InvokeHandlerAsync(window, opened: false, string.Empty, null);

            AssertFallbackHidden(window);
            StringAssert.Contains(GetStatus(window), "was rejected", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Matrix_concurrent_link_completes_shows_linked()
    {
        await WithWindowAsync(async window =>
        {
            SaveState(ClaimedState());

            await InvokeHandlerAsync(window, opened: false, string.Empty, null);

            AssertFallbackHidden(window);
            StringAssert.Contains(GetStatus(window), "Linked. Grant expires", StringComparison.Ordinal);
        });
    }

    // ------------------------------------------------------------------
    // §3 — observation lifetime, driven through the real loop.
    // ------------------------------------------------------------------

    /// <summary>
    /// Virtual-time stepping: the loop's clock seam advances one observation
    /// tick (30s of virtual time) per StepLoop step, so a full observation
    /// lifetime elapses in deterministic ticks without wall-clock waiting.
    /// </summary>
    private static void InstallVirtualClock()
    {
        DateTimeOffset current = DateTimeOffset.UtcNow;
        DesktopInstallLinkingWindow.ObservationNow = () =>
        {
            DateTimeOffset value = current;
            current = value.AddSeconds(30);
            return value;
        };
    }

    [TestMethod]
    public async Task Observation_claim_after_former_60s_boundary_reaches_dialog()
    {
        await WithWindowAsync(async window =>
        {
            InstallVirtualClock();
            DesktopInstallLinkingWindow.ObservationLifetime =
                static () => TimeSpan.FromMinutes(15);
            await InvokeHandlerAsync(window, opened: true, "https://hub.example/approve/op-2", null);

            // The claim lands after ~3 virtual minutes (well past the former
            // 60s loop bound, inside the real 15-minute lifetime).
            await StepLoopAsync(maxSteps: 5);
            SaveState(ClaimedState());
            await StepLoopAsync(maxSteps: 90);

            StringAssert.Contains(GetStatus(window), "Linked. Grant expires", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Observation_aged_uncertain_after_boundary_shows_stopped()
    {
        await WithWindowAsync(async window =>
        {
            PrepareRetainedApproval();
            await InvokeHandlerAsync(window, opened: true, "https://hub.example/approve/op-3", null);

            SaveState(LoadState() with
            {
                BrowserApprovalUncertain = true,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            AgeRetainedProof();

            await StepLoopAsync(maxSteps: 90);

            StringAssert.Contains(GetStatus(window), "has stopped", StringComparison.Ordinal);
            AssertFallbackHidden(window);
        });
    }

    [TestMethod]
    public async Task Observation_expiry_unclaimed_shows_refresh_affordance()
    {
        await WithWindowAsync(async window =>
        {
            // Virtual clock: the full 15-minute lifetime elapses after 30
            // ticks; the loop must then present the explicit re-check
            // affordance instead of implying live monitoring.
            InstallVirtualClock();
            DesktopInstallLinkingWindow.ObservationLifetime =
                static () => TimeSpan.FromMinutes(15);
            // No state changes intervene in this case. Complete the virtual
            // ticks synchronously instead of counting dispatcher timer pumps.
            DesktopInstallLinkingWindow.ObservationDelay = static (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            };
            await InvokeHandlerAsync(window, opened: true, "https://hub.example/approve/op-7", null);


            Assert.IsTrue(IsRefreshAffordanceVisible(window),
                "refresh affordance must appear when observation ends unclaimed");
            StringAssert.Contains(GetMatrixHandoffState(window), "Observation paused", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Observation_rejection_after_boundary_shows_rejection()
    {
        await WithWindowAsync(async window =>
        {
            await InvokeHandlerAsync(window, opened: true, "https://hub.example/approve/op-4", null);

            SaveState(LoadState() with
            {
                BrowserApprovalTerminal = true,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

            await StepLoopAsync(maxSteps: 90);

            StringAssert.Contains(GetStatus(window), "was rejected", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Observation_stops_on_context_change()
    {
        await WithWindowAsync(async window =>
        {
            await InvokeHandlerAsync(window, opened: true, "https://hub.example/approve/op-5", null);

            // A foreign operation appears under the same head id: the owner
            // transition must end THIS dialog's observation without touching
            // the new operation. Simulated via a new approval return-state
            // binding (the InstallationId field is conflict-guarded by the
            // state store, as a corrupted-store signal).
            SaveState(LoadState() with
            {
                BrowserApprovalReturnState = "returnstate-other-999",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

            await StepLoopAsync(maxSteps: 90);

            StringAssert.Contains(GetMatrixHandoffState(window), "Observation ended", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Matrix_browser_fallback_is_hidden_after_rejection()
    {
        await WithWindowAsync(async window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            string url = DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(before);
            await InvokeHandlerAsync(window, false, url, "browser launch failed");
            AssertFallbackShown(window, url);
            MarkTerminal();
            await StepLoopAsync(90);
            AssertFallbackHidden(window);
            StringAssert.Contains(GetStatus(window), "was rejected", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Observation_replacement_is_not_retired_by_the_cancelled_loop()
    {
        await WithWindowAsync(async window =>
        {
            var firstDelay = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int ticks = 0;
            DesktopInstallLinkingWindow.ObservationDelay = (_, token) =>
                ++ticks == 1 ? firstDelay.Task : Task.Delay(1, token);
            await InvokeHandlerAsync(window, true, "https://hub.example/approve/original", null);
            await InvokeHandlerAsync(window, true, "https://hub.example/approve/original", null);
            firstDelay.SetResult(true);
            await StepLoopAsync(5);
            Assert.AreNotEqual("Observation ended", GetMatrixHandoffState(window));
            SaveState(ClaimedState());
            await StepLoopAsync(90);
            StringAssert.Contains(GetStatus(window), "Linked. Grant expires", StringComparison.Ordinal);
        });
    }

    [TestMethod]
    public async Task Observation_does_not_adopt_a_later_account_transition()
    {
        await WithWindowAsync(async window =>
        {
            await InvokeHandlerAsync(window, true, "https://hub.example/approve/original", null);
            SaveState(ClaimedState());
            SaveState(ClaimedState() with { UserId = "other-user", SubjectId = "other-subject" });
            await StepLoopAsync(90);
            Assert.AreEqual("Observation ended", GetMatrixHandoffState(window));
            Assert.IsFalse(DesktopInstallLinkingRuntime.IsClaimed(GetPrivateField<DesktopInstallLinkingState>(window, "_state")));
        });
    }

    [TestMethod]
    public async Task Observation_refresh_does_not_switch_to_a_replacement_approval()
    {
        await WithWindowAsync(async window =>
        {
            DesktopInstallLinkingWindow.ObservationMaxSteps = 0;
            await InvokeHandlerAsync(window, true, "https://hub.example/approve/original", null);
            Assert.IsTrue(IsRefreshAffordanceVisible(window));
            StringAssert.Contains(GetStatus(window), "Automatic checking paused", StringComparison.Ordinal);
            string? original = GetPrivateField<DesktopInstallLinkingState>(window, "_state").BrowserApprovalReturnState;
            SaveState(LoadState() with { BrowserApprovalReturnState = "replacement-approval" });
            var refresh = typeof(DesktopInstallLinkingWindow).GetMethod("RefreshObservationAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)refresh.Invoke(window, null)!;
            Assert.AreEqual("Observation ended", GetMatrixHandoffState(window));
            Assert.AreEqual(original, GetPrivateField<DesktopInstallLinkingState>(window, "_state").BrowserApprovalReturnState);
            Assert.IsFalse(IsRefreshAffordanceVisible(window));
        });
    }

    [TestMethod]
    public async Task Integration_inflight_completed_response_reaches_dialog_across_proof_boundary()
    {
        // Audit P2-1 regression, worker + observer together: the runtime
        // worker admits a request, the loopback Hub withholds the response
        // BODY past the (overridden) proof-reuse boundary while the dialog's
        // observer runs. The dialog must NOT declare recovery stopped while
        // the admitted response is in flight, and the released completed
        // response must reach the dialog ("Linked") without reopening it.
        Environment.SetEnvironmentVariable("CHUMMER_PROOF_REUSE_SECONDS", "1");
        DesktopInstallLinkingWindow.ObservationDelay = static (ms, ct) => Task.Delay(15, ct);
        DesktopInstallLinkingWindow.ObservationMaxSteps = 4000;
        try
        {
            await WithWindowAsync(async window =>
            {
                using InflightHubFixture fixture = new(pendingFirst: false, holdBody: true);
                DesktopInstallLinkingState before = PrepareRetainedApproval();
                string url = DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(before);
                await InvokeHandlerAsync(window, opened: false, url, "browser launch failed");
                AssertFallbackShown(window, url);

                Assert.IsTrue(DesktopInstallLinkingRuntime.TryResumeRemoteCallbackPolling(
                    DesktopInstallLinkingRuntime.LoadOrCreateState(HeadId)));
                Assert.IsTrue(DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(
                    DesktopInstallLinkingRuntime.LoadOrCreateState(HeadId).InstallationId));

                // Let the observer run past the overridden proof-reuse
                // boundary while the response body is still withheld.
                await StepLoopAsync(maxSteps: 90);

                DesktopInstallLinkingState during = DesktopInstallLinkingRuntime.LoadOrCreateState(HeadId);
                Assert.IsTrue(DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(during.InstallationId),
                    "precondition: worker still registered while its response is in flight");
                Assert.IsFalse(GetStatus(window).Contains("has stopped", StringComparison.Ordinal), "stopped copy must not appear while the admitted response is in flight");
                Assert.IsFalse(GetStatus(window).Contains("was rejected", StringComparison.Ordinal), "rejection copy must not appear while the admitted response is in flight");

                fixture.ReleaseResponses();
                await StepLoopAsync(maxSteps: 300);
                string status = GetStatus(window);
                bool linked = status.Contains("Linked. Grant expires", StringComparison.Ordinal)
                    || status.Contains("already linked", StringComparison.Ordinal);
                Assert.IsTrue(linked,
                    "the released completed response must reach the dialog as a linked state, got: " + status);
            });
        }
        finally
        {
            DesktopInstallLinkingWindow.ObservationDelay = static (ms, ct) => Task.Delay(ms, ct);
            DesktopInstallLinkingWindow.ObservationMaxSteps = int.MaxValue;
            Environment.SetEnvironmentVariable("CHUMMER_PROOF_REUSE_SECONDS", null);
        }
    }

    [TestMethod]
    public async Task Integration_worker_ended_then_refresh_resumes_network_check()
    {
        // Audit P2-2 regression: the network worker ends (lifetime overridden
        // to 1s) after a definitive pending outcome, THEN the approval
        // completes remotely. The real RefreshObservationAsync must resume
        // the network check (re-signing is permitted because the outcome was
        // definitive, not uncertain) and the dialog must reach "Linked"
        // through the SAME retained operation - not an idle local wait.
        Environment.SetEnvironmentVariable(
            "CHUMMER_INSTALL_LINK_OBSERVATION_LIFETIME_SECONDS", "1");
        DesktopInstallLinkingWindow.ObservationDelay = static (ms, ct) => Task.Delay(15, ct);
        DesktopInstallLinkingWindow.ObservationMaxSteps = 4000;
        try
        {
            await WithWindowAsync(async window =>
            {
                using InflightHubFixture fixture = new(pendingFirst: true, holdBody: false);
                DesktopInstallLinkingState before = PrepareRetainedApproval();
                string url = DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(before);
                await InvokeHandlerAsync(window, opened: false, url, "browser launch failed");
                AssertFallbackShown(window, url);

                Assert.IsTrue(DesktopInstallLinkingRuntime.TryResumeRemoteCallbackPolling(
                    DesktopInstallLinkingRuntime.LoadOrCreateState(HeadId)));

                // Worker 1: gets the definitive pending response, then exits
                // when its 1s lifetime elapses between iterations. Sync-pump
                // (Thread.Sleep + RunJobs) - never await on the headless
                // dispatcher, or continuations starve (R76 lesson).
                for (int i = 0; i < 200; i++)
                {
                    Thread.Sleep(25);
                    Dispatcher.UIThread.RunJobs();
                    if (!DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(
                            LoadState().InstallationId))
                    {
                        break;
                    }
                }
                Assert.IsFalse(DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(LoadState().InstallationId),
                    "precondition: the network worker must have ended");
                DesktopInstallLinkingState ended = LoadState();
                Assert.IsFalse(ended.BrowserApprovalUncertain,
                    "the definitive pending response must have cleared uncertainty");
                Assert.IsFalse(DesktopInstallLinkingRuntime.IsClaimed(ended),
                    "precondition: the operation is still unclaimed when the worker ends");

                // The approval completes AFTER the worker ended. A refresh
                // that only re-read the local store could never see this.
                fixture.MarkApprovalAvailableRemotely();

                MethodInfo refresh = typeof(DesktopInstallLinkingWindow).GetMethod(
                        "RefreshObservationAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new AssertFailedException("RefreshObservationAsync missing");
                // Do NOT await the refresh task directly on the blocked headless
                // dispatcher: fire it and sync-pump until the resumed worker is
                // registered (the same starvation pattern R76 closed).
                Task refreshTask = (Task)refresh.Invoke(window, null)!;
                bool resumed = false;
                for (int i = 0; i < 240; i++)
                {
                    Thread.Sleep(25);
                    Dispatcher.UIThread.RunJobs();
                    if (DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(LoadState().InstallationId))
                    {
                        resumed = true;
                        break;
                    }
                }
                Assert.IsTrue(resumed, "refresh must have resumed the network check");

                await StepLoopAsync(maxSteps: 300);
                string finalStatus = GetStatus(window);
                bool linked = finalStatus.Contains("Linked. Grant expires", StringComparison.Ordinal)
                    || finalStatus.Contains("already linked", StringComparison.Ordinal);
                Assert.IsTrue(linked,
                    "the resumed worker must complete the SAME operation in the dialog, got: " + finalStatus);
            });
        }
        finally
        {
            DesktopInstallLinkingWindow.ObservationDelay = static (ms, ct) => Task.Delay(ms, ct);
            DesktopInstallLinkingWindow.ObservationMaxSteps = int.MaxValue;
            Environment.SetEnvironmentVariable(
                "CHUMMER_INSTALL_LINK_OBSERVATION_LIFETIME_SECONDS", null);
        }
    }

    /// <summary>
    /// Loopback Hub for worker+observer integration. pendingFirst: the first
    /// poll request is answered with a definitive 202 pending body (the
    /// runtime clears uncertainty and keeps going); later requests get a
    /// valid completed grant. holdBody: a 200 response's status line is sent
    /// immediately (request admitted) but the body is withheld until
    /// ReleaseResponses, realizing an in-flight response across the
    /// proof-reuse boundary.
    /// </summary>
    private sealed class InflightHubFixture : IDisposable
    {
        private readonly System.Net.Sockets.Socket _listener;
        private readonly string _stateRoot;
        private readonly string _priorStateRoot;
        private readonly string _priorApiBase;
        private readonly string _priorProofReuse;
        private readonly bool _pendingFirst;
        private readonly bool _holdBody;
        private Task? _listenerTask;
        private readonly List<Task> _handlerTasks = new();
        private readonly object _handlerLock = new();
        private readonly List<TaskCompletionSource<bool>> _gates = new();
        private readonly object _gateLock = new();
        private bool _responsesReleased;
        private readonly string _completedBody;
        private int _requests;
        private volatile bool _approvalAvailable;

        public InflightHubFixture(bool pendingFirst, bool holdBody)
        {
            _pendingFirst = pendingFirst;
            _holdBody = holdBody;
            _priorProofReuse = Environment.GetEnvironmentVariable("CHUMMER_PROOF_REUSE_SECONDS") ?? "";
            _priorStateRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT") ?? "";
            _priorApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL") ?? "";
            _stateRoot = Path.Combine(Path.GetTempPath(), "chummer-inflight-matrix", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_stateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", _stateRoot);
            using System.Security.Cryptography.RSA installationKey = System.Security.Cryptography.RSA.Create(2048);
            DesktopInstallLinkingState initial = LoadState();
            SaveState(initial with
            {
                Status = "guest",
                ClaimedAtUtc = null,
                GrantId = null,
                GrantToken = null,
                GrantIssuedAtUtc = null,
                GrantExpiresAtUtc = null,
                PublicKey = installationKey.ExportRSAPublicKeyPem(),
                PrivateKey = installationKey.ExportPkcs8PrivateKeyPem()
            });

            _listener = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);
            _listener.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            _listener.Listen(16);
            string apiBase = $"http://127.0.0.1:{((System.Net.IPEndPoint)_listener.LocalEndPoint!).Port}/";
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", apiBase);

            string publicKey = Convert.ToBase64String(installationKey.ExportSubjectPublicKeyInfo());
            _completedBody = BuildMatrixGrantResponse(LoadState(), publicKey);

            _listenerTask = Task.Run(async () =>
            {
                while (true)
                {
                    System.Net.Sockets.Socket connection = await _listener.AcceptAsync();
                    Task handler = Task.Run(async () =>
                    {
                        try
                        {
                            using System.Net.Sockets.Socket socket = connection;
                            byte[] buffer = new byte[65536];
                            await socket.ReceiveAsync(buffer, System.Net.Sockets.SocketFlags.None);
                            System.Threading.Interlocked.Increment(ref _requests);
                            // Pending until the approval is marked available
                            // remotely: worker 1 then ALWAYS ends unclaimed at
                            // its deadline (the audit's scenario), and the
                            // post-deadline resumed worker gets the grant.
                            bool pending = _pendingFirst && !_approvalAvailable;
                            byte[] payload = System.Text.Encoding.UTF8.GetBytes(pending
                                ? "{\"pending\":true}"
                                : _completedBody);
                            string status = pending ? "202 Pending" : "200 OK";
                            string header = "HTTP/1.1 " + status + "\r\n" +
                                            "Content-Type: application/json\r\n" +
                                            "Content-Length: " + payload.Length + "\r\n" +
                                            "Connection: close\r\n\r\n";
                            await socket.SendAsync(
                                System.Text.Encoding.ASCII.GetBytes(header),
                                System.Net.Sockets.SocketFlags.None);
                            if (!pending && _holdBody)
                            {
                                // Body withheld: the response is in flight.
                                TaskCompletionSource<bool> gate = new(
                                    System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
                                lock (_gateLock)
                                {
                                    if (_responsesReleased) gate.TrySetResult(true);
                                    else _gates.Add(gate);
                                }
                                await gate.Task;
                            }
                            else if (!pending)
                            {
                                // Give the observer a beat to observe the
                                // in-flight boundary before instant completion.
                                await System.Threading.Tasks.Task.Delay(50);
                            }
                            await socket.SendAsync(payload, System.Net.Sockets.SocketFlags.None);
                        }
                        catch (Exception ex) when (ex is System.ObjectDisposedException
                            or System.Net.Sockets.SocketException
                            or System.OperationCanceledException
                            or System.IO.IOException)
                        {
                        }
                    });
                    lock (_handlerLock) { _handlerTasks.Add(handler); }
                }
            });
        }

        public void ReleaseResponses()
        {
            List<TaskCompletionSource<bool>> gates;
            lock (_gateLock)
            {
                _responsesReleased = true;
                gates = new List<TaskCompletionSource<bool>>(_gates);
                _gates.Clear();
            }
            foreach (TaskCompletionSource<bool> gate in gates)
            {
                gate.TrySetResult(true);
            }
        }

        /// <summary>
        /// Journey marker: from this point the Hub has a completed result for
        /// the retained approval (it always serves one in this fixture); the
        /// point is that it became available only AFTER the worker ended.
        /// </summary>
        public void MarkApprovalAvailableRemotely()
        {
            _approvalAvailable = true;
        }

        public void Dispose()
        {
            // Audit-required teardown order (test isolation): request bounded
            // shutdown, stop accepting, finish accepted handlers, await the
            // runtime worker, THEN restore shared configuration and state.
            try { _listener.Dispose(); _listenerTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
            Assert.IsTrue(_listenerTask?.IsCompleted == true,
                "The accept loop must stop before taking the handler snapshot.");
            ReleaseResponses();

            Task[] handlers;
            lock (_handlerLock) { handlers = _handlerTasks.ToArray(); }
            try { Task.WhenAll(handlers).Wait(TimeSpan.FromSeconds(5)); } catch { }
            int unfinishedHandlers = handlers.Count(h => !h.IsCompleted);
            int unfinishedGates;
            lock (_gateLock) { unfinishedGates = _gates.Count; }

            // Join every still-registered runtime worker (bounded) BEFORE
            // touching process-wide environment or state.
            System.Collections.Concurrent.ConcurrentDictionary<string, Task> pollTasks = PollTasks;
            Task workerJoin = Task.CompletedTask;
            Task[] running = pollTasks.Values.ToArray();
            if (running.Length > 0)
            {
                workerJoin = Task.WhenAll(running);
            }

            Task finished = Task.WhenAny(workerJoin, Task.Delay(TimeSpan.FromSeconds(5)));
            try { finished.GetAwaiter().GetResult(); } catch { }
            int remainingWorkers = pollTasks?.Count ?? 0;

            // Teardown acceptance (audit §3): no fixture-owned work may
            // survive shared-state restoration. An assert here turns any
            // leaked handler/worker into a deterministic red, in any order.
            if (unfinishedHandlers != 0 || unfinishedGates != 0 || remainingWorkers != 0 || !workerJoin.IsCompleted)
            {
                throw new AssertFailedException(
                    $"fixture leaked work at teardown: {unfinishedHandlers} unfinished connection handler(s), "
                    + $"{unfinishedGates} unresolved response gate(s), {remainingWorkers} registered poll worker(s)");
            }
            // Observe faults as well as completion, before restoring any
            // process-wide configuration or deleting the fixture's state.
            workerJoin.GetAwaiter().GetResult();
            Task.WhenAll(handlers).GetAwaiter().GetResult();
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", _priorStateRoot);
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", _priorApiBase);
            Environment.SetEnvironmentVariable("CHUMMER_PROOF_REUSE_SECONDS", _priorProofReuse);
            try { if (Directory.Exists(_stateRoot)) Directory.Delete(_stateRoot, recursive: true); } catch { }
        }
    }

    private static string BuildMatrixGrantResponse(DesktopInstallLinkingState state, string publicKey)
    {
        // Same DTO shape the runtime integration tests use; issued in the past
        // (valid), expiring in 30 days.
        string issuedAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("o");
        string expiresAt = DateTimeOffset.UtcNow.AddDays(30).ToString("o");
        return $$"""
{
  "callback": {
    "callbackId": "callback-matrix-1",
    "callbackCode": "code-matrix-1",
    "installationId": "{{state.InstallationId}}",
    "artifactId": "avalonia-win-x64-installer",
    "channel": "{{state.ChannelId}}",
    "version": "{{state.ApplicationVersion}}",
    "installAccessClass": "open_public",
    "status": "redeemed",
    "createdAtUtc": "{{issuedAt}}",
    "expiresAtUtc": "{{expiresAt}}",
    "userId": "user-matrix-1",
    "subjectId": "subject.matrix",
    "publicKey": "{{publicKey}}",
    "headId": "{{state.HeadId}}",
    "platform": "{{state.Platform}}",
    "arch": "{{state.Arch}}",
    "hostLabel": null,
    "callbackUri": "http://127.0.0.1/matrix",
    "grantId": "grant-matrix-1"
  },
  "installation": {
    "installationId": "{{state.InstallationId}}",
    "artifactId": "avalonia-win-x64-installer",
    "channel": "{{state.ChannelId}}",
    "version": "{{state.ApplicationVersion}}",
    "installAccessClass": "open_public",
    "status": "active",
    "createdAtUtc": "{{issuedAt}}",
    "updatedAtUtc": "{{expiresAt}}",
    "userId": "user-matrix-1",
    "subjectId": "subject.matrix",
    "publicKey": "{{publicKey}}",
    "claimTicketId": "ticket-matrix-1",
    "headId": "{{state.HeadId}}",
    "platform": "{{state.Platform}}",
    "arch": "{{state.Arch}}",
    "hostLabel": null,
    "grantId": "grant-matrix-1"
  },
  "grant": {
    "grantId": "grant-matrix-1",
    "installationId": "{{state.InstallationId}}",
    "status": "active",
    "accessToken": "token-matrix-1",
    "issuedAtUtc": "{{issuedAt}}",
    "expiresAtUtc": "{{expiresAt}}",
    "userId": "user-matrix-1",
    "subjectId": "subject.matrix"
  },
  "alreadyClaimed": true
}
""";
    }

    [TestMethod]
    public async Task Resume_guard_refuses_claimed_and_terminal_operations()
    {
        // Mutation I kill: the resume entry point must refuse operations the
        // runtime has already finished with (claimed or conclusively
        // rejected), even though their retained request is still readable.
        // Without the guard the call would report success and start a new
        // network worker against a completed operation.
        await WithWindowAsync(async window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();

            // Claimed: the same operation, completed.
            SaveState(LoadState() with
            {
                Status = "claimed",
                ClaimedAtUtc = DateTimeOffset.UtcNow,
                GrantId = "grant-killtest-1",
                GrantToken = "token-killtest-1",
                LastClaimMessage = "Linked. Grant expires in 30 days.",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            Assert.IsTrue(DesktopInstallLinkingRuntime.IsClaimed(LoadState()),
                "precondition: claimed state");
            Assert.IsFalse(DesktopInstallLinkingRuntime.TryResumeRemoteCallbackPolling(LoadState()),
                "resume must refuse a claimed operation");

            // Terminal: conclusively rejected.
            SaveState(LoadState() with
            {
                Status = "guest",
                ClaimedAtUtc = null,
                GrantId = null,
                GrantToken = null,
                LastClaimMessage = null,
                BrowserApprovalTerminal = true,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            Assert.IsTrue(LoadState().BrowserApprovalTerminal, "precondition: terminal state");
            Assert.IsFalse(DesktopInstallLinkingRuntime.TryResumeRemoteCallbackPolling(LoadState()),
                "resume must refuse a terminal operation");

            // And a genuinely pending operation with a readable retained
            // request must still be resumable - the guard is narrow, not a
            // blanket veto. The positive case runs against a CONTROLLED
            // completing Hub response (audit §3B): the worker finishes inside
            // the test, and teardown never races a live worker against
            // environment restoration.
            SaveState(LoadState() with
            {
                BrowserApprovalTerminal = false,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            Assert.IsFalse(LoadState().BrowserApprovalTerminal, "precondition: pending state");
            using InflightHubFixture positiveCase = new(pendingFirst: false, holdBody: false);
            SaveState(PrepareRetainedApproval());
            bool resumed = DesktopInstallLinkingRuntime.TryResumeRemoteCallbackPolling(LoadState());
            Assert.IsTrue(resumed, "a pending operation with a retained request must be resumable");
            Assert.IsTrue(DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(LoadState().InstallationId),
                "resume must actually start the network worker");

            // Bounded shutdown: the controlled response completes the
            // operation, the worker exits, and it must be UNREGISTERED before
            // the fixture's Dispose restores shared state.
            for (int i = 0; i < 400; i++)
            {
                Thread.Sleep(25);
                Dispatcher.UIThread.RunJobs();
                if (!DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(LoadState().InstallationId))
                {
                    break;
                }
            }

            Assert.IsFalse(DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(LoadState().InstallationId),
                "the worker must have finished and unregistered before cleanup");
            Assert.IsTrue(DesktopInstallLinkingRuntime.IsClaimed(LoadState()),
                "the controlled response must have completed the same operation");
        });
    }

    [TestMethod]
    public async Task Refresh_states_explicit_limitation_when_resume_is_refused()
    {
        // Mutation M kill: when the runtime cannot resume the operation
        // (here: the retained request is unreadable), refresh must show the
        // explicit recovery-unavailable limitation instead of silently
        // starting another local wait.
        await WithWindowAsync(async window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            string url = DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(before);
            await InvokeHandlerAsync(window, opened: false, url, "browser launch failed");
            AssertFallbackShown(window, url);

            // Corrupt the retained request so TryResumeRemoteCallbackPolling
            // refuses (unreadable request - the explicit recovery case).
            SaveState(LoadState() with
            {
                BrowserApprovalRequest = "not-json",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

            var refresh = typeof(DesktopInstallLinkingWindow).GetMethod("RefreshObservationAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new AssertFailedException("RefreshObservationAsync missing");
            await (Task)refresh.Invoke(window, null)!;

            StringAssert.Contains(GetStatus(window), "cannot be resumed", StringComparison.Ordinal);
            StringAssert.Contains(GetMatrixHandoffState(window), "Recovery unavailable", StringComparison.Ordinal);
        });
    }

    // ------------------------------------------------------------------
    // Window-lifetime regressions (9 Nov re-audit P2 #2): closing the
    // optional linking window (or choosing "continue unlinked") must cancel
    // a not-yet-started automatic handoff and must not start new work after
    // asynchronous boundaries - while an already-dispatched operation stays
    // safely retained.
    // ------------------------------------------------------------------

    private static void InvokeBeginAutomaticHandoff(Window window)
    {
        MethodInfo begin = typeof(DesktopInstallLinkingWindow).GetMethod(
                "BeginAutomaticHandoffAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("BeginAutomaticHandoffAsync missing");
        begin.Invoke(window, null);
    }

    [TestMethod]
    public async Task Close_during_initial_handoff_delay_prevents_browser_dispatch()
    {
        // Audit regression row 1: close while the initial handoff delay is
        // held -> no browser dispatch and no newly started approval.
        TaskCompletionSource<bool> delayGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DesktopInstallLinkingWindow.HandoffDelay = (_, _) => delayGate.Task;
        int dispatches = 0;
        DesktopInstallLinkingWindow.PortalOpenOverride = _ =>
        {
            Interlocked.Increment(ref dispatches);
            return (true, "https://chummer.run/claim/never", null);
        };

        DesktopInstallLinkingState before = PrepareRetainedApproval();
        await WithWindowAsync(async window =>
        {
            InvokeBeginAutomaticHandoff(window);
            await StepLoopAsync(maxSteps: 6);
            Assert.AreEqual("Preparing local callback", GetMatrixHandoffState(window),
                "precondition: the handoff is waiting at its start delay");

            window.Close();
            PumpUi();

            // COMPLETE (do not cancel) the delay AFTER the close: the
            // continuation really resumes and must be stopped by the
            // lifetime recheck - the exact race the audit flagged. (The
            // cancelled-delay path is covered by the real-delay test below.)
            delayGate.TrySetResult(true);
            await StepLoopAsync(maxSteps: 30);

            Assert.AreEqual(0, dispatches, "a closed window must never dispatch the browser");
            StringAssert.DoesNotMatch(GetMatrixHandoffState(window),
                new System.Text.RegularExpressions.Regex("Browser fallback ready"),
                "cancellation must not be reported as a browser fallback outcome");
            AssertNoReplacementApproval(before);
        });
    }

    [TestMethod]
    public async Task Continue_unlinked_before_handoff_resumes_prevents_browser_dispatch()
    {
        // Audit regression row 2: select Continue unlinked before the handoff
        // resumes -> same result (no dispatch, no new approval).
        TaskCompletionSource<bool> delayGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DesktopInstallLinkingWindow.HandoffDelay = (_, _) => delayGate.Task;
        int dispatches = 0;
        DesktopInstallLinkingWindow.PortalOpenOverride = _ =>
        {
            Interlocked.Increment(ref dispatches);
            return (true, "https://chummer.run/claim/never", null);
        };

        DesktopInstallLinkingState before = PrepareRetainedApproval();
        await WithWindowAsync(async window =>
        {
            InvokeBeginAutomaticHandoff(window);
            await StepLoopAsync(maxSteps: 6);

            MethodInfo continueUnlinked = typeof(DesktopInstallLinkingWindow).GetMethod(
                    "ContinueUnlinkedAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("ContinueUnlinkedAsync missing");
            await (Task)continueUnlinked.Invoke(window, null)!;

            // Release the delay NORMALLY: the continuation resumes and must
            // still refuse to dispatch because the lifetime token is
            // cancelled (post-boundary guard, not the delay itself).
            delayGate.TrySetResult(true);
            await StepLoopAsync(maxSteps: 30);

            Assert.AreEqual(0, dispatches, "continue-unlinked must cancel the pending handoff");
            AssertNoReplacementApproval(before);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Close_during_clipboard_boundary_does_not_start_new_observer(bool clipboardFails)
    {
        // Audit regression row 3: close while the clipboard completion is
        // held -> no new window observer after closure (and no post-close
        // control updates).
        TaskCompletionSource<bool> clipboardGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DesktopInstallLinkingWindow.ClipboardWriteOverride = (_, _) => clipboardGate.Task;

        await WithWindowAsync(async window =>
        {
            DesktopInstallLinkingState before = PrepareRetainedApproval();
            string url = DesktopInstallLinkingRuntime.BuildClaimPortalAbsoluteUriForInstall(before);
            // Fire, don't await: the handler stays suspended inside the held
            // clipboard write, so awaiting it here would deadlock the test
            // body (the gate only completes after the close below).
            Task handlerTask = InvokeHandlerAsync(window, opened: false, url, "browser launch failed");
            for (int i = 0; i < 200; i++)
            {
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
                if (GetPrivateField<bool>(window, "_browserFallbackVisible"))
                {
                    break;
                }
            }

            AssertFallbackShown(window, url);

            // The observer token is null until the first StartClaimPolling;
            // read it null-safely and require it to be UNCHANGED after close
            // (a replaced token would mean a new observer started).
            FieldInfo tokenField = typeof(DesktopInstallLinkingWindow).GetField(
                    "_handoffPollCancellation", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("_handoffPollCancellation missing");
            object? observerTokenBefore = tokenField.GetValue(window);
            await StepLoopAsync(maxSteps: 6);

            window.Close();
            PumpUi();

            string statusAtClose = GetStatus(window);
            if (clipboardFails) clipboardGate.TrySetException(new IOException("Controlled clipboard failure"));
            else clipboardGate.TrySetResult(true);
            await StepLoopAsync(maxSteps: 30);
            await handlerTask;

            Assert.AreEqual(statusAtClose, GetStatus(window),
                "Clipboard success or failure must not update a dismissed window.");
            StringAssert.DoesNotMatch(GetStatus(window), new System.Text.RegularExpressions.Regex("Copied"),
                "a closed window must not run the clipboard continuation's control updates");
            StringAssert.DoesNotMatch(GetMatrixHandoffState(window),
                new System.Text.RegularExpressions.Regex("Waiting for browser callback"),
                "a closed window must not start a new observer");
            object? observerTokenAfter = tokenField.GetValue(window);
            Assert.AreSame(observerTokenBefore, observerTokenAfter,
                "StartClaimPolling must not have replaced the observer token after close");
        });
    }

    [TestMethod]
    public async Task Close_while_cancellable_delay_pending_cancels_the_handoff_as_cancellation()
    {
        // A real cancellable delay honors the lifetime token: closing the
        // window cancels the pending delay, the continuation surfaces as
        // cancellation ("Handoff cancelled", never "Browser fallback ready"),
        // and no dispatch happens.
        TaskCompletionSource delayEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DesktopInstallLinkingWindow.HandoffDelay = (_, cancellationToken) =>
        {
            delayEntered.SetResult();
            return Task.Delay(Timeout.Infinite, cancellationToken);
        };
        int dispatches = 0;
        DesktopInstallLinkingWindow.PortalOpenOverride = _ =>
        {
            Interlocked.Increment(ref dispatches);
            return (true, "https://chummer.run/claim/never", null);
        };

        DesktopInstallLinkingState before = PrepareRetainedApproval();
        await WithWindowAsync(async window =>
        {
            InvokeBeginAutomaticHandoff(window);
            Assert.IsTrue(delayEntered.Task.IsCompleted, "The cancellable delay must be entered.");
            Assert.AreEqual("Preparing local callback", GetMatrixHandoffState(window),
                "precondition: the handoff is waiting for cancellation");

            window.Close();
            PumpUi();

            // Drain the cancelled continuation without racing a wall-clock deadline.
            await StepLoopAsync(maxSteps: 30);

            Assert.AreEqual(0, dispatches, "a closed window must never dispatch the browser");
            StringAssert.Contains(GetMatrixHandoffState(window), "Handoff cancelled", StringComparison.Ordinal);
            StringAssert.DoesNotMatch(GetMatrixHandoffState(window),
                new System.Text.RegularExpressions.Regex("Browser fallback ready"),
                "cancellation must not be reported as a browser fallback outcome");
            AssertNoReplacementApproval(before);
        });
    }

    [TestMethod]
    public async Task Close_after_dispatch_retains_original_operation()
    {
        // Audit regression row 4: close after a request was dispatched -> the
        // original operation remains safely retained (no false rejection, no
        // replacement approval). The lifetime token cancels window-owned
        // work; the runtime operation's identity and outcome are untouched.
        TaskCompletionSource<bool> delayGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DesktopInstallLinkingWindow.HandoffDelay = (_, _) => delayGate.Task;
        int dispatches = 0;
        DesktopInstallLinkingWindow.PortalOpenOverride = _ =>
        {
            Interlocked.Increment(ref dispatches);
            return (true, "https://chummer.run/claim/dispatched", null);
        };

        DesktopInstallLinkingState before = PrepareRetainedApproval();
        await WithWindowAsync(async window =>
        {
            InvokeBeginAutomaticHandoff(window);
            delayGate.TrySetResult(true);
            await StepLoopAsync(maxSteps: 30);
            Assert.AreEqual(1, dispatches, "precondition: the handoff dispatched");
            StringAssert.Contains(GetMatrixHandoffState(window), "Waiting for browser callback",
                StringComparison.Ordinal);

            window.Close();
            PumpUi();
            await StepLoopAsync(maxSteps: 30);

            // The dispatched operation is preserved: same retained request,
            // same installation, no replacement approval, no false terminal.
            Assert.AreEqual(before.BrowserApprovalRequest, LoadState().BrowserApprovalRequest,
                "the dispatched operation's retained approval must be unchanged after close");
            Assert.AreEqual(before.InstallationId, LoadState().InstallationId);
            Assert.IsFalse(LoadState().BrowserApprovalTerminal,
                "closing the window must not mark the dispatched operation rejected");
        });
    }

    [TestMethod]
    public async Task StartClaimPolling_refuses_new_observer_on_closed_window()
    {
        // Mutation F kill: the central guard in StartClaimPolling is the last
        // line of defense - the recovery_pending branch (no fallback, no URL)
        // reaches it without any other closure check, so closing the window
        // first must make it refuse to start a new observation loop.
        await WithWindowAsync(async window =>
        {
            PrepareRetainedApproval();

            FieldInfo tokenField = typeof(DesktopInstallLinkingWindow).GetField(
                    "_handoffPollCancellation", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertFailedException("_handoffPollCancellation missing");
            object? tokenBefore = tokenField.GetValue(window);

            window.Close();
            PumpUi();

            // URL-less recovery path: falls straight through to
            // StartClaimPolling - which must refuse on the closed window.
            Task handlerTask = InvokeHandlerAsync(window, opened: false, "", "browser launch failed");
            for (int i = 0; i < 200; i++)
            {
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
                if (handlerTask.IsCompleted)
                {
                    break;
                }
            }

            await handlerTask;
            Assert.AreSame(tokenBefore, tokenField.GetValue(window),
                "StartClaimPolling must not have replaced the observer token on a closed window");
            StringAssert.DoesNotMatch(GetMatrixHandoffState(window),
                new System.Text.RegularExpressions.Regex("Waiting for browser callback"),
                "no new observer may run for a closed window");
        });
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    private static System.Collections.Concurrent.ConcurrentDictionary<string, Task> PollTasks =>
        (System.Collections.Concurrent.ConcurrentDictionary<string, Task>)
        (typeof(DesktopInstallLinkingRuntime).GetField("RemoteCallbackPollTasks",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("RemoteCallbackPollTasks missing")).GetValue(null)!;

    [TestMethod]
    public void Runtime_poll_is_registered_before_start_returns_and_join_includes_cleanup()
    {
        using InflightHubFixture fixture = new(pendingFirst: false, holdBody: true);
        DesktopInstallLinkingState state = PrepareRetainedApproval();
        Assert.IsTrue(DesktopInstallLinkingRuntime.TryResumeRemoteCallbackPolling(state));
        Assert.AreEqual(1, PollTasks.Count,
            "Start must publish a non-null completion handle before returning.");
        Task worker = PollTasks.Values.Single();
        Assert.IsNotNull(worker);
        Assert.IsFalse(worker.IsCompleted, "The fixture still holds the response body.");
        fixture.ReleaseResponses();
        Assert.IsTrue(worker.Wait(TimeSpan.FromSeconds(5)), "The controlled poll must terminate.");
        worker.GetAwaiter().GetResult();
        Assert.AreEqual(0, PollTasks.Count);
        Assert.IsFalse(DesktopInstallLinkingRuntime.IsRemoteCallbackPollActive(state.InstallationId));
        Assert.IsTrue(DesktopInstallLinkingRuntime.IsClaimed(LoadState()));
    }

    private static DesktopInstallLinkingState LoadState()
        => DesktopInstallLinkingRuntime.LoadOrCreateState(HeadId);

    private static void SaveState(DesktopInstallLinkingState state)
    {
        // The runtime's optimistic-concurrency store compares StateRevision
        // against the record it last observed; tests replace the whole record,
        // so always stamp the freshest revision before saving through the
        // real update path.
        state = state with { StateRevision = LoadState().StateRevision };
        MethodInfo update = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "UpdateCurrentState", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("UpdateCurrentState missing");
        object?[] args =
        [
            state.HeadId,
            (Func<DesktopInstallLinkingState, DesktopInstallLinkingState?>)(_ => state)
        ];
        update.Invoke(null, args);
    }

    /// <summary>
    /// True when the loop's explicit re-check affordance is present and
    /// visible (observation ended unclaimed).
    /// </summary>
    private static bool IsRefreshAffordanceVisible(object window)
    {
        object? value = window.GetType()
            .GetField("_refreshObservationButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(window);
        return value is global::Avalonia.Controls.Button button && button.IsVisible;
    }
    private static DesktopInstallLinkingState ClaimedState()
        => LoadState() with
        {
            Status = "claimed",
            ClaimedAtUtc = DateTimeOffset.UtcNow,
            GrantId = "grant-matrix-1",
            GrantToken = "token-matrix-1",
            UserId = "user-matrix-1",
            SubjectId = "subject.matrix"
        };

    private static DesktopInstallLinkingState PrepareRetainedApproval()
    {
        DesktopInstallLinkingState created = LoadState();
        object?[] prepareArgs = [created, null, null];
        MethodInfo prepare = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "TryPrepareBrowserApproval", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("TryPrepareBrowserApproval missing");
        Assert.AreEqual(true, prepare.Invoke(null, prepareArgs));
        DesktopInstallLinkingState prepared = (DesktopInstallLinkingState)prepareArgs[1]!;

        object? retained = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ReadRetainedRemoteCallbackRequest", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, [prepared]);
        Assert.IsNotNull(retained, "retained request missing after prepare");

        object?[] retainArgs = [prepared, retained, true, false, null];
        MethodInfo retain = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "TryRetainRemoteCallbackRequest", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("TryRetainRemoteCallbackRequest missing");
        Assert.AreEqual(true, retain.Invoke(null, retainArgs));
        return LoadState();
    }

    private static void MarkUncertain()
        => SaveState(LoadState() with
        {
            BrowserApprovalUncertain = true,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

    private static void MarkTerminal()
        => SaveState(LoadState() with
        {
            BrowserApprovalTerminal = true,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

    /// <summary>
    /// Ages the retained proof beyond the reuse window: parse the retained
    /// request JSON, set IssuedAtUnixSeconds far in the past, re-serialize,
    /// and persist through the real save path, then verify the runtime's
    /// projection classifies the aged proof as Stopped (no re-signing).
    /// </summary>
    private static void AgeRetainedProof()
    {
        DesktopInstallLinkingState current = LoadState();
        Assert.IsFalse(string.IsNullOrWhiteSpace(current.BrowserApprovalRequest));

        JsonObject request = JsonNode.Parse(current.BrowserApprovalRequest)!.AsObject();
        request["IssuedAtUnixSeconds"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10_000;
        string agedJson = request.ToJsonString();

        // Round-trip through the runtime's own deserializer to prove the aged
        // JSON is still a valid retained request before persisting it.
        DesktopInstallLinkingState agedState = current with { BrowserApprovalRequest = agedJson };
        object? retained = typeof(DesktopInstallLinkingRuntime).GetMethod(
                "ReadRetainedRemoteCallbackRequest", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, [agedState]);
        Assert.IsNotNull(retained, "aged request failed to deserialize");

        SaveState(agedState with { BrowserApprovalUncertain = true });
        Assert.AreEqual(
            DesktopRemoteCallbackRecoveryProjection.Stopped,
            DesktopInstallLinkingRuntime.DescribeRemoteCallbackRecovery(LoadState()),
            "aging must flip the projection to Stopped before the loop runs");
    }

    private static void AssertNoReplacementApproval(DesktopInstallLinkingState before)
    {
        DesktopInstallLinkingState after = LoadState();
        Assert.AreEqual(before.BrowserApprovalRequest, after.BrowserApprovalRequest,
            "the retained approval request must be unchanged (no re-minting)");
        Assert.AreEqual(before.InstallationId, after.InstallationId);
        Assert.IsFalse(after.BrowserApprovalTerminal);
    }

    private static string GetStatus(Window window)
        => GetPrivateField<TextBlock>(window, "_statusText").Text ?? string.Empty;

    private static string GetMatrixHandoffState(Window window)
        => GetPrivateField<TextBlock>(window, "_matrixHandoffStateText").Text ?? string.Empty;

    private static void AssertFallbackShown(Window window, string url)
    {
        Assert.IsTrue(GetPrivateField<bool>(window, "_browserFallbackVisible"), "fallback should be visible");
        TextBlock urlText = GetPrivateField<TextBlock>(window, "_browserFallbackUrlText");
        Assert.AreEqual(url, urlText.Text, "the manual link must carry the SAME approval URL");
    }

    private static void AssertFallbackHidden(Window window)
        => Assert.IsFalse(GetPrivateField<bool>(window, "_browserFallbackVisible"), "fallback must stay hidden");

    private static T GetPrivateField<T>(object instance, string name)
    {
        FieldInfo field = instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException($"field {name} not found on {instance.GetType().Name}");
        object? value = field.GetValue(instance);
        return value is T typed
            ? typed
            : throw new AssertFailedException($"field {name} is not {typeof(T).Name}");
    }

    private static Task InvokeHandlerAsync(
        Window window, bool opened, string accountUrl, string? failureReason)
    {
        MethodInfo handler = typeof(DesktopInstallLinkingWindow).GetMethod(
                "HandlePortalOpenOutcomeAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("HandlePortalOpenOutcomeAsync missing");
        object? invoked = handler.Invoke(window, [opened, accountUrl, failureReason])
            ?? throw new AssertFailedException("handler returned null");
        return (Task)invoked;
    }

    /// <summary>
    /// Gives the observation loop time to run. The headless session's
    /// dispatcher only runs work that is pumped while the test body
    /// occupies the UI thread, so this helper blocks synchronously on the
    /// UI thread and pumps explicitly each step: the loop's
    /// ConfigureAwait(true) continuations execute inside these pumps and
    /// no async continuation is ever posted to an unpumped queue.
    /// </summary>
    private static Task StepLoopAsync(int maxSteps)
    {
        for (int step = 0; step < maxSteps; step++)
        {
            // Pump first (drains the previous step's posted tick), then sleep:
            // the sleep gives the loop's timer continuation time to post to the
            // dispatcher so the next pump executes it.
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(25);
        }

        Dispatcher.UIThread.RunJobs();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // Headless window harness (same shape as DesktopWindowContrastTests).
    // ------------------------------------------------------------------

    private sealed class MatrixHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .ConfigureFonts(static fontManager => fontManager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "fonts:Inter#Inter"
                })
                .WithInterFont();
        }
    }

    /// <summary>
    /// Creates the window in video-preview mode (no automatic handoff on
    /// open) inside a headless session, runs the body, then closes.
    /// </summary>
    private static Task WithWindowAsync(Func<Window, Task> testBody)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(MatrixHeadlessAppBootstrap));
                return session.Dispatch(async () =>
                {
                    // loginVideoPreview: true keeps the Opened handler passive —
                    // it never starts the real automatic handoff, so no real
                    // browser dispatch happens from window construction.
                    Window window = new DesktopInstallLinkingWindow(CreateStartupContext(), loginVideoPreview: true)
                    {
                        Width = 880,
                        Height = 540
                    };
                    window.Show();
                    PumpUi();

                    try
                    {
                        await testBody(window).ConfigureAwait(true);
                    }
                    finally
                    {
                        window.Close();
                        PumpUi();
                    }
                }, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                try
                {
                    session?.Dispose();
                }
                catch (NullReferenceException)
                {
                    // Avalonia Headless can throw during teardown.
                }
            }
        }
    }

    private static void PumpUi()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
        Dispatcher.UIThread.RunJobs();
    }

    private static DesktopInstallLinkingStartupContext CreateStartupContext()
    {
        return new DesktopInstallLinkingStartupContext(
            State: LoadState(),
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "fresh_install");
    }
}
