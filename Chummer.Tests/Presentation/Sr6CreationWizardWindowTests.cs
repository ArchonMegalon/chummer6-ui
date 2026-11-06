using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Avalonia.Controls;
using Chummer.Avalonia.Creation;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class Sr6CreationWizardWindowTests
{
    [TestMethod]
    public void Development_wizard_requires_explicit_session_opt_in()
    {
        string? before = Environment.GetEnvironmentVariable(Sr6CreationWizardGatePolicy.SessionEnvVariable);
        try
        {
            foreach (string? value in new string?[] { null, "", "0", "true" })
            {
                Environment.SetEnvironmentVariable(Sr6CreationWizardGatePolicy.SessionEnvVariable, value);
                Assert.IsFalse(Sr6CreationWizardGatePolicy.IsSessionEnabled());
            }
            Environment.SetEnvironmentVariable(Sr6CreationWizardGatePolicy.SessionEnvVariable, "1");
            Assert.IsTrue(Sr6CreationWizardGatePolicy.IsSessionEnabled());
        }
        finally { Environment.SetEnvironmentVariable(Sr6CreationWizardGatePolicy.SessionEnvVariable, before); }
    }

    [TestMethod]
    public void Fresh_priority_route_collects_foundation_before_preview_and_preserves_back_navigation()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter();
            using var scope = new WizardScope(presenter);
            var window = scope.Window;
            var ranks = window.GetVisualDescendants().OfType<ComboBox>().ToArray();
            Assert.HasCount(5, ranks);
            string[] choices = ["A", "B", "C", "D", "E"];
            for (int i = 0; i < ranks.Length; i++) ranks[i].SelectedItem = choices[i];
            Click(window, "NextButton");
            Assert.AreEqual(0, presenter.Prepares, "Do not request an incomplete full preview before inputs are reachable.");
            var lists = window.GetVisualDescendants().OfType<ListBox>().ToArray();
            Assert.HasCount(2, lists);
            lists[0].SelectedItem = "Human";
            lists[1].SelectedItem = "mundane";
            Click(window, "NextButton");
            var points = window.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
            points[0].Value = 2;
            Click(window, "NextButton");
            Assert.AreEqual(1, presenter.Prepares);
            Assert.AreEqual("Human", presenter.LastSelection!.MetatypeId);
            Assert.AreEqual(2, presenter.LastSelection.Attributes!.Allocations[0].AttributePoints);
            Click(window, "BackButton");
            Assert.IsTrue(window.FindControl<Button>("NextButton")!.IsEnabled);
            Assert.AreEqual(2m, window.GetVisualDescendants().OfType<NumericUpDown>().First().Value);
            Click(window, "BackButton");
            Click(window, "BackButton");
            CollectionAssert.AreEqual(choices, window.GetVisualDescendants().OfType<ComboBox>().Select(x => x.SelectedItem).ToArray());
        });

    [TestMethod]
    public void Reopened_foundation_retains_saved_attributes_and_domains_outside_this_editor()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter();
            var foundation = presenter.Load(CharacterOverviewState.Empty).State!.Foundation;
            var skills = new Sr6CreationSkillSelection([new("Athletics", 3, [])]);
            var selection = new Sr6CreationFoundationSelection("Human", "mundane", [])
            {
                Attributes = new([new("Body", 2, 0)]), Skills = skills
            };
            presenter.SavedState = foundation with
            {
                Selection = new(foundation.Binding, selection, new(24, 32, 50000, 6, "E"), 0, 0, [], "saved-preview")
            };
            using var scope = new WizardScope(presenter);
            Click(scope.Window, "NextButton");
            CollectionAssert.AreEqual(new[] { "Human", "mundane" }, scope.Window.GetVisualDescendants().OfType<ListBox>()
                .Select(x => x.SelectedItem).ToArray());
            Click(scope.Window, "NextButton");
            Assert.AreEqual(2m, scope.Window.GetVisualDescendants().OfType<NumericUpDown>().First().Value);
            Click(scope.Window, "NextButton");
            Assert.AreSame(skills, presenter.LastSelection!.Skills, "Editing foundation must not erase a saved domain the editor does not offer.");
        });

    [TestMethod]
    public void Point_buy_values_survive_return_to_method_page()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter { PointBuy = true };
            using var scope = new WizardScope(presenter);
            scope.Window.GetVisualDescendants().OfType<NumericUpDown>().First().Value = 3;
            Click(scope.Window, "NextButton");
            Click(scope.Window, "BackButton");
            Assert.AreEqual(3m, scope.Window.GetVisualDescendants().OfType<NumericUpDown>().First().Value);
        });

    [TestMethod]
    public void Changed_context_closes_the_old_window_before_another_authority_call()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            bool current = true;
            var presenter = new RecordingSr6WizardPresenter();
            using var scope = new WizardScope(presenter, () => current);
            current = false;
            Click(scope.Window, "NextButton");
            Assert.IsFalse(scope.Window.IsVisible);
            Assert.AreEqual(0, presenter.Prepares);
            Assert.AreEqual(0, presenter.Confirms);
        });

    [TestMethod]
    public void Interrupted_confirmation_cannot_dispatch_again()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter { ThrowOnConfirm = true };
            using var scope = new WizardScope(presenter);
            Click(scope.Window, "NextButton");
            Click(scope.Window, "NextButton");
            Click(scope.Window, "NextButton");
            scope.Window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
            Click(scope.Window, "FinishButton");
            Click(scope.Window, "FinishButton"); // Programmatic click also cannot replay.
            Assert.AreEqual(1, presenter.Confirms);
            Assert.IsFalse(scope.Window.FindControl<Button>("FinishButton")!.IsEnabled);
            StringAssert.Contains(scope.Window.FindControl<TextBlock>("AuthorityStatusText")!.Text!, "could not be verified");
        });

    private static void Click(Sr6CreationWizardWindow window, string name)
        => window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private sealed class WizardScope : IDisposable
    {
        public Sr6CreationWizardWindow Window { get; }
        public WizardScope(RecordingSr6WizardPresenter presenter, Func<bool>? current = null)
        {
            Window = new(presenter, CharacterOverviewState.Empty, current);
            Window.Show();
        }
        public void Dispose() => Window.Close();
    }
}

