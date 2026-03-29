#nullable enable annotations

using Chummer.Campaign.Contracts;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using Chummer.Presentation.Overview;
using System;
using System.IO;

namespace Chummer.Tests.Presentation;

internal static class AccessibilitySignoffSmokeTests
{
    private static int Main()
    {
        try
        {
            SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation();
            GeneratedAssetReviewPanel_renders_preview_and_emits_attach_approve_archive_actions();
            BlazorHome_invalidates_spider_cards_when_session_context_shifts_and_refreshes_them();
            BlazorHome_uses_local_chummer6_flagship_media_samples();
            BlazorCampaignSpineShowcase_uses_customer_facing_build_path_copy();
            DesktopHomeCampaignProjector_uses_real_campaign_restore_truth();
            DesktopHomeSupportProjector_uses_real_support_case_truth();
            DesktopHomeBuildExplainProjector_uses_real_contract_state();
            DesktopHomeBuildExplainProjector_exposes_safe_action_and_watchouts_when_workspace_is_missing();
            DesktopHome_wires_the_campaign_projection_into_the_summary_panel();
            DesktopHome_wires_the_support_projection_into_the_summary_panel();
            DesktopHome_wires_the_build_and_explain_projection_into_the_summary_panel();
            DesktopHome_exposes_claim_aware_install_and_update_actions();
            DesktopInstallLinkingWindow_exposes_trust_actions_and_locale_guidance();
            DesktopHead_uses_canonical_catalog_only_resolver();
            Console.WriteLine("[B13] PASS: targeted accessibility smoke runner checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[B13] FAIL: {ex.Message}");
            return 1;
        }
    }

    private static void SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation()
    {
        string source = ReadSource("Chummer.Blazor/Components/Shell/SectionPane.razor");
        RequireContains(source, "role=\"listbox\"");
        RequireContains(source, "role=\"option\"");
        RequireContains(source, "aria-activedescendant=");
        RequireContains(source, "aria-selected=\"@IsBrowseResultActive");
    }

    private static void GeneratedAssetReviewPanel_renders_preview_and_emits_attach_approve_archive_actions()
    {
        string source = ReadSource("Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor");
        RequireContains(source, "role=\"tablist\"");
        RequireContains(source, "role=\"tab\"");
        RequireContains(source, "role=\"tabpanel\"");
        RequireContains(source, "aria-controls=");
        RequireContains(source, "data-generated-asset-image-slot-figure");
        RequireContains(source, "data-generated-asset-flagship-rail");
        RequireContains(source, "data-generated-asset-world-markers");
    }

    private static void BlazorHome_invalidates_spider_cards_when_session_context_shifts_and_refreshes_them()
    {
        string source = ReadSource("Chummer.Blazor/Components/Shared/GmBoardFeed.razor");
        RequireContains(source, "data-gm-board-stale-banner");
        RequireContains(source, "role=\"status\"");
        RequireContains(source, "aria-live=\"polite\"");
    }

