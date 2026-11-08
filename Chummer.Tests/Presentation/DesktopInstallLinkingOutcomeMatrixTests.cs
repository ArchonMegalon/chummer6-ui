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
        DesktopInstallLinkingWindow.ObservationDelay = static (ms, ct) => Task.Delay(ms, ct);
        DesktopInstallLinkingWindow.ObservationNow = static () => DateTimeOffset.UtcNow;
        DesktopInstallLinkingWindow.ObservationLifetime = static () =>
            DesktopInstallLinkingRuntime.ResolveRemoteCallbackObservationLifetime();
        DesktopInstallLinkingWindow.ObservationMaxSteps = int.MaxValue;
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
            await InvokeHandlerAsync(window, opened: true, "https://hub.example/approve/op-7", null);

            await StepLoopAsync(maxSteps: 40);

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

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

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
