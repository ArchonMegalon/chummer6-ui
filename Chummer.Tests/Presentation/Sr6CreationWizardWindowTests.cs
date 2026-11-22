using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    public void Talent_domain_editor_renders_published_catalog_on_confirm_step()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter();
            var foundation = presenter.Load(CharacterOverviewState.Empty).State!.Foundation;
            var selection = new Sr6CreationFoundationSelection("Human", "magician", [])
            {
                Spells = new(["sr6-spell-test-a"]),
            };
            presenter.SavedState = foundation with
            {
                SpellOptions = [new("sr6-spell-test-a", "Test Spell A", "spell", "combat", "anchor-a"),
                    new("sr6-spell-test-b", "Test Spell B", "spell", "detection", "anchor-b")],
                Selection = new(foundation.Binding, selection, new(24, 32, 50000, 6, "E"), 0, 0, [], "saved-preview"),
            };
            using var scope = new WizardScope(presenter);
            var window = scope.Window;
            var ranks = window.GetVisualDescendants().OfType<ComboBox>().ToArray();
            string[] choices = ["A", "B", "C", "D", "E"];
            for (int i = 0; i < ranks.Length; i++) ranks[i].SelectedItem = choices[i];
            Click(window, "NextButton");
            var lists = window.GetVisualDescendants().OfType<ListBox>().ToArray();
            lists[0].SelectedItem = "Human";
            lists[1].SelectedItem = "magician";
            Click(window, "NextButton");
            Click(window, "NextButton");
            // The confirm step now surfaces the talent-domain catalog as checkboxes,
            // seeded from the saved selection (sr6-spell-test-a checked).
            var domainChecks = window.GetVisualDescendants().OfType<CheckBox>()
                .Where(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c)?
                    .StartsWith("sr6-talent-domain-", StringComparison.Ordinal) == true).ToArray();
            Assert.AreEqual(2, domainChecks.Length, "both published catalog options must render");
            Assert.IsTrue(domainChecks.First(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "sr6-talent-domain-sr6-spell-test-a").IsChecked == true,
                "the saved selection seeds the checkbox state");
            Assert.IsTrue(domainChecks.First(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "sr6-talent-domain-sr6-spell-test-b").IsChecked != true,
                "unpicked catalog options start unchecked");
        });

    [TestMethod]
    public void Toggling_a_talent_domain_pick_updates_selection_and_voids_the_acknowledgement()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter();
            var foundation = presenter.Load(CharacterOverviewState.Empty).State!.Foundation;
            var selection = new Sr6CreationFoundationSelection("Human", "magician", [])
            {
                Spells = new(["sr6-spell-test-a"]),
            };
            presenter.SavedState = foundation with
            {
                SpellOptions = [new("sr6-spell-test-a", "Test Spell A", "spell", "combat", "anchor-a"),
                    new("sr6-spell-test-b", "Test Spell B", "spell", "detection", "anchor-b")],
                Selection = new(foundation.Binding, selection, new(24, 32, 50000, 6, "E"), 0, 0, [], "saved-preview"),
            };
            using var scope = new WizardScope(presenter);
            var window = scope.Window;
            var ranks = window.GetVisualDescendants().OfType<ComboBox>().ToArray();
            string[] choices = ["A", "B", "C", "D", "E"];
            for (int i = 0; i < ranks.Length; i++) ranks[i].SelectedItem = choices[i];
            Click(window, "NextButton");
            var lists = window.GetVisualDescendants().OfType<ListBox>().ToArray();
            lists[0].SelectedItem = "Human";
            lists[1].SelectedItem = "magician";
            Click(window, "NextButton");
            Click(window, "NextButton");
            var confirmCheck = window.GetVisualDescendants().OfType<CheckBox>()
                .First(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "sr6-confirm-checkbox");
            confirmCheck.IsChecked = true;
            Click(window, "FinishButton");
            Assert.AreEqual(1, presenter.Confirms);
            // Back out to attributes, come forward: the domain editor is re-rendered;
            // toggling the unpicked spell must update the saved selection and VOID
            // the earlier acknowledgement (new preview digest).
            var domainB = window.GetVisualDescendants().OfType<CheckBox>()
                .First(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "sr6-talent-domain-sr6-spell-test-b");
            domainB.IsChecked = true;
            Assert.IsFalse(window.FindControl<Button>("FinishButton")!.IsEnabled,
                "a domain change voids the earlier acknowledgement (finish needs a fresh ack)");
            // The window is the draft authority for this bounded editor, and the
            // Blocked stub leaves Back/Next disabled, so the draft is read from the
            // window state (same reflection pattern as the digest-gate test).
            const System.Reflection.BindingFlags Priv = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var stateObj = typeof(Sr6CreationWizardWindow).GetField("_state", Priv)!.GetValue(window)!;
            var foundationObj = stateObj.GetType().GetProperty("Foundation")!.GetValue(stateObj)!;
            var selStateObj = foundationObj.GetType().GetProperty("Selection")!.GetValue(foundationObj)!;
            var draft = (Sr6CreationFoundationSelection)selStateObj.GetType().GetProperty("Selection")!.GetValue(selStateObj)!;
            Assert.IsTrue(draft.Spells!.CatalogIds.Contains("sr6-spell-test-b"),
                "the toggle must land in the in-window draft selection");
            var heldDigest = (string?)typeof(Sr6CreationWizardWindow)
                .GetField("_confirmedPreviewDigest", Priv)!.GetValue(window);
            Assert.IsNull(heldDigest, "a domain change must void the held acknowledgement digest");
            // A stale or fresh press cannot dispatch again: the Blocked outcome keeps
            // the confirmation latched and all footer buttons disabled.
            Click(window, "FinishButton");
            Assert.AreEqual(1, presenter.Confirms, "the stale acknowledgement must not dispatch the new preview");
        });

    [TestMethod]
    public void Mystic_adept_renders_both_spell_and_power_domains()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter();
            var foundation = presenter.Load(CharacterOverviewState.Empty).State!.Foundation;
            var selection = new Sr6CreationFoundationSelection("Human", "mystic-adept", [])
            {
                Spells = new(["sr6-spell-test-a"]),
                AdeptPowers = new([new("sr6-power-test-a", 2)]),
            };
            presenter.SavedState = foundation with
            {
                SpellOptions = [new("sr6-spell-test-a", "Test Spell A", "spell", "combat", "anchor-a"),
                    new("sr6-spell-test-b", "Test Spell B", "spell", "detection", "anchor-b")],
                AdeptPowerOptions = [new("sr6-power-test-a", "Test Power A", null, null, "use-a", 4, 6, "anchor-pa"),
                    new("sr6-power-test-b", "Test Power B", null, null, "use-b", 4, 6, "anchor-pb")],
                Selection = new(foundation.Binding, selection, new(24, 32, 50000, 6, "E"), 0, 0, [], "saved-preview"),
            };
            using var scope = new WizardScope(presenter);
            var window = scope.Window;
            var ranks = window.GetVisualDescendants().OfType<ComboBox>().ToArray();
            string[] choices = ["A", "B", "C", "D", "E"];
            for (int i = 0; i < ranks.Length; i++) ranks[i].SelectedItem = choices[i];
            Click(window, "NextButton");
            var lists = window.GetVisualDescendants().OfType<ListBox>().ToArray();
            lists[0].SelectedItem = "Human";
            lists[1].SelectedItem = "mystic-adept";
            Click(window, "NextButton");
            Click(window, "NextButton");
            var checks = window.GetVisualDescendants().OfType<CheckBox>()
                .Where(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c)?
                    .StartsWith("sr6-talent-domain-", StringComparison.Ordinal) == true).ToArray();
            Assert.HasCount(4, checks, "both domains must render: two spells + two powers");
            Assert.IsTrue(checks.First(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "sr6-talent-domain-sr6-spell-test-a").IsChecked == true,
                "the saved spell seeds checked");
            Assert.IsTrue(checks.First(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "sr6-talent-domain-sr6-power-test-a").IsChecked == true,
                "the saved power seeds checked (both domains seeded from the same selection)");

            // A rated power (rating 2) SURVIVES toggling the unrelated spell:
            // rating preservation applies across domains.
            checks.First(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "sr6-talent-domain-sr6-spell-test-b").IsChecked = true;
            const System.Reflection.BindingFlags Priv = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var stateObj = typeof(Sr6CreationWizardWindow).GetField("_state", Priv)!.GetValue(window)!;
            var foundationObj = stateObj.GetType().GetProperty("Foundation")!.GetValue(stateObj)!;
            var selStateObj = foundationObj.GetType().GetProperty("Selection")!.GetValue(foundationObj)!;
            var draft = (Sr6CreationFoundationSelection)selStateObj.GetType().GetProperty("Selection")!.GetValue(selStateObj)!;
            Assert.IsTrue(draft.Spells!.CatalogIds.Contains("sr6-spell-test-b"), "the spell toggle landed");
            var rated = draft.AdeptPowers!.Choices.Single(c => c.CatalogId == "sr6-power-test-a");
            Assert.AreEqual(2, rated.Rating, "an unchanged rated choice must keep its rating when a spell toggles");
        });

    [TestMethod]
    public void Adept_power_rating_survives_toggling_another_power()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var presenter = new RecordingSr6WizardPresenter();
            var foundation = presenter.Load(CharacterOverviewState.Empty).State!.Foundation;
            var selection = new Sr6CreationFoundationSelection("Human", "adept", [])
            {
                AdeptPowers = new([new("sr6-power-test-a", 3)]),
            };
            presenter.SavedState = foundation with
            {
                AdeptPowerOptions = [new("sr6-power-test-a", "Test Power A", null, null, "use-a", 4, 6, "anchor-pa"),
                    new("sr6-power-test-b", "Test Power B", null, null, "use-b", 4, 6, "anchor-pb")],
                Selection = new(foundation.Binding, selection, new(24, 32, 50000, 6, "E"), 0, 0, [], "saved-preview"),
            };
            using var scope = new WizardScope(presenter);
            var window = scope.Window;
            var ranks = window.GetVisualDescendants().OfType<ComboBox>().ToArray();
            string[] choices = ["A", "B", "C", "D", "E"];
            for (int i = 0; i < ranks.Length; i++) ranks[i].SelectedItem = choices[i];
            Click(window, "NextButton");
            var lists = window.GetVisualDescendants().OfType<ListBox>().ToArray();
            lists[0].SelectedItem = "Human";
            lists[1].SelectedItem = "adept";
            Click(window, "NextButton");
            Click(window, "NextButton");
            // Toggle power B on: A's saved rating 3 must survive the rebuild; B
            // enters at rating 1 as a new pick.
            var powerB = window.GetVisualDescendants().OfType<CheckBox>()
                .First(c => global::Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "sr6-talent-domain-sr6-power-test-b");
            powerB.IsChecked = true;
            const System.Reflection.BindingFlags Priv = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var stateObj = typeof(Sr6CreationWizardWindow).GetField("_state", Priv)!.GetValue(window)!;
            var foundationObj = stateObj.GetType().GetProperty("Foundation")!.GetValue(stateObj)!;
            var selStateObj = foundationObj.GetType().GetProperty("Selection")!.GetValue(foundationObj)!;
            var draft = (Sr6CreationFoundationSelection)selStateObj.GetType().GetProperty("Selection")!.GetValue(selStateObj)!;
            var a = draft.AdeptPowers!.Choices.Single(c => c.CatalogId == "sr6-power-test-a");
            var b = draft.AdeptPowers!.Choices.Single(c => c.CatalogId == "sr6-power-test-b");
            Assert.AreEqual(3, a.Rating, "the unchanged choice keeps its saved rating 3");
            Assert.AreEqual(1, b.Rating, "a newly picked power enters at rating 1");
            // Drop B again: the pick is discarded with its rating.
            powerB.IsChecked = false;
            stateObj = typeof(Sr6CreationWizardWindow).GetField("_state", Priv)!.GetValue(window)!;
            foundationObj = stateObj.GetType().GetProperty("Foundation")!.GetValue(stateObj)!;
            selStateObj = foundationObj.GetType().GetProperty("Selection")!.GetValue(foundationObj)!;
            draft = (Sr6CreationFoundationSelection)selStateObj.GetType().GetProperty("Selection")!.GetValue(selStateObj)!;
            Assert.HasCount(1, draft.AdeptPowers!.Choices, "dropping the pick discards it");
            Assert.AreEqual(3, draft.AdeptPowers!.Choices.Single().Rating, "A still carries rating 3");
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

    [TestMethod]
    public void Transient_workspace_unavailable_loads_recover_without_surface()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(async () =>
        {
            var presenter = new RecordingSr6WizardPresenter { TransientWorkspaceUnavailableLoads = 11 };
            using var scope = new WizardScope(presenter);
            // 11 transient loads inside the 12-attempt window: the 12th load is
            // the first success, so the wizard body renders and no blocker text
            // is surfaced. The load ladder is ASYNC now (Task.Delay, not
            // Thread.Sleep), so the headless dispatcher must pump until the
            // bounded ladder finished: wait until all 11 transient loads were
            // consumed, then one further dispatcher pump for the success
            // render. The ladder re-fences owner/close/cancel on every turn.
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (presenter.TransientWorkspaceUnavailableLoads > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(50);
            Assert.AreEqual(0, presenter.TransientWorkspaceUnavailableLoads, "The bounded async ladder must consume every seeded transient load.");
            await Task.Delay(100);
            Assert.IsFalse(
                (scope.Window.FindControl<TextBlock>("AuthorityStatusText")!.Text ?? "").Contains("workspace-unavailable"),
                "Transient loads must recover without surfacing the Core blocker string.");
            Assert.IsFalse(
                (scope.Window.FindControl<TextBlock>("AuthorityStatusText")!.Text ?? "").Contains("store-read-failed"),
                "Transient loads must not surface as store failures.");
        });

    [TestMethod]
    public void Persistent_workspace_unavailable_loads_surface_the_blocker_after_the_window()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(async () =>
        {
            var presenter = new RecordingSr6WizardPresenter { TransientWorkspaceUnavailableLoads = int.MaxValue };
            using var scope = new WizardScope(presenter);
            // Beyond the bounded window (12 async attempts) the transient
            // blocker must surface (never an empty state, never a crash): the
            // user sees the exact Core blocker string. Pump the dispatcher
            // until the bounded ladder exhausts, with a generous deadline.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if ((scope.Window.FindControl<TextBlock>("AuthorityStatusText")!.Text ?? "").Contains("workspace-unavailable"))
                    break;
                await Task.Delay(100);
            }
            StringAssert.Contains(scope.Window.FindControl<TextBlock>("AuthorityStatusText")!.Text!, "workspace-unavailable");
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
    public int TransientWorkspaceUnavailableLoads;
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
        if (TransientWorkspaceUnavailableLoads > 0)
        {
            TransientWorkspaceUnavailableLoads--;
            // Mirror the Core blocker string exactly: the wizard's transient
            // retry window keys on this ordinal substring.
            return new(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                ["sr6-creation-foundation-workspace-unavailable"]);
        }
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