    private static void DesktopHomeCampaignProjector_uses_real_campaign_restore_truth()
    {
        RuleEnvironmentRef environment = new(
            EnvironmentId: "env.seattle",
            OwnerScope: "campaign:campaign-1",
            CompatibilityFingerprint: "sha256:campaign",
            ApprovalState: "approved",
            SourcePacks: ["core"],
            HouseRulePacks: ["seattle-streets"],
            OptionToggles: ["prime_runner"]);
        ContinuitySnapshotRef continuity = new(
            SnapshotId: "snapshot-1",
            CapturedAtUtc: DateTimeOffset.Parse("2026-03-27T12:00:00+00:00"),
            Summary: "Run recap and downtime packet captured.",
            RestoreState: "ready",
            SessionId: "run-1",
            SceneId: "scene-1",
            RecapArtifactId: "artifact-recap");
        RunnerDossierProjection dossier = new(
            DossierId: "dossier-1",
            RunnerHandle: "apex",
            DisplayName: "Apex",
            Status: DossierStatuses.Active,
            OwnerUserId: "user-1",
            CrewId: "crew-1",
            CampaignId: "campaign-1",
            CurrentRunId: "run-1",
            CurrentSceneId: "scene-1",
            RuleEnvironment: environment,
            LatestContinuity: continuity,
            BuildReceiptIds: ["build-receipt-1"],
            SnapshotIds: ["snapshot-1"],
            Projections:
            [
                new PublicationSafeProjection("projection-1", "dossier_card", "Living dossier", "Ready for campaign return.")
            ],
            CreatedAtUtc: DateTimeOffset.Parse("2026-03-20T12:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:10:00+00:00"));
        CampaignProjection campaign = new(
            CampaignId: "campaign-1",
            GroupId: "group-1",
            Name: "Neon Nights",
            Status: CampaignStatuses.Active,
            Visibility: "private",
            Summary: "Seattle campaign continuity is grounded and ready to resume.",
            RuleEnvironment: environment,
            ActiveRunId: "run-1",
            CrewIds: ["crew-1"],
            DossierIds: ["dossier-1"],
            RunIds: ["run-1"],
            LatestContinuity: continuity,
            CreatedAtUtc: DateTimeOffset.Parse("2026-03-20T12:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:10:00+00:00"));
        RunProjection run = new(
            RunId: "run-1",
            CampaignId: "campaign-1",
            Title: "Cold Veins",
            Status: RunStatuses.Active,
            Summary: "The extraction is mid-stream and ready to recover on the next claimed device.",
            ActiveSceneId: "scene-1",
            Objectives:
            [
                new ObjectiveProjection("objective-1", "Recover the courier", "active", "high", "Primary extraction target is still active.", DateTimeOffset.Parse("2026-03-27T12:05:00+00:00"))
            ],
            Scenes:
            [
                new SceneProjection("scene-1", "run-1", "Dockside handoff", "r3", "active", "Current scene is pinned for return.", DateTimeOffset.Parse("2026-03-27T12:06:00+00:00"))
            ],
            LatestContinuity: continuity,
            CreatedAtUtc: DateTimeOffset.Parse("2026-03-20T12:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:10:00+00:00"));
        CrewProjection crew = new(
            CrewId: "crew-1",
            Name: "Night Shift",
            Visibility: "private",
            GroupId: "group-1",
            CampaignId: "campaign-1",
            Members:
            [
                new CrewAssignmentProjection("user-1", "dossier-1", "player", "ready", DateTimeOffset.Parse("2026-03-20T12:00:00+00:00"))
            ],
            CreatedAtUtc: DateTimeOffset.Parse("2026-03-20T12:00:00+00:00"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:00:00+00:00"));
        CampaignWorkspaceProjection workspace = new(
            WorkspaceId: "workspace-1",
            CampaignId: "campaign-1",
            CampaignName: "Neon Nights",
            Visibility: "private",
            RuleEnvironment: environment,
            Crews: [crew],
            Dossiers: [dossier],
            Runs: [run],
            RecapShelf:
            [
                new PublicationSafeProjection("recap-1", "recap", "Run recap", "Return packet is ready.")
            ],
            ReadinessCues:
            [
                new CampaignReadinessCue("cue-1", "warning", "Rule drift review", "One local override still needs an explicit review before you trust the next export.")
            ],
            LatestContinuity: continuity,
            ReturnSummary: "Return to Neon Nights via Dockside handoff with Apex pinned to the active run.",
            ActiveSceneSummary: "Midnight Extraction is currently on Dockside handoff (r3). Recover the courier stays active with high pressure.",
            NextSafeAction: "Resume Dockside handoff before you fan the recap-safe output out to the rest of the crew.",
            ChangePackets:
            [
                new WorkspaceChangePacketProjection(
                    PacketId: "packet-1",
                    Kind: "scene",
                    Label: "Active scene",
                    Summary: "Dockside handoff is live and still pinned to the courier extraction.",
                    UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:06:00+00:00"))
            ],
            RosterTransfers:
            [
                new RosterTransferProjection(
                    TransferId: "transfer-1",
                    DossierId: "dossier-1",
                    RunnerHandle: "APEX",
                    PreviousOwnerUserId: "user-1",
                    CurrentOwnerUserId: "user-2",
                    SourceGroupId: "group-1",
                    SourceGroupName: "Neon Nights",
                    SourceCampaignId: "campaign-1",
                    SourceCampaignName: "Neon Nights",
                    SourceCrewId: "crew-1",
                    SourceCrewName: "Neon Nights crew",
                    TargetGroupId: "group-2",
                    TargetGroupName: "Thursday Crew Relay",
                    TargetCampaignId: "campaign-2",
                    TargetCampaignName: "Thursday Crew Relay",
                    TargetCrewId: "crew-2",
                    TargetCrewName: "Thursday Crew Relay crew",
                    InitiatedByUserId: "user-gm",
                    Summary: "APEX moved into Thursday Crew Relay with governed ownership receipts attached.",
                    AuditLines:
                    [
                        "GM moved the dossier into Thursday Crew Relay.",
                        "Ownership moved with the same dossier id preserved."
                    ],
                    Receipts:
                    [
                        new CampaignConsequenceReceipt("group-1", "source_group", "Neon Nights"),
                        new CampaignConsequenceReceipt("group-2", "target_group", "Thursday Crew Relay")
                    ],
                    TransferredAtUtc: DateTimeOffset.Parse("2026-03-27T12:06:30+00:00"))
            ]);
        BuildLabHandoffProjection handoff = new(
            HandoffId: "handoff-1",
            DossierId: "dossier-1",
            CampaignId: "campaign-1",
            Title: "Social Operator build path",
            Summary: "Build path handoff is ready for the next campaign return.",
            VariantLabel: "social-operator",
            ProgressionLabel: "prime",
            ExplainEntryId: "rules-1",
            TradeoffLines: ["Trade one gear slot for team-facing coverage."],
            ProgressionOutcomes: ["Campaign return packet keeps the same continuity target."],
            Outputs:
            [
                new PublicationSafeProjection("output-1", "build_receipt", "Build receipt", "Grounded for the current runtime.")
            ],
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:07:00+00:00"),
            NextSafeAction: "Review the build path receipt before you reopen the campaign workspace.",
            RuntimeCompatibilitySummary: "Runtime sha256:campaign is still compatible with this handoff.",
            CampaignReturnSummary: "Build handoff stays attached to Neon Nights and the current return snapshot.",
            SupportClosureSummary: "The linked install can verify whether the next promoted fix landed before the run resumes.",
            Watchouts: ["Confirm the rule drift review before you export the updated dossier."]);
        RulesNavigatorAnswerProjection rules = new(
            EntryId: "rules-1",
            Question: "How does the Seattle Streets override affect this return?",
            ShortAnswer: "It keeps the active handoff legal after restore.",
            BeforeSummary: "Baseline core rules would block the edge case.",
            AfterSummary: "Seattle Streets approves the current return path for this dossier.",
            ExplainEntryId: "explain-1",
            ProvenanceLabel: "campaign environment",
            EvidenceLines: ["Seattle Streets override is approved for this campaign workspace."],
            SupportReuseHints: ["Support can reuse the current rule-environment receipt when the reporter verifies the fix."]);
        LegacyMigrationReceiptProjection migration = new(
            ReceiptId: "migration-1",
            SourceKind: "legacy_xml",
            SourceId: "legacy-1",
            TargetDossierId: "dossier-1",
            TargetCampaignId: "campaign-1",
            Summary: "Legacy import remained campaign-compatible.",
            Fields:
            [
                new LegacyMigrationFieldProjection("field-1", "contacts", "mapped", "Contacts aligned with the current campaign workspace.")
            ],
            ImportedAtUtc: DateTimeOffset.Parse("2026-03-21T12:00:00+00:00"));
        CreatorPublicationProjection publication = new(
            PublicationId: "publication-1",
            Title: "Neon Nights recap",
            Kind: "recap",
            Summary: "Public recap packet is ready.",
            CampaignId: "campaign-1",
            DossierId: "dossier-1",
            ArtifactId: "artifact-recap",
            ProvenanceSummary: "Campaign provenance is grounded.",
            DiscoverySummary: "Visible to invited players.",
            Visibility: "private",
            PublicationStatus: "ready",
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:08:00+00:00"));
        WorkspaceRestoreProjection restore = new(
            RestoreId: "restore-1",
            UserId: "user-1",
            RecentDossiers: [dossier],
            RecentCampaigns: [campaign],
            RecentRuleEnvironments: [environment],
            RecentArtifacts:
            [
                new RestoreArtifactProjection("artifact-recap", "Run recap", "recap", "Ready to reconnect the latest continuity packet.", Channel: "preview", Version: "0.9.0")
            ],
            Entitlements:
            [
                new RestoreEntitlementProjection("entitlement-1", "Preview desktop", "install", "active", "Desktop preview stays enabled for this campaign return.")
            ],
            ClaimedDevices:
            [
                new ClaimedDeviceRestoreProjection("install-1", "play_tablet", "windows", "avalonia", "preview", "Rigger tablet", "Ready to restore the current campaign workspace.")
            ],
            ConflictSummaries:
            [
                "One cloud-only snapshot is newer than the local cache."
            ],
            LocalOnlyNotes:
            [
                "Keep the GM-only notes on the claimed desktop instead of the travel tablet."
            ],
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-27T12:09:00+00:00"));

        CampaignWorkspaceDigestProjection digest = new(
            WorkspaceId: workspace.WorkspaceId,
            CampaignId: workspace.CampaignId,
            CampaignName: workspace.CampaignName,
            ReturnSummary: "Digest return summary keeps the calmer follow-through lane visible.",
            RuleEnvironmentSummary: "campaign scope · approved · fp:campaign",
            DeviceRoleSummary: "play_tablet on windows/avalonia (preview)",
            SupportClosureSummary: "The calmer digest keeps the fix lane attached to the same claimed device.",
            ActiveSceneSummary: "Scene digest keeps the current run summary visible.",
            NextSafeAction: "Open the calmer workspace digest and continue from the pinned campaign lane.",
            ReadinessHighlights: ["Digest highlight: return packet is current."],
            Watchouts: ["Digest watchout: confirm the claimed device before reopening GM-only notes."],
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T12:10:00+00:00"));

        DesktopHomeCampaignProjection projection = DesktopHomeCampaignProjector.Create(
            new AccountCampaignSummary(
                Dossiers: [dossier],
                Campaigns: [campaign],
                Runs: [run],
                Crews: [crew],
                Workspaces: [workspace],
                CommunityOperations: [],
                BuildLabHandoffs: [handoff],
                RulesNavigator: [rules],
                MigrationReceipts: [migration],
                CreatorPublications: [publication],
                Restore: restore),
            [digest],
            new DesktopHomeCampaignServerPlane(
                WorkspaceId: workspace.WorkspaceId,
                SessionReadinessSummary: "Server plane says the session return is green and the claimed install is aligned.",
                RestoreSummary: "The restore rail stays attached to the claimed install and current continuity packet.",
                PublicationSummary: "Two publication-safe recap packets are ready for the same campaign lane.",
                RosterSummary: "One dossier and one crew are ready to reopen.",
                RunboardSummary: "Runboard keeps the active scene and objective pressure visible from the same shared campaign lane.",
                TravelModeSummary: "Two claimed devices can reopen Neon Nights, but one travel lane still needs a grounded checkpoint before the next safehouse handoff.",
                TravelPrefetchInventorySummary: "2 dossiers, 1 campaign, 1 rule environment, and the recap-safe packet stay bounded to the staged travel cache.",
                CampaignMemorySummary: "The governed memory lane keeps Dockside handoff, the courier objective, and the downtime follow-through attached to the same workspace.",
                CampaignMemoryReturnSummary: "Return through Dockside handoff so the same workspace reopens the courier chase without a lossy recap jump.",
                NextSafeAction: "Server-plane next safe action keeps the follow-through explicit.",
                ReadinessHighlights: ["Server plane highlight: the roster is current."],
                Watchouts: ["Server plane watchout: verify the preview tablet before resuming GM-only notes."],
                SupportHighlights: ["Released: the fix lane stays attached to the same claimed install."],
                DecisionNotices: ["install_role: preview_scout stays attached to windows/avalonia on preview."],
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-27T12:11:00+00:00")));

        RequireContains(projection.Summary, "Digest return summary");
        RequireContains(projection.Summary, "Digest return summary");
        RequireContains(projection.RestoreSummary, "Restore packet:");
        RequireContains(projection.RestoreSummary, "bounded offline use");
        RequireContains(projection.DeviceRoleSummary, "play_tablet");
        RequireContains(projection.SupportClosureSummary, "calmer digest keeps the fix lane");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Digest highlight:");
        RequireContains(string.Join("\n", projection.Watchouts), "Digest watchout:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign return:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Current scene:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Build handoff:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Rules follow-through:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Migration continuity:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Publication trust:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Prefetch inventory:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Claimed device:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Change packet:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Roster transfer:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Travel mode:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Travel inventory:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign memory:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Campaign memory return:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Server plane highlight:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Support lane:");
        RequireContains(string.Join("\n", projection.ReadinessHighlights), "Decision notice:");
        RequireContains(projection.NextSafeAction, "Server-plane next safe action");
        RequireContains(string.Join("\n", projection.Watchouts), "cloud-only snapshot");
        RequireContains(string.Join("\n", projection.Watchouts), "GM-only notes");
        RequireContains(string.Join("\n", projection.Watchouts), "Server plane watchout:");
    }

    private static void DesktopHomeBuildExplainProjector_uses_real_contract_state()
    {
        WorkspaceListItem workspace = new(
            new CharacterWorkspaceId("workspace-1"),
            new CharacterFileSummary(
                Name: "Apex",
                Alias: "Alias",
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "6.0",
                AppVersion: "6.0",
                Karma: 0,
                Nuyen: 0,
                Created: true),
            DateTimeOffset.Parse("2026-03-27T10:15:00+00:00"),
            "sr6.preview.v1",
            HasSavedWorkspace: true);
        CharacterBuildSection build = new(
            BuildMethod: "Priority",
            PriorityMetatype: "B",
            PriorityAttributes: "A",
            PrioritySpecial: "D",
            PrioritySkills: "C",
            PriorityResources: "E",
            PriorityTalent: "Magic",
            SumToTen: 10,
            Special: 4,
            TotalSpecial: 6,
            TotalAttributes: 24,
            ContactPoints: 10,
            ContactPointsUsed: 6);
        CharacterRulesSection rules = new(
            GameEdition: "SR6",
            Settings: "Seattle Nights",
            GameplayOption: "Prime runner preview",
            GameplayOptionQualityLimit: 2,
            MaxNuyen: 450000,
            MaxKarma: 50,
            ContactMultiplier: 3,
            BannedWareGrades: ["Used", "Prototype"]);
        ActiveRuntimeStatusProjection activeRuntime = new(
            ProfileId: "official.sr6.core",
            Title: "Official SR6 Core",
            RulesetId: "sr6",
            RuntimeFingerprint: "sha256:sr6-preview",
            InstallState: ArtifactInstallStates.Installed,
            WarningCount: 1);
        RuntimeInspectorProjection runtimeInspector = new(
            TargetKind: RuntimeInspectorTargetKinds.RuntimeLock,
            TargetId: "official.sr6.core",
            RuntimeLock: new ResolvedRuntimeLock(
                RulesetId: "sr6",
                ContentBundles: [],
                RulePacks: [],
                ProviderBindings: new Dictionary<string, string>(StringComparer.Ordinal),
                EngineApiVersion: "1.0",
                RuntimeFingerprint: "sha256:sr6-preview"),
            Install: new ArtifactInstallState(
                State: ArtifactInstallStates.Installed,
                InstalledTargetKind: RuleProfileApplyTargetKinds.GlobalDefaults,
                InstalledTargetId: "desktop"),
            ResolvedRulePacks: [],
            ProviderBindings: [],
            CompatibilityDiagnostics:
            [
                new RuntimeLockCompatibilityDiagnostic(
                    State: RuntimeLockCompatibilityStates.RebindRequired,
                    Message: "runtime.lock.compatibility.install-runtime-drift")
            ],
            Warnings:
            [
                new RuntimeInspectorWarning(
                    Kind: RuntimeInspectorWarningKinds.Migration,
                    Severity: RuntimeInspectorWarningSeverityLevels.Warning,
                    Message: "runtime.inspector.warning.migration.rebind-required")
            ],
            MigrationPreview: [],
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-27T10:20:00+00:00"));
        DesktopBuildPathCandidate[] buildPathCandidates =
        [
            new DesktopBuildPathCandidate(
                new DesktopBuildPathSuggestion(
                    BuildKitId: "edge-runner-starter",
                    Title: "Edge Runner Starter",
                    Targets: ["sr6"],
                    TrustTier: ArtifactTrustTiers.Curated,
                    Visibility: ArtifactVisibilityModes.Public),
                new DesktopBuildPathPreview(
                    State: "ready",
                    RuntimeFingerprint: "sha256:sr6-preview",
                    ChangeSummaries:
                    [
                        "Validate a compatible runtime before you apply this BuildKit: runtime sha256:sr6-preview with no extra rule packs."
                    ],
                    DiagnosticMessages:
                    [
                        "This BuildKit is ready to flow through the workbench and into a compatible runtime receipt."
                    ],
                    RequiresConfirmation: true,
                    RuntimeCompatibilitySummary: "The grounded campaign/profile runtime is already compatible with this build receipt.",
                    CampaignReturnSummary: "The emitted build receipt can return through the selected workspace after review.",
                    SupportClosureSummary: "Support closure can cite the same runtime and build receipt once the handoff lands.")),
            new DesktopBuildPathCandidate(
                new DesktopBuildPathSuggestion(
                    BuildKitId: "street-sam-starter",
                    Title: "Street Sam Starter",
                    Targets: ["sr6"],
                    TrustTier: ArtifactTrustTiers.Curated,
                    Visibility: ArtifactVisibilityModes.Public),
                new DesktopBuildPathPreview(
                    State: "review",
                    RuntimeFingerprint: "sha256:sr6-preview",
                    ChangeSummaries:
                    [
                        "Street Sam Starter keeps the armor-first path visible for the same dossier."
                    ],
                    DiagnosticMessages:
                    [
                        "Street Sam Starter still needs manual review before it lands."
                    ],
                    RequiresConfirmation: false,
                    RuntimeCompatibilitySummary: "Runtime review is still required before the fallback handoff is campaign-safe."))
        ];

        BuildLabHandoffProjection handoff = new(
            HandoffId: "handoff-1",
            DossierId: "dossier-1",
            CampaignId: "campaign-1",
            Title: "Prime runner handoff",
            Summary: "The next grounded handoff keeps build, runtime, and campaign return aligned.",
            VariantLabel: "Prime runner preview",
            ProgressionLabel: "Street launch",
            ExplainEntryId: "explain-1",
            TradeoffLines: ["Trade a late armor bump for cleaner campaign re-entry."],
            ProgressionOutcomes: ["The runner stays campaign-ready after the handoff."],
            Outputs: [],
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T10:21:00+00:00"),
            NextSafeAction: "Review the grounded handoff before you publish or return to campaign play.",
            RuntimeCompatibilitySummary: "The campaign handoff still matches the current runtime fingerprint.",
            CampaignReturnSummary: "The handoff can return through the current campaign workspace once confirmed.",
            SupportClosureSummary: "Support can cite the same handoff if verification fails.",
            Watchouts: ["The handoff still needs an explicit campaign confirmation click."]);
        RulesNavigatorAnswerProjection rulesAnswer = new(
            EntryId: "rules-1",
            Question: "Can this runner re-enter the campaign under the current rule environment?",
            ShortAnswer: "Yes, after the runtime rebind and handoff confirmation.",
            BeforeSummary: "The grounded dossier still needs one compatibility review.",
            AfterSummary: "The current rule environment stays valid after the handoff is confirmed.",
            ExplainEntryId: "rules-explain-1",
            ProvenanceLabel: "Campaign spine",
            EvidenceLines: ["The runtime fingerprint already matches the campaign workspace."],
            SupportReuseHints: ["Support can reuse the same rules answer after the runner returns."]);
        LegacyMigrationReceiptProjection migration = new(
            ReceiptId: "migration-1",
            SourceKind: "legacy",
            SourceId: "legacy-dossier-1",
            TargetDossierId: "dossier-1",
            TargetCampaignId: "campaign-1",
            Summary: "Legacy migration already mapped the dossier into the preview campaign lane.",
            Fields:
            [
                new LegacyMigrationFieldProjection("field-1", "Legacy contacts", "mapped", "The contact mapping already matches the preview runtime.")
            ],
            ImportedAtUtc: DateTimeOffset.Parse("2026-03-27T10:18:00+00:00"));
        CreatorPublicationProjection publication = new(
            PublicationId: "publication-1",
            Title: "Prime runner dossier",
            Kind: "dossier",
            Summary: "The creator lane is ready to emit the next trusted dossier projection.",
            CampaignId: "campaign-1",
            DossierId: "dossier-1",
            ArtifactId: "artifact-1",
            DiscoverySummary: "The publication stays private until the grounded handoff lands.",
            ProvenanceSummary: "Publication lineage already points at the same campaign-safe dossier.",
            Visibility: "private",
            PublicationStatus: "ready",
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-27T10:22:00+00:00"));
        AccountCampaignSummary campaignSummary = new(
            Dossiers: [],
            Campaigns: [],
            Runs: [],
            Crews: [],
            Workspaces: [],
            CommunityOperations: [],
            BuildLabHandoffs: [handoff],
            RulesNavigator: [rulesAnswer],
            MigrationReceipts: [migration],
            CreatorPublications: [publication],
            Restore: new WorkspaceRestoreProjection(
                RestoreId: "restore-1",
                UserId: "user-1",
                RecentDossiers: [],
                RecentCampaigns: [],
                RecentRuleEnvironments: [],
                RecentArtifacts: [],
                Entitlements: [],
                ClaimedDevices: [],
                ConflictSummaries: [],
                LocalOnlyNotes: [],
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-27T10:23:00+00:00")));

        DesktopHomeBuildExplainProjection projection = DesktopHomeBuildExplainProjector.Create([workspace], build, rules, campaignSummary, activeRuntime, runtimeInspector, buildPathCandidates);
        RequireContains(projection.NextSafeAction, "rebind the active profile");
        RequireContains(projection.ExplainFocus, "Explain focus:");
        RequireContains(projection.ExplainFocus, "Build path focus: Edge Runner Starter");
        RequireContains(projection.ExplainFocus, "Campaign handoff:");
        RequireContains(projection.RuntimeHealthSummary, "Official SR6 Core");
        RequireContains(projection.RuntimeHealthSummary, "runtime drift requires a rebind");
        RequireContains(projection.ReturnTarget, "Apex");
        RequireContains(projection.RulePosture, "fingerprint sha256:sr6-preview");
        if (projection.CompatibilityReceipts.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should surface explicit compatibility receipts for the flagship home cockpit.");
        }
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Compatibility receipt:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "profile rebind");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path receipt: Edge Runner Starter is ready");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path runtime:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path return:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path support:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab handoff:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab tradeoff:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build Lab progression:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Rules navigator:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Migration receipt:");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Publication receipt:");
        if (projection.BuildPathComparisons.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should compare multiple grounded build paths in the flagship home cockpit.");
        }
        RequireContains(string.Join("\n", projection.BuildPathComparisons), "Build path compare: Edge Runner Starter");
        RequireContains(string.Join("\n", projection.BuildPathComparisons), "Build path compare: Street Sam Starter");
        RequireContains(projection.Summary, "Metatype B");
        RequireContains(projection.Summary, "SR6");
        RequireContains(projection.Summary, "Used, Prototype");
        if (projection.Watchouts.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should surface multiple watchouts for the flagship home cockpit.");
        }
        RequireContains(string.Join("\n", projection.Watchouts), "campaign confirmation click");
    }

    private static void DesktopHomeBuildExplainProjector_exposes_safe_action_and_watchouts_when_workspace_is_missing()
    {
        DesktopHomeBuildExplainProjection projection = DesktopHomeBuildExplainProjector.Create(
            [],
            build: null,
            rules: null,
            buildPathCandidates:
            [
                new DesktopBuildPathCandidate(
                    new DesktopBuildPathSuggestion(
                        BuildKitId: "street-sam-starter",
                        Title: "Street Sam Starter",
                        Targets: ["sr5"],
                        TrustTier: ArtifactTrustTiers.Curated,
                        Visibility: ArtifactVisibilityModes.Public),
                    Preview: null)
            ]);
        RequireContains(projection.NextSafeAction, "Create or import the first dossier");
        RequireContains(projection.ExplainFocus, "Claim the install");
        RequireContains(projection.RuntimeHealthSummary, "no active runtime profile");
        RequireContains(projection.ReturnTarget, "No workspace return target");
        RequireContains(projection.RulePosture, "Rule posture is still generic");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "no grounded runtime fingerprint");
        RequireContains(string.Join("\n", projection.CompatibilityReceipts), "Build path receipt: Street Sam Starter is available");
        RequireContains(string.Join("\n", projection.BuildPathComparisons), "Build path compare: Street Sam Starter");
        if (projection.Watchouts.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should keep explicit watchouts even before the first workspace exists.");
        }
    }

    private static void DesktopHomeSupportProjector_uses_real_support_case_truth()
    {
        DesktopHomeSupportProjection projection = DesktopHomeSupportProjector.Create(
        [
            new DesktopHomeSupportDigest(
                CaseId: "case-123",
                Title: "Preview update did not carry the fix",
                Summary: "The tracked case is attached to the linked install and still needs one final reporter-side confirmation step.",
                StatusLabel: "Released",
                StageLabel: "Released",
                NextSafeAction: "Open downloads or update this linked install to pick up the reporter-ready fix.",
                ClosureSummary: "The fix reached preview 0.6.3-smoke.",
                VerificationSummary: "After you update on the affected install, confirm whether the fix worked here.",
                DetailHref: "/account/support/case-123",
                PrimaryActionLabel: "Open downloads",
                PrimaryActionHref: "/downloads",
                UpdatedLabel: "2026-03-28 16:05 UTC",
                FixedReleaseLabel: "preview 0.6.3-smoke",
                AffectedInstallSummary: "This case stays attached to the linked avalonia · linux x64 · preview 0.6.2-smoke install (install-smoke-001).",
                FollowUpLaneSummary: "Follow-up stays inside Account > Support for this signed-in report.",
                ReleaseProgressSummary: "The fix reached preview 0.6.3-smoke. Update or reinstall on the affected device to pick it up.",
                ReporterActionNeeded: false,
                CanVerifyFix: true)
        ],
        installClaimed: true);

        RequireContains(projection.Summary, "Tracked case:");
        RequireContains(projection.Summary, "Preview update did not carry the fix");
        RequireContains(projection.NextSafeAction, "Open downloads");
        RequireContains(string.Join("\n", projection.Highlights), "Stage: Released");
        RequireContains(string.Join("\n", projection.Highlights), "Release progress:");
        RequireContains(string.Join("\n", projection.Highlights), "Verification:");
        RequireContains(string.Join("\n", projection.Highlights), "Fixed release: preview 0.6.3-smoke");
        RequireContains(string.Join("\n", projection.Highlights), "Affected install:");
        RequireContains(string.Join("\n", projection.Highlights), "Follow-up:");
        if (!projection.NeedsAttention || !projection.HasTrackedCase)
        {
            throw new InvalidOperationException("Desktop support projection should mark reporter-verification follow-through as attention-worthy when a tracked fix is ready.");
        }
    }

    private static void DesktopHome_wires_the_campaign_projection_into_the_summary_panel()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(source, "ReadCampaignProjectionAsync");
        RequireContains(source, "BuildCampaignBody()");
        RequireContains(source, "_campaignProjection.NextSafeAction");
        RequireContains(source, "_campaignProjection.Summary");
        RequireContains(source, "_campaignProjection.RestoreSummary");
        RequireContains(source, "_campaignProjection.DeviceRoleSummary");
        RequireContains(source, "_campaignProjection.SupportClosureSummary");
        RequireContains(source, "_campaignProjection.ReadinessHighlights");
        RequireContains(source, "_campaignProjection.Watchouts");
        RequireContains(source, "CreateCampaignActions()");
        RequireContains(source, "desktop.home.section.campaign_return");
        RequireContains(source, "Open current campaign workspace");
        RequireContains(source, "client.GetAccountCampaignSummaryAsync");
        RequireContains(source, "client.GetCampaignWorkspaceDigestsAsync");
        RequireContains(source, "ReadCampaignWorkspaceDigestsAsync");
        RequireContains(source, "ReadCampaignWorkspaceServerPlaneAsync");
        RequireContains(source, "GetCampaignWorkspaceServerPlaneAsync");

        string projectorSource = ReadSource("Chummer.Presentation/Overview/DesktopHomeCampaignProjector.cs");
        RequireContains(projectorSource, "Campaign return:");
        RequireContains(projectorSource, "Support closure:");
        RequireContains(projectorSource, "Claimed device posture:");
        RequireContains(projectorSource, "Migration continuity:");
        RequireContains(projectorSource, "Publication trust:");
        RequireContains(projectorSource, "CampaignWorkspaceDigestProjection");
        RequireContains(projectorSource, "Support lane:");
        RequireContains(projectorSource, "Decision notice:");
        RequireContains(projectorSource, "Travel mode:");
        RequireContains(projectorSource, "Travel inventory:");
        RequireContains(projectorSource, "Campaign memory:");
        RequireContains(projectorSource, "Campaign memory return:");
        RequireContains(projectorSource, "DesktopHomeCampaignServerPlane");

        string serverPlaneSource = ReadSource("Chummer.Presentation/Overview/DesktopHomeCampaignServerPlane.cs");
        RequireContains(serverPlaneSource, "TravelModeSummary");
        RequireContains(serverPlaneSource, "TravelPrefetchInventorySummary");
        RequireContains(serverPlaneSource, "CampaignMemorySummary");
        RequireContains(serverPlaneSource, "CampaignMemoryReturnSummary");
    }

    private static void DesktopHome_wires_the_support_projection_into_the_summary_panel()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(source, "ReadSupportProjectionAsync");
        RequireContains(source, "BuildSupportBody()");
        RequireContains(source, "_supportProjection.NextSafeAction");
        RequireContains(source, "_supportProjection.Summary");
        RequireContains(source, "_supportProjection.Highlights");
        RequireContains(source, "CreateSupportActions()");
        RequireContains(source, "desktop.home.section.support_closure");
        RequireContains(source, "OpenPrimarySupportFollowThrough");
        RequireContains(source, "OpenTrackedSupportCase");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal");
        RequireContains(source, "client.GetDesktopHomeSupportDigestsAsync");

        string projectorSource = ReadSource("Chummer.Presentation/Overview/DesktopHomeSupportProjector.cs");
        RequireContains(projectorSource, "Tracked case:");
        RequireContains(projectorSource, "Release progress:");
        RequireContains(projectorSource, "Verification:");
        RequireContains(projectorSource, "Affected install:");
    }

    private static void DesktopHome_wires_the_build_and_explain_projection_into_the_summary_panel()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(source, "ReadBuildExplainProjectionAsync");
        RequireContains(source, "BuildBuildExplainBody()");
        RequireContains(source, "_buildExplainProjection.NextSafeAction");
        RequireContains(source, "_buildExplainProjection.ExplainFocus");
        RequireContains(source, "_buildExplainProjection.RuntimeHealthSummary");
        RequireContains(source, "_buildExplainProjection.ReturnTarget");
        RequireContains(source, "_buildExplainProjection.RulePosture");
        RequireContains(source, "_buildExplainProjection.CompatibilityReceipts");
        RequireContains(source, "_buildExplainProjection.BuildPathComparisons");
        RequireContains(source, "_buildExplainProjection.Watchouts");
        RequireContains(source, "CreateBuildExplainActions()");
        RequireContains(source, "CreateWorkspaceActions()");
        RequireContains(source, "desktop.home.section.build_explain");
        RequireContains(source, "desktop.home.section.recent_workspaces");
        RequireContains(source, "Open current workspace");
        RequireContains(source, "CreateNextSafeActionButtonLabel(_campaignProjection.NextSafeAction, \"Open campaign follow-through\")");
        RequireContains(source, "CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, \"Open build follow-through\")");
        RequireContains(source, "CreateNextSafeActionButtonLabel(_buildExplainProjection.NextSafeAction, \"Open workspace follow-through\")");
        RequireContains(source, "Open work support");
        RequireContains(source, "Next: ");
        RequireContains(source, "_buildExplainText");
        RequireContains(source, "_workspaceSummaryText");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenWorkPortal()");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(_recentWorkspaces[0].Id.Value)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace");
        RequireContains(source, "client.GetShellBootstrapAsync");
        RequireContains(source, "client.GetRuntimeInspectorProfileAsync");
        RequireContains(source, "client.GetBuildPathSuggestionsAsync");
        RequireContains(source, "ReadBuildPathCandidatesAsync");
        RequireContains(source, "client.GetBuildPathPreviewAsync");
        RequireContains(source, "client.GetBuildAsync");
        RequireContains(source, "client.GetRulesAsync");
        RequireContains(source, "ReadCampaignSummaryAsync");

        string projectorSource = ReadSource("Chummer.Presentation/Overview/DesktopHomeBuildExplainProjector.cs");
        RequireContains(projectorSource, "Compatibility receipt:");
        RequireContains(projectorSource, "Build path receipt:");
        RequireContains(projectorSource, "Build path runtime:");
        RequireContains(projectorSource, "Build path return:");
        RequireContains(projectorSource, "Build path support:");
        RequireContains(projectorSource, "Build path compare:");
        RequireContains(projectorSource, "Build Lab handoff:");
        RequireContains(projectorSource, "Build Lab tradeoff:");
        RequireContains(projectorSource, "Build Lab progression:");
        RequireContains(projectorSource, "Rules navigator:");
        RequireContains(projectorSource, "Migration receipt:");
        RequireContains(projectorSource, "Publication receipt:");
        RequireDoesNotContain(source, "Open work follow-through");
    }

    private static void DesktopHome_exposes_claim_aware_install_and_update_actions()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(source, "CreateInstallActions()");
        RequireContains(source, "CreateUpdateActions()");
        RequireContains(source, "Link this copy");
        RequireContains(source, "Open devices and access");
        RequireContains(source, "Open current workspace");
        RequireContains(source, "Open install support");
        RequireContains(source, "Open update support");
        RequireContains(source, "DesktopInstallLinkingWindow dialog = new(context);");
        RequireContains(source, "RefreshHomeStateAsync()");
        RequireContains(source, "Last claim attempt:");
        RequireContains(source, "Manifest published:");
        RequireContains(source, "Release posture:");
        RequireContains(source, "Supportability:");
        RequireContains(source, "Local release proof:");
        RequireContains(source, "Fix availability:");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate");
    }

    private static void DesktopInstallLinkingWindow_exposes_trust_actions_and_locale_guidance()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopInstallLinkingWindow.cs");
        RequireContains(source, "desktop.install_link.shipping_locales");
        RequireContains(source, "desktop.install_link.button.open_downloads");
        RequireContains(source, "desktop.install_link.button.open_support");
        RequireContains(source, "desktop.install_link.button.open_work");
        RequireContains(source, "desktop.install_link.title");
        RequireContains(source, "GetRequiredString");
        RequireContains(source, "Last claim attempt:");
        RequireContains(source, "Hub message:");
        RequireContains(source, "Claim error:");
        RequireContains(source, "Next safe action:");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenDownloadsPortal()");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_state)");
        RequireContains(source, "DesktopInstallLinkingRuntime.TryOpenWorkPortal()");
    }

    private static void BlazorHome_uses_local_chummer6_flagship_media_samples()
    {
        string source = ReadSource("Chummer.Blazor/Components/Pages/Home.razor");
        RequireContains(source, "/media/chummer6/chummer6-hero.png");
        RequireContains(source, "/media/chummer6/karma-forge.png");
        RequireContains(source, "/media/chummer6/horizons-index.png");
        RequireContains(source, "flagship_poster");
        RequireContains(source, "medscan_diagnostic");
        RequireContains(source, "forge_review_ar");
    }

    private static void BlazorCampaignSpineShowcase_uses_customer_facing_build_path_copy()
    {
        string homeSource = ReadSource("Chummer.Blazor/Components/Pages/Home.razor");
        RequireContains(homeSource, "Social Operator build path");
        RequireContains(homeSource, "build-path handoff");

        string panelSource = ReadSource("Chummer.Blazor/Components/Shared/BuildLabHandoffPanel.razor");
        RequireContains(panelSource, "Title: \"Build path\"");
        RequireContains(panelSource, "chosen build path");
    }

    private static void DesktopHead_uses_canonical_catalog_only_resolver()
    {
        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "CatalogOnlyRulesetShellCatalogResolver");

        string? repoRoot = FindRepoRoot();
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new InvalidOperationException("Could not locate the repository root for desktop runtime checks.");
        }

        string duplicateResolverPath = Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopFallbackRulesetShellCatalogResolver.cs");
        if (File.Exists(duplicateResolverPath))
        {
            throw new InvalidOperationException("Desktop runtime should not keep a duplicate fallback ruleset shell resolver.");
        }
    }

