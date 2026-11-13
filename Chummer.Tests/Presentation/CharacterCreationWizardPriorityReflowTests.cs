#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Fonts.Inter;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Rulesets.Hosting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// UX-04: the creation wizard's Priority ranks (A..E) must reflow at narrow
/// widths (960 DIP / 150% scaling): every rank button stays visible and
/// hittable when the host is constrained, instead of the fixed-width row
/// clipping the low ranks out of reach.
/// </summary>
[TestClass]
public sealed class CharacterCreationWizardPriorityReflowTests
{
    private const int NarrowWidth = 640;
    private const int NarrowHeight = 480;

    [TestMethod]
    public void Priority_ranks_reflow_at_narrow_width_and_all_five_ranks_stay_hittable()
    {
        WithUi((control, host) =>
        {
            control.SetState(StateWithMethodStep());
            control.SetPrerequisiteState(MakePrerequisiteState());
            PumpLayout(host, NarrowWidth, NarrowHeight);

            List<Button> rankButtons = control.GetVisualDescendants()
                .OfType<Button>()
                .Where(b => AutomationProperties.GetAutomationId(b)?.StartsWith("creation-wizard-priority-", StringComparison.Ordinal) == true)
                .ToList();
            Assert.IsTrue(rankButtons.Count >= 25, $"expected at least 25 rank buttons (5 categories x 5 ranks), got {rankButtons.Count}.");

            // Every rank button must start inside the window's bounds at 640 DIP.
            foreach (Button button in rankButtons)
            {
                var transform = button.TransformToVisual(host) ?? throw new InvalidOperationException("no transform");
                Point origin = transform.Transform(new Point(0, 0));
                Assert.IsTrue(
                    origin.X >= 0 && origin.X < host.Bounds.Width,
                    $"rank button '{AutomationProperties.GetAutomationId(button)}' escapes horizontally at {NarrowWidth} DIP: origin.X={origin.X}.");
            }

            // Specifically rank E of every category must be reachable.
            List<Button> eButtons = rankButtons
                .Where(b => AutomationProperties.GetAutomationId(b)!.EndsWith("-E", StringComparison.Ordinal))
                .ToList();
            Assert.AreEqual(5, eButtons.Count, "expected one rank-E button per category.");
            foreach (Button e in eButtons)
            {
                var transform = e.TransformToVisual(host) ?? throw new InvalidOperationException("no transform");
                Point origin = transform.Transform(new Point(0, 0));
                Assert.IsTrue(
                    origin.X >= 0 && origin.X < host.Bounds.Width,
                    $"rank E of a category escapes the window at {NarrowWidth} DIP: origin.X={origin.X}.");
                Assert.IsTrue(
                    e.Bounds.Width > 0,
                    $"rank E of a category collapsed to zero width at {NarrowWidth} DIP.");
            }
        });
    }

    [TestMethod]
    public void Priority_rank_click_still_selects_after_reflow()
    {
        WithUi((control, host) =>
        {
            control.SetState(StateWithMethodStep());
            control.SetPrerequisiteState(MakePrerequisiteState());
            PumpLayout(host, NarrowWidth, NarrowHeight);

            Button e = control.GetVisualDescendants()
                .OfType<Button>()
                .First(b => AutomationProperties.GetAutomationId(b) == "creation-wizard-priority-attributes-E");
            e.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(
                "E",
                control.GetPrioritySelections()["attributes"],
                "clicking the reflowed rank E must still select it.");
        });
    }