public sealed class RecordingSr6WizardPresenter : ISr6CreationFoundationInteractionPresenter
{
    public int Prepares, Confirms;
    public bool PointBuy, ThrowOnConfirm;
    public Sr6CreationFoundationSelection? LastSelection;
    public Sr6CreationFoundationState? SavedState;
    private Sr6CreationFoundationState State(CharacterOverviewState overview) => SavedState ?? new(
        new(overview.WorkspaceId ?? new CharacterWorkspaceId("sr6-test"), 1, 1, Digest, Digest, Digest),
        PointBuy ? Sr6CharacterCreationBuildMethods.PointBuy : Sr6CharacterCreationBuildMethods.Priority,
        [new("Human", ["A", "B", "C", "D", "E"])], [new("mundane", ["E"])], null)
    {
        AttributeOptions = [new("Body", 1, 6, true, true)],
        PointBuyLimits = new(100, 1, 1, 1, 20, 20, 20, 20, 1, 1, 1, 1, 1000, 10, 50)
    };
    private const string Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public Sr6CreationFoundationInteractionLoadResult Load(CharacterOverviewState overview)
        => new(CharacterCreationFoundationOutcomes.Success, new(State(overview), []), []);
    public Sr6CreationFoundationInteractionPrepareResult Prepare(CharacterOverviewState overview, Sr6CreationFoundationSelection selection)
    {
        Prepares++;
        LastSelection = selection;
        var state = State(overview);
        var preview = new Sr6CreationFoundationPreview(state.Binding, selection, new(24, 32, 50000, 6, "E"), 0, 0, [], Digest);
        return new(CharacterCreationFoundationOutcomes.Success, new(state, []), new(state, preview, Digest), []);
    }
    public Sr6CreationFoundationInteractionConfirmResult Confirm(CharacterOverviewState overview, Sr6CreationFoundationInteractionConfirmation confirmation)
    {
        Confirms++;
        if (ThrowOnConfirm) throw new IOException("test uncertain outcome");
        return new(CharacterCreationFoundationOutcomes.Blocked, confirmation.PreparedPreview, null, null, ["test-blocked"]);
    }
    public Sr6CreationFinalizationInteractionReviewResult ReviewFinalization(CharacterOverviewState overview, Sr6CreationFoundationBinding binding)
        => new(CharacterCreationFoundationOutcomes.Blocked, null, ["test-blocked"]);
    public Sr6CreationFinalizationInteractionConfirmResult ConfirmFinalization(CharacterOverviewState overview, Sr6CreationFinalizationReview review, bool acceptUnspentLoss)
        => throw new AssertFailedException("No finalization expected");
}
