using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationGearInteractionPresenterTests
{
    [TestMethod]
    public void Load_and_prepare_preserve_catalog_basket_budget_and_core_preview()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeGearService(fixture.State, fixture.Preview);
        var presenter = new CharacterCreationGearInteractionPresenter(service);
        CharacterOverviewState overview = CreateOverview(fixture.Content, fixture.State.Binding);

        CharacterCreationGearInteractionLoadResult loaded = presenter.Load(overview);
        CharacterCreationGearInteractionPrepareResult prepared = presenter.Prepare(
            overview,
            fixture.Basket);

        Assert.AreEqual(CharacterCreationGearOutcomes.Available, loaded.Outcome);
        Assert.IsNotNull(loaded.State);
        Assert.AreEqual(50_000m, loaded.State.Budget.TotalStartingNuyen);
        Assert.HasCount(2, loaded.State.Authority.Options);
        Assert.IsFalse(loaded.State.Authority.Options.Single(option =>
            option.Name == "Unsupported formula").IsSelectable);
        Assert.AreEqual(CharacterCreationGearOutcomes.Available, prepared.Outcome);
        Assert.IsNotNull(prepared.PreparedPreview);
        Assert.AreEqual(fixture.State.SnapshotDigest,
            prepared.PreparedPreview.StateSnapshotDigest);
        Assert.AreEqual(fixture.Preview.PreviewDigest,
            prepared.PreparedPreview.Preview.PreviewDigest);
        Assert.AreEqual(1_000m,
            prepared.PreparedPreview.Preview.BudgetAfter.BasketCost);
        Assert.AreEqual(49_000m,
            prepared.PreparedPreview.Preview.BudgetAfter.RemainingNuyen);
        Assert.AreEqual(fixture.State.Binding, service.LastPreview?.Binding);
        CollectionAssert.AreEqual(
            fixture.Basket.ToArray(),
            service.LastPreview!.Basket.ToArray());
    }

    [TestMethod]
    public void Confirm_requires_exact_consent_then_applies_refreshes_and_replays()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeGearService(fixture.State, fixture.Preview);
        var presenter = new CharacterCreationGearInteractionPresenter(service);
        CharacterOverviewState overview = CreateOverview(fixture.Content, fixture.State.Binding);
        CharacterCreationGearPreparedPreview prepared = presenter.Prepare(
            overview,
            fixture.Basket).PreparedPreview!;

        CharacterCreationGearInteractionConfirmResult noConsent = presenter.Confirm(
            overview,
            new CharacterCreationGearConfirmation(
                prepared,
                prepared.Preview.PreviewDigest,
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: false));
        CharacterCreationGearInteractionConfirmResult tampered = presenter.Confirm(
            overview,
            new CharacterCreationGearConfirmation(
                prepared,
                Digest('f'),
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: true));
        Assert.AreEqual(CharacterCreationGearOutcomes.Invalid, noConsent.Outcome);
        Assert.AreEqual(CharacterCreationGearOutcomes.Conflict, tampered.Outcome);
        Assert.AreEqual(0, service.ConfirmCalls);

        CharacterCreationGearInteractionConfirmResult applied = presenter.Confirm(
            overview,
            new CharacterCreationGearConfirmation(
                prepared,
                prepared.Preview.PreviewDigest,
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: true));
        Assert.AreEqual(CharacterCreationGearOutcomes.Applied, applied.Outcome);
        Assert.IsNotNull(applied.Receipt);
        Assert.IsFalse(applied.Receipt.CharacterDocumentChanged);
        Assert.AreEqual(1_000m, applied.Receipt.BasketCost);
        Assert.AreEqual(49_000m, applied.Receipt.RemainingNuyen);
        Assert.AreEqual(1, service.ConfirmCalls);
        Assert.IsNotNull(applied.RefreshedState?.PendingDraft);
        Assert.AreEqual(applied.Receipt.DraftDigest,
            applied.RefreshedState.PendingDraft.DraftDigest);

        CharacterCreationGearInteractionReceiptLookupResult lookup = presenter.LookupReceipt(
            overview,
            prepared.IdempotencyKey);
        Assert.AreEqual(CharacterCreationGearOutcomes.Available, lookup.Outcome);
        Assert.AreEqual(applied.Receipt.ReceiptDigest, lookup.Receipt!.ReceiptDigest);
    }

    [TestMethod]
    public void Disabled_options_and_stale_overview_fail_closed_before_mutation()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeGearService(fixture.State, fixture.Preview);
        var presenter = new CharacterCreationGearInteractionPresenter(service);
        CharacterOverviewState overview = CreateOverview(fixture.Content, fixture.State.Binding);
        CharacterCreationGearCatalogOption disabled = fixture.State.Authority.Options.Single(option =>
            !option.IsSelectable);

        CharacterCreationGearInteractionPrepareResult unsupported = presenter.Prepare(
            overview,
            [new CharacterCreationGearSelection(disabled.OptionId, 1)]);
        CharacterCreationGearInteractionLoadResult stale = presenter.Load(overview with
        {
            Session = overview.Session with
            {
                OpenWorkspaces =
                [
                    overview.ActiveWorkspace! with
                    {
                        ContentRevision = overview.ActiveWorkspace.ContentRevision + 1
                    }
                ]
            }
        });

        Assert.AreEqual(CharacterCreationGearOutcomes.Invalid, unsupported.Outcome);
        Assert.IsNull(unsupported.PreparedPreview);
        Assert.AreEqual(0, service.PreviewCalls);
        Assert.AreEqual(CharacterCreationGearOutcomes.Conflict, stale.Outcome);
        CollectionAssert.Contains(stale.Blockers.ToList(),
            CharacterCreationGearInteractionBlockers.BindingMismatch);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    private static Fixture CreateFixture()
    {
        const string content = "<character><created>False</created><buildmethod>Priority</buildmethod></character>";
        string rawDigest = CharacterCreationFoundationDraftLedgerIntegrity
            .ComputeRawCharacterXmlDigest(content);
        CharacterWorkspaceId workspaceId = new("creation-gear-presentation");
        CharacterCreationGearAuthority authority = Authority();
        var resourcesBudget = new CharacterCreationResourcesBudget(
            50_000m, 0, 0m, 50_000m, 0m, 50_000m, 0m, 5_000m, 45_000m,
            true, [], CharacterCreationResourcesSourceAnchors.All);
        var contributionCandidate = new CharacterCreationResourcesFinalizationContribution(
            CharacterCreationResourcesSchemas.ContributionV1,
            "D",
            "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
            50_000m,
            0,
            rawDigest,
            CharacterCreationResourcesSourceAnchors.All,
            string.Empty);
        CharacterCreationResourcesFinalizationContribution resourceContribution = contributionCandidate with
        {
            ContributionDigest = CharacterCreationResourcesRules.ComputeContributionDigest(
                contributionCandidate)
        };
        var resourceDraftCandidate = new CharacterCreationResourcesDraft(
            CharacterCreationResourcesSchemas.DraftV1,
            workspaceId,
            1,
            1,
            rawDigest,
            1,
            Digest('1'),
            Digest('2'),
            Digest('3'),
            Digest('4'),
            Digest('5'),
            "karma:0",
            0,
            resourcesBudget,
            resourceContribution,
            CharacterCreationResourcesSourceAnchors.All,
            false,
            Digest('6'),
            Digest('7'),
            Digest('8'),
            string.Empty);
        CharacterCreationResourcesDraft resourceDraft = resourceDraftCandidate with
        {
            DraftDigest = CharacterCreationResourcesRules.ComputeDraftDigest(resourceDraftCandidate)
        };
        var binding = new CharacterCreationGearBinding(
            workspaceId,
            7,
            7,
            0,
            rawDigest,
            Digest('9'),
            resourceDraft.DraftRevision,
            resourceDraft.DraftDigest,
            authority.AuthorityDigest,
            authority.SourceDigest,
            authority.RulesDigest,
            authority.RuntimeDigest);
        var stateCandidate = new CharacterCreationGearState(
            CharacterCreationGearSchemas.StateV1,
            CharacterCreationWizardStepIds.Resources,
            binding,
            authority,
            resourceDraft,
            null,
            new CharacterCreationGearBudget(50_000m, 0m, 50_000m, 0m, true, []),
            [],
            true,
            string.Empty);
        CharacterCreationGearState state = stateCandidate with
        {
            SnapshotDigest = CharacterCreationGearRules.ComputeStateDigest(stateCandidate)
        };
        CharacterCreationGearCatalogOption option = authority.Options.Single(item => item.IsSelectable);
        CharacterCreationGearSelection[] basket = [new(option.OptionId, 20)];
        CharacterCreationGearRules.TryProjectBasket(
            basket,
            authority,
            50_000m,
            out CharacterCreationGearLine[] lines,
            out CharacterCreationGearBudget budgetAfter,
            out _);
        var gearContributionCandidate = new CharacterCreationGearFinalizationContribution(
            CharacterCreationGearSchemas.ContributionV1,
            rawDigest,
            resourceDraft.DraftRevision,
            resourceDraft.DraftDigest,
            lines,
            budgetAfter.BasketCost,
            CharacterCreationGearSourceAnchors.All,
            string.Empty);
        CharacterCreationGearFinalizationContribution gearContribution = gearContributionCandidate with
        {
            ContributionDigest = CharacterCreationGearRules.ComputeContributionDigest(
                gearContributionCandidate)
        };
        var gearDraft = new CharacterCreationGearDraft(
            CharacterCreationGearSchemas.DraftV1,
            workspaceId,
            1,
            7,
            rawDigest,
            resourceDraft.DraftRevision,
            resourceDraft.DraftDigest,
            authority.AuthorityDigest,
            authority.SourceDigest,
            authority.RulesDigest,
            authority.RuntimeDigest,
            lines,
            budgetAfter,
            gearContribution,
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        var previewCandidate = new CharacterCreationGearPreview(
            CharacterCreationGearSchemas.PreviewV1,
            CharacterCreationWizardStepIds.Resources,
            binding,
            null,
            gearDraft,
            state.Budget,
            budgetAfter,
            gearContribution,
            [],
            true,
            true,
            string.Empty);
        CharacterCreationGearPreview preview = previewCandidate with
        {
            PreviewDigest = CharacterCreationGearRules.ComputePreviewDigest(previewCandidate)
        };
        return new Fixture(content, state, basket, preview);
    }

    private static CharacterCreationGearAuthority Authority()
    {
        CharacterCreationGearCatalogOption exact = Option(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            "Medkit Supplies",
            500m,
            10,
            8,
            true,
            []);
        CharacterCreationGearCatalogOption disabled = Option(
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            "Unsupported formula",
            0m,
            1,
            0,
            false,
            [CharacterCreationGearBlockers.UnsupportedSemantics],
            exact: false);
        var candidate = new CharacterCreationGearAuthority(
            CharacterCreationGearSchemas.AuthorityV1,
            "sr5",
            "default",
            12,
            4096,
            1_000_000,
            [exact, disabled],
            CharacterCreationGearSourceAnchors.All,
            [],
            true,
            Digest('a'),
            Digest('b'),
            Digest('c'),
            Digest('d'),
            string.Empty);
        return candidate with
        {
            AuthorityDigest = CharacterCreationGearRules.ComputeAuthorityDigest(candidate)
        };
    }

    private static CharacterCreationGearCatalogOption Option(
        Guid id,
        string name,
        decimal cost,
        int packageQuantity,
        int availability,
        bool selectable,
        IReadOnlyList<string> blockers,
        bool exact = true)
    {
        var candidate = new CharacterCreationGearCatalogOption(
            $"gear:{id:D}", id, name, "Biotech", cost, packageQuantity, availability,
            CharacterCreationGearLegality.Restricted, "SR5", "450", selectable, exact, exact,
            blockers, [$"gear.xml#gear:{id:D}"], Digest('e'), string.Empty);
        return candidate with
        {
            OptionDigest = CharacterCreationGearRules.ComputeOptionDigest(candidate)
        };
    }

    private static CharacterOverviewState CreateOverview(
        string content,
        CharacterCreationGearBinding binding)
    {
        WorkspaceSessionState session = new(
            binding.WorkspaceId,
            [new OpenWorkspaceState(
                binding.WorkspaceId,
                "Wizard",
                "W",
                DateTimeOffset.Parse("2026-08-27T00:00:00+00:00"),
                RulesetDefaults.Sr5,
                binding.ContentRevision,
                binding.SavedRevision)],
            [binding.WorkspaceId]);
        WorkspaceOverviewLoadResult loaded = CreateLoadedOverview(
            content,
            binding.ContentRevision,
            binding.SavedRevision);
        return new WorkspaceOverviewStateFactory().CreateLoadedState(
            CharacterOverviewState.Empty,
            binding.WorkspaceId,
            session,
            loaded,
            null,
            true);
    }

    private static WorkspaceOverviewLoadResult CreateLoadedOverview(
        string content,
        long contentRevision,
        long savedRevision) => new(
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
        contentRevision,
        savedRevision,
        new WorkspaceDocument(content, RulesetDefaults.Sr5));

    private static string Digest(char value) => "sha256:" + new string(value, 64);

    private sealed record Fixture(
        string Content,
        CharacterCreationGearState State,
        IReadOnlyList<CharacterCreationGearSelection> Basket,
        CharacterCreationGearPreview Preview);

    private sealed class FakeGearService(
        CharacterCreationGearState initial,
        CharacterCreationGearPreview preview) : ICharacterCreationGearService
    {
        private CharacterCreationGearState _current = initial;
        private CharacterCreationGearReceipt? _receipt;
        public int PreviewCalls { get; private set; }
        public int ConfirmCalls { get; private set; }
        public CharacterCreationGearPreviewRequest? LastPreview { get; private set; }

        public CharacterCreationGearResult<CharacterCreationGearState> Load(
            CharacterCreationGearLoadRequest request) =>
            new(CharacterCreationGearOutcomes.Available, _current, []);

        public CharacterCreationGearResult<CharacterCreationGearPreview> Preview(
            CharacterCreationGearPreviewRequest request)
        {
            PreviewCalls++;
            LastPreview = request;
            return new(CharacterCreationGearOutcomes.Available, preview, []);
        }

        public CharacterCreationGearResult<CharacterCreationGearReceipt> Confirm(
            CharacterCreationGearConfirmRequest request)
        {
            ConfirmCalls++;
            string keyDigest = CharacterCreationGearRules.ComputeIdempotencyKeyDigest(
                "chummer.sr5.creation-gear.idempotency.v1\0" + request.IdempotencyKey);
            string commandDigest = CharacterCreationGearRules.ComputeCommandDigest(request);
            CharacterCreationGearDraft draftCandidate = preview.After with
            {
                LastIdempotencyKeyDigest = keyDigest,
                LastPreviewDigest = request.PreviewDigest,
                LastCommandDigest = commandDigest,
                DraftDigest = string.Empty
            };
            CharacterCreationGearDraft draft = draftCandidate with
            {
                DraftDigest = CharacterCreationGearRules.ComputeDraftDigest(draftCandidate)
            };
            var receiptCandidate = new CharacterCreationGearReceipt(
                CharacterCreationGearSchemas.ReceiptV1,
                "creation-gear-123456789012345678901234",
                request.Binding.WorkspaceId,
                keyDigest,
                commandDigest,
                request.Binding.WorkspaceRevision,
                request.Binding.WorkspaceRevision + 1,
                request.Binding.SavedRevision,
                request.Binding.WorkspaceRevision + 1,
                request.Binding.RawCharacterXmlDigest,
                request.Binding.ResourcesDraftRevision,
                request.Binding.ResourcesDraftDigest,
                request.Binding.AuthorityDigest,
                request.Binding.SourceDigest,
                request.Binding.RulesDigest,
                request.Binding.RuntimeDigest,
                draft.Lines.Count,
                draft.Budget.BasketCost,
                draft.Budget.RemainingNuyen,
                draft.DraftRevision,
                draft.DraftDigest,
                request.PreviewDigest,
                CharacterCreationGearRules.ReceiptLedgerRootDigest,
                false,
                string.Empty);
            _receipt = receiptCandidate with
            {
                ReceiptDigest = CharacterCreationGearRules.ComputeReceiptDigest(receiptCandidate)
            };
            CharacterCreationGearBinding afterBinding = request.Binding with
            {
                WorkspaceRevision = request.Binding.WorkspaceRevision + 1,
                ContentRevision = request.Binding.ContentRevision + 1,
                SavedRevision = request.Binding.WorkspaceRevision + 1,
                AuxiliaryStateDigest = Digest('0')
            };
            var stateCandidate = _current with
            {
                Binding = afterBinding,
                PendingDraft = draft,
                Budget = draft.Budget,
                SnapshotDigest = string.Empty
            };
            _current = stateCandidate with
            {
                SnapshotDigest = CharacterCreationGearRules.ComputeStateDigest(stateCandidate)
            };
            return new(CharacterCreationGearOutcomes.Applied, _receipt, []);
        }

        public CharacterCreationGearResult<CharacterCreationGearReceipt> LookupReceipt(
            CharacterCreationGearReceiptLookupRequest request) => _receipt is null
            ? new(CharacterCreationGearOutcomes.NotFound, null, [])
            : new(CharacterCreationGearOutcomes.Available, _receipt, []);
    }
}
