using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Files;
using Chummer.Infrastructure.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;

namespace Chummer.Tests.Presentation;

public static class CoreCreationProjectionScenario
{
    public static async Task<int> RunAsync(string coreContentRoot, Action<string> report)
    {
        ArgumentNullException.ThrowIfNull(report);
        // The implementation under test consumes real Core packages. The separately
        // supplied, pinned content checkout is data only; never an ambient source or
        // compile fallback. Missing input is a failure, not a skipped integration test.
        if (string.IsNullOrWhiteSpace(coreContentRoot) || !Path.IsPathFullyQualified(coreContentRoot)
            || !File.Exists(Path.Combine(coreContentRoot, "Chummer", "data", "settings.xml")))
            throw new ArgumentException("Supply one explicit absolute Core content root.");

        int passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            report("PASS " + label);
            passed++;
        }

        string stateDirectory = Path.Combine(Path.GetTempPath(), "chummer-ui-core-projection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stateDirectory);
        try
        {
            var resolver = new FileSystemCharacterSourceDataResolver(
                new FileSystemContentOverlayCatalogService(
                    Path.Combine(coreContentRoot, "Chummer"), Path.Combine(coreContentRoot, "Chummer"), null));
            var queries = new XmlCharacterFileQueries(new CharacterFileService());
            var sections = new XmlCharacterSectionQueries(new CharacterSectionService(resolver));
            var store = new FileWorkspaceStore(stateDirectory);
            var codec = new Sr5WorkspaceCodec(queries, sections, new XmlCharacterMetadataCommands(new CharacterFileService()));
            var activationProjector = new CharacterCreationBootstrapActivationProjector(store, queries,
                new XmlLifeModulesCatalogService(Path.Combine(coreContentRoot, "Chummer", "data", "lifemodules.xml")),
                new UnavailableCharacterCreationFoundationApplyAuthority());
            var bootstrap = new CharacterCreationBootstrapService(store, new RulesetWorkspaceCodecResolver([codec]),
                queries, resolver, activationProjector);
            Check(CharacterCreationBootstrapProfiles.TryResolveCanonicalSettingsProfileId(CharacterCreationBuildMethods.Priority,
                out string settingsId), "canonical Priority settings resolve");
            var created = bootstrap.CreateActivation(new(CharacterCreationBootstrapSchemas.RequestV1,
                CharacterCreationBootstrapStages.AwaitingFoundationSelection, RulesetDefaults.Sr5,
                "Projection Runner", "Projection", CharacterCreationBuildMethods.Priority, settingsId));
            Check(created.Outcome == CharacterCreationBootstrapOutcomes.Success && created.Receipt is not null,
                "real Bootstrap creates fresh runner: " + string.Join(",", created.Blockers));
            Check(created.Bundle is not null, "real Bootstrap produces its source-bound initial activation bundle");
            CharacterWorkspaceId id = created.Receipt!.WorkspaceId;
            var prerequisites = new CharacterCreationPrerequisiteService(store, queries, resolver);
            var attributes = new CharacterCreationAttributesService(store, resolver);
            var skills = new CharacterCreationSkillsService(store, resolver);
            var qualities = new CharacterCreationQualitiesService(store, resolver, prerequisites, attributes);
            var magic = new CharacterCreationMagicResonanceService(store, resolver);
            var resources = new CharacterCreationResourcesService(store, resolver);
            var gear = new CharacterCreationGearService(store, resolver);
            var contacts = new CharacterCreationContactsService(store);
            var lifestyles = new CharacterCreationLifestylesService(store, resolver);
            var finalizer = new CharacterCreationFinalizationService(store, queries, prerequisites, attributes,
                skills, qualities, magic, resources, gear);
            var factory = new WorkspaceOverviewStateFactory(null, contacts, qualities, magic, lifestyles, finalizer);
            var activation = created.Bundle!;
            var activationService = new ObservedActivationService(bootstrap);
            var initialOverview = activation.WorkspaceProjection.Overview;
            var initialDocument = activation.WorkspaceProjection.Workspace;
            var initialLoader = new CapturedOverviewLoader(new(initialOverview.Profile, initialOverview.Progress,
                initialOverview.Skills, initialOverview.Rules, initialOverview.Build, initialOverview.Movement,
                initialOverview.Awakening, initialDocument.ContentRevision, initialDocument.SavedRevision, initialDocument.Document));
            var initialSessions = new WorkspaceSessionPresenter();
            using (var lifecycle = new WorkspaceOverviewLifecycleCoordinator(
                System.Reflection.DispatchProxy.Create<IChummerClient, UnusedProjectionClient>(),
                initialSessions, initialLoader, new WorkspaceViewStateStore(), new WorkspaceShellStateFactory(),
                new WorkspaceRemoteCloseService(), new WorkspaceSessionActivationService(), factory))
            {
                bool canceled = false;
                try
                {
                    await lifecycle.ActivateCreatedAsync(CharacterOverviewState.Empty, activation, activationService,
                        new CancellationToken(canceled: true));
                }
                catch (OperationCanceledException) { canceled = true; }
                Check(canceled && activationService.ValidationCount == 0 && initialSessions.State.ActiveWorkspaceId is null,
                    "pre-canceled activation does not consume Core's one-shot bundle or open a workspace");

                Task<WorkspaceOverviewLifecycleResult> activationTask;
                SynchronizationContext? previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                try
                {
                    activationTask = lifecycle.ActivateCreatedAsync(CharacterOverviewState.Empty, activation, activationService,
                        CancellationToken.None);
                }
                finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
                var activated = await activationTask;
                Check(activated.CanPublish && activated.State.WorkspaceId == id
                    && activated.State.CreationWizard is not null && !activated.State.CreationWizard.CanFinalize,
                    "real initial activation opens the unfinished wizard without inventing completion");
                Check(activationService.ValidationCount == 1 && activationService.ValidationContext is null,
                    "real Core activation validation does not run on the calling UI synchronization context");
                Check(initialLoader.LoadCount == 0 && ReferenceEquals(activated.RecoveryDocument, initialDocument.Document)
                    && initialSessions.State.ActiveWorkspace?.ContentRevision == initialDocument.ContentRevision,
                    "initial bundle avoids generic overview rereads and preserves document/revision bindings");
            }

            // Cancel immediately after the real validator accepts its one-shot bundle.
            // This is a scheduling observation around Core, not a fabricated response.
            var canceledAttempt = bootstrap.CreateActivation(new(CharacterCreationBootstrapSchemas.RequestV1,
                CharacterCreationBootstrapStages.AwaitingFoundationSelection, RulesetDefaults.Sr5,
                "Canceled Activation", "Canceled", CharacterCreationBuildMethods.Priority, settingsId));
            Check(canceledAttempt.Bundle is not null, "cancellation fixture is a real atomic Core creation");
            var canceledBundle = canceledAttempt.Bundle!;
            var canceledOverview = canceledBundle.WorkspaceProjection.Overview;
            var canceledDocument = canceledBundle.WorkspaceProjection.Workspace;
            var canceledLoader = new CapturedOverviewLoader(new(canceledOverview.Profile, canceledOverview.Progress,
                canceledOverview.Skills, canceledOverview.Rules, canceledOverview.Build, canceledOverview.Movement,
                canceledOverview.Awakening, canceledDocument.ContentRevision, canceledDocument.SavedRevision, canceledDocument.Document));
            var countedFinalizer = new ObservedFinalizationService(finalizer);
            using (var cancellation = new CancellationTokenSource())
            using (var lifecycle = new WorkspaceOverviewLifecycleCoordinator(
                System.Reflection.DispatchProxy.Create<IChummerClient, UnusedProjectionClient>(),
                new WorkspaceSessionPresenter(), canceledLoader, new WorkspaceViewStateStore(), new WorkspaceShellStateFactory(),
                new WorkspaceRemoteCloseService(), new WorkspaceSessionActivationService(),
                new WorkspaceOverviewStateFactory(null, contacts, qualities, magic, lifestyles, countedFinalizer)))
            {
                var cancelingService = new ObservedActivationService(bootstrap)
                    { AfterAcceptedValidation = cancellation.Cancel };
                bool canceled = false;
                try
                {
                    await lifecycle.ActivateCreatedAsync(CharacterOverviewState.Empty, canceledBundle, cancelingService,
                        cancellation.Token);
                }
                catch (OperationCanceledException) { canceled = true; }
                Check(canceled && lifecycle.CurrentWorkspaceId is null && countedFinalizer.LoadCount == 0,
                    "cancellation after validation stops before additional Core domain reads or activation");
                Check(store.Get(canceledDocument.Id).Value!.Document.AuxiliaryStateDigest
                        == canceledDocument.Document.AuxiliaryStateDigest,
                    "canceling activation does not undo or rewrite an already committed character");
                int workspacesBeforeRecovery = store.List().Count;
                var recovered = await lifecycle.ActivateCreatedAsync(CharacterOverviewState.Empty, canceledBundle,
                    cancelingService, CancellationToken.None);
                Check(recovered.CanPublish && canceledLoader.LoadCount == 1 && countedFinalizer.LoadCount == 1
                    && recovered.State.WorkspaceId == canceledDocument.Id,
                    "post-validation cancellation recovers through a current read of the already-created character");
                Check(store.List().Count == workspacesBeforeRecovery,
                    "post-cancellation recovery does not create another character");
            }

            var ranks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CharacterCreationPriorityCategoryIds.Heritage] = "A",
                [CharacterCreationPriorityCategoryIds.Talent] = "E",
                [CharacterCreationPriorityCategoryIds.Attributes] = "B",
                [CharacterCreationPriorityCategoryIds.Skills] = "C",
                [CharacterCreationPriorityCategoryIds.Resources] = "D"
            };
            var prerequisiteState = prerequisites.Load(new(id)).Value!;
            var heritage = prerequisiteState.Authority.Options.Single(option =>
                option.CategoryId == CharacterCreationPriorityCategoryIds.Heritage && option.Rank == "A")
                .HeritageOptions.First(option => option.IsEnabled && option.MetavariantSourceId is null && option.MetatypeName == "Human");
            var talent = prerequisiteState.Authority.Options.Single(option =>
                option.CategoryId == CharacterCreationPriorityCategoryIds.Talent && option.Rank == "E")
                .TalentOptions.First(option => option.IsEnabled && string.Equals(option.Value,
                    CharacterCreationMagicResonanceKinds.Mundane, StringComparison.OrdinalIgnoreCase)
                    && option.Magic is null && option.Resonance is null && option.Depth is null
                    && option.ActiveSkillGrant is null && option.SkillGroupGrant is null);
            var priorityPreview = prerequisites.Preview(new(prerequisiteState.Binding, ranks)
                { HeritageSelectionId = heritage.SelectionId, TalentSelectionId = talent.SelectionId }).Value!;
            Check(prerequisites.Confirm(new(priorityPreview.Binding, ranks, priorityPreview.PreviewDigest, ExplicitlyConfirmed: true)
                { HeritageSelectionId = heritage.SelectionId, TalentSelectionId = talent.SelectionId }).Outcome
                == CharacterCreationFoundationOutcomes.Success, "real Priority choices confirmed");
            var attributePreview = attributes.Preview(new(attributes.Load(new(id)).Value!.Binding, [])).Value!;
            Check(attributes.Confirm(new(attributePreview.Binding, [], attributePreview.PreviewDigest, ExplicitlyConfirmed: true)).Outcome
                == CharacterCreationFoundationOutcomes.Success, "real base-attribute choice confirmed");
            var skillState = skills.Load(new(id)).Value!;
            var language = skillState.Authority.KnowledgeSkills.First(option => option.CanBeNativeLanguage);
            CharacterCreationSkillAllocation[] allocations = [new(language.SourceSkillId, CharacterCreationSkillKinds.Knowledge, null, null, true)];
            var skillPreview = skills.Preview(new(skillState.Binding, allocations, [])).Value!;
            Check(skills.Confirm(new(skillPreview.Binding, allocations, [], skillPreview.PreviewDigest,
                "projection-skills", ExplicitlyConfirmed: true)).Outcome == CharacterCreationFoundationOutcomes.Success,
                "real native-language choice confirmed");
            var qualityPreview = qualities.Preview(new(qualities.Load(new(id)).Value!.Binding, [])).Value!;
            Check(qualities.Confirm(new(qualityPreview.Binding, [], qualityPreview.PreviewDigest,
                "projection-qualities", Guid.NewGuid(), ExplicitlyConfirmed: true)).Outcome == CharacterCreationFoundationOutcomes.Success,
                "explicit empty qualities confirmed");
            var resourceState = resources.Load(new(id)).Value!;
            string resourceOption = resourceState.Options.First(option => option.IsEnabled && option.KarmaInvestment == 0).OptionId;
            var resourcePreview = resources.Preview(new(resourceState.Binding, resourceOption)).Value!;
            Check(resources.Confirm(new(resourcePreview.Binding, resourceOption, resourcePreview.PreviewDigest,
                "projection-resources", ExplicitlyConfirmed: true)).Outcome == CharacterCreationResourcesOutcomes.Applied,
                "real resources allocation confirmed");
            var gearPreview = gear.Preview(new(gear.Load(new(id)).Value!.Binding, [])).Value!;
            Check(gear.Confirm(new(gearPreview.Binding, [], gearPreview.PreviewDigest,
                "projection-gear", ExplicitlyConfirmed: true)).Outcome == CharacterCreationGearOutcomes.Applied,
                "explicit empty gear confirmed");

