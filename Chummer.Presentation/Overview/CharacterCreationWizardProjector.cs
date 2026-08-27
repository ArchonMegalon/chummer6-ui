using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Projects the currently loaded canonical dossier into a renderer-neutral creation journey.
/// This projector deliberately does not manufacture rules, source, runtime, or provider authority.
/// </summary>
public static class CharacterCreationWizardProjector
{
    public const string ContentAuthorityUnavailable = "creation-wizard-content-authority-unavailable";
    public const string RevisionAuthorityUnavailable = "creation-wizard-revision-authority-unavailable";
    public const string RulesetAuthorityUnavailable = "creation-wizard-ruleset-authority-unavailable";
    public const string SourceAuthorityUnavailable = "creation-wizard-source-authority-unavailable";
    public const string RuntimeAuthorityUnavailable = "creation-wizard-runtime-authority-unavailable";
    public const string BuildGhostContextUnavailable = "creation-wizard-build-ghost-context-unavailable";
    public const string LegalOptionsAuthorityUnavailable = "creation-wizard-legal-options-authority-unavailable";
    public const string FinalizationAuthorityUnavailable = "creation-wizard-finalization-authority-unavailable";
    public const string LifeModuleAuthorityUnavailable = "creation-wizard-life-module-authority-unavailable";
    public const string ContactsAuthorityUnavailable = "creation-wizard-contacts-authority-unavailable";
    public const string QualitiesAuthorityUnavailable = "creation-wizard-qualities-authority-unavailable";
    public const string MagicResonanceAuthorityUnavailable = "creation-wizard-magic-resonance-authority-unavailable";
    public const string ContactCreateDeleteAuthorityUnavailable = "creation-wizard-contact-create-delete-authority-unavailable";
    public const string ContactPetsAuthorityUnavailable = "creation-wizard-contact-pets-authority-unavailable";
    public const string LifestylesAuthorityUnavailable = "creation-wizard-lifestyles-authority-unavailable";
    public const string BuildMethodUnavailable = "creation-wizard-build-method-unavailable";
    public const string BuildMethodMismatch = "creation-wizard-build-method-mismatch";

    public static CharacterCreationWizardSnapshot Project(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationState? foundation = null,
        CharacterCreationContactsState? contacts = null,
        CharacterCreationQualitiesState? qualities = null,
        CharacterCreationMagicResonanceState? magicResonance = null)
    {
        ArgumentNullException.ThrowIfNull(loadedOverview);
        if (foundation is not null
            && !MatchesLoadedOverview(workspaceId, loadedOverview, foundation))
        {
            foundation = null;
        }
        if (contacts is not null
            && !MatchesLoadedOverview(workspaceId, loadedOverview, contacts))
        {
            contacts = null;
        }
        if (qualities is not null
            && !MatchesLoadedOverview(workspaceId, loadedOverview, qualities))
        {
            qualities = null;
        }
        if (magicResonance is not null
            && !MatchesLoadedOverview(workspaceId, loadedOverview, magicResonance))
        {
            magicResonance = null;
        }

        string profileBuildMethod = CanonicalBuildMethod(loadedOverview.Profile.BuildMethod);
        string buildBuildMethod = CanonicalBuildMethod(loadedOverview.Build.BuildMethod);
        string buildMethod = !string.IsNullOrWhiteSpace(profileBuildMethod)
            ? profileBuildMethod
            : buildBuildMethod;
        List<string> methodBlockers = [];
        if (!CharacterCreationBuildMethods.IsSupported(buildMethod))
        {
            methodBlockers.Add(BuildMethodUnavailable);
        }

        if (!string.IsNullOrWhiteSpace(profileBuildMethod)
            && !string.IsNullOrWhiteSpace(buildBuildMethod)
            && !string.Equals(profileBuildMethod, buildBuildMethod, StringComparison.Ordinal))
        {
            methodBlockers.Add(BuildMethodMismatch);
        }

        bool methodAuthoritative = methodBlockers.Count == 0;
        bool usesLifeModules = string.Equals(
            buildMethod,
            CharacterCreationBuildMethods.LifeModules,
            StringComparison.Ordinal);
        string contentDigest = ComputeContentDigest(loadedOverview.Document);
        string rulesetId = loadedOverview.Document?.RulesetId ?? string.Empty;
        bool hasSourceAuthority = HasSourceAuthority(foundation)
                                  || HasSourceAuthority(qualities)
                                  || HasSourceAuthority(magicResonance);
        bool magicResonanceRequired = string.Equals(
                                          RulesetDefaults.NormalizeOptional(rulesetId),
                                          CharacterCreationMagicResonancePresentationContract.RulesetId,
                                          StringComparison.Ordinal)
                                      && string.Equals(
                                          buildMethod,
                                          CharacterCreationMagicResonancePresentationContract.BuildMethod,
                                          StringComparison.Ordinal);
        FoundationProjectionAuthority foundationAuthority = EvaluateFoundationAuthority(
            usesLifeModules,
            foundation);
        ContactsProjectionAuthority contactsAuthority = EvaluateContactsAuthority(contacts);
        QualitiesProjectionAuthority qualitiesAuthority = EvaluateQualitiesAuthority(qualities);
        MagicResonanceProjectionAuthority magicResonanceAuthority =
            EvaluateMagicResonanceAuthority(magicResonance);
        List<string> completionBlockers =
        [
            RuntimeAuthorityUnavailable,
            BuildGhostContextUnavailable,
            LegalOptionsAuthorityUnavailable,
            FinalizationAuthorityUnavailable
        ];
        if (!hasSourceAuthority)
            completionBlockers.Add(SourceAuthorityUnavailable);
        if (foundation is not null)
            completionBlockers.AddRange(foundation.AuthorityBlockers);
        completionBlockers.AddRange(methodBlockers);
        if (string.IsNullOrWhiteSpace(contentDigest))
        {
            completionBlockers.Add(ContentAuthorityUnavailable);
        }

        if (loadedOverview.ContentRevision <= 0)
        {
            completionBlockers.Add(RevisionAuthorityUnavailable);
        }

        if (string.IsNullOrWhiteSpace(rulesetId))
        {
            completionBlockers.Add(RulesetAuthorityUnavailable);
        }

        if (usesLifeModules)
        {
            completionBlockers.Add(LifeModuleAuthorityUnavailable);
        }
        if (!contactsAuthority.IsReady)
        {
            completionBlockers.Add(ContactsAuthorityUnavailable);
        }
        completionBlockers.AddRange(contactsAuthority.Blockers);
        if (!qualitiesAuthority.IsReady)
            completionBlockers.Add(QualitiesAuthorityUnavailable);
        completionBlockers.AddRange(qualitiesAuthority.Blockers);
        if (magicResonanceRequired && !magicResonanceAuthority.IsReady)
            completionBlockers.Add(MagicResonanceAuthorityUnavailable);
        completionBlockers.AddRange(magicResonanceAuthority.Blockers);
        completionBlockers.Add(ContactCreateDeleteAuthorityUnavailable);
        completionBlockers.Add(ContactPetsAuthorityUnavailable);
        completionBlockers.Add(LifestylesAuthorityUnavailable);

        IReadOnlyList<CharacterCreationBudgetState> budgets = BuildBudgets(
            loadedOverview.Build,
            usesLifeModules,
            hasSourceAuthority ? foundation : null,
            contacts,
            qualities,
            magicResonanceAuthority.Editor);
        completionBlockers.AddRange(budgets.SelectMany(static budget => budget.Blockers));

        IReadOnlyList<CharacterCreationWizardStageState> steps = BuildSteps(
            loadedOverview.Profile,
            buildMethod,
            methodAuthoritative,
            usesLifeModules,
            foundationAuthority,
            contactsAuthority,
            qualitiesAuthority,
            magicResonanceRequired,
            magicResonanceAuthority);
        string activeStepId = !methodAuthoritative
            ? CharacterCreationWizardStepIds.Method
            : magicResonanceAuthority.HasPendingDraft
                ? CharacterCreationWizardStepIds.MagicResonance
                : qualitiesAuthority.HasPendingDraft
                ? CharacterCreationWizardStepIds.Qualities
                : qualitiesAuthority.IsReady
                    ? CharacterCreationWizardStepIds.Qualities
                    : foundationAuthority.HasPendingDraft
                ? CharacterCreationWizardStepIds.LifeModules
                : CharacterCreationWizardStepIds.Foundation;
        Dictionary<string, IReadOnlyList<CharacterCreationLegalOption>> legalOptions =
            steps.ToDictionary(
                static step => step.StepId,
                static _ => (IReadOnlyList<CharacterCreationLegalOption>)[],
                StringComparer.Ordinal);
        if (foundationAuthority.IsReady)
        {
            legalOptions[CharacterCreationWizardStepIds.Foundation] =
                foundationAuthority.MetatypeOptions;
            legalOptions[CharacterCreationWizardStepIds.LifeModules] =
                foundationAuthority.NationalityOptions;
        }
        if (qualitiesAuthority.IsReady)
        {
            legalOptions[CharacterCreationWizardStepIds.Qualities] = qualitiesAuthority.Options;
        }
        if (magicResonanceAuthority.IsReady)
        {
            legalOptions[CharacterCreationWizardStepIds.MagicResonance] =
                magicResonanceAuthority.Options;
        }

        CharacterCreationWizardSnapshot snapshot = new(
            Schema: CharacterCreationWizardSchemas.SnapshotV1,
            WorkspaceId: workspaceId.Value,
            WorkspaceRevision: loadedOverview.ContentRevision,
            ContentDigest: contentDigest,
            SourceDigest: HasSourceAuthority(magicResonance)
                ? magicResonance!.Authority.SourceInputsDigest
                : HasSourceAuthority(qualities)
                ? qualities!.Authority.SourceDigest
                : HasSourceAuthority(foundation)
                    ? foundation!.Binding.SourceDigest
                    : string.Empty,
            RulesetId: rulesetId,
            RuntimeFingerprint: magicResonanceAuthority.Editor?.Binding.RuntimeDigest
                                ?? string.Empty,
            BuildMethod: buildMethod,
            CharacterCreated: loadedOverview.Profile.Created,
            ActiveStepId: activeStepId,
            Steps: steps,
            Budgets: budgets,
            LegalOptionsByStep: legalOptions,
            CompletionBlockers: completionBlockers.Distinct(StringComparer.Ordinal).ToArray(),
            Warnings: BuildWarnings(hasSourceAuthority, foundationAuthority),
            CanFinalize: false,
            SnapshotDigest: string.Empty);

        return snapshot with { SnapshotDigest = ComputeSnapshotDigest(snapshot) };
    }

