using System.Security.Cryptography;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Infrastructure.Files;
using Chummer.Infrastructure.Owners;
using Chummer.Infrastructure.Workspaces;
using Chummer.Infrastructure.Xml;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

/// <summary>Real SR5 bootstrap, source authority and owner-bound read. No invented
/// drafts, prices, capabilities or whole-review admission are used by these tests.</summary>
internal sealed class CompletionRecommendationTestFixture : IDisposable
{
    private readonly string _directory;
    public FileWorkspaceStore Store { get; }
    public ICharacterFileQueries Queries { get; }
    public ICharacterSourceDataResolver Resolver { get; }
    public CharacterWorkspaceId WorkspaceId { get; }
    public LocalOwnerContextAccessor Owners { get; } = new();

    private CompletionRecommendationTestFixture(string directory, string contentRoot, string? zeroRatedTalentGroup,
        string? magicTalent, XElement? originalLoadout)
    {
        _directory = directory;
        Store = new FileWorkspaceStore(directory);
        Queries = new XmlCharacterFileQueries(new CharacterFileService());
        Resolver = new FileSystemCharacterSourceDataResolver(
            new FileSystemContentOverlayCatalogService(contentRoot, contentRoot, null));
        if (originalLoadout is not null)
        {
            WorkspaceId = CreateOriginalLoadoutWorkspace(originalLoadout);
            SavePrerequisites(zeroRatedTalentGroup, magicTalent);
            return;
        }
        var codec = new Sr5WorkspaceCodec(Queries,
            new XmlCharacterSectionQueries(new CharacterSectionService(Resolver)),
            new XmlCharacterMetadataCommands(new CharacterFileService()));
        var bootstrap = new CharacterCreationBootstrapService(Store,
            new RulesetWorkspaceCodecResolver([codec]), Queries, Resolver);
        Assert.IsTrue(CharacterCreationBootstrapProfiles.TryResolveCanonicalSettingsProfileId(
            CharacterCreationBuildMethods.Priority, out string profile));
        var created = bootstrap.Create(new(CharacterCreationBootstrapSchemas.RequestV1,
            CharacterCreationBootstrapStages.AwaitingFoundationSelection, RulesetDefaults.Sr5,
            "Completion test runner", "Completion", CharacterCreationBuildMethods.Priority, profile));
        Assert.AreEqual(CharacterCreationBootstrapOutcomes.Success, created.Outcome,
            string.Join(",", created.Blockers));
        Assert.IsNotNull(created.Value);
        WorkspaceId = created.Value.WorkspaceId;
        SavePrerequisites(zeroRatedTalentGroup, magicTalent);
    }

    public static CompletionRecommendationTestFixture Create(TestContext context, string? zeroRatedTalentGroup = null,
        string? magicTalent = null, XElement? originalLoadout = null)
    {
        context.Properties.TryGetValue("ChummerCoreContentRoot", out object? supplied);
        string? contentRoot = supplied as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(contentRoot),
            "Supply the explicit ChummerCoreContentRoot test parameter; missing Core data cannot skip this test.");
        Assert.IsTrue(Path.IsPathRooted(contentRoot!), "Core content must be an explicitly selected absolute path.");
        Assert.IsTrue(Directory.Exists(contentRoot), "The selected Core content directory must exist.");
        string directory = Path.Combine(Path.GetTempPath(), $"chummer-completion-recommendation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try { return new(directory, contentRoot!, zeroRatedTalentGroup, magicTalent, originalLoadout); }
        catch
        {
            Directory.Delete(directory, recursive: true);
            throw;
        }
    }