            var stored = store.Get(id).Value!;
            CharacterOverviewProjection coreOverview = ((ICharacterSectionQueries)sections).ParseOverview(new(stored.Document.Content));
            var loaded = new WorkspaceOverviewLoadResult(coreOverview.Profile, coreOverview.Progress, coreOverview.Skills,
                coreOverview.Rules, coreOverview.Build, coreOverview.Movement, coreOverview.Awakening,
                stored.ContentRevision, stored.SavedRevision, stored.Document);
            var finalization = finalizer.Load(new(id));
            Check(finalization.Value?.CanReview == true, "real Core whole-build review is available: " + string.Join(",", finalization.Blockers));
            var contactState = contacts.Load(new(id)).Value!;
            var lifestyleState = lifestyles.Load(new(id)).Value!;
            var qualityState = qualities.Load(new(id)).Value!;
            var magicState = magic.Load(new(id)).Value!;
            Check(contactState.Contacts.Count == 0 && lifestyleState.Lifestyles.Count == 0,
                "optional absence comes from real Core readers, without seeded contact or lifestyle budgets");
            var session = new WorkspaceSessionState(id,
                [new OpenWorkspaceState(id, "Projection Runner", "Projection", DateTimeOffset.UtcNow,
                    RulesetDefaults.Sr5, ContentRevision: stored.ContentRevision, SavedRevision: stored.SavedRevision)], [id]);
            var state = factory.CreateLoadedState(CharacterOverviewState.Empty, id, session, loaded, null, true);
            var wizard = state.CreationWizard!;
            Check(wizard.CanFinalize, "factory projects real legal Priority completion: " + string.Join(",", wizard.CompletionBlockers));
            Check(wizard.ActiveStepId == CharacterCreationWizardStepIds.Review, "legal runner advances to Review");
            Check(wizard.Steps.Single(step => step.StepId == CharacterCreationWizardStepIds.Basics).IsComplete,
                "real Bootstrap identity completes Basics");
            Check(!wizard.Steps.Single(step => step.StepId == CharacterCreationWizardStepIds.ContactsLifestyles).IsRequired,
                "empty optional contacts and lifestyles are not invented required choices");

