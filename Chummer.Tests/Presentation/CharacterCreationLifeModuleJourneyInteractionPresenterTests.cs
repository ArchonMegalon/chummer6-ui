using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Contract gates for the Life-Modules journey interaction presenter
/// (stages 2..5 of the creation wizard life-modules step). The presenter must
/// stay authority-preserving: it never derives legality, never fabricates a
/// confirmable preview, and every mutation binds to the exact journey digest
/// held at preview time.
/// </summary>
[TestClass]
public sealed class CharacterCreationLifeModuleJourneyInteractionPresenterTests
{
    private static readonly OwnerContextStamp Owner = new(new OwnerScope("journey-owner"), "journey-test-authority", 0);
    private const string WsId = "ws-journey-a";

    [TestMethod]
    public void Load_projects_stage_2_options_after_stage_1_is_confirmed()
    {
        CharacterCreationLifeModuleJourneyState state = CoreState(2, revision: 5, selectionFinished: false, canFinish: false);
        FakeJourneyService service = new(state);

        CharacterCreationLifeModuleJourneyInteractionLoadResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Load(Overview());

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        Assert.IsNotNull(result.State);
        Assert.AreEqual(2, result.State.CurrentStageOrder);
        Assert.AreEqual(CharacterCreationLifeModuleStageIds.FormativeYears, result.State.CurrentStageId);
        Assert.AreEqual(2, result.State.Options.Count);
        Assert.IsFalse(result.State.SelectionFinished);
        Assert.IsFalse(result.State.CanFinishSelection);
    }

