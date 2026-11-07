using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class CharacterCreationWizardDesktopSessionTests
{
    [TestMethod]
    public void Session_consumes_typed_route_budget_and_legal_option_authority_without_synthesis()
    {
        CharacterCreationWizardSnapshot snapshot = CreateSnapshot();
        CharacterCreationWizardDesktopSession session = new();

        CharacterCreationWizardDesktopState state = session.Bind(snapshot);

        Assert.AreEqual(CharacterCreationWizardStepIds.Foundation, state.ActiveStepId);
        CollectionAssert.AreEqual(
            snapshot.Steps.Select(static step => step.StepId).ToArray(),
            state.Steps.Select(static step => step.StepId).ToArray());
        CharacterCreationWizardDesktopBudget budget = AssertExactlyOne(state.Budgets);
        Assert.AreEqual(30m, budget.Total);
        Assert.AreEqual(12m, budget.Used);
        Assert.AreEqual(18m, budget.Remaining);
        Assert.IsTrue(budget.IsExact);

        CharacterCreationWizardDesktopOption disabled = state.LegalOptions.Single(option =>
            string.Equals(option.OptionId, "elf", StringComparison.Ordinal));
        Assert.IsFalse(disabled.IsEnabled);
        Assert.AreEqual("requires-source-authority", disabled.DisableReasonKey);
        Assert.AreEqual("RF", disabled.SourceId);
        Assert.AreEqual(66, disabled.SourcePage);
        Assert.AreEqual(10m, AssertExactlyOne(disabled.Costs).Delta);
        CollectionAssert.Contains(disabled.SourceAnchorIds.ToArray(), "metatypes.xml#elf");

        Assert.IsTrue(session.TryContinue());
        Assert.AreEqual(CharacterCreationWizardStepIds.LifeModules, session.State.ActiveStepId);
        Assert.IsFalse(session.TrySelectStep(CharacterCreationWizardStepIds.Attributes));
        Assert.AreEqual(CharacterCreationWizardStepIds.LifeModules, session.State.ActiveStepId);
    }

    [TestMethod]
    public void Exact_checkpoint_resumes_but_revision_snapshot_and_step_changes_invalidate_to_authority()
    {
        CharacterCreationWizardSnapshot snapshot = CreateSnapshot();
        CharacterCreationWizardDesktopSession session = new();
        session.Bind(snapshot);
        Assert.IsTrue(session.TryContinue());
        CharacterCreationWizardDesktopCheckpoint checkpoint = session.CreateCheckpoint();

        CharacterCreationWizardDesktopSession exact = new();
        CharacterCreationWizardDesktopState resumed = exact.Bind(snapshot, checkpoint);
        Assert.IsTrue(resumed.Resume.Restored);
        Assert.AreEqual(CharacterCreationWizardStepIds.LifeModules, resumed.ActiveStepId);

        CharacterCreationWizardDesktopState revisionChanged = new CharacterCreationWizardDesktopSession().Bind(
            snapshot with { WorkspaceRevision = snapshot.WorkspaceRevision + 1 },
            checkpoint);
        Assert.IsFalse(revisionChanged.Resume.Restored);
        Assert.AreEqual(
            CharacterCreationWizardCheckpointInvalidationReasons.WorkspaceRevisionChanged,
            revisionChanged.Resume.InvalidationReason);
        Assert.AreEqual(CharacterCreationWizardStepIds.Foundation, revisionChanged.ActiveStepId);

        CharacterCreationWizardDesktopState digestChanged = new CharacterCreationWizardDesktopSession().Bind(
            snapshot with { SnapshotDigest = "sha256:" + new string('b', 64) },
            checkpoint);
        Assert.AreEqual(
            CharacterCreationWizardCheckpointInvalidationReasons.SnapshotChanged,
            digestChanged.Resume.InvalidationReason);

        CharacterCreationWizardDesktopCheckpoint blockedStep = checkpoint with
        {
            SelectedStepId = CharacterCreationWizardStepIds.Attributes
        };
        CharacterCreationWizardDesktopState unavailable = new CharacterCreationWizardDesktopSession().Bind(
            snapshot,
            blockedStep);
        Assert.AreEqual(
            CharacterCreationWizardCheckpointInvalidationReasons.StepUnavailable,
            unavailable.Resume.InvalidationReason);
    }

    [TestMethod]
    public void Checkpoint_codec_recovers_fail_closed_and_build_ghost_context_is_revision_bound_read_only()
    {
        CharacterCreationWizardDesktopSession session = new();
        CharacterCreationWizardDesktopState state = session.Bind(CreateSnapshot());
        CharacterCreationWizardDesktopCheckpoint checkpoint = session.CreateCheckpoint();

        byte[] payload = CharacterCreationWizardDesktopSession.SerializeCheckpoint(checkpoint);
        Assert.IsTrue(CharacterCreationWizardDesktopSession.TryDeserializeCheckpoint(payload, out CharacterCreationWizardDesktopCheckpoint? roundTrip));
        Assert.AreEqual(checkpoint, roundTrip);
        Assert.IsFalse(CharacterCreationWizardDesktopSession.TryDeserializeCheckpoint("{}"u8, out _));
        Assert.IsFalse(CharacterCreationWizardDesktopSession.TryDeserializeCheckpoint("not-json"u8, out _));

        CharacterCreationWizardBuildGhostContext context = state.BuildGhostContext;
        Assert.AreEqual(state.WorkspaceId, context.WorkspaceId);
        Assert.AreEqual(state.WorkspaceRevision, context.WorkspaceRevision);
        Assert.AreEqual(state.SnapshotDigest, context.WizardSnapshotDigest);
        Assert.AreEqual(state.ActiveStepId, context.ActiveStepId);
        Assert.IsFalse(state.AdvancedEditorUnlocked);
        Assert.IsFalse(state.CanFinalize);
        Assert.IsTrue(state.BuildGhostAvailable);
        Assert.IsTrue(CharacterCreationWizardBuildGhostPolicy.CanSend(state, aiPreferenceEnabled: true));
        Assert.IsFalse(CharacterCreationWizardBuildGhostPolicy.CanSend(state, aiPreferenceEnabled: false));
        Assert.IsFalse(typeof(CharacterCreationWizardBuildGhostContext).GetProperties().Any(property =>
            property.Name.Contains("Command", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Mutation", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Payload", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Confirm", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Build_ghost_fails_closed_without_runtime_or_context_authority()
    {
        CharacterCreationWizardSnapshot canonicalUnavailable = CreateSnapshot() with
        {
            RuntimeFingerprint = string.Empty,
            CompletionBlockers =
            [
                CharacterCreationWizardProjector.RuntimeAuthorityUnavailable,
                CharacterCreationWizardProjector.BuildGhostContextUnavailable
            ]
        };
        CharacterCreationWizardDesktopState unavailable =
            new CharacterCreationWizardDesktopSession().Bind(canonicalUnavailable);
        Assert.IsFalse(unavailable.BuildGhostAvailable);
        Assert.IsFalse(CharacterCreationWizardBuildGhostPolicy.CanSend(unavailable, aiPreferenceEnabled: true));

        CharacterCreationWizardDesktopState missingRuntime =
            new CharacterCreationWizardDesktopSession().Bind(
                CreateSnapshot() with { RuntimeFingerprint = string.Empty });
        Assert.IsFalse(missingRuntime.BuildGhostAvailable);

        CharacterCreationWizardDesktopState blockedContext =
            new CharacterCreationWizardDesktopSession().Bind(CreateSnapshot() with
            {
                CompletionBlockers = [CharacterCreationWizardProjector.BuildGhostContextUnavailable]
            });
        Assert.IsFalse(blockedContext.BuildGhostAvailable);
    }

    [TestMethod]
    public void Session_rejects_created_character_snapshot_because_completed_characters_use_advanced_editor()
    {
        CharacterCreationWizardSnapshot created = CreateSnapshot() with { CharacterCreated = true };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new CharacterCreationWizardDesktopSession().Bind(created));
    }

    [TestMethod]
    public void Journey_projects_current_stage_versions_and_inputs_instead_of_nationality()
    {
        var snapshot = CreateSnapshot();
        var journey = Journey(snapshot, 2);
        var session = new CharacterCreationWizardDesktopSession();
        session.Bind(snapshot, journey: journey);
        Assert.IsTrue(session.TryContinue());
        Assert.AreEqual(2, session.State.JourneyStep!.CurrentStageOrder);
        var option = AssertExactlyOne(session.State.LegalOptions);
        Assert.AreEqual("module-2", option.OptionId);
        Assert.AreEqual("version-a", option.VersionId);
        Assert.AreEqual("skill-choice", AssertExactlyOne(option.FollowUps).PromptId);
        Assert.AreEqual(7m, AssertExactlyOne(option.Costs).Delta);
        foreach (int stage in new[] { 3, 4, 5, 5 })
        {
            journey = Journey(snapshot, stage);
            session.Bind(snapshot, journey: journey);
            Assert.IsTrue(session.TryContinue());
            Assert.AreEqual(stage, session.State.JourneyStep!.CurrentStageOrder);
            Assert.AreEqual("module-" + stage, AssertExactlyOne(session.State.LegalOptions).OptionId);
        }
        var finished = Journey(snapshot, 5) with { SelectionFinished = true, CanFinishSelection = false, Options = [] };
        session.Bind(snapshot, journey: finished);
        Assert.IsTrue(session.TryContinue());
        Assert.IsTrue(session.State.JourneyStep!.SelectionFinished);
        Assert.AreEqual(0, session.State.LegalOptions.Count);
    }

    [TestMethod]
    public void Journey_rejects_stale_workspace_source_and_revision_and_keeps_nationality_on_foundation()
    {
        var snapshot = CreateSnapshot();
        var journey = Journey(snapshot, 2);
        foreach (var stale in new[] {
            journey with { Binding = journey.Binding with { ContentRevision = 8 } },
            journey with { Binding = journey.Binding with { SourceDigest = "different-source" } },
            journey with { Binding = journey.Binding with { WorkspaceId = new("other-runner") } } })
        {
            var session = new CharacterCreationWizardDesktopSession();
            session.Bind(snapshot, journey: stale);
            session.TryContinue();
            Assert.IsNull(session.State.JourneyStep);
            Assert.AreEqual(0, session.State.LegalOptions.Count);
        }
        var nationality = new CharacterCreationWizardDesktopSession();
        nationality.Bind(snapshot, journey: journey with { CurrentStageOrder = 1 });
        nationality.TryContinue();
        Assert.IsNull(nationality.State.JourneyStep);
    }

    internal static CharacterCreationLifeModuleJourneyInteractionState Journey(CharacterCreationWizardSnapshot snapshot, int stage)
    {
        var followUp = new LifeModuleFollowUpPromptDto("skill-choice", "Choose a skill", "choice", true,
            [new("skill-a", "Skill A", true, null, new Dictionary<string, string>(), "pistols")], [], "effect", "skill");
        var version = new LifeModuleVersionProjectionDto("version-a", "Version A", true, [], [], [followUp], [],
            "Story", 7m, "7", true, "RF", 10, "10", []);
        var option = new LifeModuleLegalOptionDto("module-" + stage, stage, "Module", 7m, "RF", 10, "Story", true,
            [], [version], [], [], [], "stage-" + stage, stage == 5, "7", true, "10", []);
        return new(new(new(snapshot.WorkspaceId), snapshot.WorkspaceRevision, snapshot.WorkspaceRevision,
            snapshot.ContentDigest, "xml-v1", snapshot.SourceDigest, "sources-v1", false, []),
            1, "sha256:" + new string('e', 64), stage, "stage-" + stage, [option], snapshot.Budgets[0], [], false, stage == 5, []);
    }

    internal static CharacterCreationWizardSnapshot CreateSnapshot()
    {
        CharacterCreationWizardStageState foundation = Stage(
            CharacterCreationWizardStepIds.Foundation,
            "Foundation",
            CharacterCreationWizardStepStatuses.InProgress,
            available: true,
            complete: false,
            next: [CharacterCreationWizardStepIds.LifeModules]);
        CharacterCreationWizardStageState lifeModules = Stage(
            CharacterCreationWizardStepIds.LifeModules,
            "Life modules",
            CharacterCreationWizardStepStatuses.Available,
            available: true,
            complete: false,
            next: []);
        CharacterCreationWizardStageState attributes = Stage(
            CharacterCreationWizardStepIds.Attributes,
            "Attributes",
            CharacterCreationWizardStepStatuses.Blocked,
            available: false,
            complete: false,
            next: []);
        CharacterCreationBudgetState budget = new(
            BudgetId: CharacterCreationBudgetIds.LifeModules,
            Label: "Life-module karma",
            Total: 30m,
            Used: 12m,
            Remaining: 18m,
            IsExact: true,
            Blockers: [],
            Unit: "karma");
        CharacterCreationLegalOption human = new(
            OptionId: "human",
            Label: "Human",
            IsEnabled: true,
            DisableReasonKey: null,
            DisableReasonArguments: new Dictionary<string, string>(),
            Costs: [],
            Consequences: [],
            SourceAnchorIds: ["metatypes.xml#human"],
            SourceId: "SR5",
            SourcePage: 65);
        CharacterCreationLegalOption elf = new(
            OptionId: "elf",
            Label: "Elf",
            IsEnabled: false,
            DisableReasonKey: "requires-source-authority",
            DisableReasonArguments: new Dictionary<string, string> { ["source"] = "RF" },
            Costs: [new CharacterCreationChoiceCost(CharacterCreationBudgetIds.LifeModules, 10m, "karma")],
            Consequences: [],
            SourceAnchorIds: ["metatypes.xml#elf"],
            SourceId: "RF",
            SourcePage: 66);

        return new CharacterCreationWizardSnapshot(
            Schema: CharacterCreationWizardSchemas.SnapshotV1,
            WorkspaceId: "workspace-creation",
            WorkspaceRevision: 9,
            ContentDigest: "sha256:" + new string('c', 64),
            SourceDigest: "sha256:" + new string('d', 64),
            RulesetId: "sr5",
            RuntimeFingerprint: "runtime-exact",
            BuildMethod: CharacterCreationBuildMethods.LifeModules,
            CharacterCreated: false,
            ActiveStepId: CharacterCreationWizardStepIds.Foundation,
            Steps: [foundation, lifeModules, attributes],
            Budgets: [budget],
            LegalOptionsByStep: new Dictionary<string, IReadOnlyList<CharacterCreationLegalOption>>(StringComparer.Ordinal)
            {
                [CharacterCreationWizardStepIds.Foundation] = [human, elf],
                [CharacterCreationWizardStepIds.LifeModules] = [],
                [CharacterCreationWizardStepIds.Attributes] = []
            },
            CompletionBlockers: ["creation-wizard-finalization-authority-unavailable"],
            Warnings: [],
            CanFinalize: false,
            SnapshotDigest: "sha256:" + new string('a', 64));
    }

    private static CharacterCreationWizardStageState Stage(
        string id,
        string label,
        string status,
        bool available,
        bool complete,
        IReadOnlyList<string> next)
        => new(
            StepId: id,
            Label: label,
            Status: status,
            IsRequired: true,
            IsAvailable: available,
            IsComplete: complete,
            BudgetIds: [],
            Blockers: available ? [] : ["authority-unavailable"],
            Warnings: [],
            LegalNextStepIds: next);

    private static T AssertExactlyOne<T>(IEnumerable<T> values)
    {
        T[] materialized = values.ToArray();
        Assert.HasCount(1, materialized);
        return materialized[0];
    }
}
