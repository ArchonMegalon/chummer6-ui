using System;
using System.Collections.Generic;
using Chummer.Avalonia.Creation;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// UX-05: Core Quality blocker reasons must be surfaced with the selection and
/// budget summary rather than a generic "blocked" notice. DescribeBlockers
/// aggregates the preview blockers plus both quality-budget blockers.
/// </summary>
[TestClass]
public sealed class CreationQualitiesReviewBlockersTests
{
    private static CharacterWorkspaceId WorkspaceId() => new("ws-ux05");

    private static CharacterCreationQualitiesBinding Binding() => new(
        WorkspaceId(),
        ContentRevision: 7,
        SavedRevision: 6,
        RawCharacterXmlDigest: "raw-digest",
        AuxiliaryStateDigest: "aux-digest",
        PrerequisiteDraftRevision: 3,
        PrerequisiteDraftDigest: "pre-digest",
        AttributesDraftRevision: 4,
        AttributesDraftDigest: "attr-digest",
        RulesetId: "sr5",
        BuildMethod: "priority",
        CharacterCreated: false,
        CreationKarmaTotal: 50,
        CreationKarmaUsedBeforeQualities: 10,
        AuthorityDigest: "authority-digest",
        RuntimeDigest: "runtime-digest");

    private static CharacterCreationQualitySelection Selection(string id, string name, int karma, string type)
        => new(
            OptionId: id,
            SourceId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SelectionKey: id,
            Name: name,
            Type: type == "positive" ? CharacterCreationQualityType.Positive : CharacterCreationQualityType.Negative,
            Rating: 1,
            KarmaCost: karma,
            IsMetagenic: false,
            CountsAgainstQualityLimit: true,
            CountsAgainstKarma: true,
            IsFreeOrGranted: false,
            FollowUpChoiceId: null,
            FollowUpChoiceLabel: null,
            SourceAnchorIds: Array.Empty<string>(),
            SourceNodeXml: null,
            SourceNodeDigest: null,
            OptionDigest: "opt-" + id);

    private static CharacterCreationQualitiesPreview Preview(
        IReadOnlyList<CharacterCreationQualitySelection> selections,
        IReadOnlyList<string> blockers,
        IReadOnlyList<string> positiveBlockers,
        IReadOnlyList<string> negativeBlockers,
        int positiveUsed,
        int negativeUsed,
        int karmaRemaining)
        => new(
            Schema: "character-creation-qualities-preview-v1",
            Binding: Binding(),
            AuthorityDigest: "authority-digest",
            Selections: selections,
            GrantedQualities: Array.Empty<CharacterCreationGrantedQuality>(),
            PositiveQualityBudget: new CharacterCreationQualitiesBudget(
                Total: 50, Used: positiveUsed, Remaining: 50 - positiveUsed, MayExceed: false, Blockers: positiveBlockers),
            NegativeQualityBudget: new CharacterCreationQualitiesBudget(
                Total: 25, Used: negativeUsed, Remaining: 25 - negativeUsed, MayExceed: false, Blockers: negativeBlockers),
            MetagenicPositiveKarma: 0,
            MetagenicNegativeKarma: 0,
            KarmaRemaining: karmaRemaining,
            SourceAnchorIds: Array.Empty<string>(),
            Blockers: blockers,
            RequiresExplicitConfirmation: blockers.Count > 0,
            CanConfirm: blockers.Count == 0 && positiveBlockers.Count == 0 && negativeBlockers.Count == 0,
            PreviewDigest: "preview-digest");

    private static CharacterCreationQualitiesState State(CharacterCreationQualitiesPreview preview)
        => new(
            Schema: "character-creation-qualities-state-v1",
            Binding: Binding(),
            Authority: null,
            PrerequisiteDraft: null,
            AttributesDraft: null,
            PendingDraft: null,
            Preview: preview,
            Blockers: preview.Blockers,
            CanEdit: true,
            SnapshotDigest: "snapshot-digest");

