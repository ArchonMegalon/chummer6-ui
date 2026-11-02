using System;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// WINDOWS_HANDOFF 2026-11-02 item 2: the Finish-with-AI affordance ships gated
/// dark. These are the first headless tests for that surface: default-off, the
/// explicit session opt-in, the projection choke point (no busy/result bypass),
/// and the stable environment-switch contract.
/// </summary>
[TestClass]
public sealed class CharacterCreationAiFinishGateAvaloniaTests
{
    private const string FinishWithAiAutomationId = "creation-wizard-finish-with-ai";

    [TestMethod]
    public void Finish_with_ai_button_is_dark_until_the_feature_gate_is_enabled()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            // Overview projects full availability; the gate still keeps the
            // affordance dark by default.
            control.SetFinishWithAiAvailability(true);
            Button button = FindByAutomationId<Button>(control, FinishWithAiAutomationId);
            Assert.IsFalse(button.IsVisible, "Finish-with-AI must ship dark by default.");
            Assert.IsFalse(button.IsEnabled);

            int raiseCount = 0;
            control.FinishWithAiRequested += (_, _) => raiseCount++;
            RaiseClick(button);
            Assert.AreEqual(
                0, raiseCount, "A gated click must not raise FinishWithAiRequested.");

            host.Close();
        });
    }

    [TestMethod]
    public void Finish_with_ai_button_projects_once_the_feature_gate_is_enabled()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            int raiseCount = 0;
            control.FinishWithAiRequested += (_, _) => raiseCount++;

            control.SetFinishWithAiAvailability(true);
            control.SetFinishWithAiFeatureEnabled(true);

            Button button = FindByAutomationId<Button>(control, FinishWithAiAutomationId);
            Assert.IsTrue(button.IsVisible, "Enabled gate + availability must surface the affordance.");
            Assert.IsTrue(button.IsEnabled);
            RaiseClick(button);
            Assert.AreEqual(1, raiseCount);

            host.Close();
        });
    }

    [TestMethod]
    public void Finish_with_ai_busy_and_result_writers_cannot_bypass_the_gate()
    {
        WithHeadlessUi(() =>
        {
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();

            control.SetFinishWithAiAvailability(true);
            control.SetFinishWithAiFeatureEnabled(true);
            Button button = FindByAutomationId<Button>(control, FinishWithAiAutomationId);
            Assert.IsTrue(button.IsVisible);

            // While busy the button hides; a failure result arriving mid-flight
            // must not resurface it (the pre-gate writers did exactly that).
            control.SetFinishWithAiBusy(true);
            Assert.IsFalse(button.IsVisible);
            control.SetFinishWithAiResult(false, "Build Ghost proposal dismissed.");
            Assert.IsFalse(button.IsVisible, "Result writers must not bypass the gate.");

            // Busy completes: the affordance returns, still under the gate.
            control.SetFinishWithAiBusy(false);
            Assert.IsTrue(button.IsVisible);
            Assert.IsTrue(button.IsEnabled);

            // Disabling the feature re-hides immediately, availability aside.
            control.SetFinishWithAiFeatureEnabled(false);
            Assert.IsFalse(button.IsVisible);
            Assert.IsFalse(button.IsEnabled);

            int raiseCount = 0;
            control.FinishWithAiRequested += (_, _) => raiseCount++;
            RaiseClick(button);
            Assert.AreEqual(0, raiseCount, "A disabled gate must swallow clicks.");

            host.Close();
        });
    }

    [TestMethod]
    public void Ai_finish_gate_session_env_contract_is_stable()
    {
        Assert.AreEqual(
            "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH",
            CharacterCreationAiFinishGatePolicy.SessionEnvVariable);
    }

    private static T FindByAutomationId<T>(Control root, string automationId)
        where T : Control
        => root.GetVisualDescendants()
            .Prepend(root)
            .OfType<T>()
            .Single(control => string.Equals(
                control.GetValue(AutomationProperties.AutomationIdProperty),
                automationId,
                StringComparison.Ordinal));

    private static void RaiseClick(Button button)
        => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void WithHeadlessUi(Action action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(AiFinishGateHeadlessAppBootstrap));
                session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                try
                {
                    session?.Dispose();
                }
                catch (NullReferenceException)
                {
                    // Avalonia headless teardown can intermittently throw after successful dispatch.
                }
            }
        }
    }

    private sealed class AiFinishGateHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .ConfigureFonts(static manager => manager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
                .WithInterFont();
    }
}