    [TestMethod]
    public void Load_blocks_when_overview_is_incomplete()
    {
        FakeJourneyService service = new(
            CoreState(2, revision: 5, selectionFinished: false, canFinish: false));

        CharacterCreationLifeModuleJourneyInteractionLoadResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service)
                .Load(Overview() with { CreationFoundation = null });

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, result.Outcome);
        Assert.IsNull(result.State);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationLifeModuleJourneyInteractionBlockers.OverviewAuthorityRequired));
    }

    [TestMethod]
    public void Load_reports_selection_finished_when_core_reports_it()
    {
        FakeJourneyService service = new(
            CoreState(5, revision: 5, selectionFinished: true, canFinish: false));

        CharacterCreationLifeModuleJourneyInteractionLoadResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Load(Overview());

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        Assert.IsNotNull(result.State);
        Assert.IsTrue(result.State.SelectionFinished);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationLifeModuleJourneyInteractionBlockers.SelectionFinished));
    }

    [TestMethod]
    public void Prepare_binds_preview_to_the_held_journey_digest_and_stage()
    {
        CharacterCreationLifeModuleJourneyState state = CoreState(2, revision: 5, selectionFinished: false, canFinish: false);
        FakeJourneyService service = new(state);

        CharacterCreationLifeModuleJourneyInteractionPrepareResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Prepare(
                Overview(),
                new CharacterCreationLifeModuleSelectionInput("module-formative-a", null));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        Assert.IsNotNull(result.PreparedPreview);
        Assert.AreEqual(Digest('b'), result.PreparedPreview.PreviewDigest);
        Assert.AreEqual(2, result.PreparedPreview.StageOrder);
        Assert.AreEqual(CharacterCreationLifeModuleStageIds.FormativeYears, result.PreparedPreview.StageId);
        Assert.IsTrue(result.PreparedPreview.RequiresExplicitConfirmation);
        Assert.AreEqual(
            state.DraftDigest,
            result.PreparedPreview.JourneyDigest,
            "the preview must bind to the exact journey draft digest held at load time");
    }

    [TestMethod]
    public void Prepare_rejects_selection_after_finish()
    {
        FakeJourneyService service = new(
            CoreState(5, revision: 5, selectionFinished: true, canFinish: false));

        CharacterCreationLifeModuleJourneyInteractionPrepareResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Prepare(
                Overview(),
                new CharacterCreationLifeModuleSelectionInput("module-reallife-a", null));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, result.Outcome);
        Assert.IsNull(result.PreparedPreview);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationLifeModuleJourneyInteractionBlockers.SelectionFinished));
    }

    [TestMethod]
    public void Confirm_requires_explicit_confirmation()
    {
        FakeJourneyService service = new(
            CoreState(2, revision: 5, selectionFinished: false, canFinish: false));
        CharacterCreationLifeModulePreparedPreview prepared = PrepareViaPresenter(service);

        CharacterCreationLifeModuleJourneyInteractionConfirmResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Confirm(
                Overview(),
                new CharacterCreationLifeModuleConfirmation(
                    prepared, prepared.PreviewDigest, ExplicitlyConfirmed: false));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, result.Outcome);
        Assert.IsNull(result.Receipt);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationFoundationBlockers.ExplicitConfirmationRequired));
    }

    [TestMethod]
    public void Confirm_rejects_a_stale_preview_digest()
    {
        FakeJourneyService service = new(
            CoreState(2, revision: 5, selectionFinished: false, canFinish: false));
        CharacterCreationLifeModulePreparedPreview prepared = PrepareViaPresenter(service);

        CharacterCreationLifeModuleJourneyInteractionConfirmResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Confirm(
                Overview(),
                new CharacterCreationLifeModuleConfirmation(
                    prepared, Digest('f'), ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, result.Outcome);
        Assert.IsNull(result.Receipt);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationFoundationBlockers.PreviewDigestMismatch));
    }

    [TestMethod]
    public void Confirm_rejects_a_preview_bound_to_a_foreign_journey_digest()
    {
        FakeJourneyService service = new(
            CoreState(2, revision: 5, selectionFinished: false, canFinish: false));
        // The prepared preview binds to digest 'c'; the live journey meanwhile
        // advanced to digest 'b' -- the confirm must refuse the stale binding.
        service.State = service.State with { DraftDigest = Digest('c') };
        CharacterCreationLifeModulePreparedPreview prepared = PrepareViaPresenter(service);
        service.State = service.State with { DraftDigest = Digest('b') };

        CharacterCreationLifeModuleJourneyInteractionConfirmResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Confirm(
                Overview(),
                new CharacterCreationLifeModuleConfirmation(
                    prepared, prepared.PreviewDigest, ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, result.Outcome);
        Assert.IsNull(result.Receipt);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationLifeModuleJourneyInteractionBlockers.BindingMismatch));
    }

    [TestMethod]
    public void Confirm_applies_module_and_refreshes_to_the_next_stage()
    {
        FakeJourneyService service = new(
            CoreState(2, revision: 5, selectionFinished: false, canFinish: false));
        CharacterCreationLifeModulePreparedPreview prepared = PrepareViaPresenter(service);
        service.NextState = CoreState(3, revision: 6, selectionFinished: false, canFinish: false)
            with { AdditionalModules = [prepared.Entry] };

        CharacterCreationLifeModuleJourneyInteractionConfirmResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Confirm(
                Overview(),
                new CharacterCreationLifeModuleConfirmation(
                    prepared, prepared.PreviewDigest, ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        Assert.IsNotNull(result.Receipt);
        Assert.IsNotNull(result.RefreshedState);
        Assert.AreEqual(3, result.RefreshedState.CurrentStageOrder);
        Assert.AreEqual(CharacterCreationLifeModuleStageIds.TeenYears, result.RefreshedState.CurrentStageId);
        Assert.AreEqual(service.NextState.DraftRevision, result.RefreshedState.DraftRevision);
        Assert.AreEqual(service.NextState.DraftDigest, result.RefreshedState.DraftDigest);
        Assert.AreEqual(1, result.RefreshedState.AdditionalModules.Count);
        Assert.IsTrue(service.LoadRequests.Count >= 2);
        Assert.IsTrue(service.LoadRequests.All(request => request.EnabledSources?.SequenceEqual(["SR5"]) == true),
            "Refresh must retain the reviewed source filter.");
    }

    [TestMethod]
    public void Confirm_rejects_a_receipt_for_a_foreign_selection()
    {
        FakeJourneyService service = new(
            CoreState(2, revision: 5, selectionFinished: false, canFinish: false));
        CharacterCreationLifeModulePreparedPreview prepared = PrepareViaPresenter(service);
        service.ReceiptSelectionOverride = "module-foreign";

        CharacterCreationLifeModuleJourneyInteractionConfirmResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Confirm(
                Overview(),
                new CharacterCreationLifeModuleConfirmation(
                    prepared, prepared.PreviewDigest, ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, result.Outcome);
        Assert.IsNull(result.RefreshedState);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationLifeModuleJourneyInteractionBlockers.ReceiptMismatch));
    }

    [TestMethod]
    public void Confirm_rejects_a_refresh_that_loses_the_applied_module()
    {
        FakeJourneyService service = new(
            CoreState(2, revision: 5, selectionFinished: false, canFinish: false));
        CharacterCreationLifeModulePreparedPreview prepared = PrepareViaPresenter(service);
        service.NextState = CoreState(3, revision: 6, selectionFinished: false, canFinish: false)
            with { AdditionalModules = [prepared.Entry] };
        service.CorruptRefreshAfterConfirm = true;

        CharacterCreationLifeModuleJourneyInteractionConfirmResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Confirm(
                Overview(),
                new CharacterCreationLifeModuleConfirmation(
                    prepared, prepared.PreviewDigest, ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, result.Outcome);
        Assert.IsNotNull(result.Receipt);
        Assert.IsNull(result.RefreshedState);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationLifeModuleJourneyInteractionBlockers.RefreshAuthorityRequired));
    }

    [TestMethod]
    public void PrepareFinish_blocks_before_stage_5_completion()
    {
        FakeJourneyService service = new(
            CoreState(2, revision: 5, selectionFinished: false, canFinish: false));

        CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service)
                .PrepareFinish(Overview());

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, result.Outcome);
        Assert.IsNull(result.FinishPreview);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationFoundationBlockers.FinalizationRequiredStagesIncomplete));
    }

    [TestMethod]
    public void PrepareFinish_succeeds_at_stage_5_with_can_finish()
    {
        FakeJourneyService service = new(
            CoreState(5, revision: 5, selectionFinished: false, canFinish: true));

        CharacterCreationLifeModuleJourneyInteractionFinishPrepareResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service)
                .PrepareFinish(Overview());

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        Assert.IsNotNull(result.FinishPreview);
        Assert.IsTrue(result.FinishPreview.CanConfirm);
    }

    [TestMethod]
    public void ConfirmFinish_requires_explicit_confirmation()
    {
        FakeJourneyService service = new(
            CoreState(5, revision: 5, selectionFinished: false, canFinish: true));

        CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).ConfirmFinish(
                Overview(),
                new CharacterCreationLifeModuleFinishConfirmation(
                    service.State.DraftDigest,
                    Digest('e'),
                    ExplicitlyConfirmed: false, OwnerContext: Owner));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, result.Outcome);
        Assert.IsNull(result.Receipt);
        Assert.IsTrue(result.Blockers.Contains(
            CharacterCreationFoundationBlockers.ExplicitConfirmationRequired));
    }

    [TestMethod]
    public void ConfirmFinish_applies_finish_and_reports_selection_finished()
    {
        FakeJourneyService service = new(
            CoreState(5, revision: 5, selectionFinished: false, canFinish: true));
        service.NextState = CoreState(5, revision: 6, selectionFinished: true, canFinish: false);

        CharacterCreationLifeModuleJourneyInteractionFinishConfirmResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).ConfirmFinish(
                Overview(),
                new CharacterCreationLifeModuleFinishConfirmation(
                    service.State.DraftDigest,
                    Digest('e'),
                    ExplicitlyConfirmed: true, OwnerContext: Owner));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        Assert.IsNotNull(result.Receipt);
        Assert.IsNotNull(result.RefreshedState);
        Assert.IsTrue(result.RefreshedState.SelectionFinished);
        Assert.AreEqual(0, result.RefreshedState.Options.Count);
        Assert.IsTrue(service.LoadRequests.Count >= 2);
        Assert.IsTrue(service.LoadRequests.All(request => request.EnabledSources?.SequenceEqual(["SR5"]) == true),
            "Refresh must retain the reviewed source filter.");
    }

    // ---- fixtures ----

    private static string Digest(char value) => "sha256:" + new string(value, 64);

    private static CharacterOverviewState Overview()
    {
        CharacterWorkspaceId ws = new(WsId);
        CharacterCreationFoundationBinding binding = MakeBinding(ws, 5);
        WorkspaceSessionState session = new(
            ws,
            [new OpenWorkspaceState(
                ws,
                "Wizard",
                "W",
                DateTimeOffset.Parse("2026-09-01T00:00:00+00:00"),
                RulesetDefaults.Sr5,
                binding.ContentRevision,
                binding.SavedRevision)],
            [ws]);
        WorkspaceOverviewLoadResult loaded = new(
            new CharacterProfileSection(
                "Wizard", "W", string.Empty, "Human", string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, "1.0", "1.0",
                CharacterCreationBuildMethods.Priority, "Standard", false,
                false, false, false, false, 0, 0),
            new CharacterProgressSection(
                0m, 0m, 0m, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m,
                0, 0, false, false, false),
            new CharacterSkillsSection(0, 0, []),
            new CharacterRulesSection("SR5", "default", "Standard", 25, 0, 0, 3, []),
            new CharacterBuildSection(
                CharacterCreationBuildMethods.Priority,
                "A", "B", "C", "D", "E", "Mundane", 10,
                0, 0, 0, 0, 0),
            new CharacterMovementSection("0", "0", "0", "0", "0", "0", 0, 0),
            new CharacterAwakeningSection(
                false, false, false, false, false, false, false, 0, 0,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                0, 0, 0, 0, 0),
            binding.ContentRevision,
            binding.SavedRevision,
            new WorkspaceDocument(new WorkspaceDocumentState(
                RulesetDefaults.Sr5, 1, "native-xml",
                "<character><id>fixture</id></character>")));
        CharacterOverviewState overview = new WorkspaceOverviewStateFactory().CreateLoadedState(
            CharacterOverviewState.Empty,
            ws,
            session,
            loaded,
            restoredView: null,
            hasSavedWorkspace: true);
        return overview with
        {
            DisplayOwnerContext = Owner,
            CreationWizard = new CharacterCreationWizardSnapshot(
                Schema: "creation-wizard-snapshot-v1",
                WorkspaceId: ws.Value,
                WorkspaceRevision: 3,
                ContentDigest: Digest('8'),
                SourceDigest: Digest('4'),
                RulesetId: "sr5",
                RuntimeFingerprint: "test",
                BuildMethod: CharacterCreationBuildMethods.Priority,
                CharacterCreated: false,
                ActiveStepId: CharacterCreationWizardStepIds.LifeModules,
                Steps: [],
                Budgets: [],
                LegalOptionsByStep:
                    new Dictionary<string, IReadOnlyList<CharacterCreationLegalOption>>(),
                CompletionBlockers: [],
                Warnings: [],
                CanFinalize: false,
                SnapshotDigest: Digest('8')),
            CreationFoundation = new CharacterCreationFoundationState(
                Schema: "creation-foundation-state-v1",
                Binding: MakeBinding(ws, 5),
                RulesetId: "sr5",
                CurrentMetatype: "Human",
                BuildMethod: CharacterCreationBuildMethods.Priority,
                CharacterCreated: false,
                MetatypeOptions: [],
                NationalityOptions: [],
                LifeModuleBudget: new CharacterCreationBudgetState("life-modules", "Life modules", 0m, 0m, 0m, true, [], "karma"),
                PendingDraft: null,
                ResumeStatus: "in-progress",
                AuthorityBlockers: [],
                SnapshotDigest: Digest('7')),
        };
    }

    private static CharacterCreationFoundationBinding MakeBinding(CharacterWorkspaceId ws, long revision)
        => new(
            ws,
            revision,
            revision,
            Digest('9'),
            "xml-v1",
            Digest('4'),
            "sources-v1",
            SourceFilterApplied: true,
            EnabledSources: ["SR5"]);

    private static IReadOnlyList<LifeModuleLegalOptionDto> StageOptions(string prefix)
        => [.. Enumerable.Range(1, 2).Select(index => new LifeModuleLegalOptionDto(
            ModuleId: $"{prefix}-opt{index}",
            StageOrder: 0,
            Name: $"{prefix} option {index}",
            KarmaCost: index == 1 ? 0m : 2m,
            Source: "SR5",
            Page: null,
            StoryTemplate: "fixture story",
            IsEnabled: true,
            Requirements: [],
            Versions: [],
            Effects: [],
            FollowUps: [],
            SourceAnchorIds: [],
            StageId: prefix,
            CanRepeat: false,
            KarmaRaw: index == 1 ? "0" : "2",
            KarmaIsExact: true,
            PageReference: string.Empty,
            AuthorityBlockers: []))];

    private static CharacterCreationLifeModuleJourneyState CoreState(
        int currentStage,
        int revision,
        bool selectionFinished,
        bool canFinish)
    {
        CharacterWorkspaceId ws = new(WsId);
        return new CharacterCreationLifeModuleJourneyState(
            Binding: MakeBinding(ws, revision),
            DraftRevision: revision,
            DraftDigest: Digest((char)('a' + currentStage)),
            CurrentStageOrder: currentStage,
            Options: selectionFinished ? [] : StageOptions(StageIdOf(currentStage)),
            Budget: new CharacterCreationBudgetState("life-modules", "Life modules", 0m, 0m, 0m, true, [], "karma"),
            AdditionalModules: [])
        {
            SelectionFinished = selectionFinished,
            CanFinishSelection = canFinish,
        };
    }

    private static string StageIdOf(int currentStage) => currentStage switch
    {
        1 => CharacterCreationLifeModuleStageIds.Nationality,
        2 => CharacterCreationLifeModuleStageIds.FormativeYears,
        3 => CharacterCreationLifeModuleStageIds.TeenYears,
        4 => CharacterCreationLifeModuleStageIds.FurtherEducation,
        _ => CharacterCreationLifeModuleStageIds.RealLife,
    };

    [TestMethod]
    public void Prepared_module_cannot_cross_an_owner_or_ABA_transition()
    {
        foreach (var transitioned in new[] { Owner with { Owner = new OwnerScope("another-owner") }, Owner with { TransitionRevision = 2 } })
        {
            var service = new FakeJourneyService(CoreState(2, 5, false, false));
            var prepared = PrepareViaPresenter(service);
            Assert.AreEqual(Owner, prepared.OwnerContext);
            service.OwnerContext = transitioned;
            var result = new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Confirm(
                Overview() with { DisplayOwnerContext = transitioned },
                new(prepared, prepared.PreviewDigest, ExplicitlyConfirmed: true));
            Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, result.Outcome);
            Assert.AreEqual(0, service.Confirms);
        }
    }

    [TestMethod]
    public void Finish_cannot_cross_the_owner_transition_after_review()
    {
        var service = new FakeJourneyService(CoreState(5, 5, false, true));
        var presenter = new CharacterCreationLifeModuleJourneyInteractionPresenter(service);
        var prepared = presenter.PrepareFinish(Overview());
        Assert.AreEqual(Owner, prepared.OwnerContext);
        var transitioned = Owner with { TransitionRevision = 2 };
        service.OwnerContext = transitioned;
        var result = presenter.ConfirmFinish(Overview() with { DisplayOwnerContext = transitioned },
            new(service.State.DraftDigest, prepared.FinishPreview!.PreviewDigest, true, prepared.OwnerContext));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, result.Outcome);
        Assert.IsFalse(service.State.SelectionFinished);
    }

    private static CharacterCreationLifeModulePreparedPreview PrepareViaPresenter(
        FakeJourneyService service)
    {
        CharacterCreationLifeModuleJourneyInteractionPrepareResult result =
            new CharacterCreationLifeModuleJourneyInteractionPresenter(service).Prepare(
                Overview(),
                new CharacterCreationLifeModuleSelectionInput("module-formative-a", null));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        return result.PreparedPreview!;
    }

    private sealed class FakeJourneyService(CharacterCreationLifeModuleJourneyState initialState)
        : IOwnerBoundCharacterCreationLifeModuleJourneyService
    {
        public CharacterCreationLifeModuleJourneyState State { get; set; } = initialState;

        public CharacterCreationLifeModuleJourneyState? NextState { get; set; }

        public string? ReceiptSelectionOverride { get; set; }

        public bool CorruptRefreshAfterConfirm { get; set; }

        public int Confirms { get; private set; }
        public OwnerContextStamp OwnerContext { get; set; } = Owner;
        public List<CharacterCreationFoundationLoadRequest> LoadRequests { get; } = [];

        public CharacterCreationLifeModuleDraftEntry? LastPreviewEntry { get; private set; }

        public CharacterCreationFoundationResult<CharacterCreationLifeModuleJourneyState> LoadJourney(
            OwnerContextStamp expectedOwner, CharacterCreationFoundationLoadRequest request)
        {
            if (expectedOwner != OwnerContext) return new(CharacterCreationFoundationOutcomes.Blocked, null, ["owner-context-changed"]);
            LoadRequests.Add(request);
            CharacterCreationLifeModuleJourneyState state = State;
            if (CorruptRefreshAfterConfirm && Confirms > 0)
            {
                // Simulates an authority that reports a refresh which lost the
                // just-applied module: the presenter must refuse to surface it.
                state = state with { AdditionalModules = [] };
            }

            return new(CharacterCreationFoundationOutcomes.Success, state, []);
        }

        public CharacterCreationFoundationResult<CharacterCreationLifeModulePreview> PreviewModule(
            OwnerContextStamp expectedOwner, CharacterCreationLifeModulePreviewRequest request)
        {
            if (expectedOwner != OwnerContext) return new(CharacterCreationFoundationOutcomes.Blocked, null, ["owner-context-changed"]);
            return new(
                CharacterCreationFoundationOutcomes.Success,
                new CharacterCreationLifeModulePreview(
                    Request: request,
                    Entry: LastPreviewEntry = new CharacterCreationLifeModuleDraftEntry(
                        StageOrder: State.CurrentStageOrder,
                        StageId: StageIdOf(State.CurrentStageOrder),
                        Selection: request.Selection,
                        KarmaCost: 2m,
                        RequirementEvaluations: [],
                        ProjectedEffects: [],
                        FollowUpValues: new Dictionary<string, string>(),
                        SourceAnchorIds: [],
                        StoryTemplate: "fixture story"),
                    BudgetBefore: State.Budget,
                    BudgetAfter: State.Budget,
                    Blockers: [],
                    CanConfirm: true,
                    PreviewDigest: Digest('b')),
                []);
        }

        public CharacterCreationFoundationResult<CharacterCreationLifeModuleApplyReceipt> ConfirmModule(
            OwnerContextStamp expectedOwner, CharacterCreationLifeModuleConfirmRequest request)
        {
            if (expectedOwner != OwnerContext) return new(CharacterCreationFoundationOutcomes.Blocked, null, ["owner-context-changed"]);
            long next = request.Request.Binding.ContentRevision + 1;
            CharacterWorkspaceId ws = request.Request.Binding.WorkspaceId;
            string moduleId = ReceiptSelectionOverride ?? request.Request.Selection.ModuleId;
            var confirmedEntry = new CharacterCreationLifeModuleDraftEntry(
                StageOrder: LastPreviewEntry!.StageOrder,
                StageId: LastPreviewEntry.StageId,
                Selection: new CharacterCreationFoundationSelection(moduleId, null),
                KarmaCost: LastPreviewEntry.KarmaCost,
                RequirementEvaluations: [],
                ProjectedEffects: [],
                FollowUpValues: new Dictionary<string, string>(),
                SourceAnchorIds: [],
                StoryTemplate: LastPreviewEntry.StoryTemplate);
            Confirms++;
            if (NextState is not null)
            {
                State = NextState;
            }
            else
            {
                int nextStage = State.CurrentStageOrder >= 5
                    ? State.CurrentStageOrder
                    : State.CurrentStageOrder + 1;
                State = State with
                {
                    Binding = MakeBinding(ws, next),
                    DraftRevision = next,
                    DraftDigest = Digest((char)('a' + nextStage)),
                    CurrentStageOrder = nextStage,
                    Options = nextStage >= 5 ? [] : StageOptions(StageIdOf(nextStage)),
                    AdditionalModules = [.. State.AdditionalModules, confirmedEntry],
                };
            }
            return new(
                CharacterCreationFoundationOutcomes.Success,
                new CharacterCreationLifeModuleApplyReceipt(
                    Binding: MakeBinding(ws, next),
                    PreviousContentRevision: request.Request.Binding.ContentRevision,
                    ContentRevision: next,
                    SavedRevision: next,
                    DraftRevision: next,
                    DraftDigest: Digest('d'),
                    Entry: LastPreviewEntry = new CharacterCreationLifeModuleDraftEntry(
                        StageOrder: LastPreviewEntry!.StageOrder,
                        StageId: LastPreviewEntry.StageId,
                        Selection: new CharacterCreationFoundationSelection(moduleId, null),
                        KarmaCost: LastPreviewEntry.KarmaCost,
                        RequirementEvaluations: [],
                        ProjectedEffects: [],
                        FollowUpValues: new Dictionary<string, string>(),
                        SourceAnchorIds: [],
                        StoryTemplate: LastPreviewEntry.StoryTemplate),
                    CharacterEffectsApplied: false),
                []);
        }

        public CharacterCreationFoundationResult<CharacterCreationLifeModuleFinishPreview> PreviewFinishSelection(
            OwnerContextStamp expectedOwner, CharacterCreationLifeModuleFinishRequest request)
        {
            if (expectedOwner != OwnerContext) return new(CharacterCreationFoundationOutcomes.Blocked, null, ["owner-context-changed"]);
            return new(
                CharacterCreationFoundationOutcomes.Success,
                new CharacterCreationLifeModuleFinishPreview(
                    Request: request,
                    Budget: State.Budget,
                    SourceAnchorIds: ["anchor-1"],
                    Blockers: [],
                    CanConfirm: true,
                    PreviewDigest: Digest('e')),
                []);
        }

        public CharacterCreationFoundationResult<CharacterCreationLifeModuleFinishReceipt> ConfirmFinishSelection(
            OwnerContextStamp expectedOwner, CharacterCreationLifeModuleFinishConfirmRequest request)
        {
            if (expectedOwner != OwnerContext) return new(CharacterCreationFoundationOutcomes.Blocked, null, ["owner-context-changed"]);
            CharacterCreationLifeModuleJourneyState target = NextState ?? State;
            long previousContentRevision = State.Binding.ContentRevision;
            State = target with
            {
                SelectionFinished = true,
                Options = [],
            };
            return new(
                CharacterCreationFoundationOutcomes.Success,
                new CharacterCreationLifeModuleFinishReceipt(
                    Binding: target.Binding,
                    PreviousContentRevision: previousContentRevision,
                    ContentRevision: target.Binding.ContentRevision,
                    SavedRevision: target.Binding.SavedRevision,
                    DraftRevision: target.DraftRevision,
                    DraftDigest: target.DraftDigest,
                    CharacterEffectsApplied: false),
                []);
        }

        // The fake mirrors the Core rule: options are only served for the
        // current stage, so a preview request is always for that stage.
    }
}