    private static CharacterCreationQualitiesSelectionReview Review(
        CharacterCreationQualitiesPreview beforePreview,
        CharacterCreationQualitiesPreview afterPreview)
        => new(
            Before: State(beforePreview),
            Preview: afterPreview,
            SelectedOptionIds: new[] { "opt-1" },
            IdempotencyKey: "desktop-qualities:ws-ux05:preview-digest",
            TransactionId: Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [TestMethod]
    public void DescribeBlockers_lists_every_reason_from_preview_and_both_budgets()
    {
        var after = Preview(
            selections: new[] { Selection("opt-1", "Toughness", 8, "positive") },
            blockers: new[] { "quality-karma-limit-exceeded" },
            positiveBlockers: new[] { "positive-quality-budget-exhausted" },
            negativeBlockers: new[] { "negative-quality-budget-exhausted" },
            positiveUsed: 50, negativeUsed: 25, karmaRemaining: -13);
        var before = Preview(
            selections: Array.Empty<CharacterCreationQualitySelection>(),
            blockers: Array.Empty<string>(),
            positiveBlockers: Array.Empty<string>(),
            negativeBlockers: Array.Empty<string>(),
            positiveUsed: 0, negativeUsed: 0, karmaRemaining: 40);

        var text = CreationQualitiesReviewWindow.DescribeBlockers(Review(before, after));

        StringAssert.Contains(text, "Rule blockers reported by Core (3)");
        StringAssert.Contains(text, "quality-karma-limit-exceeded");
        StringAssert.Contains(text, "positive-quality-budget-exhausted");
        StringAssert.Contains(text, "negative-quality-budget-exhausted");
    }

    [TestMethod]
    public void DescribeBlockers_reports_clean_selection_without_blockers()
    {
        var after = Preview(
            selections: new[] { Selection("opt-1", "Toughness", 8, "positive") },
            blockers: Array.Empty<string>(),
            positiveBlockers: Array.Empty<string>(),
            negativeBlockers: Array.Empty<string>(),
            positiveUsed: 8, negativeUsed: 0, karmaRemaining: 32);
        var before = Preview(
            selections: Array.Empty<CharacterCreationQualitySelection>(),
            blockers: Array.Empty<string>(),
            positiveBlockers: Array.Empty<string>(),
            negativeBlockers: Array.Empty<string>(),
            positiveUsed: 0, negativeUsed: 0, karmaRemaining: 40);

        Assert.AreEqual("No rule blockers.", CreationQualitiesReviewWindow.DescribeBlockers(Review(before, after)));
    }

    [TestMethod]
    public void Describe_shows_selections_budgets_and_the_blocker_reasons_together()
    {
        var after = Preview(
            selections: new[] { Selection("opt-1", "Toughness", 8, "positive") },
            blockers: new[] { "quality-karma-limit-exceeded" },
            positiveBlockers: Array.Empty<string>(),
            negativeBlockers: Array.Empty<string>(),
            positiveUsed: 8, negativeUsed: 0, karmaRemaining: 32);
        var before = Preview(
            selections: Array.Empty<CharacterCreationQualitySelection>(),
            blockers: Array.Empty<string>(),
            positiveBlockers: Array.Empty<string>(),
            negativeBlockers: Array.Empty<string>(),
            positiveUsed: 0, negativeUsed: 0, karmaRemaining: 40);

        var text = CreationQualitiesReviewWindow.Describe(Review(before, after));

        // Selection summary.
        StringAssert.Contains(text, "Added: Toughness (8 Karma)");
        StringAssert.Contains(text, "Complete selection after saving: Toughness (8 Karma)");
        // Budget summary.
        StringAssert.Contains(text, "Positive quality Karma: 0 → 8 / 50");
        StringAssert.Contains(text, "Karma remaining: 40 → 32");
        // Blocker reasons, not a generic notice.
        StringAssert.Contains(text, "Rule blockers reported by Core (1): quality-karma-limit-exceeded");
        Assert.IsFalse(text.Contains("reports blockers for this selection", StringComparison.Ordinal),
            $"Generic blocker notice must be gone, got: {text}");
    }
}
