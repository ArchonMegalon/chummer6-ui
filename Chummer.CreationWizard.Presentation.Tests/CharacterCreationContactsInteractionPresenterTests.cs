using System.Security.Cryptography;
using System.Text;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationContactsInteractionPresenterTests
{
    [TestMethod]
    public void Workspace_factory_accepts_exact_core_load_with_read_only_fields_and_opaque_pet()
    {
        string stateDirectory = Path.Combine(
            Path.GetTempPath(),
            "chummer-presentation-contacts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stateDirectory);
        try
        {
            CharacterWorkspaceId workspaceId = new("creation-contact-authority");
            const string content = """
<character>
  <created>False</created>
  <gameedition>SR5</gameedition>
  <settings>default.xml</settings>
  <buildmethod>Priority</buildmethod>
  <contactpoints>15</contactpoints>
  <improvements />
  <contacts>
    <contact>
      <guid>b7ff1656-c972-47f5-b6f1-2f3c31320b06</guid><name>Fixer</name><role>Broker</role>
      <connection>3</connection><loyalty>2</loyalty><group>False</group><free>False</free>
      <family>True</family><blackmail>True</blackmail><type>Contact</type>
    </contact>
    <contact>
      <guid>7356408a-f17a-4422-a05f-d757a341d43f</guid><name>Sibling</name>
      <connection>4</connection><loyalty>4</loyalty><group>True</group><free>False</free>
      <family>False</family><blackmail>False</blackmail><type>Contact</type>
    </contact>
    <contact>
      <guid>22222222-3333-4444-8555-666666666666</guid><name>Critter</name>
      <connection>1</connection><loyalty>1</loyalty><group>False</group><free>False</free>
      <family>False</family><blackmail>False</blackmail><type>Pet</type>
    </contact>
  </contacts>
</character>
""";
            var store = new FileWorkspaceStore(stateDirectory);
            var created = store.CreateWorkspaceDocument(
                workspaceId,
                new WorkspaceDocument(content, RulesetDefaults.Sr5, WorkspaceDocumentFormat.Chum5Xml));
            Assert.IsTrue(created.Success);
            var service = new CharacterCreationContactsService(store);
            CharacterCreationContactResult<CharacterCreationContactsState> loaded = service.Load(
                new CharacterCreationContactsLoadRequest(workspaceId));
            Assert.AreEqual(CharacterCreationContactOutcomes.Available, loaded.Outcome);
            Assert.IsNotNull(loaded.Value);

            WorkspaceSessionState session = new(
                workspaceId,
                [new OpenWorkspaceState(
                    workspaceId,
                    "Wizard",
                    "W",
                    DateTimeOffset.Parse("2026-08-23T00:00:00+00:00"),
                    RulesetDefaults.Sr5,
                    ContentRevision: 1,
                    SavedRevision: 0)],
                [workspaceId]);
            CharacterOverviewState overview = new WorkspaceOverviewStateFactory(
                    creationContactsService: service)
                .CreateLoadedState(
                    CharacterOverviewState.Empty,
                    workspaceId,
                    session,
                    CreateLoadedOverview(content, contentRevision: 1, savedRevision: 0),
                    restoredView: null,
                    hasSavedWorkspace: false);

            Assert.IsNotNull(overview.CreationContacts);
            Assert.HasCount(2, overview.CreationContacts.Contacts);
            Assert.IsFalse(overview.CreationContacts.Contacts.Any(contact =>
                contact.ContactId == Guid.Parse("22222222-3333-4444-8555-666666666666")));
            Assert.IsFalse(overview.CreationWizard!.CompletionBlockers.Contains(
                CharacterCreationWizardProjector.ContactsAuthorityUnavailable,
                StringComparer.Ordinal));
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void Load_and_prepare_preserve_core_fields_options_budgets_and_write_plan()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeContactsService(fixture.BeforeState)
        {
            PreviewResult = Available(fixture.Preview)
        };
        CharacterOverviewState overview = CreateOverview(fixture, service);
        var presenter = new CharacterCreationContactsInteractionPresenter(service);

        CharacterCreationContactsInteractionLoadResult loaded = presenter.Load(overview);
        CharacterCreationContactsInteractionPrepareResult prepared = presenter.Prepare(
            overview,
            new CharacterCreationContactEditInput(fixture.ContactId, Free: true));

        Assert.AreEqual(CharacterCreationContactOutcomes.Available, loaded.Outcome);
        Assert.IsNotNull(loaded.State);
        Assert.HasCount(CharacterCreationContactFieldIds.All.Count, loaded.State.Contacts[0].Fields);
        CharacterCreationContactFieldAuthority free = loaded.State.Contacts[0].Fields.Single(
            field => field.FieldId == CharacterCreationContactFieldIds.Free);
        Assert.IsTrue(free.IsEditable);
        CollectionAssert.AreEqual(
            new[] { "False", "True" },
            free.LegalOptions.Select(static option => option.SerializedValue).ToArray());
        Assert.AreEqual(9, loaded.State.ContactBudget.Remaining);
        Assert.AreEqual(CharacterCreationContactOutcomes.Available, prepared.Outcome);
        Assert.IsNotNull(prepared.PreparedPreview);
        Assert.AreEqual(fixture.BeforeState.SnapshotDigest, prepared.PreparedPreview.ContactsSnapshotDigest);
        Assert.AreEqual(fixture.Preview.PreviewDigest, prepared.PreparedPreview.PreviewDigest);
        Assert.AreEqual(fixture.Preview.WritePlan.PlanDigest, prepared.PreparedPreview.WritePlan.PlanDigest);
        Assert.AreEqual(12, prepared.PreparedPreview.ContactBudgetBefore.Total);
        Assert.AreEqual(12, prepared.PreparedPreview.ContactBudgetAfter.Remaining);
        Assert.AreEqual(3, service.LoadCalls);
        Assert.AreEqual(1, service.PreviewCalls);
        Assert.AreEqual(0, service.ConfirmCalls);
        Assert.AreEqual(fixture.BeforeState.Binding, service.LastPreviewRequest?.Binding);
        Assert.AreEqual(fixture.ContactId, service.LastPreviewRequest?.Edit.ContactId);
        bool? freeEdit = service.LastPreviewRequest?.Edit.Free;
        Assert.IsNotNull(freeEdit);
        Assert.IsTrue(freeEdit.Value);
    }

    [TestMethod]
    public void Confirm_requires_explicit_confirmation_and_exact_preview_digest_before_core_calls()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeContactsService(fixture.BeforeState)
        {
            PreviewResult = Available(fixture.Preview)
        };
        CharacterOverviewState overview = CreateOverview(fixture, service);
        var presenter = new CharacterCreationContactsInteractionPresenter(service);
        CharacterCreationContactPreparedPreview prepared = RequirePrepared(presenter.Prepare(
            overview,
            new CharacterCreationContactEditInput(fixture.ContactId, Free: true)));
        int loadBaseline = service.LoadCalls;

        CharacterCreationContactsInteractionConfirmResult notConfirmed = presenter.Confirm(
            overview,
            new CharacterCreationContactConfirmation(
                prepared,
                prepared.PreviewDigest,
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: false));
        CharacterCreationContactsInteractionConfirmResult wrongDigest = presenter.Confirm(
            overview,
            new CharacterCreationContactConfirmation(
                prepared,
                Digest('f'),
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: true));
        CharacterCreationContactsInteractionConfirmResult wrongIdempotencyKey = presenter.Confirm(
            overview,
            new CharacterCreationContactConfirmation(
                prepared,
                prepared.PreviewDigest,
                prepared.IdempotencyKey + "-different",
                ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationContactOutcomes.Invalid, notConfirmed.Outcome);
        CollectionAssert.Contains(
            notConfirmed.Blockers.ToArray(),
            CharacterCreationContactsBlockers.ExplicitConfirmationRequired);
        Assert.AreEqual(CharacterCreationContactOutcomes.Conflict, wrongDigest.Outcome);
        CollectionAssert.Contains(
            wrongDigest.Blockers.ToArray(),
            CharacterCreationContactsBlockers.PreviewDigestMismatch);
        Assert.AreEqual(CharacterCreationContactOutcomes.Conflict, wrongIdempotencyKey.Outcome);
        CollectionAssert.Contains(
            wrongIdempotencyKey.Blockers.ToArray(),
            CharacterCreationContactsInteractionBlockers.IdempotencyKeyMismatch);
        Assert.AreEqual(loadBaseline, service.LoadCalls);
        Assert.AreEqual(0, service.ConfirmCalls);
    }

    [TestMethod]
    public void Stale_projected_authority_and_blocked_budget_fail_closed()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeContactsService(fixture.BeforeState)
        {
            PreviewResult = Available(fixture.Preview)
        };
        CharacterOverviewState overview = CreateOverview(fixture, service);
        var presenter = new CharacterCreationContactsInteractionPresenter(service);
        CharacterOverviewState stale = overview with
        {
            CreationContacts = fixture.BeforeState with { SnapshotDigest = Digest('9') }
        };

        CharacterCreationContactsInteractionPrepareResult staleResult = presenter.Prepare(
            stale,
            new CharacterCreationContactEditInput(fixture.ContactId, Free: true));

        Assert.AreEqual(CharacterCreationContactOutcomes.Conflict, staleResult.Outcome);
        CollectionAssert.Contains(
            staleResult.Blockers.ToArray(),
            CharacterCreationContactsInteractionBlockers.BindingMismatch);
        Assert.AreEqual(0, service.PreviewCalls);

        CharacterCreationContactBudget overspent = fixture.BeforeState.ContactBudget with
        {
            Used = 13,
            Remaining = 0,
            Overspend = 1,
            Blockers = [CharacterCreationContactsBlockers.BudgetExceeded]
        };
        CharacterCreationContactsState blockedState = fixture.BeforeState with
        {
            ContactBudget = overspent,
            Blockers = [CharacterCreationContactsBlockers.BudgetExceeded],
            SnapshotDigest = Digest('8')
        };
        service.CurrentState = blockedState;
        CharacterOverviewState blockedOverview = CreateOverview(
            fixture with { BeforeState = blockedState },
            service);
        CharacterCreationContactsInteractionPrepareResult blocked = presenter.Prepare(
            blockedOverview,
            new CharacterCreationContactEditInput(fixture.ContactId, Free: true));

        Assert.AreEqual(CharacterCreationContactOutcomes.Blocked, blocked.Outcome);
        Assert.IsNotNull(blocked.State);
        Assert.AreEqual(0, blocked.State.ContactBudget.Remaining);
        CollectionAssert.Contains(
            blocked.Blockers.ToArray(),
            CharacterCreationContactsBlockers.BudgetExceeded);
        Assert.AreEqual(0, service.PreviewCalls);

        var desktop = new CharacterCreationWizardDesktopSession();
        desktop.Bind(
            blockedOverview.CreationWizard
            ?? throw new AssertFailedException("Expected blocked creation wizard."),
            contacts: blockedOverview.CreationContacts);
        Assert.IsTrue(desktop.TrySelectStep(CharacterCreationWizardStepIds.ContactsLifestyles));
        CharacterCreationWizardDesktopContactsStep blockedContacts = desktop.State.ContactsStep
            ?? throw new AssertFailedException("Expected read-only blocked Contacts projection.");
        Assert.IsFalse(blockedContacts.CanEdit);
    }

    [TestMethod]
    public void Applied_and_replayed_confirm_require_plus_one_receipt_and_refresh_exact_authority()
    {
        Fixture fixture = CreateFixture();
        foreach (string successfulOutcome in new[]
                 {
                     CharacterCreationContactOutcomes.Applied,
                     CharacterCreationContactOutcomes.Replayed
                 })
        {
            var service = new FakeContactsService(fixture.BeforeState)
            {
                PreviewResult = Available(fixture.Preview),
                ConfirmResult = new CharacterCreationContactResult<CharacterCreationContactReceipt>(
                    successfulOutcome,
                    fixture.Receipt,
                    []),
                StateAfterConfirm = fixture.AfterState
            };
            CharacterOverviewState overview = CreateOverview(fixture, service);
            var presenter = new CharacterCreationContactsInteractionPresenter(service);
            CharacterCreationContactPreparedPreview prepared = RequirePrepared(presenter.Prepare(
                overview,
                new CharacterCreationContactEditInput(fixture.ContactId, Free: true)));

            CharacterCreationContactsInteractionConfirmResult result = presenter.Confirm(
                overview,
                new CharacterCreationContactConfirmation(
                    prepared,
                    prepared.PreviewDigest,
                    prepared.IdempotencyKey,
                    ExplicitlyConfirmed: true));

            Assert.AreEqual(successfulOutcome, result.Outcome);
            Assert.AreSame(fixture.Receipt, result.Receipt);
            Assert.IsNotNull(result.RefreshedState);
            Assert.AreEqual(8, result.RefreshedState.Binding.WorkspaceRevision);
            Assert.AreEqual(8, result.RefreshedState.Binding.ContentRevision);
            Assert.AreEqual(8, result.RefreshedState.Binding.SavedRevision);
            Assert.AreEqual(fixture.Preview.ContactAfter.ContactDigest,
                result.RefreshedState.Contacts.Single(contact =>
                    contact.ContactId == fixture.ContactId).ContactDigest);
            Assert.AreEqual(1, service.ConfirmCalls);
            Assert.IsNotNull(service.LastConfirmRequest);
            Assert.AreEqual(prepared.IdempotencyKey, service.LastConfirmRequest.IdempotencyKey);
            Assert.IsTrue(service.LastConfirmRequest.ExplicitlyConfirmed);
            Assert.AreSame(prepared.Binding, service.LastConfirmRequest.Binding);
            Assert.AreEqual(prepared.WritePlan.PlanDigest, result.Receipt!.WritePlan.PlanDigest);
        }
    }

    [TestMethod]
    public void Forged_receipt_is_exposed_but_never_accepted_as_refreshed_authority()
    {
        Fixture fixture = CreateFixture();
        CharacterCreationContactReceipt forged = fixture.Receipt with
        {
            ContentRevision = fixture.Receipt.ContentRevision + 1
        };
        var service = new FakeContactsService(fixture.BeforeState)
        {
            PreviewResult = Available(fixture.Preview),
            ConfirmResult = Applied(forged),
            StateAfterConfirm = fixture.AfterState
        };
        CharacterOverviewState overview = CreateOverview(fixture, service);
        var presenter = new CharacterCreationContactsInteractionPresenter(service);
        CharacterCreationContactPreparedPreview prepared = RequirePrepared(presenter.Prepare(
            overview,
            new CharacterCreationContactEditInput(fixture.ContactId, Free: true)));

        CharacterCreationContactsInteractionConfirmResult result = presenter.Confirm(
            overview,
            new CharacterCreationContactConfirmation(
                prepared,
                prepared.PreviewDigest,
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationContactOutcomes.Conflict, result.Outcome);
        Assert.AreSame(forged, result.Receipt);
        Assert.IsNull(result.RefreshedState);
        CollectionAssert.Contains(
            result.Blockers.ToArray(),
            CharacterCreationContactsInteractionBlockers.ReceiptMismatch);
    }

    [TestMethod]
    public void Sibling_drift_after_commit_is_rejected_during_authoritative_refresh()
    {
        Fixture fixture = CreateFixture();
        CharacterCreationContactProjection driftedSibling = fixture.AfterState.Contacts[1] with
        {
            ContactDigest = Digest('b')
        };
        CharacterCreationContactsState drifted = fixture.AfterState with
        {
            Contacts = [fixture.AfterState.Contacts[0], driftedSibling],
            SnapshotDigest = Digest('c')
        };
        var service = new FakeContactsService(fixture.BeforeState)
        {
            PreviewResult = Available(fixture.Preview),
            ConfirmResult = Applied(fixture.Receipt),
            StateAfterConfirm = drifted
        };
        CharacterOverviewState overview = CreateOverview(fixture, service);
        var presenter = new CharacterCreationContactsInteractionPresenter(service);
        CharacterCreationContactPreparedPreview prepared = RequirePrepared(presenter.Prepare(
            overview,
            new CharacterCreationContactEditInput(fixture.ContactId, Free: true)));

        CharacterCreationContactsInteractionConfirmResult result = presenter.Confirm(
            overview,
            new CharacterCreationContactConfirmation(
                prepared,
                prepared.PreviewDigest,
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: true));

        Assert.AreEqual(CharacterCreationContactOutcomes.Conflict, result.Outcome);
        Assert.AreSame(fixture.Receipt, result.Receipt);
        Assert.IsNull(result.RefreshedState);
        CollectionAssert.Contains(
            result.Blockers.ToArray(),
            CharacterCreationContactsInteractionBlockers.RefreshAuthorityRequired);
    }

    [TestMethod]
    public void Receipt_lookup_projects_core_ledger_truth_without_replaying_a_mutation()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeContactsService(fixture.AfterState)
        {
            LookupResult = Available(fixture.Receipt)
        };
        CharacterOverviewState overview = CreateOverview(fixture, service);
        var presenter = new CharacterCreationContactsInteractionPresenter(service);

        CharacterCreationContactsInteractionReceiptLookupResult result =
            presenter.LookupReceipt(overview, "contact-free-7");

        Assert.AreEqual(CharacterCreationContactOutcomes.Available, result.Outcome);
        Assert.AreSame(fixture.Receipt, result.Receipt);
        Assert.IsNotNull(result.CurrentState);
        Assert.AreEqual(fixture.AfterState.Binding.ContentDigest, result.CurrentState.Binding.ContentDigest);
        Assert.AreEqual(1, service.LookupCalls);
        Assert.AreEqual(0, service.ConfirmCalls);
        Assert.AreEqual("contact-free-7", service.LastLookupRequest?.IdempotencyKey);
    }

    [TestMethod]
    public void Wizard_and_desktop_step_expose_exact_contacts_but_never_complete_lifestyles()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeContactsService(fixture.BeforeState);
        CharacterOverviewState overview = CreateOverview(fixture, service);
        CharacterCreationWizardSnapshot wizard = overview.CreationWizard
            ?? throw new AssertFailedException("Expected unfinished creation wizard.");
        CharacterCreationWizardStageState step = wizard.Steps.Single(item =>
            item.StepId == CharacterCreationWizardStepIds.ContactsLifestyles);

        Assert.IsTrue(step.IsAvailable);
        Assert.IsFalse(step.IsComplete);
        Assert.AreEqual(CharacterCreationWizardStepStatuses.InProgress, step.Status);
        Assert.IsEmpty(step.LegalNextStepIds);
        CollectionAssert.Contains(
            step.Blockers.ToArray(),
            CharacterCreationWizardProjector.ContactCreateDeleteAuthorityUnavailable);
        CollectionAssert.Contains(
            step.Blockers.ToArray(),
            CharacterCreationWizardProjector.ContactPetsAuthorityUnavailable);
        CollectionAssert.Contains(
            step.Blockers.ToArray(),
            CharacterCreationWizardProjector.LifestylesAuthorityUnavailable);
        Assert.IsFalse(wizard.CompletionBlockers.Contains(
            CharacterCreationWizardProjector.ContactsAuthorityUnavailable,
            StringComparer.Ordinal));
        CharacterCreationBudgetState contactBudget = wizard.Budgets.Single(item =>
            item.BudgetId == CharacterCreationContactBudgetIds.Contacts);
        CharacterCreationBudgetState highPlacesBudget = wizard.Budgets.Single(item =>
            item.BudgetId == CharacterCreationContactBudgetIds.FriendsInHighPlaces);
        Assert.AreEqual(9m, contactBudget.Remaining);
        Assert.AreEqual(0m, highPlacesBudget.Total);
        Assert.IsTrue(contactBudget.IsExact);

        var desktop = new CharacterCreationWizardDesktopSession();
        desktop.Bind(wizard, contacts: overview.CreationContacts);
        Assert.IsTrue(desktop.TrySelectStep(CharacterCreationWizardStepIds.ContactsLifestyles));
        CharacterCreationWizardDesktopState state = desktop.State;
        Assert.IsNotNull(state.ContactsStep);
        Assert.HasCount(2, state.ContactsStep.Contacts);
        Assert.AreEqual(fixture.ContactId, state.ContactsStep.Contacts[0].ContactId);
        Assert.AreEqual(fixture.BeforeState.SnapshotDigest, state.ContactsStep.SnapshotDigest);
        Assert.AreSame(state.ContactsStep, state.BuildGhostContext.ContactsStep);
        Assert.IsFalse(state.AdvancedEditorUnlocked);
        Assert.IsFalse(state.CanFinalize);
    }

    [TestMethod]
    public void Desktop_session_drops_structurally_forged_contacts_even_with_sha_shaped_digests()
    {
        Fixture fixture = CreateFixture();
        var service = new FakeContactsService(fixture.BeforeState);
        CharacterOverviewState overview = CreateOverview(fixture, service);
        CharacterCreationWizardSnapshot wizard = overview.CreationWizard
            ?? throw new AssertFailedException("Expected unfinished creation wizard.");
        CharacterCreationContactProjection forgedContact = fixture.BeforeState.Contacts[0] with
        {
            Fields = fixture.BeforeState.Contacts[0].Fields
                .Where(field => field.FieldId != CharacterCreationContactFieldIds.Free)
                .ToArray(),
            ContactDigest = Digest('f')
        };
        CharacterCreationContactsState forged = fixture.BeforeState with
        {
            Contacts = [forgedContact, fixture.BeforeState.Contacts[1]],
            SnapshotDigest = Digest('e')
        };
        var desktop = new CharacterCreationWizardDesktopSession();

        desktop.Bind(wizard, contacts: forged);
        Assert.IsTrue(desktop.TrySelectStep(CharacterCreationWizardStepIds.ContactsLifestyles));

        Assert.IsNull(desktop.State.ContactsStep);
        Assert.IsNull(desktop.State.BuildGhostContext.ContactsStep);

        CharacterCreationContactProjection forgedAuthority = fixture.BeforeState.Contacts[0] with
        {
            Fields = fixture.BeforeState.Contacts[0].Fields.Select(field =>
                field.FieldId == CharacterCreationContactFieldIds.Free
                    ? field with { ValueKind = CharacterCreationContactValueKinds.Text }
                    : field).ToArray(),
            ContactDigest = Digest('d')
        };
        desktop.Bind(
            wizard,
            contacts: fixture.BeforeState with
            {
                Contacts = [forgedAuthority, fixture.BeforeState.Contacts[1]],
                SnapshotDigest = Digest('c')
            });
        Assert.IsTrue(desktop.TrySelectStep(CharacterCreationWizardStepIds.ContactsLifestyles));
        Assert.IsNull(desktop.State.ContactsStep);
        Assert.IsNull(desktop.State.BuildGhostContext.ContactsStep);
    }

    private static CharacterCreationContactPreparedPreview RequirePrepared(
        CharacterCreationContactsInteractionPrepareResult result)
        => result.PreparedPreview
           ?? throw new AssertFailedException("Expected a prepared contact preview.");

    private static CharacterOverviewState CreateOverview(Fixture fixture, FakeContactsService service)
    {
        CharacterWorkspaceId workspaceId = fixture.BeforeState.Binding.WorkspaceId;
        WorkspaceSessionState session = new(
            workspaceId,
            [new OpenWorkspaceState(
                workspaceId,
                "Wizard",
                "W",
                DateTimeOffset.Parse("2026-08-23T00:00:00+00:00"),
                RulesetDefaults.Sr5,
                fixture.BeforeState.Binding.ContentRevision,
                fixture.BeforeState.Binding.SavedRevision)],
            [workspaceId]);
        return new WorkspaceOverviewStateFactory(creationContactsService: service).CreateLoadedState(
            CharacterOverviewState.Empty,
            workspaceId,
            session,
            CreateLoadedOverview(
                fixture.Content,
                fixture.BeforeState.Binding.ContentRevision,
                fixture.BeforeState.Binding.SavedRevision),
            restoredView: null,
            hasSavedWorkspace: true);
    }

    private static WorkspaceOverviewLoadResult CreateLoadedOverview(
        string content,
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
            new WorkspaceDocument(content, RulesetDefaults.Sr5));

    private static Fixture CreateFixture()
    {
        const string content = "<character><created>False</created><contactpoints>12</contactpoints></character>";
        Guid contactId = Guid.Parse("45a9f119-7337-49fb-bd00-02964378f673");
        string contentDigest = Sha256(content);
        var binding = new CharacterCreationContactBinding(
            new CharacterWorkspaceId("ws-creation-contacts"),
            WorkspaceRevision: 7,
            ContentRevision: 7,
            SavedRevision: 0,
            ContentDigest: contentDigest,
            AuxiliaryStateDigest: RawDigest('a'),
            SourceDigest: Digest('b'),
            RulesDigest: Digest('c'),
            RuntimeDigest: Digest('d'));
        var identity = new CharacterCreationContactIdentity(
            "Fixer", "Broker", "Seattle", "Trusted", "", "Human", "F", "38",
            "Fixer", "Credstick", "Urban brawl", "Partner", "");
        CharacterCreationContactProjection before = Contact(
            contactId,
            identity,
            free: false,
            cost: 3,
            countsRegular: true,
            Digest('e'));
        CharacterCreationContactProjection after = Contact(
            contactId,
            identity,
            free: true,
            cost: 0,
            countsRegular: false,
            Digest('f'));
        Guid siblingId = Guid.Parse("45a9f119-7337-49fb-bd00-02964378f674");
        CharacterCreationContactProjection sibling = Contact(
            siblingId,
            identity with { Name = "Street Doc", Role = "Medic" },
            free: true,
            cost: 0,
            countsRegular: false,
            Digest('0'));
        CharacterCreationContactBudget contactBefore = Budget(
            CharacterCreationContactBudgetIds.Contacts,
            total: 12,
            used: 3);
        CharacterCreationContactBudget contactAfter = Budget(
            CharacterCreationContactBudgetIds.Contacts,
            total: 12,
            used: 0);
        CharacterCreationContactBudget highPlaces = Budget(
            CharacterCreationContactBudgetIds.FriendsInHighPlaces,
            total: 0,
            used: 0);
        var state = new CharacterCreationContactsState(
            CharacterCreationContactsSchemas.StateV1,
            CharacterCreationWizardStepIds.ContactsLifestyles,
            binding,
            CharacterCreated: false,
            Contacts: [before, sibling],
            ContactBudget: contactBefore,
            HighPlacesBudget: highPlaces,
            Blockers: [],
            CanEdit: true,
            SnapshotDigest: Digest('1'));
        string contentAfter = Digest('2');
        var operation = new CharacterCreationContactWriteOperation(
            1,
            CharacterCreationContactFieldIds.Free,
            "False",
            "True",
            CharacterCreationContactSourceAnchors.All);
        var plan = new CharacterCreationContactAtomicWritePlan(
            CharacterCreationContactsSchemas.WritePlanV1,
            CharacterCreationWizardStepIds.ContactsLifestyles,
            contactId,
            [operation],
            contentDigest,
            contentAfter,
            Digest('3'),
            Digest('3'),
            Digest('4'),
            Digest('4'),
            PreservesUntouchedSiblingState: true,
            PreservesNestedState: true,
            PlanDigest: Digest('5'));
        var preview = new CharacterCreationContactPreview(
            CharacterCreationContactsSchemas.PreviewV1,
            CharacterCreationWizardStepIds.ContactsLifestyles,
            binding,
            before,
            after,
            contactBefore,
            contactAfter,
            highPlaces,
            highPlaces,
            plan,
            Blockers: [],
            RequiresExplicitConfirmation: true,
            CanConfirm: true,
            PreviewDigest: Digest('6'));
        var receipt = new CharacterCreationContactReceipt(
            CharacterCreationContactsSchemas.ReceiptV1,
            "creation-contact-receipt",
            CharacterCreationWizardStepIds.ContactsLifestyles,
            binding.WorkspaceId,
            contactId,
            Digest('7'),
            Digest('8'),
            PreviousWorkspaceRevision: 7,
            WorkspaceRevision: 8,
            PreviousContentRevision: 7,
            ContentRevision: 8,
            PreviousSavedRevision: 0,
            SavedRevision: 8,
            ContentDigestBefore: contentDigest,
            ContentDigestAfter: contentAfter,
            SourceDigest: binding.SourceDigest,
            RulesDigest: binding.RulesDigest,
            RuntimeDigest: binding.RuntimeDigest,
            ContactPointsBefore: 3,
            ContactPointsAfter: 0,
            ContactPointsRemaining: 12,
            HighPlacesPointsBefore: 0,
            HighPlacesPointsAfter: 0,
            HighPlacesPointsRemaining: 0,
            WritePlan: plan,
            ReceiptDigest: Digest('9'));
        CharacterCreationContactBinding afterBinding = binding with
        {
            WorkspaceRevision = 8,
            ContentRevision = 8,
            SavedRevision = 8,
            ContentDigest = contentAfter,
            AuxiliaryStateDigest = RawDigest('0')
        };
        CharacterCreationContactsState afterState = state with
        {
            Binding = afterBinding,
            Contacts = [after, sibling],
            ContactBudget = contactAfter,
            SnapshotDigest = Digest('a')
        };
        return new Fixture(content, contactId, state, preview, receipt, afterState);
    }

    private static CharacterCreationContactProjection Contact(
        Guid id,
        CharacterCreationContactIdentity identity,
        bool free,
        int cost,
        bool countsRegular,
        string digest)
    {
        IReadOnlyList<CharacterCreationContactFieldAuthority> fields =
            CharacterCreationContactFieldIds.All.Select(fieldId => Field(fieldId, identity, free)).ToArray();
        return new CharacterCreationContactProjection(
            id,
            identity,
            Connection: 2,
            Loyalty: 1,
            IsGroup: false,
            Free: free,
            Family: false,
            Blackmail: false,
            ContactPointCost: cost,
            CountsAgainstContactBudget: countsRegular,
            CountsAgainstHighPlacesBudget: false,
            Fields: fields,
            SourceAnchorIds: CharacterCreationContactSourceAnchors.All,
            ContactDigest: digest);
    }

    private static CharacterCreationContactFieldAuthority Field(
        string fieldId,
        CharacterCreationContactIdentity identity,
        bool free)
    {
        bool isBoolean = fieldId is CharacterCreationContactFieldIds.Group
            or CharacterCreationContactFieldIds.Free
            or CharacterCreationContactFieldIds.Family
            or CharacterCreationContactFieldIds.Blackmail;
        bool isInteger = fieldId is CharacterCreationContactFieldIds.Connection
            or CharacterCreationContactFieldIds.Loyalty;
        string value = fieldId switch
        {
            CharacterCreationContactFieldIds.Name => identity.Name,
            CharacterCreationContactFieldIds.Role => identity.Role,
            CharacterCreationContactFieldIds.Location => identity.Location,
            CharacterCreationContactFieldIds.Notes => identity.Notes,
            CharacterCreationContactFieldIds.CustomName => identity.CustomName,
            CharacterCreationContactFieldIds.Metatype => identity.Metatype,
            CharacterCreationContactFieldIds.Gender => identity.Gender,
            CharacterCreationContactFieldIds.Age => identity.Age,
            CharacterCreationContactFieldIds.ContactType => identity.ContactType,
            CharacterCreationContactFieldIds.PreferredPayment => identity.PreferredPayment,
            CharacterCreationContactFieldIds.HobbiesVice => identity.HobbiesVice,
            CharacterCreationContactFieldIds.PersonalLife => identity.PersonalLife,
            CharacterCreationContactFieldIds.GroupName => identity.GroupName,
            CharacterCreationContactFieldIds.Connection => "2",
            CharacterCreationContactFieldIds.Loyalty => "1",
            CharacterCreationContactFieldIds.Free => free ? "True" : "False",
            _ when isBoolean => "False",
            _ => string.Empty
        };
        IReadOnlyList<CharacterCreationContactOption> options = isBoolean
            ? [Option(false), Option(true)]
            : isInteger
                ? [Option(1), Option(2), Option(3), Option(4), Option(5), Option(6)]
                : [];
        return new CharacterCreationContactFieldAuthority(
            fieldId,
            fieldId,
            isBoolean
                ? CharacterCreationContactValueKinds.Boolean
                : isInteger
                    ? CharacterCreationContactValueKinds.Integer
                    : CharacterCreationContactValueKinds.Text,
            IsEditable: true,
            SerializedValue: value,
            Minimum: isInteger ? 1 : isBoolean ? null : 0,
            Maximum: isInteger ? 6 : isBoolean ? null : 32_767,
            LegalOptions: options,
            Blockers: [],
            SourceAnchorIds: CharacterCreationContactSourceAnchors.All);
    }

    private static CharacterCreationContactOption Option(bool value)
        => new(
            value ? "true" : "false",
            value ? "Yes" : "No",
            value.ToString(),
            IsEnabled: true,
            Blockers: [],
            SourceAnchorIds: CharacterCreationContactSourceAnchors.All);

    private static CharacterCreationContactOption Option(int value)
        => new(
            value.ToString(),
            value.ToString(),
            value.ToString(),
            IsEnabled: true,
            Blockers: [],
            SourceAnchorIds: CharacterCreationContactSourceAnchors.All);

    private static CharacterCreationContactBudget Budget(string id, int total, int used)
        => new(
            id,
            total,
            used,
            total - used,
            Math.Max(0, used - total),
            IsExact: true,
            Blockers: [],
            SourceAnchorIds: CharacterCreationContactSourceAnchors.All);

    private static CharacterCreationContactResult<T> Available<T>(T value)
        where T : class
        => new(CharacterCreationContactOutcomes.Available, value, []);

    private static CharacterCreationContactResult<CharacterCreationContactReceipt> Applied(
        CharacterCreationContactReceipt value)
        => new(CharacterCreationContactOutcomes.Applied, value, []);

    private static string Sha256(string value)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";

    private static string Digest(char value) => "sha256:" + new string(value, 64);

    private static string RawDigest(char value) => new(value, 64);

    private sealed record Fixture(
        string Content,
        Guid ContactId,
        CharacterCreationContactsState BeforeState,
        CharacterCreationContactPreview Preview,
        CharacterCreationContactReceipt Receipt,
        CharacterCreationContactsState AfterState);

    private sealed class FakeContactsService : ICharacterCreationContactsService
    {
        public FakeContactsService(CharacterCreationContactsState state)
        {
            CurrentState = state;
        }

        public CharacterCreationContactsState CurrentState { get; set; }
        public CharacterCreationContactsState? StateAfterConfirm { get; init; }
        public CharacterCreationContactResult<CharacterCreationContactPreview>? PreviewResult { get; init; }
        public CharacterCreationContactResult<CharacterCreationContactReceipt>? ConfirmResult { get; init; }
        public CharacterCreationContactResult<CharacterCreationContactReceipt>? LookupResult { get; init; }
        public int LoadCalls { get; private set; }
        public int PreviewCalls { get; private set; }
        public int ConfirmCalls { get; private set; }
        public int LookupCalls { get; private set; }
        public CharacterCreationContactPreviewRequest? LastPreviewRequest { get; private set; }
        public CharacterCreationContactConfirmRequest? LastConfirmRequest { get; private set; }
        public CharacterCreationContactReceiptLookupRequest? LastLookupRequest { get; private set; }

        public CharacterCreationContactResult<CharacterCreationContactsState> Load(
            CharacterCreationContactsLoadRequest request)
        {
            LoadCalls++;
            return new CharacterCreationContactResult<CharacterCreationContactsState>(
                CharacterCreationContactOutcomes.Available,
                CurrentState,
                CurrentState.Blockers);
        }

        public CharacterCreationContactResult<CharacterCreationContactPreview> Preview(
            CharacterCreationContactPreviewRequest request)
        {
            PreviewCalls++;
            LastPreviewRequest = request;
            return PreviewResult ?? new CharacterCreationContactResult<CharacterCreationContactPreview>(
                CharacterCreationContactOutcomes.Unavailable,
                null,
                [CharacterCreationContactsBlockers.AuthorityUnavailable]);
        }

        public CharacterCreationContactResult<CharacterCreationContactReceipt> Confirm(
            CharacterCreationContactConfirmRequest request)
        {
            ConfirmCalls++;
            LastConfirmRequest = request;
            CharacterCreationContactResult<CharacterCreationContactReceipt> result = ConfirmResult
                ?? new CharacterCreationContactResult<CharacterCreationContactReceipt>(
                    CharacterCreationContactOutcomes.Unavailable,
                    null,
                    [CharacterCreationContactsBlockers.AuthorityUnavailable]);
            if (result.Success && StateAfterConfirm is not null)
                CurrentState = StateAfterConfirm;
            return result;
        }

        public CharacterCreationContactResult<CharacterCreationContactReceipt> LookupReceipt(
            CharacterCreationContactReceiptLookupRequest request)
        {
            LookupCalls++;
            LastLookupRequest = request;
            return LookupResult ?? new CharacterCreationContactResult<CharacterCreationContactReceipt>(
                CharacterCreationContactOutcomes.NotFound,
                null,
                []);
        }
    }
}
