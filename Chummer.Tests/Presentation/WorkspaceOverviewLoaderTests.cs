#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Application.Characters;
using Chummer.Application.Workspaces;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Run.Contracts.Billing;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class WorkspaceOverviewLoaderTests
{
    [TestMethod]
    public async Task LoadAsync_returns_expected_sections_from_client()
    {
        WorkspaceOverviewLoader loader = new();
        LoaderClientStub client = new();
        CharacterWorkspaceId workspaceId = new("ws-loader");

        WorkspaceOverviewLoadResult result = await loader.LoadAsync(client, workspaceId, CancellationToken.None);

        Assert.AreEqual("Loader Neo", result.Profile.Name);
        Assert.AreEqual("LOADER", result.Profile.Alias);
        Assert.AreEqual(9m, result.Progress.Karma);
        Assert.AreEqual(1, result.Skills.Count);
        Assert.AreEqual("SR5", result.Rules.GameEdition);
        Assert.AreEqual("Priority", result.Build.BuildMethod);
        Assert.AreEqual("10/25", result.Movement.Walk);
        Assert.IsFalse(result.Awakening.MagEnabled);
        Assert.IsNull(result.CanonicalValidation, "Public display loading must never mint recovery authority.");
    }

    [TestMethod]
    public async Task LoadAsync_starts_synchronous_section_projections_concurrently()
    {
        WorkspaceOverviewLoader loader = new();
        LoaderClientStub client = new(blockSectionCalls: true);
        CharacterWorkspaceId workspaceId = new("ws-concurrent-loader");

        Task<WorkspaceOverviewLoadResult> loadTask = Task.Run(
            () => loader.LoadAsync(client, workspaceId, CancellationToken.None));
        Task timeout = Task.Delay(TimeSpan.FromSeconds(10));
        Task completed = await Task.WhenAny(client.AllSectionCallsStarted, timeout);

        try
        {
            Assert.AreSame(
                client.AllSectionCallsStarted,
                completed,
                "Every independent section projection must start before any synchronous projection returns.");
            Assert.AreEqual(8, client.SectionCallsStarted);
        }
        finally
        {
            client.ReleaseSectionCalls();
        }

        WorkspaceOverviewLoadResult result = await loadTask;
        Assert.AreEqual("Loader Neo", result.Profile.Name);
    }

    [TestMethod]
    public async Task LoadAsync_uses_snapshot_bound_projection_capability_when_available()
    {
        WorkspaceOverviewLoader loader = new();
        BatchLoaderClientStub client = new();
        CharacterWorkspaceId workspaceId = new("ws-batch-loader");

        WorkspaceOverviewLoadResult result = await loader.LoadAsync(
            client,
            workspaceId,
            CancellationToken.None);

        Assert.AreEqual(1, client.BatchCalls);
        Assert.AreEqual(1, client.ValidationCalls);
        Assert.AreEqual("Loader Neo", result.Profile.Name);
        Assert.AreEqual("Priority", result.Build.BuildMethod);
        Assert.AreEqual(1, result.ContentRevision);
    }

    [TestMethod]
    public async Task LoadAsync_rejects_snapshot_bound_projection_from_different_exact_bytes()
    {
        const string changedXml = "<character><name>Changed Bytes</name><alias>LOADER</alias>"
            + "<metatype>Human</metatype><buildmethod>Priority</buildmethod>"
            + "<createdversion>1.0</createdversion><appversion>1.0</appversion>"
            + "<karma>9</karma><nuyen>1000</nuyen><created>True</created></character>";
        WorkspaceOverviewLoader loader = new();
        BatchLoaderClientStub client = new(secondXml: changedXml);

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            loader.LoadAsync(client, new CharacterWorkspaceId("ws-batch-drift"), CancellationToken.None));

        StringAssert.Contains(error.Message, "before its snapshot-bound overview was projected");
    }

    [DataTestMethod]
    [DataRow(RulesetDefaults.Sr4, "sr4/chum4-xml")]
    [DataRow(RulesetDefaults.Sr5, "sr5/chum5-xml")]
    [DataRow(RulesetDefaults.Sr6, "sr6/chum6-xml")]
    public async Task Composition_bound_loader_accepts_canonical_documents_for_every_supported_ruleset(
        string rulesetId,
        string payloadKind)
    {
        LoaderClientStub client = new(rulesetId, payloadKind);
        WorkspaceOverviewLoader loader = WorkspaceOverviewLoader.CreateCompositionBound(client);

        WorkspaceOverviewLoadResult result = await ((IAuthoritativeWorkspaceOverviewLoader)loader)
            .LoadAuthoritativeAsync(new CharacterWorkspaceId("ws-authoritative"), CancellationToken.None);

        Assert.IsNotNull(result.CanonicalValidation);
        Assert.AreEqual(rulesetId, result.Document?.RulesetId);
    }

    [DataTestMethod]
    [DataRow(RulesetDefaults.Sr4, "sr4/chum4-xml")]
    [DataRow(RulesetDefaults.Sr5, "sr5/chum5-xml")]
    [DataRow(RulesetDefaults.Sr6, "sr6/chum6-xml")]
    public async Task Canonical_fabricated_client_on_public_display_surface_cannot_mint_recovery_authority(
        string rulesetId,
        string payloadKind)
    {
        LoaderClientStub attacker = new(rulesetId, payloadKind);

        WorkspaceOverviewLoadResult result = await new WorkspaceOverviewLoader().LoadAsync(
            attacker,
            new CharacterWorkspaceId("victim-workspace"),
            CancellationToken.None);

        Assert.IsNull(result.CanonicalValidation);
        Assert.IsFalse(typeof(WorkspaceOverviewLoadResult).GetProperties()
            .Any(property => property.PropertyType == typeof(WorkspaceOverviewLoader.CanonicalValidationCapability)));
    }

    [TestMethod]
    public async Task Public_display_load_rejects_a_snapshot_for_a_different_workspace()
    {
        LoaderClientStub attacker = new(returnedWorkspaceId: new CharacterWorkspaceId("attacker-workspace"));

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new WorkspaceOverviewLoader().LoadAsync(
                attacker,
                new CharacterWorkspaceId("victim-workspace"),
                CancellationToken.None));

        StringAssert.Contains(error.Message, "while 'victim-workspace' was requested");
    }

    [TestMethod]
    public async Task Public_display_load_rejects_same_revision_with_different_exact_document_bytes()
    {
        LoaderClientStub attacker = new(
            secondXml: "<character><name>Changed Bytes</name><alias>LOADER</alias>"
                + "<metatype>Human</metatype><buildmethod>Priority</buildmethod>"
                + "<createdversion>1.0</createdversion><appversion>1.0</appversion>"
                + "<karma>9</karma><nuyen>1000</nuyen><created>True</created></character>");

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new WorkspaceOverviewLoader().LoadAsync(
                attacker,
                new CharacterWorkspaceId("victim-workspace"),
                CancellationToken.None));

        StringAssert.Contains(error.Message, "inconsistent canonical bytes");
    }

    [TestMethod]
    public async Task Recovery_read_rejects_a_snapshot_for_a_different_workspace()
    {
        LoaderClientStub attacker = new(returnedWorkspaceId: new CharacterWorkspaceId("attacker-workspace"));
        WorkspaceOverviewLoader loader = WorkspaceOverviewLoader.CreateCompositionBound(attacker);

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ((IAuthoritativeWorkspaceOverviewLoader)loader).LoadRecoverySnapshotAsync(
                new CharacterWorkspaceId("victim-workspace"),
                CancellationToken.None));

        StringAssert.Contains(error.Message, "while 'victim-workspace' was requested");
    }

    [TestMethod]
    public async Task Recovery_read_rejects_same_revision_with_different_exact_document_bytes()
    {
        LoaderClientStub attacker = new(
            secondXml: "<character><name>Changed Bytes</name><alias>LOADER</alias>"
                + "<metatype>Human</metatype><buildmethod>Priority</buildmethod>"
                + "<createdversion>1.0</createdversion><appversion>1.0</appversion>"
                + "<karma>9</karma><nuyen>1000</nuyen><created>True</created></character>");
        WorkspaceOverviewLoader loader = WorkspaceOverviewLoader.CreateCompositionBound(attacker);

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ((IAuthoritativeWorkspaceOverviewLoader)loader).LoadRecoverySnapshotAsync(
                new CharacterWorkspaceId("victim-workspace"),
                CancellationToken.None));

        StringAssert.Contains(error.Message, "changed while its recovery snapshot was being verified");
    }

    [DataTestMethod]
    [DataRow(RulesetDefaults.Sr4, "sr4/chum4-xml")]
    [DataRow(RulesetDefaults.Sr5, "sr5/chum5-xml")]
    [DataRow(RulesetDefaults.Sr6, "sr6/chum6-xml")]
    public async Task Composition_bound_loader_rejects_malformed_payload_even_when_client_validator_always_accepts(
        string rulesetId,
        string payloadKind)
    {
        LoaderClientStub attacker = new(
            rulesetId,
            payloadKind,
            "<fabricated><name>Injected Runner</name></fabricated>");
        WorkspaceOverviewLoader loader = WorkspaceOverviewLoader.CreateCompositionBound(attacker);

        InvalidOperationException error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ((IAuthoritativeWorkspaceOverviewLoader)loader).LoadAuthoritativeAsync(
                new CharacterWorkspaceId("victim-authoritative-workspace"),
                CancellationToken.None));

        StringAssert.Contains(error.Message, "loader-owned canonical codec authority");
        Assert.AreEqual(1, attacker.ValidationCalls,
            "The malicious client validator must have returned true before loader-owned validation rejects the payload.");
    }

    [TestMethod]
    public void Recovery_capability_cannot_be_fabricated_without_the_private_loader_issuer()
    {
        WorkspaceDocument document = new(new WorkspacePayloadEnvelope(
            RulesetDefaults.Sr5,
            SchemaVersion: 1,
            PayloadKind: "sr5/chum5-xml",
            Payload: "<character><name>Forgery</name></character>"));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new WorkspaceOverviewLoader.CanonicalValidationCapability(
                new object(),
                new CharacterWorkspaceId("forged-authority"),
                1,
                document));
    }

    [TestMethod]
    public async Task Initial_creation_activation_attempt_bypasses_workspace_and_domain_reload_path()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                "dialog.new_character",
                "Select Build Method",
                null,
                [
                    new DesktopDialogField("newCharacterRulesetId", "Ruleset", RulesetDefaults.Sr5, RulesetDefaults.Sr5),
                    new DesktopDialogField("newCharacterName", "Character Name", "Nova", "Nova"),
                    new DesktopDialogField("newCharacterAlias", "Alias", "Cipher", "Cipher"),
                    new DesktopDialogField("newCharacterBuildMethod", "Build Method", CharacterCreationBuildMethods.Priority, CharacterCreationBuildMethods.Priority),
                    new DesktopDialogField("newCharacterSetting", "Character Setting", "Core Rulebook", "Core Rulebook"),
                    new DesktopDialogField("newCharacterHouseRulesEnabled", "House Rules", "false", "false"),
                    new DesktopDialogField("newCharacterIgnoreRules", "Ignore Rules", "false", "false")
                ],
                [new DesktopDialogAction("create_character", "Create", true)])
        };
        bool reloadCalled = false;
        CharacterCreationBootstrapActivationBundle? consumed = null;
        CharacterCreationBootstrapActivationBundle? produced = null;
        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published,
            LoadWorkspaceAsync: (_, _) =>
            {
                reloadCalled = true;
                return Task.CompletedTask;
            },
            CreateCharacterBootstrapActivationAsync: (request, _) =>
            {
                CharacterCreationBootstrapReceipt receipt = CreateBootstrapReceipt(
                    request,
                    new CharacterWorkspaceId("ws-activated"));
                produced = new CharacterCreationBootstrapActivationBundle(
                    CharacterCreationBootstrapActivationSchemas.BundleV1,
                    receipt,
                    null!,
                    null!,
                    null!,
                    "sha256:" + new string('a', 64));
                return Task.FromResult(
                    new CharacterCreationBootstrapActivationAttempt(
                        CharacterCreationBootstrapOutcomes.Success,
                        receipt,
                        produced,
                        []));
            },
            ActivateCharacterBootstrapAsync: (activation, _) =>
            {
                consumed = activation;
                published = published with
                {
                    Error = null,
                    WorkspaceId = activation.Receipt.WorkspaceId
                };
                return Task.CompletedTask;
            });

        await coordinator.CoordinateAsync("create_character", context, CancellationToken.None);

        Assert.IsNotNull(produced);
        Assert.AreSame(produced, consumed);
        Assert.IsFalse(
            reloadCalled,
            "An available activation must not trigger repeated workspace/domain loads.");
        Assert.AreEqual("ws-activated", published.WorkspaceId?.Value);
        Assert.IsNull(published.Error);
        Assert.IsNull(published.ActiveDialog);
    }

    private static CharacterCreationBootstrapReceipt CreateBootstrapReceipt(
        CharacterCreationBootstrapRequest request,
        CharacterWorkspaceId workspaceId)
    {
        string canonicalDigest = "sha256:" + new string('a', 64);
        string[] sourceAnchorIds = CharacterCreationBootstrapProfiles.ExpectedSourceAnchorIds(
            request.BuildMethod,
            request.SettingsProfileId);
        var unsignedBinding = new CharacterCreationBootstrapBinding(
            CharacterCreationBootstrapSchemas.BindingV1,
            CharacterCreationBootstrapStages.AwaitingFoundationSelection,
            workspaceId,
            request.RulesetId,
            request.BuildMethod,
            request.SettingsProfileId,
            CharacterCreationBootstrapRevisions.InitialContentRevision,
            CharacterCreationBootstrapRevisions.InitialSavedRevision,
            canonicalDigest,
            canonicalDigest,
            canonicalDigest,
            canonicalDigest,
            $"settings.xml#setting:{request.SettingsProfileId}",
            sourceAnchorIds,
            string.Empty);
        CharacterCreationBootstrapBinding binding = unsignedBinding with
        {
            BindingDigest = CharacterCreationBootstrapBindingDigest.Compute(unsignedBinding)
        };
        var unsignedReceipt = new CharacterCreationBootstrapReceipt(
            CharacterCreationBootstrapSchemas.ReceiptV1,
            workspaceId,
            CharacterCreationBootstrapRevisions.InitialContentRevision,
            CharacterCreationBootstrapRevisions.InitialSavedRevision,
            new CharacterFileSummary(
                request.Name,
                request.Alias,
                string.Empty,
                request.BuildMethod,
                "5.225.0",
                "5.225.0",
                0,
                0,
                false),
            binding,
            sourceAnchorIds,
            string.Empty);
        return unsignedReceipt with
        {
            ReceiptDigest = CharacterCreationBootstrapReceiptDigest.Compute(unsignedReceipt)
        };
    }

    private class LoaderClientStub : IChummerClient
    {
        private readonly string _rulesetId;
        private readonly string _payloadKind;
        private readonly string _xml;
        private readonly string? _secondXml;
        private readonly CharacterWorkspaceId? _returnedWorkspaceId;
        private readonly bool _blockSectionCalls;
        private readonly TaskCompletionSource<bool> _allSectionCallsStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseSectionCalls = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _workspaceReadCount;
        private int _sectionCallsStarted;

        public LoaderClientStub(
            string rulesetId = RulesetDefaults.Sr5,
            string payloadKind = "sr5/chum5-xml",
            string? xml = null,
            string? secondXml = null,
            CharacterWorkspaceId? returnedWorkspaceId = null,
            bool blockSectionCalls = false)
        {
            _rulesetId = rulesetId;
            _payloadKind = payloadKind;
            _xml = xml
                ?? "<character><name>Loader Neo</name><alias>LOADER</alias>"
                    + "<metatype>Human</metatype><buildmethod>Priority</buildmethod>"
                    + "<createdversion>1.0</createdversion><appversion>1.0</appversion>"
                    + "<karma>9</karma><nuyen>1000</nuyen><created>True</created></character>";
            _secondXml = secondXml;
            _returnedWorkspaceId = returnedWorkspaceId;
            _blockSectionCalls = blockSectionCalls;
        }

        public int ValidationCalls { get; private set; }

        public int SectionCallsStarted => Volatile.Read(ref _sectionCallsStarted);

        public Task AllSectionCallsStarted => _allSectionCallsStarted.Task;

        public void ReleaseSectionCalls() => _releaseSectionCalls.TrySetResult(true);

        public Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct) => throw new NotImplementedException();

        public Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct) => throw new NotImplementedException();

        public Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<RuntimeInspectorProjection?> GetRuntimeInspectorProfileAsync(string profileId, string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<MasterIndexResponse> GetMasterIndexAsync(CancellationToken ct)
            => Task.FromResult(new MasterIndexResponse(0, DateTimeOffset.UtcNow, [], "missing", 0, []));

        public Task<TranslatorLanguagesResponse> GetTranslatorLanguagesAsync(CancellationToken ct)
            => Task.FromResult(new TranslatorLanguagesResponse(0, []));

        public Task<IReadOnlyList<DesktopBuildPathSuggestion>> GetBuildPathSuggestionsAsync(string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<DesktopBuildPathPreview?> GetBuildPathPreviewAsync(string buildKitId, CharacterWorkspaceId workspaceId, string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct) => throw new NotImplementedException();

        public Task<AccountCampaignSummary?> GetAccountCampaignSummaryAsync(CancellationToken ct)
            => Task.FromResult<AccountCampaignSummary?>(null);

        public Task<MyFirstBookQuotaSnapshotDto?> GetMyFirstBookQuotaAsync(CancellationToken ct)
            => Task.FromResult<MyFirstBookQuotaSnapshotDto?>(null);

        public Task<MyFirstBookQuotaConsumeResultDto> ConsumeMyFirstBookQuotaAsync(CancellationToken ct)
            => Task.FromException<MyFirstBookQuotaConsumeResultDto>(new InvalidOperationException("Not used in this test."));

        public Task<IReadOnlyList<CampaignWorkspaceDigestProjection>> GetCampaignWorkspaceDigestsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CampaignWorkspaceDigestProjection>>(Array.Empty<CampaignWorkspaceDigestProjection>());

        public Task<IReadOnlyList<DesktopHomeSupportDigest>> GetDesktopHomeSupportDigestsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DesktopHomeSupportDigest>>([]);

        public Task<DesktopSupportCaseDetails?> GetDesktopSupportCaseDetailsAsync(string caseId, CancellationToken ct)
            => Task.FromResult<DesktopSupportCaseDetails?>(null);

        public Task<DesktopInstallLinkingSummaryProjection> GetDesktopInstallLinkingSummaryAsync(CancellationToken ct)
            => Task.FromResult(DesktopInstallLinkingSummaryProjection.Empty);

        public Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => throw new NotImplementedException();

        public Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceDocumentSnapshot>> GetWorkspaceAsync(
            CharacterWorkspaceId id,
            CancellationToken ct)
        {
            int readNumber = Interlocked.Increment(ref _workspaceReadCount);
            WorkspaceDocument document = new(new WorkspacePayloadEnvelope(
                _rulesetId,
                SchemaVersion: 1,
                PayloadKind: _payloadKind,
                Payload: readNumber > 1 && _secondXml is not null ? _secondXml : _xml));
            return Task.FromResult(new CommandResult<WorkspaceDocumentSnapshot>(
                true,
                new WorkspaceDocumentSnapshot(
                    _returnedWorkspaceId ?? id,
                    document,
                    DateTimeOffset.UtcNow,
                    ContentRevision: 1,
                    SavedRevision: 1),
                null));
        }

        public Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterFileSummary> GetSummaryAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CharacterValidationResult> ValidateAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            BlockSectionCallIfRequested();
            ValidationCalls++;
            return Task.FromResult(new CharacterValidationResult(true, []));
        }

        public Task<CharacterProfileSection> GetProfileAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            BlockSectionCallIfRequested();
            return Task.FromResult(new CharacterProfileSection(
                Name: "Loader Neo",
                Alias: "LOADER",
                PlayerName: string.Empty,
                Metatype: "Human",
                Metavariant: string.Empty,
                Sex: string.Empty,
                Age: string.Empty,
                Height: string.Empty,
                Weight: string.Empty,
                Hair: string.Empty,
                Eyes: string.Empty,
                Skin: string.Empty,
                Concept: string.Empty,
                Description: string.Empty,
                Background: string.Empty,
                CreatedVersion: string.Empty,
                AppVersion: string.Empty,
                BuildMethod: "Priority",
                GameplayOption: string.Empty,
                Created: true,
                Adept: false,
                Magician: false,
                Technomancer: false,
                AI: false,
                MainMugshotIndex: 0,
                MugshotCount: 0));
        }

        public Task<CharacterProgressSection> GetProgressAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            BlockSectionCallIfRequested();
            return Task.FromResult(new CharacterProgressSection(
                Karma: 9m,
                Nuyen: 1000m,
                StartingNuyen: 0m,
                StreetCred: 0,
                Notoriety: 0,
                PublicAwareness: 0,
                BurntStreetCred: 0,
                BuildKarma: 0,
                TotalAttributes: 0,
                TotalSpecial: 0,
                PhysicalCmFilled: 0,
                StunCmFilled: 0,
                TotalEssence: 6m,
                InitiateGrade: 0,
                SubmersionGrade: 0,
                MagEnabled: false,
                ResEnabled: false,
                DepEnabled: false));
        }

        public Task<CharacterSkillsSection> GetSkillsAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            BlockSectionCallIfRequested();
            return Task.FromResult(new CharacterSkillsSection(
                Count: 1,
                KnowledgeCount: 0,
                Skills:
                [
                    new CharacterSkillSummary(
                        Guid: "skill-1",
                        Suid: string.Empty,
                        Category: "Combat",
                        IsKnowledge: false,
                        BaseValue: 4,
                        KarmaValue: 0,
                        Specializations: ["Pistols"])
                ]));
        }

        public Task<CharacterRulesSection> GetRulesAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            BlockSectionCallIfRequested();
            return Task.FromResult(new CharacterRulesSection(
                GameEdition: "SR5",
                Settings: "default.xml",
                GameplayOption: "Standard",
                GameplayOptionQualityLimit: 25,
                MaxNuyen: 10,
                MaxKarma: 25,
                ContactMultiplier: 3,
                BannedWareGrades: []));
        }

        public Task<CharacterBuildSection> GetBuildAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            BlockSectionCallIfRequested();
            return Task.FromResult(new CharacterBuildSection(
                BuildMethod: "Priority",
                PriorityMetatype: "C,2",
                PriorityAttributes: "E,0",
                PrioritySpecial: "A,4",
                PrioritySkills: "B,3",
                PriorityResources: "D,1",
                PriorityTalent: "Mundane",
                SumToTen: 10,
                Special: 1,
                TotalSpecial: 4,
                TotalAttributes: 20,
                ContactPoints: 15,
                ContactPointsUsed: 8));
        }

        public Task<CharacterMovementSection> GetMovementAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            BlockSectionCallIfRequested();
            return Task.FromResult(new CharacterMovementSection(
                Walk: "10/25",
                Run: "20/50",
                Sprint: "40/100",
                WalkAlt: "10/25",
                RunAlt: "20/50",
                SprintAlt: "40/100",
                PhysicalCmFilled: 0,
                StunCmFilled: 0));
        }

        public Task<CharacterAwakeningSection> GetAwakeningAsync(CharacterWorkspaceId id, CancellationToken ct)
        {
            BlockSectionCallIfRequested();
            return Task.FromResult(new CharacterAwakeningSection(
                MagEnabled: false,
                ResEnabled: false,
                DepEnabled: false,
                Adept: false,
                Magician: false,
                Technomancer: false,
                AI: false,
                InitiateGrade: 0,
                SubmersionGrade: 0,
                Tradition: string.Empty,
                TraditionName: string.Empty,
                TraditionDrain: string.Empty,
                SpiritCombat: string.Empty,
                SpiritDetection: string.Empty,
                SpiritHealth: string.Empty,
                SpiritIllusion: string.Empty,
                SpiritManipulation: string.Empty,
                Stream: string.Empty,
                StreamDrain: string.Empty,
                CurrentCounterspellingDice: 0,
                SpellLimit: 0,
                CfpLimit: 0,
                AiNormalProgramLimit: 0,
                AiAdvancedProgramLimit: 0));
        }

        private void BlockSectionCallIfRequested()
        {
            if (!_blockSectionCalls)
                return;

            int started = Interlocked.Increment(ref _sectionCallsStarted);
            if (started == 8)
                _allSectionCallsStarted.TrySetResult(true);

            _releaseSectionCalls.Task.GetAwaiter().GetResult();
        }

        public Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(CharacterWorkspaceId id, UpdateWorkspaceMetadata command, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();

        public Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class BatchLoaderClientStub : LoaderClientStub, IWorkspaceOverviewProjectionClient
    {
        public BatchLoaderClientStub(string? secondXml = null)
            : base(secondXml: secondXml)
        {
        }

        public int BatchCalls { get; private set; }

        public async Task<CommandResult<WorkspaceOverviewProjection>> GetWorkspaceOverviewAsync(
            CharacterWorkspaceId workspaceId,
            CancellationToken ct)
        {
            BatchCalls++;
            CommandResult<WorkspaceDocumentSnapshot> snapshot = await GetWorkspaceAsync(workspaceId, ct);
            if (!snapshot.Success || snapshot.Value is null)
            {
                return new CommandResult<WorkspaceOverviewProjection>(
                    false,
                    null,
                    snapshot.Error ?? "Missing test snapshot.",
                    snapshot.Outcome);
            }

            CharacterOverviewProjection overview = new(
                Profile: await GetProfileAsync(workspaceId, ct),
                Progress: await GetProgressAsync(workspaceId, ct),
                Skills: await GetSkillsAsync(workspaceId, ct),
                Rules: await GetRulesAsync(workspaceId, ct),
                Build: await GetBuildAsync(workspaceId, ct),
                Movement: await GetMovementAsync(workspaceId, ct),
                Awakening: await GetAwakeningAsync(workspaceId, ct));
            CharacterValidationResult validation = await ValidateAsync(workspaceId, ct);
            return new CommandResult<WorkspaceOverviewProjection>(
                true,
                new WorkspaceOverviewProjection(snapshot.Value, overview, validation),
                null);
        }
    }
}
