using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.Characters;
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
    public const string BuildMethodUnavailable = "creation-wizard-build-method-unavailable";
    public const string BuildMethodMismatch = "creation-wizard-build-method-mismatch";

    public static CharacterCreationWizardSnapshot Project(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loadedOverview)
    {
        ArgumentNullException.ThrowIfNull(loadedOverview);

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
        List<string> completionBlockers =
        [
            SourceAuthorityUnavailable,
            RuntimeAuthorityUnavailable,
            BuildGhostContextUnavailable,
            LegalOptionsAuthorityUnavailable,
            FinalizationAuthorityUnavailable
        ];
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

        IReadOnlyList<CharacterCreationBudgetState> budgets = BuildBudgets(
            loadedOverview.Build,
            usesLifeModules);
        completionBlockers.AddRange(budgets.SelectMany(static budget => budget.Blockers));

        IReadOnlyList<CharacterCreationWizardStageState> steps = BuildSteps(
            loadedOverview.Profile,
            buildMethod,
            methodAuthoritative,
            usesLifeModules);
        string activeStepId = !methodAuthoritative
            ? CharacterCreationWizardStepIds.Method
            : usesLifeModules
                ? CharacterCreationWizardStepIds.LifeModules
                : CharacterCreationWizardStepIds.Foundation;
        IReadOnlyDictionary<string, IReadOnlyList<CharacterCreationLegalOption>> legalOptions =
            steps.ToDictionary(
                static step => step.StepId,
                static _ => (IReadOnlyList<CharacterCreationLegalOption>)[],
                StringComparer.Ordinal);

        CharacterCreationWizardSnapshot snapshot = new(
            Schema: CharacterCreationWizardSchemas.SnapshotV1,
            WorkspaceId: workspaceId.Value,
            WorkspaceRevision: loadedOverview.ContentRevision,
            ContentDigest: contentDigest,
            SourceDigest: string.Empty,
            RulesetId: rulesetId,
            RuntimeFingerprint: string.Empty,
            BuildMethod: buildMethod,
            CharacterCreated: loadedOverview.Profile.Created,
            ActiveStepId: activeStepId,
            Steps: steps,
            Budgets: budgets,
            LegalOptionsByStep: legalOptions,
            CompletionBlockers: completionBlockers.Distinct(StringComparer.Ordinal).ToArray(),
            Warnings: ["creation-wizard-read-only-foundation"],
            CanFinalize: false,
            SnapshotDigest: string.Empty);

        return snapshot with { SnapshotDigest = ComputeSnapshotDigest(snapshot) };
    }

    private static IReadOnlyList<CharacterCreationWizardStageState> BuildSteps(
        CharacterProfileSection profile,
        string buildMethod,
        bool methodAuthoritative,
        bool usesLifeModules)
    {
        string foundationNext = usesLifeModules
            ? CharacterCreationWizardStepIds.LifeModules
            : CharacterCreationWizardStepIds.Attributes;
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
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds: [],
                blockers: [LegalOptionsAuthorityUnavailable],
                warnings: string.IsNullOrWhiteSpace(profile.Metatype)
                    ? []
                    : ["creation-wizard-existing-metatype-requires-authoritative-review"],
                legalNextStepIds: [foundationNext]),
            Stage(
                CharacterCreationWizardStepIds.LifeModules,
                "Life modules",
                usesLifeModules
                    ? CharacterCreationWizardStepStatuses.Blocked
                    : CharacterCreationWizardStepStatuses.NotStarted,
                isRequired: usesLifeModules,
                isAvailable: false,
                isComplete: false,
                budgetIds: usesLifeModules ? [CharacterCreationBudgetIds.LifeModules] : [],
                blockers: usesLifeModules ? [LifeModuleAuthorityUnavailable] : [],
                legalNextStepIds: []),
            Stage(
                CharacterCreationWizardStepIds.Attributes,
                "Attributes",
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds: [CharacterCreationBudgetIds.NormalAttributes, CharacterCreationBudgetIds.SpecialAttributes],
                blockers: [LegalOptionsAuthorityUnavailable],
                legalNextStepIds: [CharacterCreationWizardStepIds.Qualities]),
            Stage(
                CharacterCreationWizardStepIds.Qualities,
                "Qualities",
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds: [CharacterCreationBudgetIds.Karma],
                blockers: [LegalOptionsAuthorityUnavailable],
                legalNextStepIds: [CharacterCreationWizardStepIds.Skills]),
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
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: profile.Adept || profile.Magician || profile.Technomancer || profile.AI,
                isAvailable: false,
                isComplete: false,
                budgetIds: [CharacterCreationBudgetIds.SpellsFormsPrograms],
                blockers: [LegalOptionsAuthorityUnavailable],
                legalNextStepIds: [CharacterCreationWizardStepIds.Resources]),
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
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds: [CharacterCreationBudgetIds.Contacts, CharacterCreationBudgetIds.Resources],
                blockers: [LegalOptionsAuthorityUnavailable],
                legalNextStepIds: [CharacterCreationWizardStepIds.IdentityStory]),
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
        bool usesLifeModules)
    {
        List<CharacterCreationBudgetState> budgets =
        [
            UnknownBudget(CharacterCreationBudgetIds.Karma, "Karma", "karma"),
            UnknownBudget(CharacterCreationBudgetIds.NormalAttributes, "Normal attributes", "points"),
            UnknownBudget(CharacterCreationBudgetIds.SpecialAttributes, "Special attributes", "points"),
            UnknownBudget(CharacterCreationBudgetIds.ActiveSkills, "Active skills", "points"),
            UnknownBudget(CharacterCreationBudgetIds.SkillGroups, "Skill groups", "points"),
            UnknownBudget(CharacterCreationBudgetIds.KnowledgeSkills, "Knowledge skills", "points"),
            ContactBudget(build),
            UnknownBudget(CharacterCreationBudgetIds.Resources, "Resources", "nuyen"),
            UnknownBudget(CharacterCreationBudgetIds.SpellsFormsPrograms, "Spells, forms, and programs", "choices")
        ];
        if (usesLifeModules)
        {
            budgets.Add(UnknownBudget(
                CharacterCreationBudgetIds.LifeModules,
                "Life modules",
                "modules",
                LifeModuleAuthorityUnavailable));
        }

        return budgets;
    }

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
        string? additionalBlocker = null)
    {
        List<string> blockers = [$"creation-wizard-budget-authority-unavailable:{budgetId}"];
        if (!string.IsNullOrWhiteSpace(additionalBlocker))
        {
            blockers.Add(additionalBlocker);
        }

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

    private static string ComputeContentDigest(WorkspaceDocument? document)
        => document is null
            ? string.Empty
            : Sha256(Encoding.UTF8.GetBytes(document.Content));

    private static string ComputeSnapshotDigest(CharacterCreationWizardSnapshot snapshot)
        => Sha256(JsonSerializer.SerializeToUtf8Bytes(snapshot with { SnapshotDigest = string.Empty }));

    private static string Sha256(byte[] bytes)
        => $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
}