    private static IReadOnlyList<CharacterCreationWizardStageState> BuildSteps(
        CharacterProfileSection profile,
        string buildMethod,
        bool methodAuthoritative,
        bool usesLifeModules,
        FoundationProjectionAuthority foundationAuthority,
        ContactsProjectionAuthority contactsAuthority,
        QualitiesProjectionAuthority qualitiesAuthority,
        bool magicResonanceRequired,
        MagicResonanceProjectionAuthority magicResonanceAuthority)
    {
        IReadOnlyList<string> methodNext = methodAuthoritative
            ? [CharacterCreationWizardStepIds.Foundation]
            : [];

        return
        [
            Stage(
                CharacterCreationWizardStepIds.Basics,
                "Basics",
                CharacterCreationWizardStepStatuses.InProgress,
                isRequired: true,
                isAvailable: true,
                isComplete: false,
                budgetIds: [],
                blockers: [],
                legalNextStepIds: [CharacterCreationWizardStepIds.Method]),
            Stage(
                CharacterCreationWizardStepIds.Method,
                "Creation method",
                methodAuthoritative
                    ? CharacterCreationWizardStepStatuses.Complete
                    : CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: true,
                isComplete: methodAuthoritative,
                budgetIds: [],
                blockers: methodAuthoritative
                    ? []
                    : [CharacterCreationBuildMethods.IsSupported(buildMethod) ? BuildMethodMismatch : BuildMethodUnavailable],
                legalNextStepIds: methodNext),
            Stage(
                CharacterCreationWizardStepIds.Foundation,
                "Metatype and foundation",
                qualitiesAuthority.IsReady
                    ? CharacterCreationWizardStepStatuses.Complete
                    : foundationAuthority.HasPendingDraft
                    ? CharacterCreationWizardStepStatuses.Complete
                    : foundationAuthority.IsReady
                        ? CharacterCreationWizardStepStatuses.InProgress
                        : CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: qualitiesAuthority.IsReady || foundationAuthority.IsReady,
                isComplete: qualitiesAuthority.IsReady || foundationAuthority.HasPendingDraft,
                budgetIds: [],
                blockers: qualitiesAuthority.IsReady || foundationAuthority.IsReady
                    ? []
                    : CombineBlockers(
                        [LegalOptionsAuthorityUnavailable],
                        foundationAuthority.Blockers),
                warnings: qualitiesAuthority.IsReady
                          || foundationAuthority.IsReady
                          || string.IsNullOrWhiteSpace(profile.Metatype)
                    ? []
                    : ["creation-wizard-existing-metatype-requires-authoritative-review"],
                legalNextStepIds: qualitiesAuthority.IsReady
                    ? [CharacterCreationWizardStepIds.Attributes]
                    : foundationAuthority.IsReady
                        ? [CharacterCreationWizardStepIds.LifeModules]
                        : []),
            Stage(
                CharacterCreationWizardStepIds.LifeModules,
                "Life modules",
                foundationAuthority.HasPendingDraft
                    ? CharacterCreationWizardStepStatuses.InProgress
                    : foundationAuthority.IsReady
                        ? CharacterCreationWizardStepStatuses.Available
                        : usesLifeModules
                            ? CharacterCreationWizardStepStatuses.Blocked
                            : CharacterCreationWizardStepStatuses.NotStarted,
                isRequired: usesLifeModules,
                isAvailable: foundationAuthority.IsReady,
                isComplete: false,
                budgetIds: usesLifeModules ? [CharacterCreationBudgetIds.LifeModules] : [],
                blockers: usesLifeModules && !foundationAuthority.IsReady
                    ? CombineBlockers(
                        [LifeModuleAuthorityUnavailable],
                        foundationAuthority.Blockers)
                    : [],
                legalNextStepIds: []),
            Stage(
                CharacterCreationWizardStepIds.Attributes,
                "Attributes",
                qualitiesAuthority.IsReady
                    ? CharacterCreationWizardStepStatuses.Complete
                    : CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: qualitiesAuthority.IsReady,
                isComplete: qualitiesAuthority.IsReady,
                budgetIds: [CharacterCreationBudgetIds.NormalAttributes, CharacterCreationBudgetIds.SpecialAttributes],
                blockers: qualitiesAuthority.IsReady ? [] : [LegalOptionsAuthorityUnavailable],
                legalNextStepIds: qualitiesAuthority.IsReady
                    ? [CharacterCreationWizardStepIds.Qualities]
                    : []),
            Stage(
                CharacterCreationWizardStepIds.Qualities,
                "Qualities",
                qualitiesAuthority.HasPendingDraft
                    ? CharacterCreationWizardStepStatuses.Complete
                    : qualitiesAuthority.IsReady
                        ? CharacterCreationWizardStepStatuses.InProgress
                        : CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: qualitiesAuthority.IsReady,
                isComplete: qualitiesAuthority.HasPendingDraft,
                budgetIds:
                [
                    CharacterCreationBudgetIds.Karma,
                    CharacterCreationBudgetIds.PositiveQualities,
                    CharacterCreationBudgetIds.NegativeQualities
                ],
                blockers: qualitiesAuthority.IsReady
                    ? qualitiesAuthority.Blockers
                    : CombineBlockers([QualitiesAuthorityUnavailable], qualitiesAuthority.Blockers),
                legalNextStepIds: qualitiesAuthority.HasPendingDraft
                    ? [CharacterCreationWizardStepIds.Skills]
                    : []),
            Stage(
                CharacterCreationWizardStepIds.Skills,
                "Skills",
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds:
                [
                    CharacterCreationBudgetIds.ActiveSkills,
                    CharacterCreationBudgetIds.SkillGroups,
                    CharacterCreationBudgetIds.KnowledgeSkills
                ],
                blockers: [LegalOptionsAuthorityUnavailable],
                legalNextStepIds: [CharacterCreationWizardStepIds.MagicResonance]),
            Stage(
                CharacterCreationWizardStepIds.MagicResonance,
                "Magic, resonance, or emergent identity",
                magicResonanceAuthority.HasPendingDraft
                    ? CharacterCreationWizardStepStatuses.Complete
                    : magicResonanceAuthority.IsReady
                        ? CharacterCreationWizardStepStatuses.InProgress
                        : magicResonanceRequired
                            ? CharacterCreationWizardStepStatuses.Blocked
                            : CharacterCreationWizardStepStatuses.NotStarted,
                isRequired: magicResonanceRequired,
                isAvailable: magicResonanceAuthority.IsReady,
                isComplete: magicResonanceAuthority.HasPendingDraft,
                budgetIds:
                [
                    CharacterCreationMagicResonancePresentationBudgetIds.Tradition,
                    CharacterCreationMagicResonancePresentationBudgetIds.Stream,
                    CharacterCreationMagicResonancePresentationBudgetIds.AdeptPowerPoints,
                    CharacterCreationMagicResonancePresentationBudgetIds.Spells,
                    CharacterCreationMagicResonancePresentationBudgetIds.ComplexForms
                ],
                blockers: magicResonanceAuthority.IsReady
                    ? magicResonanceAuthority.Blockers
                    : magicResonanceRequired
                        ? CombineBlockers(
                            [MagicResonanceAuthorityUnavailable],
                            magicResonanceAuthority.Blockers)
                        : [],
                legalNextStepIds: magicResonanceAuthority.HasPendingDraft
                    ? [CharacterCreationWizardStepIds.Resources]
                    : []),
            Stage(
                CharacterCreationWizardStepIds.Resources,
                "Resources",
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds: [CharacterCreationBudgetIds.Resources],
                blockers: [LegalOptionsAuthorityUnavailable],
                legalNextStepIds: [CharacterCreationWizardStepIds.ContactsLifestyles]),
            Stage(
                CharacterCreationWizardStepIds.ContactsLifestyles,
                "Contacts and lifestyles",
                contactsAuthority.IsReady
                    ? CharacterCreationWizardStepStatuses.InProgress
                    : CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: contactsAuthority.IsReady,
                isComplete: false,
                budgetIds:
                [
                    CharacterCreationBudgetIds.Contacts,
                    CharacterCreationContactBudgetIds.FriendsInHighPlaces,
                    CharacterCreationBudgetIds.Resources
                ],
                blockers: contactsAuthority.IsReady
                    ? CombineBlockers(
                        [
                            ContactCreateDeleteAuthorityUnavailable,
                            ContactPetsAuthorityUnavailable,
                            LifestylesAuthorityUnavailable
                        ],
                        contactsAuthority.Blockers)
                    : CombineBlockers(
                        [
                            ContactsAuthorityUnavailable,
                            ContactCreateDeleteAuthorityUnavailable,
                            ContactPetsAuthorityUnavailable,
                            LifestylesAuthorityUnavailable
                        ],
                        contactsAuthority.Blockers),
                legalNextStepIds: []),
            Stage(
                CharacterCreationWizardStepIds.IdentityStory,
                "Identity and story",
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds: [],
                blockers: [LegalOptionsAuthorityUnavailable],
                legalNextStepIds: [CharacterCreationWizardStepIds.Review]),
            Stage(
                CharacterCreationWizardStepIds.Review,
                "Review and finalize",
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds: [],
                blockers: [FinalizationAuthorityUnavailable],
                legalNextStepIds: [])
        ];
    }