            // Exercise the public asynchronous lifecycle with the same real Core readers.
            // The compatibility loader supplies the exact overview already read above; it
            // does not mint canonical recovery authority or call an external client.
            using (var lifecycle = new WorkspaceOverviewLifecycleCoordinator(
                System.Reflection.DispatchProxy.Create<IChummerClient, UnusedProjectionClient>(),
                new WorkspaceSessionPresenter(), new CapturedOverviewLoader(loaded), new WorkspaceViewStateStore(),
                new WorkspaceShellStateFactory(), new WorkspaceRemoteCloseService(), new WorkspaceSessionActivationService(), factory))
            {
                var activated = await lifecycle.LoadAsync(CharacterOverviewState.Empty, id, CancellationToken.None);
                Check(activated.CanPublish && activated.State.CreationWizard?.CanFinalize == true,
                    "asynchronous lifecycle preserves real Core completion readiness");
                Check(activated.State.CreationWizard!.SnapshotDigest == wizard.SnapshotDigest
                    && ReferenceEquals(activated.RecoveryDocument, loaded.Document),
                    "background preparation preserves exact wizard and document identity");
            }

            var consumedBundleLoader = new CapturedOverviewLoader(loaded);
            int workspacesBeforeReplay = store.List().Count;
            using (var lifecycle = new WorkspaceOverviewLifecycleCoordinator(
                System.Reflection.DispatchProxy.Create<IChummerClient, UnusedProjectionClient>(),
                new WorkspaceSessionPresenter(), consumedBundleLoader, new WorkspaceViewStateStore(),
                new WorkspaceShellStateFactory(), new WorkspaceRemoteCloseService(), new WorkspaceSessionActivationService(), factory))
            {
                var reloaded = await lifecycle.ActivateCreatedAsync(CharacterOverviewState.Empty, activation, activationService,
                    CancellationToken.None);
                Check(reloaded.CanPublish && reloaded.State.CreationWizard?.SnapshotDigest == wizard.SnapshotDigest
                    && consumedBundleLoader.LoadCount == 1 && ReferenceEquals(reloaded.RecoveryDocument, loaded.Document),
                    "consumed activation falls back to current persisted overview, never the old initial snapshot");
                Check(store.List().Count == workspacesBeforeReplay, "activation recovery does not create a duplicate runner");
            }