    private static string ReadSource(string relativePath)
    {
        string? cursor = FindRepoRoot();
        while (!string.IsNullOrWhiteSpace(cursor))
        {
            string candidate = Path.Combine(cursor, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            DirectoryInfo? parent = Directory.GetParent(cursor);
            cursor = parent?.FullName;
        }

        throw new FileNotFoundException($"Could not locate required source file: {relativePath}");
    }

    private static string? FindRepoRoot()
    {
        string?[] startingPoints =
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            AppDomain.CurrentDomain.BaseDirectory
        };

        foreach (string? startingPoint in startingPoints)
        {
            string? cursor = startingPoint;
            while (!string.IsNullOrWhiteSpace(cursor))
            {
                bool hasPresentationProject = File.Exists(Path.Combine(cursor, "Chummer.Presentation", "Chummer.Presentation.csproj"));
                bool hasBlazorShell = File.Exists(Path.Combine(cursor, "Chummer.Blazor", "Components", "Shell", "SectionPane.razor"));
                if (hasPresentationProject && hasBlazorShell)
                {
                    return cursor;
                }

                DirectoryInfo? parent = Directory.GetParent(cursor);
                cursor = parent?.FullName;
            }
        }

        return null;
    }

    private static void RequireContains(string source, string expected)
    {
        if (!source.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected to find '{expected}' in smoke target source.");
        }
    }

    private static void RequireDoesNotContain(string source, string unexpected)
    {
        if (source.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected not to find '{unexpected}' in smoke target source.");
        }
    }
}
