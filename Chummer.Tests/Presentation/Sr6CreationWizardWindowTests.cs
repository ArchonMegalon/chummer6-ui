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
    public void Development_wizard_defaults_on_and_supports_explicit_opt_out()
    {
        string? before = Environment.GetEnvironmentVariable(Sr6CreationWizardGatePolicy.SessionEnvVariable);
        try
        {
            Environment.SetEnvironmentVariable(Sr6CreationWizardGatePolicy.SessionEnvVariable, null);
            Assert.IsTrue(Sr6CreationWizardGatePolicy.IsSessionEnabled());
            Environment.SetEnvironmentVariable(Sr6CreationWizardGatePolicy.SessionEnvVariable, "0");
            Assert.IsFalse(Sr6CreationWizardGatePolicy.IsSessionEnabled());
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

    [TestMethod]
    public void Confirmation_is_voided_when_the_selection_changes_before_finish()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter();
            using var scope = new WizardScope(presenter);
            var window = scope.Window;
            // Reach the confirm step with an acknowledged preview.
            var ranks = window.GetVisualDescendants().OfType<ComboBox>().ToArray();
            string[] choices = ["A", "B", "C", "D", "E"];
            for (int i = 0; i < ranks.Length; i++) ranks[i].SelectedItem = choices[i];
            Click(window, "NextButton");
            var lists = window.GetVisualDescendants().OfType<ListBox>().ToArray();
            lists[0].SelectedItem = "Human";
            lists[1].SelectedItem = "mundane";
            Click(window, "NextButton");
            Click(window, "NextButton");
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
            Click(window, "FinishButton");
            Assert.AreEqual(1, presenter.Confirms);
            // Back out, change a rank, come forward again: the new preview has a
            // new digest, so the earlier acknowledgement must NOT dispatch.
            Click(window, "BackButton");
            Click(window, "BackButton");
            Click(window, "BackButton");
            window.GetVisualDescendants().OfType<ComboBox>().First().SelectedItem = "B";
            Click(window, "NextButton");
            Click(window, "NextButton");
            Click(window, "NextButton");
            var confirmCheck = window.GetVisualDescendants().OfType<CheckBox>().Single();
            Assert.IsFalse(confirmCheck.IsChecked == true, "New digest voids the earlier acknowledgement.");
            Click(window, "FinishButton");
            Assert.AreEqual(1, presenter.Confirms, "A stale acknowledgement must not dispatch the new preview.");
        });

    [TestMethod]
    public void Store_read_failure_on_open_surfaces_as_blockers_not_crash()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter { ThrowOnLoad = true };
            using var scope = new WizardScope(presenter);
            StringAssert.Contains(scope.Window.FindControl<TextBlock>("AuthorityStatusText")!.Text!, "store-read-failed");
        });

    [TestMethod]
    public void Store_read_failure_on_preview_surfaces_as_blockers_not_crash()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter { ThrowOnPrepare = true };
            using var scope = new WizardScope(presenter);
            Click(scope.Window, "NextButton");
            Click(scope.Window, "NextButton");
            Click(scope.Window, "NextButton");
            StringAssert.Contains(scope.Window.FindControl<TextBlock>("AuthorityStatusText")!.Text!, "store-read-failed");
            Assert.AreEqual(1, presenter.Prepares);
        });

    [TestMethod]
    public void Commit_with_failed_refresh_is_reported_as_saved_not_total_failure()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter { RefreshFailsAfterCommit = true };
            using var scope = new WizardScope(presenter);
            var window = scope.Window;
            var ranks = window.GetVisualDescendants().OfType<ComboBox>().ToArray();
            string[] choices = ["A", "B", "C", "D", "E"];
            for (int i = 0; i < ranks.Length; i++) ranks[i].SelectedItem = choices[i];
            Click(window, "NextButton");
            var lists = window.GetVisualDescendants().OfType<ListBox>().ToArray();
            lists[0].SelectedItem = "Human";
            lists[1].SelectedItem = "mundane";
            Click(window, "NextButton");
            Click(window, "NextButton");
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
            Click(window, "FinishButton");
            Assert.AreEqual(1, presenter.Confirms);
            StringAssert.Contains(window.FindControl<TextBlock>("FooterStatusText")!.Text!, "Saved; the post-save refresh failed");
            StringAssert.Contains(window.FindControl<TextBlock>("AuthorityStatusText")!.Text!, "refresh-authority-required");
        });

    [TestMethod]
    public void Finish_gate_requires_acknowledgement_digest_to_match_the_held_preview()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter();
            using var scope = new WizardScope(presenter);
            var window = scope.Window;
            var ranks = window.GetVisualDescendants().OfType<ComboBox>().ToArray();
            string[] choices = ["A", "B", "C", "D", "E"];
            for (int i = 0; i < ranks.Length; i++) ranks[i].SelectedItem = choices[i];
            Click(window, "NextButton");
            var lists = window.GetVisualDescendants().OfType<ListBox>().ToArray();
            lists[0].SelectedItem = "Human";
            lists[1].SelectedItem = "mundane";
            Click(window, "NextButton");
            Click(window, "NextButton");
            // Reach the state the gate exists for: the held preview's digest has
            // changed since the user acknowledged (a replacement path that did
            // not void the ack). Reflection is the documented way to pin the
            // window field without a UI route that re-acks.
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
            var field = typeof(Sr6CreationWizardWindow).GetField("_confirmedPreviewDigest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field);
            field.SetValue(window, "sha256:stale-stale-stale");
            Click(window, "FinishButton");
            Assert.AreEqual(0, presenter.Confirms, "A mismatched acknowledgement must not dispatch.");
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
    public bool PointBuy, ThrowOnConfirm, ThrowOnLoad, ThrowOnPrepare, RefreshFailsAfterCommit;
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
    {
        if (ThrowOnLoad) throw new IOException("test store read failed");
        return new(CharacterCreationFoundationOutcomes.Success, new(State(overview), []), []);
    }
    public Sr6CreationFoundationInteractionPrepareResult Prepare(CharacterOverviewState overview, Sr6CreationFoundationSelection selection)
    {
        Prepares++;
        if (ThrowOnPrepare) throw new IOException("test store read failed");
        LastSelection = selection;
        var state = State(overview);
        var preview = new Sr6CreationFoundationPreview(state.Binding, selection, new(24, 32, 50000, 6, "E"), 0, 0, [], Digest);
        return new(CharacterCreationFoundationOutcomes.Success, new(state, []), new(state, preview, Digest), []);
    }
    public Sr6CreationFoundationInteractionConfirmResult Confirm(CharacterOverviewState overview, Sr6CreationFoundationInteractionConfirmation confirmation)
    {
        Confirms++;
        if (ThrowOnConfirm) throw new IOException("test uncertain outcome");
        if (RefreshFailsAfterCommit)
        {
            Sr6CreationFoundationConfirmRequest command = new(
                confirmation.PreparedPreview.Foundation.Binding,
                confirmation.PreparedPreview.Preview.Selection,
                confirmation.PreviewDigest,
                confirmation.OperationId,
                ExplicitlyConfirmed: true);
            Sr6CreationFoundationDecision seed = new(
                Sr6CreationFoundationDecision.SchemaV1,
                command,
                confirmation.PreparedPreview.Preview,
                CommittedContentRevision: confirmation.PreparedPreview.Foundation.Binding.ContentRevision + 1,
                Digest);
            Sr6CreationFoundationDecision decision = seed with
            {
                DecisionDigest = Sr6CreationFoundationIntegrity.DecisionDigest(seed),
            };
            return new(
                CharacterCreationFoundationOutcomes.Conflict,
                confirmation.PreparedPreview,
                new Sr6CreationFoundationCommit(decision, Replayed: false),
                null,
                [Sr6CreationFoundationInteractionBlockers.RefreshAuthorityRequired]);
        }
        return new(CharacterCreationFoundationOutcomes.Blocked, confirmation.PreparedPreview, null, null, ["test-blocked"]);
    }
    public Sr6CreationFinalizationInteractionReviewResult ReviewFinalization(CharacterOverviewState overview, Sr6CreationFoundationBinding binding)
        => new(CharacterCreationFoundationOutcomes.Blocked, null, ["test-blocked"]);
    public Sr6CreationFinalizationInteractionConfirmResult ConfirmFinalization(CharacterOverviewState overview, Sr6CreationFinalizationReview review, bool acceptUnspentLoss)
        => throw new AssertFailedException("No finalization expected");
}