    private CharacterWorkspaceId CreateOriginalLoadoutWorkspace(XElement originalLoadout)
    {
        // Bind the complete original XML through Core before any history is
        // sealed, as Core's custom initial-document fixtures do. Editing a
        // bootstrapped document would invalidate its existing source binding.
        var root = XElement.Parse($"""
            <character>
              <name>Completion test runner</name><alias>Completion</alias><buildmethod>Priority</buildmethod>
              <createdversion>5.225.0</createdversion><appversion>5.225.0</appversion>
              <karma>0</karma><nuyen>0</nuyen><created>False</created><gameedition>SR5</gameedition>
              <settings>{CharacterCreationBootstrapProfiles.PrioritySettingsProfileId}</settings>
              <{CharacterCreationBootstrapXml.MarkerElement}>
                <{CharacterCreationBootstrapXml.SchemaElement}>{CharacterCreationBootstrapSchemas.MarkerV1}</{CharacterCreationBootstrapXml.SchemaElement}>
                <{CharacterCreationBootstrapXml.StageElement}>{CharacterCreationBootstrapStages.AwaitingFoundationSelection}</{CharacterCreationBootstrapXml.StageElement}>
              </{CharacterCreationBootstrapXml.MarkerElement}>
              <streetcred>0</streetcred><notoriety>0</notoriety><publicawareness>0</publicawareness><burntstreetcred>0</burntstreetcred>
              <expenses /><improvements /><contacts />
            </character>
            """);
        root.Add(new XElement(originalLoadout));
        var id = new CharacterWorkspaceId(Guid.NewGuid().ToString("N"));
        var document = new WorkspaceDocument(new WorkspacePayloadEnvelope(RulesetDefaults.Sr5, 1,
            "sr5/chum5-xml", root.ToString(SaveOptions.DisableFormatting)));
        Assert.IsTrue(CharacterCreationBootstrapAuthority.TryPrepareBinding(id, document, Resolver,
            out var binding, out _, out var blockers), string.Join(",", blockers));
        document = document with { State = document.State with
            { AuxiliaryState = new WorkspaceDocumentAuxiliaryState(CharacterCreationBootstrapBinding: binding) } };
        var created = ((ICharacterCreationBootstrapAtomicCreateCapability)Store)
            .CreateCharacterCreationBootstrapWorkspaceDocument(id, document);
        Assert.IsTrue(created.Success, created.Error);
        return id;
    }