    private static void WithUi(Action<CharacterCreationWizardControl, Window> action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Bootstrap));
            session.Dispatch(() =>
            {
                var control = new CharacterCreationWizardControl();
                var host = new Window
                {
                    Content = control,
                    Width = NarrowWidth,
                    Height = NarrowHeight,
                };
                host.Show();
                try { action(control, host); } finally { host.Close(); }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    // Runs the layout pass synchronously inside the live session: headless
    // RunJobs never executes queued layout, and a layout left pending explodes
    // at ResetForUnitTests when the font collections are already disposed.
    private static void PumpLayout(Layoutable root, double width, double height)
    {
        root.InvalidateMeasure();
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        Dispatcher.UIThread.RunJobs();
    }

    private static CharacterCreationWizardDesktopState StateWithMethodStep()
    {
        string step = CharacterCreationWizardStepIds.Method;
        return new CharacterCreationWizardDesktopState(
            "priority-reflow-workspace", 1, "snapshot", step,
            [new(step, step, "available", true, true, false, true, [], [], [], [])],
            [], [], [], [], false, false, false, false,
            new(false, ""),
            new(
                CharacterCreationWizardDesktopSchemas.BuildGhostContextV1,
                "priority-reflow-workspace", 1, "snapshot", step, "sr5", "runtime-fingerprint", "priority",
                [], [], [], []),
            null);
    }

    private static CharacterCreationPrerequisiteState MakePrerequisiteState()
    {
        var options = new List<CharacterCreationPriorityOptionProjection>();
        foreach ((string category, string label) in new[]
                 {
                     ("heritage", "Heritage"),
                     ("talent", "Talent"),
                     ("attributes", "Attributes"),
                     ("skills", "Skills"),
                     ("resources", "Resources"),
                 })
        {
            foreach (string rank in new[] { "A", "B", "C", "D", "E" })
            {
                options.Add(new CharacterCreationPriorityOptionProjection(
                    CategoryId: category,
                    CategoryName: label,
                    Rank: rank,
                    SourceId: category + ":" + rank,
                    Label: rank + " " + label,
                    SumToTenValue: rank switch { "A" => 4, "B" => 3, "C" => 2, "D" => 1, _ => 0 },
                    BaseNormalAttributePoints: category == "attributes" ? rank switch { "A" => 22, _ => 18 } : null,
                    SourceNodeDigest: "digest-" + category + "-" + rank,
                    SourceAnchorIds: ["anchor-" + category + "-" + rank]));
            }
        }

        var authority = new CharacterCreationPrerequisiteAuthority(
            Schema: CharacterCreationPrerequisiteSchemas.AuthorityV1,
            SettingsProfileId: "default",
            BuildMethod: CharacterCreationBuildMethods.Priority,
            CreationKarmaTotal: null,
            PriorityArray: ["A", "B", "C", "D", "E"],
            PriorityTable: "Standard",
            SumToTenTarget: null,
            RankWeights: [],
            Options: options,
            RawProfileInputsDigest: "profile",
            RawPrioritiesXmlDigest: "priorities",
            EffectivePrioritiesInputsDigest: "effective",
            SelectedPriorityCustomDataInputsDigest: "custom",
            SourceAnchorIds: ["priorities.xml#priority-table"],
            Blockers: [],
            IsAuthoritative: true,
            AuthorityDigest: "authority");

        var binding = new CharacterCreationPrerequisiteBinding(
            WorkspaceId: new CharacterWorkspaceId("priority-reflow-workspace"),
            ContentRevision: 1,
            SavedRevision: 1,
            RawCharacterXmlDigest: "raw",
            AuxiliaryStateDigest: "aux",
            AuthorityDigest: "authority");

        var draft = new CharacterCreationPrerequisiteDraft(
            Schema: CharacterCreationPrerequisiteSchemas.DraftV1,
            WorkspaceId: new CharacterWorkspaceId("priority-reflow-workspace"),
            DraftRevision: 1,
            BaseContentRevision: 1,
            BaseRawCharacterXmlDigest: "raw",
            AuthorityDigest: "authority",
            BuildMethod: CharacterCreationBuildMethods.Priority,
            SettingsProfileId: "default",
            PriorityTable: "Standard",
            PriorityArray: ["A", "B", "C", "D", "E"],
            SumToTenTarget: null,
            Assignments: [],
            CreationKarmaTotal: 0,
            CreationKarmaUsed: 0,
            SourceAnchorIds: [],
            DraftDigest: "draft");

        return new CharacterCreationPrerequisiteState(
            Schema: CharacterCreationPrerequisiteSchemas.SnapshotV1,
            Binding: binding,
            RulesetId: "sr5",
            BuildMethod: CharacterCreationBuildMethods.Priority,
            CharacterCreated: false,
            Authority: authority,
            CreationKarmaBudget: new CharacterCreationBudgetState(
                BudgetId: "karma",
                Label: "Creation karma",
                Total: 50m,
                Used: 0m,
                Remaining: 50m,
                IsExact: true,
                Blockers: [],
                Unit: "karma"),
            PendingDraft: null,
            BaseNormalAttributePoints: null,
            RequiresMetatypeAttributeAdjustment: false,
            CanEnterAttributes: true,
            Blockers: [],
            SnapshotDigest: "digest");
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
                    FontFamilyMappings = new Dictionary<string, FontFamily>
                    {
                        ["Segoe UI"] = new("fonts:Inter#Inter"),
                        ["Verdana"] = new("fonts:Inter#Inter"),
                        ["Arial"] = new("fonts:Inter#Inter"),
                    },
                }).WithInterFont();
    }
}
