using System;
using System.Linq;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Avalonia.Creation;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.CreationWizard.Presentation.Tests;
using Chummer.Presentation.Overview;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Focused actual-window regression for the SR5 Magic/Resonance editor
/// (CreationMagicResonanceWindow), Controller10541 PRIORITY-1 item 1: saved
/// selections must render correctly on reopen, one uncheck must remove only that
/// choice from the next preview, and cancel must preserve the saved data so a
/// reopen reflects the actual values. The production defect this pins: checkbox
/// seeding that ran after the rows were built and a re-prepare that re-entered
/// the change handlers. The saved state is seeded through the fixture's real
/// Core-valid confirm path (CreatePreview -> CreateConfirmed), which is exactly
/// what the service stub's Confirm happy path performs.
/// </summary>
[TestClass]
public sealed class CreationMagicResonanceWindowTests
{
    [TestMethod]
    public void Saved_spell_selections_render_checked_on_reopen_and_uncheck_updates_next_preview()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            TestContextFx context = CreateTest();
            CharacterCreationMagicResonanceInteractionLoadResult loaded =
                context.Presenter.Load(context.Overview);
            Assert.AreEqual("ok", loaded.Outcome, loaded.Error);

            var window = new CreationMagicResonanceWindow(
                context.Presenter, loaded.State!, () => context.Overview);
            try
            {
                window.Show();

                CheckBox spellOne = FindCheck(window, SpellOneAutomationId);
                CheckBox spellTwo = FindCheck(window, SpellTwoAutomationId);
                Assert.IsTrue(spellOne.IsChecked == true, "saved spell one must render checked on reopen");
                Assert.IsTrue(spellTwo.IsChecked == true, "saved spell two must render checked on reopen");
                AssertSelectedTradition(window, "Hermetic");

                // The reopen preview must carry the actual saved set, not a stale
                // or empty projection.
                Assert.IsNotNull(context.Bound.LastPreviewSelections);
                CollectionAssert.AreEquivalent(
                    new[] { SpellOneSourceId, SpellTwoSourceId },
                    context.Bound.LastPreviewSelections!.Spells
                        .Select(spell => spell.SourceId).ToArray(),
                    "the reopen preview must carry the saved spell set");

                // Unchecking one spell removes exactly that choice from the next
                // preview; the other row's checkbox state must be untouched.
                spellOne.IsChecked = false;
                Assert.IsTrue(spellTwo.IsChecked == true,
                    "spell two checkbox state is untouched by spell one's uncheck");

                Assert.IsNotNull(context.Bound.LastPreviewSelections);
                Assert.AreEqual(
                    1,
                    context.Bound.LastPreviewSelections!.Spells.Count,
                    "the uncheck must remove exactly that one choice from the next preview");
                Assert.AreEqual(
                    SpellTwoSourceId,
                    context.Bound.LastPreviewSelections.Spells.Single().SourceId);

                // The budget-incomplete set is blocked, so the fail-closed UI keeps
                // confirmation off until the selection is valid again.
                Button confirm = Find<Button>(window, CreationMagicResonanceWindow.ConfirmButtonAutomationId);
                Assert.IsFalse(confirm.IsEnabled, "an incomplete selection must not be confirmable");
            }
            finally
            {
                CloseQuietly(window);
            }
        });

    [TestMethod]
    public void Cancel_preserves_saved_selections_and_reopen_reflects_actual_values()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            TestContextFx context = CreateTest();
            CharacterCreationMagicResonanceInteractionLoadResult loaded =
                context.Presenter.Load(context.Overview);
            Assert.AreEqual("ok", loaded.Outcome, loaded.Error);

            var window = new CreationMagicResonanceWindow(
                context.Presenter, loaded.State!, () => context.Overview);
            try
            {
                window.Show();
                FindCheck(window, SpellOneAutomationId).IsChecked = false;
                Assert.IsFalse(Find<Button>(window, CreationMagicResonanceWindow.ConfirmButtonAutomationId).IsEnabled);

                Button cancel = Find<Button>(window, CreationMagicResonanceWindow.CancelButtonAutomationId);
                cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.IsFalse(window.IsVisible, "cancel must close the editor");
                Assert.IsFalse(window.Confirmed, "cancel must not confirm anything");

                // Cancel leaves the saved Core state untouched: the pending draft
                // still carries both saved spells.
                CharacterCreationMagicResonanceState saved = context.Shared.StateForTests;
                Assert.IsNotNull(saved.PendingDraft, "the seeded confirm must have persisted a pending draft");
                CollectionAssert.AreEquivalent(
                    new[] { SpellOneSourceId, SpellTwoSourceId },
                    saved.PendingDraft!.Selections.Spells
                        .Select(spell => spell.SourceId).ToArray(),
                    "cancel must preserve the saved selections");
            }
            finally
            {
                CloseQuietly(window);
            }

            // Reopen through the same presenter route: the saved data renders
            // again, reflecting the actual saved values.
            CharacterCreationMagicResonanceInteractionLoadResult reopened =
                context.Presenter.Load(context.Overview);
            Assert.AreEqual("ok", reopened.Outcome, reopened.Error);

            var reopenedWindow = new CreationMagicResonanceWindow(
                context.Presenter, reopened.State!, () => context.Overview);
            try
            {
                reopenedWindow.Show();
                Assert.IsTrue(
                    FindCheck(reopenedWindow, SpellOneAutomationId).IsChecked == true,
                    "reopen after cancel must render the saved spell one");
                Assert.IsTrue(
                    FindCheck(reopenedWindow, SpellTwoAutomationId).IsChecked == true,
                    "reopen after cancel must render the saved spell two");
                Assert.IsNotNull(context.Bound.LastPreviewSelections);
                Assert.AreEqual(
                    2,
                    context.Bound.LastPreviewSelections!.Spells.Count,
                    "the reopen preview must carry the actual saved set");
            }
            finally
            {
                CloseQuietly(reopenedWindow);
            }
        });

    [TestMethod]
    public void Technomancer_selects_stream_and_forms_confirms_and_can_discard_edits_on_reopen()
        => CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var state = CharacterCreationMagicResonanceTestFixture.CreateState(
                Digest(FixtureContent), talentKind: CharacterCreationMagicResonanceKinds.Technomancer);
            var context = CreateContext(state);
            string cleaner = CreationMagicResonanceWindow.ComplexFormCheckAutomationIdPrefix
                + CharacterCreationMagicResonanceTestFixture.CleanerId.SourceId;
            string editor = CreationMagicResonanceWindow.ComplexFormCheckAutomationIdPrefix
                + CharacterCreationMagicResonanceTestFixture.EditorId.SourceId;

            CreationMagicResonanceWindow Open()
            {
                var overview = CreateOverview(context.Bound, context.Shared);
                var presenter = new CharacterCreationMagicResonanceInteractionPresenter(context.Bound);
                var loaded = presenter.Load(overview);
                Assert.AreEqual("ok", loaded.Outcome, loaded.Error);
                var window = new CreationMagicResonanceWindow(
                    presenter, loaded.State!, () => overview);
                window.Show();
                return window;
            }

            var initial = Open();
            try
            {
                var confirm = Find<Button>(initial, CreationMagicResonanceWindow.ConfirmButtonAutomationId);
                Assert.IsFalse(confirm.IsEnabled);
                var stream = Find<ComboBox>(initial, CreationMagicResonanceWindow.StreamComboAutomationId);
                Assert.AreEqual(2, stream.ItemCount);
                stream.SelectedIndex = 1;
                FindCheck(initial, cleaner).IsChecked = true;
                Assert.IsFalse(confirm.IsEnabled);
                FindCheck(initial, editor).IsChecked = true;
                Assert.IsTrue(confirm.IsEnabled);
                confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.IsTrue(initial.Confirmed);
            }
            finally { CloseQuietly(initial); }

            var saved = context.Shared.StateForTests;
            Assert.AreEqual(CharacterCreationMagicResonanceTestFixture.StreamId, saved.PendingDraft!.Selections.Stream);
            CollectionAssert.AreEquivalent(
                CharacterCreationMagicResonanceTestFixture.TechnomancerSelections.ComplexForms.ToArray(),
                saved.PendingDraft.Selections.ComplexForms.ToArray());

            var reopened = Open();
            try
            {
                Assert.AreEqual(1, Find<ComboBox>(reopened, CreationMagicResonanceWindow.StreamComboAutomationId).SelectedIndex);
                Assert.IsTrue(FindCheck(reopened, cleaner).IsChecked == true && FindCheck(reopened, editor).IsChecked == true);
                FindCheck(reopened, cleaner).IsChecked = false;
                Assert.AreEqual(CharacterCreationMagicResonanceTestFixture.EditorId,
                    context.Bound.LastPreviewSelections!.ComplexForms.Single());
                Assert.IsFalse(Find<Button>(reopened, CreationMagicResonanceWindow.ConfirmButtonAutomationId).IsEnabled);
                Find<Button>(reopened, CreationMagicResonanceWindow.CancelButtonAutomationId)
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.IsFalse(reopened.IsVisible);
                Assert.IsFalse(reopened.Confirmed);
            }
            finally { CloseQuietly(reopened); }

            Assert.AreEqual(saved.PendingDraft.DraftDigest, context.Shared.StateForTests.PendingDraft!.DraftDigest);
            var afterCancel = Open();
            try
            {
                Assert.IsTrue(FindCheck(afterCancel, cleaner).IsChecked == true && FindCheck(afterCancel, editor).IsChecked == true);
            }
            finally { CloseQuietly(afterCancel); }
        });

    private static string SpellOneAutomationId =>
        CreationMagicResonanceWindow.SpellCheckAutomationIdPrefix
        + CharacterCreationMagicResonanceTestFixture.SpellOneId.SourceId;

    private static string SpellTwoAutomationId =>
        CreationMagicResonanceWindow.SpellCheckAutomationIdPrefix
        + CharacterCreationMagicResonanceTestFixture.SpellTwoId.SourceId;

    private static string SpellOneSourceId =>
        CharacterCreationMagicResonanceTestFixture.SpellOneId.SourceId;

    private static string SpellTwoSourceId =>
        CharacterCreationMagicResonanceTestFixture.SpellTwoId.SourceId;

    /// <summary>
    /// Seeds the stub with a Core-valid confirmed state (both spells plus the
    /// required tradition) through the fixture's real CreatePreview ->
    /// CreateConfirmed route — the same operations the service stub's Confirm
    /// happy path performs — so the window's reopen seed is an honest saved state.
    /// </summary>
    private static TestContextFx CreateTest()
    {
        CharacterCreationMagicResonanceState baseState =
            CharacterCreationMagicResonanceTestFixture.CreateState(
                Digest(FixtureContent));
        CharacterCreationMagicResonancePreview preview =
            CharacterCreationMagicResonanceTestFixture.CreatePreview(
                baseState,
                CharacterCreationMagicResonanceTestFixture.CompleteSelections);
        (CharacterCreationMagicResonanceState confirmed, _) =
            CharacterCreationMagicResonanceTestFixture.CreateConfirmed(
                baseState,
                preview,
                idempotencyKey: "creation-magic-window-seed");

        return CreateContext(confirmed);
    }

    private static TestContextFx CreateContext(CharacterCreationMagicResonanceState state)
    {
        var shared = new StubMagicResonanceService(state);
        var ownerBound = new OwnerBoundStubForWindow(shared);
        var presenter = new CharacterCreationMagicResonanceInteractionPresenter(ownerBound);
        CharacterOverviewState overview = CreateOverview(ownerBound, shared);
        return new TestContextFx(shared, ownerBound, presenter, overview);
    }

    private const string FixtureContent =
        "<character><created>False</created><gameedition>SR5</gameedition></character>";

    private static string Digest(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void AssertSelectedTradition(Window window, string expectedName)
    {
        ComboBox tradition = Find<ComboBox>(window, CreationMagicResonanceWindow.TraditionComboAutomationId);
        Assert.IsTrue(
            tradition.SelectedIndex > 0,
            "the saved tradition must be selected, not the None placeholder");
        Assert.IsTrue(
            string.IsNullOrEmpty(expectedName)
                || (tradition.SelectedItem?.ToString() ?? string.Empty).StartsWith(expectedName, StringComparison.Ordinal),
            "the saved tradition label must reflect the saved choice");
    }

    private static T Find<T>(Window window, string automationId) where T : Control
        => window.GetVisualDescendants()
            .Prepend(window)
            .OfType<T>()
            .Single(control => string.Equals(
                control.GetValue(AutomationProperties.AutomationIdProperty),
                automationId,
                StringComparison.Ordinal));

    private static CheckBox FindCheck(Window window, string automationId)
        => Find<CheckBox>(window, automationId);

    private static void CloseQuietly(Window window)
    {
        if (!window.IsVisible)
            return;
        try
        {
            window.Close();
        }
        catch (InvalidOperationException)
        {
            // Headless teardown can race a close that already happened.
        }
    }

    // Mirrors the proven presenter-test overview construction
    // (CharacterCreationMagicResonanceInteractionPresenterTests.CreateOverview)
    // with a distinct issuer stamp for the window lane.
    private static CharacterOverviewState CreateOverview(
        OwnerBoundStubForWindow service, StubMagicResonanceService shared)
    {
        CharacterCreationMagicResonanceBinding binding = shared.StateForTests.Binding;
        long contentRevision = binding.ContentRevision;
        CharacterWorkspaceId workspaceId = binding.WorkspaceId;
        WorkspaceSessionState session = new(
            workspaceId,
            [new OpenWorkspaceState(
                workspaceId,
                "Wizard",
                "W",
                DateTimeOffset.Parse("2026-08-23T00:00:00+00:00"),
                RulesetDefaults.Sr5,
                contentRevision,
                binding.SavedRevision)],
            [workspaceId]);
        return new WorkspaceOverviewStateFactory(
            creationMagicResonanceService: shared,
            ownerBoundCreationMagicResonanceService: service).CreateLoadedState(
            CharacterOverviewState.Empty,
            workspaceId,
            session,
            CreateLoadedOverview(
                contentRevision,
                binding.SavedRevision),
            restoredView: null,
            hasSavedWorkspace: true)
            with
        {
            DisplayOwnerContext = new OwnerContextStamp(
                OwnerScope.LocalSingleUser, "magic-window-test-issuer", 0),
        };
    }

    private static WorkspaceOverviewLoadResult CreateLoadedOverview(
        long contentRevision,
        long savedRevision)
        => new(
            new CharacterProfileSection(
                "Wizard", "W", string.Empty, "Human", string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, "1.0", "1.0",
                CharacterCreationBuildMethods.Priority, "Standard", Created: false,
                Adept: false, Magician: false, Technomancer: false, AI: false,
                MainMugshotIndex: 0, MugshotCount: 0),
            new CharacterProgressSection(
                12m, 5000m, 0m, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6m,
                0, 0, false, false, false),
            new CharacterSkillsSection(0, 0, []),
            new CharacterRulesSection("SR5", "default.xml", "Standard", 25, 0, 0, 3, []),
            new CharacterBuildSection(
                CharacterCreationBuildMethods.Priority,
                "A", "B", "C", "D", "E", "Mundane", 10,
                0, 0, 0, 12, 3),
            new CharacterMovementSection("0", "0", "0", "0", "0", "0", 0, 0),
            new CharacterAwakeningSection(
                false, false, false, false, false, false, false, 0, 0,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                0, 0, 0, 0, 0),
            contentRevision,
            savedRevision,
            new WorkspaceDocument(FixtureContent, RulesetDefaults.Sr5));

    private sealed record TestContextFx(
        StubMagicResonanceService Shared,
        OwnerBoundStubForWindow Bound,
        CharacterCreationMagicResonanceInteractionPresenter Presenter,
        CharacterOverviewState Overview);

    /// <summary>
    /// Owner-bound adapter over the shared stub (proven presenter-test pattern)
    /// that records each preview request so the test can assert the exact
    /// selection set the window sent to Core.
    /// </summary>
    private sealed class OwnerBoundStubForWindow : IOwnerBoundCharacterCreationMagicResonanceService
    {
        private readonly StubMagicResonanceService _shared;

        public OwnerBoundStubForWindow(StubMagicResonanceService shared) => _shared = shared;

        public CharacterCreationMagicResonanceState State
        {
            get => _shared.StateForTests;
            set => _shared.StateForTests = value;
        }

        public CharacterCreationFoundationResult<CharacterCreationMagicResonanceState> Load(
            OwnerContextStamp expectedOwner,
            CharacterCreationMagicResonanceLoadRequest request)
            => expectedOwner.IsValid
                ? _shared.Load(request)
                : new(CharacterCreationFoundationOutcomes.Invalid, null,
                    [CharacterCreationMagicResonanceBlockers.WorkspaceUnavailable]);

        public CharacterCreationFoundationResult<CharacterCreationMagicResonancePreview> Preview(
            OwnerContextStamp expectedOwner,
            CharacterCreationMagicResonancePreviewRequest request)
        {
            if (!expectedOwner.IsValid)
                return new(CharacterCreationFoundationOutcomes.Invalid, null,
                    [CharacterCreationMagicResonanceBlockers.WorkspaceUnavailable]);
            LastPreviewSelections = request.Selections;
            return _shared.Preview(request);
        }

        public CharacterCreationFoundationResult<CharacterCreationMagicResonanceReceipt> Confirm(
            OwnerContextStamp expectedOwner,
            CharacterCreationMagicResonanceConfirmRequest request)
            => expectedOwner.IsValid
                ? _shared.Confirm(request)
                : new(CharacterCreationFoundationOutcomes.Invalid, null,
                    [CharacterCreationMagicResonanceBlockers.WorkspaceUnavailable]);

        public CharacterCreationMagicResonanceSelections? LastPreviewSelections { get; private set; }
    }
}