    public CharacterCreationCompletionReviewInputs ReadInputs(bool includeLoadout = false)
    {
        IOwnerBoundCharacterCreationCompletionReader reader =
            new OwnerBoundCharacterCreationCompletionReader(Store, Owners, Resolver, Queries);
        var result = includeLoadout
            ? reader.LoadCompletionInputsWithLoadout(Owners.Capture(), new(WorkspaceId))
            : reader.LoadCompletionInputs(Owners.Capture(), new(WorkspaceId));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome, string.Join(",", result.Blockers));
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(result.Value.SnapshotDigest, CharacterCreationCompletionReviewRules.ComputeInputsDigest(result.Value));
        return result.Value;
    }

    public void SaveAttributes(IReadOnlyList<CharacterCreationAttributeAllocation> allocations,
        IReadOnlyList<string>? playerChosenAttributeIds = null)
    {
        var service = new CharacterCreationAttributesService(Store, Resolver);
        var state = service.Load(new(WorkspaceId)).Value!;
        var preview = service.Preview(new(state.Binding, allocations)
        {
            PlayerChosenAttributeIds = playerChosenAttributeIds
        }).Value!;
        Assert.IsTrue(preview.CanConfirm, string.Join(",", preview.Blockers));
        var result = service.Confirm(new(preview.Binding, allocations, preview.PreviewDigest, true)
        {
            PlayerChosenAttributeIds = playerChosenAttributeIds
        });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome, string.Join(",", result.Blockers));
    }

    public void SaveSkills(IReadOnlyList<CharacterCreationSkillAllocation> allocations,
        IReadOnlyList<CharacterCreationSkillGroupAllocation> groups)
    {
        var service = new CharacterCreationSkillsService(Store, Resolver);
        var state = service.Load(new(WorkspaceId)).Value!;
        var preview = service.Preview(new(state.Binding, allocations, groups)).Value!;
        Assert.IsTrue(preview.CanConfirm, string.Join(",", preview.Blockers));
        var result = service.Confirm(new(preview.Binding, allocations, groups, preview.PreviewDigest,
            $"completion-suggestion-{Guid.NewGuid():N}", true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome, string.Join(",", result.Blockers));
    }

    public void SaveMagic(CharacterCreationMagicResonanceSelections selections)
    {
        var service = new CharacterCreationMagicResonanceService(Store, Resolver);
        var state = service.Load(new(WorkspaceId)).Value!;
        var preview = service.Preview(new(state.Binding, selections));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, preview.Outcome, string.Join(",", preview.Blockers));
        var result = service.Confirm(new(state.Binding, selections, preview.Value!.PreviewDigest,
            $"completion-manual-magic-{Guid.NewGuid():N}", true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, result.Outcome, string.Join(",", result.Blockers));
    }

    public void PrepareContacts()
    {
        // The ordinary Contacts editor requires the saved upstream graph, unlike
        // pure completion preview. Confirm genuine empty stages, preserving the
        // attributes chosen by each test, instead of manufacturing draft state.
        var skills = new CharacterCreationSkillsService(Store, Resolver);
        var skillState = skills.Load(new(WorkspaceId)).Value!;
        var native = skillState.Authority.KnowledgeSkills.First(option => option.CanBeNativeLanguage);
        SaveSkills([new(native.SourceSkillId, CharacterCreationSkillKinds.Knowledge, null, null, true)], []);

        var prerequisites = new CharacterCreationPrerequisiteService(Store, Queries, Resolver);
        var attributes = new CharacterCreationAttributesService(Store, Resolver);
        var qualities = new CharacterCreationQualitiesService(Store, Resolver, prerequisites, attributes);
        var qualityState = qualities.Load(new(WorkspaceId)).Value!;
        var qualityPreview = qualities.Preview(new(qualityState.Binding, [])).Value!;
        Assert.IsTrue(qualityPreview.CanConfirm, string.Join(",", qualityPreview.Blockers));
        var qualityResult = qualities.Confirm(new(qualityPreview.Binding, [], qualityPreview.PreviewDigest,
            "completion-contacts-empty-qualities", Guid.NewGuid(), true));
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, qualityResult.Outcome,
            string.Join(",", qualityResult.Blockers));

        var resources = new CharacterCreationResourcesService(Store, Resolver);
        var resourceState = resources.Load(new(WorkspaceId)).Value!;
        var option = resourceState.Options.First(row => row.IsEnabled && row.KarmaInvestment == 0);
        var resourcePreview = resources.Preview(new(resourceState.Binding, option.OptionId)).Value!;
        var resourceResult = resources.Confirm(new(resourcePreview.Binding, option.OptionId,
            resourcePreview.PreviewDigest, "completion-contacts-no-karma-conversion", true));
        Assert.AreEqual(CharacterCreationResourcesOutcomes.Applied, resourceResult.Outcome,
            string.Join(",", resourceResult.Blockers));

        var gear = new CharacterCreationGearService(Store, Resolver);
        var gearState = gear.Load(new(WorkspaceId)).Value!;
        var gearPreview = gear.Preview(new(gearState.Binding, [])).Value!;
        var gearResult = gear.Confirm(new(gearPreview.Binding, [], gearPreview.PreviewDigest,
            "completion-contacts-empty-gear", true));
        Assert.AreEqual(CharacterCreationGearOutcomes.Applied, gearResult.Outcome,
            string.Join(",", gearResult.Blockers));
    }

    public void SaveContact(CharacterCreationKarmaContactSelection contact)
    {
        var service = new CharacterCreationContactsService(Store, Resolver, Queries);
        var loaded = service.Load(new(WorkspaceId));
        Assert.IsNotNull(loaded.Value, string.Join(",", loaded.Blockers));
        Assert.IsTrue(loaded.Value.CanEdit, string.Join(",", loaded.Blockers));
        var edit = new CharacterCreationContactEdit(contact.ContactId, contact.Identity,
            contact.Connection, contact.Loyalty, contact.IsGroup, contact.Free, contact.Family, contact.Blackmail)
        {
            ChangeKind = CharacterCreationContactChangeKind.Add
        };
        var preview = service.Preview(new(loaded.Value.Binding, edit));
        Assert.IsNotNull(preview.Value, string.Join(",", preview.Blockers));
        Assert.IsTrue(preview.Value.CanConfirm, string.Join(",", preview.Blockers));
        var result = service.Confirm(new(preview.Value.Binding, edit, preview.Value.PreviewDigest,
            $"completion-manual-contact-{contact.ContactId:N}", true));
        Assert.AreEqual(CharacterCreationContactOutcomes.Applied, result.Outcome, string.Join(",", result.Blockers));
    }

    public IReadOnlyDictionary<string, string> CaptureFiles()
        => Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(_directory, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);

    public void AssertFilesUnchanged(IReadOnlyDictionary<string, string> before)
    {
        var after = CaptureFiles();
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), after.Keys.ToArray());
        foreach (var (path, digest) in before)
            Assert.AreEqual(digest, after[path], $"Pure recommendation wrote {path}.");
    }

    private void SavePrerequisites(string? zeroRatedTalentGroup, string? magicTalent)
    {
        var service = new CharacterCreationPrerequisiteService(Store, Queries, Resolver);
        var state = service.Load(new(WorkspaceId)).Value!;
        var ranks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CharacterCreationPriorityCategoryIds.Heritage] = magicTalent is not null || zeroRatedTalentGroup is not null ? "E" : "A",
            [CharacterCreationPriorityCategoryIds.Talent] = magicTalent is not null ? "B" : zeroRatedTalentGroup is null ? "E" : "D",
            [CharacterCreationPriorityCategoryIds.Attributes] = magicTalent is not null || zeroRatedTalentGroup is not null ? "A" : "B",
            [CharacterCreationPriorityCategoryIds.Skills] = "C",
            [CharacterCreationPriorityCategoryIds.Resources] = zeroRatedTalentGroup is null ? "D" : "B"
        };
        var heritage = state.Authority.Options.Single(row => row.CategoryId == CharacterCreationPriorityCategoryIds.Heritage
                && row.Rank == ranks[CharacterCreationPriorityCategoryIds.Heritage]).HeritageOptions.First(row => row.IsEnabled
                && row.MetavariantSourceId is null && row.MetatypeName == "Human");
        var talent = state.Authority.Options.Single(row => row.CategoryId == CharacterCreationPriorityCategoryIds.Talent
                && row.Rank == ranks[CharacterCreationPriorityCategoryIds.Talent]).TalentOptions.First(row => row.IsEnabled
                && (magicTalent is not null ? row.Value == magicTalent : zeroRatedTalentGroup is null
                    ? string.Equals(row.Value, CharacterCreationMagicResonanceKinds.Mundane, StringComparison.OrdinalIgnoreCase)
                        && row.Magic is null && row.Resonance is null && row.Depth is null
                        && row.ActiveSkillGrant is null && row.SkillGroupGrant is null
                    : row.Value == "Aspected Magician"));
        string[] talentGroups = zeroRatedTalentGroup is null ? [] : talent.SkillGroupGrant!.Options
            .Where(row => row.CanonicalName == zeroRatedTalentGroup)
            .Take(talent.SkillGroupGrant.Quantity).Select(row => row.SelectionId).ToArray();
        string[] talentSkills = talent.ActiveSkillGrant?.Options.Where(row => row.IsEnabled)
            .Take(talent.ActiveSkillGrant.Quantity).Select(row => row.SelectionId).ToArray() ?? [];
        var preview = service.Preview(new(state.Binding, ranks)
        {
            HeritageSelectionId = heritage.SelectionId, TalentSelectionId = talent.SelectionId,
            TalentActiveSkillSelectionIds = talentSkills, TalentSkillGroupSelectionIds = talentGroups
        }).Value!;
        var saved = service.Confirm(new(preview.Binding, ranks, preview.PreviewDigest, true)
        {
            HeritageSelectionId = heritage.SelectionId, TalentSelectionId = talent.SelectionId,
            TalentActiveSkillSelectionIds = talentSkills, TalentSkillGroupSelectionIds = talentGroups
        });
        Assert.AreEqual(CharacterCreationFoundationOutcomes.Success, saved.Outcome, string.Join(",", saved.Blockers));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
