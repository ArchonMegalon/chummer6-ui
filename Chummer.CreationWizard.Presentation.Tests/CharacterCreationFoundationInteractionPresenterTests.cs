using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationFoundationInteractionPresenterTests
{
    [TestMethod]
    public void Prepare_exposes_only_core_options_and_returns_bound_diff_and_budgets()
    {
        CharacterCreationFoundationState foundation = CreateFoundation();
        CharacterCreationFoundationPreview preview = CreatePreview(foundation);
        var service = new FakeFoundationService(foundation) { PreviewResult = Success(preview) };
        var presenter = new CharacterCreationFoundationInteractionPresenter(service);

        CharacterCreationFoundationInteractionPrepareResult result = presenter.Prepare(
            CreateOverview(foundation),
            new CharacterCreationFoundationSelectionInput(
                "Human",
                "nationality-module",
                "nationality-version",
                new Dictionary<string, string> { ["native-language"] = "Sperethiel" }));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        Assert.IsNotNull(result.State);
        Assert.IsNotNull(result.PreparedPreview);
        CollectionAssert.AreEqual(
            new[] { "Human", "Elf" },
            result.State.MetatypeOptions.Select(static option => option.Label).ToArray());
        LifeModuleLegalOptionDto nationality = AssertExactlyOne(result.State.NationalityOptions);
        Assert.AreEqual("nationality-module", nationality.ModuleId);
        Assert.AreEqual("nationality-version", AssertExactlyOne(nationality.Versions).VersionId);
        Assert.AreEqual("native-language", AssertExactlyOne(nationality.Versions[0].FollowUps).PromptId);
        Assert.AreEqual(750m, result.PreparedPreview.LifeModuleBudgetBefore.Remaining);
        Assert.AreEqual(735m, result.PreparedPreview.LifeModuleBudgetAfter.Remaining);
        Assert.AreEqual("draft-ledger", AssertExactlyOne(result.PreparedPreview.Diff).Phase);
        Assert.AreEqual(preview.PreviewDigest, result.PreparedPreview.PreviewDigest);
        Assert.AreEqual(foundation.SnapshotDigest, result.PreparedPreview.FoundationSnapshotDigest);
        Assert.AreEqual(1, service.LoadCalls);
        Assert.AreEqual(1, service.PreviewCalls);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Confirm_requires_explicit_confirmation_before_any_core_call()
    {
        CharacterCreationFoundationState foundation = CreateFoundation();
        CharacterCreationFoundationPreview preview = CreatePreview(foundation);
        var service = new FakeFoundationService(foundation) { PreviewResult = Success(preview) };
        var presenter = new CharacterCreationFoundationInteractionPresenter(service);
        CharacterCreationFoundationPreparedPreview prepared = RequirePrepared(presenter.Prepare(
            CreateOverview(foundation),
            Selection()));
        int loadCallsBeforeConfirm = service.LoadCalls;

        CharacterCreationFoundationInteractionConfirmResult result = presenter.Confirm(
            CreateOverview(foundation),
            new CharacterCreationFoundationConfirmation(
                prepared,
                prepared.PreviewDigest,
                ExplicitlyConfirmed: false));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Invalid, result.Outcome);
        CollectionAssert.Contains(
            result.Blockers.ToArray(),
            CharacterCreationFoundationBlockers.ExplicitConfirmationRequired);
        Assert.AreEqual(loadCallsBeforeConfirm, service.LoadCalls);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Revision_and_preview_digest_mismatches_fail_closed()
    {
        CharacterCreationFoundationState foundation = CreateFoundation();
        CharacterCreationFoundationPreview preview = CreatePreview(foundation);
        var service = new FakeFoundationService(foundation) { PreviewResult = Success(preview) };
        var presenter = new CharacterCreationFoundationInteractionPresenter(service);
        CharacterOverviewState staleOverview = CreateOverview(foundation) with
        {
            Session = CreateSession(contentRevision: foundation.Binding.ContentRevision + 1)
        };

        CharacterCreationFoundationInteractionPrepareResult stale = presenter.Prepare(
            staleOverview,
            Selection());

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, stale.Outcome);
        Assert.IsNull(stale.PreparedPreview);
        CollectionAssert.Contains(
            stale.Blockers.ToArray(),
            CharacterCreationFoundationInteractionBlockers.BindingMismatch);
        Assert.AreEqual(0, service.PreviewCalls);

        CharacterCreationFoundationPreparedPreview prepared = RequirePrepared(presenter.Prepare(
            CreateOverview(foundation),
            Selection()));
        int loadCallsBeforeConfirm = service.LoadCalls;
        CharacterCreationFoundationInteractionConfirmResult mismatch = presenter.Confirm(
            CreateOverview(foundation),
            new CharacterCreationFoundationConfirmation(
                prepared,
                Digest('f'),
                ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Conflict, mismatch.Outcome);
        CollectionAssert.Contains(
            mismatch.Blockers.ToArray(),
            CharacterCreationFoundationBlockers.PreviewDigestMismatch);
        Assert.AreEqual(loadCallsBeforeConfirm, service.LoadCalls);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Unavailable_and_blocked_authority_are_preserved_without_widening()
    {
        CharacterCreationFoundationState foundation = CreateFoundation();
        var unavailableService = new FakeFoundationService(foundation)
        {
            LoadResult = new CharacterCreationFoundationResult<CharacterCreationFoundationState>(
                CharacterCreationFoundationOutcomes.Missing,
                null,
                [CharacterCreationFoundationBlockers.WorkspaceUnavailable])
        };
        var unavailablePresenter = new CharacterCreationFoundationInteractionPresenter(
            unavailableService);

        CharacterCreationFoundationInteractionPrepareResult unavailable =
            unavailablePresenter.Prepare(CreateOverview(foundation), Selection());

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Missing, unavailable.Outcome);
        Assert.IsNull(unavailable.State);
        Assert.IsNull(unavailable.PreparedPreview);
        Assert.AreEqual(0, unavailableService.PreviewCalls);

        CharacterCreationFoundationPreview blockedPreview = CreatePreview(foundation) with
        {
            AuthorityBlockers =
            [
                CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired
            ],
            CanConfirm = false,
            CanApply = false
        };
        var blockedService = new FakeFoundationService(foundation)
        {
            PreviewResult = new CharacterCreationFoundationResult<CharacterCreationFoundationPreview>(
                CharacterCreationFoundationOutcomes.Blocked,
                blockedPreview,
                blockedPreview.AuthorityBlockers)
        };
        var blockedPresenter = new CharacterCreationFoundationInteractionPresenter(blockedService);

        CharacterCreationFoundationInteractionPrepareResult blocked = blockedPresenter.Prepare(
            CreateOverview(foundation),
            Selection());

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, blocked.Outcome);
        Assert.IsNotNull(blocked.PreparedPreview);
        Assert.IsFalse(blocked.PreparedPreview.CanConfirm);
        CollectionAssert.Contains(
            blocked.Blockers.ToArray(),
            CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired);

        CharacterOverviewState completed = CreateOverview(foundation) with
        {
            Profile = CreateProfile(created: true),
            CreationWizard = null,
            CreationFoundation = null
        };
        int completedLoadBaseline = blockedService.LoadCalls;
        CharacterCreationFoundationInteractionLoadResult completedResult =
            blockedPresenter.Load(completed);
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Blocked, completedResult.Outcome);
        CollectionAssert.Contains(
            completedResult.Blockers.ToArray(),
            CharacterCreationFoundationBlockers.CharacterAlreadyCreated);
        Assert.AreEqual(completedLoadBaseline, blockedService.LoadCalls);
    }

    [TestMethod]
    public void Successful_confirm_returns_validated_receipt_and_refreshed_authority()
    {
        CharacterCreationFoundationState foundation = CreateFoundation();
        CharacterCreationFoundationPreview preview = CreatePreview(foundation);
        CharacterCreationFoundationApplyReceipt receipt = CreateReceipt(preview);
        CharacterCreationFoundationState refreshed = CreateRefreshed(foundation, preview, receipt);
        var service = new FakeFoundationService(foundation)
        {
            PreviewResult = Success(preview),
            ConfirmResult = Success(receipt),
            StateAfterConfirm = refreshed
        };
        var presenter = new CharacterCreationFoundationInteractionPresenter(service);
        CharacterCreationFoundationPreparedPreview prepared = RequirePrepared(presenter.Prepare(
            CreateOverview(foundation),
            Selection()));

        CharacterCreationFoundationInteractionConfirmResult result = presenter.Confirm(
            CreateOverview(foundation),
            new CharacterCreationFoundationConfirmation(
                prepared,
                prepared.PreviewDigest,
                ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome);
        Assert.AreSame(receipt, result.Receipt);
        Assert.IsNotNull(result.RefreshedState);
        Assert.AreEqual(receipt.ContentRevision, result.RefreshedState.Binding.ContentRevision);
        Assert.AreEqual(receipt.SavedRevision, result.RefreshedState.Binding.SavedRevision);
        Assert.AreEqual(receipt.DraftDigest, result.RefreshedState.PendingDraft?.DraftDigest);
        Assert.IsFalse(result.RefreshedState.PendingDraft?.CharacterEffectsApplied ?? true);
        Assert.AreEqual(1, service.ConfirmCalls);
        Assert.AreEqual(3, service.LoadCalls);
        Assert.IsNotNull(service.LastConfirmRequest);
        Assert.IsTrue(service.LastConfirmRequest.ExplicitlyConfirmed);
        Assert.AreEqual(prepared.PreviewDigest, service.LastConfirmRequest.PreviewDigest);
        Assert.AreSame(prepared.Binding, service.LastConfirmRequest.Binding);
    }

    private static CharacterCreationFoundationSelectionInput Selection()
        => new(
            "Human",
            "nationality-module",
            "nationality-version",
            new Dictionary<string, string> { ["native-language"] = "Sperethiel" });

    private static CharacterCreationFoundationState CreateFoundation()
    {
        var binding = new CharacterCreationFoundationBinding(
            new CharacterWorkspaceId("ws-wizard"),
            ContentRevision: 7,
            SavedRevision: 7,
            RawCharacterXmlDigest: Digest('a'),
            CharacterDigestSemantics: CharacterCreationFoundationDigestSemantics.RawCharacterXmlSha256,
            SourceDigest: Digest('b'),
            SourceDigestSemantics: CharacterCreationFoundationDigestSemantics.RawSourceInputsSha256,
            SourceFilterApplied: false,
            EnabledSources: ["RF", "SR5"]);
        return new CharacterCreationFoundationState(
            CharacterCreationFoundationSchemas.SnapshotV1,
            binding,
            RulesetDefaults.Sr5,
            "Human",
            CharacterCreationBuildMethods.LifeModules,
            CharacterCreated: false,
            MetatypeOptions: [Metatype("human", "Human", 0m), Metatype("elf", "Elf", 40m)],
            NationalityOptions: [CreateNationality()],
            LifeModuleBudget: Budget(used: 0m, remaining: 750m),
            PendingDraft: null,
            ResumeStatus: CharacterCreationFoundationResumeStatuses.AuthorityRequired,
            AuthorityBlockers: [],
            SnapshotDigest: Digest('c'));
    }

    private static CharacterCreationLegalOption Metatype(
        string id,
        string label,
        decimal karma)
        => new(
            id,
            label,
            IsEnabled: true,
            DisableReasonKey: null,
            DisableReasonArguments: new Dictionary<string, string>(),
            Costs: [new CharacterCreationChoiceCost(CharacterCreationBudgetIds.Karma, karma, "karma")],
            Consequences: [],
            SourceAnchorIds: [$"metatypes.xml#metatype:{id}"]);

    private static LifeModuleLegalOptionDto CreateNationality()
    {
        var followUp = new LifeModuleFollowUpPromptDto(
            "native-language",
            "Native language",
            "select",
            IsRequired: true,
            Options:
            [
                new LifeModuleFollowUpOptionDto(
                    "sperethiel",
                    "Sperethiel",
                    IsEnabled: true,
                    DisableReasonKey: null,
                    DisableReasonArguments: new Dictionary<string, string>(),
                    SourceValue: "Sperethiel")
            ],
            SourceAnchorIds: ["lifemodules.xml#followup:native-language"],
            EffectId: "language-effect",
            ValuePath: "name");
        var requirement = new LifeModuleRequirementProjectionDto(
            "human-or-elf",
            "Human or Elf",
            IsMet: true,
            DisableReasonKey: null,
            DisableReasonArguments: new Dictionary<string, string>(),
            SourceAnchorIds: ["lifemodules.xml#requirement:human-or-elf"],
            Operator: "oneof",
            SubjectKind: "metatype",
            AcceptedValues: ["Human", "Elf"],
            RawXml: "<oneof><metatype>Human</metatype><metatype>Elf</metatype></oneof>",
            RequiresCharacterAuthority: false);
        var version = new LifeModuleVersionProjectionDto(
            "nationality-version",
            "Elves/Humans",
            IsEnabled: true,
            Requirements: [requirement],
            Effects: [],
            FollowUps: [followUp],
            SourceAnchorIds: ["lifemodules.xml#version:nationality-version"],
            StoryTemplate: "$real grew up in Tir Tairngire.",
            KarmaCost: 15m,
            KarmaRaw: "15",
            KarmaIsExact: true,
            Source: "RF",
            Page: 67,
            PageReference: "67",
            AuthorityBlockers: []);
        return new LifeModuleLegalOptionDto(
            "nationality-module",
            LifeModuleJourneyStageOrders.Nationality,
            "Tir Tairngire",
            15m,
            "RF",
            67,
            "$real was born there.",
            IsEnabled: true,
            Requirements: [],
            Versions: [version],
            Effects: [],
            FollowUps: [],
            SourceAnchorIds: ["lifemodules.xml#module:nationality-module"],
            StageId: CharacterCreationLifeModuleStageIds.Nationality,
            CanRepeat: false,
            KarmaRaw: "15",
            KarmaIsExact: true,
            PageReference: "67",
            AuthorityBlockers: []);
    }

    private static CharacterCreationFoundationPreview CreatePreview(
        CharacterCreationFoundationState foundation)
    {
        LifeModuleLegalOptionDto nationality = foundation.NationalityOptions[0];
        LifeModuleVersionProjectionDto version = nationality.Versions[0];
        CharacterCreationFoundationSelection selection = new(
            nationality.ModuleId,
            version.VersionId);
        var diff = new CharacterCreationFoundationDiffEntry(
            "nationality-selection",
            "life-module",
            nationality.ModuleId,
            BeforeValue: null,
            AfterValue: nationality.Name,
            Phase: CharacterCreationFoundationDiffPhases.DraftLedger,
            AppliesToCharacterDocument: false,
            IsAuthoritative: true,
            CanApply: true,
            Blockers: [],
            SourceAnchorIds: nationality.SourceAnchorIds);
        return new CharacterCreationFoundationPreview(
            CharacterCreationFoundationSchemas.PreviewV1,
            foundation.Binding,
            "Human",
            selection,
            nationality,
            version,
            version.Requirements,
            new Dictionary<string, string> { ["native-language"] = "Sperethiel" },
            foundation.LifeModuleBudget,
            new CharacterCreationChoiceCost(CharacterCreationBudgetIds.LifeModules, 15m, "karma"),
            Budget(used: 15m, remaining: 735m),
            Diff: [diff],
            AuthorityBlockers: [],
            RequiresExplicitConfirmation: true,
            CanConfirm: true,
            CanApply: true,
            CharacterEffectsApplied: false,
            PreviewDigest: Digest('d'));
    }

    private static CharacterCreationFoundationApplyReceipt CreateReceipt(
        CharacterCreationFoundationPreview preview)
        => new(
            preview.Binding.WorkspaceId,
            PreviousContentRevision: preview.Binding.ContentRevision,
            ContentRevision: preview.Binding.ContentRevision + 1,
            SavedRevision: preview.Binding.ContentRevision + 1,
            RawCharacterXmlDigest: preview.Binding.RawCharacterXmlDigest,
            SourceDigest: preview.Binding.SourceDigest,
            PreviewDigest: preview.PreviewDigest,
            Selection: preview.Selection,
            Metatype: preview.RequestedMetatype,
            DraftRevision: 1,
            DraftDigest: Digest('e'),
            CharacterEffectsApplied: false);

    private static CharacterCreationFoundationState CreateRefreshed(
        CharacterCreationFoundationState before,
        CharacterCreationFoundationPreview preview,
        CharacterCreationFoundationApplyReceipt receipt)
    {
        var draft = new CharacterCreationFoundationDraftLedger(
            CharacterCreationFoundationSchemas.DraftLedgerV1,
            receipt.WorkspaceId,
            receipt.DraftRevision,
            receipt.PreviousContentRevision,
            receipt.RawCharacterXmlDigest,
            receipt.SourceDigest,
            preview.RequestedMetatype,
            preview.Selection,
            preview.RequirementEvaluations,
            ProjectedEffects: [],
            preview.FollowUpValues,
            SourceAnchorIds: preview.Nationality?.SourceAnchorIds ?? [],
            CompilationStatus: CharacterCreationFoundationDraftStatuses.PendingFinalization,
            CharacterEffectsApplied: false,
            receipt.DraftDigest);
        return before with
        {
            Binding = before.Binding with
            {
                ContentRevision = receipt.ContentRevision,
                SavedRevision = receipt.SavedRevision
            },
            PendingDraft = draft,
            SnapshotDigest = Digest('f')
        };
    }

    private static CharacterCreationBudgetState Budget(decimal used, decimal remaining)
        => new(
            CharacterCreationBudgetIds.LifeModules,
            "Life Modules Karma",
            Total: 750m,
            Used: used,
            Remaining: remaining,
            IsExact: true,
            Blockers: [],
            Unit: "karma");

    private static CharacterOverviewState CreateOverview(
        CharacterCreationFoundationState foundation)
    {
        WorkspaceSessionState session = CreateSession(foundation.Binding.ContentRevision);
        var wizard = new CharacterCreationWizardSnapshot(
            CharacterCreationWizardSchemas.SnapshotV1,
            foundation.Binding.WorkspaceId.Value,
            foundation.Binding.ContentRevision,
            foundation.Binding.RawCharacterXmlDigest,
            foundation.Binding.SourceDigest,
            foundation.RulesetId,
            RuntimeFingerprint: string.Empty,
            foundation.BuildMethod,
            CharacterCreated: false,
            ActiveStepId: CharacterCreationWizardStepIds.Foundation,
            Steps: [],
            Budgets: [foundation.LifeModuleBudget],
            LegalOptionsByStep: new Dictionary<string, IReadOnlyList<CharacterCreationLegalOption>>(),
            CompletionBlockers: foundation.AuthorityBlockers,
            Warnings: [],
            CanFinalize: false,
            SnapshotDigest: Digest('9'));
        return CharacterOverviewState.Empty with
        {
            WorkspaceId = foundation.Binding.WorkspaceId,
            Session = session,
            Profile = CreateProfile(created: false),
            CreationWizard = wizard,
            CreationFoundation = foundation
        };
    }

    private static WorkspaceSessionState CreateSession(long contentRevision)
    {
        var workspaceId = new CharacterWorkspaceId("ws-wizard");
        return new WorkspaceSessionState(
            workspaceId,
            [
                new OpenWorkspaceState(
                    workspaceId,
                    "Wizard",
                    "W",
                    DateTimeOffset.Parse("2026-08-22T00:00:00+00:00"),
                    RulesetDefaults.Sr5,
                    contentRevision,
                    SavedRevision: contentRevision)
            ],
            [workspaceId]);
    }

    private static CharacterProfileSection CreateProfile(bool created)
        => new(
            "Wizard", "W", string.Empty, "Human", string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty, "1.0", "1.0",
            CharacterCreationBuildMethods.LifeModules, "Standard", created,
            false, false, false, false, 0, 0);

    private static CharacterCreationFoundationPreparedPreview RequirePrepared(
        CharacterCreationFoundationInteractionPrepareResult result)
        => result.PreparedPreview
           ?? throw new AssertFailedException("Expected a prepared preview.");

    private static T AssertExactlyOne<T>(IReadOnlyList<T> values)
    {
        Assert.HasCount(1, values);
        return values[0];
    }

    private static string Digest(char value) => "sha256:" + new string(value, 64);

    private static CharacterCreationFoundationResult<T> Success<T>(T value)
        where T : class
        => new(CharacterCreationFoundationOutcomes.Success, value, []);

    private sealed class FakeFoundationService : ICharacterCreationFoundationService
    {
        private CharacterCreationFoundationState _currentState;

        public FakeFoundationService(CharacterCreationFoundationState currentState)
        {
            _currentState = currentState;
            LoadResult = Success(currentState);
            PreviewResult = new CharacterCreationFoundationResult<CharacterCreationFoundationPreview>(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                [CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired]);
            ConfirmResult = new CharacterCreationFoundationResult<CharacterCreationFoundationApplyReceipt>(
                CharacterCreationFoundationOutcomes.Blocked,
                null,
                [CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired]);
        }

        public int LoadCalls { get; private set; }
        public int PreviewCalls { get; private set; }
        public int ConfirmCalls { get; private set; }
        public CharacterCreationFoundationResult<CharacterCreationFoundationState> LoadResult { get; set; }
        public CharacterCreationFoundationResult<CharacterCreationFoundationPreview> PreviewResult { get; set; }
        public CharacterCreationFoundationResult<CharacterCreationFoundationApplyReceipt> ConfirmResult { get; set; }
        public CharacterCreationFoundationState? StateAfterConfirm { get; set; }
        public CharacterCreationFoundationConfirmRequest? LastConfirmRequest { get; private set; }

        public CharacterCreationFoundationResult<CharacterCreationFoundationState> Load(
            CharacterCreationFoundationLoadRequest request)
        {
            LoadCalls++;
            if (StateAfterConfirm is not null && ReferenceEquals(_currentState, StateAfterConfirm))
                return Success(_currentState);
            return LoadResult;
        }

        public CharacterCreationFoundationResult<CharacterCreationFoundationPreview> Preview(
            CharacterCreationFoundationPreviewRequest request)
        {
            PreviewCalls++;
            return PreviewResult;
        }

        public CharacterCreationFoundationResult<CharacterCreationFoundationApplyReceipt> Confirm(
            CharacterCreationFoundationConfirmRequest request)
        {
            ConfirmCalls++;
            LastConfirmRequest = request;
            if (ConfirmResult.Value is not null && StateAfterConfirm is not null)
                _currentState = StateAfterConfirm;
            return ConfirmResult;
        }
    }
}