    private static CharacterCreationWizardStageState Stage(
        string stepId,
        string label,
        string status,
        bool isRequired,
        bool isAvailable,
        bool isComplete,
        IReadOnlyList<string> budgetIds,
        IReadOnlyList<string> blockers,
        IReadOnlyList<string> legalNextStepIds,
        IReadOnlyList<string>? warnings = null)
        => new(
            StepId: stepId,
            Label: label,
            Status: status,
            IsRequired: isRequired,
            IsAvailable: isAvailable,
            IsComplete: isComplete,
            BudgetIds: budgetIds,
            Blockers: blockers,
            Warnings: warnings ?? [],
            LegalNextStepIds: legalNextStepIds);

    private static IReadOnlyList<CharacterCreationBudgetState> BuildBudgets(
        CharacterBuildSection build,
        bool usesLifeModules,
        CharacterCreationFoundationState? foundation,
        CharacterCreationContactsState? contacts,
        CharacterCreationQualitiesState? qualities,
        CharacterCreationMagicResonanceEditorState? magicResonance)
    {
        List<CharacterCreationBudgetState> budgets =
        [
            qualities is null
                ? UnknownBudget(CharacterCreationBudgetIds.Karma, "Karma", "karma")
                : new CharacterCreationBudgetState(
                    CharacterCreationBudgetIds.Karma,
                    "Karma",
                    qualities.Binding.CreationKarmaTotal,
                    qualities.Binding.CreationKarmaTotal - qualities.Preview.KarmaRemaining,
                    qualities.Preview.KarmaRemaining,
                    IsExact: qualities.CanEdit,
                    Blockers: qualities.Blockers,
                    Unit: "karma"),
            qualities is null
                ? UnknownBudget(CharacterCreationBudgetIds.PositiveQualities, "Positive qualities", "karma")
                : new CharacterCreationBudgetState(
                    CharacterCreationBudgetIds.PositiveQualities,
                    "Positive qualities",
                    qualities.Preview.PositiveQualityBudget.Total,
                    qualities.Preview.PositiveQualityBudget.Used,
                    qualities.Preview.PositiveQualityBudget.Remaining,
                    IsExact: qualities.CanEdit,
                    Blockers: qualities.Preview.PositiveQualityBudget.Blockers,
                    Unit: "karma"),
            qualities is null
                ? UnknownBudget(CharacterCreationBudgetIds.NegativeQualities, "Negative qualities", "karma")
                : new CharacterCreationBudgetState(
                    CharacterCreationBudgetIds.NegativeQualities,
                    "Negative qualities",
                    qualities.Preview.NegativeQualityBudget.Total,
                    qualities.Preview.NegativeQualityBudget.Used,
                    qualities.Preview.NegativeQualityBudget.Remaining,
                    IsExact: qualities.CanEdit,
                    Blockers: qualities.Preview.NegativeQualityBudget.Blockers,
                    Unit: "karma"),
            UnknownBudget(CharacterCreationBudgetIds.NormalAttributes, "Normal attributes", "points"),
            UnknownBudget(CharacterCreationBudgetIds.SpecialAttributes, "Special attributes", "points"),
            UnknownBudget(CharacterCreationBudgetIds.ActiveSkills, "Active skills", "points"),
            UnknownBudget(CharacterCreationBudgetIds.SkillGroups, "Skill groups", "points"),
            UnknownBudget(CharacterCreationBudgetIds.KnowledgeSkills, "Knowledge skills", "points"),
            contacts is null
                ? ContactBudget(build)
                : ProjectBudget(contacts.ContactBudget, "Contacts", "points"),
            UnknownBudget(CharacterCreationBudgetIds.Resources, "Resources", "nuyen"),
            ProjectCombinedMagicChoicesBudget(magicResonance),
            ProjectMagicBudget(
                magicResonance,
                CharacterCreationMagicResonanceKinds.Tradition,
                CharacterCreationMagicResonancePresentationBudgetIds.Tradition,
                "Tradition",
                "choices"),
            ProjectMagicBudget(
                magicResonance,
                CharacterCreationMagicResonanceKinds.Stream,
                CharacterCreationMagicResonancePresentationBudgetIds.Stream,
                "Stream",
                "choices"),
            ProjectMagicBudget(
                magicResonance,
                CharacterCreationMagicResonanceKinds.AdeptPower,
                CharacterCreationMagicResonancePresentationBudgetIds.AdeptPowerPoints,
                "Adept powers",
                "power-points"),
            ProjectMagicBudget(
                magicResonance,
                CharacterCreationMagicResonanceKinds.Spell,
                CharacterCreationMagicResonancePresentationBudgetIds.Spells,
                "Spells",
                "choices"),
            ProjectMagicBudget(
                magicResonance,
                CharacterCreationMagicResonanceKinds.ComplexForm,
                CharacterCreationMagicResonancePresentationBudgetIds.ComplexForms,
                "Complex forms",
                "choices")
        ];
        if (usesLifeModules)
        {
            budgets.Add(foundation?.LifeModuleBudget.IsExact == true
                && foundation.LifeModuleBudget.Blockers.Count == 0
                ? foundation.LifeModuleBudget
                : UnknownBudget(
                    CharacterCreationBudgetIds.LifeModules,
                    "Life modules",
                    "karma",
                    LifeModuleAuthorityUnavailable,
                    foundation?.LifeModuleBudget.Blockers));
        }

        if (contacts is not null)
        {
            budgets.Add(ProjectBudget(
                contacts.HighPlacesBudget,
                "Friends in High Places contacts",
                "points"));
        }

        return budgets;
    }

    private static CharacterCreationBudgetState ProjectCombinedMagicChoicesBudget(
        CharacterCreationMagicResonanceEditorState? state)
    {
        if (state is null)
        {
            return UnknownBudget(
                CharacterCreationBudgetIds.SpellsFormsPrograms,
                "Spells and complex forms",
                "choices",
                MagicResonanceAuthorityUnavailable);
        }
        CharacterCreationMagicResonanceBudgetState spells = state.Budgets.Single(budget =>
            string.Equals(budget.Kind, CharacterCreationMagicResonanceKinds.Spell, StringComparison.Ordinal));
        CharacterCreationMagicResonanceBudgetState forms = state.Budgets.Single(budget =>
            string.Equals(budget.Kind, CharacterCreationMagicResonanceKinds.ComplexForm, StringComparison.Ordinal));
        string[] blockers = spells.Blockers.Concat(forms.Blockers)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToArray();
        return new CharacterCreationBudgetState(
            CharacterCreationBudgetIds.SpellsFormsPrograms,
            "Spells and complex forms",
            spells.Total + forms.Total,
            spells.Used + forms.Used,
            spells.Remaining + forms.Remaining,
            state.CanEdit && blockers.Length == 0,
            blockers,
            "choices");
    }

    private static CharacterCreationBudgetState ProjectMagicBudget(
        CharacterCreationMagicResonanceEditorState? state,
        string kind,
        string budgetId,
        string label,
        string unit)
    {
        if (state is null)
            return UnknownBudget(budgetId, label, unit, MagicResonanceAuthorityUnavailable);
        CharacterCreationMagicResonanceBudgetState budget = state.Budgets.Single(candidate =>
            string.Equals(candidate.Kind, kind, StringComparison.Ordinal));
        return new CharacterCreationBudgetState(
            budgetId,
            label,
            budget.Total,
            budget.Used,
            budget.Remaining,
            state.CanEdit && budget.Blockers.Count == 0,
            budget.Blockers,
            unit);
    }

    private static CharacterCreationBudgetState ProjectBudget(
        CharacterCreationContactBudget budget,
        string label,
        string unit)
        => new(
            BudgetId: budget.BudgetId,
            Label: label,
            Total: budget.Total,
            Used: budget.Used,
            Remaining: budget.Remaining,
            IsExact: budget.IsExact,
            Blockers: budget.Blockers,
            Unit: unit);

    private static CharacterCreationBudgetState ContactBudget(CharacterBuildSection build)
    {
        decimal total = build.ContactPoints;
        decimal used = build.ContactPointsUsed;
        decimal remaining = total - used;
        bool isExact = total >= 0m && used >= 0m && used <= total;
        return new CharacterCreationBudgetState(
            BudgetId: CharacterCreationBudgetIds.Contacts,
            Label: "Contacts",
            Total: total,
            Used: used,
            Remaining: remaining,
            IsExact: isExact,
            Blockers: isExact ? [] : ["creation-wizard-contact-budget-inconsistent"],
            Unit: "points");
    }

    private static CharacterCreationBudgetState UnknownBudget(
        string budgetId,
        string label,
        string unit,
        string? additionalBlocker = null,
        IReadOnlyList<string>? additionalBlockers = null)
    {
        List<string> blockers = [$"creation-wizard-budget-authority-unavailable:{budgetId}"];
        if (!string.IsNullOrWhiteSpace(additionalBlocker))
        {
            blockers.Add(additionalBlocker);
        }
        if (additionalBlockers is not null)
            blockers.AddRange(additionalBlockers);

        return new CharacterCreationBudgetState(
            BudgetId: budgetId,
            Label: label,
            Total: 0m,
            Used: 0m,
            Remaining: 0m,
            IsExact: false,
            Blockers: blockers,
            Unit: unit);
    }