            CharacterCreationWizardSnapshot Project(CharacterCreationFinalizationResult<CharacterCreationFinalizationState>? result,
                CharacterCreationContactsState? contact = null, CharacterCreationLifestylesState? lifestyle = null)
                => CharacterCreationWizardProjector.Project(id, loaded, contacts: contact ?? contactState,
                    qualities: qualityState, magicResonance: magicState,
                    lifestyles: lifestyle ?? lifestyleState, finalization: result);
            Check(!Project(null).CanFinalize, "missing finalization authority cannot complete");
            var stale = finalization.Value! with
                { Binding = finalization.Value!.Binding with { ContentRevision = stored.ContentRevision - 1 } };
            stale = stale with { SnapshotDigest = CharacterCreationFinalizationDigest.Compute(stale with { SnapshotDigest = string.Empty }) };
            Check(!Project(finalization with { Value = stale }).CanFinalize, "rehashed stale Core state is rejected");
            var forgedContact = contactState with { SnapshotDigest = "sha256:" + new string('a', 64) };
            Check(!Project(finalization, forgedContact).CanFinalize, "SHA-shaped contact digest is not proof of absence");
            var staleContactAux = contactState with
                { Binding = contactState.Binding with { AuxiliaryStateDigest = new string('0', 64) } };
            staleContactAux = staleContactAux with
                { SnapshotDigest = CharacterCreationFinalizationDigest.Compute(staleContactAux with { SnapshotDigest = string.Empty }) };
            Check(!Project(finalization, staleContactAux).CanFinalize, "rehashed foreign contact auxiliary state is rejected");
            var falseContactCapability = contactState with { CanEdit = true, Blockers = ["unknown-contact-rule"] };
            falseContactCapability = falseContactCapability with
                { SnapshotDigest = CharacterCreationFinalizationDigest.Compute(falseContactCapability with { SnapshotDigest = string.Empty }) };
            Check(!Project(finalization, falseContactCapability).CanFinalize, "editable flag cannot waive a contact blocker");
            foreach (var malformed in new[]
            {
                contactState with { Contacts = null! }, contactState with { Contacts = [null!] },
                contactState with { Binding = null! }, contactState with { ContactBudget = null! }
            })
            {
                var resealed = malformed with
                    { SnapshotDigest = CharacterCreationFinalizationDigest.Compute(malformed with { SnapshotDigest = string.Empty }) };
                Check(!Project(finalization, resealed).CanFinalize, "malformed contact state fails closed without crashing");
            }
            var forgedLifestyle = lifestyleState with { Binding = lifestyleState.Binding with { ContentRevision = stored.ContentRevision + 1 } };
            forgedLifestyle = forgedLifestyle with { SnapshotDigest = CharacterCreationLifestylesRules.ComputeStateDigest(forgedLifestyle) };
            Check(!Project(finalization, lifestyle: forgedLifestyle).CanFinalize, "rehashed stale optional lifestyle state is rejected");
            var after = store.Get(id).Value!;
            Check(after.ContentRevision == stored.ContentRevision && after.Document.Content == stored.Document.Content
                && after.Document.AuxiliaryStateDigest == stored.Document.AuxiliaryStateDigest,
                "all UI projection and hostile-input checks are read-only");
            report($"{passed}/{passed} real-Core creation projection checks passed. Not Android user-route or Play evidence.");
            return passed;
        }
        finally
        {
            // Exact newly created fixture directory only; never a supplied content root.
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    private sealed class CapturedOverviewLoader(WorkspaceOverviewLoadResult overview) : IWorkspaceOverviewLoader
    {
        internal int LoadCount { get; private set; }
        public Task<WorkspaceOverviewLoadResult> LoadAsync(IChummerClient client, CharacterWorkspaceId id, CancellationToken ct)
        {
            LoadCount++;
            return Task.FromResult(overview);
        }
    }

    private sealed class ObservedActivationService(ICharacterCreationBootstrapActivationService inner)
        : ICharacterCreationBootstrapActivationService
    {
        internal int ValidationCount { get; private set; }
        internal SynchronizationContext? ValidationContext { get; private set; }
        internal Action? AfterAcceptedValidation { get; init; }

        public bool TryValidateCurrent(CharacterCreationBootstrapActivationBundle activation, out IReadOnlyList<string> blockers)
        {
            ValidationCount++;
            ValidationContext = SynchronizationContext.Current;
            bool accepted = inner.TryValidateCurrent(activation, out blockers);
            if (accepted) AfterAcceptedValidation?.Invoke();
            return accepted;
        }

        public CharacterCreationBootstrapActivationAttempt CreateActivation(CharacterCreationBootstrapRequest request)
            => throw new InvalidOperationException("Activation observation must never create another workspace.");
    }

    private sealed class ObservedFinalizationService(ICharacterCreationFinalizationService inner)
        : ICharacterCreationFinalizationService
    {
        internal int LoadCount { get; private set; }

        public CharacterCreationFinalizationResult<CharacterCreationFinalizationState> Load(CharacterCreationFinalizationLoadRequest request)
        {
            LoadCount++;
            return inner.Load(request);
        }

        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReview> Review(CharacterCreationFinalizationReviewRequest request)
            => throw new InvalidOperationException("Activation proof does not review changes.");
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> Confirm(CharacterCreationFinalizationConfirmRequest request)
            => throw new InvalidOperationException("Activation proof does not confirm changes.");
        public CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> LookupReceipt(CharacterCreationFinalizationReceiptLookupRequest request)
            => throw new InvalidOperationException("Activation proof does not recover a finalization mutation.");
    }

    public class UnusedProjectionClient : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException("Projection check must not invoke an external client or mutation route.");
    }
}
