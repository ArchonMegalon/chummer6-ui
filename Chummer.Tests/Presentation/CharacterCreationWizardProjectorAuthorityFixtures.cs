// Fixture builders for CharacterCreationWizardProjectorAuthorityTests (partial-class continuation).
// Digest chains mirror the proven CharacterCreationResourcesInteractionPresenterTests pattern.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

public partial class CharacterCreationWizardProjectorAuthorityTests
{
    private static string Digest(char value) => "sha256:" + new string(value, 64);

    private static string RawDigest(string value) => "sha256:"
        + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));


    private static WorkspaceOverviewLoadResult CreateLoadedOverview()
    {
        return new WorkspaceOverviewLoadResult(
            new CharacterProfileSection(
                "Fastpass", "Fastpass", string.Empty, "Human", string.Empty, string.Empty,
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
            7, 7,
            new WorkspaceDocument(DocumentContent, RulesetDefaults.Sr5));
    }

    private static CharacterCreationSkillsState CreateSkillsState(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loaded,
        string[]? authorityBlockers = null)
    {
        var authority = new CharacterCreationSkillsAuthority(
            CharacterCreationSkillsSchemas.AuthorityV1,
            "default",
            EffectiveSkillsInputsDigest: string.Empty,
            RawProfileInputsDigest: Digest('a'),
            KnowledgePointsExpression: "LOG + INT",
            MaxActiveSkillRatingCreate: 6,
            MaxKnowledgeSkillRatingCreate: 6,
            MaxSkillGroupRatingCreate: 4,
            BaseNativeLanguageLimit: 1,
            UsePointsOnBrokenGroups: true,
            StrictSkillGroupsInCreateMode: true,
            SpecializationsBreakSkillGroups: true,
            ActiveSkills: [],
            KnowledgeSkills: [],
            SkillGroups: [],
            KnowledgePointContributions: [],
            SourceAnchorIds: ["skills-authority:test"],
            Blockers: authorityBlockers ?? [],
            IsAuthoritative: true,
            RuntimeDigest: Digest('b'),
            AuthorityDigest: Digest('c'));
        var binding = new CharacterCreationSkillsBinding(
            workspaceId,
            loaded.ContentRevision,
            loaded.SavedRevision,
            RawDigest(DocumentContent),
            AuxiliaryStateDigest: string.Empty,
            PrerequisiteDraftRevision: 0,
            PrerequisiteDraftDigest: string.Empty,
            PrerequisiteAuthorityDigest: string.Empty,
            AttributesDraftRevision: 0,
            AttributesDraftDigest: string.Empty,
            SkillsAuthorityDigest: authority.EffectiveSkillsInputsDigest,
            RuntimeDigest: Digest('b'),
            ContributionInputsDigest: Digest('d'));
        var state = new CharacterCreationSkillsState(
            CharacterCreationSkillsSchemas.SnapshotV1,
            binding,
            authority,
            PrerequisiteDraft: null,
            AttributesDraft: null,
            PendingDraft: null,
            Skills: [],
            SkillGroups: [],
            KnowledgePointContributions: [],
            ActiveSkillPointBudget: new CharacterCreationBudgetState(
                CharacterCreationBudgetIds.ActiveSkills, "Active Skills", 46m, 0m, 46m, true, [], "points"),
            SkillGroupPointBudget: new CharacterCreationBudgetState(
                CharacterCreationBudgetIds.SkillGroups, "Skill Groups", 10m, 0m, 10m, true, [], "groups"),
            KnowledgeSkillPointBudget: new CharacterCreationBudgetState(
                CharacterCreationBudgetIds.KnowledgeSkills, "Knowledge Skills", 0m, 0m, 0m, true, [], "points"),
            IntuitionUnaugmented: 0,
            LogicUnaugmented: 0,
            Blockers: authorityBlockers ?? [],
            CanEdit: authorityBlockers is null or [],
            SnapshotDigest: string.Empty);
        return state with
        {
            SnapshotDigest = CharacterCreationSkillsDigest.Compute(state with { SnapshotDigest = string.Empty })
        };
    }

    private static CharacterCreationResourcesState CreateResourcesState(
        CharacterWorkspaceId workspaceId,
        WorkspaceOverviewLoadResult loaded)
    {
        var priorityCandidate = new CharacterCreationResourcePriorityOption(
            "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
            "D",
            50_000m,
            Digest('1'),
            CharacterCreationResourcesSourceAnchors.All,
            OptionDigest: string.Empty);
        CharacterCreationResourcePriorityOption priority = priorityCandidate with
        {
            OptionDigest = CharacterCreationResourcesRules.ComputePriorityOptionDigest(priorityCandidate)
        };
        var authorityCandidate = new CharacterCreationResourcesAuthority(
            CharacterCreationResourcesSchemas.AuthorityV1,
            RulesetDefaults.Sr5,
            "default",
            CharacterCreationBuildMethods.Priority,
            KarmaToNuyenRate: 2_000m,
            MaximumKarmaInvestment: 10,
            NuyenCarryover: 5_000m,
            MaximumAvailability: 12,
            UnrestrictedNuyen: false,
            PriorityOptions: [priority],
            SourceAnchorIds: CharacterCreationResourcesSourceAnchors.All,
            Blockers: [],
            IsAuthoritative: true,
            SourceDigest: Digest('2'),
            ProfileDigest: Digest('3'),
            RulesDigest: Digest('4'),
            RuntimeDigest: Digest('5'),
            AuthorityDigest: string.Empty);
        CharacterCreationResourcesAuthority authority = authorityCandidate with
        {
            AuthorityDigest = CharacterCreationResourcesRules.ComputeAuthorityDigest(authorityCandidate)
        };
        CharacterCreationResourceAllocationOption optionA = new(
            "priority:a",
            0,
            0m,
            45_000m,
            IsEnabled: true,
            [],
            CharacterCreationResourcesSourceAnchors.All,
            string.Empty);
        CharacterCreationResourceAllocationOption optionB = new(
            "priority:b",
            5,
            10_000m,
            50_000m,
            IsEnabled: true,
            [],
            CharacterCreationResourcesSourceAnchors.All,
            string.Empty);
        var stateCandidate = new CharacterCreationResourcesState(
            CharacterCreationResourcesSchemas.StateV1,
            "creation-wizard-resources",
            new CharacterCreationResourcesBinding(
                workspaceId,
                loaded.SavedRevision,
                loaded.ContentRevision,
                loaded.SavedRevision,
                RawDigest(DocumentContent),
                AuxiliaryStateDigest: string.Empty,
                PrerequisiteDraftRevision: 0,
                PrerequisiteDraftDigest: string.Empty,
                AuthorityDigest: authority.AuthorityDigest,
                SourceDigest: authority.SourceDigest,
                RulesDigest: authority.RulesDigest,
                RuntimeDigest: authority.RuntimeDigest),
            authority,
            PrerequisiteDraft: null,
            PendingDraft: null,
            Options: [optionA, optionB],
            Budget: new CharacterCreationResourcesBudget(
                45_000m,
                0,
                0m,
                45_000m,
                KnownPurchaseCost: 0m,
                RemainingNuyen: 45_000m,
                Overspend: 0m,
                CarryoverLimit: 5_000m,
                CarryoverExcess: 0m,
                IsExact: true,
                [],
                CharacterCreationResourcesSourceAnchors.All),
            Blockers: [],
            CanEdit: true,
            SnapshotDigest: string.Empty);
        return stateCandidate with
        {
            SnapshotDigest = CharacterCreationResourcesRules.ComputeStateDigest(stateCandidate)
        };
    }

    private static void AssertStage(
        CharacterCreationWizardSnapshot snapshot,
        string stepId,
        string status)
    {
        Assert.IsTrue(snapshot.Steps.Any(stage => stage.StepId == stepId && stage.Status == status));
    }
}