    private static string CanonicalBuildMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string candidate = value.Trim();
        return candidate.ToUpperInvariant() switch
        {
            "KARMA" => CharacterCreationBuildMethods.Karma,
            "PRIORITY" => CharacterCreationBuildMethods.Priority,
            "SUMTOTEN" => CharacterCreationBuildMethods.SumToTen,
            "LIFEMODULE" => CharacterCreationBuildMethods.LifeModules,
            _ => candidate
        };
    }

    internal static bool MatchesLoadedOverview(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationState foundation)
    {
        ArgumentNullException.ThrowIfNull(loadedOverview);
        ArgumentNullException.ThrowIfNull(foundation);
        string rawDigest = ComputeContentDigest(loadedOverview.Document);
        return loadedOverview.Document is not null
               && string.Equals(
                   foundation.Schema,
                   CharacterCreationFoundationSchemas.SnapshotV1,
                   StringComparison.Ordinal)
               && IsSha256(foundation.SnapshotDigest)
               && string.Equals(
                   foundation.Binding.WorkspaceId.Value,
                   workspaceId.Value,
                   StringComparison.Ordinal)
               && foundation.Binding.ContentRevision == loadedOverview.ContentRevision
               && foundation.Binding.SavedRevision == loadedOverview.SavedRevision
               && string.Equals(
                   foundation.Binding.RawCharacterXmlDigest,
                   rawDigest,
                   StringComparison.Ordinal)
               && string.Equals(
                   foundation.Binding.CharacterDigestSemantics,
                   CharacterCreationFoundationDigestSemantics.RawCharacterXmlSha256,
                   StringComparison.Ordinal)
               && string.Equals(
                   foundation.Binding.SourceDigestSemantics,
                   CharacterCreationFoundationDigestSemantics.RawSourceInputsSha256,
                   StringComparison.Ordinal)
               && IsSha256(foundation.Binding.SourceDigest)
               && string.Equals(
                   foundation.RulesetId,
                   loadedOverview.Document.RulesetId,
                   StringComparison.Ordinal)
               && string.Equals(
                   foundation.BuildMethod,
                   CanonicalBuildMethod(loadedOverview.Profile.BuildMethod),
                   StringComparison.Ordinal)
               && string.Equals(
                   foundation.BuildMethod,
                   CanonicalBuildMethod(loadedOverview.Build.BuildMethod),
                   StringComparison.Ordinal)
               && string.Equals(
                   foundation.CurrentMetatype,
                   loadedOverview.Profile.Metatype,
                   StringComparison.Ordinal)
               && !foundation.Binding.SourceFilterApplied
               && foundation.CharacterCreated == loadedOverview.Profile.Created;
    }

    internal static bool MatchesLoadedOverview(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationContactsState contacts)
    {
        ArgumentNullException.ThrowIfNull(loadedOverview);
        ArgumentNullException.ThrowIfNull(contacts);
        string rawDigest = ComputeContentDigest(loadedOverview.Document);
        return loadedOverview.Document is not null
               && string.Equals(
                   contacts.Binding.WorkspaceId.Value,
                   workspaceId.Value,
                   StringComparison.Ordinal)
               && contacts.Binding.WorkspaceRevision == loadedOverview.ContentRevision
               && contacts.Binding.ContentRevision == loadedOverview.ContentRevision
               && contacts.Binding.SavedRevision == loadedOverview.SavedRevision
               && string.Equals(contacts.Binding.ContentDigest, rawDigest, StringComparison.Ordinal)
               && contacts.CharacterCreated == loadedOverview.Profile.Created
               && ContactAuthorityShapeIsValid(contacts);
    }

    internal static bool MatchesLoadedOverview(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationQualitiesState qualities)
    {
        ArgumentNullException.ThrowIfNull(loadedOverview);
        ArgumentNullException.ThrowIfNull(qualities);
        string rawDigest = ComputeContentDigest(loadedOverview.Document);
        return loadedOverview.Document is not null
               && string.Equals(
                   qualities.Schema,
                   CharacterCreationQualitiesSchemas.StateV1,
                   StringComparison.Ordinal)
               && IsLowerSha256(qualities.SnapshotDigest)
               && qualities.Binding.WorkspaceId == workspaceId
               && qualities.Binding.ContentRevision == loadedOverview.ContentRevision
               && qualities.Binding.SavedRevision == loadedOverview.SavedRevision
               && string.Equals(
                   qualities.Binding.RawCharacterXmlDigest,
                   rawDigest,
                   StringComparison.Ordinal)
               && string.Equals(
                   qualities.Binding.RulesetId,
                   loadedOverview.Document.RulesetId,
                   StringComparison.Ordinal)
               && string.Equals(
                   qualities.Binding.BuildMethod,
                   CanonicalBuildMethod(loadedOverview.Profile.BuildMethod),
                   StringComparison.Ordinal)
               && string.Equals(
                   qualities.Binding.BuildMethod,
                   CanonicalBuildMethod(loadedOverview.Build.BuildMethod),
                   StringComparison.Ordinal)
               && qualities.Binding.CharacterCreated == loadedOverview.Profile.Created
               && CharacterCreationQualitiesRules.DigestsEqual(
                   qualities.Binding.AuthorityDigest,
                   qualities.Authority.AuthorityDigest)
               && CharacterCreationQualitiesRules.DigestsEqual(
                   qualities.Binding.RuntimeDigest,
                   qualities.Authority.RuntimeDigest)
               && CharacterCreationQualitiesRules.DigestsEqual(
                   qualities.SnapshotDigest,
                   CharacterCreationQualitiesRules.ComputeStateDigest(qualities))
               && QualitiesProjectionShapeIsValid(qualities);
    }

    internal static bool MatchesLoadedOverview(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationMagicResonanceState magicResonance)
    {
        ArgumentNullException.ThrowIfNull(loadedOverview);
        ArgumentNullException.ThrowIfNull(magicResonance);
        string rawDigest = ComputeContentDigest(loadedOverview.Document);
        return loadedOverview.Document is not null
               && !loadedOverview.Profile.Created
               && magicResonance.Binding.WorkspaceId == workspaceId
               && magicResonance.Binding.ContentRevision == loadedOverview.ContentRevision
               && magicResonance.Binding.SavedRevision == loadedOverview.SavedRevision
               && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
                   magicResonance.Binding.RawCharacterXmlDigest,
                   rawDigest)
               && string.Equals(
                   RulesetDefaults.NormalizeOptional(loadedOverview.Document.RulesetId),
                   CharacterCreationMagicResonancePresentationContract.RulesetId,
                   StringComparison.Ordinal)
               && string.Equals(
                   CanonicalBuildMethod(loadedOverview.Profile.BuildMethod),
                   CharacterCreationMagicResonancePresentationContract.BuildMethod,
                   StringComparison.Ordinal)
               && string.Equals(
                   CanonicalBuildMethod(loadedOverview.Build.BuildMethod),
                   CharacterCreationMagicResonancePresentationContract.BuildMethod,
                   StringComparison.Ordinal)
               && CharacterCreationMagicResonanceWorkflow.TryProject(
                   magicResonance,
                   out _);
    }

    private static bool QualitiesProjectionShapeIsValid(
        CharacterCreationQualitiesState qualities)
    {
        if (!qualities.Authority.IsAuthoritative
            || qualities.Authority.Blockers.Count != 0
            || qualities.PendingDraft is null && qualities.Preview.Selections.Count != 0
            || qualities.PendingDraft is not null
            && !qualities.PendingDraft.SelectedOptionIds.SequenceEqual(
                qualities.Preview.Selections.Select(static selection => selection.OptionId),
                StringComparer.Ordinal))
        {
            return false;
        }
        IReadOnlyList<string> selected = qualities.PendingDraft?.SelectedOptionIds ?? [];
        CharacterCreationQualitiesPreview expected = CharacterCreationQualitiesRules.Evaluate(new(
            qualities.Binding,
            qualities.Authority,
            selected));
        bool coreReady = expected.Blockers.Count == 0;
        return CharacterCreationQualitiesRules.DigestsEqual(
                   expected.PreviewDigest,
                   qualities.Preview.PreviewDigest)
               && qualities.Preview.Binding == qualities.Binding
               && qualities.CanEdit == (coreReady && qualities.Blockers.Count == 0);
    }

    internal static bool MatchesContactSnapshot(
        CharacterCreationWizardSnapshot snapshot,
        CharacterCreationContactsState contacts)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(contacts);
        return !snapshot.CharacterCreated
               && !contacts.CharacterCreated
               && string.Equals(contacts.Binding.WorkspaceId.Value, snapshot.WorkspaceId, StringComparison.Ordinal)
               && contacts.Binding.WorkspaceRevision == snapshot.WorkspaceRevision
               && contacts.Binding.ContentRevision == snapshot.WorkspaceRevision
               && string.Equals(contacts.Binding.ContentDigest, snapshot.ContentDigest, StringComparison.Ordinal)
               && ContactAuthorityShapeIsValid(contacts);
    }

    internal static bool ContactAuthorityShapeIsValid(CharacterCreationContactsState contacts)
        => string.Equals(
               contacts.Schema,
               CharacterCreationContactsSchemas.StateV1,
               StringComparison.Ordinal)
           && string.Equals(
               contacts.StepId,
               CharacterCreationWizardStepIds.ContactsLifestyles,
               StringComparison.Ordinal)
           && contacts.Binding.WorkspaceRevision == contacts.Binding.ContentRevision
           && contacts.Binding.WorkspaceRevision > 0
           && contacts.Binding.SavedRevision >= 0
           && contacts.Binding.SavedRevision <= contacts.Binding.ContentRevision
           && IsLowerSha256(contacts.Binding.ContentDigest)
           && IsLowerRawSha256(contacts.Binding.AuxiliaryStateDigest)
           && IsLowerSha256(contacts.Binding.SourceDigest)
           && IsLowerSha256(contacts.Binding.RulesDigest)
           && IsLowerSha256(contacts.Binding.RuntimeDigest)
           && IsLowerSha256(contacts.SnapshotDigest)
           && contacts.Contacts.All(ContactProjectionShapeIsValid)
           && contacts.Contacts.Select(static contact => contact.ContactId).Distinct().Count()
              == contacts.Contacts.Count
           && string.Equals(
               contacts.ContactBudget.BudgetId,
               CharacterCreationContactBudgetIds.Contacts,
               StringComparison.Ordinal)
           && string.Equals(
               contacts.HighPlacesBudget.BudgetId,
               CharacterCreationContactBudgetIds.FriendsInHighPlaces,
               StringComparison.Ordinal)
           && ContactBudgetShapeIsValid(contacts.ContactBudget)
           && ContactBudgetShapeIsValid(contacts.HighPlacesBudget)
           && StringAuthorityListIsValid(contacts.Blockers, requireExactSourceAnchors: false);

    private static ContactsProjectionAuthority EvaluateContactsAuthority(
        CharacterCreationContactsState? contacts)
    {
        if (contacts is null || contacts.CharacterCreated)
        {
            return new ContactsProjectionAuthority(false, []);
        }

        List<string> blockers = contacts.Blockers
            .Concat(contacts.ContactBudget.Blockers)
            .Concat(contacts.HighPlacesBudget.Blockers)
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToList();
        if (!contacts.CanEdit)
            blockers.Add(CharacterCreationContactsBlockers.AuthorityUnavailable);

        return new ContactsProjectionAuthority(
            IsReady: contacts.CanEdit,
            Blockers: blockers.Distinct(StringComparer.Ordinal)
                .OrderBy(static blocker => blocker, StringComparer.Ordinal)
                .ToArray());
    }

    private static QualitiesProjectionAuthority EvaluateQualitiesAuthority(
        CharacterCreationQualitiesState? qualities)
    {
        if (qualities is null || qualities.Binding.CharacterCreated)
            return new QualitiesProjectionAuthority(false, false, [], []);

        string[] blockers = qualities.Blockers
            .Concat(qualities.Preview.Blockers)
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToArray();
        bool ready = qualities.CanEdit && blockers.Length == 0;
        CharacterCreationLegalOption[] options = ready
            ? qualities.Authority.Options.Select(static option =>
            {
                var costs = new List<CharacterCreationChoiceCost>();
                if (option.CountsAgainstKarma)
                {
                    costs.Add(new CharacterCreationChoiceCost(
                        CharacterCreationBudgetIds.Karma,
                        option.KarmaCost,
                        "karma"));
                }
                if (option.CountsAgainstQualityLimit)
                {
                    costs.Add(new CharacterCreationChoiceCost(
                        option.Type == CharacterCreationQualityType.Positive
                            ? CharacterCreationBudgetIds.PositiveQualities
                            : CharacterCreationBudgetIds.NegativeQualities,
                        Math.Abs((decimal)option.KarmaCost),
                        "karma"));
                }
                string label = option.Rating == 1
                    ? option.Name
                    : $"{option.Name} ({option.Rating})";
                return new CharacterCreationLegalOption(
                    option.OptionId,
                    label,
                    option.IsSelectable,
                    option.DisableReasonKey,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    costs,
                    Consequences: [],
                    SourceAnchorIds: option.SourceAnchorIds,
                    SourceId: option.SourceId.ToString("D"),
                    SourcePage: null,
                    VersionId: option.OptionDigest);
            }).ToArray()
            : [];
        return new QualitiesProjectionAuthority(
            ready,
            ready && qualities.PendingDraft is not null,
            options,
            blockers);
    }

    private static MagicResonanceProjectionAuthority EvaluateMagicResonanceAuthority(
        CharacterCreationMagicResonanceState? state)
    {
        if (!CharacterCreationMagicResonanceWorkflow.TryProject(
                state,
                out CharacterCreationMagicResonanceEditorState? editor))
        {
            return state is null
                ? new MagicResonanceProjectionAuthority(false, false, [], [], null)
                : new MagicResonanceProjectionAuthority(
                    false,
                    false,
                    [],
                    [CharacterCreationMagicResonancePresentationContract.PresentationProjectionInvalid],
                    null);
        }

        string[] blockers = editor!.Blockers
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToArray();
        bool ready = editor.CanEdit && blockers.Length == 0;
        IReadOnlyList<CharacterCreationLegalOption> options = ready
            ? BuildMagicResonanceOptions(editor)
            : [];
        return new MagicResonanceProjectionAuthority(
            ready,
            ready && editor.HasPendingDraft,
            options,
            blockers,
            editor);
    }

    private static IReadOnlyList<CharacterCreationLegalOption> BuildMagicResonanceOptions(
        CharacterCreationMagicResonanceEditorState editor)
    {
        var options = new List<CharacterCreationLegalOption>
        {
            new(
                OptionId: $"talent:{editor.Talent.Identity.PrioritySourceId}:{editor.Talent.Identity.TalentSelectionId}",
                Label: $"Talent: {editor.Talent.Name} ({editor.Talent.Rank})",
                IsEnabled: false,
                DisableReasonKey:
                    CharacterCreationMagicResonancePresentationContract.ExactTalentOwnedByPrerequisite,
                DisableReasonArguments: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["kind"] = editor.Talent.Kind,
                    ["rank"] = editor.Talent.Rank,
                    ["requiredMetatypes"] = string.Join(", ", editor.Talent.RequiredMetatypeNames),
                    ["requiredMetatypeCategories"] = string.Join(", ", editor.Talent.RequiredMetatypeCategories),
                    ["forbiddenMetatypes"] = string.Join(", ", editor.Talent.ForbiddenMetatypeNames)
                },
                Costs: [],
                Consequences: [],
                SourceAnchorIds: editor.Talent.SourceAnchorIds,
                SourceId: editor.Talent.Identity.PrioritySourceId,
                SourcePage: null,
                VersionId: editor.Talent.SourceNodeDigest)
        };
        options.AddRange(editor.Traditions.Select(option => BuildMagicResonanceOption(editor, option)));
        options.AddRange(editor.Streams.Select(option => BuildMagicResonanceOption(editor, option)));
        options.AddRange(editor.AdeptPowers.Select(option => BuildMagicResonanceOption(editor, option)));
        options.AddRange(editor.Spells.Select(option => BuildMagicResonanceOption(editor, option)));
        options.AddRange(editor.ComplexForms.Select(option => BuildMagicResonanceOption(editor, option)));
        return options
            .OrderBy(static option => option.Label, StringComparer.Ordinal)
            .ThenBy(static option => option.OptionId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CharacterCreationLegalOption BuildMagicResonanceOption(
        CharacterCreationMagicResonanceEditorState editor,
        CharacterCreationMagicResonanceOptionProjection option)
    {
        bool kindAllowed = MagicResonanceKindAllowed(editor.Talent, option.Identity.Kind);
        bool enabled = kindAllowed && option.IsEnabled && option.Blockers.Count == 0;
        string? disableReason = enabled
            ? null
            : !kindAllowed
                ? option.Identity.Kind switch
                {
                    CharacterCreationMagicResonanceKinds.Tradition =>
                        CharacterCreationMagicResonanceBlockers.TraditionInvalid,
                    CharacterCreationMagicResonanceKinds.Stream =>
                        CharacterCreationMagicResonanceBlockers.StreamInvalid,
                    CharacterCreationMagicResonanceKinds.AdeptPower =>
                        CharacterCreationMagicResonanceBlockers.PowerSelectionNotAllowed,
                    CharacterCreationMagicResonanceKinds.Spell =>
                        CharacterCreationMagicResonanceBlockers.SpellSelectionNotAllowed,
                    CharacterCreationMagicResonanceKinds.ComplexForm =>
                        CharacterCreationMagicResonanceBlockers.ComplexFormSelectionNotAllowed,
                    _ => CharacterCreationMagicResonanceBlockers.OptionSemanticsUnsupported
                }
                : option.Blockers.FirstOrDefault()
                  ?? CharacterCreationMagicResonanceBlockers.OptionDisabled;
        decimal cost = option.Identity.Kind is CharacterCreationMagicResonanceKinds.Tradition
            or CharacterCreationMagicResonanceKinds.Stream
            or CharacterCreationMagicResonanceKinds.Spell
            or CharacterCreationMagicResonanceKinds.ComplexForm
            ? 1m
            : option.PointCost;
        int? sourcePage = int.TryParse(
            option.Page,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int parsedPage)
            ? parsedPage
            : null;
        return new CharacterCreationLegalOption(
            OptionId: $"{option.Identity.Kind}:{option.Identity.SourceId}",
            Label: $"{MagicResonanceKindLabel(option.Identity.Kind)}: {option.Name}",
            IsEnabled: enabled,
            DisableReasonKey: disableReason,
            DisableReasonArguments: new Dictionary<string, string>(StringComparer.Ordinal),
            Costs:
            [
                new CharacterCreationChoiceCost(
                    CharacterCreationMagicResonancePresentationBudgetIds.ForKind(
                        option.Identity.Kind),
                    cost,
                    option.Identity.Kind == CharacterCreationMagicResonanceKinds.AdeptPower
                        ? "power-points"
                        : "choices")
            ],
            Consequences: [],
            SourceAnchorIds: option.SourceAnchorIds,
            SourceId: option.SourceBook,
            SourcePage: sourcePage,
            VersionId: option.SourceNodeDigest);
    }

    private static bool MagicResonanceKindAllowed(
        CharacterCreationMagicResonanceTalentProjection talent,
        string kind) => kind switch
    {
        CharacterCreationMagicResonanceKinds.Tradition => talent.RequiresTradition,
        CharacterCreationMagicResonanceKinds.Stream => talent.RequiresStream,
        CharacterCreationMagicResonanceKinds.AdeptPower => talent.AllowsAdeptPowers,
        CharacterCreationMagicResonanceKinds.Spell => talent.AllowsSpells,
        CharacterCreationMagicResonanceKinds.ComplexForm => talent.AllowsComplexForms,
        _ => false
    };

    private static string MagicResonanceKindLabel(string kind) => kind switch
    {
        CharacterCreationMagicResonanceKinds.Tradition => "Tradition",
        CharacterCreationMagicResonanceKinds.Stream => "Stream",
        CharacterCreationMagicResonanceKinds.AdeptPower => "Adept power",
        CharacterCreationMagicResonanceKinds.Spell => "Spell",
        CharacterCreationMagicResonanceKinds.ComplexForm => "Complex form",
        _ => "Unsupported"
    };

    internal static bool ContactProjectionShapeIsValid(
        CharacterCreationContactProjection contact)
        => contact.ContactId != Guid.Empty
           && contact.Identity is not null
           && IsLowerSha256(contact.ContactDigest)
           && contact.ContactPointCost >= 0
           && !(contact.CountsAgainstContactBudget && contact.CountsAgainstHighPlacesBudget)
           && StringAuthorityListIsValid(contact.SourceAnchorIds, requireExactSourceAnchors: true)
           && contact.Fields.Count == CharacterCreationContactFieldIds.All.Count
           && contact.Fields.Select(static field => field.FieldId)
               .SequenceEqual(CharacterCreationContactFieldIds.All, StringComparer.Ordinal)
           && contact.Fields.All(field => ContactFieldShapeIsValid(contact, field));

    private static bool ContactFieldShapeIsValid(
        CharacterCreationContactProjection contact,
        CharacterCreationContactFieldAuthority field)
    {
        if (string.IsNullOrWhiteSpace(field.Label)
            || !StringAuthorityListIsValid(field.Blockers, requireExactSourceAnchors: false)
            || !StringAuthorityListIsValid(field.SourceAnchorIds, requireExactSourceAnchors: true)
            || !TryExpectedContactFieldValue(contact, field.FieldId, out string valueKind, out string serializedValue)
            || !string.Equals(field.ValueKind, valueKind, StringComparison.Ordinal)
            || !string.Equals(field.SerializedValue, serializedValue, StringComparison.Ordinal))
        {
            return false;
        }

        if (field.IsEditable
            && field.Blockers.Contains(CharacterCreationContactsBlockers.FieldNotEditable, StringComparer.Ordinal))
        {
            return false;
        }

        if (string.Equals(valueKind, CharacterCreationContactValueKinds.Text, StringComparison.Ordinal))
        {
            return field.Minimum == 0
                   && field.Maximum is int maximum
                   && maximum >= 0
                   && field.SerializedValue.Length <= maximum
                   && field.LegalOptions.Count == 0;
        }

        if (string.Equals(valueKind, CharacterCreationContactValueKinds.Boolean, StringComparison.Ordinal))
        {
            return field.Minimum is null
                   && field.Maximum is null
                   && bool.TryParse(field.SerializedValue, out _)
                   && field.LegalOptions.Count == 2
                   && ContactBooleanOptionsShapeIsValid(field);
        }

        if (field.Minimum is not int minimum
            || field.Maximum is not int maximumValue
            || minimum > maximumValue
            || !int.TryParse(
                field.SerializedValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int selectedInteger)
            || selectedInteger < minimum
            || selectedInteger > maximumValue)
        {
            return false;
        }

        if (string.Equals(valueKind, CharacterCreationContactValueKinds.Integer, StringComparison.Ordinal))
        {
            long optionCount = (long)maximumValue - minimum + 1;
            return optionCount is > 0 and <= int.MaxValue
                   && field.LegalOptions.Count == optionCount
                   && ContactOptionsShapeIsValid(field, minimum, maximumValue);
        }

        return false;
    }

    private static bool ContactBooleanOptionsShapeIsValid(
        CharacterCreationContactFieldAuthority field)
    {
        HashSet<string> optionIds = new(StringComparer.Ordinal);
        HashSet<string> serializedValues = new(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterCreationContactOption option in field.LegalOptions)
        {
            if (string.IsNullOrWhiteSpace(option.OptionId)
                || string.IsNullOrWhiteSpace(option.Label)
                || string.IsNullOrWhiteSpace(option.SerializedValue)
                || !optionIds.Add(option.OptionId)
                || !serializedValues.Add(option.SerializedValue)
                || option.IsEnabled != field.IsEditable
                || !StringAuthorityListIsValid(option.Blockers, requireExactSourceAnchors: false)
                || !StringAuthorityListIsValid(option.SourceAnchorIds, requireExactSourceAnchors: true)
                || !bool.TryParse(option.SerializedValue, out _))
            {
                return false;
            }
        }

        return serializedValues.SetEquals(["False", "True"])
               && serializedValues.Contains(field.SerializedValue);
    }

    private static bool ContactOptionsShapeIsValid(
        CharacterCreationContactFieldAuthority field,
        int minimum,
        int maximum)
    {
        HashSet<string> optionIds = new(StringComparer.Ordinal);
        HashSet<string> serializedValues = new(StringComparer.Ordinal);
        foreach (CharacterCreationContactOption option in field.LegalOptions)
        {
            if (string.IsNullOrWhiteSpace(option.OptionId)
                || string.IsNullOrWhiteSpace(option.Label)
                || string.IsNullOrWhiteSpace(option.SerializedValue)
                || !optionIds.Add(option.OptionId)
                || !serializedValues.Add(option.SerializedValue)
                || option.IsEnabled != field.IsEditable
                || !StringAuthorityListIsValid(option.Blockers, requireExactSourceAnchors: false)
                || !StringAuthorityListIsValid(option.SourceAnchorIds, requireExactSourceAnchors: true)
                || !int.TryParse(
                    option.SerializedValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value)
                || value < minimum
                || value > maximum)
            {
                return false;
            }
        }

        return serializedValues.Contains(field.SerializedValue);
    }

    private static bool TryExpectedContactFieldValue(
        CharacterCreationContactProjection contact,
        string fieldId,
        out string valueKind,
        out string serializedValue)
    {
        valueKind = CharacterCreationContactValueKinds.Text;
        serializedValue = fieldId switch
        {
            CharacterCreationContactFieldIds.Name => contact.Identity.Name,
            CharacterCreationContactFieldIds.Role => contact.Identity.Role,
            CharacterCreationContactFieldIds.Location => contact.Identity.Location,
            CharacterCreationContactFieldIds.Notes => contact.Identity.Notes,
            CharacterCreationContactFieldIds.CustomName => contact.Identity.CustomName,
            CharacterCreationContactFieldIds.Metatype => contact.Identity.Metatype,
            CharacterCreationContactFieldIds.Gender => contact.Identity.Gender,
            CharacterCreationContactFieldIds.Age => contact.Identity.Age,
            CharacterCreationContactFieldIds.ContactType => contact.Identity.ContactType,
            CharacterCreationContactFieldIds.PreferredPayment => contact.Identity.PreferredPayment,
            CharacterCreationContactFieldIds.HobbiesVice => contact.Identity.HobbiesVice,
            CharacterCreationContactFieldIds.PersonalLife => contact.Identity.PersonalLife,
            CharacterCreationContactFieldIds.GroupName => contact.Identity.GroupName,
            CharacterCreationContactFieldIds.Connection => Integer(contact.Connection),
            CharacterCreationContactFieldIds.Loyalty => Integer(contact.Loyalty),
            CharacterCreationContactFieldIds.Group => Boolean(contact.IsGroup),
            CharacterCreationContactFieldIds.Free => Boolean(contact.Free),
            CharacterCreationContactFieldIds.Family => Boolean(contact.Family),
            CharacterCreationContactFieldIds.Blackmail => Boolean(contact.Blackmail),
            _ => string.Empty
        };
        if (!CharacterCreationContactFieldIds.All.Contains(fieldId, StringComparer.Ordinal))
            return false;
        if (fieldId is CharacterCreationContactFieldIds.Connection or CharacterCreationContactFieldIds.Loyalty)
            valueKind = CharacterCreationContactValueKinds.Integer;
        else if (fieldId is CharacterCreationContactFieldIds.Group
                 or CharacterCreationContactFieldIds.Free
                 or CharacterCreationContactFieldIds.Family
                 or CharacterCreationContactFieldIds.Blackmail)
            valueKind = CharacterCreationContactValueKinds.Boolean;
        return serializedValue is not null;

        static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);
        static string Boolean(bool value) => value.ToString(CultureInfo.InvariantCulture);
    }

    internal static bool ContactBudgetShapeIsValid(CharacterCreationContactBudget budget)
        => !string.IsNullOrWhiteSpace(budget.BudgetId)
           && budget.Total >= 0
           && budget.Used >= 0
           && budget.Remaining == Math.Max(0, budget.Total - budget.Used)
           && budget.Overspend == Math.Max(0, budget.Used - budget.Total)
           && StringAuthorityListIsValid(budget.Blockers, requireExactSourceAnchors: false)
           && StringAuthorityListIsValid(budget.SourceAnchorIds, requireExactSourceAnchors: true);

    private static bool StringAuthorityListIsValid(
        IReadOnlyList<string> values,
        bool requireExactSourceAnchors)
        => values.All(static value => !string.IsNullOrWhiteSpace(value))
           && values.Count == values.Distinct(StringComparer.Ordinal).Count()
           && (requireExactSourceAnchors
               ? values.SequenceEqual(CharacterCreationContactSourceAnchors.All, StringComparer.Ordinal)
               : values.SequenceEqual(
                   values.OrderBy(static value => value, StringComparer.Ordinal),
                   StringComparer.Ordinal));

    private static bool HasSourceAuthority(CharacterCreationFoundationState? foundation)
        => foundation is not null
           && IsSha256(foundation.Binding.SourceDigest)
           && !foundation.AuthorityBlockers.Contains(
               CharacterCreationFoundationBlockers.EnabledSourceAuthorityRequired,
               StringComparer.Ordinal)
           && !foundation.AuthorityBlockers.Contains(
               CharacterCreationFoundationBlockers.LifeModuleCatalogAuthorityRequired,
               StringComparer.Ordinal);

    private static bool HasSourceAuthority(CharacterCreationQualitiesState? qualities)
        => qualities is not null
           && qualities.Authority.IsAuthoritative
           && qualities.Authority.Blockers.Count == 0
           && CharacterCreationQualitiesRules.IsCanonicalDigest(qualities.Authority.SourceDigest)
           && CharacterCreationQualitiesRules.IsCanonicalDigest(qualities.Authority.ProfileDigest)
           && CharacterCreationQualitiesRules.IsCanonicalDigest(qualities.Authority.GmPolicyDigest)
           && CharacterCreationQualitiesRules.IsCanonicalDigest(qualities.Authority.RuntimeDigest)
           && CharacterCreationQualitiesRules.DigestsEqual(
               qualities.Binding.AuthorityDigest,
               qualities.Authority.AuthorityDigest);

    private static bool HasSourceAuthority(CharacterCreationMagicResonanceState? magicResonance)
        => magicResonance is not null
           && CharacterCreationMagicResonanceDraftIntegrity.IsValidAuthority(
               magicResonance.Authority)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
               magicResonance.Binding.AuthorityDigest,
               magicResonance.Authority.AuthorityDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
               magicResonance.Binding.SourceInputsDigest,
               magicResonance.Authority.SourceInputsDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
               magicResonance.Binding.CustomDataInputsDigest,
               magicResonance.Authority.CustomDataInputsDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
               magicResonance.Binding.GmPolicyDigest,
               magicResonance.Authority.GmPolicyDigest)
           && CharacterCreationMagicResonanceDigest.EqualsFixedTime(
               magicResonance.Binding.RuntimeDigest,
               magicResonance.Authority.RuntimeDigest);

    private static FoundationProjectionAuthority EvaluateFoundationAuthority(
        bool usesLifeModules,
        CharacterCreationFoundationState? foundation)
    {
        if (!usesLifeModules || foundation is null || foundation.CharacterCreated)
        {
            return new FoundationProjectionAuthority(
                IsReady: false,
                HasPendingDraft: false,
                MetatypeOptions: [],
                NationalityOptions: [],
                Blockers: []);
        }

        // Canonical effect compilation is a later finalization concern. Every
        // other known or future Core blocker closes this interaction surface.
        var blockers = new List<string>(foundation.AuthorityBlockers.Where(
            static blocker => !string.Equals(
                blocker,
                CharacterCreationFoundationBlockers.LifeModuleEffectApplicationAuthorityRequired,
                StringComparison.Ordinal)));
        if (!HasSourceAuthority(foundation))
            blockers.Add(SourceAuthorityUnavailable);
        if (!foundation.LifeModuleBudget.IsExact
            || foundation.LifeModuleBudget.Blockers.Count > 0)
        {
            blockers.Add(CharacterCreationFoundationBlockers.LifeModuleBudgetAuthorityRequired);
            blockers.AddRange(foundation.LifeModuleBudget.Blockers);
        }

        IReadOnlyList<CharacterCreationLegalOption> metatypeOptions = foundation.MetatypeOptions;
        IReadOnlyList<CharacterCreationLegalOption> nationalityOptions =
            BuildNationalityOptions(foundation.NationalityOptions);
        bool hasEnabledMetatype = metatypeOptions.Any(IsEnabledOption);
        bool hasSelectableNationality = nationalityOptions.Any(IsEnabledOption)
                                        || HasMetatypeEvaluableNationality(
                                            foundation.NationalityOptions,
                                            metatypeOptions);
        if (!hasEnabledMetatype)
            blockers.Add(CharacterCreationFoundationBlockers.MetatypeLegalityAuthorityRequired);
        if (!hasSelectableNationality)
            blockers.Add(CharacterCreationFoundationBlockers.CharacterEligibilityAuthorityRequired);

        CharacterCreationFoundationDraftLedger? pendingDraft = foundation.PendingDraft;
        bool pendingDraftValid = pendingDraft is null
                                 || IsValidPendingDraft(
                                     foundation,
                                     pendingDraft,
                                     metatypeOptions,
                                     nationalityOptions,
                                     foundation.NationalityOptions);
        if (!pendingDraftValid)
        {
            blockers.Add(
                CharacterCreationFoundationBlockers.WizardStatePersistenceAuthorityRequired);
        }

        string[] normalizedBlockers = blockers
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static blocker => blocker, StringComparer.Ordinal)
            .ToArray();
        bool isReady = normalizedBlockers.Length == 0;
        return new FoundationProjectionAuthority(
            IsReady: isReady,
            HasPendingDraft: isReady && pendingDraft is not null,
            MetatypeOptions: isReady ? metatypeOptions : [],
            NationalityOptions: isReady ? nationalityOptions : [],
            Blockers: normalizedBlockers);
    }

    private static bool IsEnabledOption(CharacterCreationLegalOption option)
        => option.IsEnabled
           && string.IsNullOrWhiteSpace(option.DisableReasonKey)
           && !string.IsNullOrWhiteSpace(option.OptionId)
           && !string.IsNullOrWhiteSpace(option.Label);

    private static bool HasMetatypeEvaluableNationality(
        IReadOnlyList<LifeModuleLegalOptionDto> modules,
        IReadOnlyList<CharacterCreationLegalOption> metatypeOptions)
    {
        foreach (LifeModuleLegalOptionDto module in modules)
        {
            if (module.Versions.Count == 0)
            {
                if (CanEvaluateWithSelectedMetatype(
                        modules,
                        module,
                        null,
                        metatypeOptions))
                {
                    return true;
                }

                continue;
            }

            if (module.Versions.Any(version => CanEvaluateWithSelectedMetatype(
                    modules,
                    module,
                    version,
                    metatypeOptions)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanEvaluateWithSelectedMetatype(
        IReadOnlyList<LifeModuleLegalOptionDto> modules,
        LifeModuleLegalOptionDto module,
        LifeModuleVersionProjectionDto? version,
        IReadOnlyList<CharacterCreationLegalOption> metatypeOptions)
    {
        if (!HasExactNationalityIdentity(modules, module, version)
            || module.StageOrder != LifeModuleJourneyStageOrders.Nationality
            || !string.Equals(
                module.StageId,
                CharacterCreationLifeModuleStageIds.Nationality,
                StringComparison.OrdinalIgnoreCase)
            || module.CanRepeat
            || string.IsNullOrWhiteSpace(module.Name)
            || string.IsNullOrWhiteSpace(module.Source)
            || !HasSourceAnchors(module.SourceAnchorIds)
            || version is not null
            && (string.IsNullOrWhiteSpace(version.Label)
                || string.IsNullOrWhiteSpace(version.Source)
                || !HasSourceAnchors(version.SourceAnchorIds)))
        {
            return false;
        }

        if (!module.KarmaIsExact
            || module.KarmaCost < 0m
            || string.IsNullOrWhiteSpace(module.KarmaRaw)
            || version is not null
            && (!version.KarmaIsExact
                || version.KarmaCost < 0m
                || string.IsNullOrWhiteSpace(version.KarmaRaw)))
        {
            return false;
        }

        bool karmaIsExact = version?.KarmaIsExact ?? module.KarmaIsExact;
        decimal karmaCost = version?.KarmaCost ?? module.KarmaCost;
        string karmaRaw = version?.KarmaRaw ?? module.KarmaRaw;
        if (!karmaIsExact || karmaCost < 0m || string.IsNullOrWhiteSpace(karmaRaw))
            return false;

        LifeModuleRequirementProjectionDto[] requirements = module.Requirements
            .Concat(version?.Requirements ?? [])
            .ToArray();
        LifeModuleRequirementProjectionDto[] unresolved = requirements
            .Where(static requirement =>
                !requirement.IsMet || requirement.RequiresCharacterAuthority)
            .ToArray();
        if (unresolved.Length == 0
            || !requirements.All(static requirement =>
                string.IsNullOrWhiteSpace(requirement.DisableReasonKey)
                || string.Equals(
                    requirement.DisableReasonKey,
                    CharacterCreationFoundationBlockers.CharacterEligibilityAuthorityRequired,
                    StringComparison.Ordinal))
            || !unresolved.All(IsTypedMetatypeOneOfRequirement))
        {
            return false;
        }

        string[] blockers = module.AuthorityBlockers
            .Concat(version?.AuthorityBlockers ?? [])
            .Concat(unresolved.Select(static requirement =>
                requirement.DisableReasonKey ?? string.Empty))
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        CharacterCreationLegalOption projected = BuildNationalityOption(module, version);
        if (blockers.Length != 1
            || !string.Equals(
                blockers[0],
                CharacterCreationFoundationBlockers.CharacterEligibilityAuthorityRequired,
                StringComparison.Ordinal)
            || projected.IsEnabled
            || !string.Equals(
                projected.DisableReasonKey,
                CharacterCreationFoundationBlockers.CharacterEligibilityAuthorityRequired,
                StringComparison.Ordinal)
            || !string.Equals(
                projected.OptionId,
                module.ModuleId,
                StringComparison.Ordinal)
            || !string.Equals(
                projected.VersionId,
                version?.VersionId,
                StringComparison.Ordinal)
            || projected.Costs.Count != 1
            || projected.Costs[0].BudgetId != CharacterCreationBudgetIds.LifeModules
            || projected.Costs[0].Delta != karmaCost
            || projected.Costs[0].Delta < 0m
            || !string.Equals(projected.Costs[0].Unit, "karma", StringComparison.Ordinal))
        {
            return false;
        }

        return metatypeOptions
            .Where(IsEnabledOption)
            .Select(static option => option.Label)
            .Any(label => unresolved.All(requirement =>
                requirement.AcceptedValues.Contains(
                    label,
                    StringComparer.OrdinalIgnoreCase)));
    }

    private static bool HasExactNationalityIdentity(
        IReadOnlyList<LifeModuleLegalOptionDto> modules,
        LifeModuleLegalOptionDto module,
        LifeModuleVersionProjectionDto? version)
    {
        if (string.IsNullOrWhiteSpace(module.ModuleId)
            || modules.Count(candidate => string.Equals(
                candidate.ModuleId,
                module.ModuleId,
                StringComparison.Ordinal)) != 1)
        {
            return false;
        }

        if (module.Versions.Count == 0)
            return version is null;
        return version is not null
               && !string.IsNullOrWhiteSpace(version.VersionId)
               && module.Versions.Count(candidate => string.Equals(
                   candidate.VersionId,
                   version.VersionId,
                   StringComparison.Ordinal)) == 1;
    }

    private static bool HasSourceAnchors(IReadOnlyList<string> sourceAnchorIds)
        => sourceAnchorIds.Count > 0
           && sourceAnchorIds.All(static anchor => !string.IsNullOrWhiteSpace(anchor));

    private static bool IsTypedMetatypeOneOfRequirement(
        LifeModuleRequirementProjectionDto requirement)
        => !requirement.IsMet
           && requirement.RequiresCharacterAuthority
           && !string.IsNullOrWhiteSpace(requirement.RequirementId)
           && string.Equals(requirement.Operator, "oneof", StringComparison.OrdinalIgnoreCase)
           && string.Equals(
               requirement.SubjectKind,
               "metatype",
               StringComparison.OrdinalIgnoreCase)
           && requirement.AcceptedValues.Count > 0
           && requirement.AcceptedValues.All(static value =>
               !string.IsNullOrWhiteSpace(value));

    private static bool IsValidPendingDraft(
        CharacterCreationFoundationState foundation,
        CharacterCreationFoundationDraftLedger draft,
        IReadOnlyList<CharacterCreationLegalOption> metatypeOptions,
        IReadOnlyList<CharacterCreationLegalOption> nationalityOptions,
        IReadOnlyList<LifeModuleLegalOptionDto> nationalityCatalog)
        => string.Equals(
               draft.Schema,
               CharacterCreationFoundationSchemas.DraftLedgerV1,
               StringComparison.Ordinal)
           && string.Equals(
               draft.WorkspaceId.Value,
               foundation.Binding.WorkspaceId.Value,
               StringComparison.Ordinal)
           && draft.DraftRevision > 0
           && draft.BaseContentRevision >= 0
           && draft.BaseContentRevision < foundation.Binding.ContentRevision
           && string.Equals(
               draft.BaseRawCharacterXmlDigest,
               foundation.Binding.RawCharacterXmlDigest,
               StringComparison.Ordinal)
           && string.Equals(
               draft.SourceDigest,
               foundation.Binding.SourceDigest,
               StringComparison.Ordinal)
           && IsSha256(draft.DraftDigest)
           && !draft.CharacterEffectsApplied
           && string.Equals(
               draft.CompilationStatus,
               CharacterCreationFoundationDraftStatuses.PendingFinalization,
               StringComparison.Ordinal)
           && draft.RequirementEvaluations.All(static requirement =>
               requirement.IsMet && !requirement.RequiresCharacterAuthority)
           && metatypeOptions.Any(option =>
               IsEnabledOption(option)
               && (string.Equals(
                       option.OptionId,
                       draft.RequestedMetatype,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       option.Label,
                       draft.RequestedMetatype,
                       StringComparison.OrdinalIgnoreCase)))
           && (nationalityOptions.Any(option =>
                   IsEnabledOption(option)
                   && SelectionMatches(option, draft.Selection))
               || PendingDraftMatchesMetatypeEvaluableCandidate(
                   nationalityCatalog,
                   metatypeOptions,
                   draft));

    private static bool PendingDraftMatchesMetatypeEvaluableCandidate(
        IReadOnlyList<LifeModuleLegalOptionDto> modules,
        IReadOnlyList<CharacterCreationLegalOption> metatypeOptions,
        CharacterCreationFoundationDraftLedger draft)
    {
        LifeModuleLegalOptionDto[] matchingModules = modules
            .Where(candidate => string.Equals(
                candidate.ModuleId,
                draft.Selection.ModuleId,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (matchingModules.Length != 1)
            return false;
        LifeModuleLegalOptionDto module = matchingModules[0];

        LifeModuleVersionProjectionDto[] matchingVersions = module.Versions
            .Where(candidate => string.Equals(
                candidate.VersionId,
                draft.Selection.VersionId,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (module.Versions.Count == 0
            && !string.IsNullOrWhiteSpace(draft.Selection.VersionId))
        {
            return false;
        }

        LifeModuleVersionProjectionDto? version = module.Versions.Count == 0
            ? null
            : matchingVersions.Length == 1
                ? matchingVersions[0]
                : null;
        if (!CanEvaluateWithSelectedMetatype(
                modules,
                module,
                version,
                metatypeOptions)
            || !metatypeOptions.Any(option =>
                IsEnabledOption(option)
                && (string.Equals(
                        option.OptionId,
                        draft.RequestedMetatype,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        option.Label,
                        draft.RequestedMetatype,
                        StringComparison.OrdinalIgnoreCase))))
        {
            return false;
        }

        LifeModuleRequirementProjectionDto[] rawRequirements = module.Requirements
            .Concat(version?.Requirements ?? [])
            .ToArray();
        return rawRequirements.Length == draft.RequirementEvaluations.Count
               && rawRequirements.All(raw => draft.RequirementEvaluations.Count(evaluated =>
                   string.Equals(
                       evaluated.RequirementId,
                       raw.RequirementId,
                       StringComparison.Ordinal)) == 1)
               && rawRequirements
                   .Where(static requirement =>
                       !requirement.IsMet || requirement.RequiresCharacterAuthority)
                   .All(requirement => requirement.AcceptedValues.Contains(
                       draft.RequestedMetatype,
                       StringComparer.OrdinalIgnoreCase));
    }

    private static bool SelectionMatches(
        CharacterCreationLegalOption option,
        CharacterCreationFoundationSelection selection)
        => string.Equals(option.OptionId, selection.ModuleId, StringComparison.Ordinal)
           && string.Equals(option.VersionId, selection.VersionId, StringComparison.Ordinal);

    private static IReadOnlyList<string> BuildWarnings(
        bool hasSourceAuthority,
        FoundationProjectionAuthority foundationAuthority)
    {
        if (foundationAuthority.HasPendingDraft)
        {
            return
            [
                "creation-wizard-foundation-draft-resumable",
                "creation-wizard-character-effects-pending-finalization"
            ];
        }

        if (foundationAuthority.IsReady)
            return [];
        return hasSourceAuthority
            ? [
                "creation-wizard-read-only-foundation",
                "creation-wizard-nationality-options-read-only",
                "creation-wizard-confirm-authority-unavailable"
            ]
            : ["creation-wizard-read-only-foundation"];
    }

    private static IReadOnlyList<CharacterCreationLegalOption> BuildNationalityOptions(
        IReadOnlyList<LifeModuleLegalOptionDto> modules)
    {
        var options = new List<CharacterCreationLegalOption>();
        foreach (LifeModuleLegalOptionDto module in modules)
        {
            if (module.Versions.Count == 0)
            {
                options.Add(BuildNationalityOption(module, null));
                continue;
            }

            options.AddRange(module.Versions.Select(version =>
                BuildNationalityOption(module, version)));
        }

        return options
            .OrderBy(option => option.Label, StringComparer.Ordinal)
            .ThenBy(option => option.OptionId, StringComparer.Ordinal)
            .ThenBy(option => option.VersionId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CharacterCreationLegalOption BuildNationalityOption(
        LifeModuleLegalOptionDto module,
        LifeModuleVersionProjectionDto? version)
    {
        bool karmaIsExact = version?.KarmaIsExact ?? module.KarmaIsExact;
        decimal karma = version?.KarmaCost ?? module.KarmaCost;
        string[] blockers = module.AuthorityBlockers
            .Concat(version?.AuthorityBlockers ?? [])
            .Concat(module.Requirements.SelectMany(RequirementBlockers))
            .Concat(version?.Requirements.SelectMany(RequirementBlockers) ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        bool isEnabled = module.IsEnabled
                         && (version?.IsEnabled ?? true)
                         && karmaIsExact
                         && blockers.Length == 0;
        IReadOnlyList<string> sourceAnchors = module.SourceAnchorIds
            .Concat(version?.SourceAnchorIds ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new CharacterCreationLegalOption(
            OptionId: module.ModuleId,
            Label: version is null || string.IsNullOrWhiteSpace(version.Label)
                ? module.Name
                : $"{module.Name}: {version.Label}",
            IsEnabled: isEnabled,
            DisableReasonKey: isEnabled
                ? null
                : blockers.FirstOrDefault() ?? LegalOptionsAuthorityUnavailable,
            DisableReasonArguments: new Dictionary<string, string>(),
            Costs: karmaIsExact
                ? [new CharacterCreationChoiceCost(
                    CharacterCreationBudgetIds.LifeModules,
                    karma,
                    "karma")]
                : [],
            Consequences: [],
            SourceAnchorIds: sourceAnchors,
            SourceId: version?.Source ?? module.Source,
            SourcePage: version?.Page ?? module.Page,
            VersionId: version?.VersionId);
    }

    private static IEnumerable<string> RequirementBlockers(
        LifeModuleRequirementProjectionDto requirement)
    {
        if (!requirement.IsMet || requirement.RequiresCharacterAuthority)
            yield return requirement.DisableReasonKey ?? LegalOptionsAuthorityUnavailable;
    }

    private static bool IsSha256(string? value)
        => value is { Length: 71 }
           && value.StartsWith("sha256:", StringComparison.Ordinal)
           && value.AsSpan(7).ToString().All(Uri.IsHexDigit);

    private static bool IsLowerSha256(string? value)
        => value is { Length: 71 }
           && value.StartsWith("sha256:", StringComparison.Ordinal)
           && value.AsSpan(7).ToString().All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsLowerRawSha256(string? value)
        => value is { Length: 64 }
           && value.All(static character =>
               character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static IReadOnlyList<string> CombineBlockers(
        IReadOnlyList<string> required,
        IReadOnlyList<string>? additional)
        => required
            .Concat(additional ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string ComputeContentDigest(WorkspaceDocument? document)
        => document is null
            ? string.Empty
            : Sha256(Encoding.UTF8.GetBytes(document.Content));

    private static string ComputeSnapshotDigest(CharacterCreationWizardSnapshot snapshot)
        => Sha256(JsonSerializer.SerializeToUtf8Bytes(snapshot with { SnapshotDigest = string.Empty }));

    private static string Sha256(byte[] bytes)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";

    private sealed record FoundationProjectionAuthority(
        bool IsReady,
        bool HasPendingDraft,
        IReadOnlyList<CharacterCreationLegalOption> MetatypeOptions,
        IReadOnlyList<CharacterCreationLegalOption> NationalityOptions,
        IReadOnlyList<string> Blockers);

    private sealed record ContactsProjectionAuthority(
        bool IsReady,
        IReadOnlyList<string> Blockers);

    private sealed record QualitiesProjectionAuthority(
        bool IsReady,
        bool HasPendingDraft,
        IReadOnlyList<CharacterCreationLegalOption> Options,
        IReadOnlyList<string> Blockers);

    private sealed record MagicResonanceProjectionAuthority(
        bool IsReady,
        bool HasPendingDraft,
        IReadOnlyList<CharacterCreationLegalOption> Options,
        IReadOnlyList<string> Blockers,
        CharacterCreationMagicResonanceEditorState? Editor);
}
