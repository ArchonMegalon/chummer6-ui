using System;
using System.Collections.Generic;
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
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CharacterCreationQualitiesAvaloniaTests
{
    [TestMethod]
    public void Quality_cards_dispatch_explicit_add_remove_and_busy_blocks_even_stale_clicks()
        => WithUi(() =>
        {
            var control = new CharacterCreationWizardControl();
            var host = new Window { Content = control };
            host.Show();
            control.SetQualitiesSelection(["A"]);
            control.SetState(State(CharacterCreationWizardStepIds.Qualities, [Option("A"), Option("B")]));
            host.UpdateLayout();
            var calls = new List<CharacterCreationQualitiesOptionClickRequested>();
            int foundationCalls = 0;
            control.QualitiesOptionClickRequested += (_, request) => calls.Add(request);
            control.FoundationOptionClickRequested += (_, _) => foundationCalls++;
            Button a = Find(control, "creation-wizard-option-A");
            Button b = Find(control, "creation-wizard-option-B");
            Assert.IsTrue(a.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Remove: A"));
            Assert.IsTrue(b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Add: B"));
            a.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(2, calls.Count);
            Assert.IsTrue(calls[0].Remove);
            Assert.IsFalse(calls[1].Remove);
            Assert.AreEqual(0, foundationCalls);
            control.QualitiesMutationBusy(true);
            host.UpdateLayout();
            Assert.IsFalse(Find(control, "creation-wizard-option-A").IsEnabled);
            Assert.IsFalse(Find(control, "creation-wizard-edit-qualities").IsEnabled);
            b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(2, calls.Count);
            Assert.IsTrue(control.GetVisualDescendants().OfType<ProgressBar>().Any(p => p.IsIndeterminate));
            host.Close();
        });

    [TestMethod]
    public void Empty_quality_catalog_still_offers_explicit_selection_review_and_visible_failures()
        => WithUi(() =>
        {
            var control = new CharacterCreationWizardControl();
            var host = new Window { Content = control };
            host.Show();
            control.SetState(State(CharacterCreationWizardStepIds.Qualities, []));
            host.UpdateLayout();
            int requests = 0;
            control.QualitiesEditRequested += (_, _) => requests++;
            Find(control, "creation-wizard-edit-qualities").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.AreEqual(1, requests);
            control.QualitiesStatus("Qualities could not be loaded. Reload the character.");
            host.UpdateLayout();
            Assert.IsTrue(control.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("could not be loaded", StringComparison.Ordinal) == true));
            host.Close();
        });

    [TestMethod]
    public void Magic_choices_are_unavailable_before_clicking_and_do_not_offer_enabled_cards()
        => WithUi(() =>
        {
            var control = new CharacterCreationWizardControl();
            var host = new Window { Content = control };
            host.Show();
            control.SetState(State(CharacterCreationWizardStepIds.MagicResonance, [Option("spell")]));
            host.UpdateLayout();
            Assert.IsFalse(control.GetVisualDescendants().OfType<Button>().Any(b => b.GetValue(AutomationProperties.AutomationIdProperty) == "creation-wizard-option-spell"));
            Assert.IsTrue(control.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("not available in this Windows build", StringComparison.Ordinal) == true));
            host.Close();
        });

    private static CharacterCreationWizardDesktopOption Option(string id)
        => new(id, null, id, true, null, new Dictionary<string, string>(), [], [], [], "SR5", 1);

    private static CharacterCreationWizardDesktopState State(string step, IReadOnlyList<CharacterCreationWizardDesktopOption> options)
        => new("qualities-ui", 1, "snapshot", step,
            [new CharacterCreationWizardDesktopStep(step, step, "available", true, true, false, true, [], [], [], [])],
            [], options, [], [], false, false, false, false,
            new(false, null), new(CharacterCreationWizardDesktopSchemas.BuildGhostContextV1,
                "qualities-ui", 1, "snapshot", step, "sr5", "runtime", "Priority", [], options, [], []));

    private static Button Find(Control control, string id)
        => control.GetVisualDescendants().OfType<Button>().Single(b => b.GetValue(AutomationProperties.AutomationIdProperty) == id);

    private static void WithUi(Action action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Bootstrap));
            session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    private sealed class Bootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>().UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .ConfigureFonts(static manager => manager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "fonts:Inter#Inter",
                    FontFallbacks = [new FontFallback { FontFamily = new FontFamily("fonts:Inter#Inter") }],
                    // Headless Linux hosts do not ship the Windows shell fonts.
                    FontFamilyMappings = new Dictionary<string, FontFamily>
                    {
                        ["Segoe UI"] = new("fonts:Inter#Inter"),
                        ["Verdana"] = new("fonts:Inter#Inter"),
                        ["Arial"] = new("fonts:Inter#Inter")
                    }
                }).WithInterFont();
    }
}
