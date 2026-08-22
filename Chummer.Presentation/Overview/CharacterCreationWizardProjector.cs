using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
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
        WorkspaceOverviewLoadResult loadedOverview,
        CharacterCreationFoundationState? foundation = null)
    {
        ArgumentNullException.ThrowIfNull(loadedOverview);
        if (foundation is not null
            && !MatchesLoadedOverview(workspaceId, loadedOverview, foundation))
        {
            foundation = null;
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
        bool hasSourceAuthority = HasSourceAuthority(foundation);
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

        IReadOnlyList<CharacterCreationBudgetState> budgets = BuildBudgets(
            loadedOverview.Build,
            usesLifeModules,
            hasSourceAuthority ? foundation : null);
        completionBlockers.AddRange(budgets.SelectMany(static budget => budget.Blockers));

        IReadOnlyList<CharacterCreationWizardStageState> steps = BuildSteps(
            loadedOverview.Profile,
            buildMethod,
            methodAuthoritative,
            usesLifeModules,
            foundation);
        string activeStepId = !methodAuthoritative
            ? CharacterCreationWizardStepIds.Method
            : CharacterCreationWizardStepIds.Foundation;
        Dictionary<string, IReadOnlyList<CharacterCreationLegalOption>> legalOptions =
            steps.ToDictionary(
                static step => step.StepId,
                static _ => (IReadOnlyList<CharacterCreationLegalOption>)[],
                StringComparer.Ordinal);
        if (usesLifeModules && hasSourceAuthority && foundation is not null)
        {
            legalOptions[CharacterCreationWizardStepIds.LifeModules] =
                BuildNationalityOptions(foundation.NationalityOptions);
        }

        CharacterCreationWizardSnapshot snapshot = new(
            Schema: CharacterCreationWizardSchemas.SnapshotV1,
            WorkspaceId: workspaceId.Value,
            WorkspaceRevision: loadedOverview.ContentRevision,
            ContentDigest: contentDigest,
            SourceDigest: hasSourceAuthority ? foundation!.Binding.SourceDigest : string.Empty,
            RulesetId: rulesetId,
            RuntimeFingerprint: string.Empty,
            BuildMethod: buildMethod,
            CharacterCreated: loadedOverview.Profile.Created,
            ActiveStepId: activeStepId,
            Steps: steps,
            Budgets: budgets,
            LegalOptionsByStep: legalOptions,
            CompletionBlockers: completionBlockers.Distinct(StringComparer.Ordinal).ToArray(),
            Warnings: hasSourceAuthority
                ? [
                    "creation-wizard-read-only-foundation",
                    "creation-wizard-nationality-options-read-only",
                    "creation-wizard-confirm-authority-unavailable"
                ]
                : ["creation-wizard-read-only-foundation"],
            CanFinalize: false,
            SnapshotDigest: string.Empty);

        return snapshot with { SnapshotDigest = ComputeSnapshotDigest(snapshot) };
    }

    private static IReadOnlyList<CharacterCreationWizardStageState> BuildSteps(
        CharacterProfileSection profile,
        string buildMethod,
        bool methodAuthoritative,
        bool usesLifeModules,
        CharacterCreationFoundationState? foundation)
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
                CharacterCreationWizardStepStatuses.Blocked,
                isRequired: true,
                isAvailable: false,
                isComplete: false,
                budgetIds: [],
                blockers: CombineBlockers(
                    [LegalOptionsAuthorityUnavailable],
                    foundation?.AuthorityBlockers),
                warnings: string.IsNullOrWhiteSpace(profile.Metatype)
                    ? []
                    : ["creation-wizard-existing-metatype-requires-authoritative-review"],
                legalNextStepIds: []),
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
                blockers: usesLifeModules
                    ? CombineBlockers(
                        [LifeModuleAuthorityUnavailable],
                        foundation?.AuthorityBlockers)
                    : [],
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
        bool usesLifeModules,
        CharacterCreationFoundationState? foundation)
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
               && foundation.CharacterCreated == loadedOverview.Profile.Created;
    }

    private static bool HasSourceAuthority(CharacterCreationFoundationState? foundation)
        => foundation is not null
           && IsSha256(foundation.Binding.SourceDigest)
           && !foundation.AuthorityBlockers.Contains(
               CharacterCreationFoundationBlockers.EnabledSourceAuthorityRequired,
               StringComparer.Ordinal)
           && !foundation.AuthorityBlockers.Contains(
               CharacterCreationFoundationBlockers.LifeModuleCatalogAuthorityRequired,
               StringComparer.Ordinal);

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
}
